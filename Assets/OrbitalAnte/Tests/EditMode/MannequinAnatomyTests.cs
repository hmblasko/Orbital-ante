using System.Linq;
using NUnit.Framework;
using OrbitalAnte.Characters;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Tests
{
    public class MannequinAnatomyTests
    {
        static PartSet Without(params BodyPartId[] ids)
        {
            var s = PartSet.All;
            foreach (var id in ids) s = s.Without(id);
            return s;
        }

        [Test]
        public void HasFourteenParts()
        {
            Assert.AreEqual(14, MannequinAnatomy.AllParts.Length);
            Assert.AreEqual(14, PartSet.All.CountSet());
        }

        [Test]
        public void LosingThighTakesShinAndFoot()
        {
            var lost = MannequinAnatomy.PartsLostWhenBroken(BodyPartId.ThighL, PartSet.All);
            CollectionAssert.AreEquivalent(new[] { BodyPartId.ThighL, BodyPartId.ShinL, BodyPartId.FootL }, lost);
            Assert.AreEqual(BodyPartId.ThighL, lost[0], "chain is listed parent first");
        }

        [Test]
        public void LosingUpperArmTakesForearmAndHand()
        {
            var lost = MannequinAnatomy.PartsLostWhenBroken(BodyPartId.UpperArmR, PartSet.All);
            CollectionAssert.AreEquivalent(new[] { BodyPartId.UpperArmR, BodyPartId.ForearmR, BodyPartId.HandR }, lost);
        }

        [Test]
        public void AlreadyLostChildrenAreNotLostAgain()
        {
            var attached = Without(BodyPartId.FootL);
            var lost = MannequinAnatomy.PartsLostWhenBroken(BodyPartId.ThighL, attached);
            CollectionAssert.AreEquivalent(new[] { BodyPartId.ThighL, BodyPartId.ShinL }, lost);
        }

        [Test]
        public void TorsoTakesLimbsButNeverTheHead()
        {
            var lost = MannequinAnatomy.PartsLostWhenBroken(BodyPartId.Torso, PartSet.All);
            Assert.AreEqual(13, lost.Count);
            Assert.IsFalse(lost.Contains(BodyPartId.Head));
            Assert.AreEqual(BodyPartId.Torso, lost[0]);
        }

        [Test]
        public void EveryChildIsListedAfterItsParent()
        {
            var lost = MannequinAnatomy.PartsLostWhenBroken(BodyPartId.Torso, PartSet.All);
            for (int i = 0; i < lost.Count; i++)
            {
                var parent = MannequinAnatomy.Parent(lost[i]);
                if (parent == null) continue;
                Assert.Less(lost.IndexOf(parent.Value), i, $"{parent} must come before {lost[i]}");
            }
        }

        [Test]
        public void MovementStatesFollowWhatIsLeft()
        {
            Assert.AreEqual(LocomotionState.Normal, MannequinAnatomy.Evaluate(PartSet.All));
            Assert.AreEqual(LocomotionState.Limp, MannequinAnatomy.Evaluate(Without(BodyPartId.FootR)));
            Assert.AreEqual(LocomotionState.Limp, MannequinAnatomy.Evaluate(Without(BodyPartId.ThighL, BodyPartId.ShinL, BodyPartId.FootL)));

            var noLegs = Without(BodyPartId.ThighL, BodyPartId.ShinL, BodyPartId.FootL, BodyPartId.ThighR, BodyPartId.ShinR, BodyPartId.FootR);
            Assert.AreEqual(LocomotionState.Crawl, MannequinAnatomy.Evaluate(noLegs));

            var oneArm = noLegs.Without(BodyPartId.UpperArmL).Without(BodyPartId.ForearmL).Without(BodyPartId.HandL);
            Assert.AreEqual(LocomotionState.Crawl, MannequinAnatomy.Evaluate(oneArm), "one arm still crawls");

            var noLimbs = oneArm.Without(BodyPartId.UpperArmR).Without(BodyPartId.ForearmR).Without(BodyPartId.HandR);
            Assert.AreEqual(LocomotionState.Wriggle, MannequinAnatomy.Evaluate(noLimbs));

            var headOnly = PartSet.None.With(BodyPartId.Head);
            Assert.AreEqual(LocomotionState.HeadOnly, MannequinAnatomy.Evaluate(headOnly));
            Assert.AreEqual(LocomotionState.Dead, MannequinAnatomy.Evaluate(PartSet.All.Without(BodyPartId.Head)));
        }

        [Test]
        public void OnlyTheHeadKills()
        {
            foreach (var id in MannequinAnatomy.AllParts)
            {
                if (id == BodyPartId.Head) continue;
                var after = PartSet.All.Without(new PartSet { Bits = ToBits(MannequinAnatomy.PartsLostWhenBroken(id, PartSet.All)) });
                Assert.AreNotEqual(LocomotionState.Dead, MannequinAnatomy.Evaluate(after), $"losing {id} must not kill");
            }
        }

        [Test]
        public void ShootingNeedsAHandAndATorso()
        {
            Assert.IsTrue(MannequinAnatomy.CanShoot(PartSet.All));
            Assert.IsTrue(MannequinAnatomy.CanShoot(Without(BodyPartId.HandL)));
            Assert.IsFalse(MannequinAnatomy.CanShoot(Without(BodyPartId.HandL, BodyPartId.HandR)));
            Assert.IsFalse(MannequinAnatomy.CanShoot(PartSet.None.With(BodyPartId.Head)), "a lone head cannot shoot");
            Assert.AreEqual(1, MannequinAnatomy.UsableHands(Without(BodyPartId.ForearmR)));
        }

        [Test]
        public void LimbsPopInOneToThreeRifleHits()
        {
            const float rifleDamage = 34f; // DebugGunConfig default, mirrors the AK spoof target
            var cfg = ScriptableObject.CreateInstance<MannequinConfig>();
            foreach (var id in MannequinAnatomy.AllParts)
            {
                if (id == BodyPartId.Torso) continue;
                int hits = Mathf.CeilToInt(cfg.Tuning(id).maxHp / rifleDamage);
                Assert.That(hits, Is.InRange(1, 3), $"{id} takes {hits} hits");
            }
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void SeededStreamsAreRepeatable()
        {
            RunSeed.Set(12345);
            var a = RunSeed.CreateStream("arena.dummies");
            var b = RunSeed.CreateStream("arena.dummies");
            var c = RunSeed.CreateStream("something.else");
            float a1 = a.NextFloat(), b1 = b.NextFloat(), c1 = c.NextFloat();
            Assert.AreEqual(a1, b1);
            Assert.AreNotEqual(a1, c1);
            for (int i = 0; i < 1000; i++)
            {
                float f = a.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        static ushort ToBits(System.Collections.Generic.IEnumerable<BodyPartId> ids) =>
            (ushort)ids.Aggregate(0, (acc, id) => acc | (1 << (int)id));
    }
}
