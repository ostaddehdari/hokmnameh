using System.Security.Cryptography;
using System.Text;
using Hokmnameh.Core;
using Npgsql;
using NpgsqlTypes;

namespace Hokmnameh.Api;

// The event ledger and encrypted proof commit in the same PostgreSQL transaction.
// Match actors are fail-stopped if persistence fails; replay/rejoin is scheduled for S02-W03.
public sealed class PostgresMatchStore : IMatchEventStore
{
    private readonly string _connectionString;
    private readonly byte[] _key;
    private readonly string _mode;

    public PostgresMatchStore(string connectionString, string keyBase64, string mode)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("PostgreSQL required");
        byte[] key;
        try { key = Convert.FromBase64String(keyBase64); }
        catch (FormatException e) { throw new ArgumentException("Invalid audit encryption key", e); }
        if (key.Length != 32) throw new ArgumentException("Audit encryption key must be 32 bytes");
        if (mode != "free" && mode != "practice") throw new ArgumentException("Invalid mode");
        _connectionString = connectionString; _key = key; _mode = mode;
    }

    private static byte[] AssociatedData(Guid matchId, ulong round, string commitment) =>
        Encoding.UTF8.GetBytes(FormattableString.Invariant(
            $"HOKM-E04-PROOF-v1\0{matchId:N}:{round}:{commitment}"));

    public void Append(Guid roomId, CommandV1 command, int? publicValue, E04Round? preparedRound)
    {
        if (roomId == Guid.Empty) throw new GameRuleException("Missing match ID");
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();
        if (command.Kind == "round-started")
        {
            if (preparedRound == null || publicValue != (int)preparedRound.RoundNumber)
                throw new GameRuleException("A round event requires a prepared proof");
            using var create = new NpgsqlCommand("""
                INSERT INTO recorded_matches(id,ruleset_version,game_mode,started_at)
                VALUES (@id,@ruleset,@mode,@at) ON CONFLICT(id) DO NOTHING
                """, connection, tx);
            create.Parameters.AddWithValue("id", roomId);
            create.Parameters.AddWithValue("ruleset", command.RulesetVersion);
            create.Parameters.AddWithValue("mode", _mode);
            create.Parameters.AddWithValue("at", command.Timestamp);
            create.ExecuteNonQuery();
        }
        using (var lockVersion = new NpgsqlCommand(
            "SELECT audit_sequence FROM recorded_matches WHERE id=@id FOR UPDATE", connection, tx))
        {
            lockVersion.Parameters.AddWithValue("id", roomId);
            var version = lockVersion.ExecuteScalar();
            if (version == null || (long)version != command.StateVersion ||
                command.Sequence != command.StateVersion + 1)
                throw new GameRuleException("Stale or missing durable match version");
        }
        using (var insert = new NpgsqlCommand("""
            INSERT INTO match_events_v1(match_id,sequence,action_id,actor_seat,ruleset_version,
                happened_at,state_version,kind,public_value)
            VALUES (@id,@sequence,@action,@seat,@ruleset,@at,@version,@kind,@value)
            """, connection, tx))
        {
            insert.Parameters.AddWithValue("id", roomId);
            insert.Parameters.AddWithValue("sequence", command.Sequence);
            insert.Parameters.AddWithValue("action", command.ActionId);
            insert.Parameters.AddWithValue("seat", (short)command.ActorSeat);
            insert.Parameters.AddWithValue("ruleset", command.RulesetVersion);
            insert.Parameters.AddWithValue("at", command.Timestamp);
            insert.Parameters.AddWithValue("version", command.Sequence);
            insert.Parameters.AddWithValue("kind", command.Kind);
            insert.Parameters.Add("value", NpgsqlDbType.Integer).Value = (object?)publicValue ?? DBNull.Value;
            insert.ExecuteNonQuery();
        }
        if (preparedRound != null) StoreProof(connection, tx, roomId, preparedRound);
        using (var update = new NpgsqlCommand(
            "UPDATE recorded_matches SET audit_sequence=@version WHERE id=@id", connection, tx))
        {
            update.Parameters.AddWithValue("id", roomId);
            update.Parameters.AddWithValue("version", command.Sequence);
            update.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private void StoreProof(NpgsqlConnection c, NpgsqlTransaction tx, Guid matchId, E04Round round)
    {
        var plaintext = new byte[48];
        Buffer.BlockCopy(round.Seed, 0, plaintext, 0, 32);
        Buffer.BlockCopy(round.Nonce, 0, plaintext, 32, 16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[48]; var tag = new byte[16];
        try
        {
            using (var aes = new AesGcm(_key, 16))
                aes.Encrypt(nonce, plaintext, ciphertext, tag,
                    AssociatedData(matchId, round.RoundNumber, round.CommitmentHex));
            using var insert = new NpgsqlCommand("""
                INSERT INTO round_proofs_v1(match_id,round_number,shuffle_algorithm_id,commitment_hex,
                    cut_index,encryption_nonce,ciphertext,auth_tag,expires_at)
                VALUES (@id,@round,@algorithm,@commitment,@cut,@nonce,@ciphertext,@tag,@expires)
                """, c, tx);
            insert.Parameters.AddWithValue("id", matchId);
            insert.Parameters.AddWithValue("round", checked((long)round.RoundNumber));
            insert.Parameters.AddWithValue("algorithm", E04Shuffle.AlgorithmId);
            insert.Parameters.AddWithValue("commitment", round.CommitmentHex);
            insert.Parameters.AddWithValue("cut", (short)round.CutIndex);
            insert.Parameters.AddWithValue("nonce", nonce);
            insert.Parameters.AddWithValue("ciphertext", ciphertext);
            insert.Parameters.AddWithValue("tag", tag);
            insert.Parameters.AddWithValue("expires", DateTimeOffset.UtcNow.AddDays(30));
            insert.ExecuteNonQuery();
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    // Internal auditor-only operation. Never call this from a public route or live analytics.
    // A committed round-closed event proves final state was persisted before any reveal.
    internal (byte[] Seed, byte[] Nonce, string Commitment, int CutIndex) ReadClosedProof(Guid matchId, long round)
    {
        using var c = new NpgsqlConnection(_connectionString); c.Open();
        using var cmd = new NpgsqlCommand("""
            SELECT p.encryption_nonce,p.ciphertext,p.auth_tag,p.commitment_hex,p.cut_index
            FROM round_proofs_v1 p
            WHERE p.match_id=@id AND p.round_number=@round AND p.expires_at>now()
              AND EXISTS (SELECT 1 FROM match_events_v1 e WHERE e.match_id=p.match_id
                  AND e.kind='round-closed' AND e.public_value=@round)
            """, c);
        cmd.Parameters.AddWithValue("id", matchId); cmd.Parameters.AddWithValue("round", round);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new GameRuleException("Round proof not closed or expired");
        var nonce = reader.GetFieldValue<byte[]>(0); var ciphertext = reader.GetFieldValue<byte[]>(1);
        var tag = reader.GetFieldValue<byte[]>(2); var commitment = reader.GetString(3).Trim();
        var cut = reader.GetInt16(4); var plaintext = new byte[48];
        try
        {
            using (var aes = new AesGcm(_key, 16))
                aes.Decrypt(nonce, ciphertext, tag, plaintext,
                    AssociatedData(matchId, checked((ulong)round), commitment));
            var seed = new byte[32]; var sourceNonce = new byte[16];
            Buffer.BlockCopy(plaintext, 0, seed, 0, 32);
            Buffer.BlockCopy(plaintext, 32, sourceNonce, 0, 16);
            return (seed, sourceNonce, commitment, cut);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }
}
