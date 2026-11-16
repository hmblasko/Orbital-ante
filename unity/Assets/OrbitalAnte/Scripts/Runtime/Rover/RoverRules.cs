using System;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Rover
{
    /// <summary>
    /// Where everything on the moon rover is, in the rover's own space: origin on the ground under the middle of
    /// the wheelbase at rest, +Y up, +Z the nose, +X the driver's right. The Blender script
    /// (tools/blender/build_rover.py) carries a copy of these numbers; the rule test
    /// RoverModelFile_MatchesTheLayout fails if the two drift.
    ///
    /// The layout is the Apollo rover's (Matthew, 16 Nov 2026, asked for "a moon rover vehicle that can run enemies
    /// over"): open twin seats with the driver on the left, a hand controller between them, a motor in every
    /// wheel hub and steering at both ends, on a slightly longer, wider footprint for speed.
    /// </summary>
    public static class RoverLayout
    {
        public const string Model = "Rover";
        public const string DavitModel = "RoverDavit";

        public const float HalfTrack = 1.05f;
        public const float HalfBase = 1.30f;
        public const float WheelRadius = 0.46f;
        public const float WheelWidth = 0.30f;
        /// <summary>The wishbones' inner pivot, sideways from the middle and up from the ground.</summary>
        public const float ArmPivotX = 0.36f;
        public const float ArmPivotY = 0.50f;
        /// <summary>From the pivot out to the upright.</summary>
        public const float ArmLength = 0.46f;

        public const int Wheels = 4;
        public static readonly string[] WheelNames = { "FL", "FR", "RL", "RR" };
        /// <summary>-1 for the left wheels, +1 for the right.</summary>
        public static int Side(int wheel) => wheel % 2 == 0 ? -1 : 1;
        public static bool Front(int wheel) => wheel < 2;

        /// <summary>A wheel's hub at rest.</summary>
        public static P3 Hub(int wheel) => new P3(Side(wheel) * HalfTrack, WheelRadius, (Front(wheel) ? 1 : -1) * HalfBase);
        public static P3 ArmPivot(int wheel) => new P3(Side(wheel) * ArmPivotX, ArmPivotY, (Front(wheel) ? 1 : -1) * HalfBase);

        /// <summary>The driver's eye, sitting in the left seat.</summary>
        public static readonly P3 Eye = new P3(-0.36f, 1.58f, -0.10f);
        /// <summary>Where you step down to, beside the driver's seat, measured at the boots.</summary>
        public static readonly P3 Exit = new P3(-1.75f, 0.05f, -0.10f);
        public static P3 Boost(int side) => new P3(side * 0.30f, 0.86f, -1.86f);
        public static readonly P3 Lamp = new P3(0f, 0.86f, 1.86f);

        /// <summary>The solid shape: a low box round the frame and wheels' tops, and the roll cage over the seats.</summary>
        public static readonly P3 HullCentre = new P3(0f, 0.86f, 0.08f);
        public static readonly P3 HullSize = new P3(1.34f, 0.52f, 3.80f);
        public static readonly P3 CageCentre = new P3(0f, 1.48f, -0.30f);
        public static readonly P3 CageSize = new P3(1.20f, 0.90f, 1.30f);
        /// <summary>The space a person is hit in: the whole rover and a little more, wheels included.</summary>
        public static readonly P3 RamCentre = new P3(0f, 0.95f, 0.10f);
        public static readonly P3 RamHalfSize = new P3(1.30f, 0.95f, 2.10f);
        /// <summary>Where the driver can be shot: a capsule from the seat to the top of the helmet.</summary>
        public static readonly P3 DriverLow = new P3(-0.36f, 1.00f, -0.30f);
        public static readonly P3 DriverHigh = new P3(-0.36f, 1.65f, -0.20f);
        public const float DriverRadius = 0.28f;
        /// <summary>The centre of mass, below the axles' height, so it slides before it tips.</summary>
        public static readonly P3 CentreOfMass = new P3(0f, 0.36f, 0.0f);

        /// <summary>
        /// Where the rover's origin is when it hangs on the lander's davit, in ship space (see ShipLayout): behind
        /// the base, clear of the rear engine's flame and between the back legs, nose to the ship's back (-Z), so
        /// once it is down you drive straight away from the ship. Its tyres are about two metres off the ground.
        /// </summary>
        public static readonly P3 StowInShip = new P3(0f, -3.62f, -4.15f);
    }

    /// <summary>The rover's dials (on the RoverConfig asset). Speeds are metres per second.</summary>
    [Serializable]
    public sealed class RoverRules
    {
        // ---- the vehicle
        public float mass = 900f;
        /// <summary>Flat out on its own motors (34 m/s is 122 km/h; the real rover managed 18).</summary>
        public float topSpeed = 34f;
        /// <summary>Flat out with the boost lit.</summary>
        public float boostSpeed = 48f;
        public float reverseSpeed = 9f;
        /// <summary>The motors' pull from standing (all four hubs together), m/s². Fades to nothing at top speed.</summary>
        public float driveAccel = 10f;
        public float boostAccel = 13f;
        public float brakeAccel = 16f;
        /// <summary>How much the tyres slow it when nobody is pressing anything, m/s².</summary>
        public float rollingDrag = 0.6f;

        // ---- staying on the ground (low gravity would leave it skating and floating)
        /// <summary>While its wheels are down it is held to the ground at least this hard, m/s², whatever the world's
        /// pull: the grip gravity. On the Moon (1.6) that is most of the hold; on Earth (9.8) it adds nothing.</summary>
        public float gripGravity = 7f;
        /// <summary>More hold the faster it goes, reaching this much extra at top speed, m/s² (like downforce).</summary>
        public float downforceAtTop = 6f;
        /// <summary>In the air the world's pull is felt this many times over while falling, so jumps are long but
        /// landings come.</summary>
        public float fallScale = 1.7f;

        // ---- suspension
        public float travelUp = 0.18f;
        public float travelDown = 0.20f;
        /// <summary>How quickly the body bobs on its springs, per second.</summary>
        public float springHz = 1.4f;
        /// <summary>Damping as a share of critical: soft over bumps, firm on the way back so it never pogos.</summary>
        public float dampCompress = 0.35f;
        public float dampRebound = 0.75f;
        /// <summary>Anti-roll bar, against the spring's stiffness.</summary>
        public float antiRoll = 0.8f;

        // ---- steering and grip
        public float steerSlow = 30f;
        public float steerFast = 7f;
        /// <summary>The speed by which steering has narrowed from slow to fast.</summary>
        public float steerFadeSpeed = 30f;
        /// <summary>Degrees per second the wheels turn toward where they are being steered.</summary>
        public float steerRate = 150f;
        /// <summary>At a crawl the back wheels steer too, the other way (the real rover's double Ackermann). This
        /// share of the front's angle at standstill, fading to none by <see cref="rearSteerFade"/>.</summary>
        public float rearSteerShare = 0.6f;
        public float rearSteerFade = 9f;
        /// <summary>Tyre grip: sideways and along, against the load on the wheel.</summary>
        public float grip = 1.15f;
        /// <summary>Slip angle where sideways grip is highest, degrees. Past it the tyre slides.</summary>
        public float peakSlip = 8f;
        /// <summary>Grip while sliding, against the peak: a slide can be caught.</summary>
        public float slideGrip = 0.7f;
        /// <summary>The back tyres' grip at top speed, against the front's: the tail steps out, as it did for the
        /// Apollo crews at 10 to 12 km/h, but here only when you are really flying.</summary>
        public float rearGripAtTop = 0.85f;
        /// <summary>The back tyres' sideways grip with the handbrake held: a skid turn.</summary>
        public float handbrakeGrip = 0.3f;

        // ---- in the air
        /// <summary>How hard the stick tips it in the air, radians per second squared.</summary>
        public float airTurn = 3.2f;
        /// <summary>How strongly it turns its wheels back toward the ground by itself in the air.</summary>
        public float airLevel = 2.2f;
        public float airSpin = 2.5f;

        // ---- boost
        public float boostSeconds = 4f;
        /// <summary>The boost tank fills this share a second while it is not lit.</summary>
        public float boostRefill = 0.12f;

        // ---- people in the way
        /// <summary>Slower than this a person is only bumped.</summary>
        public float bumpFrom = 3f;
        /// <summary>From this speed whatever the rover touches comes off.</summary>
        public float breakFrom = 6f;
        /// <summary>Damage to each part touched, per metre per second over <see cref="bumpFrom"/>.</summary>
        public float damagePerSpeed = 16f;
        /// <summary>From this speed (13 m/s is 47 km/h) the whole person goes: splat.</summary>
        public float splatFrom = 13f;
        /// <summary>The share of the rover's speed a hit person's pieces leave with.</summary>
        public float fling = 0.75f;
        /// <summary>How soon the same person can be hit again, seconds.</summary>
        public float hitAgainAfter = 0.3f;

        // ---- the driver
        /// <summary>A knock bigger than this change of speed hurts the driver.</summary>
        public float hurtFrom = 11f;
        public float hurtPerSpeed = 7f;
        /// <summary>A knock this big kills the driver outright.</summary>
        public float killFrom = 28f;
        /// <summary>You can only climb out slower than this.</summary>
        public float leaveBelow = 4f;
        /// <summary>How far round you can turn your head in the seat, degrees.</summary>
        public float lookAround = 140f;
        /// <summary>Extra field of view at top speed, degrees.</summary>
        public float speedFov = 9f;

        // ---- the lander's davit
        /// <summary>How close to the hanging place the rover must be parked to be winched up, metres.</summary>
        public float stowReach = 7f;
        public float lowerSpeed = 1.3f;
        public float liftSeconds = 2.6f;
        /// <summary>How close you must be to the rover to use it, metres from your eye to its middle.</summary>
        public float reach = 3.4f;
    }

    /// <summary>The rover's handling as numbers, no Unity types. See <see cref="RoverRules"/> for what each dial means.</summary>
    public static class RoverHandling
    {
        public const float Tau = 6.2831853f;

        static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
        static float SmoothStep(float x) { x = Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>How far the front wheels may turn at this speed, degrees.</summary>
        public static float SteerAngle(RoverRules r, float speed)
        {
            float t = SmoothStep(Math.Abs(speed) / Math.Max(0.1f, r.steerFadeSpeed));
            return r.steerSlow + (r.steerFast - r.steerSlow) * t;
        }

        /// <summary>How much the back wheels steer against the front, as a share of the front's angle.</summary>
        public static float RearSteer(RoverRules r, float speed)
        {
            return r.rearSteerShare * (1f - SmoothStep(Math.Abs(speed) / Math.Max(0.1f, r.rearSteerFade)));
        }

        public static float TopSpeed(RoverRules r, bool boosting) => boosting ? r.boostSpeed : r.topSpeed;

        /// <summary>
        /// The motors' push through the tyres, m/s² along the nose, for a throttle of -1 (full reverse) to 1 at a
        /// forward speed. It fades to nothing at the top speed (the boost's top speed while it is lit) and never
        /// pushes past it.
        /// </summary>
        public static float Drive(RoverRules r, float throttle, float forwardSpeed, bool boosting)
        {
            if (throttle > 0f)
            {
                float top = TopSpeed(r, boosting);
                float share = forwardSpeed <= 0f ? 1f : 1f - (forwardSpeed / top) * (forwardSpeed / top);
                return r.driveAccel * throttle * Math.Max(0f, share);
            }
            if (throttle < 0f)
            {
                float back = -forwardSpeed;
                float share = back <= 0f ? 1f : 1f - (back / r.reverseSpeed) * (back / r.reverseSpeed);
                return -r.driveAccel * 0.6f * -throttle * Math.Max(0f, share);
            }
            return 0f;
        }

        /// <summary>The boost nozzles' push, m/s² along the nose: a rocket, so it works in the air too and needs no
        /// grip. Fades to nothing at the boost's top speed.</summary>
        public static float BoostPush(RoverRules r, float forwardSpeed)
        {
            if (forwardSpeed <= 0f) return r.boostAccel;
            float share = 1f - (forwardSpeed / r.boostSpeed) * (forwardSpeed / r.boostSpeed);
            return r.boostAccel * Math.Max(0f, share);
        }

        /// <summary>The extra hold toward the ground while the wheels are down, m/s², on top of the world's own pull.</summary>
        public static float Hold(RoverRules r, float worldPull, float speed)
        {
            float lift = Math.Max(0f, r.gripGravity - worldPull);
            float s = Math.Min(1.5f, Math.Abs(speed) / Math.Max(0.1f, r.topSpeed));
            return lift + r.downforceAtTop * s * s;
        }

        /// <summary>Spring stiffness for one corner, newtons per metre.</summary>
        public static float Stiffness(RoverRules r, float cornerMass) => cornerMass * (Tau * r.springHz) * (Tau * r.springHz);

        /// <summary>
        /// The spring and damper's push for one corner, newtons. <paramref name="offset"/> is how far the hub is above
        /// its rest place (negative hanging down), <paramref name="rising"/> how fast it is going up. At rest it holds
        /// the corner's share of the grip gravity, so the rover sits at the height it was drawn at on any world.
        /// </summary>
        public static float Suspension(RoverRules r, float cornerMass, float offset, float rising)
        {
            float k = Stiffness(r, cornerMass);
            float preload = r.gripGravity / ((Tau * r.springHz) * (Tau * r.springHz));
            float zeta = rising > 0f ? r.dampCompress : r.dampRebound;
            float c = 2f * zeta * (float)Math.Sqrt(k * cornerMass);
            return Math.Max(0f, k * (offset + preload) + c * rising);
        }

        /// <summary>Sideways grip against the slip angle (degrees): rises to 1 at the peak, then falls to the sliding grip.</summary>
        public static float SideGrip(RoverRules r, float slipDegrees)
        {
            float slip = Math.Abs(slipDegrees);
            float peak = Math.Max(0.5f, r.peakSlip);
            if (slip <= peak) return 0.25f + 0.75f * slip / peak;
            float past = Clamp01((slip - peak) / (2f * peak));
            return 1f + (r.slideGrip - 1f) * past;
        }

        /// <summary>How much grip the back tyres keep at this speed, against the front's.</summary>
        public static float RearGrip(RoverRules r, float speed, bool handbrake)
        {
            if (handbrake) return r.handbrakeGrip;
            float t = Clamp01(Math.Abs(speed) / Math.Max(0.1f, r.topSpeed));
            return 1f + (r.rearGripAtTop - 1f) * t * t;
        }

        /// <summary>A knock to the rover, measured as its change of speed: how much it hurts the driver, and whether it kills.</summary>
        public static float Hurt(RoverRules r, float knock, out bool kills)
        {
            kills = knock >= r.killFrom;
            return knock > r.hurtFrom ? (knock - r.hurtFrom) * r.hurtPerSpeed : 0f;
        }

        /// <summary>Extra field of view at this speed, degrees.</summary>
        public static float Fov(RoverRules r, float speed) => r.speedFov * SmoothStep(Math.Abs(speed) / Math.Max(0.1f, r.topSpeed));
    }

    /// <summary>What happens to a person the rover hits.</summary>
    public enum RunOverResult
    {
        /// <summary>Too slow to matter.</summary>
        Nothing,
        /// <summary>A nudge: nothing comes off.</summary>
        Bump,
        /// <summary>Whatever the rover touched takes damage, and comes off if it was enough.</summary>
        Break,
        /// <summary>The whole person goes at once: the body bursts and the head with it.</summary>
        Splat,
    }

    public static class RunOver
    {
        /// <summary>What a hit at this closing speed (metres per second) does to a living person.</summary>
        public static RunOverResult Judge(RoverRules r, float closingSpeed)
        {
            if (closingSpeed >= r.splatFrom) return RunOverResult.Splat;
            if (closingSpeed >= r.breakFrom) return RunOverResult.Break;
            if (closingSpeed >= r.bumpFrom) return RunOverResult.Bump;
            return RunOverResult.Nothing;
        }

        /// <summary>Damage to each part the rover touches at this closing speed. Loose pieces and bodies use it too.</summary>
        public static float Damage(RoverRules r, float closingSpeed)
        {
            return closingSpeed <= r.bumpFrom ? 0f : (closingSpeed - r.bumpFrom) * r.damagePerSpeed;
        }
    }

    /// <summary>The boost tank: lit while held and not empty, and it fills again while it is not lit.</summary>
    public sealed class BoostTank
    {
        /// <summary>0 empty to 1 full.</summary>
        public float Amount { get; private set; } = 1f;
        public bool Lit { get; private set; }
        bool _waitForRelease;

        public void Refill() { Amount = 1f; Lit = false; _waitForRelease = false; }

        /// <summary>Moves on by <paramref name="dt"/>. Once run dry it will not light again until the button is let go.</summary>
        public bool Step(float dt, bool held, RoverRules r)
        {
            if (!held) _waitForRelease = false;
            Lit = held && !_waitForRelease && Amount > 0f;
            if (Lit)
            {
                Amount = Math.Max(0f, Amount - dt / Math.Max(0.1f, r.boostSeconds));
                if (Amount <= 0f) { Lit = false; _waitForRelease = true; }
            }
            else
            {
                Amount = Math.Min(1f, Amount + dt * r.boostRefill);
            }
            return Lit;
        }
    }
}
