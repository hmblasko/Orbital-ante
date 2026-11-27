using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

/// <summary>
/// The Drifting maw's own rules run outside Unity: scenes "swim" (it cruises its circle with nobody about), "strike"
/// (someone floats in front of it: it comes, opens, draws them in, chews, spits them out and hangs there) and "lose"
/// (a front fin, the back fin, the tail fin and the jaw shot off, then the kill). Every frame carries its own camera
/// and where the person floats. The sea floor is the picture's floor; it swims some metres over it.
/// </summary>
static class MawDump
{
    const double Fps = 15.0;

    sealed class Loose
    {
        public string Piece;
        public Vec3d Position, Velocity, Axis, Scale;
        public QuatD Rotation;
        public double Spin;
    }

    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

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

    public static int Run(string path, string scene)
    {
        var rules = new MawRules();
        var body = new MawBody(rules.hp);
        var animator = new MawAnimator();
        var brain = new MawBrain(rules);
        var pose = new MawPose();
        var loose = new List<Loose>();
        var rng = new Random(11);
        bool dead = false;

        const double depth = 4.0;
        Vec3d root = new Vec3d(0, depth, 0), home = new Vec3d(0, depth, 6.0);
        double yaw = 0.0;
        Vec3d player = scene == "swim" ? new Vec3d(200, depth, 200) : new Vec3d(1.5, depth - 0.6, 20.0), playerSpeed = Vec3d.Zero;
        double seconds = scene == "swim" ? 8.0 : scene == "strike" ? 16.0 : 12.0;
        int frames = (int)(seconds * Fps);
        const double dt = 1.0 / 120.0;
        int perFrame = (int)(120 / Fps);
        var json = new StringBuilder();
        json.Append("{\"name\":\"").Append(scene).Append("\",\"fps\":").Append(F(Fps)).Append(",\"lens\":30,\"size\":[960,600],\"samples\":12,")
            .Append("\"eye\":[12,0,5],\"target\":[0,0,4],\"frames\":[\n");

        // Kind 0 a front fin, 1 the back fin, 2 the tail fin, 3 the jaw, 4 the kill.
        var shots = new List<(double at, int kind, int index)>();
        if (scene == "lose")
        {
            shots.Add((2.0, 0, MawLayout.RightSide));
            shots.Add((4.0, 1, 0));
            shots.Add((6.0, 2, 0));
            shots.Add((8.0, 3, 0));
            shots.Add((10.0, 4, 0));
        }

        double time = 0.0;
        Vec3d look = root;
        MawOrders orders = default;
        Vec3d moved = Vec3d.Zero;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int step = 0; step < perFrame; step++)
            {
                time += dt;
                QuatD turnNow = QuatD.AngleAxis(Vec3d.Up, yaw), backTurn = QuatD.AngleAxis(Vec3d.Up, -yaw);
                var senses = new MawSenses
                {
                    Hostile = scene != "swim", PlayerInWater = scene != "swim", Player = backTurn.Rotate(player - root), Home = backTurn.Rotate(home - root),
                    WaterAbove = 12.0, FloorBelow = root.Y,
                };
                orders = dead ? default : brain.Think(dt, senses, body);
                yaw += orders.TurnRate * dt;
                turnNow = QuatD.AngleAxis(Vec3d.Up, yaw);
                moved = new Vec3d(0, orders.Climb, orders.Speed);
                root += turnNow.Rotate(moved) * dt;

                // The person: drawn by its pull, held in its mouth, thrown out, slowed by the water.
                if (scene != "swim")
                {
                    if (orders.Holding) { player = root + turnNow.Rotate(pose.Mouth) - Vec3d.Up * 0.9; playerSpeed = Vec3d.Zero; }
                    else
                    {
                        if (orders.Spit.SqrMagnitude > 0.0) playerSpeed = turnNow.Rotate(orders.Spit);
                        playerSpeed += turnNow.Rotate(orders.Pull) * dt;
                        playerSpeed -= playerSpeed * Math.Min(1.0, 0.9 * dt);
                        player += playerSpeed * dt;
                    }
                }

                foreach (var shot in shots.ToArray())
                {
                    if (time < shot.at) continue;
                    shots.Remove(shot);
                    void Fly(string piece, Vec3d at, QuatD q, Vec3d way)
                    {
                        loose.Add(new Loose
                        {
                            Piece = piece, Position = root + turnNow.Rotate(at), Rotation = turnNow * q, Scale = One(1),
                            Velocity = turnNow.Rotate(way) * (1.2 + rng.NextDouble() * 1.2) + Vec3d.Up * (0.3 + rng.NextDouble() * 0.6),
                            Axis = new Vec3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized, Spin = 0.8 + rng.NextDouble() * 1.4,
                        });
                    }
                    if (shot.kind == 0 && body.HitFin(shot.index, 0, sever: true).FinLost >= 0)
                    {
                        Fly(MawLayout.FinPiece, pose.FinPosition[shot.index], pose.FinRotation[shot.index], new Vec3d(MawLayout.Sign(shot.index), 0, -0.3));
                        animator.Flinch(new Vec3d(-MawLayout.Sign(shot.index) * 2.0, 0.5, 0));
                        brain.Hurt(true);
                    }
                    else if (shot.kind == 1 && body.HitBackFin(0, sever: true).BackFinLost)
                    {
                        Fly(MawLayout.BackFinPiece, pose.BackFinPosition, pose.BackFinRotation, new Vec3d(0.3, 1, -0.4));
                        animator.Flinch(new Vec3d(0, -1.5, 0));
                        brain.Hurt(true);
                    }
                    else if (shot.kind == 2 && body.HitTailFin(0, sever: true).TailFinLost)
                    {
                        Fly(MawLayout.TailFinPiece, pose.TailFinPosition, pose.TailFinRotation, new Vec3d(0.5, 0, -1));
                        brain.Hurt(true);
                    }
                    else if (shot.kind == 3 && body.HitJaw(0, sever: true).JawLost)
                    {
                        Fly(MawLayout.JawPiece, pose.JawPosition, pose.JawRotation, new Vec3d(0.2, -1, 0.6));
                        animator.Flinch(new Vec3d(0, 1.5, -1.5));
                        brain.JawLost();
                    }
                    else if (shot.kind == 4)
                    {
                        body.HitHead(0, lethal: true);
                        dead = true;
                        for (int i = 0; i < MawLayout.Nodes; i++)
                            Fly(i == 0 ? MawLayout.HeadPiece : MawLayout.PieceOf(i - 1), pose.NodePosition[i], pose.NodeRotation[i], new Vec3d(rng.NextDouble() * 2 - 1, rng.NextDouble() - 0.5, rng.NextDouble() * 2 - 1));
                        for (int s = 0; s < MawLayout.Fins; s++)
                            if (body.FinOn(s)) Fly(MawLayout.FinPiece, pose.FinPosition[s], pose.FinRotation[s], new Vec3d(MawLayout.Sign(s), 0, 0));
                        for (int k = 0; k < 2; k++) Fly(MawLayout.Meat[k], pose.NodePosition[2 + k * 3], QuatD.Identity, new Vec3d(rng.NextDouble() - 0.5, 0.5, rng.NextDouble() - 0.5));
                    }
                }
                if (!dead)
                    animator.Evaluate(dt, new MawAnimInput { Velocity = moved, TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T, List = body.List, BackFinOn = body.BackFinOn }, pose);

                // Loose pieces drift, slow and sink a little.
                foreach (Loose piece in loose)
                {
                    piece.Velocity += Vec3d.Up * (-0.6 * dt) - piece.Velocity * Math.Min(1.0, 0.8 * dt);
                    piece.Position += piece.Velocity * dt;
                    piece.Spin -= piece.Spin * Math.Min(1.0, 0.3 * dt);
                    piece.Rotation = (QuatD.AngleAxis(piece.Axis, piece.Spin * dt) * piece.Rotation).Normalized;
                    if (piece.Position.Y < 0.3) { piece.Position = new Vec3d(piece.Position.X, 0.3, piece.Position.Z); piece.Velocity = Vec3d.Zero; piece.Spin = 0; }
                }
            }

