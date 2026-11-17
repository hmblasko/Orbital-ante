using NUnit.Framework;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>How a hit on an enemy is confirmed: the X round the crosshair and the ding.</summary>
    public class HitConfirmTests
    {
        static HitKind K(bool enemy = true, bool head = false, bool severed = false, bool disarmed = false,
            bool killed = false, bool headshot = false, bool hit = true)
            => HitConfirm.Classify(hit, enemy, head, severed, disarmed, killed, headshot);

        [Test]
        public void Classify_SaysTheBiggestThingAHitDid()
        {
            Assert.AreEqual(HitKind.None, K(hit: false));
            Assert.AreEqual(HitKind.Body, K());
            Assert.AreEqual(HitKind.Limb, K(severed: true));
            Assert.AreEqual(HitKind.Head, K(head: true));
            Assert.AreEqual(HitKind.Kill, K(killed: true, severed: true));
            Assert.AreEqual(HitKind.HeadshotKill, K(head: true, killed: true, headshot: true));
            Assert.AreEqual(HitKind.Disarm, K(enemy: false, disarmed: true), "the gun in their hands is hit, not the body");
        }

        [Test]
        public void OnlyLivingEnemiesAreConfirmed()
        {
            // A rock, the ship's wall, a loose limb, a corpse, a gun on the ground: no marker, no ding.
            Assert.AreEqual(HitKind.None, K(enemy: false));
            Assert.AreEqual(HitKind.None, K(enemy: false, severed: true), "splitting a loose chunk");
            Assert.IsNull(HitConfirm.Ding(HitKind.None));
            Assert.AreEqual(0f, HitConfirm.Seconds(HitKind.None));
        }

        [Test]
        public void EveryKindLooksAndSoundsDifferentFromTheBody()
        {
            Assert.AreEqual(MarkerTone.White, HitConfirm.Tone(HitKind.Body));
            Assert.AreEqual(MarkerTone.Gold, HitConfirm.Tone(HitKind.Head));
            Assert.AreEqual(MarkerTone.Cyan, HitConfirm.Tone(HitKind.Disarm));
            Assert.AreEqual(MarkerTone.Red, HitConfirm.Tone(HitKind.Kill));
            Assert.AreEqual(MarkerTone.Red, HitConfirm.Tone(HitKind.HeadshotKill));
            Assert.IsTrue(HitConfirm.OuterRing(HitKind.HeadshotKill));
            Assert.IsFalse(HitConfirm.OuterRing(HitKind.Kill));
            Assert.Greater(HitConfirm.TickLength(HitKind.Limb), HitConfirm.TickLength(HitKind.Body), "a limb off is a bigger X");

            Assert.AreEqual(HitConfirm.BodyDing, HitConfirm.Ding(HitKind.Body));
            Assert.AreEqual(HitConfirm.KillDing, HitConfirm.Ding(HitKind.Kill));
            Assert.AreEqual(HitConfirm.HeadshotDing, HitConfirm.Ding(HitKind.HeadshotKill));
            Assert.Greater(HitConfirm.DingPitch(HitKind.Head), HitConfirm.DingPitch(HitKind.Body));
            Assert.Greater(HitConfirm.DingPitch(HitKind.Disarm), HitConfirm.DingPitch(HitKind.Body));
            Assert.AreNotEqual(HitConfirm.DingPitch(HitKind.Head), HitConfirm.DingPitch(HitKind.Disarm));
            // Musical intervals over the hit bell's D: a fifth and a fourth.
            Assert.AreEqual(1.5f, HitConfirm.HeadPitch, 0.002f);
            Assert.AreEqual(4f / 3f, HitConfirm.DisarmPitch, 0.002f);
        }

        [Test]
        public void BiggerHitsShowLongerAndAreDrawnBigger()
        {
            HitKind[] order = { HitKind.Body, HitKind.Head, HitKind.Limb, HitKind.Kill, HitKind.HeadshotKill };
            for (int i = 1; i < order.Length; i++)
            {
                Assert.GreaterOrEqual(HitConfirm.Seconds(order[i]), HitConfirm.Seconds(order[i - 1]));
                Assert.GreaterOrEqual(HitConfirm.TickLength(order[i]), HitConfirm.TickLength(order[i - 1]));
            }
            // Short enough that a body hit has gone before the next shot of a slow gun, long enough to see.
            Assert.GreaterOrEqual(HitConfirm.Seconds(HitKind.Body), 0.15f);
            Assert.LessOrEqual(HitConfirm.Seconds(HitKind.Body), 0.3f);
        }

        [Test]
        public void Marker_PopsOutThenSettles_HoldsThenFades()
        {
            foreach (HitKind k in new[] { HitKind.Body, HitKind.Kill })
            {
                Assert.Greater(HitConfirm.Scale(k, 0f), 1.2f);
                Assert.AreEqual(1f, HitConfirm.Scale(k, HitConfirm.PopSeconds));
                Assert.AreEqual(1f, HitConfirm.Scale(k, 1f));
                Assert.AreEqual(1f, HitConfirm.Alpha(k, 0f));
                Assert.AreEqual(1f, HitConfirm.Alpha(k, HitConfirm.Hold(k)));
                Assert.AreEqual(0f, HitConfirm.Alpha(k, HitConfirm.Seconds(k)));
                Assert.AreEqual(0f, HitConfirm.Alpha(k, -0.01f));
                float lastA = 2f, lastS = 9f;
                for (float t = 0f; t < HitConfirm.Seconds(k); t += 0.005f)
                {
                    float a = HitConfirm.Alpha(k, t), s = HitConfirm.Scale(k, t);
                    Assert.LessOrEqual(a, lastA + 1e-6f);
                    Assert.LessOrEqual(s, lastS + 1e-6f);
                    lastA = a;
                    lastS = s;
                }
            }
            Assert.Greater(HitConfirm.Scale(HitKind.Kill, 0f), HitConfirm.Scale(HitKind.Body, 0f), "a kill pops out further");
        }

        [Test]
        public void ASmallerHit_NeverCutsAKillShort()
        {
            Assert.IsTrue(HitConfirm.Replaces(HitKind.None, 0f, HitKind.Body));
            Assert.IsTrue(HitConfirm.Replaces(HitKind.Body, 0.05f, HitKind.Body), "a burst keeps the X up");
            Assert.IsTrue(HitConfirm.Replaces(HitKind.Body, 0.05f, HitKind.Kill));
            Assert.IsFalse(HitConfirm.Replaces(HitKind.Kill, 0.05f, HitKind.Body));
            Assert.IsTrue(HitConfirm.Replaces(HitKind.Kill, HitConfirm.Hold(HitKind.Kill), HitKind.Body), "once it fades");
            Assert.IsTrue(HitConfirm.Replaces(HitKind.Kill, 5f, HitKind.Body));
            Assert.IsFalse(HitConfirm.Replaces(HitKind.Body, 0f, HitKind.None));
        }

        [Test]
        public void Ding_IsQuieterInABurst_ButAKillNeverIs()
        {
            Assert.AreEqual(1f, HitConfirm.DingVolume(HitKind.Body, 1f));
            Assert.AreEqual(1f, HitConfirm.DingVolume(HitKind.Body, 100f), "the first hit of a fight");
            Assert.AreEqual(HitConfirm.QuietestDing, HitConfirm.DingVolume(HitKind.Body, 0f), 1e-5f);
            float smg = HitConfirm.DingVolume(HitKind.Body, 1f / 12f);
            Assert.Greater(smg, HitConfirm.QuietestDing);
            Assert.Less(smg, 1f);
            Assert.AreEqual(1f, HitConfirm.DingVolume(HitKind.Kill, 0f));
            Assert.AreEqual(1f, HitConfirm.DingVolume(HitKind.HeadshotKill, 0f));
            Assert.GreaterOrEqual(HitConfirm.QuietestDing, 0.5f, "still heard in a burst");
        }
    }
}
