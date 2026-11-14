using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Gore;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// Pools the physics containers that loose chunks ride in, keeps the number of live chunks under a cap
    /// (Steam Deck budget), and runs their lifetime: about 30 seconds, then a short fade.
    ///
    /// The fade is a shrink rather than a transparency fade: the parts use cheap opaque materials, and shrinking
    /// keeps them that way.
    /// </summary>
    public sealed class GibManager : MonoBehaviour
    {
        static GibManager _instance;

        GoreConfig _cfg;
        PhysicsMaterial _bouncy;
        readonly List<Gib> _active = new List<Gib>();
        readonly Stack<Gib> _free = new Stack<Gib>();
        readonly List<GibCapRule.Slot> _slots = new List<GibCapRule.Slot>();
        System.Action<int> _removeNow;
        System.Action<int> _beginOverflowFade;
        int _created;

        public static GibManager Instance => _instance;
        public GoreConfig Config => _cfg;
        public int ActiveCount => _active.Count;
        /// <summary>Containers created so far. Stops growing once the pool is warm.</summary>
        public int CreatedCount => _created;
        public PhysicsMaterial BouncyMaterial => _bouncy;

        /// <summary>The current manager, creating a default one if the scene has none (tests, odd scenes).</summary>
        public static GibManager GetOrCreate()
        {
            if (_instance == null) Create(null, GoreConfig.Load());
            return _instance;
        }

        public static GibManager Create(Transform parent, GoreConfig config)
        {
            if (_instance != null) Destroy(_instance.gameObject);
            var go = new GameObject("GibManager");
            if (parent != null) go.transform.SetParent(parent, false);
            _instance = go.AddComponent<GibManager>();
            _instance.Initialize(config);
            return _instance;
        }

        public static void DestroyInstance()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        public static void ResetStatics()
        {
            _instance = null;
        }

        void Initialize(GoreConfig config)
        {
            _cfg = config;
            _bouncy = new PhysicsMaterial("GibBouncy")
            {
                bounciness = Mathf.Clamp01(config.gibBounciness),
                dynamicFriction = Mathf.Clamp01(config.gibFriction),
                staticFriction = Mathf.Clamp01(config.gibFriction),
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average,
            };

            int prewarm = Mathf.Clamp(config.poolPrewarm, 0, 256);
            for (int i = 0; i < prewarm; i++) _free.Push(NewContainer());
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        Gib NewContainer()
        {
            var go = new GameObject("Gib");
            go.layer = Layers.Gib;
            go.transform.SetParent(transform, false);
            var gib = go.AddComponent<Gib>();
            gib.CreateBody();
            go.SetActive(false);
            _created++;
            return gib;
        }

        /// <summary>
        /// Hands out a container at a position. Near the cap the oldest chunk is hurried out; at the cap one is
        /// removed at once (see <see cref="GibCapRule"/>). <paramref name="splittingFrom"/> is the chunk the new one
        /// takes its parts from, if any; it is never removed here.
        /// </summary>
        public Gib Spawn(Mannequin owner, int group, Vector3 position, Quaternion rotation, Gib splittingFrom = null)
        {
            EnforceCap(splittingFrom);

            Gib gib = _free.Count > 0 ? _free.Pop() : NewContainer();
            gib.transform.SetPositionAndRotation(position, rotation);
            gib.Activate(owner, group, _cfg.decalsPerGib);
            gib.gameObject.SetActive(true);
            gib.Body.linearVelocity = Vector3.zero;
            gib.Body.angularVelocity = Vector3.zero;
            _active.Add(gib);
            return gib;
        }

        /// <summary>
        /// Keeps live chunks, fading ones included, within maxActiveGibs: a fading chunk still has a rigidbody,
        /// a collider and a renderer. Before this, fading chunks were not counted, so a burst could grow past the cap
        /// (Codex Deck audit, PR #18).
        /// </summary>
        void EnforceCap(Gib keep)
        {
            _slots.Clear();
            int keepIndex = -1;
            for (int i = 0; i < _active.Count; i++)
            {
                Gib gib = _active[i];
                if (ReferenceEquals(gib, keep)) keepIndex = i;
                _slots.Add(new GibCapRule.Slot { Fading = gib.Fading, FadeProgress = FadeProgress(gib) });
            }
            if (_removeNow == null)
            {
                _removeNow = RemoveForCap;
                _beginOverflowFade = BeginOverflowFade;
            }
            GibCapRule.MakeRoom(_slots, _cfg.maxActiveGibs, _cfg.overflowFadeSlots, keepIndex, _removeNow, _beginOverflowFade);
        }

        void RemoveForCap(int index)
        {
            _slots.RemoveAt(index);
            ReleaseAt(index);
        }

        void BeginOverflowFade(int index)
        {
            _active[index].BeginFade(_cfg.overflowFadeTime);
            _slots[index] = new GibCapRule.Slot { Fading = true, FadeProgress = 0f };
        }

        static float FadeProgress(Gib gib)
        {
            if (!gib.Fading) return 0f;
            return (gib.Age - gib.FadeStartAge) / gib.FadeDuration;
        }

        /// <summary>Prepares a part for life as loose debris: bouncy surface, gib layer.</summary>
        public void PreparePart(BodyPart part)
        {
            Layers.SetLayerRecursively(part.gameObject, Layers.Gib);
            if (part.Hitbox != null) part.Hitbox.sharedMaterial = _bouncy;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Gib gib = _active[i];
                gib.Age += dt;

                if (!gib.Fading && gib.Age >= _cfg.gibLifetime)
                {
                    gib.BeginFade(_cfg.gibFadeTime);
                }

                if (gib.Fading)
                {
                    float t = FadeProgress(gib);
                    if (t >= 1f)
                    {
                        ReleaseAt(i);
                        continue;
                    }
                    // Ease in, so it lingers at full size and then pops away.
                    float scale = 1f - t * t;
                    gib.transform.localScale = Vector3.one * Mathf.Max(0.02f, scale);
                }
            }
        }

        public void Release(Gib gib)
        {
            int index = _active.IndexOf(gib);
            if (index >= 0) ReleaseAt(index);
        }

        void ReleaseAt(int index)
        {
            Gib gib = _active[index];
            _active.RemoveAt(index);

            // The parts were made for one character and are finished; only the container is reused.
            for (int i = 0; i < gib.Parts.Count; i++)
            {
                BodyPart part = gib.Parts[i];
                if (part == null) continue;
                // Destroy waits for the end of the frame, and the container may be handed out again before then
                // (the cap removes a chunk inside Spawn), so take the parts out of play now.
                part.gameObject.SetActive(false);
                part.transform.SetParent(null, false);
                Destroy(part.gameObject);
            }

            Mannequin owner = gib.Owner;
            gib.Deactivate();
            gib.transform.localScale = Vector3.one;
            gib.gameObject.SetActive(false);
            _free.Push(gib);

            if (owner != null) owner.NotifyGibGone(gib.Group, gib);
        }

        /// <summary>Clears every loose chunk straight away (used when the arena is reset).</summary>
        public void ReleaseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--) ReleaseAt(i);
        }
    }
}
