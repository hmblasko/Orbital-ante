using System;
using System.Collections.Generic;
using OrbitalAnte.Dismemberment;

namespace OrbitalAnte.Melee
{
    /// <summary>
    /// The one thing a melee item does that a gun cannot (research report "Most fun melee weapons", 16 Nov 2026:
    /// "give every found melee weapon one verb").
    /// </summary>
    public enum MeleeVerb
    {
        /// <summary>Laser sword: takes one arm or leg part clean off, whatever it has left.</summary>
        Cut,
        /// <summary>Baseball bat: knocks a living enemy down and sends it sliding; the far end of the bat hits hardest.</summary>
        Launch,
        /// <summary>Mailbox: any limb it touches breaks off, up to three enemies a swing; a struck torso goes down.</summary>
        Crush,
        /// <summary>Butterfly knife: a stab to a hand always disarms; from behind an unaware enemy, a stab kills.</summary>
        Stab,
        /// <summary>Frying pan: a head hit rings, freezes longer and startles. Otherwise plain.</summary>
        Bonk,
    }

    /// <summary>
    /// Everything about one melee item, as plain numbers (no engine types), so the rules can be tested anywhere.
    /// The game's copy of these numbers is the <c>MeleeData</c> asset in Resources/Melee; the five defaults below
    /// are the research report's first guesses and are not tested in play.
    /// </summary>
    public sealed class MeleeSpec
    {
        public string Id = "Melee";
        public string DisplayName = "Melee";
        /// <summary>What the item does, in one line, shown when looking at it on the ground and while holding it.</summary>
        public string PickupNote = "";
        public MeleeVerb Verb = MeleeVerb.Bonk;

        /// <summary>From the press to the moment the swing can hit, in seconds.</summary>
        public float Windup = 0.18f;
        /// <summary>How long the swing can hit for, in seconds.</summary>
        public float Active = 0.10f;
        /// <summary>From the end of the hit window until the next swing can start, in seconds.</summary>
        public float Recovery = 0.45f;
        /// <summary>How far from the eye the swing reaches, in metres.</summary>
        public float Reach = 1.8f;
        /// <summary>How wide the swing is, side to side, in degrees.</summary>
        public float Arc = 60f;

        public float HeadDamage = 45f, TorsoDamage = 50f, UpperArmDamage = 35f, ForearmDamage = 35f,
                     HandDamage = 35f, ThighDamage = 35f, ShinDamage = 35f, FootDamage = 35f;

        /// <summary>The freeze when the swing connects, in milliseconds (capped at <see cref="MeleeRules.HitstopCapMs"/>).</summary>
        public float HitstopMs = 60f;
        /// <summary>The freeze for a sweet-spot hit instead, in milliseconds; 0 keeps <see cref="HitstopMs"/>.</summary>
        public float SweetHitstopMs = 0f;
        /// <summary>Added to the freeze for a pan's head hit, in milliseconds.</summary>
        public float BonusHitstopMs = 0f;

        /// <summary>The speed a struck living enemy is sent sliding at (m/s), for the verbs that knock down.</summary>
        public float LaunchSpeed = 0f;
        /// <summary>The same for a sweet-spot hit; 0 keeps <see cref="LaunchSpeed"/>.</summary>
        public float SweetLaunchSpeed = 0f;
        /// <summary>The outer part of the reach that counts as the sweet spot, in metres; 0 for none.</summary>
        public float SweetSpot = 0f;
        /// <summary>Damage times this on a sweet-spot hit.</summary>
        public float SweetMultiplier = 1f;
        /// <summary>The most separate things one swing can hit.</summary>
        public int MaxTargets = 1;

        /// <summary>Swings in quick succession before it overheats; 0 never overheats.</summary>
        public int HeatSwings = 0;
        /// <summary>How long an overheat locks it, in seconds.</summary>
        public float HeatLockSeconds = 0f;
        /// <summary>Heat only starts to go down after this long without a swing, in seconds.</summary>
        public float HeatCoolDelay = 0.8f;
        /// <summary>Then it goes down by this many swings' worth a second.</summary>
        public float HeatCoolRate = 2f;

        /// <summary>
        /// While it is held, the player is never heard from less far than this (against an enemy's hearing, the
        /// way stances count: 1 is walking). 0 makes no sound of its own.
        /// </summary>
        public float Noise = 0f;
        /// <summary>Walking speed times this while it is held (1 is no change).</summary>
        public float WalkSlow = 1f;
        /// <summary>How often it is the item a world's loot holds, against the others' weights.</summary>
        public float RarityWeight = 1f;

