using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CasinoExpansion.Casino
{
    public enum ETableGame
    {
        Vanilla = 0,          // whatever the table shipped as: blackjack or ride the bus
        BlackjackHR = 1,      // our blackjack, existing only to allow stakes vanilla cannot
        RideTheBusHR = 2,
        Baccarat = 3,
        CasinoHoldem = 4,
        ThreeCardPoker = 5,
        PaiGow = 6,
        RedDog = 7,
    }

    // Each table runs ONE game at a time, chosen at the table itself. Selecting a mod game
    // suppresses the vanilla one rather than layering on top of it, so a table is never trying to
    // be two things at once.
    public static class TableModes
    {
        private static readonly Dictionary<int, ETableGame> Modes = new Dictionary<int, ETableGame>();

        public static ETableGame Get(Component table) =>
            table != null && Modes.TryGetValue(table.GetInstanceID(), out var mode) ? mode : ETableGame.Vanilla;

        // Index arithmetic over All, so adding a game is one enum entry and one array entry.
        public static readonly ETableGame[] All =
        {
            ETableGame.Vanilla, ETableGame.BlackjackHR, ETableGame.RideTheBusHR,
            ETableGame.Baccarat, ETableGame.CasinoHoldem, ETableGame.ThreeCardPoker,
            ETableGame.PaiGow, ETableGame.RedDog,
        };

        public static ETableGame Cycle(Component table, int direction = 1)
        {
            if (table == null) return ETableGame.Vanilla;

            int i = System.Array.IndexOf(All, Get(table));
            int next = ((i + direction) % All.Length + All.Length) % All.Length;

            Modes[table.GetInstanceID()] = All[next];
            MelonLogger.Msg($"[table] '{table.name}' now running {Describe(All[next])}");
            return All[next];
        }

        public static string Describe(ETableGame game) => game switch
        {
            ETableGame.BlackjackHR => "Blackjack (High Roller)",
            ETableGame.RideTheBusHR => "Ride the Bus (High Roller)",
            ETableGame.Baccarat => "Baccarat",
            ETableGame.CasinoHoldem => "Casino Hold'em",
            ETableGame.ThreeCardPoker => "Three Card Poker",
            ETableGame.PaiGow => "Pai Gow Poker",
            ETableGame.RedDog => "Red Dog",
            _ => "House game",
        };

        // Returns true when the vanilla game should be allowed to deal.
        private static bool AllowVanilla(Component table)
        {
            var mode = Get(table);
            if (mode == ETableGame.Vanilla) return true;

            MelonLogger.Msg($"[table] '{table.name}' is set to {Describe(mode)}; vanilla deal suppressed");
            return false;
        }

        // Suppression sits on the DEAL, not on Open(). Open() also performs the camera move,
        // seating, BetPanel.Open and PlayerDisplay.Bind -- all chrome the mod games reuse, so
        // blocking it threw away the very thing we want.
        //
        // Both the public wrapper and its RpcLogic___ twin are patched: the weaver moves the body
        // into the latter, so patching only the wrapper would let the deal through on whichever
        // side invokes it directly. All targets take value parameters or none, avoiding the
        // by-ref marshalling that corrupted an earlier patch.

        [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.BlackjackGameController), "TryStartGame")]
        internal static class BjTryStart
        {
            private static bool Prefix(Il2CppScheduleOne.Casino.BlackjackGameController __instance) => AllowVanilla(__instance);
        }

        [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.BlackjackGameController), "StartGame")]
        internal static class BjStart
        {
            private static bool Prefix(Il2CppScheduleOne.Casino.BlackjackGameController __instance) => AllowVanilla(__instance);
        }

        [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.BlackjackGameController), "RpcLogic___TryStartGame_2166136261")]
        internal static class BjTryStartRpc
        {
            private static bool Prefix(Il2CppScheduleOne.Casino.BlackjackGameController __instance) => AllowVanilla(__instance);
        }

        [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.BlackjackGameController), "RpcLogic___StartGame_2166136261")]
        internal static class BjStartRpc
        {
            private static bool Prefix(Il2CppScheduleOne.Casino.BlackjackGameController __instance) => AllowVanilla(__instance);
        }

        [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.RTBGameController), "TryNextStage")]
        internal static class RtbTryNext
        {
            private static bool Prefix(Il2CppScheduleOne.Casino.RTBGameController __instance) => AllowVanilla(__instance);
        }

        [HarmonyPatch(typeof(Il2CppScheduleOne.Casino.RTBGameController), "RpcLogic___TryNextStage_2166136261")]
        internal static class RtbTryNextRpc
        {
            private static bool Prefix(Il2CppScheduleOne.Casino.RTBGameController __instance) => AllowVanilla(__instance);
        }
    }
}
