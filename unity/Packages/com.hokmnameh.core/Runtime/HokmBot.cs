using System;
using System.Collections.Generic;

namespace Hokmnameh.Core
{
    // Zero API tokens. Deterministic rules of thumb; expert level ISMCTS is a separate milestone.
    public static class HokmBot
    {
        public static int PickTrump(HokmGame game, int seat)
        {
            var cards = game.CardsInHandForAuthority(seat);
            var power = new int[4];
            foreach (int card in cards) power[HokmGame.SuitOf(card)] += 2 + HokmGame.RankOf(card) - 2;
            int best = 0;
            for (int suit = 1; suit < 4; ++suit) if (power[suit] > power[best]) best = suit;
            return best;
        }
        public static int ChooseCard(HokmGame game, int seat, int difficulty = 1)
        {
            int[] legal = game.LegalCards(seat);
            if (legal.Length == 0) throw new GameRuleException("No legal moves");
            Array.Sort(legal, (a, b) => HokmGame.RankOf(a).CompareTo(HokmGame.RankOf(b)));
            if (difficulty <= 0 || game.CurrentTrickCount == 0) return legal[0];
            var snap = game.Snapshot(seat);
            // Preserve stronger cards if partner is currently winning the trick.
            if (snap.trick.Length > 0)
            {
                int leadingSuit = HokmGame.SuitOf(snap.trick[0].card);
                int strongestSeat = snap.trick[0].seat;
                int strongestPower = -1;
                foreach (var p in snap.trick)
                {
                    int power = HokmGame.SuitOf(p.card) == snap.trump ? 100 + HokmGame.RankOf(p.card)
                        : HokmGame.SuitOf(p.card) == leadingSuit ? 20 + HokmGame.RankOf(p.card) : 0;
                    if (power > strongestPower) { strongestPower = power; strongestSeat = p.seat; }
                }
                if (HokmGame.TeamOf(strongestSeat) == HokmGame.TeamOf(seat)) return legal[0];
                foreach (int c in legal)
                {
                    int p = HokmGame.SuitOf(c) == snap.trump ? 100 + HokmGame.RankOf(c)
                        : HokmGame.SuitOf(c) == leadingSuit ? 20 + HokmGame.RankOf(c) : 0;
                    if (p > strongestPower) return c; // smallest winning card
                }
            }
            return legal[0];
        }
    }
}