        /// <summary>A stab from behind on an enemy that has not seen the player lately does at least this to the head.</summary>
        public float TakedownDamage = 0f;
        /// <summary>How wide "behind" is, in degrees, centred on the enemy's back.</summary>
        public float RearArc = 90f;
        /// <summary>How fast the business end moves in a swing (m/s). In zero-G it sets the push back from what is hit.</summary>
        public float SwingSpeed = 6f;
        /// <summary>How heavy it is, in kilograms (for when it lies on the ground).</summary>
        public float Mass = 1.5f;

        public float DamageFor(BodyPartId id)
        {
            switch (id)
            {
                case BodyPartId.Head: return HeadDamage;
                case BodyPartId.Torso: return TorsoDamage;
                case BodyPartId.LeftUpperArm:
                case BodyPartId.RightUpperArm: return UpperArmDamage;
                case BodyPartId.LeftForearm:
                case BodyPartId.RightForearm: return ForearmDamage;
                case BodyPartId.LeftHand:
                case BodyPartId.RightHand: return HandDamage;
                case BodyPartId.LeftThigh:
                case BodyPartId.RightThigh: return ThighDamage;
                case BodyPartId.LeftShin:
                case BodyPartId.RightShin: return ShinDamage;
                default: return FootDamage;
            }
        }

        /// <summary>One whole swing, press to ready again, in seconds.</summary>
        public float Cycle => Windup + Active + Recovery;

        /// <summary>Plain-language problems with these numbers; empty when all is well.</summary>
        public void Validate(List<string> problems)
        {
            if (string.IsNullOrEmpty(DisplayName)) problems.Add("has no display name.");
            if (string.IsNullOrEmpty(PickupNote)) problems.Add("has no pickup note: every melee item says what it does when you find it.");
            if (Windup < 0f || Recovery < 0f) problems.Add("windup and recovery cannot be negative.");
            if (Active <= 0f) problems.Add("the hit window must be longer than zero, or the swing can never hit.");
            if (Reach <= 0f) problems.Add("reach must be above zero.");
            if (Arc <= 0f || Arc > 180f) problems.Add("arc must be between 0 and 180 degrees.");
            if (HeadDamage <= 0f || TorsoDamage <= 0f || UpperArmDamage <= 0f || ForearmDamage <= 0f
                || HandDamage <= 0f || ThighDamage <= 0f || ShinDamage <= 0f || FootDamage <= 0f)
                problems.Add("every body part needs damage above zero.");
            if (MaxTargets < 1) problems.Add("must be able to hit at least one thing a swing.");
            if (HitstopMs < 0f || SweetHitstopMs < 0f || BonusHitstopMs < 0f) problems.Add("hitstop cannot be negative.");
            if (SweetSpot < 0f || SweetSpot > Reach) problems.Add("the sweet spot must fit inside the reach.");
            if (SweetMultiplier < 1f) problems.Add("a sweet-spot multiplier below 1 would make the sweet spot worse.");
            if (HeatSwings < 0 || (HeatSwings > 0 && HeatLockSeconds <= 0f)) problems.Add("an item that overheats needs a lock time above zero.");
            if (WalkSlow <= 0f || WalkSlow > 1f) problems.Add("walk slow must be above 0 and at most 1 (a melee item never speeds you up).");
            if (RarityWeight < 0f) problems.Add("rarity weight cannot be negative.");
            if (Noise < 0f) problems.Add("noise cannot be negative.");
            if (Verb == MeleeVerb.Stab && TakedownDamage <= 0f) problems.Add("a stabbing item needs takedown damage.");
            if (Verb == MeleeVerb.Launch && LaunchSpeed <= 0f) problems.Add("a launching item needs a launch speed.");
            if (Mass <= 0f) problems.Add("mass must be above zero.");
        }

        public MeleeSpec Copy() => (MeleeSpec)MemberwiseClone();
    }

    /// <summary>
    /// The five melee items (research report, ranked shortlist; the plunger is not built). Names are placeholders
    /// until Matthew names them. Every number is a first guess to be proved in play.
    /// </summary>
    public static class MeleeCatalog
    {
        public const string LaserSwordId = "LaserSword";
        public const string BaseballBatId = "BaseballBat";
        public const string MailboxId = "Mailbox";
        public const string ButterflyKnifeId = "ButterflyKnife";
        public const string FryingPanId = "FryingPan";

        /// <summary>Every built-in id, in the order the arena lays them out.</summary>
        public static readonly string[] Ids = { FryingPanId, ButterflyKnifeId, BaseballBatId, MailboxId, LaserSwordId };

        /// <summary>
        /// How far above the hand the far end of each weapon is, in metres: where its Tip socket stands. The model
        /// (tools/blender/build_props.py) and the stand-in shapes both keep to it (rule test PropsModelTests).
        /// </summary>
        public static float TipHeight(string id)
        {
            switch (id)
            {
                case LaserSwordId: return 1.21f;
                case BaseballBatId: return 0.86f;
                case MailboxId: return 1.15f;
                case ButterflyKnifeId: return 0.23f;
                default: return 0.50f;
            }
        }

