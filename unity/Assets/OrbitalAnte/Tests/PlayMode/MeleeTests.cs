using System.Collections;
using NUnit.Framework;
using OrbitalAnte.AI;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Melee;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The melee slot on real objects: a bat swing knocks a mannequin down and it gets up again, the knife's
    /// takedown from behind (cue and kill), the sword's single cut, and taking a second item drops the first.
    /// Written without the editor: not yet run. Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class MeleeTests
    {
        GameObject _root;
        Mannequin _mannequin;
        MannequinCombat _combat;
        Camera _eye;
        PlayerMelee _melee;
        int _hits, _severed, _kills;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            GameInput.Initialize();
            CombatWorld.Reset();
            CombatEvents.ResetStatics();
            CombatEvents.PlayerHitSomething += Count;
            _hits = _severed = _kills = 0;
            _root = new GameObject("MeleeTestRoot");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(_root.transform, false);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(50f, 1f, 50f);

            // Faces +Z, standing still.
            _mannequin = MannequinFactory.Create(Vector3.zero, Quaternion.identity, _root.transform, MannequinConfig.Load(), "MeleeTarget");
            _mannequin.GetComponent<MannequinLocomotion>().Wander = false;
            _combat = _mannequin.GetComponent<MannequinCombat>();

            var eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(_root.transform, false);
            _eye = eyeGo.AddComponent<Camera>();
            _melee = eyeGo.AddComponent<PlayerMelee>();
            _melee.Initialize(_eye, null, _root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.PlayerHitSomething -= Count;
            CombatWorld.Reset();
            GibManager.DestroyInstance();
            if (_root != null) Object.Destroy(_root);
            TimeFlow.Reset();
        }

        void Count(HitOutcome o)
        {
            if (!o.Hit) return;
            _hits++;
            if (o.Severed) _severed++;
            if (o.Killed) _kills++;
        }

        /// <summary>Puts the eye at a place, looking at a point.</summary>
        void Look(Vector3 from, Vector3 at) => _eye.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));

        MeleeItem Give(string id)
        {
            MeleeItem item = MeleeItem.Create(ContentDatabase.Get<MeleeData>(id), _root.transform);
            _melee.Take(item);
            Assert.AreEqual(item, _melee.Held);
            return item;
        }

        IEnumerator SwingAndWait(float seconds)
        {
            _melee.ForcePress = true;
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        [UnityTest]
        public IEnumerator ABatSwing_KnocksTheMannequinDown_SlidesIt_AndItGetsUp()
        {
            yield return null;
            Give(MeleeCatalog.BaseballBatId);
            Look(new Vector3(0f, 1.5f, -1.4f), new Vector3(0f, 1.1f, 0f));
            yield return null;
            Assert.IsTrue(_melee.InRange, "the in-range mark: a swing now would hit");
            Vector3 before = _mannequin.transform.position;

            yield return SwingAndWait(0.4f);
            Assert.Greater(_hits, 0, "the hit was raised for the ding, the marker and Focus");
            Assert.IsTrue(_combat.KnockedDown, "down for a moment");
            Assert.IsTrue(_mannequin.Alive, "a bat rarely kills");

            yield return new WaitForSeconds(1.4f);
            Assert.Greater((_mannequin.transform.position - before).magnitude, 0.3f, "it slid away along the swing");
            Assert.IsFalse(_combat.KnockedDown, "and got up again");
        }

        [UnityTest]
        public IEnumerator TheKnife_FromBehindAnUnawareMannequin_ShowsTakedown_AndKills()
        {
            yield return null;
            Give(MeleeCatalog.ButterflyKnifeId);
            BodyPart head = _mannequin.Part(BodyPartId.Head);
            Look(new Vector3(0f, head.transform.position.y + 0.1f, -0.9f), head.transform.position);
            yield return null;
            Assert.IsFalse(_combat.AwareOfPlayer);
            Assert.IsTrue(_melee.TakedownReady, "the TAKEDOWN cue");

            yield return SwingAndWait(0.3f);
            Assert.IsFalse(_mannequin.Alive, "the stab the cue promised");
            Assert.AreEqual(1, _kills);
        }

        [UnityTest]
        public IEnumerator TheKnife_FromTheFront_ShowsNoTakedown()
        {
            yield return null;
            Give(MeleeCatalog.ButterflyKnifeId);
            BodyPart head = _mannequin.Part(BodyPartId.Head);
            Look(new Vector3(0f, head.transform.position.y + 0.1f, 0.9f), head.transform.position);
            yield return null;
            Assert.IsFalse(_melee.TakedownReady);
            yield return SwingAndWait(0.3f);
            Assert.IsTrue(_mannequin.Alive);
        }

        [UnityTest]
        public IEnumerator TheLaserSword_CutsOffExactlyOnePart()
        {
            yield return null;
            Give(MeleeCatalog.LaserSwordId);
            BodyPart thigh = _mannequin.Part(BodyPartId.LeftThigh);
            Vector3 aim = thigh.Hitbox != null ? thigh.Hitbox.bounds.center : thigh.transform.position;
            Look(new Vector3(aim.x, 1.4f, -1.2f), aim);
            yield return null;
            int onBody = _mannequin.Model.PartsOnBody;
            yield return SwingAndWait(0.3f);
            Assert.AreEqual(1, _severed, "one cut a swing");
            Assert.Less(_mannequin.Model.PartsOnBody, onBody);
            Assert.IsTrue(_mannequin.Alive);
        }

        [UnityTest]
        public IEnumerator TakingASecondItem_DropsTheFirst_AndDyingLosesIt()
        {
            yield return null;
            MeleeItem pan = Give(MeleeCatalog.FryingPanId);
            MeleeItem box = Give(MeleeCatalog.MailboxId);
            Assert.IsFalse(pan.Held, "the pan is on the ground again");
            Assert.AreEqual(0.85f, _melee.WalkSlow, 1e-4, "the mailbox slows your walk");
            _melee.LoseHeld();
            yield return null;
            Assert.IsNull(_melee.Held);
            Assert.IsTrue(box == null, "lost, not dropped");
        }
    }
}
