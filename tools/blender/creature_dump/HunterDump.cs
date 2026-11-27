using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

/// <summary>
/// The Cave hunter's own rules run outside Unity: scenes "walk" (a run with a turn each way, to see the body follow
/// its head), "strike" (its brain against someone standing still: the weave in, the rear, the strike, its head lying
/// still, the loop away) and "lose" (legs shot off, cut in two twice, then the burst). Every frame carries its own
/// camera, which follows the animal.
/// </summary>
static class HunterDump
{
    const double Fps = 15.0;

    sealed class Rider
    {
        public string Piece;
        public Vec3d At, Scale;
        public QuatD Turn;
    }

    sealed class Loose
    {
        public readonly List<Rider> Pieces = new List<Rider>();
        public Vec3d Position, Velocity, Axis;
        public QuatD Rotation;
        public double Spin, Rest;
        public bool Resting;
    }

    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>Game axes to Blender's: (x, y, z) -> (x, z, y); a piece's own +Z (along) is Blender's +Y, its +Y Blender's +Z.</summary>
    static void Matrix(StringBuilder json, string piece, Vec3d p, QuatD q, Vec3d scale)
    {
        Vec3d gx = q.Rotate(Vec3d.Right) * scale.X, gy = q.Rotate(Vec3d.Up) * scale.Y, gz = q.Rotate(Vec3d.Forward) * scale.Z;
        double[] m =
        {
            gx.X, gz.X, gy.X, p.X,
            gx.Z, gz.Z, gy.Z, p.Z,
            gx.Y, gz.Y, gy.Y, p.Y,
            0, 0, 0, 1,
        };
        json.Append(",{\"piece\":\"").Append(piece).Append("\",\"m\":[");
        for (int i = 0; i < 16; i++) { if (i > 0) json.Append(','); json.Append(F(m[i])); }
        json.Append("]}");
    }

    static Vec3d One(double s) => new Vec3d(s, s, s);
    static Vec3d Wide(int seg) => new Vec3d(HunterLayout.Scale(seg), HunterLayout.Scale(seg), 1.0);

