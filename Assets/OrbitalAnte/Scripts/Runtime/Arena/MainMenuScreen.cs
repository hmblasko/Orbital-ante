using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Arena
{
    /// <summary>Bare main menu so "Quit to main menu" has somewhere to land. The real menu comes later.</summary>
    public class MainMenuScreen : MonoBehaviour
    {
        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.11f);
            camGo.AddComponent<AudioListener>();

            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas("MainMenu", 0);
            var card = UIFactory.Card("Menu", canvas.transform, new Vector2(520, 430));
            var title = UIFactory.Label(card, "ORBITAL ANTE", 54, UITheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.localRotation = Quaternion.Euler(0, 0, -4f);
            UIFactory.Layout(title.gameObject, 80);
            var sub = UIFactory.Label(card, "working title  ·  combat test build", UITheme.SmallSize, UITheme.TextDim, TextAnchor.MiddleCenter);
            UIFactory.Layout(sub.gameObject, 26);
            var play = UIFactory.Button(card, "Play test arena (new seed)", () => SceneFlow.StartRun(newSeed: true), 56);
            UIFactory.Button(card, "Quit to desktop", SceneFlow.QuitToDesktop, 56);
            UIFactory.Select(play.gameObject);
        }
    }
}
