using System;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The rules behind the game's look: how a body's two sky colours become a painted sky at any time of day,
    /// and the picture of deep space behind the stars. These run without Unity objects.
    /// </summary>
    public class LookRulesTests
    {
        static readonly Rgb EarthSky = new Rgb(0.43f, 0.71f, 1f);
        static readonly Rgb EarthHaze = new Rgb(0.75f, 0.85f, 0.95f);

        [Test]
        public void SunsetIsStrongestWithTheSunOnTheHorizon()
        {
            float onHorizon = SkyLook.SunsetAmount(0.03f);
            Assert.That(onHorizon, Is.EqualTo(1f).Within(0.001f));
            Assert.That(SkyLook.SunsetAmount(0.2f), Is.LessThan(onHorizon));
            Assert.That(SkyLook.SunsetAmount(-0.1f), Is.LessThan(onHorizon));
        }

        [Test]
        public void ThereIsNoSunsetAtMiddayOrInTheDeadOfNight()
        {
            Assert.That(SkyLook.SunsetAmount(1f), Is.EqualTo(0f));
            Assert.That(SkyLook.SunsetAmount(0.5f), Is.EqualTo(0f));
            Assert.That(SkyLook.SunsetAmount(-0.5f), Is.EqualTo(0f));
        }

        [Test]
        public void SunsetNeverLeavesTheRangeNoughtToOne()
        {
            for (float h = -1f; h <= 1f; h += 0.01f)
            {
                Assert.That(SkyLook.SunsetAmount(h), Is.InRange(0f, 1f), "sun height " + h);
            }
        }

        [Test]
        public void TheSkyIsDeeperOverheadThanAtTheHorizon()
        {
            SkyPalette noon = SkyLook.InAir(EarthSky, EarthHaze, 0.9f, 1f, 1f, false, 1f, 1f);
            Assert.That(noon.Zenith.Brightness, Is.LessThan(noon.Horizon.Brightness));
            // Still recognisably the body's own colour: on Earth, blue leads overhead.
            Assert.That(noon.Zenith.B, Is.GreaterThan(noon.Zenith.R));
            Assert.That(noon.Sunset.Brightness, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(noon.SunTint.R, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(noon.SunTint.B, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void AtSunsetTheHorizonTurnsWarmAndSoDoesTheSunlight()
        {
            SkyPalette dusk = SkyLook.InAir(EarthSky, EarthHaze, 0.03f, 0.6f, 1f, false, 1f, 1f);
            Assert.That(dusk.Sunset.R, Is.GreaterThan(dusk.Sunset.B * 1.5f));
            Assert.That(dusk.SunTint.B, Is.LessThan(dusk.SunTint.R));
            Assert.That(dusk.SunsetAmount, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void AStormHasNoSunsetAndNoGlowRoundTheSun()
        {
            SkyPalette storm = SkyLook.InAir(EarthSky, EarthHaze, 0.03f, 0.6f, 1f, false, 0f, 1f);
            Assert.That(storm.Sunset.Brightness, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(storm.GlowStrength, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void InsideCloudOrWaterTheSurroundingsAreOneColourAllRound()
        {
            var sea = new Rgb(0.12f, 0.43f, 0.66f);
            SkyPalette under = SkyLook.InAir(sea, sea, 0.5f, 0.8f, 1f, true, 1f, 1f);
            Assert.That(under.Zenith.R, Is.EqualTo(under.Horizon.R).Within(0.0001f));
            Assert.That(under.Zenith.B, Is.EqualTo(under.Horizon.B).Within(0.0001f));
            Assert.That(under.Sunset.Brightness, Is.EqualTo(0f));
        }

        [Test]
        public void TheSkyFadesToBlackSpaceAtTheTopOfTheAir()
        {
            SkyPalette top = SkyLook.InAir(EarthSky, EarthHaze, 0.9f, 1f, 0f, false, 1f, 0.8f);
            Assert.That(top.Zenith.Brightness, Is.EqualTo(0f));
            Assert.That(top.Horizon.Brightness, Is.EqualTo(0f));
            // And the Sun's glow there is exactly the glow of open space, so nothing jumps as you leave the air.
            Assert.That(top.GlowStrength, Is.EqualTo(SkyLook.OpenSpace(0.8f).GlowStrength).Within(0.0001f));
        }

        [Test]
        public void TheGlowRoundTheSunGoesOutOnceItHasSet()
        {
            SkyPalette night = SkyLook.InAir(EarthSky, EarthHaze, -0.4f, 0.035f, 1f, false, 1f, 1f);
            Assert.That(night.GlowStrength, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void NoSkyColourIsEverNegative()
        {
            var odd = new Rgb(1f, 0f, 0.02f);
            for (float h = -1f; h <= 1f; h += 0.05f)
            {
                SkyPalette sky = SkyLook.InAir(odd, odd, h, 1f, 1f, false, 1f, 1f);
                foreach (Rgb c in new[] { sky.Zenith, sky.Horizon, sky.Sunset, sky.Glow, sky.SunTint })
                {
                    Assert.That(c.R, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(c.G, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(c.B, Is.GreaterThanOrEqualTo(0f));
                }
            }
        }

        [Test]
        public void DeepSpaceIsTheSamePictureEveryTime()
        {
            byte[] a = SpaceBackdrop.Paint(64, 32, 4104);
            byte[] b = SpaceBackdrop.Paint(64, 32, 4104);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.Length, Is.EqualTo(64 * 32 * 4));
        }

        [Test]
        public void DeepSpaceStaysDimSoTheStarsStandOut()
        {
            byte[] pixels = SpaceBackdrop.Paint(128, 64, 4104);
            int brightest = 0;
            long total = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                int most = Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2]));
                brightest = Math.Max(brightest, most);
                total += most;
                Assert.That(pixels[i + 3], Is.EqualTo(255));
            }
            Assert.That(brightest, Is.LessThan(110), "no part of deep space may compete with a star");
            Assert.That(brightest, Is.GreaterThan(20), "but it must not be plain black either");
            Assert.That(total / (pixels.Length / 4), Is.LessThan(30), "on average the sky is still dark");
        }

        [Test]
        public void DeepSpaceHasNoSeamWhereThePictureWrapsRound()
        {
            // The two directions either side of the join are next-door neighbours in the sky.
            Vec3d tilt = new Vec3d(0.35, 0.87, 0.35).Normalized;
            for (int i = 1; i < 16; i++)
            {
                double polar = i / 16.0 * Math.PI;
                var justBefore = Direction(polar, Math.PI - 1e-4);
                var justAfter = Direction(polar, -Math.PI + 1e-4);
                SpaceBackdrop.Sample(justBefore, tilt, 4104, out double r1, out double g1, out double b1);
                SpaceBackdrop.Sample(justAfter, tilt, 4104, out double r2, out double g2, out double b2);
                Assert.That(Math.Abs(r1 - r2) + Math.Abs(g1 - g2) + Math.Abs(b1 - b2), Is.LessThan(0.002));
            }
        }

        static Vec3d Direction(double polar, double around)
        {
            return new Vec3d(Math.Sin(polar) * Math.Cos(around), Math.Cos(polar), Math.Sin(polar) * Math.Sin(around));
        }
    }
}
