using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Tasks;

namespace OrbitalAnte.Tests
{
    /// <summary>Critique loop round 6: the readout's rows fit their biggest line, and PERK READY shows wherever its button works.</summary>
    public class CritiqueRound6Tests
    {
        [Test]
        public void EveryPointsLine_FitsItsRow_WithRoomForItsOutline()
        {
            foreach (PopTier tier in new[] { PopTier.Plain, PopTier.Skilled, PopTier.Masterful, PopTier.Big })
            {
                int size = PopFeedback.LookFor(tier).fontSize;
                Assert.LessOrEqual(size, PopFeedback.BiggestFont, tier + " is bigger than the biggest line");
                Assert.GreaterOrEqual(PopFeedback.RowPitch, size + 4, tier + " would be clipped or touch the row below");
            }
        }

        [Test]
        public void PerkReady_ShowsOnFootAndWalkingTheCabin_ButNotInTheSeat()
        {
            Assert.IsTrue(ReadoutFocus.ShowsPerkReady(true, false), "outside, or walking the cabin: the button works there");
            Assert.IsFalse(ReadoutFocus.ShowsPerkReady(true, true), "in the seat the button is the floodlights");
            Assert.IsFalse(ReadoutFocus.ShowsPerkReady(false, false), "nothing earned, nothing shown");
        }
    }
}
