using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Space
{
    public enum FlightAssistMode { Manual, Autopilot, VelocityLock }
    public enum AutopilotPhase { None, Accelerating, Cruising, Braking, Arrived }

    /// <summary>Explicit target and assist state. Aim is deliberately not stored here: looking away cannot retarget a lock.</summary>
    public sealed class FlightNavigation
    {
        public int Target { get; private set; } = -1;
        public int ReferenceBody { get; private set; } = -1;
        public FlightAssistMode Mode { get; private set; }
        public bool Arrived { get; private set; }

        /// <summary>Pressing lock on the same body, or on empty space, clears the existing lock.</summary>
        public void ToggleTarget(int aimedBody)
        {
            int next = aimedBody >= 0 && aimedBody != Target ? aimedBody : -1;
            CancelAssist();
            Target = next;
        }

        public bool ToggleAutopilot()
        {
            if (Mode == FlightAssistMode.Autopilot) { CancelAssist(); return false; }
            if (Target < 0) return false;
            Mode = FlightAssistMode.Autopilot;
            ReferenceBody = Target;
            Arrived = false;
            return true;
        }

        /// <summary>A fallback is captured once, never silently replaced when the craft changes neighbourhood.</summary>
        public bool ToggleVelocityLock(int fallbackBody)
        {
            if (Mode == FlightAssistMode.VelocityLock) { CancelAssist(); return false; }
            int reference = Target >= 0 ? Target : fallbackBody;
            if (reference < 0) return false;
            Mode = FlightAssistMode.VelocityLock;
            ReferenceBody = reference;
            Arrived = false;
            return true;
        }

        public void CompleteApproach()
        {
            if (Mode != FlightAssistMode.Autopilot) return;
            Mode = FlightAssistMode.VelocityLock;
            Arrived = true;
        }

        public void CancelAssist()
        {
            Mode = FlightAssistMode.Manual;
            ReferenceBody = -1;
            Arrived = false;
        }

        /// <summary>Invalid references cancel rather than steering toward another array entry or anchor.</summary>
        public void Validate(int bodyCount)
        {
            if (Target >= bodyCount) { Target = -1; CancelAssist(); }
            if (ReferenceBody >= bodyCount) CancelAssist();
        }
    }

    public struct FlightCommand
    {
        public Vec3d Acceleration;
        public AutopilotPhase Phase;
        public bool Valid;
        public bool InsufficientThrust;
    }

    /// <summary>
    /// Bounded translational guidance in any one consistent inertial basis. Inputs are relative to the selected
    /// body's reference motion. Disturbance is craft gravity minus reference acceleration, not apparent-frame gravity.
    /// This does not plan a route around obstacles or land on terrain.
    /// </summary>
    public static class FlightGuidance
    {
        public static FlightCommand Match(Vec3d relativeVelocity, Vec3d disturbance, double authority, double responseTime, double dt)
        {
            if (!InputsValid(relativeVelocity, disturbance, authority, responseTime, dt)) return default;
            return Control(relativeVelocity, Vec3d.Zero, disturbance, authority, responseTime, dt, AutopilotPhase.None);
        }

        public static FlightCommand Approach(Vec3d toBody, Vec3d relativeVelocity, Vec3d disturbance, double standOffRadius,
            double authority, double cruiseSpeed, double responseTime, double arrivalDistance, double arrivalSpeed, double dt)
        {
            if (!InputsValid(relativeVelocity, disturbance, authority, responseTime, dt) || !Finite(toBody)
                || !Finite(standOffRadius) || standOffRadius <= 0 || !Finite(cruiseSpeed) || cruiseSpeed <= 0
                || !Finite(arrivalDistance) || arrivalDistance < 0 || !Finite(arrivalSpeed) || arrivalSpeed < 0) return default;
            double distance = toBody.Magnitude;
            if (!Finite(distance) || distance < 1e-6) return default;
            double reserve = authority - disturbance.Magnitude;
            if (reserve <= 0) return new FlightCommand { InsufficientThrust = true };

            double gap = distance - standOffRadius;
            double speed = relativeVelocity.Magnitude;
            if (Math.Abs(gap) <= arrivalDistance && speed <= arrivalSpeed)
                return Control(relativeVelocity, Vec3d.Zero, disturbance, authority, responseTime, dt, AutopilotPhase.Arrived);

            Vec3d direction = toBody / distance;
            // Reserve half the net authority for course corrections and start braking before a full-power stopping
            // distance. The response buffer accounts for the finite time used to reach the commanded speed.
            double usableDistance = Math.Max(0, Math.Abs(gap) - speed * responseTime);
            double desiredSpeed = Math.Min(cruiseSpeed, Math.Sqrt(reserve * usableDistance));
            desiredSpeed = Math.Min(desiredSpeed, Math.Abs(gap) / Math.Max(responseTime, dt));
            if (gap < 0) desiredSpeed = -desiredSpeed;
            Vec3d desired = direction * desiredSpeed;
            double closing = Vec3d.Dot(relativeVelocity, direction);
            double sideways = (relativeVelocity - direction * closing).Magnitude;
            AutopilotPhase phase = closing > desiredSpeed + arrivalSpeed || sideways > Math.Max(arrivalSpeed, Math.Abs(desiredSpeed))
                ? AutopilotPhase.Braking
                : desiredSpeed >= cruiseSpeed * 0.99 ? AutopilotPhase.Cruising : AutopilotPhase.Accelerating;
            return Control(relativeVelocity, desired, disturbance, authority, responseTime, dt, phase);
        }

        static FlightCommand Control(Vec3d velocity, Vec3d desired, Vec3d disturbance, double authority,
            double responseTime, double dt, AutopilotPhase phase)
        {
            if (disturbance.Magnitude >= authority) return new FlightCommand { InsufficientThrust = true };
            Vec3d command = (desired - velocity) / Math.Max(responseTime, dt) - disturbance;
            double length = command.Magnitude;
            if (!Finite(length)) return default;
            if (length > authority) command *= authority / length;
            return new FlightCommand { Acceleration = command, Phase = phase, Valid = true };
        }

        /// <summary>True when a straight segment enters a solid body's conservative envelope.</summary>
        public static bool SegmentBlocked(Vec3d start, Vec3d end, Vec3d centre, double radius)
        {
            if (!Finite(start) || !Finite(end) || !Finite(centre) || !Finite(radius) || radius < 0) return true;
            Vec3d segment = end - start;
            double length2 = segment.SqrMagnitude;
            if (length2 < 1e-12) return (start - centre).SqrMagnitude <= radius * radius;
            double along = Math.Max(0, Math.Min(1, Vec3d.Dot(centre - start, segment) / length2));
            return (start + segment * along - centre).SqrMagnitude <= radius * radius;
        }

        static bool InputsValid(Vec3d velocity, Vec3d disturbance, double authority, double responseTime, double dt)
            => Finite(velocity) && Finite(disturbance) && Finite(authority) && authority > 0
                && Finite(responseTime) && responseTime > 0 && Finite(dt) && dt > 0;

        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool Finite(Vec3d value) => Finite(value.X) && Finite(value.Y) && Finite(value.Z);
    }
}
