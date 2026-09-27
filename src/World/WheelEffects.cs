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

        public void Build(Transform parent, Vector3 emitterLocalPos, System.Action<string> log, System.Action<string> warn)
        {
            var donor = Object.FindObjectOfType<Slot>();
            if (donor == null) { warn("[fx] no SlotMachine to borrow effects from"); return; }

            _miniWin = Clone(donor.MiniWinSound, parent, "MiniWin");
            _bigWin = Clone(donor.BigWinSound, parent, "BigWin");
            _jackpot = Clone(donor.JackpotSound, parent, "Jackpot");
            _spinLoop = Clone(donor.SpinLoop, parent, "SpinLoop");

            var source = donor.JackpotParticles;
            if (source == null || source.Length == 0) { warn("[fx] donor had no jackpot particles"); return; }

            _jackpotParticles = new ParticleSystem[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var clone = Object.Instantiate(source[i].gameObject, parent);
                clone.name = $"JackpotParticles{i}";

                // Emitters sit low and behind the concealing bar, so bursts rise from behind the
                // machine rather than appearing out of a visible point in mid-air.
                clone.transform.localPosition = emitterLocalPos + new Vector3((i - (source.Length - 1) * 0.5f) * 0.35f, 0f, 0f);
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
