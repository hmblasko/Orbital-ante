using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Weapons
{
    public enum WeaponHold
    {
        /// <summary>Lying in the world as a physics object. Anyone with a hand can pick it up.</summary>
        Dropped,
        HeldByPlayer,
        /// <summary>In an enemy's hands. Shooting it knocks it away.</summary>
        HeldByCharacter,
    }

    /// <summary>Something that can have a gun shot out of its hands.</summary>
    public interface IWeaponHolder
    {
        void WeaponShotOutOfHands(Weapon weapon, in HitInfo hit);
    }

    /// <summary>
    /// One gun in the world. The same object is used whether it is in the player's hands, in an enemy's hands or
    /// lying on the floor, and it keeps its magazine count as it changes hands.
    /// </summary>
    public sealed class Weapon : MonoBehaviour, IHittable
    {
        static readonly List<Weapon> Registry = new List<Weapon>();
        /// <summary>Every weapon that currently exists.</summary>
        public static IReadOnlyList<Weapon> All => Registry;

        public static void ResetStatics() => Registry.Clear();

        public WeaponData Data { get; private set; }
        public FirearmState State { get; private set; }
        public WeaponHold Hold { get; private set; } = WeaponHold.Dropped;
        public IWeaponHolder Holder { get; private set; }
        public Rigidbody Body { get; private set; }

        public Transform Grip => _sockets.Grip;
        public Transform SupportHand => _sockets.SupportHand;
        public Transform Muzzle => _sockets.Muzzle;
        public Transform Magazine => _sockets.Magazine;

        /// <summary>Unscaled time this weapon last left someone's hands. Stops it being grabbed back instantly.</summary>
        public float DroppedAt { get; private set; } = -100f;

        WeaponSockets _sockets;
        BoxCollider _box;
        Renderer[] _renderers;

        public static Weapon Create(WeaponData data, Transform parent, bool oneHandCanReload, string name = null)
        {
            var go = new GameObject(name ?? data.displayName);
            go.layer = Layers.Item;
            go.transform.SetParent(parent, false);
            var weapon = go.AddComponent<Weapon>();
            weapon.Initialize(data, oneHandCanReload);
            return weapon;
        }

        void Initialize(WeaponData data, bool oneHandCanReload)
        {
            Data = data;
            State = new FirearmState(data.ToSpec(oneHandCanReload));
            _sockets = WeaponModelFactory.Build(data, transform);
            _renderers = GetComponentsInChildren<Renderer>(true);

            _box = gameObject.AddComponent<BoxCollider>();
            Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = Mathf.Max(0.2f, data.mass);
            Body.angularDamping = 0.4f;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            ApplyHold(WeaponHold.Dropped, null);
        }

        void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        void OnDisable()
        {
            Registry.Remove(this);
        }

        // ------------------------------------------------------------------ changing hands

        /// <summary>Puts the weapon in the player's hands. The caller parents and positions it.</summary>
        public void TakeByPlayer()
        {
            ApplyHold(WeaponHold.HeldByPlayer, null);
        }

        /// <summary>Puts the weapon in a character's hands. The caller parents and positions it.</summary>
        public void TakeByCharacter(IWeaponHolder holder)
        {
            ApplyHold(WeaponHold.HeldByCharacter, holder);
        }

        /// <summary>Lets go of the weapon. It becomes a physics object moving at the given velocity.</summary>
        public void Drop(Transform newParent, Vector3 velocity, Vector3 angularVelocity)
        {
            State.CancelReload();
            SetMagazineVisible(true);
            transform.SetParent(newParent, true);
            transform.localScale = Vector3.one;
            ApplyHold(WeaponHold.Dropped, null);
            DroppedAt = Time.unscaledTime;
            Body.linearVelocity = velocity;
            Body.angularVelocity = angularVelocity;
        }

        void ApplyHold(WeaponHold hold, IWeaponHolder holder)
        {
            Hold = hold;
            Holder = holder;
            Bounds b = _sockets.LocalBounds;

            switch (hold)
            {
                case WeaponHold.Dropped:
                    Layers.SetLayerRecursively(gameObject, Layers.Item);
                    _box.enabled = true;
                    _box.center = b.center;
                    _box.size = Vector3.Max(b.size, new Vector3(0.04f, 0.04f, 0.04f));
                    Body.isKinematic = false;
                    Body.useGravity = true;
                    Body.interpolation = RigidbodyInterpolation.Interpolate;
                    SetShadows(true);
                    break;

                case WeaponHold.HeldByCharacter:
                    // Only bullets see it while it is held, and the box is a little generous so that shooting a
                    // gun out of someone's hands is a fair shot rather than a pixel hunt.
                    Layers.SetLayerRecursively(gameObject, Layers.Hitbox);
                    _box.enabled = true;
                    _box.center = b.center;
                    _box.size = b.size * 1.15f + new Vector3(0.05f, 0.05f, 0.05f);
                    Body.isKinematic = true;
                    Body.interpolation = RigidbodyInterpolation.None;
                    SetShadows(true);
                    break;

                case WeaponHold.HeldByPlayer:
                    Layers.SetLayerRecursively(gameObject, Layers.Player);
                    _box.enabled = false;
                    Body.isKinematic = true;
                    Body.interpolation = RigidbodyInterpolation.None;
                    // A gun floating in front of the camera should not cast a shadow on the floor.
                    SetShadows(false);
                    break;
            }
        }

        void SetShadows(bool on)
        {
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].shadowCastingMode = mode;
            }
        }

        /// <summary>Shows or hides the magazine (used by the reload animation).</summary>
        public void SetMagazineVisible(bool visible)
        {
            if (_sockets.MagazineMesh != null && _sockets.MagazineMesh.activeSelf != visible)
            {
                _sockets.MagazineMesh.SetActive(visible);
            }
        }

        // ------------------------------------------------------------------ being shot

        public HitOutcome Hit(in HitInfo hit)
        {
            if (Hold == WeaponHold.HeldByCharacter && Holder != null)
            {
                Holder.WeaponShotOutOfHands(this, hit);
                return new HitOutcome
                {
                    Hit = true,
                    Disarmed = true,
                    Juice = 0.35f,
                    Label = Data.displayName + " (knocked away)",
                };
            }

            if (Hold == WeaponHold.Dropped && Body != null)
            {
                // A loose gun skitters when shot.
                Body.AddForceAtPosition(hit.Direction * 4f, hit.Point, ForceMode.VelocityChange);
            }
            return new HitOutcome { Hit = true, Label = Data.displayName };
        }

        public string DescribeForDebug()
        {
            string where = Hold == WeaponHold.Dropped ? " (on the ground)" : " (held: shoot it to disarm)";
            return Data.displayName + where + "  " + State.Ammo + " / " + State.Spec.MagazineSize;
        }
    }
}
