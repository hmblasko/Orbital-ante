using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public enum RollerMode
    {
        /// <summary>On its feet: wandering, walking up, backing off to make room.</summary>
        Walk,
        /// <summary>Curling into a ball: the warning. It still turns to face the player for the first part.</summary>
        Wind,
        /// <summary>A ball, bowling in a straight line.</summary>
        Roll,
        /// <summary>Falling open at the end of a roll, onto its back.</summary>
        Open,
        /// <summary>On its back with its legs in the air and its soft underside up: the time to shoot.</summary>
        OnBack,
        /// <summary>Half curling and rocking forward onto its feet.</summary>
        Righting,
        /// <summary>A short butt with its head, for someone standing too near to roll at.</summary>
        Butt,
    }

    /// <summary>How the Roller behaves: every number a designer may want to change. On the CreatureConfig asset.</summary>
    [Serializable]
    public sealed class RollerRules
    {
        /// <summary>Metres a second on foot: slower than the player walks. Its danger is the roll.</summary>
        public float pace = 2.6f;
        public float wanderPace = 0.9f;
        /// <summary>Radians a second.</summary>
        public float turnRate = 1.6f;
        public float sightRange = 44f;
        /// <summary>It rolls at someone between these two distances, and backs off from someone nearer to make room.</summary>
        public float chargeFrom = 5f;
        public float chargeTo = 22f;
        /// <summary>Seconds it takes to curl up before it goes: the warning.</summary>
        public float windSeconds = 0.9f;
        /// <summary>Metres a second as a ball: faster than the player sprints (8), but in a straight line.</summary>
        public float rollSpeed = 11f;
        /// <summary>It rolls this far past where the player stood when it took aim.</summary>
        public float overshoot = 5f;
        public float rollDamage = 30f;
        /// <summary>How near the middle of the ball must pass to hit (the ball is 0.63 m in radius, a person about 0.4).</summary>
        public float hitRadius = 1.05f;
        public float openSeconds = 0.45f;
        /// <summary>Seconds on its back after a roll, and after rolling into something solid.</summary>
        public float onBackSeconds = 1.5f;
        public float crashBackSeconds = 3.6f;
        public float rightSeconds = 0.6f;
        /// <summary>Seconds on its feet before it may curl up again.</summary>
        public float rest = 1.0f;
        public float buttRange = 1.9f;
        public float buttSeconds = 0.75f;
        public float buttDamage = 12f;
        public float buttRest = 1.0f;
        /// <summary>With too few plates to roll it keeps at least this far away.</summary>
        public float fleeTo = 28f;
        public float wanderRadius = 20f;
        public RollerHp hp = RollerHp.Default;
        /// <summary>Seconds a loose piece lies before it fades.</summary>
        public float pieceLife = 30f;
        /// <summary>Shared with the walkers' loose pieces.</summary>
        public int maxLoosePieces = 34;
    }

    public struct RollerSenses
    {
        public bool SeesPlayer;
        /// <summary>The player's feet in creature space (x right, z ahead).</summary>
        public Vec3d Player;
        /// <summary>Its home in creature space: it wanders round it.</summary>
        public Vec3d Home;
        /// <summary>Something solid is in the way of the last move.</summary>
        public bool Blocked;
    }

    public struct RollerOrders
    {
        /// <summary>Creature space, metres a second.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        public RollerMode Mode;
        /// <summary>How far through the mode it is, 0 to 1 (0 while walking).</summary>
        public double T;
        /// <summary>True on the one step a butt arrives.</summary>
        public bool Landed;
    }

    /// <summary>
    /// What a Roller decides, with no Unity types. It walks up slowly; with room it curls into a ball (the warning),
    /// takes aim until <see cref="LockAt"/> of the way through the curl, then bowls in a straight line past where the
    /// player stood, so a step aside after it has taken aim is a miss (constitution rule 3). At the end of the roll it
    /// falls open on its back, soft side up, and has to turn itself over: that is the time to shoot it. Rolling into
    /// something solid leaves it on its back longer. With too few plates left to close into a ball
    /// (<see cref="RollerBody.CanRoll"/>) it turns its bare back and makes off, and butts only someone who crowds it.
    /// </summary>
    public sealed class RollerBrain
    {
        /// <summary>The share of the curl after which it no longer turns to follow the player.</summary>
        public const double LockAt = 0.6;
        /// <summary>The share of a butt at which the head arrives.</summary>
        public const double ButtLands = 0.7;

        readonly RollerRules _rules;
        readonly SeededRandom _random;
        double _t, _rest, _alert, _planned, _rolled, _backFor, _rollFor, _wanderFor, _wanderTurn;
        bool _landed;

        public RollerBrain(RollerRules rules, int seed)
        {
            _rules = rules ?? new RollerRules();
            _random = new SeededRandom(seed);
        }

        public RollerMode Mode { get; private set; } = RollerMode.Walk;
        /// <summary>The last roll ended against something solid.</summary>
        public bool Crashed { get; private set; }
        /// <summary>Soft side up and unable to move.</summary>
        public bool Exposed => Mode == RollerMode.Open || Mode == RollerMode.OnBack || Mode == RollerMode.Righting;
        public bool Alerted => _alert > 0.0;
        public bool Fleeing { get; private set; }

        public void Alert(double seconds = 8.0) => _alert = Math.Max(_alert, seconds);

        /// <summary>Does a ball whose middle is at the creature's origin hit someone whose feet are here (creature space)?</summary>
        public bool RollHits(Vec3d playerFeet) =>
            Math.Sqrt(playerFeet.X * playerFeet.X + playerFeet.Z * playerFeet.Z) < _rules.hitRadius && playerFeet.Y < 1.3 && playerFeet.Y > -2.0;

        /// <summary>How much further than its reach, to the side or up and down, someone still counts as in the roll's way.</summary>
        public const double InTheWayMargin = 1.0;

        /// <summary>
        /// Is someone whose feet are here (creature space) still in the way of the roll under way? For the visor, which
        /// dims a strike that can no longer reach the player (UI/WindupCues): ahead of the ball or level with it, short of
        /// where the roll ends, and no further to the side than a step past its reach. False when it is not rolling.
        /// </summary>
        public bool RollReaches(Vec3d playerFeet)
        {
            if (Mode != RollerMode.Roll) return false;
            double reach = _rules.hitRadius, side = reach + InTheWayMargin;
            return playerFeet.Z > -reach && playerFeet.Z < _planned - _rolled + reach && Math.Abs(playerFeet.X) < side
                   && playerFeet.Y < 1.3 + InTheWayMargin && playerFeet.Y > -2.0 - InTheWayMargin;
        }

        void Enter(RollerMode mode)
        {
            Mode = mode;
            _t = 0.0;
        }

        public RollerOrders Think(RollerBody body, in RollerSenses senses, double dt)
        {
            var orders = new RollerOrders { Mode = Mode };
            if (!body.Alive) return orders;
            _alert = Math.Max(0.0, _alert - dt);
            double pace = _rules.pace * body.SpeedShare;
            double turn = _rules.turnRate * (0.5 + 0.5 * body.SpeedShare);
            var toPlayer = new Vec3d(senses.Player.X, 0, senses.Player.Z);
            double distance = toPlayer.Magnitude;
            double bearing = Math.Atan2(toPlayer.X, toPlayer.Z);
            bool hunting = senses.SeesPlayer || Alerted;
            Fleeing = false;

            switch (Mode)
            {
                case RollerMode.Wind:
                    if (!body.CanRoll)
                    {
                        // A plate was shot off as it closed: it cannot make a ball, and falls open on its feet.
                        Enter(RollerMode.Walk);
                        _rest = _rules.rest;
                        break;
                    }
                    _t += dt / Math.Max(0.2, _rules.windSeconds);
                    if (_t < LockAt && hunting)
                    {
                        orders.TurnRate = Toward(bearing, turn * 1.5);
                        _planned = distance + _rules.overshoot;
                    }
                    if (_t >= 1.0)
                    {
                        Enter(RollerMode.Roll);
                        _rolled = 0.0;
                        _rollFor = 0.0;
                        Crashed = false;
                    }
                    break;

                case RollerMode.Roll:
                {
                    if (senses.Blocked && _rollFor > 0.0)
                    {
                        Crashed = true;
                        Enter(RollerMode.Open);
                        break;
                    }
                    _rollFor += dt;
                    double speed = _rules.rollSpeed * Math.Min(1.0, 0.25 + _rollFor / 0.3);
                    orders.Velocity = new Vec3d(0, 0, speed);
                    _rolled += speed * dt;
                    _t = Math.Min(1.0, _rolled / Math.Max(1.0, _planned));
                    if (_rolled >= _planned)
                    {
                        Enter(RollerMode.Open);
                        orders.Velocity = Vec3d.Zero;
                    }
                    break;
                }

                case RollerMode.Open:
                    // Stopped by something solid, it falls open back the way it came, not into what stopped it.
                    if (Crashed) orders.Velocity = new Vec3d(0, 0, -(0.5 * RollerLayout.TotalLength - RollerLayout.BallRadius + 0.3) / Math.Max(0.1, _rules.openSeconds));
                    _t += dt / Math.Max(0.1, _rules.openSeconds);
                    if (_t >= 1.0)
                    {
                        Enter(RollerMode.OnBack);
                        _backFor = (Crashed ? _rules.crashBackSeconds : _rules.onBackSeconds) * body.OnBackShare;
                    }
                    break;

                case RollerMode.OnBack:
                    _t += dt / Math.Max(0.1, _backFor);
                    if (_t >= 1.0) Enter(RollerMode.Righting);
                    break;

                case RollerMode.Righting:
                    _t += dt / Math.Max(0.1, _rules.rightSeconds);
                    if (_t >= 1.0)
                    {
                        Enter(RollerMode.Walk);
                        _rest = _rules.rest;
                    }
                    break;

                case RollerMode.Butt:
                {
                    double before = _t;
                    _t += dt / Math.Max(0.2, _rules.buttSeconds);
                    if (before < ButtLands && _t >= ButtLands && !_landed)
                    {
                        _landed = true;
                        orders.Landed = true;
                    }
                    if (_t >= 1.0)
                    {
                        Enter(RollerMode.Walk);
                        _rest = _rules.buttRest;
                    }
                    break;
                }

                default:
                    _rest = Math.Max(0.0, _rest - dt);
                    if (hunting) Hunt(body, senses, ref orders, distance, bearing, pace, turn);
                    else Wander(senses, ref orders, pace, turn, dt);
                    break;
            }
            orders.Mode = Mode;
            orders.T = Mode == RollerMode.Walk ? 0.0 : Math.Min(1.0, _t);
            return orders;
        }

        void Hunt(RollerBody body, in RollerSenses senses, ref RollerOrders orders, double distance, double bearing, double pace, double turn)
        {
            orders.TurnRate = Toward(bearing, turn);
            bool ready = _rest <= 0.0;
            if (body.CanRoll && ready && distance >= _rules.chargeFrom && distance <= _rules.chargeTo && Math.Abs(bearing) < 0.35)
            {
                Enter(RollerMode.Wind);
                _planned = distance + _rules.overshoot;
                return;
            }
            if (ready && distance < _rules.buttRange && Math.Abs(bearing) < 0.5)
            {
                Enter(RollerMode.Butt);
                _landed = false;
                return;
            }
            if (body.CanRoll)
            {
                // It wants room to roll: it walks up to rolling distance, and backs away from someone too near.
                if (distance > _rules.chargeTo * 0.85 && Math.Abs(bearing) < 0.9 && !senses.Blocked) orders.Velocity = new Vec3d(0, 0, pace);
                else if (distance < _rules.chargeFrom && distance > _rules.buttRange * 0.8 && !senses.Blocked) orders.Velocity = new Vec3d(0, 0, -0.6 * pace);
            }
            else if (distance < _rules.fleeTo)
            {
                // Too few plates to make a ball: it shows its bare back and makes off.
                Fleeing = true;
                double away = bearing > 0 ? bearing - Math.PI : bearing + Math.PI;
                orders.TurnRate = Toward(away, turn);
                if (Math.Abs(away) < 1.2 && !senses.Blocked) orders.Velocity = new Vec3d(0, 0, pace);
            }
        }

        void Wander(in RollerSenses senses, ref RollerOrders orders, double pace, double turn, double dt)
        {
            _wanderFor -= dt;
            if (_wanderFor <= 0.0)
            {
                _wanderFor = 3.0 + 4.0 * _random.NextDouble();
                _wanderTurn = (_random.NextDouble() - 0.5) * 0.6;
                if (_random.NextDouble() < 0.3) _wanderTurn = 0.0;
            }
            var home = new Vec3d(senses.Home.X, 0, senses.Home.Z);
            if (home.Magnitude > _rules.wanderRadius) orders.TurnRate = Toward(Math.Atan2(home.X, home.Z), turn * 0.6);
            else if (senses.Blocked) orders.TurnRate = turn * 0.8;
            else orders.TurnRate = _wanderTurn;
            if (!senses.Blocked) orders.Velocity = new Vec3d(0, 0, Math.Min(_rules.wanderPace, pace));
        }

        /// <summary>A turn rate that closes a bearing without swinging past it.</summary>
        static double Toward(double bearing, double rate) => Math.Max(-rate, Math.Min(rate, bearing * 3.0));
    }
}
