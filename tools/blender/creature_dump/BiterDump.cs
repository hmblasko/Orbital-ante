using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

/// <summary>
/// A pack of Pack biters run by their own rules outside Unity: scenes "swim" (three cruise round where they live),
/// "strike" (someone floats among them: they ring them and take turns to dart) and "lose" (one loses a front fin, its
/// back fin, its tail fin and its jaw, then all three die). Every frame carries its own camera and where the person is.
/// </summary>
static class BiterDump
{
    const double Fps = 15.0;

    sealed class Fish
    {
        public BiterBody Body;
        public BiterBrain Brain;
        public BiterAnimator Animator = new BiterAnimator();
        public BiterPose Pose = new BiterPose();
        public Vec3d Root;
        public double Yaw;
        public bool Dead;
        public BiterOrders Orders;
    }

    sealed class Loose
    {
        public string Piece;
        public Vec3d Position, Velocity, Axis;
        public QuatD Rotation;
        public double Spin, Scale = 1.0;
    }

    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    static void Matrix(StringBuilder json, string piece, Vec3d p, QuatD q, double scale)
    {
        Vec3d gx = q.Rotate(Vec3d.Right) * scale, gy = q.Rotate(Vec3d.Up) * scale, gz = q.Rotate(Vec3d.Forward) * scale;
        double[] m = { gx.X, gz.X, gy.X, p.X, gx.Z, gz.Z, gy.Z, p.Z, gx.Y, gz.Y, gy.Y, p.Y, 0, 0, 0, 1 };
        json.Append(",{\"piece\":\"").Append(piece).Append("\",\"m\":[");
        for (int i = 0; i < 16; i++) { if (i > 0) json.Append(','); json.Append(F(m[i])); }
        json.Append("]}");
    }

