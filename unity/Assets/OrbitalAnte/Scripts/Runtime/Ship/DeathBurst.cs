using System;
using System.Collections.Generic;
using OrbitalAnte.Dismemberment;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// How the player's own body comes apart when they die (Matthew, 15 Nov 2026: "the death animation should be the
    /// player gibbing everywhere not just leaning over"). Plain numbers, no engine, so it is tested outside Unity.
    ///
    /// The body is the same breakable body the enemies have (Dismemberment), so the pops, blood, decals and bouncy
    /// chunks are the ones already tuned for them. It always stays first person (Part B section 4: no third-person
    /// camera, not when dying): the view rides the head as it flies, and the rest of you goes everywhere around it.
    ///
    /// Inside the cabin the pieces must stay inside: the cabin has no colliders of its own (the walker is not a
    /// physics object) and the hull's colliders are solid shapes the pieces would start inside. So for the burst the
    /// pieces ignore the hull and a cage of boxes, fitted just outside the cabin's inside faces, holds them in
    /// (<see cref="CageBoxes"/>). The cage only ever touches the player's pieces.
    /// </summary>
    public static class DeathBurst
    {
        /// <summary>One break: at this many seconds after death, this part is destroyed.</summary>
        public readonly struct Step
        {
            public readonly float At;
            public readonly BodyPartId Part;
            public Step(float at, BodyPartId part) { At = at; Part = part; }
        }

        /// <summary>
        /// The order of the breaks. The torso goes first, which throws all four limbs outward, and the head goes in
        /// the same moment so the view is already flying. Then the hands, feet, forearms and shins split off the
        /// limbs in a quick ragged run (pop-pop-pop, not one bang), so every one of the 14 parts ends up on its own.
        /// </summary>
        public static readonly Step[] Steps =
        {
            new Step(0f, BodyPartId.Torso),
            new Step(0f, BodyPartId.Head),
            new Step(0.05f, BodyPartId.LeftHand),
            new Step(0.08f, BodyPartId.RightFoot),
            new Step(0.11f, BodyPartId.RightHand),
            new Step(0.15f, BodyPartId.LeftFoot),
            new Step(0.19f, BodyPartId.LeftForearm),
            new Step(0.23f, BodyPartId.RightShin),
            new Step(0.27f, BodyPartId.RightForearm),
            new Step(0.31f, BodyPartId.LeftShin),
        };

        /// <summary>Damage given to each part in its step: always enough.</summary>
        public const float Overkill = 1e6f;

        /// <summary>Seconds from death to the death page when the body has burst (the slump alone keeps 1.8 s).</summary>
        public const float Hold = 2.6f;

        /// <summary>How hard the head (and so the view) is flung, against what an enemy's head gets: enough to tumble,
        /// not so much that the whole moment is a blur. Lower in the cabin, where it meets a wall within a metre.</summary>
        public const float HeadFlingOutside = 0.7f;
        public const float HeadFlingAboard = 0.45f;

        /// <summary>Slowest and fastest spin of the head, radians a second: it rolls, it does not whirl.</summary>
        public const float HeadSpinLeast = 1.5f;
        public const float HeadSpinMost = 4f;

        /// <summary>The cabin's own pull on loose pieces, the same as the walker's (<see cref="InteriorWalker.Gravity"/>).</summary>
        public const float CabinPull = 9f;

        /// <summary>A change in the ship's velocity bigger than this in one physics step is not a push the cabin
        /// gives its contents but a change of reference frame (or a crash the cage would pass on anyway), so the
        /// pieces are given the same change instead of being struck by the walls.</summary>
        public const float FrameJump = 4f;

        /// <summary>Most acceleration handed to the pieces from the ship's motion, metres a second squared.</summary>
        public const float MostShipAcceleration = 60f;

        /// <summary>Applies the whole burst to a body model at once (for tests): what the game does over a third of a second.</summary>
        public static void ApplyAll(BodyModel body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            for (int i = 0; i < Steps.Length; i++) body.ApplyDamage(Steps[i].Part, Overkill);
        }

        /// <summary>One box of the cabin cage, in ship space: its middle, its half size (x across, y up, z out from
        /// the cabin's middle) and the angle it is turned about the ship's up axis (radians, the column's angle).</summary>
        public readonly struct Box
        {
            public readonly P3 Centre;
            public readonly P3 Half;
            public readonly float Yaw;
            public Box(P3 centre, P3 half, float yaw) { Centre = centre; Half = half; Yaw = yaw; }

            /// <summary>True when a ship-space point is inside this box.</summary>
            public bool Contains(P3 p)
            {
                float dx = p.X - Centre.X, dz = p.Z - Centre.Z;
                float c = (float)Math.Cos(Yaw), s = (float)Math.Sin(Yaw);
                // Local axes: out = (sin, 0, cos), across = (cos, 0, -sin), the same as the cabin's columns.
                float across = dx * c - dz * s;
                float outward = dx * s + dz * c;
                return Math.Abs(across) <= Half.X && Math.Abs(p.Y - Centre.Y) <= Half.Y && Math.Abs(outward) <= Half.Z;
            }
        }

        public const int CageColumns = 16;
        public const float CageThickness = 0.5f;
        const float Overlap = 0.08f;

        /// <summary>The heights the wall of the cage is cut at: the cove, the straight wall and the dome, split so
        /// each band's inside face can sit just outside the cabin's curve without cutting into the air.</summary>
        static readonly float[] Bands = { 0f, 0.2f, ShipLayout.CoveHeight, ShipLayout.WallTop, 1.75f, 2.2f, ShipLayout.CeilingPeak };

        /// <summary>The cage: a floor, rings of wall boxes up the curve of the cabin, and a lid.</summary>
        public static List<Box> CageBoxes()
        {
            var boxes = new List<Box>();
            float wide = ShipLayout.CabinRadius + CageThickness + 0.3f;
            float t = CageThickness * 0.5f;
            boxes.Add(new Box(new P3(0f, -t, 0f), new P3(wide, t, wide), 0f));
            boxes.Add(new Box(new P3(0f, ShipLayout.CeilingPeak + t, 0f), new P3(wide, t, wide), 0f));

            for (int b = 0; b + 1 < Bands.Length; b++)
            {
                float low = Bands[b], high = Bands[b + 1];
                // The wall's radius changes one way within a band, so its widest end is the furthest the air reaches.
                float radius = Math.Max(ShipLayout.InnerRadiusAt(low), ShipLayout.InnerRadiusAt(high));
                float middle = radius + t;
                // Wide enough that neighbours overlap at their outside faces, with no gap to slip through.
                float half = (radius + CageThickness) * (float)Math.Tan(Math.PI / CageColumns) * 1.15f;
                // Each band reaches a little into its neighbour, so there is no seam between them, but only where
                // its face would still be outside the cabin's air there.
                float bottom = ShipLayout.InnerRadiusAt(Math.Max(0f, low - Overlap)) <= radius ? low - Overlap : low;
                float top = ShipLayout.InnerRadiusAt(high + Overlap) <= radius ? high + Overlap : high;
                float bandHalf = (top - bottom) * 0.5f;
                for (int c = 0; c < CageColumns; c++)
                {
                    float a = (float)(c * 2.0 * Math.PI / CageColumns);
                    var centre = new P3(Math.Sin(a) * middle, (bottom + top) * 0.5f, Math.Cos(a) * middle);
                    boxes.Add(new Box(centre, new P3(half, bandHalf, t), a));
                }
            }
            return boxes;
        }
    }
}
