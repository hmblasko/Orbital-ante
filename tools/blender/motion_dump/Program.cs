using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;

/// <summary>
/// Runs the enemy's animation rules (BodyAnimator) through short scenes outside Unity, moving the body the way
/// MannequinLocomotion and MannequinCombat would, and writes every frame to JSON for
/// tools/blender/preview_enemy_motion.py to render as a GIF. The scenes are a stand-in for the game's own
/// movement code (simplified: flat ground, no physics, the gun is a box), so the pictures are not game footage.
///
///     dotnet run -c Release -- out.json disarm|hobble|arm|walk|hunch [metres per second] [old]
/// </summary>
static partial class Program
{
    /// <summary>What a scene asks of the body each frame (the same fields as BodyAnimInput).</summary>
    public struct Sim
    {
        public MovementState State;
        public double Speed, Turn, Phase;
        public bool Moving;
        public int StandingLeg, GunSide;
        public bool LeftArm, RightArm;
        public double Aim, LossLeft, LossRight, Stump, Scramble, Reach, Teeter, Tactical;
        public int ReachSide;
        public bool HasLook;
        public Vec3d Look;
    }

    public interface IBody
    {
        void Step(double dt, in Sim s);
        BodyPose Pose { get; }
        void Startle(double x, double z);
        void Wounded(int side, double severity, bool arm = false);
        void Flinch(double x, double z, double height, double side, double strength);
        void Stumble(double side);
        bool Stumbling { get; }
        /// <summary>How far into the tactical hunch it is (0 for an animator from before the hunch).</summary>
        double Tactical { get; }
    }

    sealed class NewBody : IBody
    {
        readonly BodyAnimator _a;
        public NewBody(int seed) { _a = new BodyAnimator(seed); }
        public BodyPose Pose => _a.Pose;
        public bool Stumbling => _a.Stumbling;
        public double Tactical => _a.Tactical;
        public void Startle(double x, double z) => _a.Startle(x, z);
        public void Wounded(int side, double severity, bool arm = false) => _a.Wounded(side, severity, arm);
        public void Flinch(double x, double z, double h, double side, double k) => _a.Flinch(x, z, h, side, k);
        public void Stumble(double side) => _a.Stumble(side);
        public void Step(double dt, in Sim s)
        {
            var input = new BodyAnimInput
            {
                State = s.State, ForwardSpeed = s.Speed, TurnRate = s.Turn, Phase = s.Phase, Moving = s.Moving,
                WalkSpeed = 2.0, SprintSpeed = 4.6, StandingLeg = s.StandingLeg, LeftArmWorks = s.LeftArm, RightArmWorks = s.RightArm,
                Aim = s.Aim, GunSide = s.GunSide, ArmLossLeft = s.LossLeft, ArmLossRight = s.LossRight, StumpLength = s.Stump,
                Scramble = s.Scramble, Reach = s.Reach, ReachSide = s.ReachSide, Teeter = s.Teeter, Tactical = s.Tactical,
                HasLook = s.HasLook, LookX = s.Look.X, LookY = s.Look.Y, LookZ = s.Look.Z,
            };
            _a.Update(dt, input);
        }
    }

    /// <summary>Set by an optional second file to the animation as it was before, for side-by-side pictures.</summary>
    static Func<int, IBody> MakeOld;
    static bool _old;

    static IBody MakeBody(int seed) => _old && MakeOld != null ? MakeOld(seed) : new NewBody(seed);

    // ------------------------------------------------------------------ writing frames

    const double Dt = 1.0 / 60.0;
    static int Every = 3;   // 20 pictures a second ("dense" writes all 60)

