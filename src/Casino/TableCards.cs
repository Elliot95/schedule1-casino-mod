using System;
using System.Collections;
using MelonLoader;
using UnityEngine;
using CasinoExpansion.Core;
using Bj = Il2CppScheduleOne.Casino.BlackjackGameController;
using Controller = Il2CppScheduleOne.Casino.CasinoGameController;
using PlayingCard = Il2CppScheduleOne.Casino.PlayingCard;

namespace CasinoExpansion.Casino
{
    // Puts a mod game's hands onto the table as real cards, rather than describing them in text.
    //
    // The table already owns a pool of PlayingCard objects, the seat and dealer card positions,
    // and the glide/flip animation. What it does NOT own is which card comes next -- DrawCard
    // picks from the controller's own list, which is seeded independently on every client. So we
    // let the table allocate a card object, then stamp our deterministic face onto it before it
    // is handed to a hand. Every client runs the same seeded deck and therefore paints the same
    // table without a single card crossing the network.
    //
    // Placement goes through the RpcLogic___ bodies rather than the public wrappers. The public
    // ones are the RPC entry points: on a client they would be dropped (CasinoGamePlayers is
    // unowned), and on the host they would send a second copy to everyone who already dealt it
    // locally. The Logic body is the part that actually moves the card.
    public static class TableCards
    {
        public const string DealerHand = "Banker";

        public static bool Supported(Controller c) => c != null && c.TryCast<Bj>() != null;

        private static PlayingCard.ECardSuit Suit(Card c) => c.Suit switch
        {
            0 => PlayingCard.ECardSuit.Spades,
            1 => PlayingCard.ECardSuit.Hearts,
            2 => PlayingCard.ECardSuit.Diamonds,
            _ => PlayingCard.ECardSuit.Clubs,
        };

        private static PlayingCard.ECardValue Value(int rank) => rank switch
        {
            1 => PlayingCard.ECardValue.Ace,
            2 => PlayingCard.ECardValue.Two,
            3 => PlayingCard.ECardValue.Three,
            4 => PlayingCard.ECardValue.Four,
            5 => PlayingCard.ECardValue.Five,
            6 => PlayingCard.ECardValue.Six,
            7 => PlayingCard.ECardValue.Seven,
            8 => PlayingCard.ECardValue.Eight,
            9 => PlayingCard.ECardValue.Nine,
            10 => PlayingCard.ECardValue.Ten,
            11 => PlayingCard.ECardValue.Jack,
            12 => PlayingCard.ECardValue.Queen,
            _ => PlayingCard.ECardValue.King,
        };

        public static void Clear(Controller c)
        {
            var bj = c?.TryCast<Bj>();
            if (bj == null) return;
            try { bj.ResetCards(); }
            catch (Exception e) { MelonLogger.Warning($"[cards] reset failed: {e.Message}"); }
        }

        // The seat the local player is sitting in. Card positions are indexed by seat, so a
        // hand dealt to the wrong index lands on someone else's felt.
        public static int LocalSeat(Controller c)
        {
            try
            {
                var players = c.Players;
                if (players == null) return 0;
                for (int i = 0; i < players.CurrentPlayerCount; i++)
                {
                    var p = players.GetPlayer(i);
                    if (p != null && p.IsLocalPlayer) return i;
                }
            }
            catch { }
            return 0;
        }

        // Deals one card and returns once it has been placed. Face-up throughout: none of the
        // house-banked games in this mod have a hole card, and a face-down card the player can
        // never turn over just reads as a bug.
        public static bool Place(Controller c, int seat, bool toDealer, Card card)
        {
            var bj = c?.TryCast<Bj>();
            if (bj == null) return false;

            try
            {
                var playing = bj.DrawCard();
                if (playing == null) { MelonLogger.Warning("[cards] table ran out of card objects"); return false; }

                playing.SetCard(Suit(card), Value(card.Rank), true);
                playing.SetFaceUp(true, true);

                if (toDealer) bj.RpcLogic___AddCardToDealerHand_3615296227(playing.CardID);
                else bj.RpcLogic___AddCardToPlayerHand_2801973956(seat, playing.CardID);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[cards] could not place {card}: {e.Message}");
                return false;
            }
        }

        // Deals the whole set in the order a dealer would: one to each hand, then round again,
        // rather than a hand at a time. Third cards (baccarat draws) fall out naturally as the
        // longer hand simply has more rounds.
        // `from` gives a starting index per hand, so cards added by a mid-round decision can be
        // dealt without re-dealing what is already on the felt.
        public static IEnumerator DealOut(Controller c, HandSet hands, float gap = 0.32f, int[] from = null)
        {
            int seat = LocalSeat(c);
            int longest = 0;
            foreach (var h in hands.Hands) longest = Math.Max(longest, h.Cards.Count);

            for (int round = 0; round < longest; round++)
            {
                for (int hi = 0; hi < hands.Hands.Count; hi++)
                {
                    var hand = hands.Hands[hi];
                    if (from != null && hi < from.Length && round < from[hi]) continue;
                    if (round >= hand.Cards.Count) continue;

                    bool dealer = string.Equals(hand.Name, DealerHand, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(hand.Name, "Dealer", StringComparison.OrdinalIgnoreCase);

                    Place(c, seat, dealer, hand.Cards[round]);
                    yield return new WaitForSeconds(gap);
                }
            }
        }
    }
}
