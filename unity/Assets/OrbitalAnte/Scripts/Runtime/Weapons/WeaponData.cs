using System.Collections.Generic;
using OrbitalAnte.Content;
using OrbitalAnte.Dismemberment;
using UnityEngine;

namespace OrbitalAnte.Weapons
{
    public enum WeaponShape
    {
        Rifle,
        Pistol,
    }

    /// <summary>
    /// Everything about one gun, as data. Assets live in Assets/OrbitalAnte/Resources/Weapons and can be edited
    /// in the Inspector. New guns are new assets, not new code.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Weapon", fileName = "Weapon")]
    public sealed class WeaponData : ContentDefinition
    {
        public const string ResourceFolder = "Weapons";
        public const string RifleId = "Rifle";
        public const string PistolId = "Pistol";
        public const string SmgId = "Smg";
        public const string ShotgunId = "Shotgun";
        public const string SniperId = "Sniper";

        [Header("Identity")]
        [Tooltip("Name shown on the HUD. Placeholder until the real roster is named.")]
        public string displayName = "Gun";
        [Tooltip("Needs both hands to shoot properly. With one hand the penalties below apply.")]
        public bool twoHanded = true;

        [Header("Damage per body part")]
        public float headDamage = 34f;
        public float torsoDamage = 34f;
        public float upperArmDamage = 34f;
        public float forearmDamage = 34f;
        public float handDamage = 34f;
        public float thighDamage = 34f;
        public float shinDamage = 34f;
        public float footDamage = 34f;
        [Tooltip("Damage to the player when an enemy fires this gun.")]
        public float playerDamage = 4f;

        [Header("Firing")]
        [Tooltip("Hold to fire. Off means one shot per trigger pull.")]
        public bool automatic = true;
        public float shotsPerSecond = 10f;
        public float range = 250f;

        [Header("Spread (aim error, in degrees)")]
        public float baseSpread = 0.35f;
        [Tooltip("Added by each shot, so long bursts wander.")]
        public float spreadPerShot = 0.22f;
        [Tooltip("The most that sustained fire can add.")]
        public float maxBloom = 2.6f;
        [Tooltip("How fast the built-up error settles once the gun stops firing, in degrees per second.")]
        public float bloomRecovery = 7f;

        [Header("Recoil (view kick, in degrees per shot)")]
        public float recoilPitch = 0.75f;
        [Tooltip("Random sideways kick, plus or minus.")]
        public float recoilYaw = 0.35f;
        [Tooltip("Part of each kick that settles back by itself. The rest must be pulled down by the player.")]
        [Range(0f, 1f)] public float recoilReturn = 0.55f;
        [Tooltip("How fast the returning part settles, in degrees per second.")]
        public float recoilRecovery = 14f;

        [Header("Magazine")]
        public int magazineSize = 30;
        public float reloadSeconds = 1.9f;

        [Header("One-handed (after losing a hand)")]
        public float oneHandSpreadMultiplier = 4f;
        public float oneHandRecoilMultiplier = 2.5f;
        public float oneHandReloadMultiplier = 2.2f;

        [Header("Look")]
        public WeaponShape placeholderShape = WeaponShape.Rifle;
        [Tooltip("The game's own model for this gun: a file name in Resources/Models, without the ending. Leave empty to use the one for its shape (Rifle or Pistol).")]
        public string modelFile = "";
        [Tooltip("Optional. Drag a model from Assets/ThirdParty/Weapons here to replace the placeholder shape.")]
        public GameObject modelPrefab;
        [Tooltip("Turn an imported model so its barrel points along blue (Z) and its top along green (Y).")]
        public Vector3 modelRotation;
        [Tooltip("Barrel-tip to stock length the imported model is scaled to, in metres. 0 keeps its own size.")]
        public float modelLength = 0f;
        [Tooltip("Tick to place the sockets below by hand. Otherwise they are estimated from the model's size.")]
        public bool useCustomSockets;
        public Vector3 gripSocket;
        public Vector3 supportHandSocket;
        public Vector3 muzzleSocket;
        public Vector3 magazineSocket;
        public Color tracerColor = new Color(1f, 0.85f, 0.35f);
        public float muzzleFlashSize = 0.22f;

        [Header("Sound")]
        [Tooltip("The audio event for this gun's shot, for example weapon.smg.shot. Leave empty to pick by shape.")]
        public string shotEvent = "";
        [Tooltip("Only for the old code-made fallback sound. Lower is boomier.")]
        public float shotPitch = 1f;
        public float shotVolume = 0.8f;

        [Header("Physics when dropped")]
        public float mass = 3.5f;

