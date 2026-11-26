using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Hokmnameh.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hokmnameh.Api;

public sealed record CreateRoomRequest(string? Mode);
public sealed record RoomView(Guid Id, string Code, string Mode, string RulesetVersion, int PlayerCount, int Phase, bool Started,
    string? CutPhase = null, DateTimeOffset? CutDeadline = null);
public sealed record JoinRoomResponse(RoomView Room, int Seat);
public sealed record SetTrumpRequest(int Suit);
public sealed record PlayCardRequest(int Card);
public sealed record CutCardRequest(int CutIndex, Guid ActionId, long StateVersion);

public sealed class CutCommandException(string code, int statusCode) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

internal sealed class ClientSocket(WebSocket socket, int seat)
{
    public WebSocket Socket { get; } = socket;
    public int Seat { get; } = seat;
    public SemaphoreSlim WriteLock { get; } = new(1, 1);
}

public sealed class GameRoom
{
    private readonly object _lock = new();
    private readonly Guid?[] _players = new Guid?[4];
    private readonly bool[] _bots = new bool[4];
    private readonly ConcurrentDictionary<Guid, ClientSocket> _clients = new();
    private readonly HokmGame _game;
    private readonly MatchAudit _audit;
    private readonly IMatchParticipantStore? _participantStore;
    private bool _faulted;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _turnDeadline;
    private int _turnSeconds;
    private readonly bool _requireCut;
    private E04Round? _pendingCutRound;
    private int _pendingHakem;
    private DateTimeOffset _cutDeadline;
    private readonly HashSet<Guid> _cutActions = new();
    public Guid Id { get; }
    public string Code { get; }
    public string Mode { get; }
    public string RulesetVersion { get; }
    public DateTimeOffset CreatedAt { get; }

    public GameRoom(Guid id, string code, string mode, Guid owner, int turnSeconds, string rulesetVersion,
        TimeProvider? timeProvider = null, IMatchEventStore? eventStore = null, bool requireCut = false)
    {
        if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("Ruleset version is required", nameof(rulesetVersion));
        Id = id; Code = code; Mode = mode; _players[0] = owner;
        _game = new HokmGame(id);
        _audit = new MatchAudit(id, eventStore);
        _participantStore = eventStore as IMatchParticipantStore;
        RulesetVersion = rulesetVersion;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _requireCut = requireCut;
        CreatedAt = _timeProvider.GetUtcNow();
        _turnSeconds = Math.Clamp(turnSeconds, 10, 120);
        if (mode == "practice")
        {
            for (int i = 1; i < 4; i++) _bots[i] = true;
            BeginRound(0);
        }
    }

    public RoomView View()
    {
        lock (_lock) return new RoomView(Id, Code, Mode, RulesetVersion, _players.Count(p => p.HasValue) + _bots.Count(b => b),
            (int)_game.Phase, _game.Round > 0 || _pendingCutRound != null,
            _pendingCutRound == null ? null : "CUT_PENDING", _pendingCutRound == null ? null : _cutDeadline);
    }
    public int? SeatOf(Guid identity)
    {
        lock (_lock)
            for (int i = 0; i < 4; i++) if (_players[i] == identity) return i;
        return null;
    }
    public JoinRoomResponse Join(Guid identity)
    {
        lock (_lock)
        {
            EnsureHealthy();
            for (int i = 0; i < 4; i++) if (_players[i] == identity) return new JoinRoomResponse(View(), i);
            if (Mode == "practice") throw new GameRuleException("Practice room has only one human seat");
            if (_game.Round != 0) throw new GameRuleException("Match already started");
            for (int i = 0; i < 4; i++)
                if (!_players[i].HasValue && !_bots[i])
                {
                    _players[i] = identity;
                    if (_players.All(p => p.HasValue)) BeginRound(0);
                    return new JoinRoomResponse(View(), i);
                }
            throw new GameRuleException("Room full");
        }
    }
    public object State(Guid identity)
    {
        lock (_lock)
        {
            EnsureHealthy();
            var seat = SeatOf(identity) ?? throw new GameRuleException("Not a room member");
            var signed = _game.Round > 0 ? _audit.Sign(Id, seat, _game.Snapshot(seat)) : null;
            return new { room = View(), game = signed?.Snapshot, snapshotProof = signed == null ? null : new { signed.StateVersion, signed.Signature },
                cutStateVersion = _pendingCutRound == null ? (long?)null : _audit.StateVersion,
                isYourCut = _pendingCutRound != null && seat == CutterSeat(_pendingHakem),
                turnDeadline = _pendingCutRound == null ? (DateTimeOffset?)_turnDeadline : null,
                isYourTurn = _pendingCutRound == null && _game.Turn == seat &&
                    (_game.Phase == GamePhase.ChoosingTrump || _game.Phase == GamePhase.Playing) };
        }
    }
    private static int CutterSeat(int hakem) => (hakem + 3) % 4;

