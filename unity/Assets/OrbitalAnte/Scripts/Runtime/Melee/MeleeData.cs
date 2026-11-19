using System.Collections.Generic;
using OrbitalAnte.Content;
using UnityEngine;

namespace OrbitalAnte.Melee
{
    /// <summary>
    /// One melee item, as data. Assets live in Assets/OrbitalAnte/Resources/Melee (made by the setup tool) and can
    /// be edited in the Inspector; the file name is the id. If an asset is missing, the built-in numbers from
    /// <see cref="MeleeCatalog"/> are used, so the game always has all five. The rules read a plain copy
    /// (<see cref="ToSpec"/>), so they can be tested without Unity.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Melee Item", fileName = "Melee")]
    public sealed class MeleeData : ContentDefinition
    {
        public const string ResourceFolder = "Melee";

        [Header("Identity")]
        [Tooltip("Name shown on the HUD. Placeholder until Matthew names it.")]
        public string displayName = "Melee";
        [Tooltip("What it does, in one line: shown when looking at it on the ground and while holding it.")]
        public string pickupNote = "";
        [Tooltip("Its one special thing. Cut: takes one limb off a swing. Launch: knocks down and sends sliding. Crush: breaks any limb, several enemies. Stab: hand stab disarms, takedown from behind. Bonk: a head hit rings and startles.")]
        public MeleeVerb verb = MeleeVerb.Bonk;

        [Header("Timing (seconds)")]
        [Tooltip("From the press until the swing can hit.")]
        public float windup = 0.18f;
        [Tooltip("How long the swing can hit for.")]
        public float active = 0.10f;
        [Tooltip("From the end of the hit window until the next swing.")]
        public float recovery = 0.45f;

        [Header("Shape of the swing")]
        [Tooltip("Metres from the eye.")]
        public float reach = 1.8f;
        [Tooltip("Degrees, side to side.")]
        public float arc = 60f;
        [Tooltip("The most separate things one swing can hit. A Cut always hits exactly one part.")]
        public int maxTargets = 1;

        [Header("Damage per body part")]
        public float headDamage = 45f;
        public float torsoDamage = 50f;
        public float upperArmDamage = 35f;
        public float forearmDamage = 35f;
        public float handDamage = 35f;
        public float thighDamage = 35f;
        public float shinDamage = 35f;
        public float footDamage = 35f;

        [Header("Feel")]
        [Tooltip("Freeze on contact, milliseconds. Never more than 120 in total, and shorter when one swing hits several.")]
        public float hitstopMs = 60f;
        [Tooltip("Freeze for a sweet-spot hit instead (0 keeps the one above).")]
        public float sweetHitstopMs = 0f;
        [Tooltip("Extra freeze for a Bonk on the head.")]
        public float bonusHitstopMs = 0f;
        [Tooltip("How fast a struck living enemy slides away (m/s) when this item knocks it down.")]
        public float launchSpeed = 0f;
        [Tooltip("The same for a sweet-spot hit (0 keeps the one above).")]
        public float sweetLaunchSpeed = 0f;
        [Tooltip("The outer part of the reach that is the sweet spot, metres (0 for none).")]
        public float sweetSpot = 0f;
        [Tooltip("Damage times this on a sweet-spot hit.")]
        public float sweetMultiplier = 1f;
        [Tooltip("How fast the business end moves (m/s). In zero-G it sets how hard a hit pushes you back.")]
        public float swingSpeed = 6f;

        [Header("Costs")]
        [Tooltip("Swings in quick succession before it overheats (0 never).")]
        public int heatSwings = 0;
        [Tooltip("Seconds an overheat locks it.")]
        public float heatLockSeconds = 0f;
        [Tooltip("Heat only goes down after this long without a swing (seconds).")]
        public float heatCoolDelay = 0.8f;
        [Tooltip("Then it drops by this many swings' worth a second.")]
        public float heatCoolRate = 2f;
        [Tooltip("While held you are never heard from less far than this (1 = walking). 0 makes no sound.")]
        public float noise = 0f;
        [Tooltip("Walking speed times this while held (1 = no change).")]
        [Range(0.1f, 1f)] public float walkSlow = 1f;