        /// <summary>The built-in numbers for an id, or null for an id that is not one of the five.</summary>
        public static MeleeSpec Defaults(string id)
        {
            switch (id)
            {
                case LaserSwordId: return LaserSword();
                case BaseballBatId: return BaseballBat();
                case MailboxId: return Mailbox();
                case ButterflyKnifeId: return ButterflyKnife();
                case FryingPanId: return FryingPan();
                default: return null;
            }
        }

        /// <summary>Cuts one part off a swing. Strongest, so it overheats, hums (no sneaking) and is the rarest.</summary>
        public static MeleeSpec LaserSword() => new MeleeSpec
        {
            Id = LaserSwordId, DisplayName = "Laser Sword (name TBD)", Verb = MeleeVerb.Cut,
            PickupNote = "Cuts off one limb a swing. Overheats after 5 quick swings. Its hum gives you away.",
            Windup = 0.12f, Active = 0.10f, Recovery = 0.35f, Reach = 2.0f, Arc = 70f,
            HeadDamage = 45f, TorsoDamage = 120f, UpperArmDamage = 70f, ForearmDamage = 70f, HandDamage = 70f,
            ThighDamage = 70f, ShinDamage = 70f, FootDamage = 70f,
            HitstopMs = 70f, MaxTargets = 1,
            HeatSwings = 5, HeatLockSeconds = 2.5f, HeatCoolDelay = 0.8f, HeatCoolRate = 2f,
            Noise = 1f, WalkSlow = 1f, RarityWeight = 5f, SwingSpeed = 7f, Mass = 1.2f,
        };

        /// <summary>Knocks them down and sends them sliding; the end of the bat hits harder. Rarely kills.</summary>
        public static MeleeSpec BaseballBat() => new MeleeSpec
        {
            Id = BaseballBatId, DisplayName = "Baseball Bat (name TBD)", Verb = MeleeVerb.Launch,
            PickupNote = "Knocks enemies flat and sends them flying. Hit with the end of the bat for more.",
            Windup = 0.22f, Active = 0.12f, Recovery = 0.45f, Reach = 2.1f, Arc = 90f,
            HeadDamage = 45f, TorsoDamage = 60f, UpperArmDamage = 30f, ForearmDamage = 30f, HandDamage = 30f,
            ThighDamage = 34f, ShinDamage = 34f, FootDamage = 30f,
            HitstopMs = 60f, SweetHitstopMs = 100f, LaunchSpeed = 4f, SweetLaunchSpeed = 8f,
            SweetSpot = 0.4f, SweetMultiplier = 1.5f, MaxTargets = 1,
            WalkSlow = 1f, RarityWeight = 22f, SwingSpeed = 9f, Mass = 1.0f,
        };

        /// <summary>Breaks any limb it touches, up to three enemies a swing. Slow, and slows your walk.</summary>
        public static MeleeSpec Mailbox() => new MeleeSpec
        {
            Id = MailboxId, DisplayName = "Mailbox (name TBD)", Verb = MeleeVerb.Crush,
            PickupNote = "Breaks off any limb it hits, up to 3 enemies a swing. Heavy: slow swing, slower walk.",
            Windup = 0.45f, Active = 0.15f, Recovery = 0.80f, Reach = 2.3f, Arc = 120f,
            HeadDamage = 60f, TorsoDamage = 120f, UpperArmDamage = 60f, ForearmDamage = 45f, HandDamage = 30f,
            ThighDamage = 68f, ShinDamage = 50f, FootDamage = 30f,
            HitstopMs = 120f, LaunchSpeed = 3f, MaxTargets = 3,
            WalkSlow = 0.85f, RarityWeight = 11f, SwingSpeed = 6f, Mass = 9f,
        };

        /// <summary>Fast and weak; a stab to a hand disarms; from behind an unaware enemy it kills.</summary>
        public static MeleeSpec ButterflyKnife() => new MeleeSpec
        {
            Id = ButterflyKnifeId, DisplayName = "Butterfly Knife (name TBD)", Verb = MeleeVerb.Stab,
            PickupNote = "Stab a hand to disarm. From behind, on an enemy that has not seen you: TAKEDOWN.",
            Windup = 0.06f, Active = 0.08f, Recovery = 0.20f, Reach = 1.5f, Arc = 30f,
            HeadDamage = 25f, TorsoDamage = 25f, UpperArmDamage = 25f, ForearmDamage = 25f, HandDamage = 30f,
            ThighDamage = 25f, ShinDamage = 25f, FootDamage = 30f,
            HitstopMs = 40f, MaxTargets = 1, TakedownDamage = 90f, RearArc = 90f,
            WalkSlow = 1f, RarityWeight = 22f, SwingSpeed = 5f, Mass = 0.2f,
        };

