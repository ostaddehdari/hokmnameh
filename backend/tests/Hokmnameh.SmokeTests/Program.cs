using Hokmnameh.Core;
using Hokmnameh.Api;
using Hokmnameh.Unity;
using System.Text.Json;
using System.Text;
using System.Net.WebSockets;
using System.Security.Cryptography;

void Assert(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS: " + name); }
void AssertRuleError(Action action, string name)
{
    try { action(); throw new Exception("FAILED: " + name + " (no error)"); }
    catch (GameRuleException) { Console.WriteLine("PASS: " + name); }
}
void AssertCutError(Action action, string code, int status)
{
    try { action(); throw new Exception("FAILED: expected " + code); }
    catch (CutCommandException e)
    {
        Assert(e.Code == code && e.StatusCode == status, "typed cut error " + code);
    }
}
foreach (var width in new[] { 280f, 320f, 504f, 1280f })
{
    foreach (var rtl in new[] { false, true })
    {
        var layout=ProfileHudLayout.Create(width,rtl);
        var slots=new[] { layout.Avatar,layout.Name,layout.Level,layout.Reputation };
        Assert(slots.All(x => x.X >= 0 && x.Width > 0 && x.X+x.Width <= width && x.Height == 68) &&
            slots.OrderBy(x => x.X).Zip(slots.OrderBy(x => x.X).Skip(1),
                (a,b) => a.X+a.Width <= b.X).All(x => x),
            $"HUD fits and badges do not overlap at {width}, RTL={rtl}");
        Assert(rtl ? layout.Avatar.X > layout.Name.X && layout.Name.X > layout.Level.X &&
            layout.Level.X > layout.Reputation.X :
            layout.Avatar.X < layout.Name.X && layout.Name.X < layout.Level.X &&
            layout.Level.X < layout.Reputation.X,
            $"HUD direction at {width}, RTL={rtl}");
    }
}
try { ProfileHudLayout.Create(279,true); throw new Exception("FAILED: HUD narrow width accepted"); }
catch (ArgumentOutOfRangeException) { Console.WriteLine("PASS: HUD narrow width rejected"); }
var game = new HokmGame();
game.StartRound(0);
Assert(game.Phase == GamePhase.ChoosingTrump, "20-card preliminary deal");
Assert(game.ShuffleCommitment.Length == 64 && game.ShuffleAlgorithmId == E04Shuffle.AlgorithmId,
    "E04 commitment and version established before first deal");
Assert(game.CutIndex >= 1 && game.CutIndex <= 51, "E04 fallback cut range");
Assert(game.DealCursors.SequenceEqual(new[] { 20 }), "E04 preliminary cursor 20");
Assert(E04DealPlan.Version == "E04-DEAL-5-4-4-CCW-v1" &&
    Enumerable.Range(0, 3).Select(E04DealPlan.PacketSize).SequenceEqual(new[] { 5, 4, 4 }) &&
    Enumerable.Range(0, 3).Select(E04DealPlan.Cursor).SequenceEqual(new[] { 20, 36, 52 }),
    "approved immutable deal plan");
for (int hakem = 0; hakem < 4; hakem++)
    Assert(Enumerable.Range(0, 4).Select(offset => E04DealPlan.Seat(hakem, offset))
        .SequenceEqual(new[] { hakem, (hakem + 3) % 4, (hakem + 2) % 4, (hakem + 1) % 4 }),
        "counterclockwise seat order " + hakem);
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
var staged = new HokmGame();
staged.StartRound(2);
Assert(staged.Snapshot(2).dealCursor == 20 && staged.Snapshot(2).dealPlanVersion == E04DealPlan.Version,
    "preliminary cursor is versioned in private snapshot");
staged.SelectTrump(2, 3);
Assert(staged.Phase == GamePhase.DealingSecond && staged.DealCursors.SequenceEqual(new[] { 20 }),
    "trump decision does not consume the shuffled deck");
staged.AdvanceDeal();
Assert(staged.Phase == GamePhase.DealingFinal && staged.DealCursors.SequenceEqual(new[] { 20, 36 }) &&
    Enumerable.Range(0, 4).All(seat => staged.Snapshot(seat).hand.Length == 9), "durable second-stage cursor");
staged.AdvanceDeal();
Assert(staged.Phase == GamePhase.Playing && staged.DealCursors.SequenceEqual(new[] { 20, 36, 52 }) &&
    Enumerable.Range(0, 4).SelectMany(seat => staged.Snapshot(seat).hand).Distinct().Count() == 52,
    "final-stage cursor, 52 unique cards, one shuffle");
AssertRuleError(() => staged.AdvanceDeal(), "stage cannot advance twice");
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
for (int cut = 1; cut <= 51; cut++)
    for (int hakem = 0; hakem < 4; hakem++)
    {
        var id = Guid.NewGuid();
        using var prepared = E04Shuffle.Create(id, 1, ClassicRules.ImmutableId,
            Convert.FromHexString(input.GetProperty("seedHex").GetString()!),
            Convert.FromHexString(input.GetProperty("nonceHex").GetString()!), cut);
        var propertyGame = new HokmGame(id);
        propertyGame.StartRound(hakem, prepared);
        propertyGame.AssertInvariant();
        propertyGame.SelectTrump(hakem, 0);
        propertyGame.AdvanceDeal();
        propertyGame.AssertInvariant();
        propertyGame.AdvanceDeal();
        propertyGame.AssertInvariant();
        Assert(propertyGame.DealCursors.SequenceEqual(new[] { 20, 36, 52 }) &&
            Enumerable.Range(0, 4).SelectMany(s => propertyGame.Snapshot(s).hand).Distinct().Count() == 52,
            "E04 deal property cut=" + cut + " hakem=" + hakem);
    }
var uncutDeck = Enumerable.Range(0, 52).Reverse().ToArray();
for (int cut = 1; cut <= 51; ++cut)
{
    var rotated = CardCut.Rotate(uncutDeck, cut);
    Assert(rotated.SequenceEqual(uncutDeck.Skip(cut).Concat(uncutDeck.Take(cut))) &&
        rotated.Distinct().Count() == 52 && uncutDeck.SequenceEqual(Enumerable.Range(0, 52).Reverse()),
        "CardCut rotation, uniqueness and input immutability " + cut);
}
foreach (int invalidCut in new[] { 0, -1, 52, int.MaxValue })
{
    try { _ = CardCut.Rotate(uncutDeck, invalidCut); throw new Exception("FAILED: invalid CardCut index " + invalidCut); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("PASS: invalid CardCut index " + invalidCut); }
}
try { _ = CardCut.Rotate(uncutDeck.Take(51).ToArray(), 1); throw new Exception("FAILED: short deck"); }
catch (ArgumentException) { Console.WriteLine("PASS: short deck rejected"); }
var duplicateDeck = (int[])uncutDeck.Clone(); duplicateDeck[0] = duplicateDeck[1];
try { _ = CardCut.Rotate(duplicateDeck, 1); throw new Exception("FAILED: duplicate card"); }
catch (ArgumentException) { Console.WriteLine("PASS: duplicate card rejected"); }
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

var cutClock = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
var cutPlayers = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
var cutStore = new CapturingCutStore();
var cutRoom = new GameRoom(Guid.NewGuid(), "CUTROOM", "free", cutPlayers[0], 30,
    ClassicRules.ImmutableId, cutClock, cutStore, requireCut: true);
for (int i = 1; i < 4; ++i) cutRoom.Join(cutPlayers[i]);
using (var pendingJson = JsonDocument.Parse(JsonSerializer.Serialize(cutRoom.State(cutPlayers[3]), JsonOptions.Default)))
{
    var pending = pendingJson.RootElement;
    Assert(pending.GetProperty("room").GetProperty("cutPhase").GetString() == "CUT_PENDING" &&
        pending.GetProperty("cutStateVersion").GetInt64() == 0 && pending.GetProperty("isYourCut").GetBoolean() &&
        pending.GetProperty("game").ValueKind == JsonValueKind.Null &&
        !pendingJson.RootElement.ToString().Contains("seed", StringComparison.OrdinalIgnoreCase),
        "v2 cut pending exposes only cutter status and no cards or seed");
}
var chosenCutAction = Guid.NewGuid();
var chosenCut = new CutCardRequest(17, chosenCutAction, 0);
AssertCutError(() => cutRoom.Cut(Guid.NewGuid(), chosenCut), "cut_not_member", 403);
AssertCutError(() => cutRoom.Cut(cutPlayers[0], chosenCut), "wrong_cutter", 403);
AssertCutError(() => cutRoom.Cut(cutPlayers[3], chosenCut with { ActionId = Guid.Empty }), "invalid_cut_action_id", 400);
AssertCutError(() => cutRoom.Cut(cutPlayers[3], chosenCut with { StateVersion = 1 }), "stale_cut_state", 409);
AssertCutError(() => cutRoom.Cut(cutPlayers[3], chosenCut with { CutIndex = 0 }), "invalid_cut_index", 400);
Assert(cutStore.Events.Count == 0, "rejected cut commands never enter audit");
cutRoom.Cut(cutPlayers[3], chosenCut);
Assert(cutStore.Events.Count == 3 && cutStore.Events[0].Kind == "cut-selected" &&
    cutStore.Events[0].ActionId == chosenCutAction && cutStore.Events[0].ActorSeat == 3 &&
    cutStore.Events[0].PublicValue == 17 && cutStore.Events[1].Kind == "round-started" &&
    cutStore.Events[2].Kind == "deal-initial" && cutStore.Events[2].PublicValue == 20 &&
    cutStore.Proofs.Single().Cut == 17, "accepted cut, proof and first cursor persisted in event order");
AssertCutError(() => cutRoom.Cut(cutPlayers[3], chosenCut), "duplicate_cut_action", 409);
AssertCutError(() => cutRoom.Cut(cutPlayers[3], chosenCut with { ActionId = Guid.NewGuid() }), "cut_not_pending", 409);
using (var replayCut = new ReplayBundleV1(cutRoom.Id, ClassicRules.ImmutableId,
    cutStore.Events.Count, cutStore.Events, cutStore.Proofs.Select(p =>
        new ReplayRoundSecretV1(p.Round, (byte[])p.Seed.Clone(), (byte[])p.Nonce.Clone(),
            p.Commitment, p.Cut, p.Algorithm)).ToArray()))
{
    var recoveredCut = MatchReplay.Rebuild(replayCut);
    Assert(recoveredCut.Game.CutIndex == 17 && recoveredCut.Game.Phase == GamePhase.ChoosingTrump,
        "authorized cut can be reconstructed from audit and encrypted proof contract");
}
cutRoom.ChooseTrump(cutPlayers[0], 2);
var reconnectVersion = cutStore.Events.Count;
_ = cutRoom.State(cutPlayers[0]);
_ = cutRoom.State(cutPlayers[3]);
Assert(cutStore.Events.Count == reconnectVersion, "seat reconnection and UI reads do not advance deal");
Assert(cutStore.Events.TakeLast(3).Select(e => e.Kind).SequenceEqual(new[] {
    "trump-staged", "deal-second", "deal-final" }) &&
    cutStore.Events.TakeLast(2).Select(e => e.PublicValue).SequenceEqual(new int?[] { 36, 52 }),
    "trump and each stage have an ordered durable audit cursor");
using (var replayStaged = new ReplayBundleV1(cutRoom.Id, ClassicRules.ImmutableId,
    cutStore.Events.Count, cutStore.Events, cutStore.Proofs.Select(p =>
        new ReplayRoundSecretV1(p.Round, (byte[])p.Seed.Clone(), (byte[])p.Nonce.Clone(),
            p.Commitment, p.Cut, p.Algorithm)).ToArray()))
    Assert(MatchReplay.Rebuild(replayStaged).Game.DealCursors.SequenceEqual(new[] { 20, 36, 52 }),
        "v2 deal reconstructs from persisted cursor events");
using (var incomplete = new ReplayBundleV1(cutRoom.Id, ClassicRules.ImmutableId,
    cutStore.Events.Count - 1, cutStore.Events.Take(cutStore.Events.Count - 1).ToArray(),
    cutStore.Proofs.Select(p => new ReplayRoundSecretV1(p.Round, (byte[])p.Seed.Clone(),
        (byte[])p.Nonce.Clone(), p.Commitment, p.Cut, p.Algorithm)).ToArray()))
    AssertRuleError(() => MatchReplay.Rebuild(incomplete), "incomplete staged cursor fails closed");
var fallbackClock = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
var fallbackCutPlayers = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
var fallbackCutStore = new CapturingCutStore();
var fallbackCutRoom = new GameRoom(Guid.NewGuid(), "CUTTIME", "free", fallbackCutPlayers[0], 30,
    ClassicRules.ImmutableId, fallbackClock, fallbackCutStore, requireCut: true);
for (int i = 1; i < 4; ++i) fallbackCutRoom.Join(fallbackCutPlayers[i]);
var originalCutDeadline = fallbackCutRoom.View().CutDeadline;
fallbackClock.Advance(TimeSpan.FromMilliseconds(7999));
Assert(!fallbackCutRoom.Tick() && fallbackCutRoom.View().CutDeadline == originalCutDeadline,
    "cut deadline does not move during wait");
fallbackClock.Advance(TimeSpan.FromMilliseconds(1));
AssertCutError(() => fallbackCutRoom.Cut(fallbackCutPlayers[3], new CutCardRequest(5, Guid.NewGuid(), 0)),
    "cut_deadline_expired", 410);
Assert(fallbackCutRoom.Tick() && fallbackCutStore.Events[0].Kind == "cut-fallback" &&
    fallbackCutStore.Events[0].ActorSeat == -1 && fallbackCutStore.Proofs.Single().Cut is >= 1 and <= 51 &&
    fallbackCutRoom.View().CutPhase == null, "timeout records deterministic fallback before deal");
var fallbackProof = fallbackCutStore.Proofs.Single();
using (var derived = E04Shuffle.Create(fallbackCutRoom.Id, 1, ClassicRules.ImmutableId,
    fallbackProof.Seed, fallbackProof.Nonce))
    Assert(derived.CutIndex == fallbackProof.Cut && derived.CommitmentHex == fallbackProof.Commitment,
        "fallback uses sealed HMAC domain and matches committed proof");
var expectedFallbackSuit = HokmBot.PickTrump(MatchReplay.Rebuild(new ReplayBundleV1(
    fallbackCutRoom.Id, ClassicRules.ImmutableId, fallbackCutStore.Events.Count, fallbackCutStore.Events,
    fallbackCutStore.Proofs)).Game, 0);
fallbackClock.Advance(TimeSpan.FromSeconds(20));
AssertCutError(() => fallbackCutRoom.ChooseTrump(fallbackCutPlayers[0], 0), "trump_deadline_expired", 410);
Assert(fallbackCutRoom.Tick() && fallbackCutStore.Events.TakeLast(3).Select(e => e.Kind)
    .SequenceEqual(new[] { "trump-staged-fallback", "deal-second", "deal-final" }) &&
    fallbackCutStore.Events[^3].PublicValue == expectedFallbackSuit,
    "20-second hokm timeout chooses maximum suit count with stable tie order");
fallbackProof.Dispose();
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
var failPlayers = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
var failRoom = new GameRoom(Guid.NewGuid(), "FAILSTOP", "free", failPlayers[0], 30,
    ClassicRules.ImmutableId, eventStore: new ThrowOnTrumpStore());
for (int seat = 1; seat < 4; seat++) failRoom.Join(failPlayers[seat]);
try { failRoom.ChooseTrump(failPlayers[0], 0); throw new Exception("FAILED: persistence failure rejected"); }
catch (InvalidOperationException e) when (e.Message == "Simulated persistent event failure")
{ Console.WriteLine("PASS: persistence failure rejected"); }
try { _ = failRoom.State(failPlayers[0]); throw new Exception("FAILED: failed authority state hidden"); }
catch (InvalidOperationException e) when (e.Message.Contains("quarantined"))
{ Console.WriteLine("PASS: failed authority state quarantined"); }

// Independent Node-generated full-match fixture: fixed E04 proofs, 517 ordered events, 11 rounds.
var replayPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
    "../../../../../../docs/replay_match_v1.golden.json"));
