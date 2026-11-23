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
public sealed record RoomView(Guid Id, string Code, string Mode, string RulesetVersion, int PlayerCount, int Phase, bool Started);
public sealed record JoinRoomResponse(RoomView Room, int Seat);
public sealed record SetTrumpRequest(int Suit);
public sealed record PlayCardRequest(int Card);

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
    private readonly HokmGame _game = new();
    private DateTimeOffset _turnDeadline;
    private int _turnSeconds;
    public Guid Id { get; }
    public string Code { get; }
    public string Mode { get; }
    public string RulesetVersion { get; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    public GameRoom(Guid id, string code, string mode, Guid owner, int turnSeconds, string rulesetVersion)
    {
        if (string.IsNullOrWhiteSpace(rulesetVersion)) throw new ArgumentException("Ruleset version is required", nameof(rulesetVersion));
        Id = id; Code = code; Mode = mode; _players[0] = owner;
        RulesetVersion = rulesetVersion;
        _turnSeconds = Math.Clamp(turnSeconds, 10, 120);
        if (mode == "practice")
        {
            for (int i = 1; i < 4; i++) _bots[i] = true;
            _game.StartRound(0); ResetDeadline();
        }
    }

    public RoomView View()
    {
        lock (_lock) return new RoomView(Id, Code, Mode, RulesetVersion, _players.Count(p => p.HasValue) + _bots.Count(b => b), (int)_game.Phase, _game.Round > 0);
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
            for (int i = 0; i < 4; i++) if (_players[i] == identity) return new JoinRoomResponse(View(), i);
            if (Mode == "practice") throw new GameRuleException("Practice room has only one human seat");
            if (_game.Round != 0) throw new GameRuleException("Match already started");
            for (int i = 0; i < 4; i++)
                if (!_players[i].HasValue && !_bots[i])
                {
                    _players[i] = identity;
                    if (_players.All(p => p.HasValue)) { _game.StartRound(0); ResetDeadline(); }
                    return new JoinRoomResponse(View(), i);
                }
            throw new GameRuleException("Room full");
        }
    }
    public object State(Guid identity)
    {
        lock (_lock)
        {
            var seat = SeatOf(identity) ?? throw new GameRuleException("Not a room member");
            return new { room = View(), game = _game.Round > 0 ? _game.Snapshot(seat) : null,
                turnDeadline = _turnDeadline, isYourTurn = _game.Turn == seat &&
                    (_game.Phase == GamePhase.ChoosingTrump || _game.Phase == GamePhase.Playing) };
        }
    }
    public void ChooseTrump(Guid identity, int suit)
    {
        lock (_lock)
        {
            int seat = SeatOf(identity) ?? throw new GameRuleException("Not a room member");
            _game.ChooseTrump(seat, suit); ResetDeadline();
        }
    }
    public void Play(Guid identity, int card)
    {
        lock (_lock)
        {
            int seat = SeatOf(identity) ?? throw new GameRuleException("Not a room member");
            _game.Play(seat, card); ResetDeadline();
        }
    }
    private void ResetDeadline() => _turnDeadline = DateTimeOffset.UtcNow.AddSeconds(_turnSeconds);

    public bool Tick()
    {
        lock (_lock)
        {
            if (_game.Round == 0 || _game.Phase == GamePhase.Finished) return false;
            if (_game.Phase == GamePhase.Waiting)
            {
                _game.StartRound(_game.Hakem); ResetDeadline(); return true;
            }
            int seat = _game.Phase == GamePhase.ChoosingTrump ? _game.Hakem : _game.Turn;
            bool timerExpired = DateTimeOffset.UtcNow >= _turnDeadline;
            if (!_bots[seat] && !timerExpired) return false;
            if (_game.Phase == GamePhase.ChoosingTrump) _game.ChooseTrump(seat, HokmBot.PickTrump(_game, seat));
            else _game.Play(seat, HokmBot.ChooseCard(_game, seat, 1));
            ResetDeadline(); return true;
        }
    }

    public void RegisterSocket(Guid socketId, WebSocket ws, int seat) => _clients[socketId] = new ClientSocket(ws, seat);
    public void RemoveSocket(Guid socketId) => _clients.TryRemove(socketId, out _);
    public async Task BroadcastAsync(CancellationToken ct = default)
    {
        foreach (var kv in _clients)
        {
            var client = kv.Value;
            if (client.Socket.State != WebSocketState.Open) continue;
            object payload;
            lock (_lock) payload = new { type = "state", roomId = Id, state = _game.Round > 0 ? _game.Snapshot(client.Seat) : null,
                room = View(), turnDeadline = _turnDeadline };
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

    private static string ResolveRulesetVersion(IConfiguration config)
    {
        var configured = config.GetValue<string>("Hokm:RulesetVersion") ?? ClassicRules.ImmutableId;
        if (!string.Equals(configured, ClassicRules.ImmutableId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unsupported ruleset version: {configured}");
        return configured;
    }

    public GameRoom Create(Guid owner, string mode)
    {
        if (mode != "free" && mode != "practice") throw new GameRuleException("Supported modes: free, practice");
        var id = Guid.NewGuid();
        // Human readable invites are intentionally not credentials.
        var code = id.ToString("N")[..8].ToUpperInvariant();
        // Capture once at match creation. Later configuration/default changes cannot alter an active match.
        var room = new GameRoom(id, code, mode, owner, _turnSeconds, _rulesetVersion);
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