        /// <summary>Plain numbers; a head hit rings like a bell, freezes longer and startles.</summary>
        public static MeleeSpec FryingPan() => new MeleeSpec
        {
            Id = FryingPanId, DisplayName = "Frying Pan (name TBD)", Verb = MeleeVerb.Bonk,
            PickupNote = "A frying pan. On the head it rings them like a bell: dazed, they cannot shoot for a moment.",
            Windup = 0.18f, Active = 0.10f, Recovery = 0.45f, Reach = 1.8f, Arc = 60f,
            HeadDamage = 45f, TorsoDamage = 50f, UpperArmDamage = 35f, ForearmDamage = 35f, HandDamage = 35f,
            ThighDamage = 35f, ShinDamage = 35f, FootDamage = 35f,
            HitstopMs = 60f, BonusHitstopMs = 100f, MaxTargets = 1,
            WalkSlow = 1f, RarityWeight = 40f, SwingSpeed = 7f, Mass = 1.6f,
        };
    }

    public enum SwingPhase
    {
        /// <summary>Nothing happening: a press starts a swing.</summary>
        Ready,
        /// <summary>Drawing back: cannot hit yet.</summary>
        Windup,
        /// <summary>The swing can hit (once).</summary>
        Active,
        /// <summary>Following through: cannot hit, cannot swing again yet.</summary>
        Recovery,
    }

    /// <summary>
    /// One swing at a time: windup, then the hit window, then recovery, then ready. The hit window is never skipped,
    /// even by a long frame, so a swing always gets one chance to hit. A swing makes contact once (everything it
    /// hits, it hits together, like TF2's single sweep at damage time) and strikes each body part at most once.
    /// A press in the last <see cref="BufferSeconds"/> of recovery is kept and starts the next swing on time.
    /// </summary>
    public sealed class MeleeSwing
    {
        public const float BufferSeconds = 0.15f;

        readonly HashSet<int> _struck = new HashSet<int>();
        MeleeSpec _spec;
        double _t;
        bool _queued;

        public MeleeSwing(MeleeSpec spec) { _spec = spec ?? throw new ArgumentNullException(nameof(spec)); }

        public MeleeSpec Spec => _spec;
        public SwingPhase Phase { get; private set; } = SwingPhase.Ready;
        /// <summary>True once this swing has hit something.</summary>
        public bool Contacted { get; private set; }
        /// <summary>Seconds since the swing started.</summary>
        public double Elapsed => Phase == SwingPhase.Ready ? 0 : _t;
        /// <summary>0 to 1 through the whole swing, for the animation; 0 when ready.</summary>
        public float Progress => Phase == SwingPhase.Ready || _spec.Cycle <= 0f ? 0f : (float)Math.Min(1.0, _t / _spec.Cycle);
        /// <summary>True while the swing may still hit.</summary>
        public bool CanHit => Phase == SwingPhase.Active && !Contacted;
        public bool Busy => Phase != SwingPhase.Ready;

        /// <summary>Swaps the item (only between swings). Returns false while a swing is going.</summary>
        public bool SetSpec(MeleeSpec spec)
        {
            if (spec == null || Busy) return false;
            _spec = spec;
            _queued = false;
            return true;
        }

        /// <summary>Starts a swing if ready (true). Late in recovery the press is kept for when it ends.</summary>
        public bool Press()
        {
            if (Phase == SwingPhase.Ready) { Begin(); return true; }
            if (Phase == SwingPhase.Recovery && _spec.Cycle - _t <= BufferSeconds) _queued = true;
            return false;
        }

        void Begin()
        {
            Phase = SwingPhase.Windup;
            _t = 0;
            _queued = false;
            Contacted = false;
            _struck.Clear();
        }

