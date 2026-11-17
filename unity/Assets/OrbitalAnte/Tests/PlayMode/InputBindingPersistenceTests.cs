using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Drafts for the real Input System JSON round trip. Assets are rebuilt independently, as on a new launch;
    /// no test writes PlayerPrefs or overrides the live GameInput maps. These require the Unity Test Runner.
    /// </summary>
    public class InputBindingPersistenceTests
    {
        readonly List<InputActionAsset> _assets = new List<InputActionAsset>();

        [TearDown]
        public void TearDown()
        {
            foreach (InputActionAsset asset in _assets)
            {
                asset.Disable();
                UnityEngine.Object.DestroyImmediate(asset);
            }
            _assets.Clear();
        }

        InputActionAsset NewAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _assets.Add(asset);
            return asset;
        }

        InputActionAsset BuildControls(bool revised = false)
        {
            InputActionAsset asset = NewAsset();
            string[] maps = revised ? new[] { "Flight", "Gameplay" } : new[] { "Gameplay", "Flight" };
            foreach (string name in maps)
            {
                InputActionMap map = asset.AddActionMap(name);
                if (revised) map.AddAction("New action", InputActionType.Button).AddBinding("<Keyboard>/u");
                InputAction button = map.AddAction(ButtonName(name), InputActionType.Button);
                if (revised)
                {
                    button.AddBinding("<Keyboard>/k", groups: GameInput.KeyboardMouseGroup).WithName("Alternate");
                    button.AddBinding("<Gamepad>/buttonSouth", groups: GameInput.GamepadGroup);
                    button.AddBinding("<Keyboard>/p", groups: GameInput.KeyboardMouseGroup);
                }
                else
                {
                    button.AddBinding("<Keyboard>/space", groups: GameInput.KeyboardMouseGroup);
                    button.AddBinding("<Gamepad>/buttonSouth", groups: GameInput.GamepadGroup);
                }
                InputAction move = map.AddAction(MoveName(name), InputActionType.Value, expectedControlLayout: "Vector2");
                AddComposite(move, "WASD", revised);
                move.AddBinding("<Gamepad>/leftStick", groups: GameInput.GamepadGroup);
                InputBindingIdentity.Assign(map);
            }
            return asset;
        }

        static void AddComposite(InputAction action, string name, bool reversed = false)
        {
            int root = action.bindings.Count;
            var composite = action.AddCompositeBinding("2DVector");
            action.ChangeBinding(root).WithName(name);
            if (reversed)
            {
                composite.With("Right", "<Keyboard>/d", GameInput.KeyboardMouseGroup)
                    .With("Left", "<Keyboard>/a", GameInput.KeyboardMouseGroup)
                    .With("Down", "<Keyboard>/s", GameInput.KeyboardMouseGroup)
                    .With("Up", "<Keyboard>/w", GameInput.KeyboardMouseGroup);
            }
            else
            {
                composite.With("Up", "<Keyboard>/w", GameInput.KeyboardMouseGroup)
                    .With("Down", "<Keyboard>/s", GameInput.KeyboardMouseGroup)
                    .With("Left", "<Keyboard>/a", GameInput.KeyboardMouseGroup)
                    .With("Right", "<Keyboard>/d", GameInput.KeyboardMouseGroup);
            }
        }

        static string ButtonName(string map) => map == "Gameplay" ? "Fire" : "Autopilot";
        static string MoveName(string map) => map == "Gameplay" ? "Move" : "Thrust";

        static int Slot(InputAction action, string group, string name = null)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.groups == group && string.Equals(binding.name ?? string.Empty, name ?? string.Empty, StringComparison.Ordinal))
                    return i;
            }
            Assert.Fail("Missing slot " + action.actionMap.name + "/" + action.name + "/" + group + "/" + name);
            return -1;
        }

        static InputAction Action(InputActionAsset asset, string map, string action) => asset.FindActionMap(map, true).FindAction(action, true);

        [TestCase("Gameplay", GameInput.KeyboardMouseGroup, "<Keyboard>/j")]
        [TestCase("Gameplay", GameInput.GamepadGroup, "<Gamepad>/buttonNorth")]
        [TestCase("Flight", GameInput.KeyboardMouseGroup, "<Keyboard>/j")]
        [TestCase("Flight", GameInput.GamepadGroup, "<Gamepad>/buttonNorth")]
        public void ReconstructedAssetsRestoreBothMapsAndDevices(string map, string group, string path)
        {
            InputActionAsset first = BuildControls();
            InputAction before = Action(first, map, ButtonName(map));
            int index = Slot(before, group);
            before.ApplyBindingOverride(index, path);
            string saved = first.SaveBindingOverridesAsJson();

            InputActionAsset fresh = BuildControls();
            InputAction after = Action(fresh, map, ButtonName(map));
            Assert.AreNotEqual(before.id, after.id, "This is a fresh construction, not a clone retaining action IDs.");
            fresh.LoadBindingOverridesFromJson(saved);

            Assert.AreEqual(path, after.bindings[Slot(after, group)].overridePath);
            string otherGroup = group == GameInput.GamepadGroup ? GameInput.KeyboardMouseGroup : GameInput.GamepadGroup;
            Assert.IsNull(after.bindings[Slot(after, otherGroup)].overridePath, "The other device must keep its own control.");
            string otherMap = map == "Gameplay" ? "Flight" : "Gameplay";
            InputAction untouched = Action(fresh, otherMap, ButtonName(otherMap));
            Assert.IsNull(untouched.bindings[Slot(untouched, group)].overridePath, "Overrides must not cross map boundaries.");
        }

        [TestCase("Gameplay")]
        [TestCase("Flight")]
        public void CompositePartsRestoreAfterTheirOrderChanges(string map)
        {
            InputActionAsset first = BuildControls();
            InputAction before = Action(first, map, MoveName(map));
            before.ApplyBindingOverride(Slot(before, GameInput.KeyboardMouseGroup, "Up"), "<Keyboard>/i");

            InputActionAsset fresh = BuildControls(true);
            fresh.LoadBindingOverridesFromJson(first.SaveBindingOverridesAsJson());
            InputAction after = Action(fresh, map, MoveName(map));
            Assert.AreEqual("<Keyboard>/i", after.bindings[Slot(after, GameInput.KeyboardMouseGroup, "Up")].overridePath);
            Assert.IsNull(after.bindings[Slot(after, GameInput.KeyboardMouseGroup, "Right")].overridePath);
        }

        [TestCase("Gameplay")]
        [TestCase("Flight")]
        public void AnIntentionallyUnboundControlStaysUnboundAfterReconstruction(string map)
        {
            InputActionAsset first = BuildControls();
            InputAction before = Action(first, map, ButtonName(map));
            before.ApplyBindingOverride(Slot(before, GameInput.GamepadGroup), string.Empty);

            InputActionAsset fresh = BuildControls();
            fresh.LoadBindingOverridesFromJson(first.SaveBindingOverridesAsJson());
            InputAction after = Action(fresh, map, ButtonName(map));
            InputBinding restored = after.bindings[Slot(after, GameInput.GamepadGroup)];
            Assert.AreEqual(string.Empty, restored.overridePath, "Empty means unbound; null would silently restore the default.");
            Assert.AreEqual(string.Empty, restored.effectivePath);
            Assert.IsTrue(restored.hasOverrides);
        }

        [Test]
        public void ADefaultEmptySlotCanBeReboundAndRestored()
        {
            InputActionAsset first = NewAsset();
            InputActionMap map = first.AddActionMap("Flight");
            InputAction before = map.AddAction("Optional control", InputActionType.Button);
            before.AddBinding(string.Empty, groups: GameInput.GamepadGroup);
            InputBindingIdentity.Assign(map);
            before.ApplyBindingOverride(0, "<Gamepad>/buttonNorth");

            InputActionAsset fresh = NewAsset();
            InputActionMap freshMap = fresh.AddActionMap("Flight");
            InputAction after = freshMap.AddAction("Optional control", InputActionType.Button);
            after.AddBinding(string.Empty, groups: GameInput.GamepadGroup);
            InputBindingIdentity.Assign(freshMap);
            fresh.LoadBindingOverridesFromJson(first.SaveBindingOverridesAsJson());

            Assert.AreEqual(string.Empty, after.bindings[0].path);
            Assert.AreEqual("<Gamepad>/buttonNorth", after.bindings[0].effectivePath);
        }

        [Test]
        public void InsertingAndReorderingBindingsDoesNotChangeExistingSlotIdentities()
        {
            InputActionAsset first = BuildControls();
            InputActionAsset revised = BuildControls(true);
            foreach (InputActionMap map in first.actionMaps)
            {
                InputActionMap nextMap = revised.FindActionMap(map.name, true);
                foreach (InputAction action in map.actions)
                {
                    InputAction next = nextMap.FindAction(action.name, true);
                    foreach (InputBinding binding in action.bindings)
                    {
                        bool found = false;
                        foreach (InputBinding candidate in next.bindings)
                            if (candidate.id == binding.id) found = true;
                        Assert.IsTrue(found, "Existing identity moved when unrelated actions or controls were inserted: " + action.name + "/" + binding.name);
                    }
                }
            }
        }

        [Test]
        public void ChangingTheDefaultPathKeepsThePlayersOverrideOnItsSlot()
        {
            InputActionAsset first = BuildControls();
            InputAction before = Action(first, "Gameplay", "Fire");
            before.ApplyBindingOverride(Slot(before, GameInput.KeyboardMouseGroup), "<Keyboard>/j");

            InputActionAsset revised = BuildControls(true);
            revised.LoadBindingOverridesFromJson(first.SaveBindingOverridesAsJson());
            InputAction after = Action(revised, "Gameplay", "Fire");
            InputBinding slot = after.bindings[Slot(after, GameInput.KeyboardMouseGroup)];
            Assert.AreEqual("<Keyboard>/p", slot.path, "The revised build really has a different default.");
            Assert.AreEqual("<Keyboard>/j", slot.effectivePath);
            Assert.IsNull(after.bindings[Slot(after, GameInput.KeyboardMouseGroup, "Alternate")].overridePath);
        }

        [Test]
        public void ReorderingAGroupListDoesNotMoveItsOverride()
        {
            InputActionAsset first = NewAsset();
            InputActionMap firstMap = first.AddActionMap("Gameplay");
            InputAction before = firstMap.AddAction("Fire", InputActionType.Button);
            before.AddBinding("<Keyboard>/space", groups: "KeyboardMouse;Shared");
            InputBindingIdentity.Assign(firstMap);
            before.ApplyBindingOverride(0, "<Keyboard>/j");

            InputActionAsset fresh = NewAsset();
            InputActionMap nextMap = fresh.AddActionMap("Gameplay");
            InputAction after = nextMap.AddAction("Fire", InputActionType.Button);
            after.AddBinding("<Keyboard>/space", groups: "Shared;KeyboardMouse");
            InputBindingIdentity.Assign(nextMap);
            fresh.LoadBindingOverridesFromJson(first.SaveBindingOverridesAsJson());

            Assert.AreEqual(before.bindings[0].id, after.bindings[0].id);
            Assert.AreEqual("<Keyboard>/j", after.bindings[0].overridePath);
            Assert.AreEqual("Shared;KeyboardMouse", after.bindings[0].groups, "Canonical identity must not rewrite binding metadata.");
        }

        [TestCase(null)]
        [TestCase("Primary")]
        public void AmbiguousSlotsAreRejectedInsteadOfChoosingAnArbitraryControl(string name)
        {
            InputActionMap map = NewAsset().AddActionMap("Gameplay");
            InputAction action = map.AddAction("Fire", InputActionType.Button);
            action.AddBinding("<Keyboard>/space", groups: GameInput.KeyboardMouseGroup).WithName(name);
            action.AddBinding("<Keyboard>/j", groups: GameInput.KeyboardMouseGroup).WithName(name);
            Assert.Throws<InvalidOperationException>(() => InputBindingIdentity.Assign(map));
        }

        [Test]
        public void NamedCompositesCanReusePartNamesWithoutSharingOverrides()
        {
            InputActionAsset first = NewAsset();
            InputActionMap map = first.AddActionMap("Gameplay");
            InputAction before = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            AddComposite(before, "WASD");
            AddComposite(before, "Alternative");
            InputBindingIdentity.Assign(map);
            Guid firstUp = before.bindings[1].id;
            Guid otherUp = before.bindings[6].id;
            Assert.AreNotEqual(firstUp, otherUp);
            before.ApplyBindingOverride(6, "<Keyboard>/i");

            InputActionAsset fresh = NewAsset();
            InputActionMap freshMap = fresh.AddActionMap("Gameplay");
            InputAction after = freshMap.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            AddComposite(after, "Alternative");
            AddComposite(after, "WASD");
            InputBindingIdentity.Assign(freshMap);
            fresh.LoadBindingOverridesFromJson(first.SaveBindingOverridesAsJson());
            Assert.AreEqual(otherUp, after.bindings[1].id);
            Assert.AreEqual("<Keyboard>/i", after.bindings[1].overridePath);
            Assert.AreEqual(firstUp, after.bindings[6].id);
            Assert.IsNull(after.bindings[6].overridePath);
        }

        [Test]
        public void AssigningIdentitiesPreservesAllOtherBindingFields()
        {
            InputActionAsset asset = BuildControls();
            InputAction fire = Action(asset, "Gameplay", "Fire");
            fire.ChangeBinding(0).WithInteractions("press(behavior=1)").WithProcessors("scale(factor=0.5)");
            fire.ApplyBindingOverride(0, new InputBinding
            {
                overridePath = string.Empty,
                overrideInteractions = "hold(duration=0.4)",
                overrideProcessors = "scale(factor=0.75)"
            });

            foreach (InputActionMap map in asset.actionMaps)
            {
                var snapshots = new List<InputBinding>();
                foreach (InputBinding binding in map.bindings) snapshots.Add(binding);
                InputBindingIdentity.Assign(map);
                for (int i = 0; i < snapshots.Count; i++)
                {
                    InputBinding before = snapshots[i];
                    InputBinding after = map.bindings[i];
                    Assert.AreEqual(before.id, after.id, "Assigning identities again must be stable.");
                    Assert.AreEqual(before.name, after.name);
                    Assert.AreEqual(before.path, after.path);
                    Assert.AreEqual(before.action, after.action);
                    Assert.AreEqual(before.groups, after.groups);
                    Assert.AreEqual(before.interactions, after.interactions);
                    Assert.AreEqual(before.processors, after.processors);
                    Assert.AreEqual(before.overridePath, after.overridePath);
                    Assert.AreEqual(before.overrideInteractions, after.overrideInteractions);
                    Assert.AreEqual(before.overrideProcessors, after.overrideProcessors);
                    Assert.AreEqual(before.isComposite, after.isComposite);
                    Assert.AreEqual(before.isPartOfComposite, after.isPartOfComposite);
                }
            }
        }

        [Test]
        public void AssignRequiresADisabledNonNullMap()
        {
            Assert.Throws<ArgumentNullException>(() => InputBindingIdentity.Assign(null));
            InputActionMap map = NewAsset().AddActionMap("Gameplay");
            map.AddAction("Fire", InputActionType.Button).AddBinding("<Keyboard>/space");
            map.Enable();
            Assert.Throws<InvalidOperationException>(() => InputBindingIdentity.Assign(map));
        }

        [Test]
        public void CurrentGameInputMapsAlreadyHaveUniqueStableIdentities()
        {
            GameInput.Initialize();
            // Inspect a local copy, leaving live actions, overrides and preferences untouched.
            InputActionAsset copy = InputActionAsset.FromJson(GameInput.Asset.ToJson());
            _assets.Add(copy);
            var seen = new HashSet<Guid>();
            foreach (string mapName in new[] { "Gameplay", "Flight" })
            {
                InputActionMap actual = GameInput.Asset.FindActionMap(mapName, true);
                InputActionMap local = copy.FindActionMap(mapName, true);
                InputBindingIdentity.Assign(local);
                Assert.Greater(actual.bindings.Count, 0);
                for (int i = 0; i < actual.bindings.Count; i++)
                {
                    Assert.AreNotEqual(Guid.Empty, actual.bindings[i].id);
                    Assert.IsTrue(seen.Add(actual.bindings[i].id), "Every current play binding needs its own identity.");
                    Assert.AreEqual(local.bindings[i].id, actual.bindings[i].id,
                        "GameInput must assign stable identities before loading saved overrides.");
                }
            }
        }
    }
}
