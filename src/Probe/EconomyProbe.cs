using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasinoExpansion.Probe
{
    // Diagnostic only. Reads the vanilla casino's real bet ladders and payout ratios so the
    // prize wheel can be tuned against them instead of against invented numbers.
    // Each section is guarded independently: a partial reading is still useful.
    public static class EconomyProbe
    {
        private static readonly string[] CandidatePrizeIds =
        {
            "goldbar", "goldwatch", "goldchain", "silverwatch", "silverchain",
            "chemistrystation", "growtent", "energydrink", "jar", "brick",
        };

        public static void Run(Action<string> log, Action<string> warn)
        {
            Slots(log, warn);
            TableLimits(log, warn);
            Interaction(log, warn);
            CloneSources(log, warn);
            PrizeIds(log, warn);
            PropScout.Run(log, warn);
        }

        private static void Slots(Action<string> log, Action<string> warn)
        {
            try
            {
                var machines = Object.FindObjectsOfType<Il2CppScheduleOne.Casino.SlotMachine>();

                // BetAmounts is static -- one ladder shared by every machine.
                var amounts = Il2CppScheduleOne.Casino.SlotMachine.BetAmounts;
                var ladder = amounts == null
                    ? "(null)"
                    : string.Join(", ", Enumerable.Range(0, amounts.Length).Select(i => amounts[i].ToString()));
                log($"-- SlotMachine x{machines.Length}, shared bet ladder=[{ladder}]");

                foreach (var m in machines)
                    log($"   {m.name}: current={m.currentBetAmount} spinning={m.IsSpinning}");

                // Calibrated against the measured slots rather than a guess, because the vanilla
                // machines turn out to return well over 100% and no hand-picked table would have
                // landed near that by accident.
                var slotRtp = SlotRtp.Measure(200_000, log, warn);
                Core.Prizes.Calibrate(Games.PrizeWheelGame.Stake, slotRtp ?? 1f, log, warn);

                log($"-- Wheel vs slots: {Core.Prizes.TotalRtp():P2} vs {slotRtp:P2} -> " +
                    $"{(slotRtp.HasValue && Core.Prizes.TotalRtp() <= slotRtp.Value ? "at or below" : "ABOVE")}");
            }
            catch (Exception e) { warn($"slot probe failed: {e.Message}"); }
        }

        private static void TableLimits(Action<string> log, Action<string> warn)
        {
            try
            {
                // Payout ratios are static game constants, not per-table.
                log($"-- Blackjack ratios: payout={Il2CppScheduleOne.Casino.BlackjackGameController.PayoutRatio} " +
                    $"blackjack={Il2CppScheduleOne.Casino.BlackjackGameController.BlackjackPayoutRatio}");

                foreach (var bj in Object.FindObjectsOfType<Il2CppScheduleOne.Casino.BlackjackGameController>())
                {
                    bj.GetBetLimits(out var min, out var max);
                    log($"   Blackjack {bj.name}: bet {min}-{max}");
                }

                foreach (var rtb in Object.FindObjectsOfType<Il2CppScheduleOne.Casino.RTBGameController>())
                {
                    rtb.GetBetLimits(out var min, out var max);
                    log($"   RideTheBus {rtb.name}: bet {min}-{max}");
                }
            }
            catch (Exception e) { warn($"table limits probe failed: {e.Message}"); }
        }

        // The wheel prop must sit on a layer inside this mask or the interaction raycast
        // will never hit it -- there is no interactable registry to fall back on.
        private static void Interaction(Action<string> log, Action<string> warn)
        {
            try
            {
                var im = Object.FindObjectOfType<Il2CppScheduleOne.Interaction.InteractionManager>();
                if (im == null) { warn("InteractionManager not found"); return; }

                int mask = im.Interaction_SearchMask.value;
                var layers = Enumerable.Range(0, 32)
                    .Where(i => (mask & (1 << i)) != 0)
                    .Select(i => $"{i}:{LayerMask.LayerToName(i)}");

                log($"-- Interaction: range={Il2CppScheduleOne.Interaction.InteractionManager.MaxInteractionRange} " +
                    $"rayRadius={Il2CppScheduleOne.Interaction.InteractionManager.RayRadius}");
                log($"   searchMask=0x{mask:X8} layers=[{string.Join(", ", layers)}]");
            }
            catch (Exception e) { warn($"interaction probe failed: {e.Message}"); }
        }

        // InteractableObject carries editor-serialized binding data, so the wheel must clone a
        // live one rather than AddComponent. Confirm a donor actually exists.
        private static void CloneSources(Action<string> log, Action<string> warn)
        {
            try
            {
                var doors = Object.FindObjectsOfType<Il2CppScheduleOne.Doors.StaticDoor>();
                int withIntObj = doors.Count(d => d.IntObj != null);
                log($"-- Clone donors: StaticDoor x{doors.Length} ({withIntObj} with IntObj)");

                var donor = doors.FirstOrDefault(d => d.IntObj != null);
                if (donor != null)
                {
                    var io = donor.IntObj;
                    log($"   donor '{donor.name}' layer={io.gameObject.layer}:{LayerMask.LayerToName(io.gameObject.layer)} range={io.MaxInteractionRange}");
                }
                else warn("no StaticDoor with an IntObj to clone from");
            }
            catch (Exception e) { warn($"clone source probe failed: {e.Message}"); }
        }

        private static void PrizeIds(Action<string> log, Action<string> warn)
        {
            try
            {
                var valid = CandidatePrizeIds.Where(Il2CppScheduleOne.Registry.ItemExists).ToArray();
                var invalid = CandidatePrizeIds.Except(valid).ToArray();

                log($"-- Prize ids valid: [{string.Join(", ", valid)}]");
                if (invalid.Length > 0) log($"   invalid: [{string.Join(", ", invalid)}]");
            }
            catch (Exception e) { warn($"prize id probe failed: {e.Message}"); }
        }
    }
}
