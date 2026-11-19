using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Combat;
using OrbitalAnte.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Deep Breath on the real clock: holding it slows game time and the physics step together, letting go
    /// puts both back exactly, pause and hitstop still win, and only enemies give breath back. Whether it
    /// feels good can only be judged by playing.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class DeepBreathTests
    {
        GameObject _root;
        DeepBreathFx _fx;
        float _stepWas;
        bool _hitstopWas, _slowMoWas;

        [SetUp]
        public void SetUp()
        {
            TimeFlow.Reset();
            _stepWas = Time.fixedDeltaTime;
            _hitstopWas = FeelSettings.Hitstop;
            _slowMoWas = FeelSettings.HeadshotSlowMo;
            _root = new GameObject("DeepBreathTestRoot");
            _fx = _root.AddComponent<DeepBreathFx>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            if (GameSession.Paused) GameSession.SetPaused(false);
            FeelSettings.Hitstop = _hitstopWas;
            FeelSettings.HeadshotSlowMo = _slowMoWas;
            TimeFlow.Reset();
            Time.fixedDeltaTime = _stepWas;
            OrbitalAnte.Core.TimeFlow.Reset();
        }

        [UnityTest]
        public IEnumerator Holding_SlowsTimeAndThePhysicsStepTogether_LettingGoPutsBothBackExactly()
        {
            float slowTo = _fx.Breath.Rules.slowTo;
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, 0.001f);
            Assert.AreEqual(0f, DeepBreathFx.Amount);
            Assert.IsFalse(_fx.MeterShown, "a full, unused meter stays off the screen");

            _fx.ForceHold = true;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(slowTo, Time.timeScale, 0.001f);
            Assert.AreEqual(_stepWas * slowTo, Time.fixedDeltaTime, _stepWas * 0.01f, "the step shrinks with time");
            Assert.AreEqual(1f, DeepBreathFx.Amount, 0.001f);
            Assert.Less(_fx.Breath.Meter, 1f);
            Assert.IsTrue(_fx.MeterShown);

            _fx.ForceHold = false;
            yield return new WaitForSecondsRealtime(_fx.Breath.Rules.easeOut + 0.3f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(_stepWas, Time.fixedDeltaTime, _stepWas * 0.001f);
            Assert.AreEqual(0f, DeepBreathFx.Amount);
        }

        [UnityTest]
        public IEnumerator Pause_StillStopsTime_AndTheBreathIsStillThereAfter()
        {
            _fx.ForceHold = true;
            yield return new WaitForSecondsRealtime(0.4f);
            GameSession.SetPaused(true);
            Assert.AreEqual(0f, Time.timeScale);
            float meter = _fx.Breath.Meter;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(0f, Time.timeScale, "the breath does not unfreeze a paused game");
            Assert.AreEqual(meter, _fx.Breath.Meter, "and costs nothing while paused");

            GameSession.SetPaused(false);
            yield return null;
            Assert.AreEqual(_fx.Breath.Rules.slowTo, Time.timeScale, 0.001f);
        }

        [UnityTest]
        public IEnumerator Hitstop_StillFreezes_InsideABreath_ThenTheBreathCarriesOn()
        {
            FeelSettings.Hitstop = true;
            FeelSettings.HeadshotSlowMo = false;
            _root.AddComponent<TimeFx>();
            _fx.ForceHold = true;
            yield return new WaitForSecondsRealtime(0.4f);

            CombatEvents.RaisePlayerHit(new HitOutcome { Hit = true, Severed = true, Enemy = true, Juice = 0.7f });
            yield return null;
            Assert.Less(Time.timeScale, 0.2f, "near-frozen during hitstop");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(_fx.Breath.Rules.slowTo, Time.timeScale, 0.001f, "back to the breath's speed, not to full speed");
        }

        [UnityTest]
        public IEnumerator OnlyEnemies_GiveBreathBack_ALimbALittle_AKillMore()
        {
            _fx.ForceHold = true;
            yield return new WaitForSecondsRealtime(3f);
            _fx.ForceHold = false;
            yield return null;
            DeepBreathRules rules = _fx.Breath.Rules;
            float meter = _fx.Breath.Meter;
            Assert.Less(meter, 0.6f);

            // A part shot off the ship is not an enemy's limb.
            CombatEvents.RaisePlayerHit(new HitOutcome { Hit = true, Severed = true, Juice = 0.7f });
            Assert.AreEqual(meter, _fx.Breath.Meter);

            CombatEvents.RaisePlayerHit(new HitOutcome { Hit = true, Severed = true, Enemy = true, Juice = 0.7f });
            Assert.AreEqual(meter + rules.perLimb, _fx.Breath.Meter, 1e-5f);

            CombatEvents.RaisePlayerHit(new HitOutcome { Hit = true, Severed = true, Killed = true, Enemy = true, Juice = 1f });
            Assert.AreEqual(meter + rules.perLimb + rules.perKill, _fx.Breath.Meter, 1e-5f, "a kill gives the kill's share, once");

            _fx.ResetForNewRun();
            Assert.AreEqual(1f, _fx.Breath.Meter);
        }

        [UnityTest]
        public IEnumerator Removed_TheWorldIsNeverLeftSlowed()
        {
            _fx.ForceHold = true;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.Less(Time.timeScale, 0.5f);
            Object.Destroy(_root);
            _root = null;
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(0f, DeepBreathFx.Amount);
            Assert.AreEqual(_stepWas, Time.fixedDeltaTime, _stepWas * 0.001f);
        }
    }
}
