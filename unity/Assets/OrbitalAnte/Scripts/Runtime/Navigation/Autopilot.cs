using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Navigation
{
    public enum PilotPhase
    {
        Off,
        /// <summary>Speeding up toward the target.</summary>
        Burning,
        /// <summary>Engines quiet, on the way.</summary>
        Coasting,
        /// <summary>Slowing down to arrive.</summary>
        Braking,
    }

    /// <summary>Why the autopilot let go. Shown to the pilot in plain words, so it never just stops.</summary>
    public enum PilotStop
    {
        None,
        Arrived,
        PilotTookOver,
        Blocked,
        NoFuel,
        NoTarget,
        Unbuckled,
        Knocked,
        OnGround,
    }

    [Serializable]
    public sealed class AutopilotRules
    {
        /// <summary>The push the autopilot may use, metres per second squared. A little under boost, so a pilot who flies by hand can beat it.</summary>
        public float push = 45f;
        /// <summary>It burns this much more fuel than a pilot would for the same push. Flying by hand is the thrifty way; boost is the fast way; this is the easy way.</summary>
        public float fuelSurcharge = 1.25f;
        /// <summary>It plans its braking as if it had only this share of its push, to leave room for gravity and error.</summary>
        public float brakeShare = 0.7f;
        /// <summary>It never goes faster than this, metres per second.</summary>
        public float topSpeed = 2500f;
        /// <summary>It stops this high above the target, as a share of the target's radius...</summary>
        public float standOffShare = 0.6f;
        /// <summary>...but never lower than this many metres...</summary>
        public float standOffLeast = 250f;
        /// <summary>...or higher than this.</summary>
        public float standOffMost = 1800f;
        /// <summary>Seconds it gives itself to fix a speed error. Smaller is snappier and twitchier.</summary>
        public float settleSeconds = 1.2f;
        /// <summary>Over the last stretch it eases in, as if it had this many seconds left at every moment, so it settles and does not jab.</summary>
        public float easeSeconds = 2.5f;
        /// <summary>It calls the trip done within this many metres per second of the target's own speed.</summary>
        public float arriveSpeed = 3f;
        /// <summary>How much wider than a body the autopilot treats it when checking the path, as a share of its radius.</summary>
        public float clearance = 1.25f;
        /// <summary>How fast the nose swings toward the target, degrees per second.</summary>
        public float turnSpeed = 50f;
    }

    /// <summary>What the autopilot wants this moment.</summary>
    public struct PilotOrder
    {
        /// <summary>The push wanted, metres per second squared, in the same directions the inputs were given in.</summary>
        public Vec3d Push;
        public PilotPhase Phase;
        /// <summary>0 to 1: how hard the engines are working, for the dash and the sound.</summary>
        public float Effort;
        /// <summary>Rough seconds until arrival.</summary>
        public float SecondsLeft;
        /// <summary>Metres still to go to the stopping place.</summary>
        public double DistanceLeft;
        public bool Arrived;
    }

    /// <summary>
    /// The autopilot's rules. Plain C#, no Unity types. It flies to a stopping place above a target body and
    /// ends there moving with it, in the spirit of Outer Wilds: speed up, coast, slow down, hand back.
    ///
    /// How it decides: from how far there is to go it works out the fastest closing speed from which it could
    /// still stop in time, aims for that speed straight at the target, and pushes to remove the difference
    /// between that and what the ship is really doing. Sideways drift is a difference like any other, so it
    /// is cancelled along the way. It is a helper, not a better pilot: it plans gentle braking, will not
    /// thread past a body in the way, and gives up the moment the pilot touches the thrust.
    /// </summary>
    public static class Autopilot
    {
        /// <summary>How high above the target's nominal surface the trip ends.</summary>
        public static double StandOff(AutopilotRules rules, double radius)
        {
            return Math.Max(rules.standOffLeast, Math.Min(rules.standOffMost, radius * rules.standOffShare));
        }

        /// <summary>The fastest closing speed from which the ship could still stop in the distance left.</summary>
        public static double SafeClosingSpeed(AutopilotRules rules, double distanceLeft, double push)
        {
            if (distanceLeft <= 0.0) return Math.Max(-20.0, distanceLeft * 0.5);
            double brake = Math.Max(0.1, push * rules.brakeShare);
            double ease = distanceLeft / Math.Max(0.2, rules.easeSeconds);
            return Math.Min(rules.topSpeed, Math.Min(ease, Math.Sqrt(2.0 * brake * distanceLeft)));
        }

        /// <summary>
        /// The braking it takes to stay on the safe-speed curve while riding down it. Without this the ship
        /// only brakes once it is already too fast, runs late all the way in and sails past its stopping place.
        /// </summary>
        public static double BrakingOnTheCurve(AutopilotRules rules, double distanceLeft, double push)
        {
            if (distanceLeft <= 0.0) return 0.0;
            double brake = Math.Max(0.1, push * rules.brakeShare);
            double easeSeconds = Math.Max(0.2, rules.easeSeconds);
            double ease = distanceLeft / easeSeconds, curve = Math.Sqrt(2.0 * brake * distanceLeft);
            if (rules.topSpeed <= ease && rules.topSpeed <= curve) return 0.0;
            return ease < curve ? ease / easeSeconds : brake;
        }

        /// <param name="toTarget">From the ship to the target's centre.</param>
        /// <param name="relativeVelocity">The ship's velocity measured against the target.</param>
        /// <param name="radius">The target's radius.</param>
        /// <param name="gravity">The pull on the ship right now, measured against the target (zero if unknown).</param>
        /// <param name="pushShare">0 to 1: how much of its push the ship can give (damage, an empty tank).</param>
        public static PilotOrder Fly(AutopilotRules rules, Vec3d toTarget, Vec3d relativeVelocity, double radius, Vec3d gravity, double pushShare = 1.0)
        {
            var order = new PilotOrder();
            double push = Math.Max(0.0, rules.push * Math.Min(1.0, pushShare));
            double range = toTarget.Magnitude;
            if (range < 1e-6 || push <= 0.0) { order.Phase = PilotPhase.Coasting; return order; }
            Vec3d toward = toTarget / range;
            double left = range - radius - StandOff(rules, radius);
            order.DistanceLeft = Math.Max(0.0, left);

            double closing = Vec3d.Dot(relativeVelocity, toward);
            double wantClosing = SafeClosingSpeed(rules, left, push);
            Vec3d error = toward * wantClosing - relativeVelocity;

            double band = Math.Max(15.0, StandOff(rules, radius) * 0.06);
            if (Math.Abs(left) <= band && relativeVelocity.Magnitude <= rules.arriveSpeed)
            {
                order.Arrived = true;
                order.Phase = PilotPhase.Off;
                return order;
            }

            // Push to remove the error in good time, holding the ship up against the pull while it does.
            // Riding down the curve it brakes as the curve asks, in step with how fast it is really closing.
            double riding = wantClosing > 1e-6 ? Math.Max(0.0, Math.Min(1.0, closing / wantClosing)) : 0.0;
            Vec3d planned = toward * (-BrakingOnTheCurve(rules, left, push) * riding);
            Vec3d wanted = planned + error / Math.Max(0.1, rules.settleSeconds) - gravity;
            double size = wanted.Magnitude;
            if (size > push) wanted = wanted * (push / size);
            // A whisper of push is not worth the fuel or the noise.
            if (size < push * 0.02) { wanted = Vec3d.Zero; size = 0.0; }
            order.Push = wanted;
            order.Effort = (float)Math.Min(1.0, size / push);

            double along = Vec3d.Dot(wanted, toward);
            if (order.Effort < 0.12f) order.Phase = PilotPhase.Coasting;
            else if (along < 0.0 && closing > 0.0) order.Phase = PilotPhase.Braking;
            else order.Phase = PilotPhase.Burning;

            order.SecondsLeft = (float)SecondsFor(Math.Max(0.0, left), Math.Max(0.0, closing), push, push * rules.brakeShare, rules.topSpeed);
            return order;
        }

        /// <summary>Rough time to cover a distance: speed up, perhaps cruise at top speed, slow down.</summary>
        public static double SecondsFor(double distance, double speedNow, double push, double brake, double topSpeed)
        {
            if (distance <= 0.0) return 0.0;
            push = Math.Max(0.1, push);
            brake = Math.Max(0.1, brake);
            // Peak speed if there were no limit: gain v^2/2a going up, lose v^2/2b coming down.
            double peak = Math.Sqrt((2.0 * distance + speedNow * speedNow / push) / (1.0 / push + 1.0 / brake));
            if (peak <= speedNow) return 2.0 * distance / Math.Max(1.0, speedNow);
            if (peak <= topSpeed) return (peak - speedNow) / push + peak / brake;
            double up = (topSpeed * topSpeed - speedNow * speedNow) / (2.0 * push);
            double down = topSpeed * topSpeed / (2.0 * brake);
            double cruise = Math.Max(0.0, distance - up - down);
            return Math.Max(0.0, topSpeed - speedNow) / push + cruise / topSpeed + topSpeed / brake;
        }

        /// <summary>
        /// The first body standing between the ship and its target, or -1. A body the ship is leaving (the
        /// nearest point of the path to it is the start) is not in the way: you can always fly away from the
        /// ground you are standing on.
        /// </summary>
        /// <param name="positions">Body centres, measured from the ship.</param>
        public static int Blocker(AutopilotRules rules, Vec3d[] positions, double[] radii, int count, int target)
        {
            if (target < 0 || target >= count) return -1;
            Vec3d path = positions[target];
            double length = path.Magnitude;
            if (length < 1e-6) return -1;
            Vec3d along = path / length;
            // The trip ends above the target, not at its centre.
            double end = Math.Max(0.0, length - radii[target] - StandOff(rules, radii[target]));
            int worst = -1;
            double nearest = double.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (i == target) continue;
                double t = Vec3d.Dot(positions[i], along);
                if (t <= 0.0) continue;
                if (t > end) t = end;
                double miss = (positions[i] - along * t).Magnitude;
                if (miss < radii[i] * rules.clearance && t < nearest)
                {
                    nearest = t;
                    worst = i;
                }
            }
            return worst;
        }

        /// <summary>What the dash says while it flies: what it is doing and roughly how long is left.</summary>
        public static string Words(PilotPhase phase, float secondsLeft)
        {
            string doing = phase == PilotPhase.Braking ? "SLOWING" : phase == PilotPhase.Burning ? "SPEEDING UP" : "COASTING";
            int s = (int)Math.Max(0f, Math.Min(5999f, secondsLeft));
            return "AUTOPILOT: " + doing + "  " + (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>True when it let go without being asked and without finishing: the pilot must be told loudly.</summary>
        public static bool IsAlarm(PilotStop stop) => stop == PilotStop.Blocked || stop == PilotStop.NoFuel || stop == PilotStop.Knocked;

        /// <summary>The words for why it let go. Empty for "still flying".</summary>
        public static string Describe(PilotStop stop, string blocker = null)
        {
            switch (stop)
            {
                case PilotStop.Arrived: return "AUTOPILOT DONE: FLY, YOU ARE FALLING";
                case PilotStop.PilotTookOver: return "AUTOPILOT OFF: YOU HAVE THE SHIP";
                case PilotStop.Blocked: return string.IsNullOrEmpty(blocker) ? "AUTOPILOT: SOMETHING IS IN THE WAY" : "AUTOPILOT: " + blocker + " IS IN THE WAY";
                case PilotStop.NoFuel: return "AUTOPILOT OFF: NO FUEL";
                case PilotStop.NoTarget: return "AUTOPILOT: LOCK A TARGET FIRST";
                case PilotStop.Unbuckled: return "AUTOPILOT OFF: NOBODY IN THE SEAT";
                case PilotStop.Knocked: return "AUTOPILOT OFF: WE HIT SOMETHING";
                case PilotStop.OnGround: return "AUTOPILOT: TAKE OFF FIRST";
                default: return string.Empty;
            }
        }
    }

    /// <summary>Which body the ship is locked on to. Plain C#.</summary>
    public static class TargetLock
    {
        /// <summary>
        /// What pressing Lock does: lock what is under the nose; pressing it on the locked body, or on empty
        /// sky, lets go.
        /// </summary>
        public static int Press(int locked, int underNose)
        {
            if (underNose < 0 || underNose == locked) return -1;
            return underNose;
        }

        /// <summary>The body everything is measured against: the lock if there is one, else whatever is under the nose.</summary>
        public static int Target(int locked, int underNose) => locked >= 0 ? locked : underNose;

        /// <summary>
        /// How far off the nose a body is, in degrees, counting anywhere on its disc as dead on. Smaller wins;
        /// the tiny distance term makes the nearer of two bodies under the nose win.
        /// </summary>
        public static double Score(double angleOffCentre, double distance, double radius)
        {
            if (distance <= radius) return double.MaxValue;
            double off = angleOffCentre - Math.Asin(radius / distance) * 180.0 / Math.PI;
            if (off < 0.0) off = 0.0;
            return off + distance * 1e-7;
        }
    }
}
