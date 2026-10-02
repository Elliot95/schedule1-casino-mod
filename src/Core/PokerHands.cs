using System;
using System.Collections.Generic;

namespace CasinoExpansion.Core
{
    public enum EHandRank
    {
        HighCard = 0,
        Pair = 1,
        TwoPair = 2,
        Trips = 3,
        Straight = 4,
        Flush = 5,
        FullHouse = 6,
        Quads = 7,
        StraightFlush = 8,
    }

    // Five-card hand ranking, shared by Casino Hold'em, Three Card Poker's dealer side and Pai
    // Gow. Scores pack the category and every tiebreak into one int so comparing two hands is
    // an integer compare -- a "which is better" written per game is exactly the kind of thing
    // that silently pays the wrong side, and here it is written once.
    //
    // Score layout, high nibble first: rank, then five kicker ranks in descending significance.
    // Aces are 14 except in the wheel (A-2-3-4-5), where the straight scores as five-high.
    public static class PokerHands
    {
        public readonly struct Score : IComparable<Score>
        {
            public readonly EHandRank Rank;
            public readonly int Value;

            public Score(EHandRank rank, int value) { Rank = rank; Value = value; }

            public int CompareTo(Score other) => Value.CompareTo(other.Value);

            public override string ToString() => Rank switch
            {
                EHandRank.StraightFlush => "straight flush",
                EHandRank.Quads => "four of a kind",
                EHandRank.FullHouse => "full house",
                EHandRank.Flush => "flush",
                EHandRank.Straight => "straight",
                EHandRank.Trips => "three of a kind",
                EHandRank.TwoPair => "two pair",
                EHandRank.Pair => "a pair",
                _ => "high card",
            };
        }

        private static int Up(Card c) => c.Rank == 1 ? 14 : c.Rank;

        private static int Pack(EHandRank rank, params int[] kickers)
        {
            int v = (int)rank;
            for (int i = 0; i < 5; i++)
                v = (v << 4) | (i < kickers.Length ? kickers[i] : 0);
            return v;
        }

        public static Score Five(IReadOnlyList<Card> cards)
        {
            if (cards.Count != 5) throw new ArgumentException("Five() needs exactly five cards");

            var ranks = new List<int>(5);
            var suits = new List<int>(5);
            foreach (var c in cards) { ranks.Add(Up(c)); suits.Add(c.Suit); }
            ranks.Sort();
            ranks.Reverse();

            bool flush = suits[0] == suits[1] && suits[1] == suits[2]
                      && suits[2] == suits[3] && suits[3] == suits[4];

            int straightHigh = StraightHigh(ranks);

            // Counts, ordered by multiplicity then rank: this is what turns trips-over-pair
            // into a comparable kicker list without a special case per category.
            var counts = new Dictionary<int, int>();
            foreach (var r in ranks) counts[r] = counts.TryGetValue(r, out var n) ? n + 1 : 1;

            var groups = new List<KeyValuePair<int, int>>(counts);
            groups.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value)
                                                     : b.Key.CompareTo(a.Key));

            if (flush && straightHigh > 0) return new Score(EHandRank.StraightFlush, Pack(EHandRank.StraightFlush, straightHigh));
            if (groups[0].Value == 4) return new Score(EHandRank.Quads, Pack(EHandRank.Quads, groups[0].Key, groups[1].Key));
            if (groups[0].Value == 3 && groups[1].Value == 2) return new Score(EHandRank.FullHouse, Pack(EHandRank.FullHouse, groups[0].Key, groups[1].Key));
            if (flush) return new Score(EHandRank.Flush, Pack(EHandRank.Flush, ranks[0], ranks[1], ranks[2], ranks[3], ranks[4]));
            if (straightHigh > 0) return new Score(EHandRank.Straight, Pack(EHandRank.Straight, straightHigh));
            if (groups[0].Value == 3) return new Score(EHandRank.Trips, Pack(EHandRank.Trips, groups[0].Key, groups[1].Key, groups[2].Key));
            if (groups[0].Value == 2 && groups[1].Value == 2) return new Score(EHandRank.TwoPair, Pack(EHandRank.TwoPair, groups[0].Key, groups[1].Key, groups[2].Key));
            if (groups[0].Value == 2) return new Score(EHandRank.Pair, Pack(EHandRank.Pair, groups[0].Key, groups[1].Key, groups[2].Key, groups[3].Key));

