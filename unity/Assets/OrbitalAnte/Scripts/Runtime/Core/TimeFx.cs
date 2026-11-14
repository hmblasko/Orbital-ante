using OrbitalAnte.Gore;
using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Times the hitstop (a few hundredths of a second of near-freeze when the player pops a limb) and the
    /// optional brief slow-mo on a headshot kill. It hands both to <see cref="TimeFlow"/>, which is the only
    /// place the clock is set; pausing always wins there.
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
            TimeFlow.Hitstop = 1f;
            TimeFlow.Headshot = 1f;
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
            // (While paused these are still handed in; TimeFlow keeps time at zero until the menu closes.)
            TimeFlow.Hitstop = InHitstop ? Mathf.Clamp(_cfg.hitstopTimeScale, 0.001f, 1f) : 1f;
            TimeFlow.Headshot = InSlowMo ? Mathf.Clamp(_cfg.headshotSlowMoScale, 0.05f, 1f) : 1f;
        }

        /// <summary>Cancels any freeze or slow-mo in progress (used when a run restarts).</summary>
        public void Clear()
        {
            _hitstopUntil = 0f;
            _slowMoUntil = 0f;
            TimeFlow.Hitstop = 1f;
            TimeFlow.Headshot = 1f;
        }
    }
}
