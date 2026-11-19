using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Gore;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks the jointed body on real objects: a dead body and a shot-off limb get joints, the joints hold, the
    /// body folds instead of staying a statue, a jointed chunk can still be shot apart, and the cost is capped.
    /// These are the things about Unity's joints that could not be checked in the cloud. Whether it looks right
    /// can only be judged by playing.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class RagdollTests
    {
        const float Big = 100000f;

        GameObject _root;
        GoreConfig _gore;
        MannequinConfig _config;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            _root = new GameObject("RagdollTestRoot");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(_root.transform, false);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(50f, 1f, 50f);
            _gore = ScriptableObject.CreateInstance<GoreConfig>();
            _gore.decalMode = DecalMode.MeshOnly;
            _gore.poolPrewarm = 4;
            _config = Object.Instantiate(MannequinConfig.Load());
            _config.ragdoll.jointed = true;
            TimeFlow.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GibManager.DestroyInstance();
            GibRagdoll.ResetStatics();
            if (GoreFx.Instance != null) Object.Destroy(GoreFx.Instance.gameObject);
            if (_root != null) Object.Destroy(_root);
            if (_gore != null) Object.Destroy(_gore);
            if (_config != null) Object.Destroy(_config);
            TimeFlow.Reset();
        }

        Mannequin NewMannequin(Vector3 position)
        {
            Mannequin m = MannequinFactory.Create(position, Quaternion.identity, _root.transform, _config, "RagdollMannequin");
            m.GetComponent<MannequinLocomotion>().Wander = false;
            return m;
        }

        static Gib ChunkOf(Mannequin m, BodyPartId id) => m.Part(id).GetComponentInParent<Gib>();

        /// <summary>How far a part's joint has come away from where it is fixed on the part above, in metres.</summary>
        static float Gap(BodyPart part)
        {
            ConfigurableJoint joint = part.Joint;
            Vector3 mine = part.transform.TransformPoint(joint.anchor);
            Vector3 theirs = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
            return Vector3.Distance(mine, theirs);
        }

        [UnityTest]
        public IEnumerator ADeadBody_IsJointed_HoldsTogether_AndFolds()
        {
            GibManager.Create(_root.transform, _gore);
            Mannequin m = NewMannequin(Vector3.zero);
            yield return null;

            m.DamagePart(BodyPartId.Head, Big, Vector3.forward);
            Gib corpse = ChunkOf(m, BodyPartId.Torso);
            Assert.IsNotNull(corpse, "the body is a chunk once the head is gone");
            Assert.IsNotNull(corpse.Ragdoll);
            Assert.AreEqual(12, corpse.Ragdoll.JointCount, "every part below the torso hangs from the part above");
            Assert.IsNull(m.Part(BodyPartId.Torso).OwnBody, "the top part rides on the chunk itself");

            var parts = new[] { BodyPartId.LeftUpperArm, BodyPartId.LeftForearm, BodyPartId.LeftHand, BodyPartId.RightThigh, BodyPartId.RightShin, BodyPartId.RightFoot };
            var before = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                BodyPart part = m.Part(parts[i]);
                Assert.IsNotNull(part.OwnBody, parts[i] + " has no weight of its own");
                Assert.IsNotNull(part.Joint, parts[i] + " has no joint");
                Assert.IsNotNull(part.Joint.connectedBody, parts[i] + " hangs from nothing");
                Assert.Less(Gap(part), 0.02f, parts[i] + " was made away from its joint");
                before[i] = Quaternion.Inverse(m.Part(BodyPartId.Torso).transform.rotation) * part.transform.rotation;
            }

            float worstGap = 0f;
            for (int frame = 0; frame < 100; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (corpse.Ragdoll == null || !corpse.Ragdoll.Jointed) break;
                for (int i = 0; i < parts.Length; i++) worstGap = Mathf.Max(worstGap, Gap(m.Part(parts[i])));
            }
            Assert.Less(worstGap, 0.12f, "a joint came apart while the body fell");

            float turned = 0f;
            for (int i = 0; i < parts.Length; i++)
            {
                Quaternion now = Quaternion.Inverse(m.Part(BodyPartId.Torso).transform.rotation) * m.Part(parts[i]).transform.rotation;
                turned = Mathf.Max(turned, Quaternion.Angle(before[i], now));
            }
            Assert.Greater(turned, 8f, "the body fell as one stiff piece: no joint bent");
            for (int i = 0; i < parts.Length; i++)
            {
                Vector3 p = m.Part(parts[i]).transform.position;
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), parts[i] + " flew off to nowhere");
                Assert.Less(p.magnitude, 30f, parts[i] + " was thrown far too hard");
                Assert.Greater(p.y, -0.5f, parts[i] + " fell through the floor");
            }
        }

        [UnityTest]
        public IEnumerator AJointBendsOnlyWithinItsRange()
        {
            // The knee must never fold forwards, however the body lands: this is what the relaxed-pose joint
            // set-up has to get right in Unity.
            GibManager.Create(_root.transform, _gore);
            Mannequin m = NewMannequin(new Vector3(0f, 1.5f, 0f));
            yield return null;
            m.DamagePart(BodyPartId.Head, Big, new Vector3(0.3f, 0f, 1f).normalized);
            Gib corpse = ChunkOf(m, BodyPartId.Torso);
            float worst = 0f;
            BodyPartId worstPart = BodyPartId.Torso;
            for (int frame = 0; frame < 150; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (corpse.Ragdoll == null || !corpse.Ragdoll.Jointed) break;
                foreach (BodyPartId id in new[] { BodyPartId.LeftShin, BodyPartId.RightShin, BodyPartId.LeftForearm, BodyPartId.RightForearm })
                {
                    BodyPart part = m.Part(id), above = m.Part(BodyLayout.Parent(id));
                    if (part.Joint == null) continue;
                    Quaternion q = Quaternion.Inverse(above.transform.rotation) * part.transform.rotation;
                    double beyond = RagdollRules.Beyond(id, new QuatD(q.x, q.y, q.z, q.w));
                    if (beyond > worst) { worst = (float)beyond; worstPart = id; }
                }
            }
            Assert.Less(worst, 40f, worstPart + " bent far past its stop (a knee or elbow folding the wrong way)");
        }

        [UnityTest]
        public IEnumerator AShotOffArm_IsJointed_AndCanStillBeShotApart()
        {
            GibManager manager = GibManager.Create(_root.transform, _gore);
            Mannequin m = NewMannequin(Vector3.zero);
            yield return null;

            m.DamagePart(BodyPartId.LeftUpperArm, Big, Vector3.forward);
            Gib arm = ChunkOf(m, BodyPartId.LeftUpperArm);
            Assert.AreEqual(2, arm.Ragdoll.JointCount, "the forearm and the hand");
            yield return new WaitForFixedUpdate();

            m.DamagePart(BodyPartId.LeftForearm, Big, Vector3.forward);
            Gib fore = ChunkOf(m, BodyPartId.LeftForearm);
            Assert.AreNotSame(arm, fore, "the forearm left the arm");
            Assert.AreEqual(2, manager.ActiveCount);
            Assert.IsNull(m.Part(BodyPartId.LeftForearm).OwnBody, "the forearm is the top of its own chunk now");
            Assert.IsNotNull(m.Part(BodyPartId.LeftHand).Joint, "the hand still hangs from the forearm");
            Assert.AreSame(fore.Body, m.Part(BodyPartId.LeftHand).Joint.connectedBody);
            Assert.IsFalse(arm.Ragdoll != null && arm.Ragdoll.Jointed, "an upper arm by itself has nothing to joint");
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Gap(m.Part(BodyPartId.LeftHand)), 0.12f);
            Assert.AreSame(fore.Body, m.Part(BodyPartId.LeftForearm).Hitbox.attachedRigidbody, "the forearm's collider did not go over to its chunk");
        }

        [UnityTest]
        public IEnumerator OnlyOneChunkAFrame_IsGivenJoints()
        {
            GibManager.Create(_root.transform, _gore);
            Mannequin m = NewMannequin(Vector3.zero);
            yield return null;
            m.DamagePart(BodyPartId.LeftUpperArm, Big, Vector3.forward);
            m.DamagePart(BodyPartId.RightUpperArm, Big, Vector3.forward);
            Assert.AreEqual(1, GibRagdoll.LiveCount, "the second chunk of the frame stays one stiff piece");
            Assert.IsNotNull(m.Part(BodyPartId.LeftForearm).OwnBody);
            Assert.IsNull(m.Part(BodyPartId.RightForearm).OwnBody);
        }

        [UnityTest]
        public IEnumerator AJointedChunk_GoesStiffAgain_AndOnlyAFewAreJointedAtOnce()
        {
            _config.ragdoll.jointedSeconds = 0.4f;
            _config.ragdoll.mostJointed = 2;
            GibManager.Create(_root.transform, _gore);
            Mannequin m = NewMannequin(Vector3.zero);
            yield return null;

            // One chunk a frame is given joints, so the cuts are a frame apart.
            m.DamagePart(BodyPartId.LeftUpperArm, Big, Vector3.forward);
            yield return null;
            m.DamagePart(BodyPartId.RightUpperArm, Big, Vector3.forward);
            yield return null;
            m.DamagePart(BodyPartId.LeftThigh, Big, Vector3.forward);
            Assert.LessOrEqual(GibRagdoll.LiveCount, 2, "more chunks are jointed than the budget allows");
            Assert.IsNull(m.Part(BodyPartId.LeftForearm).OwnBody, "the oldest gave its joints up to make room");

            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0, GibRagdoll.LiveCount, "every chunk goes stiff once its time is up");
            Assert.IsNull(m.Part(BodyPartId.LeftShin).OwnBody);
            Assert.IsNull(m.Part(BodyPartId.LeftShin).Joint);
            Assert.IsNotNull(ChunkOf(m, BodyPartId.LeftShin), "the stiff chunk is still there");
        }
    }
}
