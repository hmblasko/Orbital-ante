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