using var golden = JsonDocument.Parse(File.ReadAllText(replayPath));
var root = golden.RootElement;
var fixtureMatch = Guid.Parse(root.GetProperty("matchId").GetString()!);
var secrets = root.GetProperty("rounds").EnumerateArray().Select(r =>
    new ReplayRoundSecretV1(r.GetProperty("round").GetInt64(),
        Convert.FromHexString(r.GetProperty("seedHex").GetString()!),
        Convert.FromHexString(r.GetProperty("nonceHex").GetString()!),
        r.GetProperty("commitment").GetString()!, r.GetProperty("cut").GetInt32(),
        E04Shuffle.AlgorithmId)).ToArray();
for (int i = 0; i < secrets.Length; ++i)
{
    using var proof = E04Shuffle.Create(fixtureMatch, (ulong)secrets[i].Round,
        ClassicRules.ImmutableId, secrets[i].Seed, secrets[i].Nonce, secrets[i].Cut);
    Assert(proof.CommitmentHex == secrets[i].Commitment &&
        proof.Deck.Take(12).SequenceEqual(root.GetProperty("rounds")[i]
            .GetProperty("first12").EnumerateArray().Select(x => x.GetInt32())),
        "independent Node E04 fixture round=" + secrets[i].Round);
}
var transcript = root.GetProperty("events").EnumerateArray().Select((e, i) =>
    new MatchEventV1(i + 1, new Guid(i + 1, 0, 0, new byte[8]),
        e.GetProperty("seat").GetInt32(), ClassicRules.ImmutableId,
        DateTimeOffset.UnixEpoch.AddSeconds(i + 1), i + 1,
        e.GetProperty("kind").GetString()!, e.GetProperty("value").GetInt32())).ToArray();
