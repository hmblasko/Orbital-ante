using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

/// <summary>
/// The Blind stalker's own rules run outside Unity: scenes "strike" (someone walks up and stops near it: it hears,
/// comes, warns, lunges, lies there), "lost" (someone is heard, then steps aside and stands still: it goes where the
/// sound was, casts about and goes still) and "lose" (a gill shot off, a leg, the tail cut, then the burst). Every
/// frame carries its own camera and where the person stands.
/// </summary>
static class StalkerDump
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
    static Vec3d Wide(int seg) => new Vec3d(StalkerLayout.Scale(seg), StalkerLayout.Tall(seg), 1.0);
    static readonly double CutAt = StalkerLayout.Spacing * 0.5 + StalkerLayout.WoundOut;
    static string SegName(int seg) => StalkerLayout.IsTail(seg) ? StalkerLayout.TailPiece : StalkerLayout.SegPiece;

    public static int Run(string path, string scene)
    {
        var rules = new StalkerRules();
        var body = new StalkerBody();
        var animator = new StalkerAnimator();
        var brain = new StalkerBrain(rules);
        var pose = new StalkerPose();
        var loose = new List<Loose>();
        var legStumps = new List<int>();
        var gillStumps = new List<int>();
        var rng = new Random(7);
        bool dead = false;
        int cutAt = -1;

        Vec3d root = new Vec3d(0, 0, -2.0), home = root;
        double yaw = 0.0;
        var player = scene == "strike" ? new Vec3d(2.0, 0, 9.0) : scene == "lost" ? new Vec3d(-5.0, 0, 7.0) : new Vec3d(3.5, 0, 6.0);
        double seconds = scene == "strike" ? 9.0 : scene == "lost" ? 12.0 : 9.0;
        int frames = (int)(seconds * Fps);
        const double dt = 1.0 / 120.0;
        int perFrame = (int)(120 / Fps);
        var json = new StringBuilder();
        json.Append("{\"name\":\"").Append(scene).Append("\",\"fps\":").Append(F(Fps)).Append(",\"lens\":30,\"size\":[960,600],\"samples\":12,")
            .Append("\"eye\":[6,0,1.6],\"target\":[0,0,0.5],\"frames\":[\n");

        // What is shot, and when: (seconds, kind, index). Kind 0 a gill, 1 a leg, 2 the tail cut at that length, 3 the kill.
        var shots = new List<(double at, int kind, int index)>();
        if (scene == "lose")
        {
            shots.Add((2.2, 0, StalkerLayout.RightSide));
            shots.Add((4.0, 1, StalkerLayout.Leg(0, StalkerLayout.RightSide)));
            shots.Add((5.4, 2, StalkerLayout.Trunk + 1));
            shots.Add((7.4, 3, 0));
        }

        double time = 0.0, noise = 0.0;
        Vec3d look = root;
        StalkerOrders orders = default;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int step = 0; step < perFrame; step++)
            {
                time += dt;
                // The person.
                Vec3d walk = Vec3d.Zero;
                if (scene == "strike")
                {
                    // Walks in across its front, and stops.
                    if (time > 0.4 && time < 2.4) walk = new Vec3d(-0.6, 0, -2.4);
                }
                else if (scene == "lost")
                {
                    // Walks, is heard, then creeps well aside without a sound and stands quite still.
                    if (time > 0.4 && time < 2.0) walk = new Vec3d(2.6, 0, -0.4);
                    else if (time >= 2.0 && time < 3.8) walk = new Vec3d(2.4, 0, 1.0);
                }
                else if (time > 0.3 && time < 1.4) walk = new Vec3d(0, 0, -1.2);
                player += walk * dt;
                noise = walk.Magnitude > 0.1 ? Math.Max(0.3, walk.Magnitude / 5.0) : 0.0;
                if (scene == "lost" && time >= 2.0) noise = 0.0;   // creeping, then still
                if (scene == "strike" && time >= 2.4) noise = 0.6;   // shifting their feet: it can still hear them
                if (scene == "lose" && time >= 1.4) noise = 0.5;

                QuatD backTurn = QuatD.AngleAxis(Vec3d.Up, -yaw);
                var senses = new StalkerSenses { Hostile = true, Player = backTurn.Rotate(player - root), Noise = noise, Home = backTurn.Rotate(home - root), North = backTurn.Rotate(Vec3d.Forward) };
                orders = dead ? default : brain.Think(body, senses, dt);
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
                            Velocity = turnNow.Rotate(way) * (1.5 + rng.NextDouble() * 2.0) + Vec3d.Up * (2.5 + rng.NextDouble() * 2.5),
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
                    void LegOff(int leg)
                    {
                        Loose l = Fly(pose.LegPosition[leg], pose.LegRotation[leg], new Vec3d(StalkerLayout.Sign(leg), 0, 0), 0.05);
                        l.Pieces.Add(new Rider { Piece = StalkerLayout.LegPiece, Turn = QuatD.Identity, Scale = One(1) });
                        Ride(l, StalkerLayout.ShinPiece, pose.LegPosition[leg], pose.LegRotation[leg], pose.ShinPosition[leg], pose.ShinRotation[leg], One(1));
                    }
                    void GillOff(int side)
                    {
                        Loose g = Fly(pose.GillPosition[side], pose.GillRotation[side], new Vec3d(StalkerLayout.Sign(side), 0, -0.4), 0.04);
                        g.Pieces.Add(new Rider { Piece = StalkerLayout.GillPiece, Turn = QuatD.Identity, Scale = One(1) });
                    }
                    if (shot.kind == 0 && body.HitGill(shot.index, 0, sever: true).GillLost >= 0)
                    {
                        GillOff(shot.index);
                        gillStumps.Add(shot.index);
                        if (brain.Reel()) animator.Flinch(new Vec3d(-StalkerLayout.Sign(shot.index) * 2.0, 1.0, -1.5));
                        brain.Hurt(senses, senses.Player);
                    }
                    else if (shot.kind == 1 && body.HitLeg(shot.index, 0, sever: true).LegLost >= 0)
                    {
                        LegOff(shot.index);
                        legStumps.Add(shot.index);
                        animator.Flinch(new Vec3d(-StalkerLayout.Sign(shot.index), 0.6, 0) * 1.5);
                        brain.Hurt(senses, senses.Player);
                    }
                    else if (shot.kind == 2)
                    {
                        int had = body.Kept;
                        if (body.HitSeg(shot.index, 0, sever: true).TailCutAt >= 0)
                        {
                            // Everything from the cut back leaves in one stiff piece.
                            int first = shot.index + 1;
                            Loose chunk = Fly(pose.NodePosition[first], pose.NodeRotation[first], new Vec3d(0.8, 0, -1.0), 0.12);
                            for (int seg = shot.index; seg < had; seg++)
                                Ride(chunk, SegName(seg), pose.NodePosition[first], pose.NodeRotation[first], pose.NodePosition[seg + 1], pose.NodeRotation[seg + 1], Wide(seg));
                            chunk.Pieces.Add(new Rider { Piece = StalkerLayout.Stump, At = new Vec3d(0, 0, CutAt), Turn = QuatD.Identity,
                                Scale = One(StalkerLayout.WoundSize(shot.index) / StalkerLayout.StumpRadius) });
                            cutAt = shot.index;
                            animator.Flinch(new Vec3d(0.5, 1.0, 1.0));
                            brain.Hurt(senses, senses.Player);
                        }
                    }
                    else if (shot.kind == 3)
                    {
                        int had = body.Kept;
                        body.HitHead(0, lethal: true);
                        dead = true;
                        for (int leg = 0; leg < StalkerLayout.Legs; leg++) if (!legStumps.Contains(leg)) LegOff(leg);
                        for (int side = 0; side < StalkerLayout.Gills; side++) if (!gillStumps.Contains(side)) GillOff(side);
                        for (int seg = 0; seg < had; seg++)
                        {
                            Loose chunk = Fly(pose.NodePosition[seg + 1], pose.NodeRotation[seg + 1], new Vec3d(rng.NextDouble() * 2 - 1, 0, rng.NextDouble() * 2 - 1), 0.12);
                            chunk.Pieces.Add(new Rider { Piece = SegName(seg), Turn = QuatD.Identity, Scale = Wide(seg) });
                            Vec3d size = One(StalkerLayout.WoundSize(seg) / StalkerLayout.StumpRadius);
                            chunk.Pieces.Add(new Rider { Piece = StalkerLayout.Stump, At = new Vec3d(0, 0, CutAt), Turn = QuatD.Identity, Scale = size });
                            if (seg < had - 1) chunk.Pieces.Add(new Rider { Piece = StalkerLayout.Stump, At = new Vec3d(0, 0, -CutAt), Turn = QuatD.AngleAxis(Vec3d.Up, Math.PI), Scale = size });
                        }
                        Loose head = Fly(pose.NodePosition[0], pose.NodeRotation[0], new Vec3d(0.3, 0, 1), 0.1);
                        head.Pieces.Add(new Rider { Piece = StalkerLayout.HeadPiece, Turn = QuatD.Identity, Scale = One(1) });
                        Ride(head, StalkerLayout.JawPiece, pose.NodePosition[0], pose.NodeRotation[0], pose.JawPosition, pose.JawRotation, One(1));
                        for (int k = 0; k < 2; k++)
                        {
                            Loose meat = Fly(pose.NodePosition[2 + k * 2], QuatD.Identity, new Vec3d(rng.NextDouble() - 0.5, 0, rng.NextDouble() - 0.5), 0.07);
                            meat.Pieces.Add(new Rider { Piece = StalkerLayout.Meat[k], Turn = QuatD.Identity, Scale = One(1) });
                        }
                    }
                }
                if (!dead) animator.Update(body, new StalkerAnimInput { Velocity = orders.Velocity, TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T, HasPlace = orders.HasPlace, Place = orders.Place, Aware = orders.Aware, Took = orders.Took }, dt, pose);

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
            // The camera watches between the animal and the person, from the side and a little above.
            int watch = Math.Min(pose.Nodes - 1, 3);
            Vec3d middle = root + turnRoot.Rotate(pose.NodePosition[watch]);
            middle = middle * 0.65 + player * 0.35;
            look += (new Vec3d(middle.X, 0, middle.Z) - look) * (frame == 0 ? 1.0 : 0.2);
            if (frame > 0) json.Append(",\n");
            json.Append("{\"eye\":[").Append(F(look.X + 6.8)).Append(',').Append(F(look.Z - 1.8)).Append(",2.3],\"target\":[")
                .Append(F(look.X)).Append(',').Append(F(look.Z)).Append(",0.4],\"person\":[").Append(F(player.X)).Append(',').Append(F(player.Z))
                .Append("],\"mode\":\"").Append(dead ? "Dead" : orders.Mode.ToString())
                .Append("\",\"pieces\":[{\"piece\":\"Stump\",\"m\":[0.001,0,0,0,0,0.001,0,0,0,0,0.001,-5,0,0,0,1]}");
            if (!dead)
            {
                for (int i = 0; i < pose.Nodes; i++)
                    Matrix(json, i == 0 ? StalkerLayout.HeadPiece : SegName(i - 1), root + turnRoot.Rotate(pose.NodePosition[i]), turnRoot * pose.NodeRotation[i], i == 0 ? One(1) : Wide(i - 1));
                Matrix(json, StalkerLayout.JawPiece, root + turnRoot.Rotate(pose.JawPosition), turnRoot * pose.JawRotation, One(1));
                for (int leg = 0; leg < StalkerLayout.Legs; leg++)
                {
                    if (!body.LegOn(leg)) continue;
                    Matrix(json, StalkerLayout.LegPiece, root + turnRoot.Rotate(pose.LegPosition[leg]), turnRoot * pose.LegRotation[leg], One(1));
                    Matrix(json, StalkerLayout.ShinPiece, root + turnRoot.Rotate(pose.ShinPosition[leg]), turnRoot * pose.ShinRotation[leg], One(1));
                }
                for (int side = 0; side < StalkerLayout.Gills; side++)
                    if (body.GillOn(side)) Matrix(json, StalkerLayout.GillPiece, root + turnRoot.Rotate(pose.GillPosition[side]), turnRoot * pose.GillRotation[side], One(1));
                foreach (int leg in legStumps)
                {
                    int node = StalkerLayout.NodeOf(leg);
                    QuatD frameQ = turnRoot * pose.NodeRotation[node];
                    Vec3d at = root + turnRoot.Rotate(pose.NodePosition[node] + pose.NodeRotation[node].Rotate(StalkerLayout.HipOf(leg)));
                    Matrix(json, StalkerLayout.Stump, at, frameQ * WalkerAnimator.Look(new Vec3d(StalkerLayout.Sign(leg), -0.2, 0), Vec3d.Up), One(StalkerLayout.LegRadius * 1.3 / StalkerLayout.StumpRadius));
                }
                foreach (int side in gillStumps)
                {
                    QuatD frameQ = turnRoot * pose.NodeRotation[0];
                    Vec3d at = root + turnRoot.Rotate(pose.NodePosition[0] + pose.NodeRotation[0].Rotate(StalkerLayout.GillRootOf(side)));
                    Matrix(json, StalkerLayout.Stump, at, frameQ * WalkerAnimator.Look(new Vec3d(StalkerLayout.Sign(side), 0.2, -0.6), Vec3d.Up), One(0.8));
                }
                if (cutAt >= 0)
                {
                    int node = cutAt;
                    QuatD q = turnRoot * pose.NodeRotation[node];
                    Vec3d at = root + turnRoot.Rotate(pose.NodePosition[node] + pose.NodeRotation[node].Rotate(new Vec3d(0, 0, -CutAt)));
                    Matrix(json, StalkerLayout.Stump, at, q * QuatD.AngleAxis(Vec3d.Up, Math.PI), One(StalkerLayout.WoundSize(Math.Max(0, cutAt - 1)) / StalkerLayout.StumpRadius));
                }
            }
            foreach (Loose piece in loose)
                foreach (Rider rider in piece.Pieces)
                    Matrix(json, rider.Piece, piece.Position + piece.Rotation.Rotate(rider.At), piece.Rotation * rider.Turn, rider.Scale);
            json.Append("]}");
        }
        json.Append("\n]}\n");
        File.WriteAllText(path, json.ToString());
        Console.WriteLine("wrote " + path + ": " + frames + " frames of " + scene + "; lengths " + body.Kept + ", legs " + body.LegCount + ", gills " + body.GillCount + ", mode " + brain.Mode);
        return 0;
    }
}
