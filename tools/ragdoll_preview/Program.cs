using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;

/// <summary>
/// Drops a body, or throws a cut-off limb, with the joints the game's rules describe (RagdollRules: how far each
/// joint bends, the springy stop at the end of its range, the muscle's brace and pull), and writes every frame to
/// JSON for tools/blender/preview_enemy_motion.py to draw. It is a small physics of its own (weights, joints held
/// by stiff springs, balls for touching the ground and each other): a way to see what the rules ask for, not the
/// game's physics engine, so the pictures are previews and not game footage.
///
///     dotnet run -c Release -- out.json corpse|arm|leg [jointed|stiff] [gravity m/s2]
/// </summary>
static class Program
{
    const double Deg = Math.PI / 180.0;

    sealed class Ball { public Vec3d At; public double Radius; }

    sealed class Body
    {
        public string Name;
        public double Mass, Inertia;
        public Vec3d X, V, W;          // middle of the weight, speed, spin (world)
        public QuatD Q = QuatD.Identity;
        public Vec3d Force, Torque;
        public readonly List<Ball> Balls = new List<Ball>();
        // The breakable parts that ride on this weight: where each part's origin is, and how it is turned, in this body.
        public readonly List<(BodyPartId id, Vec3d at, QuatD turn)> Parts = new List<(BodyPartId, Vec3d, QuatD)>();
        public Vec3d World(Vec3d local) => X + Q.Rotate(local);
        public Vec3d SpeedAt(Vec3d world) => V + Vec3d.Cross(W, world - X);
        public void Push(Vec3d force, Vec3d at) { Force += force; Torque += Vec3d.Cross(at - X, force); }
    }

    sealed class Joint
    {
        public BodyPartId Id;
        public Body Above, Below;
        public Vec3d OnAbove, OnBelow;     // the joint's place in each body
        public QuatD AboveTurn, BelowTurn; // each part's turn inside its body
    }

    static readonly double[] Thick = { 0.17, 0.215, 0.085, 0.066, 0.082, 0.085, 0.066, 0.082, 0.10, 0.076, 0.058, 0.10, 0.076, 0.058 };

    /// <summary>The balls a part touches the world with, in the part's own space.</summary>
    static List<Ball> BallsOf(BodyPartId id)
    {
        var balls = new List<Ball>();
        double r = Thick[(int)id];
        Vec3d d = BodySkeleton.PartDirection(id);
        switch (id)
        {
            case BodyPartId.Head: balls.Add(new Ball { At = Vec3d.Zero, Radius = r }); break;
            case BodyPartId.Torso:
                foreach (double y in new[] { 0.02, 0.14, 0.27 })
                    foreach (double x in new[] { -0.07, 0.07 })
                        balls.Add(new Ball { At = new Vec3d(x, y, 0), Radius = 0.15 });
                break;
            case BodyPartId.LeftHand: case BodyPartId.RightHand: balls.Add(new Ball { At = d * 0.08, Radius = r }); break;
            case BodyPartId.LeftFoot: case BodyPartId.RightFoot:
                foreach (double z in new[] { -0.04, 0.06, 0.16 }) balls.Add(new Ball { At = new Vec3d(0, -0.04, z), Radius = r });
                break;
            default:
                double length = RagdollRules.PartLength(id);
                for (int i = 0; i <= 3; i++) balls.Add(new Ball { At = d * (length * i / 3.0), Radius = r });
                break;
        }
        return balls;
    }

    static Vec3d MiddleOf(BodyPartId id)
    {
        switch (id)
        {
            case BodyPartId.Head: return Vec3d.Zero;
            case BodyPartId.Torso: return new Vec3d(0, 0.14, 0);
            case BodyPartId.LeftFoot: case BodyPartId.RightFoot: return new Vec3d(0, -0.04, 0.06);
            case BodyPartId.LeftHand: case BodyPartId.RightHand: return BodySkeleton.PartDirection(id) * 0.08;
            default: return BodySkeleton.PartDirection(id) * (RagdollRules.PartLength(id) * 0.5);
        }
    }

