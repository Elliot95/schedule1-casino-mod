using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;

namespace CasinoExpansion.Tweaks
{
    // Raises the casino's stakes: extra tiers on the slot ladder, higher table maximums.
    //
    // Table limits are applied by patching GetBetLimits, NOT by assigning
    // BlackjackGameController.MaximumBet. Those statics come from const fields; Il2CppInterop
    // emits setters for them anyway, but there is no writable storage behind the setter and the
    // write hard-crashes the process. Reading them is fine.
    //
    // Values derive from vanilla at runtime rather than being hardcoded, so a patch that retunes
    // the economy carries through.
    public static class BetLimits
    {
        private static int[] _originalSlotLadder;

        // The game's static property holds only a raw pointer. If the interop wrapper is
        // finalized the array can be collected underneath it, leaving the game reading freed
        // memory on a later frame. Keeping a static reference pins it for the process lifetime.
        private static Il2CppStructArray<int> _ladderRef;

        private static MelonPreferences_Entry<int> _extraSlotTiers;
        private static MelonPreferences_Entry<int> _tableMaxMultiplier;
        private static MelonPreferences_Entry<bool> _slotLadderEnabled;
        private static MelonPreferences_Entry<bool> _tableLimitsEnabled;

        // Read by the Harmony patches on every GetBetLimits call. 1 means leave vanilla alone.
        public static int TableMultiplier =>
            (_tableLimitsEnabled?.Value ?? false)
                ? System.Math.Max(1, _tableMaxMultiplier?.Value ?? 1)
                : 1;

        public static void InitPreferences()
        {
            var cat = MelonPreferences.CreateCategory("CasinoExpansion");

            _slotLadderEnabled = cat.CreateEntry("RaiseSlotLadder", false,
                description: "Append higher tiers to the slot bet ladder.");
            _tableLimitsEnabled = cat.CreateEntry("RaiseTableLimits", false,
                description: "Raise Blackjack and Ride the Bus maximum bets.");
            _extraSlotTiers = cat.CreateEntry("ExtraSlotTiers", 2,
                description: "Extra slot bet tiers to append. 2 adds double and triple the vanilla max.");
            _tableMaxMultiplier = cat.CreateEntry("TableMaxBetMultiplier", 3,
                description: "Multiplier applied to Blackjack and Ride the Bus maximum bets.");
        }

        public static void Apply(System.Action<string> log, System.Action<string> warn)
        {
            log($"Table bet multiplier: x{TableMultiplier} (applied via GetBetLimits patch)");

            if (_slotLadderEnabled?.Value ?? false)
                ApplySlotLadder(log, warn);
            else
                log("Slot ladder unchanged (RaiseSlotLadder is off).");
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
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.BlackjackGameController),
        nameof(Il2CppScheduleOne.Casino.BlackjackGameController.GetBetLimits))]
    internal static class BlackjackBetLimitsPatch
    {
        private static void Postfix(ref float minimum, ref float maximum)
        {
            maximum *= BetLimits.TableMultiplier;
        }
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.RTBGameController),
        nameof(Il2CppScheduleOne.Casino.RTBGameController.GetBetLimits))]
    internal static class RtbBetLimitsPatch
    {
        private static void Postfix(ref float minimum, ref float maximum)
        {
            maximum *= BetLimits.TableMultiplier;
        }
    }
}
