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
        private static MelonPreferences_Entry<bool> _slotLadderEnabled;

        public static void InitPreferences()
        {
            var cat = MelonPreferences.CreateCategory("CasinoExpansion");

            _slotLadderEnabled = cat.CreateEntry("RaiseSlotLadder", true,
                description: "Append higher tiers to the slot bet ladder.");
            _extraSlotTiers = cat.CreateEntry("ExtraSlotTiers", 2,
                description: "Extra slot bet tiers to append. 2 adds double and triple the vanilla max.");
        }

        public static void Apply(System.Action<string> log, System.Action<string> warn)
        {
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

}

// Table bet limits are deliberately NOT implemented.
//
// Two approaches have been tried and both failed:
//
// 1. Assigning BlackjackGameController.MaximumBet / RTBGameController.MaximumBet. These statics
//    come from const fields. Il2CppInterop emits setters for them, but there is no writable
//    storage behind the setter, so the write lands on invalid memory and hard-crashes the
//    process. Reading them is fine. Do not trust a static setter in these interop assemblies.
//
// 2. A Harmony postfix on GetBetLimits(out float minimum, out float maximum), multiplying the
//    maximum. This compiled and ran, but left the in-game bet slider pinned at zero and
//    undraggable -- Harmony's by-ref parameter binding on Il2Cpp methods does not appear to
//    marshal these out params correctly, so the postfix wrote zeros over the real limits.
//    Note the config toggle did not protect against this: the patch applies at load and ran
//    even when the multiplier was 1.
//
// A third route, not yet attempted: leave GetBetLimits alone and instead adjust whatever UI
// component configures the bet slider's range. That needs the bet-entry UI mapped first.
