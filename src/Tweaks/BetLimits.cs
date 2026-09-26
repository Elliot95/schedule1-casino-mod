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

        // The game's static property holds only a raw pointer. If the interop wrapper is
        // finalized the array can be collected underneath it, leaving the game reading freed
        // memory on a later frame. Keeping a static reference pins it for the process lifetime.
        private static Il2CppStructArray<int> _ladderRef;

        private static MelonPreferences_Entry<int> _extraSlotTiers;
        private static MelonPreferences_Entry<int> _tableMaxMultiplier;
        private static MelonPreferences_Entry<bool> _slotLadderEnabled;
        private static MelonPreferences_Entry<bool> _tableLimitsEnabled;

        public static void InitPreferences()
        {
            var cat = MelonPreferences.CreateCategory("CasinoExpansion");

            // Both default off: an earlier build crashed the game here and the cause is not yet
            // confirmed, so raising limits is opt-in until each half is verified separately.
            _slotLadderEnabled = cat.CreateEntry("RaiseSlotLadder", false,
                description: "Append higher tiers to the slot bet ladder. UNVERIFIED - may crash.");
            _tableLimitsEnabled = cat.CreateEntry("RaiseTableLimits", false,
                description: "Raise Blackjack and Ride the Bus maximum bets. UNVERIFIED - may crash.");
            _extraSlotTiers = cat.CreateEntry("ExtraSlotTiers", 2,
                description: "Extra slot bet tiers to append. 2 adds double and triple the vanilla max.");
            _tableMaxMultiplier = cat.CreateEntry("TableMaxBetMultiplier", 3,
                description: "Multiplier applied to Blackjack and Ride the Bus maximum bets.");
        }

        public static void Apply(System.Action<string> log, System.Action<string> warn)
        {
            bool slots = _slotLadderEnabled?.Value ?? false;
            bool tables = _tableLimitsEnabled?.Value ?? false;

            if (!slots && !tables)
            {
                log("Bet limit changes are off. Enable RaiseSlotLadder / RaiseTableLimits in MelonPreferences.cfg to test.");
                return;
            }

            // Logged individually so that if the game dies again, the last line written names
            // exactly which step was responsible.
            if (slots) { log("step: slot ladder"); ApplySlotLadder(log, warn); log("step: slot ladder OK"); }
            if (tables) { log("step: table limits"); ApplyTableMax(log, warn); log("step: table limits OK"); }
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

                _ladderRef = new Il2CppStructArray<int>(ladder.Length);
                for (int i = 0; i < ladder.Length; i++) _ladderRef[i] = ladder[i];
                Il2CppScheduleOne.Casino.SlotMachine.BetAmounts = _ladderRef;

                log($"Slot ladder: [{string.Join(", ", _originalSlotLadder)}] -> [{string.Join(", ", ladder)}]");
            }
            catch (System.Exception e) { warn($"slot ladder change failed: {e.Message}"); }
        }

        private static void ApplyTableMax(System.Action<string> log, System.Action<string> warn)
        {
            int mult = System.Math.Max(1, _tableMaxMultiplier?.Value ?? 3);

            try
            {
                log("  reading blackjack max...");
                if (_originalBlackjackMax < 0)
                    _originalBlackjackMax = Il2CppScheduleOne.Casino.BlackjackGameController.MaximumBet;
                log($"  blackjack max read = {_originalBlackjackMax}, writing...");

                Il2CppScheduleOne.Casino.BlackjackGameController.MaximumBet = _originalBlackjackMax * mult;
                log($"  blackjack max bet: {_originalBlackjackMax} -> {_originalBlackjackMax * mult}");
            }
            catch (System.Exception e) { warn($"blackjack max bet change failed: {e.Message}"); }

            try
            {
                log("  reading ride the bus max...");
                if (_originalRtbMax < 0)
                    _originalRtbMax = Il2CppScheduleOne.Casino.RTBGameController.MaximumBet;
                log($"  ride the bus max read = {_originalRtbMax}, writing...");

                Il2CppScheduleOne.Casino.RTBGameController.MaximumBet = _originalRtbMax * mult;
                log($"  ride the bus max bet: {_originalRtbMax} -> {_originalRtbMax * mult}");
            }
            catch (System.Exception e) { warn($"ride the bus max bet change failed: {e.Message}"); }
        }
    }
}
