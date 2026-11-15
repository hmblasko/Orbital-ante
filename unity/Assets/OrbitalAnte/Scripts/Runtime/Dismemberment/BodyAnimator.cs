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

        /// <summary>How much of each arm's weight is gone: 0 whole, about 0.15 for a hand, 0.5 a forearm, 1 the whole arm.</summary>
        public double ArmLossLeft, ArmLossRight;
        /// <summary>One leg: how much of the lost leg is left, 0 (thigh gone) to 1 (only the foot gone).</summary>
        public double StumpLength;
        /// <summary>True when there is something to look at: <see cref="LookX"/>, <see cref="LookY"/>, <see cref="LookZ"/>.</summary>
        public bool HasLook;
        /// <summary>The point to look at, in the character's space at the skeleton's size (x right, y up from the feet, z ahead).</summary>
        public double LookX, LookY, LookZ;
        /// <summary>0 to 1: hurrying bent low after something (a dropped gun), with quick short steps.</summary>
        public double Scramble;
        /// <summary>0 to 1: bending down to take something off the ground with the hand on <see cref="ReachSide"/>.</summary>
        public double Reach;
        public int ReachSide;
        /// <summary>0 to 1, one leg only: it landed badly and is fighting to keep its balance on the spot.</summary>
        public double Teeter;
    }

    /// <summary>
    /// How one kind of character carries itself: the same skeleton and the same walk, held differently. Plain
    /// numbers, never changed after they are made, so one style can be shared by every character of a kind.
    /// The default is the ordinary trooper and changes nothing.
    /// </summary>
    public sealed class BodyStyle
    {
        /// <summary>Degrees the whole body leans forward from the hips, standing or moving.</summary>
        public readonly double Lean;
        /// <summary>Degrees the back curls forward. Negative stands up very straight.</summary>
        public readonly double Hunch;
        /// <summary>Degrees the arms are held out from the sides.</summary>
        public readonly double ArmsOut;
        /// <summary>Extra degrees the elbows are always bent.</summary>
        public readonly double ElbowBend;
        /// <summary>How far apart the feet are kept: 1 is the trooper's stance.</summary>
        public readonly double Stance;
        /// <summary>How long each stride is: above 1 takes fewer, longer steps at the same speed.</summary>
        public readonly double Stride;
        /// <summary>How much the body rises and falls with each step.</summary>
        public readonly double Bob;
        /// <summary>How much the hips swing from side to side over each foot.</summary>
        public readonly double Sway;
        /// <summary>How far the arms swing.</summary>
        public readonly double ArmSwing;
        /// <summary>How much it fidgets standing still: breathing, shifting its weight, looking about. 0 is a statue.</summary>
        public readonly double Idle;
        /// <summary>Metres the hips are carried lower than a trooper's, knees bent to suit.</summary>
        public readonly double Crouch;
        /// <summary>How deep the chest is beside the trooper's (the torso's number in BodyFit), so a body lying on it rests on the ground and not in it.</summary>
        public readonly double Girth;

        public BodyStyle(double lean = 0.0, double hunch = 0.0, double armsOut = 0.0, double elbowBend = 0.0, double stance = 1.0, double stride = 1.0,
                         double bob = 1.0, double sway = 1.0, double armSwing = 1.0, double idle = 1.0, double crouch = 0.0, double girth = 1.0)
        {
            Lean = Limit(lean, -10.0, 20.0);
            Hunch = Limit(hunch, -8.0, 20.0);
            ArmsOut = Limit(armsOut, 0.0, 40.0);
            ElbowBend = Limit(elbowBend, 0.0, 50.0);
            Stance = Limit(stance, 0.5, 2.0);
            Stride = Limit(stride, 0.6, 1.5);
            Bob = Limit(bob, 0.0, 2.5);
            Sway = Limit(sway, 0.0, 2.5);
            ArmSwing = Limit(armSwing, 0.0, 2.0);
            Idle = Limit(idle, 0.0, 2.0);
            Crouch = Limit(crouch, 0.0, 0.12);
            Girth = Limit(girth, 0.6, 1.8);
        }

        static double Limit(double v, double low, double high) => double.IsNaN(v) ? low : v < low ? low : v > high ? high : v;

        /// <summary>The ordinary soldier: changes nothing.</summary>
        public static readonly BodyStyle Trooper = new BodyStyle();
        /// <summary>Small and quick: leans into its run, knees bent, arms pumping, never quite still.</summary>
        public static readonly BodyStyle Runner = new BodyStyle(lean: 9.0, crouch: 0.035, stride: 1.0, armSwing: 1.25, bob: 0.8, stance: 0.9, idle: 1.3, girth: 0.88);
        /// <summary>Huge and heavy: feet wide, arms held out round its own bulk, rolling from foot to foot on long, slow strides.</summary>
        public static readonly BodyStyle Bruiser = new BodyStyle(lean: 3.0, hunch: 9.0, armsOut: 20.0, elbowBend: 20.0, stance: 1.55, stride: 1.18, bob: 1.5, sway: 1.5, armSwing: 0.7, idle: 0.8, girth: 1.25);
        /// <summary>Tall and still: bolt upright, feet together, gliding more than walking, hardly moving at rest.</summary>
        public static readonly BodyStyle Marksman = new BodyStyle(hunch: -2.0, armsOut: 2.0, stance: 0.85, stride: 1.15, bob: 0.6, sway: 0.5, armSwing: 0.6, idle: 0.4, girth: 0.90);

        /// <summary>The style that goes with a body model, by the model's name. Anything unknown walks like a trooper.</summary>
        public static BodyStyle For(string model)
        {
            switch (model)
            {
                case "EnemyRunner": return Runner;
                case "EnemyBruiser": return Bruiser;
                case "EnemyMarksman": return Marksman;
                default: return Trooper;
            }
        }
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

        readonly BodyStyle _style;
        double _size = 1.0;

        /// <summary>How this character carries itself. Set when it is made.</summary>
        public BodyStyle Style => _style;

        /// <summary>
        /// How large the model is drawn (its root scale). The animator works at the skeleton's own size, so the
        /// speeds it is given are divided by this: a body drawn 1.3 times as large covers 1.3 times the ground
        /// with every stride, and without this its feet would skate.
        /// </summary>
        public double Size
        {
            get => _size;
            set => _size = double.IsNaN(value) || value < 0.2 ? 0.2 : value > 5.0 ? 5.0 : value;
        }

        /// <param name="style">How it carries itself; null is the ordinary trooper.</param>
        public BodyAnimator(int seed = 1, BodyStyle style = null)
        {
            _style = style ?? BodyStyle.Trooper;
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

        // ------------------------------------------------------------------ reactions
        //
        // Timings and angles below were measured from the CMU Graphics Lab Motion Capture Database (free for
        // research and commercial projects; see docs/reports/enemy-animation.md for the clips and numbers):
        // a calm pick-up takes 0.7 s down and 0.6 s up (69_70, 69_73), a stumble throws the trunk 40-65
        // degrees forward with the arms flung above the shoulders for about a second and a half (104_13),
        // balancing on one leg holds the arms about 70 degrees out from the sides with the trunk 20 degrees
        // forward and the free knee bent about 95 degrees (49_18), a one-footed hop takes about 0.55 s and
        // lifts the hips about a sixth of a leg (49_02), a hurt walk leans about 10 degrees to one side (91_24),
        // and a hand is held to a hurt body (91_26). The startle itself is the classic pattern (Landis and
        // Hunt, 1939): within a tenth of a second the shoulders come up, the elbows bend, the trunk curls
        // forward and the knees give, and it is over within about half a second.

        const double StartleTime = 0.7;
        const double StumbleTime = 1.25;

        double _startleAge = 99.0, _startleSide;
        double _stumbleAge = 99.0, _stumbleSide = 1.0;
        double _shock, _woundSide;
        double _reelAge = 99.0, _reelSize;
        double _exertion;
        double _scramble, _reach, _teeter, _lookWeight;
        double _lookYawTo, _lookPitchTo;
        Spring _stumpSwing;

        /// <summary>
        /// Startled: something was knocked out of its hands. The direction is where the thing went, in the
        /// character's own space (x right, z ahead); the body flinches away from it and the head goes after it.
        /// </summary>
        public void Startle(double towardX, double towardZ)
        {
            _startleAge = 0.0;
            _startleSide = towardX > 0.05 ? 1.0 : towardX < -0.05 ? -1.0 : 0.0;
            _hitPitch.V += (towardZ >= 0 ? -60.0 : 60.0);
        }

        /// <summary>
        /// A part came off. Side is BodySkeleton.Left or Right; severity 1 for a whole limb, less for a hand or a
        /// foot. For a few seconds the body curls round the wound and moves like it hurts. An arm also spins the
        /// trunk back on that side and doubles it over for about a second, and the head turns to look at the
        /// stump (a leg has its own lurch, see Stumble).
        /// </summary>
        public void Wounded(int side, double severity, bool arm = false)
        {
            _shock = Math.Max(_shock, Clamp01(severity));
            _woundSide = side == BodySkeleton.Left ? -1.0 : 1.0;
            if (arm)
            {
                _reelAge = 0.0;
                _reelSize = Clamp01(severity);
            }
        }

        /// <summary>How much of the reel from losing an arm is showing: in at once, gone over about a second.</summary>
        double ReelAmount()
        {
            double a = _reelAge;
            if (a >= 2.5) return 0.0;
            return _reelSize * (a < 0.07 ? a / 0.07 : Math.Exp(-(a - 0.07) / 0.45));
        }

        /// <summary>How much the head is turned to the stump: after the first jolt, held for most of a second, then back.</summary>
        double GlanceAmount()
        {
            double a = _reelAge;
            if (a >= 1.7) return 0.0;
            double up = Clamp01((a - 0.08) / 0.2), down = Clamp01((1.7 - a) / 0.6);
            return _reelSize * Smooth(Math.Min(up, down));
        }

        /// <summary>Loses its footing for a second: pitches forward, arms flung out, then catches itself. Side is which way it lurches (-1 left, 1 right).</summary>
        public void Stumble(double side)
        {
            if (_stumbleAge < StumbleTime * 0.6) return;
            _stumbleAge = 0.0;
            _stumbleSide = side < 0 ? -1.0 : 1.0;
        }

        /// <summary>True while it is stumbling and should hardly be moved forward.</summary>
        public bool Stumbling => _stumbleAge < StumbleTime * 0.7;
        /// <summary>0 to 1, how shaken it is by its last wound.</summary>
        public double Shock => _shock;
        /// <summary>0 to 1, how out of breath it is.</summary>
        public double Exertion => _exertion;

        /// <summary>How much of the startle is showing, 0 to 1: up in a tenth of a second, held, then gone by 0.7 s.</summary>
        double StartleAmount()
        {
            double t = _startleAge;
            if (t >= StartleTime) return 0.0;
            if (t < 0.08) return Smooth(t / 0.08);
            if (t < 0.25) return 1.0;
            return 1.0 - Smooth((t - 0.25) / (StartleTime - 0.25));
        }

        /// <summary>How much of the stumble is showing: a fast lurch (a quarter of a second), then a slower recovery.</summary>
        double StumbleAmount()
        {
            double t = _stumbleAge;
            if (t >= StumbleTime) return 0.0;
            if (t < 0.22) return Smooth(t / 0.22);
            return 1.0 - Smooth((t - 0.22) / (StumbleTime - 0.22));
        }

        /// <summary>
        /// The head's turn toward the look point, as yaw and pitch in the character's space (pitch positive
        /// looks down). Eyes are taken to be just above the neck.
        /// </summary>
        static void LookAngles(in BodyAnimInput input, double eyeY, double previousYaw, out double yaw, out double pitch)
        {
            double flat = Math.Sqrt(input.LookX * input.LookX + input.LookZ * input.LookZ);
            yaw = Clamp(Math.Atan2(input.LookX, Math.Max(-0.2, input.LookZ) + 1e-6) / Deg, -80.0, 80.0);
            // Something nearly straight behind: keep looking back over the same shoulder rather than whipping
            // the head from one to the other as it crosses the middle.
            if (input.LookZ < 0.0 && Math.Abs(input.LookX) < 0.35 * -input.LookZ && Math.Abs(previousYaw) > 20.0)
                yaw = Math.Sign(previousYaw) * Math.Abs(yaw);
            pitch = Clamp(Math.Atan2(eyeY - input.LookY, Math.Max(0.15, flat)) / Deg, -35.0, 70.0);
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
            double wanted = Math.Max(0.0, input.ForwardSpeed) / _size;
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
            StepReactions(dt, input);

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
                // Losing a leg from under it: it lurches over the leg it has left before it finds the hop.
                if (_state == MovementState.Intact && input.State == MovementState.OneLeg)
                {
                    _blendTime = 0.3;
                    _stumbleAge = 99.0;
                    Stumble(input.StandingLeg == BodySkeleton.Left ? 1.0 : -1.0);
                }
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
            double walk = Math.Max(0.1, input.WalkSpeed / _size);
            double sprint = Math.Max(walk + 0.1, input.SprintSpeed / _size);
            double v = _speed;
            double turnAmount = Clamp01((Math.Abs(_turn) - 20.0) / 50.0);
            // Shuffle round fast enough that a planted foot never twists more than about 30 degrees each way.
            double turning = Math.Min(2.4, Math.Abs(_turn) * 0.62 / 60.0);
            // Scrambling takes quick, short steps: the same ground covered in more of them.
            double frantic = 1.0 + 0.22 * _scramble;
            double stride = StrideLength(v, RunAmount(v, walk, sprint)) * _strideScale * _style.Stride / frantic;
            return Math.Max(v / stride, turning * turnAmount);
        }

        /// <summary>Hip joint to ankle, straight: the length every stride is measured in.</summary>
        const double LegLength = BodySkeleton.ThighLength + BodySkeleton.ShinLength;
        /// <summary>Where the foot rolls over: the ball of the foot ahead of the ankle, and the heel behind it.</summary>
        const double BallAhead = 0.14, HeelBehind = 0.07;

        /// <summary>
        /// How long one stride (two steps) is at a speed, in metres at the skeleton's size. Measured from CMU walks
        /// and runs (02_01, 07_01, 08_01, 16_15, 09_01, 35_17), in leg lengths: walking, 1.25 times the square
        /// root of the speed in leg lengths a second (1.5 to 1.8 legs a stride at an ordinary to brisk walk);
        /// running, half a leg plus 0.6 of the speed (2.7 to 2.9 legs at a jog). Below the stride a person really
        /// takes, the steps look quick, short and mechanical.
        /// </summary>
        static double StrideLength(double v, double run)
        {
            double legs = Math.Max(0.0, v) / LegLength;
            double walking = 1.25 * Math.Sqrt(legs);
            // Past a fast run the stride stops growing much and the legs turn over faster instead.
            double running = Math.Min(0.5 + 0.6 * legs, 3.4 + 0.15 * legs);
            // Never so short that a crawl of a walk ticks over like a sewing machine.
            return Math.Max(0.35, Lerp(walking, running, run)) * LegLength;
        }

        /// <summary>
        /// 0 walking to 1 running: from where the speed sits between the body's own walk and sprint, but never a
        /// walk past what legs can walk (people break into a run at about 2.6 to 3 leg lengths a second).
        /// </summary>
        static double RunAmount(double v, double walk, double sprint) =>
            Math.Max(Smooth((v - walk * 1.2) / Math.Max(0.1, sprint * 0.95 - walk * 1.2)), Smooth((v / LegLength - 2.6) / 1.0));

        /// <summary>
        /// How much of a stride a foot spends on the ground: 61% walking; running, about 35% at a jog down to
        /// about a fifth at a flat-out sprint (CMU runs, and the usual contact times of 0.2 s at 4 to 5 m/s
        /// and 0.11 s at 8 m/s).
        /// </summary>
        static double Duty(double v, double run)
        {
            double running = Clamp(0.42 - 0.02 * Math.Max(0.0, v) / LegLength, 0.2, 0.36);
            return Lerp(0.61, running, run);
        }

        void BuildStanding(BodyPose pose, in BodyAnimInput input)
        {
            BodyStyle style = _style;
            double walk = Math.Max(0.1, input.WalkSpeed / _size);
            double sprint = Math.Max(walk + 0.1, input.SprintSpeed / _size);
            double v = _speed;
            double moveAmount = Clamp01(v / (walk * 0.5));
            double run = RunAmount(v, walk, sprint);
            double turnRadians = _turn * Deg;
            double turnAmount = Clamp01((Math.Abs(_turn) - 20.0) / 50.0);
            double activity = Math.Max(moveAmount, turnAmount);
            double still = 1.0 - activity;
            double aimStill = _aim * (1.0 - moveAmount);
            double gunSign = input.GunSide == BodySkeleton.Left ? -1.0 : 1.0;

            double duty = Duty(v, run);
            double stanceTime = duty / Math.Max(_cadence, 0.4);
            // A runner lands nearly under itself and pushes off far behind; a walker's step is about even.
            double centre = Lerp(0.42, 0.32, run);
            double g = _gait;

            // ---- pelvis over the feet
            double breath = Math.Sin(TwoPi * (_time + _timeOffset) / 3.7);
            double weight = Math.Sin(TwoPi * (_time + _timeOffset) / 7.3) + 0.35 * Math.Sin(TwoPi * (_time + _timeOffset) / 3.1);
            double idle = still * (1.0 - 0.7 * _aim) * style.Idle;

            double mid = g - duty * centre;                    // 0 when the left foot is right under the body
            double perStep = Math.Cos(2.0 * TwoPi * mid);      // 1 at the middle of either foot's time on the ground
            double perStride = Math.Cos(TwoPi * mid);          // 1 over the left foot, -1 over the right
            // Most of a walk's rise and fall comes from the legs themselves (a straight leg vaulting over its foot,
            // see the sink below); this adds a little. A run is lowest in the middle of each landing.
            double bob = (0.012 * (1.0 - run) - 0.040 * run) * moveAmount * perStep * style.Bob;
            // Taking the weight: just after each heel strike the knee gives about 15 degrees and straightens again.
            double load = 0.0;
            for (int f = 0; f < 2; f++)
            {
                double k = Frac(g + 0.5 * f) / Math.Max(duty, 0.05);
                if (k < 0.45) load = Math.Max(load, Math.Sin(Math.PI * k / 0.45));
            }
            bob -= 0.014 * load * moveAmount * (1.0 - run) * style.Bob;
            // Walking on nearly straight legs; only a run carries the hips a little lower.
            double drop = 0.004 + 0.008 * moveAmount + 0.022 * run + 0.045 * aimStill + 0.02 * turnAmount * (1.0 - moveAmount) + style.Crouch;
            double sway = -(0.017 * moveAmount * (1.0 - 0.6 * run) + 0.018 * turnAmount * (1.0 - moveAmount)) * perStride * style.Sway
                          + 0.030 * weight * idle;
            // The hip over the swinging leg drops about 4 degrees, and the pelvis turns about 7 degrees each way,
            // the leading leg's side forward (CMU: 4 to 7 and 4 to 10).
            double hipRoll = -4.2 * moveAmount * perStride * style.Sway + 2.2 * weight * idle;
            double gaitYaw = (7.0 + 3.0 * run) * moveAmount * Math.Cos(TwoPi * g);
            double stanceYaw = gunSign * 16.0 * aimStill;
            double lean = style.Lean + 2.0 * moveAmount + 9.0 * run + _lean * moveAmount;

            // ---- reactions laid over the gait
            double startle = StartleAmount();
            double stumble = StumbleAmount();
            double reach = _reach;
            double scramble = _scramble * (1.0 - reach);
            double lossLeft = Clamp01(input.ArmLossLeft), lossRight = Clamp01(input.ArmLossRight);
            double lostArm = Math.Max(lossLeft, lossRight);
            // Bent low and driving forward after the gun; bent right over to pick it up; thrown forward by a stumble.
            lean += 20.0 * scramble + 40.0 * reach + 28.0 * stumble;
            drop += 0.05 * scramble + 0.27 * reach + 0.06 * startle + 0.05 * stumble + 0.03 * _shock;
            // Leans into a turn like anyone running round a bend (half of what the bend would really ask for).
            double bank = Clamp(Math.Atan(v * turnRadians / 9.81) / Deg * 0.5, -12.0, 12.0) * moveAmount;
            hipRoll += -bank - _stumbleSide * 12.0 * stumble * Math.Sin(Math.PI * Math.Min(1.0, _stumbleAge / 0.6));

            pose.RootRotation = QuatD.Identity;
            pose.RootPosition = new Vec3d(sway + _pushX.X, bob - drop - Math.Max(0.0, _dip.X),
                                          _pushZ.X - 0.03 * run - 0.05 * startle + 0.05 * stumble + 0.04 * reach);
            QuatD hips = Qy(gaitYaw + stanceYaw) * Qx(lean) * Qz(hipRoll);
            pose.Local[(int)BodyBone.Hips] = hips;
            Vec3d hipsPosition = pose.RootPosition + new Vec3d(0, BodySkeleton.HipsY, 0);

            // ---- feet on the ground, legs bent to reach them
            double halfWidth = (Lerp(Lerp(0.125, 0.098, moveAmount), 0.072, run) + 0.03 * aimStill) * style.Stance;
            // Heel down, roll over the foot, push off the ball, and the ankle carried high early in the swing so the
            // knee folds (about 60 degrees walking, more running), then the leg reaches out straight for the heel.
            double kick = 0.025 * turnAmount * (1.0 - moveAmount) + (0.07 + 0.22 * run) * moveAmount;
            double strikeToeUp = (18.0 + 4.0 * run) * moveAmount;
            double rollOff = (52.0 + 20.0 * run) * moveAmount;
            Vec3d ankleLeft = Vec3d.Zero, ankleRight = Vec3d.Zero;
            QuatD footLeft = QuatD.Identity, footRight = QuatD.Identity;
            // How much each leg may pull the hips down to reach its foot: a planted leg fully, a swinging leg only
            // as its heel comes down (otherwise the body would dip for a leg that is still in the air).
            double holdLeft = 1.0, holdRight = 1.0;
            for (int s = 0; s < 2; s++)
            {
                double sx = s == BodySkeleton.Left ? -1.0 : 1.0;
                double u = Frac(g + (s == BodySkeleton.Right ? 0.5 : 0.0));
                double hold = u < duty ? 1.0 : Smooth(((u - duty) / (1.0 - duty) - 0.7) / 0.3);
                if (s == BodySkeleton.Left) holdLeft = hold; else holdRight = hold;
                // Standing still, one foot sits a little ahead of the other; aiming, the gun-side foot drops back.
                double zBias = (s == BodySkeleton.Left ? _stagger : -_stagger) * (1.0 - moveAmount) * (1.0 - _aim)
                               + (sx == gunSign ? -0.11 : 0.08) * aimStill;
                double restYaw = sx == gunSign ? gunSign * 32.0 * aimStill : 0.0;

                Vec3d ankle;
                double yaw, pitch;
                if (u < duty)
                {
                    double k = u / duty;
                    Vec3d ground = StancePoint(sx * halfWidth, zBias, (k - centre) * stanceTime, v, turnRadians, out yaw);
                    // Heel strike, flat by a sixth of the way through, the heel coming up from 60% of the way and
                    // quickest at the end, rolling over the ball (the knee stays nearly straight until the other
                    // heel is down).
                    double toeUp = strikeToeUp * (1.0 - Smooth(k / 0.16));
                    double toeDown = rollOff * Math.Pow(Clamp01((k - 0.6) / 0.4), 1.6);
                    pitch = toeDown - toeUp;
                    ankle = ground + Rolled(yaw, toeDown, toeUp);
                }
                else
                {
                    double k = (u - duty) / (1.0 - duty);
                    Vec3d from = StancePoint(sx * halfWidth, zBias, (1.0 - centre) * stanceTime, v, turnRadians, out double yawFrom)
                                 + Rolled(yawFrom, rollOff, 0.0);
                    Vec3d to = StancePoint(sx * halfWidth, zBias, -centre * stanceTime, v, turnRadians, out double yawTo)
                               + Rolled(yawTo, 0.0, strikeToeUp);
                    // The swing carries on from the push-off and lands still: the ankle leaves moving the way the
                    // rolling foot was moving (back with the ground, and rising), and arrives moving back at the
                    // ground's speed, so neither the toe-off nor the heel strike jerks or slides.
                    double perSwing = (1.0 - duty) / Math.Max(duty, 0.05);
                    double rollRate = 4.0 * rollOff * Deg;          // how fast the foot was rocking at the end, per stance
                    double ra = rollOff * Deg;
                    var leave = new Vec3d(0.0, (BallAhead * Math.Cos(ra) - BodySkeleton.AnkleY * Math.Sin(ra)) * rollRate,
                                          -v * stanceTime + (BallAhead * Math.Sin(ra) + BodySkeleton.AnkleY * Math.Cos(ra)) * rollRate) * perSwing;
                    var land = new Vec3d(0.0, 0.0, -v * stanceTime) * perSwing;
                    // A runner's foot keeps travelling back after it leaves and is still behind it at the top of the
                    // kick; it comes through late and fast.
                    leave = leave + new Vec3d(0.0, 0.0, -1.2 * run * Math.Abs(to.Z - from.Z));
                    double k2 = k * k, k3 = k2 * k;
                    ankle = from * (2.0 * k3 - 3.0 * k2 + 1.0) + leave * (k3 - 2.0 * k2 + k)
                            + to * (3.0 * k2 - 2.0 * k3) + land * (k3 - k2);
                    double e = Smooth(k);
                    yaw = Lerp(yawFrom, yawTo, e);
                    // The heel kicked up behind early, down to the heel's height by the end; smooth at both ends.
                    // Running, the kick comes sooner, while the foot is still behind: the heel folds up toward the
                    // seat rather than the knee coming up in front.
                    double late = 1.0 - k;
                    double walkLift = 16.0 * k2 * late * late * (1.0 + 0.6 * late) / 1.3;
                    double runLift = 45.56 * k2 * late * late * late * late;
                    ankle = ankle + new Vec3d(0.0, kick * Lerp(walkLift, runLift, run), 0.0);
                    // The toes go on pointing down a little after they leave (the push carries on), then come up for
                    // the next heel strike.
                    double kp = Clamp01(k / 0.7);
                    double pitchRate = rollRate / Deg * perSwing * 0.7;
                    pitch = rollOff * (2.0 * kp * kp * kp - 3.0 * kp * kp + 1.0) + Math.Min(pitchRate, 140.0) * (kp * kp * kp - 2.0 * kp * kp + kp)
                            - strikeToeUp * (3.0 * kp * kp - 2.0 * kp * kp * kp) + 14.0 * run * Math.Sin(Math.PI * k);
                }

                ankle = new Vec3d(ankle.X, BodySkeleton.AnkleY + ankle.Y, ankle.Z);
                if (s == BodySkeleton.Left) { ankleLeft = ankle; footLeft = Qy(yaw + restYaw) * Qx(pitch); }
                else { ankleRight = ankle; footRight = Qy(yaw + restYaw) * Qx(pitch); }
            }

            // A long stride is further than a straight leg can reach from a high pelvis, so the pelvis sinks just
            // enough for both legs to reach their feet: the dip at each footfall comes from the legs themselves.
            Vec3d jointLeft = hipsPosition + hips.Rotate(BodySkeleton.RestOffset(BodyBone.LeftThigh));
            Vec3d jointRight = hipsPosition + hips.Rotate(BodySkeleton.RestOffset(BodyBone.RightThigh));
            double sink = Math.Max(SinkToReach(jointLeft, ankleLeft) * holdLeft, SinkToReach(jointRight, ankleRight) * holdRight);
            if (sink > 0.0)
            {
                var down = new Vec3d(0.0, sink, 0.0);
                pose.RootPosition = pose.RootPosition - down;
                jointLeft = jointLeft - down;
                jointRight = jointRight - down;
            }
            // A leg in the air never snaps straight: near full stretch it eases out, so the knee straightens at
            // the end of the swing as a person's does rather than in one frame.
            ankleLeft = SoftReach(jointLeft, ankleLeft, 1.0 - holdLeft);
            ankleRight = SoftReach(jointRight, ankleRight, 1.0 - holdRight);
            SolveLeg(pose, BodySkeleton.Left, hips, jointLeft, ankleLeft, footLeft);
            SolveLeg(pose, BodySkeleton.Right, hips, jointRight, ankleRight, footRight);


            // ---- upper body
            double breathSize = 1.0 + 2.0 * _exertion + 1.5 * _shock;
            // Losing an arm: the hit spins that shoulder back and doubles the trunk over, then the head turns to the stump.
            double reel = ReelAmount(), glance = GlanceAmount() * (1.0 - _lookWeight);
            double spinePitch = 1.1 * breath * breathSize * still * style.Idle + 0.25 * lean + 5.0 * aimStill + style.Hunch
                                + 14.0 * startle + 22.0 * reach + 8.0 * scramble + 12.0 * stumble + 9.0 * _shock + 4.0 * lostArm + 12.0 * reel;
            double spineRoll = -0.8 * hipRoll + Guard(input, lossLeft, lossRight) - _woundSide * 8.0 * reel;
            double counter = 1.0 + 0.3 * lostArm;
            double lookYaw = _lookWeight * _lookYawTo;
            pose.Local[(int)BodyBone.Spine] = Qy(-1.5 * gaitYaw * counter + 0.3 * lookYaw + _woundSide * 26.0 * reel) * Qx(spinePitch) * Qz(spineRoll);
            double look = (1.0 - _aim) * still * Math.Min(1.0, style.Idle) * (1.0 - _lookWeight);
            double headPitch = -(0.9 * lean + 0.6 * spinePitch) + _lookPitch * look + 10.0 * startle + 10.0 * _shock * (1.0 - _lookWeight)
                               + _lookWeight * (_lookPitchTo - 0.1 * lean - 0.4 * spinePitch) + 22.0 * glance;
            pose.Local[(int)BodyBone.Head] = Qy(0.7 * gaitYaw - stanceYaw + _lookYaw * look + 0.7 * lookYaw + _woundSide * 50.0 * glance)
                                             * Qx(Clamp(headPitch, -60.0, 50.0))
                                             * Qz(-0.3 * spineRoll);
            double trunk = lean + spinePitch;

            // The arms swing further the faster it walks, a little more behind than in front; running, the elbows
            // are bent to about 100 degrees and the arms pump mostly behind the body (CMU walks and runs, as above).
            double legsPerSecond = v / LegLength;
            double walkSwing = Clamp(-4.0 + 12.0 * legsPerSecond, 0.0, 26.0) * moveAmount;
            double swingSize = Lerp(walkSwing, 24.0, run) * _armScale * style.ArmSwing;
            double swingBack = Lerp(5.0 * moveAmount, 20.0, run);
            double lag = 0.04;
            for (int s = 0; s < 2; s++)
            {
                double sign = s == BodySkeleton.Left ? 1.0 : -1.0;
                double wave = sign * Math.Cos(TwoPi * (g - lag));      // -1 is this arm fully forward
                double forward = Math.Max(0.0, -wave);
                double down = 79.0 - style.ArmsOut - 3.0 * run + 1.2 * breath * still * style.Idle;
                double swing = swingSize * wave + 2.0 * Math.Sin(TwoPi * (_time + _timeOffset) / 5.1 + s) * still * style.Idle + swingBack;
                double elbow = 14.0 + style.ElbowBend + 6.0 * moveAmount + 78.0 * run + 16.0 * moveAmount * forward;
                // The arm that is left swings wider to balance the one that is not.
                double otherLoss = s == BodySkeleton.Left ? lossRight : lossLeft;
                swing *= 1.0 + 0.35 * otherLoss;
                swing -= 12.0 * scramble;
                elbow += 15.0 * scramble;
                ReactArms(s, input, lossLeft, lossRight, trunk, startle, stumble, reach, scramble, breath, ref down, ref swing, ref elbow);
                SetArm(pose, s, down, swing, elbow);
            }

            AddKnocks(pose, 1.0);
        }

        /// <summary>
        /// How far the trunk leans sideways over a lost arm: a little toward the missing side for the weight
        /// that is gone, and more, for a few seconds, curling round the wound.
        /// </summary>
        double Guard(in BodyAnimInput input, double lossLeft, double lossRight)
        {
            // Qz turns the trunk toward -x, so leaning toward a side means turning by minus that side's sign.
            double weight = 3.5 * lossLeft - 3.5 * lossRight;
            double curl = -_woundSide * 9.0 * _shock;
            return weight + curl;
        }

        /// <summary>
        /// The arm reactions, laid over whatever the arm was doing: a lost arm's stump held in, the other arm
        /// thrown out for balance as the hit spins the body, the startled flinch, the stumble's flailing and the
        /// reach down to the ground. The other hand never reaches across to the wound (Matthew, 15 Nov 2026).
        /// </summary>
        void ReactArms(int s, in BodyAnimInput input, double lossLeft, double lossRight, double trunk, double startle, double stumble,
                       double reach, double scramble, double breath, ref double down, ref double swing, ref double elbow)
        {
            double myLoss = s == BodySkeleton.Left ? lossLeft : lossRight;
            double otherLoss = s == BodySkeleton.Left ? lossRight : lossLeft;
            if (myLoss >= 0.4)
            {
                // A stump is held in tight to the body, twitching while the shock lasts.
                double twitch = Math.Sin(TwoPi * (_time + _timeOffset) * 3.3) * 6.0 * _shock;
                down = Lerp(down, 84.0, 0.8);
                swing = Lerp(swing, -8.0 + twitch, 0.8);
                elbow = Lerp(elbow, 35.0, 0.8);
                return;
            }
            if (otherLoss >= 0.4)
            {
                // The arm that is left is thrown out a little for balance while the hit spins the body.
                double reel = ReelAmount();
                down -= 22.0 * reel;
                elbow += 15.0 * reel;
            }
            if (startle > 0.0)
            {
                // Hands come up and in.
                down -= 35.0 * startle;
                elbow += 50.0 * startle;
                swing -= 25.0 * startle;
            }
            if (stumble > 0.0)
            {
                // Arms flung out and up, windmilling, one beat apart.
                double w = TwoPi * 1.6 * _stumbleAge + (s == BodySkeleton.Left ? 0.0 : Math.PI);
                down = Lerp(down, -20.0 + 35.0 * Math.Sin(w), stumble);
                swing = Lerp(swing, -35.0 * Math.Cos(w), stumble);
                elbow = Lerp(elbow, 20.0, stumble);
            }
            if (reach > 0.0)
            {
                // The reaching hand hangs straight down to the ground ahead; the other braces on the knee.
                bool reaching = s == (input.ReachSide == BodySkeleton.Left ? BodySkeleton.Left : BodySkeleton.Right);
                if (reaching)
                {
                    down = Lerp(down, 84.0, reach);
                    swing = Lerp(swing, -0.95 * trunk - 10.0, reach);
                    elbow = Lerp(elbow, 8.0, reach);
                }
                else
                {
                    down = Lerp(down, 74.0, reach);
                    swing = Lerp(swing, -0.55 * trunk, reach);
                    elbow = Lerp(elbow, 50.0, reach);
                }
            }
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

        /// <summary>
        /// How far the ankle is lifted and carried from where it sits over a flat foot when the foot rocks: up on
        /// the ball by <paramref name="toeDown"/> degrees (heel up, the ball stays put), or back on the heel by
        /// <paramref name="toeUp"/> degrees (toes up, the heel stays put). Turned to the foot's heading.
        /// </summary>
        static Vec3d Rolled(double yawDegrees, double toeDown, double toeUp)
        {
            double h = BodySkeleton.AnkleY;
            double z = 0.0, y = 0.0;
            if (toeDown > 0.0)
            {
                double a = toeDown * Deg;
                z += -BallAhead * Math.Cos(a) + h * Math.Sin(a) + BallAhead;
                y += BallAhead * Math.Sin(a) + h * Math.Cos(a) - h;
            }
            if (toeUp > 0.0)
            {
                double a = toeUp * Deg;
                z += HeelBehind * Math.Cos(a) - h * Math.Sin(a) - HeelBehind;
                y += HeelBehind * Math.Sin(a) + h * Math.Cos(a) - h;
            }
            double yaw = yawDegrees * Deg;
            return new Vec3d(z * Math.Sin(yaw), y, z * Math.Cos(yaw));
        }

        double _lastHopSin;

        /// <summary>
        /// The hobble on one leg. The body rocks forward over the planted foot and the foot swings through under
        /// it in the air; the trunk leans out over the good leg, hardest just after landing; the arms are held
        /// out for balance and flap with each hop; whatever is left of the other leg swings on its hip like a
        /// pendulum. A bad landing (Teeter) holds it on the spot fighting for balance, and a stumble throws it
        /// forward with the arms flung up.
        /// </summary>
        void BuildOneLeg(BodyPose pose, in BodyAnimInput input)
        {
            int standing = input.StandingLeg == BodySkeleton.Left ? BodySkeleton.Left : BodySkeleton.Right;
            int missing = 1 - standing;
            double sx = standing == BodySkeleton.Left ? -1.0 : 1.0;
            double cycle = Frac(input.Phase / TwoPi);
            double sin = Math.Sin(input.Phase);
            bool hopping = input.Moving && _teeter < 0.5;
            double air = hopping ? Math.Max(0.0, sin) : 0.0;        // 1 at the top of the hop
            double crouch = hopping ? Math.Max(0.0, -sin) : 0.0;    // winding up on the ground
            double teeter = _teeter;
            double wobbleRate = 1.0 + 1.4 * teeter;
            double wobble = Math.Sin((_time + _timeOffset) * 2.3 * wobbleRate) + 0.5 * Math.Sin((_time + _timeOffset) * 3.7 * wobbleRate);
            double still = hopping ? 0.0 : 1.0;
            double startle = StartleAmount();
            double stumble = StumbleAmount();
            double reach = _reach;
            double scramble = _scramble * (1.0 - reach);
            double lossLeft = Clamp01(input.ArmLossLeft), lossRight = Clamp01(input.ArmLossRight);
            double stump = Clamp01(input.StumpLength);

            // Landing: the stump is thrown forward and swings back; the body drops onto the knee.
            // (Negative swings it forward.)
            if (hopping && _lastHopSin > 0.0 && sin <= 0.0) _stumpSwing.V -= 140.0 + 60.0 * scramble;
            if (hopping && _lastHopSin <= 0.0 && sin > 0.0) _stumpSwing.V += 90.0;
            _lastHopSin = hopping ? sin : 0.0;

            double rock;
            if (!hopping) rock = 0.0;
            else if (cycle < 0.5) rock = Lerp(0.07, -0.07, cycle * 2.0);
            else rock = Lerp(-0.07, 0.07, Smooth((cycle - 0.5) * 2.0));

            // A hop lifts the hips about a sixth of a leg (measured: 0.14 m on this skeleton).
            double height = air * 0.15 - crouch * 0.11 - 0.025 - Math.Max(0.0, _dip.X)
                            - 0.06 * stumble - 0.18 * reach - 0.03 * _shock - 0.02 * teeter;
            // Out over the good leg: more with less of the other leg to balance it, most just after landing,
            // and swaying wide while it fights for balance.
            double over = 7.0 + 3.0 * (1.0 - stump) + 5.0 * crouch + 3.0 * scramble;
            double roll = sx * -over + wobble * (1.6 * still + 5.0 * teeter)
                          - _stumbleSide * 10.0 * stumble * Math.Sin(Math.PI * Math.Min(1.0, _stumbleAge / 0.6));
            // Forward: about 20 degrees standing on one leg (measured), more mid-hop, more in a hurry or a stumble.
            double lean = 12.0 + 9.0 * air - 5.0 * crouch + 10.0 * scramble + 28.0 * stumble + 45.0 * reach + 6.0 * _shock;
            pose.RootPosition = new Vec3d(sx * 0.035 + _pushX.X + wobble * (0.008 * still + 0.02 * teeter), height,
                                          rock + _pushZ.X - 0.04 * startle + 0.04 * stumble + 0.05 * reach);
            QuatD hips = Qx(lean) * Qz(roll);
            pose.Local[(int)BodyBone.Hips] = hips;
            Vec3d hipsPosition = pose.RootPosition + new Vec3d(0, BodySkeleton.HipsY, 0);

            Vec3d ankle = new Vec3d(sx * 0.045, BodySkeleton.AnkleY + air * 0.20, 0.0);
            Vec3d joint = hipsPosition + hips.Rotate(BodySkeleton.RestOffset(BodySkeleton.Thigh(standing)));
            SolveLeg(pose, standing, hips, joint, ankle, Qx(26.0 * air));

            // Whatever is left of the other leg hangs forward with the knee bent (about 95 degrees, measured)
            // and swings with the hops.
            double thighAngle = -32.0 + 14.0 * Math.Sin(input.Phase + 1.0) * (hopping ? 1.0 : 0.0) + _stumpSwing.X
                                + 6.0 * wobble * still - 20.0 * reach;
            pose.Local[(int)BodySkeleton.Thigh(missing)] = Qx(Clamp(thighAngle, -80.0, 25.0)) * Qz(sx * (10.0 + 6.0 * teeter));
            pose.Local[(int)BodySkeleton.Shin(missing)] = Qx(70.0 + 25.0 * crouch - 0.25 * _stumpSwing.X);

            double spinePitch = -4.0 * air + 8.0 * crouch + 10.0 * stumble + 20.0 * reach + 8.0 * _shock + 6.0 * scramble + 12.0 * startle;
            double spineRoll = -0.6 * roll + Guard(input, lossLeft, lossRight);
            double lookYaw = _lookWeight * _lookYawTo;
            pose.Local[(int)BodyBone.Spine] = Qy(0.3 * lookYaw) * Qx(spinePitch) * Qz(spineRoll);
            double headPitch = -(lean * 0.8) + 10.0 * startle + _lookWeight * (_lookPitchTo - 0.2 * lean - 0.6 * spinePitch);
            pose.Local[(int)BodyBone.Head] = Qy(0.7 * lookYaw) * Qx(Clamp(headPitch, -60.0, 50.0)) * Qz(-0.3 * roll);
            double trunk = lean + spinePitch;

            // Arms out for balance (about 70 degrees from the sides, measured), flapping a beat apart with each
            // hop, flung higher and wider while it teeters.
            double breath = Math.Sin(TwoPi * (_time + _timeOffset) / Lerp(3.7, 1.6, Math.Max(_exertion, _shock)));
            for (int s = 0; s < 2; s++)
            {
                double beat = Math.Sin(input.Phase + (s == standing ? 0.0 : 0.9));
                double flail = Math.Sin(TwoPi * 1.3 * (_time + _timeOffset) + s * 2.1);
                double down = 44.0 - 22.0 * air + 12.0 * crouch + 6.0 * beat * (hopping ? 1.0 : 0.0)
                              - 4.0 * wobble * still * (s == standing ? 1.0 : -1.0) - teeter * (14.0 + 18.0 * flail);
                double swing = -8.0 + 14.0 * crouch - 10.0 * air - 18.0 * teeter * Math.Cos(TwoPi * 1.3 * (_time + _timeOffset) + s * 2.1);
                double elbow = 24.0 + 26.0 * crouch - 8.0 * teeter;
                // The one arm left does the balancing for both.
                double otherLoss = s == BodySkeleton.Left ? lossRight : lossLeft;
                down -= 12.0 * otherLoss;
                swing -= 18.0 * scramble;
                ReactArms(s, input, lossLeft, lossRight, trunk, startle, stumble, reach, scramble, breath, ref down, ref swing, ref elbow);
                SetArm(pose, s, down, swing, elbow);
            }

            AddKnocks(pose, 1.0);
        }

        /// <summary>Lays the figure face down along its forward direction, its chest resting where the character is.</summary>
        void SetProne(BodyPose pose, double yawDegrees, double rollDegrees, double pitchBumpDegrees)
        {
            pose.RootRotation = Qy(yawDegrees) * Qx(86.0 - pitchBumpDegrees) * Qz(rollDegrees);
            // Whatever the tilt, the middle of the torso stays put, so humping up never lifts the body off the ground.
            var torsoCentre = new Vec3d(0.0, BodySkeleton.SpineY + BodySkeleton.TorsoCentreAboveSpine, 0.0);
            pose.RootPosition = new Vec3d(0.0, ProneHeight * Style.Girth + 0.004 * pitchBumpDegrees, 0.0) - pose.RootRotation.Rotate(torsoCentre);
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
                if (_reach > 0.05 && s == (input.ReachSide == BodySkeleton.Left ? BodySkeleton.Left : BodySkeleton.Right))
                {
                    // Stretching out along the ground for the gun.
                    SetArm(pose, s, Lerp(-42.0, -82.0, _reach), Lerp(-30.0, -8.0, _reach), Lerp(62.0, 6.0, _reach));
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
            pose.Local[(int)BodyBone.Head] = Qy(-9.0 * sin + _lookYaw * (input.Moving ? 0.0 : 0.6) * (1.0 - _lookWeight) + 0.8 * _lookWeight * _lookYawTo)
                                             * Qx(-40.0 + 7.0 * heave - 12.0 * StartleAmount());
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
            reach = Clamp(reach, Math.Abs(thigh - shin) + 0.02, thigh + shin - 0.002);
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

        /// <summary>
        /// Brings an ankle target that is nearly out of reach a little closer, easing in over the last 4 cm, so a
        /// leg reaching for it straightens smoothly instead of locking straight in one frame. Amount 0 changes
        /// nothing (a planted foot must be reached exactly), 1 is the whole easing.
        /// </summary>
        static Vec3d SoftReach(Vec3d joint, Vec3d ankle, double amount)
        {
            if (amount <= 0.0) return ankle;
            const double longest = BodySkeleton.ThighLength + BodySkeleton.ShinLength - 0.004;
            const double width = 0.04;
            Vec3d d = ankle - joint;
            double m = d.Magnitude;
            if (m <= longest - width || m < 1e-6) return ankle;
            double soft = longest - width * Math.Exp(-(m - (longest - width)) / width);
            return joint + d * (Lerp(m, Math.Min(m, soft), Clamp01(amount)) / m);
        }

        /// <summary>How far a hip must come down for a nearly straight leg to reach from it to an ankle (0 if it already can).</summary>
        static double SinkToReach(Vec3d joint, Vec3d ankle)
        {
            const double longest = BodySkeleton.ThighLength + BodySkeleton.ShinLength - 0.007;
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

        void StepReactions(double dt, in BodyAnimInput input)
        {
            _startleAge += dt;
            _stumbleAge += dt;
            _reelAge += dt;
            // Shock fades over a few seconds; it never quite leaves a body that has lost an arm (see Guard).
            _shock *= Math.Exp(-dt / 2.4);
            if (_shock < 1e-3) _shock = 0.0;

            double quick = 1.0 - Math.Exp(-10.0 * dt);
            _scramble += (Clamp01(input.Scramble) - _scramble) * quick;
            // Bending down is quick (about a third of a second, half the calm pick-up: it is in a hurry); the
            // smoothing gives the ease in and out.
            _reach += (Clamp01(input.Reach) - _reach) * (1.0 - Math.Exp(-9.0 * dt));
            _teeter += (Clamp01(input.Teeter) - _teeter) * (1.0 - Math.Exp(-8.0 * dt));
            _lookWeight += ((input.HasLook ? 1.0 : 0.0) - _lookWeight) * (1.0 - Math.Exp(-7.0 * dt));
            if (input.HasLook)
            {
                LookAngles(input, BodySkeleton.NeckY + 0.12, _lookYawTo, out double yaw, out double pitch);
                // The head snaps toward what it wants to see (faster while startled).
                double k = 1.0 - Math.Exp(-(StartleAmount() > 0.0 ? 16.0 : 9.0) * dt);
                _lookYawTo += (yaw - _lookYawTo) * k;
                _lookPitchTo += (pitch - _lookPitchTo) * k;
            }

            // Out of breath after running, scrambling or hopping; it comes back slowly.
            double effort = input.State == MovementState.OneLeg && input.Moving ? 0.8
                            : Math.Max(Clamp01((_speed - input.WalkSpeed / _size) / Math.Max(0.1, (input.SprintSpeed - input.WalkSpeed) / _size)), _scramble);
            _exertion += effort > _exertion ? (effort - _exertion) * dt / 4.0 : -_exertion * dt / 9.0;
            _exertion = Clamp01(_exertion);

            // What is left of a lost leg swings on its hip like a pendulum: it lags the hop and overshoots.
            _stumpSwing.Step(dt, 2.1, 0.22);
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
