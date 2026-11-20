using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>Where every piece of a Screamer is, in creature space. Pieces that are gone keep their last place.</summary>
    public sealed class ScreamerPose
    {
        public Vec3d BodyPosition, AbdomenPosition;
        public QuatD BodyRotation = QuatD.Identity, AbdomenRotation = QuatD.Identity;
        public readonly Vec3d[] DrumPosition = new Vec3d[ScreamerLayout.Drums];
        public readonly QuatD[] DrumRotation = new QuatD[ScreamerLayout.Drums];
        public readonly Vec3d[] WingPosition = new Vec3d[ScreamerLayout.Wings];
        public readonly QuatD[] WingRotation = new QuatD[ScreamerLayout.Wings];
        public readonly Vec3d[] LegPosition = new Vec3d[ScreamerLayout.Legs], ShinPosition = new Vec3d[ScreamerLayout.Legs], FootPosition = new Vec3d[ScreamerLayout.Legs];
        public readonly QuatD[] LegRotation = new QuatD[ScreamerLayout.Legs], ShinRotation = new QuatD[ScreamerLayout.Legs];
        /// <summary>How hard its drums are buzzing, 0 to 1: for sound and for the air in front of it.</summary>
        public double Buzz;
    }

    public struct ScreamerAnimInput
    {
        /// <summary>What it really moved, creature space, metres a second.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        /// <summary>Metres its feet are off the ground.</summary>
        public double Height;
        public ScreamerMode Mode;
        public double T;
    }

    /// <summary>
    /// How a Screamer moves, with no Unity types and no animation clips.
    ///
    /// - On the ground its wings lie folded over its back like a roof and it walks on alternating threes of its legs.
    /// - In the air the wings are out and beating, the body hangs nose up and banks into its turns, the legs tuck.
    /// - The warning: the abdomen lifts and the wings fan up and out and shiver (<see cref="FanOf"/>,
    ///   <see cref="LiftOf"/>). At <see cref="ScreamerBrain.LockAt"/> both jerk to their full height: that is the
    ///   moment it has taken aim. Through the scream the whole animal trembles; afterwards everything sags.
    /// </summary>
    public sealed class ScreamerAnimator
    {
        /// <summary>Wing beats a second in the air, and shivers a second in the warning.</summary>
        public const double BeatRate = 9.5, ShiverRate = 16.0;
        const double Stride = 0.42;

        double _time, _fly, _fan, _lift, _gait, _roll, _pitch, _spin, _sink;
        Vec3d _flinch;
        bool _started;

        public void Flinch(Vec3d push) => _flinch = _flinch + push * 0.05;

        static double Smooth(double x)
        {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>How far its wings are fanned, 0 to 1: about half through the first of the warning, all the way at the lock.</summary>
        public static double FanOf(ScreamerMode mode, double t)
        {
            switch (mode)
            {
                case ScreamerMode.WindUp:
                    return t < ScreamerBrain.LockAt ? 0.5 * Smooth(t / ScreamerBrain.LockAt) : 0.5 + 0.5 * Smooth((t - ScreamerBrain.LockAt) / 0.12);
                case ScreamerMode.Scream: return 1.0;
                case ScreamerMode.Spent: return 0.35 * (1.0 - Smooth(t * 1.5));
                default: return 0.0;
            }
        }

        /// <summary>How far its abdomen is lifted, radians: up through the warning, sagging when it is spent.</summary>
        public static double LiftOf(ScreamerMode mode, double t)
        {
            switch (mode)
            {
                case ScreamerMode.WindUp:
                    return t < ScreamerBrain.LockAt ? 0.42 * Smooth(t / ScreamerBrain.LockAt) : 0.42 + 0.36 * Smooth((t - ScreamerBrain.LockAt) / 0.12);
                case ScreamerMode.Scream: return 0.78;
                case ScreamerMode.Spent: return -0.22;
                case ScreamerMode.Fly:
                case ScreamerMode.Land: return -0.12;
                case ScreamerMode.Fall: return 0.2;
                default: return 0.0;
            }
        }

        static double Ease(double from, double to, double rate, double dt) => from + (to - from) * (1.0 - Math.Exp(-rate * dt));

        public void Update(ScreamerBody body, in ScreamerAnimInput input, double dt, ScreamerPose pose)
        {
            dt = Math.Max(0.0, dt);
            _time += dt;
            ScreamerMode mode = input.Mode;
            bool flying = mode == ScreamerMode.Fly || mode == ScreamerMode.Land || mode == ScreamerMode.Fall;
            double air = Smooth(input.Height / 0.8);
            double fan = FanOf(mode, input.T), lift = LiftOf(mode, input.T);
            if (!_started)
            {
                _started = true;
                _fly = flying ? 1.0 : 0.0;
                _fan = fan;
                _lift = lift;
            }
            _fly = Ease(_fly, flying ? 1.0 : 0.0, flying ? 14.0 : 9.0, dt);
            _fan = Ease(_fan, fan, 30.0, dt);
            _lift = Ease(_lift, lift, mode == ScreamerMode.Spent ? 5.0 : 30.0, dt);
            _flinch = _flinch * Math.Exp(-9.0 * dt);

            double speed = Math.Sqrt(input.Velocity.X * input.Velocity.X + input.Velocity.Z * input.Velocity.Z);
            double buzz = mode == ScreamerMode.Scream ? 1.0 : mode == ScreamerMode.WindUp ? 0.25 + 0.5 * input.T : 0.0;
            pose.Buzz = buzz;

            // ---- the fore body
            bool stands = body.LegCount >= ScreamerBody.LegsToWalk;
            double stand = stands ? ScreamerLayout.StandHeight * (0.82 + 0.18 * body.LegCount / ScreamerLayout.Legs) : ScreamerLayout.BellyHeight;
            _sink = Ease(_sink, mode == ScreamerMode.Spent ? 0.10 : mode == ScreamerMode.WindUp || mode == ScreamerMode.Scream ? 0.04 : 0.0, 6.0, dt);
            double ground = Math.Max(ScreamerLayout.BellyHeight, stand - _sink);
            double y = ground + (ScreamerLayout.StandHeight - ground) * air + input.Height;
            if (flying) y += 0.06 * Math.Sin(_time * 2.0 * Math.PI * 1.3) * air;
            // Nose up in the air, less so at speed; nose down through the warning and the scream, so the abdomen stands high.
            double pitch = flying ? (0.34 - 0.2 * Math.Min(1.0, speed / 8.0)) * air : mode == ScreamerMode.WindUp || mode == ScreamerMode.Scream ? -0.16 * Math.Min(1.0, _fan * 2.0) : 0.0;
            double roll = flying ? Math.Max(-0.5, Math.Min(0.5, -input.TurnRate * 0.2)) * air : 0.0;
            // On three wings it flies lopsided.
            if (flying && body.WingCount == 3)
                for (int w = 0; w < ScreamerLayout.Wings; w++)
                    if (!body.WingOn(w)) roll += -0.22 * ScreamerLayout.Sign(ScreamerLayout.WingSide(w)) + 0.06 * Math.Sin(_time * 7.0);
            if (mode == ScreamerMode.Fall)
            {
                _spin += 7.0 * dt;
                pitch = -0.5;
            }
            else
            {
                _spin = Ease(_spin, 0.0, 12.0, dt);
            }
            _pitch = Ease(_pitch, pitch, 8.0, dt);
            _roll = Ease(_roll, roll, 6.0, dt);
            double tremble = buzz * buzz * 0.012;
            var bodyAt = new Vec3d(_flinch.X + tremble * Math.Sin(_time * 131.0), y + _flinch.Y + tremble * Math.Sin(_time * 173.0), _flinch.Z);
            QuatD bodyTurn = QuatD.AngleAxis(Vec3d.Right, -_pitch) * QuatD.AngleAxis(Vec3d.Forward, _roll + _spin);
            pose.BodyPosition = bodyAt;
            pose.BodyRotation = bodyTurn;

            // ---- the abdomen and its drums
            double breathe = 0.025 * Math.Sin(_time * 2.4) + buzz * 0.03 * Math.Sin(_time * 2.0 * Math.PI * 21.0);
            QuatD abdomenTurn = bodyTurn * QuatD.AngleAxis(Vec3d.Right, _lift + breathe);
            Vec3d abdomenAt = bodyAt + bodyTurn.Rotate(ScreamerLayout.AbdomenJoint);
            pose.AbdomenPosition = abdomenAt;
            pose.AbdomenRotation = abdomenTurn;
            for (int side = 0; side < ScreamerLayout.Drums; side++)
            {
                Vec3d at = ScreamerLayout.DrumOf(side);
                // A drum buckles in and out as it sounds.
                at = new Vec3d(at.X + ScreamerLayout.Sign(side) * buzz * 0.012 * Math.Sin(_time * 2.0 * Math.PI * 27.0 + side), at.Y, at.Z);
                pose.DrumPosition[side] = abdomenAt + abdomenTurn.Rotate(at);
                pose.DrumRotation[side] = side == ScreamerLayout.RightSide ? abdomenTurn : abdomenTurn * QuatD.AngleAxis(Vec3d.Forward, Math.PI);
            }

            // ---- the wings
            double beat = Math.Sin(_time * 2.0 * Math.PI * BeatRate), beatLate = Math.Sin(_time * 2.0 * Math.PI * BeatRate - 0.9);
            double shiver = Math.Sin(_time * 2.0 * Math.PI * ShiverRate);
            if (mode == ScreamerMode.Fall) beat = Math.Sin(_time * 31.0) * Math.Sin(_time * 4.3);
            for (int w = 0; w < ScreamerLayout.Wings; w++)
            {
                if (!body.WingOn(w)) continue;
                bool hind = ScreamerLayout.IsHind(w);
                double sign = ScreamerLayout.Sign(ScreamerLayout.WingSide(w));
                // Folded over the back like a roof; fanned up and out for the warning; out flat and beating in the air.
                double foldSweep = hind ? 1.46 : 1.40, foldRise = hind ? -0.16 : -0.10, foldRoof = hind ? 0.6 : 0.75;
                double fanSweep = hind ? 1.0 : 0.72, fanRise = (hind ? 0.5 : 0.8) + 0.035 * shiver * (0.4 + buzz), fanRoof = 0.3;
                double flySweep = hind ? 0.42 : 0.08, flyRise = 0.12 + (hind ? 0.6 * beatLate : 0.72 * beat), flyRoof = -0.25 * (hind ? beatLate : beat);
                double fanShare = _fan * (1.0 - _fly);
                double sweep = foldSweep + (fanSweep - foldSweep) * fanShare + (flySweep - foldSweep) * _fly;
                double rise = foldRise + (fanRise - foldRise) * fanShare + (flyRise - foldRise) * _fly;
                double roof = foldRoof + (fanRoof - foldRoof) * fanShare + (flyRoof - foldRoof) * _fly;
                WingFrame(sign, sweep, rise, roof, out Vec3d along, out Vec3d upper);
                pose.WingPosition[w] = bodyAt + bodyTurn.Rotate(ScreamerLayout.HingeOf(w));
                pose.WingRotation[w] = bodyTurn * WalkerAnimator.Look(along, upper);
            }

            // ---- the legs
            double turn = input.TurnRate;
            double moving = Math.Min(1.0, (speed + Math.Abs(turn) * 0.4) / 0.3) * (1.0 - air);
            _gait += (speed + Math.Abs(turn) * 0.35) / Stride * dt * 0.5;
            _gait -= Math.Floor(_gait);
            for (int leg = 0; leg < ScreamerLayout.Legs; leg++)
            {
                if (!body.LegOn(leg)) continue;
                int side = ScreamerLayout.LegSide(leg), place = ScreamerLayout.LegPlace(leg);
                double sign = ScreamerLayout.Sign(side);
                Vec3d hip = bodyAt + bodyTurn.Rotate(ScreamerLayout.HipOf(leg));
                Vec3d rest = ScreamerLayout.FootOf(leg);
                if (!stands) rest = new Vec3d(rest.X * 1.12, 0, rest.Z);
                // Alternating threes: front and back of one side with the middle of the other.
                double u = _gait + ((place == 1) == (side == ScreamerLayout.RightSide) ? 0.5 : 0.0);
                u -= Math.Floor(u);
                // The ground under a foot seems to move backward as it walks and round as it turns.
                var slide = new Vec3d(-input.Velocity.X - rest.Z * turn, 0, -input.Velocity.Z + rest.X * turn);
                double far = slide.Magnitude;
                Vec3d reach = far > 1e-6 ? slide * (Math.Min(0.15, far * 0.11) / far) : Vec3d.Zero;
                double along = u < 0.5 ? -1.0 + 4.0 * u : 1.0 - 4.0 * (u - 0.5);
                double up = u < 0.5 ? 0.0 : Math.Sin(Math.PI * (u - 0.5) * 2.0) * 0.11;
                Vec3d foot = rest + reach * (along * moving) + new Vec3d(0, up * moving, 0);
                // Out of breath it twitches; screaming, its feet are braced.
                if (mode == ScreamerMode.Spent) foot = foot + new Vec3d(0, Math.Max(0.0, Math.Sin(_time * 9.0 + leg * 2.1)) * 0.02, 0);
                // In the air the legs are drawn up under it, knees out, feet trailing.
                Vec3d tucked = bodyAt + bodyTurn.Rotate(ScreamerLayout.HipOf(leg) + new Vec3d(sign * 0.12, -0.20 + 0.03 * Math.Sin(_time * 5.0 + leg), -0.20 + 0.06 * (1 - place)));
                foot = foot + (tucked - foot) * air;
                Bend(hip, foot, sign, pose, leg);
            }
        }

        /// <summary>
        /// The way a right or left wing lies: <paramref name="sweep"/> is how far it is swung back from straight out
        /// (radians), <paramref name="rise"/> how far its tip is lifted, <paramref name="roof"/> how far its trailing
        /// edge is tipped up. The answer is the line along the wing and the way its piece's +Y must face: up for the
        /// right wing, down for the left, which is the same piece the other way up.
        /// </summary>
        public static void WingFrame(double sign, double sweep, double rise, double roof, out Vec3d along, out Vec3d upper)
        {
            // Worked out for the right wing, then mirrored.
            var d = new Vec3d(Math.Cos(sweep) * Math.Cos(rise), Math.Sin(rise), -Math.Sin(sweep) * Math.Cos(rise));
            var chord0 = new Vec3d(-Math.Sin(sweep), 0, -Math.Cos(sweep));
            Vec3d normal0 = Vec3d.Cross(d, chord0);
            Vec3d chord = chord0 * Math.Cos(roof) + normal0 * Math.Sin(roof);
            Vec3d normal = Vec3d.Cross(d, chord);
            along = new Vec3d(d.X * sign, d.Y, d.Z);
            // The mirrored wing's own upper side faces the other way: its trailing edge must still trail.
            upper = sign > 0 ? normal : new Vec3d(normal.X, -normal.Y, -normal.Z);
        }

        /// <summary>Bends a leg's two pieces from its hip to its foot, knee up and a little out.</summary>
        static void Bend(Vec3d hip, Vec3d foot, double sign, ScreamerPose pose, int leg)
        {
            const double l1 = ScreamerLayout.LegLength, l2 = ScreamerLayout.ShinLength;
            Vec3d d = foot - hip;
            double far = d.Magnitude;
            Vec3d dir = far > 1e-6 ? d / far : new Vec3d(0, -1, 0);
            double len = Math.Max(Math.Abs(l1 - l2) + 0.02, Math.Min(l1 + l2 - 0.004, far));
            double a = (l1 * l1 - l2 * l2 + len * len) / (2.0 * len);
            double h = Math.Sqrt(Math.Max(0.0, l1 * l1 - a * a));
            var pole = new Vec3d(sign * 0.6, 1.0, 0.0);
            Vec3d p = pole - dir * Vec3d.Dot(pole, dir);
            p = p.SqrMagnitude > 1e-9 ? p.Normalized : Vec3d.Up;
            Vec3d knee = hip + dir * a + p * h;
            Vec3d end = hip + dir * len;
            pose.LegPosition[leg] = hip;
            pose.LegRotation[leg] = WalkerAnimator.Look(knee - hip, p);
            pose.ShinPosition[leg] = knee;
            pose.ShinRotation[leg] = WalkerAnimator.Look(end - knee, p);
            pose.FootPosition[leg] = end;
        }
    }
}
