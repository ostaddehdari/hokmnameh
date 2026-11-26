using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net.WebSockets;
using Hokmnameh.Api;
using Hokmnameh.Core;

// Operator-only container CLI. No replay or raw proof is exposed through HTTP/WS.
if (args is ["--verify-replay", var matchText])
{
    if (!Guid.TryParse(matchText, out var matchId)) throw new ArgumentException("Invalid match ID");
    var settings = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    var store = new PostgresMatchStore(settings.GetConnectionString("Postgres") ?? "",
        settings["Hokm:AuditKeyBase64"] ?? "", "free");
    using var bundle = store.LoadReplay(matchId);
    var replay = MatchReplay.Rebuild(bundle);
    var snapshot = replay.Game.Snapshot(0);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        format = ReplayBundleV1.Format, matchId,
        stateVersion = replay.StateVersion, closedRounds = replay.ClosedRounds,
        phase = snapshot.phase, round = snapshot.round, score = snapshot.matchPoints,
        commitment = snapshot.shuffleCommitment
    }));
    return;
}

static string RatePartition(HttpContext context) =>
    $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

static RateLimitPartition<string> FixedWindow(string key, int permits, TimeSpan window) =>
    RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permits,
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true
    });

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<RoomDirectory>();
builder.Services.AddHostedService<MatchTicker>();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(opts => opts.SerializerOptions.IncludeFields = true);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Only proxy traffic from the local Nginx frontend. Never trust arbitrary client XFF.
    options.KnownProxies.Add(IPAddress.Loopback);
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth-guest", context =>
        FixedWindow(RatePartition(context), 10, TimeSpan.FromMinutes(5)));
    options.AddPolicy("authenticated-command", context =>
        FixedWindow(RatePartition(context), 120, TimeSpan.FromMinutes(1)));
    options.AddPolicy("websocket-connect", context =>
        FixedWindow(RatePartition(context), 20, TimeSpan.FromMinutes(1)));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://www.rfc-editor.org/rfc/rfc6585#section-4",
            title = "Too many requests",
            status = StatusCodes.Status429TooManyRequests
        }, cancellationToken);
    };
});
var app = builder.Build();
app.UseForwardedHeaders();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.UseRateLimiter();
var api = app.MapGroup("/hokmnameh");
api.MapGet("/", () => Results.Redirect("/hokmnameh/health/live"));
api.MapGet("/health/live", () => Results.Ok(new { status = "live", service = "hokmnameh-api", version = "0.1.0" }));
api.MapGet("/health/ready", async (AuthService auth, CancellationToken ct) =>
    await auth.ReadyAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));

api.MapPost("/api/v1/identity/guest", async (AuthService auth, CancellationToken ct) =>
{
    var guest = await auth.CreateGuestAsync(ct);
    return Results.Created("/hokmnameh/api/v1/identity/me", guest);
}).RequireRateLimiting("auth-guest");

api.MapGet("/api/v1/identity/me", async (HttpRequest req, AuthService auth, CancellationToken ct) =>
{
    var identity = await auth.ValidateAsync(req, ct);
    return identity == null ? Results.Unauthorized() : Results.Ok(identity);
}).RequireRateLimiting("authenticated-command");

api.MapPost("/api/v1/rooms", async (HttpRequest req, CreateRoomRequest body, AuthService auth,
    RoomDirectory rooms, CancellationToken ct) =>
{
    var identity = await auth.ValidateAsync(req, ct);
    if (identity == null) return Results.Unauthorized();
    var room = rooms.Create(identity.AccountId, (body.Mode ?? "free").ToLowerInvariant());
    return Results.Created($"/hokmnameh/api/v1/rooms/{room.Id}", new JoinRoomResponse(room.View(), 0));
}).RequireRateLimiting("authenticated-command");

// E-04 cut rooms are opt-in under v2, preserving the v1 immediate-deal prototype.
api.MapPost("/api/v2/rooms", async (HttpRequest req, CreateRoomRequest body, AuthService auth,
    RoomDirectory rooms, CancellationToken ct) =>
{
    var identity = await auth.ValidateAsync(req, ct);
    if (identity == null) return Results.Unauthorized();
    var room = rooms.Create(identity.AccountId, (body.Mode ?? "free").ToLowerInvariant(), requireCut: true);
    return Results.Created($"/hokmnameh/api/v1/rooms/{room.Id}", new JoinRoomResponse(room.View(), 0));
}).RequireRateLimiting("authenticated-command");

