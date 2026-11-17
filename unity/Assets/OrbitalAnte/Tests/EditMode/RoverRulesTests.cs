using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Look;
using OrbitalAnte.Rover;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests.EditMode
{
    public class RoverRulesTests
    {
        static readonly RoverRules Rules = new RoverRules();

        [Test]
        public void Steering_NarrowsWithSpeed_AndTheBackWheelsOnlyHelpAtACrawl()
        {
            Assert.That(RoverHandling.SteerAngle(Rules, 0f), Is.EqualTo(Rules.steerSlow).Within(0.01f));
            Assert.That(RoverHandling.SteerAngle(Rules, Rules.topSpeed), Is.EqualTo(Rules.steerFast).Within(0.01f));
            Assert.That(RoverHandling.SteerAngle(Rules, 15f), Is.LessThan(Rules.steerSlow).And.GreaterThan(Rules.steerFast));
            Assert.That(RoverHandling.RearSteer(Rules, 0f), Is.EqualTo(Rules.rearSteerShare).Within(0.001f));
            Assert.That(RoverHandling.RearSteer(Rules, 15f), Is.EqualTo(0f).Within(0.001f), "at speed four-wheel steering would make it twitchy");
        }

        [Test]
        public void TheMotors_NeverPushPastTopSpeed_AndBoostRaisesIt()
        {
            Assert.That(RoverHandling.Drive(Rules, 1f, 0f, false), Is.EqualTo(Rules.driveAccel).Within(0.001f));
            Assert.That(RoverHandling.Drive(Rules, 1f, Rules.topSpeed, false), Is.EqualTo(0f).Within(0.001f));
            Assert.That(RoverHandling.Drive(Rules, 1f, Rules.topSpeed + 5f, false), Is.EqualTo(0f));
            Assert.That(RoverHandling.Drive(Rules, 1f, Rules.topSpeed, true), Is.GreaterThan(0f), "with the boost lit the motors keep pulling");
            Assert.That(RoverHandling.BoostPush(Rules, Rules.boostSpeed), Is.EqualTo(0f).Within(0.001f));
            Assert.That(RoverHandling.BoostPush(Rules, 0f), Is.EqualTo(Rules.boostAccel));
            Assert.That(RoverHandling.Drive(Rules, -1f, 0f, false), Is.LessThan(0f));
            Assert.That(RoverHandling.Drive(Rules, -1f, -Rules.reverseSpeed, false), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Rules.topSpeed * 3.6f, Is.GreaterThan(100f), "really fast (the real rover managed 18 km/h)");
        }

        [Test]
        public void OnTheMoon_ItIsHeldDownHarder_ThanOnEarth_AndMoreWhenFast()
        {
            float moon = RoverHandling.Hold(Rules, 1.62f, 0f), earth = RoverHandling.Hold(Rules, 9.81f, 0f);
            Assert.That(moon, Is.GreaterThan(5f));
            Assert.That(earth, Is.EqualTo(0f));
            Assert.That(RoverHandling.Hold(Rules, 1.62f, Rules.topSpeed), Is.GreaterThan(moon));
            Assert.That(1.62f + moon, Is.EqualTo(Rules.gripGravity).Within(0.001f), "parked on the Moon it presses on its tyres like the grip gravity");
        }

        [Test]
        public void AtRest_TheSpringsHoldTheGripGravity_SoItSitsAtTheHeightItWasDrawn()
        {
            float corner = Rules.mass / 4f;
            Assert.That(RoverHandling.Suspension(Rules, corner, 0f, 0f), Is.EqualTo(corner * Rules.gripGravity).Within(corner * 0.01f));
            Assert.That(RoverHandling.Suspension(Rules, corner, -1f, 0f), Is.EqualTo(0f), "a spring never pulls the wheel down onto the ground");
            float compressing = RoverHandling.Suspension(Rules, corner, 0f, 1f) - RoverHandling.Suspension(Rules, corner, 0f, 0f);
            float rebounding = RoverHandling.Suspension(Rules, corner, 0.1f, 0f) - RoverHandling.Suspension(Rules, corner, 0.1f, -1f);
            Assert.That(rebounding, Is.GreaterThan(compressing), "firmer coming back up, so it never pogos off bumps");
        }

        [Test]
        public void SidewaysGrip_PeaksThenSlides_SoASlideCanBeCaught()
        {
            Assert.That(RoverHandling.SideGrip(Rules, Rules.peakSlip), Is.EqualTo(1f).Within(0.001f));
            Assert.That(RoverHandling.SideGrip(Rules, 2f), Is.LessThan(1f));
            Assert.That(RoverHandling.SideGrip(Rules, 80f), Is.EqualTo(Rules.slideGrip).Within(0.001f));
            Assert.That(RoverHandling.RearGrip(Rules, Rules.topSpeed, false), Is.EqualTo(Rules.rearGripAtTop).Within(0.001f));
            Assert.That(RoverHandling.RearGrip(Rules, 0f, false), Is.EqualTo(1f));
            Assert.That(RoverHandling.RearGrip(Rules, 0f, true), Is.EqualTo(Rules.handbrakeGrip));
        }

        [Test]
        public void RunningSomeoneOver_GoesFromBumpToLimbsToSplat()
        {
            Assert.That(RunOver.Judge(Rules, 1f), Is.EqualTo(RunOverResult.Nothing));
            Assert.That(RunOver.Judge(Rules, Rules.bumpFrom + 0.1f), Is.EqualTo(RunOverResult.Bump));
            Assert.That(RunOver.Judge(Rules, Rules.breakFrom), Is.EqualTo(RunOverResult.Break));
            Assert.That(RunOver.Judge(Rules, Rules.splatFrom), Is.EqualTo(RunOverResult.Splat));
            Assert.That(Rules.splatFrom, Is.LessThan(Rules.topSpeed / 2f), "a splat should be easy to get, not a top-speed special");
            // MannequinConfig's defaults: shin 50, thigh 68, forearm 45. At the speed limbs start to break, a shin goes.
            Assert.That(RunOver.Damage(Rules, Rules.breakFrom), Is.GreaterThanOrEqualTo(45f));
            Assert.That(RunOver.Damage(Rules, Rules.splatFrom - 0.1f), Is.GreaterThanOrEqualTo(68f), "just under a splat, a thigh comes off");
            Assert.That(RunOver.Damage(Rules, Rules.bumpFrom), Is.EqualTo(0f));
        }

        [Test]
        public void TheBoostTank_RunsDry_AndWillNotRelightUntilLetGo()
        {
            var tank = new BoostTank();
            float t = 0f;
            while (tank.Step(0.02f, true, Rules)) t += 0.02f;
            Assert.That(t, Is.EqualTo(Rules.boostSeconds).Within(0.05f));
            for (int i = 0; i < 100; i++) Assert.That(tank.Step(0.02f, true, Rules), Is.False, "still held after running dry");
            tank.Step(0.02f, false, Rules);
            Assert.That(tank.Step(0.02f, true, Rules), Is.True, "let go and pressed again, it lights on what has refilled");
        }

        [Test]
        public void Knocks_HurtOnlyPastAThreshold_AndAWallAtFullSpeedKills()
        {
            Assert.That(RoverHandling.Hurt(Rules, Rules.hurtFrom, out bool kills), Is.EqualTo(0f));
            Assert.That(kills, Is.False);
            Assert.That(RoverHandling.Hurt(Rules, Rules.hurtFrom + 5f, out kills), Is.GreaterThan(0f));
            RoverHandling.Hurt(Rules, Rules.topSpeed, out kills);
            Assert.That(kills, Is.True, "head first into a cliff at top speed is fatal");
            Assert.That(Rules.killFrom, Is.GreaterThan(Rules.splatFrom * 1.5f), "running people over never hurts the driver");
        }

        [Test]
        public void HangingOnTheDavit_ItsTyresAreWellClearOfTheGround_AndBetweenTheBackLegs()
        {
            float tyreBottom = RoverLayout.StowInShip.Y - Rules.travelDown;
            Assert.That(tyreBottom - ShipLayout.GroundY, Is.GreaterThan(1.5f));
            // The back legs at the rover's height must be wider apart than the rover.
            for (int leg = 0; leg < ShipLayout.Legs; leg++)
            {
                P3 root = ShipLayout.LegRoot(leg), foot = ShipLayout.Footpad(leg);
                if (root.Z > 0f) continue;
                float y = RoverLayout.StowInShip.Y + 1f;
                float t = (y - root.Y) / (foot.Y - root.Y);
                float x = root.X + (foot.X - root.X) * t;
                Assert.That(Math.Abs(x), Is.GreaterThan(RoverLayout.HalfTrack + RoverLayout.WheelWidth), "leg " + leg);
            }
            // Its back end stays out of the rear engine's flame (the engine ring is 1.5 m out, the exit 0.4 wide).
            float tail = RoverLayout.StowInShip.Z + 1.9f;   // the rover faces backward, so its tail points at the ship
            Assert.That(tail, Is.LessThan(-ShipLayout.EngineDistance - ShipLayout.EngineExitRadius));
        }

        // ------------------------------------------------------------------ the model files

        static string ModelsFolder()
        {
            string env = Environment.GetEnvironmentVariable("OA_MODELS");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 12 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Resources", "Models");
                if (Directory.Exists(candidate)) return candidate;
                candidate = Path.Combine(dir, "Assets", "OrbitalAnte", "Resources", "Models");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        static ModelFile Load(string id)
        {
            string folder = ModelsFolder();
            if (folder == null) Assert.Ignore("Resources/Models not found");
            string path = Path.Combine(folder, id + ".bytes");
            Assert.IsTrue(File.Exists(path), "Model file not found: " + path);
            ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
            Assert.IsNotNull(model, problem);
            return model;
        }

        static void AssertSocket(ModelFile model, string name, P3 expected)
        {
            Assert.IsTrue(model.TrySocket(name, out ModelSocket s), "missing " + name);
            Assert.AreEqual(expected.X, s.X, 0.002f, name + " x");
            Assert.AreEqual(expected.Y, s.Y, 0.002f, name + " y");
            Assert.AreEqual(expected.Z, s.Z, 0.002f, name + " z");
        }

        [Test]
        public void RoverModelFile_MatchesTheLayout()
        {
            // tools/blender/build_rover.py carries its own copy of RoverLayout. If either changes, this fails until
            // the script is changed to match and run again.
            ModelFile model = Load(RoverLayout.Model);
            for (int i = 0; i < RoverLayout.Wheels; i++)
            {
                AssertSocket(model, "Wheel" + RoverLayout.WheelNames[i], RoverLayout.Hub(i));
                AssertSocket(model, "Arm" + RoverLayout.WheelNames[i], RoverLayout.ArmPivot(i));
            }
            AssertSocket(model, "Eye", RoverLayout.Eye);
            AssertSocket(model, "Exit", RoverLayout.Exit);
            AssertSocket(model, "BoostL", RoverLayout.Boost(-1));
            AssertSocket(model, "BoostR", RoverLayout.Boost(1));
            AssertSocket(model, "Lamp", RoverLayout.Lamp);
            foreach (string piece in new[] { "body", "wheel", "arm", "lampGlow", "screenGlow", "boostGlow" })
                Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            // The body plus four wheels and four arms, as drawn: docs/performance_budget.md allows 60,000.
            int drawn = model.Part("body").TriangleCount + 4 * (model.Part("wheel").TriangleCount + model.Part("arm").TriangleCount);
            Assert.LessOrEqual(drawn, 60000);

            ModelFile davit = Load(RoverLayout.DavitModel);
            AssertSocket(davit, "Stow", RoverLayout.StowInShip);
            Assert.IsNotNull(davit.Part("davit"));
        }
    }
}
