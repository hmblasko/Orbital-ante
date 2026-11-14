using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks the controls page's real layout and selection at the Deck's reference size.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class ControlsPageTests
    {
        GameObject _root;
        Canvas _canvas;
        EventSystem _events;
        EventSystem _previousEvents;
        bool _previousEventsEnabled;
        ControlsPage _page;
        bool _wasFlying;
        bool _playWasEnabled;

        [SetUp]
        public void SetUp()
        {
            GameInput.Initialize();
            _wasFlying = GameInput.Play == GameInput.Flight;
            _playWasEnabled = GameInput.Play.enabled;
            GameInput.Play.Disable();

            _previousEvents = EventSystem.current;
            _previousEventsEnabled = _previousEvents != null && _previousEvents.enabled;
            if (_previousEventsEnabled) _previousEvents.enabled = false;

            _root = new GameObject("ControlsPageTestRoot");
            var eventObject = new GameObject("ControlsPageTestEvents", typeof(EventSystem));
            eventObject.transform.SetParent(_root.transform, false);
            _events = eventObject.GetComponent<EventSystem>();
            EventSystem.current = _events;

            _canvas = UiKit.CreateCanvas("ControlsPageTestCanvas", _root.transform, 0);
            // Keep the layout at exactly 1280 x 800 regardless of the Test Runner window size.
            _canvas.GetComponent<CanvasScaler>().enabled = false;
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.scaleFactor = 1f;
            ((RectTransform)_canvas.transform).sizeDelta = UiKit.ReferenceResolution;

            _page = new ControlsPage();
            _page.Build(_canvas.transform, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            _page?.Close();
            if (_root != null) Object.DestroyImmediate(_root);
            if (_previousEvents != null)
            {
                _previousEvents.enabled = _previousEventsEnabled;
                if (_previousEventsEnabled) EventSystem.current = _previousEvents;
            }
            GameInput.SetFlying(_wasFlying);
            if (_playWasEnabled) GameInput.Play.Enable();
            else GameInput.Play.Disable();
        }

        [UnityTest]
        public IEnumerator BothMapsAndBothDeviceTabsFitTheDeckAndContainEveryBinding()
        {
            for (int mode = 0; mode < 2; mode++)
            {
                GameInput.SetFlying(mode == 1);
                _page.Open();
                for (int device = 0; device < 2; device++)
                {
                    string group = device == 0 ? GameInput.KeyboardMouseGroup : GameInput.GamepadGroup;
                    ChooseTab(device == 1);
                    yield return Layout();

                    RectTransform canvas = (RectTransform)_canvas.transform;
                    AssertInside((RectTransform)_page.Root.transform.Find("ControlsCard"), canvas);
                    AssertInside((RectTransform)_page.Root.transform.Find("ControlsCard/Body/Tabs"), canvas);
                    AssertInside((RectTransform)_page.Root.transform.Find("ControlsCard/Body/Footer"), canvas);

                    ScrollRect scroll = _page.Root.GetComponentInChildren<ScrollRect>();
                    Assert.IsNotNull(scroll, "The bindings need a way to reach rows beyond the viewport.");
                    Assert.Greater(scroll.viewport.rect.height, 0f);
                    Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height,
                        "The current binding lists must keep their full height inside the viewport.");
                    var entries = GameInput.GetEntries(group);
                    Button[] rows = scroll.content.GetComponentsInChildren<Button>();
                    Assert.AreEqual(entries.Count, rows.Length, "Some bindings disappeared when changing tab or map.");
                    for (int i = 0; i < entries.Count; i++)
                        Assert.AreEqual("Row " + entries[i].Label, rows[i].transform.parent.name);
                }
                _page.Close();
            }
        }

        [UnityTest]
        public IEnumerator NavigationReachesEveryRebindableRowInBothDirectionsAndKeepsItVisible()
        {
            for (int mode = 0; mode < 2; mode++)
            {
                GameInput.SetFlying(mode == 1);
                _page.Open();
                for (int device = 0; device < 2; device++)
                {
                    ChooseTab(device == 1);
                    yield return Layout();
                    ScrollRect scroll = _page.Root.GetComponentInChildren<ScrollRect>();
                    Assert.IsNotNull(scroll);
                    string group = device == 0 ? GameInput.KeyboardMouseGroup : GameInput.GamepadGroup;
                    var entries = GameInput.GetEntries(group);
                    Selectable tab = _page.DefaultSelection.GetComponent<Selectable>();
                    Selectable selected = tab;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (!entries[i].Rebindable) continue;
                        selected = selected.FindSelectableOnDown();
                        Assert.IsNotNull(selected, "Navigation stopped before " + entries[i].Label + ".");
                        Assert.AreEqual("Row " + entries[i].Label, selected.transform.parent.name,
                            "Down skipped a binding or left the list early.");
                        _events.SetSelectedGameObject(selected.gameObject);
                        _page.Tick();
                        yield return Layout();
                        AssertInside((RectTransform)selected.transform, scroll.viewport);
                    }

                    Selectable reset = selected.FindSelectableOnDown();
                    Assert.IsNotNull(reset);
                    Assert.AreEqual("Button Reset to defaults", reset.name);
                    Selectable back = reset.FindSelectableOnRight();
                    Assert.IsNotNull(back, "The footer must also expose Back to the controller.");
                    Assert.AreEqual("Button Back", back.name);
                    Assert.AreSame(reset, back.FindSelectableOnLeft());
                    Assert.AreSame(selected, back.FindSelectableOnUp());
                    selected = reset;
                    for (int i = entries.Count - 1; i >= 0; i--)
                    {
                        if (!entries[i].Rebindable) continue;
                        selected = selected.FindSelectableOnUp();
                        Assert.IsNotNull(selected, "Navigation stopped before " + entries[i].Label + ".");
                        Assert.AreEqual("Row " + entries[i].Label, selected.transform.parent.name,
                            "Up skipped a binding or left the list early.");
                        _events.SetSelectedGameObject(selected.gameObject);
                        _page.Tick();
                        yield return Layout();
                        AssertInside((RectTransform)selected.transform, scroll.viewport);
                    }
                    Assert.AreSame(tab, selected.FindSelectableOnUp(), "Up from the first binding must reach its device tab.");
                }
                _page.Close();
            }
        }

        [UnityTest]
        public IEnumerator ScrollingWithTheMouseIsKeptUntilSelectionChangesAndChangingTabStartsAtTheTop()
        {
            GameInput.SetFlying(true);
            _page.Open();
            ChooseTab(false);
            yield return Layout();
            ScrollRect scroll = _page.Root.GetComponentInChildren<ScrollRect>();
            Assert.IsNotNull(scroll);
            Button[] rows = scroll.content.GetComponentsInChildren<Button>();
            Button first = FirstInteractable(rows);
            _events.SetSelectedGameObject(first.gameObject);
            _page.Tick();
            scroll.StopMovement();
            scroll.OnScroll(new PointerEventData(_events) { scrollDelta = new Vector2(0f, -1000f) });
            Vector2 afterScroll = scroll.content.anchoredPosition;

            _page.Tick();
            yield return Layout();
            Assert.AreEqual(afterScroll.y, scroll.content.anchoredPosition.y, 0.5f,
                "Keeping the same selection must not undo mouse scrolling.");
            Assert.IsFalse(IsInside((RectTransform)first.transform, scroll.viewport));

            ChooseTab(true);
            yield return Layout();
            scroll = _page.Root.GetComponentInChildren<ScrollRect>();
            rows = scroll.content.GetComponentsInChildren<Button>();
            AssertInside((RectTransform)FirstInteractable(rows).transform, scroll.viewport);
            Assert.AreEqual(1f, scroll.verticalNormalizedPosition, 0.001f);
            Assert.AreSame(_page.DefaultSelection, _events.currentSelectedGameObject);
        }

        void ChooseTab(bool gamepad)
        {
            string name = gamepad ? "Controller" : "Keyboard & Mouse";
            var tab = _page.Root.transform.Find("ControlsCard/Body/Tabs/Button " + name).GetComponent<Button>();
            tab.onClick.Invoke();
        }

        IEnumerator Layout()
        {
            // Destroyed rows from the previous tab leave the hierarchy at the end of this frame.
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_page.Root.transform);
            Canvas.ForceUpdateCanvases();
        }

        static Button FirstInteractable(Button[] rows)
        {
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].interactable) return rows[i];
            Assert.Fail("The controls page has no selectable binding rows.");
            return null;
        }

        static bool IsInside(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Rect bounds = parent.rect;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 local = parent.InverseTransformPoint(corners[i]);
                if (local.x < bounds.xMin - 0.5f || local.x > bounds.xMax + 0.5f
                    || local.y < bounds.yMin - 0.5f || local.y > bounds.yMax + 0.5f)
                    return false;
            }
            return true;
        }

        static void AssertInside(RectTransform child, RectTransform parent)
        {
            Assert.IsNotNull(child);
            Assert.IsTrue(IsInside(child, parent), child.name + " extends outside " + parent.name + ".");
        }
    }
}
