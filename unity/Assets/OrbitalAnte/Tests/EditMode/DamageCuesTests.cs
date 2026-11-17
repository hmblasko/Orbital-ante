using NUnit.Framework;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>What the visor shows when the player is hurt: where the shot came from, the edge glow, cracks.</summary>
    public class DamageCuesTests
    {
        [Test]
        public void Marker_PointsTheWayTheShotCameFrom()
        {
            Assert.AreEqual(90f, DamageCues.MarkerAngle(0f, 0f, 1f), 1e-3f, "in front: top");
            Assert.AreEqual(270f, DamageCues.MarkerAngle(0f, 0f, -1f), 1e-3f, "behind: bottom");
            Assert.AreEqual(0f, DamageCues.MarkerAngle(1f, 0f, 0f), 1e-3f, "right");
            Assert.AreEqual(180f, DamageCues.MarkerAngle(-1f, 0f, 0f), 1e-3f, "left");
            Assert.AreEqual(135f, DamageCues.MarkerAngle(-1f, 0.3f, 1f), 1e-3f, "front left, a little above");
        }

        [Test]
        public void Marker_FromStraightAboveOrBelow_PointsUpOrDown()
        {
            Assert.AreEqual(90f, DamageCues.MarkerAngle(0.05f, 1f, 0f), 4f);
            Assert.AreEqual(270f, DamageCues.MarkerAngle(0f, -1f, 0.1f), 1e-3f);
            Assert.AreEqual(270f, DamageCues.MarkerAngle(0f, 0f, 0f), 1e-3f, "no direction at all");
            Assert.AreEqual(270f, DamageCues.MarkerAngle(float.NaN, 0f, 0f), 1e-3f);
        }

        [Test]
        public void Marker_HoldsThenFades_AndIsGoneAfterItsTime()
        {
            Assert.AreEqual(1f, DamageCues.MarkerAlpha(0f));
            Assert.AreEqual(1f, DamageCues.MarkerAlpha(0.3f));
            float mid = DamageCues.MarkerAlpha(0.7f);
            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f);
            Assert.AreEqual(0f, DamageCues.MarkerAlpha(DamageCues.MarkerSeconds));
            Assert.AreEqual(0f, DamageCues.MarkerAlpha(-0.1f));
            float last = 1f;
            for (float t = 0f; t < DamageCues.MarkerSeconds; t += 0.01f)
            {
                float a = DamageCues.MarkerAlpha(t);
                Assert.LessOrEqual(a, last + 1e-6f);
                last = a;
            }
        }

        [Test]
        public void Glow_IsStrongerForAHeavierHit_AndFades()
        {
            Assert.AreEqual(0f, DamageCues.GlowStrength(0f));
            Assert.Less(DamageCues.GlowStrength(4f), DamageCues.GlowStrength(20f));
            Assert.AreEqual(1f, DamageCues.GlowStrength(100f));
            Assert.GreaterOrEqual(DamageCues.GlowStrength(0.1f), 0.35f, "even a graze is seen");
            Assert.AreEqual(0.8f, DamageCues.GlowFade(0.8f, 0f), 1e-6f);
            Assert.AreEqual(0f, DamageCues.GlowFade(1f, DamageCues.GlowSeconds));
        }

        [Test]
        public void EdgeShares_LightTheSideTheMarkerPointsTo()
        {
            var s = new float[4];
            DamageCues.EdgeShares(0f, s);
            Assert.AreEqual(1f, s[0], 1e-5f);
            Assert.AreEqual(0f, s[1] + s[2] + s[3], 1e-5f);
            DamageCues.EdgeShares(225f, s);
            Assert.AreEqual(0f, s[0]);
            Assert.AreEqual(0f, s[1]);
            Assert.AreEqual(0.7071f, s[2], 1e-3f);
            Assert.AreEqual(0.7071f, s[3], 1e-3f);
        }

        [Test]
        public void Cracks_MatchThePlatesLost_AndCloseAsTheSuitRecovers()
        {
            Assert.AreEqual(0, DamageCues.Cracks(1f));
            Assert.AreEqual(0, DamageCues.Cracks(0.95f), "the first plate is worn, not lost");
            Assert.AreEqual(1, DamageCues.Cracks(0.9f));
            Assert.AreEqual(3, DamageCues.Cracks(0.62f));
            Assert.AreEqual(DamageCues.MaxCracks, DamageCues.Cracks(0.05f));
            Assert.AreEqual(DamageCues.MaxCracks, DamageCues.Cracks(0f));
            for (float f = 0f; f <= 1f; f += 0.01f)
                Assert.AreEqual(System.Math.Min(DamageCues.MaxCracks, VisorReadout.Plates - VisorReadout.PlatesLeft(f)), DamageCues.Cracks(f));
        }

        [Test]
        public void HurtSound_IsPannedTowardTheShot_AndNotRepeatedInABurst()
        {
            Assert.Greater(DamageCues.SoundPan(0f), 0f);
            Assert.Less(DamageCues.SoundPan(180f), 0f);
            Assert.AreEqual(0f, DamageCues.SoundPan(90f), 1e-3f);
            Assert.LessOrEqual(System.Math.Abs(DamageCues.SoundPan(0f)), 0.6f);
            Assert.IsTrue(DamageCues.SoundAllowed(10f, -100f));
            Assert.IsFalse(DamageCues.SoundAllowed(10.05f, 10f));
            Assert.IsTrue(DamageCues.SoundAllowed(10.2f, 10f));
        }
    }
}
