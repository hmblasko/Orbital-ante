using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

/// <summary>
/// The Roller's own rules run outside Unity: scenes "walk", "roll" (its brain against someone standing still: the
/// curl, the roll, on its back, getting up) and "lose" (plates, legs and the tail shot off, then the burst).
/// Every frame carries its own camera, which follows the animal.
/// </summary>
static class RollerDump
{
    const double Fps = 15.0;

    sealed class Loose
    {
        public string Piece;
        public Vec3d Position, Velocity, Axis, Scale;
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
    static Vec3d Wide(int link) => new Vec3d(RollerLayout.Scale(link), RollerLayout.Scale(link), 1.0);

    public static int Run(string path, string scene)
    {
        var rules = new RollerRules();
        var body = new RollerBody();
        var animator = new RollerAnimator();
        var brain = new RollerBrain(rules, 1);
        var pose = new RollerPose();
        var loose = new List<Loose>();
        var legStumps = new List<int>();
        var rng = new Random(5);
        bool dead = false, tailStump = false;

        Vec3d root = scene == "roll" ? new Vec3d(0, 0, -7.0) : new Vec3d(0, 0, -5.0);
        double yaw = 0.0;
        var player = new Vec3d(0, 0, 4.0);
        double seconds = scene == "roll" ? 8.0 : scene == "lose" ? 7.0 : 5.0;
        int frames = (int)(seconds * Fps);
        const double dt = 1.0 / 120.0;
        int perFrame = (int)(120 / Fps);
        var json = new StringBuilder();
        json.Append("{\"name\":\"").Append(scene).Append("\",\"fps\":").Append(F(Fps)).Append(",\"lens\":30,\"size\":[960,600],\"samples\":12,")
            .Append("\"eye\":[6,0,1.6],\"target\":[0,0,0.5],\"person\":[-1.6,").Append(scene == "roll" ? "4.0" : "1.0").Append("],\"frames\":[\n");

        // What is shot off, and when: (seconds, kind, index). Kind 3 is the kill.
        var shots = new List<(double at, int kind, int index)>();
        if (scene == "lose")
        {
            shots.Add((1.0, 1, 2));
            shots.Add((1.5, 0, RollerLayout.Leg(1, RollerLayout.RightSide)));
            shots.Add((2.0, 0, RollerLayout.Leg(4, RollerLayout.RightSide)));
            shots.Add((2.6, 1, 4));
            shots.Add((3.2, 2, 0));
            shots.Add((3.8, 0, RollerLayout.Leg(2, RollerLayout.RightSide)));
            shots.Add((4.6, 3, 0));
        }

        double time = 0.0;
        Vec3d look = root;
        RollerOrders orders = default;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int step = 0; step < perFrame; step++)
            {
                time += dt;
                QuatD turnNow = QuatD.AngleAxis(Vec3d.Up, yaw);
                if (scene == "roll")
                {
                    var senses = new RollerSenses { SeesPlayer = time > 0.6, Player = QuatD.AngleAxis(Vec3d.Up, -yaw).Rotate(player - root) };
                    // After the first roll the someone it was after has gone: it gets up and stands.
                    if (brain.Mode == RollerMode.Righting || (brain.Mode == RollerMode.Walk && time > 4.0)) senses.SeesPlayer = false;
                    orders = brain.Think(body, senses, dt);
                    if (brain.Mode == RollerMode.Walk && time > 4.0) orders.Velocity = Vec3d.Zero;
                }
                else
                {
                    double pace = time < 0.4 || dead ? 0.0 : (scene == "lose" ? 2.0 : rules.pace) * body.SpeedShare;
                    double turn = scene == "walk" && time > 2.0 && time < 3.6 ? 0.45 : 0.0;
                    orders = new RollerOrders { Velocity = new Vec3d(0, 0, pace), TurnRate = turn, Mode = RollerMode.Walk };
                }
                yaw += orders.TurnRate * dt;
                root += QuatD.AngleAxis(Vec3d.Up, yaw).Rotate(orders.Velocity) * dt;
                turnNow = QuatD.AngleAxis(Vec3d.Up, yaw);

                foreach (var shot in shots.ToArray())
                {
                    if (time < shot.at) continue;
                    shots.Remove(shot);
                    void Fly(string piece, Vec3d at, QuatD q, Vec3d scale, Vec3d way, double rest)
                    {
                        loose.Add(new Loose
                        {
                            Piece = piece, Scale = scale, Position = root + turnNow.Rotate(at), Rotation = turnNow * q, Rest = rest,
                            Velocity = turnNow.Rotate(way) * (2.5 + rng.NextDouble() * 2.5) + Vec3d.Up * (3.0 + rng.NextDouble() * 3.0),
                            Axis = new Vec3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized, Spin = 3.0 + rng.NextDouble() * 5.0,
                        });
                    }
                    if (shot.kind == 0 && body.HitLeg(shot.index, 0, sever: true).Losses.Count > 0)
                    {
                        Fly(RollerLayout.LegPiece, pose.LegPosition[shot.index], pose.LegRotation[shot.index], One(1),
                            new Vec3d(RollerLayout.Sign(shot.index), 0, 0), 0.05);
                        legStumps.Add(shot.index);
                        animator.Flinch(new Vec3d(-RollerLayout.Sign(shot.index), 0.6, 0) * 1.5);
                    }
                    else if (shot.kind == 1 && body.HitPlate(shot.index, 0, sever: true).Losses.Count > 0)
                    {
                        Fly(RollerLayout.PlatePiece, pose.LinkPosition[shot.index + 1], pose.LinkRotation[shot.index + 1], Wide(shot.index + 1), new Vec3d(0.6, 0, -0.3), 0.14);
                        animator.Flinch(new Vec3d(-0.6, 1.0, 0) * 1.5);
                    }
                    else if (shot.kind == 2 && body.HitTail(0, sever: true).Losses.Count > 0)
                    {
                        Fly(RollerLayout.TailPiece, pose.LinkPosition[8], pose.LinkRotation[8], One(1), new Vec3d(0.3, 0, -1), 0.1);
                        tailStump = true;
                        animator.Flinch(new Vec3d(0, 0.8, 1.5));
                    }
                    else if (shot.kind == 3)
                    {
                        body.HitBody(true, 0, lethal: true);
                        dead = true;
                        for (int i = 0; i < RollerLayout.Links; i++)
                        {
                            if (i == RollerLayout.TailLink && !body.TailOn) continue;
                            Vec3d away = pose.LinkPosition[i] - pose.Centre;
                            away = new Vec3d(away.X + (rng.NextDouble() - 0.5) * 0.8, 0, away.Z).Normalized;
                            string piece = i == 0 ? RollerLayout.HeadPiece : i == 8 ? RollerLayout.TailPiece : RollerLayout.SegPiece;
                            Fly(piece, pose.LinkPosition[i], pose.LinkRotation[i], Wide(i), away, i == 0 || i == 8 ? 0.12 : 0.2);
                            if (i >= 1 && i <= 7 && body.PlateOn(i - 1)) Fly(RollerLayout.PlatePiece, pose.LinkPosition[i], pose.LinkRotation[i], Wide(i), away + new Vec3d(0.5, 0, 0.2), 0.14);
                        }
                        for (int leg = 0; leg < RollerLayout.Legs; leg++)
                            if (body.LegOn(leg)) Fly(RollerLayout.LegPiece, pose.LegPosition[leg], pose.LegRotation[leg], One(1), new Vec3d(RollerLayout.Sign(leg), 0, rng.NextDouble() - 0.5), 0.05);
                        for (int k = 0; k < 2; k++) Fly(RollerLayout.Meat[k], pose.Centre, QuatD.Identity, One(1), new Vec3d(rng.NextDouble() - 0.5, 0, rng.NextDouble() - 0.5), 0.1);
                    }
                }
                if (!dead) animator.Update(body, new RollerAnimInput { Velocity = orders.Velocity, TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T }, dt, pose);

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
                            // Comes to rest lying flat.
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
            Vec3d middle = root + turnRoot.Rotate(pose.Centre);
            look += (middle - look) * 0.35;
            if (frame > 0) json.Append(",\n");
            // The camera goes along beside it, a little ahead.
            json.Append("{\"eye\":[").Append(F(look.X + 5.2)).Append(',').Append(F(look.Z + 2.4)).Append(",1.5],\"target\":[")
                .Append(F(look.X)).Append(',').Append(F(look.Z + 0.2)).Append(",0.45],\"pieces\":[{\"piece\":\"Stump\",\"m\":[0.001,0,0,0,0,0.001,0,0,0,0,0.001,-5,0,0,0,1]}");
            if (!dead)
            {
                for (int i = 0; i < RollerLayout.Links; i++)
                {
                    if (i == RollerLayout.TailLink && !body.TailOn) continue;
                    string piece = i == 0 ? RollerLayout.HeadPiece : i == 8 ? RollerLayout.TailPiece : RollerLayout.SegPiece;
                    Vec3d at = root + turnRoot.Rotate(pose.LinkPosition[i]);
                    QuatD q = turnRoot * pose.LinkRotation[i];
                    Matrix(json, piece, at, q, Wide(i));
                    if (i >= 1 && i <= 7 && body.PlateOn(i - 1)) Matrix(json, RollerLayout.PlatePiece, at, q, Wide(i));
                }
                for (int leg = 0; leg < RollerLayout.Legs; leg++)
                    if (body.LegOn(leg)) Matrix(json, RollerLayout.LegPiece, root + turnRoot.Rotate(pose.LegPosition[leg]), turnRoot * pose.LegRotation[leg], One(1));
                foreach (int leg in legStumps)
                {
                    int link = RollerLayout.SegOf(leg) + 1;
                    QuatD frameQ = turnRoot * pose.LinkRotation[link];
                    Vec3d at = root + turnRoot.Rotate(pose.LinkPosition[link] + pose.LinkRotation[link].Rotate(RollerLayout.HipOf(leg)));
                    Matrix(json, RollerLayout.Stump, at, frameQ * WalkerAnimator.Look(new Vec3d(RollerLayout.Sign(leg), -0.6, 0), Vec3d.Up), One(RollerLayout.LegRadius * 1.3 / RollerLayout.StumpRadius));
                }
                if (tailStump)
                {
                    QuatD q = turnRoot * pose.LinkRotation[7];
                    Vec3d at = root + turnRoot.Rotate(pose.LinkPosition[7] + pose.LinkRotation[7].Rotate(new Vec3d(0, 0, -0.5 * RollerLayout.SegLength)));
                    Matrix(json, RollerLayout.Stump, at, q * QuatD.AngleAxis(Vec3d.Up, Math.PI), One(0.16 / RollerLayout.StumpRadius));
                }
            }
            foreach (Loose piece in loose) Matrix(json, piece.Piece, piece.Position, piece.Rotation, piece.Scale);
            json.Append("]}");
        }
        json.Append("\n]}\n");
        File.WriteAllText(path, json.ToString());
        Console.WriteLine("wrote " + path + ": " + frames + " frames of " + scene + "; plates " + body.Plates + ", legs " + body.Legs + ", mode " + brain.Mode);
        return 0;
    }
}
