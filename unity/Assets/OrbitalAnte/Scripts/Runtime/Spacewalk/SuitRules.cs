using System;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Spacewalk
{
    /// <summary>The numbers behind the suit: its jets, its air, its tether. Held in the SuitConfig asset.</summary>
    [Serializable]
    public sealed class SuitRules
    {
        /// <summary>The push of the jets in any direction, metres per second squared. The Moon pulls at 1.6, Mars 3.7, Earth 9.8: the jets lift you on the first two, and only soften a fall on Earth.</summary>
        public float jetPush = 6.5f;
        /// <summary>Seconds of steady full push a full jet tank gives. Drifting costs nothing.</summary>
        public float fuelSeconds = 80f;
        /// <summary>Below this share of a tank the suit warns you.</summary>
        public float fuelLow = 0.25f;
        /// <summary>The share of full push the jets give while the fine-control button is held, for small careful moves.</summary>
        public float fineShare = 0.3f;
        /// <summary>With the jet tank dry the jets run on your air instead. A full air tank is this many seconds of full push.</summary>
        public float airAsFuelSeconds = 40f;
        /// <summary>Minutes of air in the suit.</summary>
        public float airMinutes = 8f;
        /// <summary>Below this share of a tank the suit warns you.</summary>
        public float airLow = 0.25f;
        /// <summary>Seconds you last with no air at all. The suit says so the whole time.</summary>
        public float airGraceSeconds = 14f;
        /// <summary>Seconds to fill the suit's tanks from the ship once you are aboard.</summary>
        public float refillSeconds = 4f;
        /// <summary>How much of the ship's air tank one full suit tank is.</summary>
        public float suitShareOfShipAir = 0.06f;

        /// <summary>Length of the safety tether, metres.</summary>
        public float tetherLength = 40f;

        /// <summary>Within this distance of the ship, Match Velocity matches the ship whatever you look at.</summary>
        public float matchShipNear = 60f;
        /// <summary>Further out, it matches the ship only if you are looking at it (within <see cref="matchLookDegrees"/>) and it is within this distance.</summary>
        public float matchShipFar = 3000f;
        public float matchLookDegrees = 20f;

        /// <summary>You float free (no "down", look any way up) when the pull on you is weaker than this, metres per second squared...</summary>
        public float freePull = 0.15f;
        /// <summary>...or when you are higher above the ground than this share of the body's radius, and never less than <see cref="freeHeightLeast"/> metres (which is what counts on every body under 600 m in radius, the Earth and Moon included).</summary>
        public float freeHeightShare = 0.25f;
        public float freeHeightLeast = 150f;

        /// <summary>Hitting something faster than this (metres per second) knocks the wind out of you.</summary>
        public float hurtSpeed = 9f;
        /// <summary>Hitting something faster than this kills you. On Earth that is a fall of about 34 metres; on full jets it is four seconds of push.</summary>
        public float fatalSpeed = 26f;
        /// <summary>Above this share of the fatal speed, the speed reading turns to a warning.</summary>
        public float warnShare = 0.5f;
    }

    public enum Posture
    {
        /// <summary>There is a "down": you stand, walk and fly head-up.</summary>
        Upright,
        /// <summary>Floating: no "down" at all. You can look, and turn over, any way you like.</summary>
        Free,
    }

    /// <summary>Whether the suit keeps you head-up or lets you float free. Plain C#.</summary>
    public static class SuitPosture
    {
        /// <param name="pull">The pull on you, metres per second squared.</param>
        /// <param name="height">Height above the ground of the body pulling you, metres.</param>
        /// <param name="bodyRadius">That body's radius, metres.</param>
        /// <param name="grounded">Standing on something.</param>
        public static Posture Next(SuitRules rules, Posture now, float pull, float height, float bodyRadius, bool grounded)
        {
            float freeHeight = Math.Max(rules.freeHeightLeast, bodyRadius * rules.freeHeightShare);
            if (now == Posture.Free)
            {
                // A little further in than where you were let go, so you do not flip back and forth on the line.
                bool held = pull > rules.freePull * 1.2f && height < freeHeight * 0.85f;
                return held ? Posture.Upright : Posture.Free;
            }
            if (grounded && pull > rules.freePull) return Posture.Upright;
            bool loose = pull < rules.freePull || height > freeHeight;
            return loose ? Posture.Free : Posture.Upright;
        }
    }

    /// <summary>
    /// The suit's air and jet fuel, each as a share of a full tank. Plain C#, no Unity types.
    ///
    /// Air goes down whenever you are breathing from the suit: outside, or aboard a ship that has lost its own
    /// air. Jet fuel goes down only while the jets push. With the jet tank dry the jets burn your air instead,
    /// so you are never left unable to move, but every push then costs breathing time. Both tanks fill from the
    /// ship in a few seconds once you are aboard, and the air comes out of the ship's tank.
    /// </summary>
    public sealed class SuitSupplies
    {
        public readonly SuitRules Rules;
        public float Air { get; private set; } = 1f;
        public float Fuel { get; private set; } = 1f;
        public bool AirLow => Air <= Rules.airLow;
        public bool AirOut => Air <= 0f;
        public bool FuelLow => Fuel <= Rules.fuelLow;
        public bool FuelOut => Fuel <= 0f;
        /// <summary>True while the jets are being fed from the air tank.</summary>
        public bool BurningAir { get; private set; }
        /// <summary>Seconds spent with no air at all.</summary>
        public float Starved { get; private set; }
        /// <summary>Seconds left before no air kills, or -1 while there is air.</summary>
        public float SecondsLeft => AirOut ? Math.Max(0f, Rules.airGraceSeconds - Starved) : -1f;
        public bool Suffocated => AirOut && Starved >= Rules.airGraceSeconds;
        /// <summary>1 while the jets have anything to burn, 0 when both tanks are empty.</summary>
        public float PushFactor => Fuel > 0f || Air > 0f ? 1f : 0f;
        public bool Filling { get; private set; }

        public SuitSupplies(SuitRules rules = null)
        {
            Rules = rules ?? new SuitRules();
        }

        /// <param name="onSuitAir">Breathing from the suit: outside, or aboard with no air in the ship.</param>
        /// <param name="aboard">Inside the ship (seat, cabin or lift), where the suit can fill up.</param>
        /// <param name="push">How hard the jets are pushing, 0 to 1.</param>
        /// <param name="shipAir">How full the ship's air tank is, 0 to 1.</param>
        /// <returns>The share of the ship's air tank the suit drew in this step.</returns>
        public float Tick(float dt, bool onSuitAir, bool aboard, float push, float shipAir)
        {
            Filling = false;
            BurningAir = false;
            if (dt <= 0f) return 0f;
            push = push < 0f ? 0f : push > 1f ? 1f : push;

            if (push > 0.01f)
            {
                if (Fuel > 0f) Fuel = Math.Max(0f, Fuel - push * dt / Math.Max(1f, Rules.fuelSeconds));
                else if (Air > 0f)
                {
                    BurningAir = true;
                    Air = Math.Max(0f, Air - push * dt / Math.Max(1f, Rules.airAsFuelSeconds));
                }
            }
            if (onSuitAir) Air = Math.Max(0f, Air - dt / Math.Max(1f, Rules.airMinutes * 60f));

            float drawn = 0f;
            if (aboard)
            {
                float fill = dt / Math.Max(0.1f, Rules.refillSeconds);
                if (Fuel < 1f) { Fuel = Math.Min(1f, Fuel + fill); Filling = true; }
                if (shipAir > 0f && Air < 1f)
                {
                    float before = Air;
                    Air = Math.Min(1f, Air + fill);
                    drawn = (Air - before) * Math.Max(0f, Rules.suitShareOfShipAir);
                    Filling = true;
                }
            }

            if (AirOut) Starved += dt;
            else Starved = 0f;
            return drawn;
        }

        public void Set(float air, float fuel)
        {
            Air = air < 0f ? 0f : air > 1f ? 1f : air;
            Fuel = fuel < 0f ? 0f : fuel > 1f ? 1f : fuel;
            if (!AirOut) Starved = 0f;
        }
    }

    /// <summary>What Match Velocity is matching.</summary>
    public enum MatchTarget { Ship, Ground }

    /// <summary>The jets. Plain C#: turns what you ask for into a push, and a push into which nozzles are lit.</summary>
    public static class Jetpack
    {
        /// <summary>
        /// The push for a wish. <paramref name="wish"/> is right, up, forward, each -1 to 1; pushing in two
        /// directions at once gives no more than pushing in one.
        /// </summary>
        public static P3 Push(SuitRules rules, P3 wish, float pushFactor)
        {
            float length = wish.Length;
            if (length < 0.05f || pushFactor <= 0f) return P3.Zero;
            if (length > 1f) wish = wish * (1f / length);
            return wish * (rules.jetPush * pushFactor);
        }

        /// <summary>
        /// The push that brings <paramref name="relativeVelocity"/> (yours, measured against what you are
        /// matching) to nothing as quickly as the jets can, without ever pushing past it.
        /// </summary>
        public static P3 Match(SuitRules rules, P3 relativeVelocity, float dt, float pushFactor)
        {
            float speed = relativeVelocity.Length;
            if (speed < 0.02f || dt <= 0f || pushFactor <= 0f) return P3.Zero;
            float push = Math.Min(rules.jetPush * pushFactor, speed / dt);
            return relativeVelocity * (-push / speed);
        }

        /// <summary>What Match Velocity matches: the ship when you are beside it or looking at it, otherwise the ground.</summary>
        public static MatchTarget Choose(SuitRules rules, float shipDistance, float degreesOffShip)
        {
            if (shipDistance <= rules.matchShipNear) return MatchTarget.Ship;
            if (shipDistance <= rules.matchShipFar && degreesOffShip <= rules.matchLookDegrees) return MatchTarget.Ship;
            return MatchTarget.Ground;
        }
    }

    /// <summary>
    /// How brightly each of the six nozzles burns, eased so a tap is a puff and not a flicker. Plain C#.
    /// A nozzle is named for the way its flame points: pushing you forward lights the Back nozzle.
    /// </summary>
    public sealed class JetGlow
    {
        public const int Right = 0, Left = 1, Up = 2, Down = 3, Front = 4, Back = 5, Count = 6;
        const float RiseSeconds = 0.05f, FallSeconds = 0.14f;

        readonly float[] _level = new float[Count];
        /// <summary>All six together, 0 to 1: how loud the jets should be.</summary>
        public float Loudness { get; private set; }
        /// <summary>True on the step any nozzle lights from cold: the moment for the "puff".</summary>
        public bool Lit { get; private set; }

        public float Level(int nozzle) => _level[nozzle];

        /// <param name="push">The push being given, as a share of full push, in your own directions (right, up, forward).</param>
        public void Tick(float dt, P3 push)
        {
            Lit = false;
            float sum = 0f;
            for (int i = 0; i < Count; i++)
            {
                float axis = i < 2 ? push.X : i < 4 ? push.Y : push.Z;
                // The flame points away from the push: pushing right (+X) lights the Left nozzle.
                float want = i % 2 == 0 ? -axis : axis;
                want = want < 0f ? 0f : want > 1f ? 1f : want;
                float was = _level[i];
                float step = dt / (want > was ? RiseSeconds : FallSeconds);
                _level[i] = want > was ? Math.Min(want, was + step) : Math.Max(want, was - step);
                if (was < 0.02f && want > 0.3f) Lit = true;
                sum += _level[i] * _level[i];
            }
            Loudness = Math.Min(1f, (float)Math.Sqrt(sum));
        }

        public void Reset()
        {
            for (int i = 0; i < Count; i++) _level[i] = 0f;
            Loudness = 0f;
            Lit = false;
        }
    }

    /// <summary>
    /// The safety tether: a line of fixed length from the ship to the suit. Plain C#.
    ///
    /// It stops you at its full length and does nothing else. It never reels you in, never pulls you about
    /// while it is slack, and it does not stretch and fling you back. Only the suit is moved: the ship is some
    /// thirty times heavier, and a person on a line does not steer a spacecraft.
    /// </summary>
    public static class Tether
    {
        /// <param name="offset">From the ship's anchor to you.</param>
        /// <param name="relativeVelocity">Your velocity measured against the anchor's.</param>
        /// <param name="taut">True when the line is at full stretch and holding you.</param>
        /// <param name="back">How far to move you to put you back at the line's full length (a fast drift can run a little past it in one step).</param>
        /// <returns>The change to make to your velocity this step: only ever the removal of your speed away from the ship.</returns>
        public static P3 Pull(SuitRules rules, P3 offset, P3 relativeVelocity, out bool taut, out P3 back)
        {
            taut = false;
            back = P3.Zero;
            float distance = offset.Length;
            if (distance <= rules.tetherLength || distance < 1e-4f) return P3.Zero;
            P3 outward = offset * (1f / distance);
            float leaving = P3.Dot(relativeVelocity, outward);
            if (leaving <= 0f) return P3.Zero;
            taut = true;
            back = outward * (rules.tetherLength - distance);
            return outward * -leaving;
        }

        /// <summary>Whether the line can be clipped on from here.</summary>
        public static bool InReach(SuitRules rules, float distance) => distance <= rules.tetherLength;

        /// <summary>How much the drawn line should droop at its middle, metres: a slack line hangs, a taut one is straight.</summary>
        public static float Slack(SuitRules rules, float distance)
        {
            float spare = rules.tetherLength - distance;
            if (spare <= 0f) return 0f;
            // The sag of a line with this much spare length, kept modest so it never swings through the view.
            return Math.Min(3f, (float)Math.Sqrt(spare * Math.Max(0.5f, distance)) * 0.35f);
        }
    }

    /// <summary>What a blow does to someone in a suit, and the words for it. Plain C#.</summary>
    public static class SuitKnock
    {
        public enum Result { Nothing, Winded, Killed }

        public static Result For(SuitRules rules, float speed)
        {
            if (speed >= rules.fatalSpeed) return Result.Killed;
            return speed >= rules.hurtSpeed ? Result.Winded : Result.Nothing;
        }

        public static string Cause(float speed)
        {
            return "You hit something at " + Math.Round(speed).ToString("0") + " m/s. The suit is not built for that.";
        }

        public const string NoAir = "Your suit ran out of air.";
    }

    /// <summary>What the suit's readout needs to know.</summary>
    public struct SuitInputs
    {
        public float Air, Fuel;
        public bool AirLow, AirOut, FuelLow, FuelOut, BurningAir;
        public float SecondsLeft;
        public bool Tethered, TetherTaut, TetherInReach;
        public float ShipDistance;
        public bool Matching;
        public MatchTarget MatchTarget;
        /// <summary>The name of the body being matched, in capitals ("MOON").</summary>
        public string GroundName;
        /// <summary>True close above a body, where speed is measured against the ground turning under you and not the body's centre.</summary>
        public bool AgainstGround;
        /// <summary>Speed measured against the same thing Match Velocity would match, metres per second.</summary>
        public float Speed;
        public bool Floating;
    }

    /// <summary>The suit's words. Plain C#. One notice at a time, the most urgent first.</summary>
    public static class SuitReadout
    {
        public static string Notice(SuitInputs s)
        {
            if (s.AirOut) return "NO AIR: GET ABOARD  " + Math.Ceiling(Math.Max(0f, s.SecondsLeft)).ToString("0");
            if (s.AirLow) return s.BurningAir ? "AIR LOW: THE JETS ARE BURNING IT" : "AIR LOW: HEAD BACK TO THE SHIP";
            // Said before the first push on air, not after: the cost should never be a surprise.
            if (s.FuelOut) return "JET FUEL OUT: JETS NOW BURN YOUR AIR";
            if (s.FuelLow) return "JET FUEL LOW";
            if (s.TetherTaut) return "TETHER AT FULL STRETCH";
            if (s.Matching) return s.MatchTarget == MatchTarget.Ship ? "MATCHING THE SHIP" : "MATCHING " + Ground(s);
            return string.Empty;
        }

        /// <summary>True when the notice is a danger, not just information.</summary>
        public static bool IsAlarm(SuitInputs s) => s.AirOut || s.AirLow || s.BurningAir || s.FuelOut;

        /// <summary>What the speed is measured against, in a couple of words.</summary>
        public static string SpeedAgainst(SuitInputs s)
        {
            return s.MatchTarget == MatchTarget.Ship ? "vs SHIP" : "vs " + Ground(s);
        }

        static string Ground(SuitInputs s) => (s.GroundName ?? string.Empty) + (s.AgainstGround ? " GROUND" : string.Empty);
    }
}
