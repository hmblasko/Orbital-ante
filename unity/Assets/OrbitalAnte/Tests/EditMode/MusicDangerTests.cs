using NUnit.Framework;
using OrbitalAnte.Audio;
using OrbitalAnte.Tasks;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// The music hears danger (Ralph loop pass 3, 28 Nov 2026): what it follows is how things stand for the player
    /// (calm, hunted, a fight, the last plates), each its own band, and not only gunfire near them.
    /// </summary>
    public class MusicDangerTests
    {
        const float Never = 1000f;

        static DangerSense Quiet(float calm = MusicDanger.OnFoot) =>
            new DangerSense { Calm = calm, SinceSeen = Never, SinceCreature = Never, Armour = 1f };

        // ------------------------------------------------------------------ calm

        [Test]
        public void Calm_IsFullerEachAnte_ButOnFootNeverSoundsLikeSomeoneIsAfterYou()
        {
            float last = -1f;
            for (int ante = 0; ante <= 20; ante++)
            {
                float onFoot = MusicDanger.Calm(false, 0f, ante);
                Assert.GreaterOrEqual(onFoot, last, "never quieter for a later ante");
                last = onFoot;
                Assert.Less(onFoot, 40f, "ante " + ante + ": the tension layer is for being hunted");
                Assert.Less(IntensityModel.BandOf(onFoot), IntensityBand.Tension, "ante " + ante);
            }
            Assert.AreEqual(MusicDanger.OnFoot, MusicDanger.Calm(false, 0f, 1), "ante 1 is the place's own level");
            Assert.AreEqual(MusicDanger.OnFoot, MusicDanger.Calm(false, 0f, 0), "outside a run, the same");
            Assert.Greater(MusicDanger.Calm(false, 0f, 5), MusicDanger.Calm(false, 0f, 2));
            Assert.AreEqual(MusicDanger.Calm(false, 0f, 5), MusicDanger.Calm(false, 0f, 9), "it stops growing");
        }

        [Test]
        public void FlyingHard_LiftsTheFlightMusic_ButNeverStartsTheFightMusic()
        {
            for (int ante = 0; ante <= 20; ante++)
                for (float engine = 0f; engine <= 1.5f; engine += 0.1f)
                    Assert.Less(MusicDanger.Calm(true, engine, ante), 60f, "ante " + ante + ", engine " + engine);
            Assert.AreEqual(20f + 36f, MusicDanger.Calm(true, 1f, 1), 1e-4, "ante 1 at full burn is what it always was");
            Assert.AreEqual(20f, MusicDanger.Calm(true, 0f, 1), 1e-4);
            Assert.Greater(MusicDanger.Calm(true, 0f, 4), MusicDanger.Calm(true, 0f, 1), "coasting late in a run is fuller");
        }

        [Test]
        public void NobodyAfterYou_LeavesTheCalmAlone()
        {
            var danger = new MusicDanger();
            Assert.AreEqual(MusicDanger.OnFoot, danger.Threat(Quiet()));
            DangerSense hurt = Quiet();
            hurt.Armour = 0.05f;
            Assert.AreEqual(MusicDanger.OnFoot, danger.Threat(hurt), "low armour with nobody about is not a fight");
            Assert.IsFalse(danger.OnTheLastPlates);
        }

        // ------------------------------------------------------------------ hunted and fights

        [Test]
        public void BeingLookedFor_IsTension()
        {
            var danger = new MusicDanger();
            DangerSense s = Quiet();
            s.Coming = true;
            Assert.AreEqual(IntensityBand.Tension, IntensityModel.BandOf(danger.Threat(s)), "a squad on its way");
            s = Quiet();
            s.Aware = 2;
            s.SinceSeen = MusicDanger.FightHold + 1f;
            Assert.AreEqual(IntensityBand.Tension, IntensityModel.BandOf(danger.Threat(s)), "they know where you are but lost sight of you");
        }

        [Test]
        public void BeingSeen_OrACreaturesWarning_IsAFight_ForTheReadoutsSixSeconds()
        {
            var danger = new MusicDanger();
            DangerSense s = Quiet();
            s.SinceSeen = 0.1f;
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)));
            s.SinceSeen = MusicDanger.FightHold - 0.01f;
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)));
            s.SinceSeen = MusicDanger.FightHold;
            Assert.Less(danger.Threat(s), 60f, "after the hold it is over, as the readout says");
            Assert.AreEqual(ReadoutFocus.FightHold, MusicDanger.FightHold, "one rule for the screen and the music");

            s = Quiet();
            s.SinceCreature = 2f;
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)), "a creature never shoots, but its warning is a fight");
            s.SinceCreature = -5f;
            Assert.Less(danger.Threat(s), 60f, "a time from before the clock started is no warning");
        }

        [Test]
        public void TheLastPlates_AreChaos_OnAndOffWithTheSuitsPulse()
        {
            var danger = new MusicDanger();
            DangerSense s = Quiet();
            s.SinceSeen = 1f;
            s.Armour = 0.5f;
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)));
            s.Armour = PulseClock.StartAt;
            Assert.AreEqual(IntensityBand.Chaos, IntensityModel.BandOf(danger.Threat(s)), "the pulse starts here");
            s.Armour = 0.25f;
            Assert.AreEqual(IntensityBand.Chaos, IntensityModel.BandOf(danger.Threat(s)), "and holds until it stops");
            s.Armour = PulseClock.StopAt;
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)), "the pulse stops here");
            s.Armour = 0.25f;
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)), "between the two it stays where it was");

            // Out of the fight it lets go at once, and does not come back on until a fight and low armour meet again.
            s.Armour = 0.1f;
            Assert.IsTrue(danger.Threat(s) >= 80f);
            s.SinceSeen = Never;
            Assert.Less(danger.Threat(s), 40f);
            s.SinceSeen = 0.5f;
            s.Armour = 0.25f;
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)), "not on again from the old fight");
        }

        // ------------------------------------------------------------------ through the music's own model

        [Test]
        public void ASighting_BringsTheFightMusicAtOnce_AndItLetsGoAfterTheFight()
        {
            var danger = new MusicDanger();
            var model = new IntensityModel();
            const float dt = 0.25f;
            float time = 0f, seenAt = 2f, combatAt = -1f, calmAt = -1f;
            for (int step = 0; step < 160; step++, time += dt)
            {
                DangerSense s = Quiet();
                s.SinceSeen = time >= seenAt ? time - seenAt : Never;
                model.SetThreat(danger.Threat(s));
                model.Tick(dt);
                if (combatAt < 0f && model.Band >= IntensityBand.Combat) combatAt = time;
                if (combatAt >= 0f && calmAt < 0f && model.Value < 38f) calmAt = time;
            }
            Assert.GreaterOrEqual(combatAt, seenAt);
            Assert.LessOrEqual(combatAt - seenAt, 1.25f, "the fight music comes in with the sighting, before the first shot's beat is long over");
            Assert.Greater(calmAt, seenAt + MusicDanger.FightHold, "it holds through the readout's fight");
            Assert.Less(calmAt, seenAt + MusicDanger.FightHold + 6f, "and lets go soon after (combat ends below 38)");
        }
    }
}