        public override string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public override void Validate(List<string> problems)
        {
            if (string.IsNullOrEmpty(displayName)) problems.Add("has no display name.");
            if (shotsPerSecond <= 0f) problems.Add("fire rate must be above zero.");
            if (magazineSize < 1) problems.Add("magazine size must be at least 1.");
            if (range <= 0f) problems.Add("range must be above zero.");
            if (reloadSeconds < 0f) problems.Add("reload time cannot be negative.");
            if (headDamage <= 0f || torsoDamage <= 0f || upperArmDamage <= 0f || forearmDamage <= 0f
                || handDamage <= 0f || thighDamage <= 0f || shinDamage <= 0f || footDamage <= 0f)
            {
                problems.Add("every body part needs damage above zero, or that part can never be shot off.");
            }
            if (oneHandSpreadMultiplier < 1f || oneHandRecoilMultiplier < 1f || oneHandReloadMultiplier < 1f)
            {
                problems.Add("one-handed multipliers below 1 would make one hand better than two.");
            }
            if (recoilReturn < 0f || recoilReturn > 1f) problems.Add("recoil return must be between 0 and 1.");
            if (mass <= 0f) problems.Add("mass must be above zero.");
        }

        public float DamageFor(BodyPartId id)
        {
            switch (id)
            {
                case BodyPartId.Head: return headDamage;
                case BodyPartId.Torso: return torsoDamage;
                case BodyPartId.LeftUpperArm:
                case BodyPartId.RightUpperArm: return upperArmDamage;
                case BodyPartId.LeftForearm:
                case BodyPartId.RightForearm: return forearmDamage;
                case BodyPartId.LeftHand:
                case BodyPartId.RightHand: return handDamage;
                case BodyPartId.LeftThigh:
                case BodyPartId.RightThigh: return thighDamage;
                case BodyPartId.LeftShin:
                case BodyPartId.RightShin: return shinDamage;
                default: return footDamage;
            }
        }

        public FirearmSpec ToSpec(bool oneHandCanReload)
        {
            return new FirearmSpec
            {
                Automatic = automatic,
                ShotsPerSecond = Mathf.Max(0.1f, shotsPerSecond),
                MagazineSize = Mathf.Max(1, magazineSize),
                ReloadSeconds = Mathf.Max(0f, reloadSeconds),
                BaseSpread = Mathf.Max(0f, baseSpread),
                SpreadPerShot = Mathf.Max(0f, spreadPerShot),
                MaxBloom = Mathf.Max(0f, maxBloom),
                BloomRecovery = Mathf.Max(0f, bloomRecovery),
                OneHandSpreadMultiplier = Mathf.Max(1f, oneHandSpreadMultiplier),
                OneHandRecoilMultiplier = Mathf.Max(1f, oneHandRecoilMultiplier),
                OneHandReloadMultiplier = Mathf.Max(1f, oneHandReloadMultiplier),
                OneHandCanReload = oneHandCanReload,
            };
        }

        // ------------------------------------------------------------------ the two Milestone 3 guns

        /// <summary>
        /// The AK-47 spoof: full-auto, hits hard, climbs if you hold the trigger. Limbs come off in 1-2 hits.
        /// </summary>
        public static WeaponData CreateRifleDefaults()
        {
            var w = CreateInstance<WeaponData>();
            w.name = RifleId;
            w.displayName = "AK-ish (name TBD)";
            w.twoHanded = true;
            w.placeholderShape = WeaponShape.Rifle;
            // Everything else is the field defaults above.
            return w;
        }

        /// <summary>
        /// The test pistol: one shot per pull, accurate, small magazine. Rewards headshots, slower on limbs.
        /// </summary>
        public static WeaponData CreatePistolDefaults()
        {
            var w = CreateInstance<WeaponData>();
            w.name = PistolId;
            w.displayName = "Test Pistol";
            w.twoHanded = false;
            w.placeholderShape = WeaponShape.Pistol;
            w.headDamage = 46f;
            w.torsoDamage = 24f;
            w.upperArmDamage = 24f;
            w.forearmDamage = 24f;
            w.handDamage = 30f;
            w.thighDamage = 24f;
            w.shinDamage = 26f;
            w.footDamage = 30f;
            w.playerDamage = 6f;
            w.automatic = false;
            w.shotsPerSecond = 5f;
            w.range = 120f;
            w.baseSpread = 0.2f;
            w.spreadPerShot = 0.5f;
            w.maxBloom = 2f;
            w.bloomRecovery = 9f;
            w.recoilPitch = 1.5f;
            w.recoilYaw = 0.4f;
            w.recoilReturn = 0.8f;
            w.recoilRecovery = 18f;
            w.magazineSize = 12;
            w.reloadSeconds = 1.3f;
            // A pistol is made for one hand, so losing the other costs little.
            w.oneHandSpreadMultiplier = 1.5f;
            w.oneHandRecoilMultiplier = 1.3f;
            w.oneHandReloadMultiplier = 1.8f;
            w.tracerColor = new Color(0.75f, 0.95f, 1f);
            w.muzzleFlashSize = 0.15f;
            w.shotPitch = 1.6f;
            w.shotVolume = 0.65f;
            w.mass = 1.1f;
            return w;
        }

