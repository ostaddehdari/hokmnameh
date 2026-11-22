using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Hokmnameh.Core
{
    // One rules engine shared by ASP.NET match workers and Android LAN-hosted Unity games.
    // All card values are integers 0..51. Only the match authority owns hidden hands.
    public enum GamePhase { Waiting = 0, ChoosingTrump = 1, Playing = 2, Finished = 3 }
    public enum Suit { Spades = 0, Hearts = 1, Diamonds = 2, Clubs = 3 }

    [Serializable]
    public sealed class CardPlayed
    {
        public int seat;
        public int card;
        public CardPlayed() { }
        public CardPlayed(int seat, int card) { this.seat = seat; this.card = card; }
    }

    [Serializable]
    public sealed class GameSnapshot
    {
        public int phase;
        public int yourSeat;
        public int hakem;
        public int turn;
        public int trump = -1;
        public int round;
        public int[] hand = new int[0];
        public int[] handCounts = new int[4];
        public int[] tricksWon = new int[2];
        public int[] matchPoints = new int[2];
        public CardPlayed[] trick = new CardPlayed[0];
        public CardPlayed[] lastTrick = new CardPlayed[0];
        public int lastTrickWinner = -1;
        public int winnerTeam = -1;
        public int ledSuit = -1;
        public string ruleset = ClassicRules.ImmutableId;
    }

    public sealed class GameRuleException : Exception
    {
        public GameRuleException(string message) : base(message) { }
    }

    public static class ClassicRules
    {
        public const string ImmutableId = "iranian-hokm-classic-v1";
        public const int PreliminaryCardsPerSeat = 5;
        public const int CardsPerSeat = 13;
        public const int TricksToWinRound = 7;
        public const int MatchTargetPoints = 7;

        public static int RoundPoints(int winningTeam, int hakemTeam, int losingTeamTricks)
        {
            if (winningTeam < 0 || winningTeam > 1) throw new ArgumentOutOfRangeException("winningTeam");
            if (hakemTeam < 0 || hakemTeam > 1) throw new ArgumentOutOfRangeException("hakemTeam");
            if (losingTeamTricks < 0 || losingTeamTricks > 6) throw new ArgumentOutOfRangeException("losingTeamTricks");
            if (losingTeamTricks != 0) return 1;
            return winningTeam == hakemTeam ? 2 : 3;
        }

        public static int NextHakem(int currentHakem, int winningTeam)
        {
            if (currentHakem < 0 || currentHakem > 3) throw new ArgumentOutOfRangeException("currentHakem");
            if (winningTeam < 0 || winningTeam > 1) throw new ArgumentOutOfRangeException("winningTeam");
            return currentHakem % 2 == winningTeam ? currentHakem : (currentHakem + 1) % 4;
        }
    }

    public sealed class HokmGame
    {
        private readonly List<int>[] hands = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private readonly List<CardPlayed> trick = new List<CardPlayed>();
        private readonly List<CardPlayed> lastTrick = new List<CardPlayed>();
        private readonly Queue<int> undealt = new Queue<int>();
        private readonly int[] tricksWon = { 0, 0 };
        private readonly int[] points = { 0, 0 };
        private readonly HashSet<int> played = new HashSet<int>();
        private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();
        public GamePhase Phase { get; private set; } = GamePhase.Waiting;
        public int Hakem { get; private set; }
        public int Turn { get; private set; }
        public int Trump { get; private set; } = -1;
        public int Round { get; private set; }
        public int WinnerTeam { get; private set; } = -1;
        public int LastTrickWinner { get; private set; } = -1;
        public int[] Score { get { return (int[])points.Clone(); } }
        public int[] Tricks { get { return (int[])tricksWon.Clone(); } }
        public int CurrentTrickCount { get { return trick.Count; } }
        public static int SuitOf(int card) { return card / 13; }
        public static int RankOf(int card) { return (card % 13) + 2; }
        public static int TeamOf(int seat) { return seat % 2; }

        public void StartRound(int hakem)
        {
            if (hakem < 0 || hakem > 3) throw new ArgumentOutOfRangeException("hakem");
            if (Phase == GamePhase.Playing || Phase == GamePhase.ChoosingTrump) throw new GameRuleException("Round already active");
            if (Phase == GamePhase.Finished) throw new GameRuleException("Match finished");
            Hakem = hakem; Turn = hakem; Trump = -1; WinnerTeam = -1; LastTrickWinner = -1;
            Round++; trick.Clear(); lastTrick.Clear(); undealt.Clear(); played.Clear();
            tricksWon[0] = tricksWon[1] = 0;
            foreach (var h in hands) h.Clear();
            var deck = new int[52];
            for (int i = 0; i < 52; ++i) deck[i] = i;
            for (int i = deck.Length - 1; i > 0; --i)
            {
                int j = SecureInt(i + 1);
                int x = deck[i]; deck[i] = deck[j]; deck[j] = x;
            }
            // First five cards for every seat, starting from hakem; only then choose trump.
            for (int i = 0; i < 20; ++i) hands[(Hakem + i % 4) % 4].Add(deck[i]);
            for (int i = 20; i < 52; ++i) undealt.Enqueue(deck[i]);
            Phase = GamePhase.ChoosingTrump;
            AssertInvariant();
        }

        private int SecureInt(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0) throw new ArgumentOutOfRangeException("exclusiveUpperBound");
            var b = new byte[4];
            // Rejection sampling avoids modulo bias with cryptographic entropy.
            ulong range = (ulong)uint.MaxValue + 1UL;
            ulong limit = range - (range % (ulong)exclusiveUpperBound);
            uint value;
            do
            {
                random.GetBytes(b);
                value = BitConverter.ToUInt32(b, 0);
            } while ((ulong)value >= limit);
            return (int)((ulong)value % (ulong)exclusiveUpperBound);
        }

        public void ChooseTrump(int seat, int suit)
        {
            if (Phase != GamePhase.ChoosingTrump || seat != Hakem) throw new GameRuleException("Only hakem chooses trump now");
            if (suit < 0 || suit > 3) throw new GameRuleException("Invalid trump suit");
            Trump = suit;
            // Four remaining groups of eight. All four hands end with exactly thirteen cards.
            for (int i = 0; i < 32; ++i) hands[(Hakem + i % 4) % 4].Add(undealt.Dequeue());
            Phase = GamePhase.Playing; Turn = Hakem;
            AssertInvariant();
        }

        public int[] LegalCards(int seat)
        {
            if (seat < 0 || seat > 3) throw new ArgumentOutOfRangeException("seat");
            if (Phase != GamePhase.Playing) return new int[0];
            if (trick.Count == 0) return hands[seat].ToArray();
            int ledSuit = SuitOf(trick[0].card);
            var follow = hands[seat].FindAll(c => SuitOf(c) == ledSuit);
            return follow.Count > 0 ? follow.ToArray() : hands[seat].ToArray();
        }

        public void Play(int seat, int card)
        {
            if (Phase != GamePhase.Playing || seat != Turn) throw new GameRuleException("Not your turn");
            if (card < 0 || card >= 52 || !hands[seat].Contains(card)) throw new GameRuleException("Card not held");
            bool legal = false;
            foreach (int c in LegalCards(seat)) if (c == card) legal = true;
            if (!legal) throw new GameRuleException("Must follow led suit");
            hands[seat].Remove(card);
            if (!played.Add(card)) throw new InvalidOperationException("Duplicate played card");
            trick.Add(new CardPlayed(seat, card));
            if (trick.Count < 4) { Turn = (seat + 1) % 4; AssertInvariant(); return; }
            var winner = DetermineTrickWinner(trick, Trump);
            LastTrickWinner = winner;
            lastTrick.Clear();
            foreach (var move in trick) lastTrick.Add(new CardPlayed(move.seat, move.card));
            trick.Clear();
            tricksWon[TeamOf(winner)]++;
            if (tricksWon[TeamOf(winner)] >= ClassicRules.TricksToWinRound)
            {
                int winningTeam = TeamOf(winner);
                int increment = ClassicRules.RoundPoints(winningTeam, TeamOf(Hakem), tricksWon[1 - winningTeam]);
                points[winningTeam] += increment;
                if (points[winningTeam] >= ClassicRules.MatchTargetPoints) { WinnerTeam = winningTeam; Phase = GamePhase.Finished; }
                else
                {
                    Phase = GamePhase.Waiting;
                    Hakem = ClassicRules.NextHakem(Hakem, winningTeam);
                }
            }
            else Turn = winner;
            AssertInvariant();
        }

        public static int DetermineTrickWinner(IList<CardPlayed> cards, int trump)
        {
            if (cards == null || cards.Count != 4) throw new GameRuleException("Expected four cards");
            int lead = SuitOf(cards[0].card), bestSeat = -1, best = -1;
            var seats = new HashSet<int>();
            foreach (var p in cards)
            {
                if (!seats.Add(p.seat)) throw new GameRuleException("Duplicate seat in trick");
                int power = SuitOf(p.card) == trump ? 100 + RankOf(p.card)
                    : SuitOf(p.card) == lead ? 20 + RankOf(p.card) : 0;
                if (power > best) { best = power; bestSeat = p.seat; }
            }
            return bestSeat;
        }

        public GameSnapshot Snapshot(int seat)
        {
            if (seat < 0 || seat > 3) throw new ArgumentOutOfRangeException("seat");
            var v = new GameSnapshot();
            v.phase = (int)Phase; v.yourSeat = seat; v.hakem = Hakem; v.turn = Turn;
            v.trump = Trump; v.round = Round; v.winnerTeam = WinnerTeam;
            v.lastTrickWinner = LastTrickWinner;
            v.hand = hands[seat].ToArray();
            for (int i = 0; i < 4; i++) v.handCounts[i] = hands[i].Count;
            v.tricksWon = (int[])tricksWon.Clone(); v.matchPoints = (int[])points.Clone();
            v.trick = trick.ToArray(); v.lastTrick = lastTrick.ToArray();
            v.ledSuit = trick.Count == 0 ? -1 : SuitOf(trick[0].card);
            return v;
        }

        public int[] CardsInHandForAuthority(int seat) { return hands[seat].ToArray(); }

        public void AssertInvariant()
        {
            var seen = new HashSet<int>();
            int total = 0;
            foreach (var h in hands)
                foreach (var c in h)
                {
                    if (c < 0 || c >= 52 || !seen.Add(c)) throw new InvalidOperationException("Invalid/duplicate hand card");
                    total++;
                }
            foreach (var c in undealt)
            {
                if (!seen.Add(c)) throw new InvalidOperationException("Duplicate undealt card"); total++;
            }
            foreach (var c in played)
            {
                if (!seen.Add(c)) throw new InvalidOperationException("Duplicate played card"); total++;
            }
            if (Phase != GamePhase.Waiting || Round > 0)
                if (total != 52) throw new InvalidOperationException("Cards missing from round");
        }
    }
}
