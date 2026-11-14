using System;
using OrbitalAnte.Ship;
using UnityEngine;

namespace OrbitalAnte.Spacewalk
{
    /// <summary>
    /// The suit's sounds, made in code like the ship's. Heard from inside the helmet, so they are close and
    /// soft-edged: a jet is a rush of gas with a low body to it, not a rocket.
    /// </summary>
    public sealed class SuitAudio
    {
        public AudioClip Jet, Puff, Clip, Unclip, Taut, Warn, Bump;

        public void Destroy()
        {
            AudioClip[] all = { Jet, Puff, Clip, Unclip, Taut, Warn, Bump };
            foreach (AudioClip clip in all)
            {
                if (clip != null) UnityEngine.Object.Destroy(clip);
            }
        }

        public static SuitAudio Build()
        {
            var a = new SuitAudio();
            // The steady rush: hiss shaped by a slow flutter, over a low hum felt through the backpack.
            a.Jet = ShipAudio.Make("SuitJet", 1.6f, 31, (t, r) =>
            {
                float flutter = 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * 5f * t) * Mathf.Sin(2f * Mathf.PI * 1.25f * t);
                return r * 0.55f * flutter + Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.16f + Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.05f;
            }, lowPass: 0.42f, loop: true);
            // The start of a push: a quick "pfft" with a soft knock as the valve opens.
            a.Puff = ShipAudio.Make("SuitPuff", 0.22f, 32, (t, r) =>
                r * Mathf.Exp(-t * 22f) * 0.9f + Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 45f) * 0.5f, lowPass: 0.55f);
            a.Clip = ShipAudio.Make("TetherClip", 0.25f, 33, (t, r) => Tick(t, 0f, 1500f, 60f) + Tick(t, 0.07f, 2100f, 50f) * 0.9f, lowPass: 0.6f);
            a.Unclip = ShipAudio.Make("TetherUnclip", 0.22f, 34, (t, r) => Tick(t, 0f, 2000f, 55f) + Tick(t, 0.06f, 1300f, 45f) * 0.8f, lowPass: 0.6f);
            // The line coming up short: a dull tug felt through the harness.
            a.Taut = ShipAudio.Make("TetherTaut", 0.45f, 35, (t, r) =>
                Mathf.Sin(2f * Mathf.PI * (95f - 30f * t) * t) * Mathf.Exp(-t * 9f) * 0.9f + r * Mathf.Exp(-t * 40f) * 0.25f, lowPass: 0.3f);
            // Two soft notes, the second lower: "attention", not "panic".
            a.Warn = ShipAudio.Make("SuitWarn", 0.5f, 36, (t, r) => Note(t, 0f, 660f) + Note(t, 0.2f, 520f), lowPass: 0.5f);
            a.Bump = ShipAudio.Make("SuitBump", 0.35f, 37, (t, r) =>
                Mathf.Sin(2f * Mathf.PI * 60f * t) * Mathf.Exp(-t * 14f) + r * Mathf.Exp(-t * 30f) * 0.5f, lowPass: 0.25f);
            return a;
        }

        static float Tick(float t, float at, float pitch, float decay)
        {
            float d = t - at;
            return d < 0f ? 0f : Mathf.Sin(2f * Mathf.PI * pitch * d) * Mathf.Exp(-d * decay);
        }

        static float Note(float t, float at, float pitch)
        {
            float d = t - at;
            if (d < 0f) return 0f;
            return Mathf.Sin(2f * Mathf.PI * pitch * d) * Mathf.Exp(-d * 9f) * Mathf.Clamp01(d / 0.006f) * 0.6f;
        }
    }
}
