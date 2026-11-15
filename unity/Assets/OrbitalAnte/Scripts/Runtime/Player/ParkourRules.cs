using System;

namespace OrbitalAnte.Player
{
    /// <summary>Traversal intent checks. Directions and gravity are supplied by the owning controller; no engine types.</summary>
    public static class ParkourRules
    {
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool GravityAllows(float gravity, float minimum) => Finite(gravity) && Finite(minimum) &&
            gravity > 0f && minimum >= 0f && gravity >= minimum;

        /// <param name="normalDot">Dot product of the level travel direction and wall normal. Near zero means along the wall.</param>
        public static bool WallIntent(bool allowed, bool grounded, bool sprint, float forward,
            float speed, float minSpeed, float normalDot, float maxNormalDot, float gravity,
            float minGravity, bool crouched)
        {
            if (!allowed || grounded || !sprint || crouched) return false;
            if (!Finite(forward) || !Finite(speed) || !Finite(minSpeed) || !Finite(normalDot) ||
                !Finite(maxNormalDot) || !Finite(gravity) || !Finite(minGravity)) return false;
            if (forward <= 0f || forward > 1f || speed <= 0f || minSpeed < 0f ||
                gravity <= 0f || minGravity < 0f || maxNormalDot < 0f || maxNormalDot >= 1f ||
                normalDot < -1f || normalDot > 1f) return false;
            return speed >= minSpeed && GravityAllows(gravity, minGravity) && Math.Abs(normalDot) <= maxNormalDot;
        }
    }

    /// <summary>
    /// One finite wall run and one climb can be spent between safe landings. Releasing a wall, changing walls,
    /// or aborting a climb never grants another push in midair. Geometry and explicit jump presses stay with the motor.
    /// </summary>
    public sealed class ParkourState
    {
        bool _wallSpent, _climbSpent;
        public bool WallRunning { get; private set; }
        public bool Climbing { get; private set; }
        public float WallSecondsLeft { get; private set; }
        public int ActiveWallId { get; private set; }
        public bool ClimbAvailable => !_climbSpent && !Climbing;

        public bool WallAvailable(int wallId) => wallId != 0 && !_wallSpent && !WallRunning && !Climbing;

        public bool BeginWall(int wallId, float duration)
        {
            if (!WallAvailable(wallId) || !ParkourRules.Finite(duration) || duration <= 0f) return false;
            _wallSpent = true;
            WallRunning = true;
            WallSecondsLeft = duration;
            ActiveWallId = wallId;
            return true;
        }

        public void StopWall()
        {
            WallRunning = false;
            WallSecondsLeft = 0f;
            ActiveWallId = 0;
        }

        /// <summary>Spend before moving. If a later sweep fails, StopClimb does not refund this attempt.</summary>
        public bool BeginClimb()
        {
            if (!ClimbAvailable) return false;
            _climbSpent = true;
            StopWall();
            Climbing = true;
            return true;
        }

        public void StopClimb() => Climbing = false;

        public void Tick(float delta)
        {
            if (!WallRunning || !ParkourRules.Finite(delta) || delta <= 0f) return;
            WallSecondsLeft = Math.Max(0f, WallSecondsLeft - delta);
            if (WallSecondsLeft == 0f) StopWall();
        }

        /// <summary>Call only for a safe floor landing, never for wall contact or during a climb.</summary>
        public void Grounded() => Reset();

        /// <summary>Also used when respawning or leaving the owning movement mode.</summary>
        public void Reset()
        {
            StopWall();
            StopClimb();
            _wallSpent = _climbSpent = false;
        }
    }
}
