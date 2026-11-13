using System;
using System.Collections.Generic;

namespace OrbitalAnte.Ship
{
    public enum WindowShape { LanderTriangle, Round, RoundedBox }

    /// <summary>One window: a patch of the cabin wall, counted in the wall's own grid of columns and rows.</summary>
    public sealed class WindowSpec
    {
        public string Id;
        public string Name;
        /// <summary>First column of the wall grid the window's panel covers (may wrap past the last column).</summary>
        public int Column;
        public int Columns;
        public int Row;
        public int Rows;
        public WindowShape Shape;
        /// <summary>Mirrors the shape left to right (the two front windows are a matching pair).</summary>
        public bool Mirror;
    }

    /// <summary>Something standing on the cabin floor that you walk round: a circle on the floor plan.</summary>
    public struct FloorObstacle
    {
        public float X, Z, Radius;
        public FloorObstacle(float x, float z, float radius) { X = x; Z = z; Radius = radius; }
    }

    /// <summary>
    /// Every measurement of the starter lander, in one place, so the hull, the cabin, the colliders, the light
    /// baker and the walking rules all agree. Metres, in the ship's own space: the middle of the cabin floor is
    /// (0, 0, 0), up is +Y, the nose (the pilot's view) is +Z, and the pilot's right is +X.
    ///
    /// The shape is the Apollo Lunar Module made friendlier: a round bowl of a cabin sitting on a gold eight-sided
    /// base with four splayed legs. The one big change from the real thing is the belly: the single engine has
    /// become four small ones, to leave the middle clear for the lift shaft you enter by.
    /// </summary>
    public static class ShipLayout
    {
        // ---------------------------------------------------------------- cabin
        /// <summary>Inside radius of the cabin at its widest.</summary>
        public const float CabinRadius = 2.5f;
        /// <summary>Radius of the floor itself, where the wall curves up out of it like the side of a bowl.</summary>
        public const float FloorRadius = 2.15f;
        public const float CoveHeight = 0.5f;
        /// <summary>Height at which the wall starts curving over into the ceiling.</summary>
        public const float WallTop = 1.2f;
        /// <summary>Height of the ceiling at its middle.</summary>
        public const float CeilingPeak = 2.6f;
        public const float HullThickness = 0.14f;

        public const int WallColumns = 64;
        const int CoveSteps = 4;
        const int DomeSteps = 13;
        const float DomeStepDegrees = 6f;
        /// <summary>Rows in the wall grid, from the floor to the edge of the round ceiling cap.</summary>
        public const int WallRows = CoveSteps + 1 + 2 + DomeSteps;

        // ---------------------------------------------------------------- the person
        public const float PersonRadius = 0.3f;
        public const float PersonHeight = 1.8f;
        public const float EyeHeight = 1.62f;
        /// <summary>How far from the middle you can walk before your head meets the curve of the wall.</summary>
        public const float WalkRadius = 1.95f;

        // ---------------------------------------------------------------- belly entry
        /// <summary>Radius of the lift shaft that runs from the belly up to the cabin floor.</summary>
        public const float ShaftRadius = 0.8f;
        /// <summary>Height of the belly (the bottom of the base) below the cabin floor.</summary>
        public const float BellyY = -1.7f;
        /// <summary>Standing this close to the middle while the hatch is open, you are taken by the lift.</summary>
        public const float LiftCatchRadius = 0.6f;
        public const float RingRadius = 0.98f;

        // ---------------------------------------------------------------- base and legs
        /// <summary>Distance from the middle to each flat side of the eight-sided base.</summary>
        public const float BaseInradius = 2.3f;
        public const float BaseTop = -0.05f;
        /// <summary>Height of the bottom of the footpads: the ground, when the ship stands level.</summary>
        public const float GroundY = -3.8f;
        public const float FootpadRadius = 0.45f;
        public const float FootpadDistance = 4.3f;
        public const int Legs = 4;
        public const float EngineDistance = 1.5f;
        public const float EngineExitRadius = 0.4f;
        public const float EngineExitY = -2.35f;

