using System;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// The ship's sounds, made from simple maths when the ship is built so there is nothing to import. They are
    /// placeholders for recorded sounds, but shaped to feel right: a click and a hiss for the seat, a ratchet
    /// that climbs to a chime for a finished repair (the one repair sound, shared with satellites later).
    /// </summary>
    public sealed class ShipAudio
    {
        const int Rate = 22050;

        public AudioClip BeltClick, SeatHiss, BeltRelease, HatchMove, HatchThunk, LiftHum, RepairTick, RepairDone;
        public AudioClip GlassCrack, GlassShatter, Thud, Crunch, TearOff, Deny, Lever, Groan, Scrape;

        /// <summary>Frees the clips. They are made afresh for every run.</summary>
        public void Dispose()
        {
            AudioClip[] all = { BeltClick, SeatHiss, BeltRelease, HatchMove, HatchThunk, LiftHum, RepairTick, RepairDone, GlassCrack, GlassShatter, Thud, Crunch, TearOff, Deny, Lever, Groan, Scrape };
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null) UnityEngine.Object.Destroy(all[i]);
            }
        }

        public static ShipAudio Build()
        {
            var a = new ShipAudio();
            a.BeltClick = Make("BeltClick", 0.12f, 1, (t, r) => (Click(t, 0f, 2600f, 90f) + Click(t, 0.035f, 1700f, 70f) * 0.8f) * 0.9f);
            // A hydraulic settle: air let out through a valve, dropping in pitch as it empties.
            a.SeatHiss = Make("SeatHiss", 0.75f, 2, (t, r) =>
            {
                float envelope = Mathf.Clamp01(t / 0.04f) * Mathf.Exp(-t * 4.2f);
                return r * envelope * 0.55f + Mathf.Sin(2f * Mathf.PI * (90f - 40f * t) * t) * Mathf.Exp(-t * 6f) * 0.35f;
            }, lowPass: 0.35f);
            a.BeltRelease = Make("BeltRelease", 0.3f, 3, (t, r) => Click(t, 0f, 1500f, 60f) * 0.8f + r * Mathf.Exp(-t * 14f) * 0.25f, lowPass: 0.5f);
            a.HatchMove = Make("HatchMove", 0.5f, 4, (t, r) =>
            {
                float envelope = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.5f - t) / 0.08f);
                float motor = Mathf.Sin(2f * Mathf.PI * (140f + 60f * t) * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * (283f + 120f * t) * t);
                return (motor * 0.28f + r * 0.12f) * envelope;
            }, lowPass: 0.4f);
            a.HatchThunk = Make("HatchThunk", 0.25f, 5, (t, r) => Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 22f) * 0.9f + r * Mathf.Exp(-t * 60f) * 0.3f, lowPass: 0.3f);
            // Every note fits a whole number of times into the clip, so it loops without a click.
            a.LiftHum = Make("LiftHum", 1.5f, 6, (t, r) =>
            {
                float wobble = 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 4f * t);
                return (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.16f + Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.08f) * wobble;
            }, loop: true);
            a.RepairTick = Make("RepairTick", 0.07f, 7, (t, r) => Click(t, 0f, 2100f, 110f) * 0.7f + r * Mathf.Exp(-t * 90f) * 0.3f);
            // Finished: two rising ratchet clicks, then a bright two-note chime that rings on a little.
            a.RepairDone = Make("RepairDone", 0.9f, 8, (t, r) =>
            {
                float s = Click(t, 0f, 1800f, 80f) * 0.6f + Click(t, 0.07f, 2400f, 80f) * 0.6f;
                s += Bell(t, 0.14f, 880f) * 0.5f + Bell(t, 0.26f, 1318.5f) * 0.55f;
                return s;
            });
            a.GlassCrack = Make("GlassCrack", 0.16f, 9, (t, r) => Click(t, 0f, 5200f, 70f) * 0.7f + Click(t, 0.02f, 3900f, 55f) * 0.5f + r * Mathf.Exp(-t * 55f) * 0.35f);
            a.GlassShatter = Make("GlassShatter", 0.8f, 10, (t, r) =>
            {
                float burst = r * Mathf.Exp(-t * 9f) * 0.7f;
                float tinkle = 0f;
                for (int i = 0; i < 9; i++) tinkle += Click(t, 0.04f + i * 0.06f + 0.02f * Mathf.Sin(i * 12.9f), 3200f + 420f * ((i * 7) % 9), 45f) * 0.22f;
                return burst + tinkle;
            });
            a.Thud = Make("Thud", 0.35f, 11, (t, r) => Mathf.Sin(2f * Mathf.PI * (62f - 30f * t) * t) * Mathf.Exp(-t * 13f) + r * Mathf.Exp(-t * 40f) * 0.35f, lowPass: 0.25f);
            a.Crunch = Make("Crunch", 0.45f, 12, (t, r) =>
            {
                float grit = r * (0.5f + 0.5f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 37f * t))) * Mathf.Exp(-t * 8f);
                return grit * 0.6f + Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 12f) * 0.5f;
            }, lowPass: 0.45f);
            a.TearOff = Make("TearOff", 0.7f, 13, (t, r) =>
            {
                float screech = Mathf.Sin(2f * Mathf.PI * (300f + 900f * t * t) * t) * Mathf.Exp(-t * 5f) * 0.25f;
                return screech + r * Mathf.Exp(-t * 6f) * 0.5f + Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 9f) * 0.6f;
            }, lowPass: 0.5f);
            a.Deny = Make("Deny", 0.22f, 14, (t, r) => (Mathf.Sin(2f * Mathf.PI * 180f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 190f * t)) * Mathf.Clamp01((0.22f - t) / 0.05f) * 0.3f);
            // The frame settling after a real blow: two low notes rubbing against each other, sagging in pitch,
            // with a few ticks of metal taking up the strain. Heard through the hull, so it is dull, not bright.
            a.Groan = Make("Groan", 1.6f, 16, (t, r) =>
            {
                float sag = 1f - 0.18f * t;
                float body = Mathf.Sin(2f * Mathf.PI * 71f * sag * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 74.5f * sag * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 143f * sag * t);
                float swell = Mathf.Clamp01(t / 0.15f) * Mathf.Exp(-t * 2.2f);
                float ticks = Click(t, 0.22f, 420f, 30f) * 0.35f + Click(t, 0.61f, 360f, 28f) * 0.28f + Click(t, 1.02f, 300f, 26f) * 0.2f;
                return body * swell * 0.33f + ticks + r * swell * 0.05f;
            }, lowPass: 0.3f);
            // A skid: grit dragged under metal, shuddering as the hull catches and lets go. Loops.
            a.Scrape = Make("Scrape", 1.2f, 17, (t, r) =>
            {
                float shudder = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (10f / 1.2f) * t) * Mathf.Sin(2f * Mathf.PI * (3f / 1.2f) * t);
                return r * shudder * 0.5f + Mathf.Sin(2f * Mathf.PI * (60f / 1.2f * 1f) * t) * 0.12f;
            }, lowPass: 0.35f, loop: true);
            a.Lever = Make("Lever", 0.2f, 15, (t, r) => Click(t, 0f, 900f, 40f) + Click(t, 0.09f, 600f, 35f) * 0.9f, lowPass: 0.5f);
            return a;
        }

        static float Click(float t, float at, float pitch, float decay)
        {
            float d = t - at;
            return d < 0f ? 0f : Mathf.Sin(2f * Mathf.PI * pitch * d) * Mathf.Exp(-d * decay);
        }

        static float Bell(float t, float at, float pitch)
        {
            float d = t - at;
            if (d < 0f) return 0f;
            return (Mathf.Sin(2f * Mathf.PI * pitch * d) + 0.35f * Mathf.Sin(2f * Mathf.PI * pitch * 2.01f * d) + 0.15f * Mathf.Sin(2f * Mathf.PI * pitch * 3.02f * d))
                   * Mathf.Exp(-d * 5.5f) * Mathf.Clamp01(d / 0.004f);
        }

        /// <param name="shape">Given the time in seconds and a random value from -1 to 1, returns the sound's height.</param>
        /// <param name="lowPass">0 to 1: lower removes more hiss (0 leaves the sound untouched).</param>
        static AudioClip Make(string name, float seconds, int seed, Func<float, float, float> shape, float lowPass = 0f, bool loop = false)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[count];
            var rng = new SeededRandom((ulong)(seed * 7919 + 13));
            float smooth = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float noise = rng.Range(-1f, 1f);
                float v = shape(t, noise);
                if (lowPass > 0f)
                {
                    smooth += (v - smooth) * lowPass;
                    v = smooth;
                }
                // No click at the very end.
                float tail = loop ? 1f : Mathf.Clamp01((count - i) / 200f);
                data[i] = Mathf.Clamp(v * tail, -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
