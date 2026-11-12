using OrbitalAnte.Gore;
using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Owns game speed while playing: hitstop (a few hundredths of a second of near-freeze when the player pops
    /// a limb) and the optional brief slow-mo on a headshot kill. Pausing always wins over both.
    /// </summary>
    public sealed class TimeFx : MonoBehaviour
    {
        GoreConfig _cfg;
        float _hitstopUntil;
        float _slowMoUntil;

        /// <summary>True while a hitstop freeze is in progress.</summary>
        public bool InHitstop => Time.unscaledTime < _hitstopUntil;
        public bool InSlowMo => !InHitstop && Time.unscaledTime < _slowMoUntil;

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
            if (!GameSession.Paused) Time.timeScale = 1f;
        }

        void OnPlayerHit(HitOutcome outcome)
        {
            if (outcome.Juice <= 0f) return;
            float now = Time.unscaledTime;
            float stop = 0f;

            if (FeelSettings.Hitstop)
            {
                stop = outcome.Killed ? _cfg.killHitstop : outcome.Juice >= 0.9f ? _cfg.torsoHitstop : _cfg.limbHitstop;
                // Overlapping pops extend the freeze to the longest one; they never add up.
                _hitstopUntil = Mathf.Max(_hitstopUntil, now + stop);
            }

            if (outcome.HeadshotKill && FeelSettings.HeadshotSlowMo)
            {
                _slowMoUntil = Mathf.Max(_slowMoUntil, now + stop + _cfg.headshotSlowMoDuration);
            }
        }

        void Update()
        {
            // While paused the pause menu holds time at zero.
            if (GameSession.Paused) return;

            float target = 1f;
            if (InHitstop) target = Mathf.Clamp(_cfg.hitstopTimeScale, 0.001f, 1f);
            else if (InSlowMo) target = Mathf.Clamp(_cfg.headshotSlowMoScale, 0.05f, 1f);

            if (!Mathf.Approximately(Time.timeScale, target)) Time.timeScale = target;
        }

        /// <summary>Cancels any freeze or slow-mo in progress (used when a run restarts).</summary>
        public void Clear()
        {
            _hitstopUntil = 0f;
            _slowMoUntil = 0f;
            if (!GameSession.Paused) Time.timeScale = 1f;
        }
    }
}
