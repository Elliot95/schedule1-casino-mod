using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CasinoExpansion.Mod), "CasinoExpansion", "0.1.0", "Elliot95")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CasinoExpansion
{
    public class Mod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("CasinoExpansion loaded.");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene initialized: {sceneName} (index {buildIndex})");

            if (sceneName == "Main")
                ProbeGameApis();
        }

        // Phase 0 validation: confirms the APIs the whole design depends on are reachable at runtime.
        private void ProbeGameApis()
        {
            try
            {
                var mm = Il2CppScheduleOne.Money.MoneyManager.Instance;
                LoggerInstance.Msg(mm != null
                    ? $"MoneyManager reachable. Cash balance: {mm.cashBalance}"
                    : "MoneyManager.Instance was null.");
            }
            catch (System.Exception e)
            {
                LoggerInstance.Warning($"MoneyManager probe failed: {e.Message}");
            }

            LogFound<Il2CppScheduleOne.Casino.BlackjackGameController>("BlackjackGameController");
            LogFound<Il2CppScheduleOne.Casino.CasinoGamePlayers>("CasinoGamePlayers");
            LogFound<Il2CppScheduleOne.Casino.CardController>("CardController");
            LogFound<Il2CppScheduleOne.Casino.SlotMachine>("SlotMachine");
        }

        private void LogFound<T>(string label) where T : Object
        {
            try
            {
                var found = Object.FindObjectsOfType<T>();
                LoggerInstance.Msg($"{label}: {found.Length} in scene");
            }
            catch (System.Exception e)
            {
                LoggerInstance.Warning($"{label} probe failed: {e.Message}");
            }
        }
    }
}