        // ---------------------------------------------------------------- seat and controls
        public static readonly P3 SeatPosition = new P3(0f, 0f, 1.2f);
        /// <summary>Where your eyes are when buckled in.</summary>
        public static readonly P3 SeatedEye = new P3(0f, 1.18f, 1.28f);
        /// <summary>The hatch lever, on a post beside the lift well.</summary>
        public static readonly P3 HatchLever = new P3(1.05f, 1.0f, -0.55f);
        /// <summary>The same control outside, on the belly beside the ring.</summary>
        public static readonly P3 OutsideHatchButton = new P3(1.35f, BellyY - 0.05f, 0f);

        public static readonly FloorObstacle[] Obstacles =
        {
            new FloorObstacle(0f, 1.2f, 0.42f),     // the seat
            new FloorObstacle(1.05f, -0.55f, 0.16f) // the lever post
        };
        /// <summary>The instrument desk across the nose: you cannot walk further forward than this.</summary>
        public const float DeskZ = 1.72f;
        /// <summary>Lockers across the back wall.</summary>
        public const float LockerZ = -1.62f;

        // ---------------------------------------------------------------- lights (for the baker and the fittings)
        public const float CeilingLightRadius = 0.9f;
        public const float CeilingLightY = 2.36f;

        // ---------------------------------------------------------------- windows
        public static readonly WindowSpec[] Windows =
        {
            // One wide windscreen across the nose (Matthew, 13 Nov 2026: two small windows were hard to see out
            // of). It runs from just inside one front thruster to just inside the other (they sit at columns 56
            // and 8, with a wall lamp behind each), and from desk height up to where the roof window starts.
            new WindowSpec { Id = "window_front", Name = "Front window", Column = 58, Columns = 12, Row = 5, Rows = 7, Shape = WindowShape.RoundedBox },
            new WindowSpec { Id = "window_overhead", Name = "Overhead window", Column = 61, Columns = 6, Row = 13, Rows = 4, Shape = WindowShape.RoundedBox },
            new WindowSpec { Id = "window_right", Name = "Right porthole", Column = 13, Columns = 6, Row = 5, Rows = 5, Shape = WindowShape.Round },
            new WindowSpec { Id = "window_left", Name = "Left porthole", Column = 45, Columns = 6, Row = 5, Rows = 5, Shape = WindowShape.Round },
        };

        // ---------------------------------------------------------------- the wall's shape

        /// <summary>
        /// The inside of the cabin wall at one row of its grid: distance from the middle, and height. Row 0 is
        /// where the wall leaves the floor; the last row is the rim of the round ceiling cap.
        /// </summary>
        public static void InnerProfile(int row, out float radius, out float height)
        {
            if (row <= CoveSteps)
            {
                double a = row / (double)CoveSteps * Math.PI * 0.5;
                radius = FloorRadius + (CabinRadius - FloorRadius) * (float)Math.Sin(a);
                height = CoveHeight * (1f - (float)Math.Cos(a));
                return;
            }
            if (row == CoveSteps + 1) { radius = CabinRadius; height = 0.85f; return; }
            if (row == CoveSteps + 2) { radius = CabinRadius; height = WallTop; return; }
            double phi = (row - (CoveSteps + 2)) * DomeStepDegrees * Math.PI / 180.0;
            radius = CabinRadius * (float)Math.Cos(phi);
            height = WallTop + (CeilingPeak - WallTop) * (float)Math.Sin(phi);
        }

        /// <summary>The outside of the hull at the same row: the inside pushed out by the hull's thickness.</summary>
        public static void OuterProfile(int row, out float radius, out float height)
        {
            InnerProfile(Math.Max(0, row - 1), out float r0, out float h0);
            InnerProfile(Math.Min(WallRows - 1, row + 1), out float r1, out float h1);
            InnerProfile(row, out float r, out float h);
            // The wall runs from (r0, h0) to (r1, h1); outward is a quarter turn from that.
            float dr = r1 - r0, dh = h1 - h0;
            float length = (float)Math.Sqrt(dr * dr + dh * dh);
            radius = r + dh / length * HullThickness;
            height = h - dr / length * HullThickness;
        }

