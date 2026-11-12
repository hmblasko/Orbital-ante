using OrbitalAnte.Core;
using OrbitalAnte.Gore;
using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// Small rotational camera shake when the player pops a limb. First person only: it nudges the view, it never
    /// moves the camera out of the head. Can be switched off in Pause > Feel.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        GoreConfig _cfg;
        float _trauma;

        /// <summary>Extra rotation to apply on top of where the player is looking.</summary>
        public Quaternion Offset { get; private set; } = Quaternion.identity;

        void Awake()
        {
            _cfg = GoreConfig.Load();
        }

        void OnEnable()
        {
            CombatEvents.PlayerHitSomething += OnPlayerHit;
        }

        void OnDisable()
        {
            CombatEvents.PlayerHitSomething -= OnPlayerHit;
        }

        void OnPlayerHit(HitOutcome outcome)
        {
            if (outcome.Juice <= 0f || !FeelSettings.CameraShake) return;
            float add = outcome.Juice >= 0.9f ? _cfg.shakeBig : _cfg.shakeLimb;
            _trauma = Mathf.Clamp01(_trauma + add);
        }

        void Update()
        {
            if (_trauma <= 0f)
            {
                Offset = Quaternion.identity;
                return;
            }

            // Unscaled time, so the shake still plays through hitstop.
            _trauma = Mathf.Max(0f, _trauma - _cfg.shakeDecay * Time.unscaledDeltaTime);
            float strength = _trauma * _trauma * _cfg.shakeMaxDegrees;
            float t = Time.unscaledTime * 26f;
            float pitch = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f * strength;
            float yaw = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f * strength;
            float roll = (Mathf.PerlinNoise(t, t + 5.3f) - 0.5f) * 2f * strength * 0.6f;
            Offset = Quaternion.Euler(pitch, yaw, roll);
        }
    }
}
