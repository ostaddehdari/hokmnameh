using Hokmnameh.Core;
using Hokmnameh.Api;
using System.Text.Json;
using System.Text;
using System.Net.WebSockets;

void Assert(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS: " + name); }
void AssertRuleError(Action action, string name)
{
    try { action(); throw new Exception("FAILED: " + name + " (no error)"); }
    catch (GameRuleException) { Console.WriteLine("PASS: " + name); }
}
var game = new HokmGame();
game.StartRound(0);
Assert(game.Phase == GamePhase.ChoosingTrump, "20-card preliminary deal");
Assert(game.ShuffleCommitment.Length == 64 && game.ShuffleAlgorithmId == E04Shuffle.AlgorithmId,
    "E04 commitment and version established before first deal");
Assert(game.CutIndex >= 1 && game.CutIndex <= 51, "E04 fallback cut range");
Assert(game.DealCursors.SequenceEqual(new[] { 20 }), "E04 preliminary cursor 20");
Assert(game.Snapshot(0).shuffleCommitment == game.ShuffleCommitment, "commitment visible after preliminary deal");
for (int i = 0; i < 4; ++i) Assert(game.Snapshot(i).hand.Length == 5, "Five cards seat=" + i);
int suit = HokmBot.PickTrump(game, 0);
game.ChooseTrump(0, suit);
Assert(game.DealCursors.SequenceEqual(new[] { 20, 36, 52 }), "E04 staged cursors 20/36/52");
var seen = new HashSet<int>();
for (int i = 0; i < 4; ++i)
{
    var hand = game.Snapshot(i).hand;
    Assert(hand.Length == 13, "13 cards seat=" + i);
    foreach (var card in hand) Assert(seen.Add(card), "unique card " + card);
}
Assert(seen.Count == 52, "all cards distributed exactly once");
var partial = game.Snapshot(0);
Assert(partial.hand.Length == 13 && partial.handCounts.Length == 4, "private snapshot");
// Exercise full game to the first scored round and validate every turn transition.
for (int moves = 0; moves < 52 && game.Phase == GamePhase.Playing; ++moves)
{
    int player = game.Turn;
    int picked = HokmBot.ChooseCard(game, player, 1);
    game.Play(player, picked);
}
Assert(game.Phase == GamePhase.Waiting || game.Phase == GamePhase.Finished, "round terminates");
Assert(game.Score[0] + game.Score[1] >= 1, "points scored");
Assert(HokmGame.DetermineTrickWinner(new[] { new CardPlayed(0,0),new CardPlayed(1,12),new CardPlayed(2,13),new CardPlayed(3,26) },3) == 1, "lead high ace wins when no trump played");
Assert(HokmGame.DetermineTrickWinner(new[] { new CardPlayed(0,12),new CardPlayed(1,13),new CardPlayed(2,11),new CardPlayed(3,26) },1) == 1, "golden cut: low trump beats lead ace");
Assert(ClassicRules.ImmutableId == "iranian-hokm-classic-v1", "immutable ruleset ID");
Assert(ClassicRules.RoundPoints(0, 0, 2) == 1, "normal round scores one");
Assert(ClassicRules.RoundPoints(0, 0, 0) == 2, "hakem team sweep scores two");
Assert(ClassicRules.RoundPoints(1, 0, 0) == 3, "non-hakem sweep scores three");
Assert(ClassicRules.NextHakem(0, 0) == 0, "winning hakem stays");
Assert(ClassicRules.NextHakem(0, 1) == 1, "losing hakem rotates clockwise");
Assert(ClassicRules.NextHakem(3, 0) == 0, "hakem rotation wraps seat three to zero");
var legalFollow = ClassicRules.LegalCards(new[] { 0, 12, 13 }, 0);
Assert(legalFollow.SequenceEqual(new[] { 0, 12 }), "golden follow-suit filters off-suit card");
Assert(ClassicRules.LegalCards(new[] { 13, 26 }, 0).SequenceEqual(new[] { 13, 26 }), "golden void suit allows cut or discard");

var trumpGame = new HokmGame();
trumpGame.StartRound(0);
AssertRuleError(() => trumpGame.ChooseTrump(1, 0), "golden trump: non-hakem rejected");
Assert(trumpGame.Phase == GamePhase.ChoosingTrump && trumpGame.Snapshot(0).hand.Length == 5, "golden trump: decision occurs after five cards");
trumpGame.ChooseTrump(0, 2);
Assert(trumpGame.Trump == 2 && trumpGame.Phase == GamePhase.Playing, "golden trump: hakem selection starts play");

var fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../docs/ruleset_hokm_classic_v1.golden.json"));
using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
foreach (var sample in fixture.RootElement.GetProperty("scoring").EnumerateArray())
{
    var actual = ClassicRules.RoundPoints(sample.GetProperty("winningTeam").GetInt32(), sample.GetProperty("hakemTeam").GetInt32(), sample.GetProperty("losingTeamTricks").GetInt32());
    Assert(actual == sample.GetProperty("expectedPoints").GetInt32(), "golden score " + sample.GetProperty("id").GetString());
}
foreach (var sample in fixture.RootElement.GetProperty("hakemRotation").EnumerateArray())
{
    var actual = ClassicRules.NextHakem(sample.GetProperty("currentHakem").GetInt32(), sample.GetProperty("winningTeam").GetInt32());
    Assert(actual == sample.GetProperty("expectedHakem").GetInt32(), "golden hakem " + sample.GetProperty("id").GetString());
}
var selectedVersion = ClassicRules.ImmutableId;
var versionedRoom = new GameRoom(Guid.NewGuid(), "VERSION1", "free", Guid.NewGuid(), 30, selectedVersion);
selectedVersion = "iranian-hokm-classic-v2";
Assert(versionedRoom.RulesetVersion == ClassicRules.ImmutableId, "active match keeps captured ruleset version");
Assert(versionedRoom.View().RulesetVersion == ClassicRules.ImmutableId, "room API records ruleset version");

var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
var owner = Guid.NewGuid();
var fallbackRoom = new GameRoom(Guid.NewGuid(), "TIMEOUT1", "free", owner, 10, ClassicRules.ImmutableId, clock);
fallbackRoom.Join(Guid.NewGuid()); fallbackRoom.Join(Guid.NewGuid()); fallbackRoom.Join(Guid.NewGuid());
fallbackRoom.ChooseTrump(owner, 0);
Assert(!fallbackRoom.Tick(), "golden disconnect: no fallback before deadline");
clock.Advance(TimeSpan.FromSeconds(11));
Assert(fallbackRoom.Tick(), "golden disconnect: legal auto-play after deadline");

var e04Path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../docs/e04_test_vector_v1.json"));
using var e04 = JsonDocument.Parse(File.ReadAllText(e04Path));
var input = e04.RootElement.GetProperty("input");
var expected = e04.RootElement.GetProperty("expected");
var vector = E04Shuffle.Create(Guid.Parse(input.GetProperty("matchId").GetString()!),
    input.GetProperty("roundNumber").GetUInt64(), input.GetProperty("rulesetId").GetString()!,
    Convert.FromHexString(input.GetProperty("seedHex").GetString()!),
    Convert.FromHexString(input.GetProperty("nonceHex").GetString()!),
    input.GetProperty("cutIndex").GetInt32());
Assert(vector.CommitmentHex == expected.GetProperty("commitmentHex").GetString(), "E04 official commitment");
Assert(vector.Deck.SequenceEqual(expected.GetProperty("cutDeck").EnumerateArray().Select(x => x.GetInt32())), "E04 official 52-card cut deck");
Assert(vector.Deck.Distinct().Count() == 52 && vector.Deck.Min() == 0 && vector.Deck.Max() == 51, "E04 permutation invariant");
var changedSeed = Convert.FromHexString(input.GetProperty("seedHex").GetString()!);
changedSeed[0] ^= 1;
var different = E04Shuffle.Create(Guid.Parse(input.GetProperty("matchId").GetString()!), 7,
    ClassicRules.ImmutableId, changedSeed, Convert.FromHexString(input.GetProperty("nonceHex").GetString()!), 17);
Assert(different.CommitmentHex != vector.CommitmentHex && !different.Deck.SequenceEqual(vector.Deck), "E04 seed sensitivity");
for (int cut = 1; cut <= 51; ++cut)
{
    var result = E04Shuffle.Create(Guid.Parse(input.GetProperty("matchId").GetString()!), 7,
        ClassicRules.ImmutableId, changedSeed, Convert.FromHexString(input.GetProperty("nonceHex").GetString()!), cut);
    Assert(result.Deck.Distinct().Count() == 52, "cut permutation " + cut);
}
try { _ = new HokmGame(Guid.Empty); throw new Exception("FAILED: missing match identity rejected"); }
catch (ArgumentException) { Console.WriteLine("PASS: missing match identity rejected"); }
using (var audit = new MatchAudit())
{
    var action = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
    audit.Append(new CommandV1(1, action, -1, ClassicRules.ImmutableId, now, 0, "round-started"));
    Assert(audit.Events[0].SchemaVersion == 1 && audit.Events[0].Sequence == 1 &&
        audit.Events[0].StateVersion == 1 && audit.Events[0].ActionId == action,
        "versioned event preserves required command metadata");
    AssertRuleError(() => audit.Append(new CommandV1(2, action, 0, ClassicRules.ImmutableId, now, 1, "duplicate")), "duplicate action rejected");
    AssertRuleError(() => audit.Append(new CommandV1(3, Guid.NewGuid(), 0, ClassicRules.ImmutableId, now, 1, "gap")), "sequence gap rejected");
    var signed = audit.Sign(Guid.NewGuid(), 0, trumpGame.Snapshot(0));
    Assert(audit.Verify(signed), "private seat snapshot signature valid");
    var other = trumpGame.Snapshot(1);
    Assert(!audit.Verify(signed with { Snapshot = other }), "other seat snapshot signature rejected");
    Assert(!audit.Verify(signed with { StateVersion = 2 }), "snapshot version tamper rejected");
    signed.Snapshot.hand[0] = other.hand[0];
    Assert(!audit.Verify(signed), "snapshot card tamper rejected");
    var seatJson = JsonSerializer.Serialize(signed, JsonOptions.Default);
    var otherPrivateCard = other.hand.First(x => !signed.Snapshot.hand.Contains(x));
    Assert(!signed.Snapshot.hand.Contains(otherPrivateCard) &&
        !seatJson.Contains("\"seed\"", StringComparison.OrdinalIgnoreCase) &&
        !seatJson.Contains("\"hands\"", StringComparison.OrdinalIgnoreCase),
        "seat snapshot excludes RNG and all-hands fields");
    Assert(!JsonSerializer.Serialize(audit.Events, JsonOptions.Default).Contains("hand", StringComparison.OrdinalIgnoreCase),
        "public event audit excludes private hands");
}

var players = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
var privateRoom = new GameRoom(Guid.NewGuid(), "PRIVATE", "free", players[0], 30, ClassicRules.ImmutableId);
for (int i = 1; i < 4; ++i) privateRoom.Join(players[i]);
AssertRuleError(() => privateRoom.State(Guid.NewGuid()), "non-member HTTP state rejected");
using var unauthorizedSocket = new CapturingSocket();
AssertRuleError(() => privateRoom.RegisterSocket(Guid.NewGuid(), unauthorizedSocket, Guid.NewGuid()),
    "non-member WebSocket registration rejected");
var sockets = new CapturingSocket[4];
var seatHands = new List<int[]>();
for (int seat = 0; seat < 4; ++seat)
{
    using var stateJson = JsonDocument.Parse(JsonSerializer.Serialize(privateRoom.State(players[seat]), JsonOptions.Default));
    var gameJson = stateJson.RootElement.GetProperty("game");
    Assert(gameJson.GetProperty("yourSeat").GetInt32() == seat, "HTTP state owner seat=" + seat);
    Assert(!stateJson.RootElement.TryGetProperty("seed", out _) &&
        !gameJson.TryGetProperty("hands", out _), "HTTP response excludes secrets seat=" + seat);
    var owned = gameJson.GetProperty("hand").EnumerateArray().Select(x => x.GetInt32()).ToArray();
    Assert(owned.Length == 5, "preliminary HTTP hand has five seat=" + seat);
    seatHands.Add(owned);
    sockets[seat] = new CapturingSocket();
    privateRoom.RegisterSocket(Guid.NewGuid(), sockets[seat], players[seat]);
}
Assert(seatHands.SelectMany(x => x).Distinct().Count() == 20, "HTTP preliminary hands disjoint");
await privateRoom.BroadcastAsync();
for (int seat = 0; seat < 4; ++seat)
{
    Assert(sockets[seat].Messages.Count == 1, "WebSocket sent one state seat=" + seat);
    using var wsJson = JsonDocument.Parse(sockets[seat].Messages[0]);
    var state = wsJson.RootElement.GetProperty("state");
    var wsHand = state.GetProperty("hand").EnumerateArray().Select(x => x.GetInt32()).ToArray();
    Assert(wsHand.SequenceEqual(seatHands[seat]) && state.GetProperty("yourSeat").GetInt32() == seat,
        "WebSocket contains only seat hand=" + seat);
    Assert(!state.TryGetProperty("hands", out _) &&
        !wsJson.RootElement.TryGetProperty("seed", out _), "WebSocket excludes secret fields seat=" + seat);
    sockets[seat].Dispose();
}
Console.WriteLine("All core smoke tests passed");

sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
}

sealed class CapturingSocket : WebSocket
{
    public List<string> Messages { get; } = new();
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => WebSocketState.Open;
    public override string? SubProtocol => null;
    public override void Abort() { }
    public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken ct) => Task.CompletedTask;
    public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken ct) => Task.CompletedTask;
    public override void Dispose() { }
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct) =>
        Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool end, CancellationToken ct)
    {
        Messages.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
        return Task.CompletedTask;
    }
}
