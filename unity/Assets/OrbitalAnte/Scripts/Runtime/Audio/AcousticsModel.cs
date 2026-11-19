using System;

namespace OrbitalAnte.Audio
{
    /// <summary>The kind of space the listener is standing in.</summary>
    public enum Enclosure
    {
        Open,
        Cave,
        Interior,
        Cockpit,
    }

    /// <summary>Where the listener is, as far as sound is concerned.</summary>
    public struct ListenerSpace
    {
        /// <summary>How much air there is to carry sound: 0 vacuum, 1 Earth at sea level, above 1 for thick atmospheres.</summary>
        public float Air;
        public Enclosure Enclosure;
        public bool Underwater;
    }

    /// <summary>How one sound should be treated in the current space.</summary>
    public struct SoundTreatment
    {
        public float GainDb;
        /// <summary>Low-pass cutoff in Hz; 22000 means untouched.</summary>
        public float Cutoff;
        /// <summary>How much of the sound is sent to the reverb, 0 to 1.</summary>
        public float ReverbSend;
    }

    /// <summary>
    /// How the world changes what you hear, with no Unity types.
    ///
    /// The game's rule for vacuum is "grounded, not silent": sound cannot cross empty space, but your own gun
    /// reaches you through your gloves and suit, footsteps come up through your boots, and a big hit nearby
    /// arrives through the ground as a dull thud. Sounds made inside the suit never change. It keeps firefights
    /// on the Moon readable and makes them sound unmistakably different from firefights on Mars.
    /// </summary>
    public static class AcousticsModel
    {
        public const float Open = 22000f;

        public static SoundTreatment Treat(VacuumPolicy policy, ListenerSpace space)
        {
            var t = new SoundTreatment { GainDb = 0f, Cutoff = Open, ReverbSend = 0f };
            if (policy == VacuumPolicy.Ui) return t;
            if (policy == VacuumPolicy.Suit)
            {
                // The suit's own sounds sit in a tiny, dry space of their own.
                return t;
            }

            float air = Clamp01(space.Air);
            // Thin air carries highs badly and everything quietly (Mars is about 1% of Earth, so real Mars would
            // be near-silent: tuned for play, as the brief asks).
            float carry = (float)Math.Pow(air, 0.35);
            if (policy == VacuumPolicy.Air)
            {
                // Not as dull as physics would have it: on a handheld's speakers anything under 300 Hz is simply
                // gone, and "someone is shooting at me" has to survive on an airless moon.
                t.Cutoff = Lerp(900f, Open, carry * carry);
                t.GainDb = Lerp(-9f, 0f, carry);
            }
            else
            {
                t.Cutoff = Lerp(2200f, Open, carry * carry);
                t.GainDb = Lerp(-4f, 0f, carry);
            }

            if (space.Underwater)
            {
                t.Cutoff = Math.Min(t.Cutoff, 900f);
                t.GainDb -= 3f;
            }

            // Reverb needs air to exist, except inside a pressurised cabin where the air is always there.
            float room;
            switch (space.Enclosure)
            {
                case Enclosure.Cave: room = 0.55f; break;
                case Enclosure.Interior: room = 0.35f; break;
                case Enclosure.Cockpit: room = 0.18f; break;
                default: room = 0f; break;
            }
            t.ReverbSend = room * (space.Enclosure == Enclosure.Open || space.Enclosure == Enclosure.Cave ? carry : 1f);
            return t;
        }

        /// <summary>Thick air makes wind and weather louder; none means none.</summary>
        public static float WeatherGain(ListenerSpace space) => space.Enclosure == Enclosure.Open ? Clamp01(space.Air) : Clamp01(space.Air) * 0.25f;

        static float Lerp(float a, float b, float k) => a + (b - a) * Clamp01(k);
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