        /// <summary>The angle of a wall column in radians: 0 is the nose, a quarter turn is the pilot's right.</summary>
        public static double ColumnAngle(int column) => column * 2.0 * Math.PI / WallColumns;

        public static P3 InnerPoint(int column, int row)
        {
            InnerProfile(row, out float r, out float h);
            double a = ColumnAngle(column);
            return new P3(Math.Sin(a) * r, h, Math.Cos(a) * r);
        }

        public static P3 OuterPoint(int column, int row)
        {
            OuterProfile(row, out float r, out float h);
            double a = ColumnAngle(column);
            return new P3(Math.Sin(a) * r, h, Math.Cos(a) * r);
        }

        /// <summary>Height of the ceiling above a point this far from the middle of the cabin.</summary>
        public static float CeilingAt(float distance)
        {
            if (distance >= CabinRadius) return WallTop;
            double k = distance / CabinRadius;
            return WallTop + (CeilingPeak - WallTop) * (float)Math.Sqrt(1.0 - k * k);
        }

        /// <summary>How far the inside face of the cabin wall is from the middle at a given height (0 above the ceiling).</summary>
        public static float InnerRadiusAt(float height)
        {
            if (height < 0f || height > CeilingPeak) return 0f;
            if (height <= CoveHeight)
            {
                double a = Math.Acos(Math.Max(-1.0, Math.Min(1.0, 1.0 - height / CoveHeight)));
                return FloorRadius + (CabinRadius - FloorRadius) * (float)Math.Sin(a);
            }
            if (height <= WallTop) return CabinRadius;
            double phi = Math.Asin(Math.Min(1.0, (height - WallTop) / (CeilingPeak - WallTop)));
            return CabinRadius * (float)Math.Cos(phi);
        }

        /// <summary>True when a point (ship space) is in the open air of the cabin, inside its walls, floor and ceiling.</summary>
        public static bool InsideCabin(P3 p)
        {
            float d = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
            return p.Y >= 0f && d < InnerRadiusAt((float)p.Y);
        }

        public enum CabinExitKind { Wall, Floor, Shaft, Window }

        /// <summary>
        /// Follows a straight line (a bullet) from a point inside the cabin to where it first meets the inside of
        /// the hull. Returns false if the start is not inside the cabin. <paramref name="window"/> is the window
        /// whose glass it meets (index into <see cref="Windows"/>), or -1. Shaft means it goes down the hatch well.
        /// </summary>
        public static bool CabinExit(P3 origin, P3 direction, out float distance, out P3 point, out CabinExitKind kind, out int window)
        {
            distance = 0f; point = origin; kind = CabinExitKind.Wall; window = -1;
            if (!InsideCabin(origin)) return false;
            P3 dir = direction.Unit();
            const float step = 0.05f, longest = 2f * CabinRadius + CeilingPeak;
            float inside = 0f, outside = -1f;
            for (float t = step; t <= longest; t += step)
            {
                if (!InsideCabin(origin + dir * t)) { outside = t; break; }
                inside = t;
            }
            if (outside < 0f) outside = longest;
            for (int i = 0; i < 20; i++)
            {
                float mid = 0.5f * (inside + outside);
                if (InsideCabin(origin + dir * mid)) inside = mid; else outside = mid;
            }
            distance = outside;
            point = origin + dir * outside;
            float across = (float)Math.Sqrt(point.X * point.X + point.Z * point.Z);
            if (point.Y < 0.001f && across < FloorRadius - 0.01f)
            {
                kind = across < ShaftRadius ? CabinExitKind.Shaft : CabinExitKind.Floor;
                return true;
            }
            for (int w = 0; w < Windows.Length; w++)
            {
                // LocateOnWindow keeps the height inside the panel, so check the height first.
                InnerProfile(Windows[w].Row, out _, out float bottom);
                InnerProfile(Math.Min(WallRows - 1, Windows[w].Row + Windows[w].Rows), out _, out float top);
                if (point.Y < bottom || point.Y > top) continue;
                LocateOnWindow(Windows[w], point, out float u, out float v);
                if (u >= 0f && u <= 1f && v >= 0f && v <= 1f && OnGlass(Windows[w], u, v))
                {
                    kind = CabinExitKind.Window;
                    window = w;
                    return true;
                }
            }
            return true;
        }

