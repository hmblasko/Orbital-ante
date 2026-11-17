using System;
using NUnit.Framework;
using OrbitalAnte.Spacewalk;

namespace OrbitalAnte.Tests.EditMode
{
    public class StanceTests
    {
        static readonly StanceRules Rules = new StanceRules();

        static void Hold(StanceState s, float seconds, Func<Stance, bool> room = null)
        {
            for (float t = 0f; t < seconds; t += 0.02f) s.Step(0.02f, true, true, room, Rules);
            s.Step(0.02f, false, true, room, Rules);
        }

        static void Tap(StanceState s, Func<Stance, bool> room = null) => Hold(s, 0.1f, room);

        static void Settle(StanceState s)
        {
            for (int i = 0; i < 60; i++) s.Step(0.02f, false, true, null, Rules);
        }

        [Test]
        public void ATap_Crouches_AndASecondTap_StandsYouUp()
        {
            var s = new StanceState();
            Tap(s);
            Assert.That(s.Current, Is.EqualTo(Stance.Crouch));
            Tap(s);
            Assert.That(s.Current, Is.EqualTo(Stance.Stand));
        }

        [Test]
        public void Holding_LiesYouDown_AndHoldingAgain_StandsYouUp()
        {
            var s = new StanceState();
            Hold(s, Rules.holdSeconds + 0.1f);
            Assert.That(s.Current, Is.EqualTo(Stance.Prone));
            Hold(s, Rules.holdSeconds + 0.1f);
            Assert.That(s.Current, Is.EqualTo(Stance.Stand));
        }

        [Test]
        public void ATapWhileProne_BringsYouUpToACrouch()
        {
            var s = new StanceState();
            Hold(s, Rules.holdSeconds + 0.1f);
            Tap(s);
            Assert.That(s.Current, Is.EqualTo(Stance.Crouch));
        }

        [Test]
        public void ALongHold_ChangesOnlyOnce()
        {
            var s = new StanceState();
            Hold(s, Rules.holdSeconds * 4f);
            Assert.That(s.Current, Is.EqualTo(Stance.Prone), "keeping the button down does not flip you back up");
        }

        [Test]
        public void WithNoRoomAbove_YouStayDown_AndAreToldWhy()
        {
            var s = new StanceState();
            Tap(s);
            Func<Stance, bool> lowRoof = st => st != Stance.Stand;
            for (float t = 0f; t < 0.1f; t += 0.02f) s.Step(0.02f, true, true, lowRoof, Rules);
            s.Step(0.02f, false, true, lowRoof, Rules);
            Assert.That(s.Current, Is.EqualTo(Stance.Crouch));
            Assert.That(s.Blocked, Is.True);
            Assert.That(s.RiseToJump(lowRoof), Is.False, "nor can you jump up into the roof");
        }

        [Test]
        public void LeavingTheGround_StandsYouUp()
        {
            var s = new StanceState();
            Tap(s);
            s.Step(0.02f, false, false, null, Rules);
            Assert.That(s.Current, Is.EqualTo(Stance.Stand));
        }

        [Test]
        public void LandingWithJetsDownStillHeld_ChangesNothing_UntilItIsPressedAgain()
        {
            var s = new StanceState();
            for (int i = 0; i < 20; i++) s.Step(0.02f, true, false, null, Rules); // pushing down in the air
            for (int i = 0; i < 50; i++) s.Step(0.02f, true, true, null, Rules);  // landed, still holding a second
            s.Step(0.02f, false, true, null, Rules);
            Assert.That(s.Current, Is.EqualTo(Stance.Stand), "the press was for the jets: no crouch, no prone");
            Tap(s);
            Assert.That(s.Current, Is.EqualTo(Stance.Crouch), "a fresh press works as usual");
        }

