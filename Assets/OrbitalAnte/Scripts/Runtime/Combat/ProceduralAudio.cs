using OrbitalAnte.Characters;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// Placeholder sounds generated in code so the arena is not silent. Milestone 2 replaces
    /// these with proper pop and ping sounds.
    /// </summary>
    public static class ProceduralAudio
    {
        static AudioClip ping, shot, pop;

        public static AudioClip HitPing => ping != null ? ping : ping = Tone("HitPing", 0.09f, 1850f, 2600f, 38f, 0f);
        public static AudioClip Shot => shot != null ? shot : shot = Tone("Shot", 0.12f, 180f, 70f, 30f, 0.85f);
        public static AudioClip LimbPop => pop != null ? pop : pop = Tone("LimbPop", 0.16f, 420f, 90f, 22f, 0.35f);

        static AudioClip popSmall, popLimb, popTorso, popHead, squelch, gunHeavy, gunLight, reloadOut, reloadIn, dryFire, clatter;

        /// <summary>
        /// A distinct cartoon pop per kind of part: tiny bits "plink", limbs "thwop", the torso
        /// "thoom", the head gets a pop with a bell-like ring on top.
        /// </summary>
        public static AudioClip PopFor(BodyPartId id)
        {
            switch (id)
            {
                case BodyPartId.HandL: case BodyPartId.HandR: case BodyPartId.FootL: case BodyPartId.FootR:
                    return popSmall != null ? popSmall : popSmall = Pop("PopSmall", 0.13f, 900f, 260f, 30f, 0.25f, 0f);
                case BodyPartId.Torso:
                    return popTorso != null ? popTorso : popTorso = Pop("PopTorso", 0.3f, 240f, 55f, 12f, 0.45f, 0f);
                case BodyPartId.Head:
                    return popHead != null ? popHead : popHead = Pop("PopHead", 0.42f, 620f, 140f, 18f, 0.3f, 1320f);
                default:
                    return popLimb != null ? popLimb : popLimb = Pop("PopLimb", 0.19f, 520f, 120f, 20f, 0.35f, 0f);
            }
        }

        public static AudioClip Squelch => squelch != null ? squelch : squelch = Tone("Squelch", 0.14f, 160f, 60f, 26f, 0.7f);
        public static AudioClip GunHeavy => gunHeavy != null ? gunHeavy : gunHeavy = Tone("GunHeavy", 0.22f, 140f, 45f, 18f, 0.9f);
        public static AudioClip GunLight => gunLight != null ? gunLight : gunLight = Tone("GunLight", 0.14f, 320f, 110f, 28f, 0.8f);
        public static AudioClip ReloadOut => reloadOut != null ? reloadOut : reloadOut = Tone("ReloadOut", 0.08f, 1400f, 900f, 50f, 0.6f);
        public static AudioClip ReloadIn => reloadIn != null ? reloadIn : reloadIn = Tone("ReloadIn", 0.1f, 700f, 1600f, 45f, 0.55f);
        public static AudioClip DryFire => dryFire != null ? dryFire : dryFire = Tone("DryFire", 0.05f, 2200f, 1800f, 80f, 0.5f);
        public static AudioClip Clatter => clatter != null ? clatter : clatter = Tone("Clatter", 0.25f, 1100f, 600f, 14f, 0.75f);

        /// <summary>Pop = falling sine "thwop" + noise transient + optional ring partial.</summary>
        static AudioClip Pop(string name, float seconds, float f0, float f1, float decay, float noise, float ring)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random((int)Core.RunSeed.StableHash(name)); // same noise every launch
            double phase = 0, ringPhase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float u = Mathf.Clamp01(t / (seconds * 0.5f));
                float f = Mathf.Lerp(f0, f1, 1f - (1f - u) * (1f - u));
                phase += 2 * Mathf.PI * f / rate;
                float body = Mathf.Sin((float)phase) * Mathf.Exp(-decay * t);
                float click = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-180f * t) * noise * 2f;
                float r = 0f;
                if (ring > 0f)
                {
                    ringPhase += 2 * Mathf.PI * ring / rate;
                    r = Mathf.Sin((float)ringPhase) * Mathf.Exp(-9f * t) * 0.35f;
                }
                float attack = Mathf.Clamp01(i / 48f);
                data[i] = Mathf.Clamp((body + click + r) * attack * 0.7f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Sine sweep from f0 to f1 with exponential decay and optional noise.</summary>
        static AudioClip Tone(string name, float seconds, float f0, float f1, float decay, float noise)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random((int)Core.RunSeed.StableHash(name)); // same noise every launch
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float u = t / seconds;
                float f = Mathf.Lerp(f0, f1, u);
                phase += 2 * Mathf.PI * f / rate;
                float env = Mathf.Exp(-decay * t) * Mathf.Clamp01(i / 64f);
                float s = Mathf.Sin((float)phase) * (1f - noise) + ((float)rng.NextDouble() * 2f - 1f) * noise;
                data[i] = s * env * 0.6f;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
