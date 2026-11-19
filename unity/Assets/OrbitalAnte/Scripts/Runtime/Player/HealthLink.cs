using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// Put on a collider that stands in for the player somewhere their own body is not (the driver's seat of the
    /// rover): a shot that hits it hurts <see cref="Target"/>. Enemy shots look for it after looking for a
    /// <see cref="PlayerHealth"/> (see Ballistics.FireFromEnemy).
    /// </summary>
    public sealed class HealthLink : MonoBehaviour
    {
        public PlayerHealth Target;
    }
}
