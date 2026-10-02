using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using CasinoExpansion.Casino;
using BetPanel = Il2CppScheduleOne.Casino.UI.CasinoGameBetPanel;

namespace CasinoExpansion.Tweaks
{
    // Raises the table stake to $50,000 for mod games, which three earlier attempts failed to do.
    //
    // Writing BlackjackGameController.MaximumBet crashes the process: it is a const-backed
    // static with no storage behind the interop setter. A postfix on GetBetLimits compiled and
    // ran but left the slider pinned at zero, because Harmony does not marshal those out params
    // correctly. Both routes are dead and documented in BetLimits.
    //
    // This is the third seam and the one that works: GetBetFromSliderValue maps the slider's
    // position to an amount, returns a plain float, and everything downstream -- the displayed
    // amount, BetSliderChanged, SetLocalPlayerBet, and so LocalPlayerBet, which is what the
    // session reads as the buy-in -- goes through it. Rescaling its result moves the whole
    // chain at once without touching a single const or by-ref parameter.
    //
    // Vanilla tables are left exactly as they were: the postfix returns early unless the table
    // is running one of ours.
    public static class BetScaling
    {
        [HarmonyPatch(typeof(BetPanel), nameof(BetPanel.GetBetFromSliderValue))]
        internal static class SliderScalePatch
        {
            private static void Postfix(BetPanel __instance, float sliderVal, ref float __result)
            {
                try
                {
                    var controller = __instance?._gameController;
                    if (controller == null) return;

                    var game = TableGames.For(TableModes.Get(controller));
                    if (game == null) return;                     // vanilla table, leave it alone

                    float t = Normalise(__instance, sliderVal);
                    float span = game.Limits.Max - game.Limits.Min;

                    // Rounded to the nearest $10 so the readout is a clean number at every
                    // slider position rather than $37,412.
                    float raw = game.Limits.Min + t * span;
                    __result = Mathf.Round(raw / 10f) * 10f;

                    // Kept here as well, because rescaling the slider is not sufficient on its
                    // own: SetLocalPlayerBet clamps the value back to the vanilla table maximum
                    // before it ever reaches LocalPlayerBet, so reading that gives $1,000 no
                    // matter what the slider says. The session's own figure is the stake that
                    // actually gets banked.
                    TableSession.For(controller).Stake = __result;
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"[bets] could not rescale the slider: {e.Message}");
                }
            }
        }

        // The readout is written from LocalPlayerBet, which is clamped, so it would show
        // $1,000 under a slider sitting at $50,000. Rewritten after vanilla has had its say.
        [HarmonyPatch(typeof(BetPanel), nameof(BetPanel.RefreshDisplayedBet))]
        internal static class DisplayPatch
        {
            private static void Postfix(BetPanel __instance)
            {
                try
                {
                    var controller = __instance?._gameController;
                    if (controller == null || __instance._betAmount == null) return;
                    if (TableGames.For(TableModes.Get(controller)) == null) return;

                    float stake = TableSession.For(controller).Stake;
                    __instance._betAmount.text = $"${stake:N0}";
                }
                catch { }
            }
        }

        // The slider is almost certainly 0-1, but reading its actual range costs nothing and
        // means a value outside it cannot silently produce a stake beyond the table maximum.
        private static float Normalise(BetPanel panel, float sliderVal)
        {
            var slider = panel._betSlider;
            if (slider == null) return Mathf.Clamp01(sliderVal);

            float min = slider.minValue, max = slider.maxValue;
            if (Mathf.Approximately(max, min)) return 0f;

            return Mathf.Clamp01((sliderVal - min) / (max - min));
        }
    }
}
