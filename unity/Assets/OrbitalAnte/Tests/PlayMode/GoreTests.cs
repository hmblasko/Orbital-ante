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
    /// Checks the Milestone 2 systems on real objects: the gib pool and cap, lifetime and fade, bouncy physics,
    /// blood, splats, hitstop and slow-mo. These check that the systems do what they say; whether it feels good
    /// can only be judged by playing.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class GoreTests
    {
        const float Big = 100000f;

        GameObject _root;
        GoreConfig _cfg;
        bool _hitstopWas, _slowMoWas;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            _root = new GameObject("GoreTestRoot");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(_root.transform, false);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(50f, 1f, 50f);

            _cfg = ScriptableObject.CreateInstance<GoreConfig>();
            _cfg.decalMode = DecalMode.MeshOnly;
            _cfg.poolPrewarm = 4;

            _hitstopWas = FeelSettings.Hitstop;
            _slowMoWas = FeelSettings.HeadshotSlowMo;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            GibManager.DestroyInstance();
            if (GoreFx.Instance != null) Object.Destroy(GoreFx.Instance.gameObject);
            if (_root != null) Object.Destroy(_root);
            if (_cfg != null) Object.Destroy(_cfg);
            FeelSettings.Hitstop = _hitstopWas;
            FeelSettings.HeadshotSlowMo = _slowMoWas;
            Time.timeScale = 1f;
        }

        Mannequin NewMannequin(Vector3 position)
        {
            Mannequin m = MannequinFactory.Create(position, Quaternion.identity, _root.transform, MannequinConfig.Load(), "TestMannequin");
            m.GetComponent<MannequinLocomotion>().Wander = false;
            return m;
        }

        static int SolidCount(Mannequin m, params BodyPartId[] roots)
        {
            int solid = 0;
            foreach (BodyPartId id in roots)
            {
                BodyPart part = m.Part(id);
                Gib gib = part != null ? part.GetComponentInParent<Gib>() : null;
                if (gib != null && !gib.Fading) solid++;
            }
            return solid;
        }

        [UnityTest]
        public IEnumerator GibCap_PushesOutTheOldestChunk()
        {
            _cfg.maxActiveGibs = 3;
            _cfg.overflowFadeTime = 0.1f;
            GibManager manager = GibManager.Create(_root.transform, _cfg);
            Mannequin m = NewMannequin(Vector3.zero);
            yield return null;

            m.DamagePart(BodyPartId.LeftThigh, Big, Vector3.forward);
            Gib oldest = m.Part(BodyPartId.LeftThigh).GetComponentInParent<Gib>();
            m.DamagePart(BodyPartId.RightThigh, Big, Vector3.forward);
            m.DamagePart(BodyPartId.LeftUpperArm, Big, Vector3.forward);
            Assert.AreEqual(3, manager.ActiveCount);
            Assert.IsFalse(oldest.Fading, "nothing is pushed out while under the cap");

            m.DamagePart(BodyPartId.RightUpperArm, Big, Vector3.forward);
            Assert.IsTrue(oldest.Fading, "the oldest chunk starts fading when the cap is reached");
            Assert.AreEqual(3, SolidCount(m, BodyPartId.LeftThigh, BodyPartId.RightThigh, BodyPartId.LeftUpperArm, BodyPartId.RightUpperArm));

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(3, manager.ActiveCount, "the pushed-out chunk is gone");
            Assert.AreEqual(3, m.ActiveGibCount);
        }

        [UnityTest]
        public IEnumerator Gibs_FadeAfterTheirLifetime_AndTheContainerIsReused()
        {
            _cfg.gibLifetime = 0.3f;
            _cfg.gibFadeTime = 0.2f;
            GibManager manager = GibManager.Create(_root.transform, _cfg);
            Mannequin m = NewMannequin(Vector3.zero);
            yield return null;
            int createdAtStart = manager.CreatedCount;
            Assert.AreEqual(4, createdAtStart, "the pool is created up front");

            m.DamagePart(BodyPartId.LeftForearm, Big, Vector3.forward);
            Gib gib = m.Part(BodyPartId.LeftForearm).GetComponentInParent<Gib>();
            Assert.IsNotNull(gib);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(gib.Fading, "fading after its lifetime");
            Assert.Less(gib.transform.localScale.x, 1f, "shrinking while it fades");

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0, manager.ActiveCount);
            Assert.AreEqual(0, m.ActiveGibCount);
            Assert.IsFalse(gib.gameObject.activeSelf, "the container goes back to the pool");
            Assert.IsTrue(m.Part(BodyPartId.LeftForearm) == null, "the parts themselves are removed");

            m.DamagePart(BodyPartId.RightForearm, Big, Vector3.forward);
            m.DamagePart(BodyPartId.LeftThigh, Big, Vector3.forward);
            Assert.AreEqual(createdAtStart, manager.CreatedCount, "no new containers were needed");
            Assert.AreEqual(Vector3.one, m.Part(BodyPartId.RightForearm).GetComponentInParent<Gib>().transform.localScale);
        }

        [UnityTest]
        public IEnumerator LooseParts_AreBouncy_AndActuallyBounce()
        {
            GibManager manager = GibManager.Create(_root.transform, _cfg);
            Mannequin m = NewMannequin(Vector3.zero);
            yield return null;

            m.DamagePart(BodyPartId.LeftHand, Big, Vector3.forward);
            BodyPart hand = m.Part(BodyPartId.LeftHand);
            Assert.AreSame(manager.BouncyMaterial, hand.Hitbox.sharedMaterial);
            Assert.Greater(manager.BouncyMaterial.bounciness, 0.3f);

            // Drop it straight down and look for upward speed after it meets the floor.
            Gib gib = hand.GetComponentInParent<Gib>();
            gib.transform.position = new Vector3(5f, 2f, 5f);
            gib.Body.linearVelocity = Vector3.down * 6f;
            gib.Body.angularVelocity = Vector3.zero;
            float bestUp = 0f;
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForFixedUpdate();
                bestUp = Mathf.Max(bestUp, gib.Body.linearVelocity.y);
            }
            Assert.Greater(bestUp, 1f, "the hand bounced back up off the floor");
        }

        [UnityTest]
        public IEnumerator PoppingALimb_MakesBlood_AndBothWoundsSpray()
        {
            GibManager.Create(_root.transform, _cfg);
            GoreFx fx = GoreFx.Create(_root.transform, _cfg);
            Mannequin m = NewMannequin(Vector3.zero);
            yield return null;

            m.DamagePart(BodyPartId.LeftThigh, Big, Vector3.forward);
            Assert.AreEqual(2, fx.ActiveBleeders, "the loose leg and the stump both spray");
            yield return null;
            yield return null;
            Assert.Greater(fx.LiveDroplets, 0, "blood burst");
            Assert.LessOrEqual(fx.LiveDroplets, _cfg.maxBloodParticles);

            yield return new WaitForSeconds(_cfg.trailDuration * 1.3f + 0.3f);
            Assert.AreEqual(0, fx.ActiveBleeders, "wounds stop spraying");
        }

        [UnityTest]
        public IEnumerator Splats_AreCapped_AndClear()
        {
            _cfg.maxDecals = 4;
            GoreFx fx = GoreFx.Create(_root.transform, _cfg);
            yield return null;
            Assert.IsFalse(fx.Decals.UsingProjectors, "mesh splats were asked for");

            for (int i = 0; i < 10; i++) fx.GibLanded(new Vector3(i, 0f, 0f), Vector3.up, 8f, 5f);
            Assert.AreEqual(4, fx.Decals.LiveCount, "old splats are reused instead of piling up");

            fx.Clear();
            Assert.AreEqual(0, fx.Decals.LiveCount);
        }

        [UnityTest]
        public IEnumerator AGibHittingTheFloor_LeavesASplat()
        {
            GibManager.Create(_root.transform, _cfg);
            GoreFx fx = GoreFx.Create(_root.transform, _cfg);
            Mannequin m = NewMannequin(Vector3.zero);
            yield return null;

            m.DamagePart(BodyPartId.RightThigh, Big, Vector3.down);
            Gib gib = m.Part(BodyPartId.RightThigh).GetComponentInParent<Gib>();
            gib.Body.linearVelocity = Vector3.down * 8f;
            for (int i = 0; i < 100 && fx.Decals.LiveCount == 0; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(fx.Decals.LiveCount, 0);
        }

        [UnityTest]
        public IEnumerator Outcome_ReportsHowBigTheMomentWas()
        {
            GibManager.Create(_root.transform, _cfg);
            Mannequin a = NewMannequin(Vector3.zero);
            Mannequin b = NewMannequin(new Vector3(4f, 0f, 0f));
            yield return null;

            HitOutcome graze = a.DamagePart(BodyPartId.Torso, 5f, Vector3.forward);
            Assert.AreEqual(0f, graze.Juice, "no hitstop or shake for an ordinary hit");

            HitOutcome hand = a.DamagePart(BodyPartId.LeftHand, Big, Vector3.forward);
            HitOutcome thigh = a.DamagePart(BodyPartId.RightThigh, Big, Vector3.forward);
            Assert.Greater(hand.Juice, 0f);
            Assert.Greater(thigh.Juice, hand.Juice, "a whole leg is a bigger moment than a hand");
            Assert.IsFalse(thigh.HeadshotKill);

            HitOutcome headshot = b.DamagePart(BodyPartId.Head, Big, Vector3.forward);
            Assert.IsTrue(headshot.HeadshotKill);
            Assert.AreEqual(1f, headshot.Juice);

            // Finishing off a lone head is a kill, but not a headshot on a standing body.
            a.DamagePart(BodyPartId.Torso, Big, Vector3.forward);
            HitOutcome lastHead = a.DamagePart(BodyPartId.Head, Big, Vector3.forward);
            Assert.IsTrue(lastHead.Killed);
            Assert.IsFalse(lastHead.HeadshotKill);
        }

        [UnityTest]
        public IEnumerator Hitstop_FreezesBriefly_ThenTimeComesBack()
        {
            FeelSettings.Hitstop = true;
            FeelSettings.HeadshotSlowMo = true;
            var fx = _root.AddComponent<TimeFx>();
            yield return null;

            CombatEvents.RaisePlayerHit(new HitOutcome { Hit = true, Severed = true, Juice = 0.7f });
            Assert.IsTrue(fx.InHitstop);
            yield return null;
            Assert.Less(Time.timeScale, 0.2f, "near-frozen during hitstop");

            yield return new WaitForSecondsRealtime(0.25f);
            Assert.IsFalse(fx.InHitstop);
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "back to normal speed");

            // A headshot kill adds a short slow-mo after the freeze.
            CombatEvents.RaisePlayerHit(new HitOutcome { Hit = true, Killed = true, HeadshotKill = true, Juice = 1f });
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(fx.InSlowMo);
            Assert.Less(Time.timeScale, 0.9f);
            Assert.Greater(Time.timeScale, 0.1f);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [UnityTest]
        public IEnumerator Hitstop_CanBeSwitchedOff()
        {
            FeelSettings.Hitstop = false;
            FeelSettings.HeadshotSlowMo = false;
            var fx = _root.AddComponent<TimeFx>();
            yield return null;

            CombatEvents.RaisePlayerHit(new HitOutcome { Hit = true, Killed = true, HeadshotKill = true, Juice = 1f });
            yield return null;
            Assert.IsFalse(fx.InHitstop);
            Assert.IsFalse(fx.InSlowMo);
            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }
    }
}
