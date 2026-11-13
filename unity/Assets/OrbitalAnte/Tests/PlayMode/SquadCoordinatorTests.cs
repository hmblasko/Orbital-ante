using System.Collections;
using NUnit.Framework;
using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Weapons;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>Engine integration fixtures. These require Unity PlayMode; cloud compatibility compilation does not run them.</summary>
    public class SquadCoordinatorTests
    {
        GameObject _root;
        TacticalAIConfig _config;
        MannequinConfig _bodyConfig;
        SquadCoordinator _squad;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure(); CombatWorld.Reset(); Time.timeScale = 1;
            _root = new GameObject("SquadFixture");
            Box(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
            _config = ScriptableObject.CreateInstance<TacticalAIConfig>();
            _config.senseInterval = 0.05f; _config.sightFreshness = 0.2f; _config.memorySeconds = 0.6f;
            _config.simultaneousAttackers = 1;
            _bodyConfig = ScriptableObject.CreateInstance<MannequinConfig>();
            _bodyConfig.reactionTime = 0.1f;
            _squad = _root.AddComponent<SquadCoordinator>();
            _squad.Initialize(_config, new TacticalGrid(-28, -28, 28, 28, 1.5, 0.7, null));
        }
        [TearDown]
        public void TearDown()
        {
            CombatWorld.Reset(); GibManager.DestroyInstance();
            if (WeaponFx.Instance != null) Object.Destroy(WeaponFx.Instance.gameObject);
            if (_root != null) Object.Destroy(_root);
            if (_config != null) Object.Destroy(_config);
            if (_bodyConfig != null) Object.Destroy(_bodyConfig);
            Time.timeScale = 1;
        }
        GameObject Box(Vector3 centre, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(_root.transform, false); box.transform.position = centre; box.transform.localScale = size;
            return box;
        }
        Mannequin Agent(int id, Vector3 position, bool armed = false, bool moves = false)
        {
            var body = MannequinFactory.Create(position, Quaternion.identity, _root.transform, _bodyConfig);
            var combat = body.GetComponent<MannequinCombat>(); var loco = body.GetComponent<MannequinLocomotion>();
            loco.Wander = moves; combat.RequireSquad = true;
            Assert.True(_squad.Register(combat, loco, id));
            if (armed)
            {
                Weapon gun = Weapon.Create(WeaponData.Rifle, _root.transform, true);
                gun.transform.position = position + Vector3.up; Assert.True(combat.TryPickUp(gun));
            }
            return body;
        }
        Transform Eye(Vector3 position)
        {
            var eye = new GameObject("ObservedPlayerEye").transform;
            eye.SetParent(_root.transform, false); eye.position = position;
            CombatWorld.PlayerEye = eye; return eye;
        }

        [UnityTest]
        public IEnumerator DisableReleasesRegistrationLeaseAndReservation_ReenableRegistersOnce_AndDuplicateIdsAreRejected()
        {
            Mannequin first = Agent(0, Vector3.zero);
            MannequinCombat combat = first.GetComponent<MannequinCombat>();
            Assert.True(_squad.Register(combat, first.GetComponent<MannequinLocomotion>(), 0));
            Assert.AreEqual(1, _squad.Model.Count);
            Mannequin other = MannequinFactory.Create(new Vector3(4, 0, 0), Quaternion.identity, _root.transform, _bodyConfig);
            Assert.False(_squad.Register(other.GetComponent<MannequinCombat>(), other.GetComponent<MannequinLocomotion>(), 0));
            _squad.Model.UpdateMember(0, new SquadMemberState { Alive = true, Armed = true, ReadyToFire = true, CanMove = true });
            _squad.Model.ReportSight(0, new Vec3d(0, 1.3, 12), Time.time); _squad.Model.Tick(Time.time);
            _squad.Model.Reserve(0, new Vec3d(4, 0, 4)); Assert.True(_squad.Model.HasFirePermit(0, Time.time));
            combat.enabled = false;
            Assert.False(combat.TacticalState.Alive); Assert.AreEqual(0, _squad.Model.Count);
            Assert.False(_squad.Model.HasFirePermit(0, Time.time));
            Assert.True(_squad.Model.ReservationFree(1, new Vec3d(4, 0, 4), 2));
            combat.enabled = true; Assert.AreEqual(1, _squad.Model.Count);
            yield return null; Assert.AreEqual(1, _squad.Model.Count);
            combat.enabled = false;
            Assert.True(_squad.Register(other.GetComponent<MannequinCombat>(), other.GetComponent<MannequinLocomotion>(), 0));
            Object.Destroy(first.gameObject);
            yield return null; Assert.AreEqual(1, _squad.Model.Count, "delayed destruction cannot remove the replacement actor");
        }

        [UnityTest]
        public IEnumerator ClearingMembersForRespawnForgetsTheOldSquadsContact()
        {
            Agent(0, Vector3.zero); yield return null;
            _squad.Model.ReportSight(0, new Vec3d(15, 1.3, 10), Time.time);
            Assert.True(_squad.KnowsTarget(out _));
            _squad.ClearMembers(); Assert.False(_squad.KnowsTarget(out _)); Assert.AreEqual(0, _squad.Model.Count);
            Agent(0, new Vector3(2, 0, 0)); Assert.False(_squad.KnowsTarget(out _));
        }

        [UnityTest]
        public IEnumerator AReactivatedReplacedActorCannotBorrowItsReplacementsRegistrationOrPermit()
        {
            Mannequin old = Agent(0, new Vector3(-4, 0, 0), true);
            var oldCombat = old.GetComponent<MannequinCombat>(); oldCombat.enabled = false;
            Mannequin replacement = Agent(0, new Vector3(4, 0, 0), true);
            oldCombat.enabled = true; Assert.AreEqual(1, _squad.Model.Count);
            Eye(new Vector3(4, 1.65f, 12)); Physics.SyncTransforms();
            yield return new WaitForSeconds(1);
            Assert.AreEqual(0, oldCombat.ShotsFired);
            Assert.Greater(replacement.GetComponent<MannequinCombat>().ShotsFired, 0);
        }

        [UnityTest]
        public IEnumerator DisablingOnlyTheCoordinatorImmediatelyStopsItsMembersAndLeases_AndReenableNeedsFreshSight()
        {
            Mannequin body = Agent(0, Vector3.zero, true); Eye(new Vector3(0, 1.65f, 12));
            yield return new WaitForSeconds(0.3f); Assert.True(_squad.KnowsTarget(out _));
            var combat = body.GetComponent<MannequinCombat>();
            var loco = body.GetComponent<MannequinLocomotion>();
            _squad.enabled = false; int shots = combat.ShotsFired;
            Assert.False(_squad.KnowsTarget(out _)); Assert.False(_squad.Model.HasFirePermit(0, Time.time));
            Assert.False(loco.MovementProbe());
            _squad.Drive(0); Assert.False(loco.Destination.HasValue); Assert.False(loco.FacePoint.HasValue);
            yield return new WaitForSeconds(0.3f); Assert.AreEqual(shots, combat.ShotsFired);
            _squad.enabled = true; Assert.False(_squad.KnowsTarget(out _));
            yield return new WaitForSeconds(0.3f); Assert.True(_squad.KnowsTarget(out _));
        }

        [UnityTest]
        public IEnumerator SharedMemoryDoesNotFollowAPlayerMovingBehindAnOccluder_AndExpires()
        {
            Agent(0, new Vector3(-6, 0, 4), true); Agent(1, Vector3.zero, true);
            Box(new Vector3(0, 1.5f, 6), new Vector3(2, 3, 0.5f));
            Transform eye = Eye(new Vector3(0, 1.65f, 12)); Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            Assert.True(_squad.Model.SeesTarget(0, Time.time)); Assert.False(_squad.Model.SeesTarget(1, Time.time));
            Assert.True(_squad.KnowsTarget(out Vector3 seen));
            Box(new Vector3(0, 1.5f, 7), new Vector3(60, 3, 0.5f));
            eye.position = new Vector3(15, 1.65f, 12); Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            Assert.True(_squad.KnowsTarget(out Vector3 remembered)); Assert.AreEqual(seen, remembered);
            yield return new WaitForSeconds(0.6f); Assert.False(_squad.KnowsTarget(out _));
            Assert.AreEqual(TacticalRole.Hold, _squad.Model.RoleOf(0));
        }

        [UnityTest]
        public IEnumerator AnActualCapsuleStopsAtABox_AndACombatActorWithNoGoalDoesNotWander()
        {
            Mannequin body = Agent(0, new Vector3(-4, 0, 0), false, true);
            body.GetComponent<MannequinCombat>().enabled = false;
            var loco = body.GetComponent<MannequinLocomotion>();
            Box(new Vector3(0, 1.5f, 0), new Vector3(0.3f, 3, 8));
            body.transform.rotation = Quaternion.LookRotation(Vector3.right);
            loco.Destination = new Vector3(4, 0, 0); Physics.SyncTransforms();
            yield return new WaitForSeconds(4);
            Assert.Less(body.transform.position.x, -0.4f, "the blocker capsule may not cross the actual box");
            Assert.Greater(body.transform.position.x, -3f, "it approached the obstacle");
            loco.Destination = null; Vector3 stopped = body.transform.position;
            yield return new WaitForSeconds(0.5f); Assert.Less(Vector3.Distance(stopped, body.transform.position), 0.01f);
        }

        [UnityTest]
        public IEnumerator DeniedMovementProbeBudgetHoldsThePhysicalStep()
        {
            Mannequin body = Agent(0, Vector3.zero, false, true);
            body.GetComponent<MannequinCombat>().enabled = false;
            var loco = body.GetComponent<MannequinLocomotion>(); loco.MovementProbe = () => false;
            loco.Destination = new Vector3(0, 0, 5);
            yield return new WaitForSeconds(0.4f); Assert.Less(body.transform.position.magnitude, 0.01f);
            loco.MovementProbe = () => true;
            yield return new WaitForSeconds(0.4f); Assert.Greater(body.transform.position.z, 0.2f);
        }

        [UnityTest]
        public IEnumerator ATeammateInTheFiringLaneWithholdsShots_ThenClearingTheLaneAllowsFire()
        {
            Mannequin shooter = Agent(0, Vector3.zero, true);
            Mannequin teammate = Agent(1, new Vector3(0, 0, 6));
            Eye(new Vector3(0, 1.65f, 12)); Physics.SyncTransforms();
            yield return new WaitForSeconds(1);
            var combat = shooter.GetComponent<MannequinCombat>(); Assert.AreEqual(0, combat.ShotsFired);
            teammate.transform.position = new Vector3(6, 0, 6); Physics.SyncTransforms();
            yield return new WaitForSeconds(1); Assert.Greater(combat.ShotsFired, 0);
            combat.DropWeapon(Vector3.zero, 0); Assert.False(_squad.Model.HasFirePermit(0, Time.time));
        }

        [UnityTest]
        public IEnumerator GunRecoveryStillWorksWithoutAPlayerOrDuringHoldFire_OnACoarseGrid()
        {
            _squad.Initialize(_config, new TacticalGrid(-28, -28, 28, 28, 4, 0.7, null));
            Mannequin body = Agent(0, Vector3.zero, false, true); CombatWorld.EnemiesHoldFire = true;
            Weapon gun = Weapon.Create(WeaponData.Pistol, _root.transform, true);
            gun.transform.position = new Vector3(2, 0.3f, 6); Physics.SyncTransforms();
            var combat = body.GetComponent<MannequinCombat>(); float deadline = Time.time + 10;
            while (combat.Weapon == null && Time.time < deadline) yield return null;
            Assert.AreSame(gun, combat.Weapon, "the route must approach the gun itself, not stop at a grid centre");
            Assert.AreEqual(0, combat.ShotsFired);
        }
    }
}
