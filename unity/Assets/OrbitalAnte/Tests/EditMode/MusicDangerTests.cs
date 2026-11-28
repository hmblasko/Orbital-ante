using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OrbitalAnte.Audio;
using OrbitalAnte.Tasks;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// The music hears danger (Ralph loop pass 3, 28 Nov 2026): what it follows is how things stand for the player
    /// (calm, hunted, a fight), each its own band, and not only gunfire near them; on the last plates it thins so the
    /// suit's pulse is heard.
    /// </summary>
    public class MusicDangerTests
    {
        const float Never = 1000f;

        static DangerSense Quiet(float calm = MusicDanger.OnFoot) =>
            new DangerSense { Calm = calm, SinceSeen = Never, SinceCreature = Never, Armour = 1f };

        /// <summary>As the solar system tells it: who is after the player is counted there.</summary>
        static DangerSense Counted()
        {
            DangerSense s = Quiet();
            s.Counted = true;
            return s;
        }

        static string Folder(params string[] rel)
        {
            string dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && dir != null; i++)
            {
                foreach (string head in new[] { Path.Combine(dir, "unity", "Assets", "OrbitalAnte"), Path.Combine(dir, "Assets", "OrbitalAnte") })
                {
                    string p = Path.Combine(head, Path.Combine(rel));
                    if (Directory.Exists(p)) return p;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        /// <summary>The game's own music rules and pieces, or the test is skipped where they cannot be found.</summary>
        static AudioManifest Real()
        {
            string folder = Folder("Resources", "Audio");
            if (folder == null) Assert.Ignore("The audio folder was not found from here.");
            return AudioManifest.Parse(File.ReadAllText(Path.Combine(folder, "audio_manifest.json")));
        }

        static float LayerFrom(AudioManifest m, string track, string stem)
        {
            foreach (StemDef s in m.Tracks[track].Stems) if (s.Id == stem) return s.Lo;
            Assert.Fail(track + " has no layer " + stem);
            return 0f;
        }

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
            Assert.Greater(MusicDanger.Calm(false, 0f, 3), MusicDanger.Calm(false, 0f, 2));
            Assert.AreEqual(MusicDanger.Calm(false, 0f, 3), MusicDanger.Calm(false, 0f, 9), "it stops growing, by ante 3");
        }

        [Test]
        public void TheAntesLift_IsHeardBeforeMostFirstRunsEnd()
        {
            // Part B section 5: a first run usually ends at ante 3 or 4, so the lift must be heard by then.
            AudioManifest m = Real();
            float choir = LayerFrom(m, "mystery_dish", "choir"), perc = LayerFrom(m, "explore_regolith", "perc");
            Assert.Less(MusicDanger.Calm(false, 0f, 1), choir, "ante 1 is the cave and sea as they always were");
            Assert.Less(MusicDanger.Calm(false, 0f, 1), perc, "and the surface");
            Assert.GreaterOrEqual(MusicDanger.Calm(false, 0f, 2), choir, "ante 2: the choir joins in caves and the sea");
            Assert.GreaterOrEqual(MusicDanger.Calm(false, 0f, 3), perc, "ante 3: the percussion joins on the surface");
            Assert.Less(MusicDanger.Calm(false, 0f, 20), LayerFrom(m, "explore_regolith", "tension"), "never the tension layer");
        }

        [Test]
        public void FlyingHard_LiftsTheFlightMusic_ButNeverStartsTheFightMusic_AndCoastingStaysQuiet()
        {
            for (int ante = 0; ante <= 20; ante++)
            {
                for (float engine = 0f; engine <= 1.5f; engine += 0.1f)
                    Assert.Less(MusicDanger.Calm(true, engine, ante), 60f, "ante " + ante + ", engine " + engine);
                Assert.AreEqual(MusicDanger.Coasting, MusicDanger.Calm(true, 0f, ante), 1e-4, "ante " + ante + ": coasting is as quiet as ever, so a burn still stands out");
            }
            Assert.AreEqual(20f + 36f, MusicDanger.Calm(true, 1f, 1), 1e-4, "ante 1 at full burn is what it always was");
            Assert.Greater(MusicDanger.Calm(true, 0.5f, 4), MusicDanger.Calm(true, 0.5f, 1), "a burn late in a run is fuller");
        }

        [Test]
        public void NobodyAfterYou_LeavesTheCalmAlone()
        {
            var danger = new MusicDanger();
            Assert.AreEqual(MusicDanger.OnFoot, danger.Threat(Quiet()));
            DangerSense hurt = Counted();
            hurt.Armour = 0.05f;
            Assert.AreEqual(MusicDanger.OnFoot, danger.Threat(hurt), "low armour with nobody about is not a fight");
            Assert.IsFalse(danger.OnTheLastPlates);
            Assert.IsFalse(danger.SomeoneAfter, "so the music may rest");
        }

        // ------------------------------------------------------------------ hunted and fights

        [Test]
        public void BeingLookedFor_IsTension_AndKeepsTheMusicFromResting()
        {
            var danger = new MusicDanger();
            DangerSense s = Counted();
            s.Coming = true;
            Assert.AreEqual(IntensityBand.Tension, IntensityModel.BandOf(danger.Threat(s)), "someone coming to look where they heard you");
            Assert.IsTrue(danger.SomeoneAfter);
            s = Counted();
            s.Aware = 2;
            s.SinceSeen = MusicDanger.FightHold + 1f;
            Assert.AreEqual(IntensityBand.Tension, IntensityModel.BandOf(danger.Threat(s)), "they know where you are but lost sight of you");
            s = Counted();
            s.CreatureAware = true;
            Assert.AreEqual(IntensityBand.Tension, IntensityModel.BandOf(danger.Threat(s)), "a maw has noticed you in the water and is coming");
            Assert.IsTrue(danger.SomeoneAfter);
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
        public void InTheSolarSystem_ASightingIsAFight_OnlyWhileAnArmedEnemyKnowsWhereYouAre()
        {
            var danger = new MusicDanger();
            DangerSense s = Counted();
            s.SinceSeen = 1f;
            Assert.Less(danger.Threat(s), 60f, "seen by a disarmed enemy hiding: no fight");
            s.Aware = 1;
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)), "seen by someone with a gun");
            s = Quiet();
            s.SinceSeen = 1f;
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)), "the practice range counts nobody, so a sighting is enough");
        }

        [Test]
        public void ACreaturesFight_HoldsBetweenItsStrikes_WhileItStillHasYouInMind()
        {
            var danger = new MusicDanger();
            DangerSense s = Counted();
            s.CreatureAware = true;
            // The Drifting maw comes again every 12 to 20 s: the fight holds through the gap.
            foreach (float since in new[] { 2f, 8f, 12f, 20f, MusicDanger.CreatureHold - 0.1f })
            {
                s.SinceCreature = since;
                Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(danger.Threat(s)), since + " s after its warning");
            }
            s.SinceCreature = MusicDanger.CreatureHold + 1f;
            Assert.AreEqual(IntensityBand.Tension, IntensityModel.BandOf(danger.Threat(s)), "a long while without a strike: it is only hunting you");
            s.CreatureAware = false;
            s.SinceCreature = 8f;
            Assert.Less(danger.Threat(s), 40f, "lost interest (or dead): over after the usual six seconds");
            Assert.Greater(MusicDanger.CreatureHold, 20f, "longer than the maw's slowest gap");
        }

        [Test]
        public void TheLastPlates_ThinTheMusic_OnAndOffWithTheSuitsPulse_AndNeverRaiseIt()
        {
            var danger = new MusicDanger();
            DangerSense s = Quiet();
            s.SinceSeen = 1f;
            s.Armour = 0.5f;
            Assert.IsFalse(danger.OnTheLastPlates);
            float fight = danger.Threat(s);
            s.Armour = PulseClock.StartAt;
            Assert.AreEqual(fight, danger.Threat(s), "the last plates never push the music up: it would bury the pulse");
            Assert.IsTrue(danger.OnTheLastPlates, "the pulse starts here");
            s.Armour = 0.25f;
            danger.Threat(s);
            Assert.IsTrue(danger.OnTheLastPlates, "and holds until it stops");
            s.Armour = PulseClock.StopAt;
            danger.Threat(s);
            Assert.IsFalse(danger.OnTheLastPlates, "the pulse stops here");
            s.Armour = 0.25f;
            danger.Threat(s);
            Assert.IsFalse(danger.OnTheLastPlates, "between the two it stays where it was");

            // Out of the fight it lets go at once, and does not come back on until a fight and low armour meet again.
            s.Armour = 0.1f;
            danger.Threat(s);
            Assert.IsTrue(danger.OnTheLastPlates);
            s.SinceSeen = Never;
            danger.Threat(s);
            Assert.IsFalse(danger.OnTheLastPlates);
            s.SinceSeen = 0.5f;
            s.Armour = 0.25f;
            danger.Threat(s);
            Assert.IsFalse(danger.OnTheLastPlates, "not on again from the old fight");
            Assert.Less(MusicDanger.ThinDb, 0f);
        }

        // ------------------------------------------------------------------ against the music's own rules

        [Test]
        public void TheSteps_SitEitherSideOfTheFightMusicsDoors()
        {
            AudioManifest m = Real();
            Assert.Less(MusicDanger.Hunted, m.CombatExit, "a search after a fight is heard as tension, not as the fight going on");
            Assert.GreaterOrEqual(MusicDanger.Hunted, LayerFrom(m, "explore_regolith", "tension"), "being hunted brings the surface's tension layer");
            Assert.GreaterOrEqual(MusicDanger.Hunted, LayerFrom(m, "mystery_dish", "pulse"), "and the unknown's pulse");
            Assert.GreaterOrEqual(MusicDanger.Fight, m.CombatEnter, "a fight starts the fight music");
            Assert.Less(MusicDanger.FlyingMost, m.CombatEnter, "flying hard never does");
            Assert.Less(MusicDanger.Calm(false, 0f, 20), m.CombatExit);
        }

        [Test]
        public void ASighting_BringsTheFightMusicAtOnce_AndASearchAfterItIsHeardAsTension()
        {
            AudioManifest m = Real();
            var danger = new MusicDanger();
            var model = new IntensityModel();
            const float dt = 0.25f;
            float time = 0f, seenAt = 2f, combatAt = -1f, exitAt = -1f;
            for (int step = 0; step < 160; step++, time += dt)
            {
                DangerSense s = Counted();
                s.SinceSeen = time >= seenAt ? time - seenAt : Never;
                s.Aware = time >= seenAt && time < seenAt + MusicDanger.FightHold + 4f ? 1 : 0;
                // Then they come to look where they last saw you.
                s.Coming = time >= seenAt + MusicDanger.FightHold;
                model.SetThreat(danger.Threat(s));
                model.Tick(dt);
                if (combatAt < 0f && model.Band >= IntensityBand.Combat) combatAt = time;
                if (combatAt >= 0f && exitAt < 0f && model.Value < m.CombatExit) exitAt = time;
            }
            Assert.GreaterOrEqual(combatAt, seenAt);
            Assert.LessOrEqual(combatAt - seenAt, 1.25f, "the fight music comes in with the sighting, before the first shot's beat is long over");
            Assert.Greater(exitAt, seenAt + MusicDanger.FightHold, "it holds through the readout's fight");
            Assert.Less(exitAt, seenAt + MusicDanger.FightHold + 6f, "and then falls under the fight music's exit");
            Assert.AreEqual(IntensityBand.Tension, model.Band, "while they look for you it is tension");
            Assert.AreEqual(MusicDanger.Hunted, model.Value, 0.5f);
        }

        // ------------------------------------------------------------------ every way of being looked for is heard

        static string Source(string file)
        {
            string folder = Folder("Scripts", "Runtime");
            if (folder == null) Assert.Ignore("The game's code was not found from here.");
            return File.ReadAllText(Path.Combine(folder, file));
        }

        static string Body(string source, string signature)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, "not found: " + signature);
            int open = source.IndexOf('{', at), depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }
            return source.Substring(open);
        }

        [Test]
        public void EveryWayAnEnemyComesToLook_IsHeardAsBeingHunted()
        {
            string combat = Source("AI/MannequinCombat.cs");
            string faction = Source("AI/MannequinCombat.Faction.cs");
            string tactics = Source("AI/MannequinCombat.Tactics.cs");
            string spawner = Source("Enemies/EnemySpawner.cs");
            StringAssert.Contains("public bool LookingForPlayer => _heardAt != null;", tactics);
            StringAssert.Contains("l.Combat.LookingForPlayer", Body(spawner, "public void Hunters"), "the music counts it");
            // An alarm (a Scout's flare, a mast's call, a supply drop, overtime's hunt all go through CombatWorld.RaiseAlarm),
            // a restless squad's call and a sound heard each give it a place to look.
            StringAssert.Contains("CombatWorld.Alarm += OnAlarm", faction + combat);
            Assert.IsTrue(Regex.IsMatch(Body(faction, "void OnAlarm("), @"_heardAt\s*=\s*playerAt"), "an alarm");
            Assert.IsTrue(Regex.IsMatch(Body(faction, "public void SendToLook("), @"_heardAt\s*=\s*place"), "a restless squad");
            Assert.IsTrue(Regex.IsMatch(combat, @"_heardAt\s*=\s*CombatWorld\.PlayerEye\.position"), "a sound heard");
        }
    }
}