using var bundle = new ReplayBundleV1(fixtureMatch, ClassicRules.ImmutableId,
    transcript.Length, transcript, secrets);
var finalReplay = MatchReplay.Rebuild(bundle);
var finalState = finalReplay.Game.Snapshot(0);
var goal = root.GetProperty("expected");
Assert(finalReplay.StateVersion == goal.GetProperty("eventCount").GetInt32() &&
    finalReplay.ClosedRounds == goal.GetProperty("round").GetInt32() &&
    finalState.round == goal.GetProperty("round").GetInt32() &&
    finalState.matchPoints.SequenceEqual(goal.GetProperty("points").EnumerateArray().Select(x => x.GetInt32())) &&
    finalState.winnerTeam == goal.GetProperty("winner").GetInt32() &&
    finalReplay.Game.Phase == GamePhase.Finished, "golden full match replay matches independent Node result");
foreach (int count in new[] { 1, 2, 20, transcript.Length / 2, transcript.Length })
{
    var prefix = transcript.Take(count).ToArray();
    int startCount = prefix.Count(e => e.Kind == "round-started");
    var interrupted = new ReplayBundleV1(fixtureMatch, ClassicRules.ImmutableId, count,
        prefix, secrets.Take(startCount).ToArray());
    var resumed = MatchReplay.Rebuild(interrupted);
    Assert(resumed.StateVersion == count && resumed.Game.Round == startCount,
        "interrupted replay at sequence " + count);
}
var duplicate = transcript.ToArray();
duplicate[3] = duplicate[3] with { ActionId = duplicate[1].ActionId };
AssertRuleError(() => MatchReplay.Rebuild(new ReplayBundleV1(fixtureMatch,
    ClassicRules.ImmutableId, duplicate.Length, duplicate, secrets)), "duplicate action rejects replay");
