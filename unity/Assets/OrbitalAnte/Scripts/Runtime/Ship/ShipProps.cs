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
        const string DeckA = "#8E7762", DeckB = "#806A56";
        const string DeskBody = "#C4B69C", DeskTop = "#6E4A33";
        const string LampWhite = "#FFD7A0", LampWarm = "#FFB870";
        // The comforts: things that make the cabin a place to come home to.
        const string Rust = "#A8482E", Teal = "#3F7C7C", TealDark = "#2C4A56", Mustard = "#D9A441", Wool = "#E9DFC8";
        const string Cork = "#B58A5C", Terracotta = "#B9653F", Soil = "#3A2A20", Coffee = "#3B2414";
        const string LeafA = "#5E8C4A", LeafB = "#7BA85A", LeafC = "#4C7A45";
        const string BulbAmber = "#FFB25A", BulbWhite = "#FFE0AE", BulbSoft = "#FFC98A";
        const string StarGlow = "#E6FFD6";
        const string SeatShell = "#6B5A4D";

        /// <summary>How brightly each kind of lamp shines for the light baker, against the others.</summary>
        public const float RingLampGlow = 1.8f, WallLampGlow = 4.5f, FairyGlow = 2.5f, LanternGlow = 4f;

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
            yield return Named("lockers", Lockers());
            yield return Named("ceiling_light", CeilingLight());
            yield return Named("wall_lamps", WallLamps());
            yield return Named("comforts", Comforts());
            yield return Named("fairy_lights", FairyLights());
            yield return Named("hardware", Hardware());
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
            b.Color(SeatShell);
            SoftBox(b, s + new P3(0f, 0.37f, 0.03f), new P3(0.27f, 0.06f, 0.3f), 0.5f, 16, 6);
            b.Color(Orange);
            SoftBox(b, s + new P3(0f, 0.445f, 0.05f), new P3(0.245f, 0.055f, 0.27f), 0.55f, 16, 6);

            // Back, leaning away a little.
            double lean = 8.0 * Math.PI / 180.0;
            P3 backUp = new P3(0, Math.Cos(lean), -Math.Sin(lean));
            P3 right = new P3(1f, 0f, 0f);
            b.Color(SeatShell);
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

            // The console (the Console model, drawn by ConsoleInstruments) stands on the desk top and carries every
            // instrument, so the desk itself is left plain apart from the two sticks at its front edge.
            // A hand controller either side of the seat line.
            for (int side = -1; side <= 1; side += 2)
            {
                P3 foot = DeskTopPoint(0.85f * side, 0f) + new P3(0f, 0f, -0.012f);
                b.Color(DarkMetal);
                Blob(b, foot + new P3(0f, 0.008f, 0f), new P3(0.03f, 0.014f, 0.03f), 8, 4);
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

        /// <summary>Which of the five bays across the back wall holds the ship's computer instead of a locker door.</summary>
        public const int ComputerBay = 1;

        /// <summary>
        /// The ship's computer, set into one bay of the back wall: a small green screen, two tape reels, a field
        /// of pilot lights, a row of switches and a curl of printout. Its glows are all under
        /// <see cref="LightBake.CastsLightFrom"/>: they show lit and light nothing.
        /// </summary>
        static void Computer(MeshBuilder b, float x, float width)
        {
            P3 right = new P3(1f, 0f, 0f), forward = new P3(0f, 0f, 1f);
            float z = LockerFront;
            b.Color("#2B3138");
            Pad(b, new P3(x, 0.76f, z), right, P3.Up, forward, width * 0.5f - 0.025f, 0.64f, 0.03f, 0.04f, 2, 6);
            z += 0.03f;                                              // set back a little from the doors either side

            // The screen: a cream hood round green glass, with a few lines of dark "writing" on it.
            b.Color(CreamDark);
            SoftBox(b, new P3(x, 1.17f, z + 0.012f), new P3(0.165f, 0.125f, 0.03f), 0.35f, 12, 4);
            b.Color(Black);
            Pad(b, new P3(x, 1.17f, z + 0.04f), right, P3.Up, forward, 0.135f, 0.098f, 0.004f, 0.012f, 2, 1);
            b.Color(ScreenGreen, 1.0);
            Pad(b, new P3(x, 1.17f, z + 0.044f), right, P3.Up, forward, 0.122f, 0.085f, 0.003f, 0.016f, 2, 1);
            b.Color("#12351F");
            for (int line = 0; line < 4; line++)
            {
                float w = 0.03f + 0.065f * Scatter(line, 91);
                Pad(b, new P3(x - 0.1f + w, 1.215f - line * 0.03f, z + 0.0475f), right, P3.Up, forward, w, 0.0055f, 0.0005f, 0.001f);
            }

            // Two tape reels, turned to different places.
            for (int reel = 0; reel < 2; reel++)
            {
                P3 hub = new P3(x + (reel == 0 ? -0.088f : 0.088f), 0.9f, z);
                b.Color(Silver);
                Revolve(b, hub, forward, new[] { 0f, 0.074f, 0.074f, 0.06f, 0f }, new[] { 0f, 0f, 0.012f, 0.016f, 0.016f }, 14, 0.005f, null);
                b.Color("#5A3A28");                                  // the tape itself, showing between the flanges
                Revolve(b, hub, forward, new[] { 0.058f, 0.058f, 0.03f }, new[] { 0.0162f, 0.02f, 0.02f }, 14, 0.01f, null);
                b.Color(DarkMetal);
                Revolve(b, hub, forward, new[] { 0.03f, 0.022f, 0f }, new[] { 0.02f, 0.03f, 0.03f }, 8, 0.01f, null);
                for (int spoke = 0; spoke < 3; spoke++)
                {
                    double a = spoke * 2.0 * Math.PI / 3.0 + reel * 0.7;
                    P3 at = hub + new P3(Math.Cos(a) * 0.043, Math.Sin(a) * 0.043, 0.0205);
                    b.Color(Black);
                    Blob(b, at, new P3(0.011f, 0.011f, 0.002f), 6, 2);
                }
            }
            // The tape's path between them, over a little guide.
            b.Color("#5A3A28");
            Rod(b, new P3(x - 0.088f, 0.96f, z + 0.018f), new P3(x, 0.985f, z + 0.018f), Thinnest, Thinnest, 4, false, false);
            Rod(b, new P3(x, 0.985f, z + 0.018f), new P3(x + 0.088f, 0.96f, z + 0.018f), Thinnest, Thinnest, 4, false, false);
            b.Color(SilverDark);
            Blob(b, new P3(x, 0.985f, z + 0.012f), new P3(0.014f, 0.014f, 0.014f), 8, 4);

            // A field of pilot lights: some lit, some not, by rule so it is the same every time.
            string[] lit = { "#FFB347", "#7CFFB0", "#FF6A4D", "#FFB347" };
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    float pick = Scatter(row * 7 + col, 33);
                    P3 at = new P3(x - 0.135f + col * 0.045f, 0.71f - row * 0.042f, z + 0.004f);
                    if (pick < 0.55f) b.Color(lit[(row + col) % lit.Length], 1.1);
                    else b.Color(DarkMetal);
                    Blob(b, at, new P3(0.011f, 0.011f, 0.007f), 6, 3);
                }
            }

            // Switches, and a slot with a curl of printout.
            string[] caps = { Silver, Red, Silver, Silver, Yellow, Silver };
            for (int k = 0; k < caps.Length; k++)
            {
                P3 at = new P3(x - 0.125f + k * 0.05f, 0.52f, z);
                b.Color(Black);
                Blob(b, at, new P3(0.013f, 0.013f, 0.006f), 6, 3);
                b.Color(caps[k]);
                Rod(b, at, at + new P3(0f, k % 2 == 0 ? 0.016f : -0.016f, 0.034f), 0.005f, 0.008f, 6, false, true);
            }
            b.Color(Black);
            Pad(b, new P3(x, 0.4f, z), right, P3.Up, forward, 0.09f, 0.012f, 0.004f, 0.004f);
            var paper = new List<P3>();
            for (int k = 0; k <= 6; k++)
            {
                float t = k / 6f;
                paper.Add(new P3(0f, 0.4f - 0.2f * t, z + 0.006f + 0.045f * (float)Math.Sin(t * 2.6) * (1f - 0.4f * t)));
            }
            b.Color(Wool);
            for (int k = 0; k < 6; k++)
            {
                Flat(b, paper[k] + new P3(x - 0.075f, 0f, 0f), paper[k] + new P3(x + 0.075f, 0f, 0f),
                    paper[k + 1] + new P3(x + 0.075f, 0f, 0f), paper[k + 1] + new P3(x - 0.075f, 0f, 0f), forward);
            }
            b.Color("#9A8F7C");
            for (int k = 1; k < 5; k++)
            {
                float w = 0.02f + 0.04f * Scatter(k, 57);
                P3 mid = (paper[k] + paper[k + 1]) * 0.5f + new P3(x - 0.06f + w, 0f, 0.0015f);
                Flat(b, mid + new P3(-w, 0.004f, 0f), mid + new P3(w, 0.004f, 0f), mid + new P3(w, -0.004f, 0f), mid + new P3(-w, -0.004f, 0f), forward);
            }
        }

        /// <summary>A bank of rounded lockers across the back wall, cream with orange latches, with the ship's computer set into one bay.</summary>
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
                if (k == ComputerBay)
                {
                    Computer(b, x, width);
                    continue;
                }
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
                if (j > sides / 2) b.Color(LampWhite, RingLampGlow);
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

        /// <summary>
        /// Four small warm lamps on the wall above the legs: a frosted globe on a short brass arm, with a brass
        /// cap. The globe itself is the light, so the lamp glows from wherever you stand.
        /// </summary>
        public static ShipMesh WallLamps()
        {
            var b = new MeshBuilder();
            const float row = 9.86f;
            const string Brass = "#B08A45";
            for (int k = 0; k < ShipLayout.Legs; k++)
            {
                int column = k * (ShipLayout.WallColumns / 4) + ShipLayout.WallColumns / 8;
                double a = ShipLayout.ColumnAngle(column);
                P3 wall = ShipLayout.SurfacePoint(column, row, 0f);
                P3 inward = (wall - ShipLayout.SurfacePoint(column, row, 1f)).Unit();
                P3 across = new P3(Math.Cos(a), 0, -Math.Sin(a));
                P3 up = P3.Cross(inward, across).Unit();
                if (up.Y < 0f) up = -up;
                P3 foot = wall + inward * 0.004f - up * 0.05f;
                P3 globe = wall + inward * 0.082f + P3.Up * 0.012f;

                // Back plate and the arm that swans out and up to hold the globe from below.
                b.Color(Brass);
                Pad(b, foot, across, up, inward, 0.034f, 0.05f, 0.008f, 0.012f);
                var arm = new List<P3>();
                for (int n = 0; n <= 5; n++)
                {
                    float t = n / 5f;
                    P3 under = globe - P3.Up * 0.062f;
                    arm.Add(P3.Lerp(foot + inward * 0.006f, under, t) - P3.Up * ((float)Math.Sin(t * Math.PI) * 0.022f));
                }
                Pipe(b, arm, n => 0.008f, 6, false, false);
                float[] cupR = { 0.012f, 0.03f, 0.036f };
                float[] cupH = { -0.066f, -0.058f, -0.04f };
                Revolve(b, globe, P3.Up, cupR, cupH, 8, -0.2f, null);

                // The globe.
                b.Color(LampWarm, WallLampGlow);
                Blob(b, globe, new P3(0.054f, 0.058f, 0.054f), 10, 6);

                // A little cap on top.
                b.Color(Brass);
                float[] capR = { 0.03f, 0.022f, 0.008f, 0f };
                float[] capH = { 0.05f, 0.06f, 0.066f, 0.072f };
                Revolve(b, globe, P3.Up, capR, capH, 8, 0f, null);
            }
            return b.Finish();
        }

        // ================================================================ comforts

        /// <summary>The wall-grid row at a height above the floor (the opposite of <see cref="ShipLayout.InnerProfile"/>, from the cove up).</summary>
        static float RowAtHeight(float y)
        {
            if (y <= 0.85f) return 4f + Clamp((y - ShipLayout.CoveHeight) / (0.85f - ShipLayout.CoveHeight), 0f, 1f);
            if (y <= ShipLayout.WallTop) return 5f + (y - 0.85f) / (ShipLayout.WallTop - 0.85f);
            double phi = Math.Asin(Clamp((y - ShipLayout.WallTop) / (ShipLayout.CeilingPeak - ShipLayout.WallTop), 0f, 1f));
            return 6f + (float)(phi * 180.0 / Math.PI / 6.0);
        }

        /// <summary>A place on the inside of the wall: the point, and the directions along the wall, up it and out into the room.</summary>
        static void WallFrame(float column, float y, out P3 foot, out P3 across, out P3 up, out P3 inward)
        {
            float row = RowAtHeight(y);
            foot = ShipLayout.SurfacePoint(column, row, 0f);
            inward = (foot - ShipLayout.SurfacePoint(column, row, 1f)).Unit();
            double a = column * 2.0 * Math.PI / ShipLayout.WallColumns;
            across = new P3(Math.Cos(a), 0, -Math.Sin(a));
            up = P3.Cross(inward, across).Unit();
            if (up.Y < 0f) up = -up;
        }

        /// <summary>A number from 0 to 1 that is always the same for the same two whole numbers (for scattering things by hand-free rule).</summary>
        static float Scatter(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>An enamel mug with a little coffee in it, standing at <paramref name="foot"/>, handle toward <paramref name="handle"/>.</summary>
        static void Mug(MeshBuilder b, P3 foot, string colour, P3 handle)
        {
            float[] r = { 0f, 0.032f, 0.038f, 0.038f, 0.032f, 0.03f, 0f };
            float[] h = { 0f, 0f, 0.012f, 0.082f, 0.082f, 0.066f, 0.066f };
            Revolve(b, foot, P3.Up, r, h, 10, 0.03f, row => b.Color(row >= 5 ? Coffee : colour));
            P3 side = handle.Unit();
            var loop = new List<P3>();
            for (int k = 0; k <= 5; k++)
            {
                double t = Math.PI * k / 5.0;
                loop.Add(foot + side * (0.036f + (float)Math.Sin(t) * 0.026f) + P3.Up * (0.045f + (float)Math.Cos(t) * 0.022f));
            }
            b.Color(colour);
            Pipe(b, loop, k => 0.006f, 5, false, false);
        }

        /// <summary>
        /// Everything that makes the cabin a home: a woven rug in front of the lockers, a plant, books, a lantern,
        /// a radio and a flask on the locker tops, a pin board of postcards, a rolled sleeping bag on the wall,
        /// a mug and a cactus on the desk, and glow-in-the-dark stars stuck to the ceiling. None of it stands
        /// where you can walk (see <see cref="ShipLayout.WalkRadius"/>), so nothing here needs to stop you.
        /// </summary>
        public static ShipMesh Comforts()
        {
            var b = new MeshBuilder();
            P3 right = new P3(1f, 0f, 0f), forward = new P3(0f, 0f, 1f);

            // ---- the rug: a crescent of woven bands between the lift well and the lockers
            {
                const int across = 22, bands = 6;
                const float inner = 1.0f, outer = 1.6f, y = 0.008f;
                const double from = 128.0 * Math.PI / 180.0, to = 232.0 * Math.PI / 180.0;
                string[] weave = { Rust, Wool, TealDark, TealDark, Mustard, Rust };
                for (int j = 0; j < bands; j++)
                {
                    float r0 = Lerp(inner, outer, j / (float)bands), r1 = Lerp(inner, outer, (j + 1) / (float)bands);
                    for (int i = 0; i < across; i++)
                    {
                        double a0 = from + (to - from) * i / across, a1 = from + (to - from) * (i + 1) / across;
                        // The two middle bands carry a row of diamonds: every other patch is the lighter thread.
                        bool diamond = (j == 2 || j == 3) && (i + j) % 2 == 0;
                        b.Color(diamond ? Teal : weave[j]);
                        Flat(b, new P3(Math.Sin(a0) * r0, y, Math.Cos(a0) * r0), new P3(Math.Sin(a1) * r0, y, Math.Cos(a1) * r0),
                            new P3(Math.Sin(a1) * r1, y, Math.Cos(a1) * r1), new P3(Math.Sin(a0) * r1, y, Math.Cos(a0) * r1), P3.Up);
                    }
                }
                // A tasselled fringe at each end.
                b.Color(Wool);
                for (int end = 0; end < 2; end++)
                {
                    double a = end == 0 ? from : to, step = (end == 0 ? -1.0 : 1.0) * 0.035;
                    for (int k = 0; k < 9; k++)
                    {
                        float r0 = Lerp(inner + 0.02f, outer - 0.02f, k / 9f), r1 = r0 + 0.035f;
                        Flat(b, new P3(Math.Sin(a) * r0, y, Math.Cos(a) * r0), new P3(Math.Sin(a) * r1, y, Math.Cos(a) * r1),
                            new P3(Math.Sin(a + step) * r1, y, Math.Cos(a + step) * r1), new P3(Math.Sin(a + step) * r0, y, Math.Cos(a + step) * r0), P3.Up);
                    }
                }
            }

            // ---- on top of the lockers
            float shelf = LockerTop;
            {
                // A pot plant, its leaves spilling toward the room.
                P3 pot = new P3(-0.88f, shelf, -2.0f);
                float[] r = { 0f, 0.05f, 0.074f, 0.08f, 0.066f, 0f };
                float[] h = { 0f, 0f, 0.1f, 0.1f, 0.09f, 0.09f };
                Revolve(b, pot, P3.Up, r, h, 10, 0.04f, row => b.Color(row >= 4 ? Soil : Terracotta));
                string[] greens = { LeafA, LeafB, LeafC };
                for (int k = 0; k < 11; k++)
                {
                    double turn = k * 2.399963 + 0.4;                       // the golden angle: no two leaves line up
                    float lift = k < 8 ? 0.35f + 0.6f * Scatter(k, 1) : -0.25f - 0.3f * Scatter(k, 2);
                    float length = k < 8 ? 0.1f + 0.07f * Scatter(k, 3) : 0.14f + 0.05f * Scatter(k, 4);
                    P3 flat = k < 8 ? new P3(Math.Sin(turn), 0, Math.Cos(turn)) : new P3(Math.Sin(turn) * 0.5, 0, 0.6 + 0.4 * Math.Abs(Math.Cos(turn))).Unit();
                    P3 dir = (flat + P3.Up * lift).Unit();
                    P3 from = pot + P3.Up * 0.09f + flat * (k < 8 ? 0.02f : 0.07f);
                    b.Color(greens[k % 3]);
                    Blob(b, from + dir * (length * 0.5f), new P3(0.028f, length * 0.5f, 0.009f), 6, 4, P3.Cross(dir, P3.Up), dir);
                }

                // A stack of books, none quite square to the next, and one leaning on them.
                string[] covers = { Rust, TealDark, Mustard };
                for (int k = 0; k < 3; k++)
                {
                    double skew = (Scatter(k, 7) - 0.5) * 0.5;
                    P3 along = new P3(Math.Cos(skew), 0, Math.Sin(skew));
                    b.Color(covers[k]);
                    SoftBox(b, new P3(-0.42f, shelf + 0.019f + k * 0.036f, -1.98f), new P3(0.115f - k * 0.008f, 0.017f, 0.08f - k * 0.004f), 0.22f, 8, 2, along, P3.Up);
                }
                b.Color(Wool);
                SoftBox(b, new P3(-0.255f, shelf + 0.1f, -1.99f), new P3(0.016f, 0.105f, 0.075f), 0.22f, 8, 2, new P3(0.94, 0.34, 0), new P3(-0.34, 0.94, 0));

                // A storm lantern: the cabin's night light.
                P3 lantern = new P3(0.06f, shelf, -1.98f);
                float[] br = { 0f, 0.062f, 0.062f, 0.04f, 0.036f };
                float[] bh = { 0f, 0f, 0.028f, 0.044f, 0.05f };
                b.Color(DarkMetal);
                Revolve(b, lantern, P3.Up, br, bh, 10, 0.02f, null);
                float[] gr = { 0.036f, 0.052f, 0.054f, 0.046f, 0.034f };
                float[] gh = { 0.05f, 0.075f, 0.125f, 0.16f, 0.18f };
                b.Color(LampWarm, LanternGlow);
                Revolve(b, lantern, P3.Up, gr, gh, 10, 0.11f, null);
                float[] cr = { 0.034f, 0.046f, 0.04f, 0.018f, 0f };
                float[] ch = { 0.18f, 0.186f, 0.2f, 0.225f, 0.232f };
                b.Color(DarkMetal);
                Revolve(b, lantern, P3.Up, cr, ch, 10, 0.19f, null);
                var bail = new List<P3>();
                for (int k = 0; k <= 8; k++)
                {
                    double t = Math.PI * k / 8.0;
                    bail.Add(lantern + right * ((float)Math.Cos(t) * 0.066f) + P3.Up * (0.115f + (float)Math.Sin(t) * 0.15f));
                }
                Pipe(b, bail, k => Thinnest, 5, false, false);

                // A little radio, its dial lit.
                P3 radio = new P3(0.5f, shelf + 0.062f, -1.98f);
                b.Color(Teal);
                SoftBox(b, radio, new P3(0.115f, 0.062f, 0.05f), 0.3f, 12, 4);
                b.Color(DarkMetal);
                Pad(b, radio + new P3(-0.04f, 0f, 0.0495f), right, P3.Up, forward, 0.05f, 0.04f, 0.004f, 0.008f, 1, 1);
                b.Color(Wool);
                Pad(b, radio + new P3(0.058f, 0.022f, 0.0495f), right, P3.Up, forward, 0.034f, 0.012f, 0.003f, 0.004f);
                b.Color(Orange);
                Blob(b, radio + new P3(0.058f, -0.02f, 0.054f), new P3(0.016f, 0.016f, 0.01f), 8, 4);
                b.Color("#7CFFB0", 1.2);
                Blob(b, radio + new P3(0.092f, 0.024f, 0.052f), new P3(0.006f, 0.006f, 0.004f), 6, 3);
                b.Color(Silver);
                Rod(b, radio + new P3(0.09f, 0.058f, -0.02f), radio + new P3(0.16f, 0.3f, -0.05f), Thinnest, Thinnest, 5, false, false);
                Blob(b, radio + new P3(0.16f, 0.3f, -0.05f), new P3(0.008f, 0.008f, 0.008f), 6, 3);

                // A flask and yesterday's mug.
                P3 flask = new P3(0.9f, shelf, -2.0f);
                float[] fr = { 0f, 0.04f, 0.04f, 0.04f, 0.031f, 0.031f, 0f };
                float[] fh = { 0f, 0f, 0.09f, 0.18f, 0.195f, 0.24f, 0.246f };
                Revolve(b, flask, P3.Up, fr, fh, 10, 0.12f, row => b.Color(row >= 4 ? Silver : Rust));
                Mug(b, new P3(1.02f, shelf, -1.93f), Wool, new P3(1f, 0f, 0.6f));

                // A stretchy cord along the front of the shelf, so none of it goes flying when the ship does.
                const int posts = 10;
                var cord = new List<P3>();
                for (int k = 0; k <= posts * 2; k++)
                {
                    float x = Lerp(-LockerHalfWidth + 0.06f, LockerHalfWidth - 0.06f, k / (float)(posts * 2));
                    cord.Add(new P3(x, shelf + 0.062f - (k % 2 == 1 ? 0.006f : 0f), -1.815f));
                }
                b.Color(Rust);
                Pipe(b, cord, k => 0.006f, 5, false, false);
                b.Color(DarkMetal);
                for (int k = 0; k <= posts; k += 2)
                {
                    float x = Lerp(-LockerHalfWidth + 0.06f, LockerHalfWidth - 0.06f, k / (float)posts);
                    Rod(b, new P3(x, shelf - 0.004f, -1.815f), new P3(x, shelf + 0.072f, -1.815f), 0.008f, 0.008f, 6, false, true);
                }
            }

            // ---- a pin board on the wall behind the pilot's right shoulder, under a wall lamp
            {
                WallFrame(21.5f, 0.97f, out P3 foot, out P3 across, out P3 up, out P3 inward);
                const float halfWidth = 0.3f, halfHeight = 0.2f;
                // The wall curves and the board does not: stand its middle off so its edges just meet the wall.
                P3 centre = foot + inward * 0.022f;
                b.Color("#6B4A33");
                Pad(b, centre, across, up, inward, halfWidth + 0.02f, halfHeight + 0.02f, 0.008f, 0.012f, 3, 2);
                b.Color(Cork);
                Pad(b, centre + inward * 0.008f, across, up, inward, halfWidth, halfHeight, 0.006f, 0.01f, 3, 2);
                // x, y, half width, half height, tilt (degrees), card, picture
                float[][] cards =
                {
                    new[] { -0.19f, 0.07f, 0.07f, 0.085f, -6f }, new[] { -0.03f, 0.09f, 0.075f, 0.055f, 4f },
                    new[] { 0.16f, 0.06f, 0.06f, 0.075f, 9f }, new[] { -0.13f, -0.1f, 0.05f, 0.05f, 5f },
                    new[] { 0.03f, -0.08f, 0.085f, 0.06f, -3f }, new[] { 0.2f, -0.11f, 0.045f, 0.045f, -8f },
                };
                string[] card = { Wool, Wool, Wool, Mustard, Wool, "#E8A0A0" };
                string[] picture = { TealDark, Rust, "#22324A", null, "#D98E4A", null };
                string[] pin = { Red, Blue, Yellow, Red, Yellow, Blue };
                for (int k = 0; k < cards.Length; k++)
                {
                    double tilt = cards[k][4] * Math.PI / 180.0;
                    P3 cRight = across * (float)Math.Cos(tilt) + up * (float)Math.Sin(tilt);
                    P3 cUp = up * (float)Math.Cos(tilt) - across * (float)Math.Sin(tilt);
                    P3 at = centre + across * cards[k][0] + up * cards[k][1] + inward * (0.0145f + k * 0.0006f);
                    b.Color(card[k]);
                    Pad(b, at, cRight, cUp, inward, cards[k][2], cards[k][3], 0.002f, 0.003f);
                    if (picture[k] != null)
                    {
                        b.Color(picture[k]);
                        Pad(b, at + inward * 0.0022f + cUp * (cards[k][3] * 0.12f), cRight, cUp, inward, cards[k][2] * 0.82f, cards[k][3] * 0.7f, 0.001f, 0.002f);
                    }
                    b.Color(pin[k]);
                    Blob(b, at + cUp * (cards[k][3] * 0.82f) + inward * 0.008f, new P3(0.008f, 0.008f, 0.008f), 6, 3);
                }
            }

            // ---- a rolled sleeping bag strapped to the wall behind the pilot's left shoulder
            {
                WallFrame(42.5f, 0.86f, out P3 foot, out P3 across, out P3 up, out P3 inward);
                P3 centre = foot + inward * 0.135f;
                const float half = 0.3f, fat = 0.105f;
                float[] r = { 0f, fat * 0.55f, fat * 0.9f, fat, fat, fat, fat * 0.9f, fat * 0.55f, 0f };
                float[] h = { 0f, 0.004f, 0.03f, 0.08f, half, 2f * half - 0.08f, 2f * half - 0.03f, 2f * half - 0.004f, 2f * half };
                b.Color(Teal);
                Revolve(b, centre - across * half, across, r, h, 12, half, null);
                for (int side = -1; side <= 1; side += 2)
                {
                    P3 at = centre + across * (0.16f * side);
                    float[] sr = { fat + 0.001f, fat + 0.007f, fat + 0.007f, fat + 0.001f };
                    float[] sh = { -0.022f, -0.016f, 0.016f, 0.022f };
                    b.Color(Rust);
                    Revolve(b, at, across, sr, sh, 12, 0f, null);
                    b.Color(DarkMetal);
                    Rod(b, at + up * (fat + 0.004f), at + up * (fat + 0.004f) - inward * 0.125f, 0.012f, 0.012f, 6, false, false);
                    b.Color(Silver);
                    SoftBox(b, at + inward * (fat + 0.008f), new P3(0.022f, 0.016f, 0.006f), 0.4f, 8, 2, across, up);
                }
            }

            // ---- on the desk, out at the ends where they are not in the way
            {
                Mug(b, DeskTopPoint(1.47f, 0.35f) - P3.Up * 0.004f, Orange, new P3(-1f, 0f, -0.5f));
                P3 pot = DeskTopPoint(-1.47f, 0.4f) - P3.Up * 0.004f;
                float[] r = { 0f, 0.03f, 0.04f, 0.043f, 0.034f, 0f };
                float[] h = { 0f, 0f, 0.04f, 0.04f, 0.034f, 0.034f };
                Revolve(b, pot, P3.Up, r, h, 8, 0.02f, row => b.Color(row >= 4 ? Soil : Wool));
                b.Color(LeafA);
                Blob(b, pot + new P3(0f, 0.068f, 0f), new P3(0.026f, 0.038f, 0.026f), 8, 5);
                b.Color(LeafB);
                Blob(b, pot + new P3(0.026f, 0.06f, 0.004f), new P3(0.013f, 0.018f, 0.013f), 6, 4);
                b.Color("#F08FA0");
                Blob(b, pot + new P3(-0.004f, 0.107f, 0.002f), new P3(0.01f, 0.006f, 0.01f), 6, 3);
            }

            // ---- glow-in-the-dark stars stuck to the ceiling
            for (int k = 0; k < 44; k++)
            {
                float column = Scatter(k, 11) * ShipLayout.WallColumns;
                float y = Lerp(2.08f, 2.5f, Scatter(k, 12));
                // Only over the back and sides of the room: the overhead window (columns 61 to 67) and the
                // sky seen through it and the front window are left to the real stars.
                if (column > 53f || column < 11f) continue;
                WallFrame(column, y, out P3 foot, out P3 across, out P3 up, out P3 inward);
                float size = 0.016f + 0.02f * Scatter(k, 13);
                double turn = Scatter(k, 14) * Math.PI;
                P3 a = across * (float)Math.Cos(turn) + up * (float)Math.Sin(turn), c = up * (float)Math.Cos(turn) - across * (float)Math.Sin(turn);
                P3 at = foot + inward * 0.006f;
                b.Color(StarGlow, 1.2);
                // Four points: two slim diamonds crossed.
                Flat(b, at + a * size, at + c * (size * 0.28f), at - a * size, at - c * (size * 0.28f), inward);
                Flat(b, at + c * size, at + a * (size * 0.28f) + inward * 0.0005f, at - c * size, at - a * (size * 0.28f) + inward * 0.0005f, inward);
            }
            return b.Finish();
        }

        /// <summary>Where the string of lights hangs from: hooks round the dome, leaving the front window clear.</summary>
        public const int FairyHooks = 8;
        public const float FairyHookY = 2.02f, FairySag = 0.1f;
        public static double FairyHookAngle(int hook) => (55.0 + 250.0 * hook / (FairyHooks - 1)) * Math.PI / 180.0;

        /// <summary>
        /// A string of small warm bulbs looped round the dome above the wall lamps, from one side of the front
        /// window right round to the other. Each bulb is a little lamp to the light baker, so the wall behind
        /// the string glows.
        /// </summary>
        public static ShipMesh FairyLights()
        {
            var b = new MeshBuilder();
            const int perSwag = 9, cuts = perSwag * 2;
            string[] bulbs = { BulbAmber, BulbWhite, BulbSoft, BulbAmber, BulbWhite };
            int bulb = 0;
            for (int hook = 0; hook < FairyHooks - 1; hook++)
            {
                double a0 = FairyHookAngle(hook), a1 = FairyHookAngle(hook + 1);
                Func<float, P3> at = t =>
                {
                    double a = a0 + (a1 - a0) * t;
                    float y = FairyHookY - FairySag * 4f * t * (1f - t);
                    float radius = WallRadius(y) - 0.1f;
                    return new P3(Math.Sin(a) * radius, y, Math.Cos(a) * radius);
                };
                var wire = new List<P3>();
                for (int k = 0; k <= cuts; k++) wire.Add(at(k / (float)cuts));
                b.Color("#2E2A28");
                Pipe(b, wire, k => Thinnest, 4, false, false);

                for (int k = 0; k < perSwag; k++, bulb++)
                {
                    P3 from = at((k + 0.5f) / perSwag);
                    b.Color("#2E2A28");
                    Rod(b, from, from - P3.Up * 0.02f, 0.007f, 0.009f, 4, false, false);
                    b.Color(bulbs[bulb % bulbs.Length], FairyGlow);
                    Blob(b, from - P3.Up * 0.04f, new P3(0.02f, 0.026f, 0.02f), 6, 3);
                }
            }
            // The hooks.
            b.Color(DarkMetal);
            for (int hook = 0; hook < FairyHooks; hook++)
            {
                double a = FairyHookAngle(hook);
                float radius = WallRadius(FairyHookY);
                P3 wall = new P3(Math.Sin(a) * radius, FairyHookY, Math.Cos(a) * radius);
                P3 inward = new P3(-Math.Sin(a), 0, -Math.Cos(a));
                Rod(b, wall + inward * 0.012f, wall + inward * 0.104f, 0.006f, 0.006f, 5, false, true);
            }
            return b.Finish();
        }

        // ================================================================ hardware

        const string Wood = "#7A5238", BrassTrim = "#B08A45", PanelDark = "#2B3138", Copper = "#B5673A";

        /// <summary>Wall columns the roof ribs run up (kept clear of the windows, the wall lamps and the bare wall saved for paintings).</summary>
        public static readonly float[] RibColumns = { 12.4f, 20f, 28f, 36f, 44f, 51.6f };
        /// <summary>How far the ribs stand off the dome, and how low the hanging orrery comes.</summary>
        /// <summary>
        /// The radius of the thinnest rod or wire allowed in the cabin: 12 mm across. Anything finer is under two
        /// pixels wide on the Deck's screen from across the room and crawls as you move.
        /// </summary>
        public const float Thinnest = 0.006f;
        public const float RibDepth = 0.035f, OrreryPlane = 2.14f;
        /// <summary>Where on the wall the air bottles and the spare suit hang (wall columns), either side of the lockers.</summary>
        public const float AirRackColumn = 25f, SuitStandColumn = 39f;

        /// <summary>
        /// The working parts of a spacecraft, so the cabin is somewhere that flies as well as somewhere to live:
        /// wooden ribs up the dome to a brass crown, a little orrery hanging from it, a switch panel either side
        /// of the roof window, a rack of air bottles with their gauge, the spare suit pack, helmet and tether,
        /// a pipe with a valve wheel and a fire extinguisher. All of it hangs on the wall or the roof: nothing
        /// stands where you walk, so nothing here needs to stop you. Its glows are all under
        /// <see cref="LightBake.CastsLightFrom"/>: they show lit and light nothing.
        /// </summary>
        public static ShipMesh Hardware()
        {
            var b = new MeshBuilder();
            Ribs(b);
            Orrery(b);
            OverheadPanels(b);
            AirRack(b);
            SuitStand(b);
            ValvePipe(b);
            Extinguisher(b);
            return b.Finish();
        }

        /// <summary>A point on the inside of the wall or dome at a height, stood off into the room and moved along the wall.</summary>
        static P3 OnWall(float column, float y, float off, float aside = 0f)
        {
            if (y < ShipLayout.CoveHeight)
            {
                // The cove at the foot of the wall curves in to the floor.
                double a = column * 2.0 * Math.PI / ShipLayout.WallColumns;
                float r = WallRadius(y) - off;
                return new P3(Math.Sin(a) * r, y, Math.Cos(a) * r) + new P3(Math.Cos(a), 0, -Math.Sin(a)) * aside;
            }
            WallFrame(column, y, out P3 foot, out P3 across, out _, out P3 inward);
            return foot + inward * off + across * aside;
        }

        /// <summary>Height on the dome at an angle up from the top of the wall (degrees).</summary>
        static float DomeHeight(double degrees) =>
            ShipLayout.WallTop + (ShipLayout.CeilingPeak - ShipLayout.WallTop) * (float)Math.Sin(degrees * Math.PI / 180.0);

        /// <summary>A ring of round bar about an axis.</summary>
        static void Hoop(MeshBuilder b, P3 centre, P3 axis, float major, float tube, int around, int sides)
        {
            P3 a = axis.Unit();
            Basis(a, out P3 e1, out P3 e2);
            Skin(b, around, sides, (i, j) =>
            {
                double t = i * 2.0 * Math.PI / around, s = j * 2.0 * Math.PI / sides;
                P3 spoke = e1 * (float)Math.Cos(t) + e2 * (float)Math.Sin(t);
                return centre + spoke * (major + tube * (float)Math.Cos(s)) + a * (tube * (float)Math.Sin(s));
            }, (c, i, j) =>
            {
                P3 flat = c - centre;
                flat = flat - a * P3.Dot(flat, a);
                return c - (centre + flat.Unit() * major);
            }, wrapCols: true, wrapRows: true);
        }

        /// <summary>A straight round bar cut into short lengths, so the baked light has corners to live on.</summary>
        static void LongRod(MeshBuilder b, P3 from, P3 to, float radiusFrom, float radiusTo, int sides, bool capStart, bool capEnd)
        {
            int cuts = Math.Max(1, (int)Math.Ceiling(P3.Distance(from, to) / 0.2f));
            var path = new List<P3>();
            for (int k = 0; k <= cuts; k++) path.Add(P3.Lerp(from, to, k / (float)cuts));
            Pipe(b, path, k => Lerp(radiusFrom, radiusTo, k / (float)cuts), sides, capStart, capEnd);
        }

        /// <summary>A small round light or button lying on a surface.</summary>
        static void Stud(MeshBuilder b, P3 at, P3 across, P3 up, float size)
        {
            Blob(b, at, new P3(size, size, size * 0.65f), 6, 3, across, up);
        }

        /// <summary>
        /// A round dial lying on a surface: brass rim, cream face, a dark needle at <paramref name="turn"/> degrees.
        /// Scenery: the needle is dark, never red, so it is not taken for one of the console's real warnings.
        /// </summary>
        static void Dial(MeshBuilder b, P3 at, P3 across, P3 up, P3 normal, float radius, double turn)
        {
            b.Color(BrassTrim);
            Revolve(b, at, normal, new[] { radius + 0.007f, radius + 0.007f, radius }, new[] { 0f, 0.012f, 0.012f }, 12, -0.1f, null);
            b.Color(CreamLight);
            Revolve(b, at, normal, new[] { radius, 0f }, new[] { 0.008f, 0.008f }, 12, -0.1f, null);
            double t = turn * Math.PI / 180.0;
            P3 along = across * (float)Math.Sin(t) + up * (float)Math.Cos(t), side = across * (float)Math.Cos(t) - up * (float)Math.Sin(t);
            P3 hub = at + normal * 0.0095f;
            b.Color(PanelDark);
            FlatTri(b, hub + side * (radius * 0.12f) - along * (radius * 0.2f), hub - side * (radius * 0.12f) - along * (radius * 0.2f), hub + along * (radius * 0.86f), normal);
            b.Color(BrassTrim);
            Blob(b, hub, new P3(radius * 0.16f, radius * 0.16f, 0.004f), 6, 2, across, up);
        }

        /// <summary>Six wooden ribs from the top of the wall to a brass crown ring round the ceiling cap.</summary>
        static void Ribs(MeshBuilder b)
        {
            const int steps = 14;
            const float half = 0.035f;
            const double crown = 77.6;
            float[] u = { -1f, -0.7f, 0f, 0.7f, 1f };
            float[] d = { 0f, 0.82f, 1f, 0.82f, 0f };
            P3 heart = new P3(0f, 1.1f, 0f);
            foreach (float column in RibColumns)
            {
                // Behind the lockers the wall is hidden up to their top, so those two start there.
                bool overLockers = column > 27f && column < 37f;
                float from = overLockers ? LockerTop + 0.03f : ShipLayout.WallTop + 0.002f;
                double start = Math.Asin((from - ShipLayout.WallTop) / (ShipLayout.CeilingPeak - ShipLayout.WallTop)) * 180.0 / Math.PI;
                float col = column;
                b.Color(Wood);
                Skin(b, u.Length, steps + 1, (i, j) =>
                {
                    float y = DomeHeight(start + (crown - start) * j / steps);
                    WallFrame(col, y, out P3 foot, out P3 across, out _, out P3 inward);
                    return foot + across * (u[i] * half) + inward * (d[i] * RibDepth);
                }, Toward(heart));

                // A brass shoe where it leaves the wall.
                WallFrame(col, from + 0.04f, out P3 f, out P3 a, out P3 up, out P3 n);
                b.Color(BrassTrim);
                SoftBox(b, f + n * 0.03f, new P3(0.05f, 0.05f, 0.027f), 0.5f, 8, 4, a, up);
            }
            b.Color(BrassTrim);
            Hoop(b, new P3(0f, DomeHeight(crown) - 0.03f, 0f), P3.Up, 0.5f, 0.028f, 32, 6);
        }

        /// <summary>
        /// A little clockwork solar system hanging from the crown, over the lift well: a glowing sun and six
        /// worlds on brass arms. It is well above your head.
        /// </summary>
        static void Orrery(MeshBuilder b)
        {
            float hub = OrreryPlane + 0.11f;
            b.Color(BrassTrim);
            LongRod(b, new P3(0f, ShipLayout.CeilingPeak - 0.004f, 0f), new P3(0f, hub - 0.02f, 0f), 0.009f, 0.009f, 6, false, true);
            Rod(b, new P3(0f, hub, 0f), new P3(0f, OrreryPlane + 0.05f, 0f), 0.005f, 0.005f, 5, false, false);
            b.Color("#FFC56A", 1.2);
            Blob(b, new P3(0f, OrreryPlane, 0f), new P3(0.055f, 0.055f, 0.055f), 10, 6);

            // reach, angle (degrees), size
            float[][] worlds =
            {
                new[] { 0.12f, 20f, 0.016f }, new[] { 0.18f, 150f, 0.022f }, new[] { 0.25f, 262f, 0.025f },
                new[] { 0.31f, 78f, 0.019f }, new[] { 0.41f, 205f, 0.044f }, new[] { 0.5f, 318f, 0.033f },
            };
            string[] paint = { "#9A948C", "#E2B56A", "#3F8FC4", Rust, "#D9B48A", "#E0C98A" };
            for (int k = 0; k < worlds.Length; k++)
            {
                double a = worlds[k][1] * Math.PI / 180.0;
                float reach = worlds[k][0], size = worlds[k][2];
                float collar = hub + 0.026f * (k + 1);
                P3 dir = new P3(Math.Sin(a), 0, Math.Cos(a));
                P3 elbow = new P3(0f, collar, 0f) + dir * reach;
                P3 world = new P3(0f, OrreryPlane, 0f) + dir * reach;
                b.Color(BrassTrim);
                Revolve(b, new P3(0f, collar, 0f), P3.Up, new[] { 0.009f, 0.016f, 0.016f, 0.009f }, new[] { -0.01f, -0.007f, 0.007f, 0.01f }, 6, 0f, null);
                LongRod(b, new P3(0f, collar, 0f), elbow, Thinnest, Thinnest, 4, false, false);
                LongRod(b, elbow, world + P3.Up * (size * 0.9f), Thinnest, Thinnest, 4, false, false);
                Blob(b, elbow, new P3(0.009f, 0.009f, 0.009f), 5, 3);

                if (k == 4)
                {
                    // The big one has bands.
                    const int rows = 9;
                    var r = new float[rows];
                    var h = new float[rows];
                    for (int j = 0; j < rows; j++)
                    {
                        double lat = -Math.PI * 0.5 + Math.PI * j / (rows - 1);
                        r[j] = j == 0 || j == rows - 1 ? 0f : size * (float)Math.Cos(lat);
                        h[j] = size * (float)Math.Sin(lat);
                    }
                    Revolve(b, world, P3.Up, r, h, 14, 0f, row => b.Color(row % 2 == 0 ? paint[k] : "#B9825A"));
                }
                else
                {
                    b.Color(paint[k]);
                    Blob(b, world, new P3(size, size, size), 8, 5);
                }
                if (k == 2)
                {
                    // A moon on a hair of wire.
                    P3 moon = world + new P3(dir.Z, 0.25f, -dir.X) * 0.056f;
                    b.Color(BrassTrim);
                    Rod(b, world, moon, Thinnest, Thinnest, 4, false, false);
                    b.Color("#CFCBC2");
                    Blob(b, moon, new P3(0.012f, 0.012f, 0.012f), 6, 3);
                }
                if (k == 5)
                {
                    // Rings, tipped over a little; one skin for each side so they show from above and below.
                    P3 tilt = new P3(0.32f, 1f, 0.12f).Unit();
                    b.Color("#C9B27A");
                    Revolve(b, world, tilt, new[] { size * 1.35f, size * 2.1f }, new[] { 0.0012f, 0.0012f }, 14, -1f, null);
                    Revolve(b, world, tilt, new[] { size * 1.35f, size * 2.1f }, new[] { -0.0012f, -0.0012f }, 14, 1f, null);
                }
            }
        }

        /// <summary>A panel of switches, dials and pilot lights on the roof either side of the overhead window.</summary>
        static void OverheadPanels(MeshBuilder b)
        {
            // Amber, green and pale blue: never red, which is kept for the console's real warnings.
            string[] lit = { "#FFB347", "#7CFFB0", "#8FD3FF" };
            string[] caps = { Silver, Red, Silver, Silver, Yellow, Silver };
            for (int side = -1; side <= 1; side += 2)
            {
                float column = side > 0 ? 5.3f : 58.7f;
                WallFrame(column, DomeHeight(54.0), out P3 foot, out P3 along, out P3 up, out P3 inward);
                P3 across = along * side;                                        // away from the roof window, on both sides
                P3 centre = foot + inward * 0.024f;
                b.Color(CreamDark);
                Pad(b, centre, across, up, inward, 0.2f, 0.16f, 0.014f, 0.02f, 2, 2);
                b.Color(PanelDark);
                Pad(b, centre + inward * 0.014f, across, up, inward, 0.178f, 0.138f, 0.008f, 0.012f, 2, 2);
                P3 face = centre + inward * 0.022f;

                Dial(b, face + across * -0.09f + up * 0.07f, across, up, inward, 0.04f, side > 0 ? 40.0 : -65.0);
                Dial(b, face + across * 0.09f + up * 0.07f, across, up, inward, 0.04f, side > 0 ? -110.0 : 25.0);

                for (int k = 0; k < caps.Length; k++)
                {
                    P3 at = face + across * (-0.125f + k * 0.05f) + up * -0.03f;
                    b.Color(CreamLight);                                         // a blank label plate above each switch
                    Pad(b, at + up * 0.03f, across, up, inward, 0.017f, 0.005f, 0.0015f, 0.002f);
                    b.Color(Black);
                    Stud(b, at, across, up, 0.012f);
                    b.Color(caps[(k + (side > 0 ? 0 : 3)) % caps.Length]);
                    bool thrown = Scatter(k, 40 + side) < 0.5f;
                    Rod(b, at, at + inward * 0.03f + up * (thrown ? 0.014f : -0.014f), 0.005f, 0.008f, 6, false, true);
                }
                for (int k = 0; k < 6; k++)
                {
                    P3 at = face + across * (-0.12f + k * 0.048f) + up * -0.095f;
                    if (Scatter(k, 70 + side) < 0.6f) b.Color(lit[(k + (side > 0 ? 0 : 1)) % lit.Length], 1.1);
                    else b.Color(DarkMetal);
                    Stud(b, at, across, up, 0.011f);
                }
            }
        }

        /// <summary>Three air bottles strapped in a rack on the wall beside the lockers, piped up to a gauge.</summary>
        static void AirRack(MeshBuilder b)
        {
            string[] paint = { Cream, Teal, Mustard };
            string[] band = { Orange, Wool, Rust };
            const float shelfY = 0.52f, off = 0.105f, fat = 0.074f;
            var joins = new P3[3];
            for (int k = 0; k < 3; k++)
            {
                float column = AirRackColumn - 1f + k;
                WallFrame(column, 0.8f, out P3 foot, out _, out _, out P3 inward);
                P3 stand = AtHeight(foot + inward * off, shelfY);
                float[] r = { 0f, 0.058f, fat, fat, fat, fat, 0.062f, 0.034f, 0.026f, 0.026f, 0f };
                float[] h = { 0f, 0f, 0.022f, 0.18f, 0.34f, 0.5f, 0.56f, 0.6f, 0.612f, 0.64f, 0.64f };
                b.Color(paint[k]);
                Revolve(b, stand, P3.Up, r, h, 12, 0.3f, null);
                b.Color(band[k]);
                Revolve(b, stand, P3.Up, new[] { fat + 0.0005f, fat + 0.0035f, fat + 0.0035f, fat + 0.0005f }, new[] { 0.33f, 0.336f, 0.394f, 0.4f }, 12, 0.365f, null);

                // The valve: a brass block, a little hand wheel, and a pipe back to the wall.
                P3 top = stand + P3.Up * 0.64f;
                b.Color(BrassTrim);
                Rod(b, top, top + P3.Up * 0.05f, 0.012f, 0.012f, 6, false, false);
                SoftBox(b, top + P3.Up * 0.028f, new P3(0.022f, 0.016f, 0.022f), 0.5f, 8, 4);
                b.Color(k == 1 ? Red : Silver);
                Revolve(b, top + P3.Up * 0.05f, P3.Up, new[] { 0f, 0.03f, 0.03f, 0f }, new[] { 0f, 0f, 0.009f, 0.009f }, 8, 0.0045f, null);
                joins[k] = top + P3.Up * 0.028f - inward * 0.062f;
                b.Color(Copper);
                Rod(b, top + P3.Up * 0.028f, joins[k], 0.008f, 0.008f, 5, false, false);
            }

            // The pipe that joins them, and its riser to the gauge.
            WallFrame(AirRackColumn, 1.42f, out P3 gf, out P3 ga, out P3 gu, out P3 gn);
            b.Color(Copper);
            Pipe(b, new[] { joins[0], joins[1], joins[2] }, k => 0.01f, 6, true, true);
            Rod(b, joins[1], gf + gn * 0.03f - gu * 0.05f, 0.01f, 0.01f, 6, false, false);
            b.Color(CreamDark);
            SoftBox(b, gf + gn * 0.022f, new P3(0.085f, 0.06f, 0.022f), 0.4f, 10, 4, ga, gu);
            Dial(b, gf + gn * 0.043f - ga * 0.03f, ga, gu, gn, 0.036f, 55.0);
            b.Color("#7CFFB0", 1.0);
            Stud(b, gf + gn * 0.044f + ga * 0.05f + gu * 0.022f, ga, gu, 0.011f);
            b.Color(DarkMetal);
            Stud(b, gf + gn * 0.044f + ga * 0.05f - gu * 0.018f, ga, gu, 0.011f);

            // The shelf they stand on, with a lip, on two struts.
            const int cuts = 8;
            const float c0 = AirRackColumn - 1.5f, c1 = AirRackColumn + 1.5f, deep = 0.2f, thick = 0.03f;
            b.Color(DarkMetal);
            for (int i = 0; i < cuts; i++)
            {
                float ca = Lerp(c0, c1, i / (float)cuts), cb = Lerp(c0, c1, (i + 1) / (float)cuts);
                P3 wa = AtHeight(OnWall(ca, 0.6f, 0f), shelfY), wb = AtHeight(OnWall(cb, 0.6f, 0f), shelfY);
                P3 fa = AtHeight(OnWall(ca, 0.6f, deep), shelfY), fb = AtHeight(OnWall(cb, 0.6f, deep), shelfY);
                P3 down = P3.Up * -thick;
                Flat(b, wa, wb, fb, fa, P3.Up);
                Flat(b, wa + down, wb + down, fb + down, fa + down, -P3.Up);
                Flat(b, fa, fb, fb + down, fa + down, (fa + fb) * -1f + new P3(0f, 2f * shelfY, 0f));
                if (i == 0) Flat(b, wa, fa, fa + down, wa + down, wa - wb);
                if (i == cuts - 1) Flat(b, wb, fb, fb + down, wb + down, wb - wa);
            }
            for (int k = 0; k < 2; k++)
            {
                float column = AirRackColumn - 0.5f + k;
                Rod(b, AtHeight(OnWall(column, 0.6f, deep - 0.03f), shelfY - thick), OnWall(column, 0.3f, 0.014f), 0.012f, 0.012f, 5, false, false);
            }

            // A strap round the three of them.
            var strap = new List<P3> { OnWall(c0 + 0.08f, 0.95f, 0.012f) };
            for (int i = 0; i <= 10; i++) strap.Add(OnWall(Lerp(c0 + 0.16f, c1 - 0.16f, i / 10f), 0.95f, off + fat + 0.008f));
            strap.Add(OnWall(c1 - 0.08f, 0.95f, 0.012f));
            b.Color(Rust);
            Pipe(b, strap, k => 0.009f, 5, false, false);
            WallFrame(AirRackColumn + 0.5f, 0.95f, out P3 bf, out P3 ba, out P3 bu, out P3 bn);
            b.Color(Silver);
            SoftBox(b, bf + bn * (off + fat + 0.014f), new P3(0.022f, 0.016f, 0.007f), 0.4f, 8, 2, ba, bu);
        }

        /// <summary>
        /// The spare suit, on the wall the other side of the lockers: the jet pack on a board, a helmet with a
        /// gold visor on a shelf above it, and a coil of tether on a hook.
        /// </summary>
        static void SuitStand(MeshBuilder b)
        {
            WallFrame(SuitStandColumn, 0.85f, out P3 foot, out P3 across, out P3 up, out P3 inward);
            b.Color(Wood);
            Pad(b, foot + inward * 0.014f, across, up, inward, 0.22f, 0.33f, 0.012f, 0.016f, 3, 4);

            // The pack: a fat cream box, an orange stripe, a fuel strip, straps, and six nozzles.
            P3 pack = foot + inward * 0.098f + up * 0.01f;
            P3 half = new P3(0.15f, 0.21f, 0.068f);
            b.Color(Cream);
            SoftBox(b, pack, half, 0.42f, 12, 6, across, up);
            b.Color(Orange);
            Pad(b, pack + inward * (half.Z - 0.004f), across, up, inward, 0.034f, 0.14f, 0.007f, 0.008f, 1, 2);
            b.Color(DarkMetal);
            Pad(b, pack + inward * (half.Z - 0.006f) + across * 0.092f + up * 0.03f, across, up, inward, 0.016f, 0.075f, 0.008f, 0.004f);
            b.Color(ScreenGreen, 1.0);
            Pad(b, pack + inward * (half.Z + 0.0025f) + across * 0.092f + up * -0.005f, across, up, inward, 0.009f, 0.034f, 0.002f, 0.002f);
            for (int side = -1; side <= 1; side += 2)
            {
                // Down-pointing main jets, and smaller ones up and out to the side.
                P3 corner = pack + across * (0.085f * side);
                b.Color(DarkMetal);
                Revolve(b, corner - up * (half.Y - 0.012f), -up, new[] { 0.02f, 0.026f, 0.04f, 0.033f, 0.012f }, new[] { 0f, 0.02f, 0.062f, 0.058f, 0.02f }, 8, 0.03f, null);
                Revolve(b, corner + up * (half.Y - 0.012f), up, new[] { 0.014f, 0.018f, 0.027f, 0.021f, 0.008f }, new[] { 0f, 0.012f, 0.036f, 0.033f, 0.012f }, 8, 0.018f, null);
                Revolve(b, pack + across * ((half.X - 0.012f) * side) + up * 0.09f, across * side, new[] { 0.014f, 0.018f, 0.027f, 0.021f, 0.008f }, new[] { 0f, 0.012f, 0.036f, 0.033f, 0.012f }, 8, 0.018f, null);

                // A shoulder strap, hanging slack down the front.
                var strap = new List<P3>();
                for (int k = 0; k <= 6; k++)
                {
                    float t = k / 6f;
                    strap.Add(pack + across * (0.105f * side) + up * Lerp(0.17f, -0.17f, t) + inward * (half.Z - 0.012f + 0.036f * (float)Math.Sin(t * Math.PI)));
                }
                b.Color(Rust);
                Pipe(b, strap, k => 0.011f, 5, false, false);
            }

            // The helmet on its shelf.
            double a = SuitStandColumn * 2.0 * Math.PI / ShipLayout.WallColumns;
            P3 level = new P3(-Math.Sin(a), 0, -Math.Cos(a));                    // straight into the room
            P3 wall = OnWall(SuitStandColumn, 1.3f, 0f);
            b.Color(Wood);
            SoftBox(b, wall + level * 0.135f, new P3(0.155f, 0.012f, 0.125f), 0.3f, 10, 2, across, P3.Up);
            b.Color(BrassTrim);
            for (int side = -1; side <= 1; side += 2)
                Rod(b, wall + level * 0.2f + across * (0.12f * side) - P3.Up * 0.01f, wall + level * 0.012f + across * (0.12f * side) - P3.Up * 0.11f, 0.008f, 0.008f, 5, false, false);
            P3 head = wall + level * 0.165f + P3.Up * 0.14f;
            b.Color(Silver);
            Revolve(b, wall + level * 0.165f + P3.Up * 0.012f, P3.Up, new[] { 0.078f, 0.092f, 0.092f, 0.078f }, new[] { 0f, 0.004f, 0.03f, 0.036f }, 12, 0.018f, null);
            b.Color(CreamLight);
            Blob(b, head, new P3(0.12f, 0.12f, 0.125f), 14, 8, across, P3.Up);
            // The visor stands well proud of the shell, so its edge is a clean oval however coarsely both are cut.
            b.Color("#C9973A");
            Blob(b, head + level * 0.07f + P3.Up * 0.006f, new P3(0.086f, 0.066f, 0.076f), 14, 8, across, P3.Up);
            b.Color(Orange);
            for (int side = -1; side <= 1; side += 2)
                Blob(b, head + across * (0.118f * side) - P3.Up * 0.01f, new P3(0.012f, 0.028f, 0.028f), 8, 4, across, P3.Up);

            // The tether: a coil of orange line on a hook, with its clip.
            WallFrame(SuitStandColumn + 1.45f, 1.02f, out P3 hf, out _, out P3 hu, out P3 hn);
            b.Color(BrassTrim);
            Rod(b, hf + hn * 0.009f, hf + hn * 0.075f + hu * 0.012f, 0.008f, 0.008f, 5, false, true);
            b.Color(Orange);
            for (int k = 0; k < 3; k++)
                Hoop(b, hf + hn * (0.024f + 0.019f * k) - hu * (0.076f + 0.006f * (k % 2)), hn, 0.082f - 0.004f * k, 0.011f, 16, 5);
            b.Color(Silver);
            Hoop(b, hf + hn * 0.045f - hu * 0.185f, hn, 0.022f, 0.005f, 8, 4);
        }

        /// <summary>A copper pipe and a cable up the wall beside one rib and on over the dome to the crown, with a red valve wheel.</summary>
        static void ValvePipe(MeshBuilder b)
        {
            const float column = 20f, aside = -0.075f;
            var pipe = new List<P3> { OnWall(column, 0.16f, 0.02f, aside) };
            var cable = new List<P3> { OnWall(column, 0.2f, 0.01f, aside - 0.045f) };
            foreach (float y in new[] { 0.26f, 0.38f, 0.5f, 0.68f, 0.86f, 1.03f, 1.2f })
            {
                pipe.Add(OnWall(column, y, 0.03f, aside));
                cable.Add(OnWall(column, y, 0.012f, aside - 0.045f));
            }
            for (int k = 1; k <= 14; k++)
            {
                float y = DomeHeight(77.4 * k / 14.0);
                pipe.Add(OnWall(column, y, 0.03f, aside));
                // The cable leaves off at a junction box part way up.
                if (k <= 4) cable.Add(OnWall(column, y, 0.012f, aside - 0.045f));
            }
            b.Color(Copper);
            Pipe(b, pipe, k => 0.016f, 6, false, false);
            b.Color("#2E2A28");
            Pipe(b, cable, k => 0.007f, 4, false, false);
            WallFrame(column, DomeHeight(77.4 * 4 / 14.0), out P3 jf, out P3 ja, out P3 ju, out P3 jn);
            b.Color(CreamDark);
            SoftBox(b, jf + ja * (aside - 0.045f) + jn * 0.018f + ju * 0.03f, new P3(0.035f, 0.045f, 0.018f), 0.4f, 8, 4, ja, ju);
            b.Color("#FFB347", 1.1);
            Stud(b, jf + ja * (aside - 0.045f) + jn * 0.037f + ju * 0.045f, ja, ju, 0.009f);

            // Brass clips holding the pipe to the wall.
            b.Color(BrassTrim);
            foreach (float y in new[] { 0.6f, 1.14f, DomeHeight(28.0), DomeHeight(52.0) })
            {
                WallFrame(column, y, out P3 f, out P3 a, out P3 up, out P3 n);
                SoftBox(b, f + a * aside + n * 0.027f, new P3(0.03f, 0.014f, 0.025f), 0.5f, 8, 4, a, up);
            }

            // The valve: a brass body in the line and a red wheel on a stem.
            WallFrame(column, 0.94f, out P3 vf, out P3 va, out P3 vu, out P3 vn);
            P3 body = vf + va * aside + vn * 0.03f;
            b.Color(BrassTrim);
            SoftBox(b, body, new P3(0.028f, 0.04f, 0.028f), 0.6f, 8, 4, va, vu);
            Rod(b, body, body + vn * 0.066f, 0.008f, 0.008f, 5, false, false);
            P3 wheel = body + vn * 0.062f;
            b.Color(Red);
            Hoop(b, wheel, vn, 0.05f, 0.008f, 14, 5);
            for (int k = 0; k < 4; k++)
            {
                double t = k * Math.PI * 0.5 + 0.5;
                P3 spoke = va * (float)Math.Cos(t) + vu * (float)Math.Sin(t);
                Rod(b, wheel, wheel + spoke * 0.05f, 0.005f, 0.005f, 4, false, false);
            }
            Blob(b, wheel, new P3(0.012f, 0.012f, 0.008f), 6, 3, va, vu);
        }

        /// <summary>A fire extinguisher clipped to the wall beside the left porthole.</summary>
        static void Extinguisher(MeshBuilder b)
        {
            WallFrame(44.35f, 0.75f, out P3 foot, out P3 across, out _, out P3 inward);
            P3 stand = AtHeight(foot + inward * 0.068f, 0.56f);
            float[] r = { 0f, 0.044f, 0.052f, 0.052f, 0.052f, 0.044f, 0.02f, 0.02f, 0f };
            float[] h = { 0f, 0f, 0.016f, 0.16f, 0.3f, 0.34f, 0.36f, 0.385f, 0.385f };
            b.Color(Red);
            Revolve(b, stand, P3.Up, r, h, 10, 0.17f, null);
            b.Color(Wool);
            Revolve(b, stand, P3.Up, new[] { 0.0525f, 0.0545f, 0.0545f, 0.0525f }, new[] { 0.14f, 0.144f, 0.226f, 0.23f }, 10, 0.185f, null);
            P3 top = stand + P3.Up * 0.385f;
            b.Color(Black);
            SoftBox(b, top + P3.Up * 0.012f, new P3(0.022f, 0.012f, 0.022f), 0.5f, 8, 4);
            Rod(b, top + P3.Up * 0.02f, top + P3.Up * 0.034f + across * 0.07f, 0.007f, 0.006f, 5, false, true);
            var hose = new List<P3>();
            for (int k = 0; k <= 6; k++)
            {
                float t = k / 6f;
                hose.Add(top + P3.Up * (0.012f - 0.2f * t + 0.05f * (float)Math.Sin(t * Math.PI)) - across * (0.02f + 0.045f * (float)Math.Sin(t * Math.PI * 0.6)) + inward * (0.012f * t));
            }
            Pipe(b, hose, k => k >= 5 ? 0.01f : 0.006f, 5, false, true);
            // The clip that holds it.
            b.Color(Silver);
            Hoop(b, stand + P3.Up * 0.27f, P3.Up, 0.054f, 0.006f, 12, 4);
            Rod(b, stand + P3.Up * 0.27f - inward * 0.05f, AtHeight(foot + inward * 0.01f, 0.56f + 0.27f), 0.008f, 0.008f, 4, false, false);
            Pad(b, AtHeight(foot + inward * 0.002f, 0.6f), across, P3.Up, inward, 0.03f, 0.012f, 0.012f, 0.004f);
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
