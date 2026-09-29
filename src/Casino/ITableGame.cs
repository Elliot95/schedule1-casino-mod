using System.Collections.Generic;
using System.Linq;
using CasinoExpansion.Core;

namespace CasinoExpansion.Casino
{
    public readonly struct BetRange
    {
        public readonly float Min, Max;
        public BetRange(float min, float max) { Min = min; Max = max; }
    }

    public sealed class Hand
    {
        public readonly string Name;
        public readonly List<Card> Cards = new List<Card>();

        public Hand(string name) { Name = name; }

        public void Add(Card c) => Cards.Add(c);
        public override string ToString() => string.Join(" ", Cards.Select(c => c.ToString()));
    }

    public sealed class HandSet
    {
        public readonly List<Hand> Hands = new List<Hand>();

        public Hand Add(string name)
        {
            var hand = new Hand(name);
            Hands.Add(hand);
            return hand;
        }

        public Hand this[string name] => Hands.FirstOrDefault(h => h.Name == name);
    }

    public readonly struct Outcome
    {
        public readonly float Multiplier;   // applied to the stake, INCLUDES it: 0 loses, 1 pushes
        public readonly string Summary;

        public Outcome(float multiplier, string summary) { Multiplier = multiplier; Summary = summary; }
    }

    // A game is three things: what it deals, what that pays, and how to describe it. Everything
    // else -- the panel, the bank, the round counter, consensus, presentation -- is shared, so
    // adding a game should not require touching any of that.
    public interface ITableGame
    {
        ETableGame Id { get; }
        string Title { get; }
        BetRange Limits { get; }

        // Deals into the set. Runs on the authority only; every client reconstructs the same
        // cards from the same seed rather than having them replicated.
        void Deal(HandSet hands, Deck deck);

        // PURE. Same hands and stake must give the same result on every client, because
        // ChangeCashBalance is local and any disagreement silently mints or destroys money.
        Outcome Resolve(HandSet hands, float stake);
    }

    // Implemented alongside ITableGame by any game with a decision in the middle of the round --
    // Red Dog's raise now, blackjack's hit/stand and double later.
    //
    // Deal puts down only what the player decides on; Decide then asks, may draw further cards
    // into the same HandSet, and may raise the wager. Resolve stays pure and sees the finished
    // hands, so the money rule is unchanged: every client reaches the same verdict from the same
    // cards. Only the local player's answer differs, and that is carried in the Wager.
    public interface IDecidingGame
    {
        System.Collections.IEnumerator Decide(TableSession session, HandSet hands, Deck deck, Wager wager);
    }

    // The live stake for a round in progress. Games raise through Add rather than writing the
    // total, so the session can take the extra money at the moment it is committed.
    public sealed class Wager
    {
        public float Opening { get; }
        public float Extra { get; private set; }
        public float Total => Opening + Extra;

        public Wager(float opening) { Opening = opening; }

        public bool Add(float amount, System.Func<float, bool> take)
        {
            if (amount <= 0f || !take(amount)) return false;
            Extra += amount;
            return true;
        }
    }

    public static class TableGames
    {
        private static readonly Dictionary<ETableGame, ITableGame> Registry =
            new Dictionary<ETableGame, ITableGame>
            {
                [ETableGame.Baccarat] = new Games.BaccaratGame(),
                [ETableGame.RedDog] = new Games.RedDogGame(),
            };

        public static ITableGame For(ETableGame id) =>
            Registry.TryGetValue(id, out var game) ? game : null;

        public static bool IsImplemented(ETableGame id) => Registry.ContainsKey(id);
    }
}
