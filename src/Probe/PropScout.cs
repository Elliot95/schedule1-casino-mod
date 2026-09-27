using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasinoExpansion.Probe
{
    // Lists scene objects roughly the size and shape of an arcade or vending cabinet, so a body
    // for the wheel can be chosen from what the game actually ships rather than guessed from type
    // names. Cloning SlotMachine directly produced its reel drum, because the component sits on
    // the reels and the cabinet is further up the hierarchy -- so the parent chain is reported too.
    public static class PropScout
    {
        private static readonly string[] Wanted =
        {
            "billboard", "sign", "poster", "advert", "banner", "display", "board",
            "vending", "arcade", "atm", "jukebox", "cabinet", "kiosk", "screen",
        };

        public static void Run(Action<string> log, Action<string> warn)
        {
            try
            {
                // Matched by name rather than shape. Grouping by root collapsed everything under
                // region names, and a billboard is wide and flat so a cabinet-shaped filter misses
                // it entirely.
                var hits = new Dictionary<string, (Vector3 size, string path)>();

                foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
                {
                    var name = r.transform.name;
                    var lower = name.ToLowerInvariant();
                    if (!Wanted.Any(w => lower.Contains(w))) continue;

                    var size = r.bounds.size;
                    if (size.magnitude < 0.5f || size.magnitude > 12f) continue;

                    var key = $"{name} [{size.x:0.0}x{size.y:0.0}x{size.z:0.0}]";
                    if (!hits.ContainsKey(key)) hits[key] = (size, Path(r.transform));
                }

                log($"-- Mountable props by name: {hits.Count} distinct");
                foreach (var kv in hits.OrderByDescending(k => k.Value.size.magnitude).Take(30))
                    log($"   {kv.Key}  {kv.Value.path}");
            }
            catch (Exception e) { warn($"prop scout failed: {e.Message}"); }
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null && parts.Count < 5; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