        [Test]
        public void KeepingStill_IsNeverHeard_AndAShotAlwaysIs()
        {
            Assert.That(StanceState.HeardFrom(Stance.Stand, false, false, false, false, Rules), Is.EqualTo(0f));
            Assert.That(StanceState.HeardFrom(Stance.Prone, false, true, false, true, Rules), Is.GreaterThan(1f));
        }

        [Test]
        public void TheEye_EasesDown_AndEndsAtTheStancesHeight()
        {
            var s = new StanceState();
            Settle(s);
            Assert.That(s.Eye, Is.EqualTo(Rules.standEye).Within(1e-4));
            Hold(s, Rules.holdSeconds + 0.1f);
            Assert.That(s.Eye, Is.LessThan(Rules.standEye).And.GreaterThan(Rules.proneEye), "not in one jump");
            Settle(s);
            Assert.That(s.Eye, Is.EqualTo(Rules.proneEye).Within(1e-4));
        }

        [Test]
        public void JumpingFromACrouch_StandsAndGoes_FromProne_OnlyRisesToACrouch()
        {
            var s = new StanceState();
            Tap(s);
            Assert.That(s.RiseToJump(null), Is.True);
            Assert.That(s.Current, Is.EqualTo(Stance.Stand));
            Hold(s, Rules.holdSeconds + 0.1f);
            Assert.That(s.RiseToJump(null), Is.False);
            Assert.That(s.Current, Is.EqualTo(Stance.Crouch));
        }

        [Test]
        public void LowerAndQuieter_IsSlowerHarderToSeeAndHarderToHear()
        {
            Assert.That(StanceState.SpeedOf(Stance.Prone, false, Rules), Is.LessThan(StanceState.SpeedOf(Stance.Crouch, false, Rules)));
            Assert.That(StanceState.SpeedOf(Stance.Crouch, false, Rules), Is.LessThan(StanceState.SpeedOf(Stance.Stand, false, Rules)));
            Assert.That(StanceState.SpeedOf(Stance.Stand, true, Rules), Is.LessThan(1f), "sneaking is a slow walk");

            Assert.That(StanceState.SeenFrom(Stance.Prone, true, Rules), Is.LessThan(StanceState.SeenFrom(Stance.Crouch, true, Rules)));
            Assert.That(StanceState.SeenFrom(Stance.Crouch, true, Rules), Is.LessThan(StanceState.SeenFrom(Stance.Stand, true, Rules)));
            Assert.That(StanceState.SeenFrom(Stance.Stand, false, Rules), Is.LessThan(StanceState.SeenFrom(Stance.Stand, true, Rules)), "still is harder to pick out");
            Assert.That(StanceState.SeenFrom(Stance.Stand, true, Rules), Is.EqualTo(1f), "standing and walking changes nothing");

            float walk = StanceState.HeardFrom(Stance.Stand, true, false, false, false, Rules);
            Assert.That(walk, Is.EqualTo(1f));
            Assert.That(StanceState.HeardFrom(Stance.Stand, true, true, false, false, Rules), Is.LessThan(walk * 0.5f), "sneaking is quiet");
            Assert.That(StanceState.HeardFrom(Stance.Prone, true, false, false, false, Rules), Is.LessThan(StanceState.HeardFrom(Stance.Crouch, true, false, false, false, Rules)));
            Assert.That(StanceState.HeardFrom(Stance.Prone, true, true, true, false, Rules), Is.GreaterThan(walk), "the jets are loud whatever you are doing");
            Assert.That(StanceState.HeardFrom(Stance.Prone, false, true, false, true, Rules), Is.GreaterThan(Rules.jetsHeard), "and a shot is louder still");
        }

        [Test]
        public void EveryStance_FitsItsEyeInsideItsBody()
        {
            foreach (Stance st in Enum.GetValues(typeof(Stance)))
                Assert.That(StanceState.EyeOf(st, Rules), Is.LessThan(StanceState.HeightOf(st, Rules)), st.ToString());
        }
    }
}
