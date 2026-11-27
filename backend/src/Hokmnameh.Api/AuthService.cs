using System.Security.Cryptography;
using Npgsql;

namespace Hokmnameh.Api;

public sealed record GuestIdentity(Guid AccountId, string Nickname, string AccessToken, DateTimeOffset ExpiresAt);
public sealed record Identity(Guid AccountId, string Nickname);
public sealed class AuthService
{
    private readonly string _connectionString;
    public AuthService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required");
    }
    public async Task<bool> ReadyAsync(CancellationToken ct)
    {
        try
        {
            await using var c = new NpgsqlConnection(_connectionString);
            await c.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT 1 FROM accounts LIMIT 1", c);
            await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch { return false; }
    }
    public async Task<GuestIdentity> CreateGuestAsync(CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        var nickname = "Guest-" + id.ToString("N")[..8];
        var expires = DateTimeOffset.UtcNow.AddDays(30);
        await using var c = new NpgsqlConnection(_connectionString);
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await using (var cmd = new NpgsqlCommand("INSERT INTO accounts(id,status,is_guest) VALUES (@id,'active',true)", c, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand("INSERT INTO profiles(account_id,nickname,nickname_normalized) VALUES (@id,@n,@nn)", c, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("n", nickname);
            cmd.Parameters.AddWithValue("nn", nickname.ToLowerInvariant());
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO player_profiles_v1(account_id,avatar_id) VALUES (@id,'avatar-01')", c, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        foreach (var table in new[] { "player_reputation_v1", "player_league_v1" })
        {
            await using var cmd = new NpgsqlCommand($"INSERT INTO {table}(account_id) VALUES (@id)", c, tx);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand("INSERT INTO auth_sessions(account_id,token_hash,expires_at) VALUES (@id,@hash,@expires)", c, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("hash", hash);
            cmd.Parameters.AddWithValue("expires", expires);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new GuestIdentity(id, nickname, token, expires);
    }
    public async Task<Identity?> ValidateAsync(HttpRequest request, CancellationToken ct)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = authorization.Substring(7).Trim();
        if (token.Length < 40 || token.Length > 256) return null;
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        await using var c = new NpgsqlConnection(_connectionString);
        await c.OpenAsync(ct);
        const string sql = """
          SELECT a.id, p.nickname FROM auth_sessions s
          JOIN accounts a ON a.id=s.account_id
          JOIN profiles p ON p.account_id=a.id
          WHERE s.token_hash=@hash AND s.expires_at>now() AND s.revoked_at IS NULL
            AND a.status='active'
          """;
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("hash", hash);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new Identity(reader.GetGuid(0), reader.GetString(1)) : null;
    }

    public async Task<bool> LogoutAsync(HttpRequest request, CancellationToken ct)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var token = authorization.Substring(7).Trim();
        if (token.Length < 40 || token.Length > 256) return false;
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        await using var c = new NpgsqlConnection(_connectionString);
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            UPDATE auth_sessions SET revoked_at=now(),revoked_reason='user-logout'
            WHERE token_hash=@hash AND revoked_at IS NULL AND expires_at>now()
            """, c);
        cmd.Parameters.AddWithValue("hash", hash);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }
}
