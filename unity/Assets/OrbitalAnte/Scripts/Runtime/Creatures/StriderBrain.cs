using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>How a Strider behaves: every number a designer may want to change. On the CreatureConfig asset.</summary>
    [Serializable]
    public sealed class StriderRules
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
        public StriderHp hp = StriderHp.Default;
        /// <summary>Seconds a loose piece lies before it fades.</summary>
        public float pieceLife = 30f;
        public int maxLoosePieces = 20;
    }

    public struct StriderSenses
    {
        public bool SeesPlayer;
        /// <summary>The player's feet in strider space (x right, z ahead), and where a claw should land (their chest).</summary>
        public Vec3d Player;
        public Vec3d Chest;
        /// <summary>Its home in strider space: it wanders round it.</summary>
        public Vec3d Home;
        /// <summary>Something solid is in the way of the last move.</summary>
        public bool Blocked;
        /// <summary>How high the middle of its body is now (it sinks as it loses legs).</summary>
        public double BodyHeight;
    }

    public struct StriderOrders
    {
        /// <summary>Strider space, metres a second.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        public int StrikeSide;
        public double StrikeT;
        public Vec3d StrikeAt;
        /// <summary>True on the one step the claw's point arrives at <see cref="StrikeAt"/>.</summary>
        public bool Landed;
    }

    /// <summary>
    /// What a Strider decides, with no Unity types. It wanders round its home; seeing the player it walks up, stops at
    /// arm's length and throws a claw: the raise is the warning, and the place it will land is fixed at
    /// <see cref="LockAt"/> of the way through, so a step aside afterwards is a miss (constitution rule 3: learnable,
    /// never a hidden rule). It strikes with the claw on the player's side if it still has it. With both claws gone it
    /// cannot hurt anyone and backs away. Fewer legs only make it slower (<see cref="StriderBody.SpeedShare"/>).
    /// </summary>
    public sealed class StriderBrain
    {
        /// <summary>The share of a strike after which the claw no longer follows the player.</summary>
        public const double LockAt = 0.25;

        readonly StriderRules _rules;
        readonly SeededRandom _random;
        double _strikeT = -1.0, _rest, _wanderTurn, _wanderFor, _alert;
        int _strikeSide = -1;
        Vec3d _strikeAt;
        bool _landed;

        public StriderBrain(StriderRules rules, int seed)
        {
            _rules = rules ?? new StriderRules();
            _random = new SeededRandom(seed);
        }

        public bool Striking => _strikeT >= 0.0;
        public bool Fleeing { get; private set; }
        /// <summary>It has been hurt: for a while it knows where the player is whatever it can see.</summary>
        public bool Alerted => _alert > 0.0;

        public void Alert(double seconds = 8.0) => _alert = Math.Max(_alert, seconds);

        /// <summary>
        /// The strike is thrown in strider space, so a turn of the body would carry it; the body stands still while a
        /// claw is out, and the animator is given the same place every step after the lock.
        /// </summary>
        public StriderOrders Think(StriderBody body, in StriderSenses senses, double dt)
        {
            var orders = new StriderOrders { StrikeSide = -1 };
            if (!body.Alive) return orders;
            _rest = Math.Max(0.0, _rest - dt);
            _alert = Math.Max(0.0, _alert - dt);
            double pace = _rules.pace * body.SpeedShare;
            double turn = _rules.turnRate * (0.4 + 0.6 * body.SpeedShare);
            var toPlayer = new Vec3d(senses.Player.X, 0, senses.Player.Z);
            double distance = toPlayer.Magnitude;
            double bearing = Math.Atan2(toPlayer.X, toPlayer.Z);
            bool hunting = senses.SeesPlayer || Alerted;
            bool armed = body.CanStrike(StriderLayout.Right) || body.CanStrike(StriderLayout.Left);
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
                if (_strikeT < LockAt && hunting) _strikeAt = Reachable(_strikeSide, senses.Chest, senses.BodyHeight);
                if (before < StriderAnimator.StrikeLands && _strikeT >= StriderAnimator.StrikeLands && !_landed)
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
                if (distance > _rules.standOff && Math.Abs(bearing) < 0.9 && !senses.Blocked)
                    orders.Velocity = new Vec3d(0, 0, Math.Min(pace, (distance - _rules.standOff) * 2.0 + 0.3));
                else if (distance < _rules.tooNear && body.Stance == StriderStance.Standing)
                    orders.Velocity = new Vec3d(0, 0, -0.7 * pace);
                if (distance < _rules.strikeRange && Math.Abs(bearing) < 0.45 && _rest <= 0.0)
                {
                    int near = senses.Player.X >= 0 ? StriderLayout.Right : StriderLayout.Left;
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

        /// <summary>The place nearest <paramref name="wanted"/> the claw on this side can put its point.</summary>
        public static Vec3d Reachable(int side, Vec3d wanted, double bodyHeight = 0.0)
        {
            int arm = StriderLayout.Limb(side, 0);
            if (bodyHeight <= 0.0) bodyHeight = StriderLayout.StandHeight;
            Vec3d shoulder = StriderLayout.HipModelled(arm) * StriderLayout.Size + new Vec3d(0, bodyHeight, 0);
            double reach = 0.94 * (StriderLayout.Length(arm, 0) + StriderLayout.Length(arm, 1) + StriderLayout.Length(arm, 2));
            Vec3d to = wanted - shoulder;
            double far = to.Magnitude;
            if (far > reach) wanted = shoulder + to * (reach / far);
            // Never through its own body, and never under the ground.
            return new Vec3d(wanted.X, Math.Max(0.15, wanted.Y), Math.Max(wanted.Z, 0.9));
        }
    }
}
