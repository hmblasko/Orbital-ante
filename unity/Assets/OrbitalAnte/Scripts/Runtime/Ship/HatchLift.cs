using System;

namespace OrbitalAnte.Ship
{
    public enum LiftRide { None, Up, Down }

    /// <summary>
    /// The belly hatch and the gravity lift under it.
    ///
    /// The hatch is a pair of doors in the cabin floor over a short shaft down to the belly, where a blue ring
    /// shows the way in. With the doors open the lift is on: stand under the ring and it draws you up the
    /// shaft and into the cabin, and the doors close beneath your feet. Step onto the open well from inside and
    /// it lowers you out. The doors can be opened and closed at any time, in flight too, unless the hatch is
    /// broken, in which case they stay as they are until mended.
    ///
    /// Works in the ship's own space (see <see cref="ShipLayout"/>). No Unity types.
    /// </summary>
    public sealed class HatchLift
    {
        public float DoorSeconds = 0.5f;
        public float LiftSpeed = 2.6f;
        /// <summary>How far below the belly the lift can reach to pick someone up.</summary>
        public float Reach = 4f;
        const float ExitClearance = 0.1f;
        const float PickupInset = 0.1f;
        /// <summary>The working reach always leaves room for a whole suit and a reboarding margin.</summary>
        public float EffectiveReach => float.IsNaN(Reach) || float.IsInfinity(Reach)
            ? ShipLayout.PersonHeight + ExitClearance + PickupInset
            : Math.Max(Reach, ShipLayout.PersonHeight + ExitClearance + PickupInset);
        /// <summary>Feet position for release into space: clear of the hull but still inside pickup reach.</summary>
        public float SpaceExitY => ShipLayout.BellyY - EffectiveReach + PickupInset;

        /// <summary>The physical suit must be below the convex base before it is switched on.</summary>
        public bool HasExitClearance(float feetY) => feetY <= ShipLayout.BellyY - ShipLayout.PersonHeight - ExitClearance + 1e-4f;

        /// <summary>0 shut, 1 fully open.</summary>
        public float Openness { get; private set; }
        public bool WantOpen { get; private set; }
        public bool Open => Openness >= 0.95f;
        public bool Shut => Openness <= 0.001f;
        public bool Moving => !Shut && !Open || WantOpen != Open;

        public LiftRide Ride { get; private set; }
        /// <summary>The rider's feet, in ship space, while riding.</summary>
        public P3 Rider { get; private set; }
        float _rideBottom;
        bool _mustStepAway;

        /// <summary>Asks the doors to open or close. Returns false (and does nothing) if the hatch is broken.</summary>
        public bool Set(bool open, bool hatchWorks)
        {
            if (!hatchWorks) return false;
            // The doors will not close on someone in the shaft.
            if (!open && Ride != LiftRide.None) return false;
            WantOpen = open;
            return true;
        }

        public bool Toggle(bool hatchWorks) => Set(!WantOpen, hatchWorks);

        /// <summary>
        /// True when someone standing with their feet at <paramref name="feet"/> (ship space) is where the lift
        /// would pick them up: under the ring, within reach, with the doors open.
        /// </summary>
        public bool CanPickUp(P3 feet, bool requested = false)
        {
            float d = (float)Math.Sqrt(feet.X * feet.X + feet.Z * feet.Z);
            bool under = d < ShipLayout.ShaftRadius - 0.1f && feet.Y < ShipLayout.BellyY + 0.3f && feet.Y > ShipLayout.BellyY - EffectiveReach;
            if (!under)
            {
                // Having been set down, you have to step out from under the ring before it will take you again.
                if (d > ShipLayout.ShaftRadius + 0.15f || feet.Y < ShipLayout.BellyY - EffectiveReach) _mustStepAway = false;
                return false;
            }
            // Leaving the lift must not immediately pull you back in, but an explicit request can reboard
            // without first having to walk (or use a jetpack) out of the beam.
            return Open && WantOpen && Ride == LiftRide.None && (!_mustStepAway || requested);
        }

        public void BeginUp(P3 feet)
        {
            Ride = LiftRide.Up;
            Rider = feet;
        }

        /// <param name="bottomY">Ground height in ship space; NaN means release into space below the whole suit.</param>
        public void BeginDown(P3 feet, float bottomY)
        {
            Ride = LiftRide.Down;
            // Preserve a partly completed descent if the ground goes away while the ship is moving.
            Rider = new P3(feet.X, Math.Min(0f, feet.Y), feet.Z);
            if (float.IsNaN(bottomY) || float.IsInfinity(bottomY)) bottomY = SpaceExitY;
            _rideBottom = Math.Max(ShipLayout.BellyY - EffectiveReach, Math.Min(bottomY, ShipLayout.BellyY - 0.2f));
        }

