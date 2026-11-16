using NUnit.Framework;
using OrbitalAnte.AI;
using OrbitalAnte.Enemies;

namespace OrbitalAnte.Tests
{
    /// <summary>Cover and flanking (Matthew, 16 Nov 2026): the squad's hold-and-flank roles, and how cover is chosen.</summary>
    public class CoverAndFlankTests
    {
        const float After = 2.5f;

        static Squad Engaged(int members, out int[] ids, float at = 100f, bool runnerFirst = false)
        {
            var squad = new Squad(members);
            ids = new int[members];
            for (int i = 0; i < members; i++) ids[i] = squad.Join(true, runnerFirst && i == members - 1 ? 2 : 1);
            squad.Saw(at);
            return squad;
        }

        [Test]
        public void NobodyIsSentRound_UntilTheSquadHasFoughtFaceOnForAMoment()
        {
            Squad squad = Engaged(4, out int[] ids);
            Assert.AreEqual(SquadRole.Hold, squad.RoleOf(ids[0], 101f, After, 1), "first contact is from the front");
            squad.Saw(102.6f);
            Assert.AreEqual(SquadRole.Flank, squad.RoleOf(ids[0], 102.6f, After, 1));
        }

        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(8, 2)]
        public void ASquadSendsAtMostOneEachSide_AndAlwaysKeepsSomeoneHolding(int members, int flankers)
        {
            Squad squad = Engaged(members, out int[] ids);
            float now = 103f;
            squad.Saw(now);
            int sent = 0, left = 0, right = 0;
            // Everyone asks several times, as they would think by think.
            for (int round = 0; round < 3; round++)
                for (int i = 0; i < members; i++)
                    squad.RoleOf(ids[i], now, After, i % 2 == 0 ? 1 : -1);
            for (int i = 0; i < members; i++)
            {
                if (squad.RoleOf(ids[i], now, After, 1) != SquadRole.Flank) continue;
                sent++;
                if (squad.SideOf(ids[i]) < 0) left++; else right++;
            }
            Assert.AreEqual(flankers, sent);
            Assert.LessOrEqual(left, 1);
            Assert.LessOrEqual(right, 1);
            Assert.Less(sent, members == 1 ? 2 : members, "somebody always holds the front");
        }

        [Test]
        public void TheKeenestGoesFirst_AndSnipersAndWalkersDownNeverGo()
        {
            var squad = new Squad(3);
            int marksman = squad.Join(TacticsRules.CanFlank(holdsStill: true, closesIn: false, rushes: false), 1);
            int bruiser = squad.Join(TacticsRules.CanFlank(holdsStill: false, closesIn: true, rushes: false), 1);
            int runner = squad.Join(TacticsRules.CanFlank(holdsStill: false, closesIn: true, rushes: true), TacticsRules.Eagerness(true));
            squad.Saw(0f);
            squad.Saw(3f);
            for (int round = 0; round < 2; round++)
                foreach (int id in new[] { marksman, bruiser, runner }) squad.RoleOf(id, 3f, After, 1);
            Assert.AreEqual(SquadRole.Hold, squad.RoleOf(marksman, 3f, After, 1));
            Assert.AreEqual(SquadRole.Hold, squad.RoleOf(bruiser, 3f, After, 1));
            Assert.AreEqual(SquadRole.Flank, squad.RoleOf(runner, 3f, After, 1));
            Assert.IsFalse(TacticsRules.UsesCover(holdsStill: false, closesIn: true, rushes: false), "the Bruiser walks you down in the open");
            Assert.IsTrue(TacticsRules.UsesCover(holdsStill: true, closesIn: false, rushes: false), "the Marksman hides");
        }

        [Test]
        public void AFallenFlanker_IsReplacedOnlyAfterAPause()
        {
            Squad squad = Engaged(3, out int[] ids);
            squad.Saw(103f);
            int first = -1;
            for (int i = 0; i < 3; i++) if (squad.RoleOf(ids[i], 103f, After, 1) == SquadRole.Flank) first = ids[i];
            Assert.GreaterOrEqual(first, 0);
            squad.Leave(first, 104f, 5f);
            squad.Saw(106f);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(SquadRole.Hold, squad.RoleOf(ids[i], 106f, After, 1), "no instant replacement");
            squad.Saw(109.5f);
            int sent = 0;
            for (int i = 0; i < 3; i++) if (squad.RoleOf(ids[i], 109.5f, After, 1) == SquadRole.Flank) sent++;
            Assert.AreEqual(1, sent, "two left: one goes round again");
        }

        [Test]
        public void AFlankThatIsDone_KeepsItsPlace_SoTheSameSideIsNotSentAgain()
        {
            Squad squad = Engaged(2, out int[] ids);
            squad.Saw(103f);
            int f = squad.RoleOf(ids[0], 103f, After, 1) == SquadRole.Flank ? ids[0] : ids[1];
            squad.RoleOf(ids[1], 103f, After, 1);
            squad.FlankDone(f);
            Assert.IsTrue(squad.IsFlankDone(f));
            int other = f == ids[0] ? ids[1] : ids[0];
            Assert.AreEqual(SquadRole.Hold, squad.RoleOf(other, 104f, After, 1));
        }

