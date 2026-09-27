using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using CasinoExpansion.World;
using Object = UnityEngine.Object;
using IntObj = Il2CppScheduleOne.Interaction.InteractableObject;

namespace CasinoExpansion.Casino
{
    // Puts a "change game" control on each casino table. The table is the interaction point the
    // game already trains players to use, and unlike the staff it is present whatever the hour,
    // which matters now the casino never closes.
    public static class TableToggle
    {
        public static void InstallAll(Action<string> log, Action<string> warn)
        {
            var tables = Object.FindObjectsOfType<Il2CppScheduleOne.Casino.CasinoGameController>();
            if (tables.Length == 0) { warn("[table] no casino tables found"); return; }

            foreach (var table in tables) Install(table, log, warn);
            log($"[table] game toggle installed on {tables.Length} tables");
        }

        private static void Install(Il2CppScheduleOne.Casino.CasinoGameController table,
            Action<string> log, Action<string> warn)
        {
            try
            {
                var anchor = new GameObject($"GameToggle_{table.name}");
                anchor.transform.SetParent(table.transform, false);

                // Offset to the side of the table so it does not compete with the seat the player
                // already uses to sit down and play.
                anchor.transform.localPosition = new Vector3(0.9f, 0.45f, 0f);

                var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.transform.SetParent(anchor.transform, false);
                pad.transform.localScale = new Vector3(0.16f, 0.08f, 0.16f);
                Object.Destroy(pad.GetComponent<Collider>());

                IntObj handle = null;
                handle = InteractableFactory.Attach(anchor, Label(table), new Vector3(1.6f, 2.4f, 1.6f),
                    (UnityAction)(() =>
                    {
                        TableModes.Cycle(table);
                        handle?.SetMessage(Label(table));
                    }),
                    log, warn);

                if (handle == null) warn($"[table] could not attach toggle to '{table.name}'");
            }
            catch (Exception e) { warn($"[table] toggle failed on '{table.name}': {e.Message}"); }
        }

        private static string Label(Il2CppScheduleOne.Casino.CasinoGameController table) =>
            $"Change game (now: {TableModes.Describe(TableModes.Get(table))})";
    }
}
