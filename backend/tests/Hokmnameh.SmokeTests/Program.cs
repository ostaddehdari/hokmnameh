using Hokmnameh.Core;
using Hokmnameh.Api;
using System.Text.Json;

void Assert(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS: " + name); }
void AssertRuleError(Action action, string name)
{
    try { action(); throw new Exception("FAILED: " + name + " (no error)"); }
    catch (GameRuleException) { Console.WriteLine("PASS: " + name); }
}
var game = new HokmGame();
game.StartRound(0);
Assert(game.Phase == GamePhase.ChoosingTrump, "20-card preliminary deal");
for (int i = 0; i < 4; ++i) Assert(game.Snapshot(i).hand.Length == 5, "Five cards seat=" + i);
int suit = HokmBot.PickTrump(game, 0);
game.ChooseTrump(0, suit);
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
Console.WriteLine("All core smoke tests passed");

sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
}
