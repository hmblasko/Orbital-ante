using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Weapons
{
    /// <summary>
    /// Digs a planet's ground where a shovel, a bullet or a blast strikes it (Matthew, 17 Nov 2026: "shoot holes in
    /// the ground and the ground gets dug up, including when enemies shoot at you"). Only ground that says it can be
    /// dug (<see cref="DiggableGround"/>: the ground tiles, dug ground and cave walls) is dug; rocks, trees, bases and
    /// the ship are not. The size of each hole is in <see cref="DigRules"/>.
    /// </summary>
    /// <summary>What a shovel bite came to: it took ground, the ground would not give (and why), or it was not ground at all.</summary>
    public enum ShovelResult { NotGround, Dug, Refused }

    public static class GroundDamage
    {
        /// <summary>A bullet (the player's or an enemy's) struck something. Digs if it was the ground.</summary>
        public static bool Bullet(in RaycastHit hit, Vector3 direction, float damage, bool fromEnemy)
        {
            PlanetSurface s = SurfaceOf(hit.collider);
            if (s == null || s.Digging == null) return false;
            if (!(fromEnemy ? s.Settings.enemyBulletsDig : s.Settings.bulletsDig)) return false;
            Vec3d p = SpaceConvert.ToVec3d(s.transform.InverseTransformPoint(hit.point));
            Vec3d along = SpaceConvert.ToVec3d(s.transform.InverseTransformDirection(direction));
            return s.Digging.Carve(DigRules.Bullet(p, along, damage), DigSource.Bullet);
        }

        /// <summary>
        /// The shovel's blade bit the ground at a hit, digging along <paramref name="direction"/>, by someone standing
        /// at <paramref name="feet"/> (world space; the tunnel's floor is kept level with them). What came of it, and
        /// if it was refused, why (<paramref name="why"/>).
        /// </summary>
        public static ShovelResult Shovel(in RaycastHit hit, Vector3 direction, Vector3? feet, out DigRefusal why)
        {
            why = DigRefusal.None;
            PlanetSurface s = SurfaceOf(hit.collider);
            if (s == null || s.Digging == null) return ShovelResult.NotGround;
            Vec3d p = SpaceConvert.ToVec3d(s.transform.InverseTransformPoint(hit.point));
            Vec3d along = SpaceConvert.ToVec3d(s.transform.InverseTransformDirection(direction));
            Vec3d? at = feet.HasValue ? SpaceConvert.ToVec3d(s.transform.InverseTransformPoint(feet.Value)) : (Vec3d?)null;
            bool any = false;
            foreach (DigCarve bite in DigRules.ShovelBites(p, along, at))
            {
                if (s.Digging.Carve(bite, DigSource.Shovel)) any = true;
                else if (why == DigRefusal.None || why == DigRefusal.AlreadyDug) why = s.Digging.LastRefusal;
            }
            if (any) why = DigRefusal.None;
            return any ? ShovelResult.Dug : ShovelResult.Refused;
        }

        /// <summary>
        /// A blast of this radius at a world point (a meteor, a grenade, anything that explodes): a crater, if it is
        /// on or near diggable ground of a world the player is near. For other systems to call; nothing in the game
        /// explodes on the ground yet.
        /// </summary>
        public static bool Blast(Vector3 world, float radius)
        {
            PlanetSurface s = PlanetDigging.SurfaceNear(world, Mathf.Max(1f, radius));
            if (s == null || s.Digging == null) return false;
            Vec3d p = SpaceConvert.ToVec3d(s.transform.InverseTransformPoint(world));
            return s.Digging.Carve(DigRules.Blast(p, radius), DigSource.Blast);
        }

        /// <summary>The body whose diggable ground a collider is, or null.</summary>
        public static PlanetSurface SurfaceOf(Collider collider)
        {
            if (collider == null) return null;
            var ground = collider.GetComponent<DiggableGround>();
            return ground != null ? ground.Surface : null;
        }
    }
}