    static readonly StringBuilder Json = new StringBuilder();
    static bool _firstFrame = true;

    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    static void Frame(IBody body, Vec3d at, double headingDegrees, HashSet<BodyPartId> gone, List<(Vec3d at, double heading)> guns, string caption, double heldPitch = double.NaN)
    {
        if (!_firstFrame) Json.Append(",\n");
        _firstFrame = false;
        QuatD turn = QuatD.AngleAxis(Vec3d.Up, headingDegrees * Math.PI / 180.0);
        Json.Append("{\"caption\":\"").Append(caption).Append("\",\"parts\":{");
        bool first = true;
        for (int i = 0; i < BodyLayout.Count; i++)
        {
            var id = (BodyPartId)i;
            if (gone != null && gone.Contains(id)) continue;
            BodyBone bone = BodySkeleton.BoneOf(id);
            body.Pose.World(bone, out Vec3d p, out QuatD q);
            Vec3d origin = p + q.Rotate(BodySkeleton.PartOrigin(id) - BodySkeleton.RestPosition(bone));
            origin = at + turn.Rotate(origin);
            QuatD r = turn * q;
            Vec3d x = r.Rotate(Vec3d.Right), y = r.Rotate(Vec3d.Up), z = r.Rotate(Vec3d.Forward);
            double[] m = { x.X, y.X, z.X, origin.X, x.Y, y.Y, z.Y, origin.Y, x.Z, y.Z, z.Z, origin.Z, 0, 0, 0, 1 };
            if (!first) Json.Append(',');
            first = false;
            Json.Append('"').Append(id).Append("\":[");
            for (int k = 0; k < 16; k++) { if (k > 0) Json.Append(','); Json.Append(F(m[k])); }
            Json.Append(']');
        }
        Json.Append("},\"guns\":[");
        for (int g = 0; g < guns.Count; g++)
        {
            if (g > 0) Json.Append(',');
            Json.Append("{\"at\":[").Append(F(guns[g].at.X)).Append(',').Append(F(guns[g].at.Y)).Append(',').Append(F(guns[g].at.Z))
                .Append("],\"heading\":").Append(F(guns[g].heading));
            if (!double.IsNaN(heldPitch)) Json.Append(",\"pitch\":").Append(F(heldPitch));
            Json.Append('}');
        }
        Json.Append("]}");
    }

    static Vec3d Palm(IBody body, int side, Vec3d at, double heading)
    {
        body.Pose.World(BodySkeleton.Hand(side), out Vec3d p, out QuatD q);
        p = p + q.Rotate(new Vec3d(side == BodySkeleton.Left ? -0.07 : 0.07, 0, 0));
        return at + QuatD.AngleAxis(Vec3d.Up, heading * Math.PI / 180.0).Rotate(p);
    }

    /// <summary>A point in the world, in the body's own space.</summary>
    static Vec3d Local(Vec3d world, Vec3d at, double heading) =>
        QuatD.AngleAxis(Vec3d.Up, -heading * Math.PI / 180.0).Rotate(world - at);

    static double TurnToward(ref double heading, Vec3d from, Vec3d to, double rate)
    {
        double want = Math.Atan2(to.X - from.X, to.Z - from.Z) * 180.0 / Math.PI;
        double diff = ((want - heading) % 360.0 + 540.0) % 360.0 - 180.0;
        double stepTurn = Math.Max(-rate * Dt, Math.Min(rate * Dt, diff));
        heading += stepTurn;
        return stepTurn / Dt;
    }

    static Vec3d Ahead(double heading) => new Vec3d(Math.Sin(heading * Math.PI / 180.0), 0, Math.Cos(heading * Math.PI / 180.0));

    // ------------------------------------------------------------------ scenes

