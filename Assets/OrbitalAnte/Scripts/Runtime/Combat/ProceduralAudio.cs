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

        /// <summary>Sine sweep from f0 to f1 with exponential decay and optional noise.</summary>
        static AudioClip Tone(string name, float seconds, float f0, float f1, float decay, float noise)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
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