    static double InertiaOf(BodyPartId id)
    {
        double l = RagdollRules.PartLength(id), r = Thick[(int)id];
        return RagdollRules.PartMass(id) * (l * l / 12.0 + r * r * 0.4);
    }

    // ------------------------------------------------------------------ the world

    static readonly List<Body> Bodies = new List<Body>();
    static readonly List<Joint> Joints = new List<Joint>();
    static readonly HashSet<(Body, Body)> Joined = new HashSet<(Body, Body)>();
    static RagdollTuning Tuning = new RagdollTuning { jointed = true };
    static double Gravity = 9.81;
    static double Age;

    /// <summary>Makes the weights for a set of parts standing in a pose: one each when jointed, one for all when stiff.</summary>
    static Dictionary<BodyPartId, Body> Build(BodyPose pose, Vec3d at, IReadOnlyList<BodyPartId> members, bool jointed)
    {
        var where = new Dictionary<BodyPartId, (Vec3d p, QuatD q)>();
        foreach (BodyPartId id in members)
        {
            BodyBone bone = BodySkeleton.BoneOf(id);
            pose.World(bone, out Vec3d p, out QuatD q);
            where[id] = (at + p + q.Rotate(BodySkeleton.PartOrigin(id) - BodySkeleton.RestPosition(bone)), q);
        }

        var made = new Dictionary<BodyPartId, Body>();
        if (!jointed)
        {
            var one = new Body { Name = members[0].ToString() };
            double mass = 0;
            Vec3d middle = Vec3d.Zero;
            foreach (BodyPartId id in members)
            {
                double m = RagdollRules.PartMass(id);
                middle += (where[id].p + where[id].q.Rotate(MiddleOf(id))) * m;
                mass += m;
            }
            one.Mass = mass;
            one.X = middle / mass;
            double inertia = 0;
            foreach (BodyPartId id in members)
            {
                Vec3d c = where[id].p + where[id].q.Rotate(MiddleOf(id));
                inertia += InertiaOf(id) + RagdollRules.PartMass(id) * (c - one.X).SqrMagnitude * (2.0 / 3.0);
                one.Parts.Add((id, where[id].p - one.X, where[id].q));
                foreach (Ball b in BallsOf(id)) one.Balls.Add(new Ball { At = where[id].p + where[id].q.Rotate(b.At) - one.X, Radius = b.Radius });
                made[id] = one;
            }
            one.Inertia = inertia;
            Bodies.Add(one);
            return made;
        }

        foreach (BodyPartId id in members)
        {
            var b = new Body { Name = id.ToString(), Mass = RagdollRules.PartMass(id), Inertia = InertiaOf(id), Q = where[id].q };
            b.X = where[id].p + where[id].q.Rotate(MiddleOf(id));
            b.Parts.Add((id, -MiddleOf(id), QuatD.Identity));
            foreach (Ball ball in BallsOf(id)) b.Balls.Add(new Ball { At = ball.At - MiddleOf(id), Radius = ball.Radius });
            made[id] = b;
            Bodies.Add(b);
        }
        foreach (BodyPartId id in members)
        {
            if (!BodyLayout.HasParent(id) || !made.ContainsKey(BodyLayout.Parent(id))) continue;
            BodyPartId above = BodyLayout.Parent(id);
            Vec3d joint = BodySkeleton.RestPosition(BodySkeleton.BoneOf(id));
            Joints.Add(new Joint
            {
                Id = id, Above = made[above], Below = made[id],
                OnAbove = joint - BodySkeleton.PartOrigin(above) - MiddleOf(above),
                OnBelow = joint - BodySkeleton.PartOrigin(id) - MiddleOf(id),
                AboveTurn = QuatD.Identity, BelowTurn = QuatD.Identity,
            });
            Joined.Add((made[above], made[id]));
            Joined.Add((made[id], made[above]));
        }
        return made;
    }

