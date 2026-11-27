using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public struct BiterAnimInput
    {
        /// <summary>Creature space, metres a second: what it really moved, not what it wanted to (x right, y up, z ahead).</summary>
        public Vec3d Velocity;
        /// <summary>Radians a second about up, as it really turned.</summary>
        public double TurnRate;
        public BiterMode Mode;
        public double T;
        /// <summary>From its body: which way it lists with a front fin gone (+1 right, -1 left), and whether its back fin is on.</summary>
        public double List;
        public bool BackFinOn;
    }

    /// <summary>Where every piece of a Pack biter is, in creature space.</summary>
    public sealed class BiterPose
    {
        public readonly Vec3d[] NodePosition = new Vec3d[BiterLayout.Nodes];
        public readonly QuatD[] NodeRotation = new QuatD[BiterLayout.Nodes];
        public Vec3d JawPosition;
        public QuatD JawRotation;
        public readonly Vec3d[] FinPosition = new Vec3d[BiterLayout.Fins];
        public readonly QuatD[] FinRotation = new QuatD[BiterLayout.Fins];
        public Vec3d BackFinPosition, TailFinPosition;
        public QuatD BackFinRotation, TailFinRotation;
        /// <summary>The front of its mouth: where a bite lands.</summary>
        public Vec3d Mouth;
        /// <summary>0 shut, 1 as wide as it goes.</summary>
        public double Gape;
    }

    /// <summary>
    /// A Pack biter's movement, worked out every frame with no clips and no Unity types. The Drifting maw's
    /// animator (<see cref="MawAnimator"/>) made quicker and stiffer for a hard-bodied river hunter.
    ///
    /// - The body follows its head through the water it has really covered; a quick, short wave runs down it, most
    ///   of it in the tail fin.
    /// - Its lengths are laid joint to joint, so the one skin never parts.
    /// - The warning is seen three ways at once: its jaws flare open, it rolls onto its side so its silver flank
    ///   flashes, and its front fins sweep back. The moment it takes aim it snaps level, jaws wide, and darts.
    /// - It leans into a turn, lists to the side of a lost front fin, and rolls without its back fin.
    /// </summary>
    public sealed class BiterAnimator
    {
        readonly Vec3d[] _g = new Vec3d[BiterLayout.Nodes];
        Vec3d _p;
        double _heading, _phase, _time, _swim, _gape = 0.10, _flash, _bank, _thrash, _brace;
        bool _laid;
        Vec3d _flinch, _flinchSpeed;

        public void Flinch(Vec3d push) => _flinchSpeed += push;

        static double Smooth(double x)
        {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>How wide its mouth is at a point in a mode, 0 to 1.</summary>
        public static double GapeOf(BiterMode mode, double t)
        {
            switch (mode)
            {
                // Its teeth always show: the jaw hangs a little open as it swims.
                case BiterMode.Shoal: return 0.10;
                case BiterMode.Ring: return 0.16;
                // The warning: the jaws flare wide and stay wide the moment it has taken aim.
                case BiterMode.Mark: return t < BiterBrain.LockAt ? 0.2 + 0.5 * Smooth(t / BiterBrain.LockAt) : 1.0;
                case BiterMode.Dart: return t < 0.85 ? 1.0 : 0.05;
                case BiterMode.Peel: return 0.05;
                case BiterMode.Scatter: return 0.4;
                case BiterMode.Flee: return 0.2;
                default: return 0.12;
            }
        }

        void LayOut()
        {
            for (int i = 0; i < BiterLayout.Nodes; i++)
                _g[i] = _p - new Vec3d(Math.Sin(_heading), 0, Math.Cos(_heading)) * (i * BiterLayout.Spacing);
            _laid = true;
        }

        static QuatD Facing(Vec3d f, double bank)
        {
            var level = new Vec3d(f.X, 0, f.Z);
            level = level.SqrMagnitude > 1e-9 ? level.Normalized : Vec3d.Forward;
            Vec3d up = Vec3d.Up - level * f.Y;
            // A lean to its right is a turn the other way about its own nose.
            return (WalkerAnimator.Look(f, up) * QuatD.AngleAxis(Vec3d.Forward, -bank)).Normalized;
        }

        public void Evaluate(double dt, in BiterAnimInput input, BiterPose pose)
        {
            if (!_laid) LayOut();
            dt = Math.Max(0.0, Math.Min(0.1, dt));
            _time += dt;
            int nodes = BiterLayout.Nodes;
            BiterMode mode = input.Mode;

            // ---- the head goes where it really went, and the body is drawn along behind it
            _heading += input.TurnRate * dt;
            double sin = Math.Sin(_heading), cos = Math.Cos(_heading);
            Vec3d v = input.Velocity;
            _p += new Vec3d(cos * v.X + sin * v.Z, v.Y, -sin * v.X + cos * v.Z) * dt;
            _g[0] = _p;
            for (int i = 1; i < nodes; i++)
            {
                Vec3d d = _g[i] - _g[i - 1];
                double l = d.Magnitude;
                _g[i] = l > 1e-9 ? _g[i - 1] + d * (BiterLayout.Spacing / l) : _g[i - 1] - new Vec3d(sin, 0, cos) * BiterLayout.Spacing;
            }

            double speed = v.Magnitude;
            double ease = Math.Min(1.0, dt * 3.0);
            _swim += (Math.Min(1.0, 0.35 + speed / 4.0) - _swim) * ease;
            _phase += dt * (3.0 + 2.2 * Math.Min(speed, 6.0));
            _thrash += ((mode == BiterMode.Scatter ? 1.0 : 0.0) - _thrash) * Math.Min(1.0, dt * 8.0);
            _brace += ((mode == BiterMode.Dart || (mode == BiterMode.Mark && input.T >= BiterBrain.LockAt) ? 1.0 : 0.0) - _brace) * Math.Min(1.0, dt * 9.0);
            // The flash: through the warning it rolls onto its side, showing the silver of its flank, and snaps level as it takes aim.
            _flash += ((mode == BiterMode.Mark && input.T < BiterBrain.LockAt ? 0.75 * Math.Sin(Math.PI * input.T / BiterBrain.LockAt) : 0.0) - _flash) * Math.Min(1.0, dt * 14.0);
            _gape += (GapeOf(mode, input.T) - _gape) * Math.Min(1.0, dt * (mode == BiterMode.Mark || mode == BiterMode.Dart ? 24.0 : 8.0));
            double bankWant = 0.22 * input.TurnRate + 0.25 * input.List + _flash + (input.BackFinOn ? 0.0 : 0.20 * Math.Sin(_time * 3.1));
            _bank += (bankWant - _bank) * Math.Min(1.0, dt * 8.0);

            // A knock: a small spring.
            _flinchSpeed += (_flinch * -90.0 - _flinchSpeed * 12.0) * dt;
            _flinch += _flinchSpeed * dt;
            Vec3d knock = _flinch * 0.05;

            // ---- nodes in creature space, with the wave that drives it
            for (int i = 0; i < nodes; i++)
            {
                Vec3d rel = _g[i] - _p;
                var at = new Vec3d(cos * rel.X - sin * rel.Z, rel.Y, sin * rel.X + cos * rel.Z);
                int a = Math.Max(0, i - 1), b = Math.Min(nodes - 1, i + 1);
                Vec3d run = _g[a] - _g[b];
                var flat = new Vec3d(cos * run.X - sin * run.Z, 0, sin * run.X + cos * run.Z);
                Vec3d side = flat.SqrMagnitude > 1e-9 ? Vec3d.Cross(Vec3d.Up, flat.Normalized) : Vec3d.Right;
                double along = (double)i / (nodes - 1);
                // The head hardly moves; the tail sweeps widest.
                double wave = (0.004 + 0.07 * along * along) * _swim * Math.Sin(_phase - i * 0.75)
                    + 0.10 * _thrash * Math.Sin(_time * 18.0 - i * 0.9) * (0.4 + 0.6 * along);
                double mid = Math.Sin(Math.PI * along);
                pose.NodePosition[i] = at + side * wave + knock * (0.4 + 0.6 * mid);
            }
            for (int i = 0; i < nodes; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(nodes - 1, i + 1);
                Vec3d f = pose.NodePosition[a] - pose.NodePosition[b];
                pose.NodeRotation[i] = Facing(f.SqrMagnitude > 1e-9 ? f.Normalized : Vec3d.Forward, _bank * (1.0 - 0.5 * i / (nodes - 1.0)));
            }

            // ---- joint to joint: two lengths always meet at the same point
            Vec3d neck = (pose.NodePosition[0] + pose.NodePosition[1]) * 0.5;
            Vec3d front = neck;
            for (int i = 1; i < nodes; i++)
            {
                Vec3d back = i + 1 < nodes
                    ? (pose.NodePosition[i] + pose.NodePosition[i + 1]) * 0.5
                    : pose.NodePosition[i] + (pose.NodePosition[i] - front);
                Vec3d f = front - back;
                if (f.SqrMagnitude > 1e-9)
                {
                    f = f.Normalized;
                    pose.NodeRotation[i] = Facing(f, _bank * (1.0 - 0.5 * i / (nodes - 1.0)));
                    pose.NodePosition[i] = front - f * (BiterLayout.Spacing * 0.5);
                }
                front = back;
            }

            // ---- the head: its nose comes up a little as its jaw drops; it turns about the neck joint
            pose.Gape = _gape;
            QuatD head = (pose.NodeRotation[0] * QuatD.AngleAxis(Vec3d.Right, -0.12 * _gape)).Normalized;
            var toNeck = new Vec3d(0, 0, -BiterLayout.Spacing * 0.5);
            pose.NodePosition[0] = neck - head.Rotate(toNeck);
            pose.NodeRotation[0] = head;
            pose.JawPosition = pose.NodePosition[0] + head.Rotate(BiterLayout.JawHinge);
            pose.JawRotation = (head * QuatD.AngleAxis(Vec3d.Right, _gape * BiterLayout.JawSwing)).Normalized;
            pose.Mouth = pose.NodePosition[0] + head.Rotate(BiterLayout.Mouth);

            // ---- front fins: out, back and a little down, beating slowly; spread flat and forward to brake
            int finNode = BiterLayout.FinSeg + 1;
            for (int s = 0; s < BiterLayout.Fins; s++)
            {
                double sign = BiterLayout.Sign(s);
                double beat = Math.Sin(_time * 3.2 + s * 0.6);
                double droop = 0.30 + 0.12 * beat + 0.10 * _brace + 0.18 * Math.Max(0.0, sign * input.TurnRate);
                double sweep = 0.75 + 0.45 * _brace + 0.10 * beat;
                var way = new Vec3d(sign * Math.Cos(droop) * Math.Cos(sweep), -Math.Sin(droop), -Math.Sin(sweep) * Math.Cos(droop));
                // The fin is the same above as below: the right one is the piece turned over, so its leading edge is ahead on both sides.
                Vec3d flatUp = s == BiterLayout.RightSide ? -Vec3d.Up : Vec3d.Up;
                QuatD local = WalkerAnimator.Look(way, flatUp);
                pose.FinPosition[s] = pose.NodePosition[finNode] + pose.NodeRotation[finNode].Rotate(BiterLayout.FinRootOf(s));
                pose.FinRotation[s] = (pose.NodeRotation[finNode] * local).Normalized;
            }

            int backNode = BiterLayout.BackFinSeg + 1;
            pose.BackFinPosition = pose.NodePosition[backNode] + pose.NodeRotation[backNode].Rotate(BiterLayout.BackFinRoot);
            pose.BackFinRotation = pose.NodeRotation[backNode];

            // ---- the tail fin swings a little further than the tail that carries it
            int last = nodes - 1;
            double swing = 0.30 * _swim * Math.Sin(_phase - last * 0.75 - 0.9) + 0.4 * _thrash * Math.Sin(_time * 18.0 - last * 0.9 - 0.9);
            pose.TailFinPosition = pose.NodePosition[last] + pose.NodeRotation[last].Rotate(BiterLayout.TailFinRoot);
            pose.TailFinRotation = (pose.NodeRotation[last] * QuatD.AngleAxis(Vec3d.Up, -swing)).Normalized;
        }
    }
}
