using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// A loose chunk of one or more body parts flying around as a single physics object.
    /// It can be split again by shooting any part that is not its top part.
    /// </summary>
    public sealed class Gib : MonoBehaviour
    {
        public int Group { get; private set; }
        public Mannequin Owner { get; private set; }
        public Rigidbody Body { get; private set; }
        public readonly List<BodyPart> Parts = new List<BodyPart>();

        float _expiresAt;

        public void Setup(Mannequin owner, int group, Rigidbody body, float lifetime)
        {
            Owner = owner;
            Group = group;
            Body = body;
            _expiresAt = Time.time + lifetime;
        }

        public void RecalculateMass()
        {
            float mass = 0f;
            for (int i = 0; i < Parts.Count; i++) mass += Parts[i].Mass;
            Body.mass = Mathf.Max(0.5f, mass);
        }

        void Update()
        {
            if (Time.time >= _expiresAt)
            {
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            if (Owner != null) Owner.NotifyGibGone(this);
        }
    }
}
