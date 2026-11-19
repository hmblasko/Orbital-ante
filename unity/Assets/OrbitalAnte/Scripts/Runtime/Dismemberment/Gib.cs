using System.Collections.Generic;
using OrbitalAnte.Gore;
using OrbitalAnte.Space;
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
        /// <summary>In the solar system, the pull toward the world it is on (null in the flat arena).</summary>
        public GravityBody Pull { get; private set; }
        /// <summary>True while the solar system pulls and moves it, so a re-centre must leave it alone.</summary>
        public bool OwnPull => Pull != null && Pull.enabled;

        /// <summary>Seconds since this chunk came off.</summary>
        public float Age { get; set; }
        /// <summary>Age at which the fade began, or a negative number while not fading.</summary>
        public float FadeStartAge { get; private set; } = -1f;
        public float FadeDuration { get; private set; }
        public bool Fading => FadeStartAge >= 0f;
        /// <summary>True between being handed out by the pool and being returned to it.</summary>
        public bool InUse { get; private set; }

        System.Action<Vector3, Transform> _sank;
        /// <summary>
        /// True once it has sunk into ground that is not solid (nobody near, often a limb thrown far on a light
        /// world): it is laid still on top and the manager fades it out there, so it never vanishes mid-air.
        /// </summary>
        public bool Sunk { get; private set; }

        void SankInto(Vector3 onTop, Transform world)
        {
            // Called while the solar system walks its loose objects: only made still here, nothing switched off.
            Body.position = onTop;
            transform.position = onTop;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            Sunk = true;
        }

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

        /// <summary>
        /// In the solar system every chunk falls toward its own world, not the player's "down": a limb shot off on
        /// a small moon falls onto that moon wherever the player is. The solar system then pulls it, moves it when
        /// the world re-centres, and gives it the air's drag and the sea's lift like everything else loose.
        /// Call while the container is inactive (from the pool).
        /// </summary>
        public void UseOwnPull(bool on)
        {
            if (on && Pull == null)
            {
                Pull = gameObject.AddComponent<GravityBody>();
                // The container's origin is at its top part, a hand's width or so above whatever it rests on.
                Pull.SunkBelow = -1f;
                Pull.PutBackAt = 0.3f;
            }
            if (Pull != null)
            {
                Pull.enabled = on;
                Pull.Sank = on ? (_sank ??= SankInto) : null;
            }
            Body.useGravity = !on;
            // Objects in motion stay in motion: in the solar system a tumbling limb keeps nearly all its spin (the
            // air, where there is any, and the ground slow it); the arena keeps its old, quicker settle.
            Body.angularDamping = on ? 0.03f : 0.2f;
        }

        public void Activate(Mannequin owner, int group, int decalBudget)
        {
            Owner = owner;
            Group = group;
            Age = 0f;
            FadeStartAge = -1f;
            FadeDuration = 0f;
            InUse = true;
            // One laid still where it sank last time moves again.
            if (Sunk && Body != null) Body.isKinematic = false;
            Sunk = false;
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
            fx.GibLanded(contact.point, contact.normal, speed, Body.mass, contact.otherCollider);
        }
    }
}
