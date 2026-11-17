using NUnit.Framework;
using OrbitalAnte.Combat;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;

namespace OrbitalAnte.Tests
{
    /// <summary>The rare one-hit gun's rules with no Unity objects: one hit kills, the blow-up chance, where it lies.</summary>
    public class RareGunRulesTests
    {
        static BodyModel Body()
        {
            var hp = new float[BodyLayout.Count];
            for (int i = 0; i < hp.Length; i++) hp[i] = 40f;
            hp[(int)BodyPartId.Torso] = 200f;
            hp[(int)BodyPartId.Head] = 120f;
            return new BodyModel(hp);
        }

        [Test]
        public void ALethalHit_OnAHand_KillsTheCharacter_AndTheHandStillComesOff()
        {
            var body = Body();
            DamageResult r = body.ApplyLethal(BodyPartId.LeftHand, 60f);
            Assert.IsTrue(r.Killed);
            Assert.IsFalse(body.Alive);
            Assert.IsTrue(r.Detachments.Exists(d => d.Root == BodyPartId.LeftHand), "the hand flies off");
            Assert.IsTrue(r.Detachments.Exists(d => d.Root == BodyPartId.Head), "and the head goes too");
        }

        [Test]
        public void ALethalHit_ThatDoesTooLittleDamageToSeverAnything_StillKills()
        {
            var body = Body();
            DamageResult r = body.ApplyLethal(BodyPartId.Torso, 1f);
            Assert.IsTrue(r.Killed);
        }

        [Test]
        public void ALethalHit_OnTheHead_KillsOnce()
        {
            var body = Body();
            DamageResult r = body.ApplyLethal(BodyPartId.Head, 500f);
            Assert.IsTrue(r.Killed);
            Assert.AreEqual(1, r.Detachments.FindAll(d => d.Root == BodyPartId.Head).Count);
        }

        [Test]
        public void ALethalHit_OnALoosePiece_OrACorpse_DoesNothingMore()
        {
            var body = Body();
            body.ApplyDamage(BodyPartId.LeftThigh, 100f); // the leg is off and loose
            DamageResult loose = body.ApplyLethal(BodyPartId.LeftFoot, 1f);
            Assert.IsFalse(loose.Killed, "shooting a severed foot does not kill its owner");
            Assert.IsTrue(body.Alive);

            body.ApplyDamage(BodyPartId.Head, 1000f);
            DamageResult dead = body.ApplyLethal(BodyPartId.Torso, 1f);
            Assert.IsFalse(dead.Killed, "a corpse is not killed again");
        }

        [Test]
        public void ANormalHit_IsUnchanged()
        {
            var body = Body();
            DamageResult r = body.ApplyDamage(BodyPartId.LeftHand, 60f);
            Assert.IsFalse(r.Killed);
            Assert.IsTrue(body.Alive);
        }

        [Test]
        public void TheBlowUp_HappensExactlyWhenTheRollIsUnderTheChance()
        {
            Assert.IsTrue(RareGuns.Backfires(0.0005, 0.001f));
            Assert.IsFalse(RareGuns.Backfires(0.0011, 0.001f));
            Assert.IsFalse(RareGuns.Backfires(0.5, 0.001f));
            Assert.IsFalse(RareGuns.Backfires(0.0, 0f), "a gun with no chance never blows up");
            Assert.IsFalse(RareGuns.Backfires(double.NaN, 0.001f));
        }

        [Test]
        public void OverManySeededShots_TheBlowUpRate_IsCloseTo1In1000()
        {
            var rng = new SeededRandom(2026);
            int blew = 0;
            const int shots = 400000;
            for (int i = 0; i < shots; i++) if (RareGuns.Backfires(rng.NextDouble(), 0.001f)) blew++;
            Assert.AreEqual(400, blew, 70, "about 400 in 400,000");
        }

        [Test]
        public void TheChanceIsSaidInPlainWords()
        {
            Assert.AreEqual("1 in 1,000", RareGuns.OneIn(0.001f));
            Assert.AreEqual("never", RareGuns.OneIn(0f));
            Assert.AreEqual(0.0952, RareGuns.ChanceWithin(100, 0.001f), 0.0005, "about 1 in 10 over a hundred shots");
        }

        [Test]
        public void AWorld_HoldsAtMostOne_AtTheFirstBaseThatQualifies()
        {
            var seeds = new System.Collections.Generic.List<int>();
            int first = -1;
            for (int seed = 0; seeds.Count < 40; seed++)
            {
                seeds.Add(seed);
                if (first < 0 && RareGuns.BaseHoldsOne(seed)) first = seeds.Count - 1;
            }
            Assert.AreEqual(first, RareGuns.PickBase(seeds));
            Assert.AreEqual(-1, RareGuns.PickBase(new int[0]));
            Assert.AreEqual(-1, RareGuns.PickBase(null));
        }

        [Test]
        public void AboutOneBaseInSix_HoldsTheRareGun_AndTheSameSeedAlwaysAgrees()
        {
            int holding = 0;
            for (int seed = 0; seed < 6000; seed++) if (RareGuns.BaseHoldsOne(seed)) holding++;
            Assert.AreEqual(1000, holding, 120);
            Assert.AreEqual(RareGuns.BaseHoldsOne(12345), RareGuns.BaseHoldsOne(12345));
        }
    }
}
