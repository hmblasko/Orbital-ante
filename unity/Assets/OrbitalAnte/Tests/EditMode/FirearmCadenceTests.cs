using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Weapons;

namespace OrbitalAnte.Tests
{
    /// <summary>Cadence checks use elapsed game time, with no Unity clock or objects.</summary>
    public class FirearmCadenceTests
    {
        static FirearmSpec Rifle(float rate = 10f)
        {
            return new FirearmSpec
            {
                Automatic = true, ShotsPerSecond = rate, MagazineSize = 200,
                ReloadSeconds = 0.5f, BaseSpread = 0.35f, SpreadPerShot = 0.22f,
                MaxBloom = 2.6f, BloomRecovery = 7f,
                OneHandSpreadMultiplier = 4f, OneHandRecoilMultiplier = 2.5f,
                OneHandReloadMultiplier = 2.2f, OneHandCanReload = true,
            };
        }

        static IEnumerable RatesAndFrames()
        {
            foreach (float rate in new[] { 5.5f, 7.3f, 10f, 11.5f, 15f })
                foreach (int fps in new[] { 30, 40, 60, 120 })
                    yield return new TestCaseData(rate, fps);
        }

        static List<double> HeldShots(FirearmSpec spec, float[] steps, int hands, out List<float> spreads)
        {
            var gun = new FirearmState(spec);
            var times = new List<double> { 0d };
            FireResult first = gun.TryFire(hands, true, true);
            Assert.IsTrue(first.Fired);
            spreads = new List<float> { first.SpreadDegrees };
            double elapsed = 0d;
            for (int i = 0; elapsed < 5d; i++)
            {
                float dt = steps[i % steps.Length];
                elapsed += dt;
                gun.Tick(dt);
                FireResult shot = gun.TryFire(hands, true, false);
                if (shot.Fired)
                {
                    times.Add(elapsed);
                    spreads.Add(shot.SpreadDegrees);
                }
                Assert.That(gun.Cooldown, Is.GreaterThanOrEqualTo(0f));
                Assert.IsFalse(gun.TryFire(hands, true, false).Fired, "one shot per time step");
            }
            Assert.That(Math.Abs((times.Count - 1) - elapsed * spec.ShotsPerSecond), Is.LessThanOrEqualTo(1.00001d));
            return times;
        }

        static void CheckDeadlines(List<double> times, float rate, double longestFrame)
        {
            for (int shot = 1; shot < times.Count; shot++)
            {
                double due = shot / (double)rate;
                Assert.That(times[shot], Is.GreaterThanOrEqualTo(due - 0.000001d), "no early shot " + shot);
                Assert.That(times[shot] - due, Is.LessThanOrEqualTo(longestFrame + 0.000001d), "no accumulated delay at shot " + shot);
            }
        }

        [TestCaseSource(nameof(RatesAndFrames))]
        public void HeldAutomatic_FollowsConfiguredDeadlines(float rate, int fps)
        {
            List<float> spreads;
            List<double> times = HeldShots(Rifle(rate), new[] { 1f / fps }, 2, out spreads);
            CheckDeadlines(times, rate, 1d / fps);
        }

        [TestCase(7.3f)]
        [TestCase(10f)]
        [TestCase(15f)]
        public void UnevenFrames_PreserveCadenceAndSpreadAtEachShot(float rate)
        {
            FirearmSpec spec = Rifle(rate);
            List<float> reference, uneven;
            HeldShots(spec, new[] { 1f / 300f }, 2, out reference);
            float[] pattern = { 1f / 120f, 1f / 30f, 1f / 40f, 1f / 60f };
            List<double> times = HeldShots(spec, pattern, 2, out uneven);
            CheckDeadlines(times, rate, 1d / 30d);
            for (int i = 0; i < Math.Min(reference.Count, uneven.Count); i++)
                Assert.AreEqual(reference[i], uneven[i], 0.00001f, "spread at shot " + i);
        }