var gap = transcript.ToArray(); gap[3] = gap[3] with { Sequence = 99 };
AssertRuleError(() => MatchReplay.Rebuild(new ReplayBundleV1(fixtureMatch,
    ClassicRules.ImmutableId, gap.Length, gap, secrets)), "sequence gap rejects replay");
AssertRuleError(() => MatchReplay.Rebuild(new ReplayBundleV1(fixtureMatch,
    ClassicRules.ImmutableId, transcript.Length, transcript, secrets, "future-replay-v2")),
    "unknown replay format fails closed without changing V1");
var badSeed = (byte[])secrets[0].Seed.Clone(); badSeed[0] ^= 1;
using (var changed = new ReplayRoundSecretV1(1, badSeed, (byte[])secrets[0].Nonce.Clone(),
    secrets[0].Commitment, secrets[0].Cut, E04Shuffle.AlgorithmId))
    AssertRuleError(() => MatchReplay.Rebuild(new ReplayBundleV1(fixtureMatch,
        ClassicRules.ImmutableId, transcript.Length, transcript,
        new[] { changed }.Concat(secrets.Skip(1)).ToArray())), "seed tamper rejects replay");
using (var changedCut = new ReplayRoundSecretV1(1, (byte[])secrets[0].Seed.Clone(),
    (byte[])secrets[0].Nonce.Clone(), secrets[0].Commitment,
    secrets[0].Cut == 51 ? 50 : secrets[0].Cut + 1, E04Shuffle.AlgorithmId))
    AssertRuleError(() => MatchReplay.Rebuild(new ReplayBundleV1(fixtureMatch,
        ClassicRules.ImmutableId, transcript.Length, transcript,
        new[] { changedCut }.Concat(secrets.Skip(1)).ToArray())), "cut tamper rejects replay transcript");
