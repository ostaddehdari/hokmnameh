using Npgsql;

namespace Hokmnameh.Api;

public sealed record ProgressView(Guid AccountId, long Xp, int Level);

// Awards are committed with round-closed in PostgresMatchStore; clients cannot issue XP.
public sealed class XpLedger(string connectionString)
{
    public async Task<ProgressView?> ReadAsync(Guid accountId, CancellationToken ct)
    {
        await using var c = new NpgsqlConnection(connectionString);
        await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT xp,level FROM profiles WHERE account_id=@account", c);
        cmd.Parameters.AddWithValue("account", accountId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new ProgressView(accountId, reader.GetInt64(0), reader.GetInt32(1)) : null;
    }
}
