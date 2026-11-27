using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Enemies;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.AI
{
    /// <summary>
    /// Faction A's three abilities (Milestone 13), each with a tell and a way to stop it:
    /// the Scout's signal flare, the Line's covering fire and the Heavy's spin-up. Rules in
    /// <see cref="FactionRules"/>; this part only turns them into poses, sounds and shots.
    /// </summary>
    public sealed partial class MannequinCombat
    {
        /// <summary>What this one does that nobody else does (set by the spawner from its type).</summary>
        public FactionAbility Ability = FactionAbility.None;
        /// <summary>Whose side it is on (<see cref="Factions"/>); a flare is heard only by its own side.</summary>
        public string Faction = Factions.None;

        readonly FlareState _flare = new FlareState();
        readonly SpinUpState _spin = new SpinUpState();
        GameObject _flareGlow;
        bool _holdingForFlare;
        float _alarmMemoryUntil = -1000f;
        Transform _cell;
        Vector3 _cellScale;
        Weapon _cellOf;

        static EnemyConfig Dials => EnemyConfig.Load();

        int FreeSide => 1 - _gunSide;
        bool FreeHandUsable => _owner != null && _owner.Model != null && _owner.Model.HandUsable(FreeSide == MannequinRig.Left);

        /// <summary>Called on the first sight of the player in a while. A Scout begins its flare.</summary>
        void FirstSight(float now, bool squadAlready)
        {
            if (Ability != FactionAbility.Flare) return;
            if (_flare.Begin(now, Dials.flare, squadAlready)) OrbitalAnte.Audio.AudioDirector.Play("enemy.flare.raise", HeadPosition);
        }

        /// <summary>
        /// Moves the abilities on each frame. True while the Scout is raising its flare: it stands still with the
        /// arm up and does not shoot (that is the window to stop it).
        /// </summary>
        bool TickAbilities(float now)
        {
            if (Ability != FactionAbility.Flare || _loco == null) return false;
            // Knocked down mid-raise also spends the flare.
            bool can = _owner.Alive && Weapon != null && FreeHandUsable && !KnockedDown;
            bool fired = _flare.Tick(now, can);
            ShowFlareGlow(_flare.Raising ? _flare.RaiseAmount(now, Dials.flare) : 0f);
            if (fired) FireFlare();
            if (_flare.Raising)
            {
                _loco.Hold = true;
                _loco.Destination = null;
                _holdingForFlare = true;
                return true;
            }
            if (_holdingForFlare)
            {
                // Done (fired or stopped): let go of the spot, unless something else holds it there now.
                _holdingForFlare = false;
                if (_loco != null && Time.time >= _startledUntil && !KnockedDown) _loco.Hold = false;
            }
            return false;
        }

        void FireFlare()
        {
            FlareRules rules = Dials.flare;
            Vector3 up = transform.up;
            Vector3 burst = HeadPosition + up * rules.burstHeight;
            FlareFx.Launch(HeadPosition + up * 0.4f, burst, rules.hangSeconds);
            OrbitalAnte.Audio.AudioDirector.Play("enemy.flare.fire", HeadPosition);
            // Everyone near hears where the player was when it went up: a place, never a live track.
            if (CombatWorld.PlayerEye != null) CombatWorld.RaiseAlarm(transform.position, rules.wakeRadius, CombatWorld.PlayerEye.position, Faction);
        }

        /// <summary>Another enemy's flare: if it is near enough, go and look where the player was.</summary>
        void OnAlarm(Vector3 at, float radius, Vector3 playerAt, string side)
        {
            if (_owner == null || !_owner.Alive || _seesPlayer || !Factions.Same(side, Faction)) return;
            if ((transform.position - at).sqrMagnitude > radius * radius) return;
            float now = Time.time;
            _heardAt = playerAt;
            _heardTime = now;
            _heardPauseUntil = now + HeardPause;
            _alarmMemoryUntil = now + Dials.flare.memory;
        }

        /// <summary>
        /// Restless camps (Enemies/LullRules): after a long quiet the spawner sends this enemy's squad to the place
        /// the player was. It turns to it first, then runs until it is near, then comes on carefully; it remembers the place for long enough to get
        /// there (<paramref name="memory"/> seconds). A place, never a live track: a player who moves on is lost.
        /// </summary>
        public void SendToLook(Vector3 place, float memory, float walkInFrom = 50f)
        {
            if (_owner == null || !_owner.Alive || _seesPlayer) return;
            float now = Time.time;
            _heardAt = place;
            _heardTime = now;
            _heardPauseUntil = now + HeardPause;
            _alarmMemoryUntil = now + Mathf.Max(0f, memory);
            _sentUntil = _alarmMemoryUntil;
            _sentWalkIn = Mathf.Max(0f, walkInFrom);
        }

        /// <summary>Within this distance of the place it was sent, it stops running and comes on carefully.</summary>
        float _sentWalkIn;

        /// <summary>Until when it is on its way to a place it was sent to look (it goes at a run).</summary>
        float _sentUntil = -1000f;

        /// <summary>The glowing hand while the flare is raised (and the arm up, in <see cref="PoseFlareArm"/>).</summary>
        void ShowFlareGlow(float amount)
        {
            bool on = amount > 0f;
            if (!on)
            {
                if (_flareGlow != null && _flareGlow.activeSelf) _flareGlow.SetActive(false);
                return;
            }
            Transform hand = _rig.Hand[FreeSide];
            if (hand == null) return;
            if (_flareGlow == null)
            {
                _flareGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _flareGlow.name = "FlareGlow";
                Object.DestroyImmediate(_flareGlow.GetComponent<Collider>());
                var r = _flareGlow.GetComponent<Renderer>();
                r.sharedMaterial = FlareFx.GlowMaterial();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (_flareGlow.transform.parent != hand) _flareGlow.transform.SetParent(hand, false);
            if (!_flareGlow.activeSelf) _flareGlow.SetActive(true);
            _flareGlow.transform.localPosition = Vector3.zero;
            float size = (0.12f + 0.16f * amount) / Mathf.Max(0.1f, transform.lossyScale.y);
            _flareGlow.transform.localScale = Vector3.one * size;
        }

        /// <summary>The free arm straight up while the flare is raised, after the gun arms are posed.</summary>
        void PoseFlareArm()
        {
            if (Ability != FactionAbility.Flare || !_flare.Raising || !FreeHandUsable) return;
            Vector3 shoulder = _rig.UpperArm[FreeSide].position;
            float size = transform.lossyScale.y;
            SolveArm(FreeSide, shoulder + transform.up * (0.62f * size) + transform.forward * (0.08f * size));
        }

        /// <summary>
        /// The Heavy's gun: gates the trigger. False while it is still spinning up (the trigger stays off); the
        /// cell glows brighter through the spin. While braced it turns slowly.
        /// </summary>
        bool SpinGate(bool wantsToShoot, float now)
        {
            if (Ability != FactionAbility.SpinUp) return true;
            bool onFeet = _owner.State == MovementState.Intact || _owner.State == MovementState.OneLeg;
            SpinUpRules rules = Dials.spinUp;
            bool ready = false;
            if (!wantsToShoot || !onFeet || Weapon == null) _spin.Stop();
            else ready = _spin.Want(now, rules);
            if (_loco != null) _loco.MaxTurnRate = _spin.Braced ? rules.bracedTurn : 0f;
            if (_spin.Braced && _loco != null) _loco.Destination = null;
            ShowCell(_spin.Glow(now, rules));
            if (_spin.Step == SpinStep.Spinning && !_whining)
            {
                _whining = true;
                OrbitalAnte.Audio.AudioDirector.Play("enemy.heavy.spinup", HeadPosition);
            }
            if (_spin.Step != SpinStep.Spinning) _whining = false;
            return ready;
        }

        bool _whining;

        /// <summary>The gun's cell piece grows brighter and bigger through the spin.</summary>
        void ShowCell(float glow)
        {
            if (Weapon == null) return;
            if (_cellOf != Weapon)
            {
                // A new gun in its hands (picked up, or another's): look for its cell once. The old gun's cell is put back.
                if (_cell != null) _cell.localScale = _cellScale;
                _cellOf = Weapon;
                _cell = null;
                foreach (Transform t in Weapon.GetComponentsInChildren<Transform>(true))
                    if (t.name == "CellGlow")
                    {
                        // The gun remembers its cell's own size, so no holder ever takes a grown one as the base.
                        var size = t.GetComponent<CellSize>();
                        if (size == null) { size = t.gameObject.AddComponent<CellSize>(); size.Base = t.localScale; }
                        _cell = t;
                        _cellScale = size.Base;
                        break;
                    }
            }
            if (_cell != null) _cell.localScale = _cellScale * (0.55f + 0.85f * glow);
        }

        /// <summary>The Line's covering fire: while a squadmate is round the side, it takes long, wide bursts (still one squad turn at a time).</summary>
        bool Covering(float now) => Ability == FactionAbility.Suppress && Squad != null && Squad.FlankersOut > 0;

        /// <summary>Dead: the hand stops glowing, and the gun abilities are let go.</summary>
        void EndAbilities()
        {
            ShowFlareGlow(0f);
            LetGoOfGunAbilities();
        }

        /// <summary>The gun is leaving its hands (dropped, knocked away, dead): wind down, free the turn, put the cell back.</summary>
        void LetGoOfGunAbilities()
        {
            _spin.Stop();
            _whining = false;
            if (_loco != null) _loco.MaxTurnRate = 0f;
            if (_cell != null) _cell.localScale = _cellScale;
            _cell = null;
            _cellOf = null;
        }

        void SubscribeFaction() => CombatWorld.Alarm += OnAlarm;
        void UnsubscribeFaction() => CombatWorld.Alarm -= OnAlarm;
    }

    /// <summary>The flare itself: a bright ball that climbs to where it bursts and hangs there, then goes out.</summary>
    public sealed class FlareFx : MonoBehaviour
    {
        Vector3 _from, _to;
        float _born, _hang;
        const float Climb = 1.2f;

        public static void Launch(Vector3 from, Vector3 to, float hang)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SignalFlare";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = GlowMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.35f;
            var fx = go.AddComponent<FlareFx>();
            fx._from = from;
            fx._to = to;
            fx._born = Time.time;
            fx._hang = hang;
            // Under the world it was fired on, so it turns and re-centres with that world: its path is kept in the
            // world's own space from the start.
            Transform parent = CombatWorld.GroundParent;
            if (parent != null)
            {
                go.transform.SetParent(parent, true);
                fx._from = parent.InverseTransformPoint(from);
                fx._to = parent.InverseTransformPoint(to);
            }
        }

        void Update()
        {
            float age = Time.time - _born;
            if (age > Climb + _hang)
            {
                Destroy(gameObject);
                return;
            }
            Transform parent = transform.parent;
            float t = Mathf.Clamp01(age / Climb);
            Vector3 at = Vector3.Lerp(_from, _to, 1f - (1f - t) * (1f - t));
            if (parent == null) transform.position = at;
            else transform.localPosition = at;
            // Flickers a little as it hangs, and fades out over its last second.
            float fade = Mathf.Clamp01((Climb + _hang - age) / 1f);
            transform.localScale = Vector3.one * (0.35f + 0.6f * t) * (0.9f + 0.1f * Mathf.Sin(age * 23f)) * fade
                                   / Mathf.Max(0.01f, parent != null ? parent.lossyScale.x : 1f);
        }

        static Material _glow;
        /// <summary>A hot red-white glow, brighter than white so it blooms. Unlit: a real light would cost the Deck too much.</summary>
        public static Material GlowMaterial()
        {
            // Its own copy: the shared unlit cache keys by 8-bit colour, which would clip this brighter-than-white one.
            if (_glow == null)
            {
                _glow = new Material(Mat.Unlit(Color.white)) { name = "SignalFlareGlow" };
                Color c = new Color(4f, 1.3f, 0.9f);
                if (_glow.HasProperty("_BaseColor")) _glow.SetColor("_BaseColor", c);
                if (_glow.HasProperty("_Color")) _glow.SetColor("_Color", c);
            }
            return _glow;
        }
    }

    /// <summary>Kept on a gun's CellGlow piece: its size as built, before any Heavy grew it.</summary>
    public sealed class CellSize : MonoBehaviour
    {
        public Vector3 Base;
    }
}
