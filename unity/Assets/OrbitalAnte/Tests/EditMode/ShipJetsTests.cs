using System;
using NUnit.Framework;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests
{
    /// <summary>The small thrusters light the way the ship is being pushed or turned, and nothing else.</summary>
    public class ShipJetsTests
    {
        static readonly P3 None = new P3(0f, 0f, 0f);
        static float[] Drive(P3 push, P3 turn, Func<int, bool> working = null)
        {
            var into = new float[ShipJets.Count];
            ShipJets.Drive(push, turn, working, into);
            return into;
        }

        /// <summary>The push the lit nozzles give together, weighted by how hard each fires.</summary>
        static P3 Total(float[] drive)
        {
            P3 sum = None;
            for (int n = 0; n < ShipJets.Count; n++) sum = sum - ShipJets.Way(n) * drive[n];
            return sum;
        }

        static P3 Twist(float[] drive)
        {
            P3 sum = None;
            for (int n = 0; n < ShipJets.Count; n++)
                sum = sum + P3.Cross(ShipJets.Mouth(n) - ShipJets.Pivot, -ShipJets.Way(n)) * drive[n];
            return sum;
        }

        [Test]
        public void AtRest_NothingFires()
        {
            foreach (float d in Drive(None, None)) Assert.AreEqual(0f, d);
        }

        [Test]
        public void EveryWayTheShipIsPushed_LightsNozzlesThatPushItThatWay()
        {
            P3[] ways = { new P3(1, 0, 0), new P3(-1, 0, 0), new P3(0, 0, 1), new P3(0, 0, -1), new P3(0, -1, 0) };
            foreach (P3 way in ways)
            {
                float[] drive = Drive(way, None);
                P3 total = Total(drive);
                Assert.Greater(total.Length, 0.5f, "something fires for " + way);
                Assert.Greater(P3.Dot(total.Unit(), way), 0.95f, "the lit nozzles push the ship " + way);
                for (int n = 0; n < ShipJets.Count; n++)
                    if (drive[n] > 0f) Assert.Less(P3.Dot(ShipJets.Way(n), way), 0f, "a lit flame points away from " + way);
            }
        }

        [Test]
        public void AnUpwardPush_IsTheBigEnginesJob_AndLightsNoSmallNozzle()
        {
            foreach (float d in Drive(new P3(0, 1, 0), None)) Assert.AreEqual(0f, d);
        }

        [Test]
        public void EveryWayTheShipIsTurned_LightsNozzlesThatTwistItThatWay()
        {
            P3[] turns = { new P3(1, 0, 0), new P3(-1, 0, 0), new P3(0, 1, 0), new P3(0, -1, 0), new P3(0, 0, 1), new P3(0, 0, -1) };
            foreach (P3 turn in turns)
            {
                float[] drive = Drive(None, turn);
                P3 twist = Twist(drive);
                Assert.Greater(twist.Length, 0.1f, "something fires for a turn about " + turn);
                Assert.Greater(P3.Dot(twist.Unit(), turn), 0.9f, "the lit nozzles twist the ship about " + turn);
                Assert.Less(Total(drive).Length, 0.35f * Count(drive), "a turn hardly pushes the ship off its course");
            }
        }

        static int Count(float[] drive) { int c = 0; foreach (float d in drive) if (d > 0f) c++; return Math.Max(1, c); }

        [Test]
        public void AResting_HandOnTheMouse_DoesNotFlickerTheJets()
        {
            foreach (float d in Drive(None, new P3(0, ShipJets.LeastTurnRate * 0.5f, 0))) Assert.AreEqual(0f, d);
        }

        [Test]
        public void ALostCluster_NeverFires()
        {
            float[] drive = Drive(new P3(1, -1, 1), new P3(0.5f, 0.5f, 0.5f), cluster => cluster != 2);
            for (int n = 0; n < ShipJets.Count; n++)
                if (ShipJets.ClusterOf(n) == 2) Assert.AreEqual(0f, drive[n]);
        }

        [Test]
        public void ANozzle_NeverFiresHarderThanFull()
        {
            foreach (float d in Drive(new P3(5, -5, 5), new P3(9, 9, 9))) Assert.LessOrEqual(d, 1f);
        }

        [Test]
        public void TheNozzles_AreTheOnesTheThrusterShapeDraws()
        {
            for (int n = 0; n < ShipJets.Count; n++)
            {
                P3 centre = ShipLayout.Thruster(ShipJets.ClusterOf(n));
                Assert.AreEqual(ShipJets.MouthOut, P3.Distance(centre, ShipJets.Mouth(n)), 1e-4f);
                Assert.AreEqual(1f, ShipJets.Way(n).Length, 1e-4f);
                // Every nozzle points straight up or down or along the hull, never into it.
                P3 radial = new P3(centre.X, 0f, centre.Z).Unit();
                Assert.AreEqual(0f, P3.Dot(ShipJets.Way(n), radial), 1e-4f);
            }
        }
    }
}
