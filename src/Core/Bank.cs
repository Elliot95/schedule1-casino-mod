using System.Collections.Generic;
using MelonLoader;

namespace CasinoExpansion.Core
{
    // MoneyManager.ChangeCashBalance is a LOCAL call with no server authority, so every
    // client applies its own payout. A replayed or duplicated message would therefore mint
    // real money -- hence the applied-key guard on every transaction.
    public static class Bank
    {
        private static readonly HashSet<string> Applied = new HashSet<string>();

        // cashBalance reads through cashInstance, which stays null until save data has
        // populated -- it throws, rather than returning 0, if touched too early.
        public static bool TryGetCashBalance(out float balance)
        {
            balance = 0f;
            try
            {
                var mm = Il2CppScheduleOne.Money.MoneyManager.Instance;
                if (mm == null) return false;
                balance = mm.cashBalance;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsReady => TryGetCashBalance(out _);

        public static bool TryTakeBet(string gameId, int round, float amount)
        {
            if (amount <= 0f) return false;
            if (!TryGetCashBalance(out var balance)) return false;
            if (balance < amount) return false;
            return Apply($"{gameId}:{round}:bet", -amount, "bet");
        }

        // A mid-round raise is a second, independent stake: Red Dog's raise, and later a
        // blackjack double or split. It needs its own key or the idempotence guard would treat
        // it as a duplicate of the opening bet and silently drop it.
        public static bool TryTakeRaise(string gameId, int round, float amount)
        {
            if (amount <= 0f) return false;
            if (!TryGetCashBalance(out var balance)) return false;
            if (balance < amount) return false;
            return Apply($"{gameId}:{round}:raise", -amount, "raise");
        }

        public static void ApplyPayout(string gameId, int round, float amount)
        {
            if (amount <= 0f) return;
            Apply($"{gameId}:{round}:payout", amount, "payout");
        }

        public static void RefundBet(string gameId, int round, float amount)
        {
            if (amount <= 0f) return;
            Apply($"{gameId}:{round}:refund", amount, "refund");
        }

        private static bool Apply(string key, float delta, string kind)
        {
            if (!Applied.Add(key))
            {
                MelonLogger.Warning($"[Bank] duplicate {kind} ignored: {key}");
                return false;
            }

            var mm = Il2CppScheduleOne.Money.MoneyManager.Instance;
            if (mm == null)
            {
                Applied.Remove(key);
                MelonLogger.Error($"[Bank] MoneyManager unavailable, {kind} dropped: {key}");
                return false;
            }

            mm.ChangeCashBalance(delta, true, true);
            TryGetCashBalance(out var after);
            MelonLogger.Msg($"[Bank] {key} delta={delta:+0.##;-0.##} balance={after:0.##}");
            return true;
        }

        public static void ForgetRound(string gameId, int round)
        {
            Applied.Remove($"{gameId}:{round}:bet");
            Applied.Remove($"{gameId}:{round}:raise");
            Applied.Remove($"{gameId}:{round}:payout");
            Applied.Remove($"{gameId}:{round}:refund");
        }
    }
}
