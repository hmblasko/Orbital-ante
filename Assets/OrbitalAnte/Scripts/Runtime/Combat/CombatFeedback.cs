using OrbitalAnte.Feel;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// What the shooter feels when a shot lands: hit marker, ping, hitstop and shake on limb pops,
    /// optional slow-mo on headshot kills. One place, so every weapon feels consistent.
    /// </summary>
    public static class CombatFeedback
    {
        static AudioSource uiSource;

        static AudioSource UiSource
        {
            get
            {
                if (uiSource == null)
                {
                    var go = new GameObject("CombatFeedbackAudio");
                    uiSource = go.AddComponent<AudioSource>();
                    uiSource.spatialBlend = 0f;
                    uiSource.playOnAwake = false;
                }
                return uiSource;
            }
        }

        public static void PlayerShotLanded(HitResult result)
        {
            if (result == HitResult.None) return;
            var cfg = GameFeelConfig.Get();
            bool big = result == HitResult.Severed || result == HitResult.Killed;
            Hud.Instance?.ShowHitMarker(big);
            UiSource.pitch = big ? 1.12f : 1f;
            UiSource.PlayOneShot(ProceduralAudio.HitPing, big ? 0.6f : 0.45f);

            switch (result)
            {
                case HitResult.Severed:
                    GameFeel.Hitstop(cfg.limbHitstopSeconds);
                    GameFeel.AddTrauma(cfg.limbPopTrauma);
                    break;
                case HitResult.GibSplit:
                    GameFeel.Hitstop(cfg.limbHitstopSeconds * 0.6f);
                    GameFeel.AddTrauma(cfg.limbPopTrauma * 0.5f);
                    break;
                case HitResult.Killed:
                    GameFeel.Hitstop(cfg.killHitstopSeconds);
                    GameFeel.AddTrauma(cfg.killTrauma);
                    if (GameFeel.HeadshotSlowMo) GameFeel.SlowMo(cfg.slowMoSeconds);
                    Hud.Instance?.Notify("HEADSHOT!", NoticeKind.Combat);
                    break;
            }
        }

        public static void PlayerFired(float recoilTrauma)
        {
            GameFeel.AddTrauma(recoilTrauma);
        }
    }
}
