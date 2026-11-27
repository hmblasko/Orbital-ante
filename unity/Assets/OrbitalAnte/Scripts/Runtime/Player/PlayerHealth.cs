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
            Max = Mathf.Max(1f, config.maxHealth * _maxScale);
            Current = Max;
        }

        float _maxScale = 1f, _regenScale = 1f;
        bool _noRegen;

        /// <summary>
        /// The run's build (perks, attributes, the chosen hazard; 20 Nov 2026): how much health there is and how it
        /// comes back. The share of health left is kept when the most changes.
        /// </summary>
        public void SetBuild(float maxScale, float regenScale, bool noRegen)
        {
            _regenScale = Mathf.Max(0f, regenScale);
            _noRegen = noRegen;
            maxScale = Mathf.Max(0.1f, maxScale);
            if (Mathf.Approximately(maxScale, _maxScale) || _config == null) { _maxScale = maxScale; return; }
            float share = Fraction;
            _maxScale = maxScale;
            Max = Mathf.Max(1f, _config.maxHealth * _maxScale);
            if (Alive) Current = Mathf.Max(1f, share * Max);
        }

        /// <summary>Gives health back (a perk's heal on a kill). Never past the most, never to the dead.</summary>
        public void Heal(float amount)
        {
            if (!Alive || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
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

        void Update() => Regenerate();

        /// <summary>
        /// Wears back toward full after a quiet spell. Called by Update, and by the owner while this object is switched
        /// off (the suit body is put away while the player drives the rover, but their armour is still being shot).
        /// </summary>
        public void Regenerate()
        {
            if (_config == null || !Alive || Current >= Max || _noRegen) return;
            if (Time.time - _lastHitTime < _config.regenDelay) return;
            Current = Mathf.Min(Max, Current + _config.regenPerSecond * _regenScale * Time.deltaTime);
        }
    }
}
