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
        public static void Run(Action<string> log, Action<string> warn)
        {
            try
            {
                var seen = new Dictionary<string, (Vector3 size, string path, int count)>();

                foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
                {
                    var size = r.bounds.size;

                    // Cabinet-shaped: taller than wide, roughly person height, not a wall.
                    if (size.y < 1.0f || size.y > 2.4f) continue;
                    if (size.x < 0.4f || size.x > 1.8f) continue;
                    if (size.z < 0.2f || size.z > 1.8f) continue;

                    var root = r.transform;
                    while (root.parent != null && root.parent.name != "Map") root = root.parent;

                    var key = root.name;
                    if (seen.TryGetValue(key, out var existing))
                        seen[key] = (existing.size, existing.path, existing.count + 1);
                    else
                        seen[key] = (size, Path(r.transform), 1);
                }

                log($"-- Prop candidates (cabinet-shaped): {seen.Count} distinct");
                foreach (var kv in seen.OrderByDescending(k => k.Value.count).Take(25))
                    log($"   {kv.Key} x{kv.Value.count}  size={kv.Value.size}  e.g. {kv.Value.path}");
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
