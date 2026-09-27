using System.Linq;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;
using Zone = Il2CppScheduleOne.Map.TimedAccessZone;

namespace CasinoExpansion.Tweaks
{
    // The casino opens only at the end of the in-game day, which makes anything built inside it
    // painful to test and limits when it can be played at all.
    //
    // Access is governed by TimedAccessZone.OpenTime / CloseTime, both plain settable ints -- so
    // unlike the table bet limits, this needs no Harmony patch and no writes to const-backed
    // storage. Widening the window is enough; GetIsOpen reads these each minute pass.
    public static class CasinoHours
    {
        private static MelonPreferences_Entry<bool> _enabled;

        public static void InitPreferences(MelonPreferences_Category cat)
        {
            _enabled = cat.CreateEntry("CasinoOpen24h", true,
                description: "Keep the casino open around the clock.");
        }

        public static void Apply(System.Action<string> log, System.Action<string> warn)
        {
            if (!(_enabled?.Value ?? true)) { log("Casino hours left at vanilla."); return; }

            try
            {
                // The casino's zone is identified by proximity to the machines rather than by
                // name, so this keeps working if the zone is ever renamed.
                var anchors = Object.FindObjectsOfType<Il2CppScheduleOne.Casino.SlotMachine>()
                    .Select(m => m.transform.position).ToArray();
                if (anchors.Length == 0) { warn("no slot machines found, cannot locate the casino zone"); return; }

                var centre = new Vector3(anchors.Average(a => a.x), anchors.Average(a => a.y), anchors.Average(a => a.z));

                var zones = Object.FindObjectsOfType<Zone>();
                if (zones.Length == 0) { warn("no TimedAccessZone in scene"); return; }

                var target = zones.OrderBy(z => Vector3.Distance(z.transform.position, centre)).First();
                float distance = Vector3.Distance(target.transform.position, centre);

                log($"Casino zone '{target.name}' {distance:0.#}m from the machines, " +
                    $"was open {target.OpenTime}-{target.CloseTime}");

                target.OpenTime = 0;
                target.CloseTime = 2359;

                log($"Casino now open {target.OpenTime}-{target.CloseTime} (isOpen={target.GetIsOpen()})");
            }
            catch (System.Exception e) { warn($"casino hours change failed: {e.Message}"); }
        }
    }
}
