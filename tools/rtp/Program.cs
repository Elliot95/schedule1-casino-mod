using System;
using System.Collections;
using System.Collections.Generic;
using CasinoExpansion.Casino;
using CasinoExpansion.Core;
using CasinoExpansion.Games;

// Monte Carlo over the shipped game code. Measures what each game actually returns per unit
// staked, because the README's house edges were calculated from standard rules and these are
// not, in several places, standard rules.
static class Program
{
    const int Rounds = 300_000;

    static void Main()
    {
        Console.WriteLine($"Each game played {Rounds:N0} rounds at a $10 stake.\n");
        Console.WriteLine($"{"Game",-26}{"Return",10}{"Edge",10}   Strategy");
        Console.WriteLine(new string('-', 78));

        Run("Blackjack HR", new BlackjackHrGame(), 0, Blackjack, "stand on 17+, no side bets");
        Run("Blackjack + Perfect Pairs", new BlackjackHrGame(), 1, Blackjack, "same, pairs side bet");
        Run("Blackjack + 21+3", new BlackjackHrGame(), 2, Blackjack, "same, 21+3 side bet");
        Run("Blackjack + both", new BlackjackHrGame(), 3, Blackjack, "same, both side bets");
        Run("Ride the Bus HR", new RideTheBusHrGame(), 0, RideAll, "ride all four");
        Run("Ride the Bus (cash @2)", new RideTheBusHrGame(), 0, RideTwo, "cash out after two");
        Run("Baccarat (Player)", new BaccaratGame(), 0, First, "back Player");
        Run("Baccarat (Banker)", new BaccaratGame(), 1, First, "back Banker");
        Run("Baccarat (Tie)", new BaccaratGame(), 2, First, "back Tie");
        Run("Casino Hold'em", new CasinoHoldemGame(), 0, AlwaysCall, "always call");
        Run("Three Card Poker", new ThreeCardPokerGame(), 0, PlayQ64, "play Q-6-4 or better");
        Run("Pai Gow Poker", new PaiGowGame(), 0, First, "house way (first option)");
        Run("Red Dog", new RedDogGame(), 0, RedDogRaise, "raise on spread 7+");
    }

    // ---- strategies: given the prompt and its options, pick one -------------------------

    static int First(string prompt, string[] options) => 0;

    static int AlwaysCall(string prompt, string[] options) => 0;      // Call / Fold

    static int RideAll(string prompt, string[] options) =>
        options.Length == 2 && options[0] == "Continue" ? 0 : 0;      // guess + always continue

    static int _rideStage;
    static int RideTwo(string prompt, string[] options)
    {
        if (options.Length == 2 && options[0] == "Continue")
            return ++_rideStage >= 2 ? 1 : 0;                          // cash out after two wins
        return 0;
    }

    // Hit below 17. Double and split are declined so this measures the base game rather than
    // a strategy's quality.
    static int Blackjack(string prompt, string[] options)
    {
        int total = 0;
        int at = prompt.IndexOf("you have ", StringComparison.Ordinal);
        if (at >= 0) int.TryParse(prompt.Substring(at + 9).Split(' ')[0], out total);
        if (prompt.Contains("insurance")) return 1;                    // decline: it is a bad bet
        return total >= 17 ? 1 : 0;                                    // 0 Hit, 1 Stand
    }

    // The standard Three Card Poker rule: play Q-6-4 or better, fold below.
    static ThreeCardPokerGame _tcp;
    static HandSet _tcpHands;
    static int PlayQ64(string prompt, string[] options)
    {
        var cards = _tcpHands["Player"].Cards;
        var r = new List<int>();
        foreach (var c in cards) r.Add(c.Rank == 1 ? 14 : c.Rank);
        r.Sort(); r.Reverse();

        // Anything that ranks above high card -- pair, flush, straight, trips -- is already
        // better than Q-6-4 and is always played. Only a bare high-card hand is compared.
        bool play = PokerHands.Three(cards).Rank > EHandRank.HighCard
                 || r[0] > 12
                 || (r[0] == 12 && r[1] > 6)
                 || (r[0] == 12 && r[1] == 6 && r[2] >= 4);
        return play ? 0 : 1;                                           // 0 Play, 1 Fold
    }

    // Raise only on a spread of 7 or more, which is the only spread where raising gains.
    static int RedDogRaise(string prompt, string[] options)
    {
        int spread = 0;
        int at = prompt.IndexOf("Spread ", StringComparison.Ordinal);
        if (at >= 0) int.TryParse(prompt.Substring(at + 7).Split(' ')[0], out spread);
        return spread >= 7 ? 0 : 1;                                    // 0 Raise, 1 Stand
    }

    // ---- the harness --------------------------------------------------------------------

    static void Run(string label, ITableGame game, int side,
                    Func<string, string[], int> strategy, string note)
    {
        var rng = new Random(20261003);
        const float stake = 10f;

        double staked = 0, returned = 0;

        for (int i = 0; i < Rounds; i++)
        {
            _rideStage = 0;

            var deck = new Deck(rng.Next(1, 16_777_216), game.Decks);
            var hands = new HandSet();
            hands.Notes["side"] = side;
            game.Deal(hands, deck);

            var session = new TableSession { Strategy = strategy };
            var wager = new Wager(stake);

            _tcpHands = hands;

            if (game is IDecidingGame deciding)
                Drain(deciding.Decide(session, hands, deck, wager));

            var outcome = game.Resolve(hands, wager, side);

            staked += wager.Total;
            returned += wager.Total * outcome.Multiplier;
        }

        double rtp = returned / staked;
        Console.WriteLine($"{label,-26}{rtp,9:P2}{(1 - rtp),10:P2}   {note}");
    }

    // Coroutines without a Unity loop: run them to completion, following nested enumerators.
    static void Drain(IEnumerator routine)
    {
        while (routine.MoveNext())
            if (routine.Current is IEnumerator inner) Drain(inner);
    }
}
