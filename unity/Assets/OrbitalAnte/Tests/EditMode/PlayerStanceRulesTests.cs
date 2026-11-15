using NUnit.Framework;
using OrbitalAnte.Player;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests.EditMode
{
    public sealed class PlayerStanceRulesTests
    {
        [Test]
        public void HoldingCrouchAcrossTakeoffDoesNotFireADownwardJet()
        {
            var latch = new DownJetLatch();
            Assert.IsFalse(latch.Read(true, true));
            Assert.IsFalse(latch.Read(false, true), "jumping with crouch held must not cancel the jump with jets");
            Assert.IsFalse(latch.Read(false, true), "waiting in the air is not a fresh press");
            Assert.IsFalse(latch.Read(false, false));
            Assert.IsTrue(latch.Read(false, true), "a deliberate new airborne press still descends");
            Assert.IsFalse(latch.Read(true, true), "landing changes the control back to crouch");
            Assert.IsFalse(latch.Read(false, true));
        }

        [Test]
        public void CrouchNeverMakesAnInvalidCapsule_AndOpposingLeansCancel()
        {
            Assert.AreEqual(0.7f, StanceRules.CrouchHeight(0f, 1.8f, 0.35f));
            Assert.AreEqual(1.8f, StanceRules.CrouchHeight(4f, 1.8f, 0.35f));
            Assert.AreEqual(1.8f, StanceRules.CrouchHeight(float.NaN, 1.8f, 0.35f));
            Assert.AreEqual(0, StanceRules.Lean(true, true));
        }

        [Test]
        public void EffectiveHeightDeterminesEyeHeightWhileAStandIsBlocked()
        {
            Assert.AreEqual(1.02f, StanceRules.EyeHeight(1.15f, 1.8f, 1.62f, 1.15f, 1.02f), 0.0001f);
            Assert.AreEqual(1.62f, StanceRules.EyeHeight(1.8f, 1.8f, 1.62f, 1.15f, 1.02f), 0.0001f);
            Assert.LessOrEqual(StanceRules.EyeHeight(1.15f, 1.8f, 1.62f, 1.15f, 5f), 1.15f);
        }

        [Test]
        public void APeekStopsAtTheInsideOfTheCurvedCabin()
        {
            var from = new P3(1.5f, 1.62f, 0f);
            var desired = new P3(3f, 1.62f, 0f);
            P3 clipped = CabinHeadClearance.Clip(from, desired, 0.22f);
            Assert.IsTrue(CabinHeadClearance.Contains(from, 0.22f));
            Assert.IsTrue(CabinHeadClearance.Contains(clipped, 0.22f));
            Assert.Greater(clipped.X, from.X);
            Assert.Less(clipped.X, 2.5f - 0.22f);
        }

        [Test]
        public void ACameraCannotCrossTheSeatEvenIfTheEndpointIsClear()
        {
            var from = new P3(-1f, 1.3f, 1.2f);
            var desired = new P3(1f, 1.3f, 1.2f);
            Assert.IsTrue(CabinHeadClearance.Contains(desired, 0.15f));
            P3 clipped = CabinHeadClearance.Clip(from, desired, 0.15f);
            Assert.Less(clipped.X, -0.56f);
            Assert.IsTrue(CabinHeadClearance.Contains(clipped, 0.15f));
        }

        [Test]
        public void StandingUnderTheDomeRequiresTheWholeCapsuleToFit()
        {
            var feet = new P3(1.9f, 0.5f, 0f);
            Assert.IsTrue(CabinHeadClearance.Fits(feet, 1.15f, ShipLayout.PersonRadius));
            Assert.IsFalse(CabinHeadClearance.Fits(feet, ShipLayout.PersonHeight, ShipLayout.PersonRadius));
            var from = new P3(-1f, 1.2f, 0f);
            P3 head = CabinHeadClearance.Clip(from, new P3(-1f, 3f, 0f), 0.22f);
            Assert.IsTrue(CabinHeadClearance.Contains(head, 0.22f));
            Assert.Less(head.Y, ShipLayout.CeilingPeak - 0.22f);
        }

        [Test]
        public void CabinWalkerUsesEffectiveCrouchHeightWhenJumpingBelowTheDome()
        {
            var walker = new InteriorWalker { Height = 1.15f, JumpHeight = 0.45f };
            walker.Place(-1.8f, 0f);
            float peak = 0f;
            for (int i = 0; i < 240; i++)
            {
                walker.Step(1f / 240f, 0f, 0f, i == 0, false);
                peak = System.Math.Max(peak, walker.Y);
                Assert.LessOrEqual(walker.Y + walker.Height, ShipLayout.CeilingAt(2f) + 0.0001f);
            }
            Assert.Greater(peak, 0.35f, "a crouched head should not use the standing ceiling limit");
        }
    }
}
