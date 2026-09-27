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
                log($"[staff] {npcs.Length} NPCs in scene; those within 30m of the casino:");

                var near = npcs
                    .Select(n => (npc: n, dist: Vector3.Distance(n.transform.position, centre)))
                    .Where(x => x.dist <= 30f)
                    .OrderBy(x => x.dist)
                    .ToArray();

                if (near.Length == 0) log("   none — the casino appears to be unstaffed");

                foreach (var (npc, dist) in near.Take(15))
                {
                    var hasDialogue = npc.GetComponentInChildren<Il2CppScheduleOne.Dialogue.DialogueHandler>(true) != null;
                    log($"   {npc.name} at {dist:0.#}m  dialogue={hasDialogue}");
                }
            }
            catch (Exception e) { warn($"[staff] probe failed: {e.Message}"); }
        }
    }
}