            QuatD turnRoot = QuatD.AngleAxis(Vec3d.Up, yaw);
            Vec3d middle = root + turnRoot.Rotate(pose.NodePosition[3]);
            if (scene != "swim" && (player - middle).Magnitude < 16.0) middle = middle * 0.7 + player * 0.3;
            look += (middle - look) * (frame == 0 ? 1.0 : 0.15);
            if (frame > 0) json.Append(",\n");
            json.Append("{\"eye\":[").Append(F(look.X + 11.5)).Append(',').Append(F(look.Z + 4.5)).Append(',').Append(F(look.Y + 1.6)).Append("],\"target\":[")
                .Append(F(look.X)).Append(',').Append(F(look.Z)).Append(',').Append(F(look.Y)).Append("],");
            if (scene != "swim") json.Append("\"person\":[").Append(F(player.X)).Append(',').Append(F(player.Z)).Append(',').Append(F(player.Y)).Append("],");
            json.Append("\"mode\":\"").Append(dead ? "Dead" : orders.Mode.ToString())
                .Append("\",\"pieces\":[{\"piece\":\"Stump\",\"m\":[0.001,0,0,0,0,0.001,0,0,0,0,0.001,-5,0,0,0,1]}");
            if (!dead)
            {
                Vec3d At(Vec3d p) => root + turnRoot.Rotate(p);
                for (int i = 0; i < MawLayout.Nodes; i++)
                    Matrix(json, i == 0 ? MawLayout.HeadPiece : MawLayout.PieceOf(i - 1), At(pose.NodePosition[i]), turnRoot * pose.NodeRotation[i], One(1));
                if (body.JawOn) Matrix(json, MawLayout.JawPiece, At(pose.JawPosition), turnRoot * pose.JawRotation, One(1));
                for (int s = 0; s < MawLayout.Fins; s++)
                    if (body.FinOn(s)) Matrix(json, MawLayout.FinPiece, At(pose.FinPosition[s]), turnRoot * pose.FinRotation[s], One(1));
                    else
                    {
                        int node = MawLayout.FinSeg + 1;
                        Matrix(json, MawLayout.Stump, At(pose.NodePosition[node] + pose.NodeRotation[node].Rotate(MawLayout.FinRootOf(s))),
                            turnRoot * pose.NodeRotation[node] * WalkerAnimator.Look(new Vec3d(MawLayout.Sign(s), -0.3, 0), Vec3d.Up), One(1.3));
                    }
                if (body.BackFinOn) Matrix(json, MawLayout.BackFinPiece, At(pose.BackFinPosition), turnRoot * pose.BackFinRotation, One(1));
                else Matrix(json, MawLayout.Stump, At(pose.BackFinPosition), turnRoot * pose.BackFinRotation * WalkerAnimator.Look(Vec3d.Up, Vec3d.Forward), One(1.2));
                if (body.TailFinOn) Matrix(json, MawLayout.TailFinPiece, At(pose.TailFinPosition), turnRoot * pose.TailFinRotation, One(1));
                else Matrix(json, MawLayout.Stump, At(pose.TailFinPosition), turnRoot * pose.TailFinRotation * QuatD.AngleAxis(Vec3d.Up, Math.PI), One(0.9));
            }
            foreach (Loose piece in loose) Matrix(json, piece.Piece, piece.Position, piece.Rotation, piece.Scale);
            json.Append("]}");
        }
        json.Append("\n]}\n");
        File.WriteAllText(path, json.ToString());
        Console.WriteLine("wrote " + path + ": " + frames + " frames of " + scene + "; fins " + body.FinCount + ", jaw " + body.JawOn + ", mode " + brain.Mode);
        return 0;
    }
}
