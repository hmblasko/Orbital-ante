using System.Collections.Generic;
using OrbitalAnte.Combat;
using UnityEngine;

namespace OrbitalAnte.Characters
{
    /// <summary>
    /// Turns detached parts into jointed physics chains, and keeps the number of live gib
    /// bodies under the Steam Deck cap (oldest go first). Milestone 2 adds pooling, fading,
    /// blood and sound on top of this.
    /// </summary>
    public class GibManager : MonoBehaviour
    {
        static GibManager instance;
        static readonly List<BodyPart> live = new List<BodyPart>();
        static PhysicsMaterial gibMaterial;
        static int cap = 120;
        static float lifetime = 30f;

        public static int ActiveCount => live.Count;

        static GibManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Gibs");
                    instance = go.AddComponent<GibManager>();
                }
                return instance;
            }
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                live.Clear();
            }
        }

        /// <summary>
        /// Detach <paramref name="chain"/> (parents first) as one ragdoll chunk flying along the hit.
        /// </summary>
        public static void SpawnChain(List<BodyPart> chain, in HitInfo hit, Vector3 inheritVelocity, MannequinConfig cfg, float impulseShare)
        {
            if (chain.Count == 0) return;
            var mgr = Instance;
            cap = Mathf.Max(8, cfg.maxGibBodies);
            lifetime = cfg.gibLifetimeSeconds;
            if (gibMaterial == null)
            {
                gibMaterial = new PhysicsMaterial("Gib")
                {
                    bounciness = cfg.gibBounciness,
                    dynamicFriction = cfg.gibFriction,
                    staticFriction = cfg.gibFriction,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                    frictionCombine = PhysicsMaterialCombine.Average,
                };
            }

            var inChain = new HashSet<BodyPart>(chain);
            float now = Time.time;
            Vector3 dir = hit.Direction.sqrMagnitude > 0.0001f ? hit.Direction.normalized : Random.onUnitSphere;
            float scale = Mathf.Max(0.05f, hit.ForceScale) * impulseShare;
            Vector3 launch = inheritVelocity + dir * cfg.detachImpulse * scale + Vector3.up * cfg.detachLift * scale;

            foreach (var p in chain)
            {
                p.transform.SetParent(mgr.transform, true);
                var rb = p.gameObject.AddComponent<Rigidbody>();
                var tuning = cfg.Tuning(p.Id);
                rb.mass = Mathf.Max(0.1f, tuning.gibMass);
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.maxAngularVelocity = 60f;
                rb.solverIterations = 12;
                rb.solverVelocityIterations = 4;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.25f;

                p.Collider.isTrigger = false;
                p.Collider.sharedMaterial = gibMaterial;

                CharacterJoint joint = null;
                if (p.Parent != null && inChain.Contains(p.Parent) && p.Parent.Body != null)
                {
                    joint = p.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = p.Parent.Body;
                    joint.anchor = p.ProximalLocal;
                    joint.autoConfigureConnectedAnchor = true;
                    joint.axis = Vector3.up;
                    joint.swingAxis = Vector3.right;
                    joint.lowTwistLimit = new SoftJointLimit { limit = -cfg.jointTwistLimit };
                    joint.highTwistLimit = new SoftJointLimit { limit = cfg.jointTwistLimit };
                    joint.swing1Limit = new SoftJointLimit { limit = cfg.jointSwingLimit };
                    joint.swing2Limit = new SoftJointLimit { limit = cfg.jointSwingLimit };
                    joint.enableProjection = true;
                    joint.projectionDistance = 0.03f;
                    joint.projectionAngle = 15f;
                }
                p.BecomeGib(rb, joint, now);
                rb.linearVelocity = launch;
                live.Add(p);
            }

            chain[0].Body.angularVelocity = Random.insideUnitSphere * cfg.detachSpin * Mathf.Clamp01(scale + 0.3f);
            EnforceCap();
        }

        public static void Unregister(BodyPart p) => live.Remove(p);

        static void EnforceCap()
        {
            while (live.Count > cap)
            {
                var oldest = live[0];
                live.RemoveAt(0);
                if (oldest != null) oldest.DestroyGib();
            }
        }

        void Update()
        {
            float now = Time.time;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                if (p == null)
                {
                    live.RemoveAt(i);
                    continue;
                }
                bool expired = now - p.GibBornTime > lifetime;
                bool fellOut = p.transform.position.y < -60f;
                if (expired || fellOut)
                {
                    live.RemoveAt(i);
                    p.DestroyGib();
                }
            }
        }

        public static void ClearAll()
        {
            foreach (var p in live)
                if (p != null) Destroy(p.gameObject); // whole scene is going, no joint care needed
            live.Clear();
        }
    }
}
