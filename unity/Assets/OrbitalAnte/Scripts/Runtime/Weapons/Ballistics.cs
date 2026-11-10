using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Gore;
using OrbitalAnte.Player;
using UnityEngine;

namespace OrbitalAnte.Weapons
{
    public struct ShotResult
    {
        public bool HitAnything;
        /// <summary>Where the bullet stopped (or the end of its range).</summary>
        public Vector3 End;
        public HitOutcome Outcome;
        public Collider Collider;
        /// <summary>An enemy's bullet hit the player.</summary>
        public bool HitPlayer;
    }

    /// <summary>
    /// Instant-hit bullets. One place works out what a bullet struck, how much damage that body part takes from
    /// that gun, and which impact effect to show, for the player's shots and the enemies' shots alike.
    /// </summary>
    public static class Ballistics
    {
        /// <summary>A direction picked evenly from a cone around <paramref name="forward"/>.</summary>
        public static Vector3 SpreadDirection(Vector3 forward, float coneDegrees, SeededRandom rng)
        {
            if (coneDegrees <= 0f || forward.sqrMagnitude < 0.0001f) return forward.normalized;
            // Square root keeps shots evenly spread over the circle instead of bunched in the middle.
            float radius = coneDegrees * Mathf.Sqrt((float)rng.NextDouble());
            float around = (float)rng.NextDouble() * Mathf.PI * 2f;
            Quaternion aim = Quaternion.LookRotation(forward.normalized);
            return aim * Quaternion.Euler(Mathf.Sin(around) * radius, Mathf.Cos(around) * radius, 0f) * Vector3.forward;
        }

        /// <summary>A shot fired by the player. Damages what it hits and raises the hit feedback event.</summary>
        public static ShotResult FireFromPlayer(WeaponData data, Vector3 origin, Vector3 direction)
        {
            var result = new ShotResult { End = origin + direction * data.range };
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, data.range, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                return result;
            }

            result.HitAnything = true;
            result.End = hit.point;
            result.Collider = hit.collider;
            WeaponFx fx = WeaponFx.Instance;

            var target = hit.collider.GetComponentInParent<IHittable>();
            if (target == null)
            {
                if (fx != null) fx.Impact(hit.point, hit.normal);
                return result;
            }

            // Damage depends on which body part was hit; anything else takes the gun's torso damage.
            var part = target as BodyPart;
            var info = new HitInfo
            {
                Damage = part != null ? data.DamageFor(part.Id) : data.torsoDamage,
                Point = hit.point,
                Direction = direction,
                Normal = hit.normal,
            };
            result.Outcome = target.Hit(info);

            if (target is Weapon)
            {
                if (fx != null)
                {
                    fx.Sparks(hit.point, hit.normal, 10);
                    fx.Audio.PlayClang(hit.point);
                }
            }
            else if (part != null && !result.Outcome.Severed && !result.Outcome.Killed)
            {
                // A hit that removes nothing still gets a small puff, so every bullet visibly lands.
                GoreFx gore = GoreFx.Instance;
                if (gore != null) gore.Burst(hit.point, hit.normal, 4, 2.2f);
            }

            if (result.Outcome.Hit) CombatEvents.RaisePlayerHit(result.Outcome);
            return result;
        }

        /// <summary>A shot fired by an enemy. It can hit the world or the player.</summary>
        public static ShotResult FireFromEnemy(WeaponData data, Vector3 origin, Vector3 direction)
        {
            var result = new ShotResult { End = origin + direction * data.range };
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, data.range, Layers.EnemyShootMask, QueryTriggerInteraction.Ignore))
            {
                return result;
            }

            result.HitAnything = true;
            result.End = hit.point;
            result.Collider = hit.collider;

            var health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                result.HitPlayer = true;
                health.TakeDamage(data.playerDamage, direction);
            }
            else
            {
                WeaponFx fx = WeaponFx.Instance;
                if (fx != null) fx.Impact(hit.point, hit.normal);
            }
            return result;
        }
    }
}