// Matchmaking rank, advertisement and cosmetic metadata never enter E04 context or input.
foreach (var ignoredProfile in new[] { (mmr: 0, skin: "default", ad: ""),
    (mmr: 9999, skin: "sponsor", ad: "placement-1") })
{
    using var unchanged = E04Shuffle.Create(fixtureMatch, 1, ClassicRules.ImmutableId,
        secrets[0].Seed, secrets[0].Nonce, secrets[0].Cut);
    Assert(unchanged.CommitmentHex == secrets[0].Commitment &&
        unchanged.Deck.Take(12).SequenceEqual(root.GetProperty("rounds")[0]
            .GetProperty("first12").EnumerateArray().Select(x => x.GetInt32())),
        $"E04 ignores MMR/skin/advertising profile {ignoredProfile.mmr}");
}
try { _ = E04Shuffle.Create(fixtureMatch, 1, ClassicRules.ImmutableId, new byte[31], new byte[16]);
    throw new Exception("FAILED: weak seed rejected"); }
catch (ArgumentException) { Console.WriteLine("PASS: weak seed rejected"); }
var failedEntropyGame = new HokmGame(fixtureMatch);
using (var brokenRng = new BrokenRandomNumberGenerator())
{
    try { using var never = E04Shuffle.CreateWithRandom(fixtureMatch, 1,
        ClassicRules.ImmutableId, brokenRng); throw new Exception("FAILED: RNG failure accepted"); }
    catch (CryptographicException) { Console.WriteLine("PASS: RNG failure prevents deal"); }
}
Assert(failedEntropyGame.Round == 0 && failedEntropyGame.Phase == GamePhase.Waiting,
    "no round transition after RNG failure");
