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

    public static class TableGames
    {
        private static readonly Dictionary<ETableGame, ITableGame> Registry =
            new Dictionary<ETableGame, ITableGame>
            {
                [ETableGame.Baccarat] = new Games.BaccaratGame(),
            };

        public static ITableGame For(ETableGame id) =>
            Registry.TryGetValue(id, out var game) ? game : null;

        public static bool IsImplemented(ETableGame id) => Registry.ContainsKey(id);
    }
}
