using System;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// How a placed rock, tree or plant sits in the ground, so the scatter stops looking stamped out
    /// (Matthew, 22 Nov 2026: the game should not "seem like it was created by AI"). Every copy stood dead
    /// upright, sat on top of the ground with its base showing, had one set of proportions and one colour per
    /// world. Real rocks are half buried and tipped, real trees lean a little and differ in girth, and no two
    /// stones on a hillside are the same shade.
    ///
    /// Plain C#, no Unity types. Everything comes from the placement's own seeded numbers, so the same rock
    /// always sits the same way for a seed (it is only looks, but a rock that changes when a tile reloads would
    /// be noticed). Nothing here moves a thing sideways or changes its height class, so what is solid, what
    /// shows from how far and the counts on F3 are unchanged.
    /// </summary>
    public static class NaturalPose
    {
        /// <summary>The way a thing is set into the ground.</summary>
        public readonly struct Pose
        {
            /// <summary>Tip away from upright, in degrees.</summary>
            public readonly float TiltDegrees;
            /// <summary>Which way it tips, in degrees round its own upright axis (after its yaw).</summary>
            public readonly float TiltHeading;
            /// <summary>Stretch along its own width (x), height (y) and depth (z). 1 is the model as made.</summary>
            public readonly float StretchX, StretchY, StretchZ;
            /// <summary>How far it is pushed into the ground, as a share of its height.</summary>
            public readonly float Sink;
            /// <summary>Its colour multiplied by this (1 unchanged), and nudged warm (above 0) or cool (below 0).</summary>
            public readonly float Shade, Warmth;

            public Pose(float tilt, float heading, float sx, float sy, float sz, float sink, float shade, float warmth)
            {
                TiltDegrees = tilt; TiltHeading = heading; StretchX = sx; StretchY = sy; StretchZ = sz;
                Sink = sink; Shade = shade; Warmth = warmth;
            }

            public static Pose Upright => new Pose(0f, 0f, 1f, 1f, 1f, 0f, 1f, 0f);
        }

        /// <summary>The limits for one family of things.</summary>
        public readonly struct Limits
        {
            public readonly float MaxTilt, Width, Height, MinSink, MaxSink, Shade, Warmth;
            /// <summary>True when width and depth must stretch together (a tree's trunk is a capsule).</summary>
            public readonly bool RoundTrunk;

            public Limits(float maxTilt, float width, float height, float minSink, float maxSink, float shade, float warmth, bool roundTrunk)
            {
                MaxTilt = maxTilt; Width = width; Height = height; MinSink = minSink; MaxSink = maxSink;
                Shade = shade; Warmth = warmth; RoundTrunk = roundTrunk;
            }
        }

        /// <summary>Colours are rounded to this many steps each way, so a world's rocks share nine materials (three shades by three warmths), not hundreds.</summary>
        public const int ShadeSteps = 1;

        /// <summary>What each kind is allowed. Tall thin landmarks barely move; loose stones move the most.</summary>
        public static Limits For(FeatureKind kind)
        {
            switch (kind)
            {
                // Loose stones: well bedded, tipped, never the same shape twice.
                case FeatureKind.Boulder: case FeatureKind.RockPile: case FeatureKind.Crag: case FeatureKind.SnowCrag:
                case FeatureKind.Outcrop: case FeatureKind.Tor:
                    return new Limits(14f, 0.24f, 0.16f, 0.10f, 0.24f, 0.09f, 0.5f, false);
                case FeatureKind.Slab:
                    return new Limits(9f, 0.22f, 0.12f, 0.12f, 0.28f, 0.09f, 0.5f, false);
                // A balanced rock is the one that must look like it should fall and has not.
                case FeatureKind.BalancedRock:
                    return new Limits(0f, 0.08f, 0.06f, 0.03f, 0.06f, 0.06f, 0.4f, false);
                // Landmarks: a lean of a few degrees reads as weathering, more reads as broken.
                case FeatureKind.Hoodoo: case FeatureKind.Spire: case FeatureKind.Arch: case FeatureKind.BasaltColumns:
                case FeatureKind.VentChimney:
                    return new Limits(2.5f, 0.08f, 0.06f, 0.03f, 0.06f, 0.06f, 0.4f, false);
                case FeatureKind.Crystal: case FeatureKind.IceShard: case FeatureKind.Stalagmite:
                    return new Limits(10f, 0.18f, 0.2f, 0.05f, 0.12f, 0.05f, 0.2f, false);
                // Floats on the sea: no sinking, it bobs at sea level already.
                case FeatureKind.Iceberg:
                    return new Limits(4f, 0.2f, 0.15f, 0f, 0f, 0.05f, 0.2f, false);
                // Trees: a small lean, roots in the ground, girth that differs tree to tree.
                case FeatureKind.Conifer: case FeatureKind.Broadleaf: case FeatureKind.Birch: case FeatureKind.Maple:
                case FeatureKind.Blossom: case FeatureKind.Pine: case FeatureKind.Acacia: case FeatureKind.SnowConifer:
                case FeatureKind.GiantTree:
                    return new Limits(4f, 0.12f, 0f, 0.015f, 0.03f, 0.07f, 0.6f, true);
                case FeatureKind.Palm:
                    return new Limits(9f, 0.1f, 0f, 0.015f, 0.03f, 0.06f, 0.5f, true);
                // A dead tree is usually on its way down.
                case FeatureKind.Snag:
                    return new Limits(11f, 0.12f, 0f, 0.02f, 0.04f, 0.08f, 0.5f, true);
                case FeatureKind.Log:
                    return new Limits(3f, 0.15f, 0.1f, 0.12f, 0.2f, 0.08f, 0.5f, false);
                // Small plants: a breeze's worth of lean, clumps of different spread.
                case FeatureKind.Bush: case FeatureKind.Cactus: case FeatureKind.Meadow: case FeatureKind.Reeds:
                case FeatureKind.Fern:
                    return new Limits(7f, 0.2f, 0.12f, 0.02f, 0.06f, 0.1f, 0.7f, false);
                default:
                    return new Limits(0f, 0f, 0f, 0f, 0f, 0f, 0f, false);
            }
        }

        /// <summary>The pose for one placement. Same numbers in, same pose out.</summary>
        public static Pose Of(FeatureKind kind, int variant, double yaw, bool floats)
        {
            Limits l = For(kind);
            uint h = Hash((uint)variant * 2654435761u ^ (uint)(yaw * 1e6) ^ ((uint)kind << 24));
            float u1 = Unit(ref h), u2 = Unit(ref h), u3 = Unit(ref h), u4 = Unit(ref h);
            float u5 = Unit(ref h), u6 = Unit(ref h), u7 = Unit(ref h), u8 = Unit(ref h);

            // Most things lean a little and a few lean a lot, as on a real hillside: square the draw.
            float tilt = l.MaxTilt * u1 * u1;
            float heading = u2 * 360f;
            float sx = 1f + l.Width * (u3 * 2f - 1f);
            float sz = l.RoundTrunk ? sx : 1f + l.Width * (u4 * 2f - 1f);
            float sy = 1f + l.Height * (u5 * 2f - 1f);
            float sink = floats ? 0f : l.MinSink + (l.MaxSink - l.MinSink) * u6;
            // Rounded to a few steps, so the shades share materials.
            float shade = 1f + l.Shade * Step(u7 * 2f - 1f);
            float warmth = l.Warmth * Step(u8 * 2f - 1f);
            return new Pose(tilt, heading, sx, sy, sz, sink, shade, warmth);
        }

        /// <summary>A colour with the pose's shade and warmth applied, each channel kept between 0 and 1.</summary>
        public static void Tint(in Pose pose, float r, float g, float b, out float outR, out float outG, out float outB)
        {
            // Warm adds a touch of red and takes a touch of blue; cool the other way. 0.04 at full is a shade a
            // painter would mix, not a colour change.
            float w = pose.Warmth * 0.04f;
            outR = Clamp01(r * pose.Shade + w);
            outG = Clamp01(g * pose.Shade);
            outB = Clamp01(b * pose.Shade - w);
        }

        static float Step(float signed)
        {
            float s = (float)Math.Round(signed * ShadeSteps) / ShadeSteps;
            return s < -1f ? -1f : s > 1f ? 1f : s;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        static uint Hash(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
                return x == 0 ? 1u : x;
            }
        }

        static float Unit(ref uint state)
        {
            unchecked
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (state >> 8) * (1f / 16777216f);
            }
        }
    }
}
