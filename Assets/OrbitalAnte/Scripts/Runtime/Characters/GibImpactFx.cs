using OrbitalAnte.Combat;
using OrbitalAnte.Feel;
using OrbitalAnte.Fx;
using UnityEngine;

namespace OrbitalAnte.Characters
{
    /// <summary>On a gib: leaves a blood splat, a little spray and a squelch where it lands hard.</summary>
    [RequireComponent(typeof(BodyPart))]
    public class GibImpactFx : MonoBehaviour
    {
        float nextAllowed;

        void OnCollisionEnter(Collision c)
        {
            if (!enabled || Time.time < nextAllowed) return;
            if (c.rigidbody != null && !c.rigidbody.isKinematic) return; // gib on gib: no splat
            var cfg = GameFeelConfig.Get();
            float speed = c.relativeVelocity.magnitude;
            if (speed < cfg.splatImpactSpeed || c.contactCount == 0) return;

            nextAllowed = Time.time + cfg.splatCooldownSeconds;
            var contact = c.GetContact(0);
            float t = Mathf.InverseLerp(cfg.splatImpactSpeed, cfg.splatImpactSpeed * 5f, speed);
            DecalFx.Splat(contact.point, contact.normal, Mathf.Lerp(cfg.decalMinSize, cfg.decalMaxSize, t));
            BloodFx.Burst(contact.point, Vector3.Reflect(c.relativeVelocity.normalized * -1f, contact.normal) + contact.normal, 4 + Mathf.RoundToInt(8 * t));
            AudioFx.PlayAt(ProceduralAudio.Squelch, contact.point, Mathf.Lerp(0.25f, 0.7f, t), Random.Range(0.85f, 1.2f), 1f);
        }
    }
}
