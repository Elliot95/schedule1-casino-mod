namespace CasinoExpansion.Casino
{
    // Short rules shown beside the selector. Kept to a few lines each: enough to play without
    // looking anything up, not a manual.
    public static class TableRules
    {
        public static string[] For(ETableGame game) => game switch
        {
            ETableGame.BlackjackHR => new[]
            {
                "<b>Blackjack (High Roller)</b>",
                "Beat the dealer without passing 21.",
                "Dealer draws to 16, stands on 17.",
                "Blackjack pays 3:2.",
                "Stakes up to $50,000.",
            },
            ETableGame.RideTheBusHR => new[]
            {
                "<b>Ride the Bus (High Roller)</b>",
                "Four questions, each harder.",
                "Win to ride on, cash out any time.",
                "Lose one and the round is over.",
                "Stakes up to $50,000.",
            },
            ETableGame.Baccarat => new[]
            {
                "<b>Baccarat</b>",
                "Bet Player or Banker.",
                "Closest to 9 wins.",
                "Tens and faces count zero.",
                "Totals drop the tens digit.",
            },
            ETableGame.CasinoHoldem => new[]
            {
                "<b>Casino Hold'em</b>",
                "Ante, then see the flop.",
                "Call to continue or fold to quit.",
                "Best five cards beat the dealer.",
                "Dealer needs a pair to qualify.",
            },
            ETableGame.ThreeCardPoker => new[]
            {
                "<b>Three Card Poker</b>",
                "Three cards each, no draws.",
                "Straight beats flush here.",
                "Play to continue or fold.",
                "Dealer needs queen high.",
            },
            ETableGame.PaiGow => new[]
            {
                "<b>Pai Gow Poker</b>",
                "Seven cards, split into five and two.",
                "The five must outrank the two.",
                "Win both hands to win.",
                "One each is a push.",
            },
            ETableGame.RedDog => new[]
            {
                "<b>Red Dog</b>",
                "Two cards set a spread.",
                "A third falling between pays.",
                "Narrower spreads pay more.",
                "Matching pairs push.",
            },
            _ => new[]
            {
                "<b>House game</b>",
                "The table's own game, untouched.",
                "Standard limits apply.",
            },
        };
    }
}