        /// <summary>True when this cell of the wall grid is taken up by a window panel.</summary>
        public static bool InWindow(int column, int row)
        {
            for (int i = 0; i < Windows.Length; i++)
            {
                if (InWindow(Windows[i], column, row)) return true;
            }
            return false;
        }

        public static bool InWindow(WindowSpec w, int column, int row)
        {
            if (row < w.Row || row >= w.Row + w.Rows) return false;
            int offset = ((column - w.Column) % WallColumns + WallColumns) % WallColumns;
            return offset < w.Columns;
        }

        /// <summary>The middle of a window's glass, and the direction it faces (outward).</summary>
        public static void WindowCentre(WindowSpec w, out P3 centre, out P3 outward)
        {
            float column = w.Column + w.Columns * 0.5f;
            float row = w.Row + w.Rows * 0.5f;
            P3 inner = SurfacePoint(column, row, 0f);
            P3 outer = SurfacePoint(column, row, 1f);
            centre = P3.Lerp(inner, outer, 0.5f);
            outward = (outer - inner).Unit();
        }

        /// <summary>
        /// A point on the wall at a fractional column and row. <paramref name="through"/> 0 is the inside face,
        /// 1 the outside face.
        /// </summary>
        public static P3 SurfacePoint(float column, float row, float through)
        {
            int r0 = Math.Max(0, Math.Min(WallRows - 2, (int)Math.Floor(row)));
            float fr = row - r0;
            InnerProfile(r0, out float ia, out float ha);
            InnerProfile(r0 + 1, out float ib, out float hb);
            OuterProfile(r0, out float oa, out float ka);
            OuterProfile(r0 + 1, out float ob, out float kb);
            float radius = Lerp(Lerp(ia, ib, fr), Lerp(oa, ob, fr), through);
            float height = Lerp(Lerp(ha, hb, fr), Lerp(ka, kb, fr), through);
            double a = column * 2.0 * Math.PI / WallColumns;
            return new P3(Math.Sin(a) * radius, height, Math.Cos(a) * radius);
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>
        /// Where the edge of the glass is, for a point on the edge of the window's panel. Both are given from
        /// 0 to 1 across and up the panel. Every point on the panel's edge has a matching point on the glass's
        /// edge, which is how the frame between them is built.
        /// </summary>
        public static void GlassPoint(WindowSpec w, float edgeU, float edgeV, out float u, out float v)
        {
            double dx = edgeU - 0.5, dy = edgeV - 0.5;
            double angle = Math.Atan2(dy, dx);
            double reach = ShapeReach(w.Shape, angle, w.Mirror);
            double edge = Math.Sqrt(dx * dx + dy * dy);
            // Never outside the panel: leave a rim of frame all round.
            double k = Math.Min(reach / Math.Max(1e-6, edge), 0.92);
            u = (float)(0.5 + dx * k);
            v = (float)(0.5 + dy * k);
        }

        /// <summary>True when a point of a window's panel (0 to 1 across and up) is on the glass rather than the frame.</summary>
        public static bool OnGlass(WindowSpec w, float u, float v)
        {
            double dx = u - 0.5, dy = v - 0.5;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-6) return true;
            double angle = Math.Atan2(dy, dx);
            // The furthest the glass goes in this direction: its shape, but never past the rim of frame.
            double c = Math.Abs(Math.Cos(angle)), s = Math.Abs(Math.Sin(angle));
            double toEdge = 0.5 / Math.Max(c, s);
            return d <= Math.Min(ShapeReach(w.Shape, angle, w.Mirror), toEdge * 0.92);
        }

