using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public struct RollerAnimInput
    {
        /// <summary>Creature space, metres a second.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        public RollerMode Mode;
        public double T;
    }

    /// <summary>Where every piece of a Roller is, in creature space (the game's sizes are applied by whoever draws it).</summary>
    public sealed class RollerPose
    {
        public readonly Vec3d[] LinkPosition = new Vec3d[RollerLayout.Links];
        public readonly QuatD[] LinkRotation = new QuatD[RollerLayout.Links];
        public readonly Vec3d[] LegPosition = new Vec3d[RollerLayout.Legs];
        public readonly QuatD[] LegRotation = new QuatD[RollerLayout.Legs];
        /// <summary>0 stretched out, 1 a closed ball.</summary>
        public double Curl;
        /// <summary>The middle of the body (of the ball, when it is one).</summary>
        public Vec3d Centre;
    }

    /// <summary>
    /// A Roller's movement, worked out every frame with no clips and no Unity types. The body is a chain of nine links
    /// bent at its eight joints: flat when it walks, a closed ring when it is a ball. The ball turns by exactly the
    /// distance it rolls, so it never slides. A roll ends upside down: it falls open on its back, and gets up by half
    /// curling and rocking on round. Legs step in a wave that runs up the body, fold flat inside the ball, and wave in
    /// the air on its back.
    /// </summary>
    public sealed class RollerAnimator
    {
        const double Stride = 0.5;

        double _curl, _spin, _rightFrom, _phase, _time, _moving;
        RollerMode _last = RollerMode.Walk;
        Vec3d _flinch, _flinchSpeed;

        /// <summary>How far the body has turned head over tail, in radians (a whole turn brings it upright again).</summary>
        public double Spin => _spin;

        public void Flinch(Vec3d push) => _flinchSpeed += push;

        static double Smooth(double x)
        {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>How tightly it is curled, 0 to 1, at a point in a mode.</summary>
        public static double CurlOf(RollerMode mode, double t)
        {
            switch (mode)
            {
                case RollerMode.Wind:
                    // Head and tail dip while it still takes aim; the moment it has, it snaps shut. Closing is the sign.
                    return t < RollerBrain.LockAt ? 0.45 * Smooth(t / RollerBrain.LockAt) : 0.45 + 0.55 * Smooth((t - RollerBrain.LockAt) / (1.0 - RollerBrain.LockAt));
                case RollerMode.Roll: return 1.0;
                case RollerMode.Open: return 1.0 - Smooth(t);
                case RollerMode.Righting: return 0.7 * Math.Sin(Math.PI * Math.Max(0.0, Math.Min(1.0, t)));
                default: return 0.0;
            }
        }

        /// <summary>How far the head is thrown back (negative) or down (positive) through a butt, in radians.</summary>
        public static double ButtPitch(double t)
        {
            const double lands = RollerBrain.ButtLands, top = 0.55;
            if (t < top) return -0.5 * Smooth(t / top);
            if (t < lands) return -0.5 + 0.8 * (t - top) / (lands - top);
            return 0.3 * (1.0 - Smooth((t - lands) / (1.0 - lands)));
        }

        static double Nearest(double value, double step, double offset) => Math.Round((value - offset) / step) * step + offset;

        public void Update(RollerBody body, in RollerAnimInput input, double dt, RollerPose pose)
        {
            _time += dt;
            double speed = input.Velocity.Z;
            RollerMode mode = input.Mode;

            // Head over tail: a ball turns by what it rolls; open, it settles on its back; righting carries it on round.
            if (mode == RollerMode.Righting)
            {
                if (_last != RollerMode.Righting) _rightFrom = _spin;
                _spin = _rightFrom + Math.PI * Smooth(input.T);
            }
            else if (mode == RollerMode.Roll)
            {
                _spin += speed * dt / RollerLayout.BallRadius;
            }
            else
            {
                bool onBack = mode == RollerMode.Open || mode == RollerMode.OnBack;
                double target = onBack ? Nearest(_spin, 2.0 * Math.PI, Math.PI) : Nearest(_spin, 2.0 * Math.PI, 0.0);
                _spin += Math.Max(-9.0 * dt, Math.Min(9.0 * dt, target - _spin));
            }
            _last = mode;

            double curlTarget = CurlOf(mode, input.T);
            _curl += (curlTarget - _curl) * Math.Min(1.0, dt * 20.0);
            double curl = _curl;
            pose.Curl = curl;

            bool onFoot = mode == RollerMode.Walk || mode == RollerMode.Butt;
            _moving += ((onFoot ? Math.Min(1.0, Math.Abs(speed) / 0.3 + Math.Abs(input.TurnRate) / 1.2) : 0.0) - _moving) * Math.Min(1.0, dt * 8.0);
            if (onFoot) _phase += (speed + Math.Sign(speed == 0.0 ? 1.0 : speed) * Math.Abs(input.TurnRate) * 0.25) * dt / Stride * 2.0 * Math.PI;

            // ---- the chain, built out from the middle link both ways
            double bend = curl * 2.0 * Math.PI * RollerLayout.CurlTurns;
            double between = RollerLayout.TotalLength - 0.5 * (RollerLayout.HeadLength + RollerLayout.TailLength);
            double sway = Math.Max(-0.12, Math.Min(0.12, input.TurnRate * 0.07)) * (1.0 - curl);
            double butt = mode == RollerMode.Butt ? ButtPitch(input.T) : 0.0;
            int mid = RollerLayout.MiddleLink;
            pose.LinkPosition[mid] = Vec3d.Zero;
            pose.LinkRotation[mid] = QuatD.Identity;
            for (int i = mid - 1; i >= 0; i--)
            {
                double pitch = bend * 0.5 * (RollerLayout.Length(i) + RollerLayout.Length(i + 1)) / between;
                if (i == 0) pitch += butt;
                else if (i <= 2) pitch += butt * 0.3;
                double yaw = sway + 0.035 * Math.Sin(_phase - i * 0.9) * _moving * (1.0 - curl);
                QuatD before = pose.LinkRotation[i + 1];
                QuatD r = (before * QuatD.AngleAxis(Vec3d.Up, yaw) * QuatD.AngleAxis(Vec3d.Right, pitch)).Normalized;
                Vec3d joint = pose.LinkPosition[i + 1] + before.Rotate(new Vec3d(0, 0, 0.5 * RollerLayout.Length(i + 1)));
                pose.LinkRotation[i] = r;
                pose.LinkPosition[i] = joint + r.Rotate(new Vec3d(0, 0, 0.5 * RollerLayout.Length(i)));
            }
            for (int i = mid + 1; i < RollerLayout.Links; i++)
            {
                double pitch = bend * 0.5 * (RollerLayout.Length(i) + RollerLayout.Length(i - 1)) / between;
                double yaw = sway + 0.035 * Math.Sin(_phase - (i - 1) * 0.9) * _moving * (1.0 - curl);
                QuatD before = pose.LinkRotation[i - 1];
                QuatD r = (before * QuatD.AngleAxis(Vec3d.Up, -yaw) * QuatD.AngleAxis(Vec3d.Right, -pitch)).Normalized;
                Vec3d joint = pose.LinkPosition[i - 1] + before.Rotate(new Vec3d(0, 0, -0.5 * RollerLayout.Length(i - 1)));
                pose.LinkRotation[i] = r;
                pose.LinkPosition[i] = joint + r.Rotate(new Vec3d(0, 0, -0.5 * RollerLayout.Length(i)));
            }

            // ---- turned head over tail about its own middle, then stood on the ground
            QuatD over = QuatD.AngleAxis(Vec3d.Right, _spin);
            Vec3d centre = Vec3d.Zero;
            for (int i = 0; i < RollerLayout.Links; i++) centre += pose.LinkPosition[i];
            centre = centre / RollerLayout.Links;
            double lowest = double.MaxValue;
            for (int i = 0; i < RollerLayout.Links; i++)
            {
                pose.LinkPosition[i] = over.Rotate(pose.LinkPosition[i] - centre);
                pose.LinkRotation[i] = (over * pose.LinkRotation[i]).Normalized;
                double half = 0.5 * RollerLayout.Length(i), top = RollerLayout.Top(i), bottom = RollerLayout.Bottom(i);
                for (int k = -1; k <= 1; k++)
                {
                    double high = k == 0 ? 1.0 : 0.85;
                    Vec3d back = pose.LinkPosition[i] + pose.LinkRotation[i].Rotate(new Vec3d(0, top * high, half * k));
                    Vec3d belly = pose.LinkPosition[i] + pose.LinkRotation[i].Rotate(new Vec3d(0, -bottom * high, half * k));
                    lowest = Math.Min(lowest, Math.Min(back.Y, belly.Y));
                }
            }
            // On its feet the legs hold it clear of the ground, lower for each leg lost; a ball rides on its own radius.
            double upright = Math.Max(0.0, Math.Cos(_spin));
            double clear = (RollerLayout.StandHeight - RollerLayout.BellyDepth) * (1.0 - curl) * upright * (0.4 + 0.6 * body.Legs / RollerLayout.Legs);
            double resting = -lowest + clear;
            double ball = Smooth((curl - 0.5) / 0.4);
            double lift = resting + (RollerLayout.BallRadius - resting) * ball;

            // A knock: a small spring.
            _flinchSpeed += (_flinch * -120.0 - _flinchSpeed * 14.0) * dt;
            _flinch += _flinchSpeed * dt;
            var shift = new Vec3d(_flinch.X * 0.05, lift + Math.Max(0.0, _flinch.Y) * 0.04, _flinch.Z * 0.05);
            if (mode == RollerMode.Butt) shift += new Vec3d(0, 0, 0.3 * Math.Max(0.0, ButtPitch(input.T) + 0.1));
            for (int i = 0; i < RollerLayout.Links; i++) pose.LinkPosition[i] += shift;
            pose.Centre = shift;

            // ---- legs
            double onBackShare = Math.Max(0.0, -Math.Cos(_spin));
            for (int leg = 0; leg < RollerLayout.Legs; leg++)
            {
                int seg = RollerLayout.SegOf(leg), link = seg + 1;
                double s = RollerLayout.Sign(leg);
                double wave = _phase - seg * 0.9 + (RollerLayout.SideOf(leg) == RollerLayout.LeftSide ? Math.PI : 0.0);
                var walking = new Vec3d(s * 0.55, -0.80 + 0.28 * Math.Max(0.0, Math.Cos(wave)) * _moving, 0.42 * Math.Sin(wave) * _moving);
                var waving = new Vec3d(s * 0.45, -0.85, 0.5 * Math.Sin(_time * 11.0 - seg * 0.8 + RollerLayout.SideOf(leg) * Math.PI));
                var tucked = new Vec3d(-s * 0.80, -0.45, 0.1);
                Vec3d open = walking + (waving - walking) * onBackShare;
                Vec3d way = (open + (tucked - open) * Smooth(curl * 1.4)).Normalized;
                QuatD frame = pose.LinkRotation[link];
                pose.LegPosition[leg] = pose.LinkPosition[link] + frame.Rotate(RollerLayout.HipOf(leg));
                pose.LegRotation[leg] = (frame * WalkerAnimator.Look(way, new Vec3d(s * 0.8, 0.6, 0))).Normalized;
            }
        }
    }
}
