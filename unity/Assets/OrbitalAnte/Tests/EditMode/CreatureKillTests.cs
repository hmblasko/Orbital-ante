using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Creatures;
using OrbitalAnte.Tasks;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// Creature kills pay (Ralph loop pass 3, 28 Nov 2026): a creature the player kills on a world scores toward the
    /// ante like a camp's enemy, on the same tier table and with the same style feats, once a run, and a pack's killed
    /// fish stay dead when it is woken again.
    /// </summary>
    public class CreatureKillTests
    {
        // ------------------------------------------------------------------ tiers

        [Test]
        public void EveryCreature_ScoresAsAFight_OnTheCampsTable()
        {
            foreach (string kind in CreatureTiers.Kinds)
            {
                ScoreEventKind k = CreatureTiers.KindFor(kind);
                Assert.AreEqual(ScoreCategory.Combat, AnteBalance.CategoryOf(k), kind);
                Assert.AreNotEqual(ScoreEventKind.EnemyElite, k, kind + ": elite is for a marked target only");
                Assert.Greater(new AnteBalance().BasePoints(k), 0, kind + " must be worth something");
            }
        }

        [Test]
        public void TheBigOnesPayLikeAHeavy_APackFishLikeATrooper_TheRestLikeAMarksman()
        {
            Assert.AreEqual(ScoreEventKind.EnemyTier3, CreatureTiers.KindFor(MawLayout.Model));
            Assert.AreEqual(ScoreEventKind.EnemyTier3, CreatureTiers.KindFor(WalkerLayout.Strider.Model));
            Assert.AreEqual(ScoreEventKind.EnemyTier1, CreatureTiers.KindFor(BiterLayout.Model));
            foreach (string kind in new[] { WalkerLayout.Sprinter.Model, RollerLayout.Model, HunterLayout.Model, ScreamerLayout.Model, AmbusherLayout.Model, StalkerLayout.Model })
                Assert.AreEqual(ScoreEventKind.EnemyTier2, CreatureTiers.KindFor(kind), kind);
            Assert.AreEqual(ScoreEventKind.EnemyTier2, CreatureTiers.KindFor("SomethingNew"), "a kind added later pays like most");
            // On ante 1's table a whole pack of the smallest size pays about what one big animal does.
            var balance = new AnteBalance();
            int pack = 3 * balance.BasePoints(CreatureTiers.KindFor(BiterLayout.Model));
            int maw = balance.BasePoints(CreatureTiers.KindFor(MawLayout.Model));
            Assert.That(pack, Is.InRange(maw * 0.7, maw * 1.3), "three biters " + pack + " against one maw " + maw);
        }

        [Test]
        public void TheTierList_NamesEveryCreatureModel()
        {
            var models = new HashSet<string>
            {
                WalkerLayout.Strider.Model, WalkerLayout.Sprinter.Model, RollerLayout.Model, HunterLayout.Model, ScreamerLayout.Model,
                AmbusherLayout.Model, StalkerLayout.Model, MawLayout.Model, BiterLayout.Model,
            };
            CollectionAssert.AreEquivalent(models, CreatureTiers.Kinds);
        }

        // ------------------------------------------------------------------ claims

        [Test]
        public void ACreaturesClaim_IsOnePerWorldHauntAndPlace_AndNeverAnEnemys()
        {
            var seen = new HashSet<string>();
            foreach (string world in new[] { "earth", "europa" })
                for (int haunt = 0; haunt < 12; haunt++)
                    for (int place = 0; place < 8; place++)
                    {
                        string id = TaskIds.Creature(world, haunt, place);
                        Assert.IsTrue(seen.Add(id), "two animals share " + id);
                        StringAssert.StartsWith("creature:", id);
                    }
            // "earth.h1.p12" and "earth.h11.p2" must not meet.
            Assert.AreNotEqual(TaskIds.Creature("earth", 1, 12), TaskIds.Creature("earth", 11, 2));
            Assert.AreNotEqual(TaskIds.Creature("earth", 0, 0), TaskIds.Enemy("earth", 0, 0));
        }

        // ------------------------------------------------------------------ a pack woken again

        static BiterPack PackOf(int fish, out List<BiterBrain> brains)
        {
            var pack = new BiterPack();
            brains = new List<BiterBrain>();
            var rules = new BiterRules();
            for (int i = 0; i < fish; i++) brains.Add(new BiterBrain(rules, pack, i));
            return pack;
        }

        [Test]
        public void APackWokenAgain_IsStillThePackItWas_SoItsLastFishMakesOff()
        {
            // A pack of five, three of them killed: the last of the two left makes off.
            BiterPack whole = PackOf(5, out List<BiterBrain> all);
            for (int i = 0; i < 3; i++) whole.Lost(all[i]);
            Assert.IsFalse(whole.Broken, "two left that can bite");
            whole.Lost(all[3]);
            Assert.IsTrue(whole.Broken, "the last one makes off");

            // The same pack put away and woken again with only its two: it behaves the same.
            BiterPack again = PackOf(2, out List<BiterBrain> two);
            again.KilledBefore(3);
            Assert.AreEqual(whole.Started, again.Started);
            Assert.IsFalse(again.Broken, "two left that can bite");
            again.Lost(two[0]);
            Assert.IsTrue(again.Broken, "the last one makes off, as it would have");

            // Without the memory it would have been a pack of two, whose last fish fights on.
            BiterPack forgot = PackOf(2, out List<BiterBrain> plain);
            forgot.Lost(plain[0]);
            Assert.IsFalse(forgot.Broken);
        }

        [Test]
        public void KilledBefore_NeverTakesAwayFromAPack()
        {
            BiterPack pack = PackOf(3, out _);
            pack.KilledBefore(0);
            pack.KilledBefore(-4);
            Assert.AreEqual(3, pack.Started);
            Assert.AreEqual(3, pack.Living);
        }

        // ------------------------------------------------------------------ the code itself

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

        /// <summary>
        /// Every creature class says who it is when the player hits it (the task director pays only for a hit whose
        /// target is a creature with credit), so a creature added later, or brought out of the practice range onto a
        /// world, cannot fight for nothing by accident.
        /// </summary>
        [Test]
        public void EveryCreature_IsTheTargetOfTheHitsItTakes_AndCanBeGivenCredit()
        {
            string folder = Path.Combine(RuntimeFolder(), "Creatures");
            Assume.That(Directory.Exists(folder), "creature folder not found: " + folder);
            int classes = 0;
            // Spelt in two, or the rules project would take this file for a Unity one and leave it out.
            string declaration = @"public sealed class (\w+) : Mono" + @"Behaviour, IWindup([^\r\n{]*)";
            foreach (string file in Directory.GetFiles(folder, "*.cs"))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(text, declaration))
                {
                    classes++;
                    string name = m.Groups[1].Value;
                    StringAssert.Contains("ICreature", m.Groups[2].Value, name + " must implement ICreature");
                    StringAssert.Contains("public CreatureCredit Credit { get; set; }", text, name);
                    Assert.AreEqual(Regex.Matches(text, @"outcome\.Enemy = true;").Count, Regex.Matches(text, @"outcome\.Enemy = true;\s*outcome\.Target = this;").Count,
                        name + ": every hit that counts as an enemy names it as the target");
                    // A swimmer reaches only someone in the water with it; on land a creature can always come to the player.
                    bool swims = name == "Maw" || name == "Biter";
                    if (swims)
                    {
                        StringAssert.Contains("public bool InReach => _inReach;", text, name);
                        StringAssert.Contains("_inReach = Water == null || senses.PlayerInWater;", text, name + ": in reach exactly when it takes notice of the player");
                    }
                    else StringAssert.Contains("public bool InReach => true;", text, name);
                }
            }
            Assert.AreEqual(8, classes, "the eight creature classes (the walker is two kinds)");
        }

        [Test]
        public void ASeaCreatureKilledFromOutOfItsReach_PaysNothing_AndTheNoteSaysWhatDoes()
        {
            string director = File.ReadAllText(Path.Combine(RuntimeFolder(), "Tasks", "TaskDirector.cs"));
            int at = director.IndexOf("void OnCreatureHit(", System.StringComparison.Ordinal);
            Assume.That(at >= 0, "OnCreatureHit not found");
            string body = director.Substring(at, director.IndexOf("KillFacts FactsOf(", at, System.StringComparison.Ordinal) - at);
            int refuse = body.IndexOf("if (!creature.InReach)", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(refuse, 0, "the kill is refused when the creature could not reach the player");
            Assert.Less(refuse, body.IndexOf("PayKill(", System.StringComparison.Ordinal), "before anything is paid, counted for a bonus mission or an unlock");
            StringAssert.Contains("AnteLoop.Note(CreatureTiers.OutOfReach)", body, "never a hidden rule");
            StringAssert.Contains("WATER", CreatureTiers.OutOfReach, "it says where they do pay");
            Assert.LessOrEqual(CreatureTiers.OutOfReach.Length, 46, "as short as the readout's other notes");
        }
    }
}
