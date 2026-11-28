using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OrbitalAnte.Creatures;
using OrbitalAnte.Tasks;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// A fight with a creature is a fight for the whole game, and a creature's kill says how it happened (Ralph loop pass
    /// 8, from the design review: a creature's fight did not count, so tips, an unlock banner or a squad's radio call
    /// could come in the middle of one, and a creature's kill said "Shot down outside the ship.").
    /// </summary>
    public class CreatureFightTests
    {
        const float Never = 1000f;

        [Test]
        public void ACreaturesWarning_OrOneAfterThePlayer_IsAFight_AndItEndsByItself()
        {
            Assert.IsFalse(ReadoutFocus.InFight(Never, Never, Never), "nobody about");
            Assert.IsTrue(ReadoutFocus.InFight(0f, Never, Never), "an enemy's sighting, as before");
            Assert.IsTrue(ReadoutFocus.InFight(Never, 0f, Never), "a creature begins a warning");
            Assert.IsTrue(ReadoutFocus.InFight(Never, ReadoutFocus.FightHold - 0.01f, Never), "and for as long as a sighting holds");
            Assert.IsFalse(ReadoutFocus.InFight(Never, ReadoutFocus.FightHold, Never));
            Assert.IsTrue(ReadoutFocus.InFight(Never, Never, 0f), "a creature that knows where the player is and can hurt them");
            Assert.IsTrue(ReadoutFocus.InFight(Never, Never, ReadoutFocus.AwareHold - 0.01f), "bridging a frame or two");
            Assert.IsFalse(ReadoutFocus.InFight(Never, Never, ReadoutFocus.AwareHold), "said every frame it is after them, so it ends soon after");
            Assert.IsFalse(ReadoutFocus.InFight(Never, -1f, -1f), "a stamp after now (a re-used clock) is not a fight");
            Assert.AreEqual(ReadoutFocus.CreatureFight(0f, Never), ReadoutFocus.InFight(Never, 0f, Never));
            Assert.LessOrEqual(ReadoutFocus.AwareHold, ReadoutFocus.FightHold);
        }

        [Test]
        public void EverythingThatWaitsOutAFight_AsksTheSameTest()
        {
            string runtime = RuntimeFolder();
            string ante = File.ReadAllText(Path.Combine(runtime, "Tasks", "AnteLoop.cs"));
            StringAssert.Contains("ReadoutFocus.InFight(now - OrbitalAnte.AI.CombatWorld.PlayerLastSeenAt, now - OrbitalAnte.AI.CombatWorld.CreatureWarnedAt,", ante,
                "the readout, the tips and an unlock's moment wait out a creature's fight");
            StringAssert.Contains("now - OrbitalAnte.AI.CombatWorld.CreatureAwareAt", ante);

            string spawner = File.ReadAllText(Path.Combine(runtime, "Enemies", "EnemySpawner.cs"));
            StringAssert.Contains("Tasks.ReadoutFocus.CreatureFight(now - CombatWorld.CreatureWarnedAt, now - CombatWorld.CreatureAwareAt)) fighting = true;", spawner,
                "a creature's fight is no quiet for the restless camps");
            Assert.Less(spawner.IndexOf("Tasks.ReadoutFocus.CreatureFight(", System.StringComparison.Ordinal),
                        spawner.IndexOf("_lull.Tick(rules, PressureAnte, dt, true, fighting,", System.StringComparison.Ordinal), "before the clock is ticked");

            string suit = File.ReadAllText(Path.Combine(runtime, "Spacewalk", "SuitHud.cs"));
            StringAssert.Contains("bool fight = Tasks.AnteLoop.InFight;", suit, "the suit's beads keep to the same fight");
            StringAssert.DoesNotContain("_creatureAt", suit, "not a timer of their own");

            string world = File.ReadAllText(Path.Combine(runtime, "AI", "CombatWorld.cs"));
            StringAssert.IsMatch(@"CreatureWarnedAt = Time\.time;\s*CreatureWindup\?\.Invoke", world, "a warning is stamped as it is raised");
            foreach (string spawn in new[] { "LandCreatures.cs", "SeaCreatures.cs" })
            {
                string text = File.ReadAllText(Path.Combine(runtime, "Creatures", spawn));
                StringAssert.Contains("if (_live.Count > 0 && AnyAware) CombatWorld.CreatureAwareAt = Time.time;", text, spawn + " says so every frame one is after the player");
                Assert.Less(text.IndexOf("CombatWorld.CreatureAwareAt = Time.time;", System.StringComparison.Ordinal),
                            text.IndexOf("if (Time.time < _nextThink) return;", System.StringComparison.Ordinal), spawn + ": every frame, not twice a second");
            }
        }

        [Test]
        public void ACreaturesKill_SaysHowItHappened_InPlainWords_NeverItsPlaceholderName()
        {
            var seen = new HashSet<string>();
            foreach (string words in StrikeWords.All)
            {
                Assert.IsTrue(seen.Add(words), words + " twice");
                Assert.IsTrue(char.IsUpper(words[0]) && words.EndsWith("."), words + ": a sentence, as the death page's other causes are");
                Assert.LessOrEqual(words.Length, 40, words + ": one line on the death page");
                foreach (string kind in CreatureTiers.Kinds)
                    StringAssert.DoesNotContain(kind.ToLowerInvariant(), words.ToLowerInvariant(), "never a placeholder name on screen");
                StringAssert.DoesNotContain("shot", words.ToLowerInvariant(), "a creature never shoots");
            }

            // Every creature's strike that hurts the player names itself, and every set of words is some strike's.
            string folder = Path.Combine(RuntimeFolder(), "Creatures");
            var used = new HashSet<string>();
            int strikes = 0;
            foreach (string file in Directory.GetFiles(folder, "*.cs"))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(text, @"player\.TakeDamage\(([^;]*)\);"))
                {
                    strikes++;
                    MatchCollection named = Regex.Matches(m.Groups[1].Value, @"StrikeWords\.(\w+)");
                    Assert.Greater(named.Count, 0, Path.GetFileName(file) + ": a creature's strike says what it was: " + m.Value);
                    foreach (Match n in named) used.Add(n.Groups[1].Value);
                }
            }
            Assert.GreaterOrEqual(strikes, 8, "every creature class that strikes");
            foreach (System.Reflection.FieldInfo field in typeof(StrikeWords).GetFields())
                if (field.IsLiteral) Assert.IsTrue(used.Contains(field.Name), "StrikeWords." + field.Name + " is some creature's strike");

            string astronaut = File.ReadAllText(Path.Combine(RuntimeFolder(), "Ship", "Astronaut.cs"));
            StringAssert.Contains("a.Armour.Died += () => a.Die(!string.IsNullOrEmpty(a.Armour.LastCause) ? a.Armour.LastCause", astronaut,
                "the death page names the strike that ended the run, and a shot as before");
        }

        static string RuntimeFolder()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                foreach (string candidate in new[] { Path.Combine(dir, "Assets", "OrbitalAnte", "Scripts", "Runtime"), Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Scripts", "Runtime") })
                    if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", "Scripts", "Runtime");
        }
    }
}
