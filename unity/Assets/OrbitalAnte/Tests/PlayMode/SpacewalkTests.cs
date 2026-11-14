using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Cockpit;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Launch;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using OrbitalAnte.Spacewalk;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks Milestone 11 on real objects: stepping out of a ship in flight keeps its speed and clips the
    /// tether on, the tether holds at its length and no closer, the suit fills from the ship, and no air or a
    /// hard knock ends the run with the cause in words. How flying on the jets feels is on the playtest
    /// checklist instead.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class SpacewalkTests
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
            SuitConfig.ResetStatics();
            PlanetSurface.PictureWidthForTests = 64;
            _root = new GameObject("SpacewalkTestRoot");
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
            SuitConfig.ResetStatics();
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

        /// <summary>Puts the pilot outside, a little below the belly, moving with the ship plus <paramref name="extra"/>.</summary>
        void StepOut(Vector3 extra)
        {
            OrbitalAnte.Ship.Ship ship = _world.Ship;
            Vector3 feet = ship.transform.TransformPoint(new Vector3(0f, ShipLayout.BellyY - 4f, 0f));
            _world.Astronaut.StepOutsideForTests(feet, ship.Body.linearVelocity + extra);
        }

        [UnityTest]
        public IEnumerator SteppingOutInFlight_YouKeepTheShipsSpeed_AndTheTetherIsOn()
        {
            yield return Frames(4);
            Astronaut crew = _world.Astronaut;
            StepOut(Vector3.zero);
            Assert.AreEqual(AstronautMode.Outside, crew.Mode);
            Assert.IsTrue(crew.Tethered, "leaving a ship that is not on the ground should clip the line on");
            yield return Steps(100);
            yield return Frames(2);
            Vector3 against = crew.Suit.linearVelocity - _world.Ship.Body.linearVelocity;
            Assert.Less(against.magnitude, 0.5f, "two seconds later you should still be moving with the ship");
            Assert.Less(Vector3.Distance(crew.Suit.position, _world.Ship.Body.position), 12f);
            Assert.AreEqual(MatchTarget.Ship, crew.SuitState.MatchTarget, "beside the ship, speed is measured against the ship");
            Assert.IsTrue(_world.SuitHud.Shown, "the suit's readout should be up outside");
            Assert.AreEqual(Posture.Free, crew.Posture, "in orbit there is no down");
        }

        [UnityTest]
        public IEnumerator TheTether_StopsYouAtItsLength_AndWithoutIt_YouAreGone()
        {
            yield return Frames(4);
            Astronaut crew = _world.Astronaut;
            float length = crew.SuitDials.rules.tetherLength;
            Vector3 away = -_world.Ship.transform.up * 8f;
            StepOut(away);
            float furthest = 0f;
            for (int i = 0; i < 500; i++)
            {
                yield return new WaitForFixedUpdate();
                furthest = Mathf.Max(furthest, Vector3.Distance(crew.Suit.worldCenterOfMass, crew.TetherAnchor));
            }
            Assert.Less(furthest, length + 1f, "the line let you go past its length");
            float end = Vector3.Distance(crew.Suit.worldCenterOfMass, crew.TetherAnchor);
            Assert.Greater(end, length - 3f, "the line must hold you at its end, not reel you in");

            // Unclip and push off again: now nothing stops you.
            crew.SetTetherForTests(false);
            crew.Suit.linearVelocity = _world.Ship.Body.linearVelocity + away;
            yield return Steps(300);
            Assert.Greater(Vector3.Distance(crew.Suit.worldCenterOfMass, crew.TetherAnchor), length + 20f);
            Assert.IsFalse(crew.SuitState.TetherInReach);
        }

        [UnityTest]
        public IEnumerator Aboard_TheSuitFillsFromTheShip_AndTheShipsAirGoesDown()
        {
            yield return Frames(4);
            Astronaut crew = _world.Astronaut;
            crew.SuitTanks.Set(0.2f, 0.1f);
            float shipBefore = _world.Dash.Supplies.Oxygen;
            yield return new WaitForSeconds(crew.SuitDials.rules.refillSeconds + 0.5f);
            Assert.AreEqual(1f, crew.SuitTanks.Air, 0.01f);
            Assert.AreEqual(1f, crew.SuitTanks.Fuel, 0.01f);
            Assert.Less(_world.Dash.Supplies.Oxygen, shipBefore - 0.01f, "the suit's air has to come from somewhere");
        }

        [UnityTest]
        public IEnumerator OutOfAirOutside_YouAreWarned_ThenItIsFatal_AndTheScreenSaysWhy()
        {
            yield return Frames(4);
            Astronaut crew = _world.Astronaut;
            StepOut(Vector3.zero);
            crew.SuitTanks.Set(0f, 1f);
            yield return Frames(12);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(crew.Dead, "there is a moment's grace");
            StringAssert.Contains("NO AIR", _world.SuitHud.NoticeShown);
            // Let the grace run out without making the test wait for it.
            crew.SuitTanks.Tick(crew.SuitDials.rules.airGraceSeconds, true, false, 0f, 0f);
            yield return Frames(3);
            Assert.IsTrue(crew.Dead);
            Assert.AreEqual(SuitKnock.NoAir, crew.DeathCause);
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(SuitKnock.NoAir, _death);
        }

        [UnityTest]
        public IEnumerator HittingSomethingHardInTheSuit_IsFatal_AndAGentleBumpIsNot()
        {
            yield return Frames(4);
            Astronaut crew = _world.Astronaut;
            StepOut(Vector3.zero);
            yield return Steps(2);
            crew.SuitHit(crew.SuitDials.rules.hurtSpeed - 2f, Vector3.forward);
            Assert.IsFalse(crew.Dead);
            Assert.IsFalse(crew.Dazed);
            crew.SuitHit(crew.SuitDials.rules.hurtSpeed + 2f, Vector3.forward);
            Assert.IsFalse(crew.Dead);
            Assert.IsTrue(crew.Dazed, "a hard bump should be felt");
            crew.SuitHit(crew.SuitDials.rules.fatalSpeed + 2f, Vector3.forward);
            Assert.IsTrue(crew.Dead);
            StringAssert.Contains("m/s", crew.DeathCause);
        }
    }
}
