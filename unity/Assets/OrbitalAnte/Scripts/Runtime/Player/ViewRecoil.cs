using System;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// The kick a gun gives the view, in plain C# (no Unity types), for any first-person view that is not the arena's
    /// <see cref="FpsController"/> (which keeps its own copy of the same rule). Each kick jumps the view at once; part of
    /// it settles back by itself and the rest stays, so holding the trigger makes the gun climb and the player has to
    /// pull it back down (Part B Milestone 3: "full-auto, recoil").
    ///
    /// The owner calls <see cref="Step"/> once a frame and adds what it hands back to the head's turn for that frame.
    /// </summary>
    public sealed class ViewRecoil
    {
        float _pendingUp, _pendingRight;
        float _returnUp, _returnRight;
        float _recovery = 14f;

        /// <summary>Degrees still to settle back upward-down and sideways (positive: the view is above / right of where it was).</summary>
        public float Unsettled => Math.Abs(_returnUp) + Math.Abs(_returnRight);

        /// <summary>
        /// A shot. <paramref name="pitchUp"/> degrees up and <paramref name="yawRight"/> degrees right, at once;
        /// <paramref name="returnFraction"/> (0 to 1) of it comes back by itself at <paramref name="recoverySpeed"/> degrees a second.
        /// </summary>
        public void Kick(float pitchUp, float yawRight, float returnFraction, float recoverySpeed)
        {
            if (float.IsNaN(pitchUp) || float.IsNaN(yawRight)) return;
            float back = Clamp01(returnFraction);
            _pendingUp += pitchUp;
            _pendingRight += yawRight;
            _returnUp += pitchUp * back;
            _returnRight += yawRight * back;
            _recovery = Math.Max(1f, float.IsNaN(recoverySpeed) ? 14f : recoverySpeed);
        }

        /// <summary>
        /// How far to turn the view this frame: the fresh kicks, less what settles back over <paramref name="dt"/>.
        /// Up and right are positive.
        /// </summary>
        public void Step(float dt, out float up, out float right)
        {
            up = _pendingUp;
            right = _pendingRight;
            _pendingUp = 0f;
            _pendingRight = 0f;
            if (!(dt > 0f)) return;

            float step = _recovery * dt;
            float nextUp = Toward(_returnUp, step);
            float nextRight = Toward(_returnRight, step);
            up -= _returnUp - nextUp;
            right -= _returnRight - nextRight;
            _returnUp = nextUp;
            _returnRight = nextRight;
        }

        /// <summary>Forgets every kick (a respawn, a change of view such as sitting down).</summary>
        public void Clear()
        {
            _pendingUp = _pendingRight = _returnUp = _returnRight = 0f;
        }

        static float Toward(float value, float step)
        {
            if (value > 0f) return Math.Max(0f, value - step);
            if (value < 0f) return Math.Min(0f, value + step);
            return 0f;
        }

        static float Clamp01(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
    }
}