        /// <summary>
        /// Finds where a point in ship space falls on a window's panel, from 0 to 1 across and up it.
        /// (The opposite of <see cref="SurfacePoint"/>.)
        /// </summary>
        public static void LocateOnWindow(WindowSpec w, P3 point, out float u, out float v)
        {
            double angle = Math.Atan2(point.X, point.Z);
            double column = angle / (2.0 * Math.PI) * WallColumns;
            double offset = column - w.Column;
            offset -= Math.Floor(offset / WallColumns) * WallColumns;
            if (offset > WallColumns * 0.5) offset -= WallColumns;
            u = (float)(offset / w.Columns);
            // Rows rise steadily with height, so find the pair of rows the point's height lies between.
            float row = w.Row;
            for (int r = w.Row; r < w.Row + w.Rows; r++)
            {
                InnerProfile(r, out _, out float h0);
                InnerProfile(r + 1, out _, out float h1);
                if (point.Y >= h0 || r == w.Row) row = r + Math.Max(0f, Math.Min(1f, (point.Y - h0) / Math.Max(1e-4f, h1 - h0)));
            }
            v = (row - w.Row) / w.Rows;
        }

        /// <summary>How far the glass reaches from the middle of its panel in a given direction (the panel is 1 wide).</summary>
        static double ShapeReach(WindowShape shape, double angle, bool mirror)
        {
            double c = Math.Cos(angle), s = Math.Sin(angle);
            if (mirror) c = -c;
            switch (shape)
            {
                case WindowShape.Round:
                    return 0.46;
                case WindowShape.RoundedBox:
                {
                    // A "squircle": a box with generously rounded corners.
                    double p = 4.0;
                    return 0.46 / Math.Pow(Math.Pow(Math.Abs(c), p) + Math.Pow(Math.Abs(s), p), 1.0 / p);
                }
                default:
                {
                    // The Lunar Module's window: a triangle, wide along the top, coming down to a point low on
                    // the inboard side, with the corners rounded off. Built from three straight edges.
                    double best = double.MaxValue;
                    // Each edge: a point on it and its outward direction.
                    Edge(c, s, 0.0, 0.45, 0.0, 1.0, ref best);          // top
                    Edge(c, s, 0.45, 0.0, 0.92, -0.39, ref best);       // outboard, leaning in toward the bottom
                    Edge(c, s, -0.34, -0.12, -0.86, -0.50, ref best);    // inboard and under
                    return Math.Min(best, 0.60) * 0.97;
                }
            }
        }

        static void Edge(double c, double s, double px, double py, double nx, double ny, ref double best)
        {
            double along = c * nx + s * ny;
            if (along <= 1e-6) return;
            double d = (px * nx + py * ny) / along;
            if (d > 0 && d < best) best = d;
        }

        // ---------------------------------------------------------------- places on the outside

        /// <summary>Angle of leg <paramref name="index"/> (0 to 3) in radians: the legs point between the axes.</summary>
        public static double LegAngle(int index) => (45.0 + index * 90.0) * Math.PI / 180.0;

        public static P3 Footpad(int index)
        {
            double a = LegAngle(index);
            return new P3(Math.Sin(a) * FootpadDistance, GroundY, Math.Cos(a) * FootpadDistance);
        }

        /// <summary>Where a leg joins the base.</summary>
        public static P3 LegRoot(int index)
        {
            double a = LegAngle(index);
            return new P3(Math.Sin(a) * (BaseInradius + 0.1f), -0.55f, Math.Cos(a) * (BaseInradius + 0.1f));
        }

        public static P3 Engine(int index)
        {
            double a = index * Math.PI * 0.5;
            return new P3(Math.Sin(a) * EngineDistance, BellyY, Math.Cos(a) * EngineDistance);
        }

        /// <summary>The four thruster clusters, on stalks at "shoulder" height between the windows.</summary>
        public static P3 Thruster(int index)
        {
            double a = LegAngle(index);
            return new P3(Math.Sin(a) * 3.05f, 1.15f, Math.Cos(a) * 3.05f);
        }

        public static readonly P3 DishMastFoot = new P3(-0.95f, 2.4f, -1.05f);
        public static readonly P3 DishCentre = new P3(-1.2f, 3.35f, -1.3f);
        public const float DishRadius = 0.62f;
    }
}
