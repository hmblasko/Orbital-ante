using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public enum MawMode
    {
        /// <summary>Cruising a slow circle round where it lives, mouth ajar.</summary>
        Drift,
        /// <summary>It has noticed someone in the water and is coming, slowly.</summary>
        Approach,
        /// <summary>The warning: all but stopped, its mouth opening. Part way through it stops following and is aimed.</summary>
        Gape,
        /// <summary>Mouth as wide as it goes: it draws the water in front of it, and whoever is in that water, down its throat.</summary>
        Inhale,
        /// <summary>It has someone in its mouth: it bites in beats, then spits them out.</summary>
        Chew,
        /// <summary>Mouth shut, hanging in the water getting its breath back: the opening.</summary>
        Spent,
        /// <summary>Swimming off and round before it comes in again.</summary>
        Circle,
        /// <summary>Shot down the throat: it chokes and thrashes, and whatever it was doing is over.</summary>
        Reel,
        /// <summary>Hurt by someone it cannot reach (out of the water): it swims away and down.</summary>
        Shy,
        /// <summary>Its jaw is gone: it makes off, once, and after that only drifts.</summary>
        Flee,
    }

    [Serializable]
    public sealed class MawRules
    {
        /// <summary>From how far it notices someone in the water.</summary>
        public float noticeRange = 34f;
        /// <summary>It never goes after anyone further than this from where it lives.</summary>
        public float roamRadius = 55f;
        /// <summary>How wide the circle it cruises is.</summary>
        public float driftRadius = 14f;
        /// <summary>Metres a second. In the suit the player's jets move them at a few metres a second in water.</summary>
        public float driftPace = 1.4f;
        public float approachPace = 3.2f;
        public float circlePace = 3.8f;
        /// <summary>Up and down, metres a second.</summary>
        public float climbPace = 1.6f;
        /// <summary>Radians a second: it is slow to come round, so getting behind it is safe.</summary>
        public float turn = 0.75f;
        /// <summary>The warning before it draws in. It takes aim at <see cref="MawBrain.LockAt"/> of the way through.</summary>
        public float gapeSeconds = 1.4f;
        public float inhaleSeconds = 2.6f;
        /// <summary>How far in front of its mouth the pull reaches, and half the angle of the cone it pulls in.</summary>
        public float pullReach = 13f;
        public float pullCone = 0.5f;
        /// <summary>Metres a second squared at its mouth; about a third of that at the edge of its reach.</summary>
        public float pullAccel = 9f;
        /// <summary>Someone this near the middle of its mouth is in it.</summary>
        public float mouthRadius = 1.0f;
        public float holdSeconds = 1.2f;
        public int chewBeats = 3;
        public float chewDamage = 10f;
        /// <summary>Metres a second it spits someone out at.</summary>
        public float spitSpeed = 9f;
        /// <summary>With no jaw it cannot hold: someone drawn into its mouth is only knocked.</summary>
        public float bumpDamage = 6f;
        public float recoverSeconds = 2.4f;
        public float circleSeconds = 5f;
        /// <summary>This much damage down its throat in one warning, pull or hold makes it choke.</summary>
        public float chokeDamage = 150f;
        public float reelSeconds = 1.3f;
        /// <summary>After choking it cannot be made to choke again for this long.</summary>
        public float steadySeconds = 4f;
        public float shySeconds = 6f;
        public float fleeSeconds = 12f;
        /// <summary>It keeps at least this much water over its back and under its belly.</summary>
        public float keepUnder = 2.5f, keepOff = 2.5f;
        public MawHp hp = MawHp.Default;
        /// <summary>How long a piece shot off drifts there.</summary>
        public float pieceLife = 30f;
        /// <summary>Loose pieces of all creatures together.</summary>
        public int maxLoosePieces = 34;
    }

    public struct MawSenses
    {
        /// <summary>There is a living player to go after.</summary>
        public bool Hostile;
        /// <summary>The player is in the water (it takes no notice of anyone who is not).</summary>
        public bool PlayerInWater;
        /// <summary>Where the player is, in creature space.</summary>
        public Vec3d Player;
        /// <summary>Where it lives, in creature space.</summary>
        public Vec3d Home;
        /// <summary>Metres of water over its head, and from its head down to the sea floor.</summary>
        public double WaterAbove, FloorBelow;
        /// <summary>Something solid is right in front of it.</summary>
        public bool Blocked;
    }

    public struct MawOrders
    {
        /// <summary>Metres a second along its nose. It never goes backward: its body follows its head.</summary>
        public double Speed;
        /// <summary>Radians a second about up.</summary>
        public double TurnRate;
        /// <summary>Metres a second up.</summary>
        public double Climb;
        public MawMode Mode;
        /// <summary>How far through the mode it is, 0 to 1.</summary>
        public double T;
        /// <summary>The pull on the player this frame, metres a second squared, in creature space (zero outside its cone).</summary>
        public Vec3d Pull;
        /// <summary>It has the player in its mouth: they go where its mouth goes.</summary>
        public bool Holding;
        /// <summary>Damage done to the player this frame.</summary>
        public double Bite;
        /// <summary>On the frame it lets the player go: the speed it throws them at, in creature space.</summary>
        public Vec3d Spit;
        /// <summary>It knows where the player is this frame.</summary>
        public bool Aware;
        /// <summary>It has taken aim: it no longer follows.</summary>
        public bool Locked;
    }

    /// <summary>
    /// A Drifting maw's mind. Plain C#, no Unity types.
    ///
    /// It cruises a slow circle with its mouth ajar and takes notice only of someone in the water. It comes slowly,
    /// stops, and opens its mouth (<see cref="MawMode.Gape"/>): until <see cref="LockAt"/> it turns to follow, then
    /// its mouth falls fully wide and it is aimed. Then it draws in (<see cref="MawMode.Inhale"/>): a cone of water in
    /// front of its mouth is pulled toward it, harder the nearer, and it can only swing round slowly, so the way out
    /// is sideways. Whoever reaches its mouth is held, bitten in beats and spat out. Then it hangs there, spent.
    /// A shot down its open throat does more than anywhere else, and enough of them make it choke and let go.
    /// </summary>
    public sealed class MawBrain
    {
        /// <summary>How far through the warning it stops following and is aimed.</summary>
        public const double LockAt = 0.6;
        /// <summary>How quickly it can still swing round while it draws in, beside its ordinary turn.</summary>
        public const double InhaleTurn = 0.2;
        /// <summary>Hurt by someone in the water, it notices them from this many times as far, for <see cref="ProvokedSeconds"/>.</summary>
        public const double ProvokedReach = 2.2, ProvokedSeconds = 12.0;
        /// <summary>The share of its full pull felt at the very edge of its reach.</summary>
        public const double EdgeShare = 0.35;

        readonly MawRules _rules;
        double _t, _throat, _sinceReel = 100.0, _circleSign = 1.0, _provoked;
        int _beats;
        bool _fled, _harmless;

        public MawBrain(MawRules rules)
        {
            _rules = rules ?? new MawRules();
        }

        public MawMode Mode { get; private set; } = MawMode.Drift;
        /// <summary>Its jaw is gone and it has made off: it only drifts now.</summary>
        public bool Harmless => _harmless;

        static double Clamp(double x, double lo, double hi) => x < lo ? lo : x > hi ? hi : x;

        void Go(MawMode mode)
        {
            Mode = mode;
            _t = 0.0;
            if (mode == MawMode.Gape) _throat = 0.0;
            if (mode == MawMode.Chew) _beats = 0;
            if (mode == MawMode.Circle) _circleSign = -_circleSign;
        }

        /// <summary>Damage went down its throat (from <see cref="MawHit.Throat"/>). True if that made it choke.</summary>
        public bool Throat(double damage)
        {
            if (Mode != MawMode.Gape && Mode != MawMode.Inhale && Mode != MawMode.Chew) return false;
            _throat += Math.Max(0.0, damage);
            if (_throat < _rules.chokeDamage || _sinceReel < _rules.steadySeconds) return false;
            _sinceReel = 0.0;
            Go(MawMode.Reel);
            return true;
        }

        /// <summary>It was hurt. <paramref name="reachable"/>: by someone in the water with it.</summary>
        public void Hurt(bool reachable)
        {
            if (_harmless) return;
            if (reachable) _provoked = ProvokedSeconds;
            if (Mode == MawMode.Drift) Go(reachable ? MawMode.Approach : MawMode.Shy);
        }

        /// <summary>Its jaw came off: whoever it holds is free, and it makes off.</summary>
        public void JawLost()
        {
            if (!_fled) Go(MawMode.Flee);
            _fled = true;
        }

        /// <summary>The pull at a place in creature space: zero outside the cone, toward its mouth inside it.</summary>
        public static Vec3d PullAt(Vec3d place, MawRules rules, double share)
        {
            Vec3d d = place - MawLayout.Mouth;
            double dist = d.Magnitude;
            if (dist > rules.pullReach || dist < 1e-6) return Vec3d.Zero;
            // Right at its lips the cone is the whole mouth.
            bool inCone = dist < rules.mouthRadius * 1.5 || (d.Z > 0.0 && Math.Acos(Clamp(d.Z / dist, -1.0, 1.0)) < rules.pullCone);
            if (!inCone) return Vec3d.Zero;
            double strength = rules.pullAccel * share * (EdgeShare + (1.0 - EdgeShare) * (1.0 - dist / rules.pullReach));
            return d * (-strength / dist);
        }

        /// <summary>Steer toward a direction in creature space.</summary>
        void Steer(Vec3d way, double pace, double turnShare, MawBody body, ref MawOrders orders)
        {
            double yaw = Math.Atan2(way.X, way.Z);
            double most = _rules.turn * body.TurnShare * turnShare;
            orders.TurnRate = Clamp(yaw * 2.0, -most, most);
            orders.Speed = pace * body.PaceShare;
            double flat = Math.Sqrt(way.X * way.X + way.Z * way.Z);
            orders.Climb = Clamp(way.Y / Math.Max(1.0, flat) * pace * 1.5, -_rules.climbPace, _rules.climbPace);
        }

        public MawOrders Think(double dt, in MawSenses senses, MawBody body)
        {
            var orders = new MawOrders();
            if (body == null || !body.Alive)
            {
                orders.Mode = Mode;
                return orders;
            }
            _t += dt;
            _sinceReel += dt;
            _provoked = Math.Max(0.0, _provoked - dt);
            double notice = _rules.noticeRange * (_provoked > 0.0 ? ProvokedReach : 1.0);

            Vec3d toPlayer = senses.Player;
            double far = toPlayer.Magnitude;
            Vec3d fromMouth = senses.Player - MawLayout.Mouth;
            double homeFar = (senses.Player - senses.Home).Magnitude;
            bool there = senses.Hostile && senses.PlayerInWater;
            bool noticed = there && !_harmless && far < notice && homeFar < _rules.roamRadius;
            orders.Aware = there && far < notice;
            if (!body.JawOn && !_fled) JawLost();

            switch (Mode)
            {
                case MawMode.Drift:
                {
                    // Round where it lives: along the circle, and in or out toward it.
                    Vec3d out_ = new Vec3d(-senses.Home.X, 0, -senses.Home.Z);
                    double r = out_.Magnitude;
                    Vec3d radial = r > 0.01 ? out_ / r : Vec3d.Forward;
                    Vec3d along = Vec3d.Cross(Vec3d.Up, radial) * _circleSign;
                    Vec3d way = along + radial * Clamp((_rules.driftRadius - r) / _rules.driftRadius, -1.0, 1.0);
                    way.Y = Clamp(senses.Home.Y * 0.15, -0.4, 0.4);
                    Steer(way, _rules.driftPace, 1.0, body, ref orders);
                    if (noticed) Go(MawMode.Approach);
                    break;
                }
                case MawMode.Approach:
                {
                    Steer(toPlayer, _rules.approachPace, 1.0, body, ref orders);
                    double yaw = Math.Abs(Math.Atan2(fromMouth.X, fromMouth.Z));
                    if (!noticed && (!there || far > notice * 1.4 || homeFar > _rules.roamRadius)) Go(MawMode.Drift);
                    else if (fromMouth.Magnitude < _rules.pullReach * 0.75 && yaw < 0.35) Go(MawMode.Gape);
                    break;
                }
                case MawMode.Gape:
                {
                    orders.T = Math.Min(1.0, _t / _rules.gapeSeconds);
                    orders.Locked = orders.T >= LockAt;
                    if (!orders.Locked)
                    {
                        Steer(toPlayer, 0.4, 1.0, body, ref orders);
                        // They got out of the water or out of reach before it took aim: it calls it off.
                        if (!there) { Go(MawMode.Drift); break; }
                        if (fromMouth.Magnitude > _rules.pullReach * 1.15) { Go(MawMode.Approach); break; }
                    }
                    else orders.Speed = 0.4 * body.PaceShare;
                    if (_t >= _rules.gapeSeconds) Go(MawMode.Inhale);
                    break;
                }
                case MawMode.Inhale:
                {
                    orders.T = Math.Min(1.0, _t / _rules.inhaleSeconds);
                    orders.Locked = true;
                    // It drifts onto what it is drawing in, and can only swing round slowly.
                    if (there) Steer(toPlayer, 0.6, InhaleTurn, body, ref orders);
                    else orders.Speed = 0.6 * body.PaceShare;
                    if (there)
                    {
                        orders.Pull = PullAt(senses.Player, _rules, body.PullShare);
                        if (fromMouth.Magnitude < _rules.mouthRadius)
                        {
                            if (body.Bites) { Go(MawMode.Chew); orders.Pull = Vec3d.Zero; break; }
                            // No jaw to hold with: they are knocked and thrown clear.
                            orders.Bite = _rules.bumpDamage;
                            orders.Spit = Vec3d.Forward * (_rules.spitSpeed * 0.6);
                            orders.Pull = Vec3d.Zero;
                            Go(MawMode.Spent);
                            break;
                        }
                    }
                    if (_t >= _rules.inhaleSeconds) Go(MawMode.Spent);
                    break;
                }
                case MawMode.Chew:
                {
                    orders.T = Math.Min(1.0, _t / _rules.holdSeconds);
                    orders.Locked = true;
                    orders.Holding = true;
                    orders.Speed = 0.3 * body.PaceShare;
                    int beats = Math.Max(1, _rules.chewBeats);
                    // A bite at the middle of each beat.
                    int due = (int)Math.Floor(orders.T * beats + 0.5);
                    if (due > beats) due = beats;
                    if (due > _beats)
                    {
                        orders.Bite = _rules.chewDamage * (due - _beats);
                        _beats = due;
                    }
                    if (!senses.Hostile || !body.Bites || _t >= _rules.holdSeconds)
                    {
                        orders.Holding = false;
                        orders.Spit = Vec3d.Forward * _rules.spitSpeed;
                        Go(MawMode.Spent);
                    }
                    break;
                }
                case MawMode.Spent:
                    orders.T = Math.Min(1.0, _t / _rules.recoverSeconds);
                    orders.Speed = 0.5 * body.PaceShare;
                    if (_t >= _rules.recoverSeconds) Go(_fled && !_harmless ? MawMode.Flee : MawMode.Circle);
                    break;
                case MawMode.Circle:
                {
                    orders.T = Math.Min(1.0, _t / _rules.circleSeconds);
                    // Away from them and round to one side.
                    Vec3d away = far > 0.01 ? new Vec3d(-toPlayer.X, 0, -toPlayer.Z).Normalized : Vec3d.Forward;
                    Vec3d way = there ? away + Vec3d.Cross(Vec3d.Up, away) * (0.8 * _circleSign) : Vec3d.Forward;
                    Steer(way, _rules.circlePace, 1.0, body, ref orders);
                    if (_t >= _rules.circleSeconds) Go(noticed ? MawMode.Approach : MawMode.Drift);
                    break;
                }
                case MawMode.Reel:
                    orders.T = Math.Min(1.0, _t / _rules.reelSeconds);
                    orders.TurnRate = _rules.turn * 1.5 * Math.Sin(_t * 9.0);
                    if (_t >= _rules.reelSeconds) Go(_fled && !_harmless ? MawMode.Flee : MawMode.Circle);
                    break;
                case MawMode.Shy:
                case MawMode.Flee:
                {
                    double length = Mode == MawMode.Shy ? _rules.shySeconds : _rules.fleeSeconds;
                    orders.T = Math.Min(1.0, _t / length);
                    Vec3d away = far > 0.01 ? new Vec3d(-toPlayer.X, 0, -toPlayer.Z).Normalized : Vec3d.Forward;
                    away.Y = -0.5;
                    Steer(senses.Hostile ? away : Vec3d.Forward, _rules.circlePace, 1.0, body, ref orders);
                    if (_t >= length)
                    {
                        if (Mode == MawMode.Flee) _harmless = true;
                        Go(MawMode.Drift);
                    }
                    break;
                }
            }

            // It keeps water over its back and under its belly, whatever it is doing.
            if (senses.WaterAbove < _rules.keepUnder) orders.Climb = Math.Min(orders.Climb, -(_rules.keepUnder - senses.WaterAbove) * 0.8);
            if (senses.FloorBelow < _rules.keepOff) orders.Climb = Math.Max(orders.Climb, (_rules.keepOff - senses.FloorBelow) * 0.8);
            if (senses.WaterAbove < _rules.keepUnder && senses.FloorBelow < _rules.keepOff) orders.Climb = 0.0;
            // Something solid ahead: it turns aside instead of pushing into it.
            if (senses.Blocked && !orders.Holding)
            {
                orders.Speed = Math.Min(orders.Speed, 0.3);
                if (Mode != MawMode.Gape && Mode != MawMode.Inhale) orders.TurnRate = _rules.turn * body.TurnShare * _circleSign;
            }
            orders.Mode = Mode;
            return orders;
        }
    }
}
