using System;

namespace OrbitalAnte.Spacewalk
{
    /// <summary>How the player holds themselves on foot (Matthew, 16 Nov 2026: "sneak, crouch, and lay prone").</summary>
    public enum Stance { Stand, Crouch, Prone }

    /// <summary>The stances' dials (on the Suit asset).</summary>
    [Serializable]
    public sealed class StanceRules
    {
        /// <summary>Eye height above the boots, metres, standing / crouched / prone.</summary>
        public float standEye = 1.62f, crouchEye = 1.05f, proneEye = 0.38f;
        /// <summary>How tall the body is, for the collider and whether there is room to get up.</summary>
        public float standHeight = 1.8f, crouchHeight = 1.2f, proneHeight = 0.6f;
        /// <summary>Walking speed against the ordinary walk.</summary>
        public float crouchSpeed = 0.55f, proneSpeed = 0.22f, sneakSpeed = 0.45f;
        /// <summary>Holding the stance button this long lies you down (or stands you up from prone), seconds.</summary>
        public float holdSeconds = 0.4f;
        /// <summary>Seconds to go from one height to the next.</summary>
        public float changeSeconds = 0.28f;
        /// <summary>
        /// How far off an enemy who has not seen you yet can pick you out, against its usual sight range. Lower is
        /// harder to see: a crouched shape is smaller, a body flat on the ground is mostly hidden by it.
        /// </summary>
        public float standSeen = 1f, crouchSeen = 0.65f, proneSeen = 0.4f;
        /// <summary>Moving makes you easier to pick out; keeping still, harder. Multiplies the above.</summary>
        public float movingSeen = 1f, stillSeen = 0.8f;
        /// <summary>How far off you can be heard, against an enemy's usual hearing (60 m on the Mannequin asset): walking
        /// 60 m, a crouched walk 18 m, sneaking 12 m, crawling 6 m, keeping still never. Sneaking is the quiet walk.</summary>
        public float walkHeard = 1f, crouchHeard = 0.3f, sneakHeard = 0.2f, proneHeard = 0.1f, stillHeard = 0f;
        /// <summary>The jets and the gun are loud whatever you are doing.</summary>
        public float jetsHeard = 1.6f, shotHeard = 2f;
        /// <summary>Seconds a shot stays "loud" after it is fired.</summary>
        public float shotLoudFor = 1.5f;
    }

    /// <summary>
    /// Standing, crouching and lying prone, and the quiet sneak. Plain C#, so it is tested outside Unity.
    ///
    /// One button, the same one as Jets Down (Ctrl, or B / Circle on a controller), because on the ground the jets
    /// have nothing to do: a tap crouches and a second tap stands you up again, holding it lies you down, and
    /// holding it again while prone stands you up. A tap while prone brings you up to a crouch. You only rise if
    /// there is room above you. Sneaking is a slow quiet walk: hold Sneak (Left Alt), or on a controller push the
    /// stick only part of the way. Jumping stands you up first.
    ///
    /// Stealth is only ever about being seen or heard sooner or later, never a hidden roll: what each stance does is
    /// in <see cref="StanceRules"/>, and once an enemy has seen you it knows where you are for a while whatever you do.
    /// </summary>
    public sealed class StanceState
    {
        public Stance Current { get; private set; } = Stance.Stand;
        /// <summary>Eye height right now, eased between stances.</summary>
        public float Eye { get; private set; } = -1f;
        /// <summary>True when the last <see cref="Step"/> wanted to rise but there was no room.</summary>
        public bool Blocked { get; private set; }

        float _held = -1f;
        bool _holdUsed, _wasOnFoot = true;

        public static float EyeOf(Stance s, StanceRules r) => s == Stance.Crouch ? r.crouchEye : s == Stance.Prone ? r.proneEye : r.standEye;
        public static float HeightOf(Stance s, StanceRules r) => s == Stance.Crouch ? r.crouchHeight : s == Stance.Prone ? r.proneHeight : r.standHeight;

