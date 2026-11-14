using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;

static class Program
{
    static readonly StringBuilder Json = new StringBuilder();
    static bool _first = true;

    static void Shot(string name, BodyPose pose, HashSet<BodyPartId> gone, double[] eye, double[] target, int w, int h, int samples, double lens)
    {
        if (!_first) Json.Append(",\n");
        _first = false;
        Json.Append("{\"name\":\"").Append(name).Append("\",\"eye\":[").Append(F(eye)).Append("],\"target\":[").Append(F(target))
            .Append("],\"size\":[").Append(w).Append(',').Append(h).Append("],\"samples\":").Append(samples).Append(",\"lens\":").Append(lens.ToString(CultureInfo.InvariantCulture)).Append(",\"parts\":{");
        bool firstPart = true;
        for (int i = 0; i < BodyLayout.Count; i++)
        {
            var id = (BodyPartId)i;
            if (gone != null && gone.Contains(id)) continue;
            BodyBone bone = BodySkeleton.BoneOf(id);
            pose.World(bone, out Vec3d p, out QuatD q);
            Vec3d origin = p + q.Rotate(BodySkeleton.PartOrigin(id) - BodySkeleton.RestPosition(bone));
            Vec3d x = q.Rotate(Vec3d.Right), y = q.Rotate(Vec3d.Up), z = q.Rotate(Vec3d.Forward);
            double[] m = { x.X, y.X, z.X, origin.X, x.Y, y.Y, z.Y, origin.Y, x.Z, y.Z, z.Z, origin.Z, 0, 0, 0, 1 };
            if (!firstPart) Json.Append(',');
            firstPart = false;
            Json.Append('"').Append(id.ToString()).Append("\":[").Append(F(m)).Append(']');
        }
        Json.Append("}}");
    }

    /// <summary>One pose as the "parts" table alone, for pictures that hold more than one body.</summary>
    static string Parts(BodyPose pose)
    {
        var json = new StringBuilder("{");
        for (int i = 0; i < BodyLayout.Count; i++)
        {
            var id = (BodyPartId)i;
            BodyBone bone = BodySkeleton.BoneOf(id);
            pose.World(bone, out Vec3d p, out QuatD q);
            Vec3d origin = p + q.Rotate(BodySkeleton.PartOrigin(id) - BodySkeleton.RestPosition(bone));
            Vec3d x = q.Rotate(Vec3d.Right), y = q.Rotate(Vec3d.Up), z = q.Rotate(Vec3d.Forward);
            double[] m = { x.X, y.X, z.X, origin.X, x.Y, y.Y, z.Y, origin.Y, x.Z, y.Z, z.Z, origin.Z, 0, 0, 0, 1 };
            if (i > 0) json.Append(',');
            json.Append('"').Append(id.ToString()).Append("\":[").Append(F(m)).Append(']');
        }
        return json.Append('}').ToString();
    }

