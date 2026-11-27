using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public enum HunterMode
    {
        /// <summary>On its legs: wandering, weaving in, running a loop out and back after a strike.</summary>
        Run,
        /// <summary>The front of its body lifts off the ground, fangs wide: the warning.</summary>
        Rear,
        /// <summary>The head whips forward and down.</summary>
        Strike,
        /// <summary>Its head lies where it landed for a moment: the time to shoot it.</summary>
        Recover,
        /// <summary>It has just been cut in two: it thrashes on the spot.</summary>
        Writhe,
    }

    /// <summary>How the Cave hunter behaves: every number a designer may want to change. On the CreatureConfig asset.</summary>
    [Serializable]
    public sealed class HunterRules
    {
        /// <summary>Metres a second: quicker than the player walks, slower than they sprint (8).</summary>
        public float pace = 5.6f;
        public float wanderPace = 1.1f;
        /// <summary>Radians a second, whole. With its pace this is the tightest circle it can run (about 2.3 m).</summary>
        public float turnRate = 2.4f;
        /// <summary>Cut right down it turns this many times quicker (in between, in proportion).</summary>
        public float shortTurn = 1.6f;
        /// <summary>With someone inside its reach it all but stops and swings its head round this many times quicker, so it cannot be out-circled.</summary>
        public float closeTurn = 2.6f;
        public float sightRange = 40f;
        /// <summary>Further off than this it weaves from side to side as it comes.</summary>
        public float weaveBeyond = 5f;
        /// <summary>Radians a second, and seconds for one whole weave.</summary>
        public float weave = 0.9f;
        public float weaveSeconds = 1.6f;
        /// <summary>Seconds it stands reared before it strikes: the warning.</summary>
        public float rearSeconds = 0.75f;
        public float strikeSeconds = 0.2f;
        /// <summary>Seconds its head lies still after a strike, hit or miss.</summary>
        public float recoverSeconds = 0.8f;
        /// <summary>Seconds it runs out and round before it comes in again.</summary>
        public float loopSeconds = 1.8f;
        public float biteDamage = 22f;
        /// <summary>How near the head must land to bite (a person is about 0.4 m across).</summary>
        public float biteRadius = 0.8f;
        /// <summary>Seconds it thrashes after being cut in two.</summary>
        public float writheSeconds = 0.9f;
        /// <summary>Seconds after a thrash in which another cut does not stop it again.</summary>
        public float steadySeconds = 2.5f;
        public float wanderRadius = 18f;
        public HunterHp hp = HunterHp.Default;
        /// <summary>Seconds a loose piece lies before it fades.</summary>
        public float pieceLife = 30f;
        /// <summary>Shared with the other creatures' loose pieces.</summary>
        public int maxLoosePieces = 34;
    }

    public struct HunterSenses
    {
        public bool SeesPlayer;
        /// <summary>The player's feet in creature space (x right, z ahead; the origin is under its head).</summary>
        public Vec3d Player;
        public Vec3d Home;
        /// <summary>Something solid is in the way of the last move.</summary>
        public bool Blocked;
    }

    public struct HunterOrders
    {
        /// <summary>Creature space, metres a second. It never backs up: a long body cannot.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        public HunterMode Mode;
        /// <summary>How far through the mode it is, 0 to 1 (0 while running).</summary>
        public double T;
        /// <summary>True on the one step the head lands.</summary>
        public bool Landed;
    }

    /// <summary>
    /// What a Cave hunter decides, with no Unity types. It weaves in on its legs, and within reach it rears (the
    /// warning), follows the player until <see cref="LockAt"/> of the way through, then whips its head down on where
    /// they stood: a step back or aside after that is a miss (constitution rule 3). Its head then lies still for a
    /// moment, and it runs a loop out and round before it comes in again (rule 8). How far it reaches goes with how
    /// much of it is left (<see cref="Reach"/>), and cutting it in two stops whatever it was doing while it thrashes
    /// (<see cref="Stagger"/>): a cut during the warning is a strike that never comes.
    /// </summary>
    public sealed class HunterBrain
    {
        /// <summary>The share of the rear after which it no longer follows the player.</summary>
        public const double LockAt = 0.65;
        /// <summary>Where the bite is, ahead of its origin, once the head has landed.</summary>
        public const double BiteAhead = 0.3;

        readonly HunterRules _rules;
        readonly SeededRandom _random;
        double _t, _alert, _lunge, _loop, _loopSide = 1.0, _weave, _wanderFor, _wanderTurn, _steady;

        public HunterBrain(HunterRules rules, int seed)
        {
            _rules = rules ?? new HunterRules();
            _random = new SeededRandom(seed);
            _weave = _random.NextDouble() * 6.0;
        }

        public HunterMode Mode { get; private set; } = HunterMode.Run;
        public bool Alerted => _alert > 0.0;
        /// <summary>Running out and round after a strike or a cut.</summary>
        public bool Looping => Mode == HunterMode.Run && _loop > 0.0;

        public void Alert(double seconds = 8.0) => _alert = Math.Max(_alert, seconds);

        /// <summary>From how far it strikes: a little less for every segment cut away (2.2 m whole, 1 m cut right down).</summary>
        public static double Reach(HunterBody body) => 1.0 + 1.2 * body.LengthShare;

        /// <summary>
        /// It has been cut in two: whatever it was doing stops and it thrashes. Not twice running: while it thrashes
        /// and for a while after, another cut shortens it but does not stop it, so it cannot be held still for good.
        /// </summary>
        public bool Stagger()
        {
            if (Mode == HunterMode.Writhe || _steady > 0.0) return false;
            Mode = HunterMode.Writhe;
            _t = 0.0;
            return true;
        }

        /// <summary>Is someone whose feet are here (creature space, after the head has landed) bitten?</summary>
        public bool BiteHits(Vec3d playerFeet)
        {
            double z = playerFeet.Z - BiteAhead;
            return Math.Sqrt(playerFeet.X * playerFeet.X + z * z) < _rules.biteRadius && playerFeet.Y < 1.2 && playerFeet.Y > -1.5;
        }

        void Enter(HunterMode mode)
        {
            Mode = mode;
            _t = 0.0;
        }

        public HunterOrders Think(HunterBody body, in HunterSenses senses, double dt)
        {
            var orders = new HunterOrders { Mode = Mode };
            if (!body.Alive) return orders;
            _alert = Math.Max(0.0, _alert - dt);
            double pace = _rules.pace * body.SpeedShare;
            _steady = Math.Max(0.0, _steady - dt);
            double turn = _rules.turnRate * (1.0 + (_rules.shortTurn - 1.0) * (1.0 - body.LengthShare));
            var toPlayer = new Vec3d(senses.Player.X, 0, senses.Player.Z);
            double distance = toPlayer.Magnitude;
            double bearing = Math.Atan2(toPlayer.X, toPlayer.Z);
            bool hunting = senses.SeesPlayer || Alerted;
            double reach = Reach(body);

            switch (Mode)
            {
                case HunterMode.Rear:
                    _t += dt / Math.Max(0.2, _rules.rearSeconds);
                    if (_t < LockAt && hunting)
                    {
                        orders.TurnRate = Toward(bearing, turn * 1.3);
                        _lunge = Math.Max(0.2, Math.Min(reach + 0.4, distance - (BiteAhead - 0.05)));
                    }
                    if (_t >= 1.0) Enter(HunterMode.Strike);
                    break;

                case HunterMode.Strike:
                {
                    double step = dt / Math.Max(0.05, _rules.strikeSeconds);
                    // The last step is cut short, so it lunges exactly as far as it aimed.
                    double share = Math.Min(step, 1.0 - _t);
                    if (!senses.Blocked && dt > 0.0) orders.Velocity = new Vec3d(0, 0, _lunge * share / dt);
                    _t += step;
                    if (_t >= 1.0)
                    {
                        orders.Landed = true;
                        Enter(HunterMode.Recover);
                    }
                    break;
                }

                case HunterMode.Recover:
                    _t += dt / Math.Max(0.1, _rules.recoverSeconds);
                    if (_t >= 1.0) LoopAway(1.0);
                    break;

                case HunterMode.Writhe:
                    _t += dt / Math.Max(0.1, _rules.writheSeconds);
                    if (_t >= 1.0)
                    {
                        _steady = _rules.steadySeconds;
                        LoopAway(0.6);
                    }
                    break;

                default:
                    if (hunting) Hunt(senses, ref orders, distance, bearing, pace, turn, reach, dt);
                    else Wander(senses, ref orders, pace, turn, dt);
                    break;
            }
            orders.Mode = Mode;
            orders.T = Mode == HunterMode.Run ? 0.0 : Math.Min(1.0, _t);
            return orders;
        }

        void LoopAway(double share)
        {
            Enter(HunterMode.Run);
            _loop = _rules.loopSeconds * share;
            _loopSide = _random.NextDouble() < 0.5 ? -1.0 : 1.0;
        }

        void Hunt(in HunterSenses senses, ref HunterOrders orders, double distance, double bearing, double pace, double turn, double reach, double dt)
        {
            bool inReach = distance < reach;
            if (inReach && Math.Abs(bearing) < 0.4 && _loop <= 0.0)
            {
                Enter(HunterMode.Rear);
                _lunge = Math.Max(0.2, distance - (BiteAhead - 0.05));
                return;
            }
            if (senses.Blocked)
            {
                // It cannot back up: it turns on the spot, toward the player's side, until the way is clear.
                orders.TurnRate = (bearing >= 0 ? 1.0 : -1.0) * turn;
                orders.Velocity = new Vec3d(0, 0, 0.15 * pace);
                return;
            }
            if (_loop > 0.0)
            {
                // Out and round: it keeps the player off to one side and behind until the loop is run.
                _loop -= dt;
                if (distance > 9.0) _loop = 0.0;
                orders.TurnRate = Toward(Wrap(bearing + _loopSide * 2.4), turn);
                orders.Velocity = new Vec3d(0, 0, pace);
                return;
            }
            // Someone inside its reach cannot walk round it faster than its head comes round.
            orders.TurnRate = inReach ? Math.Max(-turn * _rules.closeTurn, Math.Min(turn * _rules.closeTurn, bearing * 14.0)) : Toward(bearing, turn);
            if (distance > _rules.weaveBeyond)
            {
                _weave += dt;
                orders.TurnRate += _rules.weave * Math.Sin(_weave * 2.0 * Math.PI / Math.Max(0.3, _rules.weaveSeconds));
            }
            // Facing away it slows, so it turns in less room; inside its reach and not yet facing, it all but stops.
            double facing = Math.Abs(bearing) < 0.8 ? 1.0 : 0.55;
            if (inReach) facing = 0.2;
            orders.Velocity = new Vec3d(0, 0, pace * facing);
        }

        void Wander(in HunterSenses senses, ref HunterOrders orders, double pace, double turn, double dt)
        {
            _loop = 0.0;
            _wanderFor -= dt;
            if (_wanderFor <= 0.0)
            {
                _wanderFor = 2.0 + 3.0 * _random.NextDouble();
                _wanderTurn = (_random.NextDouble() - 0.5) * 1.2;
                if (_random.NextDouble() < 0.25) _wanderTurn = 0.0;
            }
            var home = new Vec3d(senses.Home.X, 0, senses.Home.Z);
            if (senses.Blocked) orders.TurnRate = turn * 0.8;
            else if (home.Magnitude > _rules.wanderRadius) orders.TurnRate = Toward(Math.Atan2(home.X, home.Z), turn * 0.6);
            else orders.TurnRate = _wanderTurn;
            orders.Velocity = new Vec3d(0, 0, Math.Min(_rules.wanderPace, pace) * (senses.Blocked ? 0.15 : 1.0));
        }

        static double Wrap(double angle)
        {
            while (angle > Math.PI) angle -= 2.0 * Math.PI;
            while (angle < -Math.PI) angle += 2.0 * Math.PI;
            return angle;
        }

        /// <summary>A turn rate that closes a bearing without swinging past it.</summary>
        static double Toward(double bearing, double rate) => Math.Max(-rate, Math.Min(rate, bearing * 3.0));
    }
}
