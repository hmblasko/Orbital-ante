using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// What a character stands on. The test arena is flat with "up" along +Y; a planet is a ball whose "up"
    /// is away from its middle and whose ground is only solid close to the player. A character asks its
    /// footing which way is up, where the ground is, and where it may wander, and never assumes +Y itself.
    /// </summary>
    public class Footing
    {
        /// <summary>Flat ground, +Y up: the combat arena.</summary>
        public static readonly Footing Flat = new Footing();

        public virtual bool IsFlat => true;

        public virtual Vector3 Up(Vector3 at) => Vector3.up;

        /// <summary>The ground under a point. False (and the point unchanged) if there is none to be found.</summary>
        public virtual bool Ground(Vector3 at, Vector3 up, out Vector3 point)
        {
            if (Physics.Raycast(at + up * 2f, -up, out RaycastHit hit, 6f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            point = at;
            return false;
        }

        /// <summary>Somewhere to stroll to.</summary>
        public virtual Vector3 WanderPoint(SeededRandom rng, Bounds area, Vector3 from)
        {
            Vector3 min = area.min, max = area.max;
            float x = Mathf.Lerp(min.x, max.x, (float)rng.NextDouble());
            float z = Mathf.Lerp(min.z, max.z, (float)rng.NextDouble());
            return new Vector3(x, from.y, z);
        }

        /// <summary>
        /// A place to be remembered across frames. On a planet the whole world can be moved under the player,
        /// so places are kept in the planet's own space; on flat ground they are kept as they are.
        /// </summary>
        public virtual Vector3 Hold(Vector3 world) => world;
        public virtual Vector3 Recall(Vector3 held) => held;
    }
}
