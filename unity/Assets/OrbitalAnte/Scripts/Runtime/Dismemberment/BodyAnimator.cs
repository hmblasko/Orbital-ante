using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>What the animation needs to know about a character this frame.</summary>
    public struct BodyAnimInput
    {
        public MovementState State;
        /// <summary>How fast the character is being moved forward right now, in metres per second.</summary>
        public double ForwardSpeed;
        /// <summary>How fast it is turning, in degrees per second. Positive is toward its own right.</summary>
        public double TurnRate;
        /// <summary>The movement cycle for hopping, crawling and wriggling, in radians (the mover owns it).</summary>
        public double Phase;
        /// <summary>True while the character is trying to get somewhere.</summary>
        public bool Moving;
        public double WalkSpeed;
        public double SprintSpeed;
        /// <summary>The leg that still works, for a one-legged character (BodySkeleton.Left or Right).</summary>
        public int StandingLeg;
        public bool LeftArmWorks;
        public bool RightArmWorks;
        /// <summary>1 while standing to aim at something, 0 otherwise.</summary>
        public double Aim;
        /// <summary>The side that holds the gun (BodySkeleton.Left or Right), or -1 for none.</summary>
        public int GunSide;
    }

    /// <summary>
    /// Works out a humanoid's pose every frame, with no stored animation clips and no Unity types.
    ///
    /// Standing characters are posed from the ground up: each foot is given a path over the ground that matches
    /// how fast the body really moves and turns, so a planted foot stays planted, and the legs are then bent to
    /// reach it. The pelvis bobs, sways and twists over the feet, the shoulders twist the other way, the arms
    /// swing, and the head stays level. Standing still it breathes, shifts its weight and looks about.
    /// Being shot knocks it about on springs. Hopping, crawling and wriggling have their own poses, and a change
    /// from one way of moving to another is blended, never snapped.
    /// </summary>
    public sealed class BodyAnimator
    {
        const double Deg = Math.PI / 180.0;
        const double TwoPi = Math.PI * 2.0;

        /// <summary>The finished pose. Read it after <see cref="Update"/>.</summary>
        public readonly BodyPose Pose = new BodyPose();

        readonly BodyPose _target = new BodyPose();
        readonly BodyPose _from = new BodyPose();
        MovementState _state = MovementState.Intact;
        bool _started;
        double _blend = 1.0;
        double _blendTime = 0.25;

        double _time;
        double _gait;          // 0..1, one full stride (two steps)
        double _cadence;       // strides per second
        double _speed;
        double _turn;
        double _aim;
        double _lean;

        // Being knocked about: degrees, and metres for the shove and the dip.
        Spring _hitPitch, _hitRoll, _hitYaw, _headPitch, _headRoll, _pushX, _pushZ, _dip;

        // What makes one character differ from the next.
        readonly double _timeOffset;
        readonly double _stagger;
        readonly double _armScale;
        readonly double _strideScale;
        uint _dice;
        double _lookYaw, _lookPitch, _lookYawGoal, _lookPitchGoal, _lookTimer;

        public BodyAnimator(int seed = 1)
        {
            _dice = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (_dice == 0) _dice = 1;
            _timeOffset = Roll() * 20.0;
            _stagger = (Roll() - 0.5) * 0.08;
            _armScale = 0.85 + Roll() * 0.35;
            _strideScale = 0.95 + Roll() * 0.10;
            _gait = Roll();
            _lookTimer = 0.5 + Roll() * 2.0;
        }

        /// <summary>Strides per second right now (two steps to a stride). For sounds and tests.</summary>
        public double Cadence => _cadence;
        /// <summary>Where in the stride the walk is, 0 to 1.</summary>
        public double Gait => _gait;

        /// <summary>
        /// A knock from a hit. The direction is the way the shot was travelling, in the character's own space
        /// (x to its right, z ahead). Height is 0 at the feet and 1 at the head. Side is where it landed across
        /// the body, -1 far left to 1 far right. Strength 1 is an ordinary bullet; losing a limb is about 2.5.
        /// </summary>
        public void Flinch(double directionX, double directionZ, double height, double side, double strength)
        {
            double length = Math.Sqrt(directionX * directionX + directionZ * directionZ);
            if (length < 1e-6 || strength <= 0) return;
            directionX /= length;
            directionZ /= length;
            strength = Math.Min(strength, 4.0);
            double upper = 0.35 + 0.65 * Clamp01(height);
            // Pushed along the shot: a shot from the front (travelling -z) rocks the body back.
            _hitPitch.V += directionZ * 190.0 * strength * upper;
            _hitRoll.V += -directionX * 170.0 * strength * upper;
            _hitYaw.V += -side * directionZ * 220.0 * strength * upper;
            _headPitch.V += directionZ * 150.0 * strength * upper;
            _headRoll.V += -directionX * 130.0 * strength * upper;
            _pushX.V += directionX * 0.55 * strength;
            _pushZ.V += directionZ * 0.55 * strength;
            // A low hit buckles the knees instead.
            _dip.V += (1.0 - Clamp01(height)) * 0.9 * strength;
        }

        /// <summary>Advances the animation by one frame and fills <see cref="Pose"/>.</summary>
        public void Update(double dt, in BodyAnimInput input)
        {
            if (dt < 0) dt = 0;
            // A long frame is taken in several short steps, so the springs stay steady and no time is lost:
            // the character has moved the whole frame's distance, and its stride has to keep up or the feet
            // skate. Beyond a fifth of a second (a hitch, not a frame rate) the rest is let go.
            if (dt > MaxStep * MaxSteps) dt = MaxStep * MaxSteps;
            int steps = Math.Max(1, (int)Math.Ceiling(dt / MaxStep - 1e-9));
            for (int i = 0; i < steps; i++) Step(dt / steps, input);
        }

        const double MaxStep = 0.05;
        const int MaxSteps = 4;

        void Step(double dt, in BodyAnimInput input)
        {
            _time += dt;

            double towardSpeed = 1.0 - Math.Exp(-9.0 * dt);
            double wanted = Math.Max(0.0, input.ForwardSpeed);
            double leanGoal = Clamp((wanted - _speed) * 2.4, -7.0, 9.0);
            _lean += (leanGoal - _lean) * (1.0 - Math.Exp(-7.0 * dt));
            _speed += (wanted - _speed) * towardSpeed;
            _turn += (input.TurnRate - _turn) * (1.0 - Math.Exp(-12.0 * dt));
            _aim += (Clamp01(input.Aim) - _aim) * (1.0 - Math.Exp(-6.0 * dt));

            _hitPitch.Step(dt, 3.1, 0.34);
            _hitRoll.Step(dt, 3.1, 0.34);
            _hitYaw.Step(dt, 2.8, 0.40);
            _headPitch.Step(dt, 2.2, 0.50);
            _headRoll.Step(dt, 2.2, 0.50);
            _pushX.Step(dt, 2.6, 0.55);
            _pushZ.Step(dt, 2.6, 0.55);
            _dip.Step(dt, 2.9, 0.45);
            // Automatic fire must not wind the body up further and further: the head is the target that matters,
            // and it has to stay roughly where it looks to be.
            _hitPitch.Limit(22.0);
            _hitRoll.Limit(22.0);
            _hitYaw.Limit(25.0);
            _headPitch.Limit(16.0);
            _headRoll.Limit(16.0);
            _pushX.Limit(0.07);
            _pushZ.Limit(0.07);
            _dip.Limit(0.12);
            StepLook(dt);

            if (input.State == MovementState.Intact)
            {
                _cadence = StandingCadence(input);
                _gait = Frac(_gait + _cadence * dt);
            }

            if (!_started)
            {
                _started = true;
                _state = input.State;
                _speed = wanted;
                _turn = input.TurnRate;
                _blend = 1.0;
            }
            else if (input.State != _state)
            {
                bool wasUpright = _state == MovementState.Intact || _state == MovementState.OneLeg;
                bool isUpright = input.State == MovementState.Intact || input.State == MovementState.OneLeg;
                _from.CopyFrom(Pose);
                _blend = 0.0;
                // Falling over takes a little longer than changing step.
                _blendTime = wasUpright == isUpright ? 0.22 : 0.42;
                if (input.State == MovementState.HeadOnly) _blendTime = 0.12;
                _state = input.State;
            }

            if (input.State == MovementState.Dead) return;

            Build(_target, input);

            if (_blend < 1.0)
            {
                _blend = Math.Min(1.0, _blend + dt / _blendTime);
                Pose.CopyFrom(_from);
                Pose.BlendToward(_target, Smooth(_blend));
            }
            else
            {
                Pose.CopyFrom(_target);
            }
        }

        // ------------------------------------------------------------------ poses

        void Build(BodyPose pose, in BodyAnimInput input)
        {
            pose.Reset();
            switch (input.State)
            {
                case MovementState.Intact: BuildStanding(pose, input); break;
                case MovementState.OneLeg: BuildOneLeg(pose, input); break;
                case MovementState.Crawl: BuildCrawl(pose, input); break;
                case MovementState.Wriggle: BuildWriggle(pose, input); break;
                case MovementState.HeadOnly: BuildHeadOnly(pose, input); break;
            }
        }

        double StandingCadence(in BodyAnimInput input)
        {
            double walk = Math.Max(0.1, input.WalkSpeed);
            double sprint = Math.Max(walk + 0.1, input.SprintSpeed);
            double v = _speed;
            double moveAmount = Clamp01(v / (walk * 0.5));
            double straight = v <= walk
                ? Lerp(1.15, 1.75, v / walk)
                : Lerp(1.75, 2.70, Clamp01((v - walk) / (sprint - walk)));
            double turnAmount = Clamp01((Math.Abs(_turn) - 20.0) / 50.0);
            // Shuffle round fast enough that a planted foot never twists more than about 30 degrees each way.
            double turning = Math.Min(2.4, Math.Abs(_turn) * 0.62 / 60.0);
            return Math.Max(straight * moveAmount / _strideScale, turning * turnAmount);
        }

        void BuildStanding(BodyPose pose, in BodyAnimInput input)
        {
            double walk = Math.Max(0.1, input.WalkSpeed);
            double sprint = Math.Max(walk + 0.1, input.SprintSpeed);
            double v = _speed;
            double moveAmount = Clamp01(v / (walk * 0.5));
            double run = Smooth((v - walk * 1.2) / Math.Max(0.1, sprint * 0.95 - walk * 1.2));
            double turnRadians = _turn * Deg;
            double turnAmount = Clamp01((Math.Abs(_turn) - 20.0) / 50.0);
            double activity = Math.Max(moveAmount, turnAmount);
            double still = 1.0 - activity;
            double aimStill = _aim * (1.0 - moveAmount);
            double gunSign = input.GunSide == BodySkeleton.Left ? -1.0 : 1.0;

            double duty = Lerp(0.62, 0.36, run);
            double stanceTime = duty / Math.Max(_cadence, 0.6);
            double g = _gait;

            // ---- pelvis over the feet
            double breath = Math.Sin(TwoPi * (_time + _timeOffset) / 3.7);
            double weight = Math.Sin(TwoPi * (_time + _timeOffset) / 7.3) + 0.35 * Math.Sin(TwoPi * (_time + _timeOffset) / 3.1);
            double idle = still * (1.0 - 0.7 * _aim);

            double mid = g - duty * 0.5;                       // 0 at the middle of the left foot's time on the ground
            double perStep = Math.Cos(2.0 * TwoPi * mid);      // 1 at the middle of either foot's time on the ground
            double perStride = Math.Cos(TwoPi * mid);          // 1 over the left foot, -1 over the right
            double bob = (0.020 * (1.0 - run) - 0.045 * run) * moveAmount * perStep;
            double drop = 0.012 + 0.036 * moveAmount + 0.022 * run + 0.045 * aimStill + 0.02 * turnAmount * (1.0 - moveAmount);
            double sway = -(0.022 * moveAmount * (1.0 - 0.6 * run) + 0.018 * turnAmount * (1.0 - moveAmount)) * perStride
                          + 0.030 * weight * idle;
            double hipRoll = -3.2 * moveAmount * perStride + 2.2 * weight * idle;
            double gaitYaw = (5.0 + 5.0 * run) * moveAmount * Math.Cos(TwoPi * g);
            double stanceYaw = gunSign * 16.0 * aimStill;
            double lean = 2.0 * moveAmount + 9.0 * run + _lean * moveAmount;

            pose.RootRotation = QuatD.Identity;
            pose.RootPosition = new Vec3d(sway + _pushX.X, bob - drop - Math.Max(0.0, _dip.X), _pushZ.X - 0.03 * run);
            QuatD hips = Qy(gaitYaw + stanceYaw) * Qx(lean) * Qz(hipRoll);
            pose.Local[(int)BodyBone.Hips] = hips;
            Vec3d hipsPosition = pose.RootPosition + new Vec3d(0, BodySkeleton.HipsY, 0);

            // ---- feet on the ground, legs bent to reach them
            double halfWidth = Lerp(Lerp(0.125, 0.098, moveAmount), 0.072, run) + 0.03 * aimStill;
            double lift = 0.045 * turnAmount * (1.0 - moveAmount) + 0.07 * moveAmount + 0.20 * run;
            double strikeToeUp = (14.0 + 6.0 * run) * moveAmount;
            double rollOff = (30.0 + 22.0 * run) * moveAmount;
            Vec3d ankleLeft = Vec3d.Zero, ankleRight = Vec3d.Zero;
            QuatD footLeft = QuatD.Identity, footRight = QuatD.Identity;
            for (int s = 0; s < 2; s++)
            {
                double sx = s == BodySkeleton.Left ? -1.0 : 1.0;
                double u = Frac(g + (s == BodySkeleton.Right ? 0.5 : 0.0));
                // Standing still, one foot sits a little ahead of the other; aiming, the gun-side foot drops back.
                double zBias = (s == BodySkeleton.Left ? _stagger : -_stagger) * (1.0 - moveAmount) * (1.0 - _aim)
                               + (sx == gunSign ? -0.11 : 0.08) * aimStill;
                double restYaw = sx == gunSign ? gunSign * 32.0 * aimStill : 0.0;

                Vec3d foot;
                double yaw, pitch, rise;
                if (u < duty)
                {
                    double k = u / duty;
                    double tau = (k - 0.5) * stanceTime;
                    foot = StancePoint(sx * halfWidth, zBias, tau, v, turnRadians, out yaw);
                    double toeUp = strikeToeUp * (1.0 - Smooth(k / 0.22));
                    double toeDown = rollOff * Smooth((k - 0.58) / 0.42);
                    pitch = toeDown - toeUp;
                    // Rolling up onto the ball of the foot lifts the ankle.
                    rise = Math.Sin(toeDown * Deg) * 0.12;
                }
                else
                {
                    double k = (u - duty) / (1.0 - duty);
                    Vec3d from = StancePoint(sx * halfWidth, zBias, stanceTime * 0.5, v, turnRadians, out double yawFrom);
                    Vec3d to = StancePoint(sx * halfWidth, zBias, -stanceTime * 0.5, v, turnRadians, out double yawTo);
                    double e = Smooth(k);
                    foot = from + (to - from) * e;
                    yaw = Lerp(yawFrom, yawTo, e);
                    // A run kicks the heel up early in the swing; a walk lifts evenly.
                    double arc = Math.Sin(Math.PI * Math.Pow(k, Lerp(1.0, 0.72, run)));
                    rise = lift * arc + Math.Sin(rollOff * Deg) * 0.12 * (1.0 - Smooth(k / 0.35));
                    pitch = Lerp(rollOff, -strikeToeUp, Smooth(k)) + 18.0 * run * arc;
                }

                Vec3d ankle = new Vec3d(foot.X, BodySkeleton.AnkleY + rise, foot.Z);
                if (s == BodySkeleton.Left) { ankleLeft = ankle; footLeft = Qy(yaw + restYaw) * Qx(pitch); }
                else { ankleRight = ankle; footRight = Qy(yaw + restYaw) * Qx(pitch); }
            }

            // A long stride is further than a straight leg can reach from a high pelvis, so the pelvis sinks just
            // enough for both legs to reach their feet: the dip at each footfall comes from the legs themselves.
            Vec3d jointLeft = hipsPosition + hips.Rotate(BodySkeleton.RestOffset(BodyBone.LeftThigh));
            Vec3d jointRight = hipsPosition + hips.Rotate(BodySkeleton.RestOffset(BodyBone.RightThigh));
            double sink = Math.Max(SinkToReach(jointLeft, ankleLeft), SinkToReach(jointRight, ankleRight));
            if (sink > 0.0)
            {
                var down = new Vec3d(0.0, sink, 0.0);
                pose.RootPosition = pose.RootPosition - down;
                jointLeft = jointLeft - down;
                jointRight = jointRight - down;
            }
            SolveLeg(pose, BodySkeleton.Left, hips, jointLeft, ankleLeft, footLeft);
            SolveLeg(pose, BodySkeleton.Right, hips, jointRight, ankleRight, footRight);


            // ---- upper body
            double spinePitch = 1.1 * breath * still + 0.25 * lean + 5.0 * aimStill;
            double spineRoll = -0.8 * hipRoll;
            pose.Local[(int)BodyBone.Spine] = Qy(-1.7 * gaitYaw) * Qx(spinePitch) * Qz(spineRoll);
            double look = (1.0 - _aim) * still;
            pose.Local[(int)BodyBone.Head] = Qy(0.7 * gaitYaw - stanceYaw + _lookYaw * look)
                                             * Qx(-(0.9 * lean + 0.6 * spinePitch) + _lookPitch * look)
                                             * Qz(-0.3 * spineRoll);

            double swingSize = (16.0 + 26.0 * run) * moveAmount * _armScale;
            double lag = 0.04;
            for (int s = 0; s < 2; s++)
            {
                double sign = s == BodySkeleton.Left ? 1.0 : -1.0;
                double wave = sign * Math.Cos(TwoPi * (g - lag));      // -1 is this arm fully forward
                double forward = Math.Max(0.0, -wave);
                double down = 79.0 - 3.0 * run + 1.2 * breath * still;
                double swing = swingSize * wave + 2.0 * Math.Sin(TwoPi * (_time + _timeOffset) / 5.1 + s) * still - 4.0 * run;
                double elbow = 14.0 + 6.0 * moveAmount + 60.0 * run + 16.0 * moveAmount * forward;
                SetArm(pose, s, down, swing, elbow);
            }

            AddKnocks(pose, 1.0);
        }

        /// <summary>
        /// Where a planted foot is, in the character's space, a time <paramref name="tau"/> before (negative) or
        /// after (positive) the middle of its time on the ground. The ground slides back under a moving body and
        /// swings round under a turning one, and the foot goes with it.
        /// </summary>
        static Vec3d StancePoint(double x, double z, double tau, double speed, double turnRadians, out double yawDegrees)
        {
            double angle = -turnRadians * tau;
            double c = Math.Cos(angle), s = Math.Sin(angle);
            yawDegrees = angle / Deg;
            return new Vec3d(x * c + z * s, 0.0, -x * s + z * c - speed * tau);
        }

        void BuildOneLeg(BodyPose pose, in BodyAnimInput input)
        {
            int standing = input.StandingLeg == BodySkeleton.Left ? BodySkeleton.Left : BodySkeleton.Right;
            int missing = 1 - standing;
            double sx = standing == BodySkeleton.Left ? -1.0 : 1.0;
            double cycle = Frac(input.Phase / TwoPi);
            double sin = Math.Sin(input.Phase);
            double air = input.Moving ? Math.Max(0.0, sin) : 0.0;        // 1 at the top of the hop
            double crouch = input.Moving ? Math.Max(0.0, -sin) : 0.0;    // winding up on the ground
            double wobble = Math.Sin((_time + _timeOffset) * 2.3) + 0.5 * Math.Sin((_time + _timeOffset) * 3.7);
            double still = input.Moving ? 0.0 : 1.0;

            // The body rocks forward over the planted foot, then the foot swings through under it in the air.
            double rock;
            if (!input.Moving) rock = 0.0;
            else if (cycle < 0.5) rock = Lerp(0.07, -0.07, cycle * 2.0);
            else rock = Lerp(-0.07, 0.07, Smooth((cycle - 0.5) * 2.0));

            double height = air * 0.20 - crouch * 0.11 - 0.025 - Math.Max(0.0, _dip.X);
            double roll = sx * -8.0 + wobble * 1.6 * still;   // lean over the good leg
            double lean = 7.0 + 9.0 * air - 5.0 * crouch;
            pose.RootPosition = new Vec3d(sx * 0.035 + _pushX.X + wobble * 0.008 * still, height, rock + _pushZ.X);
            QuatD hips = Qx(lean) * Qz(roll);
            pose.Local[(int)BodyBone.Hips] = hips;
            Vec3d hipsPosition = pose.RootPosition + new Vec3d(0, BodySkeleton.HipsY, 0);

            Vec3d ankle = new Vec3d(sx * 0.045, BodySkeleton.AnkleY + air * 0.26, 0.0);
            Vec3d joint = hipsPosition + hips.Rotate(BodySkeleton.RestOffset(BodySkeleton.Thigh(standing)));
            SolveLeg(pose, standing, hips, joint, ankle, Qx(26.0 * air));

            // Whatever is left of the other leg flails.
            pose.Local[(int)BodySkeleton.Thigh(missing)] = Qx(-28.0 + 30.0 * Math.Sin(input.Phase + 1.0) + 6.0 * wobble * still) * Qz(sx * 10.0);
            pose.Local[(int)BodySkeleton.Shin(missing)] = Qx(34.0 + 20.0 * crouch);

            pose.Local[(int)BodyBone.Spine] = Qx(-4.0 * air + 8.0 * crouch) * Qz(-0.6 * roll);
            pose.Local[(int)BodyBone.Head] = Qx(-(lean * 0.8)) * Qz(-0.3 * roll);

            // Arms out and flapping for balance, a beat apart.
            for (int s = 0; s < 2; s++)
            {
                double beat = Math.Sin(input.Phase + (s == standing ? 0.0 : 0.9));
                double down = 50.0 - 30.0 * air + 12.0 * crouch + 5.0 * beat * (input.Moving ? 1.0 : 0.0) - 4.0 * wobble * still * (s == standing ? 1.0 : -1.0);
                SetArm(pose, s, down, -8.0 + 14.0 * crouch - 10.0 * air, 24.0 + 26.0 * crouch);
            }

            AddKnocks(pose, 1.0);
        }

        /// <summary>Lays the figure face down along its forward direction, its chest resting where the character is.</summary>
        static void SetProne(BodyPose pose, double yawDegrees, double rollDegrees, double pitchBumpDegrees)
        {
            pose.RootRotation = Qy(yawDegrees) * Qx(86.0 - pitchBumpDegrees) * Qz(rollDegrees);
            // Whatever the tilt, the middle of the torso stays put, so humping up never lifts the body off the ground.
            var torsoCentre = new Vec3d(0.0, BodySkeleton.SpineY + BodySkeleton.TorsoCentreAboveSpine, 0.0);
            pose.RootPosition = new Vec3d(0.0, ProneHeight + 0.004 * pitchBumpDegrees, 0.0) - pose.RootRotation.Rotate(torsoCentre);
        }

        /// <summary>How high the middle of the torso is when the figure lies on its chest.</summary>
        public const double ProneHeight = 0.21;

        void BuildCrawl(BodyPose pose, in BodyAnimInput input)
        {
            double sin = input.Moving ? Math.Sin(input.Phase) : 0.0;
            double heave = input.Moving ? Math.Abs(Math.Cos(input.Phase)) : 0.0;
            double breath = Math.Sin(TwoPi * (_time + _timeOffset) / 3.2);
            SetProne(pose, 6.0 * sin, 8.0 * sin, 3.0 * heave + (input.Moving ? 0.0 : 1.5 * breath));

            for (int s = 0; s < 2; s++)
            {
                bool works = s == BodySkeleton.Left ? input.LeftArmWorks : input.RightArmWorks;
                if (!works)
                {
                    // A stump, or nothing: let it trail.
                    SetArm(pose, s, 55.0, 14.0 + 6.0 * sin, 20.0);
                    continue;
                }
                if (!input.Moving)
                {
                    // Propped on the elbows, getting its breath back.
                    SetArm(pose, s, -42.0, -30.0, 62.0 + 4.0 * breath);
                    continue;
                }
                // Each arm reaches quickly (40% of the time) and hauls slowly (60%). The right arm hauls around
                // phase 0 and the left around half a turn later, in step with the mover's pulls.
                double a = Frac(input.Phase / TwoPi + 0.3 + (s == BodySkeleton.Left ? 0.5 : 0.0));
                double reach, swing, elbow;
                if (a < 0.6)
                {
                    double k = a / 0.6;
                    reach = Lerp(-80.0, -8.0, Smooth(k));
                    swing = -30.0;
                    elbow = 16.0 + 58.0 * Math.Sin(Math.PI * k);
                }
                else
                {
                    double k = (a - 0.6) / 0.4;
                    reach = Lerp(-8.0, -80.0, Smooth(k));
                    swing = -30.0 + 38.0 * Math.Sin(Math.PI * k);
                    elbow = Lerp(40.0, 16.0, k);
                }
                SetArm(pose, s, reach, swing, elbow);
            }

            // Leg stumps drag and twitch behind.
            pose.Local[(int)BodyBone.LeftThigh] = Qx(9.0 * sin) * Qz(-6.0);
            pose.Local[(int)BodyBone.RightThigh] = Qx(-9.0 * sin) * Qz(6.0);
            pose.Local[(int)BodyBone.Spine] = Qx(-6.0 - 3.0 * heave) * Qz(-4.0 * sin);
            // Head up, looking where it is going, straining with each pull.
            pose.Local[(int)BodyBone.Head] = Qy(-9.0 * sin + _lookYaw * (input.Moving ? 0.0 : 0.6)) * Qx(-40.0 + 7.0 * heave);
            AddKnocks(pose, 0.5);
        }

        void BuildWriggle(BodyPose pose, in BodyAnimInput input)
        {
            double sin = input.Moving ? Math.Sin(input.Phase) : 0.0;
            double bump = input.Moving ? Math.Max(0.0, Math.Sin(input.Phase + 0.8)) : 0.0;
            double breath = Math.Sin(TwoPi * (_time + _timeOffset) / 2.6);
            // Snake from side to side and hump up just before each lunge.
            SetProne(pose, 14.0 * sin, 10.0 * sin, 11.0 * bump + (input.Moving ? 0.0 : 1.5 * breath));
            pose.Local[(int)BodyBone.Spine] = Qx(-9.0 * bump) * Qz(6.0 * sin);
            pose.Local[(int)BodyBone.LeftThigh] = Qx(12.0 * sin + 10.0 * bump);
            pose.Local[(int)BodyBone.RightThigh] = Qx(-12.0 * sin + 10.0 * bump);
            pose.Local[(int)BodyBone.Head] = Qy(-14.0 * sin + _lookYaw * (input.Moving ? 0.0 : 0.6)) * Qx(-32.0 + 14.0 * bump);
            SetArm(pose, BodySkeleton.Left, 62.0, 10.0, 15.0);
            SetArm(pose, BodySkeleton.Right, 62.0, 10.0, 15.0);
            AddKnocks(pose, 0.5);
        }

        static void BuildHeadOnly(BodyPose pose, in BodyAnimInput input)
        {
            // The mover drops the empty skeleton and rolls the head itself; every bone stays as it was built.
            pose.RootRotation = QuatD.Identity;
            pose.RootPosition = Vec3d.Zero;
        }

        // ------------------------------------------------------------------ building blocks

        /// <summary>
        /// Arms hang from a T-pose. Down lowers the arm from straight out (negative raises it above the shoulder),
        /// swing carries a lowered arm forward (negative) or back, and the elbow bends forward.
        /// </summary>
        static void SetArm(BodyPose pose, int side, double downDegrees, double swingDegrees, double elbowDegrees)
        {
            double sign = side == BodySkeleton.Left ? 1.0 : -1.0;
            pose.Local[(int)BodySkeleton.UpperArm(side)] = Qx(swingDegrees) * Qz(sign * downDegrees);
            pose.Local[(int)BodySkeleton.Forearm(side)] = Qy(sign * elbowDegrees);
        }

        /// <summary>
        /// Bends a leg so that its ankle reaches a point, knee forward, and turns the foot to a given heading and
        /// tilt. Everything is in the character's space; <paramref name="hips"/> is the pelvis's turn there.
        /// </summary>
        static void SolveLeg(BodyPose pose, int side, QuatD hips, Vec3d joint, Vec3d ankle, QuatD footTurn)
        {
            const double thigh = BodySkeleton.ThighLength;
            const double shin = BodySkeleton.ShinLength;
            Vec3d t = hips.Inverse.Rotate(ankle - joint);

            // Tip the leg's plane sideways until it holds the target, then solve the two bones in that plane.
            double m = Math.Sqrt(t.X * t.X + t.Y * t.Y);
            double spread = Math.Atan2(t.X, -t.Y);
            double reach = Math.Sqrt(m * m + t.Z * t.Z);
            reach = Clamp(reach, Math.Abs(thigh - shin) + 0.02, thigh + shin - 0.004);
            double toward = Math.Atan2(t.Z, m);
            double atHip = Math.Acos(Clamp((thigh * thigh + reach * reach - shin * shin) / (2.0 * thigh * reach), -1.0, 1.0));
            double atKnee = Math.Acos(Clamp((thigh * thigh + shin * shin - reach * reach) / (2.0 * thigh * shin), -1.0, 1.0));

            QuatD thighTurn = QuatD.AngleAxis(Vec3d.Forward, spread) * QuatD.AngleAxis(Vec3d.Right, -(toward + atHip));
            QuatD shinTurn = QuatD.AngleAxis(Vec3d.Right, Math.PI - atKnee);
            pose.Local[(int)BodySkeleton.Thigh(side)] = thighTurn;
            pose.Local[(int)BodySkeleton.Shin(side)] = shinTurn;
            QuatD above = pose.RootRotation * hips * thighTurn * shinTurn;
            pose.Local[(int)BodySkeleton.Foot(side)] = (above.Inverse * footTurn).Normalized;
        }

        /// <summary>How far a hip must come down for a nearly straight leg to reach from it to an ankle (0 if it already can).</summary>
        static double SinkToReach(Vec3d joint, Vec3d ankle)
        {
            const double longest = BodySkeleton.ThighLength + BodySkeleton.ShinLength - 0.012;
            double dx = ankle.X - joint.X, dz = ankle.Z - joint.Z;
            double flat = dx * dx + dz * dz;
            double room = longest * longest - flat;
            double canStandAt = room > 0.01 ? Math.Sqrt(room) : 0.1;
            return Math.Max(0.0, (joint.Y - ankle.Y) - canStandAt);
        }

        /// <summary>Adds the wobble from being shot to the spine, head and arms.</summary>
        void AddKnocks(BodyPose pose, double amount)
        {
            double pitch = _hitPitch.X * amount, roll = _hitRoll.X * amount, yaw = _hitYaw.X * amount;
            if (Math.Abs(pitch) + Math.Abs(roll) + Math.Abs(yaw) + Math.Abs(_headPitch.X) + Math.Abs(_headRoll.X) < 0.01) return;
            int spine = (int)BodyBone.Spine, head = (int)BodyBone.Head;
            pose.Local[spine] = pose.Local[spine] * Qy(yaw) * Qx(pitch * 0.7) * Qz(roll * 0.7);
            // The head follows late and overshoots.
            pose.Local[head] = pose.Local[head] * Qx(_headPitch.X * amount * 0.8 - pitch * 0.35) * Qz(_headRoll.X * amount * 0.8 - roll * 0.35);
            double fling = Math.Min(40.0, (Math.Abs(pitch) + Math.Abs(roll)) * 0.9);
            for (int s = 0; s < 2; s++)
            {
                double sign = s == BodySkeleton.Left ? 1.0 : -1.0;
                int arm = (int)BodySkeleton.UpperArm(s);
                pose.Local[arm] = Qz(-sign * fling) * pose.Local[arm];
            }
        }

        void StepLook(double dt)
        {
            _lookTimer -= dt;
            if (_lookTimer <= 0.0)
            {
                // Mostly looks ahead; now and then a glance to one side.
                bool glance = Roll() < 0.55;
                _lookYawGoal = glance ? (Roll() - 0.5) * 40.0 : 0.0;
                _lookPitchGoal = glance ? (Roll() - 0.4) * 10.0 : 0.0;
                _lookTimer = 1.4 + Roll() * 3.2;
            }
            double k = 1.0 - Math.Exp(-3.5 * dt);
            _lookYaw += (_lookYawGoal - _lookYaw) * k;
            _lookPitch += (_lookPitchGoal - _lookPitch) * k;
        }

        /// <summary>A number from 0 to 1 that differs per character. Only for how things look, never for rules.</summary>
        double Roll()
        {
            _dice ^= _dice << 13;
            _dice ^= _dice >> 17;
            _dice ^= _dice << 5;
            return (_dice & 0xFFFFFF) / (double)0x1000000;
        }

        struct Spring
        {
            public double X;
            public double V;

            /// <summary>Stops the swing at a furthest reach, and stops it pushing further that way.</summary>
            public void Limit(double reach)
            {
                if (X > reach) { X = reach; if (V > 0.0) V = 0.0; }
                else if (X < -reach) { X = -reach; if (V < 0.0) V = 0.0; }
            }

            /// <summary>Pulls back to zero. Frequency in swings per second; damping 1 would stop without overshoot.</summary>
            public void Step(double dt, double frequency, double damping)
            {
                if (X == 0.0 && V == 0.0) return;
                double w = TwoPi * frequency;
                V += (-w * w * X - 2.0 * damping * w * V) * dt;
                X += V * dt;
                if (Math.Abs(X) < 1e-5 && Math.Abs(V) < 1e-4)
                {
                    X = 0.0;
                    V = 0.0;
                }
            }
        }

        static QuatD Qx(double degrees) => QuatD.AngleAxis(Vec3d.Right, degrees * Deg);
        static QuatD Qy(double degrees) => QuatD.AngleAxis(Vec3d.Up, degrees * Deg);
        static QuatD Qz(double degrees) => QuatD.AngleAxis(Vec3d.Forward, degrees * Deg);
        static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
        static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        static double Frac(double v) => v - Math.Floor(v);

        static double Smooth(double t)
        {
            t = Clamp01(t);
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