        /// <summary>Moves the swing on. True when a kept press started a new swing this tick.</summary>
        public bool Tick(double dt)
        {
            if (Phase == SwingPhase.Ready || dt <= 0) return false;
            SwingPhase before = Phase;
            _t += dt;
            double windup = _spec.Windup, activeEnd = _spec.Windup + _spec.Active;
            if (_t < windup) Phase = SwingPhase.Windup;
            else if (_t < activeEnd) Phase = SwingPhase.Active;
            else if (_t < _spec.Cycle) Phase = SwingPhase.Recovery;
            else Phase = SwingPhase.Ready;

            // Never jump over the hit window: stop at its start for one tick.
            if (before == SwingPhase.Windup && Phase != SwingPhase.Windup && Phase != SwingPhase.Active)
            {
                _t = windup;
                Phase = SwingPhase.Active;
            }
            if (Phase == SwingPhase.Ready && _queued)
            {
                Begin();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Takes one body part (or one loose thing) as struck by this swing. False if the swing cannot hit now or
        /// already struck it: one hit per part per swing.
        /// </summary>
        public bool Strike(int key)
        {
            if (Phase != SwingPhase.Active) return false;
            return _struck.Add(key);
        }

        /// <summary>The swing has made its contact: nothing more this swing.</summary>
        public void MarkContact() => Contacted = true;

        /// <summary>Back to ready (the item was dropped or swapped).</summary>
        public void Cancel()
        {
            Phase = SwingPhase.Ready;
            _t = 0;
            _queued = false;
            Contacted = false;
            _struck.Clear();
        }
    }

    /// <summary>
    /// The laser sword's heat. Each swing adds one; reaching the limit locks the item for a while and then it is
    /// cool again. It only cools after a short rest, so swinging as fast as you can always overheats it on the
    /// limit's swing (the sword: the fifth). That swing still happens.
    /// </summary>
    public sealed class MeleeHeat
    {
        public int Limit { get; private set; }
        public float LockSeconds { get; private set; }
        public float CoolDelay { get; private set; }
        public float CoolRate { get; private set; }

        public double Heat { get; private set; }
        public double LockLeft { get; private set; }
        double _rested;

        public MeleeHeat(MeleeSpec spec) => Set(spec);

        public void Set(MeleeSpec spec)
        {
            Limit = spec != null ? Math.Max(0, spec.HeatSwings) : 0;
            LockSeconds = spec != null ? Math.Max(0f, spec.HeatLockSeconds) : 0f;
            CoolDelay = spec != null ? Math.Max(0f, spec.HeatCoolDelay) : 0f;
            CoolRate = spec != null ? Math.Max(0f, spec.HeatCoolRate) : 0f;
            Heat = 0;
            LockLeft = 0;
            _rested = 0;
        }

        public bool Overheats => Limit > 0;
        public bool Locked => LockLeft > 0;
        /// <summary>0 to 1 for the meter: the heat, or while locked what is left of the lock.</summary>
        public float Fill => !Overheats ? 0f : Locked ? (float)(LockSeconds > 0 ? LockLeft / LockSeconds : 0) : (float)Math.Min(1.0, Heat / Limit);

        /// <summary>A swing was made. True when this swing overheated it.</summary>
        public bool Swung()
        {
            if (!Overheats || Locked) return false;
            Heat += 1;
            _rested = 0;
            if (Heat >= Limit - 1e-9)
            {
                LockLeft = LockSeconds;
                return true;
            }
            return false;
        }

        public void Tick(double dt)
        {
            if (!Overheats || dt <= 0) return;
            if (Locked)
            {
                LockLeft -= dt;
                if (LockLeft <= 0) { LockLeft = 0; Heat = 0; _rested = 0; }
                return;
            }
            _rested += dt;
            if (_rested > CoolDelay) Heat = Math.Max(0, Heat - CoolRate * dt);
        }
    }

    /// <summary>One thing the swing could hit, measured from the player's eye by the engine side.</summary>
    public struct MeleeCandidate
    {
        /// <summary>Which separate thing this is (one enemy, one loose chunk, one gun): the engine side numbers them.</summary>
        public int Target;
        /// <summary>The part's id as a number, or -1 for something that is not a body part (a gun).</summary>
        public int Part;
        /// <summary>A part still on a living enemy (these come first).</summary>
        public bool Living;
        /// <summary>Metres from the eye to the nearest point of it.</summary>
        public float Distance;
        /// <summary>Degrees to the side of the crosshair (either way).</summary>
        public float Yaw;
        /// <summary>Degrees above or below the crosshair.</summary>
        public float Pitch;
        /// <summary>The engine side's own number for this candidate (where it measured it), untouched by the rules.</summary>
        public int Tag;
    }

    /// <summary>
    /// Which things a swing hits. The swing is a wedge in front of the eye: out to the reach, its arc wide, and
    /// <see cref="MeleeRules.VerticalHalfAngle"/> up and down. On each separate thing it takes the part nearest the
    /// crosshair (the middle of the blade's path), living enemies before loose things, nearest the crosshair first,
    /// up to the item's limit. The same test drives the in-range mark, so the mark never promises a hit the
    /// swing will not make.
    /// </summary>
    public static class MeleeTargeting
    {
        public static bool InSwing(in MeleeCandidate c, MeleeSpec spec, float verticalHalf)
        {
            return c.Distance <= spec.Reach && Math.Abs(c.Yaw) <= spec.Arc * 0.5f && Math.Abs(c.Pitch) <= verticalHalf;
        }

        public static float OffCentre(in MeleeCandidate c) => (float)Math.Sqrt(c.Yaw * c.Yaw + c.Pitch * c.Pitch);

        /// <summary>Fills <paramref name="into"/> with what the swing hits, best first. Returns how many.</summary>
        public static int Choose(IReadOnlyList<MeleeCandidate> all, MeleeSpec spec, float verticalHalf, List<MeleeCandidate> into)
        {
            into.Clear();
            if (all == null || spec == null) return 0;
            for (int i = 0; i < all.Count; i++)
            {
                MeleeCandidate c = all[i];
                if (!InSwing(c, spec, verticalHalf)) continue;
                int found = -1;
                for (int j = 0; j < into.Count; j++) if (into[j].Target == c.Target) { found = j; break; }
                if (found < 0) into.Add(c);
                else if (Better(c, into[found])) into[found] = c;
            }
            into.Sort(OrderBy);
            // A cut is always exactly one part, whatever the item's limit says.
            int limit = spec.Verb == MeleeVerb.Cut ? 1 : Math.Max(1, spec.MaxTargets);
            if (into.Count > limit) into.RemoveRange(limit, into.Count - limit);
            return into.Count;
        }

        static bool Better(in MeleeCandidate a, in MeleeCandidate b)
        {
            float oa = OffCentre(a), ob = OffCentre(b);
            if (Math.Abs(oa - ob) > 1e-4f) return oa < ob;
            return a.Distance < b.Distance;
        }

        static readonly Comparison<MeleeCandidate> OrderBy = Order;

        static int Order(MeleeCandidate a, MeleeCandidate b)
        {
            if (a.Living != b.Living) return a.Living ? -1 : 1;
            int c = OffCentre(a).CompareTo(OffCentre(b));
            if (c != 0) return c;
            c = a.Distance.CompareTo(b.Distance);
            return c != 0 ? c : a.Target.CompareTo(b.Target);
        }
    }

    /// <summary>What one swing does to one body part.</summary>
    public struct MeleeBlow
    {
        /// <summary>The part struck (a takedown always strikes the head).</summary>
        public BodyPartId Part;
        public float Damage;
        /// <summary>A limb comes off whatever it has left (BodyModel.ApplySever).</summary>
        public bool Sever;
        /// <summary>A living enemy is knocked down and sent sliding at <see cref="Shove"/> m/s.</summary>
        public bool Knockdown;
        public float Shove;
        /// <summary>A living enemy is startled (the pan's head ring).</summary>
        public bool Startle;
        /// <summary>The big clang (the pan on a head).</summary>
        public bool Clang;
        /// <summary>The pan's helmet hit: rung like a bell, it stands dazed (no shooting, no walking) for the knockdown time, without being thrown.</summary>
        public bool Dazed;
        /// <summary>The knife's kill from behind.</summary>
        public bool Takedown;
        /// <summary>Landed with the outer part of the reach.</summary>
        public bool Sweet;
        /// <summary>Extra freeze in milliseconds, before the cap.</summary>
        public float ExtraHitstopMs;
    }

    /// <summary>
    /// What each verb does to a body (research report, ranked shortlist). Plain rules: the engine side measures
    /// distances and angles, these decide what the hit means, and the same functions drive the HUD's cues.
    /// </summary>
    public static class MeleeRules
    {
        /// <summary>No melee freeze is ever longer than this (Sakurai: stronger hits freeze longer, up to a cap).</summary>
        public const float HitstopCapMs = 120f;
        /// <summary>Each extra enemy hit by one swing shortens the freeze by this share (Sakurai: shorter with more around).</summary>
        public const float CrowdShare = 0.25f;
        /// <summary>The swing's wedge reaches this far above and below the crosshair, in degrees.</summary>
        public const float VerticalHalfAngle = 40f;
        /// <summary>A knocked-down enemy cannot shoot for this long, in seconds.</summary>
        public const float KnockdownSeconds = 1.2f;
        /// <summary>Nothing is ever sent sliding faster than this by a swing (m/s).</summary>
        public const float MaxShove = 10f;

        public static bool IsLimb(BodyPartId id) => id != BodyPartId.Head && id != BodyPartId.Torso;
        public static bool IsHand(BodyPartId id) => id == BodyPartId.LeftHand || id == BodyPartId.RightHand;

        /// <summary>True when a hit this far from the eye lands with the outer part of the reach.</summary>
        public static bool Sweet(MeleeSpec spec, float distance)
        {
            return spec.SweetSpot > 0f && distance >= spec.Reach - spec.SweetSpot && distance <= spec.Reach + 1e-4f;
        }

        /// <summary>
        /// True when the player stands in the enemy's rear arc. <paramref name="angleFromFacing"/> is the angle, in
        /// degrees, between the way the enemy faces and the way from the enemy to the player (180 is right behind).
        /// </summary>
        public static bool IsBehind(float angleFromFacing, float rearArc)
        {
            return Math.Abs(angleFromFacing) >= 180f - Math.Max(0f, rearArc) * 0.5f;
        }

        /// <summary>
        /// The knife's takedown: a stab, on a living enemy that still has its head, from its rear arc, while it has
        /// not seen the player lately. The HUD's TAKEDOWN cue asks this same question about the same target.
        /// </summary>
        public static bool IsTakedown(MeleeSpec spec, bool living, bool headOnBody, float angleFromFacing, bool awareOfPlayer)
        {
            return spec != null && spec.Verb == MeleeVerb.Stab && spec.TakedownDamage > 0f && living && headOnBody
                   && !awareOfPlayer && IsBehind(angleFromFacing, spec.RearArc);
        }

        /// <summary>
        /// What the swing does to one part. <paramref name="partHp"/> is what that part has left (for a takedown,
        /// what the head has left); <paramref name="living"/> is true when the part is on a living enemy.
        /// </summary>
        public static MeleeBlow Blow(MeleeSpec spec, BodyPartId part, float partHp, bool living, bool sweet, bool takedown)
        {
            var blow = new MeleeBlow { Part = part, Sweet = sweet };
            float damage = spec.DamageFor(part) * (sweet ? Math.Max(1f, spec.SweetMultiplier) : 1f);
            blow.Damage = damage;
            switch (spec.Verb)
            {
                case MeleeVerb.Cut:
                    blow.Sever = IsLimb(part);
                    break;
                case MeleeVerb.Launch:
                    if (living)
                    {
                        blow.Knockdown = true;
                        blow.Shove = Math.Min(MaxShove, sweet && spec.SweetLaunchSpeed > 0f ? spec.SweetLaunchSpeed : spec.LaunchSpeed);
                    }
                    break;
                case MeleeVerb.Crush:
                    if (IsLimb(part))
                    {
                        blow.Sever = true;
                        blow.Damage = Math.Max(damage, partHp);
                    }
                    else if (part == BodyPartId.Torso && living)
                    {
                        blow.Knockdown = true;
                        blow.Shove = Math.Min(MaxShove, spec.LaunchSpeed);
                    }
                    break;
                case MeleeVerb.Stab:
                    if (takedown && living)
                    {
                        blow.Part = BodyPartId.Head;
                        blow.Takedown = true;
                        blow.Damage = Math.Max(spec.TakedownDamage, partHp);
                    }
                    else if (IsHand(part))
                    {
                        // A stab through the gun hand always takes it: the disarm is the knife's job from the front.
                        blow.Damage = Math.Max(damage, partHp);
                    }
                    break;
                case MeleeVerb.Bonk:
                    if (part == BodyPartId.Head && living)
                    {
                        blow.Clang = true;
                        blow.Dazed = true;
                        blow.Startle = true;
                        blow.ExtraHitstopMs = spec.BonusHitstopMs;
                    }
                    break;
            }
            return blow;
        }

        /// <summary>
        /// The freeze for one swing's contact, in seconds: the item's own (or its sweet-spot one) plus any extra,
        /// shortened when the swing hit several enemies, never longer than <see cref="HitstopCapMs"/>.
        /// </summary>
        public static float HitstopSeconds(MeleeSpec spec, bool sweet, float extraMs, int enemiesHit)
        {
            float ms = (sweet && spec.SweetHitstopMs > 0f ? spec.SweetHitstopMs : spec.HitstopMs) + Math.Max(0f, extraMs);
            if (enemiesHit > 1) ms /= 1f + CrowdShare * (enemiesHit - 1);
            return Math.Max(0f, Math.Min(HitstopCapMs, ms)) / 1000f;
        }

        /// <summary>
        /// The push back on a floating player when a swing connects (m/s, never more than <paramref name="cap"/>):
        /// the heavier the thing hit against the player, the more comes back (Heavenly Bodies: the struck thing's
        /// mass sets the reaction). A wall or the ground is infinitely heavy. Nothing is pushed back for a miss.
        /// </summary>
        public static float PushBack(float targetMass, float playerMass, float swingSpeed, float cap)
        {
            if (!(targetMass > 0f) || swingSpeed <= 0f || cap <= 0f) return 0f;
            float share = float.IsPositiveInfinity(targetMass) ? 1f : targetMass / (targetMass + Math.Max(1f, playerMass));
            return Math.Min(cap, swingSpeed * share);
        }

        /// <summary>A shoved body slides to a stop: its speed after <paramref name="dt"/> seconds of friction (m/s per second).</summary>
        public static float SlideAfter(float speed, float dt, float friction)
        {
            if (speed <= 0f) return 0f;
            return Math.Max(0f, speed - Math.Max(0f, friction) * Math.Max(0f, dt));
        }

        /// <summary>
        /// The controller's default melee button is also Roll Right while floating free (every other control on foot
        /// is taken). It swings everywhere except while floating free, where it rolls; any other binding always swings.
        /// </summary>
        public static bool SwingFromPress(bool sharedWithRoll, bool floatingFree) => !(sharedWithRoll && floatingFree);

        /// <summary>
        /// The sound event for something an item does: "melee." + the id in lower case with underscores + "." +
        /// what (LaserSword, "hum" is melee.laser_sword.hum). Events with no clip in the manifest play nothing.
        /// </summary>
        public static string SoundEvent(string id, string what)
        {
            var b = new System.Text.StringBuilder("melee.");
            for (int i = 0; id != null && i < id.Length; i++)
            {
                char c = id[i];
                if (char.IsUpper(c) && i > 0) b.Append('_');
                b.Append(char.ToLowerInvariant(c));
            }
            return b.Append('.').Append(what).ToString();
        }

        /// <summary>How far off the player is heard while holding this item: never less than its own noise.</summary>
        public static float HeardFrom(float heardWithout, MeleeSpec held) => held == null ? heardWithout : Math.Max(heardWithout, held.Noise);
    }

    /// <summary>
    /// Where melee items lie. At most one per world, at the first base whose seed says so (about one base in
    /// <see cref="BasesPerItem"/>), never the base that holds the rare gun, at its last loot point. Which item it is
    /// comes from the world's own seeded stream, weighted by rarity. Once taken, never back that run.
    /// </summary>
    public static class MeleeLoot
    {
        public const int BasesPerItem = 3;

        /// <summary>Whether the base with this seed could hold the world's melee item. Spread evenly, never random at runtime.</summary>
        public static bool BaseHoldsOne(int baseSeed)
        {
            // A different mix from the rare gun's, so the two choices do not follow each other.
            uint h = (uint)baseSeed * 2654435761u;
            h ^= h >> 16;
            h *= 2246822507u;
            h ^= h >> 13;
            h *= 3266489909u;
            h ^= h >> 16;
            return h % BasesPerItem == 0;
        }

        /// <summary>The one base on a world that holds a melee item, or -1: the first that qualifies and is not <paramref name="rareGunBase"/>.</summary>
        public static int PickBase(IList<int> baseSeeds, int rareGunBase)
        {
            if (baseSeeds == null) return -1;
            for (int i = 0; i < baseSeeds.Count; i++)
                if (i != rareGunBase && BaseHoldsOne(baseSeeds[i])) return i;
            return -1;
        }

        /// <summary>
        /// Which loot point it lies at: the last. The rare gun takes the first, so if the rare gun is at this base
        /// too and there is only one point, there is no room (-1).
        /// </summary>
        public static int LootPoint(int lootPoints, bool rareGunHere)
        {
            if (lootPoints <= 0) return -1;
            int last = lootPoints - 1;
            return rareGunHere && last == 0 ? -1 : last;
        }

        /// <summary>
        /// Which item, by weight: <paramref name="roll"/> is a fair number from 0 (inclusive) to 1 (exclusive) from
        /// the world's seeded stream. Items with no weight are never picked; null if nothing has weight.
        /// </summary>
        public static string PickItem(double roll, IReadOnlyList<MeleeSpec> items)
        {
            if (items == null) return null;
            double total = 0;
            for (int i = 0; i < items.Count; i++) if (items[i] != null && items[i].RarityWeight > 0f) total += items[i].RarityWeight;
            if (total <= 0) return null;
            double at = Math.Max(0, Math.Min(0.999999999, roll)) * total;
            string last = null;
            for (int i = 0; i < items.Count; i++)
            {
                MeleeSpec s = items[i];
                if (s == null || s.RarityWeight <= 0f) continue;
                last = s.Id;
                if (at < s.RarityWeight) return s.Id;
                at -= s.RarityWeight;
            }
            return last;
        }
    }

    /// <summary>The melee HUD's words (plain C#, so they can be checked without the engine).</summary>
    public static class MeleeWords
    {
        /// <summary>A display name without its "(name TBD)" tag.</summary>
        public static string Short(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "";
            int cut = displayName.IndexOf(" (", System.StringComparison.Ordinal);
            return cut > 0 ? displayName.Substring(0, cut) : displayName;
        }

        public static string HeldLine(MeleeSpec spec, string key, bool locked)
        {
            string name = Short(spec.DisplayName).ToUpperInvariant();
            if (locked) return name + "   OVERHEATED";
            return string.IsNullOrEmpty(key) ? name : name + "   (" + key + ": SWING)";
        }

        public static string Note(MeleeSpec spec, bool onGround, string interactKey)
        {
            string name = Short(spec.DisplayName).ToUpperInvariant();
            string take = onGround && !string.IsNullOrEmpty(interactKey) ? "  (" + interactKey + ": TAKE)" : "";
            return (onGround ? "ON THE GROUND: " : "IN HAND: ") + name + ". " + spec.PickupNote + take;
        }
    }
}
