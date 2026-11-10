using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OrbitalAnte.Dismemberment;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The Milestone 1 dismemberment rules, tested without any Unity objects.
    /// Run in Unity from Window > General > Test Runner > EditMode.
    /// </summary>
    public class BodyModelTests
    {
        const float Big = 100000f;

        static BodyModel NewModel(float hp = 100f)
        {
            return new BodyModel(Enumerable.Repeat(hp, BodyLayout.Count).ToArray());
        }

        static IEnumerable<BodyPartId> AllParts()
        {
            for (int i = 0; i < BodyLayout.Count; i++) yield return (BodyPartId)i;
        }

        [Test]
        public void HasFourteenParts_AllOnBodyAtFullHealth()
        {
            var model = NewModel();
            Assert.AreEqual(14, BodyLayout.Count);
            Assert.AreEqual(14, model.PartsOnBody);
            foreach (var id in AllParts())
            {
                Assert.IsTrue(model.OnBody(id), id.ToString());
                Assert.AreEqual(100f, model.Hp(id), id.ToString());
            }
            Assert.AreEqual(MovementState.Intact, model.State);
            Assert.IsTrue(model.CanSprint);
        }

        [Test]
        public void EveryPartExceptTorsoHasAParent_AndChainsLeadToTorso()
        {
            foreach (var id in AllParts())
            {
                if (id == BodyPartId.Torso)
                {
                    Assert.IsFalse(BodyLayout.HasParent(id));
                    continue;
                }
                var cursor = id;
                int steps = 0;
                while (BodyLayout.HasParent(cursor))
                {
                    cursor = BodyLayout.Parent(cursor);
                    Assert.Less(++steps, 5, "parent chain too long for " + id);
                }
                Assert.AreEqual(BodyPartId.Torso, cursor, id.ToString());
            }
        }

        [Test]
        public void DamageBelowHp_DetachesNothing()
        {
            var model = NewModel();
            var result = model.ApplyDamage(BodyPartId.LeftShin, 60f);
            Assert.AreEqual(0, result.Detachments.Count);
            Assert.AreEqual(40f, model.Hp(BodyPartId.LeftShin));
            Assert.IsTrue(model.OnBody(BodyPartId.LeftShin));
            Assert.AreEqual(MovementState.Intact, model.State);
        }

        [Test]
        public void LosingThigh_TakesShinAndFoot_AsOneChunk()
        {
            var model = NewModel();
            var result = model.ApplyDamage(BodyPartId.LeftThigh, Big);

            Assert.AreEqual(1, result.Detachments.Count);
            var chunk = result.Detachments[0];
            Assert.AreEqual(BodyPartId.LeftThigh, chunk.Root);
            CollectionAssert.AreEquivalent(
                new[] { BodyPartId.LeftThigh, BodyPartId.LeftShin, BodyPartId.LeftFoot }, chunk.Members);
            Assert.AreEqual(0, chunk.SourceGroup);
            Assert.AreEqual(DetachReason.Severed, chunk.Reason);

            Assert.IsFalse(model.OnBody(BodyPartId.LeftThigh));
            Assert.IsFalse(model.OnBody(BodyPartId.LeftShin));
            Assert.IsFalse(model.OnBody(BodyPartId.LeftFoot));
            Assert.AreEqual(11, model.PartsOnBody);
            Assert.IsTrue(model.SeparatedFromParent(BodyPartId.LeftThigh));
            Assert.IsFalse(model.SeparatedFromParent(BodyPartId.LeftShin));
        }

        [Test]
        public void LosingUpperArm_TakesForearmAndHand()
        {
            var model = NewModel();
            var result = model.ApplyDamage(BodyPartId.RightUpperArm, Big);
            CollectionAssert.AreEquivalent(
                new[] { BodyPartId.RightUpperArm, BodyPartId.RightForearm, BodyPartId.RightHand },
                result.Detachments.Single().Members);
            Assert.IsFalse(model.HandUsable(false));
            Assert.IsTrue(model.HandUsable(true));
        }

        [Test]
        public void LosingForearm_TakesHandOnly()
        {
            var model = NewModel();
            var result = model.ApplyDamage(BodyPartId.LeftForearm, Big);
            CollectionAssert.AreEquivalent(
                new[] { BodyPartId.LeftForearm, BodyPartId.LeftHand }, result.Detachments.Single().Members);
            Assert.IsTrue(model.OnBody(BodyPartId.LeftUpperArm));
            Assert.IsFalse(model.HandUsable(true));
        }

        [Test]
        public void LooseChunk_CanSplitAgain()
        {
            var model = NewModel();
            int thighGroup = model.ApplyDamage(BodyPartId.LeftThigh, Big).Detachments[0].Group;

            var split = model.ApplyDamage(BodyPartId.LeftShin, Big);
            Assert.AreEqual(1, split.Detachments.Count);
            Assert.AreEqual(BodyPartId.LeftShin, split.Detachments[0].Root);
            Assert.AreEqual(thighGroup, split.Detachments[0].SourceGroup);
            CollectionAssert.AreEquivalent(
                new[] { BodyPartId.LeftShin, BodyPartId.LeftFoot }, split.Detachments[0].Members);

            Assert.AreEqual(thighGroup, model.Group(BodyPartId.LeftThigh));
            Assert.AreNotEqual(thighGroup, model.Group(BodyPartId.LeftShin));
            Assert.AreEqual(model.Group(BodyPartId.LeftShin), model.Group(BodyPartId.LeftFoot));

            var again = model.ApplyDamage(BodyPartId.LeftFoot, Big);
            CollectionAssert.AreEquivalent(new[] { BodyPartId.LeftFoot }, again.Detachments.Single().Members);
        }

        [Test]
        public void ShootingTheTopOfALooseChunk_DoesNothingMore()
        {
            var model = NewModel();
            model.ApplyDamage(BodyPartId.LeftThigh, Big);
            var result = model.ApplyDamage(BodyPartId.LeftThigh, Big);
            Assert.AreEqual(0, result.Detachments.Count);
            Assert.IsFalse(result.Killed);
        }

        [Test]
        public void MovementStates_FollowWhatIsLeft()
        {
            var model = NewModel();
            Assert.AreEqual(MovementState.Intact, model.State);

            model.ApplyDamage(BodyPartId.LeftFoot, Big);
            Assert.AreEqual(MovementState.OneLeg, model.State, "a missing foot means that leg no longer works");
            Assert.IsFalse(model.CanSprint, "cannot sprint on one leg");

            model.ApplyDamage(BodyPartId.RightThigh, Big);
            Assert.AreEqual(MovementState.Crawl, model.State);

            model.ApplyDamage(BodyPartId.LeftUpperArm, Big);
            Assert.AreEqual(MovementState.Crawl, model.State, "one arm is enough to crawl");

            model.ApplyDamage(BodyPartId.RightUpperArm, Big);
            Assert.AreEqual(MovementState.Wriggle, model.State);

            model.ApplyDamage(BodyPartId.Torso, Big);
            Assert.AreEqual(MovementState.HeadOnly, model.State);
            Assert.IsTrue(model.Alive);

            model.ApplyDamage(BodyPartId.Head, Big);
            Assert.AreEqual(MovementState.Dead, model.State);
        }

        [Test]
        public void LosingOnlyForearms_StillCrawlsWithoutLegs()
        {
            var model = NewModel();
            model.ApplyDamage(BodyPartId.LeftThigh, Big);
            model.ApplyDamage(BodyPartId.RightThigh, Big);
            model.ApplyDamage(BodyPartId.LeftForearm, Big);
            model.ApplyDamage(BodyPartId.RightForearm, Big);
            Assert.AreEqual(MovementState.Crawl, model.State);
        }

        [Test]
        public void DestroyingTorso_DropsEveryLimbSeparately_AndHeadSurvives()
        {
            var model = NewModel();
            var result = model.ApplyDamage(BodyPartId.Torso, Big);

            Assert.IsFalse(result.Killed);
            Assert.IsTrue(model.Alive);
            Assert.AreEqual(MovementState.HeadOnly, model.State);
            Assert.AreEqual(1, model.PartsOnBody);
            Assert.IsTrue(model.OnBody(BodyPartId.Head));

            // Two arm chains, two leg chains and the torso itself.
            Assert.AreEqual(5, result.Detachments.Count);
            Assert.AreEqual(4, result.Detachments.Count(d => d.Reason == DetachReason.TorsoDestroyed));
            var torso = result.Detachments.Single(d => d.Reason == DetachReason.TorsoGib);
            CollectionAssert.AreEquivalent(new[] { BodyPartId.Torso }, torso.Members);
            foreach (var chain in result.Detachments.Where(d => d.Reason == DetachReason.TorsoDestroyed))
            {
                Assert.AreEqual(3, chain.Members.Length, chain.Root.ToString());
            }

            // Every chunk has its own group.
            Assert.AreEqual(5, result.Detachments.Select(d => d.Group).Distinct().Count());
        }

        [Test]
        public void DestroyingTorso_SkipsLimbsAlreadyGone()
        {
            var model = NewModel();
            model.ApplyDamage(BodyPartId.LeftUpperArm, Big);
            model.ApplyDamage(BodyPartId.RightShin, Big);
            var result = model.ApplyDamage(BodyPartId.Torso, Big);

            // Right arm chain (3), left leg chain (3), right thigh on its own (1), and the torso.
            Assert.AreEqual(4, result.Detachments.Count);
            var rightThigh = result.Detachments.Single(d => d.Root == BodyPartId.RightThigh);
            CollectionAssert.AreEquivalent(new[] { BodyPartId.RightThigh }, rightThigh.Members);
        }

        [Test]
        public void OnlyTheHeadKills()
        {
            foreach (var id in AllParts())
            {
                if (id == BodyPartId.Head) continue;
                var model = NewModel();
                var result = model.ApplyDamage(id, Big);
                Assert.IsFalse(result.Killed, id.ToString());
                Assert.IsTrue(model.Alive, id.ToString());
            }

            // Everything except the head destroyed, one part at a time: still alive.
            var stubborn = NewModel();
            foreach (var id in AllParts())
            {
                if (id == BodyPartId.Head) continue;
                stubborn.ApplyDamage(id, Big);
            }
            Assert.IsTrue(stubborn.Alive);
            Assert.AreEqual(MovementState.HeadOnly, stubborn.State);
            Assert.AreEqual(1, stubborn.PartsOnBody);

            var end = stubborn.ApplyDamage(BodyPartId.Head, Big);
            Assert.IsTrue(end.Killed);
            Assert.IsFalse(stubborn.Alive);
        }

        [Test]
        public void Headshot_LeavesHeadAndCorpseAsTwoChunks()
        {
            var model = NewModel();
            model.ApplyDamage(BodyPartId.LeftForearm, Big);
            var result = model.ApplyDamage(BodyPartId.Head, Big);

            Assert.IsTrue(result.Killed);
            Assert.AreEqual(2, result.Detachments.Count);
            var head = result.Detachments.Single(d => d.Reason == DetachReason.HeadDestroyed);
            CollectionAssert.AreEquivalent(new[] { BodyPartId.Head }, head.Members);
            var corpse = result.Detachments.Single(d => d.Reason == DetachReason.Corpse);
            Assert.AreEqual(BodyPartId.Torso, corpse.Root);
            Assert.AreEqual(11, corpse.Members.Length, "14 minus the head and the forearm+hand already lost");
            Assert.AreEqual(0, model.PartsOnBody);
        }

        [Test]
        public void Corpse_CanStillLoseLimbs_ButNothingKillsTwice()
        {
            var model = NewModel();
            model.ApplyDamage(BodyPartId.Head, Big);

            var limb = model.ApplyDamage(BodyPartId.RightThigh, Big);
            CollectionAssert.AreEquivalent(
                new[] { BodyPartId.RightThigh, BodyPartId.RightShin, BodyPartId.RightFoot },
                limb.Detachments.Single().Members);
            Assert.IsFalse(limb.Killed);

            Assert.AreEqual(0, model.ApplyDamage(BodyPartId.Torso, Big).Detachments.Count, "the corpse torso is the top of its chunk");
            Assert.AreEqual(0, model.ApplyDamage(BodyPartId.Head, Big).Detachments.Count);
        }

        [Test]
        public void HeadOnly_ThenHeadshot_LeavesNoCorpse()
        {
            var model = NewModel();
            model.ApplyDamage(BodyPartId.Torso, Big);
            var result = model.ApplyDamage(BodyPartId.Head, Big);
            Assert.IsTrue(result.Killed);
            Assert.AreEqual(DetachReason.HeadDestroyed, result.Detachments.Single().Reason);
        }

        [Test]
        public void DamageCarriesOverWhenAPartLeavesInAChunk()
        {
            var model = NewModel();
            model.ApplyDamage(BodyPartId.LeftShin, 90f);
            model.ApplyDamage(BodyPartId.LeftThigh, Big);
            // The shin was nearly gone already, so a light hit now splits it off the loose thigh.
            var result = model.ApplyDamage(BodyPartId.LeftShin, 10f);
            Assert.AreEqual(BodyPartId.LeftShin, result.Detachments.Single().Root);
        }

        [Test]
        public void ZeroOrNegativeDamage_IsIgnored()
        {
            var model = NewModel();
            Assert.AreEqual(0, model.ApplyDamage(BodyPartId.Head, 0f).Detachments.Count);
            Assert.AreEqual(0, model.ApplyDamage(BodyPartId.Head, -50f).Detachments.Count);
            Assert.AreEqual(100f, model.Hp(BodyPartId.Head));
        }

        [Test]
        public void EveryPartIsInExactlyOneGroup_AfterAnySequence()
        {
            var rng = new System.Random(4242);
            for (int run = 0; run < 200; run++)
            {
                var model = NewModel(50f);
                var seenGroups = new HashSet<int>();
                for (int shot = 0; shot < 60; shot++)
                {
                    var id = (BodyPartId)rng.Next(BodyLayout.Count);
                    var result = model.ApplyDamage(id, 30f);
                    foreach (var d in result.Detachments)
                    {
                        Assert.IsTrue(d.Group > 0);
                        Assert.IsTrue(seenGroups.Add(d.Group), "group ids are never reused");
                        Assert.AreEqual(d.Root, d.Members[0]);
                        foreach (var m in d.Members) Assert.AreEqual(d.Group, model.Group(m));
                        // A chunk is always connected: every member except the root has its parent in the chunk.
                        foreach (var m in d.Members.Skip(1))
                            Assert.Contains(BodyLayout.Parent(m), d.Members);
                    }
                }
                // The head is never on the body of a dead character, and always on the body of a living one.
                Assert.AreEqual(model.Alive, model.Group(BodyPartId.Head) == 0);
            }
        }
    }
}
