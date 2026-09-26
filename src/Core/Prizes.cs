namespace CasinoExpansion.Core
{
    // Payouts are tuned against the vanilla casino rather than invented. Reference points read
    // off a live save: slot ladder 5/10/25/50/100, Blackjack and Ride the Bus 10-1000 with a 1:1
    // payout and 3:2 on blackjack.
    //
    // RTP is meant to track the slot machines, rounded DOWN so the wheel is never the better bet.
    // The slots' win table is native and unreadable, so it is measured empirically at startup by
    // SlotRtp and logged next to ExpectedRtp() below -- compare the two and retune the rank bands
    // here if they have drifted apart.
    //
    // Current table, per 52 card slices plus the grand prize slice which pays no cash:
    //   2 red Aces at 10x = 20, 2 black Aces at 3x = 6, 12 K/Q/J at 2x = 24, rest 0
    //   total 50 over 53 slices -> ~94.3%
    // The grand prize item is on top of that, so true player return is a little higher.
    public static class Prizes
    {
        // Multiplier is applied to the stake and INCLUDES it, so 0 means the stake is lost and 1
        // means it is returned. Ranks are 1..13 with Ace low; suits are 0 Spades, 1 Hearts,
        // 2 Diamonds, 3 Clubs.
        public static float CashMultiplier(int slice)
        {
            if (slice == PrizeWheelSlices.GrandPrize) return 0f;

            var card = Card.FromIndex(slice);
            bool red = card.Suit == 1 || card.Suit == 2;

            return card.Rank switch
            {
                1 when red => 10f,              // the two red Aces are the jackpot slices
                1 => 3f,                        // black Aces
                13 or 12 or 11 => 2f,           // King, Queen, Jack
                _ => 0f,
            };
        }

        // Computed rather than asserted, so the log can show what the table actually returns and
        // it can be compared against the measured slot RTP instead of assumed to match.
        public static float ExpectedRtp()
        {
            float total = 0f;
            for (int i = 0; i < PrizeWheelSlices.Count; i++) total += CashMultiplier(i);
            return total / PrizeWheelSlices.Count;
        }

        // Grand prize item pool. Every id was verified present in Registry on a live save.
        // Chosen from the round seed so all clients agree without extra replication.
        private static readonly string[] ItemPool =
        {
            "goldbar", "goldwatch", "goldchain", "silverwatch", "silverchain",
        };

        public static string GrandPrizeItemId(int seed)
        {
            uint s = seed == 0 ? 1u : (uint)seed;
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return ItemPool[s % (uint)ItemPool.Length];
        }

        public static string Describe(int slice, float stake)
        {
            if (slice == PrizeWheelSlices.GrandPrize) return "GRAND PRIZE";

            var card = Card.FromIndex(slice);
            float mult = CashMultiplier(slice);
            return mult <= 0f
                ? $"{card} - no win"
                : $"{card} - {mult:0.##}x ({stake * mult:0.##})";
        }
    }

    public static class PrizeWheelSlices
    {
        public const int Count = 53;
        public const int GrandPrize = 52;
    }
}
