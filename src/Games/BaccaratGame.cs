using UnityEngine;
using CasinoExpansion.Casino;
using CasinoExpansion.Core;

namespace CasinoExpansion.Games
{
    // Punto banco: no decisions, the draw is entirely dictated by the rules, which is why it is
    // the right first game -- it exercises the whole round loop while the rules themselves are
    // almost trivial.
    public sealed class BaccaratGame : ITableGame
    {
        public const string PlayerHand = "Player";
        public const string BankerHand = "Banker";

        public ETableGame Id => ETableGame.Baccarat;
        public string Title => "Baccarat";
        public BetRange Limits => new BetRange(10f, 50000f);
        public string[] Sides => new[] { PlayerHand, BankerHand, "Tie" };

        // Totals count modulo 10: tens and faces are worth nothing, aces one.
        public static int Value(Card c) => c.Rank >= 10 ? 0 : c.Rank;

        public static int Total(Hand hand)
        {
            int sum = 0;
            foreach (var c in hand.Cards) sum += Value(c);
            return sum % 10;
        }

        public void Deal(HandSet hands, Deck deck)
        {
            var player = hands.Add(PlayerHand);
            var banker = hands.Add(BankerHand);

            player.Add(deck.Draw());
            banker.Add(deck.Draw());
            player.Add(deck.Draw());
            banker.Add(deck.Draw());

            int p = Total(player), b = Total(banker);

            // A natural ends the hand immediately; neither side draws.
            if (p >= 8 || b >= 8) return;

            Card? playerThird = null;
            if (p <= 5)
            {
                playerThird = deck.Draw();
                player.Add(playerThird.Value);
            }

            if (ShouldBankerDraw(b, playerThird)) banker.Add(deck.Draw());
        }

        // The banker's draw depends on the player's third card, which is the only genuinely
        // fiddly part of baccarat and the one place a mistake would be invisible.
        private static bool ShouldBankerDraw(int bankerTotal, Card? playerThird)
        {
            if (playerThird == null) return bankerTotal <= 5;

            int t = Value(playerThird.Value);
            return bankerTotal switch
            {
                <= 2 => true,
                3 => t != 8,
                4 => t >= 2 && t <= 7,
                5 => t >= 4 && t <= 7,
                6 => t == 6 || t == 7,
                _ => false,      // 7 stands, 8 and 9 are naturals and never reach here
            };
        }

        // Player pays even money. Banker also pays even money but the house takes 5% of the
        // win, which is what stops the better-than-even banker hand from being a free lunch --
        // it wins about 51% of decided hands, so without the commission the player edge would
        // be positive. Tie pays 8 to 1 and is the worst bet on the table by a distance; it is
        // offered because leaving it out would be the odd omission, not because it is wise.
        public Outcome Resolve(HandSet hands, float stake, int side)
        {
            var player = hands[PlayerHand];
            var banker = hands[BankerHand];
            int p = Total(player), b = Total(banker);

            string detail = $"Player {p} ({player})  vs  Banker {b} ({banker})";
            string winner = p > b ? PlayerHand : p < b ? BankerHand : "Tie";
            string backed = Sides[Mathf.Clamp(side, 0, Sides.Length - 1)];

            if (winner == "Tie")
                return backed == "Tie"
                    ? new Outcome(9f, $"Tie — pays 8 to 1. {detail}")
                    : new Outcome(1f, $"Tie — {backed} bets push. {detail}");

            if (backed == "Tie") return new Outcome(0f, $"{winner} wins, no tie. {detail}");
            if (backed != winner) return new Outcome(0f, $"{winner} wins. {detail}");

            return backed == BankerHand
                ? new Outcome(1.95f, $"Banker wins — less 5% commission. {detail}")
                : new Outcome(2f, $"Player wins. {detail}");
        }
    }
}
