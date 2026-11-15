using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Enemies;
using OrbitalAnte.Space;
using OrbitalAnte.Weapons;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// PlayMode draft for EnemySpawner for claude/playtest at 061cae6.
    /// Exercises the real cleanup methods; expected to fail before the captured-gun fix.
    /// Reflection only seeds the spawner's private bookkeeping and invokes those methods.
    /// </summary>
    public class CapturedWeaponCleanupTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        GameObject _root;
        EnemySpawner _spawner;
        EnemyConfig _config;
        WeaponData _data;
        int _gunNumber;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CapturedWeaponCleanupTestRoot");
            _spawner = Child("Spawner").AddComponent<EnemySpawner>();
            _spawner.enabled = false; // Coroutines still run; Update/FixedUpdate cannot change this fixture.
            _config = ScriptableObject.CreateInstance<EnemyConfig>();
            Set(_spawner, "_config", _config);

            _data = WeaponData.CreatePistolDefaults();
            // A real model-prefab input keeps weapon creation small and avoids loading shared model/LOD state.
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "TestGunModel";
            model.transform.SetParent(_root.transform, false);
            model.transform.localScale = new Vector3(0.1f, 0.1f, 0.4f);
            model.SetActive(false);
            _data.modelPrefab = model;
            _gunNumber = 0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // All guns, models, owners and components belong to this root; do not reset other tests' statics.
            if (_root != null) Object.Destroy(_root);
            if (_data != null) Object.Destroy(_data);
            if (_config != null) Object.Destroy(_config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CorpseTimer_PreservesCapturedGun_AndDeletesDroppedGun()
        {
            Weapon captured = Gun(playerHeld: true);
            Weapon dropped = Gun();

            // Run EnemySpawner's actual timer coroutine, including its WaitForSeconds and delayed Destroy.
            yield return _spawner.StartCoroutine((IEnumerator)Call("ClearGun", captured, 0f));
            yield return _spawner.StartCoroutine((IEnumerator)Call("ClearGun", dropped, 0f));
            yield return null;

            Assert.IsTrue(dropped == null, "the expired corpse timer still clears an unclaimed gun");
            Assert.IsTrue(captured != null, "the corpse timer must preserve the gun taken by the player");
            Assert.AreEqual(WeaponHold.HeldByPlayer, captured.Hold);
        }

        [UnityTest]
        public IEnumerator LooseGunCap_ExcludesCapturedGun_AndDeletesOnlyOldestDroppedGun()
        {
            // Player first catches both mistakes independently: counting it as loose, and selecting it for deletion.
            Weapon captured = Gun(playerHeld: true);
            var guns = (List<Weapon>)Get(_spawner, "_guns");
            guns.Add(captured);
            var dropped = new Weapon[13];
            for (int i = 0; i < dropped.Length; i++)
            {
                dropped[i] = Gun();
                guns.Add(dropped[i]);
            }

            Call("Tally");
            yield return null; // Destroy is deferred until the end of the frame.

            Assert.IsTrue(dropped[0] == null, "the oldest dropped gun must be removed above the cap of twelve");
            for (int i = 1; i < dropped.Length; i++)
                Assert.IsTrue(dropped[i] != null, "exactly twelve dropped guns must remain, including index " + i);
            Assert.IsTrue(captured != null, "a player-held gun cannot use or lose a loose-gun slot");
            Assert.AreEqual(WeaponHold.HeldByPlayer, captured.Hold);
        }

        [UnityTest]
        public IEnumerator SleepingOwner_PreservesCapturedGun_AndDeletesDroppedGunAndOwners()
        {
            // Bare, disabled components have no Build/Awake/Start world setup. A missing System makes Think
            // take its real 'leaving the world' cleanup branch before any terrain or spawning work.
            var world = Child("World").AddComponent<SolarSystemWorld>();
            world.enabled = false;
            Set(_spawner, "_world", world);
            _config.enemiesInTheWorld = false;

            const int body = 1;
            object campWorld = Nested("World");
            var awake = new[] { true };
            Set(campWorld, "Awake", awake);
            ((IDictionary)Get(_spawner, "_worlds")).Add(body, campWorld);

            Weapon captured = Gun(playerHeld: true);
            Weapon dropped = Gun();
            // Capture reparents the gun away from its former owner in production; keep that ownership here.
            Mannequin formerOwner = Child("FormerOwner").AddComponent<Mannequin>();
            Mannequin droppedOwner = Child("DroppedGunOwner").AddComponent<Mannequin>();
            var live = (IList)Get(_spawner, "_live");
            AddLive(live, formerOwner, captured, body);
            AddLive(live, droppedOwner, dropped, body);

            Call("Think");
            yield return null;

            Assert.AreEqual(0, live.Count, "both owners were actually removed by the sleeping cleanup path");
            Assert.IsFalse(awake[0], "the departing camp is sleeping again");
            Assert.IsTrue(formerOwner == null && droppedOwner == null, "both former owner objects are destroyed");
            Assert.IsTrue(dropped == null, "a dropped gun still goes away with its sleeping owner");
            Assert.IsTrue(captured != null, "sleeping the former owner must preserve the captured gun");
            Assert.AreEqual(WeaponHold.HeldByPlayer, captured.Hold);
        }

        GameObject Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            return go;
        }

        Weapon Gun(bool playerHeld = false)
        {
            Weapon gun = Weapon.Create(_data, _root.transform, oneHandCanReload: true);
            gun.transform.localPosition = Vector3.right * (++_gunNumber * 2f);
            gun.Body.useGravity = false;
            if (playerHeld)
            {
                gun.TakeByPlayer();
                Assert.AreEqual(WeaponHold.HeldByPlayer, gun.Hold);
                Assert.IsNull(gun.Holder, "a real player-held Weapon has no IWeaponHolder; null is not dropped");
            }
            else Assert.AreEqual(WeaponHold.Dropped, gun.Hold);
            return gun;
        }

        static void AddLive(IList live, Mannequin who, Weapon gun, int body)
        {
            object entry = Nested("Live");
            Set(entry, "Who", who);
            Set(entry, "Gun", gun);
            Set(entry, "Body", body);
            Set(entry, "Camp", 0);
            live.Add(entry);
        }

        static object Nested(string name)
        {
            Type type = typeof(EnemySpawner).GetNestedType(name, BindingFlags.NonPublic);
            Assert.IsNotNull(type, "EnemySpawner private fixture type " + name);
            return Activator.CreateInstance(type, nonPublic: true);
        }

        static object Get(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, Members);
            Assert.IsNotNull(field, "fixture field " + name);
            return field.GetValue(target);
        }

        static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Members);
            Assert.IsNotNull(field, "fixture field " + name);
            field.SetValue(target, value);
        }

        object Call(string name, params object[] args)
        {
            MethodInfo method = typeof(EnemySpawner).GetMethod(name, Members);
            Assert.IsNotNull(method, "actual cleanup method " + name);
            return method.Invoke(_spawner, args);
        }
    }
}
