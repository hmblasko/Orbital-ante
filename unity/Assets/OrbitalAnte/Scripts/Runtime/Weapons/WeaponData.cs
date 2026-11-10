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
    public sealed class WeaponData : ScriptableObject
    {
        public const string ResourceFolder = "Weapons";
        public const string RifleId = "Rifle";
        public const string PistolId = "Pistol";

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

        [Header("Sound (placeholder, generated)")]
        [Tooltip("Lower is boomier.")]
        public float shotPitch = 1f;
        public float shotVolume = 0.8f;

        [Header("Physics when dropped")]
        public float mass = 3.5f;

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

        static WeaponData _rifle;
        static WeaponData _pistol;

        /// <summary>The rifle asset if it exists, otherwise built-in defaults.</summary>
        public static WeaponData Rifle
        {
            get
            {
                if (_rifle == null) _rifle = Resources.Load<WeaponData>(ResourceFolder + "/" + RifleId);
                if (_rifle == null) _rifle = CreateRifleDefaults();
                return _rifle;
            }
        }

        public static WeaponData Pistol
        {
            get
            {
                if (_pistol == null) _pistol = Resources.Load<WeaponData>(ResourceFolder + "/" + PistolId);
                if (_pistol == null) _pistol = CreatePistolDefaults();
                return _pistol;
            }
        }
    }
}
