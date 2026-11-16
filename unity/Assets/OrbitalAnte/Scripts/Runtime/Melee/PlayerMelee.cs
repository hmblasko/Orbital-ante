using System;
using System.Collections.Generic;
using OrbitalAnte.AI;
using OrbitalAnte.Audio;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Player;
using OrbitalAnte.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OrbitalAnte.Melee
{
    /// <summary>
    /// The player's melee slot (Part B section 9: one gun at a time plus a melee secondary slot; melee items are
    /// rare and must be found). It sits next to <see cref="PlayerWeapons"/> on the eye, for both the arena player
    /// and the astronaut, and is separate from the guns: one melee item at a time, picked up off the ground with
    /// Interact (taking another drops the one in hand), lost when you die, never worn out.
    ///
    /// A swing is a scripted motion with a physics result (research report: never a physical blade that can snag
    /// on scenery). The rules are plain C# in <c>MeleeRules.cs</c>: the swing's timing (<see cref="MeleeSwing"/>),
    /// which things are inside the wedge in front of the eye (<see cref="MeleeTargeting"/>, the very same test the
    /// in-range mark uses), and what each verb does (<see cref="MeleeRules.Blow"/>). This side only measures where
    /// things are, hands each hit to <see cref="Mannequin.ApplyHit"/> along the swing (so pieces fly the way you
    /// swung), raises <see cref="CombatEvents.RaisePlayerHit"/> (the ding, the marker, Focus and the gun ladder all
    /// follow), asks for the freeze through the outcome (TimeFx hands it to TimeFlow) and draws a stand-in.
    /// </summary>
    public sealed class PlayerMelee : MonoBehaviour
    {
        const int HitMask = (1 << Layers.Hitbox) | (1 << Layers.Gib) | (1 << Layers.Item);

        MeleeConfig _cfg;
        Camera _eye;
        PlayerWeapons _weapons;
        Transform _pivot;
        MeleeSwing _swing;
        MeleeHeat _heat;
        MeleeHud _hud;
        Transform _dropParent;

        readonly Collider[] _overlaps = new Collider[96];
        readonly List<MeleeCandidate> _candidates = new List<MeleeCandidate>(64);
        readonly List<MeleeCandidate> _chosen = new List<MeleeCandidate>(4);
        // Candidate.Target is an index into this list: the thing each candidate belongs to.
        readonly List<Object> _things = new List<Object>(32);
        readonly Dictionary<Object, int> _thingIndex = new Dictionary<Object, int>(32);
        bool _wasLocked;
        float _humLevel;

        /// <summary>The melee item in hand, or null.</summary>
        public MeleeItem Held { get; private set; }
        /// <summary>A melee item on the ground close enough to take, or null.</summary>
        public MeleeItem NearbyPickup { get; private set; }
        public float NearbyPickupScore { get; private set; } = float.MaxValue;
        /// <summary>True when a swing now would hit a living enemy (the in-range mark).</summary>
        public bool InRange { get; private set; }
        /// <summary>True when a swing now would be the knife's takedown (the TAKEDOWN cue).</summary>
        public bool TakedownReady { get; private set; }
        public MeleeSwing Swing => _swing;
        public MeleeHeat Heat => _heat;
        /// <summary>How far off the player is heard because of what they hold (the sword's hum), 0 for nothing.</summary>
        public float Noise => Held != null ? Held.Spec.Noise : 0f;
        /// <summary>Walking speed times this while holding the item (the mailbox is heavy).</summary>
        public float WalkSlow => Held != null ? Mathf.Clamp(Held.Spec.WalkSlow, 0.1f, 1f) : 1f;
        /// <summary>Presses the swing as if the button went down this frame. For tests.</summary>
        public bool ForcePress;
        /// <summary>How many things the last contact hit, for tests and the debug readout.</summary>
        public int LastContactCount { get; private set; }

        /// <summary>The speed the player carries, for an item they let go of. Null in the arena (the controller's velocity is used).</summary>
        public Func<Vector3> CarriedVelocity;
        /// <summary>The player's "up" right now; null means +Y.</summary>
        public Func<Vector3> UpNow;
        /// <summary>True while floating free in zero-G: the controller's shared bumper rolls, and hits push you back.</summary>
        public Func<bool> FloatingFree;
        /// <summary>Called with the change of velocity a hit pushes a floating player back by (world space).</summary>
        public Action<Vector3> PushBack;
        /// <summary>True while inside a ship, where nothing loose is kept.</summary>
        public Func<bool> InShip;
        /// <summary>True when the player can act (alive, not dazed).</summary>
        public Func<bool> CanAct;

        public void Initialize(Camera eye, PlayerWeapons weapons, Transform dropParent)
        {
            _cfg = MeleeConfig.Load();
            _eye = eye;
            _weapons = weapons;
            _dropParent = dropParent;
            _pivot = new GameObject("MeleeHolder").transform;
            _pivot.gameObject.layer = Layers.Player;
            _pivot.SetParent(eye.transform, false);
            _pivot.localPosition = RestPivot;
            _swing = new MeleeSwing(new MeleeSpec());
            _heat = new MeleeHeat(null);
            _hud = MeleeHud.Create(transform);
            if (_weapons != null) _weapons.YieldPickup = score => BetterPickupThan(score);
        }

        public void SetDropParent(Transform parent) => _dropParent = parent;

        void OnDisable()
        {
            if (_hud != null) _hud.Hide();
            SetHum(0f);
            if (_swing != null) _swing.Cancel();
            InRange = TakedownReady = false;
        }

        void OnDestroy()
        {
            if (_hud != null) Destroy(_hud.gameObject);
            SetHum(0f);
        }

        // ------------------------------------------------------------------ taking and losing

        /// <summary>Takes an item into the melee slot; the one already held is dropped (or, aboard, put away for good).</summary>
        public void Take(MeleeItem item)
        {
            if (item == null || item == Held || _pivot == null) return;
            DropHeld();
            Held = item;
            item.TakeByPlayer();
            item.transform.SetParent(_pivot, false);
            item.transform.localPosition = ItemOffset;
            item.transform.localRotation = ItemTurn(item.Spec.Verb);
            Look.ModelLibrary.SetIndoors(item.gameObject, InShip != null && InShip());
            _swing.Cancel();
            _swing.SetSpec(item.Spec);
            _heat.Set(item.Spec);
            _wasLocked = false;
            if (NearbyPickup == item) NearbyPickup = null;
            AudioDirector.PlayLocal("melee.pickup", 0.9f);
        }

        /// <summary>Lets go of the item in hand as a physics object in front of the face.</summary>
        public void DropHeld()
        {
            MeleeItem item = Held;
            if (item == null) return;
            Held = null;
            _swing.Cancel();
            SetHum(0f);
            if (InShip != null && InShip())
            {
                // Nothing loose is kept in the cabin (you walk there by rule, with nothing to rest on).
                Destroy(item.gameObject);
                return;
            }
            Vector3 carried = CarriedVelocity != null ? CarriedVelocity() : Vector3.zero;
            Vector3 up = UpNow != null ? UpNow() : Vector3.up;
            Transform t = _eye.transform;
            item.transform.position = t.position + t.forward * 0.55f - t.up * 0.2f;
            Transform parent = CombatWorld.DropParent != null && CarriedVelocity != null ? CombatWorld.DropParent : _dropParent;
            item.Drop(parent, carried + t.forward * 2f + up * 1.2f, new Vector3(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-4f, 4f), 0f));
            AudioDirector.PlayLocal("melee.drop", 0.7f);
        }

        /// <summary>Death: the item is gone (melee items are lost when you die).</summary>
        public void LoseHeld()
        {
            if (Held == null) return;
            MeleeItem item = Held;
            Held = null;
            _swing.Cancel();
            SetHum(0f);
            Destroy(item.gameObject);
        }

        /// <summary>True when the melee item on the ground is a better take than a gun with this pick-up score.</summary>
        bool BetterPickupThan(float gunScore)
        {
            if (!isActiveAndEnabled) return false;
            UpdatePickup();
            return NearbyPickup != null && NearbyPickupScore <= gunScore;
        }

        void UpdatePickup()
        {
            NearbyPickup = null;
            NearbyPickupScore = float.MaxValue;
            Vector3 eye = _eye.transform.position, forward = _eye.transform.forward;
            float range = Mathf.Max(0.5f, _cfg.pickupRange);
            var all = MeleeItem.All;
            for (int i = 0; i < all.Count; i++)
            {
                MeleeItem m = all[i];
                if (m == null || m.Held || Time.unscaledTime - m.DroppedAt < 0.35f) continue;
                Vector3 to = m.transform.position - eye;
                float distance = to.magnitude;
                if (distance > range) continue;
                // The same score as a gun's: the one looked at, or anything right at your feet.
                float angle = distance > 0.01f ? Vector3.Angle(forward, to) : 0f;
                if (angle > 40f && distance > 1.9f) continue;
                float score = angle + distance * 10f;
                if (score < NearbyPickupScore)
                {
                    NearbyPickupScore = score;
                    NearbyPickup = m;
                }
            }
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (_eye == null || GameSession.Paused || GameInput.Asset == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool canAct = CanAct == null || CanAct();

            UpdatePickup();
            float gunScore = _weapons != null && _weapons.NearbyPickup != null ? _weapons.NearbyPickupScore : float.MaxValue;
            if (canAct && NearbyPickup != null && NearbyPickupScore <= gunScore && GameInput.Interact.WasPressedThisFrame())
                Take(NearbyPickup);

            if (Held == null)
            {
                InRange = TakedownReady = false;
                _hud.Show(null, NearbyPickup, false, false, 0f, false);
                return;
            }

            MeleeSpec spec = Held.Spec;
            _heat.Tick(dt);
            if (_wasLocked && !_heat.Locked) AudioDirector.PlayLocal(MeleeRules.SoundEvent(spec.Id, "ready"), 0.7f);
            _wasLocked = _heat.Locked;
            Held.SetGlow(_heat.Locked ? 0f : 1f);

            bool restarted = _swing.Tick(dt);
            if (restarted) OnSwingStarted(spec);

            bool pressed = ForcePress || (GameInput.Melee != null && GameInput.Melee.WasPressedThisFrame());
            ForcePress = false;
            if (pressed && canAct && SwingAllowedFromThisPress())
            {
                if (_heat.Locked) AudioDirector.PlayLocal(MeleeRules.SoundEvent(spec.Id, "locked"), 0.6f);
                else if (_swing.Press()) OnSwingStarted(spec);
            }

            // One look at what is in front: the cue, and the contact if the swing can hit now.
            GatherCandidates(spec);
            MeleeTargeting.Choose(_candidates, spec, _cfg.verticalHalfAngle, _chosen);
            InRange = false;
            TakedownReady = false;
            for (int i = 0; i < _chosen.Count; i++)
            {
                if (!_chosen[i].Living) continue;
                InRange = true;
                if (i == 0) TakedownReady = Takedown(spec, _chosen[i]);
            }
            if (_swing.CanHit && canAct) Contact(spec);

            SetHum(spec.Noise > 0f && !_heat.Locked ? 1f : 0f);
            _hud.Show(Held, NearbyPickup, InRange && _cfg.showInRange, TakedownReady, _heat.Fill, _heat.Locked);
            Animate(spec);
        }

        /// <summary>The controller's default melee button is Roll Right too: while floating free it rolls instead.</summary>
        bool SwingAllowedFromThisPress()
        {
            bool floating = FloatingFree != null && FloatingFree();
            if (!floating || GameInput.Melee == null) return true;
            var control = GameInput.Melee.activeControl;
            bool shared = false;
            if (control != null && GameInput.SuitRollRight != null)
            {
                var roll = GameInput.SuitRollRight.controls;
                for (int i = 0; i < roll.Count; i++) if (roll[i] == control) shared = true;
            }
            return MeleeRules.SwingFromPress(shared, floating);
        }

        void OnSwingStarted(MeleeSpec spec)
        {
            if (_heat.Locked) { _swing.Cancel(); return; }
            if (_heat.Swung()) AudioDirector.PlayLocal(MeleeRules.SoundEvent(spec.Id, "overheat"), 0.9f);
            AudioDirector.PlayLocal(MeleeRules.SoundEvent(spec.Id, "swing"), 0.8f, UnityEngine.Random.Range(0.95f, 1.05f));
        }

        void SetHum(float level)
        {
            if (Mathf.Approximately(level, _humLevel)) return;
            _humLevel = level;
            AudioDirector d = AudioDirector.Instance;
            if (d != null) d.SetLoop(MeleeRules.SoundEvent(MeleeCatalog.LaserSwordId, "hum"), level * 0.5f, 0.25f);
        }

        // ------------------------------------------------------------------ what is in front

        int ThingIndex(Object thing)
        {
            if (_thingIndex.TryGetValue(thing, out int i)) return i;
            i = _things.Count;
            _things.Add(thing);
            _thingIndex[thing] = i;
            return i;
        }

        /// <summary>Measures every hitbox, loose piece and loose gun near the eye into candidates for the rules.</summary>
        void GatherCandidates(MeleeSpec spec)
        {
            _candidates.Clear();
            _things.Clear();
            _thingIndex.Clear();
            Transform eye = _eye.transform;
            Vector3 from = eye.position;
            int n = Physics.OverlapSphereNonAlloc(from, spec.Reach + 0.6f, _overlaps, HitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = _overlaps[i];
                if (col == null || !col.enabled) continue;
                Object thing;
                int part = -1;
                bool living = false;
                Weapon gun = col.GetComponentInParent<Weapon>();
                BodyPart body = gun == null ? col.GetComponentInParent<BodyPart>() : null;
                MeleeItem loose = gun == null && body == null ? col.GetComponentInParent<MeleeItem>() : null;
                if (gun != null)
                {
                    if (gun.Hold == WeaponHold.HeldByPlayer) continue;
                    thing = gun;
                }
                else if (body != null && body.Owner != null && body.Owner.Model != null)
                {
                    Mannequin m = body.Owner;
                    part = (int)body.Id;
                    living = m.Model.OnBody(body.Id);
                    Gib gib = living ? null : m.GibForGroup(m.Model.Group(body.Id));
                    thing = living ? (Object)m : gib != null ? gib : (Object)body;
                }
                else if (loose != null && !loose.Held) thing = loose;
                else continue;

                Vector3 p = col.ClosestPoint(from);
                Vector3 to = p - from;
                float distance = to.magnitude;
                float yaw = 0f, pitch = 0f;
                if (distance > 0.05f)
                {
                    Vector3 local = eye.InverseTransformDirection(to);
                    yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                    // Nothing through a wall.
                    if (distance > 0.3f && Physics.Linecast(from, p - to.normalized * 0.05f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                }
                _candidates.Add(new MeleeCandidate
                {
                    Target = ThingIndex(thing), Part = part, Living = living, Distance = distance, Yaw = yaw, Pitch = pitch,
                });
            }
        }

        /// <summary>The knife's takedown on this target: the same question the TAKEDOWN cue asks.</summary>
        bool Takedown(MeleeSpec spec, in MeleeCandidate c)
        {
            if (spec.Verb != MeleeVerb.Stab || !c.Living || !(_things[c.Target] is Mannequin m)) return false;
            var combat = m.GetComponent<MannequinCombat>();
            bool aware = combat != null && combat.AwareOfPlayer;
            return MeleeRules.IsTakedown(spec, m.Alive, m.Model.OnBody(BodyPartId.Head), AngleFromFacing(m), aware);
        }

        float AngleFromFacing(Mannequin m)
        {
            Vector3 up = m.Up;
            Vector3 face = Vector3.ProjectOnPlane(m.transform.forward, up);
            Vector3 toPlayer = Vector3.ProjectOnPlane(_eye.transform.position - m.transform.position, up);
            if (face.sqrMagnitude < 1e-6f || toPlayer.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.Angle(face, toPlayer);
        }

        /// <summary>The way a swing moves through what it hits: across for a sweep, ahead for a stab, down for a bonk.</summary>
        Vector3 SwingDirection(MeleeVerb verb)
        {
            Transform t = _eye.transform;
            switch (verb)
            {
                case MeleeVerb.Stab: return t.forward;
                case MeleeVerb.Bonk: return (t.forward - t.up * 0.45f).normalized;
                default: return (t.forward * 0.6f + t.right * 0.8f).normalized;
            }
        }

        // ------------------------------------------------------------------ contact

        void Contact(MeleeSpec spec)
        {
            LastContactCount = 0;
            Transform eye = _eye.transform;
            Vector3 dir = SwingDirection(spec.Verb);
            bool floating = FloatingFree != null && FloatingFree();

            if (_chosen.Count == 0)
            {
                // Nothing to hit, but a wall or the ground within reach: a thunk, and in zero-G a shove off it.
                if (Physics.Raycast(eye.position, eye.forward, out RaycastHit wall, spec.Reach, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    _swing.MarkContact();
                    AudioDirector.Play("melee.hit.wall", wall.point, 0.8f);
                    if (floating) Push(float.PositiveInfinity, spec);
                }
                return;
            }

            _swing.MarkContact();
            int enemies = 0;
            for (int i = 0; i < _chosen.Count; i++) if (_chosen[i].Living) enemies++;

            // Work out every blow first, so the freeze is one number for the whole swing.
            var blows = new MeleeBlow[_chosen.Count];
            bool anySweet = false;
            float extra = 0f, mass = 0f;
            for (int i = 0; i < _chosen.Count; i++)
            {
                MeleeCandidate c = _chosen[i];
                bool sweet = MeleeRules.Sweet(spec, c.Distance);
                anySweet |= sweet && c.Living;
                Object thing = _things[c.Target];
                if (c.Part >= 0 && BodyOf(thing, c.Part, out Mannequin m))
                {
                    bool takedown = i == 0 && Takedown(spec, c);
                    var id = (BodyPartId)c.Part;
                    float hp = m.Model.Hp(takedown ? BodyPartId.Head : id);
                    blows[i] = MeleeRules.Blow(spec, id, hp, c.Living, sweet, takedown);
                    extra = Mathf.Max(extra, blows[i].ExtraHitstopMs);
                    mass += c.Living ? MassOnBody(m) : GibMass(thing);
                }
                else mass += ThingMass(thing);
            }
            float stop = MeleeRules.HitstopSeconds(spec, anySweet, extra, enemies);

            for (int i = 0; i < _chosen.Count; i++)
            {
                MeleeCandidate c = _chosen[i];
                if (!_swing.Strike(c.Target * 32 + c.Part + 1)) continue;
                Object thing = _things[c.Target];
                if (c.Part >= 0 && BodyOf(thing, c.Part, out Mannequin m)) StrikeBody(spec, m, blows[i], c, dir, stop);
                else StrikeThing(spec, thing, dir, stop);
                LastContactCount++;
            }
            if (floating) Push(mass, spec);
        }

        static bool BodyOf(Object thing, int part, out Mannequin m)
        {
            m = thing as Mannequin;
            if (m == null && thing is Gib g) m = g.Owner;
            if (m == null && thing is BodyPart b) m = b.Owner;
            return m != null && m.Model != null && part >= 0 && part < BodyLayout.Count;
        }

        void StrikeBody(MeleeSpec spec, Mannequin m, MeleeBlow blow, in MeleeCandidate c, Vector3 dir, float stop)
        {
            BodyPart target = m.Part(blow.Part);
            if (target == null) return;
            bool living = c.Living && m.Alive;
            Vector3 point = target.Hitbox != null ? target.Hitbox.ClosestPoint(_eye.transform.position) : target.transform.position;
            var hit = new HitInfo { Damage = blow.Damage, Point = point, Direction = dir, Normal = -dir, Sever = blow.Sever };
            HitOutcome outcome = m.ApplyHit(target, hit);

            var combat = m.GetComponent<MannequinCombat>();
            if (living && m.Alive && combat != null)
            {
                if (blow.Knockdown) combat.KnockDown(dir * blow.Shove, _cfg.knockdownSeconds, _cfg.shoveFriction);
                else combat.Noticed();
            }
            if (blow.Startle && m.Alive)
            {
                var loco = m.GetComponent<MannequinLocomotion>();
                if (loco != null) loco.Startle(point + dir);
            }
            // What is loose (a corpse, a limb that came off, a chunk that was already off) flies on along the swing.
            if (!living || !m.Alive || outcome.Severed) LaunchLoose(spec, m, blow.Part, dir);

            outcome.HitstopSeconds = stop;
            if (outcome.Enemy) outcome.Juice = Mathf.Max(outcome.Juice, 0.25f);
            if (blow.Takedown) outcome.Label = "Takedown";
            CombatEvents.RaisePlayerHit(outcome);

            string sound = blow.Clang ? MeleeRules.SoundEvent(MeleeCatalog.FryingPanId, "clang") : blow.Takedown ? MeleeRules.SoundEvent(MeleeCatalog.ButterflyKnifeId, "takedown")
                         : blow.Sweet ? MeleeRules.SoundEvent(spec.Id, "sweet") : MeleeRules.SoundEvent(spec.Id, "hit");
            AudioDirector.Play(sound, point, 1f);
        }

        void LaunchLoose(MeleeSpec spec, Mannequin m, BodyPartId part, Vector3 dir)
        {
            if (spec.Verb != MeleeVerb.Launch && spec.Verb != MeleeVerb.Crush) return;
            if (m == null || m.Model == null) return;
            Gib gib = m.GibForGroup(m.Model.Group(part));
            if (gib != null && gib.Body != null && !gib.Body.isKinematic)
                gib.Body.AddForce(dir * Mathf.Min(MeleeRules.MaxShove, _cfg.looseLaunch), ForceMode.VelocityChange);
        }

        void StrikeThing(MeleeSpec spec, Object thing, Vector3 dir, float stop)
        {
            Vector3 eye = _eye.transform.position;
            if (thing is Weapon gun)
            {
                Vector3 point = gun.transform.position;
                HitOutcome outcome = gun.Hit(new HitInfo { Damage = 1f, Point = point, Direction = dir, Normal = -dir });
                if (gun.Hold == WeaponHold.Dropped && gun.Body != null && !gun.Body.isKinematic)
                    gun.Body.AddForce(dir * _cfg.looseLaunch, ForceMode.VelocityChange);
                outcome.HitstopSeconds = outcome.Disarmed ? stop : 0f;
                CombatEvents.RaisePlayerHit(outcome);
                AudioDirector.Play("melee.hit.metal", point, 0.9f);
            }
            else if (thing is MeleeItem item)
            {
                item.Hit(new HitInfo { Damage = 1f, Point = item.transform.position, Direction = dir, Normal = -dir });
                if (item.Body != null && !item.Body.isKinematic) item.Body.AddForce(dir * _cfg.looseLaunch, ForceMode.VelocityChange);
                AudioDirector.Play("melee.hit.metal", item.transform.position, 0.7f);
            }
        }

        static float MassOnBody(Mannequin m)
        {
            float total = 0f;
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                BodyPart p = m.Part((BodyPartId)i);
                if (p != null && m.Model.OnBody((BodyPartId)i)) total += p.Mass;
            }
            return total > 0f ? total : 70f;
        }

        static float GibMass(Object thing) => thing is Gib g && g.Body != null ? g.Body.mass : 5f;

        static float ThingMass(Object thing)
        {
            if (thing is Weapon w && w.Body != null) return w.Body.mass;
            if (thing is MeleeItem i && i.Body != null) return i.Body.mass;
            return 1f;
        }

        /// <summary>Zero-G: the hit pushes a floating player back, by the struck mass against theirs, capped. Never a spin.</summary>
        void Push(float mass, MeleeSpec spec)
        {
            float speed = MeleeRules.PushBack(mass, _cfg.playerMass, spec.SwingSpeed, _cfg.pushBackCap);
            if (speed > 0f && PushBack != null) PushBack(-_eye.transform.forward * speed);
        }

        // ------------------------------------------------------------------ the stand-in's motion

        static readonly Vector3 RestPivot = new Vector3(0.24f, -0.30f, 0.30f);
        static readonly Vector3 ItemOffset = new Vector3(0f, 0f, 0.08f);

        static Quaternion ItemTurn(MeleeVerb verb)
        {
            // The item lies along its own +Y: tipped forward so it points ahead and a little up.
            switch (verb)
            {
                case MeleeVerb.Stab: return Quaternion.Euler(80f, 0f, 0f);
                case MeleeVerb.Crush: return Quaternion.Euler(55f, -10f, 0f);
                default: return Quaternion.Euler(60f, -12f, 0f);
            }
        }

        /// <summary>Swung in code: drawn back in the windup, through in the hit window, home in the recovery.</summary>
        void Animate(MeleeSpec spec)
        {
            float a = 0f;   // -1 drawn back, 0 at rest, +1 followed through
            double t = _swing.Elapsed;
            switch (_swing.Phase)
            {
                case SwingPhase.Windup: a = -Mathf.SmoothStep(0f, 1f, (float)(t / Mathf.Max(0.01f, spec.Windup))); break;
                case SwingPhase.Active: a = Mathf.Lerp(-1f, 1f, (float)((t - spec.Windup) / Mathf.Max(0.01f, spec.Active))); break;
                case SwingPhase.Recovery: a = 1f - Mathf.SmoothStep(0f, 1f, (float)((t - spec.Windup - spec.Active) / Mathf.Max(0.01f, spec.Recovery))); break;
            }
            Vector3 position = RestPivot;
            Quaternion turn;
            switch (spec.Verb)
            {
                case MeleeVerb.Stab:
                    position += new Vector3(-0.04f * Mathf.Abs(a), 0f, a < 0f ? 0.08f * a : 0.28f * a);
                    turn = Quaternion.identity;
                    break;
                case MeleeVerb.Bonk:
                    turn = Quaternion.Euler(a < 0f ? 60f * a : 45f * a, 0f, 0f);
                    position += new Vector3(-0.08f * Mathf.Abs(a), 0.08f * -a, 0f);
                    break;
                default:
                    // A flat sweep from the left across to the right, as wide as the item's arc, a little high to low.
                    float half = spec.Arc * 0.5f + 20f;
                    turn = Quaternion.Euler(10f * a, half * a, -25f * a);
                    position += new Vector3(-0.18f * (1f - Mathf.Abs(a)) * (a != 0f ? 1f : 0f), 0f, 0.05f * Mathf.Abs(a));
                    break;
            }
            // Overheated: the blade droops a little so the lock reads at a glance, as well as on the meter.
            if (_heat.Locked) turn = Quaternion.Euler(20f, 0f, 0f) * turn;
            _pivot.localPosition = position;
            _pivot.localRotation = turn;
        }
    }
}
