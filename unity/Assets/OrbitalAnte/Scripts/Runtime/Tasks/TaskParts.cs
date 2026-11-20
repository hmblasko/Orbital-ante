using System;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// One glowing cell of a signal mast (TaskDirector). Bullets find it through <see cref="IHittable"/>; the
    /// director keeps the rule. It is not an enemy: no hit marker, no ding, and it never counts as a kill.
    /// </summary>
    public sealed class MastCell : MonoBehaviour, IHittable
    {
        public int Cell;
        /// <summary>Damage, point, surface normal.</summary>
        [NonSerialized] public Action<float, Vector3, Vector3> Struck;

        public HitOutcome Hit(in HitInfo hit)
        {
            Struck?.Invoke(hit.Lethal ? 9999f : hit.Damage, hit.Point, hit.Normal);
            return new HitOutcome { Hit = true, Label = "mast cell" };
        }

        public string DescribeForDebug() => "signal mast cell " + Cell;
    }

    /// <summary>
    /// The pole of a signal mast: a rover driven into it fast enough brings the whole mast down. The pole is part of
    /// its planet's compound collider, so Unity tells the planet, not the pole: the rover reports the knock here
    /// (MoonRover.OnCollisionEnter).
    /// </summary>
    public sealed class MastRam : MonoBehaviour
    {
        public float Speed = 15f;
        [NonSerialized] public Action Rammed;

        public void Struck(float closingSpeed)
        {
            if (Rammed != null && closingSpeed >= Speed) Rammed();
        }
    }
}