        [Test]
        public void LosingALeg_TakesAFlankerOffItsRun()
        {
            Squad squad = Engaged(3, out int[] ids);
            squad.Saw(103f);
            int f = -1;
            for (int i = 0; i < 3; i++) if (squad.RoleOf(ids[i], 103f, After, 1) == SquadRole.Flank) f = ids[i];
            squad.SetCanFlank(f, false, 103.5f, 5f);
            Assert.AreEqual(SquadRole.Hold, squad.RoleOf(f, 104f, After, 1));
        }

        [Test]
        public void AFlankerGoesRoundItsOwnSide_UnlessThatSideIsTaken()
        {
            Squad squad = Engaged(5, out int[] ids);
            squad.Saw(103f);
            squad.RoleOf(ids[0], 103f, After, -1);
            Assert.AreEqual(-1, squad.SideOf(ids[0]));
            squad.RoleOf(ids[1], 103f, After, -1);
            Assert.AreEqual(1, squad.SideOf(ids[1]), "the left is taken, so the second goes right");
        }

        [Test]
        public void ANewFight_StartsTheFlankingAfresh()
        {
            Squad squad = Engaged(2, out int[] ids);
            squad.Saw(103f);
            squad.RoleOf(ids[0], 103f, After, 1);
            squad.FlankDone(ids[0]);
            squad.Saw(103f + Squad.NewFightGap + 1f);
            Assert.IsFalse(squad.IsFlankDone(ids[0]));
            Assert.AreEqual(SquadRole.Hold, squad.RoleOf(ids[0], 114.5f, After, 1), "face on again first");
        }

        [Test]
        public void TwoOfASquad_NeverTakeTheSameRock()
        {
            Squad squad = Engaged(2, out int[] ids);
            squad.Claim(ids[0], 10f, 0f, 10f);
            Assert.IsTrue(squad.Taken(11f, 0f, 10f, 2.2f, ids[1]));
            Assert.IsFalse(squad.Taken(11f, 0f, 10f, 2.2f, ids[0]), "its own spot does not count");
            Assert.IsFalse(squad.Taken(14f, 0f, 10f, 2.2f, ids[1]));
            squad.Leave(ids[0], 0f, 5f);
            Assert.IsFalse(squad.Taken(11f, 0f, 10f, 2.2f, ids[1]), "the dead hold no spots");
        }

        [Test]
        public void TheFront_IsWhereTheHoldersAre()
        {
            Squad squad = Engaged(3, out int[] ids);
            squad.Report(ids[0], 0f, 0f, 0f);
            squad.Report(ids[1], 4f, 0f, 0f);
            squad.Report(ids[2], 100f, 0f, 0f);
            squad.Saw(103f);
            int f = -1;
            for (int i = 0; i < 3; i++) if (squad.RoleOf(ids[i], 103f, After, 1) == SquadRole.Flank) f = i;
            Assert.IsTrue(squad.FrontCentre(out float x, out _, out _));
            float expected = 0f;
            int n = 0;
            float[] xs = { 0f, 4f, 100f };
            for (int i = 0; i < 3; i++) if (i != f) { expected += xs[i]; n++; }
            Assert.AreEqual(expected / n, x, 1e-4f, "the flanker is not part of the front");
        }

        static CoverCandidate Good() => new CoverCandidate { Travel = 5f, ToThreat = 15f, ChestHidden = true, HeadHidden = true, CanPeek = true };

        [Test]
        public void ASpotThePlayerCanSee_OrNoWayToShootFrom_IsNeverCover()
        {
            CoverCandidate c = Good();
            c.ChestHidden = false;
            Assert.IsTrue(float.IsInfinity(CoverScore.Of(c, true, 7f, 18f)));
            c = Good();
            c.CanPeek = false;
            Assert.IsTrue(float.IsInfinity(CoverScore.Of(c, true, 7f, 18f)), "armed: needs somewhere to shoot from");
            Assert.IsFalse(float.IsInfinity(CoverScore.Of(c, false, 7f, 18f)), "unarmed: hiding is enough");
            c = Good();
            c.Taken = true;
            Assert.IsTrue(float.IsInfinity(CoverScore.Of(c, true, 7f, 18f)));
        }

        [Test]
        public void NeverHidesInThePlayersFace()
        {
            CoverCandidate c = Good();
            c.ToThreat = 2f;
            Assert.IsTrue(float.IsInfinity(CoverScore.Of(c, true, 3f, 9f)));
        }

        [Test]
        public void NearAndFullCover_BeatsFarAndLow_AndFlankersPreferTheSide()
        {
            CoverCandidate near = Good(), far = Good(), low = Good();
            far.Travel = 12f;
            low.HeadHidden = false;
            Assert.Less(CoverScore.Of(near, true, 7f, 18f), CoverScore.Of(far, true, 7f, 18f));
            Assert.Less(CoverScore.Of(near, true, 7f, 18f), CoverScore.Of(low, true, 7f, 18f));
            CoverCandidate onSide = Good(), offSide = Good();
            onSide.Travel = 8f;
            offSide.OffBearing = 70f;
            Assert.Less(CoverScore.Of(onSide, true, 7f, 18f), CoverScore.Of(offSide, true, 7f, 18f));
        }

        [Test]
        public void InCover_ItDucksToReload_AndWhileTheSquadsTurnsAreTaken()
        {
            Assert.IsTrue(CoverRhythm.Peeking(true, false, false));
            Assert.IsFalse(CoverRhythm.Peeking(true, true, false), "reloads hidden");
            Assert.IsFalse(CoverRhythm.Peeking(true, false, true), "waits its turn hidden");
            Assert.IsFalse(CoverRhythm.Peeking(false, false, false));
        }
    }
}
