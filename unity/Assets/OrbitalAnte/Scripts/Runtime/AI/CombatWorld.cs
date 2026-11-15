using System;
using OrbitalAnte.Player;
using OrbitalAnte.Core;
using OrbitalAnte.Ship;
using UnityEngine;

namespace OrbitalAnte.AI
{
    /// <summary>
    /// What enemies need to know about the world they are fighting in: where the player is, where dropped
    /// things should live, and how to find cover. The arena fills this in when it is built.
    /// </summary>
    public static class CombatWorld
    {
        /// <summary>The player's camera. Null when there is no player (menus, some tests).</summary>
        public static Transform PlayerEye;
        public static PlayerHealth Player;
        static Astronaut _astronaut;
        static OrbitalAnte.Ship.Ship _occupiedShip;
        static PlayerHealth _colliderOwner;
        static Collider _playerCollider;
        public static OrbitalAnte.Ship.Ship OccupiedShip => _occupiedShip;
        public static int TargetRevision { get; private set; }
        /// <summary>Parent for weapons lying in the world, so resetting the arena can clear them.</summary>
        public static Transform DropParent;
        /// <summary>When true, enemies keep their guns but do not shoot. A playtest switch.</summary>
        public static bool EnemiesHoldFire;
        /// <summary>Given where a character is and where the threat is, returns a spot out of sight, or null.</summary>
        public static Func<Vector3, Vector3, Vector3?> FindCover;

        /// <summary>True when there is a living player for enemies to react to.</summary>
        public static bool PlayerPresent => PlayerEye != null && (Player == null || Player.Alive);

        /// <summary>Choose the actual living player or their occupied vessel; boarding is not invisibility.</summary>
        public static void SetPlayerTarget(Astronaut crew)
        {
            bool alive = crew != null && !crew.Dead && crew.Eye != null && (crew.Armour == null || crew.Armour.Alive);
            Transform eye = alive ? crew.Eye.transform : null;
            OrbitalAnte.Ship.Ship ship = alive && crew.Mode != AstronautMode.Outside ? crew.Ship : null;
            Astronaut targetCrew = alive ? crew : null;
            if (PlayerEye != eye || _occupiedShip != ship || _astronaut != targetCrew)
                TargetRevision = unchecked(TargetRevision + 1);
            _astronaut = targetCrew;
            PlayerEye = eye;
            Player = alive ? crew.Armour : null;
            _occupiedShip = ship;
        }

        public static Vector3 AimPointFrom(Vector3 observer, Vector3 up)
        {
            if (_occupiedShip != null && !InsideOccupiedCabin(observer))
                return _occupiedShip.transform.TransformPoint(new Vector3(0f, ShipLayout.WallTop * 0.5f, 0f));
            return PlayerEye != null ? PlayerEye.position - up * 0.3f : observer;
        }

        public static bool InsideOccupiedCabin(Vector3 point)
        {
            if (_occupiedShip == null) return false;
            Vector3 p = _occupiedShip.transform.InverseTransformPoint(point);
            return ShipLayout.InsideCabin(new P3(p.x, p.y, p.z));
        }

        /// <summary>The target vessel's first hull hit is visible; terrain and other vessels still block sight.</summary>
        public static bool VisibleFrom(Vector3 origin, Vector3 target)
        {
            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            if (distance <= 0.0001f) return true;
            if (!Physics.Raycast(origin, delta / distance, out RaycastHit hit, distance,
                    Layers.WorldMask, QueryTriggerInteraction.Ignore)) return true;
            return _occupiedShip != null && !InsideOccupiedCabin(origin)
                && hit.collider.GetComponentInParent<OrbitalAnte.Ship.Ship>() == _occupiedShip;
        }

        /// <summary>
        /// The suit physics object is disabled aboard. These logical volumes keep cabin occupants hittable
        /// without enabling exterior physics or creating another player body. Compare against world occlusion.
        /// </summary>
        public static bool RaycastPlayer(Vector3 origin, Vector3 direction, float range,
            out float distance, out Vector3 point, out Vector3 normal)
        {
            distance = 0f; point = origin; normal = -direction.normalized;
            if (!PlayerPresent || Player == null || direction.sqrMagnitude < 0.000001f || range < 0f) return false;
            if (_colliderOwner != Player || _playerCollider == null)
            {
                _colliderOwner = Player;
                _playerCollider = Player.GetComponent<Collider>();
            }
            Vector3 a, b, up;
            float radius;
            bool body = true;
            if (_astronaut != null && _astronaut.Mode != AstronautMode.Outside)
            {
                up = _astronaut.Ship != null ? _astronaut.Ship.transform.up : _astronaut.transform.up;
                radius = ShipLayout.PersonRadius;
                float height = _astronaut.StanceHeight;
                Vector3 feet = _astronaut.transform.position;
                if (_astronaut.Mode == AstronautMode.Seated && _astronaut.Ship != null)
                {
                    P3 seat = ShipLayout.SeatPosition;
                    feet = _astronaut.Ship.transform.TransformPoint(new Vector3((float)seat.X, (float)seat.Y, (float)seat.Z));
                    height = (float)ShipLayout.SeatedEye.Y + ShipLayout.PersonHeight - ShipLayout.EyeHeight;
                }
                a = feet + up * radius;
                b = feet + up * Mathf.Max(radius, height - radius);
            }
            else if (_playerCollider is CapsuleCollider capsule)
            {
                Transform t = capsule.transform;
                Vector3 scale = t.lossyScale;
                radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                up = t.up;
                Vector3 center = t.TransformPoint(capsule.center);
                float half = Mathf.Max(0f, capsule.height * Mathf.Abs(scale.y) * 0.5f - radius);
                a = center - up * half; b = center + up * half;
            }
            else if (_playerCollider is CharacterController controller)
            {
                Transform t = controller.transform;
                Vector3 scale = t.lossyScale;
                radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                up = t.up;
                Vector3 center = t.TransformPoint(controller.center);
                float half = Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * 0.5f - radius);
                a = center - up * half; b = center + up * half;
            }
            else
            {
                up = Player.transform.up; radius = ShipLayout.PersonRadius;
                a = b = PlayerEye.position; body = false;
            }
            Vec3d o = V(origin), d = V(direction);
            double closest = double.PositiveInfinity;
            if (body && RayHitVolume.Capsule(o, d, range, V(a), V(b), radius, out double along)) closest = along;
            // Lean exposes the head at the eye pose, rather than granting a camera that cannot be shot.
            Vector3 head = PlayerEye.position - up * (radius * 0.5f);
            if (RayHitVolume.Sphere(o, d, range, V(head), radius, out double headAlong)) closest = Math.Min(closest, headAlong);
            if (double.IsInfinity(closest)) return false;
            distance = (float)closest;
            point = origin + direction.normalized * distance;
            normal = -direction.normalized;
            return true;
        }

        static Vec3d V(Vector3 p) => new Vec3d(p.x, p.y, p.z);

        public static void Reset()
        {
            PlayerEye = null;
            Player = null;
            _astronaut = null;
            _occupiedShip = null;
            _colliderOwner = null;
            _playerCollider = null;
            TargetRevision = unchecked(TargetRevision + 1);
            DropParent = null;
            EnemiesHoldFire = false;
            FindCover = null;
        }
    }
}
