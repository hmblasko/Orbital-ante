using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public enum AmbusherMode
    {
        /// <summary>Down its hole with its head at the mouth of it: jaws lying open on the ground, feelers up.</summary>
        Lurk,
        /// <summary>Its head rises out of the hole and draws back, jaws wide: the warning.</summary>
        Coil,
        /// <summary>The body shoots out along the ground.</summary>
        Strike,
        /// <summary>It has someone in its jaws and is dragging them to its hole.</summary>
        Hold,
        /// <summary>It missed: its body lies stretched out on the ground. The time to shoot it through.</summary>
        Spent,
        /// <summary>It pulls itself back down the hole.</summary>
        Withdraw,
        /// <summary>Wholly under the ground, where nothing can reach it.</summary>
        Under,
        /// <summary>It moves under the ground toward the footsteps it feels; a low mound shows where.</summary>
        Tunnel,
        /// <summary>The ground swells and its head comes up.</summary>
        Rise,
    }

    /// <summary>How the Ambusher behaves: every number a designer may want to change. On the CreatureConfig asset.</summary>
    [Serializable]
    public sealed class AmbusherRules
    {
        /// <summary>From how far its head reaches, in metres.</summary>
        public float strikeRange = 4.6f;
        /// <summary>From how far it feels someone walking, whole (less for every feeler lost, and by how loud they are).</summary>
        public float feelRange = 9f;
        /// <summary>Anyone this near is felt however quietly they tread.</summary>
        public float closeRange = 1.6f;
        /// <summary>From how far it follows footsteps under the ground.</summary>
        public float huntRange = 26f;
        /// <summary>Radians a second it swings round to face what it feels.</summary>
        public float aimTurn = 5f;
        /// <summary>Seconds its head stands out of the hole before it strikes: the warning.</summary>
        public float coilSeconds = 0.7f;
        public float strikeSeconds = 0.18f;
        /// <summary>Seconds it lies stretched out after a miss.</summary>
        public float spentSeconds = 1.0f;
        public float withdrawSeconds = 0.6f;
        /// <summary>Seconds it stays under before it comes up or moves on.</summary>
        public float underSeconds = 1.6f;
        public float riseSeconds = 0.9f;
        /// <summary>Seconds it waits, feeling someone it cannot reach, before it goes after them.</summary>
        public float restSeconds = 4f;
        /// <summary>Metres a second under the ground: slower than the player walks (5).</summary>
        public float tunnelPace = 4.2f;
        /// <summary>It comes up where someone on the move will be in this many seconds.</summary>
        public float ambushAhead = 1.4f;
        /// <summary>It comes up this far short of someone standing still.</summary>
        public float ambushOff = 3.2f;
        public float biteDamage = 14f;
        /// <summary>How near its jaws must land to bite (a person is about 0.4 m across).</summary>
        public float biteRadius = 0.75f;
        /// <summary>Hurt a second while it holds on, dealt in beats.</summary>
        public float holdDamage = 8f;
        public float holdBeat = 0.5f;
        /// <summary>It lets go by itself after this long.</summary>
        public float holdSeconds = 4f;
        /// <summary>Metres a second it drags whoever it holds toward its hole.</summary>
        public float dragPace = 2.6f;
        /// <summary>It drags no nearer its hole than this.</summary>
        public float holdAt = 1.8f;
        /// <summary>This much damage while it holds on makes it let go.</summary>
        public float letGoDamage = 40f;
        /// <summary>It never tunnels further than this from where it was first.</summary>
        public float roamRadius = 30f;
        public AmbusherHp hp = AmbusherHp.Default;
        /// <summary>Seconds a loose piece lies before it fades.</summary>
        public float pieceLife = 30f;
        /// <summary>Shared with the other creatures' loose pieces.</summary>
        public int maxLoosePieces = 34;
    }

    public struct AmbusherSenses
    {
        /// <summary>Someone is there to be hunted.</summary>
        public bool Hostile;
        /// <summary>The player's feet in creature space (x right, z the way it aims; the origin is its hole).</summary>
        public Vec3d Player;
        /// <summary>Creature space, metres a second.</summary>
        public Vec3d PlayerVelocity;
        /// <summary>How loud the player is: 1 walking, less sneaking, 0 silent, more for shots.</summary>
        public double Noise;
        /// <summary>Nothing solid stands between its hole and the player.</summary>
        public bool ClearLine;
        /// <summary>The ground over it is open: it can come up here.</summary>
        public bool ClearAbove;
        /// <summary>Where it was first, in creature space.</summary>
        public Vec3d Home;
    }

    public struct AmbusherOrders
    {
        /// <summary>Creature space, metres a second: only ever under the ground.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        public AmbusherMode Mode;
        /// <summary>How far through the mode it is, 0 to 1.</summary>
        public double T;
        /// <summary>How far from its hole its head is, or is going, along the ground.</summary>
        public double Out;
        /// <summary>True on the one step its jaws close.</summary>
        public bool Landed;
        /// <summary>Hurt to deal to the player this step.</summary>
        public double Bite;
        /// <summary>It has the player in its jaws.</summary>
        public bool Holding;
        /// <summary>Creature space, metres a second: how the held player is dragged.</summary>
        public Vec3d Pull;
        /// <summary>It has no jaws left and has gone under for good.</summary>
        public bool Gone;
    }

    /// <summary>
    /// What an Ambusher decides, with no Unity types. It waits down its hole with its jaws open at the mouth of it.
    /// It feels footsteps: when someone it feels is within reach its head rises and draws back (the warning), and it
    /// follows them, aiming where they are going, until <see cref="LockAt"/> of the way through; then its jaws fly
    /// wide and it strikes at where they would have been: a change of direction after that is a miss (constitution
    /// rule 3). A hit with both jaws holds on and drags; a miss leaves its body stretched out on the ground, where
    /// any length of it can be shot through. Left feeling someone it cannot reach, it goes under and tunnels to come
    /// up ahead of them (rule 8).
    /// </summary>
    public sealed class AmbusherBrain
    {
        /// <summary>The share of the warning after which it no longer follows the player.</summary>
        public const double LockAt = 0.6;
        /// <summary>How far short of someone its head stops, so that its jaws close round them.</summary>
        public const double GripGap = 0.55;
        /// <summary>Seconds it must have been settled at the mouth of its hole before it can strike.</summary>
        public const double Settle = 0.4;
        /// <summary>Someone whose feet are higher than this above its ground is out of its reach.</summary>
        public const double TooHigh = 1.0;

        readonly AmbusherRules _rules;
        double _t, _aim, _turnLeft, _out, _held, _beat, _hurt, _restless, _tunnel, _lost, _pass;
        bool _locked, _goAfter;

        public AmbusherBrain(AmbusherRules rules)
        {
            _rules = rules ?? new AmbusherRules();
        }

        public AmbusherMode Mode { get; private set; } = AmbusherMode.Lurk;
        /// <summary>Under the ground, where nothing can hit it.</summary>
        public bool Buried => Mode == AmbusherMode.Under || Mode == AmbusherMode.Tunnel;
        public bool Holding => Mode == AmbusherMode.Hold;

        /// <summary>From how far it feels someone this loud walking.</summary>
        public static double FeelRange(AmbusherRules rules, AmbusherBody body, double noise) =>
            Math.Max(rules.closeRange, rules.feelRange * body.FeelShare * Math.Max(0.0, noise));

        /// <summary>From how far it follows someone this loud under the ground.</summary>
        public static double HuntRange(AmbusherRules rules, AmbusherBody body, double noise) =>
            Math.Max(rules.closeRange, rules.huntRange * body.FeelShare * Math.Max(0.0, noise));

        /// <summary>
        /// It has lost a jaw: a warning under way is called off and anyone it holds is let go. True if that stopped
        /// something.
        /// </summary>
        public bool Flinch()
        {
            if (Mode != AmbusherMode.Coil && Mode != AmbusherMode.Hold) return false;
            Enter(AmbusherMode.Withdraw);
            _goAfter = false;
            return true;
        }

        /// <summary>
        /// It has been hurt. Holding on, enough of it makes it let go; waiting at the mouth of its hole, it ducks
        /// under and goes after whoever did it.
        /// </summary>
        public void Hurt(double damage)
        {
            if (Mode == AmbusherMode.Hold) _hurt += Math.Max(0.0, damage);
            else if (Mode == AmbusherMode.Lurk)
            {
                _out = 0.0;
                Enter(AmbusherMode.Withdraw);
                _goAfter = true;
            }
        }

        /// <summary>Is someone whose feet are here (creature space) between its jaws when its head is this far out?</summary>
        public bool BiteHits(Vec3d playerFeet, double headOut)
        {
            double z = playerFeet.Z - (headOut + GripGap);
            return Math.Sqrt(playerFeet.X * playerFeet.X + z * z) < _rules.biteRadius && Math.Abs(playerFeet.Y) < TooHigh;
        }

        void Enter(AmbusherMode mode)
        {
            Mode = mode;
            _t = 0.0;
        }

        public AmbusherOrders Think(AmbusherBody body, in AmbusherSenses senses, double dt)
        {
            var orders = new AmbusherOrders { Mode = Mode };
            if (!body.Alive) return orders;
            var flat = new Vec3d(senses.Player.X, 0, senses.Player.Z);
            double distance = flat.Magnitude;
            double bearing = Math.Atan2(flat.X, flat.Z);
            bool level = Math.Abs(senses.Player.Y) < TooHigh;
            bool felt = senses.Hostile && distance < FeelRange(_rules, body, senses.Noise);
            bool followed = senses.Hostile && distance < HuntRange(_rules, body, senses.Noise);
            double coil = Math.Max(0.2, _rules.coilSeconds), strike = Math.Max(0.05, _rules.strikeSeconds);

            switch (Mode)
            {
                case AmbusherMode.Lurk:
                    _t += dt;
                    _out = 0.0;
                    if (!body.CanBite)
                    {
                        Enter(AmbusherMode.Withdraw);
                        _goAfter = false;
                        break;
                    }
                    if (felt) orders.TurnRate = Toward(bearing, _rules.aimTurn);
                    if (felt && level && senses.ClearLine && _t >= Settle && distance < _rules.strikeRange - 0.3)
                    {
                        Enter(AmbusherMode.Coil);
                        _locked = false;
                        _aim = Reach(distance);
                        break;
                    }
                    // Someone it can feel but cannot reach: it waits a while, then goes after them.
                    _restless = followed && distance >= _rules.strikeRange - 0.3 ? _restless + dt : 0.0;
                    if (_restless >= _rules.restSeconds)
                    {
                        Enter(AmbusherMode.Withdraw);
                        _goAfter = true;
                    }
                    break;

                case AmbusherMode.Coil:
                {
                    double before = _t;
                    _t += dt / coil;
                    if (!_locked)
                    {
                        // Where they will be when its jaws arrive, if they keep on as they are.
                        double ahead = Math.Max(0.0, 1.0 - _t) * coil + strike;
                        var lead = flat + new Vec3d(senses.PlayerVelocity.X, 0, senses.PlayerVelocity.Z) * (senses.Hostile ? ahead : 0.0);
                        double leadBearing = Math.Atan2(lead.X, lead.Z);
                        _aim = Reach(lead.Magnitude);
                        if (_t >= LockAt)
                        {
                            // From here it is aimed: it swings the rest of the way to that one place and no further.
                            _locked = true;
                            _turnLeft = leadBearing;
                        }
                        else if (senses.Hostile) orders.TurnRate = Toward(leadBearing, _rules.aimTurn);
                    }
                    if (_locked && dt > 0.0 && Math.Abs(_turnLeft) > 1e-6)
                    {
                        double left = Math.Max(dt, (1.0 - Math.Max(before, LockAt)) * coil);
                        double step = _turnLeft * Math.Min(1.0, dt / left);
                        orders.TurnRate = step / dt;
                        _turnLeft -= step;
                    }
                    _out = 0.0;
                    if (_t >= 1.0) Enter(AmbusherMode.Strike);
                    break;
                }

                case AmbusherMode.Strike:
                    _t += dt / strike;
                    _out = _aim * Math.Min(1.0, _t);
                    if (_t >= 1.0)
                    {
                        orders.Landed = true;
                        _out = _aim;
                        bool hit = senses.Hostile && senses.ClearLine && body.CanBite && BiteHits(senses.Player, _aim);
                        if (hit) orders.Bite = _rules.biteDamage * body.BiteShare;
                        if (hit && body.CanHold)
                        {
                            Enter(AmbusherMode.Hold);
                            _held = _beat = _hurt = 0.0;
                        }
                        else if (hit)
                        {
                            Enter(AmbusherMode.Withdraw);
                            _goAfter = false;
                        }
                        else Enter(AmbusherMode.Spent);
                    }
                    break;

                case AmbusherMode.Hold:
                {
                    _held += dt;
                    _beat += dt;
                    bool gone = !senses.Hostile || distance > _out + GripGap + 1.5;
                    if (gone || !body.CanHold || _hurt >= _rules.letGoDamage || _held >= _rules.holdSeconds)
                    {
                        Enter(AmbusherMode.Withdraw);
                        _goAfter = false;
                        break;
                    }
                    if (_beat >= _rules.holdBeat)
                    {
                        _beat -= _rules.holdBeat;
                        orders.Bite = _rules.holdDamage * _rules.holdBeat;
                    }
                    // Its head stays on whoever it holds; it hauls them in until they are at the mouth of its hole.
                    _out = Math.Max(0.5, distance - GripGap);
                    if (distance > _rules.holdAt && distance > 1e-6) orders.Pull = flat * (-_rules.dragPace / distance);
                    orders.TurnRate = Toward(bearing, _rules.aimTurn * 2.0);
                    orders.Holding = true;
                    _t = _held / Math.Max(0.1, _rules.holdSeconds);
                    break;
                }

                case AmbusherMode.Spent:
                    _t += dt / Math.Max(0.1, _rules.spentSeconds);
                    if (_t >= 1.0)
                    {
                        Enter(AmbusherMode.Withdraw);
                        _goAfter = false;
                    }
                    break;

                case AmbusherMode.Withdraw:
                    _t += dt / Math.Max(0.1, _rules.withdrawSeconds);
                    if (_t >= 1.0)
                    {
                        _out = 0.0;
                        Enter(AmbusherMode.Under);
                    }
                    break;

                case AmbusherMode.Under:
                    _t += dt / Math.Max(0.1, _rules.underSeconds);
                    _out = 0.0;
                    if (!body.CanBite)
                    {
                        // Nothing left to bite with: it stays down for good.
                        _t = Math.Min(_t, 1.0);
                        orders.Gone = true;
                        break;
                    }
                    if (_t < 1.0 && !_goAfter) break;
                    if (_t < 0.25) break;
                    bool inReach = felt && distance < _rules.strikeRange - 0.5;
                    if (followed && !inReach)
                    {
                        Enter(AmbusherMode.Tunnel);
                        _tunnel = _lost = _pass = 0.0;
                    }
                    else if (_t >= 1.0 && senses.ClearAbove) Enter(AmbusherMode.Rise);
                    _goAfter = false;
                    break;

                case AmbusherMode.Tunnel:
                {
                    _tunnel += dt;
                    _lost = followed ? 0.0 : _lost + dt;
                    _pass = Math.Max(0.0, _pass - dt);
                    Vec3d target = AmbushPoint(senses, flat, distance);
                    double far = target.Magnitude;
                    bool arrived = far < 0.5 && _pass <= 0.0;
                    if ((arrived || _lost > 3.0 || _tunnel > 10.0) && senses.ClearAbove)
                    {
                        Enter(AmbusherMode.Rise);
                        break;
                    }
                    // Something stands over the place: it goes on a little way and tries again.
                    if (arrived) _pass = 0.6;
                    if (_pass > 0.0) target = flat;
                    far = target.Magnitude;
                    if (far > 1e-6 && _lost <= 3.0)
                    {
                        orders.Velocity = target * (Math.Min(_rules.tunnelPace, far / Math.Max(dt, 1e-6)) / far);
                        orders.TurnRate = Toward(Math.Atan2(target.X, target.Z), 6.0);
                    }
                    _t = Math.Min(1.0, _tunnel / 10.0);
                    break;
                }

                case AmbusherMode.Rise:
                    _t += dt / Math.Max(0.1, _rules.riseSeconds);
                    if (felt) orders.TurnRate = Toward(bearing, _rules.aimTurn);
                    if (_t >= 1.0)
                    {
                        Enter(AmbusherMode.Lurk);
                        _restless = 0.0;
                    }
                    break;
            }
            orders.Mode = Mode;
            orders.T = Mode == AmbusherMode.Lurk ? 0.0 : Math.Min(1.0, _t);
            orders.Out = _out;
            return orders;
        }

        /// <summary>How far out its head goes for someone this far off: short of them by its jaws, and never past its reach.</summary>
        double Reach(double distance) => Math.Max(0.8, Math.Min(_rules.strikeRange, distance - GripGap));

        /// <summary>
        /// Where it means to come up, in creature space: ahead of someone on the move, short of someone standing
        /// still, and never further from where it was first than it roams.
        /// </summary>
        Vec3d AmbushPoint(in AmbusherSenses senses, Vec3d player, double distance)
        {
            var velocity = new Vec3d(senses.PlayerVelocity.X, 0, senses.PlayerVelocity.Z);
            Vec3d target;
            if (velocity.Magnitude > 1.5) target = player + velocity * _rules.ambushAhead;
            else if (distance > _rules.ambushOff) target = player * ((distance - _rules.ambushOff) / distance);
            else target = Vec3d.Zero;
            var home = new Vec3d(senses.Home.X, 0, senses.Home.Z);
            Vec3d fromHome = target - home;
            double away = fromHome.Magnitude;
            if (away > _rules.roamRadius) target = home + fromHome * (_rules.roamRadius / away);
            return target;
        }

        /// <summary>A turn rate that closes a bearing without swinging past it.</summary>
        static double Toward(double bearing, double rate) => Math.Max(-rate, Math.Min(rate, bearing * 6.0));
    }
}
