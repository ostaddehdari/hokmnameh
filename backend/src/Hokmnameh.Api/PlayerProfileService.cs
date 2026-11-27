using System.Text;
using Npgsql;

namespace Hokmnameh.Api;

public sealed record PlayerProfileView(Guid AccountId, string Nickname, string AvatarId, long Version,
    long NicknameVersion);
public sealed record UpdatePlayerProfileRequest(string? Nickname, string? AvatarId);
public sealed record NicknameView(string Nickname, long Version);
public sealed record AvatarView(string AvatarId, long Version);
public sealed record ReputationView(int Score, long Version);
public sealed record LeagueView(string? LeagueId, long Version);
public sealed record UpdateNicknameRequest(string? Nickname);
public sealed record UpdateAvatarRequest(string? AvatarId);

public sealed class PlayerProfileService(string connectionString)
{
    public async Task<PlayerProfileView?> ReadAsync(Guid accountId, CancellationToken ct)
    {
        await using var c = new NpgsqlConnection(connectionString);
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT p.nickname,coalesce(v.avatar_id,'avatar-01'),coalesce(v.version,1),p.nickname_version
            FROM profiles p LEFT JOIN player_profiles_v1 v ON v.account_id=p.account_id
            WHERE p.account_id=@id
            """, c);
        cmd.Parameters.AddWithValue("id", accountId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct)
            ? new PlayerProfileView(accountId,r.GetString(0),r.GetString(1),r.GetInt64(2),r.GetInt64(3)) : null;
    }

    private static string NormalizeNickname(string? value)
    {
        var nickname = value?.Trim().Normalize(NormalizationForm.FormKC);
        if (nickname == null || nickname.Length is < 3 or > 24 ||
            nickname.Any(ch => !char.IsLetterOrDigit(ch) && ch is not (' ' or '_' or '-')))
            throw new ArgumentException("Nickname must be 3–24 letters, digits, spaces, _ or -");
        return nickname;
    }

    private static string ValidateAvatar(string? avatar)
    {
        if (avatar == null || !Enumerable.Range(1,8).Any(i => avatar == $"avatar-{i:00}"))
            throw new ArgumentException("Unknown avatar");
        return avatar;
    }

    public async Task<PlayerProfileView?> UpdateAsync(Guid accountId, long expectedVersion,
        long expectedNicknameVersion,
        UpdatePlayerProfileRequest input, CancellationToken ct)
    {
        var nickname = NormalizeNickname(input.Nickname);
        var avatar = ValidateAvatar(input.AvatarId);
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
            version.Parameters.AddWithValue("avatar", avatar);
            version.Parameters.AddWithValue("expected", expectedVersion);
            if (await version.ExecuteScalarAsync(ct) == null) return null;
        }
        await using (var update = new NpgsqlCommand("""
            UPDATE profiles SET nickname=@nickname,nickname_normalized=@normalized,
              nickname_version=nickname_version+1
            WHERE account_id=@id AND nickname_version=@expected RETURNING nickname_version
            """, c, tx))
        {
            update.Parameters.AddWithValue("id", accountId);
            update.Parameters.AddWithValue("nickname", nickname);
            update.Parameters.AddWithValue("normalized", nickname.ToLowerInvariant());
            update.Parameters.AddWithValue("expected", expectedNicknameVersion);
            if (await update.ExecuteScalarAsync(ct) == null) return null;
        }
        await tx.CommitAsync(ct);
        return new PlayerProfileView(accountId,nickname,avatar,expectedVersion+1,expectedNicknameVersion+1);
    }

    public async Task<NicknameView?> ReadNicknameAsync(Guid accountId, CancellationToken ct)
    {
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT nickname,nickname_version FROM profiles WHERE account_id=@id",c);
        cmd.Parameters.AddWithValue("id",accountId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new NicknameView(r.GetString(0),r.GetInt64(1)) : null;
    }

    public async Task<NicknameView?> UpdateNicknameAsync(Guid accountId,long expected,string? value,CancellationToken ct)
    {
        var nickname=NormalizeNickname(value);
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            UPDATE profiles SET nickname=@nickname,nickname_normalized=@normalized,
              nickname_version=nickname_version+1 WHERE account_id=@id AND nickname_version=@expected
            RETURNING nickname_version
            """,c);
        cmd.Parameters.AddWithValue("id",accountId);
        cmd.Parameters.AddWithValue("expected",expected);
        cmd.Parameters.AddWithValue("nickname",nickname);
        cmd.Parameters.AddWithValue("normalized",nickname.ToLowerInvariant());
        var newVersion=await cmd.ExecuteScalarAsync(ct);
        return newVersion == null ? null : new NicknameView(nickname,(long)newVersion);
    }

    public async Task<AvatarView?> ReadAvatarAsync(Guid accountId,CancellationToken ct)
    {
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT avatar_id,version FROM player_profiles_v1 WHERE account_id=@id",c);
        cmd.Parameters.AddWithValue("id",accountId);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new AvatarView(r.GetString(0),r.GetInt64(1)) : null;
    }

    public async Task<AvatarView?> UpdateAvatarAsync(Guid accountId,long expected,string? value,CancellationToken ct)
    {
        var avatar=ValidateAvatar(value);
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            UPDATE player_profiles_v1 SET avatar_id=@avatar,version=version+1
            WHERE account_id=@id AND version=@expected RETURNING version
            """,c);
        cmd.Parameters.AddWithValue("id",accountId);
        cmd.Parameters.AddWithValue("expected",expected);
        cmd.Parameters.AddWithValue("avatar",avatar);
        var newVersion=await cmd.ExecuteScalarAsync(ct);
        return newVersion == null ? null : new AvatarView(avatar,(long)newVersion);
    }

    public async Task<ReputationView?> ReadReputationAsync(Guid accountId,CancellationToken ct)
    {
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT score,version FROM player_reputation_v1 WHERE account_id=@id",c);
        cmd.Parameters.AddWithValue("id",accountId);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new ReputationView(r.GetInt32(0),r.GetInt64(1)) : null;
    }

    public async Task<LeagueView?> ReadLeagueAsync(Guid accountId,CancellationToken ct)
    {
        await using var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT league_id,version FROM player_league_v1 WHERE account_id=@id",c);
        cmd.Parameters.AddWithValue("id",accountId);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new LeagueView(r.IsDBNull(0)?null:r.GetString(0),r.GetInt64(1)) : null;
    }
}
