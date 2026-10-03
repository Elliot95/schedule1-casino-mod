using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CasinoExpansion.Casino;
using CasinoExpansion.Core;

namespace CasinoExpansion.Games
{
    // Pai Gow Poker. Seven cards each, split into a five-card high hand and a two-card low
    // hand, with the rule that the low hand must rank below the high one. Beat the dealer on
    // both to win; split one each and the hand pushes, which is why pai gow pushes so often.
    //
    // Both sides are set by the house way rather than by hand. Setting seven cards is a real
    // decision, but a good UI for it means dragging cards between two rows, and this panel is
    // cloned buttons on a bet plate -- a "swap cards 3 and 6" prompt would be worse than no
    // choice at all. The house way is also very close to optimal, so little is lost.
    //
    // Chosen rules: no joker (a 52-card deck, which is what Deck gives us), dealer wins copies,
    // and a win pays 98% rather than even money, which is where the edge sits in a game
    // that pushes as often as this one.
    public sealed class PaiGowGame : ITableGame, IDecidingGame
    {
        public const string PlayerHigh = "Player";
        public const string PlayerLow = "Player low";
        public const string DealerHigh = "Banker";
        public const string DealerLow = "Banker low";

        public ETableGame Id => ETableGame.PaiGow;
        public string Title => "Pai Gow Poker";
        public BetRange Limits => new BetRange(10f, 50_000f);
        public int Decks => 1;
        public string[] Sides => System.Array.Empty<string>();

        public void Deal(HandSet hands, Deck deck)
        {
            // The player's seven arrive as one hand. Setting them is the decision, so it would
            // give the game away to split them before they have been seen.
            var mine = hands.Add(PlayerHigh);
            var theirs = new List<Card>();
            for (int i = 0; i < 7; i++) { mine.Add(deck.Draw()); theirs.Add(deck.Draw()); }

            Set(hands, theirs, DealerHigh, DealerLow);
        }

        // Setting the hand is the real decision in pai gow, and a table does it by moving cards
        // between two rows. There is no dragging here, so the choice is offered the other way
        // round: the legal sets are worked out and the player picks one, named by the two cards
        // it puts in front. The house way is first and the default, so doing nothing plays the
        // hand the way a dealer would.
        public IEnumerator Decide(TableSession session, HandSet hands, Deck deck, Wager wager)
        {
            var seven = new List<Card>(hands[PlayerHigh].Cards);
            var options = Splits(seven, 4);

            int pick = 0;
            if (options.Count > 1)
            {
                var labels = new string[options.Count];
                for (int i = 0; i < options.Count; i++)
                    labels[i] = $"{Name(options[i].Low[0])} {Name(options[i].Low[1])}";

                yield return session.Ask("Set your hand — which two in front?", labels, i => pick = i, 25f, 0);
            }

            var chosen = options[Mathf.Clamp(pick, 0, options.Count - 1)];

            hands[PlayerHigh].Cards.Clear();
            foreach (var c in chosen.High) hands[PlayerHigh].Add(c);

            var low = hands.Add(PlayerLow);
            foreach (var c in chosen.Low) low.Add(c);

            yield return session.Relayout(hands);
        }

        private sealed class Split
        {
            public List<Card> High, Low;
            public int LowScore;
        }

        // Every legal way to split the seven, best low hand first, de-duplicated by the pair of
        // cards in front. A split whose two-card hand outranks its five-card hand is a foul and
        // is never offered.
        private static List<Split> Splits(List<Card> seven, int limit)
        {
            var all = new List<Split>();
            var seen = new HashSet<int>();

            for (int a = 0; a < 7; a++)
            for (int b = a + 1; b < 7; b++)
            {
                var low = new List<Card> { seven[a], seven[b] };
                var high = new List<Card>(5);
                for (int i = 0; i < 7; i++) if (i != a && i != b) high.Add(seven[i]);

                int lowScore = TwoCard(low);
                if (lowScore >= FiveAsTwo(PokerHands.Five(high))) continue;
                if (!seen.Add(lowScore)) continue;

                all.Add(new Split { High = high, Low = low, LowScore = lowScore });
            }

            all.Sort((x, y) => y.LowScore.CompareTo(x.LowScore));
            if (all.Count > limit) all.RemoveRange(limit, all.Count - limit);

            if (all.Count == 0)
                all.Add(new Split { High = seven.GetRange(2, 5), Low = seven.GetRange(0, 2) });

            return all;
        }

