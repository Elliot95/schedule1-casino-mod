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
        private Il2CppScheduleOne.Interaction.InteractableObject _spinInteractable;

        private static MelonPreferences_Entry<string> _position;
        private static MelonPreferences_Entry<float> _stake;
        private static MelonPreferences_Entry<float> _testJackpotChance;

        public static void InitPreferences(MelonPreferences_Category cat)
        {
            _position = cat.CreateEntry("WheelPosition", "",
                description: "Wheel world position as x,y,z. Empty spawns it just in front of the player.");
            _testJackpotChance = cat.CreateEntry("TestJackpotChance", 0f,
                description: "TESTING ONLY. Probability 0-1 of forcing a jackpot. Set back to 0 for real odds.");
            _stake = cat.CreateEntry("WheelStake", 10f,
                description: "Cash staked per spin.");
        }

        // Mirrors the slot machines' ladder shape but runs higher, capped at 1500.
        public static readonly int[] BetLadder = { 5, 10, 25, 50, 100, 250, 500, 1000, 1500 };
        private static int _betIndex = 1;   // default 10, matching the machines' opening bet

        // Calibration needs the stake before any round runs.
        public static float Stake => BetLadder[Mathf.Clamp(_betIndex, 0, BetLadder.Length - 1)];

        public bool IsSpawned => _prop.Root != null;

        public void Spawn()
        {
            if (IsSpawned) return;

            var player = Il2CppScheduleOne.PlayerScripts.Player.Local;
            if (player == null) { MelonLogger.Warning("[wheel] no local player yet"); return; }

            var (pos, rot) = ResolvePlacement(player.transform);
            _prop.Build(pos, rot);

            // Spin is driven by the button on the front, not the whole cabinet: a body-sized
            // collider caught the player's aim from well off the prop.
            _spinInteractable = InteractableFactory.Attach(_prop.SpinButtonAnchor, SpinMessage(),
                new Vector3(1.15f, 1.5f, 1.6f), (UnityAction)OnInteract, MelonLogger.Msg, MelonLogger.Warning);

            InteractableFactory.Attach(_prop.BetUpAnchor, "Raise bet", new Vector3(1.2f, 1.4f, 1.2f),
                (UnityAction)(() => ChangeBet(1)), MelonLogger.Msg, MelonLogger.Warning);
            InteractableFactory.Attach(_prop.BetDownAnchor, "Lower bet", new Vector3(1.2f, 1.4f, 1.2f),
                (UnityAction)(() => ChangeBet(-1)), MelonLogger.Msg, MelonLogger.Warning);

            _prop.SetBet($"BET ${Stake:N0}");
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

            var pos = player.position + forward * 1.8f + Vector3.up * 0.85f;

            // Spawning into a wall looks exactly like a rendering bug, which cost a lot of time
            // to rule out once. If the spot is occupied, pull it back toward the player.
            if (Physics.CheckSphere(pos, 0.7f))
            {
                pos = player.position + forward * 1.1f + Vector3.up * 0.85f;
                MelonLogger.Msg("[wheel] spawn point was obstructed, placed closer instead");
            }

            return (pos, Quaternion.LookRotation(-forward, Vector3.up));
        }

        // Move the wheel to wherever the player is standing. The casino only opens at end of day,
        // so being able to reposition on demand is what makes this testable at all.
        public void Respawn()
        {
            _prop.Destroy();
            Spawn();
        }

        private static string SpinMessage() => $"Spin the wheel (${Stake:N0})";

        private void ChangeBet(int direction)
        {
            if (_busy) return;

            int next = Mathf.Clamp(_betIndex + direction, 0, BetLadder.Length - 1);
            if (next == _betIndex) return;

            _betIndex = next;
            _spinInteractable?.SetMessage(SpinMessage());
            _prop.SetBet($"BET ${Stake:N0}");
            MelonLogger.Msg($"[wheel] bet now {Stake:N0}");
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

            float stake = Stake;

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

            // Testing aid: the jackpot is otherwise 1 in 53, which is far too rare to verify the
            // particles and sound. Left at 0 by default so real odds are never silently skewed.
            float forceChance = _testJackpotChance?.Value ?? 0f;
            if (forceChance > 0f && Random.value < forceChance)
            {
                slice = PrizeWheelSlices.GrandPrize;
                MelonLogger.Warning($"[wheel] TEST MODE forced jackpot (chance {forceChance:P0})");
            }

            MelonLogger.Msg($"[wheel] round {_round} spinning (seed {seed}, slice {slice})");
            _prop.SetText("SPINNING");
            _prop.Effects.OnSpinStart();
            yield return _prop.Spin(slice);
            _prop.Effects.OnSpinEnd();

            Resolve(slice, stake);
            _busy = false;
        }

        private void Resolve(int slice, float stake)
        {
            MelonLogger.Msg($"[wheel] round {_round} result: {Prizes.Describe(slice, stake)}");

            float mult = Prizes.CashMultiplier(slice);
            if (mult > 0f) Bank.ApplyPayout(GameId, _round, stake * mult);

            _prop.SetText(mult > 0f ? $"${stake * mult:N0}" : "NO WIN");
            _prop.Effects.OnResult(slice == PrizeWheelSlices.GrandPrize, mult);
        }

    }
}