    /// <summary>The gun is shot out of its hands: startled, a scramble after it, a crouch to snatch it up.</summary>
    static void Disarm()
    {
        IBody b = MakeBody(7);
        var at = new Vec3d(0, 0, 0);
        double heading = 200.0;   // facing the camera, a little to one side
        var s = new Sim { State = MovementState.Intact, LeftArm = true, RightArm = true, GunSide = BodySkeleton.Right, Aim = 1, ReachSide = BodySkeleton.Right };
        for (int i = 0; i < 120; i++) b.Step(Dt, s);
        // Where the gun lands: knocked back and to the side, about three metres away.
        Vec3d land = at + new Vec3d(2.2, 0.04, 2.0);
        Vec3d from = Vec3d.Zero;
        double t = 0, knocked = 0.4, gunLanded = 0, startledUntil = 0, grabAt = -1, heldAt = -1, oldWalkUntil = 0;
        bool flying = false, onGround = false, held = true;
        var guns = new List<(Vec3d, double)>();
        int frame = 0;
        while (t < 4.6)
        {
            double turnRate = 0;
            s.Speed = 0; s.Moving = false; s.HasLook = false; s.Scramble = 0; s.Reach = 0;
            if (held && t >= knocked && heldAt < 0)
            {
                // Shot out of the hand.
                held = false; flying = true;
                from = Palm(b, BodySkeleton.Right, at, heading);
                Vec3d dir = Local(land, at, heading);
                b.Flinch(0.3, 1.0, 0.75, 0.6, 1.2);
                if (!_old) b.Startle(dir.X, dir.Z);
                startledUntil = t + 0.55;
                oldWalkUntil = t + 0.8;
                s.Aim = 0; s.GunSide = -1;
            }
            Vec3d gunAt = land;
            if (flying)
            {
                double k = Math.Min(1.0, (t - knocked) / 0.5);
                gunAt = from + (land - from) * k + new Vec3d(0, 0.7 * Math.Sin(Math.PI * k), 0);
                if (k >= 1.0) { flying = false; onGround = true; gunLanded = t; }
            }
            if (!held && heldAt < 0 && t >= knocked)
            {
                Vec3d flat = new Vec3d(land.X - at.X, 0, land.Z - at.Z);
                double distance = flat.Magnitude;
                if (!_old)
                {
                    if (t < startledUntil)
                    {
                        // Rooted to the spot, eyes on the gun, turning slowly toward it.
                        s.HasLook = true; s.Look = Local(gunAt, at, heading);
                        turnRate = TurnToward(ref heading, at, gunAt, 220.0 * 0.6);
                    }
                    else if (grabAt < 0 && distance > 1.1)
                    {
                        s.Scramble = 1; s.HasLook = true; s.Look = Local(gunAt, at, heading);
                        turnRate = TurnToward(ref heading, at, land, 220.0);
                        double angle = Math.Abs(Math.Atan2(land.X - at.X, land.Z - at.Z) * 180 / Math.PI - heading) % 360;
                        if (angle > 180) angle = 360 - angle;
                        s.Speed = distance > 1.2 ? 4.6 : 2.0;
                        if (angle > 70) s.Speed *= 0.25;
                        if (b.Stumbling) s.Speed *= 0.3;
                        s.Moving = true;
                    }
                    else
                    {
                        if (grabAt < 0) grabAt = t + 0.4;
                        s.Reach = 1; s.HasLook = true; s.Look = Local(gunAt, at, heading);
                        turnRate = TurnToward(ref heading, at, land, 220.0 * 0.6);
                        if (t >= grabAt) { heldAt = t; held = true; }
                    }
                }
                else
                {
                    // As it was: wanders on for a moment, then walks over and the gun is simply in its hand.
                    if (t < oldWalkUntil) { s.Speed = 2.0; s.Moving = true; }
                    else if (distance > 1.1)
                    {
                        turnRate = TurnToward(ref heading, at, land, 220.0);
                        s.Speed = distance > 4.0 ? 4.6 : 2.0;
                        s.Moving = true;
                    }
                    else { heldAt = t; held = true; }
                }
            }
            if (held && heldAt >= 0)
            {
                // Back up with the gun, turning to face the camera.
                turnRate = TurnToward(ref heading, at, new Vec3d(-4, 0, -9), 220.0);
                s.Aim = t - heldAt > 0.3 ? 1 : 0; s.GunSide = BodySkeleton.Right;
            }
            s.Turn = turnRate;
            at = at + Ahead(heading) * (s.Speed * Dt);
            b.Step(Dt, s);
            if (frame++ % Every == 0)
            {
                guns.Clear();
                if (held) guns.Add((Palm(b, BodySkeleton.Right, at, heading), heading));
                else if (flying || onGround) guns.Add((gunAt, 40.0));
                Frame(b, at, heading, null, guns, Caption(t, knocked, startledUntil, grabAt, heldAt));
            }
            t += Dt;
        }
    }

