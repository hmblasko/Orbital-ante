using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// The point of view the physics engine works in.
    ///
    /// Why this exists: planets here move at hundreds of metres a second and spin. A physics engine is at its
    /// best when the ground is standing still. So instead of moving the ground under the player, the game
    /// picks a body (the "anchor") and describes everything from that body's point of view. Near its surface it
    /// also turns with the body, so the ground is completely still and the sky wheels overhead instead. Far from
    /// everything, the anchor is the Sun.
    ///
    /// The sums here convert positions and velocities between "true" coordinates (measured from the Sun) and
    /// "local" coordinates (what Unity sees), and work out the extra pushes that appear when your point of view
    /// is itself accelerating or turning (the same ones that push you sideways on a roundabout). With those
    /// included, motion comes out the same whichever point of view is used.
    ///
    /// Changing anchor, or starting or stopping turning with a body, never moves anything: every object keeps
    /// its local position and only its local velocity is re-measured. Only <see cref="Recenter"/> moves things.
    /// No Unity types, so all of this is unit tested outside the engine.
    /// </summary>
    public sealed class ReferenceFrame
    {
        readonly SolarSystemModel _model;
        QuatD _base = QuatD.Identity;
        double _lockAngle;

        public ReferenceFrame(SolarSystemModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        /// <summary>The body this point of view rides along with.</summary>
        public int Anchor { get; private set; }
        /// <summary>True while the point of view also turns with the anchor's spin.</summary>
        public bool Rotating { get; private set; }
        /// <summary>Where Unity's zero point sits, measured from the anchor's centre in local axes.</summary>
        public Vec3d Origin { get; private set; }

        /// <summary>How the local axes are turned compared with the true ones.</summary>
        public QuatD Orientation(BodyState[] states)
        {
            if (!Rotating) return _base;
            return QuatD.AngleAxis(_model.SpinAxis(Anchor), states[Anchor].SpinAngle - _lockAngle) * _base;
        }

        /// <summary>The frame's own turn rate, in local axes (radians per second). Zero when not rotating.</summary>
        public Vec3d AngularVelocityLocal
        {
            get
            {
                if (!Rotating) return Vec3d.Zero;
                // The spin axis is the same before and after the frame's own turn, so the base is all that matters.
                return _base.Inverse.Rotate(_model.SpinAxis(Anchor)) * _model.SpinRate(Anchor);
            }
        }

        // ------------------------------------------------------------------ conversions

        public Vec3d ToLocalPosition(Vec3d truePosition, BodyState[] states)
        {
            return Orientation(states).Inverse.Rotate(truePosition - states[Anchor].Position) - Origin;
        }

        public Vec3d ToTruePosition(Vec3d localPosition, BodyState[] states)
        {
            return states[Anchor].Position + Orientation(states).Rotate(localPosition + Origin);
        }

        public Vec3d ToLocalVelocity(Vec3d truePosition, Vec3d trueVelocity, BodyState[] states)
        {
            QuatD inverse = Orientation(states).Inverse;
            Vec3d fromAnchor = inverse.Rotate(truePosition - states[Anchor].Position);
            return inverse.Rotate(trueVelocity - states[Anchor].Velocity) - Vec3d.Cross(AngularVelocityLocal, fromAnchor);
        }

        public Vec3d ToTrueVelocity(Vec3d localPosition, Vec3d localVelocity, BodyState[] states)
        {
            Vec3d fromAnchor = localPosition + Origin;
            return states[Anchor].Velocity + Orientation(states).Rotate(localVelocity + Vec3d.Cross(AngularVelocityLocal, fromAnchor));
        }

        /// <summary>A direction (not a position) from true axes to local axes.</summary>
        public Vec3d ToLocalDirection(Vec3d trueDirection, BodyState[] states) => Orientation(states).Inverse.Rotate(trueDirection);

        public Vec3d ToTrueDirection(Vec3d localDirection, BodyState[] states) => Orientation(states).Rotate(localDirection);

        /// <summary>An object's own spin, from true axes to local.</summary>
        public Vec3d ToLocalSpin(Vec3d trueSpin, BodyState[] states) => Orientation(states).Inverse.Rotate(trueSpin) - AngularVelocityLocal;

        public Vec3d ToTrueSpin(Vec3d localSpin, BodyState[] states) => Orientation(states).Rotate(localSpin + AngularVelocityLocal);

        /// <summary>
        /// The acceleration to give an object in local coordinates, given the true acceleration on it (gravity,
        /// say). Takes off the anchor's own acceleration and adds the turning-frame pushes.
        /// </summary>
        public Vec3d ToLocalAcceleration(Vec3d localPosition, Vec3d localVelocity, Vec3d trueAcceleration, BodyState[] states)
        {
            Vec3d local = Orientation(states).Inverse.Rotate(trueAcceleration - states[Anchor].Acceleration);
            if (!Rotating) return local;
            Vec3d w = AngularVelocityLocal;
            Vec3d fromAnchor = localPosition + Origin;
            Vec3d coriolis = Vec3d.Cross(w, localVelocity) * -2.0;
            Vec3d centrifugal = -Vec3d.Cross(w, Vec3d.Cross(w, fromAnchor));
            return local + coriolis + centrifugal;
        }

        /// <summary>
        /// The same as <see cref="ToLocalAcceleration"/>, but tuned for a physics engine that takes one simple
        /// step of <paramref name="dt"/> seconds at a time. The sideways push in a turning frame depends on your
        /// velocity, and using the velocity from the start of the step lets a small error build up every step
        /// (about a hundred metres over five minutes of low flying, in the rehearsal that became the test
        /// AGameLikeRunThroughChangingPointsOfViewStaysOnCourse). Using the velocity expected halfway
        /// through the step removes almost all of it.
        /// </summary>
        public Vec3d ToLocalAccelerationForStep(Vec3d localPosition, Vec3d localVelocity, Vec3d trueAcceleration, BodyState[] states, double dt)
        {
            Vec3d first = ToLocalAcceleration(localPosition, localVelocity, trueAcceleration, states);
            if (!Rotating) return first;
            return ToLocalAcceleration(localPosition, localVelocity + first * (dt * 0.5), trueAcceleration, states);
        }

        // ------------------------------------------------------------------ changes

        /// <summary>Rides along with a different body. Nothing moves; only velocities are measured afresh.</summary>
        public void SetAnchor(int anchor, BodyState[] states)
        {
            if (anchor == Anchor) return;
            if (Rotating) StopRotating(states);
            // Keep every true point at the same local position: shift the origin by the gap between the two anchors.
            Vec3d gap = _base.Inverse.Rotate(states[Anchor].Position - states[anchor].Position);
            Origin += gap;
            Anchor = anchor;
        }

        /// <summary>Starts turning with the anchor. Nothing moves at the moment of the change.</summary>
        public void StartRotating(BodyState[] states)
        {
            if (Rotating) return;
            _lockAngle = states[Anchor].SpinAngle;
            Rotating = true;
        }

        public void StopRotating(BodyState[] states)
        {
            if (!Rotating) return;
            _base = Orientation(states).Normalized;
            Rotating = false;
        }

        /// <summary>
        /// Moves Unity's zero point to <paramref name="localPoint"/>. Every object's local position must have
        /// the same amount taken off it at the same moment. Done now and then to keep the player near zero,
        /// where single-precision numbers are most exact.
        /// </summary>
        public void Recenter(Vec3d localPoint)
        {
            Origin += localPoint;
        }

        /// <summary>
        /// Chooses the anchor, and whether to turn with it, for an object at <paramref name="truePosition"/>.
        /// Returns true if anything changed (callers must then re-measure velocities).
        /// </summary>
        public bool Follow(Vec3d truePosition, BodyState[] states)
        {
            bool changed = false;
            int wanted = _model.NeighbourhoodOf(truePosition, states, Anchor);
            if (wanted != Anchor)
            {
                SetAnchor(wanted, states);
                changed = true;
            }

            double lockRadius = _model.LockRadius(Anchor);
            double distance = Vec3d.Distance(truePosition, states[Anchor].Position);
            if (!Rotating && lockRadius > 0 && distance < lockRadius)
            {
                StartRotating(states);
                changed = true;
            }
            else if (Rotating && distance > lockRadius * 1.1)
            {
                StopRotating(states);
                changed = true;
            }
            return changed;
        }
    }
}
