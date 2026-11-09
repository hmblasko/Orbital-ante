using System.Collections.Generic;
using OrbitalAnte.Characters;
using OrbitalAnte.Combat;
using OrbitalAnte.Input;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    public enum NoticeKind { Info, Combat, System }

    /// <summary>Crosshair, hit marker, playtest hints and the capped top-right notifications.</summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        const int MaxNotices = 3;
        const float NoticeSeconds = 3.5f;

        RectTransform hitMarker;
        Image[] hitMarkerBars;
        float hitMarkerTimer;
        Text hintText;
        Text stateText;
        RectTransform noticeColumn;
        readonly List<(GameObject go, float until)> notices = new List<(GameObject, float)>();
        float hintRefresh;
        Text ammoText;
        Text weaponText;
        Text promptText;
        Image[] damageEdges;   // top, right, bottom, left
        float[] damageAlpha = new float[4];
        const float DamageFlashSeconds = 0.45f;

        public static Hud Create()
        {
            var canvas = UIFactory.CreateCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<Hud>();
            hud.Build(canvas.transform);
            return hud;
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build(Transform root)
        {
            // Crosshair: a small dot with four ticks.
            var cross = UIFactory.Rect("Crosshair", root);
            cross.sizeDelta = new Vector2(40, 40);
            var dot = UIFactory.Image("Dot", cross, new Color(1, 1, 1, 0.9f));
            dot.rectTransform.sizeDelta = new Vector2(4, 4);
            for (int i = 0; i < 4; i++)
            {
                var tick = UIFactory.Image("Tick", cross, new Color(1, 1, 1, 0.75f));
                bool vertical = i < 2;
                tick.rectTransform.sizeDelta = vertical ? new Vector2(2, 8) : new Vector2(8, 2);
                float o = 11f * (i % 2 == 0 ? 1 : -1);
                tick.rectTransform.anchoredPosition = vertical ? new Vector2(0, o) : new Vector2(o, 0);
            }

            // Hit marker: four diagonal bars, hidden until a hit lands.
            hitMarker = UIFactory.Rect("HitMarker", root);
            hitMarker.sizeDelta = new Vector2(44, 44);
            hitMarkerBars = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var bar = UIFactory.Image("Bar", hitMarker, Color.white);
                bar.rectTransform.sizeDelta = new Vector2(3, 12);
                float angle = 45f + 90f * i;
                Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.up;
                bar.rectTransform.anchoredPosition = dir * 14f;
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                hitMarkerBars[i] = bar;
            }
            hitMarker.gameObject.SetActive(false);

            // Bottom-left playtest hints.
            hintText = UIFactory.Label(root, "", UITheme.SmallSize, UITheme.Text, TextAnchor.LowerLeft);
            var hr = hintText.rectTransform;
            hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0, 0);
            hr.anchoredPosition = new Vector2(18, 16);
            hr.sizeDelta = new Vector2(680, 140);

            stateText = UIFactory.Label(root, "", UITheme.SmallSize, UITheme.Accent, TextAnchor.LowerRight, FontStyle.Bold);
            var sr = stateText.rectTransform;
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(1, 0);
            sr.anchoredPosition = new Vector2(-18, 104);
            sr.sizeDelta = new Vector2(420, 60);

            // Bottom-right ammo readout.
            ammoText = UIFactory.Label(root, "", UITheme.TitleSize, UITheme.Text, TextAnchor.LowerRight, FontStyle.Bold);
            var ar = ammoText.rectTransform;
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(1, 0);
            ar.anchoredPosition = new Vector2(-18, 40);
            ar.sizeDelta = new Vector2(360, 64);
            weaponText = UIFactory.Label(root, "", UITheme.SmallSize, UITheme.TextDim, TextAnchor.LowerRight);
            var wr = weaponText.rectTransform;
            wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(1, 0);
            wr.anchoredPosition = new Vector2(-18, 16);
            wr.sizeDelta = new Vector2(420, 28);

            // Interact prompt, just under the crosshair.
            promptText = UIFactory.Label(root, "", UITheme.BodySize, UITheme.Accent, TextAnchor.UpperCenter, FontStyle.Bold);
            var pr = promptText.rectTransform;
            pr.anchoredPosition = new Vector2(0, -70);
            pr.sizeDelta = new Vector2(600, 40);

            // Red screen-edge flashes that show which way incoming fire came from.
            damageEdges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var e = UIFactory.Image("DamageEdge", root, new Color(UITheme.Danger.r, UITheme.Danger.g, UITheme.Danger.b, 0f));
                var er = e.rectTransform;
                bool horizontal = i % 2 == 0;
                float side = i == 0 || i == 1 ? 1f : 0f;
                er.anchorMin = horizontal ? new Vector2(0, side) : new Vector2(side, 0);
                er.anchorMax = horizontal ? new Vector2(1, side) : new Vector2(side, 1);
                er.pivot = horizontal ? new Vector2(0.5f, side) : new Vector2(side, 0.5f);
                er.sizeDelta = horizontal ? new Vector2(0, 46) : new Vector2(46, 0);
                er.anchoredPosition = Vector2.zero;
                e.raycastTarget = false;
                damageEdges[i] = e;
            }

            // Top-right notifications.
            noticeColumn = UIFactory.Rect("Notices", root);
            noticeColumn.anchorMin = noticeColumn.anchorMax = noticeColumn.pivot = new Vector2(1, 1);
            noticeColumn.anchoredPosition = new Vector2(-18, -18);
            noticeColumn.sizeDelta = new Vector2(380, 300);
            var v = noticeColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childAlignment = TextAnchor.UpperRight;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
        }

        public void ShowHitMarker(bool big)
        {
            hitMarkerTimer = big ? 0.2f : 0.11f;
            var c = big ? UITheme.Danger : Color.white;
            foreach (var b in hitMarkerBars) b.color = c;
            hitMarker.localScale = Vector3.one * (big ? 1.35f : 1f);
            hitMarker.gameObject.SetActive(true);
        }

        public void Notify(string message, NoticeKind kind = NoticeKind.Info)
        {
            Color edge = kind == NoticeKind.Combat ? UITheme.Danger : kind == NoticeKind.System ? UITheme.Teal : UITheme.Accent;
            var box = UIFactory.Image("Notice", noticeColumn, edge);
            UIFactory.Layout(box.gameObject, 48);
            var inner = UIFactory.Image("Inner", box.transform, UITheme.Card);
            UIFactory.Stretch(inner.rectTransform, 3f);
            var t = UIFactory.Label(inner.transform, message, UITheme.BodySize, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(t.rectTransform, 6f);
            box.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(-1.5f, 1.5f));
            notices.Add((box.gameObject, Time.unscaledTime + NoticeSeconds));
            while (notices.Count > MaxNotices)
            {
                Destroy(notices[0].go);
                notices.RemoveAt(0);
            }
        }

        public void SetState(string text) => stateText.text = text;

        /// <summary>
        /// Flash the screen edge facing the shooter. <paramref name="shotDirection"/> is the
        /// direction the bullet travelled (shooter to player).
        /// </summary>
        public void ShowDamageFlash(Vector3 shotDirection)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 toShooter = cam.transform.InverseTransformDirection(-shotDirection);
            float right = toShooter.x, fwd = toShooter.z;
            // Shots from in front light the top edge; behind lights the bottom.
            float[] w = { Mathf.Max(0, fwd), Mathf.Max(0, right), Mathf.Max(0, -fwd), Mathf.Max(0, -right) };
            float max = Mathf.Max(Mathf.Max(w[0], w[1]), Mathf.Max(w[2], w[3]));
            for (int i = 0; i < 4; i++)
                damageAlpha[i] = Mathf.Max(damageAlpha[i], max > 0 ? 0.55f * w[i] / max : 0.3f);
        }

        void UpdateWeaponReadout()
        {
            var pw = PlayerWeapons.Local;
            var gun = pw != null ? pw.Current : null;
            if (gun == null)
            {
                ammoText.text = "";
                weaponText.text = "";
            }
            else
            {
                // Ammo reserve is unlimited in the test arena.
                ammoText.text = gun.Reloading ? "reloading" : $"{gun.Ammo} <size={UITheme.BodySize}>/ inf</size>";
                ammoText.color = gun.Reloading ? UITheme.Accent : gun.Ammo == 0 ? UITheme.Danger : UITheme.Text;
                weaponText.text = gun.Def.displayName + (pw.OneHandedTest ? "  (one hand)" : "");
            }
            var looked = pw != null ? pw.LookedAtWeapon : null;
            promptText.text = looked != null
                ? $"[{InputGlyphs.HintFor(GameInput.Interact)}] pick up {looked.Def.displayName}"
                : "";
        }

        void Update()
        {
            if (hitMarkerTimer > 0f)
            {
                hitMarkerTimer -= Time.unscaledDeltaTime;
                if (hitMarkerTimer <= 0f) hitMarker.gameObject.SetActive(false);
            }

            for (int i = 0; i < 4; i++)
            {
                damageAlpha[i] = Mathf.MoveTowards(damageAlpha[i], 0f, Time.unscaledDeltaTime / DamageFlashSeconds);
                var c = damageEdges[i].color;
                c.a = damageAlpha[i];
                damageEdges[i].color = c;
            }
            UpdateWeaponReadout();

            for (int i = notices.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime > notices[i].until)
                {
                    Destroy(notices[i].go);
                    notices.RemoveAt(i);
                }
            }

            hintRefresh -= Time.unscaledDeltaTime;
            if (hintRefresh <= 0f)
            {
                hintRefresh = 0.4f;
                hintText.text =
                    $"<b>TEST ARENA</b>  [{InputGlyphs.HintFor(GameInput.Fire)}] fire   [{InputGlyphs.HintFor(GameInput.Aim)}] aim   [{InputGlyphs.HintFor(GameInput.Reload)}] reload\n" +
                    $"[{InputGlyphs.HintFor(GameInput.DebugRespawn)}] respawn dummies   " +
                    $"[{InputGlyphs.HintFor(GameInput.DebugWalkMode)}] walk mode: <b>{MannequinBrain.GlobalMode}</b>\n" +
                    $"[{InputGlyphs.HintFor(GameInput.DebugOverlay)}] frame times   " +
                    $"[{InputGlyphs.HintFor(GameInput.DebugSlowMo)}] headshot slow-mo: <b>{(OrbitalAnte.Feel.GameFeel.HeadshotSlowMo ? "on" : "off")}</b>   " +
                    $"[{InputGlyphs.HintFor(GameInput.DebugOneHand)}] one-handed: <b>{(PlayerWeapons.Local != null && PlayerWeapons.Local.OneHandedTest ? "on" : "off")}</b>\n" +
                    $"[{InputGlyphs.HintFor(GameInput.Pause)}] pause / controls";
            }
        }
    }
}
