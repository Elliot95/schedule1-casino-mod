namespace CasinoExpansion.Core
{
    public enum EStage
    {
        Idle = 0,
        Betting = 1,
        Locked = 2,
        Dealing = 3,
        PlayerAction = 4,
        Resolving = 5,
        Payout = 6,
        Aborted = 7,
    }

    // Replicated through CasinoGamePlayers.SendPlayerFloat/SendPlayerBool, which take an
    // arbitrary string key. Keys are namespaced so vanilla casino logic ignores them.
    public static class Keys
    {
        public const string Prefix = "mod.";

        public static string Stage(string gameId)  => $"{Prefix}{gameId}.stage";
        public static string Round(string gameId)  => $"{Prefix}{gameId}.round";
        public static string Seq(string gameId)    => $"{Prefix}{gameId}.seq";
        public static string Seed(string gameId)   => $"{Prefix}{gameId}.seed";
        public static string Result(string gameId) => $"{Prefix}{gameId}.result";
        public static string Bet(string gameId)    => $"{Prefix}{gameId}.bet";
        public static string Ready(string gameId)  => $"{Prefix}{gameId}.ready";

        // Which game a seated player has chosen. Replicated so every seat can be checked for
        // agreement before a round is dealt.
        public static string Game(string gameId)   => $"{Prefix}{gameId}.game";
    }

    public sealed class RoundState
    {
        // float holds integers exactly only up to 2^24, so every replicated integer
        // (seed, result, counters) must stay inside this range.
        public const int MaxExactInt = 16_777_216;

        public int Round;
        public int Seq;
        public EStage Stage = EStage.Idle;
        public int Seed;
        public int Result = -1;

        public bool HasResult => Result >= 0;

        public void BeginRound(int seed)
        {
            Round++;
            Seq = 0;
            Stage = EStage.Betting;
            Seed = seed;
            Result = -1;
        }

        public void Abort()
        {
            Stage = EStage.Aborted;
            Result = -1;
        }

        public override string ToString() =>
            $"round={Round} seq={Seq} stage={Stage} seed={Seed} result={Result}";
    }
}
