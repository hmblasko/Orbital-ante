using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// A standing tree that can be felled: on the holder of its trunk's capsule, so bullets
    /// (<see cref="IHittable"/>) and the ship (<see cref="IRammable"/>) find it. Pooled with the tree it
    /// belongs to: <see cref="PlanetFeatures"/> sets it up again every time the tree is put out.
    /// </summary>
    public sealed class TreeTrunk : MonoBehaviour, IHittable, IRammable
    {
        PlanetFeatures _owner;
        float _left, _max;

        /// <summary>The pooled item this trunk belongs to (only <see cref="PlanetFeatures"/> reads it).</summary>
        public object Tag { get; private set; }
        public Vec3d Position { get; private set; }
        public FeatureKind Kind { get; private set; }
        public int Variant { get; private set; }
        /// <summary>The tree's height in metres, and its trunk's radius.</summary>
        public float Scale { get; private set; }
        public float TrunkRadius { get; private set; }
        /// <summary>False for a stump, or a tree that cannot be felled.</summary>
        public bool Standing { get; private set; }

        public void Set(PlanetFeatures owner, object tag, Vec3d position, FeatureKind kind, int variant, float scale, float trunkRadius, bool standing)
        {
            _owner = owner;
            Tag = tag;
            Position = position;
            Kind = kind;
            Variant = variant;
            Scale = scale;
            TrunkRadius = trunkRadius;
            Standing = standing;
            _max = _left = PlanetFeatures.Felling.Toughness(trunkRadius);
        }

        public HitOutcome Hit(in HitInfo hit)
        {
            Weapons.WeaponFx fx = Weapons.WeaponFx.Instance;
            if (fx != null) fx.Impact(hit.Point, hit.Normal);
            var outcome = new HitOutcome { Hit = false, Label = Kind.ToString() };
            if (!Standing) return outcome;
            if (Take(hit.Damage, hit.Direction))
            {
                outcome.Hit = true;
                outcome.Juice = 0.35f;
                outcome.Label = Kind + " felled";
            }
            return outcome;
        }

        public void Rammed(float speed, float mass, Vector3 direction)
        {
            if (!Standing || Time.time - _lastRam < 0.25f) return;
            float damage = PlanetFeatures.Felling.RamDamage(speed, mass);
            if (damage <= 0f) return;
            _lastRam = Time.time;
            Take(damage, direction);
        }

        float _lastRam = -10f;

        bool Take(float damage, Vector3 push)
        {
            if (!Standing || damage <= 0f || float.IsNaN(damage)) return false;
            float before = _left;
            _left -= damage;
            if (_left > 0f)
            {
                // A creak once it is more than half cut through: it is going to go.
                if (before > _max * 0.4f && _left <= _max * 0.4f)
                    Audio.AudioDirector.Play("impact.dirt", transform.position, 0.6f, 0.5f);
                return false;
            }
            Standing = false;
            if (_owner != null) _owner.Fell(this, push);
            return true;
        }

        public string DescribeForDebug() =>
            Standing ? Kind + "  " + Mathf.RoundToInt(100f * Mathf.Max(0f, _left) / Mathf.Max(1f, _max)) + "%" : Kind + " stump";
    }

    /// <summary>
    /// The top of a felled tree while it falls: loose under the world's gravity until it comes to rest, then
    /// still and solid, kept with its stump until that part of the world is put away.
    /// </summary>
    public sealed class FallingTop : MonoBehaviour
    {
        static readonly LinkedList<FallingTop> Falling = new LinkedList<FallingTop>();
        public static int FallingCount => Falling.Count;
        public static void ResetStatics() => Falling.Clear();

        Transform _home;
        FellingRules _rules;
        Rigidbody _body;
        float _born;
        LinkedListNode<FallingTop> _node;

        public void Begin(Transform home, FellingRules rules)
        {
            _home = home;
            _rules = rules;
            _body = GetComponent<Rigidbody>();
            _born = Time.time;
            _node = Falling.AddLast(this);
            while (Falling.Count > rules.mostFalling && Falling.First != null && Falling.First.Value != this) Falling.First.Value.Settle();
        }

        void FixedUpdate()
        {
            if (_body == null) return;
            if (_home == null || !_home.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            float age = Time.time - _born;
            // Below its own foot by more than a metre: the ground here is not solid (it is only solid near the
            // player and the ship), so it is stopped where it is rather than falling for ever.
            float below = -Vector3.Dot(transform.position - _home.position, _home.up);
            bool resting = age > 2f && _body.linearVelocity.sqrMagnitude < 0.04f && _body.angularVelocity.sqrMagnitude < 0.04f;
            if (resting || age > _rules.settleSeconds || below > 1f) Settle();
        }

        public void Settle()
        {
            if (_node != null) { Falling.Remove(_node); _node = null; }
            var gravity = GetComponent<GravityBody>();
            if (gravity != null) { gravity.enabled = false; Destroy(gravity); }
            if (_body != null) { _body.isKinematic = true; Destroy(_body); }
            _body = null;
            if (_home == null) { Destroy(gameObject); return; }
            transform.SetParent(_home, true);
            Layers.SetLayerRecursively(gameObject, Layers.Default);
            enabled = false;
        }

        void OnDestroy()
        {
            if (_node != null) { Falling.Remove(_node); _node = null; }
        }
    }
}
