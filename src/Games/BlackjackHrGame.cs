using System.Collections;
using System.Collections.Generic;
using CasinoExpansion.Casino;
using CasinoExpansion.Core;

namespace CasinoExpansion.Games
{
    // Blackjack, reimplemented. The rules are the vanilla ones; this exists because
    // BlackjackGameController's MinimumBet and MaximumBet are const-backed statics whose
    // setters crash the process when written, so the only way to raise the table limit is to
    // run the game ourselves.
    //
    // Unlike every other game in the mod this one has a decision LOOP rather than a single
    // choice, so it is the real test of the prompt machinery: the player keeps answering until
    // they stand or bust, then the dealer plays out.
    //
    // Dealer stands on all 17s, blackjack pays 3 to 2, double only on the opening two cards.
    // No split -- that needs two independent hands at one seat, and the table's card positions
    // are indexed per seat with no room for a second.
    public sealed class BlackjackHrGame : ITableGame, IDecidingGame
    {
        public const string PlayerHand = "Player";
        public const string DealerHand = "Banker";

        public ETableGame Id => ETableGame.BlackjackHR;
        public string Title => "Blackjack (high roller)";
        public BetRange Limits => new BetRange(10f, 50_000f);
        public string[] Sides => System.Array.Empty<string>();

        public void Deal(HandSet hands, Deck deck)
        {
            var player = hands.Add(PlayerHand);
            var dealer = hands.Add(DealerHand);

            player.Add(deck.Draw());
            dealer.Add(deck.Draw());
            player.Add(deck.Draw());
            dealer.Add(deck.Draw());
        }

        // Aces count eleven until that would bust, then one. Counting them as one and adding
        // ten back is the version that handles several aces without a special case.
        public static int Total(IReadOnlyList<Card> cards)
        {
            int sum = 0, aces = 0;
            foreach (var c in cards)
            {
                int v = c.Rank == 1 ? 1 : (c.Rank >= 10 ? 10 : c.Rank);
                if (c.Rank == 1) aces++;
                sum += v;
            }
            while (aces > 0 && sum + 10 <= 21) { sum += 10; aces--; }
            return sum;
        }

        private static bool IsBlackjack(Hand hand) => hand.Cards.Count == 2 && Total(hand.Cards) == 21;

        public IEnumerator Decide(TableSession session, HandSet hands, Deck deck, Wager wager)
        {
            var player = hands[PlayerHand];
            var dealer = hands[DealerHand];

            // A natural on either side ends it before anyone acts.
            if (IsBlackjack(player) || IsBlackjack(dealer)) yield break;

            bool first = true;
            while (Total(player.Cards) < 21)
            {
                int total = Total(player.Cards);
                var options = first && wager.Extra <= 0f
                    ? new[] { "Hit", "Stand", "Double" }
                    : new[] { "Hit", "Stand" };

                int choice = 1;
                yield return session.Ask(
                    $"You have {total} — dealer shows {Face(dealer.Cards[0])}",
                    options,
                    i => choice = i);

                first = false;

                if (choice == 1) break;                       // stand

                if (choice == 2)
                {
                    // Double: one more card and the hand is over, win or lose.
                    if (wager.Add(wager.Opening, amount => session.TakeRaise(amount)))
                    {
                        player.Add(deck.Draw());
                        yield return session.Show(hands);
                        break;
                    }
                    session.Announce("Not enough cash to double.");
                    continue;
                }

                player.Add(deck.Draw());
                yield return session.Show(hands);
            }

            if (Total(player.Cards) > 21) yield break;        // bust, dealer need not play

            // Dealer draws to 17. Done here rather than in Resolve because Resolve is pure and
            // must not touch the deck -- every client replays this identically from the seed.
            while (Total(dealer.Cards) < 17)
            {
                dealer.Add(deck.Draw());
                yield return session.Show(hands);
            }
        }

        public Outcome Resolve(HandSet hands, Wager wager, int side)
        {
            var player = hands[PlayerHand];
            var dealer = hands[DealerHand];

            int mine = Total(player.Cards), theirs = Total(dealer.Cards);
            string detail = $"You {mine} ({player})  vs  dealer {theirs} ({dealer})";

            bool myBj = IsBlackjack(player), theirBj = IsBlackjack(dealer);

            if (myBj && theirBj) return new Outcome(1f, $"Both blackjack — push. {detail}");
            if (myBj) return new Outcome(2.5f, $"Blackjack — pays 3 to 2. {detail}");
            if (theirBj) return new Outcome(0f, $"Dealer blackjack. {detail}");

            if (mine > 21) return new Outcome(0f, $"Bust. {detail}");
            if (theirs > 21) return new Outcome(2f, $"Dealer busts. {detail}");

            if (mine > theirs) return new Outcome(2f, $"You win. {detail}");
            if (mine < theirs) return new Outcome(0f, $"Dealer wins. {detail}");
            return new Outcome(1f, $"Push. {detail}");
        }

        private static string Face(Card c) => c.Rank switch
        {
            1 => "an ace",
            11 => "a jack",
            12 => "a queen",
            13 => "a king",
            10 => "a ten",
            _ => $"a {c.Rank}",
        };
    }
}
