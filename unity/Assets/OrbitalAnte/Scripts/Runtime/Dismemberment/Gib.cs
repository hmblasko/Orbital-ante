using System.Collections.Generic;
using OrbitalAnte.Gore;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// A loose chunk of one or more body parts flying around as a single physics object.
    /// It can be split again by shooting any part that is not its top part.
    ///
    /// The container object is pooled and reused by <see cref="GibManager"/>, which also runs its lifetime and fade.
    /// </summary>
    public sealed class Gib : MonoBehaviour
    {
        public int Group { get; private set; }
        public Mannequin Owner { get; private set; }
        public Rigidbody Body { get; private set; }
        public readonly List<BodyPart> Parts = new List<BodyPart>();

        /// <summary>Seconds since this chunk came off.</summary>
        public float Age { get; set; }
        /// <summary>Age at which the fade began, or a negative number while not fading.</summary>
        public float FadeStartAge { get; private set; } = -1f;
        public float FadeDuration { get; private set; }
        public bool Fading => FadeStartAge >= 0f;
        /// <summary>True between being handed out by the pool and being returned to it.</summary>
        public bool InUse { get; private set; }

        int _decalsLeft;
        float _nextSplatTime;

        public void CreateBody()
        {
            Body = gameObject.AddComponent<Rigidbody>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.maxAngularVelocity = 40f;
            Body.angularDamping = 0.2f;
        }

        public void Activate(Mannequin owner, int group, int decalBudget)
        {
            Owner = owner;
            Group = group;
            Age = 0f;
            FadeStartAge = -1f;
            FadeDuration = 0f;
            InUse = true;
            _decalsLeft = decalBudget;
            _nextSplatTime = 0f;
            transform.localScale = Vector3.one;
        }

        public void Deactivate()
        {
            Owner = null;
            InUse = false;
            Parts.Clear();
        }

        public void BeginFade(float duration)
        {
            if (Fading) return;
            FadeStartAge = Age;
            FadeDuration = Mathf.Max(0.01f, duration);
        }

        public void RecalculateMass()
        {
            float mass = 0f;
            for (int i = 0; i < Parts.Count; i++)
            {
                if (Parts[i] != null) mass += Parts[i].Mass;
            }
            Body.mass = Mathf.Max(0.5f, mass);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!InUse || _decalsLeft <= 0 || Fading) return;
            GoreFx fx = GoreFx.Instance;
            if (fx == null) return;
            if (Time.time < _nextSplatTime) return;
            if (collision.contactCount == 0) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < fx.Config.decalMinImpactSpeed) return;

            ContactPoint contact = collision.GetContact(0);
            // Only splat on the world, not on other chunks.
            if (contact.otherCollider == null || contact.otherCollider.gameObject.layer != Core.Layers.Default) return;

            _decalsLeft--;
            _nextSplatTime = Time.time + 0.12f;
            fx.GibLanded(contact.point, contact.normal, speed, Body.mass);
        }
    }
}
