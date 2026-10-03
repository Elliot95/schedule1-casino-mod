using System.Collections;
using UnityEngine;
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
        public int Decks => 6;
        // Asked before the deal, which is when a side bet has to be placed. Each costs a
        // tenth of the main stake.
        public string[] Sides => new[] { "No side bet", "Perfect Pairs", "21+3", "Both" };

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
            var dealer = hands[DealerHand];

            yield return SideBets(session, hands, wager);
            yield return Insurance(session, hands, wager);

            // A natural on either side ends it before anyone acts.
            if (IsBlackjack(hands[PlayerHand]) || IsBlackjack(dealer)) yield break;

            // Split hands are appended to this list as they are created and played in turn, so
            // a split of a split simply lands further down the queue -- which is what makes the
            // re-split unlimited rather than capped at some arbitrary number of hands.
            var playing = new List<string> { PlayerHand };
            hands.Notes[StakeNote(PlayerHand)] = wager.Opening;

            for (int h = 0; h < playing.Count; h++)
            {
                string name = playing[h];
                var hand = hands[name];
                bool first = true;

                while (Total(hand.Cards) < 21)
                {
                    string where = playing.Count > 1 ? $"{name} ({h + 1} of {playing.Count}): " : "";
                    int total = Total(hand.Cards);

                    var options = new List<string> { "Hit", "Stand" };
                    if (first)
                    {
                        options.Add("Double");
                        if (CanSplit(hand)) options.Add("Split");
                    }

                    // The table's own Dealer/You readout, so the numbers appear where a
                    // blackjack player already looks for them.
                    session.Scores($"{Total(new List<Card> { dealer.Cards[0] })}+?", total.ToString());

                    int choice = 1;
                    yield return session.Ask(
                        $"{where}you have {total} — dealer shows {Face(dealer.Cards[0])}",
                        options.ToArray(),
                        i => choice = i);

                    first = false;

                    if (choice == 1) break;                       // stand

                    if (choice == 2)
                    {
                        // Double: one more card and this hand is finished, win or lose.
                        if (wager.Add(StakeOf(hands, name), amount => session.TakeRaise(amount)))
                        {
                            hands.Notes[StakeNote(name)] = StakeOf(hands, name) * 2f;
                            hand.Add(deck.Draw());
                            yield return session.Show(hands);
                            break;
                        }
                        session.Announce("Not enough cash to double.");
                        continue;
                    }

                    if (choice == 3)
                    {
                        // Split: the second card starts a new hand at the same stake, and both
                        // are dealt back up to two cards.
                        if (!wager.Add(StakeOf(hands, name), amount => session.TakeRaise(amount)))
                        {
                            session.Announce("Not enough cash to split.");
                            continue;
                        }

                        string fresh = $"Hand {playing.Count + 1}";
                        var moved = hand.Cards[1];
                        hand.Cards.RemoveAt(1);

                        var other = hands.Add(fresh);
                        other.Add(moved);
                        other.Add(deck.Draw());
                        hand.Add(deck.Draw());

                        hands.Notes[StakeNote(fresh)] = StakeOf(hands, name);
                        playing.Add(fresh);

                        yield return session.Show(hands);
                        first = true;                             // the new two cards may split again
                        continue;
                    }

                    hand.Add(deck.Draw());
                    yield return session.Show(hands);
                }
            }

            // The dealer only plays if something is still live. Every hand busting means there
            // is nothing left to beat.
            bool anyAlive = false;
            foreach (var name in playing) if (Total(hands[name].Cards) <= 21) anyAlive = true;
            if (!anyAlive) yield break;

            // Dealer draws to 17. Done here rather than in Resolve because Resolve is pure and
            // must not touch the deck -- every client replays this identically from the seed.
            while (Total(dealer.Cards) < 17)
            {
                dealer.Add(deck.Draw());
                session.Scores(Total(dealer.Cards).ToString(), Total(hands[PlayerHand].Cards).ToString());
                yield return session.Show(hands);
            }
        }

        // Perfect Pairs and 21+3, settled on the opening cards and the dealer's upcard. Both
        // are decided before the hand is played, so they are taken and scored here and their
        // winnings carried to Resolve -- which stays pure and must not re-derive a stake.
        //
        // The choice itself was made before the deal, through the Sides prompt, because a side
        // bet placed after seeing the cards is not a bet.
        private IEnumerator SideBets(TableSession session, HandSet hands, Wager wager)
        {
            int choice = (int)hands.Note("side");
            if (choice <= 0) yield break;

            var player = hands[PlayerHand];
            var dealer = hands[DealerHand];
            float unit = Mathf.Max(10f, Mathf.Round(wager.Opening * 0.1f / 10f) * 10f);

            bool wantPairs = choice == 1 || choice == 3;
            bool wantThree = choice == 2 || choice == 3;
            float won = 0f;

            if (wantPairs && wager.Add(unit, a => session.TakeRaise(a)))
            {
                float odds = PairOdds(player.Cards[0], player.Cards[1]);
                if (odds > 0f)
                {
                    won += unit * (odds + 1f);
                    session.Announce($"Perfect Pairs pays {odds:0} to 1");
                    yield return session.Wait(0.8f);
                }
            }

            if (wantThree && wager.Add(unit, a => session.TakeRaise(a)))
            {
                var three = new List<Card> { player.Cards[0], player.Cards[1], dealer.Cards[0] };
                float odds = ThreeOdds(PokerHands.Three(three));
                if (odds > 0f)
                {
                    won += unit * (odds + 1f);
                    session.Announce($"21+3 pays {odds:0} to 1");
                    yield return session.Wait(0.8f);
                }
            }

            if (won > 0f) hands.Notes["sidewin"] = hands.Note("sidewin") + won;
        }

        // A pair of the same rank: 25 to 1 matching suit, 12 to 1 matching colour, 6 to 1
        // otherwise. Red is hearts and diamonds, which are suits 1 and 2.
        private static float PairOdds(Card a, Card b)
        {
            if (a.Rank != b.Rank) return 0f;
            if (a.Suit == b.Suit) return 25f;

            bool redA = a.Suit == 1 || a.Suit == 2;
            bool redB = b.Suit == 1 || b.Suit == 2;
            return redA == redB ? 12f : 6f;
        }

        private static float ThreeOdds(PokerHands.Score score) => score.Rank switch
        {
            EHandRank.StraightFlush => 40f,
            EHandRank.Trips => 30f,
            EHandRank.Straight => 10f,
            EHandRank.Flush => 5f,
            _ => 0f,
        };

        // Offered when the dealer shows an ace, at half the main stake, paying 2 to 1 if the
        // dealer has blackjack. It is a bad bet and is offered because a blackjack table that
        // does not offer it is conspicuously missing something.
        private IEnumerator Insurance(TableSession session, HandSet hands, Wager wager)
        {
            var dealer = hands[DealerHand];
            if (dealer.Cards[0].Rank != 1) yield break;

            int take = 1;
            yield return session.Ask($"Dealer shows an ace — insurance for ${wager.Opening / 2f:N0}?",
                new[] { "Insure", "No" }, i => take = i, 20f, 1);

            if (take != 0) yield break;
            if (!wager.Add(wager.Opening / 2f, a => session.TakeRaise(a))) yield break;

            if (IsBlackjack(dealer))
            {
                hands.Notes["sidewin"] = hands.Note("sidewin") + wager.Opening * 1.5f;
                session.Announce("Insurance pays 2 to 1");
                yield return session.Wait(0.8f);
            }
        }

        // Only on the opening two cards, and only a matching rank. Tens and faces all count ten
        // but are not the same rank, so KQ does not split -- the stricter of the two common
        // house rules.
        private static bool CanSplit(Hand hand) =>
            hand.Cards.Count == 2 && hand.Cards[0].Rank == hand.Cards[1].Rank;

        private static string StakeNote(string hand) => $"stake:{hand}";

        private static float StakeOf(HandSet hands, string hand) => hands.Note(StakeNote(hand));

        public Outcome Resolve(HandSet hands, Wager wager, int side)
        {
            var dealer = hands[DealerHand];
            int theirs = Total(dealer.Cards);
            bool theirBj = IsBlackjack(dealer);

            // Every hand settles independently against the dealer and the returns are summed,
            // so a split that wins one and loses one comes out even rather than being scored as
            // a single verdict. The multiplier is that total measured against everything
            // staked, which is what the session pays on.
            float back = 0f;
            var lines = new List<string>();

            foreach (var hand in hands.Hands)
            {
                if (hand.Name == DealerHand) continue;

                float staked = StakeOf(hands, hand.Name);
                if (staked <= 0f) staked = wager.Opening;

                int mine = Total(hand.Cards);
                bool myBj = IsBlackjack(hand) && hands.Hands.Count <= 2;   // a split 21 is not a natural

                float ret;
                string verdict;

                if (myBj && theirBj) { ret = staked; verdict = "both blackjack, push"; }
                else if (myBj) { ret = staked * 2.5f; verdict = "blackjack, pays 3 to 2"; }
                else if (theirBj) { ret = 0f; verdict = "dealer blackjack"; }
                else if (mine > 21) { ret = 0f; verdict = "bust"; }
                else if (theirs > 21) { ret = staked * 2f; verdict = "dealer busts"; }
                else if (mine > theirs) { ret = staked * 2f; verdict = "wins"; }
                else if (mine < theirs) { ret = 0f; verdict = "loses"; }
                else { ret = staked; verdict = "push"; }

                back += ret;
                lines.Add($"{hand.Name} {mine} {verdict}");
            }

            float sideWin = hands.Note("sidewin");
            if (sideWin > 0f)
            {
                back += sideWin;
                lines.Add($"side bets ${sideWin:N0}");
            }

            string detail = $"{string.Join("; ", lines)} — dealer {theirs} ({dealer})";
            float total = wager.Total > 0f ? wager.Total : 1f;
            return new Outcome(back / total, detail);
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
