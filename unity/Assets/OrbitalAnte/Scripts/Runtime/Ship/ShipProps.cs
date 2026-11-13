using System;
using System.Collections.Generic;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// Every fitting of the starter lander that is not the cabin shell itself: the gold base, legs, engines,
    /// thrusters and dish outside, and the floor, seat, desk, lockers and lamps inside. Each method returns a
    /// finished mesh in the ship's own space (see <see cref="ShipLayout"/>), already where it belongs unless
    /// its comment says it is built at the origin for the game to place.
    ///
    /// The look is the Apollo Lunar Module made friendly: chunky and rounded, a few clear colours, no fiddly
    /// greebles. Rounded things share their corners (so they shade smoothly); panels have corners of their
    /// own (so their edges stay crisp).
    ///
    /// Nothing here trusts a primitive to face the right way. Curved skins are turned by measuring them
    /// against a point inside the shape, and flat pieces against the direction they should face.
    /// </summary>
    public static class ShipProps
    {
        // ---------------------------------------------------------------- colours (as seen on screen)
        const string Gold = "#C8962E", GoldDark = "#B07E22", GoldLight = "#D9B04A";
        const string Black = "#1E1E22", Silver = "#B9BDC2", SilverDark = "#8E939A";
        const string DarkMetal = "#3A3C42", FrameGrey = "#6E7278";
        const string Orange = "#D9772B", EngineOrange = "#B5561E";
        const string Cream = "#EFE6CF", CreamLight = "#F4ECD8", CreamDark = "#E3D7BB";
        const string Blue = "#39B8FF", ScreenGreen = "#5CFF8A", ScreenAmber = "#FFB347";
        const string Red = "#D8342A", Yellow = "#F2C230";
        const string DeckA = "#8C867C", DeckB = "#7F796F";
        const string DeskBody = "#B8B2A6", DeskTop = "#4A4D55";
        const string LampWhite = "#FFE9C4", LampWarm = "#FFC98A";

        /// <summary>Corners round the lift well and the holes in the base: one per wall column, so they line up.</summary>
        const int WellSides = ShipLayout.WallColumns;

        // ================================================================ lists

        /// <summary>Every mesh by a short name (legs and thrusters once per index).</summary>
        public static IEnumerable<KeyValuePair<string, ShipMesh>> All()
        {
            foreach (var pair in Exterior()) yield return pair;
            foreach (var pair in Interior()) yield return pair;
        }

        /// <summary>The outside fittings: lit by the Sun at run time, not baked.</summary>
        public static IEnumerable<KeyValuePair<string, ShipMesh>> Exterior()
        {
            yield return Named("base", Base());
            for (int i = 0; i < ShipLayout.Legs; i++) yield return Named("leg" + i, Leg(i));
            yield return Named("engines", Engines());
            for (int i = 0; i < 4; i++) yield return Named("thruster" + i, Thruster(i));
            yield return Named("dish", Dish());
            yield return Named("hull_details", HullDetails());
            yield return Named("ring", Ring());
        }

        /// <summary>The inside fittings: these are the ones the light baker works on.</summary>
        public static IEnumerable<KeyValuePair<string, ShipMesh>> Interior()
        {
            yield return Named("floor", Floor());
            yield return Named("hatch_door0", HatchDoor(0));
            yield return Named("hatch_door1", HatchDoor(1));
            yield return Named("seat", Seat());
            yield return Named("desk", Desk());
            yield return Named("lever_post", LeverPost());
            yield return Named("lever", Lever());
            yield return Named("lockers", Lockers());
            yield return Named("ceiling_light", CeilingLight());
            yield return Named("wall_lamps", WallLamps());
            yield return Named("belt_strap", BeltStrap());
            yield return Named("belt_buckle", BeltBuckle());
        }

        static KeyValuePair<string, ShipMesh> Named(string name, ShipMesh mesh) => new KeyValuePair<string, ShipMesh>(name, mesh);

        // ================================================================ exterior

        /// <summary>
        /// The eight-sided descent stage: foil panels in gold, black and a little silver, like the real one.
        /// The top and bottom each have a round hole for the lift shaft (the shaft's own wall is not built here).
        /// </summary>
        public static ShipMesh Base()
        {
            var b = new MeshBuilder();
            const float chamfer = 0.08f;
            float inradius = ShipLayout.BaseInradius;
            float bottom = ShipLayout.BellyY, top = ShipLayout.BaseTop;

            for (int face = 0; face < 8; face++)
            {
                double mid = (face + 1) * Math.PI / 4.0;
                P3 outward = new P3(Math.Sin(mid), 0, Math.Cos(mid));
                P3 a = OctagonCorner(face, inradius), c = OctagonCorner(face + 1, inradius);
                P3 aIn = OctagonCorner(face, inradius - chamfer), cIn = OctagonCorner(face + 1, inradius - chamfer);

                // The flat side: three panels across, two up.
                const int across = 3, up = 2;
                for (int i = 0; i < across; i++)
                {
                    for (int j = 0; j < up; j++)
                    {
                        b.Color(BasePanelColour(face, i, j));
                        float u0 = i / (float)across, u1 = (i + 1) / (float)across;
                        float y0 = Lerp(bottom + chamfer, top - chamfer, j / (float)up);
                        float y1 = Lerp(bottom + chamfer, top - chamfer, (j + 1) / (float)up);
                        Flat(b, AtHeight(P3.Lerp(a, c, u0), y0), AtHeight(P3.Lerp(a, c, u1), y0),
                            AtHeight(P3.Lerp(a, c, u1), y1), AtHeight(P3.Lerp(a, c, u0), y1), outward);
                    }
                }

                // The softened edges above and below.
                b.Color(face % 2 == 0 ? Black : GoldDark);
                Flat(b, AtHeight(a, top - chamfer), AtHeight(c, top - chamfer), AtHeight(cIn, top), AtHeight(aIn, top), outward + P3.Up);
                b.Color(Black);
                Flat(b, AtHeight(a, bottom + chamfer), AtHeight(c, bottom + chamfer), AtHeight(cIn, bottom), AtHeight(aIn, bottom), outward - P3.Up);
            }

            // Top and bottom: rings from the shaft hole out to the eight-sided edge.
            float edge = inradius - chamfer;
            b.Color("#2A2A2E");
            Skin(b, WellSides, 2, (i, j) => OctagonRing(i, j == 0 ? ShipLayout.ShaftRadius : -1f, edge, top), Facing(P3.Up), wrapCols: true);
            const float plate = 1.3f;
            b.Color(Silver);
            Skin(b, WellSides, 2, (i, j) => OctagonRing(i, j == 0 ? ShipLayout.ShaftRadius : plate, edge, bottom), Facing(-P3.Up), wrapCols: true);
            b.Color(Black);
            Skin(b, WellSides, 2, (i, j) => OctagonRing(i, j == 0 ? plate : -1f, edge, bottom), Facing(-P3.Up), wrapCols: true);
            return b.Finish();
        }

        static P3 OctagonCorner(int corner, float inradius)
        {
            double a = (22.5 + 45.0 * corner) * Math.PI / 180.0;
            double r = inradius / Math.Cos(Math.PI / 8.0);
            return new P3(Math.Sin(a) * r, 0, Math.Cos(a) * r);
        }

        /// <summary>A point of a ring at a set radius, or (radius below zero) on the eight-sided edge in the same direction.</summary>
        static P3 OctagonRing(int step, float radius, float inradius, float y)
        {
            double a = step * 2.0 * Math.PI / WellSides;
            double r = radius;
            if (radius < 0f)
            {
                // Angle away from the middle of the nearest flat side.
                double quarter = Math.PI / 4.0;
                double off = a - Math.Round(a / quarter) * quarter;
                r = inradius / Math.Cos(off);
            }
            return new P3(Math.Sin(a) * r, y, Math.Cos(a) * r);
        }

        static string BasePanelColour(int face, int column, int row)
        {
            int h = (face * 7 + column * 13 + row * 5 + face * column) % 10;
            if (face % 2 == 0)
            {
                // The sides the legs hang from: mostly black, a little silver, the odd gold patch.
                if (h < 6) return Black;
                if (h < 9) return Silver;
                return Gold;
            }
            if (h < 5) return Gold;
            if (h < 7) return GoldDark;
            if (h < 9) return GoldLight;
            return Black;
        }

        /// <summary>
        /// One landing leg: a fat gold-wrapped upper strut, a slimmer silver lower strut, two thin side struts
        /// back to the base, and a round dished footpad whose underside sits exactly on the ground. Leg 0
        /// carries the ladder.
        /// </summary>
        public static ShipMesh Leg(int index)
        {
            var b = new MeshBuilder();
            double angle = ShipLayout.LegAngle(index);
            P3 root = ShipLayout.LegRoot(index);
            P3 pad = ShipLayout.Footpad(index);
            P3 ankle = pad + new P3(0f, 0.16f, 0f);
            P3 along = ankle - root;
            P3 knee = root + along * 0.55f;
            P3 dir = along.Unit();
            P3 side = new P3(Math.Cos(angle), 0, -Math.Sin(angle));

            // Where the leg meets the base.
            b.Color(DarkMetal);
            Blob(b, root, new P3(0.2f, 0.2f, 0.2f), 10, 6);

            // Upper strut, wrapped in gold foil.
            b.Color(Gold);
            Pipe(b, new[] { root, root + dir * 0.06f, knee - dir * 0.06f, knee }, k => k == 0 || k == 3 ? 0.11f : 0.135f, 12, false, false);
            b.Color(DarkMetal);
            Blob(b, knee, new P3(0.165f, 0.165f, 0.165f), 10, 6);

            // Lower strut: the bare silver piston.
            b.Color(Silver);
            Rod(b, knee, ankle, 0.08f, 0.08f, 10, false, false);
            b.Color(SilverDark);
            Blob(b, ankle, new P3(0.12f, 0.12f, 0.12f), 10, 6);

            // Two side struts from the knee back to the corners of the base.
            for (int s = -1; s <= 1; s += 2)
            {
                double ca = angle + s * Math.PI / 8.0;
                double cr = ShipLayout.BaseInradius / Math.Cos(Math.PI / 8.0) - 0.03;
                P3 corner = new P3(Math.Sin(ca) * cr, ShipLayout.BellyY + 0.22f, Math.Cos(ca) * cr);
                b.Color(Silver);
                Rod(b, knee, corner, 0.045f, 0.045f, 8, false, false);
                b.Color(DarkMetal);
                Blob(b, corner, new P3(0.08f, 0.08f, 0.08f), 8, 4);
            }

            // The footpad: a shallow dish, flat underneath, with a rolled rim.
            float[] r = { 0f, 0.2f, 0.34f, 0.43f, 0.45f, 0.435f, 0.4f, 0.3f, 0.15f, 0f };
            float[] h = { 0f, 0f, 0.025f, 0.075f, 0.12f, 0.135f, 0.115f, 0.075f, 0.055f, 0.05f };
            for (int k = 0; k < r.Length; k++) r[k] *= ShipLayout.FootpadRadius / 0.45f;
            Revolve(b, pad, P3.Up, r, h, 20, 0.06f, row => b.Color(row >= 6 ? GoldDark : Gold));

            if (index == 0)
            {
                // The ladder, standing a little proud of the upper strut.
                P3 off = P3.Cross(side, dir).Unit();
                if (off.Y < 0f) off = -off;
                const float rail = 0.15f, stand = 0.2f, from = 0.07f, to = 0.5f;
                b.Color(Silver);
                for (int s = -1; s <= 1; s += 2)
                {
                    P3 a = root + along * from + side * (rail * s) + off * stand;
                    P3 c = root + along * to + side * (rail * s) + off * stand;
                    Rod(b, a, c, 0.022f, 0.022f, 6, true, true);
                    // Brackets holding the rail to the strut.
                    Rod(b, a + dir * 0.05f, root + along * from + dir * 0.05f, 0.016f, 0.016f, 5, false, false);
                    Rod(b, c - dir * 0.05f, root + along * to - dir * 0.05f, 0.016f, 0.016f, 5, false, false);
                }
                const int rungs = 8;
                for (int k = 0; k < rungs; k++)
                {
                    float t = Lerp(from + 0.03f, to - 0.03f, k / (float)(rungs - 1));
                    P3 m = root + along * t + off * stand;
                    Rod(b, m - side * rail, m + side * rail, 0.018f, 0.018f, 6, false, false);
                }
            }
            return b.Finish();
        }

        /// <summary>Four engine bells under the belly: dark metal outside, dull orange inside, seen from both sides.</summary>
        public static ShipMesh Engines()
        {
            var b = new MeshBuilder();
            float depth = ShipLayout.BellyY - ShipLayout.EngineExitY;
            float exit = ShipLayout.EngineExitRadius;
            const float throat = 0.11f, wall = 0.022f, neck = 0.09f;
            const int steps = 5;

            var r = new List<float>();
            var h = new List<float>();
            // Mounting collar, then the neck.
            r.Add(0.17f); h.Add(0f);
            r.Add(0.17f); h.Add(-0.06f);
            r.Add(throat); h.Add(-neck);
            // The outside of the bell.
            for (int k = 1; k <= steps; k++)
            {
                float t = k / (float)steps;
                r.Add(BellRadius(t, throat, exit)); h.Add(-neck - (depth - neck) * t);
            }
            int firstInside = r.Count;
            // Round the lip and back up the inside.
            for (int k = steps; k >= 0; k--)
            {
                float t = k / (float)steps;
                r.Add(BellRadius(t, throat, exit) - wall); h.Add(-neck - (depth - neck) * t);
            }
            // The plate at the top of the inside.
            r.Add(0f); h.Add(-neck);

            for (int e = 0; e < 4; e++)
            {
                Revolve(b, ShipLayout.Engine(e), P3.Up, r.ToArray(), h.ToArray(), 16, 0f,
                    row => b.Color(row < firstInside ? DarkMetal : row == r.Count - 1 ? Orange : EngineOrange));
            }
            return b.Finish();
        }

        static float BellRadius(float t, float throat, float exit) =>
            throat + (exit - throat) * (float)Math.Pow(Math.Sin(t * Math.PI * 0.5), 0.85);

        /// <summary>
        /// One thruster cluster on its stalk: a rounded block carrying four little nozzles that point up, down
        /// and to either side, like the Lunar Module's thruster quads.
        /// </summary>
        public static ShipMesh Thruster(int index)
        {
            var b = new MeshBuilder();
            double angle = ShipLayout.LegAngle(index);
            int column = index * (ShipLayout.WallColumns / 4) + ShipLayout.WallColumns / 8;
            P3 centre = ShipLayout.Thruster(index);
            P3 radial = new P3(Math.Sin(angle), 0, Math.Cos(angle));
            P3 side = new P3(Math.Cos(angle), 0, -Math.Sin(angle));
            // The stalk starts a little way into the hull's skin so there is no gap.
            P3 foot = ShipLayout.SurfacePoint(column, 5.86f, 0.7f);

            b.Color(SilverDark);
            Rod(b, foot, centre, 0.1f, 0.075f, 10, false, false);
            b.Color(Silver);
            SoftBox(b, centre, new P3(0.15f, 0.15f, 0.15f), 0.6f, 12, 6, side, P3.Up);

            P3[] ways = { P3.Up, -P3.Up, side, -side };
            float[] r = { 0.05f, 0.055f, 0.085f, 0.07f, 0.04f, 0f };
            float[] h = { 0f, 0.07f, 0.2f, 0.2f, 0.07f, 0.06f };
            for (int n = 0; n < ways.Length; n++)
            {
                Revolve(b, centre + ways[n] * 0.1f, ways[n], r, h, 8, 0f, row => b.Color(row < 3 ? DarkMetal : Black));
            }
            return b.Finish();
        }

        /// <summary>The steerable dish on its mast: a shallow bowl tilted up and back, with a feed horn on three struts.</summary>
        public static ShipMesh Dish()
        {
            var b = new MeshBuilder();
            P3 axis = new P3(-0.25f, 0.8f, -0.55f).Unit();
            P3 centre = ShipLayout.DishCentre;
            P3 apex = centre - axis * 0.05f;
            float radius = ShipLayout.DishRadius;
            const float depth = 0.16f, thick = 0.015f;
            float curve = depth / (radius * radius);

            // Mast and its foot.
            b.Color(SilverDark);
            Blob(b, ShipLayout.DishMastFoot + new P3(0f, 0.1f, 0f), new P3(0.11f, 0.07f, 0.11f), 10, 4);
            b.Color(Silver);
            Rod(b, ShipLayout.DishMastFoot + new P3(0f, 0.08f, 0f), apex - axis * 0.03f, 0.06f, 0.045f, 8, false, false);
            b.Color(DarkMetal);
            Blob(b, apex - axis * 0.04f, new P3(0.09f, 0.09f, 0.09f), 10, 6);

            // The bowl: back skin out to the rim, round the rim, front skin back to the middle.
            const int rings = 4;
            var r = new List<float>();
            var h = new List<float>();
            r.Add(0f); h.Add(-thick);
            for (int k = 1; k <= rings; k++)
            {
                float q = radius * k / rings;
                r.Add(q); h.Add(curve * q * q - thick);
            }
            int firstFront = r.Count;
            for (int k = rings; k >= 1; k--)
            {
                float q = radius * k / rings;
                r.Add(q); h.Add(curve * q * q);
            }
            r.Add(0f); h.Add(0f);
            Revolve(b, apex, axis, r.ToArray(), h.ToArray(), 18, depth * 0.5f, row => b.Color(row < firstFront ? SilverDark : "#ECE8DD"));

            // Feed horn at the focus, held by three struts from near the rim.
            float focus = 1f / (4f * curve);
            P3 feed = apex + axis * Math.Min(focus, 0.62f);
            b.Color(DarkMetal);
            Blob(b, feed, new P3(0.055f, 0.075f, 0.055f), 8, 4, null, axis);
            Basis(axis, out P3 e1, out P3 e2);
            b.Color(Silver);
            for (int k = 0; k < 3; k++)
            {
                double t = (90.0 + k * 120.0) * Math.PI / 180.0;
                float q = radius * 0.9f;
                P3 onDish = apex + axis * (curve * q * q) + (e1 * (float)Math.Cos(t) + e2 * (float)Math.Sin(t)) * q;
                Rod(b, onDish, feed, 0.012f, 0.012f, 5, false, false);
            }
            return b.Finish();
        }

        /// <summary>
        /// Things fixed to the outside of the cabin: the equipment bay bulge on the back, the docking ring on
        /// top, two whip antennas, grab rails and a few raised seams. All of it sits on or outside the hull's
        /// outer skin and keeps off the window panels.
        /// </summary>
        public static ShipMesh HullDetails()
        {
            var b = new MeshBuilder();
            float t = ShipLayout.HullThickness;
            P3 middle = new P3(0f, 0.3f, 0f);

            // --- Equipment bay: a pillow-shaped bulge across the back, rising smoothly out of the hull.
            const float bayFrom = 27f, bayTo = 37f, bayLow = 2.6f, bayHigh = 7.4f, bayDepth = 0.3f;
            const int bayAcross = 20, bayUp = 12;
            b.Color(SilverDark);
            Skin(b, bayAcross + 1, bayUp + 1, (i, j) =>
            {
                float u = i / (float)bayAcross, v = j / (float)bayUp;
                float rise = bayDepth * Pillow(u) * Pillow(v);
                return ShipLayout.SurfacePoint(Lerp(bayFrom, bayTo, u), Lerp(bayLow, bayHigh, v), 1f + rise / t);
            }, Away(middle));

            // --- Docking ring on the very top, sitting on the roof cap, with a dark hatch inside it.
            ShipLayout.OuterProfile(ShipLayout.WallRows - 1, out _, out float roof);
            float ringBase = roof + 0.002f;
            float[] dr = { 0.55f, 0.55f, 0.53f, 0.47f, 0.43f, 0.42f, 0.42f, 0f };
            float[] dh = { 0f, 0.1f, 0.14f, 0.16f, 0.14f, 0.08f, 0.08f, 0.08f };
            Revolve(b, new P3(0f, ringBase, 0f), P3.Up, dr, dh, 28, 0f, row => b.Color(row >= 6 ? DarkMetal : Silver));

            // --- Two whip antennas with ball tips.
            float[] whipColumn = { 19.5f, 55.5f };
            for (int k = 0; k < whipColumn.Length; k++)
            {
                P3 at = ShipLayout.SurfacePoint(whipColumn[k], 14f, 1f);
                P3 n = (at - ShipLayout.SurfacePoint(whipColumn[k], 14f, 0f)).Unit();
                P3 lean = (n * 0.45f + P3.Up).Unit();
                b.Color(DarkMetal);
                Blob(b, at + n * 0.036f, new P3(0.05f, 0.03f, 0.05f), 8, 4, null, n);
                P3 tip = at + n * 0.04f + lean * 1.15f;
                b.Color(Silver);
                Rod(b, at + n * 0.04f, tip, 0.018f, 0.009f, 5, false, false);
                b.Color(Orange);
                Blob(b, tip, new P3(0.028f, 0.028f, 0.028f), 6, 4);
            }

            // --- Grab rails: one along the nose under the front window, one under each porthole, two on the roof.
            b.Color(Silver);
            Rail(b, 60f, 4.72f, 68f, 4.72f, 10);
            Rail(b, 13.6f, 4.72f, 18.4f, 4.72f, 6);
            Rail(b, 45.6f, 4.72f, 50.4f, 4.72f, 6);
            Rail(b, 16f, 15f, 16f, 18.3f, 5);
            Rail(b, 48f, 15f, 48f, 18.3f, 5);

            // --- Raised seams: a belt below the windows (broken by the bay) and six uprights between fittings.
            b.Color("#9A9EA4");
            const float beltRow = 4.2f;
            Seam(b, 54, k => bayTo + k, k => beltRow, 0f, 0.09f, middle);
            float[] upright = { 10f, 22f, 26f, 38f, 42f, 54f };
            for (int k = 0; k < upright.Length; k++)
            {
                float column = upright[k];
                Seam(b, 15, s => column, s => 4.5f + s * 0.5f, 0.12f, 0f, middle);
            }
            return b.Finish();
        }

        /// <summary>0 at the edges, nearly flat across the middle, with rounded shoulders.</summary>
        static float Pillow(float u)
        {
            double d = Math.Abs(2.0 * u - 1.0);
            return (float)Math.Sqrt(Math.Max(0.0, 1.0 - d * d * d * d));
        }

        /// <summary>A handrail standing off the hull between two places on the wall grid.</summary>
        static void Rail(MeshBuilder b, float column0, float row0, float column1, float row1, int steps)
        {
            float t = ShipLayout.HullThickness;
            float foot = 1f + 0.006f / t, stand = 1f + 0.09f / t;
            var path = new List<P3> { ShipLayout.SurfacePoint(column0, row0, foot) };
            for (int k = 0; k <= steps; k++)
            {
                float u = k / (float)steps;
                path.Add(ShipLayout.SurfacePoint(Lerp(column0, column1, u), Lerp(row0, row1, u), stand));
            }
            path.Add(ShipLayout.SurfacePoint(column1, row1, foot));
            Pipe(b, path, k => 0.022f, 6, false, false);
        }

        /// <summary>A low raised strip along the hull, flat on top with sloped sides.</summary>
        static void Seam(MeshBuilder b, int steps, Func<int, float> column, Func<int, float> row, float halfColumns, float halfRows, P3 middle)
        {
            float top = 1f + 0.012f / ShipLayout.HullThickness;
            float[] across = { -1f, -0.6f, 0.6f, 1f };
            Skin(b, 4, steps + 1, (i, j) =>
                ShipLayout.SurfacePoint(column(j) + across[i] * halfColumns, row(j) + across[i] * halfRows, i == 0 || i == 3 ? 1f : top),
                Away(middle));
        }

        /// <summary>The blue entry ring under the belly.</summary>
        public static ShipMesh Ring()
        {
            var b = new MeshBuilder();
            b.Color(Blue, 3);
            P3 centre = new P3(0f, ShipLayout.BellyY - 0.04f, 0f);
            const float tube = 0.07f;
            const int around = 40, sides = 8;
            Skin(b, around, sides, (i, j) =>
            {
                double a = i * 2.0 * Math.PI / around, s = j * 2.0 * Math.PI / sides;
                double r = ShipLayout.RingRadius + Math.Cos(s) * tube;
                return centre + new P3(Math.Sin(a) * r, Math.Sin(s) * tube, Math.Cos(a) * r);
            }, (c, i, j) =>
            {
                // Away from the middle of the tube at this place round the ring.
                P3 flat = new P3(c.X - centre.X, 0f, c.Z - centre.Z).Unit();
                return c - (centre + flat * ShipLayout.RingRadius);
            }, wrapCols: true, wrapRows: true);
            return b.Finish();
        }

        // ================================================================ interior

        /// <summary>
        /// The cabin floor: deck plates in two close warm greys from the lift well out to where the wall
        /// begins, with a thin glowing blue strip round the well. Cut small so baked light has corners to sit on.
        /// </summary>
        public static ShipMesh Floor()
        {
            var b = new MeshBuilder();
            float well = ShipLayout.ShaftRadius;
            float stripIn = well + 0.04f, stripOut = well + 0.1f;

            b.Color(DarkMetal);
            FloorRing(b, well, stripIn);
            b.Color(Blue, 1.5);
            FloorRing(b, stripIn, stripOut);

            const int rings = 8;
            for (int ring = 0; ring < rings; ring++)
            {
                float r0 = Lerp(stripOut, ShipLayout.FloorRadius, ring / (float)rings);
                float r1 = Lerp(stripOut, ShipLayout.FloorRadius, (ring + 1) / (float)rings);
                for (int i = 0; i < WellSides; i++)
                {
                    // Plates are two rings deep and four columns wide, staggered like brickwork.
                    int course = ring / 2;
                    int plate = (i + (course % 2) * 2) / 4;
                    b.Color((course + plate) % 2 == 0 ? DeckA : DeckB);
                    Flat(b, RingPoint(i, r0, 0f), RingPoint(i + 1, r0, 0f), RingPoint(i + 1, r1, 0f), RingPoint(i, r1, 0f), P3.Up);
                }
            }
            return b.Finish();
        }

        static void FloorRing(MeshBuilder b, float inner, float outer)
        {
            for (int i = 0; i < WellSides; i++)
            {
                Flat(b, RingPoint(i, inner, 0f), RingPoint(i + 1, inner, 0f), RingPoint(i + 1, outer, 0f), RingPoint(i, outer, 0f), P3.Up);
            }
        }

        static P3 RingPoint(int step, float radius, float y)
        {
            double a = step * 2.0 * Math.PI / WellSides;
            return new P3(Math.Sin(a) * radius, y, Math.Cos(a) * radius);
        }

        /// <summary>
        /// One half of the hatch that closes the lift well, in its closed place (half 0 on the pilot's left,
        /// half 1 on the right; the game slides them apart sideways). Dark tread on top, a yellow-and-black
        /// warning edge where the two halves meet.
        /// </summary>
        public static ShipMesh HatchDoor(int half)
        {
            var b = new MeshBuilder();
            float s = half == 0 ? -1f : 1f;
            float radius = ShipLayout.ShaftRadius;
            const float thick = 0.06f, band = 0.08f;
            const int arcs = 14, rows = 5;
            double start = Math.Asin(band / radius);

            // Top tread, from the warning edge out to the rim, striped in two dark greys.
            for (int k = 0; k < arcs; k++)
            {
                b.Color(k % 2 == 0 ? "#2B2C30" : "#37383D");
                for (int m = 0; m < rows; m++)
                {
                    Flat(b, DoorPoint(s, radius, start, arcs, k, m / (float)rows, band, 0f),
                        DoorPoint(s, radius, start, arcs, k + 1, m / (float)rows, band, 0f),
                        DoorPoint(s, radius, start, arcs, k + 1, (m + 1) / (float)rows, band, 0f),
                        DoorPoint(s, radius, start, arcs, k, (m + 1) / (float)rows, band, 0f), P3.Up);
                }
            }

            // Warning edge: saw teeth of yellow and black along the meeting line.
            const int teeth = 16;
            float reach = radius * (float)Math.Cos(start);
            for (int k = 0; k < teeth; k++)
            {
                float z0 = Lerp(-radius, radius, k / (float)teeth), z1 = Lerp(-radius, radius, (k + 1) / (float)teeth);
                P3 a = new P3(0f, 0f, z0), c = new P3(0f, 0f, z1);
                P3 a2 = new P3(s * band, 0f, Clamp(z0, -reach, reach)), c2 = new P3(s * band, 0f, Clamp(z1, -reach, reach));
                b.Color(Yellow);
                FlatTri(b, a, c, a2, P3.Up);
                b.Color(Black);
                FlatTri(b, c, c2, a2, P3.Up);
            }

            // Underside.
            b.Color(FrameGrey);
            for (int k = 0; k < arcs; k++)
            {
                for (int m = 0; m < rows; m++)
                {
                    Flat(b, DoorPoint(s, radius, 0.0, arcs, k, m / (float)rows, 0f, -thick),
                        DoorPoint(s, radius, 0.0, arcs, k + 1, m / (float)rows, 0f, -thick),
                        DoorPoint(s, radius, 0.0, arcs, k + 1, (m + 1) / (float)rows, 0f, -thick),
                        DoorPoint(s, radius, 0.0, arcs, k, (m + 1) / (float)rows, 0f, -thick), -P3.Up);
                }
            }

            // Round rim.
            for (int k = 0; k < arcs; k++)
            {
                P3 a = DoorPoint(s, radius, 0.0, arcs, k, 1f, 0f, 0f), c = DoorPoint(s, radius, 0.0, arcs, k + 1, 1f, 0f, 0f);
                Flat(b, a, c, c - new P3(0f, thick, 0f), a - new P3(0f, thick, 0f), new P3((a.X + c.X) * 0.5f, 0f, (a.Z + c.Z) * 0.5f));
            }

            // The straight face where the halves meet.
            b.Color(Yellow);
            const int cuts = 8;
            for (int k = 0; k < cuts; k++)
            {
                float z0 = Lerp(-radius, radius, k / (float)cuts), z1 = Lerp(-radius, radius, (k + 1) / (float)cuts);
                Flat(b, new P3(0f, 0f, z0), new P3(0f, 0f, z1), new P3(0f, -thick, z1), new P3(0f, -thick, z0), new P3(-s, 0f, 0f));
            }
            return b.Finish();
        }

        /// <summary>
        /// A point on a hatch half: <paramref name="step"/> counts round the rim, <paramref name="out01"/> runs
        /// from the straight line at <paramref name="lineX"/> (0) to the rim (1).
        /// </summary>
        static P3 DoorPoint(float s, float radius, double start, int arcs, int step, float out01, float lineX, float y)
        {
            double a = start + (Math.PI - 2.0 * start) * step / arcs;
            P3 rim = new P3(s * Math.Sin(a) * radius, y, Math.Cos(a) * radius);
            P3 line = new P3(s * lineX, y, rim.Z);
            return P3.Lerp(line, rim, out01);
        }

        /// <summary>
        /// The pilot's seat, facing the nose: fat orange cushions on a grey frame, headrest, armrests, a short
        /// flared pedestal. Kept well clear of where the seated pilot's eyes are, and low in front of them.
        /// </summary>
        public static ShipMesh Seat()
        {
            var b = new MeshBuilder();
            P3 s = ShipLayout.SeatPosition;

            // Pedestal: wide foot, slim stem, flaring again under the seat.
            float[] r = { 0.28f, 0.28f, 0.2f, 0.13f, 0.12f, 0.13f, 0.2f };
            float[] h = { 0f, 0.03f, 0.07f, 0.14f, 0.22f, 0.3f, 0.34f };
            b.Color(FrameGrey);
            Revolve(b, s + new P3(0f, 0f, 0.05f), P3.Up, r, h, 16, 0.17f, null);

            // Seat pan: grey shell with the cushion sitting in it.
            b.Color(FrameGrey);
            SoftBox(b, s + new P3(0f, 0.37f, 0.03f), new P3(0.27f, 0.06f, 0.3f), 0.5f, 16, 6);
            b.Color(Orange);
            SoftBox(b, s + new P3(0f, 0.445f, 0.05f), new P3(0.245f, 0.055f, 0.27f), 0.55f, 16, 6);

            // Back, leaning away a little.
            double lean = 8.0 * Math.PI / 180.0;
            P3 backUp = new P3(0, Math.Cos(lean), -Math.Sin(lean));
            P3 right = new P3(1f, 0f, 0f);
            b.Color(FrameGrey);
            SoftBox(b, s + new P3(0f, 0.72f, -0.27f), new P3(0.24f, 0.31f, 0.05f), 0.5f, 16, 8, right, backUp);
            b.Color(Orange);
            SoftBox(b, s + new P3(0f, 0.72f, -0.225f), new P3(0.215f, 0.27f, 0.045f), 0.55f, 16, 8, right, backUp);

            // Headrest on a short stalk.
            b.Color(FrameGrey);
            Rod(b, s + new P3(0f, 0.98f, -0.32f), s + new P3(0f, 1.2f, -0.35f), 0.03f, 0.03f, 8, false, false);
            b.Color(Orange);
            SoftBox(b, s + new P3(0f, 1.24f, -0.335f), new P3(0.15f, 0.11f, 0.05f), 0.6f, 12, 6);

            // Armrests.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 0.33f * side;
                b.Color(FrameGrey);
                Rod(b, s + new P3(x, 0.38f, -0.08f), s + new P3(x, 0.66f, -0.08f), 0.03f, 0.03f, 8, false, false);
                b.Color(Orange);
                SoftBox(b, s + new P3(x, 0.68f, 0f), new P3(0.05f, 0.035f, 0.22f), 0.6f, 8, 6);
            }
            return b.Finish();
        }

        // --- the desk's measurements, shared by its body and the things standing on it
        const float DeskFront = ShipLayout.DeskZ + 0.03f;
        const float DeskHalfWidth = 1.62f;
        const float DeskTopFrontY = 0.675f, DeskTopBackY = 0.745f, DeskUnderY = 0.4f;
        

        static float DeskBack(float x, float y)
        {
            float r = WallRadius(y) - 0.012f;
            return (float)Math.Sqrt(Math.Max(0.01f, r * r - x * x));
        }

        /// <summary>A point on the desk's sloping top: <paramref name="back01"/> 0 is the edge by the pilot, 1 the wall.</summary>
        static P3 DeskTopPoint(float x, float back01)
        {
            P3 front = new P3(x, DeskTopFrontY, DeskFront + 0.07f);
            P3 back = new P3(x, DeskTopBackY, DeskBack(x, DeskTopBackY));
            return P3.Lerp(front, back, back01);
        }

        const int DeskProfileRows = 14, DeskRepeatedRow = 9;

        /// <summary>
        /// The desk's cross-section at one place across it, from the wall along the underside, up the rounded
        /// front, and back along the top to the wall. Row 9 repeats row 8 so the top can change colour crisply.
        /// </summary>
        static P3 DeskProfile(float x, int row)
        {
            float back = DeskBack(x, DeskUnderY);
            if (row <= 3) return new P3(x, DeskUnderY, Lerp(back, DeskFront + 0.1f, row / 3f));
            switch (row)
            {
                case 4: return new P3(x, 0.44f, DeskFront + 0.03f);
                case 5: return new P3(x, 0.52f, DeskFront);
                case 6: return new P3(x, 0.6f, DeskFront);
                case 7: return new P3(x, 0.655f, DeskFront + 0.025f);
                case 8:
                case 9: return DeskTopPoint(x, 0f);
                default: return DeskTopPoint(x, (row - 9) / 4f);
            }
        }

        /// <summary>
        /// The instrument desk across the nose, its back following the wall, under the front window. Its middle
        /// is kept clear for the real dash; the small screens and switches out to the sides are dressing.
        /// </summary>
        public static ShipMesh Desk()
        {
            var b = new MeshBuilder();
            const int across = 18, profileRows = DeskProfileRows;

            // Body: one rounded section swept across the nose.
            Skin(b, across + 1, profileRows, (i, j) => DeskProfile(Lerp(-DeskHalfWidth, DeskHalfWidth, i / (float)across), j),
                Away(new P3(0f, 0.56f, 2.0f)), paint: (i, j) => b.Color(j >= DeskRepeatedRow ? DeskTop : DeskBody));
            for (int side = -1; side <= 1; side += 2)
            {
                var loop = new List<P3>();
                for (int j = 0; j < profileRows; j++)
                {
                    if (j != DeskRepeatedRow) loop.Add(DeskProfile(DeskHalfWidth * side, j));
                }
                // One more corner half-way down the wall side, so no edge of the end is long.
                loop.Add(P3.Lerp(loop[loop.Count - 1], loop[0], 0.5f));
                b.Color(DeskBody);
                FlatFan(b, loop, new P3(side, 0f, 0f));
            }

            // Screens: a dark bezel with a glowing face.
            // The middle of the desk is left clear for the real dash (CockpitDash), which stands on it.
            DeskScreen(b, -1.3f, 0.5f, 0.1f, 0.07f, ScreenAmber, 1.0);
            DeskScreen(b, 1.3f, 0.5f, 0.1f, 0.07f, ScreenGreen, 1.0);

            // Chunky toggle rows either side.
            string[] caps = { Silver, Silver, Red, Silver, Yellow };
            for (int side = -1; side <= 1; side += 2)
            {
                P3 plateAt = DeskTopPoint(0.9f * side, 0.3f);
                DeskFrame(0.9f * side, out P3 up, out P3 normal);
                b.Color(DarkMetal);
                Pad(b, plateAt + normal * 0.001f, new P3(1f, 0f, 0f), up, normal, 0.24f, 0.04f, 0.006f, 0.01f, 2, 1);
                for (int k = 0; k < caps.Length; k++)
                {
                    P3 at = plateAt + new P3((k - 2) * 0.09f, 0f, 0f) + normal * 0.006f;
                    b.Color(caps[k]);
                    Rod(b, at, at + (normal + up * (k % 2 == 0 ? 0.35f : -0.35f)).Unit() * 0.04f, 0.013f, 0.018f, 6, false, true);
                }
            }

            // A hand controller either side of the seat line.
            for (int side = -1; side <= 1; side += 2)
            {
                P3 foot = DeskTopPoint(0.42f * side, 0.04f);
                b.Color(DarkMetal);
                Blob(b, foot + new P3(0f, 0.008f, 0f), new P3(0.04f, 0.014f, 0.04f), 8, 4);
                Rod(b, foot, foot + new P3(0f, 0.06f, 0f), 0.014f, 0.014f, 6, false, false);
                b.Color(side < 0 ? Red : Black);
                Blob(b, foot + new P3(0f, 0.07f, 0f), new P3(0.026f, 0.03f, 0.026f), 8, 4);
            }

            return b.Finish();
        }

        /// <summary>Which way is "up the slope" and "out of the surface" on the desk top at this place across it.</summary>
        static void DeskFrame(float x, out P3 up, out P3 normal)
        {
            P3 d = DeskTopPoint(x, 1f) - DeskTopPoint(x, 0f);
            up = d.Unit();
            normal = new P3(0f, d.Z, -d.Y).Unit();
        }

        static void DeskScreen(MeshBuilder b, float x, float back01, float halfWidth, float halfHeight, string colour, double glow)
        {
            DeskFrame(x, out P3 up, out P3 normal);
            P3 at = DeskTopPoint(x, back01) + normal * 0.001f;
            P3 right = new P3(1f, 0f, 0f);
            b.Color(Black);
            Pad(b, at, right, up, normal, halfWidth + 0.02f, halfHeight + 0.02f, 0.012f, 0.014f, 2, 1);
            b.Color(colour, glow);
            Pad(b, at + normal * 0.012f, right, up, normal, halfWidth, halfHeight, 0.006f, 0.01f, 2, 1);
        }

        /// <summary>The floor post that carries the hatch lever, beside the lift well.</summary>
        public static ShipMesh LeverPost()
        {
            var b = new MeshBuilder();
            P3 foot = new P3(ShipLayout.HatchLever.X, 0f, ShipLayout.HatchLever.Z);
            float[] r = { 0.13f, 0.13f, 0.07f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.075f, 0.08f, 0.06f, 0f };
            float[] h = { 0f, 0.025f, 0.06f, 0.12f, 0.29f, 0.46f, 0.63f, 0.8f, 0.84f, 0.88f, 0.915f, 0.92f };
            Revolve(b, foot, P3.Up, r, h, 12, 0f, row => b.Color(row <= 2 ? DarkMetal : row >= 8 ? Orange : FrameGrey));

            // A little blue tell-tale facing the middle of the cabin.
            P3 inward = (-foot).Unit();
            b.Color(Blue, 1.2);
            Blob(b, foot + new P3(0f, 0.86f, 0f) + inward * 0.075f, new P3(0.018f, 0.018f, 0.018f), 6, 4);
            return b.Finish();
        }

        /// <summary>The lever handle on its own, built at the origin pointing straight up, for the game to place and swing.</summary>
        public static ShipMesh Lever()
        {
            var b = new MeshBuilder();
            b.Color(DarkMetal);
            Blob(b, P3.Zero, new P3(0.032f, 0.032f, 0.032f), 8, 4);
            b.Color(Silver);
            Rod(b, P3.Zero, new P3(0f, 0.2f, 0f), 0.018f, 0.016f, 8, false, false);
            b.Color(Red);
            Blob(b, new P3(0f, 0.225f, 0f), new P3(0.055f, 0.055f, 0.055f), 12, 8);
            return b.Finish();
        }

        // --- the lockers' measurements
        const float LockerFront = ShipLayout.LockerZ - 0.08f;
        const float LockerHalfWidth = 1.15f, LockerTop = 1.5f;
        const int LockerCount = 5;
        static readonly float[] LockerHeights = { 0f, 0.04f, 0.1f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0f, 1.2f, 1.4f, 1.47f, LockerTop };
        static readonly float[] LockerSetBack = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.03f, 0.08f };

        static float LockerBack(float x, float y)
        {
            float r = WallRadius(y) - 0.012f;
            return -(float)Math.Sqrt(Math.Max(0.01f, r * r - x * x));
        }

        /// <summary>A bank of five rounded lockers across the back wall: cream, with orange latches.</summary>
        public static ShipMesh Lockers()
        {
            var b = new MeshBuilder();
            const int across = LockerCount * 3, topCuts = 4, endCuts = 3;
            int fronts = LockerHeights.Length;

            // The carcass: front face, rounded top edge, then the top running back to the wall.
            b.Color(Cream);
            Skin(b, across + 1, fronts + topCuts, (i, j) =>
            {
                float x = Lerp(-LockerHalfWidth, LockerHalfWidth, i / (float)across);
                if (j < fronts) return new P3(x, LockerHeights[j], LockerFront - LockerSetBack[j]);
                float back01 = (j - fronts + 1) / (float)topCuts;
                return new P3(x, LockerTop, Lerp(LockerFront - LockerSetBack[fronts - 1], LockerBack(x, LockerTop), back01));
            }, Away(new P3(0f, 0.7f, -2.1f)));

            // The two ends, reaching back to the wall at every height.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = LockerHalfWidth * side;
                Skin(b, endCuts + 1, fronts, (i, j) =>
                {
                    float y = LockerHeights[j];
                    return new P3(x, y, Lerp(LockerFront - LockerSetBack[j], LockerBack(x, y), i / (float)endCuts));
                }, Facing(new P3(side, 0f, 0f)));
            }

            // Doors, latches and vent slots.
            float width = 2f * LockerHalfWidth / LockerCount;
            P3 right = new P3(1f, 0f, 0f), forward = new P3(0f, 0f, 1f);
            for (int k = 0; k < LockerCount; k++)
            {
                float x = -LockerHalfWidth + width * (k + 0.5f);
                b.Color(k == 3 ? CreamDark : CreamLight);
                Pad(b, new P3(x, 0.76f, LockerFront), right, P3.Up, forward, width * 0.5f - 0.025f, 0.64f, 0.05f, 0.04f, 2, 6);
                b.Color(Orange);
                SoftBox(b, new P3(x + width * 0.3f, 0.8f, LockerFront + 0.055f), new P3(0.022f, 0.06f, 0.018f), 0.6f, 8, 4);
                b.Color(DarkMetal);
                for (int v = 0; v < 3; v++)
                {
                    Pad(b, new P3(x - 0.02f, 1.2f + v * 0.05f, LockerFront + 0.0505f), right, P3.Up, forward, 0.11f, 0.013f, 0.003f, 0.004f);
                }
            }
            return b.Finish();
        }

        /// <summary>The big ring lamp under the ceiling: warm white underneath, slim grey housing above, on four short stems.</summary>
        public static ShipMesh CeilingLight()
        {
            var b = new MeshBuilder();
            float radius = ShipLayout.CeilingLightRadius;
            P3 centre = new P3(0f, ShipLayout.CeilingLightY, 0f);
            const float wide = 0.06f, tall = 0.04f;
            const int around = ShipLayout.WallColumns, sides = 8;
            Skin(b, around, sides, (i, j) =>
            {
                double a = i * 2.0 * Math.PI / around, s = j * 2.0 * Math.PI / sides;
                double r = radius + Math.Cos(s) * wide;
                return centre + new P3(Math.Sin(a) * r, Math.Sin(s) * tall, Math.Cos(a) * r);
            }, (c, i, j) =>
            {
                P3 flat = new P3(c.X, 0f, c.Z).Unit();
                return c - (centre + flat * radius);
            }, wrapCols: true, wrapRows: true, paint: (i, j) =>
            {
                // The underside is the lamp; the rest is housing.
                if (j > sides / 2) b.Color(LampWhite, 6);
                else b.Color(FrameGrey);
            });

            b.Color(FrameGrey);
            float stemTop = CeilingHeight(radius + 0.02f) - 0.003f;
            for (int k = 0; k < ShipLayout.Legs; k++)
            {
                double a = ShipLayout.LegAngle(k);
                P3 at = new P3(Math.Sin(a) * radius, 0, Math.Cos(a) * radius);
                Rod(b, at + new P3(0f, centre.Y + tall * 0.5f, 0f), at + new P3(0f, stemTop, 0f), 0.02f, 0.02f, 6, false, false);
            }
            return b.Finish();
        }

        /// <summary>Four small warm lamps on the wall above the legs, each a glowing dome under a little hood.</summary>
        public static ShipMesh WallLamps()
        {
            var b = new MeshBuilder();
            const float row = 9.86f;
            for (int k = 0; k < ShipLayout.Legs; k++)
            {
                int column = k * (ShipLayout.WallColumns / 4) + ShipLayout.WallColumns / 8;
                double a = ShipLayout.ColumnAngle(column);
                P3 wall = ShipLayout.SurfacePoint(column, row, 0f);
                P3 inward = (wall - ShipLayout.SurfacePoint(column, row, 1f)).Unit();
                P3 across = new P3(Math.Cos(a), 0, -Math.Sin(a));
                P3 up = P3.Cross(inward, across).Unit();
                if (up.Y < 0f) up = -up;
                P3 foot = wall + inward * 0.008f;

                // The lamp: half a squashed ball on a short skirt.
                const int around = 10, rings = 4;
                Skin(b, around, rings + 2, (i, j) =>
                {
                    double lon = i * 2.0 * Math.PI / around;
                    double lat = Math.Max(0, j - 1) * (Math.PI * 0.5) / rings;
                    float outBy = j == 0 ? 0f : 0.012f + (float)Math.Sin(lat) * 0.05f;
                    return foot + across * ((float)(Math.Cos(lon) * Math.Cos(lat)) * 0.08f)
                        + up * ((float)(Math.Sin(lon) * Math.Cos(lat)) * 0.055f) + inward * outBy;
                }, Away(foot), wrapCols: true, paint: (i, j) => b.Color(LampWarm, 5));

                // The hood: a shell over the top half, grey outside and pale inside.
                const int hoodAcross = 8, hoodRings = 4;
                for (int skin = 0; skin < 2; skin++)
                {
                    float shrink = skin == 0 ? 0f : 0.008f;
                    b.Color(skin == 0 ? FrameGrey : Cream);
                    Func<int, int, P3> hoodAt = (i, j) =>
                    {
                        double lon = (15.0 + 150.0 * i / hoodAcross) * Math.PI / 180.0;
                        double lat = j * (Math.PI * 0.5) / hoodRings;
                        return foot + across * ((float)(Math.Cos(lon) * Math.Cos(lat)) * (0.115f - shrink))
                            + up * ((float)(Math.Sin(lon) * Math.Cos(lat)) * (0.085f - shrink))
                            + inward * ((float)Math.Sin(lat) * (0.09f - shrink));
                    };
                    if (skin == 0) Skin(b, hoodAcross + 1, hoodRings + 1, hoodAt, Away(foot));
                    else Skin(b, hoodAcross + 1, hoodRings + 1, hoodAt, Toward(foot));
                }
            }
            return b.Finish();
        }

        /// <summary>
        /// One seat-belt strap: a flat ribbon along +X from 0 to 1, for the game to stretch and swing in front
        /// of the camera. Dark webbing with a lighter stitched edge.
        /// </summary>
        public static ShipMesh BeltStrap()
        {
            var b = new MeshBuilder();
            const float half = 0.025f, thick = 0.004f, stitch = 0.006f;
            const int cuts = 8;
            float[] z = { -half, -half + stitch, half - stitch, half };
            for (int k = 0; k < cuts; k++)
            {
                float x0 = k / (float)cuts, x1 = (k + 1) / (float)cuts;
                for (int s = 0; s < 3; s++)
                {
                    b.Color(s == 1 ? "#33353A" : "#6B6E75");
                    Flat(b, new P3(x0, thick, z[s]), new P3(x1, thick, z[s]), new P3(x1, thick, z[s + 1]), new P3(x0, thick, z[s + 1]), P3.Up);
                }
                b.Color("#2A2B2F");
                Flat(b, new P3(x0, -thick, -half), new P3(x1, -thick, -half), new P3(x1, -thick, half), new P3(x0, -thick, half), -P3.Up);
                for (int s = -1; s <= 1; s += 2)
                {
                    Flat(b, new P3(x0, -thick, half * s), new P3(x1, -thick, half * s), new P3(x1, thick, half * s), new P3(x0, thick, half * s), new P3(0f, 0f, s));
                }
            }
            for (int s = 0; s <= 1; s++)
            {
                Flat(b, new P3(s, -thick, -half), new P3(s, -thick, half), new P3(s, thick, half), new P3(s, thick, -half), new P3(s == 0 ? -1f : 1f, 0f, 0f));
            }
            return b.Finish();
        }

        /// <summary>A chunky metal buckle with a red release button, centred on the origin and lying flat like the strap.</summary>
        public static ShipMesh BeltBuckle()
        {
            var b = new MeshBuilder();
            b.Color("#C9CCD1");
            SoftBox(b, P3.Zero, new P3(0.035f, 0.008f, 0.03f), 0.5f, 16, 4);
            P3 right = new P3(1f, 0f, 0f), forward = new P3(0f, 0f, 1f);
            b.Color(Red);
            Pad(b, new P3(0.006f, 0.0075f, 0f), right, forward, P3.Up, 0.015f, 0.013f, 0.004f, 0.004f);
            b.Color(DarkMetal);
            Pad(b, new P3(-0.024f, 0.0072f, 0f), right, forward, P3.Up, 0.004f, 0.018f, 0.002f, 0.002f);
            return b.Finish();
        }

        // ================================================================ measuring the cabin

        /// <summary>Distance from the middle to the inside of the cabin wall at a height (the wall as it is really built, in straight steps).</summary>
        static float WallRadius(float y)
        {
            ShipLayout.InnerProfile(0, out float r0, out float h0);
            if (y <= h0) return r0;
            for (int row = 1; row < ShipLayout.WallRows; row++)
            {
                ShipLayout.InnerProfile(row, out float r1, out float h1);
                if (y <= h1) return Lerp(r0, r1, (y - h0) / Math.Max(1e-6f, h1 - h0));
                r0 = r1;
                h0 = h1;
            }
            return r0;
        }

        /// <summary>Height of the ceiling as it is really built, above a point this far from the middle.</summary>
        static float CeilingHeight(float distance)
        {
            int first = ShipLayout.WallRows - 14;
            ShipLayout.InnerProfile(first, out float r0, out float h0);
            if (distance >= r0) return h0;
            for (int row = first + 1; row < ShipLayout.WallRows; row++)
            {
                ShipLayout.InnerProfile(row, out float r1, out float h1);
                if (distance >= r1) return Lerp(h0, h1, (r0 - distance) / Math.Max(1e-6f, r0 - r1));
                r0 = r1;
                h0 = h1;
            }
            return h0;
        }

        // ================================================================ shape helpers

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        static P3 AtHeight(P3 p, float y) => new P3(p.X, y, p.Z);

        static Func<P3, int, int, P3> Away(P3 pivot) => (c, i, j) => c - pivot;
        static Func<P3, int, int, P3> Toward(P3 pivot) => (c, i, j) => pivot - c;
        static Func<P3, int, int, P3> Facing(P3 direction) => (c, i, j) => direction;

        /// <summary>Two directions square to an axis and to each other.</summary>
        static void Basis(P3 axis, out P3 e1, out P3 e2)
        {
            P3 a = axis.Unit();
            P3 pick = Math.Abs(a.Y) < 0.9f ? P3.Up : new P3(1f, 0f, 0f);
            e1 = P3.Cross(a, pick).Unit();
            e2 = P3.Cross(a, e1).Unit();
        }

        /// <summary>
        /// A smooth skin of columns by rows shared corners from a rule. The whole skin is turned to face the
        /// way <paramref name="outward"/> says (asked at the middle of every patch and added up, so one odd
        /// patch cannot flip it). Rows that shrink to a point share one corner, and slivers with no area are
        /// left out.
        /// </summary>
        static void Skin(MeshBuilder b, int columns, int rows, Func<int, int, P3> at, Func<P3, int, int, P3> outward,
            bool wrapCols = false, bool wrapRows = false, Action<int, int> paint = null)
        {
            var points = new P3[columns * rows];
            var index = new int[columns * rows];
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < columns; i++)
                {
                    int k = j * columns + i;
                    P3 p = at(i, j);
                    points[k] = p;
                    if (i > 0 && P3.Distance(p, points[j * columns]) < 1e-6f)
                    {
                        index[k] = index[j * columns];
                        continue;
                    }
                    paint?.Invoke(i, j);
                    index[k] = b.Add(p);
                }
            }

            int across = wrapCols ? columns : columns - 1, up = wrapRows ? rows : rows - 1;
            double sum = 0;
            for (int j = 0; j < up; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    int i1 = (i + 1) % columns, j1 = (j + 1) % rows;
                    P3 pa = points[j * columns + i], pb = points[j * columns + i1], pc = points[j1 * columns + i1], pd = points[j1 * columns + i];
                    P3 normal = P3.Cross(pc - pa, pd - pb);
                    P3 centre = (pa + pb + pc + pd) * 0.25f;
                    sum += P3.Dot(normal, outward(centre, i, j));
                }
            }
            bool flip = sum < 0;
            for (int j = 0; j < up; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    int i1 = (i + 1) % columns, j1 = (j + 1) % rows;
                    int a = j * columns + i, c = j * columns + i1, d = j1 * columns + i1, e = j1 * columns + i;
                    if (flip)
                    {
                        Emit(b, points, index, a, d, c);
                        Emit(b, points, index, a, e, d);
                    }
                    else
                    {
                        Emit(b, points, index, a, c, d);
                        Emit(b, points, index, a, d, e);
                    }
                }
            }
        }

        static void Emit(MeshBuilder b, P3[] points, int[] index, int a, int c, int d)
        {
            if (index[a] == index[c] || index[c] == index[d] || index[a] == index[d]) return;
            if (P3.Cross(points[c] - points[a], points[d] - points[a]).Length < 1e-8f) return;
            b.Tri(index[a], index[c], index[d]);
        }

        /// <summary>A flat triangle with corners of its own, turned to face along <paramref name="toward"/>.</summary>
        static void FlatTri(MeshBuilder b, P3 a, P3 c, P3 d, P3 toward)
        {
            P3 normal = P3.Cross(c - a, d - a);
            if (normal.Length < 1e-8f) return;
            int ia = b.Add(a), ic = b.Add(c), id = b.Add(d);
            if (P3.Dot(normal, toward) >= 0f) b.Tri(ia, ic, id);
            else b.Tri(ia, id, ic);
        }

        /// <summary>A flat four-cornered panel with corners of its own (crisp edges), turned to face along <paramref name="toward"/>.</summary>
        static void Flat(MeshBuilder b, P3 a, P3 c, P3 d, P3 e, P3 toward)
        {
            bool firstOk = P3.Cross(c - a, d - a).Length >= 1e-8f, secondOk = P3.Cross(d - a, e - a).Length >= 1e-8f;
            if (!firstOk || !secondOk)
            {
                if (firstOk) FlatTri(b, a, c, d, toward);
                else if (secondOk) FlatTri(b, a, d, e, toward);
                else FlatTri(b, c, d, e, toward);
                return;
            }
            bool faces = P3.Dot(P3.Cross(c - a, d - a), toward) >= 0f;
            int ia = b.Add(a), ic = b.Add(c), id = b.Add(d), ie = b.Add(e);
            if (faces) { b.Tri(ia, ic, id); b.Tri(ia, id, ie); }
            else { b.Tri(ia, id, ic); b.Tri(ia, ie, id); }
        }

        /// <summary>Fills a flat outline with triangles meeting at its middle, facing along <paramref name="toward"/>.</summary>
        static void FlatFan(MeshBuilder b, IList<P3> loop, P3 toward)
        {
            P3 centre = P3.Zero;
            for (int i = 0; i < loop.Count; i++) centre += loop[i];
            centre *= 1f / loop.Count;
            int c = b.Add(centre);
            int first = b.VertexCount;
            for (int i = 0; i < loop.Count; i++) b.Add(loop[i]);
            for (int i = 0; i < loop.Count; i++)
            {
                int j = (i + 1) % loop.Count;
                P3 normal = P3.Cross(loop[i] - centre, loop[j] - centre);
                if (normal.Length < 1e-8f) continue;
                if (P3.Dot(normal, toward) >= 0f) b.Tri(c, first + i, first + j);
                else b.Tri(c, first + j, first + i);
            }
        }

        /// <summary>
        /// A shape turned round an axis: (radius, distance along the axis) pairs. Faces away from the point on
        /// the axis at <paramref name="pivotAlong"/>; a shape that closes on itself comes out right wherever
        /// that point is.
        /// </summary>
        static void Revolve(MeshBuilder b, P3 centre, P3 axis, float[] radius, float[] along, int sides, float pivotAlong, Action<int> paintRow)
        {
            P3 a = axis.Unit();
            Basis(a, out P3 e1, out P3 e2);
            Skin(b, sides, radius.Length, (i, j) =>
            {
                double t = i * 2.0 * Math.PI / sides;
                return centre + a * along[j] + (e1 * (float)Math.Cos(t) + e2 * (float)Math.Sin(t)) * radius[j];
            }, Away(centre + a * pivotAlong), wrapCols: true, paint: paintRow == null ? (Action<int, int>)null : (i, j) => paintRow(j));
        }

        /// <summary>A round bar following a path, with its own thickness at each point and optional flat ends.</summary>
        static void Pipe(MeshBuilder b, IList<P3> path, Func<int, float> radius, int sides, bool capStart, bool capEnd)
        {
            int n = path.Count;
            var tangent = new P3[n];
            for (int k = 0; k < n; k++)
            {
                P3 before = k > 0 ? (path[k] - path[k - 1]).Unit() : (path[1] - path[0]).Unit();
                P3 after = k < n - 1 ? (path[k + 1] - path[k]).Unit() : before;
                tangent[k] = (before + after).Unit();
            }
            var e1 = new P3[n];
            var e2 = new P3[n];
            Basis(tangent[0], out e1[0], out e2[0]);
            for (int k = 1; k < n; k++)
            {
                // Carry the frame along without letting it twist.
                P3 carried = e1[k - 1] - tangent[k] * P3.Dot(e1[k - 1], tangent[k]);
                if (carried.Length < 1e-4f) Basis(tangent[k], out carried, out _);
                e1[k] = carried.Unit();
                e2[k] = P3.Cross(tangent[k], e1[k]).Unit();
            }
            Func<int, int, P3> at = (i, j) =>
            {
                double t = i * 2.0 * Math.PI / sides;
                return path[j] + (e1[j] * (float)Math.Cos(t) + e2[j] * (float)Math.Sin(t)) * radius(j);
            };
            Skin(b, sides, n, at, (c, i, j) => c - (path[j] + path[Math.Min(n - 1, j + 1)]) * 0.5f, wrapCols: true);
            if (capStart) PipeCap(b, at, 0, sides, -tangent[0]);
            if (capEnd) PipeCap(b, at, n - 1, sides, tangent[n - 1]);
        }

        static void PipeCap(MeshBuilder b, Func<int, int, P3> at, int row, int sides, P3 toward)
        {
            var loop = new List<P3>(sides);
            for (int i = 0; i < sides; i++) loop.Add(at(i, row));
            FlatFan(b, loop, toward);
        }

        /// <summary>A straight round bar, optionally tapering.</summary>
        static void Rod(MeshBuilder b, P3 from, P3 to, float radiusFrom, float radiusTo, int sides, bool capStart, bool capEnd)
        {
            Pipe(b, new[] { from, to }, k => k == 0 ? radiusFrom : radiusTo, sides, capStart, capEnd);
        }

        /// <summary>A ball, optionally squashed, optionally turned so its own "up" lies along <paramref name="up"/>.</summary>
        static void Blob(MeshBuilder b, P3 centre, P3 radii, int around, int down, P3? right = null, P3? up = null)
        {
            SoftBox(b, centre, radii, 1f, around, down, right, up);
        }

        /// <summary>
        /// A box with its corners and edges rounded right off: <paramref name="roundness"/> 1 is a ball, about
        /// 0.5 a fat cushion, lower is squarer. One smooth closed skin.
        /// </summary>
        static void SoftBox(MeshBuilder b, P3 centre, P3 half, float roundness, int around, int down, P3? right = null, P3? up = null)
        {
            P3 u = (up ?? P3.Up).Unit();
            P3 r;
            // Left to itself the box keeps its width along the ship's own left-to-right, unless it has been stood on its side.
            P3 wanted = right ?? new P3(1f, 0f, 0f);
            P3 squared = wanted - u * P3.Dot(wanted, u);
            if (squared.Length > 0.05f) r = squared.Unit();
            else Basis(u, out r, out _);
            P3 f = P3.Cross(r, u).Unit();
            Skin(b, around, down + 1, (i, j) =>
            {
                double lon = i * 2.0 * Math.PI / around;
                double lat = -Math.PI * 0.5 + Math.PI * j / down;
                float ring = j == 0 || j == down ? 0f : Bend(Math.Cos(lat), roundness);
                float height = j == 0 ? -1f : j == down ? 1f : Bend(Math.Sin(lat), roundness);
                return centre + r * (ring * Bend(Math.Sin(lon), roundness) * half.X) + u * (height * half.Y)
                    + f * (ring * Bend(Math.Cos(lon), roundness) * half.Z);
            }, Away(centre), wrapCols: true);
        }

        static float Bend(double v, float power) => (float)(Math.Sign(v) * Math.Pow(Math.Abs(v), power));

        /// <summary>
        /// A low rounded plate standing on a surface: flat on top, sloping down to its edge, corners clipped.
        /// Open underneath. <paramref name="cutsAcross"/> and <paramref name="cutsUp"/> divide the top.
        /// </summary>
        static void Pad(MeshBuilder b, P3 centre, P3 right, P3 up, P3 normal, float halfWidth, float halfHeight,
            float thickness, float bevel, int cutsAcross = 1, int cutsUp = 1)
        {
            int columns = cutsAcross + 3, rows = cutsUp + 3;
            Skin(b, columns, rows, (i, j) =>
            {
                bool edgeI = i == 0 || i == columns - 1, edgeJ = j == 0 || j == rows - 1;
                float x = PadLine(i, columns, halfWidth, bevel), y = PadLine(j, rows, halfHeight, bevel);
                if (edgeI && edgeJ)
                {
                    // Clip the corner.
                    x -= Math.Sign(x) * bevel * 0.3f;
                    y -= Math.Sign(y) * bevel * 0.3f;
                }
                return centre + right * x + up * y + normal * (edgeI || edgeJ ? 0f : thickness);
            }, Facing(normal));
        }

        static float PadLine(int i, int count, float half, float bevel)
        {
            if (i == 0) return -half;
            if (i == count - 1) return half;
            return Lerp(-half + bevel, half - bevel, (i - 1) / (float)(count - 3));
        }
    }
}
