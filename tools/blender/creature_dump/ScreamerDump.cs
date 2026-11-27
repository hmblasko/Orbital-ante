using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

/// <summary>
/// The Screamer's own rules run outside Unity: scenes "strike" (its brain against someone standing still: the flight
/// in, the landing, the warning, the scream, sagging afterwards) and "lose" (a drum shot off in the scream, wings
/// shot off in the air, the fall, legs, then the burst). Every frame carries its own camera, which follows the animal.
/// The rings of the scream are the game's own effect and are not drawn here.
/// </summary>
static class ScreamerDump
{
    const double Fps = 15.0;

    sealed class Loose
    {
        public string Piece;
        public Vec3d Position, Velocity, Axis;
        public QuatD Rotation;
        public double Spin, Rest;
        public bool Resting;
    }

    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>Game axes to Blender's: (x, y, z) -> (x, z, y); a piece's own +Z (along) is Blender's +Y, its +Y Blender's +Z.</summary>
    static void Matrix(StringBuilder json, string piece, Vec3d p, QuatD q, double scale = 1.0)
    {
        Vec3d gx = q.Rotate(Vec3d.Right) * scale, gy = q.Rotate(Vec3d.Up) * scale, gz = q.Rotate(Vec3d.Forward) * scale;
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

    public static int Run(string path, string scene)
    {
        var rules = new ScreamerRules();
        var body = new ScreamerBody(rules.hp);
        var animator = new ScreamerAnimator();
        var brain = new ScreamerBrain(rules, 4);
        var pose = new ScreamerPose();
        var loose = new List<Loose>();
        var rng = new Random(5);
        bool dead = false;
        var wingStumps = new List<int>();
        var legStumps = new List<int>();
        var drumStumps = new List<int>();

        Vec3d root = new Vec3d(0, 0, 0);
        double yaw = 0.0;
        bool lose = scene == "lose";
        var player = lose ? new Vec3d(2.0, 0, 11.0) : new Vec3d(0, 0, 24.0);
        double seconds = lose ? 15.0 : 11.0;
        int frames = (int)(seconds * Fps);
        const double dt = 1.0 / 120.0;
        int perFrame = (int)(120 / Fps);
        var json = new StringBuilder();
        json.Append("{\"name\":\"").Append(scene).Append("\",\"fps\":").Append(F(Fps)).Append(",\"lens\":30,\"size\":[960,600],\"samples\":12,")
            .Append("\"eye\":[6,0,1.6],\"target\":[0,0,0.5],\"person\":[").Append(F(player.X)).Append(',').Append(F(player.Z)).Append("],\"frames\":[\n");

        // What is shot off, and when: (seconds, kind, index). Kinds: 0 wing, 1 leg, 2 drum, 3 the kill.
        var shots = new List<(double at, int kind, int index)>();
        if (lose)
        {
            shots.Add((1.9, 2, 0));
            shots.Add((5.2, 0, ScreamerLayout.Wing(0, false)));
            shots.Add((5.5, 0, ScreamerLayout.Wing(1, true)));
            shots.Add((9.3, 1, ScreamerLayout.Leg(0, 1)));
            shots.Add((10.0, 1, ScreamerLayout.Leg(1, 0)));
            shots.Add((11.2, 0, ScreamerLayout.Wing(1, false)));
            shots.Add((12.4, 3, 0));
        }

        double time = 0.0;
        Vec3d look = root;
        ScreamerOrders orders = default;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int step = 0; step < perFrame; step++)
            {
                time += dt;
                QuatD turnNow = QuatD.AngleAxis(Vec3d.Up, yaw);
                QuatD back = QuatD.AngleAxis(Vec3d.Up, -yaw);
                if (!dead)
                {
                    var senses = new ScreamerSenses
                    {
                        SeesPlayer = time > 0.5,
                        Player = back.Rotate(player - root),
                        Home = back.Rotate(Vec3d.Zero - root),
                        North = back.Rotate(Vec3d.Forward),
                    };
                    orders = brain.Think(body, senses, dt);
                    yaw += orders.TurnRate * dt;
                    turnNow = QuatD.AngleAxis(Vec3d.Up, yaw);
                    root += turnNow.Rotate(orders.Velocity) * dt;
                }

                foreach (var shot in shots.ToArray())
                {
                    if (time < shot.at) continue;
                    shots.Remove(shot);
                    void Fly(string piece, Vec3d at, QuatD q, Vec3d way, double rest, double hard = 1.0)
                    {
                        loose.Add(new Loose
                        {
                            Piece = piece, Position = root + turnNow.Rotate(at), Rotation = turnNow * q, Rest = rest,
                            Velocity = turnNow.Rotate(way) * (2.5 + rng.NextDouble() * 2.0) * hard + Vec3d.Up * (2.0 + rng.NextDouble() * 2.5) * hard,
                            Axis = new Vec3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized, Spin = 3.0 + rng.NextDouble() * 5.0,
                        });
                    }
                    void WingOff(int w)
                    {
                        Fly(ScreamerLayout.IsHind(w) ? ScreamerLayout.HindWingPiece : ScreamerLayout.ForeWingPiece, pose.WingPosition[w], pose.WingRotation[w],
                            new Vec3d(ScreamerLayout.Sign(ScreamerLayout.WingSide(w)), 0, -0.2), 0.02, 0.7);
                    }
                    void LegOff(int leg)
                    {
                        var way = new Vec3d(ScreamerLayout.Sign(ScreamerLayout.LegSide(leg)), 0, 0.2);
                        Fly(ScreamerLayout.LegPiece, pose.LegPosition[leg], pose.LegRotation[leg], way, 0.04);
                        Fly(ScreamerLayout.ShinPiece, pose.ShinPosition[leg], pose.ShinRotation[leg], way, 0.03);
                    }
                    if (shot.kind == 0 && body.HitWing(shot.index, 0, sever: true).WingLost >= 0)
                    {
                        WingOff(shot.index);
                        wingStumps.Add(shot.index);
                        animator.Flinch(new Vec3d(-ScreamerLayout.Sign(ScreamerLayout.WingSide(shot.index)), 0.4, 0) * 1.5);
                    }
                    else if (shot.kind == 1 && body.HitLeg(shot.index, 0, sever: true).LegLost >= 0)
                    {
                        LegOff(shot.index);
                        legStumps.Add(shot.index);
                        animator.Flinch(new Vec3d(-ScreamerLayout.Sign(ScreamerLayout.LegSide(shot.index)), 0.4, 0) * 1.5);
                    }
                    else if (shot.kind == 2 && body.HitDrum(shot.index, 0, sever: true).DrumLost >= 0)
                    {
                        Fly(ScreamerLayout.DrumPiece, pose.DrumPosition[shot.index], pose.DrumRotation[shot.index], new Vec3d(ScreamerLayout.Sign(shot.index), 0, 0), 0.07, 1.3);
                        drumStumps.Add(shot.index);
                        if (brain.Choke()) animator.Flinch(new Vec3d(-ScreamerLayout.Sign(shot.index) * 1.5, 1.0, -1.0));
                    }
                    else if (shot.kind == 3)
                    {
                        dead = true;
                        for (int w = 0; w < ScreamerLayout.Wings; w++) if (body.WingOn(w)) WingOff(w);
                        for (int leg = 0; leg < ScreamerLayout.Legs; leg++) if (body.LegOn(leg)) LegOff(leg);
                        for (int d = 0; d < ScreamerLayout.Drums; d++) if (body.DrumOn(d)) Fly(ScreamerLayout.DrumPiece, pose.DrumPosition[d], pose.DrumRotation[d], new Vec3d(ScreamerLayout.Sign(d), 0, -0.5), 0.07);
                        Fly(ScreamerLayout.BodyPiece, pose.BodyPosition, pose.BodyRotation, new Vec3d(0.2, 0, 0.8), 0.22, 0.8);
                        Fly(ScreamerLayout.AbdomenPiece, pose.AbdomenPosition, pose.AbdomenRotation, new Vec3d(-0.3, 0, -0.9), 0.2, 0.9);
                        for (int k = 0; k < 2; k++) Fly(ScreamerLayout.Meat[k], pose.AbdomenPosition, QuatD.Identity, new Vec3d(rng.NextDouble() - 0.5, 0, rng.NextDouble() - 0.5), 0.1);
                        body.HitBody(0, lethal: true);
                    }
                }
                if (!dead) animator.Update(body, new ScreamerAnimInput { Velocity = orders.Velocity, TurnRate = orders.TurnRate, Height = orders.Height, Mode = orders.Mode, T = orders.T }, dt, pose);

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
            Vec3d middle = root + turnRoot.Rotate(pose.BodyPosition);
            look += (middle - look) * 0.3;
            if (frame > 0) json.Append(",\n");
            // The camera stands off to one side of the line between it and the player, and follows it up and down.
            json.Append("{\"eye\":[").Append(F(look.X + 6.5)).Append(',').Append(F(look.Z - 3.2)).Append(',').Append(F(1.7 + look.Y * 0.35)).Append("],\"target\":[")
                .Append(F(look.X)).Append(',').Append(F(look.Z + 0.6)).Append(',').Append(F(Math.Max(0.5, look.Y))).Append("],\"pieces\":[{\"piece\":\"Stump\",\"m\":[0.001,0,0,0,0,0.001,0,0,0,0,0.001,-5,0,0,0,1]}");
            if (!dead)
            {
                Vec3d At(Vec3d p) => root + turnRoot.Rotate(p);
                Matrix(json, ScreamerLayout.BodyPiece, At(pose.BodyPosition), turnRoot * pose.BodyRotation);
                Matrix(json, ScreamerLayout.AbdomenPiece, At(pose.AbdomenPosition), turnRoot * pose.AbdomenRotation);
                for (int d = 0; d < ScreamerLayout.Drums; d++)
                    if (body.DrumOn(d)) Matrix(json, ScreamerLayout.DrumPiece, At(pose.DrumPosition[d]), turnRoot * pose.DrumRotation[d]);
                for (int w = 0; w < ScreamerLayout.Wings; w++)
                    if (body.WingOn(w)) Matrix(json, ScreamerLayout.IsHind(w) ? ScreamerLayout.HindWingPiece : ScreamerLayout.ForeWingPiece, At(pose.WingPosition[w]), turnRoot * pose.WingRotation[w]);
                for (int leg = 0; leg < ScreamerLayout.Legs; leg++)
                {
                    if (!body.LegOn(leg)) continue;
                    Matrix(json, ScreamerLayout.LegPiece, At(pose.LegPosition[leg]), turnRoot * pose.LegRotation[leg]);
                    Matrix(json, ScreamerLayout.ShinPiece, At(pose.ShinPosition[leg]), turnRoot * pose.ShinRotation[leg]);
                }
                QuatD bodyQ = turnRoot * pose.BodyRotation, abdomenQ = turnRoot * pose.AbdomenRotation;
                foreach (int w in wingStumps)
                {
                    double s = ScreamerLayout.Sign(ScreamerLayout.WingSide(w));
                    Matrix(json, ScreamerLayout.Stump, At(pose.BodyPosition + pose.BodyRotation.Rotate(ScreamerLayout.HingeOf(w))), bodyQ * WalkerAnimator.Look(new Vec3d(s, 0.5, 0), Vec3d.Up), 1.0);
                }
                foreach (int leg in legStumps)
                {
                    double s = ScreamerLayout.Sign(ScreamerLayout.LegSide(leg));
                    Matrix(json, ScreamerLayout.Stump, At(pose.BodyPosition + pose.BodyRotation.Rotate(ScreamerLayout.HipOf(leg))), bodyQ * WalkerAnimator.Look(new Vec3d(s, -0.4, 0), Vec3d.Up), ScreamerLayout.LegRadius * 1.4 / ScreamerLayout.StumpRadius);
                }
                foreach (int d in drumStumps)
                {
                    double s = ScreamerLayout.Sign(d);
                    Vec3d at = ScreamerLayout.DrumOf(d) - new Vec3d(s * 0.05, 0, 0);
                    Matrix(json, ScreamerLayout.Stump, At(pose.AbdomenPosition + pose.AbdomenRotation.Rotate(at)), abdomenQ * WalkerAnimator.Look(new Vec3d(s, 0, 0), Vec3d.Up), ScreamerLayout.DrumRadius * 0.8 / ScreamerLayout.StumpRadius);
                }
            }
            foreach (Loose piece in loose) Matrix(json, piece.Piece, piece.Position, piece.Rotation);
            json.Append("]}");
            if (frame % 15 == 0) Console.WriteLine(F(time) + " s: " + brain.Mode + " height " + F(orders.Height) + " at " + F(root.X) + ", " + F(root.Z));
        }
        json.Append("\n]}\n");
        File.WriteAllText(path, json.ToString());
        Console.WriteLine("wrote " + path + ": " + frames + " frames of " + scene + "; wings " + body.WingCount + ", legs " + body.LegCount + ", drums " + body.DrumCount + ", mode " + brain.Mode);
        return 0;
    }
}
