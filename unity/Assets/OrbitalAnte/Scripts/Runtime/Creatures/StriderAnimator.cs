using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>What the animator is told each step. Everything is in strider space (see <see cref="StriderLayout"/>).</summary>
    public struct StriderAnimInput
    {
        /// <summary>How the animal is really moving over the ground, in metres a second (Y is ignored).</summary>
        public Vec3d Velocity;
        /// <summary>How fast it is really turning, in radians a second; positive turns its nose toward its right.</summary>
        public double TurnRate;
        /// <summary>The side whose claw is striking (<see cref="StriderLayout.Right"/> or Left), or -1.</summary>
        public int StrikeSide;
        /// <summary>How far through the strike it is, 0 to 1: see <see cref="StriderAnimator.StrikeLands"/>.</summary>
        public double StrikeT;
        /// <summary>What the claw is thrown at.</summary>
        public Vec3d StrikeAt;
        /// <summary>The height of the ground at (x, z), or null for level ground at 0.</summary>
        public Func<double, double, double> Ground;
    }

    /// <summary>Where every piece of a Strider is, in strider space.</summary>
    public sealed class StriderPose
    {
        /// <summary>The middle of the body (the origin of the Carapace and Belly pieces).</summary>
        public Vec3d BodyPosition;
        public QuatD BodyRotation = QuatD.Identity;
        /// <summary>Each limb piece's joint and turn, indexed limb * 3 + segment. Only pieces still on mean anything.</summary>
        public readonly Vec3d[] Position = new Vec3d[StriderLayout.Limbs * StriderLayout.Segments];
        public readonly QuatD[] Rotation = new QuatD[StriderLayout.Limbs * StriderLayout.Segments];
        /// <summary>The moving finger of each claw, by side.</summary>
        public readonly Vec3d[] FingerPosition = new Vec3d[2];
        public readonly QuatD[] FingerRotation = new QuatD[2];
        /// <summary>Where each limb's end is being put (a foot on the ground, a claw's point).</summary>
        public readonly Vec3d[] End = new Vec3d[StriderLayout.Limbs];
        /// <summary>True while a walking leg's foot is on the ground.</summary>
        public readonly bool[] Planted = new bool[StriderLayout.Limbs];

        public StriderPose()
        {
            for (int i = 0; i < Rotation.Length; i++) Rotation[i] = QuatD.Identity;
            FingerRotation[0] = FingerRotation[1] = QuatD.Identity;
        }
    }

    /// <summary>
    /// Moves a Strider. No Unity types and no clips: every foot is given a place on the ground and kept there while the
    /// body travels over it; a foot left too far behind is lifted and swung to a new place ahead, never while a
    /// neighbour is in the air. The legs are then bent to reach from the body to their feet. So the walk comes out of
    /// the real speed and turn rate, slows to a careful one-foot-at-a-time creep on few legs, and a leg that is shot
    /// off simply stops being asked to carry: the body settles lower, leans over the legs it has left and the rest
    /// keep stepping. On too few legs to stand (<see cref="StriderStance.Down"/>) the same rules drag it along on its belly.
    /// </summary>
    public sealed class StriderAnimator
    {
        /// <summary>How far a foot may be left from where it would like to stand before it steps, in metres.</summary>
        public const double StrideRadius = 0.62;
        public const double StepHeight = 0.55;
        /// <summary>The share of a strike at which the claw arrives (before it: the raise that warns of it).</summary>
        public const double StrikeLands = 0.62;
        public const double StrikeStarts = 0.50;
        /// <summary>The bend between a leg's lower piece and its tip, and between an arm's lower piece and its claw.</summary>
        const double TipBend = 0.42;
        const double ClawBend = 0.38;

        readonly Vec3d[] _foot = new Vec3d[StriderLayout.Limbs];
        readonly Vec3d[] _from = new Vec3d[StriderLayout.Limbs];
        readonly Vec3d[] _to = new Vec3d[StriderLayout.Limbs];
        readonly bool[] _swing = new bool[StriderLayout.Limbs];
        readonly double[] _t = new double[StriderLayout.Limbs];
        readonly double[] _length = new double[StriderLayout.Limbs];
        readonly double[] _still = new double[StriderLayout.Limbs];
        readonly Vec3d[] _claw = new Vec3d[2];
        static readonly Vec3d HomeCentre = Centre();

        Spring _height, _shiftX, _shiftZ, _roll, _pitch;
        double _time;
        bool _started;

        /// <summary>How many feet are in the air.</summary>
        public int Swinging { get; private set; }

        static Vec3d Centre()
        {
            Vec3d sum = Vec3d.Zero;
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
                if (!StriderLayout.IsArm(limb)) sum += StriderLayout.FootHome(limb);
            return sum / StriderLayout.WalkingLegs;
        }

        /// <summary>A knock: the body is thrown along <paramref name="push"/> (strider space) and springs back.</summary>
        public void Flinch(Vec3d push)
        {
            _shiftX.V += push.X;
            _shiftZ.V += push.Z;
            _height.V += push.Y;
        }

        static double Ground(in StriderAnimInput input, double x, double z) => input.Ground != null ? input.Ground(x, z) : 0.0;

        Vec3d Home(int limb, in StriderAnimInput input, double spread)
        {
            Vec3d home = StriderLayout.FootHome(limb);
            Vec3d hip = StriderLayout.HipModelled(limb) * StriderLayout.Size;
            hip = new Vec3d(hip.X, 0, hip.Z);
            home = hip + (home - hip) * spread;
            return new Vec3d(home.X, Ground(input, home.X, home.Z), home.Z);
        }

        void Start(StriderBody body, in StriderAnimInput input)
        {
            _started = true;
            double share = body.HeightShare;
            for (int limb = 0; limb < StriderLayout.Limbs; limb++) _foot[limb] = Home(limb, input, Spread(share));
            _height.X = StriderLayout.BellyHeight + (StriderLayout.StandHeight - StriderLayout.BellyHeight) * share;
            for (int side = 0; side < 2; side++) _claw[side] = ClawRest(side, _height.X, 0.0, body.Stance);
        }

        /// <summary>Lying down, the legs sprawl further out than they stand.</summary>
        static double Spread(double heightShare) => 1.45 - 0.45 * heightShare;

        public void Update(StriderBody body, in StriderAnimInput input, double dt, StriderPose pose)
        {
            if (!_started) Start(body, input);
            dt = Math.Max(0.0, Math.Min(dt, 0.1));
            _time += dt;
            StriderStance stance = body.Stance;
            double share = body.HeightShare;
            var flat = new Vec3d(input.Velocity.X, 0, input.Velocity.Z);
            double speed = flat.Magnitude;

            // 1. The ground goes by under the body: every foot that is down, and the place a swinging foot left, stay
            //    where they are in the world.
            QuatD back = QuatD.AngleAxis(Vec3d.Up, -input.TurnRate * dt);
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                _foot[limb] = back.Rotate(_foot[limb] - flat * dt);
                _from[limb] = back.Rotate(_from[limb] - flat * dt);
            }

            // 2. Which feet step. The one furthest from where it would like to be goes first; never one whose
            //    neighbour (next along its side, or opposite) is in the air; never more than half of them at once.
            int carrying = 0;
            Swinging = 0;
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                if (!body.CarriesWeight(limb)) { _swing[limb] = false; continue; }
                carrying++;
                if (_swing[limb]) Swinging++;
            }
            int most = Math.Max(1, stance == StriderStance.Standing ? (carrying - 3) / 2 + 1 : carrying / 2);
            double spread = Spread(share);
            double radius = StrideRadius * (stance == StriderStance.Standing ? 1.0 : 0.6);
            double stepTime = Math.Max(0.22, Math.Min(0.46, 0.46 - 0.09 * speed)) * (stance == StriderStance.Standing ? 1.0 + 0.5 * (1.0 - share) : 1.6);
            for (int pass = 0; pass < StriderLayout.WalkingLegs && Swinging < most; pass++)
            {
                int pick = -1;
                double worst = 0.0;
                for (int limb = 0; limb < StriderLayout.Limbs; limb++)
                {
                    if (!body.CarriesWeight(limb) || _swing[limb] || NeighbourInAir(body, limb)) continue;
                    Vec3d home = Home(limb, input, spread);
                    double off = new Vec3d(_foot[limb].X - home.X, 0, _foot[limb].Z - home.Z).Magnitude;
                    bool wants = off > radius || (_still[limb] > 1.1 && off > 0.22 * radius && speed < 0.05 && Math.Abs(input.TurnRate) < 0.02);
                    if (wants && off > worst) { worst = off; pick = limb; }
                }
                if (pick < 0) break;
                Vec3d target = StriderLayout.FootHome(pick);
                Vec3d hip = StriderLayout.HipModelled(pick) * StriderLayout.Size;
                hip = new Vec3d(hip.X, 0, hip.Z);
                target = hip + (target - hip) * spread;
                // Put it down ahead of where it would stand, so the body arrives over it half-way through its turn on the ground.
                double lead = Math.Min(radius * 0.8, speed * 0.55);
                if (speed > 1e-6) target += flat / speed * lead;
                target = QuatD.AngleAxis(Vec3d.Up, Math.Max(-0.3, Math.Min(0.3, input.TurnRate * 0.45))).Rotate(target);
                _from[pick] = _foot[pick];
                _to[pick] = new Vec3d(target.X, Ground(input, target.X, target.Z), target.Z);
                _swing[pick] = true;
                _t[pick] = 0.0;
                _length[pick] = stepTime;
                Swinging++;
            }

            // 3. Feet in the air travel in an arc; feet on the ground count how long they have stood.
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                if (!body.CarriesWeight(limb)) continue;
                if (!_swing[limb]) { _still[limb] += dt; continue; }
                _t[limb] += dt / _length[limb];
                if (_t[limb] >= 1.0)
                {
                    _swing[limb] = false;
                    _foot[limb] = _to[limb];
                    _still[limb] = 0.0;
                    continue;
                }
                double t = _t[limb];
                double ease = t * t * (3.0 - 2.0 * t);
                double lift = Math.Sin(Math.PI * t) * StepHeight * (stance == StriderStance.Standing ? 1.0 : 0.3);
                _foot[limb] = _from[limb] + (_to[limb] - _from[limb]) * ease + Vec3d.Up * lift;
            }

            // 4. The body: over the middle of the feet it has, as high as its legs allow, sagging toward the side
            //    and the end with fewer legs, dipping a little for every foot in the air, tilted to the ground.
            Vec3d middle = Vec3d.Zero;
            double right = 0, left = 0, front = 0, rear = 0, groundR = 0, groundL = 0, groundF = 0, groundB = 0;
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                if (!body.CarriesWeight(limb)) continue;
                Vec3d f = _swing[limb] ? _to[limb] : _foot[limb];
                middle += f;
                if (StriderLayout.SideOf(limb) == StriderLayout.Right) { right++; groundR += f.Y; } else { left++; groundL += f.Y; }
                if (StriderLayout.PlaceOf(limb) <= 2) { front++; groundF += f.Y; } else { rear++; groundB += f.Y; }
            }
            double groundHere = Ground(input, 0, 0);
            double under = carrying > 0 ? middle.Y / carrying : groundHere;
            Vec3d lean = carrying > 0 ? middle / carrying - HomeCentre * spread : Vec3d.Zero;
            double standing = stance == StriderStance.Standing ? 1.0 : 0.0;
            double heightWanted = Math.Max(under, groundHere) + StriderLayout.BellyHeight
                                  + (StriderLayout.StandHeight - StriderLayout.BellyHeight) * share
                                  - 0.06 * Swinging * standing + 0.035 * Math.Sin(_time * 1.3) * standing;
            double rollWanted = 0.075 * (right - left) * standing;
            double pitchWanted = 0.06 * (rear - front) * standing;
            if (right > 0 && left > 0) rollWanted += Math.Atan2(groundR / right - groundL / left, 2.0 * StriderLayout.FootReach) ;
            if (front > 0 && rear > 0) pitchWanted += Math.Atan2(groundB / rear - groundF / front, 1.6 * StriderLayout.FootReach);
            double strike = input.StrikeSide >= 0 ? StrikeReach(input.StrikeT) : 0.0;
            pitchWanted += 0.10 * strike * standing;
            double stiff = stance == StriderStance.Standing ? 26.0 : 60.0;
            _height.Step(heightWanted - 0.10 * strike * standing, stiff, 8.5, dt);
            _shiftX.Step(lean.X * 0.55, 22.0, 7.5, dt);
            _shiftZ.Step(lean.Z * 0.55 + 0.25 * strike * standing, 22.0, 7.5, dt);
            _roll.Step(rollWanted, 20.0, 7.0, dt);
            _pitch.Step(pitchWanted, 20.0, 7.0, dt);
            double floor = groundHere + StriderLayout.BellyHeight;
            if (_height.X < floor) { _height.X = floor; _height.V = Math.Max(0.0, _height.V); }

            pose.BodyPosition = new Vec3d(_shiftX.X, _height.X, _shiftZ.X);
            pose.BodyRotation = QuatD.AngleAxis(Vec3d.Forward, _roll.X) * QuatD.AngleAxis(Vec3d.Right, _pitch.X);

            // 5. The legs: bent to reach from the body to their feet. One with nothing below the upper piece is held up and shakes.
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                if (StriderLayout.IsArm(limb)) continue;
                pose.Planted[limb] = body.CarriesWeight(limb) && !_swing[limb];
                int kept = body.Kept(limb);
                if (kept == 0) continue;
                Vec3d hip = pose.BodyPosition + pose.BodyRotation.Rotate(StriderLayout.HipModelled(limb) * StriderLayout.Size);
                if (kept == 1) { Wave(pose, limb, kept, hip, 0.55, 0.20, 5.0); continue; }
                pose.End[limb] = _foot[limb];
                Bend(pose, limb, hip, _foot[limb], kept, TipBend);
            }

            // 6. The claw arms: carried low in front, swaying; a strike is a slow raise that warns, a throw, a hold and a return.
            for (int side = 0; side < 2; side++)
            {
                int limb = StriderLayout.Limb(side, 0);
                int kept = body.Kept(limb);
                if (kept == 0) continue;
                Vec3d shoulder = pose.BodyPosition + pose.BodyRotation.Rotate(StriderLayout.HipModelled(limb) * StriderLayout.Size);
                if (kept < StriderLayout.Segments) { Wave(pose, limb, kept, shoulder, 0.25, 0.30, 3.2); continue; }
                Vec3d rest = ClawRest(side, _height.X, speed, stance);
                rest = new Vec3d(rest.X + _shiftX.X, Math.Max(rest.Y, Ground(input, rest.X, rest.Z) + 0.15), rest.Z + _shiftZ.X);
                Vec3d wanted = rest;
                double open = 0.20 + 0.07 * Math.Sin(_time * 1.9 + side * 2.0);
                bool striking = input.StrikeSide == side;
                if (striking)
                {
                    double sign = side == StriderLayout.Right ? 1.0 : -1.0;
                    var raised = new Vec3d(sign * 1.35, _height.X + 1.15, 1.05);
                    double t = input.StrikeT;
                    if (t < StrikeStarts) { double k = Smooth(t / StrikeStarts); wanted = rest + (raised - rest) * k; open = 0.2 + 0.6 * k; }
                    else if (t < StrikeLands) { double k = (t - StrikeStarts) / (StrikeLands - StrikeStarts); k *= k; wanted = raised + (input.StrikeAt - raised) * k; open = 0.8; }
                    else if (t < 0.76) { wanted = input.StrikeAt; open = 0.04; }
                    else { double k = Smooth((t - 0.76) / 0.24); wanted = input.StrikeAt + (rest - input.StrikeAt) * k; open = 0.04 + 0.16 * k; }
                    _claw[side] = wanted;
                }
                else
                {
                    // Eases back to rest rather than jumping there when a strike is called off.
                    _claw[side] += (wanted - _claw[side]) * Math.Min(1.0, dt * 6.0);
                }
                pose.End[limb] = _claw[side];
                Bend(pose, limb, shoulder, _claw[side], kept, ClawBend);
                int claw = limb * StriderLayout.Segments + 2;
                pose.FingerPosition[side] = pose.Position[claw] + pose.Rotation[claw].Rotate(StriderLayout.ClawHinge * StriderLayout.Size);
                pose.FingerRotation[side] = pose.Rotation[claw] * QuatD.AngleAxis(Vec3d.Right, -open);
            }
        }

        /// <summary>0 at rest, 1 while the claw is at full stretch.</summary>
        static double StrikeReach(double t)
        {
            if (t < StrikeStarts) return -0.4 * Smooth(t / StrikeStarts);
            if (t < StrikeLands) return -0.4 + 1.4 * (t - StrikeStarts) / (StrikeLands - StrikeStarts);
            if (t < 0.76) return 1.0;
            return 1.0 - Smooth((t - 0.76) / 0.24);
        }

        static double Smooth(double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            return t * t * (3.0 - 2.0 * t);
        }

        Vec3d ClawRest(int side, double height, double speed, StriderStance stance)
        {
            double sign = side == StriderLayout.Right ? 1.0 : -1.0;
            if (stance != StriderStance.Standing) return new Vec3d(sign * 0.9, 0.12, 2.3 + 0.25 * Math.Sin(_time * 1.1 + side * 3.0));
            double sway = Math.Sin(_time * 0.7 + side * 2.4);
            return new Vec3d(sign * (0.72 + 0.10 * sway), Math.Max(0.5, height - 1.35 + 0.14 * Math.Sin(_time * 0.9 + side * 1.3) + 0.18 * Math.Min(1.0, speed)),
                             2.05 + 0.12 * sway);
        }

        bool NeighbourInAir(StriderBody body, int limb)
        {
            int side = StriderLayout.SideOf(limb), place = StriderLayout.PlaceOf(limb);
            if (_swing[StriderLayout.Opposite(limb)]) return true;
            // The next leg that still carries weight along the same side, each way.
            for (int p = place - 1; p >= 1; p--)
            {
                int other = StriderLayout.Limb(side, p);
                if (!body.CarriesWeight(other)) continue;
                if (_swing[other]) return true;
                break;
            }
            for (int p = place + 1; p < StriderLayout.PerSide; p++)
            {
                int other = StriderLayout.Limb(side, p);
                if (!body.CarriesWeight(other)) continue;
                if (_swing[other]) return true;
                break;
            }
            return false;
        }

        /// <summary>What is left of a limb with nothing to stand on or strike with: held out and shaking.</summary>
        void Wave(StriderPose pose, int limb, int kept, Vec3d hip, double lift, double swing, double rate)
        {
            Vec3d outward = pose.BodyRotation.Rotate(StriderLayout.Out(limb));
            Vec3d up = pose.BodyRotation.Rotate(Vec3d.Up);
            double a = lift + swing * Math.Sin(_time * rate + limb * 1.7);
            double yaw = 0.22 * Math.Sin(_time * rate * 0.62 + limb * 2.9);
            outward = outward.RotatedAround(up, yaw);
            Vec3d way = outward * Math.Cos(a) + up * Math.Sin(a);
            Vec3d axis = Vec3d.Cross(outward, up);
            Vec3d at = hip;
            for (int s = 0; s < kept; s++)
            {
                int i = limb * StriderLayout.Segments + s;
                pose.Position[i] = at;
                pose.Rotation[i] = Look(way, Vec3d.Cross(axis, way));
                at += way * StriderLayout.Length(limb, s);
                // What hangs beyond the first piece droops.
                way = way.RotatedAround(axis, -0.5);
            }
            pose.End[limb] = at;
        }

        /// <summary>
        /// Bends a limb from its joint on the body to where its end should be. The upper piece and the rest make a
        /// triangle with the knee uppermost; the last piece is bent a fixed amount against the one before, inward.
        /// </summary>
        static void Bend(StriderPose pose, int limb, Vec3d hip, Vec3d end, int kept, double bend)
        {
            double a = StriderLayout.Length(limb, 0), b = StriderLayout.Length(limb, 1), c = kept >= 3 ? StriderLayout.Length(limb, 2) : 0.0;
            double second = Math.Sqrt(b * b + c * c + 2.0 * b * c * Math.Cos(bend));
            double offset = Math.Atan2(c * Math.Sin(bend), b + c * Math.Cos(bend));
            Vec3d to = end - hip;
            var level = new Vec3d(to.X, 0, to.Z);
            double r = level.Magnitude;
            Vec3d d = r > 1e-6 ? level / r : StriderLayout.Out(limb);
            double span = Math.Max(Math.Abs(a - second) + 0.02, Math.Min(a + second - 0.02, to.Magnitude));
            double toEnd = Math.Atan2(to.Y, r);
            double atHip = Math.Acos(Math.Max(-1.0, Math.Min(1.0, (a * a + span * span - second * second) / (2.0 * a * span))));
            double lift = toEnd + atHip;
            Vec3d axis = Vec3d.Cross(d, Vec3d.Up);
            Vec3d d1 = d * Math.Cos(lift) + Vec3d.Up * Math.Sin(lift);
            Vec3d knee = hip + d1 * a;
            Vec3d reach = hip + (d * Math.Cos(toEnd) + Vec3d.Up * Math.Sin(toEnd)) * span;
            Vec3d d2 = (reach - knee).Normalized.RotatedAround(axis, offset);
            Vec3d ankle = knee + d2 * b;
            int i = limb * StriderLayout.Segments;
            pose.Position[i] = hip;
            pose.Rotation[i] = Look(d1, Vec3d.Cross(axis, d1));
            pose.Position[i + 1] = knee;
            pose.Rotation[i + 1] = Look(d2, Vec3d.Cross(axis, d2));
            if (kept >= 3)
            {
                Vec3d d3 = d2.RotatedAround(axis, -bend);
                pose.Position[i + 2] = ankle;
                pose.Rotation[i + 2] = Look(d3, Vec3d.Cross(axis, d3));
            }
        }

        /// <summary>The turn that points +Z along <paramref name="forward"/> with +Y as near <paramref name="up"/> as that allows (Unity's LookRotation).</summary>
        public static QuatD Look(Vec3d forward, Vec3d up)
        {
            Vec3d f = forward.Normalized;
            Vec3d r = Vec3d.Cross(up, f);
            r = r.SqrMagnitude > 1e-12 ? r.Normalized : f.AnyPerpendicular();
            Vec3d u = Vec3d.Cross(f, r);
            double m00 = r.X, m01 = u.X, m02 = f.X, m10 = r.Y, m11 = u.Y, m12 = f.Y, m20 = r.Z, m21 = u.Z, m22 = f.Z;
            double trace = m00 + m11 + m22;
            QuatD q;
            if (trace > 0)
            {
                double s = Math.Sqrt(trace + 1.0) * 2.0;
                q = new QuatD((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25 * s);
            }
            else if (m00 > m11 && m00 > m22)
            {
                double s = Math.Sqrt(1.0 + m00 - m11 - m22) * 2.0;
                q = new QuatD(0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            else if (m11 > m22)
            {
                double s = Math.Sqrt(1.0 + m11 - m00 - m22) * 2.0;
                q = new QuatD((m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            else
            {
                double s = Math.Sqrt(1.0 + m22 - m00 - m11) * 2.0;
                q = new QuatD((m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s);
            }
            return q.Normalized;
        }

        struct Spring
        {
            public double X, V;

            /// <summary>A damped spring toward <paramref name="target"/>, stable for any step.</summary>
            public void Step(double target, double stiffness, double damping, double dt)
            {
                int steps = Math.Max(1, (int)Math.Ceiling(dt / 0.01));
                double h = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    V += ((target - X) * stiffness - V * damping) * h;
                    X += V * h;
                }
            }
        }
    }
}
