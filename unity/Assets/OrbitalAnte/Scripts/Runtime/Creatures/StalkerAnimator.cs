using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public struct StalkerAnimInput
    {
        /// <summary>Creature space, metres a second: what it really moved, not what it wanted to.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        public StalkerMode Mode;
        public double T;
        /// <summary>The place it has in mind, in creature space: its head turns to it.</summary>
        public bool HasPlace;
        public Vec3d Place;
        /// <summary>It hears or feels the player this frame: its gills stand out.</summary>
        public bool Aware;
        /// <summary>It has just taken a new place: its gills kick out.</summary>
        public bool Took;
    }

    /// <summary>Where every piece of a Blind stalker is, in creature space. Only the first <see cref="Nodes"/> nodes are filled.</summary>
    public sealed class StalkerPose
    {
        /// <summary>How many nodes are in use: the head and the lengths still joined to it.</summary>
        public int Nodes;
        public readonly Vec3d[] NodePosition = new Vec3d[StalkerLayout.Nodes];
        public readonly QuatD[] NodeRotation = new QuatD[StalkerLayout.Nodes];
        public readonly Vec3d[] LegPosition = new Vec3d[StalkerLayout.Legs];
        public readonly QuatD[] LegRotation = new QuatD[StalkerLayout.Legs];
        public readonly Vec3d[] ShinPosition = new Vec3d[StalkerLayout.Legs];
        public readonly QuatD[] ShinRotation = new QuatD[StalkerLayout.Legs];
        /// <summary>Where each foot is, for tests and for dust.</summary>
        public readonly Vec3d[] Foot = new Vec3d[StalkerLayout.Legs];
        public readonly Vec3d[] GillPosition = new Vec3d[StalkerLayout.Gills];
        public readonly QuatD[] GillRotation = new QuatD[StalkerLayout.Gills];
        public Vec3d JawPosition;
        public QuatD JawRotation;
        /// <summary>0 shut, 1 as wide as it goes.</summary>
        public double Gape;
        /// <summary>0 gills lying back, 1 spread wide.</summary>
        public double Flare;
    }

    /// <summary>
    /// A Blind stalker's movement, worked out every frame with no clips and no Unity types.
    ///
    /// - The body follows its head over the ground it has really covered (like the Cave hunter's), and bends from side
    ///   to side as it walks, more toward the tail.
    /// - Its feet stay planted in the world and step when they fall behind, a diagonal pair at a time; the legs are
    ///   bent to reach them, elbows out. With fewer than two legs it lies on its belly and swims along the ground.
    /// - Still, it sinks onto its belly and hardly breathes. Hearing something, its gills stand out and its head comes
    ///   round. The warning lifts its head and drops its jaw; at the moment it takes aim the jaw falls wide and the
    ///   gills snap out. The lunge straightens it like a thrown rope.
    /// </summary>
    public sealed class StalkerAnimator
    {
        const double StepAt = 0.20, FootOut = 0.34, Lift = 0.10;
        public const double BellyHeight = 0.20;

        readonly double[] _gx = new double[StalkerLayout.Nodes], _gz = new double[StalkerLayout.Nodes];
        // Feet, in the same frame as the chain.
        readonly double[] _fx = new double[StalkerLayout.Legs], _fz = new double[StalkerLayout.Legs];
        readonly double[] _fromX = new double[StalkerLayout.Legs], _fromZ = new double[StalkerLayout.Legs], _swing = new double[StalkerLayout.Legs], _swingTime = new double[StalkerLayout.Legs];
        readonly bool[] _inAir = new bool[StalkerLayout.Legs];
        double _px, _pz, _heading;
        bool _laid;
        double _phase, _time, _moving, _height = BellyHeight, _gape, _flare, _lookYaw, _thrash, _whip, _lift;
        Vec3d _flinch, _flinchSpeed;

        public void Flinch(Vec3d push) => _flinchSpeed += push;

        static double Smooth(double x)
        {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>How wide its jaw hangs at a point in a mode, 0 to 1.</summary>
        public static double GapeOf(StalkerMode mode, double t)
        {
            switch (mode)
            {
                // It opens slowly, and falls wide the moment it has taken aim: that is the sign to move.
                case StalkerMode.Gape: return t < StalkerBrain.LockAt ? 0.45 * Smooth(t / StalkerBrain.LockAt) : 0.45 + 0.55 * Smooth((t - StalkerBrain.LockAt) / 0.12);
                case StalkerMode.Lunge: return t < StalkerBrain.BiteAt ? 1.0 : 1.0 - Smooth((t - StalkerBrain.BiteAt) / 0.2);
                case StalkerMode.Reel: return 0.7;
                default: return 0.0;
            }
        }

        /// <summary>How far its gills stand out, 0 to 1.</summary>
        public static double FlareOf(StalkerMode mode, double t, bool aware)
        {
            switch (mode)
            {
                case StalkerMode.Listen: return 0.75;
                case StalkerMode.Gape: return t < StalkerBrain.LockAt ? 0.75 : 1.0;
                case StalkerMode.Lunge: return 1.0;
                case StalkerMode.Still: return aware ? 0.6 : 0.0;
                default: return aware ? 0.55 : 0.2;
            }
        }

        void LayOut()
        {
            for (int i = 0; i < StalkerLayout.Nodes; i++)
            {
                _gx[i] = _px - Math.Sin(_heading) * i * StalkerLayout.Spacing;
                _gz[i] = _pz - Math.Cos(_heading) * i * StalkerLayout.Spacing;
            }
            for (int leg = 0; leg < StalkerLayout.Legs; leg++) FootHome(leg, out _fx[leg], out _fz[leg]);
            _laid = true;
        }

        /// <summary>Where a foot would stand at rest, in the chain's frame: out to the side of its hip.</summary>
        void FootHome(int leg, out double x, out double z)
        {
            int node = StalkerLayout.NodeOf(leg);
            int a = Math.Max(0, node - 1), b = Math.Min(StalkerLayout.Nodes - 1, node + 1);
            double ax = _gx[a] - _gx[b], az = _gz[a] - _gz[b];
            double al = Math.Sqrt(ax * ax + az * az);
            if (al < 1e-9) { ax = Math.Sin(_heading); az = Math.Cos(_heading); al = 1.0; }
            ax /= al;
            az /= al;
            double s = StalkerLayout.Sign(leg);
            // To the right of the line of the body is (az, -ax).
            x = _gx[node] + az * s * FootOut + ax * 0.06;
            z = _gz[node] - ax * s * FootOut + az * 0.06;
        }

        public void Update(StalkerBody body, in StalkerAnimInput input, double dt, StalkerPose pose)
        {
            if (!_laid) LayOut();
            _time += dt;
            StalkerMode mode = input.Mode;
            int nodes = body.Kept + 1;
            pose.Nodes = nodes;

            // ---- the ground chain follows the head
            _heading += input.TurnRate * dt;
            double sin = Math.Sin(_heading), cos = Math.Cos(_heading);
            double vx = cos * input.Velocity.X + sin * input.Velocity.Z, vz = -sin * input.Velocity.X + cos * input.Velocity.Z;
            _px += vx * dt;
            _pz += vz * dt;
            _gx[0] = _px;
            _gz[0] = _pz;
            for (int i = 1; i < StalkerLayout.Nodes; i++)
            {
                double dx = _gx[i] - _gx[i - 1], dz = _gz[i] - _gz[i - 1];
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d < 1e-9) { dx = -sin; dz = -cos; d = 1.0; }
                _gx[i] = _gx[i - 1] + dx / d * StalkerLayout.Spacing;
                _gz[i] = _gz[i - 1] + dz / d * StalkerLayout.Spacing;
            }

            double speed = Math.Sqrt(vx * vx + vz * vz);
            bool walks = body.Walks;
            _moving += (Math.Min(1.0, speed / 1.2) - _moving) * Math.Min(1.0, dt * 8.0);
            _phase += speed * dt / 0.9 * 2.0 * Math.PI;

            // Still, it sinks onto its belly; on too few legs it never leaves it.
            bool down = !walks || mode == StalkerMode.Still || mode == StalkerMode.Recover;
            double heightWant = down ? BellyHeight : StalkerLayout.RideHeight;
            _height += (heightWant - _height) * Math.Min(1.0, dt * (down ? 2.5 : 6.0));
            double gapeWant = GapeOf(mode, input.T);
            _gape += (gapeWant - _gape) * Math.Min(1.0, dt * 30.0);
            double flareWant = body.GillCount > 0 ? FlareOf(mode, input.T, input.Aware) : 0.0;
            _flare += (flareWant - _flare) * Math.Min(1.0, dt * (flareWant > _flare ? 18.0 : 3.0));
            if (input.Took && body.GillCount > 0) _flare = 1.0;
            _thrash += ((mode == StalkerMode.Reel ? Math.Sin(Math.PI * input.T) : 0.0) - _thrash) * Math.Min(1.0, dt * 14.0);
            _whip += ((mode == StalkerMode.Lunge ? 1.0 : 0.0) - _whip) * Math.Min(1.0, dt * 25.0);
            // The warning lifts its head and shoulders; the lunge throws them forward and low.
            double liftWant = mode == StalkerMode.Gape ? 0.22 * Smooth(input.T / StalkerBrain.LockAt) : mode == StalkerMode.Listen ? 0.10 : 0.0;
            _lift += (liftWant - _lift) * Math.Min(1.0, dt * 14.0);
            pose.Gape = Math.Max(0.0, Math.Min(1.0, _gape));
            pose.Flare = Math.Max(0.0, Math.Min(1.0, _flare));

            // A knock: a small spring.
            _flinchSpeed += (_flinch * -120.0 - _flinchSpeed * 14.0) * dt;
            _flinch += _flinchSpeed * dt;
            var knock = new Vec3d(_flinch.X * 0.04, Math.Max(0.0, _flinch.Y) * 0.03, 0);

            // ---- the spine, in creature space: the walking bend, the tail's sweep, the thrash
            double swim = walks ? 1.0 : 2.2;
            for (int i = 0; i < nodes; i++)
            {
                double dx = _gx[i] - _px, dz = _gz[i] - _pz;
                var flat = new Vec3d(cos * dx - sin * dz, _height, sin * dx + cos * dz);
                int a = Math.Max(0, i - 1), b = Math.Min(nodes - 1, i + 1);
                double ax = _gx[a] - _gx[b], az = _gz[a] - _gz[b];
                double al = Math.Sqrt(ax * ax + az * az);
                var side = al > 1e-9 ? new Vec3d((cos * az + sin * ax) / al, 0, (sin * az - cos * ax) / al) : Vec3d.Right;
                double along = (double)i / (StalkerLayout.Nodes - 1);
                // A standing wave down the body in time with its steps, widest at the tail; the head hardly moves.
                double wave = (0.035 + 0.11 * along * along) * swim * _moving * Math.Sin(_phase - i * 0.55)
                    + 0.16 * _thrash * Math.Sin(_time * 16.0 - i * 0.8) * (1.0 - along * 0.5)
                    + 0.22 * _whip * along * along * Math.Sin(_time * 30.0 - i * 0.7)
                    // At rest the tip of the tail still moves, slowly.
                    + 0.03 * along * along * along * Math.Sin(_time * 0.7 - i * 0.4);
                double mid = Math.Sin(Math.PI * i / Math.Max(1.0, nodes - 1.0));
                // Head and shoulders lifted in the warning; the tail's fin rides a little higher than the belly.
                // (Eased in over the lengths either side of the tail's root: the body is one skin, and a step in the
                // spine would show as a ledge between two lengths.)
                double raise = _lift * Math.Max(0.0, 1.0 - i / 3.0) + 0.03 * Smooth((i - (StalkerLayout.Trunk - 2)) / 5.0);
                pose.NodePosition[i] = flat + side * (wave * (i == 0 ? 0.3 : 1.0)) + Vec3d.Up * raise + knock * (0.4 + 0.6 * mid);
            }

            // ---- which way each piece faces
            for (int i = 0; i < nodes; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(nodes - 1, i + 1);
                Vec3d f = nodes > 1 ? (pose.NodePosition[a] - pose.NodePosition[b]).Normalized : Vec3d.Forward;
                var level = new Vec3d(f.X, 0, f.Z);
                level = level.SqrMagnitude > 1e-9 ? level.Normalized : Vec3d.Forward;
                Vec3d up = Vec3d.Up - level * f.Y;
                pose.NodeRotation[i] = WalkerAnimator.Look(f, up).Normalized;
            }

            // ---- the head: round to what it heard, sweeping when it casts about, up for the warning
            double lookWant = 0.0;
            if (input.HasPlace && input.Place.SqrMagnitude > 0.01 && mode != StalkerMode.Lunge)
                lookWant = Math.Max(-0.7, Math.Min(0.7, Math.Atan2(input.Place.X, input.Place.Z)));
            if (mode == StalkerMode.Search) lookWant = 0.55 * Math.Sin(_time * 1.9);
            if (mode == StalkerMode.Reel) lookWant = 0.6 * Math.Sin(_time * 14.0) * _thrash;
            _lookYaw += (lookWant - _lookYaw) * Math.Min(1.0, dt * 5.0);
            double headPitch = -0.30 * (_lift / 0.22) - 0.25 * _whip + 0.04 * Math.Sin(_time * 0.9);
            QuatD head = (pose.NodeRotation[0] * QuatD.AngleAxis(Vec3d.Up, _lookYaw) * QuatD.AngleAxis(Vec3d.Right, headPitch)).Normalized;
            // It turns its head at the neck, half a spacing behind the head's node, so the skin of the head and of the
            // neck stay together there however far it looks round.
            var toNeck = new Vec3d(0, 0, -StalkerLayout.Spacing * 0.5);
            pose.NodePosition[0] = pose.NodePosition[0] + pose.NodeRotation[0].Rotate(toNeck) - head.Rotate(toNeck);
            pose.NodeRotation[0] = head;
            pose.JawPosition = pose.NodePosition[0] + head.Rotate(StalkerLayout.JawHinge);
            pose.JawRotation = (head * QuatD.AngleAxis(Vec3d.Right, pose.Gape * StalkerLayout.JawSwing)).Normalized;
            for (int side = 0; side < StalkerLayout.Gills; side++)
            {
                double s = StalkerLayout.Sign(side);
                pose.GillPosition[side] = pose.NodePosition[0] + head.Rotate(StalkerLayout.GillRootOf(side));
                double breathe = 0.05 * Math.Sin(_time * 1.1 + side * 0.6) + 0.04 * pose.Flare * Math.Sin(_time * 19.0 + side * 2.0);
                double outward = 0.45 + 0.75 * pose.Flare + breathe;
                var way = new Vec3d(s * Math.Sin(outward), 0.18 + 0.30 * pose.Flare, -Math.Cos(outward));
                pose.GillRotation[side] = (head * WalkerAnimator.Look(way, Vec3d.Up)).Normalized;
            }

            // ---- feet: planted in the world, stepping a diagonal pair at a time when they fall behind
            for (int leg = 0; leg < StalkerLayout.Legs; leg++)
            {
                int node = StalkerLayout.NodeOf(leg);
                if (!body.LegOn(leg) || node >= nodes) continue;
                FootHome(leg, out double hx, out double hz);
                double lift = 0.0;
                if (_inAir[leg])
                {
                    _swing[leg] += dt / _swingTime[leg];
                    double u = Smooth(_swing[leg]);
                    // It lands a little ahead of home, the way it is going.
                    double tx = hx + vx * _swingTime[leg] * 0.5, tz = hz + vz * _swingTime[leg] * 0.5;
                    _fx[leg] = _fromX[leg] + (tx - _fromX[leg]) * u;
                    _fz[leg] = _fromZ[leg] + (tz - _fromZ[leg]) * u;
                    lift = Lift * Math.Sin(Math.PI * Math.Min(1.0, _swing[leg]));
                    if (_swing[leg] >= 1.0) _inAir[leg] = false;
                }
                else
                {
                    double ex = hx - _fx[leg], ez = hz - _fz[leg];
                    // Legs 0 and 3 are one diagonal, 1 and 2 the other: never both in the air.
                    int otherA = leg == 0 || leg == 3 ? 1 : 0, otherB = leg == 0 || leg == 3 ? 2 : 3;
                    bool free = !_inAir[otherA] && !_inAir[otherB];
                    if (walks && free && Math.Sqrt(ex * ex + ez * ez) > StepAt)
                    {
                        _inAir[leg] = true;
                        _swing[leg] = 0.0;
                        _fromX[leg] = _fx[leg];
                        _fromZ[leg] = _fz[leg];
                        _swingTime[leg] = Math.Max(0.07, Math.Min(0.30, 0.30 / (1.0 + speed * 0.7)));
                    }
                }

                double s = StalkerLayout.Sign(leg);
                QuatD frame = pose.NodeRotation[node];
                Vec3d hip = pose.NodePosition[node] + frame.Rotate(StalkerLayout.HipOf(leg));
                Vec3d foot;
                if (walks)
                {
                    double dx = _fx[leg] - _px, dz = _fz[leg] - _pz;
                    foot = new Vec3d(cos * dx - sin * dz, lift, sin * dx + cos * dz);
                }
                else
                {
                    // On its belly a leg trails, paddling a little.
                    foot = hip + frame.Rotate(new Vec3d(s * 0.30, -0.10, -0.50 + 0.10 * Math.Sin(_phase + leg * 1.7) * _moving));
                    foot = new Vec3d(foot.X, Math.Max(0.02, foot.Y), foot.Z);
                }
                Bend(hip, foot, frame.Rotate(new Vec3d(s * 0.75, 0.65, 0.0)), leg, pose);
            }
        }

        /// <summary>A leg of two bones from hip to foot, its elbow toward <paramref name="pole"/>.</summary>
        static void Bend(Vec3d hip, Vec3d foot, Vec3d pole, int leg, StalkerPose pose)
        {
            const double upper = StalkerLayout.LegLength, lower = StalkerLayout.ShinLength;
            Vec3d to = foot - hip;
            double d = to.Magnitude;
            Vec3d dir = d > 1e-9 ? to * (1.0 / d) : new Vec3d(0, -1, 0);
            double reach = Math.Max(Math.Abs(upper - lower) + 0.01, Math.Min(upper + lower - 0.005, d));
            // The foot it can really reach (a foot left too far behind is drawn at full stretch toward it).
            foot = hip + dir * reach;
            double along = (upper * upper - lower * lower + reach * reach) / (2.0 * reach);
            double out2 = Math.Max(0.0, upper * upper - along * along);
            Vec3d across = pole - dir * (pole.X * dir.X + pole.Y * dir.Y + pole.Z * dir.Z);
            across = across.SqrMagnitude > 1e-9 ? across.Normalized : Vec3d.Up;
            Vec3d knee = hip + dir * along + across * Math.Sqrt(out2);
            pose.LegPosition[leg] = hip;
            pose.LegRotation[leg] = WalkerAnimator.Look((knee - hip).Normalized, across).Normalized;
            pose.ShinPosition[leg] = knee;
            pose.ShinRotation[leg] = WalkerAnimator.Look((foot - knee).Normalized, across).Normalized;
            pose.Foot[leg] = foot;
        }
    }
}
