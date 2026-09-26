using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Object = UnityEngine.Object;
using Slot = Il2CppScheduleOne.Casino.SlotMachine;

namespace CasinoExpansion.Probe
{
    // The slot machine's win table lives in native code and cannot be read from signatures, so
    // the only way to learn its return-to-player is to run the game's own functions many times
    // and measure. GetRandomSymbol, EvaluateOutcome and GetWinAmount are all callable and have
    // no side effects, so this touches no game state.
    public static class SlotRtp
    {
        public static float? Measure(int trials, Action<string> log, Action<string> warn)
        {
            try
            {
                var machine = Object.FindObjectOfType<Slot>();
                if (machine == null) { warn("no SlotMachine to measure"); return null; }

                int reels = machine.Reels?.Length ?? 3;
                const int bet = 100;

                var symbols = new Il2CppStructArray<Slot.ESymbol>(reels);
                long staked = 0, won = 0;
                var outcomes = new Dictionary<string, int>();

                for (int i = 0; i < trials; i++)
                {
                    for (int r = 0; r < reels; r++) symbols[r] = Slot.GetRandomSymbol();

                    var outcome = machine.EvaluateOutcome(symbols);
                    won += machine.GetWinAmount(outcome, bet);
                    staked += bet;

                    var key = outcome.ToString();
                    outcomes[key] = outcomes.TryGetValue(key, out var n) ? n + 1 : 1;
                }

                float rtp = staked == 0 ? 0f : (float)won / staked;
                log($"-- Slot RTP over {trials:N0} spins ({reels} reels): {rtp:P2} (staked {staked:N0}, won {won:N0})");
                foreach (var kv in outcomes.OrderByDescending(k => k.Value))
                    log($"   {kv.Key}: {kv.Value:N0} ({kv.Value / (float)trials:P2})");

                return rtp;
            }
            catch (Exception e)
            {
                warn($"slot RTP measurement failed: {e.Message}");
                return null;
            }
        }
    }
}
