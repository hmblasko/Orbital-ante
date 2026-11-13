using NUnit.Framework;
using OrbitalAnte.Core;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Real Input System display/override checks. Each test owns its actions and never changes GameInput's
    /// live maps, PlayerPrefs, or connected devices. Run in Unity's PlayMode Test Runner; not a device/UX test.
    /// </summary>
    public class InputHintTests
    {
        static InputAction Movement()
        {
            var action = new InputAction("Thrust", InputActionType.Value);
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", GameInput.KeyboardMouseGroup)
                .With("Down", "<Keyboard>/s", GameInput.KeyboardMouseGroup)
                .With("Left", "<Keyboard>/a", GameInput.KeyboardMouseGroup)
                .With("Right", "<Keyboard>/d", GameInput.KeyboardMouseGroup);
            action.AddBinding("<Gamepad>/leftStick", groups: GameInput.GamepadGroup);
            return action;
        }

        [Test]
        public void SimpleHintsSelectTheDeviceGroupRegardlessOfBindingOrder()
        {
            using (var action = new InputAction("Interact", InputActionType.Button))
            {
                action.AddBinding("<Gamepad>/buttonNorth", groups: GameInput.GamepadGroup);
                action.AddBinding("<Keyboard>/e", groups: GameInput.KeyboardMouseGroup);
                Assert.AreEqual(action.GetBindingDisplayString(1), GameInput.DescribeForGroup(action, GameInput.KeyboardMouseGroup));
                Assert.AreEqual(action.GetBindingDisplayString(0), GameInput.DescribeForGroup(action, GameInput.GamepadGroup));
            }
        }

        [Test]
        public void MovementHintIncludesEveryReboundKeyboardDirection()
        {
            using (InputAction action = Movement())
            {
                action.ApplyBindingOverride(1, "<Keyboard>/i");
                action.ApplyBindingOverride(2, "<Keyboard>/k");
                action.ApplyBindingOverride(3, "<Keyboard>/j");
                action.ApplyBindingOverride(4, "<Keyboard>/l");
                string expected = action.GetBindingDisplayString(1) + " / " + action.GetBindingDisplayString(2)
                    + " / " + action.GetBindingDisplayString(3) + " / " + action.GetBindingDisplayString(4);
                Assert.AreEqual(expected, GameInput.DescribeForGroup(action, GameInput.KeyboardMouseGroup));
            }
        }

        [Test]
        public void MovementHintSelectsTheStickBeyondTheCompositeParts()
        {
            using (InputAction action = Movement())
            {
                // The pad binding is index 5, not index 1 (which is the keyboard's forward key).
                action.ApplyBindingOverride(5, "<Gamepad>/rightStick");
                Assert.AreEqual(action.GetBindingDisplayString(5), GameInput.DescribeForGroup(action, GameInput.GamepadGroup));
            }
        }

        [Test]
        public void ZoomHintUsesOnlyTheReboundForwardAndBackKeys()
        {
            using (InputAction action = Movement())
            {
                action.ApplyBindingOverride(1, "<Keyboard>/i");
                action.ApplyBindingOverride(2, "<Keyboard>/k");
                string expected = action.GetBindingDisplayString(1) + " / " + action.GetBindingDisplayString(2);
                Assert.AreEqual(expected, GameInput.DescribeForGroup(action, GameInput.KeyboardMouseGroup, "up", "down"));
            }
        }

        [Test]
        public void ZoomHintStillDescribesADirectlyBoundStick()
        {
            using (InputAction action = Movement())
            {
                action.ApplyBindingOverride(5, "<Gamepad>/rightStick");
                Assert.AreEqual(action.GetBindingDisplayString(5), GameInput.DescribeForGroup(action, GameInput.GamepadGroup, "Up", "Down"));
            }
        }

        [Test]
        public void RemovingAnOverrideRestoresTheDefaultHint()
        {
            using (var action = new InputAction("Hold fire", InputActionType.Button))
            {
                action.AddBinding("<Keyboard>/h", groups: GameInput.KeyboardMouseGroup);
                string original = GameInput.DescribeForGroup(action, GameInput.KeyboardMouseGroup);
                action.ApplyBindingOverride(0, "<Keyboard>/u");
                Assert.AreEqual(action.GetBindingDisplayString(0), GameInput.DescribeForGroup(action, GameInput.KeyboardMouseGroup));
                action.RemoveBindingOverride(0);
                Assert.AreEqual(original, GameInput.DescribeForGroup(action, GameInput.KeyboardMouseGroup));
            }
        }

        [Test]
        public void DisabledBindingDoesNotAdvertiseItsDefaultKey()
        {
            using (var action = new InputAction("Interact", InputActionType.Button))
            {
                action.AddBinding("<Keyboard>/e", groups: GameInput.KeyboardMouseGroup);
                action.ApplyBindingOverride(0, string.Empty);
                Assert.AreEqual("Unbound", GameInput.DescribeForGroup(action, GameInput.KeyboardMouseGroup));
            }
        }

        [Test]
        public void MissingDeviceGroupDoesNotAdvertiseTheOtherDevicesKey()
        {
            using (var action = new InputAction("Debug", InputActionType.Button))
            {
                action.AddBinding("<Keyboard>/f3", groups: GameInput.KeyboardMouseGroup);
                Assert.AreEqual("Unbound", GameInput.DescribeForGroup(action, GameInput.GamepadGroup));
            }
        }
    }
}
