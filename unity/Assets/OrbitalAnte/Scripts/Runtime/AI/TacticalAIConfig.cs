using UnityEngine;

namespace OrbitalAnte.AI
{
    /// <summary>Initial engineering defaults. Fable owns combat feel and tuning; these need an arena playtest.</summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Tactical AI Config", fileName = "TacticalAIConfig")]
    public sealed class TacticalAIConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/TacticalAIConfig";
        [Header("Hard bounds (clamped again at runtime)")]
        [Range(1, 64)] public int maxAgents = 32;
        [Range(1, 8)] public int simultaneousAttackers = 3;
        [Range(1, 16)] public int sensesPerFrame = 4;
        [Range(1, 32)] public int candidatesPerPlan = 24;
        [Range(1, 4096)] public int nodesPerPlan = 2048;
        [Range(1, 32)] public int planningProbesPerFrame = 24;
        [Range(1, 32)] public int shotProbesPerFrame = 8;
        [Range(1, 256)] public int movementProbesPerFrame = 128;
        [Header("Perception and pressure")]
        [Min(0.05f)] public float senseInterval = 0.18f;
        [Min(0.05f)] public float sightFreshness = 0.45f;
        [Min(0f)] public float memorySeconds = 5f;
        [Min(0.05f)] public float attackLease = 1.3f;
        [Min(0f)] public float attackRest = 0.35f;
        [Header("Stable positioning")]
        [Min(0.1f)] public float roleInterval = 2.5f;
        [Range(15f, 150f)] public float frontTurnDegrees = 60f;
        [Min(0.1f)] public float planInterval = 0.8f;
        [Min(0.1f)] public float minimumGoalHold = 2f;
        [Min(0.2f)] public float stuckReplanSeconds = 1.5f;
        [Range(15f, 90f)] public float minimumFlankAngle = 35f;
        [Min(1f)] public float targetMoveReplan = 3f;
        [Min(1f)] public float anchorRange = 14f;
        [Min(1f)] public float flankRange = 11f;
        [Min(1f)] public float supportRange = 19f;
        [Min(0.5f)] public float reservationSpacing = 2f;
        [Range(1f, 4f)] public float gridSpacing = 1.5f;
        [Range(0.7f, 1f)] public float navigationClearance = 0.7f;
        [Min(0.1f)] public float friendlyClearance = 0.55f;
        static TacticalAIConfig _fallback;
        public static TacticalAIConfig Load()
        {
            TacticalAIConfig cfg = Resources.Load<TacticalAIConfig>(ResourcePath);
            if (cfg != null) return cfg;
            if (_fallback == null) { _fallback = CreateInstance<TacticalAIConfig>(); _fallback.hideFlags = HideFlags.HideAndDontSave; }
            return _fallback;
        }
    }
}