    public static int Run(string path, string scene)
    {
        var rules = new BiterRules();
        var pack = new BiterPack();
        var rng = new Random(13);
        const double depth = 3.0;
        Vec3d home = new Vec3d(0, depth, 0);
        var fish = new List<Fish>();
        for (int i = 0; i < 3; i++)
        {
            var f = new Fish { Body = new BiterBody(rules.hp), Brain = new BiterBrain(rules, pack, i) };
            double a = i * 2.1;
            f.Root = home + new Vec3d(Math.Cos(a) * 5.0, i * 0.4 - 0.4, Math.Sin(a) * 5.0);
            f.Yaw = a + Math.PI * 0.5;
            fish.Add(f);
        }
        Vec3d player = scene == "swim" ? new Vec3d(200, depth, 200) : new Vec3d(0.0, depth, 12.0), playerSpeed = Vec3d.Zero;
        double hurt = 0;
        double seconds = scene == "swim" ? 8.0 : scene == "strike" ? 16.0 : 12.0;
        int frames = (int)(seconds * Fps);
        const double dt = 1.0 / 120.0;
        int perFrame = (int)(120 / Fps);
        var loose = new List<Loose>();
        var json = new StringBuilder();
        json.Append("{\"name\":\"").Append(scene).Append("\",\"fps\":").Append(F(Fps)).Append(",\"lens\":30,\"size\":[960,600],\"samples\":12,")
            .Append("\"eye\":[8,0,4],\"target\":[0,0,3],\"frames\":[\n");
        // Kind 0 a front fin, 1 the back fin, 2 the tail fin, 3 the jaw, 4 a kill; all on the first fish but the kills.
        var shots = new List<(double at, int kind, int who)>();
        if (scene == "lose")
        {
            shots.Add((2.0, 0, 0)); shots.Add((3.5, 1, 0)); shots.Add((5.0, 2, 0)); shots.Add((6.5, 3, 0));
            shots.Add((8.0, 4, 1)); shots.Add((9.0, 4, 2)); shots.Add((10.0, 4, 0));
        }
        double time = 0.0;
        Vec3d look = home;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int step = 0; step < perFrame; step++)
            {
                time += dt;
                pack.Advance(dt);
                foreach (Fish f in fish)
                {
                    if (f.Dead) continue;
                    QuatD back = QuatD.AngleAxis(Vec3d.Up, -f.Yaw);
                    var senses = new BiterSenses
                    {
                        Hostile = scene != "swim", PlayerInWater = scene != "swim", Player = back.Rotate(player - f.Root), Home = back.Rotate(home - f.Root),
                        WaterAbove = 10.0, FloorBelow = f.Root.Y,
                    };
                    BiterMode was = f.Brain.Mode;
                    f.Orders = f.Brain.Think(dt, senses, f.Body);
                    if (Environment.GetEnvironmentVariable("BITER_LOG") != null && f.Brain.Mode != was) Console.Error.WriteLine(F(time) + " fish " + fish.IndexOf(f) + ": " + was + " -> " + f.Brain.Mode + " far " + F(senses.Player.Magnitude) + (f.Orders.Bite > 0 ? " BIT" : ""));
                    hurt += f.Orders.Bite;
                    f.Yaw += f.Orders.TurnRate * dt;
                    QuatD turn = QuatD.AngleAxis(Vec3d.Up, f.Yaw);
                    var moved = new Vec3d(0, f.Orders.Climb, f.Orders.Speed);
                    f.Root += turn.Rotate(moved) * dt;
                    f.Animator.Evaluate(dt, new BiterAnimInput { Velocity = moved, TurnRate = f.Orders.TurnRate, Mode = f.Orders.Mode, T = f.Orders.T, List = f.Body.List, BackFinOn = f.Body.BackFinOn }, f.Pose);
                }
                // The person drifts a little, sideways now and then, as someone dodging would.
                if (scene == "strike") player = new Vec3d(2.0 * Math.Sin(time * 0.5), depth + 0.3 * Math.Sin(time * 0.9), 12.0);

                foreach (var shot in shots.ToArray())
                {
                    if (time < shot.at) continue;
                    shots.Remove(shot);
                    Fish f = fish[shot.who];
                    if (f.Dead) continue;
                    QuatD turn = QuatD.AngleAxis(Vec3d.Up, f.Yaw);
                    void Fly(string piece, Vec3d at, QuatD q, Vec3d way)
                    {
                        loose.Add(new Loose
                        {
                            Piece = piece, Position = f.Root + turn.Rotate(at), Rotation = turn * q,
                            Velocity = turn.Rotate(way) * (1.0 + rng.NextDouble()) + Vec3d.Up * (0.3 + rng.NextDouble() * 0.5),
                            Axis = new Vec3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized, Spin = 1.5 + rng.NextDouble() * 2,
                        });
                    }
                    if (shot.kind == 0 && f.Body.HitFin(0, 0, sever: true).FinLost >= 0) { Fly(BiterLayout.FinPiece, f.Pose.FinPosition[0], f.Pose.FinRotation[0], new Vec3d(1, 0, -0.3)); f.Brain.Hurt(); }
                    else if (shot.kind == 1 && f.Body.HitBackFin(0, sever: true).BackFinLost) { Fly(BiterLayout.BackFinPiece, f.Pose.BackFinPosition, f.Pose.BackFinRotation, new Vec3d(0.3, 1, -0.4)); f.Brain.Hurt(); }
                    else if (shot.kind == 2 && f.Body.HitTailFin(0, sever: true).TailFinLost) { Fly(BiterLayout.TailFinPiece, f.Pose.TailFinPosition, f.Pose.TailFinRotation, new Vec3d(0.5, 0, -1)); f.Brain.Hurt(); }
                    else if (shot.kind == 3 && f.Body.HitJaw(0, sever: true).JawLost) { Fly(BiterLayout.JawPiece, f.Pose.JawPosition, f.Pose.JawRotation, new Vec3d(0.2, -1, 0.6)); f.Brain.Hurt(); }
                    else if (shot.kind == 4)
                    {
                        f.Body.HitHead(0, lethal: true);
                        f.Dead = true;
                        f.Brain.Died();
                        for (int i = 0; i < BiterLayout.Nodes; i++)
                            Fly(i == 0 ? BiterLayout.HeadPiece : BiterLayout.PieceOf(i - 1), f.Pose.NodePosition[i], f.Pose.NodeRotation[i], new Vec3d(rng.NextDouble() * 2 - 1, rng.NextDouble() - 0.5, rng.NextDouble() * 2 - 1));
                        if (f.Body.JawOn) Fly(BiterLayout.JawPiece, f.Pose.JawPosition, f.Pose.JawRotation, new Vec3d(0, -1, 0.5));
                        for (int s = 0; s < BiterLayout.Fins; s++) if (f.Body.FinOn(s)) Fly(BiterLayout.FinPiece, f.Pose.FinPosition[s], f.Pose.FinRotation[s], new Vec3d(BiterLayout.Sign(s), 0, 0));
                        if (f.Body.BackFinOn) Fly(BiterLayout.BackFinPiece, f.Pose.BackFinPosition, f.Pose.BackFinRotation, new Vec3d(0, 1, 0));
                        if (f.Body.TailFinOn) Fly(BiterLayout.TailFinPiece, f.Pose.TailFinPosition, f.Pose.TailFinRotation, new Vec3d(0, 0, -1));
                    }
                }
                foreach (Loose piece in loose)
                {
                    piece.Velocity += Vec3d.Up * (-0.6 * dt) - piece.Velocity * Math.Min(1.0, 0.8 * dt);
                    piece.Position += piece.Velocity * dt;
                    piece.Rotation = (QuatD.AngleAxis(piece.Axis, piece.Spin * dt) * piece.Rotation).Normalized;
                    if (piece.Position.Y < 0.1) { piece.Position = new Vec3d(piece.Position.X, 0.1, piece.Position.Z); piece.Velocity = Vec3d.Zero; piece.Spin = 0; }
                }
            }

