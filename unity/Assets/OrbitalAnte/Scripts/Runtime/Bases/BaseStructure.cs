using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Bases
{
    /// <summary>
    /// One building, mast or heap of crates on a base, standing: it takes bullets and knocks
    /// (<see cref="StructureHealth"/>), opens its door when the player comes near, draws its rooms only when
    /// they could be seen, and falls when it has had enough. Falling swaps the whole thing for the pieces it was
    /// cut into in Blender (each loose, under the world's gravity, until it comes to rest) and the ruin left
    /// standing; <see cref="BaseWrecks"/> remembers it, so it is a ruin when the base is built again.
    /// </summary>
    public sealed class BaseStructure : MonoBehaviour, IHittable, IRammable
    {
        public static StructureRules Rules = new StructureRules();

        /// <summary>Every structure standing near the player, for the air stations and the debug readout.</summary>
        public static readonly List<BaseStructure> Standing = new List<BaseStructure>();

        public string Model { get; private set; }
        public int Index { get; private set; }
        public string Label { get; private set; }
        public StructureHealth Health { get; private set; }
        public long Site { get; private set; }
        /// <summary>Where an air station's face is, in the structure's own space (the base's), or null if it has none.</summary>
        public Vector3? AirPoint { get; private set; }

        /// <summary>The air left in this building's station, in full suit tanks.</summary>
        public float AirLeft
        {
            get => PlanetBases.Wrecks.AirLeft(Site, Index, Rules.stationTanks);
            set => PlanetBases.Wrecks.SetAir(Site, Index, value);
        }

        Transform _root;
        readonly List<GameObject> _whole = new List<GameObject>();
        readonly List<Renderer> _inside = new List<Renderer>();
        Transform _door;
        Vector3 _doorShut, _doorOpen;
        float _doorOpenness;
        Vector3 _mid;
        float _nextLook;
        bool _insideShown = true, _near = true;
        readonly Queue<GameObject> _settled = new Queue<GameObject>();

        /// <summary>A piece of this structure came to rest: kept, up to a cap per structure (the oldest go).</summary>
        public void Settled(GameObject piece)
        {
            _settled.Enqueue(piece);
            while (_settled.Count > Rules.mostSettledPieces)
            {
                GameObject old = _settled.Dequeue();
                if (old != null) Destroy(old);
            }
        }

        public static void ResetStatics()
        {
            Standing.Clear();
            Rules = new StructureRules();
            LoosePiece.ResetStatics();
        }

        /// <summary>Called by <see cref="BaseView"/> once its pieces are under it.</summary>
        public void Set(string model, int index, long site, string label, Bounds solid, Vector3 mid, bool enterable)
        {
            Model = model;
            Index = index;
            Site = site;
            Label = label;
            _root = transform.parent;
            _mid = mid;
            Health = new StructureHealth(Rules.Toughness(solid.size.x, solid.size.y, solid.size.z));
            _bulletShare = Rules.BulletShare(enterable, Health.Max);
        }

        float _bulletShare = 1f, _lastRam = -10f;

        /// <summary>Takes damage, warns as it weakens, falls on the last of it. True if this brought it down.</summary>
        bool Take(float damage, Vector3 push)
        {
            float before = Health.Worn;
            if (Health.Take(damage)) { Fall(push); return true; }
            int warning = Rules.Warning(before, Health.Worn);
            if (warning > 0)
            {
                Vector3 middle = transform.TransformPoint(_mid) + transform.up * (warning == 1 ? 1.2f : 2f);
                Weapons.WeaponFx fx = Weapons.WeaponFx.Instance;
                if (fx != null)
                {
                    for (int i = 0; i < (warning == 1 ? 3 : 6); i++) fx.Impact(middle + Random.insideUnitSphere * 1.2f, transform.up);
                    if (warning == 2) fx.Sparks(middle, transform.up, 12);
                }
                // Stand-in creak and groan until the audio thread makes real ones.
                Audio.AudioDirector.Play("impact.metal", middle, warning == 1 ? 0.6f : 0.9f, warning == 1 ? 0.55f : 0.4f);
            }
            return false;
        }

        public void AddWhole(GameObject go) => _whole.Add(go);
        public void AddInside(Renderer r) => _inside.Add(r);
        public void SetAir(Vector3 local) => AirPoint = local;

        public void SetDoor(Transform door, Vector3 shut, Vector3 open)
        {
            _door = door;
            _doorShut = shut;
            _doorOpen = open;
        }

        void OnEnable() => Standing.Add(this);
        void OnDisable() => Standing.Remove(this);

        void Update()
        {
            if (Health == null || Health.Fallen) return;
            Camera eye = Camera.main;
            if (eye == null) return;
            Vector3 local = transform.InverseTransformPoint(eye.transform.position);

            if (Time.unscaledTime >= _nextLook)
            {
                _nextLook = Time.unscaledTime + 0.3f;
                float d = (local - _mid).magnitude;
                _near = d < Rules.lookAfterWithin;
                bool show = _insideShown ? d < Rules.insideDrawDistance + 5f : d < Rules.insideDrawDistance;
                if (show != _insideShown)
                {
                    _insideShown = show;
                    for (int i = 0; i < _inside.Count; i++) if (_inside[i] != null) _inside[i].enabled = show;
                }
            }

            if (_door != null && _near)
            {
                Vector3 doorMiddle = _doorShut;
                bool near = (local - doorMiddle).sqrMagnitude < Rules.doorReach * Rules.doorReach;
                float was = _doorOpenness;
                _doorOpenness = DoorRule.Step(_doorOpenness, near, Time.deltaTime, Rules.doorSeconds);
                if (_doorOpenness != was)
                {
                    _door.localPosition = (_doorOpen - _doorShut) * DoorRule.Eased(_doorOpenness);
                    // A short hiss of the slide as it sets off, either way.
                    if (was == 0f || was == 1f)
                        Audio.AudioDirector.Play("ship.switch", transform.TransformPoint(doorMiddle), 0.5f, 0.7f);
                }
            }
        }

        public HitOutcome Hit(in HitInfo hit)
        {
            // Every bullet visibly lands; the hit marker and its ping are kept for enemies, and for the one shot
            // that brings a structure down.
            Weapons.WeaponFx fx = Weapons.WeaponFx.Instance;
            if (fx != null) fx.Impact(hit.Point, hit.Normal);
            var outcome = new HitOutcome { Hit = false, Label = Label };
            if (Health == null || Health.Fallen) return outcome;
            if (Take(hit.Damage * _bulletShare, hit.Direction))
            {
                outcome.Hit = true;
                outcome.Juice = 0.5f;
                outcome.Label = Label + " brought down";
            }
            return outcome;
        }

        public string DescribeForDebug() =>
            Health == null ? Label : Label + "  " + Mathf.RoundToInt((1f - Health.Worn) * 100f) + "%";

        /// <summary>A heavy loose thing (the ship) drove into it: a hard enough knock brings it down.</summary>
        public void Rammed(float speed, float mass, Vector3 direction)
        {
            if (Health == null || Health.Fallen) return;
            // Four footpads touching at once are one knock, not four.
            if (Time.time - _lastRam < Rules.ramRepeatSeconds) return;
            float damage = Rules.RamDamage(speed, mass);
            if (damage <= 0f) return;
            _lastRam = Time.time;
            Take(damage, direction);
        }

        /// <summary>Brings it down: the whole thing goes, its pieces fall, the ruin stands.</summary>
        public void Fall(Vector3 push)
        {
            if (Health != null && !Health.Fallen) Health.Take(Health.Left + 1f);
            PlanetBases.Wrecks.Fell(Site, Index);
            // Out of the physics at once (a ram brings it down inside the physics step), gone at the end of the frame.
            for (int i = 0; i < _whole.Count; i++)
            {
                if (_whole[i] == null) continue;
                _whole[i].SetActive(false);
                Destroy(_whole[i]);
            }
            _whole.Clear();
            _inside.Clear();
            _door = null;
            AirPoint = null;
            BaseView.BuildRuin(this, Model, Index);
            Vector3 middle = transform.TransformPoint(_mid);
            BaseView.Scatter(this, Model, Index, middle, push);
            Audio.AudioDirector.Play("impact.metal", middle + transform.up * 2f, 1f, 0.45f);
            Audio.AudioDirector.Play("impact.dirt", middle, 1f, 0.6f);
            enabled = false;
        }
    }

    /// <summary>
    /// A piece of a fallen structure, loose under the world's gravity (like the ship's wreckage) until it comes
    /// to rest; then it is made part of the base, still and solid, so it rides round with its world for nothing.
    /// </summary>
    public sealed class LoosePiece : MonoBehaviour
    {
        static readonly LinkedList<LoosePiece> Loose = new LinkedList<LoosePiece>();
        public static int LooseCount => Loose.Count;

        public static void ResetStatics() => Loose.Clear();

        Transform _home;
        float _born;
        float _floorY;
        LinkedListNode<LoosePiece> _node;
        Rigidbody _body;

        public void Begin(Transform home)
        {
            _home = home;
            _born = Time.time;
            _body = GetComponent<Rigidbody>();
            _floorY = home != null ? home.InverseTransformPoint(transform.position).y : 0f;
            _node = Loose.AddLast(this);
            while (Loose.Count > BaseStructure.Rules.mostLoosePieces && Loose.First != null && Loose.First.Value != this)
                Loose.First.Value.Settle();
        }

        void FixedUpdate()
        {
            if (_body == null) return;
            if (_home == null) { Destroy(gameObject); return; }
            float age = Time.time - _born;
            float below = -_home.InverseTransformPoint(transform.position).y;
            bool resting = age > 1.5f && _body.linearVelocity.sqrMagnitude < BaseStructure.Rules.pieceRestSpeed * BaseStructure.Rules.pieceRestSpeed
                           && _body.angularVelocity.sqrMagnitude < 0.25f;
            // Off the edge of the foundation the ground may not be solid (it is only solid near the player and
            // the ship), so a piece that has gone more than 1.5 m below the floor is stopped where it is.
            if (resting || age > BaseStructure.Rules.pieceSettleSeconds || below > 1.5f) Settle();
        }

        /// <summary>Stops it for good: no more physics, part of the base's ground.</summary>
        public void Settle()
        {
            if (_node != null) { Loose.Remove(_node); _node = null; }
            // Off the solar system's list and out of the simulation now: Destroy only lands at the end of the frame.
            var gravity = GetComponent<GravityBody>();
            if (gravity != null) { gravity.enabled = false; Destroy(gravity); }
            if (_body != null) { _body.isKinematic = true; Destroy(_body); }
            _body = null;
            if (_home != null)
            {
                transform.SetParent(_home, true);
                Layers.SetLayerRecursively(gameObject, Layers.Default);
                // Small rubble keeps no solid shape: every shape left on the planet's body is moved with it every step.
                var box = GetComponent<BoxCollider>();
                if (box != null && Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z)) < BaseStructure.Rules.solidPieceSize) Destroy(box);
                var owner = _home.GetComponent<BaseStructure>();
                if (owner != null) owner.Settled(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            enabled = false;
        }

        void OnDestroy()
        {
            if (_node != null) { Loose.Remove(_node); _node = null; }
        }
    }
}
