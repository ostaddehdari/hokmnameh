using System;
using System.Security.Cryptography;
using System.Text;

namespace Hokmnameh.Core
{
    // Pure C# reference for approved DEC-2026-0006. Seed and shuffle key stay with the authority.
    public sealed class E04Round : IDisposable
    {
        internal readonly int[] Deck;
        internal readonly byte[] Seed;
        internal readonly byte[] Nonce;
        internal readonly Guid MatchId;
        internal readonly ulong RoundNumber;
        public readonly string CommitmentHex;
        public readonly int CutIndex;
        internal E04Round(int[] deck, string commitmentHex, int cutIndex, Guid matchId,
            ulong roundNumber, byte[] seed, byte[] nonce)
        {
            Deck = deck; CommitmentHex = commitmentHex; CutIndex = cutIndex;
            MatchId = matchId; RoundNumber = roundNumber;
            Seed = (byte[])seed.Clone(); Nonce = (byte[])nonce.Clone();
        }
        public void Dispose()
        {
            Array.Clear(Seed, 0, Seed.Length); Array.Clear(Nonce, 0, Nonce.Length);
        }
    }

    public static class E04Shuffle
    {
        public const string AlgorithmId = "E04-HKDF-SHA256-HMACCTR-RS-FY-v1";
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private static byte[] Text(string s) { return Utf8.GetBytes(s); }
        private static byte[] Join(params byte[][] parts)
        {
            int length = 0; foreach (var p in parts) length += p.Length;
            var output = new byte[length]; int offset = 0;
            foreach (var p in parts) { Buffer.BlockCopy(p, 0, output, offset, p.Length); offset += p.Length; }
            return output;
        }
        private static byte[] Hash(byte[] value) { using (var sha = SHA256.Create()) return sha.ComputeHash(value); }
        private static byte[] Mac(byte[] key, byte[] value) { using (var h = new HMACSHA256(key)) return h.ComputeHash(value); }
        private static byte[] Be(ulong n, int size)
        {
            var bytes = new byte[size];
            for (int i = size - 1; i >= 0; --i) { bytes[i] = (byte)n; n >>= 8; }
            if (n != 0) throw new ArgumentOutOfRangeException("n");
            return bytes;
        }
        private static byte[] Label(string s) { return Join(Text(s), new byte[] { 0 }); }
        private static byte[] LengthPrefixed(string s)
        {
            var bytes = Text(s);
            if (bytes.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException("s");
            return Join(Be((ulong)bytes.Length, 2), bytes);
        }
        public static byte[] Context(Guid matchId, ulong roundNumber, string rulesetId)
        {
            if (matchId == Guid.Empty || roundNumber == 0 || rulesetId != ClassicRules.ImmutableId)
                throw new ArgumentException("Unsupported E04 context");
            var id = matchId.ToString("N"); var idBytes = new byte[16];
            for (int i = 0; i < 16; ++i) idBytes[i] = Convert.ToByte(id.Substring(i * 2, 2), 16);
            return Join(Label("HOKM-E04-CONTEXT"), new byte[] { 1 }, idBytes,
                Be(roundNumber, 8), LengthPrefixed(rulesetId), LengthPrefixed(AlgorithmId));
        }
        private static byte[] ShuffleKey(byte[] seed, byte[] context)
        {
            var salt = Hash(Text("hokmnameh/e04/hkdf-salt/v1"));
            var prk = Mac(salt, seed);
            var okm = Mac(prk, Join(Label("hokmnameh/e04/shuffle-key/v1"), context, new byte[] { 1 }));
            Array.Clear(prk, 0, prk.Length);
            return okm;
        }
        private static uint Word(byte[] block, int offset)
        {
            return ((uint)block[offset] << 24) | ((uint)block[offset + 1] << 16)
                | ((uint)block[offset + 2] << 8) | block[offset + 3];
        }
        private static int Uniform(uint word, int n)
        {
            ulong limit = (0x100000000UL / (ulong)n) * (ulong)n;
            return (ulong)word < limit ? (int)((ulong)word % (ulong)n) : -1;
        }
        public static E04Round Create(Guid matchId, ulong roundNumber, string rulesetId,
            byte[] seed, byte[] nonce, int? cutIndex = null)
        {
            if (seed == null || seed.Length != 32 || nonce == null || nonce.Length != 16)
                throw new ArgumentException("E04 requires 32-byte seed and 16-byte nonce");
            if (cutIndex.HasValue && (cutIndex < 1 || cutIndex > 51)) throw new ArgumentOutOfRangeException("cutIndex");
            var context = Context(matchId, roundNumber, rulesetId);
            var commitment = Hash(Join(Label("hokmnameh/e04/commit/v1"), context, nonce, seed));
            var key = ShuffleKey(seed, context);
            try
            {
                var deck = new int[52]; for (int i = 0; i < 52; ++i) deck[i] = i;
                ulong counter = 1; byte[] block = new byte[0]; int position = 32;
                for (int i = 51; i > 0; --i)
                {
                    int j;
                    do
                    {
                        if (position >= 32)
                        {
                            block = Mac(key, Join(Label("hokmnameh/e04/stream/v1"), context, Be(counter++, 8)));
                            position = 0;
                        }
                        j = Uniform(Word(block, position), i + 1); position += 4;
                    } while (j < 0);
                    int swap = deck[i]; deck[i] = deck[j]; deck[j] = swap;
                }
                int cut = cutIndex ?? FallbackCut(key, context);
                var result = new int[52];
                for (int i = 0; i < 52; ++i) result[i] = deck[(i + cut) % 52];
                return new E04Round(result, BitConverter.ToString(commitment).Replace("-", "").ToLowerInvariant(),
                    cut, matchId, roundNumber, seed, nonce);
            }
            finally { Array.Clear(key, 0, key.Length); }
        }
        private static int FallbackCut(byte[] key, byte[] context)
        {
            var block = Mac(key, Join(Label("hokmnameh/e04/cut-fallback/v1"), context));
            for (int i = 0; i < block.Length; i += 4)
            {
                int value = Uniform(Word(block, i), 51);
                if (value >= 0) return value + 1;
            }
            throw new CryptographicException("E04 fallback entropy exhausted");
        }
        public static E04Round CreateRandom(Guid matchId, ulong roundNumber, string rulesetId)
        {
            var seed = new byte[32]; var nonce = new byte[16];
            try
            {
                using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(seed); rng.GetBytes(nonce); }
                return Create(matchId, roundNumber, rulesetId, seed, nonce);
            }
            finally { Array.Clear(seed, 0, seed.Length); Array.Clear(nonce, 0, nonce.Length); }
        }
    }
}
