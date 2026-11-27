using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public enum BiterMode
    {
        /// <summary>Swimming round where the pack lives, with the others.</summary>
        Shoal,
        /// <summary>Circling someone in the water, keeping its distance, waiting its turn.</summary>
        Ring,
        /// <summary>The warning: it turns in, its jaws flare and its flank flashes. Part way through it stops following and is aimed.</summary>
        Mark,
        /// <summary>A straight dash along its nose, through where they were; it bites anything at its mouth on the way.</summary>
        Dart,
        /// <summary>Peeling away after a dart, back out to the ring.</summary>
        Peel,
        /// <summary>Shot: it bolts a short way and comes back.</summary>
        Scatter,
        /// <summary>No tail to dart with: it hangs at the edge of the ring and never takes a turn.</summary>
        Limp,
        /// <summary>No jaw, or the last of its pack: it makes off and stays away.</summary>
        Flee,
    }

    [Serializable]
    public sealed class BiterRules
    {
        /// <summary>From how far a pack notices someone in the water (one notices, all know).</summary>
        public float noticeRange = 30f;
        /// <summary>It never goes after anyone further than this from where the pack lives.</summary>
        public float roamRadius = 60f;
        public float shoalRadius = 7f;
        /// <summary>How far from the player it circles while it waits.</summary>
        public float ringRadius = 7.5f;
        /// <summary>Metres a second. The suit's jets move the player a few metres a second; it swims quicker, and darts far quicker.</summary>
        public float shoalPace = 2.2f, ringPace = 4.2f, dartPace = 13f, peelPace = 6f;
        public float climbPace = 2.5f;
        /// <summary>Radians a second.</summary>
        public float turn = 2.6f;
        /// <summary>The warning before a dart. It takes aim at <see cref="BiterBrain.LockAt"/> of the way through.</summary>
        public float markSeconds = 0.9f;
        /// <summary>Until it takes aim it aims at where the player will be this many seconds on, so drifting is not a way out; changing way after the lock still is.</summary>
        public float leadSeconds = 0.5f;
        /// <summary>A dart runs this far past where they were when it took aim, at most <see cref="dartReachMost"/> in all.</summary>
        public float dartOvershoot = 3f, dartReachMost = 16f;
        /// <summary>Someone this near the front of its mouth during a dart is bitten, once a dart.</summary>
        public float biteReach = 0.9f;
        public float biteDamage = 12f;
        public float peelSeconds = 2.2f;
        /// <summary>At most one of a pack is warning or darting at a time; the next may start its warning this long after the last dart ends.</summary>
        public float turnGap = 1.4f;
        public float scatterSeconds = 1.1f;
        /// <summary>It keeps at least this much water over its back and under its belly.</summary>
        public float keepUnder = 1.5f, keepOff = 1.5f;
        public BiterHp hp = BiterHp.Default;
        public float pieceLife = 25f;
        public int maxLoosePieces = 34;
    }

    public struct BiterSenses
    {
        public bool Hostile;
        public bool PlayerInWater;
        /// <summary>Where the player is, in creature space.</summary>
        public Vec3d Player;
        /// <summary>How the player is moving, metres a second, in creature space (directions only).</summary>
        public Vec3d PlayerVelocity;
        /// <summary>Where the pack lives, in creature space.</summary>
        public Vec3d Home;
        public double WaterAbove, FloorBelow;
        public bool Blocked;
    }

    public struct BiterOrders
    {
        /// <summary>Metres a second along its nose; never backward.</summary>
        public double Speed;
        public double TurnRate;
        public double Climb;
        public BiterMode Mode;
        public double T;
        /// <summary>Damage done to the player this frame.</summary>
        public double Bite;
        public bool Aware;
        /// <summary>It has taken aim: it no longer follows.</summary>
        public bool Locked;
    }

    /// <summary>
    /// The pack: who has the turn. Plain C#. At most one member is warning or darting at a time, the next turn comes
    /// <see cref="BiterRules.turnGap"/> after the last dart ended, and it goes to whichever waiting member faces the
    /// player best. So the danger is always one fish at a time, each with its own warning: the pack is a rhythm to
    /// read, never a pile-on. The last of a pack of three or more makes off.
    /// </summary>
    public sealed class BiterPack
    {
        readonly List<BiterBrain> _members = new List<BiterBrain>();
        readonly List<BiterBrain> _armed = new List<BiterBrain>();
        BiterBrain _striker;
        double _time, _nextTurn;
        BiterBrain _best;
        double _bestScore = double.MaxValue;

        public int Started { get; private set; }
        public int Living { get; private set; }
        /// <summary>Someone in the pack has noticed the player; the rest know it too.</summary>
        public bool Alerted { get; set; }
        /// <summary>The pack is down to its last fish that can still bite (of three or more): it makes off.</summary>
        public bool Broken => Started >= 3 && Math.Min(Living, _armed.Count) <= 1;

        /// <summary>A member says whether it can still dart and bite (tail and jaw on).</summary>
        public void Armed(BiterBrain brain, bool canStrike)
        {
            if (brain == null || !_members.Contains(brain)) return;
            bool listed = _armed.Contains(brain);
            if (canStrike && !listed) _armed.Add(brain);
            else if (!canStrike && listed) _armed.Remove(brain);
        }
        public BiterBrain Striker => _striker;
        public int Count => _members.Count;

        public void Join(BiterBrain brain)
        {
            if (brain == null || _members.Contains(brain)) return;
            _members.Add(brain);
            _armed.Add(brain);
            Started++;
            Living++;
        }

        /// <summary>A member died: it gives up its turn and the pack is smaller.</summary>
        public void Lost(BiterBrain brain)
        {
            if (brain == null || !_members.Remove(brain)) return;
            _armed.Remove(brain);
            Living = Math.Max(0, Living - 1);
            if (_striker == brain) EndTurn(brain);
        }

        /// <summary>Once a frame, before the members think.</summary>
        public void Advance(double dt)
        {
            _time += Math.Max(0.0, dt);
            // The best bid of the last frame wins the turn.
            if (_striker == null && _best != null && _time >= _nextTurn)
            {
                _striker = _best;
                _best.GiveTurn();
            }
            _best = null;
            _bestScore = double.MaxValue;
        }

        /// <summary>A member in the ring asks for the turn; lower scores (better placed) win at the next frame.</summary>
        public void Bid(BiterBrain brain, double score)
        {
            if (_striker != null || _time < _nextTurn) return;
            if (score < _bestScore) { _bestScore = score; _best = brain; }
        }

        public void EndTurn(BiterBrain brain)
        {
            if (_striker != brain) return;
            _striker = null;
            _nextTurn = _time + brain.Rules.turnGap;
        }
    }

    /// <summary>
    /// A Pack biter's mind. Plain C#, no Unity types.
    ///
    /// The pack swims round where it lives and takes notice only of someone in the water. Then they ring them, each
    /// at its own place round the circle, and take turns: the one given the turn by <see cref="BiterPack"/> turns in,
    /// flares its jaws and flashes its flank (<see cref="BiterMode.Mark"/>); until <see cref="LockAt"/> it follows,
    /// then it is aimed and darts straight along its nose through where they were, biting whatever is at its mouth, and
    /// peels away back to the ring. A step aside during the dart is a miss. A hit makes it scatter, which also ends its
    /// turn. Without its tail it cannot dart; without its jaw it makes off; the last of a pack makes off too.
    /// </summary>
    public sealed class BiterBrain
    {
        public const double LockAt = 0.6;
        /// <summary>How far round the ring one member's place is from the next's, radians.</summary>
        public const double SlotSpread = 1.4;

        readonly BiterRules _rules;
        readonly BiterPack _pack;
        readonly double _slot;
        double _t, _dartLeft, _ringSign = 1.0, _sinceTurn = 100.0;
        bool _bitten, _turn;

        public BiterBrain(BiterRules rules, BiterPack pack = null, int place = 0)
        {
            _rules = rules ?? new BiterRules();
            _pack = pack;
            _slot = place * SlotSpread;
            _ringSign = place % 2 == 0 ? 1.0 : -1.0;
            pack?.Join(this);
        }

        public BiterRules Rules => _rules;
        public BiterMode Mode { get; private set; } = BiterMode.Shoal;
        public bool HasTurn => _turn;

        static double Clamp(double x, double lo, double hi) => x < lo ? lo : x > hi ? hi : x;

        void Go(BiterMode mode)
        {
            if (_turn && mode != BiterMode.Mark && mode != BiterMode.Dart)
            {
                _turn = false;
                _pack?.EndTurn(this);
            }
            Mode = mode;
            _t = 0.0;
            if (mode == BiterMode.Dart) _bitten = false;
            if (mode == BiterMode.Peel) _ringSign = -_ringSign;
        }

        /// <summary>The pack handed it the turn (see <see cref="BiterPack.Advance"/>).</summary>
        internal void GiveTurn()
        {
            _turn = true;
            _sinceTurn = 0.0;
            if (Mode == BiterMode.Ring) Go(BiterMode.Mark);
            else
            {
                _turn = false;
                _pack?.EndTurn(this);
            }
        }

        /// <summary>It was shot: it bolts (ending any turn) unless it is already making off or past its warning.</summary>
        public void Hurt()
        {
            if (_pack != null) _pack.Alerted = true;
            if (Mode == BiterMode.Flee || Mode == BiterMode.Dart) return;
            Go(BiterMode.Scatter);
        }

        /// <summary>Died: the pack is told.</summary>
        public void Died()
        {
            if (_turn) { _turn = false; _pack?.EndTurn(this); }
            _pack?.Lost(this);
        }

        void Steer(Vec3d way, double pace, BiterBody body, ref BiterOrders orders)
        {
            double yaw = Math.Atan2(way.X, way.Z);
            double most = _rules.turn * body.TurnShare;
            orders.TurnRate = Clamp(yaw * 3.0, -most, most);
            orders.Speed = pace * body.PaceShare;
            double flat = Math.Sqrt(way.X * way.X + way.Z * way.Z);
            orders.Climb = Clamp(way.Y / Math.Max(0.5, flat) * pace, -_rules.climbPace, _rules.climbPace);
        }

        /// <summary>The way to its own place on the ring round the player, in creature space.</summary>
        Vec3d RingWay(Vec3d player, double radius)
        {
            var flatTo = new Vec3d(player.X, 0, player.Z);
            double far = flatTo.Magnitude;
            Vec3d inward = far > 0.01 ? flatTo / far : Vec3d.Forward;
            Vec3d along = Vec3d.Cross(inward, Vec3d.Up) * _ringSign;
            // In when outside the ring, out when inside it, and always round.
            Vec3d way = along + inward * Clamp((far - radius) / Math.Max(1.0, radius * 0.5), -1.2, 1.2);
            way.Y = Clamp(player.Y * 0.4, -1.0, 1.0);
            return way;
        }

        public BiterOrders Think(double dt, in BiterSenses senses, BiterBody body)
        {
            var orders = new BiterOrders();
            if (body == null || !body.Alive)
            {
                orders.Mode = Mode;
                return orders;
            }
            _t += dt;
            _sinceTurn += dt;
            Vec3d toPlayer = senses.Player;
            double far = toPlayer.Magnitude;
            double homeFar = (senses.Player - senses.Home).Magnitude;
            bool there = senses.Hostile && senses.PlayerInWater && homeFar < _rules.roamRadius;
            bool alerted = _pack != null ? _pack.Alerted : false;
            bool noticed = there && (far < _rules.noticeRange || (alerted && far < _rules.noticeRange * 2.0));
            if (noticed && _pack != null) _pack.Alerted = true;
            if (!there && _pack != null && _pack.Striker == null) _pack.Alerted = false;
            orders.Aware = noticed;

            _pack?.Armed(this, body.CanStrike);
            if (Mode != BiterMode.Flee && (!body.JawOn || (_pack != null && _pack.Broken))) Go(BiterMode.Flee);
            else if (Mode != BiterMode.Flee && Mode != BiterMode.Limp && !body.TailFinOn && Mode != BiterMode.Scatter) Go(BiterMode.Limp);

            switch (Mode)
            {
                case BiterMode.Shoal:
                {
                    Vec3d out_ = new Vec3d(-senses.Home.X, 0, -senses.Home.Z);
                    double r = out_.Magnitude;
                    Vec3d radial = r > 0.01 ? out_ / r : Vec3d.Forward;
                    Vec3d along = Vec3d.Cross(Vec3d.Up, radial);
                    Vec3d way = along + radial * Clamp((_rules.shoalRadius - r) / _rules.shoalRadius, -1.0, 1.0);
                    way.Y = Clamp(senses.Home.Y * 0.2, -0.5, 0.5);
                    Steer(way, _rules.shoalPace, body, ref orders);
                    if (noticed) Go(BiterMode.Ring);
                    break;
                }
                case BiterMode.Ring:
                {
                    if (!noticed) { Go(BiterMode.Shoal); break; }
                    double radius = _rules.ringRadius * (1.0 + 0.12 * Math.Sin(_slot + _t * 0.4));
                    Steer(RingWay(toPlayer, radius), _rules.ringPace, body, ref orders);
                    // Ask for the turn: best placed is near the ring and already facing in.
                    // Only from near the ring: a warning given from too far to reach would teach that warnings lie.
                    if (_pack != null && body.CanStrike && far < _rules.ringRadius * 1.5)
                    {
                        // Whoever went last waits longest: every fish of the pack gets its turn.
                        double facing = Math.Abs(Math.Atan2(toPlayer.X, toPlayer.Z));
                        _pack.Bid(this, facing + Math.Abs(far - _rules.ringRadius) * 0.15 - Math.Min(_sinceTurn, 20.0) * 0.3);
                    }
                    else if (_pack == null && body.CanStrike && _t > _rules.turnGap + 1.0) { _turn = true; Go(BiterMode.Mark); }
                    break;
                }
                case BiterMode.Mark:
                {
                    orders.T = Math.Min(1.0, _t / _rules.markSeconds);
                    orders.Locked = orders.T >= LockAt;
                    if (!orders.Locked)
                    {
                        // It stops circling and swings its nose onto where they are going.
                        Vec3d aim = toPlayer + senses.PlayerVelocity * _rules.leadSeconds;
                        Steer(aim, _rules.ringPace * 0.3, body, ref orders);
                        double most = _rules.turn * 1.8 * body.TurnShare;
                        orders.TurnRate = Clamp(Math.Atan2(aim.X, aim.Z) * 8.0, -most, most);
                        if (!there) { Go(BiterMode.Shoal); break; }
                        // Aimed now: it will dart this far along its nose.
                        _dartLeft = Math.Min(_rules.dartReachMost, aim.Magnitude + _rules.dartOvershoot);
                    }
                    else
                    {
                        orders.Speed = 0.6;
                        orders.TurnRate = 0.0;
                    }
                    if (_t >= _rules.markSeconds) Go(BiterMode.Dart);
                    break;
                }
                case BiterMode.Dart:
                {
                    orders.Locked = true;
                    orders.Speed = _rules.dartPace * body.PaceShare;
                    orders.TurnRate = 0.0;
                    _dartLeft -= orders.Speed * dt;
                    orders.T = Clamp(1.0 - _dartLeft / Math.Max(1.0, _rules.dartReachMost), 0.0, 1.0);
                    if (!_bitten && senses.Hostile && senses.PlayerInWater && (senses.Player - BiterLayout.Mouth).Magnitude < _rules.biteReach)
                    {
                        _bitten = true;
                        orders.Bite = _rules.biteDamage;
                    }
                    if (_dartLeft <= 0.0 || senses.Blocked) Go(BiterMode.Peel);
                    break;
                }
                case BiterMode.Peel:
                {
                    orders.T = Math.Min(1.0, _t / _rules.peelSeconds);
                    Vec3d away = far > 0.01 ? new Vec3d(-toPlayer.X, 0, -toPlayer.Z).Normalized : Vec3d.Forward;
                    Steer(away + Vec3d.Cross(Vec3d.Up, away) * (1.2 * _ringSign), _rules.peelPace, body, ref orders);
                    if (_t >= _rules.peelSeconds) Go(noticed ? BiterMode.Ring : BiterMode.Shoal);
                    break;
                }
                case BiterMode.Scatter:
                {
                    orders.T = Math.Min(1.0, _t / _rules.scatterSeconds);
                    Vec3d away = far > 0.01 ? new Vec3d(-toPlayer.X, 0, -toPlayer.Z).Normalized : Vec3d.Forward;
                    Steer(away + Vec3d.Cross(Vec3d.Up, away) * (0.6 * _ringSign), _rules.peelPace * 1.2, body, ref orders);
                    if (_t >= _rules.scatterSeconds) Go(!body.TailFinOn ? BiterMode.Limp : noticed ? BiterMode.Ring : BiterMode.Shoal);
                    break;
                }
                case BiterMode.Limp:
                {
                    if (there) Steer(RingWay(toPlayer, _rules.ringRadius * 1.6), _rules.ringPace, body, ref orders);
                    else
                    {
                        Vec3d home = new Vec3d(senses.Home.X, 0, senses.Home.Z);
                        Steer(home.Magnitude > _rules.shoalRadius ? home : Vec3d.Forward, _rules.shoalPace, body, ref orders);
                    }
                    break;
                }
                case BiterMode.Flee:
                {
                    orders.T = Math.Min(1.0, _t / 8.0);
                    Vec3d away = far > 0.01 ? new Vec3d(-toPlayer.X, 0, -toPlayer.Z).Normalized : Vec3d.Forward;
                    away.Y = -0.3;
                    Steer(senses.Hostile && far < _rules.roamRadius ? away : Vec3d.Forward, _rules.peelPace, body, ref orders);
                    break;
                }
            }

            if (senses.WaterAbove < _rules.keepUnder) orders.Climb = Math.Min(orders.Climb, -(_rules.keepUnder - senses.WaterAbove) * 1.2);
            if (senses.FloorBelow < _rules.keepOff) orders.Climb = Math.Max(orders.Climb, (_rules.keepOff - senses.FloorBelow) * 1.2);
            if (senses.WaterAbove < _rules.keepUnder && senses.FloorBelow < _rules.keepOff) orders.Climb = 0.0;
            if (senses.Blocked && Mode != BiterMode.Dart)
            {
                orders.Speed = Math.Min(orders.Speed, 0.4);
                if (Mode != BiterMode.Mark) orders.TurnRate = _rules.turn * body.TurnShare * _ringSign;
            }
            orders.Mode = Mode;
            return orders;
        }
    }
}
