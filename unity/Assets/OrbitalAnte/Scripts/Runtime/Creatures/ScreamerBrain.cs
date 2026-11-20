using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public enum ScreamerMode
    {
        /// <summary>On the ground, wings folded: resting, turning to face someone, or crawling if it cannot fly.</summary>
        Perch,
        /// <summary>In the air, on its way to somewhere to land.</summary>
        Fly,
        /// <summary>Coming down.</summary>
        Land,
        /// <summary>Its abdomen lifts and its wings fan out and shiver: the warning.</summary>
        WindUp,
        /// <summary>It screams: everyone in front of it, in sight and in range, is hurt for as long as they stay there.</summary>
        Scream,
        /// <summary>Out of breath, or choked by losing a drum, or stunned by a fall: the time to shoot it.</summary>
        Spent,
        /// <summary>It has lost too many wings in the air and drops.</summary>
        Fall,
    }

    /// <summary>How the Screamer behaves: every number a designer may want to change. On the CreatureConfig asset.</summary>
    [Serializable]
    public sealed class ScreamerRules
    {
        /// <summary>Metres a second in the air: quicker than the player sprints (8), so it cannot be run down, only shot or hidden from.</summary>
        public float flyPace = 9f;
        /// <summary>Radians a second in the air.</summary>
        public float flyTurn = 2.6f;
        public float cruiseHeight = 3.4f;
        public float climbRate = 4.5f;
        /// <summary>Seconds from cruising height to the ground.</summary>
        public float landSeconds = 0.6f;
        /// <summary>Metres a second on its legs when it can no longer fly: slower than the player walks (5).</summary>
        public float walkPace = 2.2f;
        public float sightRange = 45f;
        /// <summary>How far from the player it likes to land.</summary>
        public float perchRange = 10f;
        /// <summary>Seconds its abdomen is up and its wings shiver before it screams: the warning.</summary>
        public float windUpSeconds = 1.0f;
        public float screamSeconds = 2.4f;
        /// <summary>Seconds it sags, out of breath, after a scream.</summary>
        public float spentSeconds = 1.3f;
        /// <summary>Seconds it is choked when a drum is shot off while it winds up or screams.</summary>
        public float chokeSeconds = 1.8f;
        /// <summary>Seconds it lies stunned after dropping out of the air.</summary>
        public float crashSeconds = 2.2f;
        /// <summary>How far the scream hurts with both drums (less with one).</summary>
        public float screamRange = 16f;
        /// <summary>Radians either side of straight ahead that the scream covers.</summary>
        public float coneHalfAngle = 0.5f;
        /// <summary>Damage a second right in front of it, and at the far edge of its range.</summary>
        public float damageNear = 16f;
        public float damageFar = 6f;
        /// <summary>The scream hurts in beats this far apart.</summary>
        public float pulseSeconds = 0.2f;
        /// <summary>Radians a second it turns to face someone before it has taken aim.</summary>
        public float aimTurn = 2.6f;
        /// <summary>Radians a second it can swing the scream round: slow, so it can be sidestepped.</summary>
        public float screamTurn = 0.35f;
        /// <summary>Nearer than this to a perched one that is not screaming, and it takes off.</summary>
        public float shyRange = 4f;
        /// <summary>With no drums left it keeps this far away.</summary>
        public float fleeRange = 26f;
        public float wanderRadius = 12f;
        /// <summary>It never goes further than this from where it started.</summary>
        public float roamRadius = 45f;
        public ScreamerHp hp = ScreamerHp.Default;
        /// <summary>Seconds a loose piece lies before it fades.</summary>
        public float pieceLife = 30f;
        /// <summary>Shared with the other creatures' loose pieces.</summary>
        public int maxLoosePieces = 34;
    }

    public struct ScreamerSenses
    {
        public bool SeesPlayer;
        /// <summary>The player's feet in creature space (x right, z ahead; the origin is on the ground under it).</summary>
        public Vec3d Player;
        /// <summary>Where it started, in creature space.</summary>
        public Vec3d Home;
        /// <summary>A direction along the ground that never changes, in creature space: with Home it lets it keep a place in mind while it turns and flies.</summary>
        public Vec3d North;
        /// <summary>Something solid stopped the last move.</summary>
        public bool Blocked;
    }

    public struct ScreamerOrders
    {
        /// <summary>Creature space, metres a second, along the ground.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        /// <summary>Metres its feet are off the ground.</summary>
        public double Height;
        public ScreamerMode Mode;
        /// <summary>How far through the mode it is, 0 to 1 (0 while perched or flying).</summary>
        public double T;
        /// <summary>True on each beat of the scream: whoever <see cref="ScreamerBrain.DamageRate"/> says is hurt, is.</summary>
        public bool Pulse;
        /// <summary>True on the step it hits the ground after falling.</summary>
        public bool Crashed;
    }

    /// <summary>
    /// What a Screamer decides, with no Unity types. It flies to a spot some way from the player and lands. There it
    /// lifts its abdomen and fans its wings (the warning), follows the player until <see cref="LockAt"/> of the way
    /// through, and screams: a cone of sound straight ahead that hurts everyone in it, in sight and in range, most
    /// from close up (<see cref="DamageRate"/>). It can only swing the scream round slowly, so the answers are to
    /// run out of the cone, get behind something solid, or shoot a drum off its flank, which chokes it
    /// (<see cref="Choke"/>). After a scream it sags, out of breath, then flies somewhere else (constitution rules 3
    /// and 8). With no drums it keeps away; shot out of the air it falls and lies stunned.
    /// </summary>
    public sealed class ScreamerBrain
    {
        /// <summary>The share of the warning after which it no longer turns freely to follow the player.</summary>
        public const double LockAt = 0.6;

        readonly ScreamerRules _rules;
        readonly SeededRandom _random;
        double _t, _alert, _pulse, _spentFor = 1.0, _settle, _rest, _fallSpeed, _flyFor, _side = 1.0, _drift;
        /// <summary>Where it is going, east and north of home (X and Z).</summary>
        Vec3d _target;
        int _blockedCount;

        public ScreamerBrain(ScreamerRules rules, int seed)
        {
            _rules = rules ?? new ScreamerRules();
            _random = new SeededRandom(seed);
            _rest = 4.0 + 6.0 * _random.NextDouble();
            _side = _random.NextDouble() < 0.5 ? -1.0 : 1.0;
        }

        public ScreamerMode Mode { get; private set; } = ScreamerMode.Perch;
        public double Height { get; private set; }
        public bool Alerted => _alert > 0.0;

        public void Alert(double seconds = 10.0) => _alert = Math.Max(_alert, seconds);

        /// <summary>How far its scream hurts: all of its range with both drums, about four fifths with one.</summary>
        public static double Range(ScreamerRules rules, ScreamerBody body) => rules.screamRange * (0.55 + 0.45 * body.Voice);

        /// <summary>
        /// Damage a second to someone whose feet are here (creature space) while it screams: nothing outside the cone
        /// or out of range, most from close up. Whether something solid is in between is for the caller to check.
        /// </summary>
        public static double DamageRate(ScreamerRules rules, ScreamerBody body, Vec3d playerFeet)
        {
            double voice = body.Voice;
            if (voice <= 0.0) return 0.0;
            double distance = Math.Sqrt(playerFeet.X * playerFeet.X + playerFeet.Z * playerFeet.Z);
            double range = Range(rules, body);
            if (distance > range) return 0.0;
            if (distance > 0.6 && Math.Abs(Math.Atan2(playerFeet.X, playerFeet.Z)) > rules.coneHalfAngle) return 0.0;
            return voice * (rules.damageNear + (rules.damageFar - rules.damageNear) * distance / Math.Max(0.1, range));
        }

        /// <summary>
        /// A drum has been shot off. If it was winding up or screaming it chokes: the scream stops and it sags for a
        /// while. The answer says whether it did.
        /// </summary>
        public bool Choke()
        {
            if (Mode != ScreamerMode.WindUp && Mode != ScreamerMode.Scream) return false;
            Spend(_rules.chokeSeconds);
            return true;
        }

        void Enter(ScreamerMode mode)
        {
            Mode = mode;
            _t = 0.0;
        }

        void Spend(double seconds)
        {
            Enter(ScreamerMode.Spent);
            _spentFor = Math.Max(0.1, seconds);
        }

        public ScreamerOrders Think(ScreamerBody body, in ScreamerSenses senses, double dt)
        {
            var orders = new ScreamerOrders { Mode = Mode, Height = Height };
            if (!body.Alive) return orders;
            _alert = Math.Max(0.0, _alert - dt);
            bool hunting = senses.SeesPlayer || Alerted;
            var toPlayer = new Vec3d(senses.Player.X, 0, senses.Player.Z);
            double distance = toPlayer.Magnitude;
            double bearing = Math.Atan2(toPlayer.X, toPlayer.Z);

            // Too few wings to stay up: it drops, whatever it was doing.
            if (Height > 0.0 && !body.CanFly && Mode != ScreamerMode.Fall)
            {
                Enter(ScreamerMode.Fall);
                _fallSpeed = 0.0;
            }

            switch (Mode)
            {
                case ScreamerMode.Fall:
                    _fallSpeed += 9.8 * dt;
                    Height = Math.Max(0.0, Height - _fallSpeed * dt);
                    _drift = Math.Max(0.0, _drift - 3.0 * dt);
                    if (!senses.Blocked) orders.Velocity = new Vec3d(0, 0, _drift);
                    if (Height <= 0.0)
                    {
                        orders.Crashed = true;
                        _drift = 0.0;
                        Spend(_rules.crashSeconds);
                    }
                    break;

                case ScreamerMode.Fly:
                    Fly(body, senses, ref orders, hunting, dt);
                    break;

                case ScreamerMode.Land:
                    Height = Math.Max(0.0, Height - _rules.cruiseHeight / Math.Max(0.1, _rules.landSeconds) * dt);
                    _drift = Math.Max(0.0, _drift - 6.0 * dt);
                    if (!senses.Blocked) orders.Velocity = new Vec3d(0, 0, _drift);
                    // It comes down facing whoever it is hunting.
                    if (hunting) orders.TurnRate = Toward(bearing, _rules.aimTurn);
                    if (Height <= 0.0)
                    {
                        Enter(ScreamerMode.Perch);
                        _settle = 0.3;
                    }
                    break;

                case ScreamerMode.WindUp:
                    _t += dt / Math.Max(0.2, _rules.windUpSeconds);
                    if (_t < LockAt && hunting) orders.TurnRate = Toward(bearing, _rules.aimTurn);
                    if (_t >= 1.0)
                    {
                        Enter(ScreamerMode.Scream);
                        _pulse = _rules.pulseSeconds;
                    }
                    break;

                case ScreamerMode.Scream:
                    _t += dt / Math.Max(0.2, _rules.screamSeconds);
                    if (hunting) orders.TurnRate = Toward(bearing, _rules.screamTurn);
                    _pulse -= dt;
                    if (_pulse <= 0.0)
                    {
                        orders.Pulse = true;
                        _pulse += Math.Max(0.05, _rules.pulseSeconds);
                    }
                    if (_t >= 1.0) Spend(_rules.spentSeconds);
                    break;

                case ScreamerMode.Spent:
                    _t += dt / _spentFor;
                    if (_t >= 1.0)
                    {
                        Enter(ScreamerMode.Perch);
                        // One that can still fly moves on at once; one that cannot gets its breath back where it is.
                        _settle = 1.0;
                        if (hunting && body.CanFly) FlyTo(body.CanScream ? PerchNear(senses) : Away(senses));
                    }
                    break;

                default:
                    if (hunting) Stalk(body, senses, ref orders, distance, bearing, dt);
                    else Idle(body, senses, dt);
                    break;
            }
            orders.Mode = Mode;
            orders.Height = Height;
            orders.T = Mode == ScreamerMode.Perch || Mode == ScreamerMode.Fly || Mode == ScreamerMode.Land || Mode == ScreamerMode.Fall ? 0.0 : Math.Min(1.0, _t);
            return orders;
        }

        // ------------------------------------------------------------------ on the ground

        void Stalk(ScreamerBody body, in ScreamerSenses senses, ref ScreamerOrders orders, double distance, double bearing, double dt)
        {
            _settle = Math.Max(0.0, _settle - dt);
            if (!body.CanScream)
            {
                // Silent for good: it keeps away.
                if (distance > _rules.fleeRange * 0.7) return;
                if (body.CanFly) FlyTo(Away(senses));
                else if (body.CanWalk) Crawl(body, senses, ref orders, Wrap(bearing + Math.PI), 1.0);
                return;
            }
            // Come upon on the ground, it will not stay to be shot at close quarters.
            if (distance < _rules.shyRange && body.CanFly && _settle <= 0.0)
            {
                FlyTo(PerchNear(senses));
                return;
            }
            bool inRange = senses.SeesPlayer && distance < Range(_rules, body) * 0.8;
            if (inRange)
            {
                orders.TurnRate = Toward(bearing, _rules.aimTurn * (body.CanWalk ? 1.0 : 0.5));
                if (Math.Abs(bearing) < 0.25 && _settle <= 0.0) Enter(ScreamerMode.WindUp);
                return;
            }
            if (_settle > 0.0) return;
            if (body.CanFly) FlyTo(PerchNear(senses));
            else if (body.CanWalk) Crawl(body, senses, ref orders, bearing, 1.0);
            else orders.TurnRate = Toward(bearing, _rules.aimTurn * 0.5);
        }

        void Crawl(ScreamerBody body, in ScreamerSenses senses, ref ScreamerOrders orders, double bearing, double share)
        {
            orders.TurnRate = senses.Blocked ? _side * 1.5 : Toward(bearing, 2.0);
            double facing = Math.Abs(bearing) < 0.8 ? 1.0 : 0.3;
            orders.Velocity = new Vec3d(0, 0, _rules.walkPace * body.WalkShare * share * facing * (senses.Blocked ? 0.2 : 1.0));
        }

        void Idle(ScreamerBody body, in ScreamerSenses senses, double dt)
        {
            _rest -= dt;
            if (_rest > 0.0 || !body.CanFly) return;
            _rest = 6.0 + 6.0 * _random.NextDouble();
            // A short flight to somewhere else near home.
            double angle = _random.NextDouble() * 2.0 * Math.PI, far = _rules.wanderRadius * (0.3 + 0.7 * _random.NextDouble());
            FlyTo(new Vec3d(Math.Sin(angle) * far, 0, Math.Cos(angle) * far));
        }

        // ------------------------------------------------------------------ in the air

        void FlyTo(Vec3d target)
        {
            Enter(ScreamerMode.Fly);
            _target = target;
            _flyFor = 0.0;
            _blockedCount = 0;
        }

        void Fly(ScreamerBody body, in ScreamerSenses senses, ref ScreamerOrders orders, bool hunting, double dt)
        {
            Height = Math.Min(_rules.cruiseHeight, Height + _rules.climbRate * dt);
            _flyFor += dt;
            Vec3d to = FromWorld(senses, _target);
            to = new Vec3d(to.X, 0, to.Z);
            double far = to.Magnitude, bearing = Math.Atan2(to.X, to.Z);
            if (senses.Blocked)
            {
                // Something in the way: try the other side; if that fails too, come down where it is.
                _blockedCount++;
                _side = -_side;
                if (_blockedCount > 3)
                {
                    Enter(ScreamerMode.Land);
                    return;
                }
                _target = hunting && body.CanScream ? PerchNear(senses) : ToWorld(senses, new Vec3d(-to.X, 0, -to.Z).Normalized * 4.0);
                orders.TurnRate = _side * _rules.flyTurn;
                return;
            }
            orders.TurnRate = Toward(bearing, _rules.flyTurn);
            // It slows as it nears the place, and while it is turned away from it, so it never circles it for good.
            double pace = _rules.flyPace * body.FlightShare * (Math.Abs(bearing) < 0.9 ? 1.0 : 0.45);
            pace = Math.Min(pace, far * 1.6 + 1.0);
            // It rises a little before it sets off.
            if (Height < 0.5) pace *= 0.3;
            orders.Velocity = new Vec3d(0, 0, pace);
            _drift = pace;
            if (far < 1.2 || _flyFor > 9.0) Enter(ScreamerMode.Land);
        }

        /// <summary>A place to land: its usual distance from the player, round to one side of where it is now.</summary>
        Vec3d PerchNear(in ScreamerSenses senses)
        {
            Vec3d player = ToWorld(senses, senses.Player), me = ToWorld(senses, Vec3d.Zero);
            Vec3d from = new Vec3d(me.X - player.X, 0, me.Z - player.Z);
            from = from.SqrMagnitude > 0.01 ? from.Normalized : Vec3d.Forward;
            if (_random.NextDouble() < 0.35) _side = -_side;
            double angle = _side * (0.5 + 0.6 * _random.NextDouble());
            Vec3d way = from.RotatedAround(Vec3d.Up, angle);
            return Roam(player + way * _rules.perchRange);
        }

        /// <summary>A place well away from the player, on its own side of them.</summary>
        Vec3d Away(in ScreamerSenses senses)
        {
            Vec3d player = ToWorld(senses, senses.Player), me = ToWorld(senses, Vec3d.Zero);
            Vec3d from = new Vec3d(me.X - player.X, 0, me.Z - player.Z);
            from = from.SqrMagnitude > 0.01 ? from.Normalized : Vec3d.Forward;
            return Roam(player + from.RotatedAround(Vec3d.Up, _side * 0.4) * _rules.fleeRange);
        }

        Vec3d Roam(Vec3d place)
        {
            place = new Vec3d(place.X, 0, place.Z);
            double far = place.Magnitude;
            return far > _rules.roamRadius ? place * (_rules.roamRadius / far) : place;
        }

        // ------------------------------------------------------------------ keeping a place in mind

        static Vec3d North(in ScreamerSenses senses)
        {
            var n = new Vec3d(senses.North.X, 0, senses.North.Z);
            return n.SqrMagnitude > 1e-9 ? n.Normalized : Vec3d.Forward;
        }

        /// <summary>A point in creature space, as metres east (X) and north (Z) of home.</summary>
        public static Vec3d ToWorld(in ScreamerSenses senses, Vec3d point)
        {
            Vec3d n = North(senses), e = new Vec3d(n.Z, 0, -n.X), d = point - senses.Home;
            return new Vec3d(Vec3d.Dot(d, e), 0, Vec3d.Dot(d, n));
        }

        public static Vec3d FromWorld(in ScreamerSenses senses, Vec3d world)
        {
            Vec3d n = North(senses), e = new Vec3d(n.Z, 0, -n.X);
            return senses.Home + e * world.X + n * world.Z;
        }

        static double Wrap(double angle)
        {
            while (angle > Math.PI) angle -= 2.0 * Math.PI;
            while (angle < -Math.PI) angle += 2.0 * Math.PI;
            return angle;
        }

        /// <summary>A turn rate that closes a bearing without swinging past it.</summary>
        static double Toward(double bearing, double rate) => Math.Max(-rate, Math.Min(rate, bearing * 4.0));
    }
}
