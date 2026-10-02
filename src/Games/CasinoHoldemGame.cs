using System.Collections;
using System.Collections.Generic;
using CasinoExpansion.Casino;
using CasinoExpansion.Core;

namespace CasinoExpansion.Games
{
    // Casino Hold'em: hold'em played against the house rather than other players. Two cards
    // each plus a flop, then you either call for twice the ante or fold. Call and the turn and
    // river come out, and both sides make their best five from seven.
    //
    // The dealer qualifies with a pair of fours or better. Unlike Three Card Poker an
    // unqualified dealer does not just refund -- the ante pays and the call pushes, which with
    // the ante odds below is where most of the game's return comes from.
    //
    // House edge about 2.2%, the tightest game in the mod. That is fine: the slots measure
    // ~132%, so nothing here needs to claw anything back.
    public sealed class CasinoHoldemGame : ITableGame, IDecidingGame
    {
        public const string PlayerHand = "Player";
        public const string DealerHand = "Banker";
        public const string Board = "Board";

        public ETableGame Id => ETableGame.CasinoHoldem;
        public string Title => "Casino Hold'em";
        public BetRange Limits => new BetRange(10f, 50_000f);
        public string[] Sides => System.Array.Empty<string>();

        public void Deal(HandSet hands, Deck deck)
        {
            var player = hands.Add(PlayerHand);
            var dealer = hands.Add(DealerHand);
            var board = hands.Add(Board);

            player.Add(deck.Draw());
            dealer.Add(deck.Draw());
            player.Add(deck.Draw());
            dealer.Add(deck.Draw());

            for (int i = 0; i < 3; i++) board.Add(deck.Draw());
        }

        public IEnumerator Decide(TableSession session, HandSet hands, Deck deck, Wager wager)
        {
            var player = hands[PlayerHand];
            var board = hands[Board];

            var sofar = new List<Card>(player.Cards);
            sofar.AddRange(board.Cards);

            int choice = 0;
            yield return session.Ask(
                $"Flop {board} — you have {PokerHands.Best(sofar)}. Call ${wager.Opening * 2f:N0}?",
                new[] { "Call", "Fold" },
                i => choice = i);

            if (choice != 0)
            {
                session.Announce("Folded — ante lost.");
                yield break;
            }

            // The call is twice the ante. Taken before the turn and river are shown, so the
            // player is committing on the flop exactly as the rules intend.
            if (!wager.Add(wager.Opening * 2f, amount => session.TakeRaise(amount)))
            {
                session.Announce("Not enough cash to call — ante lost.");
                yield break;
            }

            board.Add(deck.Draw());
            yield return session.Show(hands);
            board.Add(deck.Draw());
            yield return session.Show(hands);
        }

        // Ante odds. Paid on the ante only, and only when the player wins; the call bet is
        // always even money. These are the standard Casino Hold'em ante paytable.
        private static float AnteOdds(PokerHands.Score hand) => hand.Rank switch
        {
            EHandRank.StraightFlush => 20f,     // royal flush pays 100 in some houses; not here
            EHandRank.Quads => 10f,
            EHandRank.FullHouse => 3f,
            EHandRank.Flush => 2f,
            _ => 1f,
        };

        public Outcome Resolve(HandSet hands, Wager wager, int side)
        {
            var board = hands[Board];
            var player = hands[PlayerHand];
            var dealer = hands[DealerHand];

            if (wager.Extra <= 0f)
                return new Outcome(0f, $"Folded on {board}.");

            var mineCards = new List<Card>(player.Cards); mineCards.AddRange(board.Cards);
            var theirCards = new List<Card>(dealer.Cards); theirCards.AddRange(board.Cards);

            var mine = PokerHands.Best(mineCards);
            var theirs = PokerHands.Best(theirCards);

            string detail = $"Board {board}. You {mine} ({player})  vs  dealer {theirs} ({dealer})";

            // Everything from a pair of fours up qualifies. A pair below that, or no pair at
            // all, does not -- so the check is the pair's rank, not merely having one.
            bool qualifies = theirs.Rank > EHandRank.Pair
                          || (theirs.Rank == EHandRank.Pair && PairRank(theirs) >= 4);

            float ante = wager.Opening;
            float call = wager.Extra;
            float total = wager.Total;

            if (!qualifies)
            {
                // Ante pays its odds, the call pushes. Still settled on the hands, because a
                // player holding quads should be paid for them whether or not the dealer
                // bothered to qualify.
                float back = ante + ante * AnteOdds(mine) + call;
                return new Outcome(back / total, $"Dealer does not qualify — ante pays. {detail}");
            }

            if (mine.Value > theirs.Value)
            {
                float back = ante + ante * AnteOdds(mine) + call * 2f;
                return new Outcome(back / total, $"You win with {mine}. {detail}");
            }

            if (mine.Value < theirs.Value)
                return new Outcome(0f, $"Dealer wins with {theirs}. {detail}");

            return new Outcome(1f, $"Tie — stakes returned. {detail}");
        }

        // The pair's rank sits in the kicker nibble directly below the category.
        private static int PairRank(PokerHands.Score score) => (score.Value >> 16) & 0xF;
    }
}
