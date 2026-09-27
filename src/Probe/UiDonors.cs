using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasinoExpansion.Probe
{
    // Finds existing UI widgets worth cloning instead of rebuilding. The game's own settings
    // screens and the casino's Ride the Bus answer panel are both "pick one of several" UIs that
    // already match the art style, which is exactly what the game selector needs.
    public static class UiDonors
    {
        public static void Run(Action<string> log, Action<string> warn)
        {
            try
            {
                var dropdowns = Resources.FindObjectsOfTypeAll<Il2CppTMPro.TMP_Dropdown>();
                log($"[ui] TMP_Dropdown instances: {dropdowns.Length}");
                foreach (var d in dropdowns.Take(10))
                    log($"   {Path(d.transform)}  options={d.options?.Count ?? -1}  active={d.gameObject.activeInHierarchy}");

                var plain = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Dropdown>();
                log($"[ui] legacy Dropdown instances: {plain.Length}");
                foreach (var d in plain.Take(5)) log($"   {Path(d.transform)}");

                // The Ride the Bus answer panel is a ready-made choose-one-of-N in the casino's
                // own UI, so it needs no restyling to look native.
                var rtb = Object.FindObjectsOfType<Il2CppScheduleOne.Casino.UI.RTBInterface>(true).FirstOrDefault();
                if (rtb == null) log("[ui] RTBInterface not found");
                else
                {
                    log($"[ui] RTBInterface.AnswerButtons={rtb.AnswerButtons?.Length ?? -1} " +
                        $"AnswerLabels={rtb.AnswerLabels?.Length ?? -1} AnswerPanel={(rtb.AnswerPanel != null)}");
                    if (rtb.AnswerButtons != null && rtb.AnswerButtons.Length > 0)
                        log($"   first answer button: {Path(rtb.AnswerButtons[0].transform)}");
                }
            }
            catch (Exception e) { warn($"[ui] donor probe failed: {e.Message}"); }
        }

        private static string Path(Transform t)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var cur = t; cur != null && parts.Count < 6; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
