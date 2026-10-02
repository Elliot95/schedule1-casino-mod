using System.Collections;
using UnityEngine;
using CasinoExpansion.Casino;
using CasinoExpansion.Core;

namespace CasinoExpansion.Games
{
    // Ride the Bus, reimplemented for the same reason as Blackjack HR: the vanilla controller's
    // bet limits are const-backed statics that cannot be written without crashing.
    //
    // Four guesses, each on a fresh card, each harder than the last. Every correct guess
    // multiplies what is riding; one wrong guess ends the round and takes the lot. The player
    // may walk away with the accumulated winnings after any correct guess, which is the real
    // decision -- the guesses themselves are coin flips, knowing when to stop is not.
    //
    // Odds are deliberately a little under true. Colour is an even-money proposition paying
    // 2x, but higher/lower pushes on a tie and inside/outside is scored against a spread that
    // can be empty, so the house keeps an edge at every stage.
    public sealed class RideTheBusHrGame : ITableGame, IDecidingGame
    {
        public const string PlayerHand = "Player";

        public ETableGame Id => ETableGame.RideTheBusHR;
        public string Title => "Ride the Bus (high roller)";
        public BetRange Limits => new BetRange(10f, 50_000f);
        public int Decks => 1;
        public string[] Sides => System.Array.Empty<string>();

        // Multipliers for each stage, applied cumulatively. Four correct guesses returns
        // 2 x 2 x 3 x 4 = 48 times the stake, which is the headline the game is sold on.
        private static readonly float[] StageOdds = { 2f, 2f, 3f, 4f };

        // Red is hearts and diamonds; our suit order is Spades, Hearts, Diamonds, Clubs.
        private static bool IsRed(Card c) => c.Suit == 1 || c.Suit == 2;

        private static int Up(Card c) => c.Rank == 1 ? 14 : c.Rank;

        // Deal nothing: every card in this game is turned as a consequence of a guess, so
        // dealing up front would show the player the answers.
        public void Deal(HandSet hands, Deck deck) => hands.Add(PlayerHand);

        public IEnumerator Decide(TableSession session, HandSet hands, Deck deck, Wager wager)
        {
            var hand = hands[PlayerHand];
            float riding = wager.Opening;

            for (int stage = 0; stage < 4; stage++)
            {
                int choice = 0;
                string[] options = stage switch
                {
                    0 => new[] { "Red", "Black" },
                    1 => new[] { "Higher", "Lower" },
                    2 => new[] { "Inside", "Outside" },
                    _ => new[] { "Spades", "Hearts", "Diamonds", "Clubs" },
                };

                yield return session.Ask(Prompt(stage, hand, riding), options, i => choice = i);

                var card = deck.Draw();
                hand.Add(card);
                yield return session.Show(hands);

                if (!Correct(stage, hand, card, choice))
                {
                    hands.Notes["stage"] = stage;
                    hands.Notes["won"] = 0f;
                    session.Announce($"Wrong — {Describe(stage, choice)} missed. Lost ${wager.Opening:N0}.");
                    yield return session.Wait(1.2f);
                    yield break;
                }

                riding *= StageOdds[stage];
                hands.Notes["stage"] = stage + 1;
                hands.Notes["won"] = riding;

                // Cash out, except after the last guess where there is nothing left to risk.
                if (stage < 3)
                {
                    int stay = 0;
                    yield return session.Ask(
                        $"${riding:N0} riding — keep going for {StageOdds[stage + 1]:0.#}x?",
                        new[] { "Continue", "Cash out" },
                        i => stay = i);

                    if (stay == 1) yield break;
                }
            }
        }

        private static string Prompt(int stage, Hand hand, float riding) => stage switch
        {
            0 => $"${riding:N0} up — red or black?",
            1 => $"Higher or lower than {Name(hand.Cards[0])}?",
            2 => $"Inside or outside {Name(hand.Cards[0])} and {Name(hand.Cards[1])}?",
            _ => "Call the suit",
        };

        private static bool Correct(int stage, Hand hand, Card card, int choice)
        {
            switch (stage)
            {
                case 0:
                    return IsRed(card) == (choice == 0);

                case 1:
                {
                    // A tie loses rather than pushing: the stage is already even money and a
                    // push would hand the player the edge.
                    int first = Up(hand.Cards[0]), now = Up(card);
                    return choice == 0 ? now > first : now < first;
                }

                case 2:
                {
                    int a = Up(hand.Cards[0]), b = Up(hand.Cards[1]);
                    int low = a < b ? a : b, high = a < b ? b : a;
                    int now = Up(card);
                    bool inside = now > low && now < high;
                    return choice == 0 ? inside : !inside;
                }

                default:
                    return card.Suit == SuitOf(choice);
            }
        }

        // Button order is Spades, Hearts, Diamonds, Clubs, matching Card.Suit exactly.
        private static int SuitOf(int choice) => choice;

        private static string Describe(int stage, int choice) => stage switch
        {
            0 => choice == 0 ? "red" : "black",
            1 => choice == 0 ? "higher" : "lower",
            2 => choice == 0 ? "inside" : "outside",
            _ => new[] { "spades", "hearts", "diamonds", "clubs" }[Mathf.Clamp(choice, 0, 3)],
        };

        private static string Name(Card c) =>
            $"{"A23456789TJQK"[c.Rank - 1]}{"SHDC"[c.Suit]}";

        public Outcome Resolve(HandSet hands, Wager wager, int side)
        {
            var hand = hands[PlayerHand];
            float won = hands.Note("won");
            int stage = (int)hands.Note("stage");

            if (won <= 0f)
                return new Outcome(0f, $"Fell off at stage {stage + 1}. {hand}");

            string how = stage >= 4 ? "All four — rode the bus!" : $"Cashed out after {stage}.";
            return new Outcome(won / wager.Total, $"{how} {hand}");
        }
    }
}