    static void Step(double h)
    {
        foreach (Body b in Bodies)
        {
            b.Force = new Vec3d(0, -Gravity * b.Mass, 0);
            b.Torque = Vec3d.Zero;
        }

        foreach (Joint j in Joints)
        {
            // Held together: a very stiff spring between the two places that should be one.
            Vec3d pa = j.Above.World(j.OnAbove), pb = j.Below.World(j.OnBelow);
            double mu = j.Above.Mass * j.Below.Mass / (j.Above.Mass + j.Below.Mass);
            double w = 2.0 * Math.PI * 45.0;
            Vec3d pull = (pa - pb) * (mu * w * w) + (j.Above.SpeedAt(pa) - j.Below.SpeedAt(pb)) * (2.0 * mu * w);
            j.Below.Push(pull, pb);
            j.Above.Push(-pull, pa);

            // The joint's own say: the rules' soft stop, muscle pull and drag, as a twist and a swing.
            RagdollJoint spec = RagdollRules.JointFor(j.Id);
            QuatD local = (j.Above.Q.Inverse * j.Below.Q).Normalized;
            RagdollRules.Measure(j.Id, local, out double twist, out double second, out double third);
            Vec3d spin = j.Below.W - j.Above.W;
            double stop = RagdollRules.LimitSpring(j.Id, Tuning), stopDrag = RagdollRules.LimitDamper(j.Id, Tuning);
            double tone = RagdollRules.ToneSpring(j.Id, Tuning), drag = RagdollRules.JointDamper(j.Id, Tuning, Age);

            Vec3d axis = j.Below.Q.Rotate(spec.Axis);
            double twistRate = Vec3d.Dot(spin, axis) / Deg;
            Vec3d turn = axis * RagdollRules.StopTorque(twist, twistRate, -spec.Half, spec.Half, stop, stopDrag, tone, drag);

            QuatD frame = (j.Above.Q * RagdollRules.Relaxed(j.Id)).Normalized;
            Vec3d swing = frame.Rotate(spec.Second * second + spec.Third * third);     // degrees, as a vector
            Vec3d swingSpin = spin - axis * Vec3d.Dot(spin, axis);
            turn += swing * (-tone * Deg) - swingSpin * drag;
            double over = RagdollRules.SwingBeyond(spec, second, third);
            if (over > 0)
            {
                Vec3d way = swing.Normalized;
                turn += way * (-stop * over * Deg) - way * (Vec3d.Dot(swingSpin, way) * stopDrag);
            }
            j.Below.Torque += turn;
            j.Above.Torque -= turn;
        }

        // The ground: a firm, grippy floor at y = 0.
        foreach (Body b in Bodies)
        {
            foreach (Ball ball in b.Balls)
            {
                Vec3d c = b.World(ball.At);
                double sunk = ball.Radius - c.Y;
                if (sunk <= 0) continue;
                Vec3d touch = new Vec3d(c.X, 0, c.Z);
                Vec3d v = b.SpeedAt(touch);
                double k = b.Mass * 9000.0 / Math.Max(1, b.Balls.Count) * 2.0;
                double press = Math.Max(0.0, k * sunk - 2.0 * Math.Sqrt(k * b.Mass) * 0.45 * v.Y);
                Vec3d slide = new Vec3d(v.X, 0, v.Z);
                double speed = slide.Magnitude;
                Vec3d grip = speed > 1e-6 ? slide * (-Math.Min(0.8 * press, b.Mass * 60.0 * speed) / speed) : Vec3d.Zero;
                b.Push(new Vec3d(0, press, 0) + grip, touch);
            }
        }

        // Each other: balls of parts that are not joined push apart, so an arm lies on a chest and not in it.
        for (int i = 0; i < Bodies.Count; i++)
        for (int k = i + 1; k < Bodies.Count; k++)
        {
            Body a = Bodies[i], b = Bodies[k];
            if (Joined.Contains((a, b))) continue;
            if ((a.X - b.X).SqrMagnitude > 1.2) continue;
            foreach (Ball ba in a.Balls)
            foreach (Ball bb in b.Balls)
            {
                Vec3d ca = a.World(ba.At), cb = b.World(bb.At);
                Vec3d d = cb - ca;
                double gap = d.Magnitude - (ba.Radius + bb.Radius) * 0.85;
                if (gap >= 0 || d.SqrMagnitude < 1e-10) continue;
                Vec3d n = d.Normalized;
                Vec3d mid = ca + n * (ba.Radius * 0.85 + gap * 0.5);
                double mu = a.Mass * b.Mass / (a.Mass + b.Mass);
                double k2 = mu * 2500.0;
                double closing = Vec3d.Dot(b.SpeedAt(mid) - a.SpeedAt(mid), n);
                double press = Math.Max(0.0, -k2 * gap - 2.0 * Math.Sqrt(k2 * mu) * 0.6 * closing);
                b.Push(n * press, mid);
                a.Push(n * -press, mid);
            }
        }

        foreach (Body b in Bodies)
        {
            b.V += b.Force * (h / b.Mass);
            b.W += b.Torque * (h / b.Inertia);
            b.W *= 1.0 - 0.05 * h;
            b.X += b.V * h;
            double turned = b.W.Magnitude * h;
            if (turned > 1e-12) b.Q = (QuatD.AngleAxis(b.W.Normalized, turned) * b.Q).Normalized;
        }
        Age += h;
    }

