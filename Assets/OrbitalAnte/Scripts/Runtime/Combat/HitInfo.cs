using UnityEngine;

namespace OrbitalAnte.Combat
{
    public struct HitInfo
    {
        public float Damage;
        public Vector3 Point;
        /// <summary>Normalised direction the shot was travelling.</summary>
        public Vector3 Direction;
        /// <summary>Scales the detach impulse. 1 = normal rifle.</summary>
        public float ForceScale;
        public GameObject Source;
    }

    public enum HitResult
    {
        None,
        Damaged,
        Severed,
        Killed,
        GibSplit,
    }
}