api.MapPost("/api/v2/rooms/{roomId:guid}/cut", async (Guid roomId, CutCardRequest body,
    HttpRequest req, AuthService auth, RoomDirectory rooms, CancellationToken ct) =>
{
    var identity = await auth.ValidateAsync(req, ct);
    if (identity == null) return Results.Unauthorized();
    var room = rooms.Get(roomId);
    room.Cut(identity.AccountId, body);
    await room.BroadcastAsync(ct);
    return Results.Ok(room.State(identity.AccountId));
}).RequireRateLimiting("authenticated-command");

api.MapPost("/api/v1/rooms/join/{code}", async (string code, HttpRequest req, AuthService auth,
    RoomDirectory rooms, CancellationToken ct) =>
{
    var identity = await auth.ValidateAsync(req, ct);
    if (identity == null) return Results.Unauthorized();
    var room = rooms.GetByCode(code);
    var joined = room.Join(identity.AccountId);
    await room.BroadcastAsync(ct);
    return Results.Ok(joined);
}).RequireRateLimiting("authenticated-command");

api.MapGet("/api/v1/rooms/{roomId:guid}", async (Guid roomId, HttpRequest req, AuthService auth,
    RoomDirectory rooms, CancellationToken ct) =>
{
    var identity = await auth.ValidateAsync(req, ct);
    return identity == null ? Results.Unauthorized() : Results.Ok(rooms.Get(roomId).State(identity.AccountId));
}).RequireRateLimiting("authenticated-command");

api.MapPost("/api/v1/rooms/{roomId:guid}/trump", async (Guid roomId, SetTrumpRequest body,
    HttpRequest req, AuthService auth, RoomDirectory rooms, CancellationToken ct) =>
{
    var identity = await auth.ValidateAsync(req, ct);
    if (identity == null) return Results.Unauthorized();
    var room = rooms.Get(roomId);
    room.ChooseTrump(identity.AccountId, body.Suit);
    await room.BroadcastAsync(ct);
    return Results.Ok(room.State(identity.AccountId));
}).RequireRateLimiting("authenticated-command");

api.MapPost("/api/v1/rooms/{roomId:guid}/play", async (Guid roomId, PlayCardRequest body,
    HttpRequest req, AuthService auth, RoomDirectory rooms, CancellationToken ct) =>
{
    var identity = await auth.ValidateAsync(req, ct);
    if (identity == null) return Results.Unauthorized();
    var room = rooms.Get(roomId);
    room.Play(identity.AccountId, body.Card);
    await room.BroadcastAsync(ct);
    return Results.Ok(room.State(identity.AccountId));
}).RequireRateLimiting("authenticated-command");

api.MapGet("/ws/rooms/{roomId:guid}", async (Guid roomId, HttpContext context, AuthService auth,
    RoomDirectory rooms, CancellationToken ct) =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    var identity = await auth.ValidateAsync(context.Request, ct);
    if (identity == null) { context.Response.StatusCode = 401; return; }
    GameRoom room;
    try { room = rooms.Get(roomId); }
    catch (KeyNotFoundException) { context.Response.StatusCode = 404; return; }
    var seat = room.SeatOf(identity.AccountId);
    if (!seat.HasValue) { context.Response.StatusCode = 403; return; }
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var connectionId = Guid.NewGuid();
    room.RegisterSocket(connectionId, socket, identity.AccountId);
    try
    {
        await room.BroadcastAsync(ct);
        // Mutations only via authenticated HTTP commands; WS transport is state push only.
        var buffer = new byte[1024];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close) break;
            if (result.Count > 1024) break;
        }
    }
    catch (WebSocketException) { }
    catch (OperationCanceledException) { }
    finally
    {
        room.RemoveSocket(connectionId);
        if (socket.State == WebSocketState.Open)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
    }
}).RequireRateLimiting("websocket-connect");

// Explicit problem responses without leaking stack traces.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (CutCommandException e)
    {
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = e.StatusCode;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "https://srun.ir/hokmnameh/problems/cut-command",
                title = "Cut command rejected", status = e.StatusCode, code = e.Code
            }));
        }
    }
    catch (GameRuleException e)
    {
        if (!ctx.Response.HasStarted) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { error = e.Message }); }
    }
    catch (KeyNotFoundException)
    {
        if (!ctx.Response.HasStarted) { ctx.Response.StatusCode = 404; await ctx.Response.WriteAsJsonAsync(new { error = "Not found" }); }
    }
});
app.Run();
