using NUnit.Framework;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests.EditMode
{
    public class ThrusterHeardTests
    {
        [Test]
        public void LiftIsTheBigEnginesAndNothingElse()
        {
            ThrusterHeard mix = ThrusterHeard.For(0f, 1f, 0f, 0f);
            Assert.AreEqual(1f, mix.Main, 1e-4f);
            Assert.AreEqual(0f, mix.Jets, 1e-4f);
        }

        [Test]
        public void ASidewaysPushIsHeardFromTheOtherSide()
        {
            ThrusterHeard right = ThrusterHeard.For(1f, 0f, 0f, 0f);
            ThrusterHeard left = ThrusterHeard.For(-1f, 0f, 0f, 0f);
            Assert.AreEqual(0f, right.Main, 1e-4f, "The big engines only lift.");
            Assert.Greater(right.Jets, 0.9f);
            Assert.Less(right.Pan, -0.5f, "Pushed right: the left thruster fires.");
            Assert.Greater(left.Pan, 0.5f);
        }

        [Test]
        public void EveryDirectionHasItsOwnNote()
        {
            float side = ThrusterHeard.For(1f, 0f, 0f, 0f).Pitch;
            float forward = ThrusterHeard.For(0f, 0f, 1f, 0f).Pitch;
            float back = ThrusterHeard.For(0f, 0f, -1f, 0f).Pitch;
            float down = ThrusterHeard.For(0f, -1f, 0f, 0f).Pitch;
            float turn = ThrusterHeard.For(0f, 0f, 0f, 1f).Pitch;
            float[] notes = { side, forward, back, down, turn };
            for (int a = 0; a < notes.Length; a++)
            {
                for (int b = a + 1; b < notes.Length; b++)
                {
                    Assert.Greater(System.Math.Abs(notes[a] - notes[b]), 0.05f, "Two directions sound the same: " + a + " and " + b);
                }
            }
        }

        [Test]
        public void TurningAloneIsQuietButHeard()
        {
            ThrusterHeard mix = ThrusterHeard.For(0f, 0f, 0f, 1f);
            Assert.Greater(mix.Jets, 0.1f);
            Assert.Less(mix.Jets, 0.5f);
            Assert.AreEqual(0f, mix.Pan, 1e-4f);
        }

        [Test]
        public void NothingPushedIsSilence()
        {
            ThrusterHeard mix = ThrusterHeard.For(0f, 0f, 0f, 0f);
            Assert.AreEqual(0f, mix.Main, 1e-4f);
            Assert.AreEqual(0f, mix.Jets, 1e-4f);
            Assert.AreEqual(1f, mix.Pitch, 1e-4f);
        }
    }
}
