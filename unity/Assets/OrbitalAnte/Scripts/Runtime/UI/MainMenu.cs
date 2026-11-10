using System;
using OrbitalAnte.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Minimal title screen so "Quit to main menu" has somewhere to go.
    /// (The real main menu, with the notebook, comes much later.)
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        public Action PlayRequested;
        public Action QuitRequested;

        Canvas _canvas;
        GameObject _playButton;

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("MainMenu", uiRoot, 50);

            var card = UiKit.Card(_canvas.transform, "TitleCard", 620f);
            var title = UiKit.LayoutLabel(card, "ORBITAL ANTE", 64, UiKit.PanelEdge, 84f);
            title.transform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            UiKit.LayoutLabel(card, "working title", 18, UiKit.Muted, 22f, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiKit.LayoutLabel(card, GameSession.BuildLabel, 20, UiKit.Cream, 40f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _playButton = UiKit.Button(card, "Enter the test arena", () => PlayRequested?.Invoke(), 64f, 28).gameObject;
            UiKit.Button(card, "Quit to desktop", () => QuitRequested?.Invoke());
            UiKit.LayoutLabel(card, "Esc or Start pauses. Controls can be changed from the pause menu.", 16, UiKit.Muted, 26f,
                TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        public void SetVisible(bool visible)
        {
            _canvas.gameObject.SetActive(visible);
            if (visible && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_playButton);
            }
        }

        void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf || GameInput.Asset == null) return;
            EventSystem es = EventSystem.current;
            if (es == null) return;
            GameObject selected = es.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) return;
            bool navigating = GameInput.UiNavigate.ReadValue<Vector2>().sqrMagnitude > 0.25f
                              || GameInput.UiSubmit.WasPressedThisFrame();
            if (navigating) es.SetSelectedGameObject(_playButton);
        }
    }
}
