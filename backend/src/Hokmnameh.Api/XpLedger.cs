using Npgsql;

namespace Hokmnameh.Api;

public sealed record ProgressView(Guid AccountId, long Xp, int Level);

// Internal award entry point. A policy must choose the amount; clients cannot issue XP.
public sealed class XpLedger(string connectionString)
{
    public async Task<bool> AwardClosedRoundAsync(Guid matchId, long closeEventSequence,
        Guid accountId, int amount, CancellationToken ct)
    {
        if (matchId == Guid.Empty || accountId == Guid.Empty || closeEventSequence < 1 || amount is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(amount));
        await using var c = new NpgsqlConnection(connectionString);
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        // Serialize awards to this player, then insert only if the event and participant agree.
        await using (var account = new NpgsqlCommand("SELECT id FROM accounts WHERE id=@account FOR UPDATE", c, tx))
        {
            account.Parameters.AddWithValue("account", accountId);
            if (await account.ExecuteScalarAsync(ct) == null) throw new InvalidOperationException("Unknown account");
        }
        await using var award = new NpgsqlCommand("""
            INSERT INTO xp_awards_v1(match_id,event_sequence,account_id,amount)
            SELECT e.match_id,e.sequence,p.account_id,@amount FROM match_events_v1 e
              JOIN match_participants p ON p.match_id=e.match_id AND p.account_id=@account
            WHERE e.match_id=@match AND e.sequence=@sequence AND e.kind='round-closed'
            ON CONFLICT(match_id,event_sequence,account_id) DO NOTHING
            RETURNING amount
            """, c, tx);
        award.Parameters.AddWithValue("match", matchId);
        award.Parameters.AddWithValue("sequence", closeEventSequence);
        award.Parameters.AddWithValue("account", accountId);
        award.Parameters.AddWithValue("amount", amount);
        var inserted = await award.ExecuteScalarAsync(ct) != null;
        if (inserted)
        {
            await using var increment = new NpgsqlCommand(
                "UPDATE profiles SET xp=xp+@amount WHERE account_id=@account", c, tx);
            increment.Parameters.AddWithValue("amount", amount);
            increment.Parameters.AddWithValue("account", accountId);
            if (await increment.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Missing player progress");
        }
        await tx.CommitAsync(ct);
        return inserted;
    }

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