    public static int Run(string path, string scene)
    {
        var rules = new HunterRules();
        var body = new HunterBody();
        var animator = new HunterAnimator();
        var brain = new HunterBrain(rules, 1);
        var pose = new HunterPose();
        var loose = new List<Loose>();
        var legStumps = new List<int>();
        var rng = new Random(5);
        bool dead = false;
        int cutAt = -1;

        Vec3d root = scene == "strike" ? new Vec3d(1.5, 0, -6.0) : new Vec3d(0, 0, -3.0);
        double yaw = 0.0;
        var player = new Vec3d(0, 0, 4.0);
        double seconds = scene == "strike" ? 7.0 : scene == "lose" ? 8.0 : 5.0;
        int frames = (int)(seconds * Fps);
        const double dt = 1.0 / 120.0;
        int perFrame = (int)(120 / Fps);
        var json = new StringBuilder();
        json.Append("{\"name\":\"").Append(scene).Append("\",\"fps\":").Append(F(Fps)).Append(",\"lens\":30,\"size\":[960,600],\"samples\":12,")
            .Append("\"eye\":[6,0,1.6],\"target\":[0,0,0.5],\"person\":[").Append(scene == "strike" ? "0,4.2" : "-2.2,3.0").Append("],\"frames\":[\n");

        // What is shot, and when: (seconds, kind, index). Kind 0 a leg, 1 a cut at that segment, 2 the kill.
        var shots = new List<(double at, int kind, int index)>();
        if (scene == "lose")
        {
            shots.Add((0.8, 0, HunterLayout.Leg(4, HunterLayout.RightSide)));
            shots.Add((1.2, 0, HunterLayout.Leg(6, HunterLayout.RightSide)));
            shots.Add((1.6, 0, HunterLayout.Leg(7, HunterLayout.RightSide)));
            shots.Add((2.4, 1, 11));
            shots.Add((4.6, 1, 6));
            shots.Add((6.4, 2, 0));
        }

        double time = 0.0;
        Vec3d look = root + new Vec3d(0, 0, -2.0);
        HunterOrders orders = default;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int step = 0; step < perFrame; step++)
            {
                time += dt;
                if (scene == "strike")
                {
                    var senses = new HunterSenses { SeesPlayer = time > 0.3, Player = QuatD.AngleAxis(Vec3d.Up, -yaw).Rotate(player - root) };
                    orders = brain.Think(body, senses, dt);
                }
                else if (scene == "lose")
                {
                    // It runs slowly past; a cut makes it thrash through its own brain.
                    var senses = new HunterSenses { Player = new Vec3d(0, 0, 50) };
                    orders = brain.Think(body, senses, dt);
                    if (orders.Mode == HunterMode.Run) orders = new HunterOrders { Velocity = new Vec3d(0, 0, time < 0.3 ? 0.0 : 2.2 * body.SpeedShare), TurnRate = 0.12 * Math.Sin(time * 1.1) };
                }
                else
                {
                    double turn = time > 1.2 && time < 2.2 ? 1.5 : time > 2.8 && time < 3.9 ? -1.6 : 0.0;
                    orders = new HunterOrders { Velocity = new Vec3d(0, 0, time < 0.3 ? 0.0 : rules.pace), TurnRate = turn };
                }
                if (dead) orders = default;
                yaw += orders.TurnRate * dt;
                QuatD turnNow = QuatD.AngleAxis(Vec3d.Up, yaw);
                root += turnNow.Rotate(orders.Velocity) * dt;

                foreach (var shot in shots.ToArray())
                {
                    if (time < shot.at) continue;
                    shots.Remove(shot);
                    Loose Fly(Vec3d at, QuatD q, Vec3d way, double rest)
                    {
                        var piece = new Loose
                        {
                            Position = root + turnNow.Rotate(at), Rotation = turnNow * q, Rest = rest,
                            Velocity = turnNow.Rotate(way) * (1.5 + rng.NextDouble() * 2.5) + Vec3d.Up * (2.5 + rng.NextDouble() * 3.0),
                            Axis = new Vec3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized, Spin = 3.0 + rng.NextDouble() * 5.0,
                        };
                        loose.Add(piece);
                        return piece;
                    }
                    void Ride(Loose on, string piece, Vec3d nodeAt, QuatD nodeTurn, Vec3d at, QuatD turn, Vec3d scale)
                    {
                        QuatD back = nodeTurn.Inverse;
                        on.Pieces.Add(new Rider { Piece = piece, At = back.Rotate(at - nodeAt), Turn = back * turn, Scale = scale });
                    }
                    void SegmentOff(int seg, bool wound)
                    {
                        int node = seg + 1;
                        Vec3d at = pose.NodePosition[node];
                        QuatD q = pose.NodeRotation[node];
                        Loose chunk = Fly(at, q, new Vec3d(rng.NextDouble() * 2 - 1, 0, -rng.NextDouble()), 0.09);
                        chunk.Pieces.Add(new Rider { Piece = HunterLayout.SegPiece, Turn = QuatD.Identity, Scale = Wide(seg) });
                        for (int side = 0; side < 2; side++)
                        {
                            int leg = HunterLayout.Leg(seg, side);
                            if (!legStumps.Contains(leg)) Ride(chunk, HunterLayout.LegPiece, at, q, pose.LegPosition[leg], pose.LegRotation[leg], One(1));
                            if (seg == HunterLayout.Segs - 1) Ride(chunk, HunterLayout.TailPiece, at, q, pose.TailPosition[side], pose.TailRotation[side], One(1));
                        }
                        if (wound)
                            chunk.Pieces.Add(new Rider { Piece = HunterLayout.Stump, At = new Vec3d(0, 0, HunterLayout.Spacing * 0.5), Turn = QuatD.Identity,
                                Scale = One(HunterLayout.SegHalfWidth * HunterLayout.Scale(seg) * 0.62 / HunterLayout.StumpRadius) });
                    }
                    if (shot.kind == 0 && body.HitLeg(shot.index, 0, sever: true).LegLost >= 0)
                    {
                        Loose leg = Fly(pose.LegPosition[shot.index], pose.LegRotation[shot.index], new Vec3d(HunterLayout.Sign(shot.index), 0, 0), 0.03);
                        leg.Pieces.Add(new Rider { Piece = HunterLayout.LegPiece, Turn = QuatD.Identity, Scale = One(1) });
                        legStumps.Add(shot.index);
                        animator.Flinch(new Vec3d(-HunterLayout.Sign(shot.index), 0.6, 0) * 1.5);
                    }
                    else if (shot.kind == 1)
                    {
                        int had = body.Kept;
                        if (body.HitSeg(shot.index, 0, sever: true).FromSeg >= 0)
                        {
                            for (int seg = shot.index; seg < had; seg++) SegmentOff(seg, seg == shot.index);
                            cutAt = shot.index;
                            if (brain.Stagger()) animator.Flinch(new Vec3d(0.6, 1.2, -1.5));
                        }
                    }
                    else if (shot.kind == 2)
                    {
                        body.HitHead(0, lethal: true);
                        dead = true;
                        for (int seg = 0; seg < body.Kept; seg++) SegmentOff(seg, false);
                        Loose head = Fly(pose.NodePosition[0], pose.NodeRotation[0], new Vec3d(0.3, 0, 1), 0.08);
                        head.Pieces.Add(new Rider { Piece = HunterLayout.HeadPiece, Turn = QuatD.Identity, Scale = One(1) });
                        for (int side = 0; side < 2; side++)
                        {
                            Ride(head, HunterLayout.FangPiece, pose.NodePosition[0], pose.NodeRotation[0], pose.FangPosition[side], pose.FangRotation[side], One(1));
                            Ride(head, HunterLayout.FeelerPiece, pose.NodePosition[0], pose.NodeRotation[0], pose.FeelerPosition[side], pose.FeelerRotation[side], One(1));
                        }
                        for (int k = 0; k < 2; k++)
                        {
                            Loose meat = Fly(pose.NodePosition[2], QuatD.Identity, new Vec3d(rng.NextDouble() - 0.5, 0, rng.NextDouble() - 0.5), 0.07);
                            meat.Pieces.Add(new Rider { Piece = HunterLayout.Meat[k], Turn = QuatD.Identity, Scale = One(1) });
                        }
                    }
                }
                if (!dead) animator.Update(body, new HunterAnimInput { Velocity = orders.Velocity, TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T }, dt, pose);

                foreach (Loose piece in loose)
                {
                    if (piece.Resting) continue;
                    piece.Velocity += Vec3d.Up * (-9.81 * dt);
                    piece.Position += piece.Velocity * dt;
                    piece.Rotation = (QuatD.AngleAxis(piece.Axis, piece.Spin * dt) * piece.Rotation).Normalized;
                    if (piece.Position.Y < piece.Rest)
                    {
                        piece.Position = new Vec3d(piece.Position.X, piece.Rest, piece.Position.Z);
                        if (Math.Abs(piece.Velocity.Y) < 1.2)
                        {
                            // Comes to rest lying flat, either way up.
                            piece.Resting = true;
                            Vec3d along = piece.Rotation.Rotate(Vec3d.Forward);
                            along = new Vec3d(along.X, 0, along.Z).Normalized;
                            piece.Rotation = WalkerAnimator.Look(along, piece.Rotation.Rotate(Vec3d.Up).Y < 0 ? -1.0 * Vec3d.Up : Vec3d.Up);
                        }
                        else
                        {
                            piece.Velocity = new Vec3d(piece.Velocity.X * 0.5, -piece.Velocity.Y * 0.35, piece.Velocity.Z * 0.5);
                            piece.Spin *= 0.5;
                        }
                    }
                }
            }

