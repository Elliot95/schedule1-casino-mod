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
                "Blackjack pays 3 to 2.",
                "Double on your first two cards.",
                "Split pairs as often as they come.",
                "Stakes up to $50,000.",
            },
            ETableGame.RideTheBusHR => new[]
            {
                "<b>Ride the Bus (High Roller)</b>",
                "Four guesses, each harder.",
                "2x, 2x, 3x, 4x -- 48x for all four.",
                "Cash out after any correct guess.",
                "One wrong guess takes the lot.",
                "Stakes up to $50,000.",
            },
            ETableGame.Baccarat => new[]
            {
                "<b>Baccarat</b>",
                "Closest to 9 wins.",
                "Tens and faces count zero.",
                "Totals drop the tens digit.",
                "Player pays 1 to 1.",
                "Banker pays 1 to 1, less 5%.",
                "Tie pays 8 to 1.",
                "Draws follow fixed rules.",
            },
            ETableGame.CasinoHoldem => new[]
            {
                "<b>Casino Hold'em</b>",
                "Ante, then see the flop.",
                "Call for twice the ante, or fold.",
                "Best five of seven wins.",
                "Dealer needs a pair of fours.",
                "Ante pays more for big hands.",
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
                "You choose the two in front.",
                "Win both to win, less 5%.",
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
