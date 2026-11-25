using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Audio;
using OrbitalAnte.Look;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Rules for the "does not look made by a machine" pass (Matthew, 22 Nov 2026): scattered things sit in the
    /// ground one by one, the colour grade only seasons the picture, and sounds the player hears over and over
    /// never repeat exactly.
    /// </summary>
    public class HandmadeFeelTests
    {
        static IEnumerable<FeatureKind> AllKinds() => (FeatureKind[])Enum.GetValues(typeof(FeatureKind));

        [Test]
        public void SamePlacement_AlwaysSitsTheSameWay()
        {
            foreach (FeatureKind kind in AllKinds())
            {
                NaturalPose.Pose a = NaturalPose.Of(kind, 517, 2.25, false);
                NaturalPose.Pose b = NaturalPose.Of(kind, 517, 2.25, false);
                Assert.AreEqual(a.TiltDegrees, b.TiltDegrees, kind.ToString());
                Assert.AreEqual(a.StretchX, b.StretchX, kind.ToString());
                Assert.AreEqual(a.Sink, b.Sink, kind.ToString());
                Assert.AreEqual(a.Shade, b.Shade, kind.ToString());
            }
        }

        [Test]
        public void LooseRocks_DifferOneToAnother_AndAreBedded()
        {
            var tilts = new HashSet<int>();
            var widths = new HashSet<int>();
            var shades = new HashSet<float>();
            var rng = new Random(3);
            for (int i = 0; i < 400; i++)
            {
                NaturalPose.Pose p = NaturalPose.Of(FeatureKind.Boulder, rng.Next(0, 1024), rng.NextDouble() * Math.PI * 2, false);
                tilts.Add((int)p.TiltDegrees);
                widths.Add((int)(p.StretchX * 20));
                shades.Add(p.Shade);
                Assert.Greater(p.Sink, 0.05f, "a boulder sitting on top of the ground shows its base");
                Assert.LessOrEqual(p.Sink, 0.3f, "a boulder buried past a third reads as a bump");
            }
            Assert.Greater(tilts.Count, 6, "boulders all tipped the same way");
            Assert.Greater(widths.Count, 6, "boulders all the same proportions");
            Assert.AreEqual(1 + 2 * NaturalPose.ShadeSteps, shades.Count, "shades must vary, and stay rounded so materials are shared");
        }

        [Test]
        public void NothingLeansFarEnoughToLookBroken_OrStretchesOutOfShape()
        {
            var rng = new Random(11);
            foreach (FeatureKind kind in AllKinds())
            {
                for (int i = 0; i < 200; i++)
                {
                    NaturalPose.Pose p = NaturalPose.Of(kind, rng.Next(0, 1024), rng.NextDouble() * 7, false);
                    Assert.LessOrEqual(p.TiltDegrees, 15f, kind.ToString());
                    foreach (float s in new[] { p.StretchX, p.StretchY, p.StretchZ })
                    {
                        Assert.That(s, Is.InRange(0.7f, 1.3f), kind.ToString());
                    }
                    Assert.That(p.Shade, Is.InRange(0.85f, 1.15f), kind.ToString());
                }
            }
        }

        [Test]
        public void Landmarks_AndBalancedRocks_StayNearlyUpright()
        {
            foreach (FeatureKind kind in new[] { FeatureKind.Hoodoo, FeatureKind.Spire, FeatureKind.Arch, FeatureKind.BasaltColumns, FeatureKind.BalancedRock })
            {
                for (int v = 0; v < 1024; v += 7)
                {
                    Assert.LessOrEqual(NaturalPose.Of(kind, v, v * 0.37, false).TiltDegrees, 2.5f, kind.ToString());
                }
            }
        }

        [Test]
        public void Trees_KeepARoundTrunk_AndTheirHeight()
        {
            // The trunk is one capsule: width and depth must stretch together, and height is already the
            // scatter's own number, which decides what is solid and how far off it shows.
            foreach (FeatureKind kind in new[] { FeatureKind.Birch, FeatureKind.Pine, FeatureKind.Conifer, FeatureKind.Palm, FeatureKind.GiantTree, FeatureKind.Snag })
            {
                for (int v = 0; v < 1024; v += 13)
                {
                    NaturalPose.Pose p = NaturalPose.Of(kind, v, v * 0.11, false);
                    Assert.AreEqual(p.StretchX, p.StretchZ, kind.ToString());
                    Assert.AreEqual(1f, p.StretchY, kind.ToString());
                    Assert.LessOrEqual(p.Sink, 0.05f, kind + ": a tree sunk deep loses its roots and its lowest branches");
                }
            }
        }

        [Test]
        public void ThingsThatFloat_AreNeverSunk()
        {
            for (int v = 0; v < 1024; v += 5)
            {
                Assert.AreEqual(0f, NaturalPose.Of(FeatureKind.Iceberg, v, v, true).Sink);
                Assert.AreEqual(0f, NaturalPose.Of(FeatureKind.Boulder, v, v, true).Sink);
            }
        }

        [Test]
        public void Tint_KeepsColoursInRange_AndOnlyShadesThem()
        {
            NaturalPose.Pose bright = new NaturalPose.Pose(0f, 0f, 1f, 1f, 1f, 0f, 1.09f, 0.6f);
            NaturalPose.Tint(bright, 0.98f, 0.5f, 0.02f, out float r, out float g, out float b);
            Assert.That(r, Is.InRange(0f, 1f));
            Assert.That(b, Is.InRange(0f, 1f));
            Assert.AreEqual(0.5f * 1.09f, g, 1e-5f);
            NaturalPose.Tint(NaturalPose.Pose.Upright, 0.3f, 0.4f, 0.5f, out r, out g, out b);
            Assert.AreEqual(0.3f, r, 1e-6f);
            Assert.AreEqual(0.4f, g, 1e-6f);
            Assert.AreEqual(0.5f, b, 1e-6f);
        }

        [Test]
        public void ColourGrade_OnlySeasonsThePicture()
        {
            // A strong teal-and-amber split, pushed saturation and a heavy vignette are the stock look of
            // generated pictures. Keep them low; a change above these needs Matthew to have seen it in the game.
            Assert.LessOrEqual(GradeDefaults.SplitTone, 0.25f);
            Assert.LessOrEqual(GradeDefaults.Saturation, 5f);
            Assert.LessOrEqual(GradeDefaults.Contrast, 8f);
            Assert.LessOrEqual(GradeDefaults.Vignette, 0.15f);
            Assert.GreaterOrEqual(GradeDefaults.Version, 1);
        }

        static string AudioFolder()
        {
            string dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && dir != null; i++)
            {
                foreach (string rel in new[] { "Assets/OrbitalAnte/Resources/Audio", "unity/Assets/OrbitalAnte/Resources/Audio" })
                {
                    string p = Path.Combine(dir, rel);
                    if (Directory.Exists(p)) return p;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        [Test]
        public void SoundsHeardOverAndOver_NeverRepeatExactly()
        {
            string folder = AudioFolder();
            if (folder == null) Assert.Ignore("The audio folder was not found from here.");
            AudioManifest m = AudioManifest.Parse(File.ReadAllText(Path.Combine(folder, "audio_manifest.json")));
            // Menu clicks, pick-ups and melee hits are heard hundreds of times a run. The hit ding, kill chimes and
            // reward sounds are left exact on purpose: their pitch is how the player learns what happened.
            foreach (string id in new[] { "ui.select", "ui.back", "ship.confirm", "melee.pickup", "melee.drop",
                         "melee.frying_pan.clang", "melee.frying_pan.sweet", "melee.mailbox.sweet",
                         "melee.butterfly_knife.sweet", "melee.laser_sword.sweet" })
            {
                Assert.IsTrue(m.Events.TryGetValue(id, out AudioEventDef e), id + " is missing from the manifest");
                Assert.IsTrue(e.Clips.Length > 1 || e.PitchVar > 0f, id + " plays the same clip at the same pitch every time");
                Assert.LessOrEqual(e.PitchVar, 1.0f, id + " wanders far enough to sound like another sound");
            }
        }
    }
}
