namespace CasinoExpansion.Core
{
    // Payouts are tuned against the vanilla casino rather than invented. Reference points read
    // off a live save: slot ladder 5/10/25/50/100, Blackjack and Ride the Bus 10-1000 with a 1:1
    // payout and 3:2 on blackjack.
    //
    // HOUSE EDGE, deliberate -- change the rank bands below to retune, and nothing else:
    //   per 13 ranks: Ace 3x (1), K/Q/J 2x (3), 10/9/8 1x (3), 7..2 0x (6)
    //   sum = 3 + 6 + 3 + 0 = 12 over 13 cards  ->  0.923x average on a card slice
    //   over all 53 slices (the grand prize slice pays no cash) -> 48/53 = ~0.906
    // So roughly a 9% cash edge, partly given back by the grand prize item once per ~53 spins.
    // That sits well below a real-world money wheel (~11%) and above vanilla blackjack.
    public static class Prizes
    {
        // Ranks are 1..13 with Ace low. Multiplier is applied to the stake and INCLUDES it,
        // so 0 means the stake is lost and 1 means it is returned.
        public static float CashMultiplier(int slice)
        {
            if (slice == PrizeWheelSlices.GrandPrize) return 0f;

            int rank = Card.FromIndex(slice).Rank;
            return rank switch
            {
                1 => 3f,                        // Ace
                13 or 12 or 11 => 2f,           // King, Queen, Jack
                10 or 9 or 8 => 1f,
                _ => 0f,                        // 7 down to 2
            };
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
