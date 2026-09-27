using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CasinoExpansion.Casino
{
    public enum ETableGame
    {
        Vanilla = 0,     // whatever the table shipped as: blackjack or ride the bus
        Baccarat = 1,
        CasinoHoldem = 2,
    }

    // Each table runs ONE game at a time, chosen at the table itself. Selecting a mod game
    // suppresses the vanilla one rather than layering on top of it, so a table is never trying to
    // be two things at once.
    public static class TableModes
    {
        private static readonly Dictionary<int, ETableGame> Modes = new Dictionary<int, ETableGame>();

        public static ETableGame Get(Component table) =>
            table != null && Modes.TryGetValue(table.GetInstanceID(), out var mode) ? mode : ETableGame.Vanilla;

        public static ETableGame Cycle(Component table)
        {
            if (table == null) return ETableGame.Vanilla;

            var next = Get(table) switch
            {
                ETableGame.Vanilla => ETableGame.Baccarat,
                ETableGame.Baccarat => ETableGame.CasinoHoldem,
                _ => ETableGame.Vanilla,
            };

            Modes[table.GetInstanceID()] = next;
            MelonLogger.Msg($"[table] '{table.name}' now running {Describe(next)}");
            return next;
        }

        public static string Describe(ETableGame game) => game switch
        {
            ETableGame.Baccarat => "Baccarat",
            ETableGame.CasinoHoldem => "Casino Hold'em",
            _ => "House game",
        };

        // Returns true when the vanilla controller should be allowed to open.
        private static bool AllowVanilla(Component table)
        {
            var mode = Get(table);
            if (mode == ETableGame.Vanilla) return true;

            MelonLogger.Msg($"[table] '{table.name}' is set to {Describe(mode)}; vanilla game suppressed");
            return false;
        }

        // Prefixes returning false skip the original. Open() takes no by-ref parameters, so this
        // avoids the marshalling that corrupted an earlier out-param patch.
        [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.BlackjackGameController),
            nameof(Il2CppScheduleOne.Casino.BlackjackGameController.Open))]
        internal static class BlackjackOpenPatch
        {
            private static bool Prefix(Il2CppScheduleOne.Casino.BlackjackGameController __instance) =>
                AllowVanilla(__instance);
        }

        [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.RTBGameController),
            nameof(Il2CppScheduleOne.Casino.RTBGameController.Open))]
        internal static class RtbOpenPatch
        {
            private static bool Prefix(Il2CppScheduleOne.Casino.RTBGameController __instance) =>
                AllowVanilla(__instance);
        }
    }
}
