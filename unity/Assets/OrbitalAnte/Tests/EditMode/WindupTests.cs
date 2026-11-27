using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Audio;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Creature tells (Ralph loop pass 2, 27 Nov 2026): every creature that strikes warns in two sounds of its own, a
    /// wind-up as it starts and a hard mark the very frame it takes aim, and the visor shows the warning read from its
    /// own brain: an arc that counts down while it still follows the player, snaps narrow as it takes aim, and dims
    /// once its strike can no longer reach them.
    /// </summary>
    public class WindupTests
    {
        const double Dt = 1.0 / 60.0;

        // ------------------------------------------------------------------ the sounds

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

        static AudioManifest Manifest(string folder) => AudioManifest.Parse(File.ReadAllText(Path.Combine(folder, "audio_manifest.json")));

        /// <summary>Each creature's two sounds, its wind-up and its aim. tools/audio/qa.py measures the same nine pairs.</summary>
        static IEnumerable<(string warn, string aim)> Warnings()
        {
            yield return (Windups.StriderWarning, Windups.StriderAim);
            yield return (Windups.SprinterWarning, Windups.SprinterAim);
            yield return (Windups.RollerWarning, Windups.RollerAim);
            yield return (Windups.HunterWarning, Windups.HunterAim);
            yield return (Windups.MawWarning, Windups.MawAim);
            yield return (Windups.BiterWarning, Windups.BiterAim);
            yield return (Windups.ScreamerWarning, Windups.ScreamerAim);
            yield return (Windups.AmbusherWarning, Windups.AmbusherAim);
            yield return (Windups.StalkerWarning, Windups.StalkerAim);
        }

        [Test]
        public void EveryCreature_HasAWindupAndAnAimOfItsOwn()
        {
            string folder = AudioFolder();
            if (folder == null) Assert.Ignore("The audio folder was not found from here.");
            AudioManifest m = Manifest(folder);
            var clips = new HashSet<string>();
            var aims = new HashSet<string>();
            int pairs = 0;
            foreach (var (warn, aim) in Warnings())
            {
                foreach (string id in new[] { warn, aim })
                {
                    Assert.IsTrue(m.Events.TryGetValue(id, out AudioEventDef def), id + " has no sound: it would fall silent");
                    Assert.Greater(def.Clips.Length, 0, id);
                    foreach (string c in def.Clips) Assert.IsTrue(clips.Add(c), id + " shares a file with another warning");
                    Assert.IsFalse(def.Loop, id);
                    Assert.LessOrEqual(def.CooldownMs, 100, id + ": a long cooldown would silence the next creature's warning");
                }
                Assert.AreEqual(m.Events[warn].VolumeDb, m.Events[aim].VolumeDb, 1e-3, aim + " is made as one sound with " + warn + " and plays at its level");
                aims.Add(aim);
                pairs++;
            }
            Assert.AreEqual(9, pairs);
            // No creature's aim is made and then never played.
            foreach (string id in m.Events.Keys)
                if (id.StartsWith("creature.") && id.EndsWith(".aim")) Assert.IsTrue(aims.Contains(id), id + " is made but no creature plays it");
            // The walkers' are found by their model's name.
            Assert.AreEqual(Windups.StriderWarning, Windups.WalkerWarning(WalkerLayout.Strider.Model));
            Assert.AreEqual(Windups.StriderAim, Windups.WalkerAim(WalkerLayout.Strider.Model));
            Assert.AreEqual(Windups.SprinterWarning, Windups.WalkerWarning(WalkerLayout.Sprinter.Model));
            Assert.AreEqual(Windups.SprinterAim, Windups.WalkerAim(WalkerLayout.Sprinter.Model));
        }

        [Test]
        public void EveryAim_MarksTheMomentItPlays_AndStandsWellOverItsWindup()
        {
            string folder = AudioFolder();
            if (folder == null) Assert.Ignore("The audio folder was not found from here.");
            AudioManifest m = Manifest(folder);
            foreach (var (warn, aim) in Warnings())
            {
                AudioEventDef w = m.Events[warn], a = m.Events[aim];
                foreach (string clip in a.Clips)
                {
                    // Measured above 200 Hz, as the Deck's own speakers play it.
                    float[] x = HighPass(ReadWav(PathOf(folder, clip), out int rate), rate, 200.0);
                    Loudest(x, rate, out double at, out double markDb);
                    Assert.LessOrEqual(at, 0.045, clip + ": its mark is " + at.ToString("0.000") + " s in, but it plays the very frame the creature takes aim");
                    foreach (string windup in w.Clips)
                    {
                        float[] y = HighPass(ReadWav(PathOf(folder, windup), out int wr), wr, 200.0);
                        Loudest(y, wr, out _, out double windDb);
                        double over = markDb + a.VolumeDb - (windDb + w.VolumeDb);
                        Assert.GreaterOrEqual(over, 5.0, aim + " stands only " + over.ToString("0.0") + " dB over " + warn + ": the mark must be heard through it");
                        // The wind-up is heard too: its loudest tenth of a second is no whisper on the Deck's speakers.
                        double heard = LoudestStretch(y, wr / 10);
                        Assert.GreaterOrEqual(heard, -35.0, windup + " is too quiet to be heard: its loudest 100 ms is " + heard.ToString("0.0") + " dB");
                    }
                }
            }
        }

        static string PathOf(string folder, string clip)
        {
            string path = Path.Combine(Path.GetDirectoryName(folder), clip + ".wav");
            Assert.IsTrue(File.Exists(path), path);
            return path;
        }

        /// <summary>The loudest 5 ms of a sound: where it starts (seconds) and how loud it is (dB of its mean square).</summary>
        static void Loudest(float[] x, int rate, out double at, out double db)
        {
            int w = rate / 200, best = 0;
            double loudest = -1.0;
            for (int i = 0; i + w <= x.Length; i += w / 2)
            {
                double e = 0;
                for (int k = 0; k < w; k++) e += x[i + k] * x[i + k];
                if (e > loudest) { loudest = e; best = i; }
            }
            at = best / (double)rate;
            db = 10.0 * Math.Log10(Math.Max(loudest, 0.0) / Math.Max(1, w) + 1e-20);
        }

        /// <summary>The loudest stretch of <paramref name="w"/> samples, in dB of its mean square.</summary>
        static double LoudestStretch(float[] x, int w)
        {
            double e = 0, best = 0;
            for (int i = 0; i < x.Length; i++)
            {
                e += x[i] * (double)x[i];
                if (i >= w) e -= x[i - w] * (double)x[i - w];
                if (i >= w - 1 && e > best) best = e;
            }
            return 10.0 * Math.Log10(best / Math.Max(1, w) + 1e-20);
        }

        /// <summary>A second-order high-pass (Butterworth), as a small speaker hears.</summary>
        static float[] HighPass(float[] x, int rate, double hz)
        {
            double w0 = 2.0 * Math.PI * hz / rate, cos = Math.Cos(w0), alpha = Math.Sin(w0) / (2.0 * Math.Sqrt(0.5));
            double a0 = 1.0 + alpha, b0 = (1.0 + cos) / 2.0 / a0, b1 = -(1.0 + cos) / a0, b2 = b0, a1 = -2.0 * cos / a0, a2 = (1.0 - alpha) / a0;
            var y = new float[x.Length];
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double v = b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x[i]; y2 = y1; y1 = v;
                y[i] = (float)v;
            }
            return y;
        }

        /// <summary>Reads a 16-bit PCM WAV file (as the sound build writes them) into one channel.</summary>
        static float[] ReadWav(string path, out int rate)
        {
            byte[] b = File.ReadAllBytes(path);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(b, 0, 4), path);
            int channels = 1, bits = 16;
            rate = 48000;
            int p = 12;
            while (p + 8 <= b.Length)
            {
                string chunk = System.Text.Encoding.ASCII.GetString(b, p, 4);
                int size = BitConverter.ToInt32(b, p + 4);
                if (chunk == "fmt ")
                {
                    channels = BitConverter.ToInt16(b, p + 10);
                    rate = BitConverter.ToInt32(b, p + 12);
                    bits = BitConverter.ToInt16(b, p + 22);
                }
                else if (chunk == "data")
                {
                    Assert.AreEqual(16, bits, path + ": the reader here only knows 16-bit files");
                    int frames = size / (2 * channels);
                    var x = new float[frames];
                    for (int i = 0; i < frames; i++)
                    {
                        float sum = 0f;
                        for (int c = 0; c < channels; c++) sum += BitConverter.ToInt16(b, p + 8 + 2 * (i * channels + c)) / 32768f;
                        x[i] = sum / channels;
                    }
                    return x;
                }
                p += 8 + size + (size & 1);
            }
            Assert.Fail(path + " has no data");
            return null;
        }

        // ------------------------------------------------------------------ what a change of stage asks for

        static void Asks(WindupStage was, WindupStage now, bool began, bool warn, bool aim)
        {
            WindupChange c = Windups.Change(was, now);
            Assert.AreEqual(began, c.Began, was + " to " + now + ": the visor told");
            Assert.AreEqual(warn, c.Warn, was + " to " + now + ": the wind-up");
            Assert.AreEqual(aim, c.Aim, was + " to " + now + ": the mark");
        }

        [Test]
        public void TheWindupSoundsAsItStarts_TheMarkTheFrameItTakesAim_AndNothingWhenItIsCalledOff()
        {
            Asks(WindupStage.None, WindupStage.Winding, true, true, false);
            Asks(WindupStage.Winding, WindupStage.Aimed, false, false, true);
            Asks(WindupStage.Aimed, WindupStage.Striking, false, false, false);
            Asks(WindupStage.Striking, WindupStage.None, false, false, false);
            // Called off before it took aim: no mark, so the mark always means "move now".
            Asks(WindupStage.Winding, WindupStage.None, false, false, false);
            Asks(WindupStage.Aimed, WindupStage.None, false, false, false);
            // A Sprinter with no arms is aimed from the start: the mark at once, and no wind-up before it.
            Asks(WindupStage.None, WindupStage.Aimed, true, false, true);
            // A frame long enough to pass the whole aimed stretch still gives the mark, once.
            Asks(WindupStage.Winding, WindupStage.Striking, false, false, true);
            Asks(WindupStage.None, WindupStage.Striking, true, false, true);
            // Straight into another warning: a new one, told and heard.
            Asks(WindupStage.Striking, WindupStage.Winding, true, true, false);
            Asks(WindupStage.Aimed, WindupStage.Winding, true, true, false);
            foreach (WindupStage s in Enum.GetValues(typeof(WindupStage))) Asks(s, s, false, false, false);
        }

        [Test]
        public void OneWarning_GivesOneWindupAndOneMark_HoweverManyFramesItLasts()
        {
            var track = new WindupTrack();
            int began = 0, warns = 0, aims = 0;
            void Feed(WindupStage stage, double progress, int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    WindupChange c = track.Step(new WindupRead(stage, progress + i * 0.01));
                    if (c.Began) began++;
                    if (c.Warn) warns++;
                    if (c.Aim) aims++;
                }
            }
            Feed(WindupStage.None, 0, 5);
            Feed(WindupStage.Winding, 0.2, 20);
            Assert.AreEqual(0.39f, track.Progress, 1e-5, "the countdown is the brain's");
            Feed(WindupStage.Aimed, 1, 20);
            Feed(WindupStage.Striking, 1, 20);
            Feed(WindupStage.None, 0, 10);
            Assert.AreEqual(1, began);
            Assert.AreEqual(1, warns);
            Assert.AreEqual(1, aims);
            // The next warning counts again; one called off before its lock gives no mark.
            Feed(WindupStage.Winding, 0.5, 10);
            Feed(WindupStage.None, 0, 10);
            Assert.AreEqual(2, began);
            Assert.AreEqual(2, warns);
            Assert.AreEqual(1, aims);
            Assert.AreEqual(WindupStage.None, track.Stage);
            Assert.AreEqual(0f, track.Progress);
        }

        [Test]
        public void TheCountdown_RunsFromNoughtToOne_AndIsFullOnceAimed()
        {
            Assert.AreEqual(0.25f, new WindupRead(WindupStage.Winding, 0.25).Progress, 1e-6);
            Assert.AreEqual(1f, new WindupRead(WindupStage.Winding, 1.5).Progress);
            Assert.AreEqual(0f, new WindupRead(WindupStage.Winding, -1.0).Progress);
            Assert.AreEqual(0f, new WindupRead(WindupStage.Winding, double.NaN).Progress);
            Assert.AreEqual(1f, new WindupRead(WindupStage.Aimed, 0.2).Progress);
            Assert.AreEqual(1f, new WindupRead(WindupStage.Striking, 0.0).Progress);
            Assert.AreEqual(0f, new WindupRead(WindupStage.None, 0.7).Progress);
            Assert.AreEqual(WindupStage.None, WindupRead.None.Stage);
        }

        // ------------------------------------------------------------------ the stages, from each brain's orders

        /// <summary>A warning read at points through it: winding from the start, half way to the lock is half the countdown, aimed from the lock on.</summary>
        static void ReadsAsAWarning(string who, Func<double, WindupRead> warning, double lockAt)
        {
            WindupRead start = warning(0.0), half = warning(lockAt * 0.5), before = warning(lockAt - 1e-3), at = warning(lockAt + 1e-9), late = warning(0.999);
            Assert.AreEqual(WindupStage.Winding, start.Stage, who);
            Assert.AreEqual(0f, start.Progress, 1e-6, who);
            Assert.AreEqual(WindupStage.Winding, half.Stage, who);
            Assert.AreEqual(0.5f, half.Progress, 1e-5, who + ": half way to its lock is half the countdown");
            Assert.AreEqual(WindupStage.Winding, before.Stage, who);
            Assert.AreEqual(WindupStage.Aimed, at.Stage, who + " takes aim at its brain's lock");
            Assert.AreEqual(1f, at.Progress, who);
            Assert.AreEqual(WindupStage.Aimed, late.Stage, who);
        }

        [Test]
        public void EachCreaturesStage_AndCountdown_AreReadFromItsOwnOrders()
        {
            // A walker's claw: winding until its lock share, aimed until it lands (its warning is the strike up to there), then over.
            ReadsAsAWarning("Strider", t => Windups.Walker(new WalkerOrders { StrikeSide = 0, StrikeT = t * WalkerAnimator.StrikeLands }, WalkerBrain.LockAt),
                WalkerBrain.LockAt / WalkerAnimator.StrikeLands);
            Assert.AreEqual(WindupStage.None, Windups.Walker(new WalkerOrders { StrikeSide = -1, StrikeT = 0.1 }, WalkerBrain.LockAt).Stage);
            Assert.AreEqual(WindupStage.Aimed, Windups.Walker(new WalkerOrders { StrikeSide = 1, StrikeT = WalkerBrain.LockAt }, WalkerBrain.LockAt).Stage);
            Assert.AreEqual(WindupStage.None, Windups.Walker(new WalkerOrders { StrikeSide = 1, StrikeT = WalkerAnimator.StrikeLands }, WalkerBrain.LockAt).Stage, "over as the claw lands");
            Assert.AreEqual(WindupStage.Aimed, Windups.Walker(new WalkerOrders { StrikeSide = 0, StrikeT = 0.0 }, 0.0).Stage, "with no arms to feel with it is aimed from the start");

            ReadsAsAWarning("Roller", t => Windups.Roller(new RollerOrders { Mode = RollerMode.Wind, T = t }), RollerBrain.LockAt);
            ReadsAsAWarning("Cave hunter", t => Windups.Hunter(new HunterOrders { Mode = HunterMode.Rear, T = t }), HunterBrain.LockAt);
            // The maw and the biters say themselves when they are aimed, as their brains decide it.
            ReadsAsAWarning("Drifting maw", t => Windups.Maw(new MawOrders { Mode = MawMode.Gape, T = t, Locked = t >= MawBrain.LockAt }), MawBrain.LockAt);
            ReadsAsAWarning("Pack biter", t => Windups.Biter(new BiterOrders { Mode = BiterMode.Mark, T = t, Locked = t >= BiterBrain.LockAt }), BiterBrain.LockAt);
            ReadsAsAWarning("Screamer", t => Windups.Screamer(new ScreamerOrders { Mode = ScreamerMode.WindUp, T = t }), ScreamerBrain.LockAt);
            ReadsAsAWarning("Ambusher", t => Windups.Ambusher(new AmbusherOrders { Mode = AmbusherMode.Coil, T = t }), AmbusherBrain.LockAt);
            ReadsAsAWarning("Blind stalker", t => Windups.Stalker(new StalkerOrders { Mode = StalkerMode.Gape, T = t }), StalkerBrain.LockAt);
        }

        [Test]
        public void OnlyAWarningOrItsStrike_ShowsOnTheVisor_InEveryModeOfEveryCreature()
        {
            void Is(WindupStage want, WindupStage got, string who) => Assert.AreEqual(want, got, who);
            foreach (RollerMode m in Enum.GetValues(typeof(RollerMode)))
                Is(m == RollerMode.Wind ? WindupStage.Winding : m == RollerMode.Roll ? WindupStage.Striking : WindupStage.None, Windups.Roller(new RollerOrders { Mode = m, T = 0.1 }).Stage, "Roller " + m);
            foreach (HunterMode m in Enum.GetValues(typeof(HunterMode)))
                Is(m == HunterMode.Rear ? WindupStage.Winding : m == HunterMode.Strike ? WindupStage.Striking : WindupStage.None, Windups.Hunter(new HunterOrders { Mode = m, T = 0.1 }).Stage, "Cave hunter " + m);
            foreach (MawMode m in Enum.GetValues(typeof(MawMode)))
                Is(m == MawMode.Gape ? WindupStage.Winding : m == MawMode.Inhale ? WindupStage.Striking : WindupStage.None, Windups.Maw(new MawOrders { Mode = m, T = 0.1 }).Stage, "Drifting maw " + m);
            foreach (BiterMode m in Enum.GetValues(typeof(BiterMode)))
                Is(m == BiterMode.Mark ? WindupStage.Winding : m == BiterMode.Dart ? WindupStage.Striking : WindupStage.None, Windups.Biter(new BiterOrders { Mode = m, T = 0.1 }).Stage, "Pack biter " + m);
            foreach (ScreamerMode m in Enum.GetValues(typeof(ScreamerMode)))
                Is(m == ScreamerMode.WindUp ? WindupStage.Winding : m == ScreamerMode.Scream ? WindupStage.Striking : WindupStage.None, Windups.Screamer(new ScreamerOrders { Mode = m, T = 0.1 }).Stage, "Screamer " + m);
            foreach (AmbusherMode m in Enum.GetValues(typeof(AmbusherMode)))
                Is(m == AmbusherMode.Coil ? WindupStage.Winding : m == AmbusherMode.Strike ? WindupStage.Striking : WindupStage.None, Windups.Ambusher(new AmbusherOrders { Mode = m, T = 0.1 }).Stage, "Ambusher " + m);
            foreach (StalkerMode m in Enum.GetValues(typeof(StalkerMode)))
                Is(m == StalkerMode.Gape ? WindupStage.Winding : m == StalkerMode.Lunge ? WindupStage.Striking : WindupStage.None, Windups.Stalker(new StalkerOrders { Mode = m, T = 0.1 }).Stage, "Blind stalker " + m);
        }

        /// <summary>One warning followed frame by frame, as a creature's Unity class follows it.</summary>
        sealed class Heard
        {
            readonly WindupTrack _track = new WindupTrack();
            WindupStage _last;
            public int Began, Warns, Aims;
            public double WarnAt = -1, AimAt = -1, StrikeAt = -1, OverAt = -1;
            public bool Backward;

            public WindupStage Step(in WindupRead read, double t)
            {
                WindupChange c = _track.Step(read);
                if (c.Began) Began++;
                if (c.Warn) { Warns++; if (WarnAt < 0) WarnAt = t; }
                if (c.Aim) { Aims++; if (AimAt < 0) AimAt = t; }
                if (read.Stage == WindupStage.Striking && StrikeAt < 0) StrikeAt = t;
                if (read.Stage == WindupStage.None && Began > 0 && OverAt < 0) OverAt = t;
                if (read.Stage != WindupStage.None && read.Stage < _last) Backward = true;
                _last = read.Stage;
                return read.Stage;
            }

            public bool Done => OverAt >= 0;
        }

        [Test]
        public void AWalkersMark_IsWhereItStopsFollowing_AndItsArcGoesAsTheClawLands()
        {
            foreach (WalkerRules rules in new[] { new WalkerRules(), WalkerRules.ForSprinter() })
            {
                WalkerLayout kind = rules.burstSeconds > 0f ? WalkerLayout.Sprinter : WalkerLayout.Strider;
                var body = new WalkerBody(kind, rules.hp);
                var brain = new WalkerBrain(kind, rules, 5);
                var heard = new Heard();
                double gap = rules.strikeRange - 1.0;
                bool followed = false, heldAfter = true;
                Vec3d lastAt = default;
                WindupStage lastStage = WindupStage.None;
                for (double t = 0; t < 10 && !heard.Done; t += Dt)
                {
                    // The player sways from side to side all the while.
                    double x = 0.4 * Math.Sin(t * 6.0);
                    WalkerOrders o = brain.Think(body, new WalkerSenses { SeesPlayer = true, Player = new Vec3d(x, 0, gap), Chest = new Vec3d(x, 1.3, gap) }, Dt);
                    WindupStage stage = heard.Step(o.StrikeSide >= 0 ? Windups.Walker(o, brain.LockShare(body)) : WindupRead.None, t);
                    if (stage == lastStage && stage == WindupStage.Winding && Math.Abs(o.StrikeAt.X - lastAt.X) > 1e-6) followed = true;
                    if (stage == lastStage && stage == WindupStage.Aimed && Math.Abs(o.StrikeAt.X - lastAt.X) > 1e-9) heldAfter = false;
                    lastAt = o.StrikeAt;
                    lastStage = stage;
                }
                Assert.IsTrue(heard.Done, kind.Model + " never struck");
                Assert.IsFalse(heard.Backward, kind.Model);
                Assert.AreEqual(1, heard.Warns, kind.Model + ": one wind-up");
                Assert.AreEqual(1, heard.Aims, kind.Model + ": one mark");
                Assert.AreEqual(WalkerBrain.LockAt * rules.strikeSeconds, heard.AimAt - heard.WarnAt, Dt * 1.5, kind.Model + " marks where its brain stops following");
                Assert.IsTrue(followed, kind.Model + " follows the player while its arc counts down");
                Assert.IsTrue(heldAfter, kind.Model + ": from the mark on, where it strikes is fixed, so a step aside is a miss");
                Assert.AreEqual(WalkerAnimator.StrikeLands * rules.strikeSeconds, heard.OverAt - heard.WarnAt, Dt * 1.5, kind.Model + "'s arc goes as its claw lands");
            }
        }

        [Test]
        public void ASprinterWithNoArms_GivesItsMarkAtOnce_WithNoWindupBeforeIt()
        {
            WalkerRules rules = WalkerRules.ForSprinter();
            var body = new WalkerBody(WalkerLayout.Sprinter, rules.hp);
            body.HitLimb(WalkerLayout.Limb(0, 0), 0, 0, sever: true);
            body.HitLimb(WalkerLayout.Limb(1, 0), 0, 0, sever: true);
            var brain = new WalkerBrain(WalkerLayout.Sprinter, rules, 5);
            Assert.AreEqual(0.0, brain.LockShare(body));
            var heard = new Heard();
            double gap = rules.strikeRange - 1.0;
            for (double t = 0; t < 10 && !heard.Done; t += Dt)
            {
                WalkerOrders o = brain.Think(body, new WalkerSenses { SeesPlayer = true, Player = new Vec3d(0, 0, gap), Chest = new Vec3d(0, 1.3, gap) }, Dt);
                heard.Step(o.StrikeSide >= 0 ? Windups.Walker(o, brain.LockShare(body)) : WindupRead.None, t);
            }
            Assert.IsTrue(heard.Done, "it never lunged");
            Assert.AreEqual(0, heard.Warns, "no wind-up: it bites where the player stood");
            Assert.AreEqual(1, heard.Aims);
            Assert.AreEqual(1, heard.Began);
        }

        [Test]
        public void ACaveHunter_TurnsToFollowUntilItsMark_AndNotAfter()
        {
            var rules = new HunterRules();
            var body = new HunterBody();
            var brain = new HunterBrain(rules, 3);
            var heard = new Heard();
            double px = 0.0, pz = 1.8;
            bool turnedBefore = false, turnedAfter = false;
            for (double t = 0; t < 20 && !heard.Done; t += Dt)
            {
                HunterOrders o = brain.Think(body, new HunterSenses { SeesPlayer = true, Player = new Vec3d(px, 0, pz) }, Dt);
                WindupStage stage = heard.Step(Windups.Hunter(o), t);
                if (stage == WindupStage.Winding && o.TurnRate != 0.0) turnedBefore = true;
                if (stage == WindupStage.Aimed && o.TurnRate != 0.0) turnedAfter = true;
                double a = o.TurnRate * Dt, c = Math.Cos(a), s = Math.Sin(a);
                double x = c * px - s * pz, z = s * px + c * pz;
                // The player walks to the side at 1 m/s.
                px = x - o.Velocity.X * Dt + 1.0 * Dt;
                pz = z - o.Velocity.Z * Dt;
            }
            Assert.IsTrue(heard.Done, "it never struck");
            Assert.IsFalse(heard.Backward);
            Assert.AreEqual(1, heard.Warns);
            Assert.AreEqual(1, heard.Aims);
            Assert.AreEqual(HunterBrain.LockAt * rules.rearSeconds, heard.AimAt - heard.WarnAt, Dt * 1.5);
            Assert.AreEqual(rules.rearSeconds, heard.StrikeAt - heard.WarnAt, Dt * 1.5, "the strike follows the warning");
            Assert.IsTrue(turnedBefore, "it follows the player while its arc counts down");
            Assert.IsFalse(turnedAfter, "from the mark on it no longer turns: a step aside is a miss");
        }

        [Test]
        public void ARollersMark_IsWhereItSnapsShut_ItsRollIsTheStrike_AndTheRollStopsThreateningOnceItIsDodged()
        {
            var rules = new RollerRules();
            var body = new RollerBody();
            var brain = new RollerBrain(rules, 3);
            var heard = new Heard();
            Vec3d at = Vec3d.Zero, player = new Vec3d(0, 0, 10);
            double yaw = 0;
            bool turnedBefore = false, turnedAfter = false, reachedEarly = false, dodged = false;
            for (double t = 0; t < 20 && !heard.Done; t += Dt)
            {
                Vec3d local = QuatD.AngleAxis(Vec3d.Up, -yaw).Rotate(player - at);
                RollerOrders o = brain.Think(body, new RollerSenses { SeesPlayer = true, Player = local, Home = QuatD.AngleAxis(Vec3d.Up, -yaw).Rotate(-at) }, Dt);
                WindupStage stage = heard.Step(Windups.Roller(o), t);
                if (stage == WindupStage.Winding && o.TurnRate != 0.0) turnedBefore = true;
                if (stage == WindupStage.Aimed && o.TurnRate != 0.0) turnedAfter = true;
                yaw += o.TurnRate * Dt;
                at += QuatD.AngleAxis(Vec3d.Up, yaw).Rotate(o.Velocity) * Dt;
                if (stage == WindupStage.Striking)
                {
                    Vec3d feet = QuatD.AngleAxis(Vec3d.Up, -yaw).Rotate(player - at);
                    if (t - heard.StrikeAt < 0.1) reachedEarly |= brain.RollReaches(feet);
                    // Once the roll is under way the player steps well out of its line.
                    if (t - heard.StrikeAt > 0.1) player = new Vec3d(4.0, 0, 10);
                    if (t - heard.StrikeAt > 0.15 && !brain.RollReaches(feet)) dodged = true;
                }
                else player = new Vec3d(0.6 * Math.Sin(t * 2.0), 0, 10);
            }
            Assert.IsTrue(heard.Done, "it never rolled");
            Assert.IsFalse(heard.Backward);
            Assert.AreEqual(1, heard.Warns);
            Assert.AreEqual(1, heard.Aims);
            Assert.AreEqual(RollerBrain.LockAt * rules.windSeconds, heard.AimAt - heard.WarnAt, Dt * 1.5);
            Assert.AreEqual(rules.windSeconds, heard.StrikeAt - heard.WarnAt, Dt * 1.5);
            Assert.Greater(heard.OverAt, heard.StrikeAt, "the arc holds while it rolls");
            Assert.IsTrue(turnedBefore, "it follows the player while its arc counts down");
            Assert.IsFalse(turnedAfter, "from the mark on it no longer turns");
            Assert.IsTrue(reachedEarly, "a roll at someone standing in its line threatens them");
            Assert.IsTrue(dodged, "out of its line, the roll no longer threatens them, and the arc dims");
        }

        [Test]
        public void ARoll_ThreatensOnlyWhatIsAheadOfIt_InItsLine_AndShortOfWhereItEnds()
        {
            var rules = new RollerRules();
            var body = new RollerBody();
            var brain = new RollerBrain(rules, 3);
            Assert.IsFalse(brain.RollReaches(new Vec3d(0, 0, 2)), "not while it is not rolling");
            // Someone standing still 10 m ahead: it curls, takes aim and rolls at them.
            var player = new Vec3d(0, 0, 10);
            for (int i = 0; i < 1200 && brain.Mode != RollerMode.Roll; i++)
                brain.Think(body, new RollerSenses { SeesPlayer = true, Player = player, Home = Vec3d.Zero }, Dt);
            Assert.AreEqual(RollerMode.Roll, brain.Mode, "it never rolled");
            double side = rules.hitRadius + RollerBrain.InTheWayMargin;
            Assert.IsTrue(brain.RollReaches(new Vec3d(0, 0, 10)), "straight ahead, in its line");
            Assert.IsTrue(brain.RollReaches(new Vec3d(side - 0.1, 0, 6)), "a step off its line is still in its way");
            Assert.IsFalse(brain.RollReaches(new Vec3d(side + 0.1, 0, 6)), "further to the side is out of it");
            Assert.IsFalse(brain.RollReaches(new Vec3d(0, 0, -rules.hitRadius - 0.5)), "behind it");
            Assert.IsFalse(brain.RollReaches(new Vec3d(0, 0, 10 + rules.overshoot + rules.hitRadius + 1.0)), "beyond where the roll ends");
            Assert.IsFalse(brain.RollReaches(new Vec3d(0, 4.0, 6)), "well above it");
        }

        // ------------------------------------------------------------------ the visor arc

        [Test]
        public void TheArc_CountsDownWhileItFollows_ThenSnapsToHalfAsWideAndFullyBrightWhenItTakesAim()
        {
            Assert.AreEqual(WindupCues.StartSpan, WindupCues.Span(WindupStage.Winding, 0f, 0f), 1e-6);
            Assert.AreEqual(WindupCues.LockingSpan, WindupCues.Span(WindupStage.Winding, 1f, 0f), 1e-6);
            float last = float.MaxValue;
            for (float p = 0f; p <= 1f; p += 0.05f)
            {
                float span = WindupCues.Span(WindupStage.Winding, p, 5f);
                Assert.Less(span, last + 1e-6f, "it only ever narrows as the lock comes nearer");
                last = span;
            }
            Assert.AreEqual(WindupCues.StartSpan, WindupCues.Span(WindupStage.Winding, float.NaN, 0f), 1e-6, "a countdown that is not a number shows the start");
            Assert.Greater(WindupCues.StartSpan - WindupCues.LockingSpan, 10f, "the countdown is seen: it narrows by a third");

            // At the aim: one snap from where the countdown ended to half as wide.
            Assert.AreEqual(WindupCues.LockingSpan, WindupCues.Span(WindupStage.Aimed, 1f, 0f), 1e-6, "the snap starts from where it was");
            Assert.AreEqual(WindupCues.AimedSpan, WindupCues.Span(WindupStage.Aimed, 1f, WindupCues.SnapSeconds), 1e-6);
            Assert.AreEqual(WindupCues.AimedSpan, WindupCues.Span(WindupStage.Striking, 1f, 0.5f), 1e-6);
            Assert.LessOrEqual(WindupCues.AimedSpan, WindupCues.LockingSpan * 0.5f, "the change of shape reads without colour");
            Assert.LessOrEqual(WindupCues.SnapSeconds, 0.1f, "one snap, not a slide");

            // Steady while it winds up (nothing to read but the countdown), brighter once aimed.
            Assert.AreEqual(WindupCues.WindingAlpha, WindupCues.Alpha(WindupStage.Winding, true), 1e-6);
            Assert.AreEqual(WindupCues.WindingAlpha, WindupCues.Alpha(WindupStage.Winding, false), 1e-6);
            Assert.AreEqual(1f, WindupCues.Alpha(WindupStage.Aimed, true), 1e-6);
            Assert.AreEqual(1f, WindupCues.Alpha(WindupStage.Striking, true), 1e-6);
            Assert.AreEqual(0f, WindupCues.Alpha(WindupStage.None, true), 1e-6);
            Assert.LessOrEqual(WindupCues.WindingAlpha, 0.65f, "the snap is seen in brightness too");
            Assert.GreaterOrEqual(WindupCues.WindingAlpha, 0.5f, "never so faint the first frame is lost");
        }

        [Test]
        public void AStrike_ThatCanNoLongerReachThePlayer_Dims_AndBrightensAtOnceIfItCan()
        {
            Assert.AreEqual(WindupCues.MissedAlpha, WindupCues.Alpha(WindupStage.Striking, false), 1e-6);
            Assert.Less(WindupCues.MissedAlpha, WindupCues.WindingAlpha, "dimmer than a warning: this one is over for them");
            // Down over the ease, frame by frame; up at once.
            float a = 1f, t = 0f;
            while (a > WindupCues.MissedAlpha + 1e-6f && t < 1f)
            {
                a = WindupCues.Ease(a, WindupCues.MissedAlpha, 1f / 60f);
                t += 1f / 60f;
            }
            Assert.AreEqual(WindupCues.EaseSeconds, t, 1f / 60f + 1e-4f);
            Assert.AreEqual(1f, WindupCues.Ease(WindupCues.MissedAlpha, 1f, 1f / 60f), 1e-6, "danger is never shown late");
            Assert.AreEqual(0.8f, WindupCues.Ease(0.8f, 0.35f, 0f), 1e-6, "no time, no change");
            Assert.AreEqual(1f, WindupCues.Ease(float.NaN, 1f, 0.02f), 1e-6);
        }

        [Test]
        public void ASeenArc_BesideACreatureThatHasTakenAim_IsDimmed_SoTheSnapIsNotLost()
        {
            Assert.IsTrue(WindupCues.Crowds(WindupStage.Aimed, 90f, 100f));
            Assert.IsTrue(WindupCues.Crowds(WindupStage.Striking, 90f, 70f));
            Assert.IsTrue(WindupCues.Crowds(WindupStage.Aimed, 355f, 10f), "across north");
            Assert.IsFalse(WindupCues.Crowds(WindupStage.Aimed, 90f, 90f + WindupCues.CrowdDegrees + 1f), "far enough apart to read both");
            Assert.IsFalse(WindupCues.Crowds(WindupStage.Winding, 90f, 90f), "while it winds up both are read as they are");
            Assert.IsFalse(WindupCues.Crowds(WindupStage.None, 90f, 90f));
            Assert.Less(WindupCues.CrowdedShare, 0.5f);
        }

        [Test]
        public void TheArc_FadesWhenTheWarningIsOver_AndTurnsOnlyWhenItHasMovedEnoughToSee()
        {
            Assert.AreEqual(1f, WindupCues.Fade(1f, 0f), 1e-6);
            Assert.AreEqual(0f, WindupCues.Fade(1f, WindupCues.FadeSeconds), 1e-6);
            float last = 1f;
            for (float t = 0.01f; t < WindupCues.FadeSeconds; t += 0.01f)
            {
                float a = WindupCues.Fade(1f, t);
                Assert.Less(a, last);
                last = a;
            }
            Assert.AreEqual(0.5f * WindupCues.Fade(1f, 0.1f), WindupCues.Fade(0.5f, 0.1f), 1e-6, "it fades from the brightness it had");

            Assert.IsFalse(WindupCues.Turned(10f, 10.2f));
            Assert.IsTrue(WindupCues.Turned(10f, 10.3f));
            Assert.IsFalse(WindupCues.Turned(359.9f, 0.05f), "across north is still a small turn");
            Assert.IsTrue(WindupCues.Turned(359.9f, 1f));
            Assert.AreEqual(20f, WindupCues.DeltaDegrees(350f, 10f), 1e-4);
            Assert.AreEqual(-20f, WindupCues.DeltaDegrees(10f, 350f), 1e-4);
        }

        [Test]
        public void AnArc_KeepsPointingAtTheDanger_AsThePlayerTurns()
        {
            // Something straight to the right of someone facing north (+Z): the arc points right (0 degrees).
            Assert.AreEqual(0f, DamageCues.MarkerAngle(1f, 0f, 0f), 1e-4);
            // They turn a quarter right to face it: the same way in the world is now straight ahead (90, the top).
            Assert.AreEqual(90f, DamageCues.MarkerAngle(0f, 0f, 1f), 1e-4);
            // Turned on round so it is behind their left shoulder: the arc has gone round to the lower left.
            float behindLeft = DamageCues.MarkerAngle(-0.7071f, 0f, -0.7071f);
            Assert.AreEqual(225f, behindLeft, 1e-3);
            Assert.IsTrue(WindupCues.Turned(0f, behindLeft));
        }

        [Test]
        public void AnArc_FollowingSomethingOverhead_TurnsSmoothly_NeverFlippingTopToBottom()
        {
            // Something passing over the player, from ahead through straight up to behind, a little to one side or the
            // other, and the same underneath: each quarter degree it moves, the arc moves only a few degrees.
            foreach (double side in new[] { 0.3, -0.3, 0.05, -0.05, 0.0 })
            foreach (double under in new[] { 1.0, -1.0 })
            {
                float last = float.NaN, worst = 0f;
                for (double el = 0.5; el <= 179.5; el += 0.25)
                {
                    double r = el * Math.PI / 180.0;
                    float right = (float)side, up = (float)(Math.Sin(r) * under), forward = (float)Math.Cos(r);
                    float a = DamageCues.MarkerAngle(right, up, forward);
                    if (!float.IsNaN(last)) worst = Math.Max(worst, Math.Abs(WindupCues.DeltaDegrees(last, a)));
                    last = a;
                }
                Assert.Less(worst, 12f, "side " + side + (under > 0 ? " over" : " under") + ": the arc jumped " + worst.ToString("0.0") + " degrees");
            }
            // Straight up and straight down still point up and down the screen.
            Assert.AreEqual(90f, DamageCues.MarkerAngle(0f, 1f, 0.01f), 1f);
            Assert.AreEqual(270f, DamageCues.MarkerAngle(0f, -1f, 0.01f), 1f);
        }
    }
}
