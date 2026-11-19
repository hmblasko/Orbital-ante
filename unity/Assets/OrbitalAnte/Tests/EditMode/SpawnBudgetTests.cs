using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    public class SpawnBudgetTests
    {
        [Test]
        public void AGroupLargerThanTheAllowanceResumesAtTheNextUnattemptedPlacement()
        {
            var budget = new SpawnBudget();
            var group = new SpawnBudget.Group(7);
            var visited = new List<int>();
            foreach (int expected in new[] { 3, 3, 1 })
            {
                budget.BeginFrame(3);
                int count = group.Take(budget, out int first);
                Assert.AreEqual(expected, count);
                for (int i = first; i < first + count; i++) visited.Add(i);
                // Repeated calls in the same frame cannot replay the range or borrow the next frame.
                Assert.AreEqual(0, group.Take(budget, out int next));
                Assert.AreEqual(visited.Count, next);
            }
            Assert.IsTrue(group.Complete);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6 }, visited);
        }

        [Test]
        public void AnAllowanceOfOneFinishesOnePlacementPerFrame()
        {
            var budget = new SpawnBudget();
            var group = new SpawnBudget.Group(5);
            for (int frame = 0; frame < 5; frame++)
            {
                budget.BeginFrame(1);
                Assert.AreEqual(1, group.Take(budget, out int first));
                Assert.AreEqual(frame, first);
                Assert.AreEqual(0, budget.Remaining);
                Assert.AreEqual(frame == 4, group.Complete);
            }
        }

        [Test]
        public void AnEmptyGroupCompletesWithoutSpendingTheAllowance()
        {
            var budget = new SpawnBudget();
            var group = new SpawnBudget.Group(0);
            budget.BeginFrame(4);
            Assert.IsTrue(group.Complete);
            Assert.AreEqual(0, group.Take(budget, out int first));
            Assert.AreEqual(0, first);
            Assert.AreEqual(4, budget.Remaining);
        }

        [Test]
        public void HidingAPartialGroupCancelsItsRemainderWithoutRefundingThisFramesWork()
        {
            var budget = new SpawnBudget();
            var hidden = new SpawnBudget.Group(8);
            budget.BeginFrame(3);
            Assert.AreEqual(3, hidden.Take(budget, out int first));
            Assert.AreEqual(0, first);
            hidden.Cancel();
            hidden.Cancel();
            Assert.IsTrue(hidden.Cancelled);
            Assert.IsFalse(hidden.Complete);
            Assert.AreEqual(0, budget.Remaining);
            Assert.AreEqual(0, new SpawnBudget.Group(2).Take(budget, out _));
            budget.BeginFrame(3);
            Assert.AreEqual(0, hidden.Take(budget, out int cancelledFirst));
            Assert.AreEqual(3, cancelledFirst);
            Assert.AreEqual(3, hidden.NextIndex, "the owner can retire its shown items; no hidden remainder resumes");
            Assert.AreEqual(3, budget.Remaining);
            var shownAgain = new SpawnBudget.Group(8);
            Assert.AreEqual(3, shownAgain.Take(budget, out int freshFirst));
            Assert.AreEqual(0, freshFirst, "showing a retired group starts a new lifetime");
        }

        [Test]
        public void GroupsAndFixedPlacementsShareOneFrameAllowance()
        {
            var budget = new SpawnBudget();
            var a = new SpawnBudget.Group(3);
            var b = new SpawnBudget.Group(5);
            budget.BeginFrame(5);
            Assert.AreEqual(1, budget.Take(1), "one fixed placement");
            Assert.AreEqual(3, a.Take(budget, out int aFirst));
            Assert.AreEqual(0, aFirst);
            Assert.AreEqual(1, b.Take(budget, out int bFirst));
            Assert.AreEqual(0, bFirst);
            Assert.AreEqual(0, budget.Take(1), "fixed work cannot exceed scatter's shared cap");
            budget.BeginFrame(5);
            Assert.AreEqual(0, a.Take(budget, out _));
            Assert.AreEqual(4, b.Take(budget, out bFirst));
            Assert.AreEqual(1, bFirst);
            Assert.AreEqual(1, budget.Take(1));
            Assert.IsTrue(a.Complete && b.Complete);
        }

        [Test]
        public void RefusedOrClearedPlacementsDoNotRestartOrSkipTheNextRange()
        {
            var budget = new SpawnBudget();
            var group = new SpawnBudget.Group(9);
            var attempted = new List<int>();
            var shown = new List<int>();
            while (!group.Complete)
            {
                budget.BeginFrame(2);
                int count = group.Take(budget, out int first);
                for (int i = first; i < first + count; i++)
                {
                    attempted.Add(i);
                    // Clear patches and the live feature cap may refuse an attempt. They never move its cursor back.
                    if (i % 3 != 0 && shown.Count < 3) shown.Add(i);
                }
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, attempted);
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, shown);
            Assert.AreEqual(9, group.NextIndex);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void NonPositiveAllowancesCannotSpawn(int allowance)
        {
            var budget = new SpawnBudget();
            var group = new SpawnBudget.Group(10);
            budget.BeginFrame(allowance);
            Assert.AreEqual(0, budget.Remaining);
            Assert.AreEqual(0, budget.Take(1));
            Assert.AreEqual(0, group.Take(budget, out int first));
            Assert.AreEqual(0, first);
            Assert.IsFalse(group.Complete);
        }

        [TestCase(0)]
        [TestCase(-3)]
        [TestCase(int.MinValue)]
        public void NonPositiveRequestsDoNotIncreaseTheAllowance(int request)
        {
            var budget = new SpawnBudget();
            budget.BeginFrame(4);
            Assert.AreEqual(0, budget.Take(request));
            Assert.AreEqual(4, budget.Remaining);
        }

        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void NegativeGroupSizesAreEmpty(int size)
        {
            var budget = new SpawnBudget();
            var group = new SpawnBudget.Group(size);
            budget.BeginFrame(2);
            Assert.IsTrue(group.Complete);
            Assert.AreEqual(0, group.Take(budget, out _));
            Assert.AreEqual(2, budget.Remaining);
        }

        [Test]
        public void MaximumCountsCannotOverflowTheAllowanceOrCursor()
        {
            var budget = new SpawnBudget();
            var group = new SpawnBudget.Group(int.MaxValue);
            budget.BeginFrame(int.MaxValue);
            Assert.AreEqual(1, budget.Take(1));
            Assert.AreEqual(int.MaxValue - 1, group.Take(budget, out int first));
            Assert.AreEqual(0, first);
            budget.BeginFrame(int.MaxValue);
            Assert.AreEqual(1, group.Take(budget, out first));
            Assert.AreEqual(int.MaxValue - 1, first);
            Assert.AreEqual(int.MaxValue, group.NextIndex);
            Assert.AreEqual(0, group.Take(budget, out first));
            Assert.AreEqual(int.MaxValue, first);
            Assert.AreEqual(int.MaxValue - 1, budget.Remaining);
            Assert.IsTrue(group.Complete);
        }

        [Test]
        public void UnusedAllowanceDoesNotAccumulateAcrossFrames()
        {
            var budget = new SpawnBudget();
            budget.BeginFrame(5);
            Assert.AreEqual(2, budget.Take(2));
            budget.BeginFrame(5);
            Assert.AreEqual(5, budget.Take(20));
            Assert.AreEqual(0, budget.Remaining);
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(24)]
        [TestCase(int.MaxValue)]
        public void MixedGroupSizesVisitEveryIndexOnceAndNeverExceedAnyFrame(int allowance)
        {
            int[] sizes = { 0, 1, 17, 250, 2, 4, 100 };
            var groups = new SpawnBudget.Group[sizes.Length];
            var seen = new HashSet<int>[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                groups[i] = new SpawnBudget.Group(sizes[i]);
                seen[i] = new HashSet<int>();
            }
            var budget = new SpawnBudget();
            bool complete = false;
            for (int frame = 0; !complete && frame < 1000; frame++)
            {
                budget.BeginFrame(allowance);
                int spent = frame % 3 == 0 ? budget.Take(1) : 0;
                complete = true;
                for (int g = 0; g < groups.Length; g++)
                {
                    int count = groups[g].Take(budget, out int first);
                    spent += count;
                    for (int i = first; i < first + count; i++) Assert.IsTrue(seen[g].Add(i), "repeated group/index " + g + "/" + i);
                    complete &= groups[g].Complete;
                }
                Assert.LessOrEqual(spent, allowance);
                Assert.AreEqual(allowance - spent, budget.Remaining);
            }
            Assert.IsTrue(complete, "a finite batch must eventually finish");
            for (int g = 0; g < groups.Length; g++)
            {
                Assert.AreEqual(sizes[g], seen[g].Count);
                for (int i = 0; i < sizes[g]; i++) Assert.IsTrue(seen[g].Contains(i));
            }
        }
    }
}