        [Header("Stab")]
        [Tooltip("A stab from behind on an enemy that has not seen you lately does at least this to the head.")]
        public float takedownDamage = 0f;
        [Tooltip("How wide 'behind' is, degrees, centred on the enemy's back.")]
        public float rearArc = 90f;

        [Header("Finding it")]
        [Tooltip("How often it is the item a world's loot holds, against the others. 0 never appears in loot.")]
        public float rarityWeight = 1f;
        [Tooltip("Kilograms, for when it lies on the ground.")]
        public float mass = 1.5f;

        public override string DisplayName => displayName;

        public override void Validate(List<string> problems) => ToSpec().Validate(problems);

        /// <summary>A plain copy of the numbers for the rules.</summary>
        public MeleeSpec ToSpec()
        {
            return new MeleeSpec
            {
                Id = Id, DisplayName = displayName, PickupNote = pickupNote, Verb = verb,
                Windup = windup, Active = active, Recovery = recovery, Reach = reach, Arc = arc,
                HeadDamage = headDamage, TorsoDamage = torsoDamage, UpperArmDamage = upperArmDamage,
                ForearmDamage = forearmDamage, HandDamage = handDamage, ThighDamage = thighDamage,
                ShinDamage = shinDamage, FootDamage = footDamage,
                HitstopMs = hitstopMs, SweetHitstopMs = sweetHitstopMs, BonusHitstopMs = bonusHitstopMs,
                LaunchSpeed = launchSpeed, SweetLaunchSpeed = sweetLaunchSpeed, SweetSpot = sweetSpot,
                SweetMultiplier = sweetMultiplier, MaxTargets = maxTargets,
                HeatSwings = heatSwings, HeatLockSeconds = heatLockSeconds, HeatCoolDelay = heatCoolDelay,
                HeatCoolRate = heatCoolRate, Noise = noise, WalkSlow = walkSlow, RarityWeight = rarityWeight,
                TakedownDamage = takedownDamage, RearArc = rearArc, SwingSpeed = swingSpeed, Mass = mass,
            };
        }

        /// <summary>An asset holding the built-in numbers for one of the five ids (see <see cref="MeleeCatalog"/>).</summary>
        public static MeleeData CreateDefaults(string id)
        {
            MeleeSpec s = MeleeCatalog.Defaults(id);
            if (s == null) return null;
            var d = CreateInstance<MeleeData>();
            d.name = s.Id;
            d.displayName = s.DisplayName; d.pickupNote = s.PickupNote; d.verb = s.Verb;
            d.windup = s.Windup; d.active = s.Active; d.recovery = s.Recovery; d.reach = s.Reach; d.arc = s.Arc;
            d.maxTargets = s.MaxTargets;
            d.headDamage = s.HeadDamage; d.torsoDamage = s.TorsoDamage; d.upperArmDamage = s.UpperArmDamage;
            d.forearmDamage = s.ForearmDamage; d.handDamage = s.HandDamage; d.thighDamage = s.ThighDamage;
            d.shinDamage = s.ShinDamage; d.footDamage = s.FootDamage;
            d.hitstopMs = s.HitstopMs; d.sweetHitstopMs = s.SweetHitstopMs; d.bonusHitstopMs = s.BonusHitstopMs;
            d.launchSpeed = s.LaunchSpeed; d.sweetLaunchSpeed = s.SweetLaunchSpeed; d.sweetSpot = s.SweetSpot;
            d.sweetMultiplier = s.SweetMultiplier; d.swingSpeed = s.SwingSpeed;
            d.heatSwings = s.HeatSwings; d.heatLockSeconds = s.HeatLockSeconds; d.heatCoolDelay = s.HeatCoolDelay;
            d.heatCoolRate = s.HeatCoolRate; d.noise = s.Noise; d.walkSlow = s.WalkSlow;
            d.takedownDamage = s.TakedownDamage; d.rearArc = s.RearArc;
            d.rarityWeight = s.RarityWeight; d.mass = s.Mass;
            return d;
        }

        /// <summary>Every melee item the game has, sorted by id.</summary>
        public static List<MeleeData> All() => ContentDatabase.All<MeleeData>();
    }
}
