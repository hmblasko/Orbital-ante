using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Characters;
using OrbitalAnte.Fx;
using OrbitalAnte.Player;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    public struct ShotOutcome
    {
        public bool HitSomething;
        public bool HitPlayer;
        public Vector3 End;
        public HitResult Result;
    }

    /// <summary>
    /// A physical gun: magazine, reload, bloom, muzzle flash, and the hitscan shot itself.
    /// While held it is a trigger (so it can be shot out of someone's hands); dropped, it is a
    /// physics object anyone with a free hand can pick up.
    /// </summary>
    public class Weapon : MonoBehaviour, IShootable
    {
        public static readonly List<Weapon> All = new List<Weapon>();

        public WeaponDefinition Def;
        public Transform Grip, SupportHand, Muzzle, Magazine;
        public Rigidbody Body;
        public Collider Collider;
        public GameObject Flash;
        public Light FlashLight;

        public int Ammo { get; private set; }
        public bool Reloading { get; private set; }
        public float ReloadProgress => Reloading ? 1f - Mathf.Clamp01((reloadDone - Time.time) / Mathf.Max(0.01f, reloadDuration)) : 0f;
        public float Bloom { get; private set; }
        public IWeaponHolder Holder { get; private set; }
        public bool IsLoose => Holder == null;

        static SeededRandom spreadStream;
        static int spreadStreamSeed;
        /// <summary>Shot spread comes from the run seed, so a seed replays the same spread pattern.</summary>
        public static SeededRandom SpreadStream
        {
            get
            {
                if (spreadStream == null || spreadStreamSeed != RunSeed.Current)
                {
                    spreadStream = RunSeed.CreateStream("weapons.spread");
                    spreadStreamSeed = RunSeed.Current;
                }
                return spreadStream;
            }
        }

        float nextShot;
        float reloadDone;
        float reloadDuration;
        float flashUntil;
        Vector3 magazineRest;

        void Awake()
        {
            All.Add(this);
        }

        void OnDestroy() => All.Remove(this);

        public void Init(WeaponDefinition def)
        {
            Def = def;
            Ammo = def.magazineSize;
            if (Magazine != null) magazineRest = Magazine.localPosition;
        }

        // ---- holding --------------------------------------------------------------------

        public void SetHeld(IWeaponHolder holder, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            Holder = holder;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            Collider.isTrigger = true;
            transform.SetParent(parent, false);
            transform.localPosition = localPos;
            transform.localRotation = localRot;
            CancelReload();
        }

        public void Drop(Vector3 velocity, Vector3 angularVelocity)
        {
            Holder = null;
            transform.SetParent(null, true);
            Collider.isTrigger = false;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.linearVelocity = velocity;
            Body.angularVelocity = angularVelocity;
            CancelReload();
            SetShadows(true);
        }

        public void SetShadows(bool on)
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                r.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---- firing ---------------------------------------------------------------------

        public bool ReadyToFire => !Reloading && Ammo > 0 && Time.time >= nextShot;
        public bool Empty => Ammo <= 0;

        public bool TryStartReload(int hands, OneHandReload mode)
        {
            if (Reloading || Ammo >= Def.magazineSize) return false;
            if (!WeaponRules.CanReload(hands, mode)) return false;
            Reloading = true;
            reloadDuration = WeaponRules.ReloadSeconds(Def, hands);
            reloadDone = Time.time + reloadDuration;
            AudioFx.PlayAt(ProceduralAudio.ReloadOut, transform.position, 0.6f, 1f, Holder is PlayerWeapons ? 0f : 1f);
            return true;
        }

        void CancelReload()
        {
            Reloading = false;
            if (Magazine != null) Magazine.localPosition = magazineRest;
        }

        public void DryFire()
        {
            if (Time.time < nextShot) return;
            nextShot = Time.time + 0.25f;
            AudioFx.PlayAt(ProceduralAudio.DryFire, transform.position, 0.5f, 1f, Holder is PlayerWeapons ? 0f : 1f);
        }

        /// <summary>
        /// Fires one hitscan round along <paramref name="aim"/> with a random spread cone.
        /// Everything the round touches reacts: body parts, other guns, loose props, the player.
        /// </summary>
        public ShotOutcome Fire(Ray aim, float spreadDegrees, Transform ignoreRoot, float rateScale, bool firedByPlayer)
        {
            var outcome = new ShotOutcome { End = aim.origin + aim.direction * Def.range };
            nextShot = Time.time + Def.SecondsPerShot / Mathf.Max(0.05f, rateScale);
            Ammo--;
            Bloom = Mathf.Min(Bloom + Def.spreadPerShot, Def.maxBloom);
            flashUntil = Time.time + 0.045f;
            if (Flash != null)
            {
                Flash.SetActive(true);
                Flash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                Flash.transform.localScale = Vector3.one * Def.flashSize * Random.Range(0.8f, 1.25f);
            }
            if (FlashLight != null) FlashLight.enabled = true;

            var clip = Def.heavySound ? ProceduralAudio.GunHeavy : ProceduralAudio.GunLight;
            AudioFx.PlayAt(clip, Muzzle.position, firedByPlayer ? 0.55f : 0.45f, Random.Range(0.94f, 1.06f), firedByPlayer ? 0f : 1f);

            var rng = SpreadStream;
            Vector3 dir = WeaponRules.ApplySpread(aim.direction, spreadDegrees, rng.NextFloat(), rng.NextFloat());
            var ray = new Ray(aim.origin, dir);
            bool hit = Hitscan.Cast(ray, Def.range, ignoreRoot, out var h);
            float worldDistance = hit ? h.distance : Def.range;

            // Bots can hit the player, whose capsule the hitscan skips on purpose.
            if (!firedByPlayer && FirstPersonController.Instance != null)
            {
                var pcc = FirstPersonController.Instance.GetComponent<CharacterController>();
                if (pcc != null && pcc.Raycast(ray, out var ph, worldDistance))
                {
                    outcome.HitPlayer = true;
                    outcome.HitSomething = true;
                    outcome.End = ph.point;
                    Hud.Instance?.ShowDamageFlash(ray.direction);
                    WeaponFx.Tracer(Muzzle.position, outcome.End, Def.tracerColor);
                    return outcome;
                }
            }

            if (hit)
            {
                outcome.HitSomething = true;
                outcome.End = h.point;
                var target = h.collider.GetComponentInParent<IShootable>();
                if (target != null)
                {
                    float damage = target is BodyPart bp ? Def.DamageFor(bp.Id) : Def.baseDamage;
                    var info = new HitInfo
                    {
                        Damage = damage, Point = h.point, Direction = dir, ForceScale = Def.gibForceScale,
                        Source = gameObject,
                    };
                    outcome.Result = target.TakeHit(info);
                }
                else
                {
                    if (h.rigidbody != null && !h.rigidbody.isKinematic)
                        h.rigidbody.AddForceAtPosition(dir * Def.propImpulse, h.point, ForceMode.Impulse);
                    WeaponFx.Impact(h.point, h.normal);
                }
            }
            WeaponFx.Tracer(Muzzle.position, outcome.End, Def.tracerColor);
            return outcome;
        }

        // ---- being shot -----------------------------------------------------------------

        public HitResult TakeHit(in HitInfo hit)
        {
            WeaponFx.Impact(hit.Point, -hit.Direction);
            AudioFx.PlayAt(ProceduralAudio.Clatter, hit.Point, 0.6f, Random.Range(1.1f, 1.3f));
            if (Holder != null)
                return Holder.OnWeaponShot(this, hit) ? HitResult.Disarmed : HitResult.None;
            Body.AddForceAtPosition(hit.Direction * Def.propImpulse, hit.Point, ForceMode.Impulse);
            return HitResult.Damaged;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Bloom = Mathf.MoveTowards(Bloom, 0f, Def.bloomRecoveryPerSecond * dt);

            if (Reloading)
            {
                // Magazine drops out and slides back in so a reload reads at a glance.
                float t = ReloadProgress;
                float offset = t < 0.5f ? Mathf.SmoothStep(0f, 1f, t * 2f) : Mathf.SmoothStep(1f, 0f, (t - 0.5f) * 2f);
                if (Magazine != null) Magazine.localPosition = magazineRest + Vector3.down * 0.12f * offset;
                if (Time.time >= reloadDone)
                {
                    Ammo = Def.magazineSize;
                    CancelReload();
                    AudioFx.PlayAt(ProceduralAudio.ReloadIn, transform.position, 0.6f, 1f, Holder is PlayerWeapons ? 0f : 1f);
                }
            }

            if (Time.time > flashUntil)
            {
                if (Flash != null && Flash.activeSelf) Flash.SetActive(false);
                if (FlashLight != null && FlashLight.enabled) FlashLight.enabled = false;
            }
        }
    }
}