    public void Cut(Guid identity, CutCardRequest command)
    {
        lock (_lock)
        {
            EnsureHealthy();
            int seat = SeatOf(identity) ?? throw new CutCommandException("cut_not_member", 403);
            if (command.ActionId == Guid.Empty) throw new CutCommandException("invalid_cut_action_id", 400);
            if (_cutActions.Contains(command.ActionId)) throw new CutCommandException("duplicate_cut_action", 409);
            if (_pendingCutRound == null) throw new CutCommandException("cut_not_pending", 409);
            if (seat != CutterSeat(_pendingHakem)) throw new CutCommandException("wrong_cutter", 403);
            if (command.StateVersion != _audit.StateVersion) throw new CutCommandException("stale_cut_state", 409);
            if (_timeProvider.GetUtcNow() >= _cutDeadline) throw new CutCommandException("cut_deadline_expired", 410);
            if (command.CutIndex is < 1 or > 51) throw new CutCommandException("invalid_cut_index", 400);
            FinalizeCut(seat, command.CutIndex, command.ActionId, false);
        }
    }
    public void ChooseTrump(Guid identity, int suit)
    {
        lock (_lock)
        {
            int seat = SeatOf(identity) ?? throw new GameRuleException("Not a room member");
            EnsureHealthy();
            if (_requireCut && _game.Phase == GamePhase.ChoosingTrump && _timeProvider.GetUtcNow() >= _turnDeadline)
                throw new CutCommandException("trump_deadline_expired", 410);
            if (_requireCut) DealTrump(seat, suit, "trump-staged");
            else { _game.ChooseTrump(seat, suit); Record(seat, "trump-selected", suit); }
            ResetDeadline();
        }
    }
    public void Play(Guid identity, int card)
    {
        lock (_lock)
        {
            int seat = SeatOf(identity) ?? throw new GameRuleException("Not a room member");
            EnsureHealthy();
            _game.Play(seat, card); Record(seat, "card-played", card);
            if (_game.Phase == GamePhase.Waiting || _game.Phase == GamePhase.Finished)
                Record(-1, "round-closed", _game.Round);
            ResetDeadline();
        }
    }
    private void ResetDeadline() => _turnDeadline = _timeProvider.GetUtcNow().AddSeconds(
        _requireCut && _game.Phase == GamePhase.ChoosingTrump ? 20 : _turnSeconds);
    private void DealTrump(int seat, int suit, string kind)
    {
        // The transition and both cursors are independent audit events for authority replay.
        try
        {
            _game.SelectTrump(seat, suit); Record(seat, kind, suit);
            _game.AdvanceDeal(); Record(-1, "deal-second", E04DealPlan.Cursor(1));
            _game.AdvanceDeal(); Record(-1, "deal-final", E04DealPlan.Cursor(2));
        }
        catch (GameRuleException) when (_game.Phase == GamePhase.ChoosingTrump) { throw; }
        catch { _faulted = true; throw; }
    }
    private void EnsureHealthy()
    {
        if (_faulted) throw new InvalidOperationException("Match authority is quarantined after audit failure");
    }
    private void Record(int seat, string kind, int? value = null, E04Round? proof = null, Guid? actionId = null)
    {
        try
        {
            _audit.Append(new CommandV1(_audit.StateVersion + 1, actionId ?? Guid.NewGuid(), seat, RulesetVersion,
                _timeProvider.GetUtcNow(), _audit.StateVersion, kind), value, proof);
        }
        catch { _faulted = true; throw; }
    }
    private void BeginRound(int hakem)
    {
        EnsureHealthy();
        try
        {
            for (int seat = 0; seat < 4; seat++)
                if (_players[seat] is Guid accountId)
                    _participantStore?.RegisterParticipant(Id, accountId, seat, RulesetVersion);
        }
        catch { _faulted = true; throw; }
        var prepared = E04Shuffle.CreateRandom(Id, (ulong)(_game.Round + 1), RulesetVersion);
        if (_requireCut)
        {
            _pendingCutRound = prepared;
            _pendingHakem = hakem;
            _cutDeadline = _timeProvider.GetUtcNow().AddSeconds(8);
            return;
        }
        using (prepared)
        {
            Record(-1, "round-started", _game.Round + 1, prepared);
            try { _game.StartRound(hakem, prepared); ResetDeadline(); }
            catch { _faulted = true; throw; }
        }
    }

