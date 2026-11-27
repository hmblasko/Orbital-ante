using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    public enum StalkerMode
    {
        /// <summary>Motionless. It can stay like this for as long as nothing makes a sound.</summary>
        Still,
        /// <summary>It has heard something: its gills flare and its head comes round to the sound.</summary>
        Listen,
        /// <summary>Walking, quietly, to the place it last heard something.</summary>
        Stalk,
        /// <summary>Something loud (a shot): it runs at the place.</summary>
        Rush,
        /// <summary>The warning: stopped, jaw dropping open, gills spread. Part way through it takes aim.</summary>
        Gape,
        /// <summary>Its tail throws it forward and its jaws shut on the place it aimed at.</summary>
        Lunge,
        /// <summary>After a lunge it lies where it landed.</summary>
        Recover,
        /// <summary>It reached the place and nothing is there: it casts about.</summary>
        Search,
        /// <summary>A gill shot off: it reels, and whatever it was doing is over.</summary>
        Reel,
        /// <summary>Nothing heard for a long time and far from where it lives: it walks back.</summary>
        Return,
    }

    [Serializable]
    public sealed class StalkerRules
    {
        /// <summary>From how far it hears someone walking (noise 1), with both gills. Louder is heard further, quieter nearer.</summary>
        public float hearRange = 20f;
        /// <summary>Inside this it feels a suit's electrics through its snout, however still and quiet they are.</summary>
        public float senseRange = 1.5f;
        /// <summary>How often it takes a new place from what it hears: between times it is going where the sound was.</summary>
        public float hearEvery = 1.0f;
        /// <summary>Noise at or over this (a shot, jets; a sprint is under it) makes it run.</summary>
        public float loud = 1.4f;
        public float listenSeconds = 0.7f;
        /// <summary>Metres a second. The player walks at about 5 and sprints at 8.</summary>
        public float stalkPace = 2.2f;
        public float rushPace = 6.4f;
        /// <summary>With nothing loud for this long a run drops back to a walk.</summary>
        public float rushSeconds = 2.5f;
        /// <summary>Radians a second.</summary>
        public float turn = 2.2f;
        /// <summary>The warning before a lunge. It takes aim at <see cref="StalkerBrain.LockAt"/> of the way through.</summary>
        public float gapeSeconds = 0.9f;
        public float lungeSeconds = 0.22f;
        /// <summary>How far from its mouth a lunge reaches, with its whole tail.</summary>
        public float lungeReach = 3.2f;
        public float biteRadius = 0.8f;
        public float biteDamage = 24f;
        public float recoverSeconds = 1.2f;
        public float searchSeconds = 5f;
        public float reelSeconds = 1.0f;
        /// <summary>After reeling it cannot be made to reel again for this long.</summary>
        public float steadySeconds = 3f;
        /// <summary>Still, far from home and nothing heard for this long, it walks back.</summary>
        public float homesickSeconds = 10f;
        /// <summary>It never goes after a sound further than this from where it lives.</summary>
        public float roamRadius = 28f;
        /// <summary>Kept from where it is going by something solid for about this long, it gives the place up and casts about.</summary>
        public float stuckSeconds = 0.8f;
        /// <summary>How many times quicker it turns to something it feels with its snout than to a sound.</summary>
        public float feltTurn = 2.5f;
        public StalkerHp hp = StalkerHp.Default;
        /// <summary>How long a piece shot off lies there.</summary>
        public float pieceLife = 30f;
        /// <summary>Loose pieces of all creatures together.</summary>
        public int maxLoosePieces = 34;
    }

    public struct StalkerSenses
    {
        /// <summary>There is a living player to go after.</summary>
        public bool Hostile;
        /// <summary>Where the player's feet are, in creature space. It is blind: this is only what its ears and snout are worked out from.</summary>
        public Vec3d Player;
        /// <summary>How loud the player is: 0 silent, 1 walking, more for running, jets and shots.</summary>
        public double Noise;
        /// <summary>Where it lives, and which way north is, in creature space: it keeps places in mind by them.</summary>
        public Vec3d Home, North;
        /// <summary>Something solid is right in front of its snout.</summary>
        public bool Blocked;
    }

    public struct StalkerOrders
    {
        /// <summary>Creature space, metres a second. It never goes backward: its body follows its head.</summary>
        public Vec3d Velocity;
        public double TurnRate;
        public StalkerMode Mode;
        /// <summary>How far through the mode it is, 0 to 1.</summary>
        public double T;
        /// <summary>Damage done to the player this frame.</summary>
        public double Bite;
        /// <summary>It knows of a place, and this is it, in creature space (its head turns to it).</summary>
        public bool HasPlace;
        public Vec3d Place;
        /// <summary>It is hearing or feeling the player this very frame.</summary>
        public bool Aware;
        /// <summary>This frame it took a new place from what it heard or felt (its gills kick and it clicks): the player's tell that where they are now is known.</summary>
        public bool Took;
    }

    /// <summary>
    /// A Blind stalker's mind. Plain C#, no Unity types.
    ///
    /// It cannot see. It hears (further for louder; less with a gill gone) and, inside <see cref="StalkerRules.senseRange"/>,
    /// feels a suit's electrics exactly. From hearing it only ever has a place, and takes a new one at most every
    /// <see cref="StalkerRules.hearEvery"/>: it goes where the sound was. So someone who stops making noise and is
    /// not where they last made it is lost, and it casts about and goes still. A shot brings it at a run.
    /// At the place, if what it hears is fresh, it warns (<see cref="StalkerMode.Gape"/>), takes aim at
    /// <see cref="LockAt"/>, and lunges at where it aimed.
    /// </summary>
    public sealed class StalkerBrain
    {
        /// <summary>How far through the warning it stops following and is aimed.</summary>
        public const double LockAt = 0.5;
        /// <summary>How far through a lunge its jaws shut.</summary>
        public const double BiteAt = 0.7;
        /// <summary>What it has heard counts as fresh for this long.</summary>
        public const double Fresh = 0.9;

        readonly StalkerRules _rules;
        double _t, _sinceHeard = 100.0, _sinceTaken = 100.0, _sinceLoud = 100.0, _sinceReel = 100.0, _quiet, _lunge, _lungeLeft, _searchTurn = 1.0, _stuck;
        // The place it has in mind, in metres east and north of home.
        double _east, _north;
        bool _hasPlace, _bitten, _locked, _clamped;

        public StalkerBrain(StalkerRules rules)
        {
            _rules = rules ?? new StalkerRules();
        }

        public StalkerMode Mode { get; private set; } = StalkerMode.Still;

        /// <summary>From how far it hears someone this loud.</summary>
        public static double HearRange(StalkerRules rules, StalkerBody body, double noise) => rules.hearRange * body.HearShare * Math.Max(0.0, noise);

        static Vec3d East(Vec3d north) => new Vec3d(north.Z, 0, -north.X);

        void Keep(in StalkerSenses s, Vec3d local)
        {
            Vec3d d = local - s.Home;
            _north = d.X * s.North.X + d.Z * s.North.Z;
            Vec3d e = East(s.North);
            _east = d.X * e.X + d.Z * e.Z;
            // Never further from home than it roams.
            double far = Math.Sqrt(_east * _east + _north * _north);
            _clamped = far > _rules.roamRadius + 0.5;
            if (far > _rules.roamRadius)
            {
                _east *= _rules.roamRadius / far;
                _north *= _rules.roamRadius / far;
            }
            _hasPlace = true;
        }

        Vec3d Recall(in StalkerSenses s)
        {
            Vec3d e = East(s.North);
            return new Vec3d(s.Home.X + s.North.X * _north + e.X * _east, 0, s.Home.Z + s.North.Z * _north + e.Z * _east);
        }

        void Enter(StalkerMode mode)
        {
            Mode = mode;
            _t = 0.0;
            _stuck = 0.0;
        }

        /// <summary>A gill has been shot off: it reels (not twice running). True if it did.</summary>
        public bool Reel()
        {
            if (_sinceReel < _rules.steadySeconds || Mode == StalkerMode.Lunge) return false;
            _sinceReel = 0.0;
            Enter(StalkerMode.Reel);
            return true;
        }

        /// <summary>It has been hurt by someone at this place (creature space): pain tells it where, as a shot would.</summary>
        public void Hurt(in StalkerSenses senses, Vec3d from)
        {
            Keep(senses, new Vec3d(from.X, 0, from.Z));
            _sinceHeard = 0.0;
            _sinceLoud = 0.0;
            if (Mode == StalkerMode.Still || Mode == StalkerMode.Search || Mode == StalkerMode.Return || Mode == StalkerMode.Stalk) Enter(StalkerMode.Rush);
        }

        /// <summary>Is someone whose feet are here (creature space) in its jaws as they shut?</summary>
        public bool BiteHits(Vec3d playerFeet)
        {
            double z = playerFeet.Z - StalkerLayout.Mouth;
            return Math.Sqrt(playerFeet.X * playerFeet.X + z * z) < _rules.biteRadius && Math.Abs(playerFeet.Y) < 1.2;
        }

        public StalkerOrders Think(StalkerBody body, in StalkerSenses senses, double dt)
        {
            var orders = new StalkerOrders { Mode = Mode };
            if (body == null || !body.Alive) return orders;
            _t += dt;
            _sinceHeard += dt;
            _sinceTaken += dt;
            _sinceLoud += dt;
            _sinceReel += dt;

            // ---- what its ears and its snout tell it
            var flat = new Vec3d(senses.Player.X, 0, senses.Player.Z);
            double distance = flat.Magnitude;
            bool feels = senses.Hostile && distance < _rules.senseRange;
            bool hears = senses.Hostile && distance < HearRange(_rules, body, senses.Noise);
            bool aware = feels || hears;
            bool following = !(Mode == StalkerMode.Gape && _locked) && Mode != StalkerMode.Lunge && Mode != StalkerMode.Reel;
            if (aware)
            {
                _sinceHeard = 0.0;
                if (senses.Noise >= _rules.loud) _sinceLoud = 0.0;
                // Its snout knows exactly; its ears give it a place now and then.
                // (and, mouth open, it is listening as hard as it can: until it has taken aim it follows every sound)
                if (following && (feels || Mode == StalkerMode.Gape || _sinceTaken >= _rules.hearEvery || !_hasPlace))
                {
                    // (Feeling, it knows all the time; the tell is only given at its hearing's beat.)
                    if (_sinceTaken >= _rules.hearEvery || !_hasPlace) orders.Took = true;
                    Keep(senses, flat);
                    if (orders.Took) _sinceTaken = 0.0;
                }
            }
            bool fresh = _sinceHeard < Fresh;
            Vec3d place = _hasPlace ? Recall(senses) : Vec3d.Zero;
            double placeDistance = place.Magnitude;
            double bearing = placeDistance > 1e-6 ? Math.Atan2(place.X, place.Z) : 0.0;
            double reach = _rules.lungeReach * body.LungeShare;

            // Something solid in the way: the Unity side says so on the frames it refused a step, which is about every
            // other one, so the time is counted up quickly and down slowly.
            _stuck = Math.Max(0.0, _stuck + (senses.Blocked ? dt : -0.25 * dt));
            bool stuck = _stuck >= _rules.stuckSeconds * 0.375;

            double TurnTo(double angle, double rate)
            {
                double step = rate * dt;
                return Math.Abs(angle) <= step ? angle / Math.Max(dt, 1e-9) : Math.Sign(angle) * rate;
            }

            switch (Mode)
            {
                case StalkerMode.Still:
                    _quiet += dt;
                    if (aware)
                    {
                        _quiet = 0.0;
                        Enter(StalkerMode.Listen);
                    }
                    else if (_quiet > _rules.homesickSeconds && senses.Home.Magnitude > 6.0) Enter(StalkerMode.Return);
                    break;

                case StalkerMode.Listen:
                    orders.TurnRate = TurnTo(bearing, _rules.turn * 0.5);
                    if (_t >= _rules.listenSeconds) Enter(_sinceLoud < _rules.listenSeconds + 0.1 ? StalkerMode.Rush : StalkerMode.Stalk);
                    break;

                case StalkerMode.Stalk:
                case StalkerMode.Rush:
                {
                    bool rush = Mode == StalkerMode.Rush;
                    if (!rush && _sinceLoud < 0.1) { Mode = StalkerMode.Rush; rush = true; }
                    else if (rush && _sinceLoud > _rules.rushSeconds) { Mode = StalkerMode.Stalk; rush = false; }
                    // What it feels with its snout it snaps round at: there is no walking circles round it up close.
                    orders.TurnRate = TurnTo(bearing, _rules.turn * (feels ? _rules.feltTurn : rush ? 1.5 : 1.0));
                    // It slows to turn, and stops for anything solid in front of it.
                    double facing = Math.Max(0.0, Math.Cos(Math.Min(Math.PI * 0.5, Math.Abs(bearing) * 1.3)));
                    double pace = (rush ? _rules.rushPace : _rules.stalkPace) * body.PaceShare * facing;
                    double mouthTo = placeDistance - StalkerLayout.Mouth;
                    // A sound from further off than it roams is never lunged at: it goes as far as it will and casts about.
                    if (fresh && (!_clamped || feels) && mouthTo < reach * 0.85 && Math.Abs(bearing) < 0.45)
                    {
                        _locked = false;
                        Enter(StalkerMode.Gape);
                    }
                    else if (mouthTo < 0.5)
                    {
                        // At the place, and nothing there to hear (or nothing it will go further for).
                        if (!fresh || _clamped) Enter(StalkerMode.Search);
                    }
                    else if (stuck)
                    {
                        // It cannot get there: it gives the place up.
                        _hasPlace = false;
                        Enter(StalkerMode.Search);
                    }
                    else if (!senses.Blocked) orders.Velocity = new Vec3d(0, 0, pace);
                    break;
                }

                case StalkerMode.Gape:
                {
                    double t = _t / _rules.gapeSeconds;
                    if (!_locked)
                    {
                        orders.TurnRate = TurnTo(bearing, _rules.turn * (feels ? _rules.feltTurn : 1.5));
                        if (t >= LockAt)
                        {
                            // Aimed: from here on a step aside is a miss.
                            _locked = true;
                            _lunge = Math.Max(0.0, Math.Min(reach, placeDistance - StalkerLayout.Mouth));
                        }
                    }
                    if (_t >= _rules.gapeSeconds)
                    {
                        _bitten = false;
                        _lungeLeft = _lunge;
                        Enter(StalkerMode.Lunge);
                    }
                    break;
                }

                case StalkerMode.Lunge:
                {
                    double t = _t / _rules.lungeSeconds;
                    // It covers the whole distance before its jaws shut, and they shut where it aimed.
                    if (!senses.Blocked && _lungeLeft > 0.0)
                    {
                        double step = Math.Min(_lungeLeft, _lunge / (_rules.lungeSeconds * BiteAt) * dt);
                        _lungeLeft -= step;
                        orders.Velocity = new Vec3d(0, 0, step / Math.Max(dt, 1e-9));
                    }
                    if (!_bitten && t >= BiteAt)
                    {
                        _bitten = true;
                        if (senses.Hostile && BiteHits(senses.Player)) orders.Bite = _rules.biteDamage;
                    }
                    if (_t >= _rules.lungeSeconds) Enter(StalkerMode.Recover);
                    break;
                }

                case StalkerMode.Recover:
                    if (_t >= _rules.recoverSeconds) Enter(fresh ? StalkerMode.Stalk : StalkerMode.Search);
                    break;

                case StalkerMode.Reel:
                    if (_t >= _rules.reelSeconds) Enter(fresh ? StalkerMode.Stalk : StalkerMode.Search);
                    break;

                case StalkerMode.Search:
                    // (Not for a sound from beyond where it roams: it has gone as far as it will for that.)
                    if (aware && _hasPlace && !(_clamped && !feels))
                    {
                        Enter(_sinceLoud < 0.1 ? StalkerMode.Rush : StalkerMode.Stalk);
                        break;
                    }
                    // It casts about: slow loops one way, then the other.
                    if (Math.Floor(_t / 1.6) % 2 == 0) _searchTurn = 1.0; else _searchTurn = -1.0;
                    orders.TurnRate = _searchTurn * 0.9;
                    if (!senses.Blocked) orders.Velocity = new Vec3d(0, 0, _rules.stalkPace * 0.5 * body.PaceShare);
                    if (_t >= _rules.searchSeconds)
                    {
                        _quiet = 0.0;
                        Enter(StalkerMode.Still);
                    }
                    break;

                case StalkerMode.Return:
                {
                    if (aware)
                    {
                        Enter(StalkerMode.Listen);
                        break;
                    }
                    var home = new Vec3d(senses.Home.X, 0, senses.Home.Z);
                    double homeBearing = Math.Atan2(home.X, home.Z);
                    orders.TurnRate = TurnTo(homeBearing, _rules.turn);
                    double facing = Math.Max(0.0, Math.Cos(Math.Min(Math.PI * 0.5, Math.Abs(homeBearing) * 1.3)));
                    if (home.Magnitude < 2.0)
                    {
                        _quiet = 0.0;
                        Enter(StalkerMode.Still);
                    }
                    else if (stuck) Enter(StalkerMode.Search);   // round whatever is in the way, then home again later
                    else if (!senses.Blocked) orders.Velocity = new Vec3d(0, 0, _rules.stalkPace * body.PaceShare * facing);
                    break;
                }
            }

            orders.Mode = Mode;
            orders.T = Mode switch
            {
                StalkerMode.Listen => _t / _rules.listenSeconds,
                StalkerMode.Gape => _t / _rules.gapeSeconds,
                StalkerMode.Lunge => _t / _rules.lungeSeconds,
                StalkerMode.Recover => _t / _rules.recoverSeconds,
                StalkerMode.Search => _t / _rules.searchSeconds,
                StalkerMode.Reel => _t / _rules.reelSeconds,
                _ => 0.0,
            };
            orders.T = Math.Max(0.0, Math.Min(1.0, orders.T));
            orders.HasPlace = _hasPlace && Mode != StalkerMode.Still && Mode != StalkerMode.Return;
            orders.Place = place;
            orders.Aware = aware;
            return orders;
        }
    }
}
