using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public struct AmbusherAnimInput
    {
        public AmbusherMode Mode;
        public double T;
        /// <summary>How far from its hole its head is, or is going, along the ground (the brain's own number).</summary>
        public double Out;
        /// <summary>It feels someone: its feelers lean their way.</summary>
        public bool Feels;
        /// <summary>The player's feet in creature space.</summary>
        public Vec3d Player;
        /// <summary>How near it is to letting go of whoever it holds, 0 to 1.</summary>
        public double Slack;
    }

    /// <summary>Where every piece of an Ambusher is, in creature space.</summary>
    public sealed class AmbusherPose
    {
        public readonly Vec3d[] NodePosition = new Vec3d[AmbusherLayout.Nodes];
        public readonly QuatD[] NodeRotation = new QuatD[AmbusherLayout.Nodes];
        public readonly Vec3d[] JawPosition = new Vec3d[AmbusherLayout.Jaws];
        public readonly QuatD[] JawRotation = new QuatD[AmbusherLayout.Jaws];
        public readonly Vec3d[] FeelerPosition = new Vec3d[AmbusherLayout.Feelers];
        public readonly QuatD[] FeelerRotation = new QuatD[AmbusherLayout.Feelers];
        /// <summary>How wide its jaws are: 0 shut, 1 as wide as they go.</summary>
        public double Gape;
        /// <summary>How high the ring of ground round its hole stands: 0 flat, 1 whole.</summary>
        public double Mound;
        /// <summary>How much of its body is out of the hole, in metres along it.</summary>
        public double OutLength;
    }

    /// <summary>
    /// An Ambusher's movement, worked out every frame with no clips and no Unity types.
    ///
    /// - Its body is one line: straight up the burrow, over the lip of the hole and along the ground to its head.
    ///   Every piece sits at its own distance back from the head along that line, so whatever is not out of the hole
    ///   is down it, and pulling back slides the whole body home.
    /// - Waiting, its head stands in the mouth of the hole looking straight up, jaws lying open either side, feelers
    ///   up. To strike it rises and draws back like a snake; at the moment it takes aim it rears to full height and
    ///   its jaws fly wide.
    /// - A miss leaves it lying along the ground, twisting slowly; holding someone it shakes.
    /// </summary>
    public sealed class AmbusherAnimator
    {
        /// <summary>How deep its head sits when it is under the ground.</summary>
        public const double Depth = 1.0;
        /// <summary>How high it carries its head to seize someone.</summary>
        public const double GripHeight = 0.70;
        /// <summary>Its head when the warning is at its height: up, and back from the hole.</summary>
        public const double CoilHeight = 1.0, CoilBack = 0.45, CoilLean = 0.9;
        const int Samples = 40;
        const double Lip = 0.30, Lie = AmbusherLayout.SegHalfHeight;

        readonly Vec3d[] _line = new Vec3d[Samples + 1];
        readonly double[] _along = new double[Samples + 1];
        double _time, _sinceMode, _blend;
        AmbusherMode _mode = AmbusherMode.Lurk;
        // The head as it was when the mode last changed, and as it is: height, distance out, lean and gape.
        double _fromY = 0.10, _fromZ, _fromLean, _fromGape = 0.9, _fromFlat, _fromWave;
        double _y = 0.10, _z, _lean, _gape = 0.9, _flat, _wave, _mound = 1.0, _lookX, _lookZ;
        Vec3d _flinch, _flinchSpeed;
        double _alert;

        public void Flinch(Vec3d push) => _flinchSpeed += push;

        static double Smooth(double x)
        {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>How wide its jaws are at a point in a mode, 0 to 1.</summary>
        public static double GapeOf(AmbusherMode mode, double t)
        {
            switch (mode)
            {
                case AmbusherMode.Lurk: return 0.9;
                // They half close as it rises, and fly wide the moment it has taken aim: that is the sign to move.
                case AmbusherMode.Coil: return t < AmbusherBrain.LockAt ? 0.9 - 0.4 * Smooth(t / (AmbusherBrain.LockAt * 0.8)) : 0.5 + 0.5 * Smooth((t - AmbusherBrain.LockAt) / 0.12);
                case AmbusherMode.Strike: return 1.0 - 0.6 * Smooth((t - 0.75) / 0.25);
                case AmbusherMode.Hold: return 0.4;
                case AmbusherMode.Spent: return 0.15;
                case AmbusherMode.Rise: return 0.9 * Smooth((t - 0.55) / 0.45);
                default: return 0.0;
            }
        }

        /// <summary>How high its head is at a point in the warning.</summary>
        public static double CoilHeightOf(double t) =>
            t < AmbusherBrain.LockAt ? 0.10 + 0.72 * Smooth(t / AmbusherBrain.LockAt) : 0.82 + (CoilHeight - 0.82) * Smooth((t - AmbusherBrain.LockAt) / 0.12);

        /// <summary>The head for a mode: its height, how far out, how far it leans over from straight up, and how flat its feelers lie.</summary>
        static void Target(in AmbusherAnimInput input, out double y, out double z, out double lean, out double flat, out double wave)
        {
            double t = input.T, half = Math.PI * 0.5;
            y = 0.10;
            z = 0.0;
            lean = 0.0;
            flat = 0.0;
            wave = 0.0;
            switch (input.Mode)
            {
                case AmbusherMode.Coil:
                {
                    double u = Smooth(t / AmbusherBrain.LockAt), v = Smooth((t - AmbusherBrain.LockAt) / 0.12);
                    y = CoilHeightOf(t);
                    z = -0.30 * u - (CoilBack - 0.30) * v;
                    lean = CoilLean * u;
                    flat = 0.25 * u + 0.75 * v;
                    break;
                }
                case AmbusherMode.Strike:
                    // It leaves the coil faster and faster, in a low arc.
                    y = CoilHeight + (GripHeight - CoilHeight) * t + 0.20 * Math.Sin(Math.PI * t);
                    z = -CoilBack * (1.0 - t) + input.Out;
                    lean = CoilLean + (half - CoilLean) * Smooth(t * 1.6);
                    flat = 1.0;
                    break;
                case AmbusherMode.Hold:
                    y = GripHeight;
                    z = input.Out;
                    lean = half;
                    flat = 0.8;
                    wave = 1.0;
                    break;
                case AmbusherMode.Spent:
                    y = AmbusherLayout.HeadHalfHeight + 0.02;
                    z = input.Out;
                    lean = half + 0.05;
                    flat = 0.6;
                    wave = 0.6;
                    break;
                case AmbusherMode.Withdraw:
                {
                    // Back along the ground, then down.
                    double back = Smooth(t / 0.75), down = Smooth((t - 0.7) / 0.3);
                    z = input.Out * (1.0 - back);
                    y = (AmbusherLayout.HeadHalfHeight + 0.02) * (1.0 - down) - Depth * down;
                    lean = half * (1.0 - Smooth((t - 0.45) / 0.4));
                    if (input.Out < 0.3) lean = 0.0;
                    flat = 0.6;
                    wave = 0.4 * (1.0 - back);
                    break;
                }
                case AmbusherMode.Under:
                case AmbusherMode.Tunnel:
                    y = -Depth;
                    break;
                case AmbusherMode.Rise:
                    y = -Depth + (Depth + 0.10) * Smooth((t - 0.45) / 0.55);
                    break;
            }
        }

        /// <summary>How high the ring of ground stands for a mode.</summary>
        public static double MoundOf(in AmbusherAnimInput input)
        {
            switch (input.Mode)
            {
                case AmbusherMode.Under: return 0.4;
                case AmbusherMode.Tunnel: return 0.6;
                case AmbusherMode.Rise: return 0.4 + 0.6 * Smooth(input.T / 0.5);
                default: return 1.0;
            }
        }

        public void Update(AmbusherBody body, in AmbusherAnimInput input, double dt, AmbusherPose pose)
        {
            _time += dt;
            if (input.Mode != _mode)
            {
                _mode = input.Mode;
                _sinceMode = 0.0;
                _fromY = _y;
                _fromZ = _z;
                _fromLean = _lean;
                _fromGape = _gape;
                _fromFlat = _flat;
                _fromWave = _wave;
                // The strike must leave on time: almost no easing into it.
                _blend = _mode == AmbusherMode.Strike ? 0.02 : 0.14;
            }
            _sinceMode += dt;
            Target(input, out double y, out double z, out double lean, out double flat, out double wave);
            double k = Smooth(_sinceMode / _blend);
            _y = _fromY + (y - _fromY) * k;
            _z = _fromZ + (z - _fromZ) * k;
            _lean = _fromLean + (lean - _fromLean) * k;
            _flat = _fromFlat + (flat - _fromFlat) * k;
            _wave = _fromWave + (wave - _fromWave) * k;
            double gape = GapeOf(input.Mode, input.T);
            if (input.Mode == AmbusherMode.Lurk) gape += 0.04 * Math.Sin(_time * 1.3);
            // Hurt while it holds on, its jaws slacken: the sign that it is about to let go.
            if (input.Mode == AmbusherMode.Hold) gape += 0.35 * Math.Max(0.0, Math.Min(1.0, input.Slack));
            if (input.Mode == AmbusherMode.Coil && input.T >= AmbusherBrain.LockAt) gape += 0.03 * Math.Sin(_time * 55.0);
            _gape = _fromGape + (gape - _fromGape) * k;
            _mound += (MoundOf(input) - _mound) * Math.Min(1.0, dt * 6.0);
            pose.Gape = Math.Max(0.0, Math.Min(1.05, _gape));
            pose.Mound = _mound;

            // A knock: a small spring.
            _flinchSpeed += (_flinch * -140.0 - _flinchSpeed * 15.0) * dt;
            _flinch += _flinchSpeed * dt;
            bool above = _y > -0.2;
            var head = new Vec3d(above ? _flinch.X * 0.05 : 0.0, _y, _z + (above ? _flinch.Z * 0.03 : 0.0));
            // Holding someone it shakes them.
            if (input.Mode == AmbusherMode.Hold) head += new Vec3d(0.05 * Math.Sin(_time * 19.0), 0.03 * Math.Sin(_time * 23.0), 0);

            BuildLine(head, _lean);
            double outLength = _along[Samples];
            pose.OutLength = Math.Max(0.0, outLength - Lip);

            // ---- every piece at its own distance back from the head along the line
            // The body is one skin cut at its joints, so the joints are what is laid along the line: a length lies
            // from the joint in front of it to the joint behind, and two lengths always meet at the same point,
            // however sharply the line turns there. (Laid by their middles they met a little to one side of each
            // other on a tight bend, which showed as a ledge.)
            double quick = input.Mode == AmbusherMode.Hold ? 9.0 : 2.6;
            Vec3d Place(double at)
            {
                Vec3d p = At(at);
                if (at > 0.0 && _wave > 0.0)
                {
                    // It twists from side to side: not at the hole and not at the head, so its aim stays true.
                    double share = at / Math.Max(0.5, outLength);
                    p += new Vec3d(0.13 * _wave * Math.Sin(_time * quick - at * 1.7) * Math.Sin(Math.PI * share), 0, 0);
                }
                return p;
            }
            // The head is aimed by its lean alone, so a twist in the body never throws its jaws off.
            var headWay = new Vec3d(0, Math.Cos(_lean), Math.Sin(_lean));
            pose.NodePosition[0] = Place(outLength);
            pose.NodeRotation[0] = Frame(headWay);
            Vec3d joint = pose.NodePosition[0] - headWay * (AmbusherLayout.Spacing * 0.5);
            for (int i = 1; i < AmbusherLayout.Nodes; i++)
            {
                Vec3d behind = Place(outLength - (i + 0.5) * AmbusherLayout.Spacing);
                Vec3d f = behind - joint;
                f = f.SqrMagnitude > 1e-9 ? (joint - behind).Normalized : headWay;
                // (Its middle is half a length back from the joint in front, so that end always meets; where the
                // line bends the far end runs a hair into the next length, under its skin.)
                pose.NodePosition[i] = joint - f * (AmbusherLayout.Spacing * 0.5);
                pose.NodeRotation[i] = Frame(f);
                joint = behind;
            }

            // ---- jaws
            QuatD headTurn = pose.NodeRotation[0];
            for (int side = 0; side < AmbusherLayout.Jaws; side++)
            {
                double s = AmbusherLayout.Sign(side);
                pose.JawPosition[side] = pose.NodePosition[0] + headTurn.Rotate(AmbusherLayout.HingeOf(side));
                QuatD open = QuatD.AngleAxis(Vec3d.Up, s * AmbusherLayout.JawSwing * pose.Gape);
                // The left jaw is the right one rolled half a turn.
                if (side == AmbusherLayout.LeftSide) open = open * QuatD.AngleAxis(Vec3d.Forward, Math.PI);
                pose.JawRotation[side] = (headTurn * open).Normalized;
            }

            // ---- feelers: they stand up and sway, lean toward what it feels, and lie back flat when it strikes
            double want = input.Feels ? 1.0 : 0.0;
            _alert += (want - _alert) * Math.Min(1.0, dt * 4.0);
            var toward = new Vec3d(input.Player.X, 0, input.Player.Z);
            toward = toward.SqrMagnitude > 1e-6 ? toward.Normalized : Vec3d.Forward;
            _lookX += (toward.X * want - _lookX) * Math.Min(1.0, dt * 5.0);
            _lookZ += (toward.Z * want - _lookZ) * Math.Min(1.0, dt * 5.0);
            Vec3d lookLocal = headTurn.Inverse.Rotate(new Vec3d(_lookX, 0, _lookZ));
            for (int feeler = 0; feeler < AmbusherLayout.Feelers; feeler++)
            {
                double around = AmbusherLayout.FeelerAngle(feeler);
                var outward = new Vec3d(Math.Sin(around), Math.Cos(around), 0);
                // Feeling nobody they droop outward; feeling someone they stand up and lean that way.
                double splay = 0.35 + 0.45 * (1.0 - _alert) + 0.12 * Math.Sin(_time * 1.9 + feeler * 1.7) + 1.15 * _flat * (0.55 + 0.45 * _alert);
                Vec3d way = Vec3d.Forward * Math.Cos(splay) + outward * Math.Sin(splay)
                            + new Vec3d(0.10 * Math.Sin(_time * 2.7 + feeler * 2.3), 0, 0)
                            + new Vec3d(lookLocal.X, lookLocal.Y, 0) * (0.45 * (1.0 - _flat));
                pose.FeelerPosition[feeler] = pose.NodePosition[0] + headTurn.Rotate(AmbusherLayout.FeelerRoot(feeler));
                pose.FeelerRotation[feeler] = (headTurn * WalkerAnimator.Look(way.Normalized, outward)).Normalized;
            }
        }

        /// <summary>A piece's turn from the way the body runs through it: its back up, or toward -Z where it stands on end.</summary>
        static QuatD Frame(Vec3d f)
        {
            Vec3d up = Vec3d.Up - f * f.Y + new Vec3d(0, 0, -0.02);
            return WalkerAnimator.Look(f, up).Normalized;
        }

        /// <summary>
        /// The line of the body from a little way down the burrow to the head. With the head over the hole it stands
        /// up in a curve; with the head out along the ground it comes over the lip of the hole, lies along the ground
        /// and lifts to the head at the end.
        /// </summary>
        void BuildLine(Vec3d head, double lean)
        {
            // With its head down the burrow the whole of it hangs below its head.
            var start = new Vec3d(0, Math.Min(-Lip, head.Y - 0.05), 0);
            var aim = new Vec3d(0, Math.Cos(lean), Math.Sin(lean));
            double reach = (head - start).Magnitude;
            double c = Math.Max(0.02, 0.45 * reach);
            Vec3d b1 = start + Vec3d.Up * c, b2 = head - aim * c;
            double lying = Smooth((head.Z - 0.3) / 0.9);
            double run = Math.Max(0.6, head.Z);
            // How far back from the head it starts to lift off the ground.
            double lift = Math.Min(run * 0.6, 0.5 + 1.3 * Math.Max(0.0, head.Y - Lie));
            for (int i = 0; i <= Samples; i++)
            {
                double u = i / (double)Samples, w = 1.0 - u;
                Vec3d standing = start * (w * w * w) + b1 * (3.0 * w * w * u) + b2 * (3.0 * w * u * u) + head * (u * u * u);
                if (lying <= 0.0)
                {
                    _line[i] = standing;
                    continue;
                }
                Vec3d lain;
                if (u < 0.2)
                {
                    // Over the lip: a quarter turn from straight up to along the ground.
                    double a = u / 0.2 * Math.PI * 0.5;
                    lain = new Vec3d(0, Lie - Lip + Lip * Math.Sin(a), Lip - Lip * Math.Cos(a));
                }
                else
                {
                    double g = Lip + (run - Lip) * (u - 0.2) / 0.8;
                    double h = Lie + (head.Y - Lie) * Smooth((g - (run - lift)) / Math.Max(0.05, lift));
                    lain = new Vec3d(head.X * (u - 0.2) / 0.8, h, g);
                }
                // Its start is a little higher than the standing line's, so the two meet at the mouth of the hole.
                if (u < 0.2) lain += new Vec3d(0, -Lie * (1.0 - u / 0.2), 0);
                _line[i] = standing + (lain - standing) * lying;
            }
            _line[Samples] = head;
            _along[0] = 0.0;
            for (int i = 1; i <= Samples; i++) _along[i] = _along[i - 1] + (_line[i] - _line[i - 1]).Magnitude;
        }

        /// <summary>The place this far along the line from its start; below the start it runs straight down the burrow.</summary>
        Vec3d At(double along)
        {
            if (along <= 0.0) return _line[0] + new Vec3d(0, along, 0);
            for (int i = 1; i <= Samples; i++)
            {
                if (along > _along[i]) continue;
                double span = _along[i] - _along[i - 1];
                double u = span > 1e-9 ? (along - _along[i - 1]) / span : 0.0;
                return _line[i - 1] + (_line[i] - _line[i - 1]) * u;
            }
            return _line[Samples];
        }
    }
}