    static string Caption(double t, double knocked, double startledUntil, double grabAt, double heldAt)
    {
        if (t < knocked) return "aiming";
        if (heldAt >= 0 && t >= heldAt) return "back up with it";
        if (_old) return t < knocked + 0.8 ? "gun shot away" : "walks to it";
        if (t < startledUntil) return "startled";
        if (grabAt >= 0) return "snatches it up";
        return "scrambles for it";
    }

    /// <summary>Walking, then the right leg is shot off at the thigh: a lurch, then the hobble.</summary>
    static void Hobble()
    {
        IBody b = MakeBody(3);
        var at = new Vec3d(-1.2, 0, 4.0);
        double heading = 168.0;
        var s = new Sim { State = MovementState.Intact, LeftArm = true, RightArm = true, GunSide = -1, StandingLeg = BodySkeleton.Left };
        var rng = new Random(5);
        var gone = new HashSet<BodyPartId>();
        for (int i = 0; i < 120; i++) { s.Speed = 2.0; s.Moving = true; b.Step(Dt, s); }
        double t = 0, lost = 0.6, phase = 0, hopRate = 1, teeterUntil = -1;
        int hopIndex = 0, frame = 0;
        while (t < 6.5)
        {
            if (t >= lost && s.State == MovementState.Intact)
            {
                s.State = MovementState.OneLeg;
                gone.Add(BodyPartId.RightThigh); gone.Add(BodyPartId.RightShin); gone.Add(BodyPartId.RightFoot);
                s.Stump = 0;
                b.Flinch(0.2, -1.0, 0.2, 0.4, 2.5);
                if (!_old) b.Wounded(BodySkeleton.Right, 1.0);
            }
            double speed;
            if (s.State == MovementState.Intact) { speed = 2.0; s.Speed = speed; }
            else
            {
                double cadence = 1.7;
                bool teeter = false;
                if (!_old)
                {
                    cadence *= hopRate;
                    int hop = (int)Math.Floor(phase / (Math.PI * 2));
                    if (hop != hopIndex)
                    {
                        hopIndex = hop;
                        hopRate = 0.82 + rng.NextDouble() * 0.36;
                        if (rng.NextDouble() < 0.10 * 2.0) teeterUntil = t + 0.35 + rng.NextDouble() * 0.4;   // twice the game's chance, so it shows in six seconds
                        else if (rng.NextDouble() < 0.03 * 2.0) b.Stumble(rng.NextDouble() < 0.5 ? -1 : 1);
                    }
                    teeter = t < teeterUntil;
                    if (teeter) cadence = 0;
                }
                phase += Dt * cadence * Math.PI * 2;
                double pulse = Math.Max(0.0, Math.Sin(phase)) * Math.PI;
                speed = teeter ? 0 : 1.2 * pulse;
                if (!_old && b.Stumbling) speed *= 0.3;
                s.Speed = speed;
                s.Phase = phase;
                s.Teeter = teeter ? 1 : 0;
            }
            s.Moving = true;
            at = at + Ahead(heading) * (speed * Dt);
            b.Step(Dt, s);
            if (frame++ % Every == 0) Frame(b, at, heading, gone, new List<(Vec3d, double)>(), t < lost ? "walking" : t < lost + 1.2 ? "leg shot off" : "hobbling on");
            t += Dt;
        }
    }

    /// <summary>Standing, then the left arm is shot off at the shoulder: it reels, holds the wound, walks on hurt.</summary>
    static void Arm()
    {
        IBody b = MakeBody(9);
        var at = new Vec3d(0, 0, 0);
        double heading = 205.0;
        var s = new Sim { State = MovementState.Intact, LeftArm = true, RightArm = true, GunSide = -1 };
        var gone = new HashSet<BodyPartId>();
        for (int i = 0; i < 120; i++) b.Step(Dt, s);
        double t = 0, lost = 0.5;
        int frame = 0;
        while (t < 4.8)
        {
            if (t >= lost && !gone.Contains(BodyPartId.LeftUpperArm))
            {
                gone.Add(BodyPartId.LeftUpperArm); gone.Add(BodyPartId.LeftForearm); gone.Add(BodyPartId.LeftHand);
                s.LeftArm = false; s.LossLeft = 1.0;
                b.Flinch(1.0, 0.3, 0.8, -0.9, 2.5);
                if (!_old) b.Wounded(BodySkeleton.Left, 1.0, arm: true);
            }
            bool walking = t > 2.0;
            s.Speed = walking ? 1.3 : 0; s.Moving = walking;
            at = at + Ahead(heading) * (s.Speed * Dt);
            b.Step(Dt, s);
            if (frame++ % Every == 0) Frame(b, at, heading, gone, new List<(Vec3d, double)>(), t < lost ? "standing" : t < 2.0 ? "arm shot off" : "walks on, hurt");
            t += Dt;
        }
    }