        /// <summary>
        /// Moves on by <paramref name="dt"/>. <paramref name="buttonDown"/> is whether the stance button is held now.
        /// <paramref name="roomFor"/> says whether there is room to be that tall here (null: always room).
        /// <paramref name="onFoot"/> is false off the ground or floating: then the button is the jets and is ignored,
        /// and you are stood up as soon as you leave the ground.
        /// Returns true when the stance changed.
        /// </summary>
        public bool Step(float dt, bool buttonDown, bool onFoot, Func<Stance, bool> roomFor, StanceRules rules)
        {
            Stance before = Current;
            Blocked = false;
            bool landed = onFoot && !_wasOnFoot;
            _wasOnFoot = onFoot;
            if (landed && buttonDown)
            {
                // Still holding Jets Down from the landing: that press was for the jets. It counts for nothing until
                // it is let go and pressed again.
                _held = 0f;
                _holdUsed = true;
            }
            if (!onFoot)
            {
                _held = -1f;
                _holdUsed = false;
                // In the air nobody crouches: you are stood up, as soon as there is room (dropping off a ledge while
                // crawling under rock, you stay low until you are clear of it).
                if (Current != Stance.Stand && (roomFor == null || roomFor(Stance.Stand))) Current = Stance.Stand;
            }
            else if (buttonDown)
            {
                if (_held < 0f) { _held = 0f; _holdUsed = false; }
                else _held += dt;
                if (!_holdUsed && _held >= rules.holdSeconds)
                {
                    _holdUsed = true;
                    TryBe(Current == Stance.Prone ? Stance.Stand : Stance.Prone, roomFor);
                }
            }
            else
            {
                if (_held >= 0f && !_holdUsed)
                {
                    // A tap.
                    TryBe(Current == Stance.Stand ? Stance.Crouch : Current == Stance.Crouch ? Stance.Stand : Stance.Crouch, roomFor);
                }
                _held = -1f;
                _holdUsed = false;
            }

            float target = EyeOf(Current, rules);
            if (Eye < 0f) Eye = target;
            float rate = (rules.standEye - rules.proneEye) / Math.Max(0.05f, rules.changeSeconds) * dt;
            Eye = Eye < target ? Math.Min(target, Eye + rate) : Math.Max(target, Eye - rate);
            return Current != before;
        }

        /// <summary>Standing again with the eye at standing height straight away (going aboard, a new run).</summary>
        public void Reset()
        {
            Current = Stance.Stand;
            Eye = -1f;
            Blocked = false;
            _held = -1f;
            _holdUsed = false;
            _wasOnFoot = true;
        }

        /// <summary>Stands you up for a jump, if there is room. Returns true if you may jump now.</summary>
        public bool RiseToJump(Func<Stance, bool> roomFor)
        {
            if (Current == Stance.Stand) return true;
            if (Current == Stance.Prone) { TryBe(Stance.Crouch, roomFor); return false; }
            TryBe(Stance.Stand, roomFor);
            return Current == Stance.Stand;
        }

        void TryBe(Stance next, Func<Stance, bool> roomFor)
        {
            bool rising = HeightRank(next) > HeightRank(Current);
            if (rising && roomFor != null && !roomFor(next)) { Blocked = true; return; }
            Current = next;
        }

        static int HeightRank(Stance s) => s == Stance.Stand ? 2 : s == Stance.Crouch ? 1 : 0;

        /// <summary>Walking speed against the ordinary walk.</summary>
        public static float SpeedOf(Stance s, bool sneaking, StanceRules r)
        {
            float speed = s == Stance.Crouch ? r.crouchSpeed : s == Stance.Prone ? r.proneSpeed : 1f;
            return sneaking ? Math.Min(speed, r.sneakSpeed) : speed;
        }

        /// <summary>How far off you can be picked out by an enemy who has not seen you, against its sight range.</summary>
        public static float SeenFrom(Stance s, bool moving, StanceRules r)
        {
            float shape = s == Stance.Crouch ? r.crouchSeen : s == Stance.Prone ? r.proneSeen : r.standSeen;
            return shape * (moving ? r.movingSeen : r.stillSeen);
        }

        /// <summary>How far off you can be heard, against an enemy's hearing.</summary>
        public static float HeardFrom(Stance s, bool moving, bool sneaking, bool jets, bool shotJustNow, StanceRules r)
        {
            if (shotJustNow) return r.shotHeard;
            if (jets) return r.jetsHeard;
            if (!moving) return r.stillHeard;
            float heard = s == Stance.Crouch ? r.crouchHeard : s == Stance.Prone ? r.proneHeard : r.walkHeard;
            return sneaking ? Math.Min(heard, r.sneakHeard) : heard;
        }
    }
}
