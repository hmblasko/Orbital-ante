using System.Collections.Generic;
using OrbitalAnte.Combat;
using OrbitalAnte.Feel;
using UnityEngine;

namespace OrbitalAnte.Characters
{
    /// <summary>
    /// Turns detached parts into jointed physics chains, keeps live gib bodies under the Steam Deck
    /// cap (oldest go first), shrinks them away at the end of their life, and pools the part
    /// objects so new dummies reuse old gibs instead of allocating.
    /// </summary>
    public class GibManager : MonoBehaviour
    {
        static GibManager instance;
        static readonly List<BodyPart> live = new List<BodyPart>();
        static readonly Dictionary<BodyPartId, Stack<BodyPart>> pool = new Dictionary<BodyPartId, Stack<BodyPart>>();
        static PhysicsMaterial gibMaterial;
        static int cap = 120;
        static float lifetime = 30f;
        Transform poolRoot;

        public static int ActiveCount => live.Count;

        static GibManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Gibs");
                    instance = go.AddComponent<GibManager>();
                    instance.poolRoot = new GameObject("Pool").transform;
                    instance.poolRoot.SetParent(go.transform, false);
                }
                return instance;
            }
        }

        void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            live.Clear();
            pool.Clear();
            gibMaterial = null;
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
                var rb = p.GetComponent<Rigidbody>();
                if (rb == null) rb = p.gameObject.AddComponent<Rigidbody>();
                var tuning = cfg.Tuning(p.Id);
                rb.isKinematic = false;
                rb.useGravity = true;
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
                rb.angularVelocity = Vector3.zero;

                var fx = p.GetComponent<GibImpactFx>();
                if (fx == null) fx = p.gameObject.AddComponent<GibImpactFx>();
                fx.enabled = true;
                live.Add(p);
            }

            chain[0].Body.angularVelocity = Random.insideUnitSphere * cfg.detachSpin * Mathf.Clamp01(scale + 0.3f);
            EnforceCap();
        }

        public static void Unregister(BodyPart p) => live.Remove(p);

        /// <summary>Takes a pooled part of this type, or null. The caller re-parents and re-inits it.</summary>
        public static BodyPart TakePooled(BodyPartId id)
        {
            if (!pool.TryGetValue(id, out var stack)) return null;
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p != null) return p;
            }
            return null;
        }

        /// <summary>Retires a gib: frees anything jointed to it, then parks it in the pool.</summary>
        public static void Recycle(BodyPart p)
        {
            if (p == null) return;
            live.Remove(p);
            p.ReleaseChildJoints();
            var stack = pool.TryGetValue(p.Id, out var s) ? s : pool[p.Id] = new Stack<BodyPart>();
            int max = Mathf.Max(0, GameFeelConfig.Get().poolPerPart);
            if (instance == null || stack.Count >= max)
            {
                Destroy(p.gameObject);
                return;
            }
            p.ResetForPool();
            p.transform.SetParent(instance.poolRoot, false);
            stack.Push(p);
        }

        static void EnforceCap()
        {
            while (live.Count > cap)
            {
                var oldest = live[0];
                live.RemoveAt(0);
                Recycle(oldest);
            }
        }

        void Update()
        {
            float now = Time.time;
            float fade = Mathf.Max(0.01f, GameFeelConfig.Get().gibFadeSeconds);
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (i >= live.Count) continue;
                var p = live[i];
                if (p == null)
                {
                    live.RemoveAt(i);
                    continue;
                }
                float age = now - p.GibBornTime;
                bool fellOut = p.transform.position.y < -60f;
                if (age > lifetime || fellOut)
                {
                    Recycle(p);
                    continue;
                }
                float remaining = lifetime - age;
                if (remaining < fade)
                {
                    // Shrink away (opaque materials can't alpha-fade; a quick shrink reads cleanly).
                    float s = Mathf.SmoothStep(0f, 1f, remaining / fade);
                    p.transform.localScale = Vector3.one * Mathf.Max(0.01f, s);
                }
            }
        }

        public static void ClearAll()
        {
            for (int i = live.Count - 1; i >= 0; i--) Recycle(live[i]);
            live.Clear();
        }
    }
}
