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
///     dotnet run -c Release -- out.json disarm|hobble|arm|scramble_hop
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
        public double Aim, LossLeft, LossRight, Stump, Scramble, Reach, Teeter;
        public int ReachSide;
        public bool HasLook;
        public Vec3d Look;
    }

    public interface IBody
    {
        void Step(double dt, in Sim s);
        BodyPose Pose { get; }
        void Startle(double x, double z);
        void Wounded(int side, double severity);
        void Flinch(double x, double z, double height, double side, double strength);
        void Stumble(double side);
        bool Stumbling { get; }
    }

    sealed class NewBody : IBody
    {
        readonly BodyAnimator _a;
        public NewBody(int seed) { _a = new BodyAnimator(seed); }
        public BodyPose Pose => _a.Pose;
        public bool Stumbling => _a.Stumbling;
        public void Startle(double x, double z) => _a.Startle(x, z);
        public void Wounded(int side, double severity) => _a.Wounded(side, severity);
        public void Flinch(double x, double z, double h, double side, double k) => _a.Flinch(x, z, h, side, k);
        public void Stumble(double side) => _a.Stumble(side);
        public void Step(double dt, in Sim s)
        {
            var input = new BodyAnimInput
            {
                State = s.State, ForwardSpeed = s.Speed, TurnRate = s.Turn, Phase = s.Phase, Moving = s.Moving,
                WalkSpeed = 2.0, SprintSpeed = 4.6, StandingLeg = s.StandingLeg, LeftArmWorks = s.LeftArm, RightArmWorks = s.RightArm,
                Aim = s.Aim, GunSide = s.GunSide, ArmLossLeft = s.LossLeft, ArmLossRight = s.LossRight, StumpLength = s.Stump,
                Scramble = s.Scramble, Reach = s.Reach, ReachSide = s.ReachSide, Teeter = s.Teeter,
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
    const int Every = 3;   // 20 pictures a second

    static readonly StringBuilder Json = new StringBuilder();
    static bool _firstFrame = true;

    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    static void Frame(IBody body, Vec3d at, double headingDegrees, HashSet<BodyPartId> gone, List<(Vec3d at, double heading)> guns, string caption)
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
                .Append("],\"heading\":").Append(F(guns[g].heading)).Append('}');
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
                if (!_old) b.Wounded(BodySkeleton.Left, 1.0);
            }
            bool walking = t > 2.0;
            s.Speed = walking ? 1.3 : 0; s.Moving = walking;
            at = at + Ahead(heading) * (s.Speed * Dt);
            b.Step(Dt, s);
            if (frame++ % Every == 0) Frame(b, at, heading, gone, new List<(Vec3d, double)>(), t < lost ? "standing" : t < 2.0 ? "arm shot off" : "walks on, hurt");
            t += Dt;
        }
    }

    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "motion.json";
        string scene = args.Length > 1 ? args[1] : "disarm";
        _old = args.Length > 2 && args[2] == "old";
        Json.Append("{\"scene\":\"").Append(scene).Append(_old ? " (before)" : "").Append("\",\"seconds_per_frame\":").Append(F(Dt * Every)).Append(",\"frames\":[\n");
        switch (scene)
        {
            case "disarm": Disarm(); break;
            case "hobble": Hobble(); break;
            case "arm": Arm(); break;
            default: throw new ArgumentException("scene: disarm, hobble or arm");
        }
        Json.Append("]}");
        File.WriteAllText(path, Json.ToString());
        Console.WriteLine("wrote " + path);
    }
}
