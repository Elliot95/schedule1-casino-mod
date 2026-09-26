using System.Collections;
using System.Globalization;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using CasinoExpansion.Core;
using CasinoExpansion.World;

namespace CasinoExpansion.Games
{
    public sealed class PrizeWheelGame
    {
        public const string GameId = "wheel";

        private readonly PrizeWheelProp _prop = new PrizeWheelProp();
        private int _round;
        private bool _busy;

        private static MelonPreferences_Entry<string> _position;
        private static MelonPreferences_Entry<float> _stake;

        public static void InitPreferences(MelonPreferences_Category cat)
        {
            _position = cat.CreateEntry("WheelPosition", "",
                description: "Wheel world position as x,y,z. Empty spawns it just in front of the player.");
            _stake = cat.CreateEntry("WheelStake", 10f,
                description: "Cash staked per spin.");
        }

        public bool IsSpawned => _prop.Root != null;

        public void Spawn()
        {
            if (IsSpawned) return;

            var player = Il2CppScheduleOne.PlayerScripts.Player.Local;
            if (player == null) { MelonLogger.Warning("[wheel] no local player yet"); return; }

            var (pos, rot) = ResolvePlacement(player.transform);
            _prop.Build(pos, rot);

            InteractableFactory.Attach(_prop.Root, "Spin the wheel",
                (UnityAction)OnInteract, MelonLogger.Msg, MelonLogger.Warning);

            MelonLogger.Msg($"[wheel] spawned at {pos.x:0.##},{pos.y:0.##},{pos.z:0.##} " +
                            $"(player at {player.transform.position.x:0.##},{player.transform.position.y:0.##},{player.transform.position.z:0.##})");
        }

        // No hardcoded casino coordinates yet, so the default is wherever the player is standing.
        // That also keeps testing viable while the casino only opens at end of day.
        private static (Vector3, Quaternion) ResolvePlacement(Transform player)
        {
            var configured = _position?.Value;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var parts = configured.Split(',');
                if (parts.Length == 3
                    && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                    return (new Vector3(x, y, z), Quaternion.identity);

                MelonLogger.Warning($"[wheel] could not parse WheelPosition '{configured}', using player position");
            }

            var forward = player.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;

            var pos = player.position + forward * 2f + Vector3.up * 0.9f;
            return (pos, Quaternion.LookRotation(-forward, Vector3.up));
        }

        // Move the wheel to wherever the player is standing. The casino only opens at end of day,
        // so being able to reposition on demand is what makes this testable at all.
        public void Respawn()
        {
            _prop.Destroy();
            Spawn();
        }

        private void OnInteract()
        {
            if (_busy) return;
            MelonCoroutines.Start(RunRound());
        }

        private IEnumerator RunRound()
        {
            _busy = true;
            _round++;

            float stake = Mathf.Max(1f, _stake?.Value ?? 10f);

            if (!Bank.TryTakeBet(GameId, _round, stake))
            {
                Bank.TryGetCashBalance(out var bal);
                MelonLogger.Msg($"[wheel] round {_round} refused: stake {stake:0.##}, balance {bal:0.##}");
                _busy = false;
                yield break;
            }

            // Kept inside the float-exact integer range so the seed can be replicated verbatim
            // through the game's float-only channel later.
            int seed = Random.Range(1, RoundState.MaxExactInt);
            int slice = seed % PrizeWheelSlices.Count;

            MelonLogger.Msg($"[wheel] round {_round} spinning (seed {seed}, slice {slice})");
            yield return _prop.Spin(slice);

            Resolve(slice, seed, stake);
            _busy = false;
        }

        private void Resolve(int slice, int seed, float stake)
        {
            MelonLogger.Msg($"[wheel] round {_round} result: {Prizes.Describe(slice, stake)}");

            float mult = Prizes.CashMultiplier(slice);
            if (mult > 0f) Bank.ApplyPayout(GameId, _round, stake * mult);

            if (slice == PrizeWheelSlices.GrandPrize) AwardGrandPrize(seed);
        }

        private static void AwardGrandPrize(int seed)
        {
            var id = Prizes.GrandPrizeItemId(seed);
            try
            {
                if (!Il2CppScheduleOne.Registry.ItemExists(id))
                {
                    MelonLogger.Warning($"[wheel] prize id '{id}' not in Registry, skipping award");
                    return;
                }

                var instance = Il2CppScheduleOne.Registry.GetItem(id).GetDefaultInstance(1);
                var inventory = Il2CppScheduleOne.PlayerScripts.PlayerInventory.Instance;

                if (inventory == null) { MelonLogger.Warning("[wheel] no PlayerInventory"); return; }

                // Checked first, because a full inventory would otherwise swallow the item.
                if (!inventory.CanItemFitInInventory(instance, 1))
                {
                    MelonLogger.Warning($"[wheel] no inventory room for '{id}' - prize lost");
                    return;
                }

                inventory.AddItemToInventory(instance);
                MelonLogger.Msg($"[wheel] awarded grand prize: {id}");
            }
            catch (System.Exception e)
            {
                MelonLogger.Error($"[wheel] grand prize award failed for '{id}': {e.Message}");
            }
        }
    }
}
