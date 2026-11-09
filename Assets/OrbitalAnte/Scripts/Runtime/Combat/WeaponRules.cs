using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>The weapon maths, kept free of scene objects so it can be unit-tested.</summary>
    public static class WeaponRules
    {
        public static bool CanFire(int hands) => hands > 0;

        public static float SpreadDegrees(WeaponDefinition d, bool aiming, float bloom, int hands)
        {
            float s = (aiming ? d.aimSpread : d.hipSpread) + Mathf.Clamp(bloom, 0f, d.maxBloom);
            return hands == 1 ? s * d.oneHandSpreadMultiplier : s;
        }

        public static float RecoilMultiplier(WeaponDefinition d, int hands) => hands == 1 ? d.oneHandRecoilMultiplier : 1f;

        public static bool CanReload(int hands, OneHandReload mode) =>
            hands >= 2 || (hands == 1 && mode == OneHandReload.SlowReload);

        public static float ReloadSeconds(WeaponDefinition d, int hands) =>
            hands == 1 ? d.reloadSeconds * d.oneHandReloadMultiplier : d.reloadSeconds;

        /// <summary>
        /// Rotates <paramref name="dir"/> by a random angle inside a cone. u1, u2 are uniform 0..1;
        /// sqrt(u1) spreads shots evenly over the cone's area instead of bunching at the centre.
        /// </summary>
        public static Vector3 ApplySpread(Vector3 dir, float coneDegrees, float u1, float u2)
        {
            dir = dir.normalized;
            if (coneDegrees <= 0f) return dir;
            Vector3 perp = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            float angle = coneDegrees * Mathf.Sqrt(Mathf.Clamp01(u1));
            float roll = u2 * 360f;
            return Quaternion.AngleAxis(roll, dir) * (Quaternion.AngleAxis(angle, perp) * dir);
        }
    }
}
