using NUnit.Framework;
using OrbitalAnte.Bases;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    /// <summary>Breaking buildings and felling trees: the plain rules behind them.</summary>
    public class StructureRulesTests
    {
        [Test]
        public void BiggerThings_TakeMoreToBringDown_UpToACeiling()
        {
            var rules = new StructureRules();
            float crate = rules.Toughness(1f, 1f, 1f), lamp = rules.Toughness(0.3f, 5.5f, 0.3f);
            float pod = rules.Toughness(7f, 3.6f, 3.6f), keep = rules.Toughness(12f, 14f, 12f);
            Assert.Less(lamp, crate + 60f);
            Assert.Less(crate, pod);
            Assert.Less(pod, keep);
            Assert.LessOrEqual(keep, rules.mostToughness);
            // A rifle does about 24-34 a hit: a crate in a handful, a living pod in a magazine or so, never one shot.
            Assert.That(crate / 34f, Is.InRange(3f, 10f));
            Assert.That(pod / 34f, Is.InRange(12f, 40f));
        }

        [Test]
        public void Ramming_DoesNothingSlow_AndMoreTheFasterAndHeavier()
        {
            var rules = new StructureRules();
            Assert.AreEqual(0f, rules.RamDamage(rules.ramSafeSpeed, 3200f));
            Assert.AreEqual(0f, rules.RamDamage(1f, 3200f), "A touch while landing is not a crash.");
            Assert.Greater(rules.RamDamage(12f, 3200f), rules.RamDamage(8f, 3200f));
            Assert.Greater(rules.RamDamage(10f, 6400f), rules.RamDamage(10f, 3200f));
            // The lander flown into a living pod at 15 m/s brings it down.
            Assert.Greater(rules.RamDamage(15f, 3200f), rules.Toughness(7f, 3.6f, 3.6f));
        }

        [Test]
        public void Bullets_ChipBuildings_ButFellProps_AndWarnBeforeTheEnd()
        {
            var rules = new StructureRules();
            float pod = rules.Toughness(7f, 3.6f, 3.6f), crate = rules.Toughness(1f, 1f, 1f);
            Assert.Less(rules.BulletShare(true, pod), 0.5f);
            Assert.AreEqual(1f, rules.BulletShare(false, crate));
            // A rifle (34 a hit, 30 a magazine) needs about three magazines for a living pod, not two seconds.
            float hits = pod / (34f * rules.BulletShare(true, pod));
            Assert.That(hits / 30f, Is.InRange(1.8f, 4f));
            Assert.AreEqual(0, rules.Warning(0.1f, 0.3f));
            Assert.AreEqual(1, rules.Warning(0.4f, 0.55f));
            Assert.AreEqual(2, rules.Warning(0.6f, 0.85f));
            Assert.AreEqual(2, rules.Warning(0.3f, 0.9f), "Both thresholds in one blow: the louder warning.");
            Assert.AreEqual(0, rules.Warning(0.7f, 1f), "The blow that brings it down is its own moment.");
        }

        [Test]
        public void Health_FallsOnce_OnTheHitThatEmptiesIt()
        {
            var h = new StructureHealth(100f);
            Assert.IsFalse(h.Take(60f));
            Assert.IsFalse(h.Fallen);
            Assert.IsTrue(h.Take(60f));
            Assert.IsTrue(h.Fallen);
            Assert.IsFalse(h.Take(60f), "Only the hit that brings it down says so.");
            Assert.IsFalse(new StructureHealth(10f).Take(float.NaN));
            Assert.IsFalse(new StructureHealth(10f).Take(-5f));
        }

        [Test]
        public void Wrecks_AreRemembered_PerBase_PerStructure()
        {
            var w = new BaseWrecks();
            var a = new BaseSite { Seed = 7, Up = new Vec3d(0, 1, 0) };
            var b = new BaseSite { Seed = 7, Up = new Vec3d(1, 0, 0) };
            long ka = BaseWrecks.Key(a), kb = BaseWrecks.Key(b);
            Assert.AreNotEqual(ka, kb);
            Assert.AreEqual(ka, BaseWrecks.Key(a));
            w.Fell(ka, 3);
            Assert.IsTrue(w.IsDown(ka, 3));
            Assert.IsFalse(w.IsDown(ka, 4));
            Assert.IsFalse(w.IsDown(kb, 3));
            Assert.AreEqual(2.5f, w.AirLeft(ka, 0, 2.5f));
            w.SetAir(ka, 0, 1.2f);
            Assert.AreEqual(1.2f, w.AirLeft(ka, 0, 2.5f));
            w.SetAir(ka, 0, -1f);
            Assert.AreEqual(0f, w.AirLeft(ka, 0, 2.5f));
        }

        [Test]
        public void Doors_SlideAtTheirSpeed_AndStopAtTheEnds()
        {
            float open = 0f;
            for (int i = 0; i < 9; i++) open = DoorRule.Step(open, true, 0.1f, 0.9f);
            Assert.AreEqual(1f, open, 1e-4f);
            open = DoorRule.Step(open, true, 0.1f, 0.9f);
            Assert.AreEqual(1f, open);
            open = DoorRule.Step(open, false, 0.45f, 0.9f);
            Assert.AreEqual(0.5f, open, 1e-4f);
            Assert.AreEqual(0.5f, DoorRule.Eased(0.5f), 1e-6f);
            Assert.AreEqual(0f, DoorRule.Eased(-1f));
            Assert.AreEqual(1f, DoorRule.Eased(2f));
        }

        [Test]
        public void AnAirStation_FillsTheSuit_NoFasterThanItFlows_AndNoMoreThanItHolds()
        {
            Assert.AreEqual(0.1f, AirStationRule.Flow(0.2f, 3f, 0.6f, 6f), 1e-5f);
            Assert.AreEqual(0.05f, AirStationRule.Flow(0.95f, 3f, 1f, 6f), 1e-5f, "No more than the suit has room for.");
            Assert.AreEqual(0.02f, AirStationRule.Flow(0.2f, 0.02f, 1f, 6f), 1e-5f, "No more than the station holds.");
            Assert.AreEqual(0f, AirStationRule.Flow(0.2f, 0f, 1f, 6f));
            Assert.AreEqual(0f, AirStationRule.Flow(0.2f, 3f, 0f, 6f));

            // A station's store fills the suit from empty once, then it is spent: a find, not a tap.
            var rules = new StructureRules();
            float left = rules.stationTanks, suit = 0f;
            int fills = 0;
            for (int i = 0; i < 10000 && left > 0f; i++)
            {
                float flow = AirStationRule.Flow(suit, left, 0.1f, rules.stationFillSeconds);
                suit += flow;
                left -= flow;
                if (suit >= 0.999f) { fills++; suit = 0f; }
            }
            Assert.AreEqual(1, fills);
        }

        [Test]
        public void Trees_ThickerTrunks_TakeMoreToFell()
        {
            var rules = new FellingRules();
            float sapling = rules.Toughness(0.1f), tree = rules.Toughness(0.3f), giant = rules.Toughness(1.2f);
            Assert.Less(sapling, tree);
            Assert.Less(tree, giant);
            Assert.LessOrEqual(giant, rules.mostToughness);
            Assert.That(tree / 34f, Is.InRange(3f, 10f), "A woodland tree comes down to a few rifle shots.");
            Assert.AreEqual(0f, rules.RamDamage(rules.ramSafeSpeed, 3200f), "Landing against a tree does not fell it.");
            Assert.Greater(rules.RamDamage(10f, 3200f), tree, "Flown into at 10 m/s, a tree comes down.");
            Assert.That(rules.TopMass(0.3f, 9f), Is.InRange(40f, rules.mostMass));
        }

        [Test]
        public void FelledTrees_AreRemembered_ByWhereTheyStood()
        {
            var felled = new FelledTrees();
            var here = new Vec3d(6371000.123, 10.5, -42.25);
            Assert.IsFalse(felled.IsFelled(here));
            felled.Fell(here);
            Assert.IsTrue(felled.IsFelled(here));
            Assert.IsTrue(felled.IsFelled(new Vec3d(6371000.1231, 10.5001, -42.2501)), "The same tree, give or take rounding.");
            Assert.IsFalse(felled.IsFelled(new Vec3d(6371000.123, 10.5, -40.25)));
        }
    }
}
