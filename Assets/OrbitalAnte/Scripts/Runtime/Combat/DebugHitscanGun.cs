using OrbitalAnte.Characters;
using OrbitalAnte.Core;
using OrbitalAnte.Input;
using OrbitalAnte.Player;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>Full-auto test hitscan with a tracer, hit marker and ping. Temporary (see DebugGunConfig).</summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class DebugHitscanGun : MonoBehaviour
    {
        public DebugGunConfig Config;

        FirstPersonController player;
        AudioSource audioSource;
        float cooldown;
        LineRenderer tracer;
        float tracerTimer;

        void Awake()
        {
            player = GetComponent<FirstPersonController>();
            if (Config == null) Config = DebugGunConfig.Load();
        }

        void Start()
        {
            audioSource = player.Camera.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            var tracerGo = new GameObject("Tracer");
            tracer = tracerGo.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.startWidth = 0.025f;
            tracer.endWidth = 0.01f;
            tracer.sharedMaterial = MaterialLibrary.Unlit(Config.tracerColor);
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.enabled = false;
        }

        void Update()
        {
            if (tracerTimer > 0f)
            {
                tracerTimer -= Time.deltaTime;
                if (tracerTimer <= 0f) tracer.enabled = false;
            }
            if (PauseMenu.IsPaused) return;

            cooldown -= Time.deltaTime;
            if (GameInput.Fire.IsPressed() && cooldown <= 0f)
            {
                cooldown = 60f / Mathf.Max(1f, Config.roundsPerMinute);
                Fire();
            }
        }

        void Fire()
        {
            var cam = player.Camera.transform;
            var ray = new Ray(cam.position, cam.forward);
            audioSource.PlayOneShot(ProceduralAudio.Shot, 0.35f);

            Vector3 end = ray.origin + ray.direction * Config.range;
            if (Hitscan.Cast(ray, Config.range, transform, out var hit))
            {
                end = hit.point;
                var part = hit.collider.GetComponent<BodyPart>();
                if (part != null)
                {
                    var info = new HitInfo
                    {
                        Damage = Config.damagePerHit, Point = hit.point, Direction = ray.direction,
                        ForceScale = Config.forceScale, Source = gameObject,
                    };
                    var result = part.TakeHit(info);
                    OnHitResult(result);
                }
                else if (hit.rigidbody != null)
                {
                    hit.rigidbody.AddForceAtPosition(ray.direction * Config.propImpulse, hit.point, ForceMode.Impulse);
                }
            }

            tracer.SetPosition(0, cam.position + cam.forward * 0.4f - cam.up * 0.12f + cam.right * 0.12f);
            tracer.SetPosition(1, end);
            tracer.enabled = true;
            tracerTimer = 0.04f;
        }

        void OnHitResult(HitResult result) => CombatFeedback.PlayerShotLanded(result);
    }
}