    private void FinalizeCut(int actorSeat, int index, Guid actionId, bool fallback)
    {
        var pending = _pendingCutRound ?? throw new InvalidOperationException("No pending cut");
        try
        {
            using var prepared = E04Shuffle.Create(Id, pending.RoundNumber, RulesetVersion,
                pending.Seed, pending.Nonce, index);
            Record(actorSeat, fallback ? "cut-fallback" : "cut-selected", index, actionId: actionId);
            if (!fallback) _cutActions.Add(actionId);
            Record(-1, "round-started", _game.Round + 1, prepared);
            _game.StartRound(_pendingHakem, prepared);
            Record(-1, "deal-initial", E04DealPlan.Cursor(0));
            ResetDeadline();
        }
        catch { _faulted = true; throw; }
        finally { pending.Dispose(); _pendingCutRound = null; }
    }

    public bool Tick()
    {
        lock (_lock)
        {
            if (_faulted) return false;
            if (_pendingCutRound != null)
            {
                int cutter = CutterSeat(_pendingHakem);
                if (!_bots[cutter] && _timeProvider.GetUtcNow() < _cutDeadline) return false;
                FinalizeCut(-1, _pendingCutRound.CutIndex, Guid.NewGuid(), true);
                return true;
            }
            if (_game.Round == 0 || _game.Phase == GamePhase.Finished) return false;
            if (_game.Phase == GamePhase.Waiting)
            {
                BeginRound(_game.Hakem); return true;
            }
            int seat = _game.Phase == GamePhase.ChoosingTrump ? _game.Hakem : _game.Turn;
            bool timerExpired = _timeProvider.GetUtcNow() >= _turnDeadline;
            if (!_bots[seat] && !timerExpired) return false;
            if (_game.Phase == GamePhase.ChoosingTrump)
            { int suit = HokmBot.PickTrump(_game, seat);
              if (_requireCut) DealTrump(seat, suit, "trump-staged-fallback");
              else { _game.ChooseTrump(seat, suit); Record(seat, "trump-fallback", suit); } }
            else
            {
                int card = HokmBot.ChooseCard(_game, seat, 1); _game.Play(seat, card);
                Record(seat, "card-auto-played", card);
                if (_game.Phase == GamePhase.Waiting || _game.Phase == GamePhase.Finished)
                    Record(-1, "round-closed", _game.Round);
            }
            ResetDeadline(); return true;
        }
    }

