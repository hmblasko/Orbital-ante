using OrbitalAnte.Input;
using OrbitalAnte.Player;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// The player's gun hand: one gun at a time (the final loadout adds a melee slot and the rope
    /// gun). Fire, aim down sights, reload, recoil, and swap by picking a gun up off the floor.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerWeapons : MonoBehaviour, IWeaponHolder
    {
        /// <summary>The local player's weapons, for the HUD.</summary>
        public static PlayerWeapons Local { get; private set; }
        public Weapon Current { get; private set; }
        /// <summary>Test toggle: pretend the player has one hand, to feel the penalties.</summary>
        public bool OneHandedTest;

        FirstPersonController player;
        CombatRulesConfig rules;
        Transform viewRoot;
        float aimBlend;
        Vector3 kickOffset;
        float kickPitch;
        bool triggerHeldLastFrame;
        Weapon lookedAt;

        public int HandsOnWeapon => OneHandedTest ? 1 : 2;
        public Weapon LookedAtWeapon => lookedAt;

        void Awake()
        {
            player = GetComponent<FirstPersonController>();
            rules = CombatRulesConfig.Get();
            Local = this;
        }

        void OnDestroy() { if (Local == this) Local = null; }

        Transform ViewRoot
        {
            get
            {
                if (viewRoot == null)
                {
                    viewRoot = new GameObject("ViewModel").transform;
                    viewRoot.SetParent(player.ShakeRig, false);
                }
                return viewRoot;
            }
        }

        public void Equip(Weapon w)
        {
            if (w == null) return;
            if (Current != null) ThrowCurrent();
            Current = w;
            w.SetHeld(this, ViewRoot, -w.Grip.localPosition, Quaternion.identity);
            w.SetShadows(false);
            if (Time.timeSinceLevelLoad > 0.5f) Hud.Instance?.Notify("Picked up " + w.Def.displayName, NoticeKind.Info);
        }

        void ThrowCurrent()
        {
            var w = Current;
            Current = null;
            var cam = player.Camera.transform;
            w.transform.SetParent(null, true);
            w.Drop(player.Velocity + cam.forward * 3.5f + Vector3.up * 1.5f, Random.insideUnitSphere * 6f);
        }

        public bool OnWeaponShot(Weapon weapon, in HitInfo hit) => false; // the player can't be disarmed yet

        void Update()
        {
            if (PauseMenu.IsPaused) return;
            UpdateLookedAt();
            if (GameInput.Interact.WasPressedThisFrame() && lookedAt != null) Equip(lookedAt);
            if (Current == null) return;

            var def = Current.Def;
            bool aiming = GameInput.Aim.IsPressed() && !Current.Reloading;
            aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, Time.deltaTime * 7f);
            player.FovOverride = Mathf.Lerp(player.Config.fieldOfView, def.aimFieldOfView, aimBlend);
            player.LookScale = Mathf.Lerp(1f, def.aimLookScale, aimBlend);

            int hands = HandsOnWeapon;
            if (GameInput.Reload.WasPressedThisFrame()) Current.TryStartReload(hands, rules.oneHandReload);

            bool held = GameInput.Fire.IsPressed();
            bool pressed = held && !triggerHeldLastFrame;
            triggerHeldLastFrame = held;
            bool wantShot = def.fireMode == FireMode.FullAuto ? held : pressed;

            if (wantShot && WeaponRules.CanFire(hands))
            {
                if (Current.Empty && !Current.Reloading)
                {
                    if (!Current.TryStartReload(hands, rules.oneHandReload)) Current.DryFire();
                }
                else if (Current.ReadyToFire) Shoot(def, aiming, hands);
            }

            UpdateViewModel(def, hands);
        }

        void Shoot(WeaponDefinition def, bool aiming, int hands)
        {
            var cam = player.Camera.transform;
            float spread = WeaponRules.SpreadDegrees(def, aiming, Current.Bloom, hands);
            var outcome = Current.Fire(new Ray(cam.position, cam.forward), spread, transform, 1f, true);
            CombatFeedback.PlayerShotLanded(outcome.Result);

            float recoil = WeaponRules.RecoilMultiplier(def, hands);
            player.AddRecoil(def.recoilPitch * recoil * (aiming ? 0.7f : 1f), Weapon.SpreadStream.Range(-1f, 1f) * def.recoilYawJitter * recoil);
            CombatFeedback.PlayerFired(def.shotTrauma * recoil);
            kickOffset += new Vector3(0, 0.004f, -0.035f) * recoil;
            kickPitch += 4f * recoil;
        }

        void UpdateViewModel(WeaponDefinition def, int hands)
        {
            float dt = Time.deltaTime;
            kickOffset = Vector3.Lerp(kickOffset, Vector3.zero, 1f - Mathf.Exp(-18f * dt));
            kickPitch = Mathf.Lerp(kickPitch, 0f, 1f - Mathf.Exp(-16f * dt));

            Vector3 basePos = Vector3.Lerp(def.hipViewOffset, def.aimViewOffset, aimBlend);
            // One-handed: hold it lower and canted so the penalty is visible.
            float cant = hands == 1 ? 22f : 0f;
            if (hands == 1) basePos += new Vector3(0.04f, -0.04f, -0.03f);
            float reloadDip = Current.Reloading ? Mathf.Sin(Current.ReloadProgress * Mathf.PI) * 18f : 0f;

            ViewRoot.localPosition = basePos + kickOffset;
            ViewRoot.localRotation = Quaternion.Euler(-kickPitch + reloadDip, 0, -cant);
        }

        void UpdateLookedAt()
        {
            lookedAt = null;
            var cam = player.Camera.transform;
            if (Physics.SphereCast(cam.position, 0.12f, cam.forward, out var hit, rules.playerPickupRange, ~0, QueryTriggerInteraction.Ignore))
            {
                var w = hit.collider.GetComponentInParent<Weapon>();
                if (w != null && w.IsLoose && w != Current) lookedAt = w;
            }
        }
    }
}