            return new Score(EHandRank.HighCard, Pack(EHandRank.HighCard, ranks[0], ranks[1], ranks[2], ranks[3], ranks[4]));
        }

        // Returns the straight's high card, or 0. Expects ranks sorted descending.
        private static int StraightHigh(List<int> ranks)
        {
            for (int i = 1; i < ranks.Count; i++)
                if (ranks[i] == ranks[i - 1]) return 0;      // a pair cannot be a straight

            if (ranks[0] - ranks[4] == 4) return ranks[0];

            // The wheel: ace plays low and the straight is five-high, not ace-high.
            if (ranks[0] == 14 && ranks[1] == 5 && ranks[4] == 2) return 5;
            return 0;
        }

        // Best five of any larger set -- seven for Casino Hold'em, seven for Pai Gow's high hand.
        public static Score Best(IReadOnlyList<Card> cards)
        {
            if (cards.Count < 5) throw new ArgumentException("Best() needs at least five cards");
            if (cards.Count == 5) return Five(cards);

            var best = new Score(EHandRank.HighCard, -1);
            var pick = new Card[5];
            int n = cards.Count;

            for (int a = 0; a < n - 4; a++)
            for (int b = a + 1; b < n - 3; b++)
            for (int c = b + 1; c < n - 2; c++)
            for (int d = c + 1; d < n - 1; d++)
            for (int e = d + 1; e < n; e++)
            {
                pick[0] = cards[a]; pick[1] = cards[b]; pick[2] = cards[c];
                pick[3] = cards[d]; pick[4] = cards[e];

                var score = Five(pick);
                if (score.Value > best.Value) best = score;
            }
            return best;
        }

        // Three-card tiers, on their own scale. A Three() score is only ever comparable with
        // another Three() score -- never with a Five(), which packs a different ladder.
        private static int Pack3(int tier, params int[] kickers)
        {
            int v = tier;
            for (int i = 0; i < 3; i++)
                v = (v << 4) | (i < kickers.Length ? kickers[i] : 0);
            return v;
        }

        // Three-card poker ranks differently from five: with only three cards a straight is
        // harder to make than a flush, and trips harder than a straight, so the order inverts.
        // Reusing the five-card evaluator here would pay flushes over straights -- wrong way round.
        public static Score Three(IReadOnlyList<Card> cards)
        {
            if (cards.Count != 3) throw new ArgumentException("Three() needs exactly three cards");

            var r = new List<int> { Up(cards[0]), Up(cards[1]), Up(cards[2]) };
            r.Sort();
            r.Reverse();

            bool flush = cards[0].Suit == cards[1].Suit && cards[1].Suit == cards[2].Suit;
            bool trips = r[0] == r[1] && r[1] == r[2];

            bool straight = !trips && (r[0] - r[2] == 2 && r[0] != r[1] && r[1] != r[2]);
            int high = r[0];
            if (!trips && r[0] == 14 && r[1] == 3 && r[2] == 2) { straight = true; high = 3; }

            // Scored on the three-card tier ladder, NOT the five-card one. Reusing the
            // five-card ordinals here scores a flush above a straight -- right for five cards,
            // backwards for three, and it silently pays the wrong side of a showdown.
            // Rank is still the readable category; only Value changes scale.
            if (straight && flush) return new Score(EHandRank.StraightFlush, Pack3(5, high));
            if (trips) return new Score(EHandRank.Trips, Pack3(4, r[0]));
            if (straight) return new Score(EHandRank.Straight, Pack3(3, high));
            if (flush) return new Score(EHandRank.Flush, Pack3(2, r[0], r[1], r[2]));

            if (r[0] == r[1]) return new Score(EHandRank.Pair, Pack3(1, r[0], r[2]));
            if (r[1] == r[2]) return new Score(EHandRank.Pair, Pack3(1, r[1], r[0]));

            return new Score(EHandRank.HighCard, Pack3(0, r[0], r[1], r[2]));
        }
    }
}
