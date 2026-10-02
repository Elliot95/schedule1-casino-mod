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

        // Puts the table into a real round before any card is dealt.
        //
        // This is what was missing while cards piled up on the deck: GetPlayerCardPositions
        // indexes playersInCurrentRound, and that list is filled by StartGame -- which we
        // suppress, because it would deal the vanilla game. So the table had seats, a deck and
        // card objects, but no player in the round and therefore nowhere to send a card.
        //
        // Every seated player is added on every client. The call is the RpcLogic body, so each
        // client builds the same round locally rather than relying on an RPC that an unowned
        // client cannot send.
        public static void BeginRound(Controller c)
        {
            var bj = c?.TryCast<Bj>();
            if (bj == null) return;

            try
            {
                var players = c.Players;
                if (players == null) return;

                for (int i = 0; i < players.CurrentPlayerCount; i++)
                {
                    var p = players.GetPlayer(i);
                    if (p?.NetworkObject == null) continue;
                    bj.RpcLogic___AddPlayerToCurrentRound_3323014238(p.NetworkObject);
                }

                bj.RpcLogic___SetRoundEnded_1140765316(false);
                bj.CurrentStage = Bj.EStage.Dealing;

                LogVanillaDeck(bj);

                // Everything dealing depends on: who the controller thinks is playing, and
                // whether the seat it keys card positions by actually has any.
                int seated = bj.playersInCurrentRound?.Count ?? -1;
                int seat = LocalSeat(c);
                int slots = -1;
                try { slots = bj.GetPlayerCardPositions(seat)?.Length ?? -1; } catch { }
                int dealerSlots = bj.DealerCardPositions?.Length ?? -1;

                MelonLogger.Msg($"[cards] round open: {seated} in round, local seat {seat}, " +
                                $"{slots} card positions for that seat, {dealerSlots} for the dealer, " +
                                $"stage {bj.CurrentStage}");
            }
            catch (Exception e) { MelonLogger.Warning($"[cards] could not open the round: {e.Message}"); }
        }

        // How vanilla's own deck works has never actually been established -- the fields are
        // visible but their behaviour is not, and guessing at it is how a card game quietly
        // deals the same card twice. Logged once per session, the first time a table opens a
        // round: how many card values are in the deck, how many have been drawn, and how many
        // physical card objects the table owns.
        private static bool _loggedDeck;
        private static int _traced;

        private static void LogVanillaDeck(Bj bj)
        {
            if (_loggedDeck) return;
            _loggedDeck = true;

            try
            {
                int inDeck = bj.cardValuesInDeck?.Count ?? -1;
                int drawn = bj.drawnCardsValues?.Count ?? -1;
                int objects = bj.Cards?.Length ?? -1;

                MelonLogger.Msg($"[deck] vanilla: {inDeck} values in deck, {drawn} already drawn, " +
                                $"{objects} card objects on the table. " +
                                $"{(inDeck == 52 ? "One deck." : inDeck % 52 == 0 ? $"{inDeck / 52} decks." : "Not a whole number of decks -- it is not reset per round.")}");
            }
            catch (Exception e) { MelonLogger.Warning($"[deck] could not read the vanilla deck: {e.Message}"); }
        }

        // Hands the table back to vanilla. Without this the round never ends as far as the
        // controller is concerned, the ready flag stays set, and the player has to cancel and
        // ready up again before anything will deal.
        public static void EndRound(Controller c)
        {
            var bj = c?.TryCast<Bj>();
            if (bj == null) return;

            try
            {
                bj.CurrentStage = Bj.EStage.Ending;
                bj.RpcLogic___EndGame_2166136261();
            }
            catch (Exception e) { MelonLogger.Warning($"[cards] could not close the round: {e.Message}"); }
        }

        // The local player's index WITHIN THE CURRENT ROUND, which is what card positions are
        // keyed by -- not the seat index, which can differ once someone sits out.
        public static int LocalSeat(Controller c)
        {
            try
            {
                var bj = c?.TryCast<Bj>();
                var round = bj?.playersInCurrentRound;
                if (round != null)
                    for (int i = 0; i < round.Count; i++)
                        if (round[i] != null && round[i].IsLocalPlayer) return i;

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

                var before = playing.transform.position;

                if (toDealer) bj.RpcLogic___AddCardToDealerHand_3615296227(playing.CardID);
                else bj.RpcLogic___AddCardToPlayerHand_2801973956(seat, playing.CardID);

                // Logged for the first few cards of a session only. A card that does not move
                // was dealt to a hand the controller has no position for, which is invisible
                // from the outside and has already cost two wrong diagnoses.
                if (_traced < 4)
                {
                    _traced++;
                    var after = playing.transform.position;
                    MelonLogger.Msg($"[cards] {card} id='{playing.CardID}' " +
                                    $"{(toDealer ? "dealer" : $"seat {seat}")} " +
                                    $"moved={(Vector3.Distance(before, after) > 0.001f)} " +
                                    $"from {before} to {after} active={playing.gameObject.activeInHierarchy}");
                }
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