    /// <summary>
    /// The four kinds of enemy, each standing, ready, walking and running its own way at its own size and
    /// speed. Read by build_enemy_types.py -- --kinds file.json --preview folder.
    /// </summary>
    static void Kinds(string path)
    {
        const double dt = 1.0 / 60.0;
        // The model, the size it is drawn at, and how fast it moves beside the trooper (Enemies/EnemyRules.cs).
        var kinds = new (string model, double size, double quick)[] { ("EnemyRunner", 0.85, 1.5), ("Enemy", 1.0, 1.0), ("EnemyMarksman", 1.2, 0.9), ("EnemyBruiser", 1.3, 0.8) };
        var json = new StringBuilder("{\"kinds\":[\n");
        for (int k = 0; k < kinds.Length; k++)
        {
            (string model, double size, double quick) = kinds[k];
            BodyAnimInput In(double share, bool moving, double aim = 0)
            {
                BodyAnimInput input = Input(MovementState.Intact, 0, 0, moving);
                input.WalkSpeed = 2.0 * quick;
                input.SprintSpeed = 4.6 * quick;
                input.ForwardSpeed = moving ? input.WalkSpeed + (input.SprintSpeed - input.WalkSpeed) * share : 0.0;
                input.Aim = aim;
                return input;
            }
            BodyAnimator New() => new BodyAnimator(7, BodyStyle.For(model)) { Size = size };
            string Moving(double share, double goal)
            {
                BodyAnimator a = New();
                for (int i = 0; i < 200; i++) a.Update(dt, In(share, true));
                for (int guard = 0; guard < 600 && Math.Abs(((a.Gait - goal + 1.5) % 1.0) - 0.5) > 0.03; guard++) a.Update(dt, In(share, true));
                return Parts(a.Pose);
            }
            BodyAnimator still = New();
            for (int i = 0; i < 150; i++) still.Update(dt, In(0, false));
            BodyAnimator ready = New();
            for (int i = 0; i < 150; i++) ready.Update(dt, In(0, false, 1.0));
            if (k > 0) json.Append(",\n");
            json.Append("{\"model\":\"").Append(model).Append("\",\"scale\":").Append(size.ToString(CultureInfo.InvariantCulture))
                .Append(",\"idle\":").Append(Parts(still.Pose)).Append(",\"ready\":").Append(Parts(ready.Pose))
                .Append(",\"walk\":").Append(Moving(0.0, 0.18)).Append(",\"run\":").Append(Moving(1.0, 0.18)).Append('}');
        }
        File.WriteAllText(path, json.Append("\n]}").ToString());
        Console.WriteLine("wrote " + path);
    }

    static string F(double[] v)
    {
        var parts = new string[v.Length];
        for (int i = 0; i < v.Length; i++) parts[i] = v[i].ToString("0.#####", CultureInfo.InvariantCulture);
        return string.Join(",", parts);
    }

    static BodyAnimInput Input(MovementState state, double speed, double phase = 0, bool moving = true)
    {
        return new BodyAnimInput { State = state, ForwardSpeed = speed, WalkSpeed = 2.0, SprintSpeed = 4.6, Phase = phase, Moving = moving,
            LeftArmWorks = true, RightArmWorks = true, StandingLeg = 0, GunSide = 1 };
    }

    static HashSet<BodyPartId> Gone(params BodyPartId[] ids) => new HashSet<BodyPartId>(ids);

