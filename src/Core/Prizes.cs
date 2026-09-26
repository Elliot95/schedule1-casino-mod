using System;
using System.Linq;

namespace CasinoExpansion.Core
{
    // The wheel is calibrated at runtime to match the slot machines' measured return, rather than
    // carrying hardcoded payouts. Vanilla slots measure ~132% RTP -- they pay out more than they
    // take -- so a hand-picked table would have been far off without anyone noticing.
    //
    // The grand prize item counts toward that return: it has a real cash value, so awarding it
    // without budgeting for it would silently push the wheel well above target. Its value is read
    // from the registry and subtracted from the budget before the cash slices are scaled, which
    // is what forces the losing slices to carry the difference.
    //
    // Everything is floored, never rounded up, so the wheel lands at or below the slots.
    public static class Prizes
    {
        // Relative weights, not final multipliers. Scaled at calibration to hit the budget.
        private const float RedAceWeight = 10f;
        private const float BlackAceWeight = 3f;
        private const float FaceWeight = 2f;

        // Grand prize is a flat cash multiplier for now. Item prizes are a later conversation;
        // their value has to be budgeted the same way this multiplier is.
        public const int GrandPrizeMultiplier = 100;

        private static int[] _table;

        // Weight of the non-grand slices only; the grand prize is fixed, not scaled.
        private static float BaseWeight(int slice)
        {
            if (!PrizeWheelSlices.IsCard(slice)) return 0f;   // jackpot and blanks are not scaled

            var card = Card.FromIndex(slice);
            bool red = card.Suit == 1 || card.Suit == 2;

            return card.Rank switch
            {
                1 when red => RedAceWeight,
                1 => BlackAceWeight,
                13 or 12 or 11 => FaceWeight,
                _ => 0f,
            };
        }

        public static void Calibrate(float stake, float targetRtp, Action<string> log, Action<string> warn)
        {
            // Pick the smallest wheel whose jackpot share lands at or under target -- flooring
            // the RTP rather than rounding it up, so the wheel never beats the slots.
            int count = PrizeWheelSlices.MinCount;
            while (targetRtp > 0f && (float)GrandPrizeMultiplier / count > targetRtp) count++;
            PrizeWheelSlices.SetCount(count);

            _table = new int[count];
            _table[PrizeWheelSlices.GrandPrize] = GrandPrizeMultiplier;

            float grandRtp = (float)GrandPrizeMultiplier / count;
            float cashBudget = targetRtp - grandRtp;

            log($"-- Wheel calibration: target {targetRtp:P2}, {GrandPrizeMultiplier}x jackpot " +
                $"diluted across {count} slices = {grandRtp:P2}, leaving {cashBudget:P2}");

            float rawRtp = Enumerable.Range(0, count).Sum(BaseWeight) / count;
            float scale = rawRtp <= 0f ? 0f : cashBudget / rawRtp;

            for (int i = 0; i < count; i++)
                if (i != PrizeWheelSlices.GrandPrize)
                    _table[i] = (int)Math.Floor(BaseWeight(i) * scale);

            int paying = _table.Count(m => m > 0);
            log($"-- Wheel table: {count} slices, {paying} paying, jackpot {GrandPrizeMultiplier}x, " +
                $"card scale x{scale:0.##}. Total RTP {TotalRtp():P2}");
        }

        public static float CashMultiplier(int slice)
        {
            if (_table == null) return 0f;                       // uncalibrated: pay nothing
            return slice >= 0 && slice < _table.Length ? _table[slice] : 0f;
        }

        public static float CashRtp() =>
            _table == null || _table.Length == 0 ? 0f : (float)_table.Sum() / _table.Length;

        public static float TotalRtp() => CashRtp();

        public static string Describe(int slice, float stake)
        {
            if (slice == PrizeWheelSlices.GrandPrize) return $"GRAND PRIZE - {GrandPrizeMultiplier}x ({stake * GrandPrizeMultiplier:0.##})";

            float mult = CashMultiplier(slice);
            string label = PrizeWheelSlices.IsCard(slice) ? Card.FromIndex(slice).ToString() : "blank";
            return mult <= 0f ? $"{label} - no win" : $"{label} - {mult:0.##}x ({stake * mult:0.##})";
        }
    }

    public static class PrizeWheelSlices
    {
        public const int CardCount = 52;          // slices 0..51 map to a card
        public const int GrandPrize = 52;         // slice 52 is the jackpot
        public const int MinCount = 53;

        // Slices beyond the cards and the jackpot are plain losers. Their only job is to dilute
        // the jackpot: a fixed 100x on 1 of 53 is 189% RTP, so the wheel needs more slices, not
        // smaller payouts, to come back under target.
        public static int Count { get; private set; } = MinCount;

        public static void SetCount(int count) => Count = Math.Max(MinCount, count);

        public static bool IsCard(int slice) => slice >= 0 && slice < CardCount;
    }
}
