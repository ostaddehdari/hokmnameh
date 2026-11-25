using System.Security.Cryptography;
using System.Text.Json;
using Hokmnameh.Core;

namespace Hokmnameh.Api;

// An append-only in-process audit contract. Durable event storage/replay is S02-W03.
public sealed record CommandV1(long Sequence, Guid ActionId, int ActorSeat, string RulesetVersion,
    DateTimeOffset Timestamp, long StateVersion, string Kind)
{
    public int SchemaVersion => 1;
}
public sealed record MatchEventV1(long Sequence, Guid ActionId, int ActorSeat, string RulesetVersion,
    DateTimeOffset Timestamp, long StateVersion, string Kind, int? PublicValue)
{
    public int SchemaVersion => 1;
}
public sealed record SignedSeatSnapshotV1(Guid RoomId, int Seat, long StateVersion,
    GameSnapshot Snapshot, string Signature);

public interface IMatchEventStore
{
    void Append(Guid roomId, CommandV1 command, int? publicValue, E04Round? preparedRound);
}

public sealed class MatchAudit : IDisposable
{
    private readonly Guid _roomId;
    private readonly IMatchEventStore? _store;
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly List<MatchEventV1> _events = new();
    private readonly HashSet<Guid> _actions = new();
    public long StateVersion => _events.Count;
    public IReadOnlyList<MatchEventV1> Events => _events.AsReadOnly();

    public MatchAudit() : this(Guid.Empty) { }
    public MatchAudit(Guid roomId, IMatchEventStore? store = null)
    { _roomId = roomId; _store = store; }

    public void Append(CommandV1 command, int? publicValue = null, E04Round? preparedRound = null)
    {
        if (command.ActionId == Guid.Empty || command.Sequence != StateVersion + 1 ||
            command.StateVersion != StateVersion || command.ActorSeat < -1 || command.ActorSeat > 3 ||
            command.RulesetVersion != ClassicRules.ImmutableId || command.Timestamp == default ||
            command.Timestamp.Offset != TimeSpan.Zero ||
            string.IsNullOrWhiteSpace(command.Kind) || _actions.Contains(command.ActionId))
            throw new GameRuleException("Invalid or duplicate versioned action");
        if (preparedRound != null && (command.Kind != "round-started" ||
            preparedRound.MatchId != _roomId || preparedRound.RoundNumber != (ulong)publicValue.GetValueOrDefault()))
            throw new GameRuleException("Invalid round proof context");
        _store?.Append(_roomId, command, publicValue, preparedRound);
        _actions.Add(command.ActionId);
        _events.Add(new MatchEventV1(command.Sequence, command.ActionId, command.ActorSeat,
            command.RulesetVersion, command.Timestamp, command.Sequence, command.Kind, publicValue));
    }

    private byte[] Payload(Guid roomId, int seat, long version, GameSnapshot snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(new { roomId, seat, stateVersion = version, snapshot }, JsonOptions.Default);

    public SignedSeatSnapshotV1 Sign(Guid roomId, int seat, GameSnapshot snapshot)
    {
        if (seat < 0 || seat > 3 || snapshot.yourSeat != seat || roomId == Guid.Empty)
            throw new GameRuleException("Seat mismatch");
        using var hmac = new HMACSHA256(_key);
        var signature = Convert.ToHexString(hmac.ComputeHash(Payload(roomId, seat, StateVersion, snapshot))).ToLowerInvariant();
        return new SignedSeatSnapshotV1(roomId, seat, StateVersion, snapshot, signature);
    }

    public bool Verify(SignedSeatSnapshotV1 signed)
    {
        if (signed.Seat < 0 || signed.Seat > 3 || signed.Snapshot.yourSeat != signed.Seat) return false;
        byte[] provided;
        try { provided = Convert.FromHexString(signed.Signature); }
        catch (FormatException) { return false; }
        using var hmac = new HMACSHA256(_key);
        return CryptographicOperations.FixedTimeEquals(provided,
            hmac.ComputeHash(Payload(signed.RoomId, signed.Seat, signed.StateVersion, signed.Snapshot)));
    }
    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}
