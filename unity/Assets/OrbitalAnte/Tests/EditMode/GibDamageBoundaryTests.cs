using System;
using NUnit.Framework;
using OrbitalAnte.Dismemberment;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Pure BodyModel edge cases for gib event inputs/results. GoreFx and the pools
    /// themselves require Unity; this fixture does not stand in for those tests.
    /// Test drafts only: not run in Unity or a standalone C# runner here.
    /// </summary>
    public class GibDamageBoundaryTests
    {
        static float[] Health(float hp = 100f)
        {
            var values = new float[BodyLayout.Count];
            for (int i = 0; i < values.Length; i++) values[i] = hp;
            return values;
        }

        [Test]
        public void Constructor_RejectsMissingHealthData()
        {
            Assert.Throws<ArgumentNullException>(() => new BodyModel(null));
        }

        [TestCase(0)]
        [TestCase(13)]
        [TestCase(15)]
        public void Constructor_RejectsAHealthListThatDoesNotCoverEveryPart(int count)
        {
            Assert.Throws<ArgumentException>(() => new BodyModel(new float[count]));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Constructor_RejectsANonpositiveHealthEntry_EvenAtTheLastPart(float invalidHp)
        {
            float[] values = Health();
            values[(int)BodyPartId.RightFoot] = invalidHp;
            Assert.Throws<ArgumentException>(() => new BodyModel(values));
        }

        [Test]
        public void Constructor_CopiesPerPartHealth_SoCallerEditsCannotRetuneALiveBody()
        {
            float[] values = Health();
            values[(int)BodyPartId.LeftShin] = 37f;
            var model = new BodyModel(values);
            values[(int)BodyPartId.LeftShin] = 1f;

            Assert.AreEqual(37f, model.Hp(BodyPartId.LeftShin));
            Assert.AreEqual(37f, model.MaxHp(BodyPartId.LeftShin));
            model.ApplyDamage(BodyPartId.LeftShin, 36f);
            Assert.IsTrue(model.OnBody(BodyPartId.LeftShin));
            Assert.AreEqual(1f, model.Hp(BodyPartId.LeftShin));
            Assert.AreEqual(37f, model.MaxHp(BodyPartId.LeftShin));
        }

        [Test]
        public void ExactRemainingHealth_DetachesOnce_AndReportsOnlyTheHealthActuallyLost()
        {
            var model = new BodyModel(Health());
            model.ApplyDamage(BodyPartId.LeftHand, 60f);
            DamageResult sever = model.ApplyDamage(BodyPartId.LeftHand, 40f);

            Assert.AreEqual(BodyPartId.LeftHand, sever.Part);
            Assert.AreEqual(40f, sever.DamageApplied);
            Assert.AreEqual(0f, sever.RemainingHp);
            Assert.AreEqual(1, sever.Detachments.Count);
            Assert.AreEqual(DetachReason.Severed, sever.Detachments[0].Reason);
            Assert.AreEqual(100f, model.MaxHp(BodyPartId.LeftHand));

            DamageResult repeated = model.ApplyDamage(BodyPartId.LeftHand, 40f);
            Assert.AreEqual(0f, repeated.DamageApplied);
            Assert.AreEqual(0f, repeated.RemainingHp);
            Assert.AreEqual(0, repeated.Detachments.Count, "A repeated hit must not spawn another detachment effect.");
        }

        [Test]
        public void Overkill_ClampsReportedDamageAndHp_WithoutChangingTheConfiguredMaximum()
        {
            var model = new BodyModel(Health());
            model.ApplyDamage(BodyPartId.RightFoot, 75f);
            DamageResult result = model.ApplyDamage(BodyPartId.RightFoot, 1000f);

            Assert.AreEqual(25f, result.DamageApplied);
            Assert.AreEqual(0f, result.RemainingHp);
            Assert.AreEqual(0f, model.Hp(BodyPartId.RightFoot));
            Assert.AreEqual(100f, model.MaxHp(BodyPartId.RightFoot));
            Assert.AreEqual(1, result.Detachments.Count);
            Assert.IsFalse(result.Killed);
        }

        [TestCase(0f)]
        [TestCase(-20f)]
        public void NonpositiveHitsOnALooseDescendant_DoNotHealOrSplitIt(float amount)
        {
            var model = new BodyModel(Health());
            model.ApplyDamage(BodyPartId.LeftShin, 25f);
            int group = model.ApplyDamage(BodyPartId.LeftThigh, 100f).Detachments[0].Group;

            DamageResult ignored = model.ApplyDamage(BodyPartId.LeftShin, amount);
            Assert.AreEqual(0f, ignored.DamageApplied);
            Assert.AreEqual(75f, ignored.RemainingHp);
            Assert.AreEqual(75f, model.Hp(BodyPartId.LeftShin));
            Assert.AreEqual(group, model.Group(BodyPartId.LeftShin));
            Assert.AreEqual(0, ignored.Detachments.Count);

            DamageResult split = model.ApplyDamage(BodyPartId.LeftShin, 75f);
            Assert.AreEqual(1, split.Detachments.Count);
            Assert.AreEqual(group, split.Detachments[0].SourceGroup);
        }

        [Test]
        public void AnEarlierDetachmentResult_RemainsASnapshotAfterItsChunkSplits()
        {
            var model = new BodyModel(Health());
            DamageResult first = model.ApplyDamage(BodyPartId.RightUpperArm, 100f);
            Detachment original = first.Detachments[0];

            model.ApplyDamage(BodyPartId.RightForearm, 100f);
            model.ApplyDamage(BodyPartId.RightHand, 100f);

            Assert.AreEqual(1, first.Detachments.Count);
            Assert.AreEqual(100f, first.DamageApplied);
            Assert.AreEqual(0f, first.RemainingHp);
            Assert.AreEqual(0, original.SourceGroup);
            CollectionAssert.AreEqual(
                new[] { BodyPartId.RightUpperArm, BodyPartId.RightForearm, BodyPartId.RightHand }, original.Members);
            Assert.AreEqual(original.Group, model.Group(BodyPartId.RightUpperArm));
            Assert.AreNotEqual(original.Group, model.Group(BodyPartId.RightForearm));
        }

        [Test]
        public void HitsOnAnExistingGibRoot_PreserveItsRemainingHealthAndCannotConsumeItsChildren()
        {
            var model = new BodyModel(Health());
            DamageResult killed = model.ApplyDamage(BodyPartId.Head, 100f);
            Assert.IsTrue(killed.Killed);
            int torsoGroup = model.Group(BodyPartId.Torso);

            DamageResult rootHit = model.ApplyDamage(BodyPartId.Torso, 1000f);
            Assert.AreEqual(0f, rootHit.DamageApplied);
            Assert.AreEqual(100f, rootHit.RemainingHp);
            Assert.AreEqual(0, rootHit.Detachments.Count);
            Assert.AreEqual(torsoGroup, model.Group(BodyPartId.RightUpperArm));

            DamageResult childHit = model.ApplyDamage(BodyPartId.RightUpperArm, 100f);
            Assert.AreEqual(1, childHit.Detachments.Count);
            Assert.AreEqual(torsoGroup, childHit.Detachments[0].SourceGroup);
            Assert.AreEqual(DetachReason.Severed, childHit.Detachments[0].Reason);
            Assert.IsFalse(childHit.Killed);
        }
    }
}
