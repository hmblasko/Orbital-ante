using System;
using System.Collections.Generic;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// Builds the cabin itself: the padded inside of the bowl, the hull outside it, the window frames that join
    /// the two, the glass, and the lift shaft. Everything follows <see cref="ShipLayout"/>'s wall grid, so the
    /// inside and the outside always line up and the windows are real holes you can see through.
    /// No Unity types, so it can be built and checked anywhere, and on a background thread.
    /// </summary>
    public static class ShipShell
    {
        /// <summary>A point well inside the cabin: inside faces look toward it, outside faces away from it.</summary>
        static readonly P3 Heart = new P3(0f, 1.0f, 0f);

        const string Cream = "#E6D9BF";
        const string CreamDark = "#D6C7A8";
        const string Kick = "#7A746B";
        const string Ceiling = "#EFE8DA";
        const string Accent = "#D9772B";
        const string Frame = "#3F4B54";
        const string HullWhite = "#D8D5CC";
        const string HullGrey = "#9A9EA6";
        const string HullDark = "#2A2C31";
        const string HullGold = "#C8962E";

        // ------------------------------------------------------------------ inside

        /// <summary>The inside of the cabin: wall, ceiling, window frames and the lift shaft. Light is baked onto this.</summary>
        public static ShipMesh Interior()
        {
            var b = new MeshBuilder();
            PanelledSheet(b, ShipLayout.InnerPoint, InteriorPanel, InteriorColour, away: false);
            Cap(b, inside: true);

            for (int i = 0; i < ShipLayout.Windows.Length; i++)
            {
                WindowSpec w = ShipLayout.Windows[i];
                List<P3> edge = PanelEdge(w, 0f);
                List<P3> glassIn = GlassLoop(w, 0f);
                List<P3> glassOut = GlassLoop(w, 1f);
                b.Color(Frame);
                BandSeenFrom(b, edge, glassIn, Heart, away: false);
                // The "reveal": the thickness of the hull seen round the glass.
                ShipLayout.WindowCentre(w, out P3 centre, out _);
                BandSeenFrom(b, glassIn, glassOut, centre, away: false);
            }

            Shaft(b);
            return b.Finish();
        }

        static int InteriorPanel(int column, int row)
        {
            if (ShipLayout.InWindow(column, row)) return -1;
            if (row < 4) return 0;                                  // the kick band where wall meets floor
            if (row == 4) return 1;                                 // a thin accent stripe round the room
            if (row < 12) return 100 + column / 4;                  // upright padded panels
            return 200 + column / 8;                                // ceiling gores
        }

        static P3 InteriorColour(int panel)
        {
            if (panel == 0) return MeshBuilder.Web(Kick);
            if (panel == 1) return MeshBuilder.Web(Accent);
            if (panel < 200) return MeshBuilder.Web(panel % 2 == 0 ? Cream : CreamDark);
            return MeshBuilder.Web(Ceiling);
        }

        static void Shaft(MeshBuilder b)
        {
            const int sides = 32;
            const int rows = 8;
            float top = -0.06f, bottom = ShipLayout.BellyY;
            int first = b.VertexCount;
            for (int j = 0; j <= rows; j++)
            {
                for (int i = 0; i < sides; i++)
                {
                    // Four upright strips of blue light run the height of the shaft.
                    bool strip = i % 8 == 0;
                    if (strip) b.Color("#39B8FF", 2.2);
                    else b.Color("#2B3138");
                    double a = i * 2.0 * Math.PI / sides;
                    float y = bottom + (top - bottom) * j / rows;
                    b.Add(new P3(Math.Sin(a) * ShipLayout.ShaftRadius, y, Math.Cos(a) * ShipLayout.ShaftRadius));
                }
            }
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int i1 = (i + 1) % sides;
                    int a0 = first + j * sides + i, b0 = first + j * sides + i1;
                    P3 axis = new P3(0f, b.PointAt(a0).Y, 0f);
                    b.QuadSeenFrom(a0, b0, b0 + sides, a0 + sides, axis);
                }
            }
        }

        // ------------------------------------------------------------------ outside

        /// <summary>The outside of the cabin. Lit by the Sun at run time, not baked.</summary>
        public static ShipMesh Hull()
        {
            var b = new MeshBuilder();
            PanelledSheet(b, ShipLayout.OuterPoint, HullPanel, HullColour, away: true);
            Cap(b, inside: false);

            // A skirt tucking the bottom of the bowl down into the top of the base, so no gap shows.
            b.Color(HullDark);
            int first = b.VertexCount;
            ShipLayout.OuterProfile(0, out float r0, out float h0);
            for (int j = 0; j < 2; j++)
            {
                for (int i = 0; i < ShipLayout.WallColumns; i++)
                {
                    double a = ShipLayout.ColumnAngle(i);
                    float r = j == 0 ? r0 - 0.25f : r0;
                    float h = j == 0 ? h0 - 0.22f : h0;
                    b.Add(new P3(Math.Sin(a) * r, h, Math.Cos(a) * r));
                }
            }
            for (int i = 0; i < ShipLayout.WallColumns; i++)
            {
                int i1 = (i + 1) % ShipLayout.WallColumns;
                b.QuadSeenFrom(first + i, first + i1, first + ShipLayout.WallColumns + i1, first + ShipLayout.WallColumns + i, Heart, away: true);
            }

            for (int i = 0; i < ShipLayout.Windows.Length; i++)
            {
                WindowSpec w = ShipLayout.Windows[i];
                b.Color(HullDark);
                BandSeenFrom(b, PanelEdge(w, 1f), GlassLoop(w, 1f), Heart, away: true);
            }
            return b.Finish();
        }

        static int HullPanel(int column, int row)
        {
            if (ShipLayout.InWindow(column, row)) return -1;
            if (row < 4) return 0;
            if (row < 12) return 100 + column / 4;
            return 200 + column / 8;
        }

        static P3 HullColour(int panel)
        {
            if (panel == 0) return MeshBuilder.Web(HullGrey);
            if (panel < 200)
            {
                // Mostly white, with the odd dark or gold panel, in a fixed pattern (the same ship every time).
                int k = (panel * 7 + 3) % 16;
                if (k == 0 || k == 9) return MeshBuilder.Web(HullDark);
                if (k == 5) return MeshBuilder.Web(HullGold);
                if (k % 3 == 0) return MeshBuilder.Web(HullGrey);
                return MeshBuilder.Web(HullWhite);
            }
            return MeshBuilder.Web(panel % 2 == 0 ? HullWhite : "#CFCBC0");
        }

        // ------------------------------------------------------------------ glass

        /// <summary>One window's pane, part way through the hull's thickness. Seen from both sides.</summary>
        public static ShipMesh Glass(WindowSpec w)
        {
            var b = new MeshBuilder();
            b.Color("#A8D8E8");
            // One layer: the glass material draws both of its sides. The pane is built in rings from its middle
            // out to its edge, each ring lying on the curve of the wall, so a wide window bends round with the
            // cabin and does not cut straight across the inside of it.
            var edge = new List<KeyValuePair<float, float>>();
            foreach (var uv in EdgeSteps(w))
            {
                ShipLayout.GlassPoint(w, uv.Key, uv.Value, out float u, out float v);
                edge.Add(new KeyValuePair<float, float>(u, v));
            }
            int rings = Math.Max(1, Math.Max(w.Columns, w.Rows) / 3);
            int n = edge.Count;
            int centre = b.Add(ShipLayout.SurfacePoint(w.Column + 0.5f * w.Columns, w.Row + 0.5f * w.Rows, GlassDepth));
            int before = -1;
            for (int r = 1; r <= rings; r++)
            {
                float f = r / (float)rings;
                int first = b.VertexCount;
                for (int i = 0; i < n; i++)
                {
                    float u = 0.5f + (edge[i].Key - 0.5f) * f, v = 0.5f + (edge[i].Value - 0.5f) * f;
                    b.Add(ShipLayout.SurfacePoint(w.Column + u * w.Columns, w.Row + v * w.Rows, GlassDepth));
                }
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    if (r == 1) b.Tri(centre, first + i, first + j);
                    else { b.Tri(before + i, first + i, first + j); b.Tri(before + i, first + j, before + j); }
                }
                before = first;
            }
            return b.Finish();
        }

        /// <summary>How far through the hull's thickness the glass sits (0 inside face, 1 outside face).</summary>
        public const float GlassDepth = 0.55f;

        /// <summary>
        /// How many boxes it takes to cover a window's glass for bullets and repair. A small window needs one;
        /// a wide one bends round the cabin, and a single box round it would stick out into the cabin and into
        /// space, so it gets one box for every few columns.
        /// </summary>
        public static int GlassBoxCount(WindowSpec w) => Math.Max(1, (w.Columns + 3) / 4);

        /// <summary>One of the boxes that cover a window's glass: see <see cref="GlassBoxCount"/>.</summary>
        public static void GlassBox(WindowSpec w, int index, out P3 centre, out P3 right, out P3 up, out P3 outward, out P3 half)
        {
            int count = GlassBoxCount(w);
            if (count == 1) { GlassBox(w, out centre, out right, out up, out outward, out half); return; }
            float from = index / (float)count, to = (index + 1) / (float)count;
            float midColumn = w.Column + (from + to) * 0.5f * w.Columns, midRow = w.Row + 0.5f * w.Rows;
            P3 inner = ShipLayout.SurfacePoint(midColumn, midRow, 0f), outer = ShipLayout.SurfacePoint(midColumn, midRow, 1f);
            P3 middle = P3.Lerp(inner, outer, 0.5f);
            outward = (outer - inner).Unit();
            right = P3.Cross(P3.Up, outward).Unit();
            up = P3.Cross(outward, right).Unit();
            float maxR = 0f, maxU = 0f, minO = float.MaxValue, maxO = float.MinValue;
            const int steps = 16;
            for (int i = 0; i <= steps; i++)
            {
                for (int j = 0; j <= steps; j++)
                {
                    float u = from + (to - from) * i / steps, v = j / (float)steps;
                    if (!ShipLayout.OnGlass(w, u, v)) continue;
                    P3 d = ShipLayout.SurfacePoint(w.Column + u * w.Columns, w.Row + v * w.Rows, GlassDepth) - middle;
                    maxR = Math.Max(maxR, Math.Abs(P3.Dot(d, right)));
                    maxU = Math.Max(maxU, Math.Abs(P3.Dot(d, up)));
                    float o = P3.Dot(d, outward);
                    minO = Math.Min(minO, o);
                    maxO = Math.Max(maxO, o);
                }
            }
            float innerFace = Math.Min(minO, -ShipLayout.HullThickness * 0.5f);
            float outerFace = Math.Max(maxO, ShipLayout.HullThickness * 0.5f) + 0.05f;
            centre = middle + outward * ((innerFace + outerFace) * 0.5f);
            half = new P3(maxR, maxU, (outerFace - innerFace) * 0.5f);
        }

        /// <summary>
        /// A box that just contains a window's glass, for bullets and repair to find it: its middle, its three
        /// axes (right, up, outward) and half-sizes. It stands slightly proud of the hull so a shot from
        /// outside meets the glass before the hull's own collision shape.
        /// </summary>
        public static void GlassBox(WindowSpec w, out P3 centre, out P3 right, out P3 up, out P3 outward, out P3 half)
        {
            ShipLayout.WindowCentre(w, out P3 middle, out outward);
            right = P3.Cross(P3.Up, outward).Unit();
            up = P3.Cross(outward, right).Unit();
            List<P3> loop = GlassLoop(w, 0.55f);
            float maxR = 0f, maxU = 0f, minO = float.MaxValue, maxO = float.MinValue;
            for (int i = 0; i < loop.Count; i++)
            {
                P3 d = loop[i] - middle;
                maxR = Math.Max(maxR, Math.Abs(P3.Dot(d, right)));
                maxU = Math.Max(maxU, Math.Abs(P3.Dot(d, up)));
                float o = P3.Dot(d, outward);
                minO = Math.Min(minO, o);
                maxO = Math.Max(maxO, o);
            }
            // From a little inside the inner face to a little beyond the outer face.
            float inner = Math.Min(minO, -ShipLayout.HullThickness * 0.5f);
            float outer = Math.Max(maxO, ShipLayout.HullThickness * 0.5f) + 0.05f;
            centre = middle + outward * ((inner + outer) * 0.5f);
            half = new P3(maxR, maxU, (outer - inner) * 0.5f);
        }

        /// <summary>
        /// The cracks in a window, as thin white slivers lying on the glass. They spread from where it was
        /// actually hit. <paramref name="stage"/> 1 is a small star, 2 reaches further, 3 runs to the frame.
        /// The same hit points and stage always give the same cracks.
        /// </summary>
        public static ShipMesh Cracks(WindowSpec w, IList<P3> hits, int stage)
        {
            var b = new MeshBuilder();
            b.Color("#F4FAFF");
            if (stage <= 0 || hits == null) return b.Finish();
            int rays = stage == 1 ? 5 : stage == 2 ? 8 : 12;
            float reach = stage == 1 ? 0.2f : stage == 2 ? 0.45f : 1.5f;
            for (int h = 0; h < hits.Count; h++)
            {
                ShipLayout.LocateOnWindow(w, hits[h], out float u0, out float v0);
                u0 = Math.Max(0.15f, Math.Min(0.85f, u0));
                v0 = Math.Max(0.15f, Math.Min(0.85f, v0));
                uint seed = (uint)(h * 7919 + w.Column * 104729 + 17);
                for (int k = 0; k < rays; k++)
                {
                    double angle = (k + Noise(ref seed) * 0.7) * 2.0 * Math.PI / rays;
                    float length = reach * (0.55f + 0.45f * (float)Noise(ref seed));
                    float u = u0, v = v0;
                    var line = new List<KeyValuePair<float, float>> { new KeyValuePair<float, float>(u, v) };
                    for (float walked = 0f; walked < length; walked += 0.05f)
                    {
                        angle += (Noise(ref seed) - 0.5) * 0.7;
                        float nu = u + (float)Math.Cos(angle) * 0.05f, nv = v + (float)Math.Sin(angle) * 0.05f;
                        if (!ShipLayout.OnGlass(w, nu, nv)) break;
                        u = nu; v = nv;
                        line.Add(new KeyValuePair<float, float>(u, v));
                    }
                    Sliver(b, w, line, 0.5f);
                    Sliver(b, w, line, 0.6f);
                }
            }
            return b.Finish();
        }

        static double Noise(ref uint seed)
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return (seed & 0xFFFFFF) / 16777216.0;
        }

        static void Sliver(MeshBuilder b, WindowSpec w, List<KeyValuePair<float, float>> line, float through)
        {
            if (line.Count < 2) return;
            ShipLayout.WindowCentre(w, out _, out P3 outward);
            int previous = -1;
            for (int i = 0; i < line.Count; i++)
            {
                P3 p = ShipLayout.SurfacePoint(w.Column + line[i].Key * w.Columns, w.Row + line[i].Value * w.Rows, through);
                int j = i < line.Count - 1 ? i + 1 : i - 1;
                P3 q = ShipLayout.SurfacePoint(w.Column + line[j].Key * w.Columns, w.Row + line[j].Value * w.Rows, through);
                P3 along = (i < line.Count - 1 ? q - p : p - q).Unit();
                // Widest at the hit, tapering to nothing at the tip.
                float width = 0.007f * (1f - i / (float)(line.Count - 1)) + 0.0012f;
                P3 side = P3.Cross(along, outward).Unit() * width;
                int a = b.Add(p - side), c = b.Add(p + side);
                if (previous >= 0)
                {
                    // Both ways round, so the crack shows from inside and outside.
                    b.Quad(previous, previous + 1, c, a);
                    b.Quad(previous, previous + 1, c, a, flip: true);
                }
                previous = a;
            }
        }

        // ------------------------------------------------------------------ collision shapes

        /// <summary>
        /// A simple closed shape for the cabin to collide with: the hull's outline with few corners, pulled in a
        /// little so the windows' own boxes stand proud of it.
        /// </summary>
        public static ShipMesh CabinCollision()
        {
            var b = new MeshBuilder();
            const int sides = 14;
            int[] rows = { 0, 3, 6, 9, 12, 15, 18 };
            var radii = new List<float>();
            var heights = new List<float>();
            for (int i = 0; i < rows.Length; i++)
            {
                ShipLayout.OuterProfile(rows[i], out float r, out float h);
                radii.Add(r - 0.06f);
                heights.Add(h);
            }
            radii.Add(0.05f);
            heights.Add(ShipLayout.CeilingPeak + ShipLayout.HullThickness - 0.06f);
            b.Lathe(P3.Zero, radii, heights, sides, capBottom: true);
            return b.Finish();
        }

        /// <summary>The eight-sided base as a simple closed shape to collide with.</summary>
        public static ShipMesh BaseCollision()
        {
            var b = new MeshBuilder();
            // The corners of an eight-sided shape are further out than its flat sides.
            float corner = ShipLayout.BaseInradius / (float)Math.Cos(Math.PI / 8.0);
            b.Lathe(P3.Zero, new[] { corner, corner }, new[] { ShipLayout.BellyY, ShipLayout.BaseTop }, 8, capBottom: true, capTop: true);
            return b.Finish();
        }

        // ------------------------------------------------------------------ shared pieces

        /// <summary>The grid corners round the edge of a window's panel, anticlockwise from its bottom-left corner.</summary>
        static List<P3> PanelEdge(WindowSpec w, float through)
        {
            var loop = new List<P3>();
            foreach (var uv in EdgeSteps(w)) loop.Add(PanelPoint(w, uv.Key, uv.Value, through));
            return loop;
        }

        /// <summary>The outline of the glass, one point for each point of the panel's edge.</summary>
        static List<P3> GlassLoop(WindowSpec w, float through)
        {
            var loop = new List<P3>();
            foreach (var uv in EdgeSteps(w))
            {
                ShipLayout.GlassPoint(w, uv.Key, uv.Value, out float u, out float v);
                loop.Add(ShipLayout.SurfacePoint(w.Column + u * w.Columns, w.Row + v * w.Rows, through));
            }
            return loop;
        }

        /// <summary>
        /// A point on the edge of a window's panel. The wall is made of flat pieces, one per grid square, so a
        /// point part way along a square's side is placed on the straight line between its corners: it then
        /// lies exactly on the wall's own edge and no crack can open between wall and frame.
        /// </summary>
        static P3 PanelPoint(WindowSpec w, float u, float v, float through)
        {
            float c = u * w.Columns, r = v * w.Rows;
            int c0 = Math.Min(w.Columns - 1, (int)Math.Floor(c + 1e-4f)), r0 = Math.Min(w.Rows - 1, (int)Math.Floor(r + 1e-4f));
            float fc = c - c0, fr = r - r0;
            P3 a = ShipLayout.SurfacePoint(w.Column + c0, w.Row + r0, through), b = ShipLayout.SurfacePoint(w.Column + c0 + 1, w.Row + r0, through);
            P3 d = ShipLayout.SurfacePoint(w.Column + c0, w.Row + r0 + 1, through), e = ShipLayout.SurfacePoint(w.Column + c0 + 1, w.Row + r0 + 1, through);
            return P3.Lerp(P3.Lerp(a, b, fc), P3.Lerp(d, e, fc), fr);
        }

        /// <summary>Points round the edge of a window's panel. Several to each grid square, so the glass's outline is a smooth curve and not a string of straight facets.</summary>
        const int EdgeDetail = 3;

        static IEnumerable<KeyValuePair<float, float>> EdgeSteps(WindowSpec w)
        {
            int across = w.Columns * EdgeDetail, upward = w.Rows * EdgeDetail;
            for (int i = 0; i < across; i++) yield return new KeyValuePair<float, float>(i / (float)across, 0f);
            for (int j = 0; j < upward; j++) yield return new KeyValuePair<float, float>(1f, j / (float)upward);
            for (int i = across; i > 0; i--) yield return new KeyValuePair<float, float>(i / (float)across, 1f);
            for (int j = upward; j > 0; j--) yield return new KeyValuePair<float, float>(0f, j / (float)upward);
        }

        static void BandSeenFrom(MeshBuilder b, List<P3> outer, List<P3> inner, P3 eye, bool away)
        {
            int n = outer.Count;
            int o = b.VertexCount;
            for (int i = 0; i < n; i++) b.Add(outer[i]);
            int k = b.VertexCount;
            for (int i = 0; i < n; i++) b.Add(inner[i]);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                b.QuadSeenFrom(o + i, o + j, k + j, k + i, eye, away);
            }
        }

        /// <summary>
        /// The wall grid as separate panels. Corners are shared inside a panel (so it is smoothly rounded) and
        /// doubled along the joins between panels, which gives each join a faint seam, like real padding or plating.
        /// </summary>
        static void PanelledSheet(MeshBuilder b, Func<int, int, P3> at, Func<int, int, int> panelOf, Func<int, P3> colourOf, bool away)
        {
            int columns = ShipLayout.WallColumns, rows = ShipLayout.WallRows;
            var corner = new Dictionary<long, int>();
            int Corner(int panel, int column, int row)
            {
                column %= columns;
                long key = ((long)panel << 32) | (uint)(row * columns + column);
                if (corner.TryGetValue(key, out int index)) return index;
                b.Paint = colourOf(panel);
                b.Glow = 0f;
                index = b.Add(at(column, row));
                corner[key] = index;
                return index;
            }

            for (int j = 0; j < rows - 1; j++)
            {
                for (int i = 0; i < columns; i++)
                {
                    int panel = panelOf(i, j);
                    if (panel < 0) continue;
                    b.QuadSeenFrom(Corner(panel, i, j), Corner(panel, i + 1, j), Corner(panel, i + 1, j + 1), Corner(panel, i, j + 1), Heart, away);
                }
            }
        }

        /// <summary>The round cap that closes the top of the dome.</summary>
        static void Cap(MeshBuilder b, bool inside)
        {
            b.Color(inside ? Ceiling : HullWhite);
            int last = ShipLayout.WallRows - 1;
            int first = b.VertexCount;
            for (int i = 0; i < ShipLayout.WallColumns; i++) b.Add(inside ? ShipLayout.InnerPoint(i, last) : ShipLayout.OuterPoint(i, last));
            // One ring halfway in, so the light on the ceiling has somewhere to be stored.
            int mid = b.VertexCount;
            float peak = inside ? ShipLayout.CeilingPeak : ShipLayout.CeilingPeak + ShipLayout.HullThickness;
            for (int i = 0; i < ShipLayout.WallColumns; i++)
            {
                P3 rim = b.PointAt(first + i);
                b.Add(new P3(rim.X * 0.5f, rim.Y + (peak - rim.Y) * 0.72f, rim.Z * 0.5f));
            }
            int top = b.Add(new P3(0f, peak, 0f));
            for (int i = 0; i < ShipLayout.WallColumns; i++)
            {
                int i1 = (i + 1) % ShipLayout.WallColumns;
                b.QuadSeenFrom(first + i, first + i1, mid + i1, mid + i, Heart, away: !inside);
                b.TriSeenFrom(mid + i, mid + i1, top, Heart, away: !inside);
            }
        }
    }
}
