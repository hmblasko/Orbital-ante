using OrbitalAnte.Combat;
using OrbitalAnte.Core;
using OrbitalAnte.Player;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Characters
{
    /// <summary>
    /// Lets a test dummy carry a gun so disarming can be playtested: it drops the gun when its gun
    /// hand goes, can have the gun shot out of its hand, picks guns back up with any hand left,
    /// fires worse with one hand, and can't fire at all with none. In Attack mode it shoots at you
    /// (no player damage yet; you get a red flash).
    /// </summary>
    [RequireComponent(typeof(Mannequin))]
    public class DummyGunner : MonoBehaviour, IWeaponHolder
    {
        public Weapon Weapon { get; private set; }
        int handSide = 1; // 0 = left, 1 = right

        Mannequin m;
        ProceduralGait gait;
        MannequinBrain brain;
        CombatRulesConfig rules;
        SeededRandom rng;
        int burstLeft;
        float nextBurst;
        float pickupCooldown;

        public int HandsOnWeapon => Weapon == null ? 0 : m.UsableHands;

        void Awake()
        {
            m = GetComponent<Mannequin>();
            gait = GetComponent<ProceduralGait>();
            brain = GetComponent<MannequinBrain>();
            rules = CombatRulesConfig.Get();
            m.PartLost += OnPartLost;
            m.Died += OnDied;
        }

        void Start()
        {
            rng = RunSeed.CreateStream("dummy.gunner." + (brain != null ? brain.SpawnIndex : 0));
            nextBurst = Time.time + 1f;
        }

        // ---- holding --------------------------------------------------------------------

        public bool Equip(Weapon w)
        {
            int side = MannequinAnatomy.HandUsable(m.Attached, false) ? 1 : MannequinAnatomy.HandUsable(m.Attached, true) ? 0 : -1;
            if (side < 0 || w == null || !w.IsLoose && w.Holder != null) return false;
            handSide = side;
            Weapon = w;
            // The hand bone points along -X (left) or +X (right); the gun points the same way.
            Vector3 along = side == 1 ? Vector3.right : Vector3.left;
            var rot = Quaternion.LookRotation(along, Vector3.up);
            var pos = along * 0.075f + Vector3.down * 0.01f - rot * w.Grip.localPosition;
            w.SetHeld(this, m.Rig.Hand[side], pos, rot);
            return true;
        }

        void DropWeapon(Vector3 velocity)
        {
            if (Weapon == null) return;
            var w = Weapon;
            Weapon = null;
            w.Drop(velocity, Random.insideUnitSphere * 8f);
            pickupCooldown = Time.time + 1.2f;
        }

        public bool OnWeaponShot(Weapon weapon, in HitInfo hit)
        {
            if (weapon != Weapon) return false;
            DropWeapon(hit.Direction * rules.knockAwaySpeed + Vector3.up * 2.5f);
            Hud.Instance?.Notify("Shot the gun right out of its hand!", NoticeKind.Combat);
            return true;
        }

        void OnPartLost(Mannequin _, BodyPartId id)
        {
            if (Weapon == null) return;
            if (!MannequinAnatomy.HandUsable(m.Attached, handSide == 0))
            {
                DropWeapon(Vector3.up * 2f + Random.insideUnitSphere * 1.5f);
                Hud.Instance?.Notify("Disarmed!", NoticeKind.Combat);
            }
        }

        void OnDied(Mannequin _)
        {
            DropWeapon(Vector3.up * 1.5f + Random.insideUnitSphere);
        }

        // ---- behaviour ------------------------------------------------------------------

        void Update()
        {
            if (!m.IsAlive) return;
            var state = m.State;
            bool canHold = Weapon != null && state != LocomotionState.HeadOnly && state != LocomotionState.Wriggle;
            gait.AimMain = canHold;
            gait.AimSide = handSide;
            gait.AimSupport = canHold && m.UsableHands >= 2;

            brain.MoveOverride = null;
            if (Weapon == null)
            {
                SeekPickup();
                return;
            }

            var player = FirstPersonController.Instance;
            if (player == null || MannequinBrain.GlobalMode != DummyWalkMode.Attack)
            {
                gait.AimPitch = 0f;
                return;
            }

            Vector3 target = player.Camera.transform.position + Vector3.down * 0.35f;
            Vector3 from = Weapon.Muzzle.position;
            Vector3 to = target - from;
            float flat = new Vector2(to.x, to.z).magnitude;
            gait.AimPitch = Mathf.Atan2(to.y, Mathf.Max(0.1f, flat)) * Mathf.Rad2Deg;

            int hands = m.UsableHands;
            if (!WeaponRules.CanFire(hands) || to.magnitude > rules.dummyRange) return;
            // Only shoot once roughly facing the target, so shots never leave sideways from the gun.
            Vector3 flatTo = new Vector3(to.x, 0f, to.z);
            if (Vector3.Angle(transform.forward, flatTo) > rules.dummyFireConeDegrees) return;

            if (Weapon.Empty)
            {
                Weapon.TryStartReload(hands, rules.oneHandReload);
                return;
            }
            if (Time.time < nextBurst) return;
            if (burstLeft <= 0) burstLeft = rng.Range(rules.dummyBurstMin, rules.dummyBurstMax + 1);
            if (!Weapon.ReadyToFire) return;

            float spread = WeaponRules.SpreadDegrees(Weapon.Def, false, Weapon.Bloom, hands) + rules.dummyExtraSpread;
            Weapon.Fire(new Ray(from, to.normalized), spread, transform, rules.dummyFireRateScale, false);
            burstLeft--;
            if (burstLeft <= 0 || Weapon.Def.fireMode == FireMode.SemiAuto && rng.NextFloat() < 0.3f)
            {
                burstLeft = 0;
                nextBurst = Time.time + rng.Range(rules.dummyPauseMin, rules.dummyPauseMax);
            }
        }

        void SeekPickup()
        {
            if (m.UsableHands == 0 || Time.time < pickupCooldown) return;
            if (m.State == LocomotionState.HeadOnly || m.State == LocomotionState.Wriggle) return;
            Weapon best = null;
            float bestD = rules.dummyPickupRadius;
            foreach (var w in Weapon.All)
            {
                if (w == null || !w.IsLoose) continue;
                if (w.Body.linearVelocity.sqrMagnitude > 1f) continue; // wait until it lands
                float d = Vector3.Distance(transform.position, w.transform.position);
                if (d < bestD) { bestD = d; best = w; }
            }
            if (best == null) return;
            Vector3 flatTo = best.transform.position - transform.position;
            flatTo.y = 0f;
            if (flatTo.magnitude < 0.9f)
            {
                if (Equip(best)) Hud.Instance?.Notify("A dummy picked a gun back up", NoticeKind.Info);
            }
            else brain.MoveOverride = best.transform.position;
        }
    }
}
