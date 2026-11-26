using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

/// <summary>
/// Runs a walker's own animation rules (the Strider's, or the Sprinter's) outside Unity and writes every frame as piece matrices, for
/// tools/blender/build_creatures.py -- --poses file.json to draw. See README.md.
/// Matrices are in Blender's space (x right, y forward, z up), row by row, with the game's sizes in them.
/// </summary>
static class Program
{
    const double Fps = 15.0;

    sealed class Loose
    {
        public string Piece;
        public Vec3d Position, Velocity, Axis;
        public QuatD Rotation;
        public double Spin, Scale;
        public bool Resting;
    }

    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    static void Matrix(StringBuilder json, string piece, Vec3d p, QuatD q, double scale)
    {
        // Game axes to Blender's: (x, y, z) -> (x, z, y); a piece's own +Z (along) is Blender's +Y, its +Y Blender's +Z.
        Vec3d gx = q.Rotate(Vec3d.Right) * scale, gy = q.Rotate(Vec3d.Up) * scale, gz = q.Rotate(Vec3d.Forward) * scale;
        double[] m =
        {
            gx.X, gz.X, gy.X, p.X,
            gx.Z, gz.Z, gy.Z, p.Z,
            gx.Y, gz.Y, gy.Y, p.Y,
            0, 0, 0, 1,
        };
        json.Append("{\"piece\":\"").Append(piece).Append("\",\"m\":[");
        for (int i = 0; i < 16; i++) { if (i > 0) json.Append(','); json.Append(F(m[i])); }
        json.Append("]}");
    }

    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("usage: creature_dump <out.json> <walk|strike|lose> [Strider|Sprinter]"); return 2; }
        string scene = args[1];
        if (args.Length > 2 && args[2] == RollerLayout.Model) return RollerDump.Run(args[0], scene);
        if (args.Length > 2 && args[2] == HunterLayout.Model) return HunterDump.Run(args[0], scene);
        if (args.Length > 2 && args[2] == ScreamerLayout.Model) return ScreamerDump.Run(args[0], scene);
        if (args.Length > 2 && args[2] == AmbusherLayout.Model) return AmbusherDump.Run(args[0], scene);
        if (args.Length > 2 && args[2] == StalkerLayout.Model) return StalkerDump.Run(args[0], scene);
        WalkerLayout _l = args.Length > 2 ? WalkerLayout.For(args[2]) : WalkerLayout.Strider;
        if (_l == null) { Console.Error.WriteLine("no such kind: " + args[2]); return 2; }
        bool quick = _l == WalkerLayout.Sprinter;
        WalkerRules rules = quick ? WalkerRules.ForSprinter() : new WalkerRules();
        var body = new WalkerBody(_l);
        var animator = new WalkerAnimator(_l);
        var brain = new WalkerBrain(_l, rules, 1);
        var pose = new WalkerPose();
        var loose = new List<Loose>();
        var stumps = new List<(int limb, int segment)>();
        var jawStumps = new List<int>();
        var rng = new Random(5);

        // Where the animal is in the world, and which way it faces (a turn about up).
        Vec3d root = scene == "walk" ? new Vec3d(-1.0, 0, -7.5) : scene == "lose" ? new Vec3d(0, 0, -6.0) : new Vec3d(0, 0, -1.2);
        if (quick) root = scene == "walk" ? new Vec3d(-0.6, 0, -11.0) : scene == "lose" ? new Vec3d(0, 0, -9.0) : new Vec3d(0, 0, -0.9);
        double yaw = 0.0;
        double seconds = scene == "strike" ? 7.0 : scene == "lose" ? 14.0 : 9.0;
        if (quick) seconds = scene == "strike" ? 5.0 : scene == "lose" ? 9.0 : 6.0;
        double strikeSeconds = rules.strikeSeconds;
        var person = new Vec3d(scene == "strike" ? 0.55 : 2.6, 0, scene == "strike" ? 2.6 : 1.5);
        if (quick) person = new Vec3d(scene == "strike" ? 0.35 : 1.8, 0, scene == "strike" ? 1.25 : 1.0);
        int frames = (int)(seconds * Fps);
        const double dt = 1.0 / 120.0;
        int perFrame = (int)(120 / Fps);
        var json = new StringBuilder();
        string eye = scene == "strike" ? "[4.2,-3.4,1.7]" : scene == "lose" ? "[7.2,-2.5,1.7]" : "[7.5,-1.5,1.7]";
        string target = scene == "strike" ? "[0.3,1.0,1.7]" : "[0.0,0.5,1.6]";
        if (quick)
        {
            eye = scene == "strike" ? "[3.6,-1.6,1.5]" : scene == "lose" ? "[6.5,-4.0,2.0]" : "[7.0,-5.0,2.2]";
            target = scene == "strike" ? "[0.1,0.2,0.6]" : scene == "lose" ? "[0.0,-4.0,0.4]" : "[0.0,-5.0,0.4]";
        }
        json.Append("{\"name\":\"").Append(scene).Append("\",\"fps\":").Append(F(Fps)).Append(",\"lens\":").Append(scene == "strike" ? "30" : "26")
            .Append(",\"size\":[960,600],\"samples\":12,\"eye\":").Append(eye).Append(",\"target\":").Append(target)
            .Append(",\"person\":[").Append(F(person.X)).Append(',').Append(F(person.Z)).Append("],\"frames\":[\n");

        // What is shot off, and when (seconds, side, place, segment).
        var shots = new List<(double at, int limb, int segment)>();
        if (scene == "lose")
        {
            shots.Add((2.0, WalkerLayout.Limb(WalkerLayout.Right, 2), 1));
            shots.Add((4.0, WalkerLayout.Limb(WalkerLayout.Left, 1), 0));
            shots.Add((6.0, WalkerLayout.Limb(WalkerLayout.Right, 0), 2));
            shots.Add((7.5, WalkerLayout.Limb(WalkerLayout.Right, 4), 0));
            shots.Add((9.5, WalkerLayout.Limb(WalkerLayout.Left, 3), 1));
            shots.Add((10.5, WalkerLayout.Limb(WalkerLayout.Right, 1), 0));
            if (quick)
            {
                // The same order in half the time, and a jaw as well (limb 10).
                for (int i = 0; i < shots.Count; i++) shots[i] = (0.8 + shots[i].at * 0.55, shots[i].limb, shots[i].segment);
                shots.Add((3.0, WalkerBody.JawLimb(WalkerLayout.Right), 0));
            }
        }

        double time = 0.0;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int step = 0; step < perFrame; step++)
            {
                time += dt;
                var input = new WalkerAnimInput { StrikeSide = -1 };
                double pace = 0.0, turn = 0.0;
                if (scene == "walk") { pace = time < 0.6 ? 0.0 : 1.7; turn = time > 4.0 && time < 7.0 ? 0.25 : 0.0; }
                if (scene == "lose") pace = time < 0.5 ? 0.0 : 1.5;
                if (quick && scene == "walk") { pace = time < 0.5 || time > 1.8 && time < 2.4 || time > 3.9 ? 0.0 : rules.pace; turn = time > 2.4 && time < 3.4 ? 0.5 : 0.0; }
                if (quick && scene == "lose") pace = time < 0.4 ? 0.0 : rules.pace * 0.55;
                pace *= body.SpeedShare;
                if (scene == "strike")
                {
                    double t = (time - 1.0) / strikeSeconds, t2 = (time - (quick ? 3.0 : 4.0)) / strikeSeconds;
                    Vec3d at = QuatD.AngleAxis(Vec3d.Up, -yaw).Rotate(person + new Vec3d(0, 1.3, 0) - root);
                    at = brain.Reachable(t >= 0 && t <= 1 ? WalkerLayout.Right : WalkerLayout.Left, at);
                    if (t >= 0 && t <= 1) { input.StrikeSide = WalkerLayout.Right; input.StrikeT = t; input.StrikeAt = at; }
                    if (t2 >= 0 && t2 <= 1) { input.StrikeSide = WalkerLayout.Left; input.StrikeT = t2; input.StrikeAt = at; }
                }
                input.Velocity = new Vec3d(0, 0, pace);
                input.TurnRate = turn;
                yaw += turn * dt;
                root += QuatD.AngleAxis(Vec3d.Up, yaw).Rotate(input.Velocity) * dt;

                foreach (var shot in shots.ToArray())
                {
                    if (time < shot.at) continue;
                    shots.Remove(shot);
                    if (shot.limb >= WalkerLayout.Limbs)
                    {
                        int jawSide = shot.limb - WalkerLayout.Limbs;
                        if (body.HitJaw(jawSide, 0, sever: true).Losses.Count == 0) continue;
                        QuatD now = QuatD.AngleAxis(Vec3d.Up, yaw);
                        foreach (bool finger in new[] { false, true })
                            loose.Add(new Loose
                            {
                                Piece = finger ? _l.FingerPiece : _l.JawPiece, Scale = _l.Size,
                                Position = root + now.Rotate(finger ? pose.FingerPosition[jawSide] : pose.JawPosition[jawSide]),
                                Rotation = now * (finger ? pose.FingerRotation[jawSide] : pose.JawRotation[jawSide]),
                                Velocity = now.Rotate(new Vec3d(1.2, 0, 2.5)) + Vec3d.Up * 3.5, Axis = Vec3d.Right, Spin = 7.0,
                            });
                        jawStumps.Add(jawSide);
                        animator.Flinch(new Vec3d(0, 0.4, -1.2));
                        continue;
                    }
                    int kept = body.Kept(shot.limb);
                    WalkerHit hit = body.HitLimb(shot.limb, shot.segment, 0, sever: true);
                    if (hit.Losses.Count == 0) continue;
                    stumps.RemoveAll(s => s.limb == shot.limb);
                    stumps.Add((shot.limb, shot.segment));
                    QuatD turnNow = QuatD.AngleAxis(Vec3d.Up, yaw);
                    Vec3d away = turnNow.Rotate(_l.Out(shot.limb));
                    for (int s = shot.segment; s < kept; s++)
                    {
                        int i = shot.limb * WalkerLayout.Segments + s;
                        loose.Add(new Loose
                        {
                            Piece = _l.Piece(shot.limb, s), Scale = _l.Scale(shot.limb),
                            Position = root + turnNow.Rotate(pose.Position[i]), Rotation = turnNow * pose.Rotation[i],
                            Velocity = away * (2.5 + rng.NextDouble() * 2.0) + Vec3d.Up * (3.0 + rng.NextDouble() * 2.5),
                            Axis = new Vec3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized, Spin = 3.0 + rng.NextDouble() * 4.0,
                        });
                        if (s == 2 && WalkerLayout.IsArm(shot.limb) && !_l.HasJaws)
                        {
                            int side = WalkerLayout.SideOf(shot.limb);
                            loose.Add(new Loose { Piece = _l.FingerPiece, Scale = _l.Size, Position = root + turnNow.Rotate(pose.FingerPosition[side]),
                                Rotation = turnNow * pose.FingerRotation[side], Velocity = away * 3.5 + Vec3d.Up * 4.5, Axis = Vec3d.Right, Spin = 6.0 });
                        }
                    }
                    animator.Flinch(_l.Out(shot.limb) * -1.6 + Vec3d.Up * 0.9);
                }
                animator.Update(body, input, dt, pose);

                foreach (Loose piece in loose)
                {
                    if (piece.Resting) continue;
                    piece.Velocity += Vec3d.Up * (-9.81 * dt);
                    piece.Position += piece.Velocity * dt;
                    piece.Rotation = (QuatD.AngleAxis(piece.Axis, piece.Spin * dt) * piece.Rotation).Normalized;
                    if (piece.Position.Y < 0.09)
                    {
                        piece.Position = new Vec3d(piece.Position.X, 0.09, piece.Position.Z);
                        if (Math.Abs(piece.Velocity.Y) < 1.2)
                        {
                            // Comes to rest lying flat.
                            piece.Resting = true;
                            Vec3d along = piece.Rotation.Rotate(Vec3d.Forward);
                            along = new Vec3d(along.X, 0, along.Z).Normalized;
                            piece.Rotation = WalkerAnimator.Look(along, Vec3d.Up);
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
            if (frame > 0) json.Append(",\n");
            json.Append("{\"pieces\":[");
            Matrix(json, _l.Armoured, root + turnRoot.Rotate(pose.BodyPosition), turnRoot * pose.BodyRotation, _l.Size);
            json.Append(',');
            // The soft piece turns about its own joint on the armoured one.
            Vec3d pivot = _l.SoftPivot * _l.Size;
            Vec3d softAt = pose.BodyPosition + pose.BodyRotation.Rotate(pivot - pose.SoftTurn.Rotate(pivot));
            Matrix(json, _l.Soft, root + turnRoot.Rotate(softAt), turnRoot * pose.BodyRotation * pose.SoftTurn, _l.Size);
            for (int side = 0; side < 2 && _l.HasJaws; side++)
            {
                if (!body.JawOn(side)) continue;
                json.Append(',');
                Matrix(json, _l.JawPiece, root + turnRoot.Rotate(pose.JawPosition[side]), turnRoot * pose.JawRotation[side], _l.Size);
                json.Append(',');
                Matrix(json, _l.FingerPiece, root + turnRoot.Rotate(pose.FingerPosition[side]), turnRoot * pose.FingerRotation[side], _l.Size);
            }
            foreach (int side in jawStumps)
            {
                json.Append(',');
                Matrix(json, _l.Stump, root + turnRoot.Rotate(pose.BodyPosition + pose.BodyRotation.Rotate(_l.JawAt(side) * _l.Size)), turnRoot * pose.BodyRotation, _l.JawRadius / _l.StumpRadius * _l.Size);
            }
            for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
            {
                for (int s = 0; s < body.Kept(limb); s++)
                {
                    int i = limb * WalkerLayout.Segments + s;
                    json.Append(',');
                    Matrix(json, _l.Piece(limb, s), root + turnRoot.Rotate(pose.Position[i]), turnRoot * pose.Rotation[i], _l.Scale(limb));
                }
                if (WalkerLayout.IsArm(limb) && !_l.HasJaws && body.Kept(limb) == 3)
                {
                    int side = WalkerLayout.SideOf(limb);
                    json.Append(',');
                    Matrix(json, _l.FingerPiece, root + turnRoot.Rotate(pose.FingerPosition[side]), turnRoot * pose.FingerRotation[side], _l.Size);
                }
            }
            foreach (var stump in stumps)
            {
                // The wound sits where the lost piece began, looking along the piece that is left (or out from the body).
                Vec3d at, way, up;
                double radius;
                if (stump.segment == 0)
                {
                    at = pose.BodyPosition + pose.BodyRotation.Rotate(_l.HipModelled(stump.limb) * _l.Size);
                    way = pose.BodyRotation.Rotate(_l.Out(stump.limb));
                    up = pose.BodyRotation.Rotate(Vec3d.Up);
                    radius = _l.Radius(stump.limb, 0);
                }
                else
                {
                    int i = stump.limb * WalkerLayout.Segments + stump.segment - 1;
                    way = pose.Rotation[i].Rotate(Vec3d.Forward);
                    up = pose.Rotation[i].Rotate(Vec3d.Up);
                    at = pose.Position[i] + way * _l.Length(stump.limb, stump.segment - 1);
                    radius = _l.Radius(stump.limb, stump.segment);
                }
                json.Append(',');
                Matrix(json, _l.Stump, root + turnRoot.Rotate(at), turnRoot * WalkerAnimator.Look(way, up), radius / _l.StumpRadius);
            }
            foreach (Loose piece in loose)
            {
                json.Append(',');
                Matrix(json, piece.Piece, piece.Position, piece.Rotation, piece.Scale);
            }
            json.Append("]}");
        }
        json.Append("\n]}\n");
        File.WriteAllText(args[0], json.ToString());
        Console.WriteLine("wrote " + args[0] + ": " + frames + " frames of " + scene + "; legs left " + body.Legs + ", stance " + body.Stance);
        return 0;
    }
}
