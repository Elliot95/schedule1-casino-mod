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
        public const int GrandPrizeMultiplier = 50;

        // Roughly one spin in six should pay something. A single 100x jackpot consumed the whole
        // RTP budget and left 75 of 76 slices dead, which made the wheel unreadable and untestable.
        public const int PayingSliceFraction = 6;

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
            // Smallest wheel whose jackpot share fits under target, flooring rather than rounding
            // up so the wheel never beats the slots.
            int count = PrizeWheelSlices.MinCount;
            while (targetRtp > 0f && (float)GrandPrizeMultiplier / count > targetRtp) count++;
            PrizeWheelSlices.SetCount(count);

            _table = new int[count];
            _table[PrizeWheelSlices.GrandPrize] = GrandPrizeMultiplier;

            float grandRtp = (float)GrandPrizeMultiplier / count;
            float cashBudget = Math.Max(0f, targetRtp - grandRtp);

            // Winners are spread evenly around the face rather than clustered, so the wheel reads
            // as a fair mix at a glance instead of one live wedge.
            int winners = Math.Max(1, count / PayingSliceFraction - 1);
            int perWinner = (int)Math.Floor(cashBudget * count / winners);

            if (perWinner >= 1)
            {
                float step = (float)count / winners;
                for (int w = 0; w < winners; w++)
                {
                    int slice = (int)Math.Round(w * step) % count;
                    if (slice == PrizeWheelSlices.GrandPrize) slice = (slice + 1) % count;
                    _table[slice] = perWinner;
                }
            }
            else warn($"cash budget {cashBudget:P2} too small to pay any slice");

            int paying = _table.Count(m => m > 0);
            log($"-- Wheel calibration: target {targetRtp:P2}, jackpot {GrandPrizeMultiplier}x " +
                $"over {count} slices = {grandRtp:P2}, leaving {cashBudget:P2}");
            log($"-- Wheel table: {paying} of {count} paying (1 in {count / (float)paying:0.#}), " +
                $"winners {perWinner}x. Total RTP {TotalRtp():P2}");
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