        [TestCase(30)]
        [TestCase(40)]
        [TestCase(60)]
        [TestCase(120)]
        public void RifleSpreadAtShot_IsIndependentOfFrameRate(int fps)
        {
            List<float> reference, sampled;
            HeldShots(Rifle(), new[] { 0.01f }, 2, out reference);
            HeldShots(Rifle(), new[] { 1f / fps }, 2, out sampled);
            for (int i = 0; i < Math.Min(reference.Count, sampled.Count); i++)
                Assert.AreEqual(reference[i], sampled[i], 0.00001f, "spread at shot " + i);
        }

        [Test]
        public void OneHandAndTwoHands_UseTheSameCadenceWithExistingPenalties()
        {
            List<float> twoHandSpread, oneHandSpread;
            List<double> two = HeldShots(Rifle(11.5f), new[] { 0.025f }, 2, out twoHandSpread);
            List<double> one = HeldShots(Rifle(11.5f), new[] { 0.025f }, 1, out oneHandSpread);
            CollectionAssert.AreEqual(two, one, "the shared player/enemy state has no rate advantage for either hand count");
            for (int i = 0; i < twoHandSpread.Count; i++)
                Assert.AreEqual(twoHandSpread[i] * 4f, oneHandSpread[i], 0.00001f);
        }

        [TestCase(30)]
        [TestCase(40)]
        [TestCase(60)]
        [TestCase(120)]
        public void PistolDefaultRate_HasNoFloatBoundaryMisses_AndHoldingDoesNotFire(int fps)
        {
            FirearmSpec spec = Rifle(5f);
            spec.Automatic = false;
            var gun = new FirearmState(spec);
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
            for (int pull = 0; pull < 10; pull++)
            {
                for (int frame = 1; frame <= fps / 5; frame++)
                {
                    gun.Tick(1f / fps);
                    Assert.IsFalse(gun.TryFire(2, true, false).Fired);
                }
                Assert.IsTrue(gun.TryFire(2, true, true).Fired, "fresh pull " + pull);
                Assert.IsFalse(gun.TryFire(2, true, true).Fired, "same-time pull cannot fire twice");
            }
        }

        [Test]
        public void NonDivisibleSemiautomaticRate_NeverBorrowsTimeFromFreshPulls()
        {
            FirearmSpec spec = Rifle(5.5f);
            spec.Automatic = false;
            var gun = new FirearmState(spec);
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
            double sinceShot = 0d;
            for (int frame = 0; frame < 200; frame++)
            {
                gun.Tick(0.025f);
                sinceShot += 0.025f;
                if (!gun.TryFire(2, true, true).Fired) continue;
                Assert.That(sinceShot, Is.GreaterThanOrEqualTo(1d / spec.ShotsPerSecond));
                sinceShot = 0d;
            }
        }

        [TestCase("release")]
        [TestCase("new pull")]
        [TestCase("no hands")]
        [TestCase("hands changed")]
        [TestCase("cancel reload or drop")]
        public void BurstInterruption_DiscardsFractionalDebtAndKeepsQuietRecovery(string interruption)
        {
            var gun = new FirearmState(Rifle());
            gun.TryFire(2, true, true);
            gun.Tick(0.08f);
            if (interruption == "release") gun.TryFire(2, false, false);
            else if (interruption == "new pull") gun.TryFire(2, true, true);
            else if (interruption == "no hands") gun.TryFire(0, true, false);
            else if (interruption == "hands changed") gun.HandsChanged(0);
            else gun.CancelReload();
            gun.Tick(0.045f);
            FireResult next = gun.TryFire(2, true, false);
            Assert.IsTrue(next.Fired);
            Assert.AreEqual(0.35f + 0.22f - 7f * 0.025f, next.SpreadDegrees, 0.00001f);
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f, "new burst starts a full interval");
        }

