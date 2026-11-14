using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Cockpit;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Launch;
using OrbitalAnte.Navigation;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks Milestone 10 on real objects: the lock holds, the autopilot flies toward a locked body and says
    /// why when it will not, and a hard knock throws or kills a pilot who is not strapped in but only jolts one
    /// who is. How flying and crashing feel is on the playtest checklist instead.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class NavigationTests
    {
        GameObject _root;
        SolarSystemWorld _world;
        string _death;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            GameInput.Initialize();
            ContentDatabase.ResetStatics();
            SolarSystem.ResetStatics();
            ShipConfig.ResetStatics();
            ShipMeshes.ResetStatics();
            LaunchConfig.ResetStatics();
            CockpitConfig.ResetStatics();
            PlanetSurface.PictureWidthForTests = 64;
            _root = new GameObject("NavigationTestRoot");
            _world = _root.AddComponent<SolarSystemWorld>();
            _world.Build(4242, StartPlace.Orbit);
            _death = null;
            Astronaut.OnDeath = cause => _death = cause;
        }

        [TearDown]
        public void TearDown()
        {
            Astronaut.OnDeath = null;
            if (_root != null) Object.Destroy(_root);
            PlanetSurface.PictureWidthForTests = 0;
            Time.timeScale = 1f;
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        int Body(string id)
        {
            SolarSystem system = _world.Craft.System;
            for (int i = 0; i < system.Bodies.Length; i++)
            {
                if (system.Bodies[i].Spec.id == id) return i;
            }
            Assert.Fail("no body called " + id);
            return -1;
        }

        [UnityTest]
        public IEnumerator WithNothingLocked_TheAutopilotRefuses_AndSaysWhy()
        {
            yield return Frames(4);
            TestCraft craft = _world.Craft;
            Assert.AreEqual(-1, craft.LockedTarget);
            Assert.IsFalse(craft.StartPilot());
            Assert.IsFalse(craft.PilotOn);
            Assert.AreEqual(PilotStop.NoTarget, craft.LastStop);
            StringAssert.Contains("LOCK A TARGET", craft.LastStopWords);
        }

        [UnityTest]
        public IEnumerator LockedOnTheMoon_TheAutopilotClosesOnIt_AndLetsGoWhenThePilotIsNotInTheSeat()
        {
            yield return Frames(4);
            TestCraft craft = _world.Craft;
            // Point the nose at a body that is in plain view from here (which ones are depends on the seed)
            // and lock it, as a pilot would. It is called "moon" below whichever it turns out to be.
            int moon = -1;
            Vector3 to = Vector3.forward;
            foreach (string id in new[] { "Moon", "Mars", "Venus", "Jupiter", "Mercury", "Saturn" })
            {
                int body = Body(id);
                to = craft.System.Bodies[body].transform.position - craft.Body.position;
                craft.Body.rotation = Quaternion.LookRotation(to.normalized, craft.Body.rotation * Vector3.up);
                yield return Steps(2);
                yield return Frames(2);
                if (craft.UnderNose == body) { moon = body; break; }
            }
            Assert.GreaterOrEqual(moon, 0, "no body could be brought under the nose");
            craft.PressLock();
            Assert.AreEqual(moon, craft.LockedTarget);

            // The nose can wander; the lock stays.
            craft.Body.rotation *= Quaternion.Euler(0f, 70f, 0f);
            yield return Frames(2);
            Assert.AreEqual(moon, craft.Target);

            bool started = craft.StartPilot();
            if (!started)
            {
                // From this seed's starting place the Earth may stand in the way, and then it must say so.
                Assert.AreEqual(PilotStop.Blocked, craft.LastStop, craft.LastStopWords);
                StringAssert.Contains("IN THE WAY", craft.LastStopWords);
                yield break;
            }
            float before = (craft.System.Bodies[moon].transform.position - craft.Body.position).magnitude;
            Vector3 relativeBefore = craft.Body.linearVelocity - craft.System.LocalVelocityOf(moon);
            float closingBefore = Vector3.Dot(relativeBefore, to.normalized);
            float mostPush = 0f;
            for (int i = 0; i < 150; i++)
            {
                yield return new WaitForFixedUpdate();
                mostPush = Mathf.Max(mostPush, craft.PilotPushShare);
            }
            Assert.IsTrue(craft.PilotOn, "it gave up: " + craft.LastStopWords);
            Vector3 toNow = craft.System.Bodies[moon].transform.position - craft.Body.position;
            Vector3 relativeNow = craft.Body.linearVelocity - craft.System.LocalVelocityOf(moon);
            Assert.Greater(Vector3.Dot(relativeNow, toNow.normalized), closingBefore + 20f, "three seconds of autopilot should have it closing much faster");
            Assert.Less(Vector3.Angle(craft.transform.forward, toNow), 25f, "the nose should have swung round to the Moon");
            Assert.Greater(mostPush, 0f, "the autopilot's push must be charged to the fuel");
            Assert.AreNotEqual(PilotPhase.Off, craft.Order.Phase);
            Assert.Greater(before, 0f);

            // Locking a different body in mid-flight sends the autopilot there instead (or it says what is in the way).
            int other = -1;
            foreach (string id in new[] { "Mars", "Venus", "Jupiter", "Saturn", "Moon" })
            {
                int body = Body(id);
                if (body == moon) continue;
                Vector3 toOther = craft.System.Bodies[body].transform.position - craft.Body.position;
                craft.Body.rotation = Quaternion.LookRotation(toOther.normalized, craft.Body.rotation * Vector3.up);
                yield return Frames(2);
                if (craft.UnderNose == body || craft.NearNose == body) { other = body; break; }
            }
            if (other >= 0)
            {
                craft.PressLock();
                Assert.AreEqual(other, craft.LockedTarget, "Lock should take the new body while the autopilot is on");
                if (!craft.PilotOn)
                {
                    Assert.AreEqual(PilotStop.Blocked, craft.LastStop, craft.LastStopWords);
                    yield break;
                }
                yield return Steps(5);
                Assert.IsTrue(craft.PilotOn, "it gave up after being sent somewhere new: " + craft.LastStopWords);
            }

            // Reading the map takes the pilot's hands off, but the autopilot flies on.
            craft.ControlsLocked = true;
            yield return Frames(3);
            yield return Steps(2);
            Assert.IsTrue(craft.PilotOn, "opening the map must not stop the autopilot");
            craft.ControlsLocked = false;

            // Unbuckle: nobody is flying, so it lets go and says so.
            _world.Ship.Belt.Release();
            yield return Frames(3);
            yield return Steps(2);
            Assert.IsFalse(craft.PilotOn);
            Assert.AreEqual(PilotStop.Unbuckled, craft.LastStop);
        }

        [UnityTest]
        public IEnumerator AHardKnock_JoltsAStrappedPilot_AndKillsALooseOne()
        {
            yield return Frames(4);
            Astronaut crew = _world.Astronaut;
            Assert.AreSame(crew, _world.Ship.Crew);
            Assert.IsTrue(_world.Ship.Belt.Buckled);
            float fatalLoose = crew.Crash.Rules.fatalLoose;

            crew.Knock(fatalLoose + 2f, Vector3.forward);
            yield return Frames(3);
            Assert.IsFalse(crew.Dead, "strapped in, that knock is survivable");
            Assert.AreNotEqual(0f, crew.Crash.KickPitch, "but it should be felt");

            // Wait for the blow to be over, then take the belt off and take the same knock again.
            yield return new WaitForSeconds(0.4f);
            _world.Ship.Belt.Release();
            yield return Frames(3);
            crew.Knock(fatalLoose + 2f, Vector3.forward);
            Assert.IsTrue(crew.Dead);
            StringAssert.Contains("not buckled in", crew.DeathCause);
            Assert.IsNull(_death, "the screen waits a moment, so you see what happened first");
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(crew.DeathCause, _death, "the cause should have been handed to the death screen");
            yield return Frames(2);
            Assert.IsTrue(_world.Craft.Unmanned, "nobody is flying the ship now");
        }

        [UnityTest]
        public IEnumerator ALooseBodyInAMiddlingKnock_IsThrownAndDazed_ThenGetsUp()
        {
            yield return Frames(4);
            Astronaut crew = _world.Astronaut;
            _world.Ship.Belt.Release();
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(AstronautMode.Aboard, crew.Mode, "after unbuckling you should be on your feet in the cabin");
            crew.Knock(crew.Crash.Rules.throwSpeed + 2f, Vector3.right);
            Assert.IsTrue(crew.Dazed);
            Assert.IsFalse(crew.Dead);
            yield return new WaitForSeconds(crew.Crash.Rules.dazedSeconds + 0.3f);
            Assert.IsFalse(crew.Dazed, "the daze should pass");
        }
    }
}
