using System.Text;
using Npgsql;

namespace Hokmnameh.Api;

public sealed record PlayerProfileView(Guid AccountId, string Nickname, string AvatarId, long Version);
public sealed record UpdatePlayerProfileRequest(string? Nickname, string? AvatarId);

public sealed class PlayerProfileService(string connectionString)
{
    public async Task<PlayerProfileView?> ReadAsync(Guid accountId, CancellationToken ct)
    {
        await using var c = new NpgsqlConnection(connectionString);
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT p.nickname,coalesce(v.avatar_id,'avatar-01'),coalesce(v.version,1)
            FROM profiles p LEFT JOIN player_profiles_v1 v ON v.account_id=p.account_id
            WHERE p.account_id=@id
            """, c);
        cmd.Parameters.AddWithValue("id", accountId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct)
            ? new PlayerProfileView(accountId,r.GetString(0),r.GetString(1),r.GetInt64(2)) : null;
    }

    public async Task<PlayerProfileView?> UpdateAsync(Guid accountId, long expectedVersion,
        UpdatePlayerProfileRequest input, CancellationToken ct)
    {
        var nickname = input.Nickname?.Trim().Normalize(NormalizationForm.FormKC);
        if (nickname == null || nickname.Length is < 3 or > 24 ||
            nickname.Any(ch => !char.IsLetterOrDigit(ch) && ch is not (' ' or '_' or '-')))
            throw new ArgumentException("Nickname must be 3–24 letters, digits, spaces, _ or -");
        if (input.AvatarId == null || !Enumerable.Range(1,8).Any(i => input.AvatarId == $"avatar-{i:00}"))
            throw new ArgumentException("Unknown avatar");
        await using var c = new NpgsqlConnection(connectionString);
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        // The optimistic version serializes both nickname and avatar as one profile update.
        await using (var version = new NpgsqlCommand("""
            UPDATE player_profiles_v1 SET avatar_id=@avatar,version=version+1
            WHERE account_id=@id AND version=@expected RETURNING version
            """, c, tx))
        {
            version.Parameters.AddWithValue("id", accountId);
            version.Parameters.AddWithValue("avatar", input.AvatarId);
            version.Parameters.AddWithValue("expected", expectedVersion);
            if (await version.ExecuteScalarAsync(ct) == null) return null;
        }
        await using (var update = new NpgsqlCommand("""
            UPDATE profiles SET nickname=@nickname,nickname_normalized=@normalized
            WHERE account_id=@id
            """, c, tx))
        {
            update.Parameters.AddWithValue("id", accountId);
            update.Parameters.AddWithValue("nickname", nickname);
            update.Parameters.AddWithValue("normalized", nickname.ToLowerInvariant());
            if (await update.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Profile missing");
        }
        await tx.CommitAsync(ct);
        return new PlayerProfileView(accountId,nickname,input.AvatarId,expectedVersion+1);
    }
}
