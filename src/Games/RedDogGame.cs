using System.Collections;
using System.Collections.Generic;
using CasinoExpansion.Casino;
using CasinoExpansion.Core;

namespace CasinoExpansion.Games
{
    // Red Dog (Acey-Deucey / In-Between). Two cards are turned; a third is dealt and the player
    // wins if it falls strictly between them. The wider the gap, the likelier that is, so the
    // payout shrinks as the spread grows.
    //
    // Chosen as the second game because it is the simplest one that still has a real decision:
    // after seeing the spread the player may double their stake. That decision is the whole
    // point -- it is the machinery Blackjack HR's hit/stand needs, built somewhere the rules
    // cannot obscure whether it works.
    //
    // House edge is about 3.2% on a single deck without raising, which is on the right side of
    // the line: the mod's other games must not out-pay the slots, which measure ~132%.
    public sealed class RedDogGame : ITableGame, IDecidingGame
    {
        public ETableGame Id => ETableGame.RedDog;
        public string Title => "Red Dog";
        public BetRange Limits => new BetRange(10f, 50_000f);

        // One wager only: the decision here is the raise, not which side to back.
        public int Decks => 1;
        public string[] Sides => System.Array.Empty<string>();

        private const string PlayerHand = "Table";

        // Ace is always high here, unlike baccarat. Ranks run 2..14 so a spread is plain
        // subtraction rather than a special case at either end.
        private static int Rank(Card c) => c.Rank == 1 ? 14 : c.Rank;

        public void Deal(HandSet hands, Deck deck)
        {
            var hand = hands.Add(PlayerHand);
            hand.Add(deck.Draw());
            hand.Add(deck.Draw());

            // A pair resolves immediately on a third card: match it and it pays 11 to 1,
            // otherwise the round is a push. There is nothing to decide, so it is dealt here
            // rather than waiting on a prompt the player cannot meaningfully answer.
            if (Rank(hand.Cards[0]) == Rank(hand.Cards[1]))
                hand.Add(deck.Draw());
        }

        public IEnumerator Decide(TableSession session, HandSet hands, Deck deck, Wager wager)
        {
            var hand = hands[PlayerHand];

            // Already settled by Deal: a pair, or consecutive cards with no gap to bet into.
            if (hand.Cards.Count > 2) yield break;

            int spread = Spread(hand.Cards[0], hand.Cards[1]);
            if (spread == 0)
            {
                session.Announce("Consecutive — push.");
                yield return session.Wait(1f);
                yield break;
            }

            int choice = 0;
            yield return session.Ask(
                $"Spread {spread} — pays {PayoutFor(spread)} to 1",
                new[] { "Raise", "Stand" },
                i => choice = i);

            if (choice == 0)
                wager.Add(wager.Opening, amount => session.TakeRaise(amount));

            hand.Add(deck.Draw());
        }

        public Outcome Resolve(HandSet hands, Wager wager, int side)
        {
            var cards = hands[PlayerHand].Cards;
            int a = Rank(cards[0]), b = Rank(cards[1]);

            if (a == b)
            {
                // Three of a kind. Anything else is a push -- the two matching cards were never
                // a bet the player could lose.
                if (cards.Count < 3) return new Outcome(1f, "Pair — push");
                return Rank(cards[2]) == a
                    ? new Outcome(12f, "Three of a kind — pays 11 to 1")
                    : new Outcome(1f, "Pair, no match — push");
            }

            int spread = Spread(cards[0], cards[1]);
            if (spread == 0) return new Outcome(1f, "Consecutive — push");

            int low = a < b ? a : b, high = a < b ? b : a;
            int third = Rank(cards[2]);

            if (third > low && third < high)
            {
                int pays = PayoutFor(spread);
                return new Outcome(1f + pays, $"{Name(third)} is inside — pays {pays} to 1");
            }

            return new Outcome(0f, $"{Name(third)} is outside {Name(low)}–{Name(high)}");
        }

        // Cards between the two, exclusive. Consecutive cards leave nothing to land on.
        private static int Spread(Card x, Card y)
        {
            int d = Rank(x) - Rank(y);
            if (d < 0) d = -d;
            return d - 1;
        }

        private static int PayoutFor(int spread) => spread switch
        {
            1 => 5,
            2 => 4,
            3 => 2,
            _ => 1,
        };

        private static readonly string[] Names =
            { "", "", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };

        private static string Name(int rank) => Names[rank];
    }
}