        // ------------------------------------------------------------------ the enemy types' guns
        // Each is the reason its carrier fights the way it does. Names are placeholders until Matthew picks them.

        /// <summary>The Runner's gun: a spray of weak bullets from close in. Loud, inaccurate, empties fast.</summary>
        public static WeaponData CreateSmgDefaults()
        {
            var w = CreateInstance<WeaponData>();
            w.name = SmgId;
            w.displayName = "Buzz Gun (name TBD)";
            w.twoHanded = false;
            w.placeholderShape = WeaponShape.Pistol;
            w.modelFile = SmgId;
            w.headDamage = 22f; w.torsoDamage = 16f; w.upperArmDamage = 16f; w.forearmDamage = 16f;
            w.handDamage = 18f; w.thighDamage = 16f; w.shinDamage = 17f; w.footDamage = 18f;
            w.playerDamage = 2.5f;
            w.automatic = true;
            w.shotsPerSecond = 15f;
            w.range = 60f;
            w.baseSpread = 1.4f; w.spreadPerShot = 0.3f; w.maxBloom = 4.5f; w.bloomRecovery = 6f;
            w.recoilPitch = 0.5f; w.recoilYaw = 0.5f;
            w.magazineSize = 32; w.reloadSeconds = 1.5f;
            w.oneHandSpreadMultiplier = 1.6f; w.oneHandRecoilMultiplier = 1.4f; w.oneHandReloadMultiplier = 1.8f;
            w.tracerColor = new Color(1f, 0.6f, 0.25f);
            w.muzzleFlashSize = 0.16f;
            w.shotPitch = 1.9f; w.shotVolume = 0.55f;
            w.mass = 1.8f;
            return w;
        }

        /// <summary>The Bruiser's gun: a slow, wide laser blast that hurts badly up close and barely reaches far.</summary>
        public static WeaponData CreateShotgunDefaults()
        {
            var w = CreateInstance<WeaponData>();
            w.name = ShotgunId;
            w.displayName = "Double Zapper (name TBD)";
            w.twoHanded = true;
            w.placeholderShape = WeaponShape.Rifle;
            w.modelFile = ShotgunId;
            w.headDamage = 90f; w.torsoDamage = 80f; w.upperArmDamage = 70f; w.forearmDamage = 70f;
            w.handDamage = 70f; w.thighDamage = 70f; w.shinDamage = 70f; w.footDamage = 70f;
            w.playerDamage = 22f;
            w.automatic = false;
            w.shotsPerSecond = 1.1f;
            w.range = 22f;
            w.baseSpread = 4.5f; w.spreadPerShot = 1f; w.maxBloom = 6f; w.bloomRecovery = 5f;
            w.recoilPitch = 3f; w.recoilYaw = 0.8f;
            w.magazineSize = 2; w.reloadSeconds = 2.4f;
            w.tracerColor = new Color(0.45f, 1f, 0.5f);
            w.muzzleFlashSize = 0.34f;
            w.shotPitch = 0.7f; w.shotVolume = 0.95f;
            w.mass = 4.5f;
            return w;
        }

        /// <summary>The Marksman's gun: one very accurate, very painful shot, then a long wait. It reaches a long way.</summary>
        public static WeaponData CreateSniperDefaults()
        {
            var w = CreateInstance<WeaponData>();
            w.name = SniperId;
            w.displayName = "Long Shot (name TBD)";
            w.twoHanded = true;
            w.placeholderShape = WeaponShape.Rifle;
            w.modelFile = SniperId;
            w.headDamage = 200f; w.torsoDamage = 120f; w.upperArmDamage = 90f; w.forearmDamage = 90f;
            w.handDamage = 90f; w.thighDamage = 90f; w.shinDamage = 90f; w.footDamage = 90f;
            w.playerDamage = 25f;
            w.automatic = false;
            w.shotsPerSecond = 0.45f;
            w.range = 400f;
            w.baseSpread = 0.05f; w.spreadPerShot = 0.6f; w.maxBloom = 1.5f; w.bloomRecovery = 3f;
            w.recoilPitch = 3.5f; w.recoilYaw = 0.3f;
            w.magazineSize = 4; w.reloadSeconds = 2.8f;
            w.oneHandSpreadMultiplier = 6f;
            w.tracerColor = new Color(1f, 0.25f, 0.3f);
            w.muzzleFlashSize = 0.3f;
            w.shotPitch = 0.55f; w.shotVolume = 1f;
            w.mass = 5.5f;
            return w;
        }

        /// <summary>The rifle from the content database (the asset if it exists, otherwise built-in defaults).</summary>
        public static WeaponData Rifle => ContentDatabase.Get<WeaponData>(RifleId);

        public static WeaponData Pistol => ContentDatabase.Get<WeaponData>(PistolId);
    }
}
