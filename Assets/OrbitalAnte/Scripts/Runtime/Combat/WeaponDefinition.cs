using OrbitalAnte.Characters;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    public enum FireMode { FullAuto, SemiAuto }

    /// <summary>
    /// Everything that makes a gun feel like itself. One asset per weapon in Resources/Weapons.
    /// Damage numbers are per hit; the per-region multipliers turn them into per-part damage.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Weapon Definition", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Weapon";
        [Tooltip("Test-arena-only weapons (the pistol) are not part of the final loadout.")]
        public bool testOnly = false;
        [Tooltip("Optional model. Needs child transforms named Grip, SupportHand, Muzzle and Magazine. Empty = placeholder.")]
        public GameObject modelPrefab;
        public WeaponShape placeholderShape = WeaponShape.Rifle;

        [Header("Firing")]
        public FireMode fireMode = FireMode.FullAuto;
        public float roundsPerMinute = 600f;
        public int magazineSize = 30;
        public float reloadSeconds = 2.1f;
        public float range = 250f;

        [Header("Damage per hit, by body region")]
        public float baseDamage = 34f;
        public float headMultiplier = 1.35f;
        public float torsoMultiplier = 1f;
        public float armMultiplier = 1f;
        public float handMultiplier = 1f;
        public float legMultiplier = 1f;
        public float footMultiplier = 1f;
        [Tooltip("How hard limbs fly when this gun pops them (1 = normal).")]
        public float gibForceScale = 1f;
        [Tooltip("Shove on loose physics objects and dropped guns.")]
        public float propImpulse = 4f;

        [Header("Spread (degrees)")]
        public float hipSpread = 1.6f;
        public float aimSpread = 0.35f;
        public float spreadPerShot = 0.45f;
        public float maxBloom = 4f;
        public float bloomRecoveryPerSecond = 6f;

        [Header("Recoil (degrees)")]
        public float recoilPitch = 0.9f;
        public float recoilYawJitter = 0.35f;
        public float recoilRecoveryPerSecond = 7f;
        [Tooltip("Camera shake per shot.")]
        public float shotTrauma = 0.05f;

        [Header("One-handed penalties")]
        public float oneHandSpreadMultiplier = 3f;
        public float oneHandRecoilMultiplier = 2.2f;
        public float oneHandReloadMultiplier = 1.8f;

        [Header("Aiming down sights")]
        public float aimFieldOfView = 62f;
        public float aimLookScale = 0.75f;
        public Vector3 hipViewOffset = new Vector3(0.2f, -0.2f, 0.42f);
        public Vector3 aimViewOffset = new Vector3(0f, -0.105f, 0.3f);

        [Header("Feel")]
        public bool heavySound = true;
        public Color tracerColor = new Color(1f, 0.85f, 0.45f);
        public Color flashColor = new Color(1f, 0.75f, 0.3f);
        public float flashSize = 0.16f;

        public float DamageFor(BodyPartId part)
        {
            float m;
            switch (part)
            {
                case BodyPartId.Head: m = headMultiplier; break;
                case BodyPartId.Torso: m = torsoMultiplier; break;
                case BodyPartId.HandL: case BodyPartId.HandR: m = handMultiplier; break;
                case BodyPartId.FootL: case BodyPartId.FootR: m = footMultiplier; break;
                case BodyPartId.ThighL: case BodyPartId.ThighR: case BodyPartId.ShinL: case BodyPartId.ShinR: m = legMultiplier; break;
                default: m = armMultiplier; break;
            }
            return baseDamage * m;
        }

        public float SecondsPerShot => 60f / Mathf.Max(1f, roundsPerMinute);

        public static WeaponDefinition DefaultRifle()
        {
            var d = CreateInstance<WeaponDefinition>();
            d.name = "RifleAK";
            d.displayName = "AK Spoof (working name)";
            d.placeholderShape = WeaponShape.Rifle;
            return d;
        }

        public static WeaponDefinition DefaultPistol()
        {
            var d = CreateInstance<WeaponDefinition>();
            d.name = "TestPistol";
            d.displayName = "Test Pistol";
            d.testOnly = true;
            d.placeholderShape = WeaponShape.Pistol;
            d.fireMode = FireMode.SemiAuto;
            d.roundsPerMinute = 360f;
            d.magazineSize = 12;
            d.reloadSeconds = 1.4f;
            d.baseDamage = 26f;
            d.headMultiplier = 1.6f;
            d.gibForceScale = 0.8f;
            d.hipSpread = 1.1f;
            d.aimSpread = 0.25f;
            d.spreadPerShot = 0.8f;
            d.maxBloom = 3f;
            d.recoilPitch = 1.6f;
            d.recoilYawJitter = 0.5f;
            d.shotTrauma = 0.04f;
            d.oneHandSpreadMultiplier = 1.6f; // pistols are made for one hand
            d.oneHandRecoilMultiplier = 1.4f;
            d.oneHandReloadMultiplier = 1.5f;
            d.aimFieldOfView = 68f;
            d.hipViewOffset = new Vector3(0.18f, -0.17f, 0.36f);
            d.aimViewOffset = new Vector3(0f, -0.085f, 0.3f);
            d.heavySound = false;
            d.flashSize = 0.11f;
            return d;
        }
    }

    public enum WeaponShape { Rifle, Pistol }
}
