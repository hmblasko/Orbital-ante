using System;
using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// The bare minimum of player health, so that enemies shooting back can be tested: a number that goes down
    /// when hit, comes back after a few quiet seconds, and knocks the player down at zero. The real suit,
    /// oxygen and damage model replace this in later milestones.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        PlayerConfig _config;
        float _lastHitTime = -100f;

        public float Max { get; private set; } = 100f;
        public float Current { get; private set; } = 100f;
        public bool Alive => Current > 0f;
        /// <summary>0 to 1.</summary>
        public float Fraction => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
        /// <summary>Unscaled time of the last hit, for the HUD flash.</summary>
        public float LastHitUnscaledTime { get; private set; } = -100f;

        /// <summary>Raised on every hit, with the damage taken and the direction the bullet was travelling.</summary>
        public event Action<float, Vector3> Damaged;
        public event Action Died;

        public void Initialize(PlayerConfig config)
        {
            _config = config;
            Max = Mathf.Max(1f, config.maxHealth);
            Current = Max;
        }

        public void TakeDamage(float amount, Vector3 bulletDirection)
        {
            if (!Alive || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            _lastHitTime = Time.time;
            LastHitUnscaledTime = Time.unscaledTime;
            Damaged?.Invoke(amount, bulletDirection);
            if (Current <= 0f) Died?.Invoke();
        }

        public void Revive()
        {
            Current = Max;
            _lastHitTime = -100f;
        }

        void Update()
        {
            if (_config == null || !Alive || Current >= Max) return;
            if (Time.time - _lastHitTime < _config.regenDelay) return;
            Current = Mathf.Min(Max, Current + _config.regenPerSecond * Time.deltaTime);
        }
    }
}
