using System;

namespace Hokmnameh.Core
{
    // Approved DEC-2026-0006. An immutable plan over one already shuffled and cut deck.
    public static class E04DealPlan
    {
        public const string Version = "E04-DEAL-5-4-4-CCW-v1";
        private static readonly int[] Packets = { 5, 4, 4 };
        private static readonly int[] Cursors = { 20, 36, 52 };
        public static int PacketSize(int stage) => stage >= 0 && stage < 3 ? Packets[stage] : throw new ArgumentOutOfRangeException(nameof(stage));
        public static int Cursor(int stage) => stage >= 0 && stage < 3 ? Cursors[stage] : throw new ArgumentOutOfRangeException(nameof(stage));
        public static int Seat(int hakem, int offset) => hakem >= 0 && hakem < 4 && offset >= 0 && offset < 4
            ? (hakem - offset + 4) % 4 : throw new ArgumentOutOfRangeException();
    }
}
