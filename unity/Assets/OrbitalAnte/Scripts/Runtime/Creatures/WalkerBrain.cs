using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>How one kind of walker behaves: every number a designer may want to change. On the CreatureConfig asset.
    /// The numbers written here are the Strider's; <see cref="ForSprinter"/> gives the Sprinter's.</summary>
    [Serializable]
    public sealed class WalkerRules
    {
        /// <summary>Metres a second on all eight legs. Slower than the player runs: it is outrun, not outgunned.</summary>
        public float pace = 3.4f;
        public float wanderPace = 1.1f;
        /// <summary>Radians a second.</summary>
        public float turnRate = 1.3f;
        public float sightRange = 46f;
        /// <summary>It stops this far from the player (measured along the ground from the middle of its body).</summary>
        public float standOff = 3.3f;
        /// <summary>It backs off when the player is nearer than this, so nobody can stand safe under its body.</summary>
        public float tooNear = 2.2f;
        /// <summary>It throws a claw when the player is this near and in front of it.</summary>
        public float strikeRange = 4.0f;
        /// <summary>Seconds a strike takes from the first lift of the claw to drawing it back.</summary>
        public float strikeSeconds = 1.6f;
        public float strikeRest = 1.3f;
        /// <summary>How near the claw's point must land to hurt, in metres.</summary>
        public float strikeRadius = 0.8f;
        public float strikeDamage = 34f;
        /// <summary>With both claws gone it keeps at least this far away.</summary>
        public float fleeTo = 32f;
        public float wanderRadius = 22f;
        public WalkerHp hp = WalkerHp.Strider;
        /// <summary>Hunting, it runs for about this long, then stands still for about <see cref="pauseSeconds"/> feeling the air
        /// (a camel spider's stop-and-go dash): the pause is the time to shoot. 0 for a kind that walks steadily.</summary>
        public float burstSeconds = 0f;
        public float pauseSeconds = 0f;
        /// <summary>It only stops between dashes when the player is further off than this; nearer, it comes straight in.</summary>
        public float pauseBeyond = 8f;
        /// <summary>Seconds a loose piece lies before it fades.</summary>
        public float pieceLife = 30f;
        /// <summary>Shared by every walker. One death throws about 16, so this holds two.</summary>
        public int maxLoosePieces = 34;

        /// <summary>
        /// The Sprinter: quicker than the player walks (5) and slower than they sprint (8), so it cannot be strolled
        /// away from but can be outrun; light (see
        /// <see cref="WalkerHp.Sprinter"/>), it stops between dashes, and its bite is a lunge that is aimed early.
        /// </summary>
        public static WalkerRules ForSprinter() => new WalkerRules
        {
            pace = 6.2f, wanderPace = 1.6f, turnRate = 3.2f, sightRange = 40f,
            standOff = 2.0f, tooNear = 1.1f, strikeRange = 2.5f,
            strikeSeconds = 1.05f, strikeRest = 0.9f, strikeRadius = 0.6f, strikeDamage = 22f,
            fleeTo = 30f, wanderRadius = 18f, hp = WalkerHp.Sprinter,
            burstSeconds = 1.3f, pauseSeconds = 0.55f,
        };
    }

    public struct WalkerSenses
    {
        public bool SeesPlayer;
        /// <summary>The player's feet in walker space (x right, z ahead), and where a claw should land (their chest).</summary>
        public Vec3d Player;
        public Vec3d Chest;
        /// <summary>Its home in walker space: it wanders round it.</summary>
        public Vec3d Home;
        /// <summary>Something solid is in the way of the last move.</summary>
        public bool Blocked;
        /// <summary>How high the middle of its body is now (it sinks as it loses legs).</summary>
        public double BodyHeight;
    }

    public struct WalkerOrders
    {
        /// <summary>Walker space, metres a second.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        public int StrikeSide;
        public double StrikeT;
        public Vec3d StrikeAt;
        /// <summary>True on the one step the claw's point arrives at <see cref="StrikeAt"/>.</summary>
        public bool Landed;
    }

    /// <summary>
    /// What a walker decides, with no Unity types. It wanders round its home; seeing the player it walks up, stops at
    /// arm's length and throws a claw: the raise is the warning, and the place it will land is fixed at
    /// <see cref="LockAt"/> of the way through, so a step aside afterwards is a miss (constitution rule 3: learnable,
    /// never a hidden rule). It strikes with the claw on the player's side if it still has it. With both claws gone it
    /// cannot hurt anyone and backs away. Fewer legs only make it slower (<see cref="WalkerBody.SpeedShare"/>).
    /// </summary>
    public sealed class WalkerBrain
    {
        /// <summary>The share of a strike after which the claw no longer follows the player.</summary>
        public const double LockAt = 0.25;

        readonly WalkerRules _rules;
        readonly WalkerLayout _l;
        double _burst, _pause, _gapAtDash = double.MaxValue;
        readonly SeededRandom _random;
        double _strikeT = -1.0, _rest, _wanderTurn, _wanderFor, _alert;
        int _strikeSide = -1;
        Vec3d _strikeAt;
        bool _landed;

        public WalkerBrain(WalkerLayout layout, WalkerRules rules, int seed)
        {
            _l = layout;
            _rules = rules ?? new WalkerRules();
            _random = new SeededRandom(seed);
        }

        /// <summary>
        /// The share of a strike after which it no longer follows the player: <see cref="LockAt"/>, but a kind that
        /// lunges feels for its target with its arms, and with both of them shot off it bites where the player
        /// stood when it began (so losing them costs it something that can be seen).
        /// </summary>
        public double LockShare(WalkerBody body) =>
            _l.Attack == WalkerAttack.Lunge && body.Kept(WalkerLayout.Limb(0, 0)) < WalkerLayout.Segments && body.Kept(WalkerLayout.Limb(1, 0)) < WalkerLayout.Segments
                ? 0.0 : LockAt;

        public bool Striking => _strikeT >= 0.0;
        public bool Fleeing { get; private set; }
        /// <summary>Standing still between two dashes.</summary>
        public bool Pausing => _pause > 0.0;
        /// <summary>It has been hurt: for a while it knows where the player is whatever it can see.</summary>
        public bool Alerted => _alert > 0.0;

        public void Alert(double seconds = 8.0) => _alert = Math.Max(_alert, seconds);

        /// <summary>
        /// The strike is thrown in walker space, so a turn of the body would carry it; the body stands still while a
        /// claw is out, and the animator is given the same place every step after the lock.
        /// </summary>
        public WalkerOrders Think(WalkerBody body, in WalkerSenses senses, double dt)
        {
            var orders = new WalkerOrders { StrikeSide = -1 };
            if (!body.Alive) return orders;
            _rest = Math.Max(0.0, _rest - dt);
            _alert = Math.Max(0.0, _alert - dt);
            double pace = _rules.pace * body.SpeedShare;
            double turn = _rules.turnRate * (0.4 + 0.6 * body.SpeedShare);
            var toPlayer = new Vec3d(senses.Player.X, 0, senses.Player.Z);
            double distance = toPlayer.Magnitude;
            double bearing = Math.Atan2(toPlayer.X, toPlayer.Z);
            bool hunting = senses.SeesPlayer || Alerted;
            bool armed = body.CanStrike(WalkerLayout.Right) || body.CanStrike(WalkerLayout.Left);
            Fleeing = false;

            if (Striking)
            {
                if (_strikeSide < 0 || !body.CanStrike(_strikeSide))
                {
                    // The claw was shot off in mid swing.
                    _strikeT = -1.0;
                    _rest = _rules.strikeRest;
                    return orders;
                }
                double before = _strikeT;
                _strikeT += dt / Math.Max(0.2, _rules.strikeSeconds);
                if (_strikeT < LockShare(body) && hunting) _strikeAt = Reachable(_strikeSide, senses.Chest, senses.BodyHeight);
                if (before < WalkerAnimator.StrikeLands && _strikeT >= WalkerAnimator.StrikeLands && !_landed)
                {
                    _landed = true;
                    orders.Landed = true;
                }
                orders.StrikeSide = _strikeSide;
                orders.StrikeT = Math.Min(1.0, _strikeT);
                orders.StrikeAt = _strikeAt;
                if (_strikeT >= 1.0)
                {
                    _strikeT = -1.0;
                    _rest = _rules.strikeRest;
                }
                return orders;
            }

            if (hunting && !armed)
            {
                // Nothing left to hurt with: turn tail until it is far enough off.
                if (distance < _rules.fleeTo)
                {
                    Fleeing = true;
                    double away = Wrap(bearing + Math.PI);
                    orders.TurnRate = Toward(away, turn);
                    if (Math.Abs(away) < 1.2 && !senses.Blocked) orders.Velocity = new Vec3d(0, 0, pace);
                }
                return orders;
            }

            if (hunting)
            {
                orders.TurnRate = Toward(bearing, turn);
                // A kind that dashes stops now and then on the way in, never once it is close enough to bite.
                if (_rules.burstSeconds > 0f && distance > _rules.pauseBeyond)
                {
                    if (_pause > 0.0)
                    {
                        _pause -= dt;
                        if (_pause <= 0.0)
                        {
                            _burst = _rules.burstSeconds * (0.7 + 0.6 * _random.NextDouble());
                            _gapAtDash = distance;
                        }
                    }
                    else
                    {
                        _burst -= dt;
                        if (_burst <= 0.0)
                        {
                            // It stops only if that dash gained on the player; behind someone walking away it keeps running.
                            bool gained = _gapAtDash - distance > 0.6 * pace * _rules.burstSeconds;
                            if (gained) _pause = _rules.pauseSeconds * (0.8 + 0.4 * _random.NextDouble());
                            else _burst = _rules.burstSeconds * (0.7 + 0.6 * _random.NextDouble());
                            _gapAtDash = distance;
                        }
                    }
                }
                else
                {
                    _pause = 0.0;
                }
                if (_pause > 0.0) return orders;
                if (distance > _rules.standOff && Math.Abs(bearing) < 0.9 && !senses.Blocked)
                {
                    // A kind that lunges keeps its full pace right up to where it stops (it must catch someone walking
                    // away); a kind that reaches with a claw eases in.
                    bool rush = _l.Attack == WalkerAttack.Lunge;
                    orders.Velocity = new Vec3d(0, 0, rush ? pace : Math.Min(pace, (distance - _rules.standOff) * 2.0 + 0.3));
                }
                else if (distance < _rules.tooNear && body.Stance == WalkerStance.Standing)
                    orders.Velocity = new Vec3d(0, 0, -0.7 * pace);
                if (distance < _rules.strikeRange && Math.Abs(bearing) < 0.45 && _rest <= 0.0)
                {
                    int near = senses.Player.X >= 0 ? WalkerLayout.Right : WalkerLayout.Left;
                    _strikeSide = body.CanStrike(near) ? near : 1 - near;
                    _strikeT = 0.0;
                    _landed = false;
                    _strikeAt = Reachable(_strikeSide, senses.Chest, senses.BodyHeight);
                    orders.Velocity = Vec3d.Zero;
                    orders.StrikeSide = _strikeSide;
                    orders.StrikeAt = _strikeAt;
                }
                return orders;
            }

            // Nobody about: an unhurried walk round its home, turning now and then, and back when it has strayed.
            _wanderFor -= dt;
            if (_wanderFor <= 0.0)
            {
                _wanderFor = 2.5 + 4.0 * _random.NextDouble();
                _wanderTurn = (_random.NextDouble() - 0.5) * 0.7;
                if (_random.NextDouble() < 0.25) _wanderTurn = 0.0;
            }
            var home = new Vec3d(senses.Home.X, 0, senses.Home.Z);
            if (home.Magnitude > _rules.wanderRadius) orders.TurnRate = Toward(Math.Atan2(home.X, home.Z), turn * 0.6);
            else if (senses.Blocked) orders.TurnRate = turn * 0.8;
            else orders.TurnRate = _wanderTurn;
            if (!senses.Blocked) orders.Velocity = new Vec3d(0, 0, Math.Min(_rules.wanderPace, pace));
            return orders;
        }

        static double Wrap(double angle)
        {
            while (angle > Math.PI) angle -= 2 * Math.PI;
            while (angle < -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        /// <summary>A turn rate that closes a bearing without swinging past it.</summary>
        static double Toward(double bearing, double rate) => Math.Max(-rate, Math.Min(rate, bearing * 3.0));

        /// <summary>
        /// The place nearest <paramref name="wanted"/> this kind can strike: where the claw on that side can put its
        /// point, or (a kind that lunges) how far its jaws can be thrown, at the height it carries them.
        /// </summary>
        public Vec3d Reachable(int side, Vec3d wanted, double bodyHeight = 0.0)
        {
            if (bodyHeight <= 0.0) bodyHeight = _l.StandHeight;
            if (_l.Attack == WalkerAttack.Lunge)
            {
                Vec3d rest = _l.BiteRest(bodyHeight);
                var by = new Vec3d(wanted.X - rest.X, 0, Math.Max(0.1, wanted.Z - rest.Z));
                double length = by.Magnitude;
                if (length > _l.StrikeShift) by = by * (_l.StrikeShift / length);
                return rest + by;
            }
            int arm = WalkerLayout.Limb(side, 0);
            Vec3d shoulder = _l.HipModelled(arm) * _l.Size + new Vec3d(0, bodyHeight, 0);
            double reach = 0.94 * (_l.Length(arm, 0) + _l.Length(arm, 1) + _l.Length(arm, 2));
            Vec3d to = wanted - shoulder;
            double far = to.Magnitude;
            if (far > reach) wanted = shoulder + to * (reach / far);
            // Never through its own body, and never under the ground.
            return new Vec3d(wanted.X, Math.Max(0.15, wanted.Y), Math.Max(wanted.Z, 0.9));
        }
    }
}
