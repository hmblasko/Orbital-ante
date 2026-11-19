using System;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// Walking about inside the cabin. The ship has its own gravity: "down" is always the cabin floor, however
    /// the ship is turned and whatever it is doing, so this works entirely in the ship's own space and never
    /// touches the physics engine. That is what keeps it from ever jittering, sliding or falling through the
    /// floor while the ship tumbles.
    ///
    /// The room is a round floor with a few things to walk round. No Unity types.
    /// </summary>
    public sealed class InteriorWalker
    {
        public float X, Y, Z;
        public float VelocityX, VelocityY, VelocityZ;
        public bool Grounded { get; private set; } = true;
        /// <summary>True when standing over the open lift well: the lift should take over.</summary>
        public bool OverOpenWell { get; private set; }

        public float WalkSpeed = 3.2f;
        public float Acceleration = 26f;
        public float Gravity = 9.0f;
        public float JumpHeight = 0.45f;

        public void Place(float x, float z)
        {
            X = x; Z = z; Y = 0f;
            VelocityX = VelocityY = VelocityZ = 0f;
            Grounded = true;
            OverOpenWell = false;
            Resolve();
        }

        /// <param name="wishX">Wanted movement to the ship's right, -1 to 1.</param>
        /// <param name="wishZ">Wanted movement toward the ship's nose, -1 to 1.</param>
        public void Step(float dt, float wishX, float wishZ, bool jump, bool hatchOpen)
        {
            if (dt <= 0f) return;
            float length = (float)Math.Sqrt(wishX * wishX + wishZ * wishZ);
            if (length > 1f) { wishX /= length; wishZ /= length; }
            float control = Grounded ? 1f : 0.35f;
            VelocityX = Toward(VelocityX, wishX * WalkSpeed, Acceleration * control * dt);
            VelocityZ = Toward(VelocityZ, wishZ * WalkSpeed, Acceleration * control * dt);

            float oldX = X, oldZ = Z;
            X += VelocityX * dt;
            Z += VelocityZ * dt;
            Resolve();
            // In a tight corner the pushes can disagree. Then you simply stay where you were, which was fine.
            if (!CanStand(X, Z) && CanStand(oldX, oldZ)) { X = oldX; Z = oldZ; }
            // What the walls would not allow is taken out of the speed, so you slide along them without sticking.
            VelocityX = (X - oldX) / dt;
            VelocityZ = (Z - oldZ) / dt;

            if (Grounded && jump)
            {
                VelocityY = (float)Math.Sqrt(2f * Gravity * JumpHeight);
                Grounded = false;
            }
            VelocityY -= Gravity * dt;
            Y += VelocityY * dt;

            float distance = (float)Math.Sqrt(X * X + Z * Z);
            float head = ShipLayout.CeilingAt(distance + 0.2f) - ShipLayout.PersonHeight;
            if (Y > head)
            {
                // Also when drifting under the lower edge of the roof on the way down.
                Y = Math.Max(0f, head);
                if (VelocityY > 0f) VelocityY = 0f;
            }
            if (Y <= 0f)
            {
                Y = 0f;
                VelocityY = 0f;
                Grounded = true;
            }
            OverOpenWell = hatchOpen && Grounded && distance < ShipLayout.LiftCatchRadius;
        }

        /// <summary>Puts the walker back inside the room and outside everything standing in it.</summary>
        void Resolve()
        {
            for (int pass = 0; pass < 3; pass++)
            {
                float distance = (float)Math.Sqrt(X * X + Z * Z);
                if (distance > ShipLayout.WalkRadius)
                {
                    float k = ShipLayout.WalkRadius / distance;
                    X *= k; Z *= k;
                }
                float front = ShipLayout.DeskZ - ShipLayout.PersonRadius;
                float back = ShipLayout.LockerZ + ShipLayout.PersonRadius;
                if (Z > front) Z = front;
                if (Z < back) Z = back;

                for (int i = 0; i < ShipLayout.Obstacles.Length; i++)
                {
                    FloorObstacle o = ShipLayout.Obstacles[i];
                    float dx = X - o.X, dz = Z - o.Z;
                    float keep = o.Radius + ShipLayout.PersonRadius;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d >= keep) continue;
                    if (d < 1e-4f) { dx = 0f; dz = -1f; d = 1f; }
                    X = o.X + dx / d * keep;
                    Z = o.Z + dz / d * keep;
                }
            }
        }

        /// <summary>True when a spot on the floor plan is somewhere a person can stand.</summary>
        public static bool CanStand(float x, float z)
        {
            if (x * x + z * z > ShipLayout.WalkRadius * ShipLayout.WalkRadius + 1e-4f) return false;
            if (z > ShipLayout.DeskZ - ShipLayout.PersonRadius + 1e-4f) return false;
            if (z < ShipLayout.LockerZ + ShipLayout.PersonRadius - 1e-4f) return false;
            for (int i = 0; i < ShipLayout.Obstacles.Length; i++)
            {
                FloorObstacle o = ShipLayout.Obstacles[i];
                float dx = x - o.X, dz = z - o.Z;
                float keep = o.Radius + ShipLayout.PersonRadius - 1e-3f;
                if (dx * dx + dz * dz < keep * keep) return false;
            }
            return true;
        }

        static float Toward(float value, float target, float step)
        {
            if (value < target) return Math.Min(target, value + step);
            return Math.Max(target, value - step);
        }
    }
}
