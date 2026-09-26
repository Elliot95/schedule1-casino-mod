using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;

namespace CasinoExpansion.Tweaks
{
    // Raises the casino's stakes: extra tiers on the slot ladder, higher table maximums.
    //
    // Every value is derived from the vanilla numbers at runtime rather than hardcoded, so the
    // tweak survives game patches that retune the economy. The originals are cached on first
    // apply because these are STATIC properties -- deriving from the current value instead would
    // compound on every scene load and inflate the ladder without bound.
    public static class BetLimits
    {
        private static int[] _originalSlotLadder;
        private static int _originalBlackjackMax = -1;
        private static int _originalRtbMax = -1;

        private static MelonPreferences_Entry<int> _extraSlotTiers;
        private static MelonPreferences_Entry<int> _tableMaxMultiplier;
        private static MelonPreferences_Entry<bool> _enabled;

        public static void InitPreferences()
        {
            var cat = MelonPreferences.CreateCategory("CasinoExpansion");
            _enabled = cat.CreateEntry("RaiseBetLimits", true,
                description: "Raise casino bet limits above vanilla.");
            _extraSlotTiers = cat.CreateEntry("ExtraSlotTiers", 2,
                description: "Extra slot bet tiers to append. 2 adds double and triple the vanilla max.");
            _tableMaxMultiplier = cat.CreateEntry("TableMaxBetMultiplier", 3,
                description: "Multiplier applied to Blackjack and Ride the Bus maximum bets.");
        }

        public static void Apply(System.Action<string> log, System.Action<string> warn)
        {
            if (_enabled != null && !_enabled.Value)
            {
                log("Bet limit changes disabled by config.");
                return;
            }

            ApplySlotLadder(log, warn);
            ApplyTableMax(log, warn);
        }

        private static void ApplySlotLadder(System.Action<string> log, System.Action<string> warn)
        {
            try
            {
                var current = Il2CppScheduleOne.Casino.SlotMachine.BetAmounts;
                if (current == null || current.Length == 0) { warn("slot bet ladder unavailable"); return; }

                _originalSlotLadder ??= Enumerable.Range(0, current.Length).Select(i => current[i]).ToArray();

                int extra = System.Math.Max(0, _extraSlotTiers?.Value ?? 2);
                if (extra == 0) return;

                int baseMax = _originalSlotLadder[_originalSlotLadder.Length - 1];
                var ladder = _originalSlotLadder
                    .Concat(Enumerable.Range(2, extra).Select(m => baseMax * m))
                    .ToArray();

                var replacement = new Il2CppStructArray<int>(ladder.Length);
                for (int i = 0; i < ladder.Length; i++) replacement[i] = ladder[i];
                Il2CppScheduleOne.Casino.SlotMachine.BetAmounts = replacement;

                log($"Slot ladder: [{string.Join(", ", _originalSlotLadder)}] -> [{string.Join(", ", ladder)}]");
            }
            catch (System.Exception e) { warn($"slot ladder change failed: {e.Message}"); }
        }

        private static void ApplyTableMax(System.Action<string> log, System.Action<string> warn)
        {
            int mult = System.Math.Max(1, _tableMaxMultiplier?.Value ?? 3);

            try
            {
                if (_originalBlackjackMax < 0)
                    _originalBlackjackMax = Il2CppScheduleOne.Casino.BlackjackGameController.MaximumBet;

                Il2CppScheduleOne.Casino.BlackjackGameController.MaximumBet = _originalBlackjackMax * mult;
                log($"Blackjack max bet: {_originalBlackjackMax} -> {_originalBlackjackMax * mult} " +
                    $"(min {Il2CppScheduleOne.Casino.BlackjackGameController.MinimumBet})");
            }
            catch (System.Exception e) { warn($"blackjack max bet change failed: {e.Message}"); }

            try
            {
                if (_originalRtbMax < 0)
                    _originalRtbMax = Il2CppScheduleOne.Casino.RTBGameController.MaximumBet;

                Il2CppScheduleOne.Casino.RTBGameController.MaximumBet = _originalRtbMax * mult;
                log($"Ride the Bus max bet: {_originalRtbMax} -> {_originalRtbMax * mult} " +
                    $"(min {Il2CppScheduleOne.Casino.RTBGameController.MinimumBet})");
            }
            catch (System.Exception e) { warn($"ride the bus max bet change failed: {e.Message}"); }
        }
    }
}
