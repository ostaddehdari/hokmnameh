using System.Security.Cryptography;
using Hokmnameh.Core;

namespace Hokmnameh.Api;

// Internal authority-only format. Never serialize this bundle or expose it through HTTP/WS.
internal sealed class ReplayRoundSecretV1 : IDisposable
{
    internal readonly long Round;
    internal readonly byte[] Seed;
    internal readonly byte[] Nonce;
    internal readonly string Commitment;
    internal readonly int Cut;
    internal readonly string Algorithm;

    internal ReplayRoundSecretV1(long round, byte[] seed, byte[] nonce, string commitment, int cut, string algorithm)
    { Round = round; Seed = seed; Nonce = nonce; Commitment = commitment; Cut = cut; Algorithm = algorithm; }

    public void Dispose()
    { CryptographicOperations.ZeroMemory(Seed); CryptographicOperations.ZeroMemory(Nonce); }
}

internal sealed class ReplayBundleV1 : IDisposable
{
    internal const string Format = "hokmnameh-match-replay-v1";
    internal readonly string FormatId;
    internal readonly Guid MatchId;
    internal readonly string Ruleset;
    internal readonly long AuditSequence;
    internal readonly IReadOnlyList<MatchEventV1> Events;
    internal readonly IReadOnlyList<ReplayRoundSecretV1> Rounds;

    internal ReplayBundleV1(Guid matchId, string ruleset, long sequence,
        IReadOnlyList<MatchEventV1> events, IReadOnlyList<ReplayRoundSecretV1> rounds,
        string formatId = Format)
    { MatchId = matchId; Ruleset = ruleset; AuditSequence = sequence; Events = events; Rounds = rounds; FormatId = formatId; }

    public void Dispose()
    { foreach (var round in Rounds) round.Dispose(); }
}

internal sealed record ReplayResultV1(HokmGame Game, long StateVersion, int ClosedRounds);

internal static class MatchReplay
{
    // Dispatch is intentionally closed: old V1 records keep V1 semantics when newer rulesets exist.
    internal static ReplayResultV1 Rebuild(ReplayBundleV1 bundle)
    {
        if (bundle.FormatId != ReplayBundleV1.Format || bundle.MatchId == Guid.Empty ||
            bundle.Ruleset != ClassicRules.ImmutableId || bundle.AuditSequence != bundle.Events.Count)
            throw new GameRuleException("Unsupported or incomplete replay format");
        var game = new HokmGame(bundle.MatchId);
        var roundIndex = 0;
        var closed = 0;
        int? pendingCut = null;
        var actions = new HashSet<Guid>();
        for (var i = 0; i < bundle.Events.Count; i++)
        {
            var e = bundle.Events[i];
            if (e.SchemaVersion != 1 || e.Sequence != i + 1 || e.StateVersion != e.Sequence ||
                e.ActionId == Guid.Empty || !actions.Add(e.ActionId) ||
                e.RulesetVersion != bundle.Ruleset || e.Timestamp == default ||
                e.Timestamp.Offset != TimeSpan.Zero || e.ActorSeat is < -1 or > 3)
                throw new GameRuleException("Invalid replay event envelope");
            switch (e.Kind)
            {
                case "cut-selected":
                case "cut-fallback":
                    if (game.Phase != GamePhase.Waiting || pendingCut.HasValue ||
                        e.PublicValue is < 1 or > 51 || e.PublicValue == null ||
                        (e.Kind == "cut-selected" && e.ActorSeat != (game.Hakem + 3) % 4) ||
                        (e.Kind == "cut-fallback" && e.ActorSeat != -1))
                        throw new GameRuleException("Invalid replay cut");
                    pendingCut = e.PublicValue;
                    break;
                case "round-started":
                    if (e.ActorSeat != -1 || e.PublicValue != game.Round + 1 || roundIndex >= bundle.Rounds.Count)
                        throw new GameRuleException("Invalid round start");
                    var proof = bundle.Rounds[roundIndex++];
                    if (proof.Round != e.PublicValue || proof.Algorithm != E04Shuffle.AlgorithmId ||
                        proof.Seed.Length != 32 || proof.Nonce.Length != 16 || proof.Cut is < 1 or > 51 ||
                        (pendingCut.HasValue && proof.Cut != pendingCut.Value))
                        throw new GameRuleException("Invalid replay proof");
                    using (var prepared = E04Shuffle.Create(bundle.MatchId, (ulong)proof.Round,
                        bundle.Ruleset, proof.Seed, proof.Nonce, proof.Cut))
                    {
                        if (prepared.CommitmentHex != proof.Commitment)
                            throw new GameRuleException("Replay commitment mismatch");
                        game.StartRound(game.Round == 0 ? 0 : game.Hakem, prepared);
                    }
                    pendingCut = null;
                    break;
                case "trump-selected":
                case "trump-fallback":
                    if (e.ActorSeat != game.Hakem || e.PublicValue is not (>= 0 and <= 3))
                        throw new GameRuleException("Invalid replay trump");
                    game.ChooseTrump(e.ActorSeat, e.PublicValue.Value);
                    break;
                case "deal-initial":
                    if (e.ActorSeat != -1 || e.PublicValue != E04DealPlan.Cursor(0) ||
                        game.Phase != GamePhase.ChoosingTrump || game.DealCursors.Length != 1)
                        throw new GameRuleException("Invalid initial deal cursor");
                    break;
                case "trump-staged":
                case "trump-staged-fallback":
                    if (e.ActorSeat != game.Hakem || e.PublicValue is not (>= 0 and <= 3))
                        throw new GameRuleException("Invalid staged trump");
                    game.SelectTrump(e.ActorSeat, e.PublicValue.Value);
                    break;
                case "deal-second":
                case "deal-final":
                    if (e.ActorSeat != -1 || e.PublicValue != E04DealPlan.Cursor(e.Kind == "deal-second" ? 1 : 2) ||
                        game.Phase != (e.Kind == "deal-second" ? GamePhase.DealingSecond : GamePhase.DealingFinal))
                        throw new GameRuleException("Invalid staged deal cursor");
                    game.AdvanceDeal();
                    break;
                case "card-played":
                case "card-auto-played":
                    if (e.ActorSeat != game.Turn || e.PublicValue is not (>= 0 and < 52))
                        throw new GameRuleException("Invalid replay move");
                    game.Play(e.ActorSeat, e.PublicValue.Value);
                    break;
                case "round-closed":
                    if (e.ActorSeat != -1 || e.PublicValue != game.Round ||
                        game.Phase is not (GamePhase.Waiting or GamePhase.Finished))
                        throw new GameRuleException("Invalid round closure");
                    closed++;
                    break;
                default:
                    throw new GameRuleException("Unsupported replay event kind");
            }
            // A scored round must be closed before the next start; a closure cannot be duplicated.
            if (game.Round - closed > 1 || (game.Round == closed && game.Phase == GamePhase.Playing) ||
                (i + 1 < bundle.Events.Count && game.Phase is (GamePhase.Waiting or GamePhase.Finished) &&
                 bundle.Events[i + 1].Kind != "round-closed" && closed < game.Round))
                throw new GameRuleException("Replay round boundary mismatch");
        }
        if (pendingCut.HasValue || game.Phase is GamePhase.DealingSecond or GamePhase.DealingFinal ||
            roundIndex != bundle.Rounds.Count || game.Round != roundIndex || closed > roundIndex ||
            (game.Phase is GamePhase.Waiting or GamePhase.Finished && closed != roundIndex))
            throw new GameRuleException("Replay proof or closing event missing");
        return new ReplayResultV1(game, bundle.AuditSequence, closed);
    }
}
