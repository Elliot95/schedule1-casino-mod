using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasinoExpansion.Probe
{
    // Is there anyone standing in the casino worth talking to? The assembly has no croupier,
    // cashier or bartender type, so the only way to know is to look at who is actually placed
    // near the tables in a live scene.
    public static class CasinoStaff
    {
        public static void Run(Action<string> log, Action<string> warn)
        {
            try
            {
                // Casino centre taken from the machines themselves rather than hardcoded.
                var anchors = Object.FindObjectsOfType<Il2CppScheduleOne.Casino.SlotMachine>()
                    .Select(m => m.transform.position)
                    .Concat(Object.FindObjectsOfType<Il2CppScheduleOne.Casino.CasinoGamePlayers>()
                        .Select(p => p.transform.position))
                    .ToArray();

                if (anchors.Length == 0) { warn("[staff] no casino objects found"); return; }

                var centre = new Vector3(anchors.Average(a => a.x), anchors.Average(a => a.y), anchors.Average(a => a.z));
                log($"[staff] casino centre ~{centre}, from {anchors.Length} anchors");

                var npcs = Object.FindObjectsOfType<Il2CppScheduleOne.NPCs.NPC>();

                // No distance cap: an earlier run capped at 30m and, because it fired seconds
                // after load before NPCs had finished spawning, reported whoever happened to
                // exist rather than the actual casino staff. Nearest-first tells the truth.
                var ranked = npcs
                    .Select(n => (npc: n, dist: Vector3.Distance(n.transform.position, centre)))
                    .OrderBy(x => x.dist)
                    .ToArray();

                log($"[staff] {npcs.Length} NPCs in scene; nearest 15 to the casino:");
                foreach (var (npc, dist) in ranked.Take(15))
                {
                    var handler = npc.GetComponentInChildren<Il2CppScheduleOne.Dialogue.DialogueHandler>(true);
                    var id = string.IsNullOrEmpty(npc.FirstName) ? npc.name : $"{npc.FirstName} {npc.LastName}".Trim();
                    log($"   {id}  [{npc.name}]  {dist:0.#}m  dialogue={handler != null}");
                }
            }
            catch (Exception e) { warn($"[staff] probe failed: {e.Message}"); }
        }
    }
}
