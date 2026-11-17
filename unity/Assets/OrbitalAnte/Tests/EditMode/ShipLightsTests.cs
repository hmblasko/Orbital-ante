using System;
using NUnit.Framework;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests.EditMode
{
    public class ShipLightsTests
    {
        static readonly FloodRules Rules = new FloodRules();

        static void Run(ShipLights lights, float seconds, float dark, bool working = true)
        {
            for (float t = 0f; t < seconds; t += 0.02f) lights.Step(0.02f, dark, working, Rules);
        }

        [Test]
        public void InTheDark_TheyComeOnByThemselves_AndGoOffInTheLight()
        {
            var lights = new ShipLights();
            Run(lights, 1f, 0f);
            Assert.That(lights.Level, Is.EqualTo(0f));
            Run(lights, 1f, 0.9f);
            Assert.That(lights.Level, Is.EqualTo(1f));
            Run(lights, 1f, 0.35f);
            Assert.That(lights.Level, Is.EqualTo(1f), "in between they stay as they were, so they never flicker at dusk");
            Run(lights, 1f, 0.05f);
            Assert.That(lights.Level, Is.EqualTo(0f));
        }

        [Test]
        public void APress_TakesOver_AndTheyStayAsThePilotSetThem()
        {
            var lights = new ShipLights();
            Run(lights, 1f, 0f);
            Assert.That(lights.Press(), Is.True, "off in daylight, a press puts them on");
            Assert.That(lights.Setting, Is.EqualTo(ShipLights.Mode.On));
            Run(lights, 1f, 0f);
            Assert.That(lights.Level, Is.EqualTo(1f), "and they stay on in daylight");

            Run(lights, 1f, 1f);
            Assert.That(lights.Press(), Is.False, "in the dark, a press puts them off");
            Assert.That(lights.Setting, Is.EqualTo(ShipLights.Mode.Off));
            Run(lights, 1f, 1f);
            Assert.That(lights.Level, Is.EqualTo(0f), "switched off, the dark does not put them back on");
        }

        [Test]
        public void APress_ThatAgreesWithTheDark_HandsThemBackToThemselves()
        {
            var lights = new ShipLights();
            Run(lights, 1f, 0f);
            lights.Press();
            Assert.That(lights.Press(), Is.False, "off again in daylight");
            Assert.That(lights.Setting, Is.EqualTo(ShipLights.Mode.Auto), "which is what the daylight wants, so they are automatic again");
            Run(lights, 1f, 1f);
            Assert.That(lights.Level, Is.EqualTo(1f), "and the next dark place puts them on");
        }

        [Test]
        public void TheySwitchQuickly_ButNotInAnInstant()
        {
            var lights = new ShipLights();
            lights.Press();
            lights.Step(0.05f, 0f, true, Rules);
            Assert.That(lights.Level, Is.GreaterThan(0f).And.LessThan(1f));
            Run(lights, Rules.switchSeconds + 0.05f, 0f);
            Assert.That(lights.Level, Is.EqualTo(1f));
        }

        [Test]
        public void WithNoPower_TheyAreOut_WhateverTheSetting()
        {
            var lights = new ShipLights();
            lights.Press();
            Run(lights, 1f, 1f, working: false);
            Assert.That(lights.Level, Is.EqualTo(0f));
        }

        [Test]
        public void TheBeam_LightsTheGroundAheadOfALandedShip()
        {
            P3 way = ShipLights.Direction(Rules);
            // Standing level, the ground is at GroundY. Ahead of the nose, from just past the legs to well out.
            for (float ahead = 8f; ahead <= 40f; ahead += 4f)
            {
                var ground = new P3(0f, ShipLayout.GroundY, ahead);
                Assert.That(ShipLights.Reaching(ShipLights.Origin, way, ground, Rules), Is.GreaterThan(0.1f), $"the ground {ahead} m ahead is lit");
            }
            // And something straight ahead at the ship's own height, a little way off, is lit too.
            Assert.That(ShipLights.Reaching(ShipLights.Origin, way, new P3(0f, -1f, 20f), Rules), Is.GreaterThan(0.1f));
        }

        [Test]
        public void TheBeam_NeverLightsTheCabinOrWhatIsBehindTheShip()
        {
            P3 way = ShipLights.Direction(Rules);
            for (float x = -2f; x <= 2f; x += 0.5f)
            for (float y = 0f; y <= ShipLayout.CeilingPeak; y += 0.5f)
            for (float z = -2f; z <= 2f; z += 0.5f)
            {
                var p = new P3(x, y, z);
                if (!ShipLayout.InsideCabin(p)) continue;
                Assert.That(ShipLights.Reaching(ShipLights.Origin, way, p, Rules), Is.EqualTo(0f), $"the cabin at {x},{y},{z}");
            }
            Assert.That(ShipLights.Reaching(ShipLights.Origin, way, new P3(0f, ShipLayout.GroundY, -15f), Rules), Is.EqualTo(0f));
        }

        [Test]
        public void TheLamps_SitOnTheFrontFaceOfTheBase_ClearOfTheLegs()
        {
            float faceHalf = ShipLayout.BaseInradius * (float)Math.Tan(Math.PI / 8.0);
            foreach (P3 lamp in ShipLights.Lamps)
            {
                Assert.That(Math.Abs(lamp.X) + ShipLights.LampRadius, Is.LessThan(faceHalf), "on the front face, not round the corner");
                Assert.That(lamp.Y, Is.LessThan(ShipLayout.BaseTop - 0.2f).And.GreaterThan(ShipLayout.BellyY + 0.2f), "within the height of the base");
                Assert.That(lamp.Z, Is.GreaterThan(ShipLayout.BaseInradius), "in front of the face, not buried in it");
            }
            P3 way = ShipLights.Direction(Rules);
            Assert.That(way.Z, Is.GreaterThan(0.5f), "it points ahead");
            Assert.That(way.Y, Is.LessThan(0f), "and down");
        }
    }
}