            QuatD turnRoot = QuatD.AngleAxis(Vec3d.Up, yaw);
            // The camera watches a point a third of the way down the body, from the side and a little above.
            int watch = Math.Min(pose.Nodes - 1, 5);
            Vec3d middle = root + turnRoot.Rotate(pose.NodePosition[watch]);
            if (scene == "strike") middle = (middle + player) * 0.5;
            look += (new Vec3d(middle.X, 0, middle.Z) - look) * 0.25;
            if (frame > 0) json.Append(",\n");
            double back = scene == "strike" ? 7.4 : 6.6;
            json.Append("{\"eye\":[").Append(F(look.X + back)).Append(',').Append(F(look.Z + 2.6)).Append(",2.5],\"target\":[")
                .Append(F(look.X)).Append(',').Append(F(look.Z)).Append(",0.45],\"pieces\":[{\"piece\":\"Stump\",\"m\":[0.001,0,0,0,0,0.001,0,0,0,0,0.001,-5,0,0,0,1]}");
            if (!dead)
            {
                for (int i = 0; i < pose.Nodes; i++)
                    Matrix(json, i == 0 ? HunterLayout.HeadPiece : HunterLayout.SegPiece, root + turnRoot.Rotate(pose.NodePosition[i]), turnRoot * pose.NodeRotation[i], i == 0 ? One(1) : Wide(i - 1));
                for (int leg = 0; leg < body.Kept * 2; leg++)
                    if (body.LegOn(leg)) Matrix(json, HunterLayout.LegPiece, root + turnRoot.Rotate(pose.LegPosition[leg]), turnRoot * pose.LegRotation[leg], One(1));
                for (int side = 0; side < 2; side++)
                {
                    Matrix(json, HunterLayout.FangPiece, root + turnRoot.Rotate(pose.FangPosition[side]), turnRoot * pose.FangRotation[side], One(1));
                    Matrix(json, HunterLayout.FeelerPiece, root + turnRoot.Rotate(pose.FeelerPosition[side]), turnRoot * pose.FeelerRotation[side], One(1));
                    if (body.HasTail) Matrix(json, HunterLayout.TailPiece, root + turnRoot.Rotate(pose.TailPosition[side]), turnRoot * pose.TailRotation[side], One(1));
                }
                foreach (int leg in legStumps)
                {
                    int node = HunterLayout.SegOf(leg) + 1;
                    if (node >= pose.Nodes) continue;
                    QuatD frameQ = turnRoot * pose.NodeRotation[node];
                    Vec3d at = root + turnRoot.Rotate(pose.NodePosition[node] + pose.NodeRotation[node].Rotate(HunterLayout.HipOf(leg)));
                    Matrix(json, HunterLayout.Stump, at, frameQ * WalkerAnimator.Look(new Vec3d(HunterLayout.Sign(leg), -0.3, 0), Vec3d.Up), One(HunterLayout.LegRadius * 1.4 / HunterLayout.StumpRadius));
                }
                if (cutAt >= 0)
                {
                    int node = cutAt;
                    QuatD q = turnRoot * pose.NodeRotation[node];
                    Vec3d at = root + turnRoot.Rotate(pose.NodePosition[node] + pose.NodeRotation[node].Rotate(new Vec3d(0, 0, -0.5 * HunterLayout.Spacing)));
                    Matrix(json, HunterLayout.Stump, at, q * QuatD.AngleAxis(Vec3d.Up, Math.PI), One(HunterLayout.SegHalfWidth * HunterLayout.Scale(cutAt) * 0.62 / HunterLayout.StumpRadius));
                }
            }
            foreach (Loose piece in loose)
                foreach (Rider rider in piece.Pieces)
                    Matrix(json, rider.Piece, piece.Position + piece.Rotation.Rotate(rider.At), piece.Rotation * rider.Turn, rider.Scale);
            json.Append("]}");
        }
        json.Append("\n]}\n");
        File.WriteAllText(path, json.ToString());
        Console.WriteLine("wrote " + path + ": " + frames + " frames of " + scene + "; segments " + body.Kept + ", legs " + body.Legs + ", mode " + brain.Mode);
        return 0;
    }
}