        private static string Name(Card c) =>
            $"{"A23456789TJQK"[c.Rank - 1]}{"SHDC"[c.Suit]}";

        // The house way, simplified to its load-bearing rule: of every legal way to split seven
        // cards, take the one with the strongest two-card low hand that still ranks below the
        // high hand. That is what the published house ways amount to in all but a handful of
        // edge cases, and unlike a table of special cases it cannot be subtly mis-transcribed.
        private static void Set(HandSet hands, List<Card> seven, string highName, string lowName)
        {
            var bestHigh = new List<Card>();
            var bestLow = new List<Card>();
            int bestLowScore = -1;

            for (int a = 0; a < 7; a++)
            for (int b = a + 1; b < 7; b++)
            {
                var low = new List<Card> { seven[a], seven[b] };
                var high = new List<Card>(7);
                for (int i = 0; i < 7; i++) if (i != a && i != b) high.Add(seven[i]);

                var highScore = PokerHands.Five(high);
                int lowScore = TwoCard(low);

                // The low hand must not outrank the high hand -- a "foul" in pai gow, which
                // would surrender the hand outright.
                if (lowScore >= FiveAsTwo(highScore)) continue;

                if (lowScore > bestLowScore)
                {
                    bestLowScore = lowScore;
                    bestLow = low;
                    bestHigh = high;
                }
            }

            // Only possible if every split fouls, which cannot happen with five cards behind a
            // pair, but guard anyway rather than hand the table an empty hand.
            if (bestHigh.Count == 0)
            {
                bestLow = new List<Card> { seven[0], seven[1] };
                bestHigh = seven.GetRange(2, 5);
            }

            var highHand = hands.Add(highName);
            foreach (var c in bestHigh) highHand.Add(c);

            var lowHand = hands.Add(lowName);
            foreach (var c in bestLow) lowHand.Add(c);
        }

        // Two cards rank as a pair or as high cards. Packed the same way as PokerHands so the
        // two are comparable, which is what the foul check needs.
        private static int TwoCard(List<Card> two)
        {
            int x = Up(two[0]), y = Up(two[1]);
            int hi = x > y ? x : y, lo = x > y ? y : x;
            return x == y ? (1 << 16) | (hi << 8) : (hi << 8) | lo;
        }

        // A five-card hand always outranks any two-card hand except when the five-card hand is
        // a bare high card, where the comparison is between top cards. Collapsing it this way
        // keeps the foul test a single integer compare.
        private static int FiveAsTwo(PokerHands.Score score) =>
            score.Rank > EHandRank.HighCard ? int.MaxValue : (1 << 16);

        private static int Up(Card c) => c.Rank == 1 ? 14 : c.Rank;

        public Outcome Resolve(HandSet hands, Wager wager, int side)
        {
            var myHigh = PokerHands.Five(hands[PlayerHigh].Cards);
            var theirHigh = PokerHands.Five(hands[DealerHigh].Cards);
            int myLow = TwoCard(hands[PlayerLow].Cards);
            int theirLow = TwoCard(hands[DealerLow].Cards);

            // Copies go to the dealer, which is where a large part of the house edge lives.
            bool highWon = myHigh.Value > theirHigh.Value;
            bool lowWon = myLow > theirLow;

            string detail = $"High {myHigh} ({hands[PlayerHigh]}) vs {theirHigh} ({hands[DealerHigh]}); " +
                            $"low {hands[PlayerLow]} vs {hands[DealerLow]}";

            if (highWon && lowWon) return new Outcome(1.98f, $"Both hands win — pays 98%. {detail}");
            if (!highWon && !lowWon) return new Outcome(0f, $"Dealer takes both. {detail}");
            return new Outcome(1f, $"One each — push. {detail}");
        }
    }
}
