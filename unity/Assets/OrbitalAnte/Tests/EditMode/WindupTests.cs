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
    /// Creature tells (Ralph loop pass 2, 27 Nov 2026): every creature that strikes has its own warning sound with one
    /// hard mark where it takes aim, the visor shows its wind-up in three stages read from its own brain, and the arc
    /// snaps narrow at the same moment as the sound's mark.
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

        /// <summary>Each creature's warning and the second, at its default dials, at which it takes aim.</summary>
        static IEnumerable<(string id, double lockSeconds)> Warnings()
        {
            yield return (Windups.StriderWarning, Windups.WalkerLock(new WalkerRules(), WalkerBrain.LockAt));
            yield return (Windups.SprinterWarning, Windups.WalkerLock(WalkerRules.ForSprinter(), WalkerBrain.LockAt));
            yield return (Windups.RollerWarning, Windups.RollerLock(new RollerRules()));
            yield return (Windups.HunterWarning, Windups.HunterLock(new HunterRules()));
            yield return (Windups.MawWarning, Windups.MawLock(new MawRules()));
            yield return (Windups.BiterWarning, Windups.BiterLock(new BiterRules()));
            yield return (Windups.ScreamerWarning, Windups.ScreamerLock(new ScreamerRules()));
            yield return (Windups.AmbusherWarning, Windups.AmbusherLock(new AmbusherRules()));
            yield return (Windups.StalkerWarning, Windups.StalkerLock(new StalkerRules()));
        }

        [Test]
        public void EveryCreaturesWarning_HasItsOwnSound_WithItsMarkWhereItTakesAim()
        {
            string folder = AudioFolder();
            if (folder == null) Assert.Ignore("The audio folder was not found from here.");
            AudioManifest m = AudioManifest.Parse(File.ReadAllText(Path.Combine(folder, "audio_manifest.json")));
            var clips = new HashSet<string>();
            int count = 0;
            foreach (var (id, lockSeconds) in Warnings())
            {
                Assert.IsTrue(m.Events.TryGetValue(id, out AudioEventDef def), id + " has no sound: it would fall silent");
                Assert.Greater(def.Clips.Length, 0, id);
                foreach (string c in def.Clips) Assert.IsTrue(clips.Add(c), id + " shares a file with another creature's warning");
                Assert.AreEqual(lockSeconds, def.LockAt, 0.006, id + ": the mark must be where it takes aim at its default dials");
                Assert.IsFalse(def.Loop, id);
                count++;
            }
            Assert.AreEqual(9, count);
            // The walkers' warnings are found by their model's name.
            Assert.AreEqual(Windups.StriderWarning, Windups.WalkerWarning(WalkerLayout.Strider.Model));
            Assert.AreEqual(Windups.SprinterWarning, Windups.WalkerWarning(WalkerLayout.Sprinter.Model));
        }

        [Test]
        public void EveryWarningSound_IsLoudestAtItsMark_AndHeardBeforeIt()
        {
            string folder = AudioFolder();
            if (folder == null) Assert.Ignore("The audio folder was not found from here.");
            AudioManifest m = AudioManifest.Parse(File.ReadAllText(Path.Combine(folder, "audio_manifest.json")));
            foreach (var (id, _) in Warnings())
            {
                AudioEventDef def = m.Events[id];
                foreach (string clip in def.Clips)
                {
                    string path = Path.Combine(Path.GetDirectoryName(folder), clip + ".wav");
                    Assert.IsTrue(File.Exists(path), path);
                    float[] x = ReadWav(path, out int rate);
                    // The loudest 5 ms is the mark (a few clicks ring on for some milliseconds after it starts).
                    int w = rate / 200, best = 0;
                    double loudest = -1.0;
                    for (int i = 0; i + w <= x.Length; i += w / 2)
                    {
                        double e = 0;
                        for (int k = 0; k < w; k++) e += x[i + k] * x[i + k];
                        if (e > loudest) { loudest = e; best = i; }
                    }
                    double at = best / (double)rate;
                    Assert.That(at, Is.InRange(def.LockAt - 0.01, def.LockAt + 0.045), clip + ": its loudest moment is at " + at.ToString("0.000") + " s, not its mark");
                    // Something is heard from the start: the wind-up is a sound, not silence then a click.
                    double before = 0;
                    int upTo = (int)((def.LockAt - 0.02) * rate);
                    for (int k = 0; k < upTo; k++) before += x[k] * x[k];
                    double db = 10.0 * Math.Log10(before / Math.Max(1, upTo) + 1e-20);
                    Assert.Greater(db, -40.0, clip + ": the wind-up before the mark is all but silent");
                }
            }
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

        // ------------------------------------------------------------------ the sound's speed

        [Test]
        public void AtTheDefaultDials_AWarningPlaysAsMade_AndATunedOneStillMarksItsLock()
        {
            Assert.AreEqual(1f, Windups.PitchFor(0.40, Windups.WalkerLock(new WalkerRules(), WalkerBrain.LockAt)), 1e-6);
            // The Strider's strike tuned from 1.6 to 1.4 s takes aim at 0.35 s: the clack, made at 0.40 s, is played
            // that much quicker and lands there too.
            var quick = new WalkerRules { strikeSeconds = 1.4f };
            double lockAt = Windups.WalkerLock(quick, WalkerBrain.LockAt);
            float pitch = Windups.PitchFor(0.40, lockAt);
            Assert.AreEqual(0.35, lockAt, 1e-6);
            Assert.AreEqual(lockAt, 0.40 / pitch, 1e-6);
            // Never so far that it stops sounding like the animal.
            Assert.AreEqual(Windups.MostPitch, Windups.PitchFor(0.40, 0.2), 1e-6);
            Assert.AreEqual(Windups.LeastPitch, Windups.PitchFor(0.40, 0.9), 1e-6);
            // No mark, or aimed from the start (a Sprinter with no arms): played as it is.
            Assert.AreEqual(1f, Windups.PitchFor(0.0, 0.4), 1e-6);
            Assert.AreEqual(1f, Windups.PitchFor(0.4, 0.0), 1e-6);
        }

        // ------------------------------------------------------------------ the stages, from each brain's orders

        [Test]
        public void EachCreaturesStage_IsReadFromItsOwnOrders()
        {
            // A walker's claw: winding until its lock share, aimed until it lands, then over.
            Assert.AreEqual(WindupStage.None, Windups.Walker(new WalkerOrders { StrikeSide = -1 }, WalkerBrain.LockAt));
            Assert.AreEqual(WindupStage.Winding, Windups.Walker(new WalkerOrders { StrikeSide = 0, StrikeT = 0.0 }, WalkerBrain.LockAt));
            Assert.AreEqual(WindupStage.Aimed, Windups.Walker(new WalkerOrders { StrikeSide = 1, StrikeT = WalkerBrain.LockAt }, WalkerBrain.LockAt));
            Assert.AreEqual(WindupStage.None, Windups.Walker(new WalkerOrders { StrikeSide = 1, StrikeT = WalkerAnimator.StrikeLands }, WalkerBrain.LockAt));
            Assert.AreEqual(WindupStage.Aimed, Windups.Walker(new WalkerOrders { StrikeSide = 0, StrikeT = 0.0 }, 0.0), "with no arms to feel with it is aimed from the start");

            Assert.AreEqual(WindupStage.Winding, Windups.Roller(new RollerOrders { Mode = RollerMode.Wind, T = RollerBrain.LockAt - 0.01 }));
            Assert.AreEqual(WindupStage.Aimed, Windups.Roller(new RollerOrders { Mode = RollerMode.Wind, T = RollerBrain.LockAt }));
            Assert.AreEqual(WindupStage.Striking, Windups.Roller(new RollerOrders { Mode = RollerMode.Roll }));
            Assert.AreEqual(WindupStage.None, Windups.Roller(new RollerOrders { Mode = RollerMode.Open }));

            Assert.AreEqual(WindupStage.Winding, Windups.Hunter(new HunterOrders { Mode = HunterMode.Rear, T = HunterBrain.LockAt - 0.01 }));
            Assert.AreEqual(WindupStage.Aimed, Windups.Hunter(new HunterOrders { Mode = HunterMode.Rear, T = HunterBrain.LockAt }));
            Assert.AreEqual(WindupStage.Striking, Windups.Hunter(new HunterOrders { Mode = HunterMode.Strike }));
            Assert.AreEqual(WindupStage.None, Windups.Hunter(new HunterOrders { Mode = HunterMode.Recover }));

            Assert.AreEqual(WindupStage.Winding, Windups.Maw(new MawOrders { Mode = MawMode.Gape, Locked = false }));
            Assert.AreEqual(WindupStage.Aimed, Windups.Maw(new MawOrders { Mode = MawMode.Gape, Locked = true }));
            Assert.AreEqual(WindupStage.Striking, Windups.Maw(new MawOrders { Mode = MawMode.Inhale }));
            Assert.AreEqual(WindupStage.None, Windups.Maw(new MawOrders { Mode = MawMode.Chew }), "once it holds someone, they know");

            Assert.AreEqual(WindupStage.Winding, Windups.Biter(new BiterOrders { Mode = BiterMode.Mark, Locked = false }));
            Assert.AreEqual(WindupStage.Aimed, Windups.Biter(new BiterOrders { Mode = BiterMode.Mark, Locked = true }));
            Assert.AreEqual(WindupStage.Striking, Windups.Biter(new BiterOrders { Mode = BiterMode.Dart }));
            Assert.AreEqual(WindupStage.None, Windups.Biter(new BiterOrders { Mode = BiterMode.Scatter }), "shot while it warns, it scatters and its turn is over");

            Assert.AreEqual(WindupStage.Winding, Windups.Screamer(new ScreamerOrders { Mode = ScreamerMode.WindUp, T = ScreamerBrain.LockAt - 0.01 }));
            Assert.AreEqual(WindupStage.Aimed, Windups.Screamer(new ScreamerOrders { Mode = ScreamerMode.WindUp, T = ScreamerBrain.LockAt }));
            Assert.AreEqual(WindupStage.Striking, Windups.Screamer(new ScreamerOrders { Mode = ScreamerMode.Scream }));
            Assert.AreEqual(WindupStage.None, Windups.Screamer(new ScreamerOrders { Mode = ScreamerMode.Spent }));

            Assert.AreEqual(WindupStage.Winding, Windups.Ambusher(new AmbusherOrders { Mode = AmbusherMode.Coil, T = AmbusherBrain.LockAt - 0.01 }));
            Assert.AreEqual(WindupStage.Aimed, Windups.Ambusher(new AmbusherOrders { Mode = AmbusherMode.Coil, T = AmbusherBrain.LockAt }));
            Assert.AreEqual(WindupStage.Striking, Windups.Ambusher(new AmbusherOrders { Mode = AmbusherMode.Strike }));
            Assert.AreEqual(WindupStage.None, Windups.Ambusher(new AmbusherOrders { Mode = AmbusherMode.Hold }));
            Assert.AreEqual(WindupStage.None, Windups.Ambusher(new AmbusherOrders { Mode = AmbusherMode.Lurk }), "backing out of reach calls it off");

            Assert.AreEqual(WindupStage.Winding, Windups.Stalker(new StalkerOrders { Mode = StalkerMode.Gape, T = StalkerBrain.LockAt - 0.01 }));
            Assert.AreEqual(WindupStage.Aimed, Windups.Stalker(new StalkerOrders { Mode = StalkerMode.Gape, T = StalkerBrain.LockAt }));
            Assert.AreEqual(WindupStage.Striking, Windups.Stalker(new StalkerOrders { Mode = StalkerMode.Lunge }));
            Assert.AreEqual(WindupStage.None, Windups.Stalker(new StalkerOrders { Mode = StalkerMode.Recover }));
        }

        /// <summary>Records when each stage of one warning began, in seconds from its start.</summary>
        sealed class Timeline
        {
            public double Start = -1, Aimed = -1, Striking = -1, Over = -1;
            WindupStage _last = WindupStage.None;
            public bool Backward;

            public void Step(WindupStage stage, double t)
            {
                if (stage == _last) return;
                if (stage != WindupStage.None && stage < _last) Backward = true;
                if (stage == WindupStage.Winding && Start < 0) Start = t;
                if (stage == WindupStage.Aimed && Aimed < 0) Aimed = t;
                if (stage == WindupStage.Striking && Striking < 0) Striking = t;
                if (stage == WindupStage.None && Start >= 0 && Over < 0) Over = t;
                _last = stage;
            }

            public bool Done => Over >= 0;
        }

        [Test]
        public void AStridersStrike_TakesAimWhereItsSoundClacks_AndIsOverWhenTheClawLands()
        {
            foreach (WalkerRules rules in new[] { new WalkerRules(), WalkerRules.ForSprinter() })
            {
                WalkerLayout kind = rules.burstSeconds > 0f ? WalkerLayout.Sprinter : WalkerLayout.Strider;
                var body = new WalkerBody(kind, rules.hp);
                var brain = new WalkerBrain(kind, rules, 5);
                var line = new Timeline();
                double gap = rules.strikeRange - 1.0;
                for (double t = 0; t < 10 && !line.Done; t += Dt)
                {
                    WalkerOrders o = brain.Think(body, new WalkerSenses { SeesPlayer = true, Player = new Vec3d(0, 0, gap), Chest = new Vec3d(0, 1.3, gap) }, Dt);
                    line.Step(Windups.Walker(o, brain.LockShare(body)), t);
                }
                Assert.IsTrue(line.Done, kind.Model + " never struck");
                Assert.IsFalse(line.Backward, kind.Model);
                Assert.AreEqual(Windups.WalkerLock(rules, WalkerBrain.LockAt), line.Aimed - line.Start, Dt * 1.5, kind.Model + " takes aim where its sound marks it");
                Assert.AreEqual(WalkerAnimator.StrikeLands * rules.strikeSeconds, line.Over - line.Start, Dt * 1.5, kind.Model + "'s arc goes as its claw lands");
            }
        }

        [Test]
        public void ACaveHuntersRear_IsWindingThenAimed_ThenTheStrike()
        {
            var rules = new HunterRules();
            var body = new HunterBody();
            var brain = new HunterBrain(rules, 3);
            var line = new Timeline();
            double px = 0.0, pz = 1.8;
            for (double t = 0; t < 20 && !line.Done; t += Dt)
            {
                HunterOrders o = brain.Think(body, new HunterSenses { SeesPlayer = true, Player = new Vec3d(px, 0, pz) }, Dt);
                double a = o.TurnRate * Dt, c = Math.Cos(a), s = Math.Sin(a);
                double x = c * px - s * pz, z = s * px + c * pz;
                px = x - o.Velocity.X * Dt;
                pz = z - o.Velocity.Z * Dt;
                line.Step(Windups.Hunter(o), t);
            }
            Assert.IsTrue(line.Done, "it never struck");
            Assert.IsFalse(line.Backward);
            Assert.AreEqual(Windups.HunterLock(rules), line.Aimed - line.Start, Dt * 1.5);
            Assert.AreEqual(rules.rearSeconds, line.Striking - line.Start, Dt * 1.5, "the strike follows the warning");
        }

        [Test]
        public void ARollersCurl_IsAimedWhereItSnapsShut_AndItsRollIsTheStrike()
        {
            var rules = new RollerRules();
            var body = new RollerBody();
            var brain = new RollerBrain(rules, 3);
            var line = new Timeline();
            Vec3d at = Vec3d.Zero, player = new Vec3d(0, 0, 10);
            double yaw = 0;
            for (double t = 0; t < 20 && !line.Done; t += Dt)
            {
                Vec3d local = QuatD.AngleAxis(Vec3d.Up, -yaw).Rotate(player - at);
                RollerOrders o = brain.Think(body, new RollerSenses { SeesPlayer = true, Player = local, Home = QuatD.AngleAxis(Vec3d.Up, -yaw).Rotate(-at) }, Dt);
                yaw += o.TurnRate * Dt;
                at += QuatD.AngleAxis(Vec3d.Up, yaw).Rotate(o.Velocity) * Dt;
                line.Step(Windups.Roller(o), t);
            }
            Assert.IsTrue(line.Done, "it never rolled");
            Assert.IsFalse(line.Backward);
            Assert.AreEqual(Windups.RollerLock(rules), line.Aimed - line.Start, Dt * 1.5);
            Assert.AreEqual(rules.windSeconds, line.Striking - line.Start, Dt * 1.5);
            Assert.Greater(line.Over, line.Striking, "the arc holds while it rolls");
        }

        // ------------------------------------------------------------------ the visor arc

        [Test]
        public void TheArc_FlickersWideWhileItFollows_ThenSnapsNarrowAndSteadyWhenItTakesAim()
        {
            float low = 1f, high = 0f;
            for (float t = 0f; t < 1f; t += 0.005f)
            {
                float a = WindupCues.Alpha(WindupStage.Winding, t);
                low = Math.Min(low, a);
                high = Math.Max(high, a);
                Assert.AreEqual(WindupCues.FollowSpan, WindupCues.Span(WindupStage.Winding, t), 1e-6);
            }
            Assert.Less(low, 0.55f, "it visibly flickers");
            Assert.Greater(high, 0.8f);
            Assert.GreaterOrEqual(low, WindupCues.FlickerLow - 1e-6, "never so faint it is lost");
            Assert.AreEqual(WindupCues.FlickerHigh, WindupCues.Alpha(WindupStage.Winding, 0f), 1e-6, "it starts bright, so the first frame is seen");

            // At the aim: steady, and narrowed to well under half within the snap.
            for (float t = 0f; t < 2f; t += 0.01f)
            {
                Assert.AreEqual(1f, WindupCues.Alpha(WindupStage.Aimed, t), 1e-6);
                Assert.AreEqual(1f, WindupCues.Alpha(WindupStage.Striking, t), 1e-6);
            }
            Assert.AreEqual(WindupCues.FollowSpan, WindupCues.Span(WindupStage.Aimed, 0f), 1e-6, "the snap starts from where it was");
            Assert.AreEqual(WindupCues.AimedSpan, WindupCues.Span(WindupStage.Aimed, WindupCues.SnapSeconds), 1e-6);
            Assert.AreEqual(WindupCues.AimedSpan, WindupCues.Span(WindupStage.Striking, 0.5f), 1e-6);
            Assert.Less(WindupCues.AimedSpan, WindupCues.FollowSpan * 0.5f, "the change of shape reads without colour");
            Assert.LessOrEqual(WindupCues.SnapSeconds, 0.1f, "one snap, not a slide");
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
    }
}
