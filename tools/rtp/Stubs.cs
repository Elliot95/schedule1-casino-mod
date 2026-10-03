using System;
using System.Collections;

// Enough of the mod's runtime for the real game files to compile and run outside Unity. The
// games themselves are NOT reimplemented here -- they are compiled from source, so what this
// measures is the code that actually ships.

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Round(float v) => (float)Math.Round(v, MidpointRounding.AwayFromZero);
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 1e-6f;
    }
}

namespace CasinoExpansion.Casino
{
    public enum ETableGame
    {
        Vanilla = 0, BlackjackHR = 1, RideTheBusHR = 2, Baccarat = 3,
        CasinoHoldem = 4, ThreeCardPoker = 5, PaiGow = 6, RedDog = 7,
    }

    // Stands in for the real session. Answers prompts from a strategy rather than from a
    // player, and does nothing at all with cards, sounds or timing.
    public sealed class TableSession
    {
        public Func<string, string[], int> Strategy;
        public float Bankroll;
        public float Taken;

        public IEnumerator Ask(string prompt, string[] options, Action<int> chosen,
                               float timeout = 25f, int fallback = 1)
        {
            int pick = Strategy != null ? Strategy(prompt, options) : fallback;
            chosen?.Invoke(Mathf_Clamp(pick, options.Length));
            yield break;
        }

        private static int Mathf_Clamp(int v, int len) => v < 0 ? 0 : v >= len ? len - 1 : v;

        public bool TakeRaise(float amount) { Taken += amount; return true; }

        public void Announce(string text) { }
        public void Scores(string dealer, string player) { }
        public IEnumerator Wait(float seconds) { yield break; }
        public IEnumerator Show(HandSet hands) { yield break; }
        public IEnumerator Relayout(HandSet hands) { yield break; }
    }
}
