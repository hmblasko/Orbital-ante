using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tests
{
    /// <summary>These identities are part of the saved-controls format and run without Unity.</summary>
    public class BindingIdentityTests
    {
        [Test]
        public void TheSavedIdentityContractIsPinned()
        {
            Assert.AreEqual(new Guid("3477d95f-de3a-85c7-2f36-95eef581553c"),
                BindingIdentity.Create("Gameplay", "Fire", "KeyboardMouse", null, "binding"),
                "Changing this identity requires a deliberate saved-controls migration.");
        }

        [Test]
        public void EverySlotComponentSeparatesOtherwiseIdenticalBindings()
        {
            Guid original = BindingIdentity.Create("Gameplay", "Fire", "KeyboardMouse", "Primary", "binding");
            Assert.AreNotEqual(original, BindingIdentity.Create("Flight", "Fire", "KeyboardMouse", "Primary", "binding"));
            Assert.AreNotEqual(original, BindingIdentity.Create("Gameplay", "Reload", "KeyboardMouse", "Primary", "binding"));
            Assert.AreNotEqual(original, BindingIdentity.Create("Gameplay", "Fire", "Gamepad", "Primary", "binding"));
            Assert.AreNotEqual(original, BindingIdentity.Create("Gameplay", "Fire", "KeyboardMouse", "Secondary", "binding"));
            Assert.AreNotEqual(original, BindingIdentity.Create("Gameplay", "Fire", "KeyboardMouse", "Primary", "part"));
        }

        [Test]
        public void TheSamePartNameInTwoNamedCompositesHasDistinctIdentities()
        {
            Assert.AreNotEqual(
                BindingIdentity.Create("Gameplay", "Move", "KeyboardMouse", "Up", "part", "WASD"),
                BindingIdentity.Create("Gameplay", "Move", "KeyboardMouse", "Up", "part", "Arrows"));
        }

        [Test]
        public void LengthPrefixedFieldsCannotAliasThroughDelimiters()
        {
            Assert.AreNotEqual(
                BindingIdentity.Create("a:b", "c", "KeyboardMouse", "slot", "binding"),
                BindingIdentity.Create("a", "b:c", "KeyboardMouse", "slot", "binding"));
            Assert.AreNotEqual(
                BindingIdentity.Create("Map", "Action", "a;", "b", "part", "root"),
                BindingIdentity.Create("Map", "Action", "a", ";b", "part", "root"));
            Assert.AreNotEqual(
                BindingIdentity.Create("Map", "Action", "Group", "up", "part", "a:b"),
                BindingIdentity.Create("Map", "Action", "Group", "up:part", "a", "b"));
        }

        [Test]
        public void MissingOptionalNamesAndGroupsMeanTheSameAsEmptyStrings()
        {
            Assert.AreEqual(
                BindingIdentity.Create("Gameplay", "Fire", null, null, "binding"),
                BindingIdentity.Create("Gameplay", "Fire", string.Empty, string.Empty, "binding", string.Empty));
        }

        [TestCase("tr-TR")]
        [TestCase("ar-SA")]
        public void IdentitiesDoNotDependOnThePlayersCulture(string culture)
        {
            Guid expected = BindingIdentity.Create("Gameplay", "Interact I", "KeyboardMouse", "slot:ı", "binding");
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            CultureInfo previousUi = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                Assert.AreEqual(expected,
                    BindingIdentity.Create("Gameplay", "Interact I", "KeyboardMouse", "slot:ı", "binding"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
                Thread.CurrentThread.CurrentUICulture = previousUi;
            }
        }

        [TestCase(null)]
        [TestCase("")]
        public void AMapNameIsRequired(string map)
        {
            Assert.Throws<ArgumentException>(() => BindingIdentity.Create(map, "Fire", null, null, "binding"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void AnActionNameIsRequired(string action)
        {
            Assert.Throws<ArgumentException>(() => BindingIdentity.Create("Gameplay", action, null, null, "binding"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void ABindingKindIsRequired(string kind)
        {
            Assert.Throws<ArgumentException>(() => BindingIdentity.Create("Gameplay", "Fire", null, null, kind));
        }
    }
}
