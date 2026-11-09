using System;
using UnityEngine;

namespace OrbitalAnte.Characters
{
    [Serializable]
    public struct PartTuning
    {
        public BodyPartId part;
        [Tooltip("Hit points. The test rifle does 34 per hit, so 34 = 1 hit, 68 = 2 hits, 102 = 3 hits.")]
        public float maxHp;
        [Tooltip("Mass in kg once it becomes a gib. Keep neighbours within about 10x of each other or joints get jittery.")]
        public float gibMass;
    }

    /// <summary>All the numbers for the breakable playtest mannequin. Edit the asset in Resources/Config.</summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Mannequin Config", fileName = "MannequinConfig")]
    public class MannequinConfig : ScriptableObject
    {
        [Header("Per-part HP (low: limbs pop in 1-3 rifle hits)")]
        public PartTuning[] parts = DefaultParts();

        [Header("Movement speed per state (m/s)")]
        public float walkSpeed = 1.9f;
        public float limpSpeed = 0.95f;
        public float crawlSpeed = 0.55f;
        public float wriggleSpeed = 0.28f;
        public float headHopSpeed = 0.35f;
        public float turnSpeedDegrees = 140f;
        public float approachStopDistance = 2.2f;
        public float wanderRadius = 14f;

        [Header("Gait cadence per state (steps per second)")]
        public float walkCadence = 1.5f;
        public float limpCadence = 1.25f;
        public float crawlCadence = 0.85f;
        public float wriggleCadence = 1.1f;
        public float headHopCadence = 1.6f;

        [Header("Detachment (exaggerated on purpose)")]
        [Tooltip("Speed added along the bullet direction when a part pops (m/s).")]
        public float detachImpulse = 9f;
        [Tooltip("Extra upward speed so gibs arc instead of skidding (m/s).")]
        public float detachLift = 3.5f;
        [Tooltip("Random spin on the detached chain (rad/s).")]
        public float detachSpin = 14f;
        [Tooltip("Multiplier on impulse for the head on a killing shot.")]
        public float headshotImpulseMultiplier = 1.4f;
        [Tooltip("Share of the impulse the rest of the body gets when the head dies.")]
        public float deathBodyImpulseShare = 0.3f;

        [Header("Gibs")]
        [Tooltip("HP a part gets back once it is a gib, as a share of max HP, so it can be split again.")]
        public float gibHpFactor = 0.6f;
        public float gibSplitImpulse = 5f;
        public float gibLifetimeSeconds = 30f;
        [Tooltip("Hard cap on gib rigidbodies alive at once (Steam Deck budget). Oldest go first.")]
        public int maxGibBodies = 120;
        public float jointTwistLimit = 35f;
        public float jointSwingLimit = 60f;
        public float gibBounciness = 0.35f;
        public float gibFriction = 0.6f;

        [Header("Look")]
        public Color bodyColor = new Color(0.93f, 0.66f, 0.36f);
        public Color jointColor = new Color(0.36f, 0.28f, 0.24f);
        public Color stumpColor = new Color(0.55f, 0.07f, 0.09f);
        [Tooltip("How much each spawned dummy's colour varies.")]
        public float colorJitter = 0.08f;

        public static MannequinConfig Load()
        {
            var c = Resources.Load<MannequinConfig>("Config/MannequinConfig");
            return c != null ? c : CreateInstance<MannequinConfig>();
        }

        public PartTuning Tuning(BodyPartId id)
        {
            if (parts != null)
                foreach (var t in parts)
                    if (t.part == id) return t;
            foreach (var t in DefaultParts())
                if (t.part == id) return t;
            return new PartTuning { part = id, maxHp = 50, gibMass = 3 };
        }

        public float SpeedFor(LocomotionState s)
        {
            switch (s)
            {
                case LocomotionState.Normal: return walkSpeed;
                case LocomotionState.Limp: return limpSpeed;
                case LocomotionState.Crawl: return crawlSpeed;
                case LocomotionState.Wriggle: return wriggleSpeed;
                case LocomotionState.HeadOnly: return headHopSpeed;
                default: return 0f;
            }
        }

        public float CadenceFor(LocomotionState s)
        {
            switch (s)
            {
                case LocomotionState.Normal: return walkCadence;
                case LocomotionState.Limp: return limpCadence;
                case LocomotionState.Crawl: return crawlCadence;
                case LocomotionState.Wriggle: return wriggleCadence;
                case LocomotionState.HeadOnly: return headHopCadence;
                default: return 0f;
            }
        }

        public static PartTuning[] DefaultParts() => new[]
        {
            new PartTuning { part = BodyPartId.Head, maxHp = 80, gibMass = 4f },
            new PartTuning { part = BodyPartId.Torso, maxHp = 150, gibMass = 12f },
            new PartTuning { part = BodyPartId.UpperArmL, maxHp = 50, gibMass = 2.5f },
            new PartTuning { part = BodyPartId.ForearmL, maxHp = 34, gibMass = 1.8f },
            new PartTuning { part = BodyPartId.HandL, maxHp = 20, gibMass = 1f },
            new PartTuning { part = BodyPartId.UpperArmR, maxHp = 50, gibMass = 2.5f },
            new PartTuning { part = BodyPartId.ForearmR, maxHp = 34, gibMass = 1.8f },
            new PartTuning { part = BodyPartId.HandR, maxHp = 20, gibMass = 1f },
            new PartTuning { part = BodyPartId.ThighL, maxHp = 60, gibMass = 5f },
            new PartTuning { part = BodyPartId.ShinL, maxHp = 40, gibMass = 3f },
            new PartTuning { part = BodyPartId.FootL, maxHp = 20, gibMass = 1.5f },
            new PartTuning { part = BodyPartId.ThighR, maxHp = 60, gibMass = 5f },
            new PartTuning { part = BodyPartId.ShinR, maxHp = 40, gibMass = 3f },
            new PartTuning { part = BodyPartId.FootR, maxHp = 20, gibMass = 1.5f },
        };
    }
}
