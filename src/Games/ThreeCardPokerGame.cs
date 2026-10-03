using System.Collections;
using CasinoExpansion.Casino;
using CasinoExpansion.Core;

namespace CasinoExpansion.Games
{
    // Three Card Poker, ante-and-play. Three cards each, and after seeing yours you either
    // match the ante to stay in or fold and lose it. The dealer needs queen-high to qualify;
    // below that the ante pays and the play bet pushes.
    //
    // Three-card ranking is not five-card ranking in miniature: with three cards a straight is
    // rarer than a flush and trips rarer than a straight, so the order inverts. That lives in
    // PokerHands.Three -- using the five-card evaluator here would pay flushes over straights.
    //
    // House edge about 3.4% on the ante, which sits in the band the rest of the mod's games use.
    public sealed class ThreeCardPokerGame : ITableGame, IDecidingGame
    {
        public const string PlayerHand = "Player";
        public const string DealerHand = "Banker";   // dealt to the table's dealer position

        public ETableGame Id => ETableGame.ThreeCardPoker;
        public string Title => "Three Card Poker";
        public BetRange Limits => new BetRange(10f, 50_000f);
        public int Decks => 1;
        public string[] Sides => System.Array.Empty<string>();

        public void Deal(HandSet hands, Deck deck)
        {
            var player = hands.Add(PlayerHand);
            var dealer = hands.Add(DealerHand);

            for (int i = 0; i < 3; i++)
            {
                player.Add(deck.Draw());
                dealer.Add(deck.Draw());
            }
        }

        public IEnumerator Decide(TableSession session, HandSet hands, Deck deck, Wager wager)
        {
            var mine = PokerHands.Three(hands[PlayerHand].Cards);

            int choice = 0;
            yield return session.Ask(
                $"You hold {mine} — play for another ${wager.Opening:N0}?",
                new[] { "Play", "Fold" },
                i => choice = i);

            if (choice == 0)
                wager.Add(wager.Opening, amount => session.TakeRaise(amount));
            else
                session.Announce("Folded — ante lost.");
        }

        // Folding is read straight off the wager: no play bet was taken, so Extra is zero.
        //
        // Multipliers are against the TOTAL staked, which makes the arithmetic look odd.
        // Folding stakes one unit and returns nothing. Playing stakes two, so an unqualified
        // dealer -- ante paid, play pushed, three units back -- is 1.5x, and a win is 2x.
        public Outcome Resolve(HandSet hands, Wager wager, int side)
        {
            var player = hands[PlayerHand];
            var dealer = hands[DealerHand];

            var mine = PokerHands.Three(player.Cards);
            var theirs = PokerHands.Three(dealer.Cards);

            string detail = $"You {mine} ({player})  vs  dealer {theirs} ({dealer})";

            if (wager.Extra <= 0f) return new Outcome(0f, $"Folded. {detail}");

            // The ante bonus. Paid on the ante for a straight or better whatever the dealer
            // holds -- it is not a wager against the dealer, it is a prize for the hand. Left
            // out of the first pass, which measured a 5% house edge against a real game's 2%;
            // this is most of that difference.
            float bonus = mine.Rank switch
            {
                EHandRank.StraightFlush => 5f,
                EHandRank.Trips => 4f,
                EHandRank.Straight => 1f,
                _ => 0f,
            };
            float ante = wager.Opening;
            float total = wager.Total;
            string bonusNote = bonus > 0f ? $" Ante bonus {bonus:0} to 1." : "";

            // Queen-high or better. A pair or anything above always clears the bar, so only a
            // high-card hand needs checking, and its top kicker sits in the second nibble.
            bool qualifies = theirs.Rank > EHandRank.HighCard || TopCard(dealer) >= 12;

            float extra = ante * bonus;

            if (!qualifies)
                return new Outcome((total * 1.5f + extra) / total,
                    $"Dealer does not qualify — ante pays.{bonusNote} {detail}");

            if (mine.Value > theirs.Value)
                return new Outcome((total * 2f + extra) / total, $"You win.{bonusNote} {detail}");

            if (mine.Value < theirs.Value)
                return new Outcome(extra / total, $"Dealer wins.{bonusNote} {detail}");

            return new Outcome((total + extra) / total, $"Tie — stakes returned.{bonusNote} {detail}");
        }

        private static int TopCard(Hand hand)
        {
            int top = 0;
            foreach (var c in hand.Cards)
            {
                int v = c.Rank == 1 ? 14 : c.Rank;
                if (v > top) top = v;
            }
            return top;
        }
    }
}
