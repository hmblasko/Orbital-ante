using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public struct HunterAnimInput
    {
        /// <summary>Creature space, metres a second: what it really moved, not what it wanted to.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        public HunterMode Mode;
        public double T;
    }

    /// <summary>Where every piece of a Cave hunter is, in creature space. Only the first <see cref="Nodes"/> nodes are filled.</summary>
    public sealed class HunterPose
    {
        /// <summary>How many nodes are in use: the head and the segments still joined to it.</summary>
        public int Nodes;
        public readonly Vec3d[] NodePosition = new Vec3d[HunterLayout.Nodes];
        public readonly QuatD[] NodeRotation = new QuatD[HunterLayout.Nodes];
        public readonly Vec3d[] LegPosition = new Vec3d[HunterLayout.Legs];
        public readonly QuatD[] LegRotation = new QuatD[HunterLayout.Legs];
        public readonly Vec3d[] FangPosition = new Vec3d[2];
        public readonly QuatD[] FangRotation = new QuatD[2];
        public readonly Vec3d[] FeelerPosition = new Vec3d[2];
        public readonly QuatD[] FeelerRotation = new QuatD[2];
        public readonly Vec3d[] TailPosition = new Vec3d[2];
        public readonly QuatD[] TailRotation = new QuatD[2];
        /// <summary>0 flat on the ground, 1 reared as high as it goes.</summary>
        public double Rear;
        /// <summary>0 shut, 1 wide.</summary>
        public double Fangs;
    }

    /// <summary>
    /// A Cave hunter's movement, worked out every frame with no clips and no Unity types.
    ///
    /// - The body follows its head: every node is drawn along behind the one in front at a fixed distance, over the
    ///   ground it has really covered, so the body lies along the path the head took and a turn runs down it.
    /// - To strike, the front of the body is lifted joint by joint (steep at the bottom, curling over at the top),
    ///   which pulls the head back as well as up; the strike lays it flat again as the whole animal lunges.
    /// - Legs step in a wave that runs up the body, opposite sides out of step; lifted, they spread and grasp.
    /// - Just cut in two, it thrashes from side to side.
    /// </summary>
    public sealed class HunterAnimator
    {
        const double Stride = 0.55;
        /// <summary>How steeply the lowest lifted joint and the highest stand when it is reared, in radians.</summary>
        const double RearBase = 1.48, RearTop = -0.15;

        // The ground chain, in a frame of its own that does not turn with the animal.
        readonly double[] _gx = new double[HunterLayout.Nodes], _gz = new double[HunterLayout.Nodes];
        double _px, _pz, _heading;
        bool _laid;
        double _phase, _time, _moving, _rear, _slam, _writhe, _fangs;
        Vec3d _flinch, _flinchSpeed;

        public void Flinch(Vec3d push) => _flinchSpeed += push;

        static double Smooth(double x)
        {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>How far it is reared, 0 to 1, at a point in a mode.</summary>
        public static double RearOf(HunterMode mode, double t)
        {
            switch (mode)
            {
                // Up to nine tenths of its height while it still follows; the moment it has taken aim it draws
                // back to full height (and its fangs fly wide): that is the sign to move.
                case HunterMode.Rear: return t < HunterBrain.LockAt ? 0.88 * Smooth(t / (HunterBrain.LockAt * 0.9)) : 0.88 + 0.12 * Smooth((t - HunterBrain.LockAt) / 0.12);
                // It hangs for an instant, then comes down faster and faster.
                case HunterMode.Strike: return 1.0 - t * t;
                default: return 0.0;
            }
        }

        /// <summary>How wide the fangs are, 0 to 1.</summary>
        public static double FangsOf(HunterMode mode, double t)
        {
            switch (mode)
            {
                case HunterMode.Rear: return t < HunterBrain.LockAt ? 0.45 * Smooth(t / 0.3) : 1.0;
                case HunterMode.Strike: return 1.0 - Smooth((t - 0.6) / 0.4);
                case HunterMode.Writhe: return 0.6;
                default: return 0.0;
            }
        }

        /// <summary>How steeply lifted joint <paramref name="j"/> of <paramref name="count"/> stands (0 the lowest), fully reared.</summary>
        public static double RearPitch(int j, int count)
        {
            // It leaves the ground in a curve, not a corner: steeper joint by joint to upright two fifths of the way
            // up, then over forward to the head. With fewer joints to share the bend it stands less steeply.
            double u = (j + 0.5) / count;
            double steep = RearBase * Math.Min(1.0, 0.6 + 0.4 * count / 6.0);
            if (u < 0.4) return steep * (0.35 + 0.65 * Smooth(u / 0.4));
            return steep + (RearTop - steep) * Math.Pow((u - 0.4) / 0.6, 1.5);
        }

        void LayOut()
        {
            // Straight out behind the head.
            for (int i = 0; i < HunterLayout.Nodes; i++)
            {
                _gx[i] = _px - Math.Sin(_heading) * i * HunterLayout.Spacing;
                _gz[i] = _pz - Math.Cos(_heading) * i * HunterLayout.Spacing;
            }
            _laid = true;
        }

        public void Update(HunterBody body, in HunterAnimInput input, double dt, HunterPose pose)
        {
            if (!_laid) LayOut();
            _time += dt;
            HunterMode mode = input.Mode;
            int nodes = body.Kept + 1;
            pose.Nodes = nodes;

            // ---- the ground chain follows the head
            _heading += input.TurnRate * dt;
            double sin = Math.Sin(_heading), cos = Math.Cos(_heading);
            _px += (cos * input.Velocity.X + sin * input.Velocity.Z) * dt;
            _pz += (-sin * input.Velocity.X + cos * input.Velocity.Z) * dt;
            _gx[0] = _px;
            _gz[0] = _pz;
            for (int i = 1; i < HunterLayout.Nodes; i++)
            {
                double dx = _gx[i] - _gx[i - 1], dz = _gz[i] - _gz[i - 1];
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d < 1e-9)
                {
                    dx = -sin;
                    dz = -cos;
                    d = 1.0;
                }
                _gx[i] = _gx[i - 1] + dx / d * HunterLayout.Spacing;
                _gz[i] = _gz[i - 1] + dz / d * HunterLayout.Spacing;
            }

            double speed = input.Velocity.Z;
            bool running = mode == HunterMode.Run;
            _moving += ((running ? Math.Min(1.0, Math.Abs(speed) / 0.8) : 0.0) - _moving) * Math.Min(1.0, dt * 8.0);
            _phase += speed * dt / Stride * 2.0 * Math.PI;
            double writheTarget = mode == HunterMode.Writhe ? Math.Sin(Math.PI * Math.Max(0.0, Math.Min(1.0, input.T))) : 0.0;
            _writhe += (writheTarget - _writhe) * Math.Min(1.0, dt * 14.0);
            // Standing with nothing to do it lifts its head a little and tastes the air.
            double idle = running ? (1.0 - _moving) * (0.10 + 0.04 * Math.Sin(_time * 1.3)) : 0.0;
            double rearTarget = Math.Max(RearOf(mode, input.T), idle);
            _rear += (rearTarget - _rear) * Math.Min(1.0, dt * 30.0);
            double slamTarget = mode == HunterMode.Strike ? input.T * input.T * input.T : mode == HunterMode.Recover ? 1.0 - Smooth((input.T - 0.6) / 0.4) : 0.0;
            _slam += (slamTarget - _slam) * Math.Min(1.0, dt * 30.0);
            _fangs += (Math.Max(FangsOf(mode, input.T), 0.08 + 0.06 * Math.Sin(_time * 3.1)) - _fangs) * Math.Min(1.0, dt * 25.0);
            pose.Rear = _rear;
            pose.Fangs = _fangs;

            // A knock: a small spring.
            _flinchSpeed += (_flinch * -120.0 - _flinchSpeed * 14.0) * dt;
            _flinch += _flinchSpeed * dt;
            var knock = new Vec3d(_flinch.X * 0.04, Math.Max(0.0, _flinch.Y) * 0.03, 0);

            // ---- flat on the ground, in creature space, with the running wave and the thrash from side to side
            for (int i = 0; i < nodes; i++)
            {
                double dx = _gx[i] - _px, dz = _gz[i] - _pz;
                var flat = new Vec3d(cos * dx - sin * dz, HunterLayout.RideHeight, sin * dx + cos * dz);
                int a = Math.Max(0, i - 1), b = Math.Min(nodes - 1, i + 1);
                double ax = _gx[a] - _gx[b], az = _gz[a] - _gz[b];
                double al = Math.Sqrt(ax * ax + az * az);
                // To the animal's right of the line of the body here.
                var side = al > 1e-9 ? new Vec3d((cos * az + sin * ax) / al, 0, (sin * az - cos * ax) / al) : Vec3d.Right;
                double taper = i == 0 ? 0.3 : i == 1 ? 0.65 : 1.0;
                double wave = 0.045 * _moving * Math.Sin(_phase - i * 0.75) + 0.20 * _writhe * Math.Sin(_time * 17.0 - i * 0.9);
                // A knock is felt most in the middle of what is left of it.
                double mid = Math.Sin(Math.PI * i / Math.Max(1.0, nodes - 1.0));
                pose.NodePosition[i] = flat + side * (wave * taper) + knock * (0.4 + 0.6 * mid);
            }

            // ---- the front lifted joint by joint from the last node left on the ground
            int lifted = Math.Min(body.RearSegs + 1, nodes - 1);
            Vec3d above = pose.NodePosition[lifted];
            for (int i = lifted - 1; i >= 0; i--)
            {
                Vec3d along = pose.NodePosition[i] - pose.NodePosition[i + 1];
                double pitch = _rear * RearPitch(lifted - 1 - i, lifted);
                // (Node i + 1 has not been moved yet when its flat place is read here: the loop goes head-ward.)
                var raised = above + new Vec3d(along.X, 0, along.Z) * Math.Cos(pitch) + Vec3d.Up * (along.Magnitude * Math.Sin(pitch));
                above = raised;
                _lift[i] = raised;
            }
            for (int i = 0; i < lifted; i++) pose.NodePosition[i] = _lift[i];
            // The head comes down on the ground and lies there.
            pose.NodePosition[0] += new Vec3d(0, -0.15 * _slam, 0);
            if (nodes > 1) pose.NodePosition[1] += new Vec3d(0, -0.06 * _slam, 0);

            // ---- which way each piece faces
            for (int i = 0; i < nodes; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(nodes - 1, i + 1);
                Vec3d f = (pose.NodePosition[a] - pose.NodePosition[b]).Normalized;
                var level = new Vec3d(f.X, 0, f.Z);
                level = level.SqrMagnitude > 1e-9 ? level.Normalized : Vec3d.Forward;
                // The back of a piece that stands on end faces back the way the body came.
                Vec3d up = Vec3d.Up - level * f.Y;
                QuatD r = WalkerAnimator.Look(f, up);
                if (i == 0) r = r * QuatD.AngleAxis(Vec3d.Right, 0.75 * _rear - 0.10 * _slam);
                pose.NodeRotation[i] = r.Normalized;
            }

            // ---- head parts
            QuatD head = pose.NodeRotation[0];
            for (int side = 0; side < 2; side++)
            {
                double s = side == HunterLayout.RightSide ? 1.0 : -1.0;
                var root = new Vec3d(s * HunterLayout.FangRoot.X, HunterLayout.FangRoot.Y, HunterLayout.FangRoot.Z);
                pose.FangPosition[side] = pose.NodePosition[0] + head.Rotate(root);
                QuatD open = QuatD.AngleAxis(Vec3d.Up, s * 0.75 * _fangs);
                // The left fang is the right one rolled half a turn.
                if (side == HunterLayout.LeftSide) open = open * QuatD.AngleAxis(Vec3d.Forward, Math.PI);
                pose.FangRotation[side] = (head * open).Normalized;

                root = new Vec3d(s * HunterLayout.FeelerRoot.X, HunterLayout.FeelerRoot.Y, HunterLayout.FeelerRoot.Z);
                pose.FeelerPosition[side] = pose.NodePosition[0] + head.Rotate(root);
                double yaw = s * (0.50 + 0.22 * Math.Sin(_time * 2.3 + side * 1.9) + 0.45 * _rear + 0.3 * _writhe * Math.Sin(_time * 13.0 + side));
                double pitch = -0.10 + 0.16 * Math.Sin(_time * 1.7 + side * 2.6) - 0.5 * _rear;
                pose.FeelerRotation[side] = (head * QuatD.AngleAxis(Vec3d.Up, yaw) * QuatD.AngleAxis(Vec3d.Right, pitch)).Normalized;
            }

            // ---- legs
            for (int leg = 0; leg < body.Kept * 2; leg++)
            {
                int seg = HunterLayout.SegOf(leg), node = seg + 1, sideOf = HunterLayout.SideOf(leg);
                double s = HunterLayout.Sign(leg);
                double inAir = Math.Max(0.0, Math.Min(1.0, (pose.NodePosition[node].Y - HunterLayout.RideHeight - 0.05) / 0.25));
                double wave = _phase - seg * 0.8 + (sideOf == HunterLayout.LeftSide ? Math.PI : 0.0);
                double thrash = _writhe * Math.Sin(_time * 21.0 - seg * 1.3 + sideOf * 2.0);
                var walking = new Vec3d(s * 0.80, -0.50 + 0.30 * Math.Max(0.0, Math.Cos(wave)) * _moving + 0.35 * Math.Max(0.0, thrash), 0.50 * Math.Sin(wave) * _moving + 0.3 * thrash);
                var grasping = new Vec3d(s * 0.75, -0.10 + 0.22 * Math.Sin(_time * 7.0 - seg * 0.9 + sideOf * Math.PI), 0.30);
                Vec3d way = (walking + (grasping - walking) * inAir).Normalized;
                QuatD frame = pose.NodeRotation[node];
                pose.LegPosition[leg] = pose.NodePosition[node] + frame.Rotate(HunterLayout.HipOf(leg));
                pose.LegRotation[leg] = (frame * WalkerAnimator.Look(way, new Vec3d(s * 0.4, 0.9, 0))).Normalized;
            }

            // ---- the two long legs it trails
            if (body.HasTail)
            {
                int last = nodes - 1;
                QuatD frame = pose.NodeRotation[last];
                for (int side = 0; side < 2; side++)
                {
                    double s = side == HunterLayout.RightSide ? 1.0 : -1.0;
                    var root = new Vec3d(s * HunterLayout.TailRoot.X, HunterLayout.TailRoot.Y, HunterLayout.TailRoot.Z);
                    pose.TailPosition[side] = pose.NodePosition[last] + frame.Rotate(root);
                    var way = new Vec3d(s * (0.30 + 0.10 * Math.Sin(_time * 2.1 + side * 2.0) + 0.3 * _writhe * Math.Sin(_time * 15.0 + side)), 0.10 + 0.06 * Math.Sin(_time * 1.6 + side), -1.0);
                    pose.TailRotation[side] = (frame * WalkerAnimator.Look(way, Vec3d.Up)).Normalized;
                }
            }
        }

        readonly Vec3d[] _lift = new Vec3d[HunterLayout.Nodes];
    }
}