    /// <summary>Walking straight on at one speed (metres per second, from the third argument or 2.0), for judging the plain walk.</summary>
    static void Walk(double speed)
    {
        IBody b = MakeBody(5);
        var at = new Vec3d(0, 0, 0);
        double heading = 200.0;
        var s = new Sim { State = MovementState.Intact, LeftArm = true, RightArm = true, GunSide = -1 };
        for (int i = 0; i < 60; i++) b.Step(Dt, s);
        double t = 0;
        int frame = 0;
        while (t < 4.6)
        {
            s.Speed = speed * Smooth01(t / 0.8);
            s.Moving = true;
            at = at + Ahead(heading) * (s.Speed * Dt);
            b.Step(Dt, s);
            if (frame++ % Every == 0) Frame(b, at, heading, null, new List<(Vec3d, double)>(), speed < 3.0 ? "walking" : "running");
            t += Dt;
        }
    }

    /// <summary>
    /// A rifle held in both hands the way MannequinCombat.PoseWeapon holds it in the game: the grip in front of the
    /// right shoulder pointing along <paramref name="aim"/> (the body's own space), and both arms bent to reach it
    /// (the same two-bone solve, elbows down and out). Returns the grip, in the body's own space.
    /// </summary>
    static Vec3d HoldRifle(IBody body, Vec3d aim)
    {
        BodyPose pose = body.Pose;
        int side = BodySkeleton.Right;
        pose.World(BodySkeleton.UpperArm(side), out Vec3d shoulder, out _);
        Vec3d grip = shoulder + aim * 0.30 + new Vec3d(0, -0.10, 0) + new Vec3d(-0.07, 0, 0);
        Solve(pose, side, grip);
        // The support hand on the fore-end, about a forearm's length further along.
        Solve(pose, BodySkeleton.Left, grip + aim * 0.30 + new Vec3d(0, 0.02, 0));
        return grip;
    }

    static void Solve(BodyPose pose, int side, Vec3d target)
    {
        BodyBone upper = BodySkeleton.UpperArm(side), fore = BodySkeleton.Forearm(side), hand = BodySkeleton.Hand(side);
        pose.World(upper, out Vec3d shoulder, out _);
        pose.World(fore, out Vec3d elbowRest, out _);
        pose.World(hand, out Vec3d wrist, out _);
        double upperLength = Vec3d.Distance(shoulder, elbowRest);
        double foreLength = Vec3d.Distance(elbowRest, wrist) + 0.07;
        Vec3d to = target - shoulder;
        double distance = Math.Max(Math.Abs(upperLength - foreLength) + 0.01, Math.Min(upperLength + foreLength - 0.005, to.Magnitude));
        Vec3d dir = to * (1.0 / Math.Max(1e-6, to.Magnitude));
        double cos = (upperLength * upperLength + distance * distance - foreLength * foreLength) / (2 * upperLength * distance);
        double angle = Math.Acos(Math.Max(-1, Math.Min(1, cos)));
        Vec3d outward = new Vec3d(side == BodySkeleton.Left ? -1 : 1, 0, 0);
        Vec3d bend = (new Vec3d(0, -1, 0) + outward * 0.6);
        Vec3d axis = Vec3d.Cross(dir, bend * (1.0 / bend.Magnitude));
        axis = axis * (1.0 / Math.Max(1e-6, axis.Magnitude));
        Vec3d upperDir = QuatD.AngleAxis(axis, angle).Rotate(dir);
        Vec3d elbow = shoulder + upperDir * upperLength;
        Vec3d foreDir = shoulder + dir * distance - elbow;
        foreDir = foreDir * (1.0 / Math.Max(1e-6, foreDir.Magnitude));
        Vec3d boneAxis = side == BodySkeleton.Left ? new Vec3d(-1, 0, 0) : new Vec3d(1, 0, 0);
        QuatD upperWorld = QuatD.FromTo(boneAxis, upperDir), foreWorld = QuatD.FromTo(boneAxis, foreDir);
        pose.World((BodyBone)BodySkeleton.Parent(upper), out _, out QuatD parent);
        pose.Local[(int)upper] = (parent.Inverse * upperWorld).Normalized;
        pose.Local[(int)fore] = (upperWorld.Inverse * foreWorld).Normalized;
        pose.Local[(int)hand] = QuatD.Identity;
    }

