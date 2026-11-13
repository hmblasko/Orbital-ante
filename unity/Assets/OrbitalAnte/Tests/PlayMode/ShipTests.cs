using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using OrbitalAnte.Weapons;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks Milestone 7 on real objects: the ship is built whole with its baked light, you start strapped into
    /// its seat, the cabin carries you with it whatever the ship does, and damage, mending and the hatch work.
    /// Things that need a button pressed (walking, buckling, the lift ride) are on the playtest checklist instead.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class ShipTests
    {
        GameObject _root;
        SolarSystemWorld _world;
        Ship.Ship _ship;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            GameInput.Initialize();
            ContentDatabase.ResetStatics();
            SolarSystem.ResetStatics();
            ShipConfig.ResetStatics();
            ShipMeshes.ResetStatics();
            PlanetSurface.PictureWidthForTests = 64;
            _root = new GameObject("ShipTestRoot");
            _world = _root.AddComponent<SolarSystemWorld>();
            _world.Build(4242, StartPlace.Orbit);
            _ship = _world.Ship;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            SolarSystem.ResetStatics();
            PlanetSurface.ResetStatics();
            ContentDatabase.ResetStatics();
            ShipMeshes.ResetStatics();
        }

        static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        [Test]
        public void TheShipIsBuiltWhole_WithEveryPartHittable()
        {
            Assert.IsNotNull(_ship);
            ShipPart[] parts = _ship.GetComponentsInChildren<ShipPart>(true);
            var seen = new bool[_ship.Model.Count];
            foreach (ShipPart part in parts)
            {
                Assert.AreSame(_ship, part.Ship);
                seen[part.Index] = true;
                // The hatch is hit through the base it sits in; everything else has a shape of its own, sometimes on a child.
                if (_ship.Model.Parts[part.Index].Kind == PartKind.Hatch) continue;
                Assert.IsNotNull(part.GetComponentInChildren<Collider>(true), _ship.Model.Parts[part.Index].Name + " has a part marker with nothing to hit.");
            }
            for (int i = 0; i < seen.Length; i++) Assert.IsTrue(seen[i], _ship.Model.Parts[i].Name + " cannot be hit.");
            Assert.AreSame(_ship.Flight, _world.Craft);
            Assert.IsFalse(_ship.Body.useGravity, "the ship must only feel the solar system's gravity");
        }

        [Test]
        public void TheBakedLightFileFitsTheCabin()
        {
            // If this fails, the cabin was reshaped without the light being baked again.
            Assert.IsTrue(_ship.LightBaked, "Resources/Ship/LanderBake is missing or was baked for a different cabin.");
        }

        [Test]
        public void YouStartStrappedIn_LookingOutOfTheFrontWindows()
        {
            Astronaut you = _world.Astronaut;
            Assert.AreEqual(AstronautMode.Seated, you.Mode);
            Assert.IsTrue(_ship.Belt.Buckled);
            Vector3 eye = _ship.transform.InverseTransformPoint(you.Eye.transform.position);
            Assert.AreEqual(ShipLayout.SeatedEye.X, eye.x, 0.01f);
            Assert.AreEqual(ShipLayout.SeatedEye.Y, eye.y, 0.01f);
            Assert.AreEqual(ShipLayout.SeatedEye.Z, eye.z, 0.01f);
            Assert.Greater(Vector3.Dot(you.Eye.transform.forward, _ship.transform.forward), 0.99f);
            Assert.AreSame(you.Eye, _ship.Flight.Eye);
        }

        [UnityTest]
        public IEnumerator TheCabinCarriesYou_HoweverTheShipTumbles()
        {
            Astronaut you = _world.Astronaut;
            _ship.Body.angularVelocity = new Vector3(2.5f, 1.5f, -2f);
            _ship.Body.AddForce(_ship.transform.right * 40f, ForceMode.VelocityChange);
            yield return Steps(120);
            yield return null;
            Vector3 eye = _ship.transform.InverseTransformPoint(you.Eye.transform.position);
            Assert.Less(Vector3.Distance(eye, new Vector3(ShipLayout.SeatedEye.X, ShipLayout.SeatedEye.Y, ShipLayout.SeatedEye.Z)), 0.02f,
                "the view came loose from the seat");
        }

        [UnityTest]
        public IEnumerator OnlyABuckledPilotFliesTheShip()
        {
            yield return null;
            Assert.IsFalse(_ship.Flight.Unmanned);
            Assert.IsTrue(_ship.Belt.Release());
            yield return new WaitForSeconds(_ship.Config.unbuckleSeconds + 0.3f);
            Assert.IsTrue(_ship.Flight.Unmanned, "the thrusters must not answer when nobody is strapped in");
            Assert.AreEqual(AstronautMode.Aboard, _world.Astronaut.Mode);
            Vector3 feet = _world.Astronaut.transform.localPosition;
            Assert.IsTrue(InteriorWalker.CanStand(feet.x, feet.z), "stood up somewhere you cannot stand");
        }

        [UnityTest]
        public IEnumerator Glass_Cracks_Breaks_AndIsMended()
        {
            int window = _ship.Model.Find(PartKind.Window, 0);
            Vector3 at = _ship.PartPosition(window);
            float max = _ship.Model.Parts[window].MaxHealth;
            int damagedCalls = 0, mended = 0;
            _ship.PartDamaged += (i, r) => damagedCalls++;
            _ship.PartRepaired += i => mended++;

            _ship.DamagePart(window, max * 0.5f, at);
            yield return null;
            Assert.AreEqual(PartState.Damaged, _ship.Model.State(window));
            Assert.IsTrue(_ship.Model.CabinIntact);
            _ship.DamagePart(window, max, at);
            yield return null;
            Assert.AreEqual(PartState.Broken, _ship.Model.State(window));
            Assert.IsFalse(_ship.Model.CabinIntact);
            Assert.AreEqual(2, damagedCalls);

            for (int i = 0; i < 400 && mended == 0; i++) _ship.Repair(window, 0.02f);
            Assert.AreEqual(1, mended);
            Assert.AreEqual(PartState.Sound, _ship.Model.State(window));
            Assert.IsTrue(_ship.Model.CabinIntact);
        }

        [UnityTest]
        public IEnumerator AShotFromInside_StopsAtTheSolidHull()
        {
            yield return null;
            _ship.CrewAboard = true;
            Assert.AreSame(_ship, Ship.Ship.Boarded);
            int cabin = _ship.CabinPart;
            float before = _ship.Model.Health(cabin);
            // Straight back from behind the seat: plain hull there, and nothing of the ship beyond it.
            Vector3 eye = _ship.transform.TransformPoint(new Vector3(0f, 1.62f, -1.15f));
            Vector3 aim = -_ship.transform.forward;
            WeaponData pistol = ContentDatabase.Get<WeaponData>(WeaponData.PistolId);
            ShotResult shot = Ballistics.FireFromPlayer(pistol, eye, aim);
            Assert.IsTrue(shot.HitAnything, "the bullet went through the cabin wall");
            Vector3 wall = _ship.transform.TransformPoint(new Vector3(0f, 1.62f, -ShipLayout.InnerRadiusAt(1.62f)));
            Assert.Less(Vector3.Distance(shot.End, wall), 0.05f);
            Assert.Less(_ship.Model.Health(cabin), before, "the cabin took no damage");

            // Out through the glass is still the glass's business, not the wall's.
            int front = _ship.Model.IndexOf(ShipLayout.Windows[1].Id);
            ShipLayout.WindowCentre(ShipLayout.Windows[1], out P3 centre, out _);
            Vector3 from = _ship.transform.TransformPoint(new Vector3(0f, 1.3f, 0f));
            Vector3 to = _ship.transform.TransformPoint(new Vector3(centre.X, centre.Y, centre.Z));
            float glassBefore = _ship.Model.Health(front);
            Ballistics.FireFromPlayer(pistol, from, (to - from).normalized);
            Assert.Less(_ship.Model.Health(front), glassBefore, "a shot at the window should hit the glass");
            _ship.CrewAboard = false;
            Assert.IsNull(Ship.Ship.Boarded);
        }

        [UnityTest]
        public IEnumerator AHeavyBlow_TearsALegOff_AsLooseWreckage()
        {
            int leg = _ship.Model.Find(PartKind.Leg, 1);
            int bodiesBefore = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
            DamageResult result = _ship.DamagePart(leg, _ship.Model.Parts[leg].MaxHealth * 3f, _ship.PartPosition(leg));
            Assert.IsTrue(result.FellOff);
            yield return Steps(2);
            Assert.AreEqual(3, _ship.Model.LegsStanding);
            Assert.Greater(Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length, bodiesBefore, "the leg vanished instead of coming away");
            Assert.AreNotEqual(leg, _ship.FindRepairTarget(_ship.PartPosition(leg) + _ship.transform.forward * 2f, -_ship.transform.forward),
                "a lost part must not be offered for mending");
        }

        [UnityTest]
        public IEnumerator TheHatchOpensAndCloses_EvenInFlight_ButNotWhenBroken()
        {
            Assert.IsTrue(_ship.Hatch.Shut);
            Assert.IsTrue(_ship.ToggleHatch());
            yield return new WaitForSeconds(_ship.Config.doorSeconds + 0.3f);
            Assert.IsTrue(_ship.Hatch.Open);
            Assert.IsTrue(_ship.ToggleHatch());
            yield return new WaitForSeconds(_ship.Config.doorSeconds + 0.3f);
            Assert.IsTrue(_ship.Hatch.Shut);

            int hatch = _ship.Model.Find(PartKind.Hatch);
            _ship.DamagePart(hatch, _ship.Model.Parts[hatch].MaxHealth * 2f, _ship.PartPosition(hatch));
            Assert.IsFalse(_ship.ToggleHatch(), "a broken hatch must stay as it is until mended");
        }

        [UnityTest]
        public IEnumerator InOrbit_TheLiftHasNowhereToSetYouDown()
        {
            yield return Steps(2);
            Assert.IsTrue(float.IsNaN(_ship.FindLiftBottom()), "the lift would lower you into empty space");
        }

        [UnityTest]
        public IEnumerator ABrokenDish_TakesTheMapAway()
        {
            yield return null;
            Assert.IsTrue(_world.Map.CanOpen());
            int dish = _ship.Model.Find(PartKind.Dish);
            _ship.DamagePart(dish, _ship.Model.Parts[dish].MaxHealth, _ship.PartPosition(dish));
            Assert.IsFalse(_world.Map.CanOpen());
            Assert.IsNotEmpty(_world.Map.WhyNot());
        }
    }
}