        /// <summary>
        /// Moves the doors and anyone riding. Returns the ride that finished this step (Up: now standing in the
        /// cabin; Down: now outside under the ship), or None.
        /// </summary>
        /// <param name="hatchWorks">False while the hatch is broken: the doors then stay exactly where they are,
        /// even halfway through opening or closing, until it is mended (then they carry on to where they were going).</param>
        public LiftRide Tick(float dt, bool hatchWorks = true)
        {
            LastFinished = TickInner(dt, hatchWorks);
            return LastFinished;
        }

        /// <summary>What the most recent <see cref="Tick"/> returned.</summary>
        public LiftRide LastFinished { get; private set; }

        LiftRide TickInner(float dt, bool hatchWorks)
        {
            if (hatchWorks)
            {
                float target = WantOpen ? 1f : 0f;
                float step = dt / Math.Max(0.05f, DoorSeconds);
                Openness = Openness < target ? Math.Min(target, Openness + step) : Math.Max(target, Openness - step);
            }

            if (Ride == LiftRide.None) return LiftRide.None;

            // Drawn to the middle of the shaft first, so shoulders clear the rim.
            P3 r = Rider;
            float pull = Math.Min(1f, dt * 5f);
            r.X += (0f - r.X) * pull;
            r.Z += (0f - r.Z) * pull;
            float off = (float)Math.Sqrt(r.X * r.X + r.Z * r.Z);
            float room = ShipLayout.ShaftRadius - ShipLayout.PersonRadius - 0.05f;
            bool centred = off < room;

            if (Ride == LiftRide.Up)
            {
                bool belowBelly = r.Y + ShipLayout.PersonHeight < ShipLayout.BellyY;
                if (centred || belowBelly) r.Y = Math.Min(0f, r.Y + LiftSpeed * dt);
                Rider = r;
                if (r.Y >= 0f)
                {
                    Ride = LiftRide.None;
                    WantOpen = false;   // the doors close behind you
                    return LiftRide.Up;
                }
                return LiftRide.None;
            }

            if (centred && Open) r.Y = Math.Max(_rideBottom, r.Y - LiftSpeed * dt);
            Rider = r;
            if (r.Y <= _rideBottom)
            {
                Ride = LiftRide.None;
                _mustStepAway = true;
                return LiftRide.Down;
            }
            return LiftRide.None;
        }
    }

    public enum BeltState { Free, Buckling, Buckled, Unbuckling }

    /// <summary>
    /// Buckling into the pilot's seat. It takes a moment each way (the belt comes across, clicks, the seat
    /// settles with a hiss), and only when it has finished do the flight controls come alive.
    /// No Unity types.
    /// </summary>
    public sealed class SeatBelt
    {
        public float BuckleSeconds = 1.1f;
        public float UnbuckleSeconds = 0.6f;

        public BeltState State { get; private set; } = BeltState.Free;
        /// <summary>0 standing free, 1 strapped in. Moves smoothly between the two.</summary>
        public float Progress { get; private set; }
        public bool Buckled => State == BeltState.Buckled;
        public bool Seated => State != BeltState.Free;

        /// <summary>Set for one step at the moment the buckle clicks home.</summary>
        public bool Clicked { get; private set; }

        public bool Begin()
        {
            if (State != BeltState.Free) return false;
            State = BeltState.Buckling;
            return true;
        }

        public bool Release()
        {
            if (State != BeltState.Buckled) return false;
            State = BeltState.Unbuckling;
            return true;
        }

        /// <summary>Straps in at once, for starting a run already in the seat.</summary>
        public void StartBuckled()
        {
            State = BeltState.Buckled;
            Progress = 1f;
        }

        public void Tick(float dt)
        {
            Clicked = false;
            if (State == BeltState.Buckling)
            {
                Progress = Math.Min(1f, Progress + dt / BuckleSeconds);
                if (Progress >= 1f) { State = BeltState.Buckled; Clicked = true; }
            }
            else if (State == BeltState.Unbuckling)
            {
                Progress = Math.Max(0f, Progress - dt / UnbuckleSeconds);
                if (Progress <= 0f) State = BeltState.Free;
            }
        }

        /// <summary>How far you have sunk into the seat, 0 to 1 (eased, and done in the first part of buckling).</summary>
        public float Sit => Smooth(Math.Min(1f, Progress / 0.45f));

        /// <summary>How far across the belt has been drawn, 0 to 1 (the second part of buckling).</summary>
        public float Draw => Smooth(Math.Max(0f, Math.Min(1f, (Progress - 0.3f) / 0.65f)));

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