    static void Main(string[] args)
    {
        string mode = args.Length > 1 ? args[1] : "sheet";
        if (mode == "kinds") { Kinds(args[0]); return; }
        const double dt = 1.0 / 60.0;
        double[] side = { 4.2, 1.0, 0.25 }, sideT = { 0, 0.92, 0.1 };
        double[] quarter = { 2.5, 1.5, 3.0 }, quarterT = { 0, 0.9, 0 };
        double[] low = { 2.2, 1.0, 2.6 }, lowT = { 0, 0.3, 0.2 };
        int W = 420, H = 520, S = 36;

        if (mode == "sheet")
        {
            // Idle
            var a = new BodyAnimator(7);
            for (int i = 0; i < 150; i++) a.Update(dt, Input(MovementState.Intact, 0, 0, false));
            Shot("a0_idle", a.Pose, null, quarter, quarterT, W, H, S, 62);
            // Walk: 6 frames across a stride
            a = new BodyAnimator(7);
            for (int i = 0; i < 180; i++) a.Update(dt, Input(MovementState.Intact, 2.0));
            for (int f = 0; f < 6; f++)
            {
                double goal = f / 6.0;
                for (int guard = 0; guard < 400 && Math.Abs(((a.Gait - goal + 1.5) % 1.0) - 0.5) > 0.012; guard++) a.Update(dt, Input(MovementState.Intact, 2.0));
                Shot("b" + f + "_walk", a.Pose, null, side, sideT, W, H, S, 62);
                a.Update(dt, Input(MovementState.Intact, 2.0));
            }
            a = new BodyAnimator(7);
            for (int i = 0; i < 180; i++) a.Update(dt, Input(MovementState.Intact, 4.6));
            for (int f = 0; f < 6; f++)
            {
                double goal = f / 6.0;
                for (int guard = 0; guard < 400 && Math.Abs(((a.Gait - goal + 1.5) % 1.0) - 0.5) > 0.025; guard++) a.Update(dt, Input(MovementState.Intact, 4.6));
                Shot("c" + f + "_run", a.Pose, null, side, sideT, W, H, S, 62);
                a.Update(dt, Input(MovementState.Intact, 4.6));
            }
            // Hit from the front
            a = new BodyAnimator(7);
            for (int i = 0; i < 120; i++) a.Update(dt, Input(MovementState.Intact, 0, 0, false));
            a.Flinch(0.3, -1, 0.8, 0.5, 1.6);
            for (int i = 0; i < 6; i++) a.Update(dt, Input(MovementState.Intact, 0, 0, false));
            Shot("d0_hit", a.Pose, null, quarter, quarterT, W, H, S, 62);
            // Aim stance
            a = new BodyAnimator(7);
            for (int i = 0; i < 120; i++) { var inp = Input(MovementState.Intact, 0, 0, false); inp.Aim = 1; a.Update(dt, inp); }
            Shot("d1_ready", a.Pose, null, quarter, quarterT, W, H, S, 62);
            // Hop: wind-up and top
            var goneLeg = Gone(BodyPartId.RightShin, BodyPartId.RightFoot);
            foreach (var (tag, ph) in new[] { ("d2_hop_windup", 4.5), ("d3_hop_air", 1.4) })
            {
                a = new BodyAnimator(7);
                for (int i = 0; i < 90; i++) a.Update(dt, Input(MovementState.OneLeg, 1.2, ph));
                Shot(tag, a.Pose, goneLeg, quarter, quarterT, W, H, S, 62);
            }
            // Crawl
            var noLegs = Gone(BodyPartId.RightShin, BodyPartId.RightFoot, BodyPartId.LeftShin, BodyPartId.LeftFoot, BodyPartId.LeftThigh);
            foreach (var (tag, ph) in new[] { ("e0_crawl", 0.3), ("e1_crawl", 3.4) })
            {
                a = new BodyAnimator(7);
                for (int i = 0; i < 90; i++) a.Update(dt, Input(MovementState.Crawl, 0.65, ph));
                Shot(tag, a.Pose, noLegs, low, lowT, W, H, S, 50);
            }
            var stub = Gone(BodyPartId.RightShin, BodyPartId.RightFoot, BodyPartId.LeftShin, BodyPartId.LeftFoot, BodyPartId.LeftThigh, BodyPartId.RightThigh,
                BodyPartId.LeftUpperArm, BodyPartId.LeftForearm, BodyPartId.LeftHand, BodyPartId.RightForearm, BodyPartId.RightHand);
            a = new BodyAnimator(7);
            for (int i = 0; i < 90; i++) a.Update(dt, Input(MovementState.Wriggle, 0.28, 1.0));
            Shot("e2_wriggle", a.Pose, stub, low, lowT, W, H, S, 50);
        }
        else
        {
            // A looping clip for a moving picture: walk then run, three-quarter view.
            double speed = mode == "run" ? 4.6 : 2.0;
            int frames = mode == "run" ? 12 : 16;
            var a = new BodyAnimator(7);
            for (int i = 0; i < 240; i++) a.Update(dt, Input(MovementState.Intact, speed));
            for (int f = 0; f < frames; f++)
            {
                double goal = f / (double)frames;
                for (int guard = 0; guard < 600 && Math.Abs(((a.Gait - goal + 1.5) % 1.0) - 0.5) > (mode == "run" ? 0.024 : 0.015); guard++) a.Update(dt, Input(MovementState.Intact, speed));
                Shot(mode + f.ToString("00"), a.Pose, null, new[] { 3.0, 1.3, 2.4 }, new[] { 0, 0.9, 0.0 }, 360, 460, 28, 62);
                a.Update(dt, Input(MovementState.Intact, speed));
            }
        }
        File.WriteAllText(args[0], "{\"shots\":[\n" + Json + "\n]}");
        Console.WriteLine("wrote " + args[0]);
    }
}
