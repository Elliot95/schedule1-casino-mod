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

        // Betting the player hand: even money, a push on a tie. Banker bets and the tie bet are
        // deliberately left out of this first pass -- they need a bet-type selector, and the
        // banker's 5% commission is exactly the sort of detail worth doing properly rather than
        // bolting on.
        public Outcome Resolve(HandSet hands, float stake)
        {
            var player = hands[PlayerHand];
            var banker = hands[BankerHand];
            int p = Total(player), b = Total(banker);

            string detail = $"Player {p} ({player})  vs  Banker {b} ({banker})";

            if (p > b) return new Outcome(2f, $"Player wins. {detail}");
            if (p < b) return new Outcome(0f, $"Banker wins. {detail}");
            return new Outcome(1f, $"Tie, stake returned. {detail}");
        }
    }
}
