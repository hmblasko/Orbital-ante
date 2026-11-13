using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Audio;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The audio rules, tested with no Unity objects: the manifest and every file it names, the intensity number,
    /// the music director's timing, reward limiting, voice limits, vacuum treatment, and the whole five-minute
    /// showcase run through the real rules on a pretend clock.
    /// </summary>
    public class AudioRulesTests
    {
        static string AudioFolder()
        {
            string env = Environment.GetEnvironmentVariable("OA_AUDIO");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            string dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && dir != null; i++)
            {
                foreach (string rel in new[] { "Assets/OrbitalAnte/Resources/Audio", "unity/Assets/OrbitalAnte/Resources/Audio" })
                {
                    string p = Path.Combine(dir, rel);
                    if (Directory.Exists(p)) return p;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        static AudioManifest Real()
        {
            string folder = AudioFolder();
            if (folder == null) Assert.Ignore("The audio folder was not found from here.");
            return AudioManifest.Parse(File.ReadAllText(Path.Combine(folder, "audio_manifest.json")));
        }

        /// <summary>Two pieces at 120 bpm (2 s bars), a combat piece, and one riser cue with a 1.5 s lead-in.</summary>
        static AudioManifest Small()
        {
            const string json = @"{""events"":[], ""music"":{
              ""tracks"":[
                {""id"":""calm"",""bpm"":120,""beatsPerBar"":4,""bars"":8,""seconds"":16,""stems"":[
                    {""id"":""pad"",""clip"":""a"",""lo"":0,""hi"":59,""fadeInBars"":0,""fadeOutBars"":1},
                    {""id"":""drums"",""clip"":""b"",""lo"":30,""hi"":59,""fadeInBars"":1,""fadeOutBars"":1}]},
                {""id"":""fight"",""bpm"":150,""beatsPerBar"":4,""bars"":10,""seconds"":16,""stems"":[
                    {""id"":""all"",""clip"":""c"",""lo"":0,""hi"":100,""fadeInBars"":0,""fadeOutBars"":1},
                    {""id"":""lead"",""clip"":""d"",""lo"":80,""hi"":100,""fadeInBars"":1,""fadeOutBars"":1}]}],
              ""cues"":[{""id"":""riser"",""clip"":""r"",""preSeconds"":1.5,""seconds"":4},
                        {""id"":""hit"",""clip"":""h"",""preSeconds"":0,""seconds"":2},
                        {""id"":""ending"",""clip"":""e"",""preSeconds"":0,""seconds"":5}],
              ""contexts"":[{""id"":""walk"",""track"":""calm"",""maxLoops"":2,""restSeconds"":20,""combat"":true},
                            {""id"":""menu"",""track"":""calm"",""maxLoops"":0,""restSeconds"":0,""combat"":false},
                            {""id"":""void"",""track"":"""",""maxLoops"":0,""restSeconds"":0,""combat"":true}],
              ""combatTrack"":""fight"",""combatEnter"":60,""combatExit"":38,""combatMinSeconds"":10,""ambushIntensity"":85,
              ""transitions"":[
                {""from"":""*"",""to"":""fight"",""quantize"":""beat"",""cue"":""riser"",""ambushCue"":""hit"",""fadeOut"":1.2,""fadeIn"":0,""hush"":0},
                {""from"":""fight"",""to"":""*"",""quantize"":""bar"",""cue"":""ending"",""ambushCue"":"""",""fadeOut"":0.1,""fadeIn"":3,""hush"":4},
                {""from"":""*"",""to"":""*"",""quantize"":""bar"",""cue"":"""",""ambushCue"":"""",""fadeOut"":2,""fadeIn"":2,""hush"":0.5}]}}";
            return AudioManifest.Parse(json);
        }

        static List<MusicCommand> Drive(MusicBrain brain, double from, double to, Func<double, float> intensity, double step = 0.02)
        {
            var all = new List<MusicCommand>();
            var frame = new List<MusicCommand>();
            for (double t = from; t < to; t += step)
            {
                frame.Clear();
                brain.Update(t, intensity(t), frame);
                foreach (MusicCommand c in frame)
                {
                    Assert.GreaterOrEqual(c.Time, t - 1e-9, "a command was scheduled in the past: " + c);
                    all.Add(c);
                }
            }
            return all;
        }

        static bool OnGrid(double time, double origin, double unit) => Math.Abs(Math.IEEERemainder(time - origin, unit)) < 1e-6;

        // ------------------------------------------------------------------ json and manifest

        [Test]
        public void MiniJson_ReadsNestedValues()
        {
            var o = MiniJson.Obj(MiniJson.Parse("{\"a\": [1, 2.5, -3e2], \"b\": {\"c\": \"x\\ny\", \"d\": true, \"e\": null}}"));
            Assert.AreEqual(3, MiniJson.List(o, "a").Count);
            Assert.AreEqual(-300.0, (double)MiniJson.List(o, "a")[2]);
            Assert.AreEqual("x\ny", MiniJson.Str(MiniJson.Obj(o["b"]), "c"));
            Assert.IsTrue(MiniJson.Bool(MiniJson.Obj(o["b"]), "d"));
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\": }"));
        }

        [Test]
        public void Manifest_EveryNamedFileExists_AndNothingIsWrong()
        {
            AudioManifest m = Real();
            string folder = AudioFolder();
            string resources = Path.GetDirectoryName(folder);
            var problems = new List<string>();
            m.Validate(problems, path => File.Exists(Path.Combine(resources, path + ".wav")) || File.Exists(Path.Combine(resources, path + ".ogg")));
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.GreaterOrEqual(m.Events.Count, 30, "the slice promises at least 30 sound effects");
            Assert.GreaterOrEqual(m.Tracks.Count, 4, "the slice promises at least 4 compositions");
        }

        [Test]
        public void Manifest_EveryAudioFileOnDiskIsUsed()
        {
            AudioManifest m = Real();
            string folder = AudioFolder();
            string resources = Path.GetDirectoryName(folder);
            var used = new HashSet<string>();
            foreach (AudioEventDef e in m.Events.Values) foreach (string c in e.Clips) used.Add(c);
            foreach (MusicTrackDef t in m.Tracks.Values) foreach (StemDef s in t.Stems) used.Add(s.Clip);
            foreach (CueDef c in m.Cues.Values) used.Add(c.Clip);
            var orphans = new List<string>();
            foreach (string file in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file);
                if (ext != ".wav" && ext != ".ogg") continue;
                string rel = file.Substring(resources.Length + 1).Replace('\\', '/');
                rel = rel.Substring(0, rel.Length - ext.Length);
                if (!used.Contains(rel)) orphans.Add(rel);
            }
            Assert.IsEmpty(orphans, "audio files nothing refers to:\n" + string.Join("\n", orphans));
        }

        [Test]
        public void Manifest_TheSliceWeaponsAllHaveTheirOwnSound()
        {
            AudioManifest m = Real();
            foreach (string w in new[] { "rifle", "smg", "handcannon", "sniper", "suppressed", "laser_revolver", "raygun", "laser_shotgun" })
            {
                Assert.IsTrue(m.Events.ContainsKey("weapon." + w + ".shot"), w);
                Assert.GreaterOrEqual(m.Events["weapon." + w + ".shot"].Clips.Length, 2, w + " needs variations so it does not machine-gun");
            }
            Assert.IsTrue(m.Events.ContainsKey("weapon.gravity.punt"));
        }

        // ------------------------------------------------------------------ intensity

        [Test]
        public void Intensity_FiringAtNothingNeverStartsABattle()
        {
            var im = new IntensityModel();
            for (int i = 0; i < 3000; i++)
            {
                if (i % 5 == 0) im.Report(IntensityEvent.PlayerShot);
                im.Tick(0.02f);
                Assert.Less(im.Value, 40f);
            }
        }

        [Test]
        public void Intensity_TakingFireReachesCombatFast_ThenCoolsSlowly()
        {
            var im = new IntensityModel();
            float seconds = 0f;
            while (im.Value < 60f && seconds < 10f)
            {
                if (((int)(seconds * 50)) % 10 == 0) im.Report(IntensityEvent.DamageTaken);
                im.Tick(0.02f);
                seconds += 0.02f;
            }
            Assert.Less(seconds, 2.5f, "an ambush should be felt in the music within a couple of seconds");
            float peak = im.Value;
            for (int i = 0; i < 100; i++) im.Tick(0.02f);
            Assert.Greater(im.Value, peak - 20f, "two quiet seconds must not drop the music out of the fight");
            for (int i = 0; i < 2000; i++) im.Tick(0.02f);
            Assert.AreEqual(0f, im.Value, 0.001f);
            Assert.AreEqual(IntensityBand.Solitude, im.Band);
        }

        [Test]
        public void Intensity_BandDoesNotFlickerAtAnEdge()
        {
            var im = new IntensityModel();
            im.SetThreat(60f);
            for (int i = 0; i < 200; i++) im.Tick(0.02f);
            int changes = 0;
            IntensityBand last = im.Band;
            for (int i = 0; i < 2000; i++)
            {
                im.SetThreat(i / 10 % 2 == 0 ? 58f : 62f);
                im.Tick(0.02f);
                if (im.Band != last) { changes++; last = im.Band; }
            }
            Assert.LessOrEqual(changes, 2);
        }

        [Test]
        public void Intensity_TargetPracticeIsTenseAtMost_ButAFightThatShootsBackIsAFight()
        {
            var im = new IntensityModel();
            for (int i = 0; i < 1500; i++)
            {
                if (i % 5 == 0) { im.Report(IntensityEvent.PlayerShot); im.Report(IntensityEvent.HitDealt); }
                if (i % 50 == 0) { im.Report(IntensityEvent.PartSevered); im.Report(IntensityEvent.Kill); }
                im.Tick(0.02f);
                Assert.Less(im.Value, 60f, "shooting something that never shoots back must not start combat music");
            }
            im.Report(IntensityEvent.EnemyShotNearby);
            for (int i = 0; i < 150; i++)
            {
                if (i % 5 == 0) im.Report(IntensityEvent.HitDealt);
                if (i % 25 == 0) im.Report(IntensityEvent.Kill);
                im.Tick(0.02f);
            }
            Assert.GreaterOrEqual(im.Value, 60f);
            // A crash while flying is a fright, not a fight.
            var crash = new IntensityModel();
            crash.Report(IntensityEvent.Crash);
            crash.Report(IntensityEvent.Crash);
            for (int i = 0; i < 200; i++) { crash.Tick(0.02f); Assert.Less(crash.Value, 60f); }
        }

        [Test]
        public void Music_AFightThatFlaresUpAgainInterruptsTheHush()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            // A fight, quiet, the ending plays at about 26 s, and one second into the hush the shooting starts again.
            double endedAt = -1;
            var all = new List<MusicCommand>();
            var frame = new List<MusicCommand>();
            float level = 10f;
            for (double t = 10; t < 60; t += 0.02)
            {
                level = t > 14 && t < 16 ? 75f : (endedAt > 0 && t > endedAt + 1.0 ? 75f : 10f);
                frame.Clear();
                brain.Update(t, level, frame);
                foreach (MusicCommand c in frame)
                {
                    all.Add(c);
                    if (c.Kind == MusicCommandKind.PlayCue && c.Cue == "ending" && endedAt < 0) endedAt = c.Time;
                }
            }
            List<MusicCommand> fights = all.FindAll(x => x.Kind == MusicCommandKind.StartTrack && x.Track == "fight");
            Assert.AreEqual(2, fights.Count);
            Assert.Less(fights[1].Time - (endedAt + 1.0), 2.6, "combat music must come back within its riser's length, not after the hush");
        }

        [Test]
        public void Music_ASuddenThreatIsRecognisedAsAnAmbushFromWhereTheIntensityIsHeading()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            var im = new IntensityModel();
            im.SetThreat(24f);
            var all = new List<MusicCommand>();
            var frame = new List<MusicCommand>();
            for (double t = 10; t < 25; t += 0.02)
            {
                if (t > 15) im.SetThreat(95f);
                im.Tick(0.02f);
                frame.Clear();
                brain.Update(t, im.Value, frame, im.Target);
                all.AddRange(frame);
            }
            Assert.AreEqual("hit", all.Find(x => x.Kind == MusicCommandKind.PlayCue).Cue);
        }

        [Test]
        public void Music_APieceComingBackPicksUpAtItsNextEntryPoint_StillOnItsOwnGrid()
        {
            AudioManifest m = Small();
            m.Tracks["calm"].EntryBars = new[] { 1, 5 };
            var brain = new MusicBrain(m);
            brain.SetContext("walk");
            List<MusicCommand> c = Drive(brain, 10, 60, t => t > 14 && t < 16 ? 75f : 10f);
            List<MusicCommand> calm = c.FindAll(x => x.Kind == MusicCommandKind.StartTrack && x.Track == "calm");
            Assert.AreEqual(0.0, calm[0].Offset, 1e-9);
            Assert.AreEqual(8.0, calm[1].Offset, 1e-9, "second time it starts from bar 5 (four 2-second bars in)");
            Assert.AreEqual(0.0, c.Find(x => x.Track == "fight" && x.Kind == MusicCommandKind.StartTrack).Offset, 1e-9, "combat always starts from the top");
            Assert.AreEqual(calm[1].Time - 8.0, brain.TrackStart, 1e-9);
        }

        [Test]
        public void Music_DangerThatComesAndGoesDoesNotRestartTheFightMusicEachTime()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            // Danger high for 5 s, low for 3 s, over and over for two minutes: one fight, not fifteen.
            List<MusicCommand> c = Drive(brain, 10, 130, t => ((t - 10) % 8) < 5 ? 75f : 15f);
            Assert.AreEqual(1, c.FindAll(x => x.Kind == MusicCommandKind.StartTrack && x.Track == "fight").Count);
            Assert.AreEqual(0, c.FindAll(x => x.Kind == MusicCommandKind.PlayCue && x.Cue == "ending").Count);
        }

        [Test]
        public void Music_AFightThatFlaresUpAsTheEndingIsCountedIn_LetsTheEndingLandThenFadesItUnderTheRiser()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            var all = new List<MusicCommand>();
            var frame = new List<MusicCommand>();
            double stopAt = -1;
            for (double t = 10; t < 60; t += 0.02)
            {
                // The moment the ending is ordered (before it has landed) the shooting starts again.
                float level = t > 14 && t < 16 ? 75f : (stopAt > 0 ? 75f : 10f);
                frame.Clear();
                brain.Update(t, level, frame);
                foreach (MusicCommand c in frame)
                {
                    all.Add(c);
                    if (c.Kind == MusicCommandKind.StopTrack && c.Track == "fight" && stopAt < 0) stopAt = c.Time + c.Seconds;
                }
            }
            List<MusicCommand> fights = all.FindAll(x => x.Kind == MusicCommandKind.StartTrack && x.Track == "fight");
            Assert.AreEqual(2, fights.Count);
            MusicCommand riser = all.FindLast(x => x.Kind == MusicCommandKind.PlayCue && x.Cue == "riser");
            Assert.GreaterOrEqual(riser.Time, stopAt - 1e-6, "the new riser must not start before the old fight music has finished leaving");
            MusicCommand fade = all.Find(x => x.Kind == MusicCommandKind.StopCue);
            Assert.AreEqual("ending", fade.Cue, "the ending still ringing is faded out under the riser");
            Assert.Less(fade.Time, fights[1].Time);
        }

        [Test]
        public void Intensity_DistantFireDoesNotMakeItThePlayersFight()
        {
            var im = new IntensityModel();
            for (int i = 0; i < 1500; i++)
            {
                if (i % 10 == 0) im.Report(IntensityEvent.EnemyShotNearby, 0.3f);
                if (i % 5 == 0) im.Report(IntensityEvent.HitDealt);
                im.Tick(0.02f);
                Assert.Less(im.Value, 60f);
            }
        }

        [Test]
        public void Intensity_BandsMatchTheBrief()
        {
            Assert.AreEqual(IntensityBand.Solitude, IntensityModel.BandOf(19f));
            Assert.AreEqual(IntensityBand.Exploration, IntensityModel.BandOf(20f));
            Assert.AreEqual(IntensityBand.Tension, IntensityModel.BandOf(59f));
            Assert.AreEqual(IntensityBand.Combat, IntensityModel.BandOf(60f));
            Assert.AreEqual(IntensityBand.Chaos, IntensityModel.BandOf(80f));
        }

        // ------------------------------------------------------------------ music director

        [Test]
        public void Music_FirstPieceStartsWithItsFloorLayersOnly()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            List<MusicCommand> c = Drive(brain, 10, 10.1, t => 10f);
            Assert.AreEqual(MusicCommandKind.StartTrack, c[0].Kind);
            Assert.AreEqual("calm", c[0].Track);
            Assert.AreEqual(1f, c.Find(x => x.Stem == "pad").Gain);
            Assert.AreEqual(0f, c.Find(x => x.Stem == "drums").Gain);
        }

        [Test]
        public void Music_LayersEnterAndLeaveOnBarLines()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            List<MusicCommand> c = Drive(brain, 10, 26, t => t < 13.3 ? 10f : t < 20.7 ? 45f : 10f);
            double start = c[0].Time;
            List<MusicCommand> drums = c.FindAll(x => x.Kind == MusicCommandKind.StemGain && x.Stem == "drums" && x.Time > start);
            Assert.AreEqual(2, drums.Count);
            Assert.AreEqual(1f, drums[0].Gain);
            Assert.AreEqual(0f, drums[1].Gain);
            foreach (MusicCommand d in drums) Assert.IsTrue(OnGrid(d.Time, start, 2.0), "not on a bar line: " + d);
        }

        [Test]
        public void Music_CombatRiserStartsOnABeat_AndThePieceStartsWhereItLands()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            List<MusicCommand> c = Drive(brain, 10, 30, t => t < 17.37 ? 30f : 70f);
            double calmStart = c[0].Time;
            MusicCommand cue = c.Find(x => x.Kind == MusicCommandKind.PlayCue);
            MusicCommand fight = c.Find(x => x.Kind == MusicCommandKind.StartTrack && x.Track == "fight");
            MusicCommand stop = c.Find(x => x.Kind == MusicCommandKind.StopTrack && x.Track == "calm");
            Assert.AreEqual("riser", cue.Cue);
            Assert.IsTrue(OnGrid(cue.Time, calmStart, 0.5), "the riser must start on a beat of the piece it interrupts");
            Assert.Less(cue.Time - 17.37, 0.5 + MusicBrain.Lead + 0.05, "the riser must not keep the player waiting");
            Assert.AreEqual(cue.Time + 1.5, fight.Time, 1e-9, "combat must start exactly where the riser lands");
            Assert.AreEqual(fight.Time, stop.Time + stop.Seconds, 1e-9, "the old piece must be gone exactly as the new one starts");
        }

        [Test]
        public void Music_AmbushSkipsTheRiser()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            List<MusicCommand> c = Drive(brain, 10, 20, t => t < 15.1 ? 30f : 95f);
            MusicCommand cue = c.Find(x => x.Kind == MusicCommandKind.PlayCue);
            MusicCommand fight = c.Find(x => x.Kind == MusicCommandKind.StartTrack && x.Track == "fight");
            Assert.AreEqual("hit", cue.Cue);
            Assert.AreEqual(cue.Time, fight.Time, 1e-9);
            Assert.Less(fight.Time - 15.1, 0.5 + MusicBrain.Lead + 0.05);
        }

        [Test]
        public void Music_CombatStaysForItsMinimum_ThenEndsOnABarWithAnEndingAndAHush()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            // Danger for two seconds only: the music must not pop in and straight back out.
            List<MusicCommand> c = Drive(brain, 10, 50, t => t > 14 && t < 16 ? 75f : 10f);
            MusicCommand fight = c.Find(x => x.Kind == MusicCommandKind.StartTrack && x.Track == "fight");
            MusicCommand stop = c.Find(x => x.Kind == MusicCommandKind.StopTrack && x.Track == "fight");
            MusicCommand ending = c.Find(x => x.Kind == MusicCommandKind.PlayCue && x.Cue == "ending");
            MusicCommand back = c.FindLast(x => x.Kind == MusicCommandKind.StartTrack && x.Track == "calm");
            double end = stop.Time + stop.Seconds;
            Assert.GreaterOrEqual(end - fight.Time, 8.0, "combat music left too soon");
            Assert.IsTrue(OnGrid(end, fight.Time, 1.6), "combat must end on one of its own bar lines");
            Assert.AreEqual(end, ending.Time, 1e-9);
            Assert.AreEqual(end + 4.0, back.Time, 1e-9, "the calm piece returns after the hush");
            Assert.AreEqual(3.0, back.Seconds, 1e-9);
        }

        [Test]
        public void Music_DoesNotFlapWhenIntensityHoversAtTheThreshold()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            List<MusicCommand> c = Drive(brain, 10, 130, t => ((int)(t * 2)) % 2 == 0 ? 57f : 63f);
            int starts = c.FindAll(x => x.Kind == MusicCommandKind.StartTrack).Count;
            Assert.LessOrEqual(starts, 2, "hovering around the combat level must not keep swapping pieces");
        }

        [Test]
        public void Music_NoCombatInTheMenu()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("menu");
            List<MusicCommand> c = Drive(brain, 10, 30, t => 90f);
            Assert.IsFalse(c.Exists(x => x.Track == "fight"));
        }

        [Test]
        public void Music_RestsAfterItsLoops_AndComesBackQuietly()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("walk");
            List<MusicCommand> c = Drive(brain, 10, 90, t => 10f);
            double start = c[0].Time;
            MusicCommand stop = c.Find(x => x.Kind == MusicCommandKind.StopTrack);
            Assert.AreEqual(start + 32.0, stop.Time + stop.Seconds, 1e-6, "it should stop exactly at the end of its second time round");
            MusicCommand again = c.FindLast(x => x.Kind == MusicCommandKind.StartTrack);
            Assert.GreaterOrEqual(again.Time, start + 32.0 + 20.0 - 1e-6, "the rest was cut short");
            Assert.GreaterOrEqual(again.Seconds, 6.0, "after a rest the piece should fade in gently");
            Assert.IsFalse(c.Exists(x => x.Kind == MusicCommandKind.PlayCue), "no riser for a piece returning from rest");
        }

        [Test]
        public void Music_BoundariesStayOnTheGridAfterManyLoops()
        {
            var brain = new MusicBrain(Small());
            brain.SetContext("menu");
            Drive(brain, 10, 10.1, t => 10f);
            double start = brain.TrackStart;
            double t0 = start + 16.0 * 400 + 3.3;
            Assert.AreEqual(start + 16.0 * 400 + 4.0, brain.NextBoundary(t0, "bar"), 1e-6);
            Assert.AreEqual(start + 16.0 * 400 + 3.5, brain.NextBoundary(t0, "beat"), 1e-6);
            Assert.AreEqual(start + 16.0 * 400 + 8.0, brain.NextBoundary(t0, "phrase"), 1e-6);
        }

        [Test]
        public void GainLine_ScheduledFadesStartOnTimeFromWhereverTheLevelIs()
        {
            var g = new GainLine();
            g.Reset(0f);
            g.Add(1.0, 2.0, 1f);
            Assert.AreEqual(0f, g.At(0.5), 1e-6);
            Assert.AreEqual(0.5f, g.At(2.0), 1e-6);
            g.Add(2.0, 1.0, 0f);
            Assert.AreEqual(0.25f, g.At(2.5), 1e-6);
            Assert.AreEqual(0f, g.At(5.0), 1e-6);
            Assert.IsTrue(g.Idle);
        }

        // ------------------------------------------------------------------ rewards, voices, acoustics

        [Test]
        public void Rewards_BiggestGoesFirst_SmallStaleOnesAreDropped()
        {
            var r = new RewardRules();
            r.Request(RewardKind.Tier1, 0);
            r.Request(RewardKind.Tier4, 0);
            r.Request(RewardKind.Tier2, 0);
            Assert.IsTrue(r.Poll(0, out RewardKind first));
            Assert.AreEqual(RewardKind.Tier4, first);
            Assert.IsFalse(r.Poll(0.1, out _), "one reward voice at a time");
            // By the time the big one has had its moment the small ones are old news.
            Assert.IsFalse(r.Poll(1.0, out _));
            Assert.AreEqual(2, r.Dropped);
        }

        [Test]
        public void Rewards_ScoreTicksAreCappedToACountableRate()
        {
            var r = new RewardRules();
            int played = 0;
            for (int i = 0; i < 1000; i++)
            {
                double t = i * 0.001;
                r.Request(RewardKind.PointsTick, t);
                if (r.Poll(t, out _)) played++;
            }
            Assert.LessOrEqual(played, 20);
            Assert.GreaterOrEqual(played, 15);
            Assert.Greater(r.TickStep, 5, "consecutive ticks climb");
        }

        [Test]
        public void Voices_AreCapped_AndTheLeastImportantGivesWay()
        {
            var a = new VoiceAllocator(2);
            var low = new AudioEventDef { Id = "low", Priority = 200, MaxVoices = 4 };
            var high = new AudioEventDef { Id = "high", Priority = 10, MaxVoices = 4 };
            Assert.AreEqual(0, a.Request(low, 0, 5));
            Assert.AreEqual(1, a.Request(high, 0, 5));
            Assert.AreEqual(0, a.Request(high, 0.1, 5), "an important sound takes the unimportant voice");
            Assert.AreEqual(-1, a.Request(low, 0.2, 5), "an unimportant sound is turned away when everything playing matters more");
            Assert.AreEqual(2, a.Active(0.3));
            Assert.AreEqual(0, a.Active(6));
        }

        [Test]
        public void Voices_PerEventLimitAndCooldown()
        {
            var a = new VoiceAllocator(8);
            var shot = new AudioEventDef { Id = "shot", Priority = 50, MaxVoices = 2, CooldownMs = 40 };
            Assert.GreaterOrEqual(a.Request(shot, 0, 1), 0);
            Assert.AreEqual(-1, a.Request(shot, 0.01, 1), "inside the cooldown");
            int second = a.Request(shot, 0.05, 1);
            int third = a.Request(shot, 0.10, 1);
            Assert.GreaterOrEqual(second, 0);
            Assert.AreEqual(0, third, "the third shot reuses the oldest of its own two voices");
            Assert.AreEqual(2, a.Active(0.11));
        }

        [Test]
        public void Acoustics_VacuumIsDullAndQuietButNotSilent_AndTheSuitNeverChanges()
        {
            var vacuum = new ListenerSpace { Air = 0f, Enclosure = Enclosure.Open };
            var earth = new ListenerSpace { Air = 1f, Enclosure = Enclosure.Open };
            var mars = new ListenerSpace { Air = 0.3f, Enclosure = Enclosure.Open };
            SoundTreatment far = AcousticsModel.Treat(VacuumPolicy.Air, vacuum);
            SoundTreatment mine = AcousticsModel.Treat(VacuumPolicy.Self, vacuum);
            Assert.Less(far.Cutoff, 1000f);
            Assert.Less(far.GainDb, -6f);
            Assert.Greater(far.GainDb, -30f);
            Assert.Greater(mine.Cutoff, far.Cutoff, "your own gun reaches you better than someone else's");
            Assert.Greater(mine.GainDb, far.GainDb);
            Assert.AreEqual(0f, far.ReverbSend, 1e-6, "no air, no echo");
            Assert.AreEqual(AcousticsModel.Open, AcousticsModel.Treat(VacuumPolicy.Air, earth).Cutoff, 1f);
            Assert.AreEqual(0f, AcousticsModel.Treat(VacuumPolicy.Suit, vacuum).GainDb);
            Assert.AreEqual(AcousticsModel.Open, AcousticsModel.Treat(VacuumPolicy.Ui, vacuum).Cutoff);
            SoundTreatment thin = AcousticsModel.Treat(VacuumPolicy.Air, mars);
            Assert.Less(thin.Cutoff, AcousticsModel.Open);
            Assert.Greater(thin.Cutoff, far.Cutoff);
            var cave = new ListenerSpace { Air = 1f, Enclosure = Enclosure.Cave };
            Assert.Greater(AcousticsModel.Treat(VacuumPolicy.Air, cave).ReverbSend, AcousticsModel.Treat(VacuumPolicy.Air, earth).ReverbSend);
        }

        [Test]
        public void Mix_SliderIsSilentAtZeroAndFullAtOne()
        {
            Assert.AreEqual(0f, MixMath.SliderToGain(0f));
            Assert.AreEqual(1f, MixMath.SliderToGain(1f));
            Assert.AreEqual(0.25f, MixMath.SliderToGain(0.5f), 1e-6);
            Assert.AreEqual(0.5f, MixMath.DbToGain(-6.0206f), 1e-4);
            Assert.AreEqual(AudioVolume.Music, MixMath.SliderFor(AudioBus.Music));
            Assert.AreEqual(AudioVolume.Effects, MixMath.SliderFor(AudioBus.Weapons));
            Assert.AreEqual(AudioVolume.Dialogue, MixMath.SliderFor(AudioBus.Dialogue));
        }

        // ------------------------------------------------------------------ the showcase, end to end

        [Test]
        public void Showcase_EverySoundItAsksForExists()
        {
            AudioManifest m = Real();
            foreach (DemoStep s in DemoTimeline.Build())
            {
                if (s.Kind == DemoStepKind.Play || s.Kind == DemoStepKind.LoopOn || s.Kind == DemoStepKind.LoopOff || s.Kind == DemoStepKind.LoopLevel)
                    Assert.IsTrue(m.Events.ContainsKey(s.Text), "unknown event in the showcase: " + s.Text);
                if (s.Kind == DemoStepKind.Context) Assert.IsTrue(m.Contexts.ContainsKey(s.Text), "unknown situation: " + s.Text);
                if (s.Kind == DemoStepKind.Reward) Assert.IsTrue(m.Events.ContainsKey(RewardRules.EventId((RewardKind)s.Number)));
            }
        }

        [Test]
        public void Showcase_TheDirectorScoresItInTheRightOrder_WithEveryChangeOnTheGrid()
        {
            AudioManifest m = Real();
            var sim = new DemoSimulator();
            sim.Run(m, DemoTimeline.Build());

            var order = new List<string>();
            foreach (MusicCommand c in sim.Music) if (c.Kind == MusicCommandKind.StartTrack) order.Add(c.Track);
            CollectionAssert.AreEqual(new[]
            {
                "explore_regolith", "mystery_dish", "explore_regolith", "combat_all_in", "explore_regolith", "flight_burn_window", "explore_regolith",
            }, order, "pieces started: " + string.Join(", ", order));

            // Combat arrives with its riser, and promptly once the fight starts at 2:13.
            MusicCommand combat = sim.Music.Find(c => c.Kind == MusicCommandKind.StartTrack && c.Track == "combat_all_in");
            MusicCommand riser = sim.Music.Find(c => c.Kind == MusicCommandKind.PlayCue && c.Cue == "to_combat");
            Assert.AreEqual(riser.Time + m.Cues["to_combat"].PreSeconds, combat.Time, 1e-6);
            Assert.That(combat.Time, Is.InRange(133.0, 141.0));
            // It ends with the written ending, after the last kill at 2:52, and the calm piece waits for the hush.
            MusicCommand ending = sim.Music.Find(c => c.Kind == MusicCommandKind.PlayCue && c.Cue == "resolve_combat");
            Assert.That(ending.Time, Is.InRange(172.0, 190.0));
            Assert.IsTrue(sim.Music.Exists(c => c.Kind == MusicCommandKind.PlayCue && c.Cue == "to_flight"));
            Assert.IsTrue(sim.Music.Exists(c => c.Kind == MusicCommandKind.PlayCue && c.Cue == "to_calm"));

            // Every layer change happens on a bar line of its piece.
            var starts = new Dictionary<string, double>();
            foreach (MusicCommand c in sim.Music)
            {
                if (c.Kind == MusicCommandKind.StartTrack) starts[c.Track] = c.Time;
                if (c.Kind == MusicCommandKind.StemGain && c.Time > starts[c.Track] + 1e-6)
                    Assert.IsTrue(OnGrid(c.Time, starts[c.Track], m.Tracks[c.Track].BarSeconds), "layer change off the bar: " + c);
            }
            // The chaos layer of the combat piece is reached, and the lead before it.
            Assert.IsTrue(sim.Music.Exists(c => c.Kind == MusicCommandKind.StemGain && c.Stem == "chaos" && c.Gain > 0.5f));
            Assert.IsTrue(sim.Music.Exists(c => c.Kind == MusicCommandKind.StemGain && c.Stem == "lead" && c.Track == "combat_all_in" && c.Gain > 0.5f));

            // All five intensity bands are visited.
            var bands = new HashSet<IntensityBand>();
            foreach (DemoSimulator.Trace t in sim.Traces) bands.Add(t.Band);
            Assert.AreEqual(5, bands.Count);
            Assert.Greater(sim.Sounds.Count, 250);
        }
    }
}
