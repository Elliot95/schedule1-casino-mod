using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Slot = Il2CppScheduleOne.Casino.SlotMachine;
using Audio = Il2CppScheduleOne.Audio.AudioSourceController;

namespace CasinoExpansion.World
{
    // Win feedback borrowed from the slot machines rather than authored from scratch, the same
    // way the shader and interactables were. Their particles and sounds already match the
    // casino's look, and cloning them inherits correct materials for free.
    public sealed class WheelEffects
    {
        private ParticleSystem[] _jackpotParticles;
        private Audio _miniWin, _bigWin, _jackpot, _spinLoop;

        private const int Copies = 5;      // five times the donor's emitter count

        public void Build(Transform parent, Vector3 emitterLocalPos, float barWidth,
            System.Action<string> log, System.Action<string> warn)
        {
            var donor = Object.FindObjectOfType<Slot>();
            if (donor == null) { warn("[fx] no SlotMachine to borrow effects from"); return; }

            _miniWin = Clone(donor.MiniWinSound, parent, "MiniWin");
            _bigWin = Clone(donor.BigWinSound, parent, "BigWin");
            _jackpot = Clone(donor.JackpotSound, parent, "Jackpot");
            _spinLoop = Clone(donor.SpinLoop, parent, "SpinLoop");

            var source = donor.JackpotParticles;
            if (source == null || source.Length == 0) { warn("[fx] donor had no jackpot particles"); return; }

            // Every donor emitter is cloned several times and the whole set spread evenly across
            // the bar, so confetti erupts along its full width rather than from one or two points.
            int total = source.Length * Copies;
            _jackpotParticles = new ParticleSystem[total];

            for (int i = 0; i < total; i++)
            {
                var clone = Object.Instantiate(source[i % source.Length].gameObject, parent);
                clone.name = $"JackpotParticles{i}";

                float t = total == 1 ? 0.5f : i / (float)(total - 1);
                float x = Mathf.Lerp(-barWidth * 0.5f, barWidth * 0.5f, t);

                // Low and behind the concealing bar, so bursts rise from behind the machine
                // rather than appearing out of a visible point in mid-air.
                clone.transform.localPosition = emitterLocalPos + new Vector3(x, 0f, 0f);
                clone.transform.localRotation = Quaternion.identity;

                _jackpotParticles[i] = clone.GetComponent<ParticleSystem>();
                _jackpotParticles[i]?.Stop();
            }

            log($"[fx] borrowed {_jackpotParticles.Length} particle systems and " +
                $"{new[] { _miniWin, _bigWin, _jackpot, _spinLoop }.Count(a => a != null)} sounds");
        }

        private static Audio Clone(Audio donor, Transform parent, string name)
        {
            if (donor == null) return null;
            var clone = Object.Instantiate(donor.gameObject, parent);
            clone.name = name;
            clone.transform.localPosition = Vector3.zero;
            return clone.GetComponent<Audio>();
        }

        public void OnSpinStart() => Safe(() => _spinLoop?.Play());
        public void OnSpinEnd() => Safe(() => _spinLoop?.Stop());

        // Tiered so a 2x reads differently from a jackpot without having to read the log.
        public void OnResult(bool jackpot, float multiplier)
        {
            Safe(() =>
            {
                if (jackpot)
                {
                    _jackpot?.PlayOneShot();
                    if (_jackpotParticles != null)
                        foreach (var ps in _jackpotParticles) ps?.Play();
                    return;
                }

                if (multiplier >= 4f) _bigWin?.PlayOneShot();
                else if (multiplier > 0f) _miniWin?.PlayOneShot();
            });
        }

        private static void Safe(System.Action action)
        {
            try { action(); }
            catch (System.Exception e) { MelonLoader.MelonLogger.Warning($"[fx] {e.Message}"); }
        }
    }
}
