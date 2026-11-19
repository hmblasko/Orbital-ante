using System;
using System.Collections.Generic;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Launch
{
    /// <summary>
    /// Every shape of the launch base, built in code in the pad's own space: the origin is the middle of the pad's
    /// top, +Y up, +Z north (the way the ship's nose points), +X east. Stand-ins for the NASA launch pad model,
    /// laid out the way a real pad is: a raised concrete mound with a blast dish, a service tower with a swing
    /// arm, floodlight masts, a water tower for the sound-damping deluge, and a crew hut. No flags.
    /// Plain C#; the same meshes are used for the pictures in the report.
    /// </summary>
    public static class LaunchShapes
    {
        public const float PadRadius = LaunchLayout.PadRadius;
        public const float DishRadius = LaunchLayout.DishRadius;
        public const float DishDepth = LaunchLayout.DishDepth;
        public const float ShipHeightOnPad = LaunchLayout.ShipHeightOnPad;
        public static readonly P3 TowerFoot = LaunchLayout.TowerFoot;
        public const float TowerHalf = LaunchLayout.TowerHalf;
        public const float TowerHeight = LaunchLayout.TowerHeight;
        public static readonly P3 ArmPivot = LaunchLayout.ArmPivot;
        public const float ArmLength = LaunchLayout.ArmLength;
        public static readonly P3 WaterTowerFoot = LaunchLayout.WaterTowerFoot;
        public static readonly P3 HutCentre = LaunchLayout.HutCentre;
        public static readonly P3 HutHalf = LaunchLayout.HutHalf;
        public const float MastDistance = LaunchLayout.MastDistance;
        public const float MastHeight = LaunchLayout.MastHeight;

        const string Concrete = "#B9B4A8";
        const string ConcreteDark = "#8E8A80";
        const string Scorch = "#3A3632";
        const string Steel = "#C24A2A";       // the red oxide of a real launch tower
        const string SteelDark = "#7C2F1C";
        const string Grey = "#8D9399";
        const string Yellow = "#E8B520";
        const string Black = "#1E1E20";
        const string White = "#ECE9E0";

        public static P3 Mast(int i) => LaunchLayout.Mast(i);

        // ------------------------------------------------------------------ the mound

        /// <summary>
        /// The raised concrete hardstand. <paramref name="depth"/> is how far its skirt goes down, which must be
        /// enough to reach below the lowest ground under it.
        /// </summary>
        public static ShipMesh Mound(float depth)
        {
            var b = new MeshBuilder();
            const int sides = 48;
            // The blast dish: a shallow scorched bowl under the engines, easy to walk out of.
            b.Color(Scorch);
            b.Sheet(sides, 5, (i, j) =>
            {
                double a = i * 2.0 * Math.PI / sides;
                float t = j / 4f;
                float r = DishRadius * t;
                float y = -DishDepth * (1f - t * t);
                return new P3(Math.Sin(a) * r, y, Math.Cos(a) * r);
            }, flip: true, wrap: true);
            Ring(b, DishRadius, 4.6f, "#6E6860", sides);            // heat-stained concrete
            Stripes(b, 4.6f, 5.0f, sides);                          // keep-clear line
            Ring(b, 5.0f, 12.6f, Concrete, sides);
            Stripes(b, 12.6f, 13.0f, sides);
            Ring(b, 13.0f, PadRadius, ConcreteDark, sides);
            // Scorch streaks fanning out of the dish, a hair above the concrete.
            b.Color(Scorch);
            for (int k = 0; k < 12; k++)
            {
                double a = (k * 30 + (k % 3) * 7) * Math.PI / 180.0;
                float length = 2.2f + (k * 7 % 5) * 0.55f;
                float half = 0.12f + (k % 4) * 0.035f;
                P3 dir = new P3(Math.Sin(a), 0, Math.Cos(a));
                P3 side = new P3(dir.Z, 0, -dir.X);
                P3 from = dir * (DishRadius - 0.1f) + new P3(0, 0.012f, 0);
                P3 to = dir * (DishRadius + length) + new P3(0, 0.012f, 0);
                b.PanelFacing(from - side * half * 2f, from + side * half * 2f, to + side * half * 0.7f, to - side * half * 0.7f, new P3(0, 100, 0));
            }
            // The skirt: a short kerb, then a long slope into the ground.
            float foot = PadRadius + depth * 1.15f;
            b.Color(ConcreteDark);
            // (Rows run from the bottom up: that is the order the lathe expects for a surface seen from outside.)
            b.Lathe(P3.Zero, new[] { PadRadius + 0.5f, PadRadius }, new[] { -0.5f, 0f }, sides);
            b.Color("#A39E92");
            b.Lathe(P3.Zero, new[] { foot, PadRadius + 0.5f }, new[] { -depth, -0.5f }, sides);
            return b.Finish();
        }

        /// <summary>The mound as one simple solid for standing and landing on.</summary>
        public static ShipMesh MoundCollision(float depth)
        {
            var b = new MeshBuilder();
            float foot = PadRadius + depth * 1.15f;
            b.Lathe(P3.Zero, new[] { foot, PadRadius + 0.5f, PadRadius }, new[] { -depth, -0.5f, 0f }, 24, capBottom: true, capTop: true);
            return b.Finish();
        }

        static void Ring(MeshBuilder b, float inner, float outer, string colour, int sides)
        {
            b.Color(colour);
            b.Disc(P3.Zero, outer, sides, faceUp: true, innerRadius: inner);
        }

        static void Stripes(MeshBuilder b, float inner, float outer, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                double a0 = i * 2.0 * Math.PI / sides, a1 = (i + 1) * 2.0 * Math.PI / sides;
                b.Color(i % 2 == 0 ? Yellow : Black);
                b.PanelFacing(new P3(Math.Sin(a0) * inner, 0, Math.Cos(a0) * inner), new P3(Math.Sin(a1) * inner, 0, Math.Cos(a1) * inner),
                              new P3(Math.Sin(a1) * outer, 0, Math.Cos(a1) * outer), new P3(Math.Sin(a0) * outer, 0, Math.Cos(a0) * outer), new P3(0, 100, 0));
            }
        }

        // ------------------------------------------------------------------ the service tower

        public static ShipMesh Tower()
        {
            var b = new MeshBuilder();
            P3 f = TowerFoot;
            float h = TowerHalf;
            // Footing.
            b.Color(ConcreteDark);
            b.Box(f + new P3(0, 0.15f, 0), new P3(h + 0.5f, 0.15f, h + 0.5f));
            // Four corner posts.
            b.Color(Steel);
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -h : h, sz = i < 2 ? -h : h;
                b.Box(f + new P3(sx, TowerHeight * 0.5f, sz), new P3(0.13f, TowerHeight * 0.5f, 0.13f));
            }
            // A level frame every three metres, with cross-bracing between: the lattice look of a real tower.
            int levels = 5;
            float gap = TowerHeight / levels;
            for (int level = 0; level <= levels; level++)
            {
                float y = level * gap;
                b.Color(Steel);
                if (level > 0)
                {
                    b.Box(f + new P3(0, y, -h), new P3(h, 0.08f, 0.08f));
                    b.Box(f + new P3(0, y, h), new P3(h, 0.08f, 0.08f));
                    b.Box(f + new P3(-h, y, 0), new P3(0.08f, 0.08f, h));
                    b.Box(f + new P3(h, y, 0), new P3(0.08f, 0.08f, h));
                }
                if (level == levels) break;
                b.Color(SteelDark);
                bool lean = level % 2 == 0;
                for (int side = 0; side < 4; side++)
                {
                    P3 a, c;
                    float y0 = y + 0.1f, y1 = y + gap - 0.1f;
                    if (side == 0) { a = new P3(-h, lean ? y0 : y1, -h); c = new P3(h, lean ? y1 : y0, -h); }
                    else if (side == 1) { a = new P3(-h, lean ? y0 : y1, h); c = new P3(h, lean ? y1 : y0, h); }
                    else if (side == 2) { a = new P3(-h, lean ? y0 : y1, -h); c = new P3(-h, lean ? y1 : y0, h); }
                    else { a = new P3(h, lean ? y0 : y1, -h); c = new P3(h, lean ? y1 : y0, h); }
                    b.Tube(f + a, f + c, 0.05f, 0.05f, 6);
                }
                // A grey deck on every other level.
                if (level > 0 && level % 2 == 1)
                {
                    b.Color(Grey);
                    b.Box(f + new P3(0, y + 0.1f, 0), new P3(h - 0.1f, 0.03f, h - 0.1f));
                }
            }
            // The lift cage running up the middle, and its glowing call lamp.
            b.Color("#5E6368");
            b.Box(f + new P3(0, TowerHeight * 0.5f, 0), new P3(0.45f, TowerHeight * 0.5f - 0.2f, 0.45f));
            // Cap, lightning mast and beacon.
            b.Color(White);
            b.Box(f + new P3(0, TowerHeight + 0.15f, 0), new P3(h + 0.15f, 0.15f, h + 0.15f));
            b.Color(Grey);
            b.Tube(f + new P3(0, TowerHeight + 0.3f, 0), f + new P3(0, TowerHeight + 6f, 0), 0.09f, 0.03f, 8);
            b.Color("#FF3B2A", 4);
            b.Ball(f + new P3(0, TowerHeight + 0.55f, 0), new P3(0.2f, 0.2f, 0.2f), 10, 6);
            // Pipes down the side away from the ship (propellant and water lines).
            b.Color(White);
            b.Tube(f + new P3(-h - 0.2f, 0.2f, -0.5f), f + new P3(-h - 0.2f, TowerHeight - 1f, -0.5f), 0.12f, 0.12f, 8);
            b.Color(Yellow);
            b.Tube(f + new P3(-h - 0.2f, 0.2f, 0.4f), f + new P3(-h - 0.2f, TowerHeight - 3f, 0.4f), 0.08f, 0.08f, 8);
            return b.Finish();
        }

        /// <summary>
        /// The crew arm, built pointing along +X from its hinge at the origin. It reaches the ship's side before
        /// launch and swings clear at ignition.
        /// </summary>
        public static ShipMesh Arm()
        {
            var b = new MeshBuilder();
            float len = ArmLength;
            b.Color(Grey);
            b.Box(new P3(len * 0.5f, -0.06f, 0), new P3(len * 0.5f, 0.06f, 0.5f));          // walkway
            b.Color(Steel);
            b.Box(new P3(len * 0.5f, -0.22f, -0.5f), new P3(len * 0.5f, 0.1f, 0.05f));       // side beams
            b.Box(new P3(len * 0.5f, -0.22f, 0.5f), new P3(len * 0.5f, 0.1f, 0.05f));
            b.Color(Yellow);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Tube(new P3(0.1f, 1.0f, side * 0.5f), new P3(len - 0.1f, 1.0f, side * 0.5f), 0.035f, 0.035f, 6);   // handrail
                for (int k = 0; k <= 5; k++)
                {
                    float x = 0.1f + (len - 0.2f) * k / 5f;
                    b.Tube(new P3(x, 0f, side * 0.5f), new P3(x, 1.0f, side * 0.5f), 0.025f, 0.025f, 6);
                }
            }
            // The soft collar that sits against the hull.
            b.Color(White);
            b.Box(new P3(len - 0.12f, 0.55f, 0), new P3(0.12f, 0.62f, 0.62f));
            b.Color(Black);
            b.Box(new P3(len + 0.02f, 0.55f, 0), new P3(0.03f, 0.5f, 0.5f));
            // Hinge.
            b.Color(SteelDark);
            b.Tube(new P3(0, -0.5f, 0), new P3(0, 1.2f, 0), 0.14f, 0.14f, 10);
            return b.Finish();
        }

        // ------------------------------------------------------------------ the smaller things

        /// <summary>One floodlight mast, with its lamp head turned to light the middle of the pad.</summary>
        public static ShipMesh Floodlight(int i)
        {
            var b = new MeshBuilder();
            P3 foot = Mast(i);
            P3 top = foot + new P3(0, MastHeight, 0);
            b.Color(ConcreteDark);
            b.Box(foot + new P3(0, 0.12f, 0), new P3(0.35f, 0.12f, 0.35f));
            b.Color(Grey);
            b.Tube(foot + new P3(0, 0.2f, 0), top, 0.11f, 0.07f, 8);
            // The head: a bank of three lamps on a cross-bar, tipped down toward the pad.
            P3 aim = (new P3(0, 1.5f, 0) - top).Unit();
            P3 right = P3.Cross(P3.Up, aim).Unit();
            P3 up = P3.Cross(aim, right).Unit();
            b.Color(Black);
            b.Box(top, new P3(0.95f, 0.08f, 0.08f), right, up);
            for (int k = -1; k <= 1; k++)
            {
                P3 c = top + right * (k * 0.62f) + up * 0.02f + aim * 0.1f;
                b.Color(Black);
                b.Box(c, new P3(0.26f, 0.2f, 0.14f), right, up);
                b.Color("#FFF1CF", 5);
                b.PanelFacing(c + aim * 0.145f - right * 0.21f - up * 0.15f, c + aim * 0.145f + right * 0.21f - up * 0.15f,
                              c + aim * 0.145f + right * 0.21f + up * 0.15f, c + aim * 0.145f - right * 0.21f + up * 0.15f, c + aim * 10f);
            }
            return b.Finish();
        }

        /// <summary>The water tower that feeds the sound-damping deluge under the engines.</summary>
        public static ShipMesh WaterTower()
        {
            var b = new MeshBuilder();
            P3 f = WaterTowerFoot;
            const float tankY = 7.2f, tankR = 1.9f;
            b.Color(Grey);
            for (int i = 0; i < 4; i++)
            {
                double a = (45 + 90 * i) * Math.PI / 180.0;
                P3 foot = f + new P3(Math.Sin(a) * 1.9, 0, Math.Cos(a) * 1.9);
                P3 top = f + new P3(Math.Sin(a) * 1.1, tankY - 1.0f, Math.Cos(a) * 1.1);
                b.Tube(foot, top, 0.09f, 0.07f, 8);
            }
            for (int i = 0; i < 4; i++)
            {
                double a0 = (45 + 90 * i) * Math.PI / 180.0, a1 = (135 + 90 * i) * Math.PI / 180.0;
                b.Tube(f + new P3(Math.Sin(a0) * 1.5, 3.4f, Math.Cos(a0) * 1.5), f + new P3(Math.Sin(a1) * 1.5, 3.4f, Math.Cos(a1) * 1.5), 0.04f, 0.04f, 6);
            }
            b.Color(White);
            b.Ball(f + new P3(0, tankY, 0), new P3(tankR, tankR * 0.8f, tankR), 20, 12);
            b.Color("#3E8FD0");
            b.Torus(f + new P3(0, tankY, 0), tankR * 0.995f, 0.09f, 24, 6);
            // The fat pipe down to the pad and across to the dish.
            b.Color("#3E8FD0");
            b.Tube(f + new P3(0, tankY - tankR * 0.75f, 0), f + new P3(0, 0.25f, 0), 0.2f, 0.2f, 10);
            P3 toDish = (new P3(0, 0, 0) - f).Unit();
            b.Tube(f + new P3(0, 0.25f, 0), f + toDish * 7.6f + new P3(0, 0.25f, 0), 0.2f, 0.2f, 10);
            return b.Finish();
        }

        /// <summary>The crew hut, where the player's locker lives. A round-roofed shed with a lit window and an orange door.</summary>
        public static ShipMesh Hut()
        {
            var b = new MeshBuilder();
            P3 c = HutCentre, h = HutHalf;
            b.Color(White);
            b.Box(c + new P3(0, h.Y, 0), h);
            // Barrel roof, running east to west.
            b.Color("#7A8894");
            const int arc = 10;
            b.Sheet(arc + 1, 2, (i, j) =>
            {
                double a = Math.PI * i / arc;
                float x = j == 0 ? -h.X - 0.15f : h.X + 0.15f;
                return c + new P3(x, h.Y * 2f + Math.Sin(a) * 0.75f, -Math.Cos(a) * (h.Z + 0.15f));
            }, flip: false);
            for (int end = -1; end <= 1; end += 2)
            {
                var loop = new List<P3>();
                for (int i = 0; i <= arc; i++)
                {
                    double a = Math.PI * i / arc;
                    loop.Add(c + new P3(end * (h.X + 0.15f), h.Y * 2f + Math.Sin(a) * 0.75f, -Math.Cos(a) * (h.Z + 0.15f)));
                }
                int centre = b.Add(c + new P3(end * (h.X + 0.15f), h.Y * 2f, 0));
                for (int i = 0; i < arc; i++)
                {
                    int p = b.Add(loop[i]), q = b.Add(loop[i + 1]);
                    b.TriSeenFrom(centre, p, q, c + new P3(end * 50f, h.Y * 2f, 0));
                }
            }
            // Door and window on the side facing the pad (west), a step, and a lamp over the door.
            float wall = c.X - h.X - 0.012f;
            P3 seen = new P3(-100, 1, c.Z);
            b.Color("#E0762A");
            b.PanelFacing(new P3(wall, 0.05f, c.Z - 1.1f), new P3(wall, 0.05f, c.Z - 0.2f), new P3(wall, 2.05f, c.Z - 0.2f), new P3(wall, 2.05f, c.Z - 1.1f), seen);
            b.Color("#FFE2A6", 2.2);
            b.PanelFacing(new P3(wall, 1.1f, c.Z + 0.25f), new P3(wall, 1.1f, c.Z + 1.25f), new P3(wall, 1.9f, c.Z + 1.25f), new P3(wall, 1.9f, c.Z + 0.25f), seen);
            b.Color(Black);
            b.Box(new P3(wall - 0.02f, 1.5f, c.Z + 0.75f), new P3(0.02f, 0.42f, 0.03f));
            b.Color(ConcreteDark);
            b.Box(new P3(wall - 0.35f, 0.06f, c.Z - 0.65f), new P3(0.35f, 0.06f, 0.6f));
            b.Color("#FFD9A0", 4);
            b.Ball(new P3(wall - 0.1f, 2.3f, c.Z - 0.65f), new P3(0.1f, 0.1f, 0.1f), 8, 6);
            return b.Finish();
        }

        /// <summary>Small lamps set into the pad's edge, alternating blue and amber, and four yellow chocks by the footpads.</summary>
        public static ShipMesh PadLamps()
        {
            var b = new MeshBuilder();
            const int count = 24;
            for (int i = 0; i < count; i++)
            {
                double a = (i + 0.5) * 2.0 * Math.PI / count;
                P3 p = new P3(Math.Sin(a) * 14.2, 0.05, Math.Cos(a) * 14.2);
                b.Color(Black);
                b.Box(p, new P3(0.11f, 0.05f, 0.11f));
                b.Color(i % 2 == 0 ? "#49B7FF" : "#FFB13A", 4);
                b.Ball(p + new P3(0, 0.07f, 0), new P3(0.07f, 0.05f, 0.07f), 8, 4);
            }
            b.Color(Yellow);
            for (int i = 0; i < ShipLayout.Legs; i++)
            {
                P3 pad = ShipLayout.Footpad(i);
                P3 outward = new P3(pad.X, 0, pad.Z).Unit();
                P3 side = new P3(outward.Z, 0, -outward.X);
                P3 c = outward * (ShipLayout.FootpadDistance + ShipLayout.FootpadRadius + 0.3f) + new P3(0, 0.12f, 0);
                b.Box(c, new P3(0.4f, 0.12f, 0.16f), side, P3.Up);
            }
            return b.Finish();
        }

        /// <summary>Everything that stands still, by name. The arm is separate because it moves.</summary>
        public static List<KeyValuePair<string, ShipMesh>> Fixed(float moundDepth)
        {
            var list = new List<KeyValuePair<string, ShipMesh>>
            {
                new KeyValuePair<string, ShipMesh>("Mound", Mound(moundDepth)),
                new KeyValuePair<string, ShipMesh>("Tower", Tower()),
                new KeyValuePair<string, ShipMesh>("WaterTower", WaterTower()),
                new KeyValuePair<string, ShipMesh>("Hut", Hut()),
                new KeyValuePair<string, ShipMesh>("PadLamps", PadLamps()),
            };
            for (int i = 0; i < 4; i++) list.Add(new KeyValuePair<string, ShipMesh>("Floodlight" + i, Floodlight(i)));
            return list;
        }
    }
}