    /// <summary>
    /// The tactical hunch: patrolling upright with the rifle low, then it sees you, drops into the hunch and
    /// advances, gun up, and stops to aim. With "old" it does the same thing without the hunch (the game before it).
    /// </summary>
    static void Hunch()
    {
        IBody b = MakeBody(5);
        var at = new Vec3d(0, 0, 0);
        double heading = 200.0;
        var s = new Sim { State = MovementState.Intact, LeftArm = true, RightArm = true, GunSide = BodySkeleton.Right };
        for (int i = 0; i < 60; i++) b.Step(Dt, s);
        double t = 0;
        int frame = 0;
        const double spotted = 1.4, stop = 4.4, end = 5.6;
        while (t < end)
        {
            bool fighting = t >= spotted && !_old;
            s.Tactical = fighting ? 1.0 : 0.0;
            double pace = t < spotted ? 1.4 : t < stop ? 1.6 : 0.0;
            s.Speed += (pace - s.Speed) * (1.0 - Math.Exp(-6.0 * Dt));
            s.Moving = s.Speed > 0.05;
            s.Aim = t >= stop ? 1.0 : 0.0;
            at = at + Ahead(heading) * (s.Speed * Dt);
            b.Step(Dt, s);
            // The gun: low ready, raised to just under the eyes in the hunch, levelled at the target when aiming.
            double down = 0.75 + (0.25 - 0.75) * b.Tactical;
            Vec3d aim = t >= stop ? new Vec3d(0.05, -0.04, 1.0) : new Vec3d(0, -down, 1.0);
            aim = aim * (1.0 / aim.Magnitude);
            Vec3d grip = HoldRifle(b, aim);
            if (frame++ % Every == 0)
            {
                Vec3d world = at + QuatD.AngleAxis(Vec3d.Up, heading * Math.PI / 180.0).Rotate(grip + aim * 0.12);
                double pitch = Math.Asin(aim.Y) * 180.0 / Math.PI;
                string caption = t < spotted ? "patrolling" : t < stop ? (_old ? "advancing" : "advancing, hunched") : "aiming";
                Frame(b, at, heading, null, new List<(Vec3d, double)> { (world, heading) }, caption, pitch);
            }
            t += Dt;
        }
    }

    static double Smooth01(double x) { x = x < 0 ? 0 : x > 1 ? 1 : x; return x * x * (3 - 2 * x); }

    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "motion.json";
        string scene = args.Length > 1 ? args[1] : "disarm";
        _old = Array.IndexOf(args, "old") >= 2;
        if (Array.IndexOf(args, "dense") >= 2) Every = 1;
        double speedArg = 2.0;
        for (int i = 2; i < args.Length; i++) if (double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double sp)) speedArg = sp;
        Json.Append("{\"scene\":\"").Append(scene).Append(_old ? " (before)" : "").Append("\",\"seconds_per_frame\":").Append(F(Dt * Every)).Append(",\"frames\":[\n");
        switch (scene)
        {
            case "disarm": Disarm(); break;
            case "hobble": Hobble(); break;
            case "arm": Arm(); break;
            case "walk": Walk(speedArg); break;
            case "hunch": Hunch(); break;
            default: throw new ArgumentException("scene: disarm, hobble, arm, walk [speed] or hunch");
        }
        Json.Append("]}");
        File.WriteAllText(path, Json.ToString());
        Console.WriteLine("wrote " + path);
    }
}
