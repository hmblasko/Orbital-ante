using System;

namespace OrbitalAnte.Ship
{
    /// <summary>The ship's floodlights' dials (on the Ship asset).</summary>
    [Serializable]
    public sealed class FloodRules
    {
        /// <summary>How bright the beam is where it starts, against the Sun at 1.</summary>
        public float brightness = 1.6f;
        /// <summary>How far it reaches before it has faded to nothing, metres.</summary>
        public float reach = 95f;
        /// <summary>Half the width of the bright middle of the beam, degrees.</summary>
        public float innerDegrees = 20f;
        /// <summary>Half the width of the whole beam, where it has faded out, degrees.</summary>
        public float outerDegrees = 40f;
        /// <summary>How far below the nose it points, degrees: ahead and down, so it lights the ground you are about
        /// to land on as well as what you are flying toward.</summary>
        public float downDegrees = 32f;
        /// <summary>Seconds to come fully on or go fully off.</summary>
        public float switchSeconds = 0.25f;
    }

    /// <summary>
    /// When the ship's floodlights are on (Matthew, 16 Nov 2026: "an outside light so that it can see in dark
    /// areas"). Plain C#, so it is tested outside Unity.
    ///
    /// They look after themselves: they come on when it is dark near the ground (the same darkness that lights the
    /// player's own lamp) and go off when it is light again. A press of the Floodlights button puts them the other
    /// way from how they are now and holds them there, until a press puts them back to what the dark would choose.
    /// </summary>
    public sealed class ShipLights
    {
        public enum Mode { Auto, On, Off }

        public Mode Setting { get; private set; } = Mode.Auto;
        /// <summary>0 off to 1 fully on, eased.</summary>
        public float Level { get; private set; }
        /// <summary>Whether they are meant to be on right now.</summary>
        public bool Wanted { get; private set; }

        bool _darkWants;

        /// <summary>
        /// The pilot pressed the button: the other way from now. If that is what the dark would choose anyway they
        /// go back to looking after themselves (Auto); otherwise they stay as the pilot set them. So switching them
        /// off in daylight never leaves them off in the next cave. Returns whether they are now on.
        /// </summary>
        public bool Press()
        {
            bool on = !Wanted;
            Setting = on == _darkWants ? Mode.Auto : (on ? Mode.On : Mode.Off);
            Wanted = on;
            return on;
        }

        /// <summary>Moves on by <paramref name="dt"/> seconds. <paramref name="dark"/> is how dark it is near the
        /// ground, 0 to 1 (the player's lamp level). <paramref name="working"/> is false when there is no power or
        /// the lamps are gone: then they are out whatever the setting.</summary>
        public void Step(float dt, float dark, bool working, FloodRules rules)
        {
            // The same stay-on band as the lamp, so they never flicker at dusk.
            if (dark > 0.5f) _darkWants = true;
            else if (dark < 0.2f) _darkWants = false;
            Wanted = Setting == Mode.Auto ? _darkWants : Setting == Mode.On;

            float target = Wanted && working ? 1f : 0f;
            float rate = dt / Math.Max(0.01f, rules != null ? rules.switchSeconds : 0.25f);
            Level = target > Level ? Math.Min(target, Level + rate) : Math.Max(target, Level - rate);
        }

        /// <summary>The way the beam points, in ship space (unit vector): ahead along +Z, tipped down.</summary>
        public static P3 Direction(FloodRules rules)
        {
            double a = (rules != null ? rules.downDegrees : 32f) * Math.PI / 180.0;
            return new P3(0.0, -Math.Sin(a), Math.Cos(a));
        }

        /// <summary>Where the beam starts, in ship space: between the two lamps on the front face of the base.</summary>
        public static readonly P3 Origin = new P3(0f, LampY, ShipLayout.BaseInradius + 0.05f);

        /// <summary>The two lamps you can see, either side of the middle of the base's front face.</summary>
        public static readonly P3[] Lamps =
        {
            new P3(-LampSpacing, LampY, ShipLayout.BaseInradius + 0.03f),
            new P3(LampSpacing, LampY, ShipLayout.BaseInradius + 0.03f),
        };

        /// <summary>Height of the lamps: in the lower half of the base, well clear of the cabin floor above.</summary>
        public const float LampY = -1.15f;
        public const float LampSpacing = 0.6f;
        /// <summary>Radius of each lamp's lit face.</summary>
        public const float LampRadius = 0.13f;

        /// <summary>How much of the beam reaches a point, 0 to 1, before the surface's own facing: the cone and the
        /// fall-off with distance, exactly as the shaders work it out (OA_Beam in OA_Look.hlsl).</summary>
        public static float Reaching(P3 origin, P3 direction, P3 point, FloodRules rules)
        {
            P3 away = point - origin;
            float distance = Math.Max(0.001f, away.Length);
            float along = P3.Dot(away * (1f / distance), direction);
            float cosOuter = (float)Math.Cos(rules.outerDegrees * Math.PI / 180.0);
            float cosInner = (float)Math.Cos(rules.innerDegrees * Math.PI / 180.0);
            float cone = Clamp01((along - cosOuter) / Math.Max(1e-4f, cosInner - cosOuter));
            float fade = Clamp01(1f - distance / Math.Max(1f, rules.reach));
            return cone * cone * fade * fade;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
