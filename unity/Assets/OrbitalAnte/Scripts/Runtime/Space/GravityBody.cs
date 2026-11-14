using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// Put this on anything loose that should feel the solar system's gravity: the ship, later the player, gibs
    /// and debris. It switches off Unity's flat "down" gravity and lets <see cref="SolarSystem"/> pull the object
    /// toward whatever is near.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GravityBody : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        /// <summary>The pull felt last physics step, in Unity's coordinates (metres per second squared).</summary>
        public Vector3 FeltGravity { get; set; }
        /// <summary>The body whose neighbourhood this object was in last step.</summary>
        public int Neighbourhood { get; set; }

        /// <summary>
        /// True for the things the player rides or is (the ship, the suit). The ground under them is kept solid
        /// and in its finest shape wherever they are, even when the player is somewhere else.
        /// </summary>
        public bool NeedsGround;
        /// <summary>
        /// How low this object's own origin can honestly be above the ground, in metres (negative is below it).
        /// Lower than this and it has sunk into the world, and <c>PlanetWorld</c> puts it back on top.
        /// </summary>
        public float SunkBelow = -2.5f;
        /// <summary>How far above the ground its origin is put when it is put back.</summary>
        public float PutBackAt = 2f;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.useGravity = false;
        }

        void OnEnable()
        {
            if (SolarSystem.Instance != null) SolarSystem.Instance.Register(this);
        }

        void OnDisable()
        {
            if (SolarSystem.Instance != null) SolarSystem.Instance.Unregister(this);
        }
    }
}