    public void RegisterSocket(Guid socketId, WebSocket ws, Guid identity)
    {
        lock (_lock)
        {
            EnsureHealthy();
            int seat = SeatOf(identity) ?? throw new GameRuleException("Not a room member");
            _clients[socketId] = new ClientSocket(ws, seat);
        }
    }
    public void RemoveSocket(Guid socketId) => _clients.TryRemove(socketId, out _);
    public async Task BroadcastAsync(CancellationToken ct = default)
    {
        foreach (var kv in _clients)
        {
            var client = kv.Value;
            if (client.Socket.State != WebSocketState.Open) continue;
            object payload;
            lock (_lock)
            {
                EnsureHealthy();
                var signed = _game.Round > 0 ? _audit.Sign(Id, client.Seat, _game.Snapshot(client.Seat)) : null;
                payload = new { type = "state", roomId = Id, state = signed?.Snapshot,
                    snapshotProof = signed == null ? null : new { signed.StateVersion, signed.Signature },
                    room = View(), cutStateVersion = _pendingCutRound == null ? (long?)null : _audit.StateVersion,
                    isYourCut = _pendingCutRound != null && client.Seat == CutterSeat(_pendingHakem),
                    turnDeadline = _pendingCutRound == null ? (DateTimeOffset?)_turnDeadline : null };
            }
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions.Default);
            try
            {
                await client.WriteLock.WaitAsync(ct);
                try { await client.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct); }
                finally { client.WriteLock.Release(); }
            }
            catch (WebSocketException) { _clients.TryRemove(kv.Key, out _); }
            catch (ObjectDisposedException) { _clients.TryRemove(kv.Key, out _); }
        }
    }
}

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new() { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}

public sealed class RoomDirectory(IConfiguration config)
{
    private readonly ConcurrentDictionary<Guid, GameRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, Guid> _codes = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _turnSeconds = config.GetValue<int?>("Hokm:TurnSeconds") ?? 30;
    private readonly string _rulesetVersion = ResolveRulesetVersion(config);
    private readonly string? _connectionString = config.GetConnectionString("Postgres");
    private readonly string? _auditKey = config.GetValue<string>("Hokm:AuditKeyBase64");

    private static string ResolveRulesetVersion(IConfiguration config)
    {
        var configured = config.GetValue<string>("Hokm:RulesetVersion") ?? ClassicRules.ImmutableId;
        if (!string.Equals(configured, ClassicRules.ImmutableId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unsupported ruleset version: {configured}");
        return configured;
    }

    public GameRoom Create(Guid owner, string mode, bool requireCut = false)
    {
        if (mode != "free" && mode != "practice") throw new GameRuleException("Supported modes: free, practice");
        if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_auditKey))
            throw new InvalidOperationException("Room creation requires PostgreSQL and Hokm:AuditKeyBase64");
        var id = Guid.NewGuid();
        // Human readable invites are intentionally not credentials.
        var code = id.ToString("N")[..8].ToUpperInvariant();
        // Capture once at match creation. Later configuration/default changes cannot alter an active match.
        var room = new GameRoom(id, code, mode, owner, _turnSeconds, _rulesetVersion,
            eventStore: new PostgresMatchStore(_connectionString, _auditKey, mode), requireCut: requireCut);
        if (!_rooms.TryAdd(id, room)) throw new InvalidOperationException("Failed to register room");
        _codes[code] = id;
        return room;
    }
    public GameRoom Get(Guid id) => _rooms.TryGetValue(id, out var value) ? value : throw new KeyNotFoundException("Room missing");
    public GameRoom GetByCode(string code) => _codes.TryGetValue(code, out var id) ? Get(id) : throw new KeyNotFoundException("Room code missing");
    public IEnumerable<GameRoom> ActiveRooms() => _rooms.Values;
}

public sealed class MatchTicker(RoomDirectory directory, ILogger<MatchTicker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            foreach (var room in directory.ActiveRooms())
                try { if (room.Tick()) await room.BroadcastAsync(stoppingToken); }
                catch (Exception e) { logger.LogError(e, "Room tick failed: {RoomId}", room.Id); }
    }
}
