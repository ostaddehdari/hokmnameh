using System;

namespace Hokmnameh.Core
{
    // Authority-side operation: the cutter supplies only an index, never a deck.
    public static class CardCut
    {
        public static int[] Rotate(int[] shuffledDeck, int cutIndex)
        {
            if (shuffledDeck == null) throw new ArgumentNullException("shuffledDeck");
            if (shuffledDeck.Length != 52) throw new ArgumentException("Expected a 52-card deck", "shuffledDeck");
            if (cutIndex < 1 || cutIndex > 51) throw new ArgumentOutOfRangeException("cutIndex");

            var seen = new bool[52];
            foreach (int card in shuffledDeck)
            {
                if (card < 0 || card >= 52 || seen[card])
                    throw new ArgumentException("Expected each card exactly once", "shuffledDeck");
                seen[card] = true;
            }

            var result = new int[52];
            Array.Copy(shuffledDeck, cutIndex, result, 0, 52 - cutIndex);
            Array.Copy(shuffledDeck, 0, result, 52 - cutIndex, cutIndex);
            return result;
        }
    }
}
