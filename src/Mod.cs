using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CasinoExpansion.Mod), "CasinoExpansion", "0.3.1", "Elliot95")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CasinoExpansion
{
    public class Mod : MelonMod
    {
        private readonly CasinoExpansion.Games.PrizeWheelGame _wheel = new CasinoExpansion.Games.PrizeWheelGame();

        public override void OnInitializeMelon()
        {
            Tweaks.BetLimits.InitPreferences();
            var cat = MelonPreferences.CreateCategory("CasinoExpansion");
            CasinoExpansion.Games.PrizeWheelGame.InitPreferences(cat);
            Tweaks.CasinoHours.InitPreferences(cat);
            LoggerInstance.Msg("CasinoExpansion loaded. F9 moves the prize wheel to you, F10 scans for casino staff.");
        }

        public override void OnUpdate()
        {
            Casino.TableSession.TickAll();
            Casino.TablePanelChrome.TickRefresh();

            if (Input.GetKeyDown(KeyCode.F9))
                _wheel.Respawn();

            // On demand, because the startup scan fires before NPCs have finished spawning.
            if (Input.GetKeyDown(KeyCode.F10))
                Probe.CasinoStaff.Run(LoggerInstance.Msg, LoggerInstance.Warning);

            if (Input.GetKeyDown(KeyCode.F11))
                Probe.UiDonors.Run(LoggerInstance.Msg, LoggerInstance.Warning);

            // The multiplayer gating test -- run from a non-host client during a co-op session.
            if (Input.GetKeyDown(KeyCode.F8))
                Tweaks.BetLimits.ProbeOwnership(LoggerInstance.Msg, LoggerInstance.Warning);
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene initialized: {sceneName} (index {buildIndex})");

            if (sceneName == "Main")
                ProbeGameApis();
        }

        private void ProbeGameApis()
        {
            LogFound<Il2CppScheduleOne.Casino.BlackjackGameController>("BlackjackGameController");
            LogFound<Il2CppScheduleOne.Casino.CasinoGamePlayers>("CasinoGamePlayers");
            LogFound<Il2CppScheduleOne.Casino.CardController>("CardController");
            LogFound<Il2CppScheduleOne.Casino.SlotMachine>("SlotMachine");

            MelonCoroutines.Start(WaitForMoney());
        }

        // Cash is unreadable at scene-init and only becomes valid once save data lands.
        // Timing it tells us how long after load a bet can safely be accepted.
        private System.Collections.IEnumerator WaitForMoney()
        {
            const float timeout = 60f;
            var start = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - start < timeout)
            {
                if (Core.Bank.TryGetCashBalance(out var balance))
                {
                    LoggerInstance.Msg($"Money ready after {Time.realtimeSinceStartup - start:0.0}s. Cash: {balance:0.##}");
                    LoggerInstance.Msg("--- vanilla economy (before changes) ---");
                    Probe.EconomyProbe.Run(LoggerInstance.Msg, LoggerInstance.Warning);

                    LoggerInstance.Msg("--- applying bet limit changes ---");
                    Tweaks.BetLimits.Apply(LoggerInstance.Msg, LoggerInstance.Warning);
                    Tweaks.CasinoHours.Apply(LoggerInstance.Msg, LoggerInstance.Warning);

                    LoggerInstance.Msg("--- spawning prize wheel ---");
                    _wheel.Spawn();
                    yield break;
                }
                yield return new WaitForSeconds(1f);
            }

            LoggerInstance.Warning($"Money still unreadable after {timeout}s.");
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
