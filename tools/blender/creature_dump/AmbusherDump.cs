using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

/// <summary>
/// The Ambusher's own rules run outside Unity. Scenes: "strike" (someone walks up and stands still: the warning, the
/// strike, the hold and the drag toward its hole, letting go, going under), "miss" (they step aside after it has taken
/// aim: it lies spent, goes under, tunnels after them and comes up ahead) and "lose" (a jaw shot off in the warning,
/// feelers, the second jaw, then cut through). Every frame carries its own camera and where the person stands.
/// </summary>
static class AmbusherDump
{
    const double Fps = 15.0;

    sealed class Loose
    {
        public string Piece;
        public Vec3d Position, Velocity, Axis;
        public QuatD Rotation;
        public double Spin, Rest, Wide = 1.0;
        public bool Resting;
    }

    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>Game axes to Blender's: (x, y, z) -> (x, z, y); a piece's own +Z (along) is Blender's +Y, its +Y Blender's +Z.</summary>
    static void Matrix(StringBuilder json, string piece, Vec3d p, QuatD q, double wide = 1.0, double tall = 1.0, double along = 1.0)
    {
        Vec3d gx = q.Rotate(Vec3d.Right) * wide, gy = q.Rotate(Vec3d.Up) * tall, gz = q.Rotate(Vec3d.Forward) * along;
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
        var rules = new AmbusherRules();
        var body = new AmbusherBody(rules.hp);
        var animator = new AmbusherAnimator();
        var brain = new AmbusherBrain(rules);
        var pose = new AmbusherPose();
        var loose = new List<Loose>();
        var rng = new Random(7);
        bool dead = false;
        var jawStumps = new List<int>();

        Vec3d root = Vec3d.Zero;
        double yaw = 0.0;
        bool lose = scene == "lose", miss = scene == "miss";
        var player = new Vec3d(0.6, 0, 9.0);
        double seconds = lose ? 16.5 : miss ? 13.0 : 8.0;
        int frames = (int)(seconds * Fps);
        const double dt = 1.0 / 120.0;
        int perFrame = (int)(120 / Fps);
        var json = new StringBuilder();
        json.Append("{\"name\":\"").Append(scene).Append("\",\"fps\":").Append(F(Fps)).Append(",\"lens\":30,\"size\":[960,600],\"samples\":12,")
            .Append("\"eye\":[6,0,1.6],\"target\":[0,0,0.5],\"frames\":[\n");

        // What is shot, and when: (seconds, kind, index). Kinds: 0 jaw, 2 cut through at a length, 4 a jaw in the first warning,
        // 5 hurt while it holds, 6 a feeler in the second warning (at that share of it).
        var shots = new List<(double at, int kind, int index)>();
        if (lose)
        {
            shots.Add((0, 4, 0));        // the first jaw goes during the first warning, whenever that comes
            shots.Add((0.25, 6, 1));     // feelers, as it warns a second time
            shots.Add((0.45, 6, 3));
            shots.Add((0.3, 0, 1));      // the second jaw, in its third warning: it is thrown out on the ground
            shots.Add((0.55, 2, 4));     // and cut through as it lies there
        }
        else if (!miss) shots.Add((0, 5, 0));   // shot at while it holds: enough to make it let go

        double time = 0.0, hurtWhileHeld = 0.0, sidestep = -1.0, freed = -1.0;
        int coils = 0;
        AmbusherMode was = AmbusherMode.Lurk;
        Vec3d look = new Vec3d(0, 0.6, 1.5), playerWas = player;
        AmbusherOrders orders = default;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int step = 0; step < perFrame; step++)
            {
                time += dt;
                QuatD turnNow = QuatD.AngleAxis(Vec3d.Up, yaw);
                QuatD back = QuatD.AngleAxis(Vec3d.Up, -yaw);

                // The person's own plan.
                Vec3d walk = Vec3d.Zero;
                if (!orders.Holding)
                {
                    if (lose)
                    {
                        // Walks up, backs off out of reach, and stands to shoot.
                        if (time < 2.6 && player.Z > 3.6) walk = new Vec3d(0, 0, -2.4);
                        else if (time > 3.6 && time < 6.2) walk = new Vec3d(1.2, 0, 1.6);
                    }
                    else if (miss)
                    {
                        if (sidestep < 0.0 && player.Z > 3.4) walk = new Vec3d(0, 0, -2.4);
                        if (sidestep < 0.0 && orders.Mode == AmbusherMode.Coil && orders.T > AmbusherBrain.LockAt + 0.05) sidestep = time;
                        // A step aside once it has taken aim, then on past it at a walk.
                        if (sidestep >= 0.0 && time < sidestep + 0.45) walk = new Vec3d(4.6, 0, 0);
                        else if (sidestep >= 0.0 && time > sidestep + 0.8 && time < sidestep + 4.2) walk = new Vec3d(2.2, 0, -1.6);
                    }
                    else if (freed < 0.0 && player.Z > 3.5) walk = new Vec3d(0, 0, -2.4);
                    // Let go of: they get clear.
                    else if (freed >= 0.0 && time > freed + 0.3 && player.Z < 8.5) walk = new Vec3d(0.4, 0, 3.4);
                }
                player += walk * dt;

                if (!dead)
                {
                    var senses = new AmbusherSenses
                    {
                        Hostile = true,
                        Player = back.Rotate(player - root),
                        PlayerVelocity = back.Rotate((player - playerWas) * (1.0 / dt)),
                        Noise = 1.0,
                        ClearLine = true,
                        ClearAbove = true,
                        Home = back.Rotate(Vec3d.Zero - root),
                    };
                    orders = brain.Think(body, senses, dt);
                    yaw += orders.TurnRate * dt;
                    turnNow = QuatD.AngleAxis(Vec3d.Up, yaw);
                    root += turnNow.Rotate(orders.Velocity) * dt;
                    playerWas = player;
                    if (orders.Holding) player += turnNow.Rotate(orders.Pull) * dt;
                    if (orders.Mode != was && orders.Mode == AmbusherMode.Coil) coils++;
                    if (was == AmbusherMode.Hold && orders.Mode != AmbusherMode.Hold) freed = time;
                    was = orders.Mode;
                }
                else orders.Holding = false;

                foreach (var shot in shots.ToArray())
                {
                    bool now = shot.kind == 4 ? orders.Mode == AmbusherMode.Coil && orders.T > 0.35
                        : shot.kind == 5 ? orders.Holding
                        : shot.kind == 6 ? coils >= 2 && orders.Mode == AmbusherMode.Coil && orders.T > shot.at
                        : shot.kind == 0 ? coils >= 3 && orders.Mode == AmbusherMode.Coil && orders.T > shot.at
                        : shot.kind == 2 ? body.JawCount == 0 && orders.Mode == AmbusherMode.Spent && orders.T > shot.at
                        : time >= shot.at;
                    if (!now || dead) continue;
                    void Fly(string piece, Vec3d at, QuatD q, Vec3d way, double rest, double hard = 1.0, double wide = 1.0)
                    {
                        loose.Add(new Loose
                        {
                            Piece = piece, Position = root + turnNow.Rotate(at), Rotation = turnNow * q, Rest = rest, Wide = wide,
                            Velocity = turnNow.Rotate(way) * (2.0 + rng.NextDouble() * 2.0) * hard + Vec3d.Up * (2.0 + rng.NextDouble() * 2.5) * hard,
                            Axis = new Vec3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized, Spin = 3.0 + rng.NextDouble() * 5.0,
                        });
                    }
                    void JawOff(int jaw) => Fly(AmbusherLayout.JawPiece, pose.JawPosition[jaw], pose.JawRotation[jaw], new Vec3d(AmbusherLayout.Sign(jaw), 0, 0.3), 0.04, 1.2);
                    void FeelerOff(int feeler) => Fly(AmbusherLayout.FeelerPiece, pose.FeelerPosition[feeler], pose.FeelerRotation[feeler], new Vec3d(rng.NextDouble() - 0.5, 0, 0.5), 0.02, 0.7);
                    if (shot.kind == 5)
                    {
                        // Shot in the body while it holds: it lets go once it has taken enough.
                        hurtWhileHeld += 30.0 * dt;
                        brain.Hurt(30.0 * dt, body);
                        if (hurtWhileHeld < rules.letGoDamage + 1.0) continue;
                    }
                    shots.Remove(shot);
                    if ((shot.kind == 0 || shot.kind == 4) && body.HitJaw(shot.index, 0, sever: true).JawLost >= 0)
                    {
                        JawOff(shot.index);
                        jawStumps.Add(shot.index);
                        if (brain.Flinch(body)) animator.Flinch(new Vec3d(-AmbusherLayout.Sign(shot.index) * 2.0, 0, -3.0));
                    }
                    else if (shot.kind == 6 && body.HitFeeler(shot.index, 0, sever: true).FeelerLost >= 0) FeelerOff(shot.index);
                    else if (shot.kind == 2)
                    {
                        body.HitSeg(shot.index, 0, sever: true);
                        dead = true;
                        // Whatever is out of the ground comes apart at every joint.
                        for (int i = 0; i < AmbusherLayout.Nodes; i++)
                        {
                            if (pose.NodePosition[i].Y < 0.0) continue;
                            double w = i == 0 ? 1.0 : AmbusherLayout.Scale(i - 1);
                            Fly(i == 0 ? AmbusherLayout.HeadPiece : AmbusherLayout.SegPiece, pose.NodePosition[i], pose.NodeRotation[i],
                                new Vec3d(rng.NextDouble() - 0.5, 0, rng.NextDouble() - 0.5) * 2.0, 0.15, 1.0, w);
                        }
                        for (int jaw = 0; jaw < AmbusherLayout.Jaws; jaw++) if (body.JawOn(jaw)) JawOff(jaw);
                        for (int feeler = 0; feeler < AmbusherLayout.Feelers; feeler++) if (body.FeelerOn(feeler)) FeelerOff(feeler);
                        for (int k = 0; k < 2; k++) Fly(AmbusherLayout.Meat[k], pose.NodePosition[shot.index + 1], QuatD.Identity, new Vec3d(rng.NextDouble() - 0.5, 0, rng.NextDouble() - 0.5), 0.08);
                    }
                }
                if (!dead)
                {
                    QuatD backNow = QuatD.AngleAxis(Vec3d.Up, -yaw);
                    Vec3d local = backNow.Rotate(player - root);
                    bool feels = local.Magnitude < AmbusherBrain.FeelRange(rules, body, 1.0);
                    animator.Update(body, new AmbusherAnimInput { Mode = orders.Mode, T = orders.T, Out = orders.Out, Feels = feels, Player = local, Slack = orders.Slack }, dt, pose);
                }

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
            // The camera watches a point between the hole and the person, from one side.
            Vec3d between = root + (player - root) * 0.4;
            look += (new Vec3d(between.X, 0.7, between.Z) - look) * 0.12;
            if (frame > 0) json.Append(",\n");
            json.Append("{\"eye\":[").Append(F(look.X + 5.6)).Append(',').Append(F(look.Z - 2.2)).Append(",2.0],\"target\":[")
                .Append(F(look.X)).Append(',').Append(F(look.Z)).Append(',').Append(F(look.Y)).Append("],\"person\":[").Append(F(player.X)).Append(',').Append(F(player.Z))
                .Append("],\"pieces\":[{\"piece\":\"Stump\",\"m\":[0.001,0,0,0,0,0.001,0,0,0,0,0.001,-5,0,0,0,1]}");
            Vec3d At(Vec3d p) => root + turnRoot.Rotate(p);
            double mound = dead ? 1.0 : Math.Clamp(pose.Mound, 0.0, 1.0);
            if (mound > 0.02) Matrix(json, AmbusherLayout.RimPiece, root, turnRoot, 0.75 + 0.25 * mound, mound, 0.75 + 0.25 * mound);
            if (!dead && !brain.Buried)
            {
                for (int i = 0; i < AmbusherLayout.Nodes; i++)
                {
                    if (pose.NodePosition[i].Y <= -0.3) continue;
                    double w = i == 0 ? 1.0 : AmbusherLayout.Scale(i - 1);
                    Matrix(json, i == 0 ? AmbusherLayout.HeadPiece : AmbusherLayout.SegPiece, At(pose.NodePosition[i]), turnRoot * pose.NodeRotation[i], w, w);
                }
                if (pose.NodePosition[0].Y > -0.3)
                {
                    for (int jaw = 0; jaw < AmbusherLayout.Jaws; jaw++)
                        if (body.JawOn(jaw)) Matrix(json, AmbusherLayout.JawPiece, At(pose.JawPosition[jaw]), turnRoot * pose.JawRotation[jaw]);
                    for (int feeler = 0; feeler < AmbusherLayout.Feelers; feeler++)
                        if (body.FeelerOn(feeler)) Matrix(json, AmbusherLayout.FeelerPiece, At(pose.FeelerPosition[feeler]), turnRoot * pose.FeelerRotation[feeler]);
                    QuatD headQ = turnRoot * pose.NodeRotation[0];
                    foreach (int jaw in jawStumps)
                    {
                        double s = AmbusherLayout.Sign(jaw), size = AmbusherLayout.JawThick * 1.3 / AmbusherLayout.StumpRadius;
                        Matrix(json, AmbusherLayout.Stump, At(pose.NodePosition[0] + pose.NodeRotation[0].Rotate(AmbusherLayout.HingeOf(jaw))),
                            headQ * WalkerAnimator.Look(new Vec3d(s * 0.5, 0, 1).Normalized, Vec3d.Up), size, size, size);
                    }
                }
            }
            foreach (Loose piece in loose) Matrix(json, piece.Piece, piece.Position, piece.Rotation, piece.Wide, piece.Wide);
            json.Append("]}");
            if (frame % 8 == 0) Console.WriteLine(F(time) + " s: " + brain.Mode + " t " + F(orders.T) + " out " + F(orders.Out) + (orders.Holding ? " HOLDING" : "") + " head " + F(pose.NodePosition[0].Y)
                + " root " + F(root.X) + ", " + F(root.Z) + " person " + F(player.X) + ", " + F(player.Z));
        }
        json.Append("\n]}\n");
        File.WriteAllText(path, json.ToString());
        Console.WriteLine("wrote " + path + ": " + frames + " frames of " + scene + "; jaws " + body.JawCount + ", feelers " + body.FeelerCount + ", alive " + body.Alive + ", mode " + brain.Mode);
        return 0;
    }
}