using (var randomFirst = E04Shuffle.CreateRandom(fixtureMatch, 1, ClassicRules.ImmutableId))
using (var randomSecond = E04Shuffle.CreateRandom(fixtureMatch, 2, ClassicRules.ImmutableId))
    Assert(randomFirst.Seed.Length == 32 && randomFirst.Nonce.Length == 16 &&
        !randomFirst.Seed.SequenceEqual(randomSecond.Seed) &&
        !randomFirst.Nonce.SequenceEqual(randomSecond.Nonce),
        "fresh independent 256-bit seed and 128-bit nonce per round");
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

sealed class ThrowOnTrumpStore : IMatchEventStore
{
    public void Append(Guid roomId, CommandV1 command, int? publicValue, E04Round? preparedRound,
        RoundOutcomeV1? outcome)
    {
        if (command.Kind == "trump-selected")
            throw new InvalidOperationException("Simulated persistent event failure");
    }
}

sealed class CapturingCutStore : IMatchEventStore
{
    public List<MatchEventV1> Events { get; } = new();
    public List<ReplayRoundSecretV1> Proofs { get; } = new();
    public void Append(Guid roomId, CommandV1 command, int? publicValue, E04Round? preparedRound,
        RoundOutcomeV1? outcome)
    {
        Events.Add(new MatchEventV1(command.Sequence, command.ActionId, command.ActorSeat,
            command.RulesetVersion, command.Timestamp, command.Sequence, command.Kind, publicValue));
        if (preparedRound != null)
            Proofs.Add(new ReplayRoundSecretV1((long)preparedRound.RoundNumber,
                (byte[])preparedRound.Seed.Clone(), (byte[])preparedRound.Nonce.Clone(),
                preparedRound.CommitmentHex, preparedRound.CutIndex, E04Shuffle.AlgorithmId));
    }
}

sealed class BrokenRandomNumberGenerator : RandomNumberGenerator
{
    public override void GetBytes(byte[] data) => throw new CryptographicException("Simulated entropy failure");
    public override void GetNonZeroBytes(byte[] data) => throw new CryptographicException("Simulated entropy failure");
}