            Vec3d middle = Vec3d.Zero;
            int alive = 0;
            foreach (Fish f in fish) if (!f.Dead) { middle += f.Root; alive++; }
            middle = alive > 0 ? middle / alive : home;
            if (scene != "swim") middle = middle * 0.6 + player * 0.4;
            look += (middle - look) * (frame == 0 ? 1.0 : 0.12);
            if (frame > 0) json.Append(",\n");
            json.Append("{\"eye\":[").Append(F(look.X + 7.5)).Append(',').Append(F(look.Z - 2.5)).Append(',').Append(F(look.Y + 1.8)).Append("],\"target\":[")
                .Append(F(look.X)).Append(',').Append(F(look.Z)).Append(',').Append(F(look.Y)).Append("],");
            if (scene != "swim") json.Append("\"person\":[").Append(F(player.X)).Append(',').Append(F(player.Z)).Append(',').Append(F(player.Y - 0.9)).Append("],");
            json.Append("\"mode\":\"").Append(fish[0].Dead ? "Dead" : fish[0].Orders.Mode.ToString())
                .Append("\",\"pieces\":[{\"piece\":\"Stump\",\"m\":[0.001,0,0,0,0,0.001,0,0,0,0,0.001,-5,0,0,0,1]}");
            foreach (Fish f in fish)
            {
                if (f.Dead) continue;
                QuatD turn = QuatD.AngleAxis(Vec3d.Up, f.Yaw);
                Vec3d At(Vec3d p) => f.Root + turn.Rotate(p);
                BiterPose pose = f.Pose;
                for (int i = 0; i < BiterLayout.Nodes; i++)
                    Matrix(json, i == 0 ? BiterLayout.HeadPiece : BiterLayout.PieceOf(i - 1), At(pose.NodePosition[i]), turn * pose.NodeRotation[i], 1);
                if (f.Body.JawOn) Matrix(json, BiterLayout.JawPiece, At(pose.JawPosition), turn * pose.JawRotation, 1);
                for (int s = 0; s < BiterLayout.Fins; s++)
                    if (f.Body.FinOn(s)) Matrix(json, BiterLayout.FinPiece, At(pose.FinPosition[s]), turn * pose.FinRotation[s], 1);
                if (f.Body.BackFinOn) Matrix(json, BiterLayout.BackFinPiece, At(pose.BackFinPosition), turn * pose.BackFinRotation, 1);
                if (f.Body.TailFinOn) Matrix(json, BiterLayout.TailFinPiece, At(pose.TailFinPosition), turn * pose.TailFinRotation, 1);
            }
            foreach (Loose piece in loose) Matrix(json, piece.Piece, piece.Position, piece.Rotation, piece.Scale);
            json.Append("]}");
        }
        json.Append("\n]}\n");
        File.WriteAllText(path, json.ToString());
        Console.WriteLine("wrote " + path + ": " + frames + " frames of " + scene + "; bitten for " + F(hurt) + ", pack " + pack.Living + " of " + pack.Started + ", first fish " + fish[0].Brain.Mode);
        return 0;
    }
}