    // ------------------------------------------------------------------ writing frames

    static readonly StringBuilder Json = new StringBuilder();
    static bool _first = true;
    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    static void Frame(string caption)
    {
        if (!_first) Json.Append(",\n");
        _first = false;
        Json.Append("{\"caption\":\"").Append(caption).Append("\",\"parts\":{");
        bool first = true;
        foreach (Body b in Bodies)
        {
            foreach (var (id, at, turn) in b.Parts)
            {
                Vec3d origin = b.World(at);
                QuatD r = (b.Q * turn).Normalized;
                Vec3d x = r.Rotate(Vec3d.Right), y = r.Rotate(Vec3d.Up), z = r.Rotate(Vec3d.Forward);
                double[] m = { x.X, y.X, z.X, origin.X, x.Y, y.Y, z.Y, origin.Y, x.Z, y.Z, z.Z, origin.Z, 0, 0, 0, 1 };
                if (!first) Json.Append(',');
                first = false;
                Json.Append('"').Append(id).Append("\":[");
                for (int k = 0; k < 16; k++) { if (k > 0) Json.Append(','); Json.Append(F(m[k])); }
                Json.Append(']');
            }
        }
        Json.Append("},\"guns\":[]}");
    }

    static void Run(double seconds, string caption)
    {
        const double h = 1.0 / 2400.0;
        int perFrame = 120;                       // 20 pictures a second
        int steps = (int)Math.Round(seconds / h);
        for (int i = 0; i < steps; i++)
        {
            if (i % perFrame == 0) Frame(caption);
            Step(h);
        }
    }

    /// <summary>A body as the animator holds it after walking for a moment.</summary>
    static BodyPose Standing(double speed, double seconds)
    {
        var anim = new BodyAnimator(3);
        var input = new BodyAnimInput
        {
            State = MovementState.Intact, ForwardSpeed = speed, Moving = speed > 0.01, WalkSpeed = 2.0, SprintSpeed = 4.6,
            LeftArmWorks = true, RightArmWorks = true, GunSide = -1,
        };
        for (int i = 0; i < (int)(seconds * 60); i++) anim.Update(1.0 / 60.0, input);
        var pose = new BodyPose();
        pose.CopyFrom(anim.Pose);
        return pose;
    }

