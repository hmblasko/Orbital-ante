using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Enemies
{
    public enum EnemyShipStage { Approach, Hover, Land, Disembark, Reboard }

    /// <summary>Trial flight dials for Fable's playtest pass. The Unity adapter supplies these from config.</summary>
    [Serializable]
    public sealed class EnemyShipTuning
    {
        public float approachHeight = 40f;
        public float hoverHeight = 14f;
        public float maxSpeed = 16f;
        public float acceleration = 24f;
        public float settleSeconds = 1.2f;
        public float settleSpeed = 1f;
        public float arrivedDistance = 1.5f;
        public float groundClearance = 5.6f;
        public float descentSpeed = 2f;
    }

    /// <summary>All positions and velocities use the same frame. GroundPoint is real ground below the ship,
    /// not a body's nominal radius. TargetPoint is the desired ship origin, including its stage's altitude.</summary>
    public struct EnemyShipSample
    {
        public Vec3d Position, Velocity, TargetPoint, TargetVelocity, GroundPoint, Up, Gravity, Forward;
        public bool Grounded, HatchOpen, PilotPresent, CrewAboard;
    }

    public struct EnemyShipOrder
    {
        /// <summary>Engine acceleration in the sample's frame, including compensation for Gravity.</summary>
        public Vec3d Acceleration;
        /// <summary>Orthogonal unit directions for the adapter's bounded rotation, never a teleport.</summary>
        public Vec3d Forward, Up;
        /// <summary>The target after enforcing the currently known terrain clearance.</summary>
        public Vec3d Target;
        public bool Arrived, CanDisembark, Valid;
    }

    /// <summary>Local flight guidance only. Spawning, collision avoidance, state timers, hatch operation and
    /// ownership belong to the Unity adapter; these rules never move a body or claim a safe flight path.</summary>
    public static class EnemyShipRules
    {
        static readonly EnemyShipTuning Defaults = new EnemyShipTuning();

        /// <summary>The current starter lift and cabin walker support a person-sized upright capsule.</summary>
        public static bool FitsCabin(double height, double radius, double supportedHeight, double supportedRadius)
        {
            return Finite(height) && Finite(radius) && Finite(supportedHeight) && Finite(supportedRadius)
                && height > 0 && radius > 0 && supportedHeight > 0 && supportedRadius > 0
                && height >= radius * 2 && height <= supportedHeight + 0.00001
                && radius <= supportedRadius + 0.00001;
        }

        public static EnemyShipOrder Guide(EnemyShipStage stage, in EnemyShipSample sample, EnemyShipTuning tuning = null)
        {
            var order = new EnemyShipOrder { Up = Vec3d.Up, Forward = Vec3d.Forward };
            if (!Finite(sample.Position) || !Finite(sample.Velocity) || !Finite(sample.TargetPoint)
                || !Finite(sample.TargetVelocity) || !Finite(sample.GroundPoint) || !Finite(sample.Up)
                || !Finite(sample.Gravity)) return order;

            EnemyShipTuning rules = tuning ?? Defaults;
            double push = NonNegative(rules.acceleration, Defaults.acceleration);
            double speedLimit = NonNegative(rules.maxSpeed, Defaults.maxSpeed);
            double seconds = Math.Max(0.05, NonNegative(rules.settleSeconds, Defaults.settleSeconds));
            double clearance = NonNegative(rules.groundClearance, Defaults.groundClearance);
            double arriveDistance = NonNegative(rules.arrivedDistance, Defaults.arrivedDistance);
            double arriveSpeed = NonNegative(rules.settleSpeed, Defaults.settleSpeed);

            Vec3d up = Unit(sample.Up);
            if (up.SqrMagnitude < 0.5) up = Unit(-sample.Gravity);
            if (up.SqrMagnitude < 0.5) up = Vec3d.Up;
            order.Up = up;

            Vec3d aboveGround = sample.Position - sample.GroundPoint;
            Vec3d targetAboveGround = sample.TargetPoint - sample.GroundPoint;
            Vec3d relativeVelocity = sample.Velocity - sample.TargetVelocity;
            if (!Finite(aboveGround) || !Finite(targetAboveGround) || !Finite(relativeVelocity)) return order;
            double altitude = Vec3d.Dot(aboveGround, up);
            double targetAltitude = Vec3d.Dot(targetAboveGround, up);
            if (!Finite(altitude) || !Finite(targetAltitude)) return order;
            Vec3d target = sample.TargetPoint + up * Math.Max(0.0, clearance - targetAltitude);
            Vec3d delta = target - sample.Position;
            if (!Finite(target) || !Finite(delta)) return order;

            order.Target = target;
            double heightError = Vec3d.Dot(delta, up);
            if (!Finite(heightError)) return order;
            Vec3d across = delta - up * heightError;
            if (!Finite(across)) return order;
            order.Forward = Unit(across);
            if (order.Forward.SqrMagnitude < 0.5)
            {
                Vec3d forward = Finite(sample.Forward) ? sample.Forward : Vec3d.Forward;
                // Normalize first, since an arbitrary camera direction need not have unit length.
                forward = Unit(forward);
                order.Forward = Unit(forward - up * Vec3d.Dot(forward, up));
            }
            if (order.Forward.SqrMagnitude < 0.5) order.Forward = up.AnyPerpendicular();

            double distance = Length(delta), relativeSpeed = Length(relativeVelocity);
            bool landing = stage == EnemyShipStage.Land || stage == EnemyShipStage.Disembark || stage == EnemyShipStage.Reboard;
            // A stable actual landing can finish slightly off the planned point (friction may have stopped
            // the legs there). A grounded craft must not wait forever for a target it no longer thrusts toward.
            order.Arrived = relativeSpeed <= arriveSpeed && (landing ? sample.Grounded : distance <= arriveDistance);
            order.CanDisembark = sample.Grounded && relativeSpeed <= arriveSpeed && sample.HatchOpen && sample.CrewAboard;
            order.Valid = true;
            // Once parked, leave gravity and ground contact to physics. Nobody aboard means no remote pilot.
            if (!sample.PilotPresent || push <= 0.0 || (landing && sample.Grounded && relativeSpeed <= arriveSpeed)) return order;

            // Approach no faster than the speed from which half the available acceleration could stop us.
            // The easing term removes the last part of a position error without oscillating past the target.
            double closing = Math.Min(speedLimit, Math.Min(distance / seconds, Math.Sqrt(push) * Math.Sqrt(distance)));
            Vec3d desiredVelocity = Unit(delta) * closing;
            double downwardLimit = landing ? NonNegative(rules.descentSpeed, Defaults.descentSpeed) : speedLimit;
            // Even an approach/hover target cannot ask us to descend through known ground. The last metres
            // have their own braking curve, independent of the distant target's horizontal error.
            double room = Math.Max(0.0, altitude - clearance);
            downwardLimit = Math.Min(downwardLimit, Math.Sqrt(push) * Math.Sqrt(room));
            double vertical = Vec3d.Dot(desiredVelocity, up);
            if (vertical < -downwardLimit) desiredVelocity += up * (-downwardLimit - vertical);
            Vec3d wanted = (desiredVelocity - relativeVelocity) / seconds - sample.Gravity;
            // A hovering target at the standing height never quite touches the ground. In the final short,
            // slow descent, let gravity place the feet on the collider. Continue braking a fast descent and
            // correcting sideways drift; only real contact can finish the landing above.
            double downwardSpeed = -Vec3d.Dot(relativeVelocity, up);
            if (landing && !sample.Grounded && altitude >= clearance - 0.01 && altitude <= clearance + arriveDistance && Length(across) <= arriveDistance
                && downwardSpeed <= NonNegative(rules.descentSpeed, Defaults.descentSpeed))
                wanted -= up * Math.Max(0.0, Vec3d.Dot(wanted, up));
            if (!Finite(wanted))
            {
                order.Valid = false;
                return order;
            }
            order.Acceleration = Limit(wanted, push);
            return order;
        }

        /// <summary>A repeatable point on a circle in the tangent plane. Pass a named/forked game stream.</summary>
        public static Vec3d SeededOffset(SeededRandom rng, Vec3d up, double distance)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            up = Finite(up) ? Unit(up) : Vec3d.Up;
            if (up.SqrMagnitude < 0.5) up = Vec3d.Up;
            if (!Finite(distance) || distance <= 0.0) return Vec3d.Zero;
            Vec3d right = up.AnyPerpendicular();
            Vec3d forward = Vec3d.Cross(up, right);
            double angle = rng.NextDouble() * Math.PI * 2.0;
            return (right * Math.Cos(angle) + forward * Math.Sin(angle)) * distance;
        }

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool Finite(Vec3d value) => Finite(value.X) && Finite(value.Y) && Finite(value.Z);
        static double NonNegative(float value, float fallback) => Finite(value) ? Math.Max(0.0, value) : fallback;

        // Scaling before squaring keeps finite but unusually large frame errors from overflowing a norm.
        static double Scale(Vec3d value) => Math.Max(Math.Abs(value.X), Math.Max(Math.Abs(value.Y), Math.Abs(value.Z)));
        static double Length(Vec3d value)
        {
            double scale = Scale(value);
            return scale > 0.0 ? (value / scale).Magnitude * scale : 0.0;
        }

        static Vec3d Unit(Vec3d value)
        {
            double scale = Scale(value);
            return scale > 0.0 ? (value / scale).Normalized : Vec3d.Zero;
        }

        static Vec3d Limit(Vec3d value, double limit)
        {
            double length = Length(value);
            return length > limit ? Unit(value) * limit : value;
        }
    }
}
