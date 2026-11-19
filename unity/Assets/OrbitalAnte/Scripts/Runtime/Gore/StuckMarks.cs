using UnityEngine;

namespace OrbitalAnte.Gore
{
    /// <summary>
    /// Keeps pooled marks (blood splats, bullet holes) on whatever they were made on. In the solar system almost
    /// everything solid moves in Unity's space: the ship flies off, a world is slid back to Unity's zero point when
    /// the player wanders far. A mark made on the ship's hull or on the ground follows the rigidbody it was made on
    /// (the ship, or the world's body, which carries its ground, rocks and bases), so it never hangs in the air.
    ///
    /// Followed in LateUpdate, after the frame's movement, so it never trails behind what it is on. A mark whose
    /// rigidbody has gone (a part torn off and cleared away) goes with it.
    /// </summary>
    public sealed class StuckMarks
    {
        readonly Transform[] _on;
        readonly Vector3[] _position;
        readonly Quaternion[] _rotation;

        public StuckMarks(int count)
        {
            _on = new Transform[count];
            _position = new Vector3[count];
            _rotation = new Quaternion[count];
        }

        /// <summary>What a mark made on this collider should stay on: its rigidbody, if it has one.</summary>
        public static Transform On(Collider hit)
        {
            Rigidbody body = hit != null ? hit.attachedRigidbody : null;
            return body != null ? body.transform : null;
        }

        /// <summary>Call once the slot has been put in place: remembers where it is on <paramref name="on"/>.</summary>
        public void Stick(int index, Transform slot, Transform on)
        {
            _on[index] = on;
            if (on == null || slot == null) return;
            _position[index] = on.InverseTransformPoint(slot.position);
            _rotation[index] = Quaternion.Inverse(on.rotation) * slot.rotation;
        }

        /// <summary>Puts the slot back where it belongs on what it is stuck to. False if that has gone.</summary>
        public bool Follow(int index, Transform slot)
        {
            Transform on = _on[index];
            if (ReferenceEquals(on, null)) return true;
            if (on == null)
            {
                _on[index] = null;
                return false;
            }
            slot.SetPositionAndRotation(on.TransformPoint(_position[index]), on.rotation * _rotation[index]);
            return true;
        }

        public void Forget(int index) => _on[index] = null;
    }
}
