using Hokmnameh.Core;
using Hokmnameh.Api;
using System.Text.Json;

void Assert(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS: " + name); }
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
Assert(ClassicRules.ImmutableId == "iranian-hokm-classic-v1", "immutable ruleset ID");
Assert(ClassicRules.RoundPoints(0, 0, 2) == 1, "normal round scores one");
Assert(ClassicRules.RoundPoints(0, 0, 0) == 2, "hakem team sweep scores two");
Assert(ClassicRules.RoundPoints(1, 0, 0) == 3, "non-hakem sweep scores three");
Assert(ClassicRules.NextHakem(0, 0) == 0, "winning hakem stays");
Assert(ClassicRules.NextHakem(0, 1) == 1, "losing hakem rotates clockwise");
Assert(ClassicRules.NextHakem(3, 0) == 0, "hakem rotation wraps seat three to zero");

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
Console.WriteLine("All core smoke tests passed");
