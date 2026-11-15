using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using UnityEngine;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// The player's body bursting when they die, seen from the head as it flies (<see cref="DeathBurst"/> has the
    /// rules). A breakable body like an enemy's is made where the player was, every part of it is broken in a quick
    /// run, and the existing gib, blood, decal and pop systems do the rest. The view is put on the head each frame
    /// (never parented to it, so the camera can never be put away with a piece).
    ///
    /// The pieces are pulled by this, not by Unity's global gravity: outside, by what the suit felt; in the cabin,
    /// toward the cabin floor plus whatever the ship itself is doing, so they fall the way the walker would.
    /// </summary>
    // Before the solar system centres the sky and stars on the view (-500), so they use this frame's eye.
    [DefaultExecutionOrder(-600)]
    public sealed class PlayerBurstFx : MonoBehaviour
    {
        /// <summary>Stand-in for the player's body until there is a suit model built on the breakable body.</summary>
        public const string BodyName = "PlayerRemains";

        sealed class FixedUp : Footing
        {
            readonly Vector3 _up;
            public FixedUp(Vector3 up) { _up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up; }
            public override bool IsFlat => false;
            public override Vector3 Up(Vector3 at) => _up;
        }

        Mannequin _body;
        Camera _eye;
        Ship _ship;            // set when the burst is in the cabin
        GravityBodyRead _pull;  // what pulls the pieces outside
        Transform _head;
        Vector3 _eyeOffset;    // the eye, in the head's own space
        Quaternion _eyeTurn;
        float _age;
        int _next;
        Vector3 _blow;
        Vector3 _shipVelocity;
        bool _hasShipVelocity;
        Rigidbody _cage;
        readonly List<Gib> _pieces = new List<Gib>();
        Collider[] _cageWalls;

        /// <summary>The burst in the cabin right now, if any (only one player dies at a time).</summary>
        static PlayerBurstFx _caged;

        /// <summary>Loose wreckage torn off the ship must never be pushed by the cage (it is on the gib layer too).</summary>
        public static void IgnoreCage(Collider[] loose)
        {
            if (_caged == null || _caged._cageWalls == null || loose == null) return;
            for (int a = 0; a < loose.Length; a++)
            {
                if (loose[a] == null) continue;
                for (int b = 0; b < _caged._cageWalls.Length; b++) Physics.IgnoreCollision(loose[a], _caged._cageWalls[b], true);
            }
        }

        public static void ResetStatics() => _caged = null;

        /// <summary>What pulls the pieces when outside: read each step, since the suit keeps falling too.</summary>
        public delegate Vector3 GravityBodyRead();

        /// <summary>
        /// Bursts the player's body. <paramref name="feet"/> and <paramref name="rotation"/> place it (upright the
        /// way the player stood); <paramref name="blow"/> is the way the killing blow pushed. <paramref name="ship"/> is
        /// the ship when the player died inside it, otherwise null and <paramref name="pull"/> says what pulls.
        /// Returns null if the body could not be made, and the caller keeps the old slump.
        /// </summary>
        public static PlayerBurstFx Begin(Transform parent, Vector3 feet, Quaternion rotation, Vector3 velocity, Vector3 blow,
            Camera eye, Ship ship, GravityBodyRead pull)
        {
            Mannequin body = MannequinFactory.Create(feet, rotation, parent, MannequinConfig.Load(), BodyName);
            if (body == null) return null;
            // It is remains, not a fighter: no brain, no legs to walk on, nothing that stops anyone walking.
            var combat = body.GetComponent<AI.MannequinCombat>();
            if (combat != null) DestroyImmediate(combat);
            var locomotion = body.GetComponent<MannequinLocomotion>();
            if (locomotion != null) DestroyImmediate(locomotion);
            Transform blocker = body.transform.Find("BodyBlocker");
            if (blocker != null && blocker.TryGetComponent(out Collider c)) c.enabled = false;

            var fx = body.gameObject.AddComponent<PlayerBurstFx>();
            fx._body = body;
            fx._eye = eye;
            fx._ship = ship;
            fx._pull = pull;
            fx._blow = blow.sqrMagnitude > 1e-6f ? blow.normalized : -(rotation * Vector3.forward);
            body.Footing = new FixedUp(rotation * Vector3.up);
            body.Velocity = velocity;
            body.PartDetached += fx.OnPartDetached;

            BodyPart head = body.Part(BodyPartId.Head);
            if (head != null)
            {
                fx._head = head.transform;
                Vector3 eyeAt = eye != null ? eye.transform.position : HeadCentre(head);
                // Stand the body so its head is where the eye was, sitting or standing, so the view does not jump.
                body.transform.position += eyeAt - HeadCentre(head);
                // The view is carried at the middle of the head, never out past it: it must not poke through a wall or
                // the ground the head is resting on (the camera sees 0.12 m ahead of itself).
                Vector3 centre = head.transform.InverseTransformPoint(HeadCentre(head));
                Vector3 offset = head.transform.InverseTransformPoint(eyeAt) - centre;
                fx._eyeOffset = centre + Vector3.ClampMagnitude(offset, EyeFromCentre);
                fx._eyeTurn = Quaternion.Inverse(head.transform.rotation) * (eye != null ? eye.transform.rotation : rotation);
                // You do not see the inside of your own helmet.
                foreach (var r in head.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }

            if (ship != null) fx.BuildCage();
            body.KeepsHeadClean = true;
            fx.Tick(0f);
            return fx;
        }

        /// <summary>Most the view sits from the middle of the head, metres.</summary>
        public const float EyeFromCentre = 0.04f;

        static Vector3 HeadCentre(BodyPart head)
        {
            Transform t = head.transform;
            switch (head.Hitbox)
            {
                case SphereCollider sphere: return t.TransformPoint(sphere.center);
                case CapsuleCollider capsule: return t.TransformPoint(capsule.center);
                case BoxCollider box: return t.TransformPoint(box.center);
                default: return t.position;
            }
        }

        /// <summary>Where the ship will be at the end of this physics step, so the cage keeps up with the cabin.</summary>
        static void Ahead(Rigidbody hull, float dt, out Vector3 position, out Quaternion rotation)
        {
            position = hull.position + hull.linearVelocity * dt;
            Vector3 w = hull.angularVelocity;
            float angle = w.magnitude * dt * Mathf.Rad2Deg;
            rotation = angle > 1e-4f ? Quaternion.AngleAxis(angle, w.normalized) * hull.rotation : hull.rotation;
        }

        void BuildCage()
        {
            var go = new GameObject("BurstCage");
            go.layer = Layers.Gib;
            go.transform.SetParent(transform.parent, false);
            Ahead(_ship.Body, Time.fixedDeltaTime, out Vector3 at, out Quaternion turn);
            go.transform.SetPositionAndRotation(at, turn);
            _cage = go.AddComponent<Rigidbody>();
            _cage.isKinematic = true;
            _cage.useGravity = false;
            _cage.interpolation = RigidbodyInterpolation.None;
            // The cage is only for the player's pieces: it touches nothing but loose pieces.
            _cage.excludeLayers = ~(1 << Layers.Gib);
            List<DeathBurst.Box> boxes = DeathBurst.CageBoxes();
            _cageWalls = new Collider[boxes.Count];
            int made = 0;
            foreach (DeathBurst.Box box in boxes)
            {
                var b = new GameObject("CageBox");
                b.layer = Layers.Gib;
                b.transform.SetParent(go.transform, false);
                b.transform.localPosition = new Vector3(box.Centre.X, box.Centre.Y, box.Centre.Z);
                b.transform.localRotation = Quaternion.Euler(0f, box.Yaw * Mathf.Rad2Deg, 0f);
                var collider = b.AddComponent<BoxCollider>();
                collider.size = new Vector3(box.Half.X * 2f, box.Half.Y * 2f, box.Half.Z * 2f);
                if (GibManager.Instance != null) collider.sharedMaterial = GibManager.Instance.BouncyMaterial;
                _cageWalls[made++] = collider;
            }
            _caged = this;
            // Wreckage the killing blow has already torn off is on the gib layer too: the cage leaves it alone.
            // (Nothing of the player's body is loose yet: the body breaks after this.)
            foreach (Collider other in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (other != null && other.gameObject.layer == Layers.Gib && other.attachedRigidbody != _cage) IgnoreCage(new[] { other });
            _shipVelocity = _ship.Body.linearVelocity;
            _hasShipVelocity = true;
        }

        void OnPartDetached(Mannequin who, Detachment detachment, Gib gib, HitInfo hit)
        {
            if (gib == null || gib.Body == null) return;
            if (!_pieces.Contains(gib)) _pieces.Add(gib);
            gib.Body.useGravity = false;
            if (_ship != null)
            {
                // In the cabin the hull's solid shapes are ignored (the pieces start inside them); the cage holds them.
                foreach (BodyPart part in gib.Parts)
                    if (part != null && part.Hitbox != null) part.Hitbox.excludeLayers = 1 << Layers.Default;
            }
            if (detachment.Root == BodyPartId.Head)
            {
                float fling = _ship != null ? DeathBurst.HeadFlingAboard : DeathBurst.HeadFlingOutside;
                gib.Body.linearVelocity = _body.Velocity + (gib.Body.linearVelocity - _body.Velocity) * fling;
                gib.Body.angularVelocity = Random.onUnitSphere * Random.Range(DeathBurst.HeadSpinLeast, DeathBurst.HeadSpinMost);
            }
        }

        void Update() => Tick(Time.deltaTime);

        void Tick(float dt)
        {
            _age += dt;
            while (_next < DeathBurst.Steps.Length && DeathBurst.Steps[_next].At <= _age)
            {
                DeathBurst.Step step = DeathBurst.Steps[_next++];
                BodyPart part = _body.Part(step.Part);
                if (part == null) continue;
                // Each piece is thrown out from the body's middle, along with the blow.
                Vector3 from = _body.transform.position + _body.transform.up * 1.1f;
                Vector3 outward = part.transform.position - from;
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Random.onUnitSphere;
                _body.DamagePart(step.Part, DeathBurst.Overkill, (_blow + outward).normalized);
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;
            Vector3 pull;
            Vector3 jump = Vector3.zero;
            if (_ship != null && _ship.Body != null)
            {
                Rigidbody hull = _ship.Body;
                Vector3 v = hull.linearVelocity;
                Vector3 change = _hasShipVelocity ? v - _shipVelocity : Vector3.zero;
                _shipVelocity = v;
                _hasShipVelocity = true;
                Vector3 shipAcceleration = Vector3.zero;
                if (change.magnitude > DeathBurst.FrameJump) jump = change;
                else shipAcceleration = Vector3.ClampMagnitude(change / dt, DeathBurst.MostShipAcceleration);
                pull = shipAcceleration - hull.rotation * Vector3.up * DeathBurst.CabinPull;

                if (_cage != null)
                {
                    // Moved, not placed, so its walls push the pieces as the ship turns; placed only after a jump
                    // (the world re-centred), which would otherwise fling everything.
                    // Aimed at where the ship will be at the end of the step, or the cage trails the cabin by its
                    // speed times the step (0.2 m at 10 m/s) and the pieces pass through its walls.
                    Ahead(hull, dt, out Vector3 at, out Quaternion turn);
                    // Placed, not moved, only when the cage has fallen far from the ship (the world re-centred).
                    if ((_cage.position - hull.position).sqrMagnitude > 25f)
                    {
                        _cage.position = at;
                        _cage.rotation = turn;
                    }
                    else
                    {
                        _cage.MovePosition(at);
                        _cage.MoveRotation(turn);
                    }
                }
            }
            else
            {
                pull = _pull != null ? _pull() : Physics.gravity;
            }

            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                Gib gib = _pieces[i];
                // Cleaned up, or handed to someone else by the pool: it is not ours to pull any more.
                if (gib == null || !gib.InUse || gib.Owner != _body)
                {
                    if (gib != null && gib.Body != null) gib.Body.useGravity = true;
                    _pieces.RemoveAt(i);
                    continue;
                }
                if (jump != Vector3.zero) gib.Body.linearVelocity += jump;
                gib.Body.AddForce(pull, ForceMode.Acceleration);
            }
        }

        void LateUpdate()
        {
            if (_eye == null || _head == null) return;
            _eye.transform.SetPositionAndRotation(_head.TransformPoint(_eyeOffset), _head.rotation * _eyeTurn);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                Gib gib = _pieces[i];
                if (gib != null && gib.Body != null) gib.Body.useGravity = true;
            }
            _pieces.Clear();
            if (_body != null) _body.PartDetached -= OnPartDetached;
            if (_cage != null) Destroy(_cage.gameObject);
            if (_caged == this) _caged = null;
        }
    }
}
