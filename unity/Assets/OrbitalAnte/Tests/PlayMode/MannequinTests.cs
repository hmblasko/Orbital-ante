using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks that the real mannequin object behaves the way the rules say.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class MannequinTests
    {
        const float Big = 100000f;

        GameObject _root;
        Mannequin _mannequin;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            _root = new GameObject("TestRoot");

            // A floor, so loose parts have something to land on.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(_root.transform, false);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(50f, 1f, 50f);

            _mannequin = MannequinFactory.Create(Vector3.zero, Quaternion.identity, _root.transform, MannequinConfig.Load(), "TestMannequin");
            _mannequin.GetComponent<MannequinLocomotion>().Wander = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        [UnityTest]
        public IEnumerator HasFourteenParts_EachWithItsOwnHitboxAndHp()
        {
            yield return null;
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                var id = (BodyPartId)i;
                BodyPart part = _mannequin.Part(id);
                Assert.IsNotNull(part, id.ToString());
                Assert.IsNotNull(part.Hitbox, id + " hitbox");
                Assert.AreSame(part.gameObject, part.Hitbox.gameObject, id + " hitbox sits on the part");
                Assert.AreEqual(Layers.Hitbox, part.gameObject.layer, id + " layer");
                Assert.Greater(_mannequin.Model.MaxHp(id), 0f, id + " hp");
                Assert.IsNotNull(part.GetComponentInChildren<MeshRenderer>(), id + " mesh");
                if (BodyLayout.HasParent(id))
                {
                    Assert.IsNotNull(part.ProximalCap, id + " wound cap");
                    Assert.IsFalse(part.ProximalCap.activeSelf, id + " wound cap hidden while attached");
                    GameObject parentCap = _mannequin.Part(BodyLayout.Parent(id)).ChildCap(id);
                    Assert.IsNotNull(parentCap, id + " stump cap on parent");
                    Assert.IsFalse(parentCap.activeSelf, id + " stump cap hidden while attached");
                }
            }
            // Exactly one hitbox per part: the primitives' own colliders must be gone.
            int hitboxes = 0;
            foreach (Collider c in _mannequin.GetComponentsInChildren<Collider>())
            {
                if (c.gameObject.layer == Layers.Hitbox) hitboxes++;
            }
            Assert.AreEqual(BodyLayout.Count, hitboxes);
        }

        [UnityTest]
        public IEnumerator HasAValidHumanoidAvatar()
        {
            yield return null;
            Animator animator = _mannequin.Rig.Animator;
            Assert.IsNotNull(animator);
            Assert.IsNotNull(animator.avatar, "avatar was built");
            Assert.IsTrue(animator.avatar.isValid, "avatar is valid");
            Assert.IsTrue(animator.avatar.isHuman, "avatar is Humanoid");
        }

        [UnityTest]
        public IEnumerator EachPartHangsOffItsMatchingBone()
        {
            yield return null;
            MannequinRig rig = _mannequin.Rig;
            Assert.AreSame(rig.Spine, _mannequin.Part(BodyPartId.Torso).transform.parent);
            Assert.AreSame(rig.Head, _mannequin.Part(BodyPartId.Head).transform.parent);
            Assert.AreSame(rig.UpperArm[MannequinRig.Left], _mannequin.Part(BodyPartId.LeftUpperArm).transform.parent);
            Assert.AreSame(rig.Forearm[MannequinRig.Right], _mannequin.Part(BodyPartId.RightForearm).transform.parent);
            Assert.AreSame(rig.Hand[MannequinRig.Left], _mannequin.Part(BodyPartId.LeftHand).transform.parent);
            Assert.AreSame(rig.Thigh[MannequinRig.Right], _mannequin.Part(BodyPartId.RightThigh).transform.parent);
            Assert.AreSame(rig.Shin[MannequinRig.Left], _mannequin.Part(BodyPartId.LeftShin).transform.parent);
            Assert.AreSame(rig.Foot[MannequinRig.Right], _mannequin.Part(BodyPartId.RightFoot).transform.parent);
        }

        [UnityTest]
        public IEnumerator LimbsComeOffInOneToThreeRifleHits()
        {
            yield return null;
            MannequinConfig config = MannequinConfig.Load();
            float rifle = PlayerConfig.Load().testRifleDamage;
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                var id = (BodyPartId)i;
                if (id == BodyPartId.Head || id == BodyPartId.Torso) continue;
                int hits = Mathf.CeilToInt(config.HpFor(id) / rifle);
                Assert.That(hits, Is.InRange(1, 3), id + " takes " + hits + " hits");
            }
        }

        [UnityTest]
        public IEnumerator ShootingOffAThigh_MakesOneGibOfThreeParts_WithCaps()
        {
            yield return null;
            HitOutcome outcome = _mannequin.DamagePart(BodyPartId.LeftThigh, Big, Vector3.forward);
            Assert.IsTrue(outcome.Severed);
            Assert.IsFalse(outcome.Killed);

            BodyPart thigh = _mannequin.Part(BodyPartId.LeftThigh);
            BodyPart shin = _mannequin.Part(BodyPartId.LeftShin);
            BodyPart foot = _mannequin.Part(BodyPartId.LeftFoot);

            Gib gib = thigh.GetComponentInParent<Gib>();
            Assert.IsNotNull(gib, "thigh is in a gib");
            Assert.AreSame(gib, shin.GetComponentInParent<Gib>(), "shin went with it");
            Assert.AreSame(gib, foot.GetComponentInParent<Gib>(), "foot went with it");
            Assert.AreEqual(3, gib.Parts.Count);
            Assert.IsNull(thigh.GetComponentInParent<Mannequin>(), "no longer under the mannequin");

            Rigidbody body = gib.GetComponent<Rigidbody>();
            Assert.IsNotNull(body);
            Assert.IsFalse(body.isKinematic);
            Assert.Greater(body.linearVelocity.magnitude, 3f, "launched hard");
            Assert.Greater(Vector3.Dot(body.linearVelocity, Vector3.forward), 0f, "launched along the bullet");
            Assert.Greater(body.angularVelocity.magnitude, 1f, "spinning");
            Assert.AreEqual(Layers.Gib, thigh.gameObject.layer);

            Assert.IsTrue(thigh.ProximalCap.activeSelf, "wound cap on the thigh");
            Assert.IsTrue(_mannequin.Part(BodyPartId.Torso).ChildCap(BodyPartId.LeftThigh).activeSelf, "stump cap on the torso");
            Assert.IsFalse(shin.ProximalCap.activeSelf, "knee is intact");

            Assert.AreEqual(MovementState.OneLeg, _mannequin.State);
            Assert.AreEqual(1, _mannequin.ActiveGibCount);

            // Let physics run for a moment to prove nothing explodes or throws.
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.IsFalse(float.IsNaN(gib.transform.position.x));
        }

        [UnityTest]
        public IEnumerator AGib_CanBeSplitAgain()
        {
            yield return null;
            _mannequin.DamagePart(BodyPartId.RightUpperArm, Big, Vector3.right);
            Gib first = _mannequin.Part(BodyPartId.RightUpperArm).GetComponentInParent<Gib>();
            Assert.AreEqual(3, first.Parts.Count);

            HitOutcome outcome = _mannequin.DamagePart(BodyPartId.RightForearm, Big, Vector3.up);
            Assert.IsTrue(outcome.Severed);

            Gib second = _mannequin.Part(BodyPartId.RightForearm).GetComponentInParent<Gib>();
            Assert.AreNotSame(first, second);
            Assert.AreEqual(1, first.Parts.Count, "upper arm left on its own");
            Assert.AreEqual(2, second.Parts.Count, "forearm and hand together");
            Assert.AreSame(second, _mannequin.Part(BodyPartId.RightHand).GetComponentInParent<Gib>());
            Assert.IsTrue(_mannequin.Part(BodyPartId.RightUpperArm).ChildCap(BodyPartId.RightForearm).activeSelf);
            Assert.AreEqual(2, _mannequin.ActiveGibCount);
        }

        [UnityTest]
        public IEnumerator MovementStatesDegrade_AndSlowDown()
        {
            yield return null;
            var locomotion = _mannequin.GetComponent<MannequinLocomotion>();
            Assert.AreEqual(MovementState.Intact, _mannequin.State);
            float walk = locomotion.SpeedFor(MovementState.Intact, false);
            float sprint = locomotion.SpeedFor(MovementState.Intact, true);
            Assert.Greater(sprint, walk);

            _mannequin.DamagePart(BodyPartId.LeftShin, Big, Vector3.forward);
            Assert.AreEqual(MovementState.OneLeg, _mannequin.State);
            Assert.IsFalse(_mannequin.Model.CanSprint);
            Assert.Less(locomotion.SpeedFor(MovementState.OneLeg, true), walk, "hopping is slower than walking, sprint or not");
            yield return null;

            _mannequin.DamagePart(BodyPartId.RightThigh, Big, Vector3.forward);
            Assert.AreEqual(MovementState.Crawl, _mannequin.State);
            Assert.Less(locomotion.SpeedFor(MovementState.Crawl, false), locomotion.SpeedFor(MovementState.OneLeg, false));
            yield return null;

            _mannequin.DamagePart(BodyPartId.LeftUpperArm, Big, Vector3.forward);
            _mannequin.DamagePart(BodyPartId.RightUpperArm, Big, Vector3.forward);
            Assert.AreEqual(MovementState.Wriggle, _mannequin.State);
            Assert.Less(locomotion.SpeedFor(MovementState.Wriggle, false), locomotion.SpeedFor(MovementState.Crawl, false));
            yield return null;

            _mannequin.DamagePart(BodyPartId.Torso, Big, Vector3.forward);
            Assert.AreEqual(MovementState.HeadOnly, _mannequin.State);
            Assert.IsTrue(_mannequin.Alive, "just a head, still alive");
            Assert.IsNotNull(_mannequin.Part(BodyPartId.Head).GetComponentInParent<Mannequin>(), "head still belongs to the character");
            yield return null;
            yield return null;

            HitOutcome end = _mannequin.DamagePart(BodyPartId.Head, Big, Vector3.forward);
            Assert.IsTrue(end.Killed);
            Assert.IsFalse(_mannequin.Alive);
        }

        [UnityTest]
        public IEnumerator EachMovementStateActuallyMoves_WithoutErrors()
        {
            yield return null;
            var locomotion = _mannequin.GetComponent<MannequinLocomotion>();
            locomotion.SetWander(new Bounds(new Vector3(0f, 0f, 12f), new Vector3(2f, 0.1f, 2f)), 7);
            locomotion.Wander = true;

            BodyPartId[] sequence =
            {
                BodyPartId.LeftThigh,     // one leg
                BodyPartId.RightThigh,    // crawl
                BodyPartId.LeftUpperArm,
                BodyPartId.RightUpperArm, // wriggle
                BodyPartId.Torso,         // head only
            };

            Vector3 last = _mannequin.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.Greater(Vector3.Distance(last, _mannequin.transform.position), 0.2f, "walks when intact");

            foreach (BodyPartId id in sequence)
            {
                _mannequin.DamagePart(id, Big, Vector3.back);
                last = _mannequin.transform.position;
                yield return new WaitForSeconds(1.5f);
                float moved = Vector3.Distance(last, _mannequin.transform.position);
                Assert.Greater(moved, 0.05f, _mannequin.State + " should still make progress");
                Assert.IsFalse(float.IsNaN(_mannequin.transform.position.x));
            }
        }

        [UnityTest]
        public IEnumerator Headshot_Kills_AndLeavesHeadPlusCorpse()
        {
            yield return null;
            bool died = false;
            _mannequin.Died += m => died = true;

            HitOutcome outcome = _mannequin.DamagePart(BodyPartId.Head, Big, Vector3.forward);
            Assert.IsTrue(outcome.Killed);
            Assert.IsTrue(died);
            Assert.AreEqual(MovementState.Dead, _mannequin.State);
            Assert.AreEqual(2, _mannequin.ActiveGibCount);

            Gib corpse = _mannequin.Part(BodyPartId.Torso).GetComponentInParent<Gib>();
            Assert.AreEqual(13, corpse.Parts.Count);
            Gib head = _mannequin.Part(BodyPartId.Head).GetComponentInParent<Gib>();
            Assert.AreEqual(1, head.Parts.Count);

            // The corpse can still lose a limb.
            Assert.IsTrue(_mannequin.DamagePart(BodyPartId.LeftThigh, Big, Vector3.forward).Severed);
            Assert.AreEqual(10, corpse.Parts.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NonFatalHit_OnlyReducesHp()
        {
            yield return null;
            float before = _mannequin.Model.Hp(BodyPartId.Torso);
            HitOutcome outcome = _mannequin.DamagePart(BodyPartId.Torso, 10f, Vector3.forward);
            Assert.IsTrue(outcome.Hit);
            Assert.IsFalse(outcome.Severed);
            Assert.AreEqual(before - 10f, _mannequin.Model.Hp(BodyPartId.Torso), 0.001f);
            Assert.AreEqual(0, _mannequin.ActiveGibCount);
        }
    }
}
