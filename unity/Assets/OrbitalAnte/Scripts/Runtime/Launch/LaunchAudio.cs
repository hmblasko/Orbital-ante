using System;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Launch
{
    /// <summary>
    /// The sound of the main engines, made from maths when the ship is built so there is nothing to import.
    /// Three layers: a deep roar you feel through the seat, the tearing crackle a rocket makes in air close to the
    /// ground, and the cough and whoosh of ignition. Placeholders for recorded sound, shaped to be heavy rather
    /// than hissy. No music, no jingles.
    /// </summary>
    public sealed class LaunchAudio
    {
        const int Rate = 22050;

        public AudioClip Roar, Crackle, Ignite, Release;

        public void Dispose()
        {
            AudioClip[] all = { Roar, Crackle, Ignite, Release };
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null) UnityEngine.Object.Destroy(all[i]);
            }
        }

        public static LaunchAudio Build()
        {
            var a = new LaunchAudio();

            // The roar: noise with nearly all the top taken off, riding on a slow beating pair of very low notes.
            // Every note fits the clip a whole number of times, and the noise is cross-faded, so it loops cleanly.
            a.Roar = Loop("LaunchRoar", 3f, 31, (t, deep, rough) =>
            {
                float beat = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * (1f / 3f) * 4f * t);
                float low = Mathf.Sin(2f * Mathf.PI * 41f * t) * 0.34f + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.22f + Mathf.Sin(2f * Mathf.PI * 82f * t) * 0.1f;
                return deep * 2.6f + low * beat + rough * 0.05f;
            });

            // The crackle: short hard bursts at uneven moments over a rough bed. It is what tells you there is air
            // and ground out there; in space it is silent.
            a.Crackle = Loop("LaunchCrackle", 2.5f, 32, (t, deep, rough) =>
            {
                float gate = Mathf.PerlinNoise(t * 31f, 0.4f);
                float pop = gate > 0.6f ? (gate - 0.6f) * 2.5f : 0f;
                return rough * (0.1f + pop * 0.9f) + deep * 0.5f * pop;
            });

            // Ignition: a valve cough, then a rush that climbs.
            a.Ignite = Shot("LaunchIgnite", 1.1f, 33, (t, deep, rough) =>
            {
                float cough = Mathf.Sin(2f * Mathf.PI * (130f - 60f * t) * t) * Mathf.Exp(-t * 16f) * 0.9f;
                float rush = (deep * 2.2f + rough * 0.12f) * Mathf.Clamp01(t / 0.9f) * Mathf.Clamp01((1.1f - t) / 0.2f);
                return cough + rush;
            });

            // Letting go of the pad: one heavy thump through the frame.
            a.Release = Shot("LaunchRelease", 0.6f, 34, (t, deep, rough) =>
                Mathf.Sin(2f * Mathf.PI * (58f - 26f * t) * t) * Mathf.Exp(-t * 7f) + deep * 1.5f * Mathf.Exp(-t * 9f));

            return a;
        }

        static AudioClip Loop(string name, float seconds, int seed, Func<float, float, float, float> shape)
        {
            float[] data = Fill(seconds + 0.25f, seed, shape);
            int count = Mathf.RoundToInt(seconds * Rate);
            int blend = data.Length - count;
            var looped = new float[count];
            Array.Copy(data, looped, count);
            // The spare quarter second is folded back over the start, so the end runs into the beginning.
            for (int i = 0; i < blend; i++)
            {
                float k = i / (float)blend;
                looped[i] = data[count + i] * (1f - k) + data[i] * k;
            }
            return ToClip(name, looped);
        }

        static AudioClip Shot(string name, float seconds, int seed, Func<float, float, float, float> shape)
        {
            float[] data = Fill(seconds, seed, shape);
            for (int i = 0; i < 300 && i < data.Length; i++) data[data.Length - 1 - i] *= i / 300f;
            return ToClip(name, data);
        }

        static float[] Fill(float seconds, int seed, Func<float, float, float, float> shape)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[count];
            var rng = new SeededRandom((ulong)(seed * 7919 + 13));
            float deep = 0f, deeper = 0f;
            for (int i = 0; i < count; i++)
            {
                float rough = rng.Range(-1f, 1f);
                // Two gentle filters in a row: a rumble with no hiss left in it.
                deep += (rough - deep) * 0.035f;
                deeper += (deep - deeper) * 0.12f;
                data[i] = Mathf.Clamp(shape(i / (float)Rate, deeper, rough), -1f, 1f);
            }
            return data;
        }

        static AudioClip ToClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
