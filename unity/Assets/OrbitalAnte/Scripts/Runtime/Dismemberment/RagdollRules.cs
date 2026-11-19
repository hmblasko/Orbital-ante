using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// The dials of a jointed body (Matthew, 15 Nov 2026: "a very realistic ragdoll ... soft limits with tension").
    /// A chunk that comes off with more than one part in it (a corpse, an arm, a leg) is no longer one stiff
    /// object: every part is its own weight, held to the next by a joint that bends as far as a person's does,
    /// meets a springy stop there instead of a wall, and is held a little by the muscle's own pull.
    /// </summary>
    [Serializable]
    public sealed class RagdollTuning
    {
        /// <summary>Off: every loose chunk is one stiff object, as before.</summary>
        public bool jointed;
        /// <summary>How hard the stop at the end of a joint's range is: the beat, in cycles a second, at which the limb would bounce on it. Low is soft and stretchy, high is a wall.</summary>
        public float limitHz = 5f;
        /// <summary>How quickly a limb settles on its stop: 0 bounces for ever, 1 does not bounce at all.</summary>
        public float limitDamping = 0.7f;
        /// <summary>The muscle's pull toward the joint's relaxed pose, as a beat in cycles a second. 0: none (a rope).</summary>
        public float toneHz = 0.55f;
        /// <summary>How much every joint resists being moved once limp: 0 swings freely, 1 moves like through treacle.</summary>
        public float limpDamping = 0.35f;
        /// <summary>Just after it comes off the joints are still braced: they resist this many times more...</summary>
        public float braceDamping = 5f;
        /// <summary>...and that fades over this many seconds, so a body crumples rather than dropping like a cut puppet.</summary>
        public float braceSeconds = 0.5f;
        /// <summary>The most jointed chunks at once (the Steam Deck's limit). The oldest is made stiff when another arrives.</summary>
        public int mostJointed = 3;
        /// <summary>A jointed chunk is made stiff after this many seconds whatever it is doing...</summary>
        public float jointedSeconds = 12f;
        /// <summary>...or once it has lain still this long.</summary>
        public float stillSeconds = 1f;
    }

    /// <summary>
    /// The joint that holds a part to the part above it, in the part's own space at rest (the T-pose: +X the
    /// figure's right, +Y up, +Z forward). Angles are the part's turn against its parent, in degrees, by the same
    /// rule as <see cref="QuatD.AngleAxis"/> and the animator's poses.
    /// </summary>
    public readonly struct RagdollJoint
    {
        /// <summary>The main axis: the hinge of a knee or elbow, the length of an upper arm.</summary>
        public readonly Vec3d Axis;
        /// <summary>The second axis, at right angles to the first. The third is Axis x Second.</summary>
        public readonly Vec3d Second;
        /// <summary>The range about the main axis.</summary>
        public readonly double Low, High;
        /// <summary>The range about the second axis.</summary>
        public readonly double SecondLow, SecondHigh;
        /// <summary>The range about the third axis.</summary>
        public readonly double ThirdLow, ThirdHigh;

        public RagdollJoint(Vec3d axis, Vec3d second, double low, double high, double secondLow, double secondHigh, double thirdLow, double thirdHigh)
        {
            Axis = axis;
            Second = second;
            Low = low;
            High = high;
            SecondLow = secondLow;
            SecondHigh = secondHigh;
            ThirdLow = thirdLow;
            ThirdHigh = thirdHigh;
        }

        public Vec3d Third => Vec3d.Cross(Axis, Second);
        public double Mid => (Low + High) * 0.5;
        public double SecondMid => (SecondLow + SecondHigh) * 0.5;
        public double ThirdMid => (ThirdLow + ThirdHigh) * 0.5;
        /// <summary>Half the range about each axis: what the physics joint is given either side of the relaxed pose.</summary>
        public double Half => (High - Low) * 0.5;
        public double SecondHalf => (SecondHigh - SecondLow) * 0.5;
        public double ThirdHalf => (ThirdHigh - ThirdLow) * 0.5;
    }

    /// <summary>
    /// How a body's joints bend, how soft their stops are and how the muscles hold them. Plain numbers, no engine:
    /// the game's joints (<c>GibRagdoll</c>), the rule tests and the offline preview all read these.
    ///
    /// The ranges are people's (the usual clinical figures for each joint, rounded, a little short of the extremes
    /// so a body never folds flat). Each range is given about the relaxed pose in its middle, because a physics
    /// joint that is told only "so far either side of where I started" cannot get a sign wrong.
    /// </summary>
    public static class RagdollRules
    {
        const double Deg = Math.PI / 180.0;

        /// <summary>The joint above a part. The torso has none (it is the top of a body).</summary>
        public static RagdollJoint JointFor(BodyPartId id)
        {
            bool left = BodyLayout.IsLeftSide(id);
            switch (id)
            {
                case BodyPartId.Head:
                    // Nod about X (chin down is positive), turn about Y, tip toward a shoulder about Z.
                    return new RagdollJoint(Vec3d.Right, Vec3d.Up, -45, 50, -65, 65, -35, 35);

                case BodyPartId.LeftUpperArm:
                case BodyPartId.RightUpperArm:
                    // The arm lies along X. About its own length it turns 70 either way; about Y it swings forward
                    // much further than back (forward is negative on the right); about Z it drops to the side and
                    // rises overhead (up is positive on the right).
                    return new RagdollJoint(Vec3d.Right, Vec3d.Up, -70, 70,
                        left ? -35 : -110, left ? 110 : 35, -85, 85);

                case BodyPartId.LeftForearm:
                case BodyPartId.RightForearm:
                    // A hinge about Y: it folds forward (negative on the right) and stops a few degrees past straight.
                    return new RagdollJoint(Vec3d.Up, Vec3d.Right,
                        left ? -4 : -145, left ? 145 : 4, -60, 60, -4, 4);

                case BodyPartId.LeftHand:
                case BodyPartId.RightHand:
                    // Palm down: the wrist folds about Z, and a little from side to side about Y.
                    return new RagdollJoint(Vec3d.Forward, Vec3d.Up, -70, 70, -25, 25, -8, 8);

                case BodyPartId.LeftThigh:
                case BodyPartId.RightThigh:
                    // The leg hangs along -Y. About X it swings forward (negative) much further than back; about Y
                    // the whole leg turns in and out; about Z it lifts out to the side (positive on the right).
                    return new RagdollJoint(Vec3d.Right, Vec3d.Up, -110, 30, -35, 35,
                        left ? -40 : -20, left ? 20 : 40);

                case BodyPartId.LeftShin:
                case BodyPartId.RightShin:
                    // A hinge about X: it folds back (positive) and stops a few degrees past straight.
                    return new RagdollJoint(Vec3d.Right, Vec3d.Up, -4, 140, -10, 10, -4, 4);

                case BodyPartId.LeftFoot:
                case BodyPartId.RightFoot:
                    // Toes up is negative about X; it rolls a little about Z and turns a little about Y.
                    return new RagdollJoint(Vec3d.Right, Vec3d.Up, -35, 50, -15, 15, -12, 12);

                default:
                    return new RagdollJoint(Vec3d.Right, Vec3d.Up, 0, 0, 0, 0, 0, 0);
            }
        }

        /// <summary>True for every part held by a joint (all but the torso).</summary>
        public static bool HasJoint(BodyPartId id) => BodyLayout.HasParent(id);

        /// <summary>
        /// The relaxed pose of a joint: the middle of its range, as the part's turn against its parent. The physics
        /// joint is made with the part held like this, and then allowed <see cref="RagdollJoint.Half"/> either side.
        /// </summary>
        public static QuatD Relaxed(BodyPartId id)
        {
            RagdollJoint j = JointFor(id);
            return (QuatD.AngleAxis(j.Third, j.ThirdMid * Deg)
                    * QuatD.AngleAxis(j.Second, j.SecondMid * Deg)
                    * QuatD.AngleAxis(j.Axis, j.Mid * Deg)).Normalized;
        }

        /// <summary>
        /// A part's turn against its parent, measured the way the physics joint measures it: from the relaxed pose,
        /// as a twist about the joint's main axis and a swing of that axis toward the second and third, in degrees.
        /// Zero, zero, zero is the relaxed pose.
        /// </summary>
        public static void Measure(BodyPartId id, QuatD local, out double twist, out double swingSecond, out double swingThird)
        {
            RagdollJoint j = JointFor(id);
            QuatD d = (Relaxed(id).Inverse * local).Normalized;
            if (d.W < 0) d = new QuatD(-d.X, -d.Y, -d.Z, -d.W);
            // d = swing * twist: the twist is the part of the turn that is about the main axis.
            Vec3d v = new Vec3d(d.X, d.Y, d.Z);
            double along = Vec3d.Dot(v, j.Axis);
            QuatD tw = new QuatD(j.Axis.X * along, j.Axis.Y * along, j.Axis.Z * along, d.W).Normalized;
            twist = 2.0 * Math.Atan2(along, d.W) / Deg;
            if (twist > 180) twist -= 360;
            if (twist < -180) twist += 360;
            QuatD swing = (d * tw.Inverse).Normalized;
            if (swing.W < 0) swing = new QuatD(-swing.X, -swing.Y, -swing.Z, -swing.W);
            Vec3d sv = new Vec3d(swing.X, swing.Y, swing.Z);
            double sine = sv.Magnitude;
            double angle = 2.0 * Math.Atan2(sine, swing.W) / Deg;
            Vec3d about = sine > 1e-12 ? sv / sine : Vec3d.Zero;
            swingSecond = angle * Vec3d.Dot(about, j.Second);
            swingThird = angle * Vec3d.Dot(about, j.Third);
        }

        /// <summary>
        /// How far outside its joint's range a part's turn is, in degrees: 0 anywhere inside. The soft stop pulls
        /// back in proportion to this. The swing's range is an oval cone, as the physics joint's is.
        /// </summary>
        public static double Beyond(BodyPartId id, QuatD local)
        {
            RagdollJoint j = JointFor(id);
            Measure(id, local, out double twist, out double second, out double third);
            double over = Math.Max(0.0, Math.Abs(twist) - j.Half);
            return Math.Max(over, SwingBeyond(j, second, third));
        }

        /// <summary>How far a swing is outside the oval cone a joint allows, in degrees.</summary>
        public static double SwingBeyond(in RagdollJoint j, double second, double third)
        {
            double a = Math.Max(SwingFloor, j.SecondHalf), b = Math.Max(SwingFloor, j.ThirdHalf);
            double r = Math.Sqrt(second * second / (a * a) + third * third / (b * b));
            if (r <= 1.0) return 0.0;
            return Math.Sqrt(second * second + third * third) * (1.0 - 1.0 / r);
        }

        /// <summary>The physics engine's narrowest swing, in degrees: a hinge is given this much play to the side.</summary>
        public const double SwingFloor = 3.0;

        // ---------------------------------------------------------------- weights

        static readonly double[] Mass = { 5, 30, 3, 2, 1, 3, 2, 1, 8, 5, 1.5, 8, 5, 1.5 };

        /// <summary>A part's weight in kilograms (the same numbers the body is built with).</summary>
        public static double PartMass(BodyPartId id) => Mass[(int)id];

        /// <summary>A part's length from its joint to the joint below, in metres.</summary>
        public static double PartLength(BodyPartId id)
        {
            switch (id)
            {
                case BodyPartId.Head: return BodySkeleton.HeadCentreAboveNeck * 2.0;
                case BodyPartId.Torso: return BodySkeleton.NeckY - BodySkeleton.HipJointY;
                case BodyPartId.LeftUpperArm: case BodyPartId.RightUpperArm: return BodySkeleton.UpperArmLength;
                case BodyPartId.LeftForearm: case BodyPartId.RightForearm: return BodySkeleton.ForearmLength;
                case BodyPartId.LeftHand: case BodyPartId.RightHand: return 0.16;
                case BodyPartId.LeftThigh: case BodyPartId.RightThigh: return BodySkeleton.ThighLength;
                case BodyPartId.LeftShin: case BodyPartId.RightShin: return BodySkeleton.ShinLength;
                default: return 0.22;
            }
        }

        /// <summary>
        /// How hard it is to swing everything below a joint about that joint, with the limb out straight
        /// (kilogram metres squared): each part as a thin rod at its distance down the limb.
        /// </summary>
        public static double SwingInertia(BodyPartId id)
        {
            double total = 0, reach = 0;
            BodyPartId at = id;
            while (true)
            {
                double length = PartLength(at), mass = PartMass(at);
                double centre = reach + length * 0.5;
                total += mass * (length * length / 12.0 + centre * centre);
                reach += length;
                var below = BodyLayout.Children(at);
                if (below.Count != 1) break;
                at = below[0];
            }
            return total;
        }

        // ---------------------------------------------------------------- springs

        /// <summary>The stop's spring, in newton metres per radian: stiff enough that the limb would bounce on it at <see cref="RagdollTuning.limitHz"/>.</summary>
        public static double LimitSpring(BodyPartId id, RagdollTuning t)
        {
            double w = 2.0 * Math.PI * Math.Max(0.1f, t.limitHz);
            return SwingInertia(id) * w * w;
        }

        /// <summary>The stop's damper, in newton metre seconds per radian.</summary>
        public static double LimitDamper(BodyPartId id, RagdollTuning t)
        {
            double inertia = SwingInertia(id);
            return 2.0 * Clamp01(t.limitDamping) * Math.Sqrt(LimitSpring(id, t) * inertia);
        }

        /// <summary>The muscle's pull toward the relaxed pose, in newton metres per radian.</summary>
        public static double ToneSpring(BodyPartId id, RagdollTuning t)
        {
            double w = 2.0 * Math.PI * Math.Max(0f, t.toneHz);
            return SwingInertia(id) * w * w;
        }

        /// <summary>
        /// How much a joint resists being moved, <paramref name="age"/> seconds after its chunk came off, in newton
        /// metre seconds per radian: braced at first, easing to limp over <see cref="RagdollTuning.braceSeconds"/>.
        /// </summary>
        public static double JointDamper(BodyPartId id, RagdollTuning t, double age)
        {
            double inertia = SwingInertia(id);
            // Measured against a beat of one cycle a second, so the dial reads the same for a hand and a leg.
            double limp = 2.0 * Math.Max(0f, t.limpDamping) * inertia * (2.0 * Math.PI);
            return limp * Brace(t, age);
        }

        /// <summary>How braced the joints still are: <see cref="RagdollTuning.braceDamping"/> at the start, 1 once limp.</summary>
        public static double Brace(RagdollTuning t, double age)
        {
            double most = Math.Max(1f, t.braceDamping);
            if (t.braceSeconds <= 0f || age >= t.braceSeconds) return 1.0;
            double x = Math.Max(0.0, age) / t.braceSeconds;
            // Lets go slowly at first and then all at once, the way knees give.
            double left = 1.0 - x * x * (3.0 - 2.0 * x);
            return 1.0 + (most - 1.0) * left;
        }

        /// <summary>
        /// The turning force a joint gives back about one axis, in newton metres: nothing but the muscle's light
        /// pull and drag inside the range, and past either end a spring in proportion to how far past, with its
        /// damper. This is the "soft limit with tension", in one line, and what the offline preview steps.
        /// </summary>
        /// <param name="angle">Degrees about the axis.</param>
        /// <param name="rate">Degrees a second about the axis.</param>
        public static double StopTorque(double angle, double rate, double low, double high, double spring, double damper, double tone, double drag)
        {
            double mid = (low + high) * 0.5;
            double torque = -tone * (angle - mid) * Deg - drag * rate * Deg;
            double over = angle > high ? angle - high : angle < low ? angle - low : 0.0;
            if (over != 0.0) torque += -spring * over * Deg - damper * rate * Deg;
            return torque;
        }

        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>
    /// Which jointed chunks to make stiff again, so that only a few are ever worked out joint by joint (a jointed
    /// corpse is twelve weights and eleven joints; a stiff one is a single weight). Plain rule, no engine.
    /// </summary>
    public static class RagdollBudget
    {
        public struct Slot
        {
            /// <summary>Seconds since it came off.</summary>
            public float Age;
            /// <summary>Seconds it has lain still.</summary>
            public float Still;
            /// <summary>How many joints it has.</summary>
            public int Joints;
        }

        /// <summary>True when a jointed chunk has had its time: too old, or at rest.</summary>
        public static bool Done(in Slot slot, RagdollTuning t)
        {
            return slot.Joints <= 0 || slot.Age >= t.jointedSeconds || slot.Still >= t.stillSeconds;
        }

        /// <summary>
        /// Before another jointed chunk is added: the index of one to make stiff first, or -1 if there is room.
        /// The one with most joints among the oldest goes: an old corpse before a fresh arm.
        /// </summary>
        public static int MakeRoom(System.Collections.Generic.IReadOnlyList<Slot> live, RagdollTuning t)
        {
            int most = Math.Max(1, t.mostJointed);
            if (live.Count < most) return -1;
            int pick = 0;
            for (int i = 1; i < live.Count; i++)
            {
                if (live[i].Age > live[pick].Age) pick = i;
            }
            return pick;
        }
    }
}