    static List<BodyPartId> Chain(BodyPartId top)
    {
        var list = new List<BodyPartId>();
        void Add(BodyPartId id)
        {
            list.Add(id);
            foreach (BodyPartId c in BodyLayout.Children(id)) if (c != BodyPartId.Head) Add(c);
        }
        Add(top);
        return list;
    }

    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("usage: out.json corpse|arm|leg [jointed|stiff] [gravity]"); return 2; }
        string scene = args[1];
        bool jointed = args.Length < 3 || args[2] != "stiff";
        if (args.Length > 3) Gravity = double.Parse(args[3], CultureInfo.InvariantCulture);
        string label = jointed ? "jointed" : "stiff";

        if (scene == "corpse")
        {
            // Walking toward the camera's left, shot in the head from in front: the head goes, the rest is a corpse.
            // The launch is the game's own (Mannequin.SpawnGib): for a corpse 0.35 of the gib launch speed along
            // the shot plus a little lift; for the head 1.2 of it, with 1.5 of the lift.
            BodyPose pose = Standing(2.0, 1.37);
            Vec3d shot = new Vec3d(0.25, 0, -1).Normalized;
            var body = Build(pose, Vec3d.Zero, Chain(BodyPartId.Torso), jointed);
            var head = Build(pose, Vec3d.Zero, new[] { BodyPartId.Head }, false)[BodyPartId.Head];
            Vec3d walk = new Vec3d(0, 0, 2.0);
            Vec3d launch = walk + shot * (9.0 * 0.35) + new Vec3d(0, 0.6, 0);
            Vec3d spin = new Vec3d(-2.8, 0.9, 1.6);
            Vec3d about = body[BodyPartId.Torso].X;
            var seen = new HashSet<Body>();
            foreach (Body b in body.Values)
            {
                if (!seen.Add(b)) continue;
                b.V = launch + Vec3d.Cross(spin, b.X - about);
                b.W = spin;
            }
            head.V = walk + shot * (9.0 * 1.2) + new Vec3d(0, 6.0, 0);
            head.W = new Vec3d(9, 3, -6);
            Run(4.0, "A body shot in the head (" + label + ")");
        }
        else if (scene == "arm" || scene == "leg")
        {
            BodyPose pose = Standing(2.0, 1.37);
            BodyPartId top = scene == "arm" ? BodyPartId.RightUpperArm : BodyPartId.LeftThigh;
            var limb = Build(pose, Vec3d.Zero, Chain(top), jointed);
            Vec3d shot = new Vec3d(-0.3, 0, -1).Normalized;
            Vec3d launch = new Vec3d(0, 0, 2.0) + shot * 9.0 + new Vec3d(0, 4.0, 0);
            Vec3d spin = scene == "arm" ? new Vec3d(4, 9, -13) : new Vec3d(-11, 3, 5);
            Vec3d about = limb[top].X;
            var seen = new HashSet<Body>();
            foreach (Body b in limb.Values)
            {
                if (!seen.Add(b)) continue;
                b.V = launch + Vec3d.Cross(spin, b.X - about);
                b.W = spin;
            }
            Run(3.5, "A " + scene + " shot off (" + label + ")");
        }
        else { Console.Error.WriteLine("unknown scene " + scene); return 2; }

        // How well the joints kept to their ranges, for the report.
        double worst = 0;
        foreach (Joint j in Joints) worst = Math.Max(worst, RagdollRules.Beyond(j.Id, (j.Above.Q.Inverse * j.Below.Q).Normalized));
        double fastest = 0;
        foreach (Body b in Bodies) fastest = Math.Max(fastest, b.V.Magnitude);
        Console.WriteLine("ragdoll_preview: " + scene + " " + label + ", at the end the worst joint is " + F(worst) + " degrees past its range, the fastest part moves " + F(fastest) + " m/s");

        File.WriteAllText(args[0], "{\"seconds_per_frame\":0.05,\"frames\":[\n" + Json + "\n]}\n");
        return 0;
    }
}