        [Test]
        public void FreshPullAfterDueTime_KeepsActualQuietRecovery()
        {
            var gun = new FirearmState(Rifle());
            gun.TryFire(2, true, true);
            gun.Tick(0.125f);
            Assert.AreEqual(0f, gun.Cooldown);
            FireResult next = gun.TryFire(2, true, true);
            Assert.IsTrue(next.Fired);
            Assert.AreEqual(0.395f, next.SpreadDegrees, 0.00001f);
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f);
        }

        [Test]
        public void PressWithoutAHeldTrigger_CannotEstablishAutomaticContinuity()
        {
            var gun = new FirearmState(Rifle());
            Assert.IsTrue(gun.TryFire(2, false, true).Fired);
            gun.Tick(0.125f);
            FireResult next = gun.TryFire(2, true, false);
            Assert.IsTrue(next.Fired);
            Assert.AreEqual(0.395f, next.SpreadDegrees, 0.00001f);
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f);
        }

        [Test]
        public void FailedReload_DoesNotInterruptAValidHeldBurst()
        {
            var gun = new FirearmState(Rifle());
            gun.TryFire(2, true, true);
            gun.Tick(0.125f);
            Assert.IsFalse(gun.StartReload(0));
            FireResult next = gun.TryFire(2, true, false);
            Assert.IsTrue(next.Fired);
            Assert.AreEqual(0.57f, next.SpreadDegrees, 0.00001f);
            Assert.AreEqual(0.075f, gun.Cooldown, 0.000001f);
        }

        [Test]
        public void LosingOnlyTheSpareHand_KeepsCadenceAndAppliesExistingPenalty()
        {
            var gun = new FirearmState(Rifle());
            gun.TryFire(2, true, true);
            gun.Tick(0.125f);
            gun.HandsChanged(1);
            FireResult next = gun.TryFire(1, true, false);
            Assert.IsTrue(next.Fired);
            Assert.AreEqual(0.57f * 4f, next.SpreadDegrees, 0.00001f);
            Assert.AreEqual(2.5f, next.RecoilScale);
            Assert.AreEqual(0.075f, gun.Cooldown, 0.000001f);
        }

        [Test]
        public void LongIdle_HasNoDebtOrRestoredBloom()
        {
            var gun = new FirearmState(Rifle());
            gun.TryFire(2, true, true);
            gun.Tick(5f);
            Assert.AreEqual(0f, gun.Bloom);
            Assert.IsTrue(gun.TryFire(2, true, false).Fired);
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f);
            gun.Tick(0.001f);
            Assert.IsFalse(gun.TryFire(2, true, false).Fired);
        }

        [TestCase(0.199f, true)]
        [TestCase(0.2f, false)]
        [TestCase(0.201f, false)]
        [TestCase(2f, false)]
        public void MissingAWholeInterval_DiscardsCatchUpAndKeepsRecovery(float dt, bool fractional)
        {
            FirearmSpec spec = Rifle();
            spec.SpreadPerShot = 2f;
            spec.BloomRecovery = 1f;
            var gun = new FirearmState(spec);
            gun.TryFire(2, true, true);
            gun.Tick(dt);
            FireResult next = gun.TryFire(2, true, false);
            Assert.IsTrue(next.Fired);
            Assert.AreEqual(fractional ? 2.35f : 0.35f + Math.Max(0f, 2f - (dt - 0.1f)), next.SpreadDegrees, 0.00001f);
            Assert.AreEqual(fractional ? 0.2f - dt : 0.1f, gun.Cooldown, 0.000001f);
            Assert.IsFalse(gun.TryFire(2, true, false).Fired);
            if (!fractional)
            {
                gun.Tick(0.05f);
                Assert.IsFalse(gun.TryFire(2, true, false).Fired, "no catch-up on the next short frame");
            }
        }

        [Test]
        public void TickPartitioning_PreservesFractionalLatenessAndBloom()
        {
            var whole = new FirearmState(Rifle());
            var split = new FirearmState(Rifle());
            whole.TryFire(2, true, true);
            split.TryFire(2, true, true);
            whole.Tick(0.125f);
            for (int i = 0; i < 5; i++) split.Tick(0.025f);
            FireResult a = whole.TryFire(2, true, false);
            FireResult b = split.TryFire(2, true, false);
            Assert.IsTrue(a.Fired && b.Fired);
            Assert.AreEqual(a.SpreadDegrees, b.SpreadDegrees, 0.000001f);
            Assert.AreEqual(whole.Cooldown, split.Cooldown, 0.000001f);
        }

        [Test]
        public void SplitTicksMissingAWholeInterval_DoNotReviveTheOldBurst()
        {
            var gun = new FirearmState(Rifle());
            gun.TryFire(2, true, true);
            for (int i = 0; i < 4; i++) gun.Tick(0.0625f);
            FireResult next = gun.TryFire(2, true, false);
            Assert.IsTrue(next.Fired);
            Assert.AreEqual(0.35f, next.SpreadDegrees);
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f);
        }

        [Test]
        public void ExactlyOneWholeMissedInterval_DropsDebt()
        {
            var gun = new FirearmState(Rifle(8f));
            gun.TryFire(2, true, true);
            gun.Tick(0.25f); // Both intervals are exact binary fractions.
            Assert.IsTrue(gun.TryFire(2, true, false).Fired);
            Assert.AreEqual(0.125f, gun.Cooldown);
        }

        [Test]
        public void ReloadCompletion_KeepsRecoveryAndStartsANewBurst()
        {
            var gun = new FirearmState(Rifle());
            gun.TryFire(2, true, true);
            gun.Tick(0.125f);
            Assert.IsTrue(gun.StartReload(2));
            gun.Tick(0.5f);
            Assert.IsFalse(gun.Reloading);
            FireResult shot = gun.TryFire(2, true, false);
            Assert.IsTrue(shot.Fired);
            Assert.AreEqual(0.35f, shot.SpreadDegrees);
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f);
        }

        [Test]
        public void InstantReloadAfterLastRound_CannotFireAgainWithoutTimeAdvancing()
        {
            FirearmSpec spec = Rifle();
            spec.MagazineSize = 1;
            spec.ReloadSeconds = 0f;
            var gun = new FirearmState(spec);
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
            Assert.IsTrue(gun.StartReload(2));
            Assert.IsFalse(gun.TryFire(2, true, false).Fired);
            gun.Tick(0.125f);
            Assert.IsTrue(gun.TryFire(2, true, false).Fired);
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f);
        }

        [Test]
        public void InstantReloadAfterPartialMagazine_DiscardsOldLatenessAndRefund()
        {
            FirearmSpec spec = Rifle();
            spec.ReloadSeconds = 0f;
            var gun = new FirearmState(spec);
            gun.TryFire(2, true, true);
            gun.Tick(0.125f);
            Assert.IsTrue(gun.StartReload(2));
            FireResult next = gun.TryFire(2, true, false);
            Assert.IsTrue(next.Fired);
            Assert.AreEqual(0.395f, next.SpreadDegrees, 0.00001f);
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f);
        }

        [Test]
        public void CancelledReload_KeepsItsQuietRecovery()
        {
            var gun = new FirearmState(Rifle());
            gun.TryFire(2, true, true);
            gun.Tick(0.08f);
            gun.StartReload(2);
            gun.Tick(0.045f);
            gun.CancelReload();
            FireResult next = gun.TryFire(2, true, false);
            Assert.IsTrue(next.Fired);
            Assert.AreEqual(0.395f, next.SpreadDegrees, 0.00001f);
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f);
        }

        [Test]
        public void EmptyGun_DryFiresOnlyOnAPressAndDoesNotEarnDebt()
        {
            var gun = new FirearmState(Rifle(), 1);
            gun.TryFire(2, true, true);
            gun.Tick(0.125f);
            Assert.IsFalse(gun.TryFire(2, true, false).DryFire);
            Assert.IsTrue(gun.TryFire(2, true, true).DryFire);
            Assert.AreEqual(0f, gun.Cooldown);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidOrNonpositiveTime_DoesNotRearmTheSameOpportunity(float dt)
        {
            var gun = new FirearmState(Rifle(float.MaxValue));
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
            Assert.IsFalse(gun.Tick(dt));
            Assert.IsFalse(gun.TryFire(2, true, false).Fired);
            gun.Tick(0.001f);
            Assert.IsTrue(gun.TryFire(2, true, false).Fired);
            Assert.IsFalse(gun.TryFire(2, true, false).Fired);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void NonfiniteFireRate_IsRejected(float rate)
        {
            Assert.Throws<ArgumentException>(() => new FirearmState(Rifle(rate)));
        }
    }
}
