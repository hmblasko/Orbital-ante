using System.Collections.Generic;
using OrbitalAnte.Combat;
using UnityEngine;

namespace OrbitalAnte.Content
{
    /// <summary>Ids the code itself needs. Everything else is discovered from the data files.</summary>
    public static class ContentIds
    {
        public const string RifleAK = "weapon.rifle_ak";
        public const string TestPistol = "weapon.test_pistol";
    }

    /// <summary>
    /// Finds content by id. Weapons are every <see cref="WeaponDefinition"/> asset in
    /// Resources/Weapons: drop a new asset in that folder, give it an id, and it exists in the game.
    /// </summary>
    public static class ContentDatabase
    {
        const string WeaponsFolder = "Weapons";

        static Dictionary<string, WeaponDefinition> weapons;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() => weapons = null;

        public static IReadOnlyCollection<WeaponDefinition> Weapons
        {
            get
            {
                EnsureLoaded();
                return weapons.Values;
            }
        }

        /// <summary>The weapon with this id, or null (with a warning) if there is none.</summary>
        public static WeaponDefinition Weapon(string id)
        {
            EnsureLoaded();
            if (id != null && weapons.TryGetValue(id, out var w)) return w;
            Debug.LogWarning($"[OA] No weapon with id \"{id}\".");
            return null;
        }

        public static bool TryGetWeapon(string id, out WeaponDefinition weapon)
        {
            EnsureLoaded();
            weapon = null;
            return id != null && weapons.TryGetValue(id, out weapon);
        }

        static void EnsureLoaded()
        {
            if (weapons != null) return;
            weapons = Index(Resources.LoadAll<WeaponDefinition>(WeaponsFolder));

            // The two test weapons always exist, even before project setup has made their assets.
            AddBuiltIn(ContentIds.RifleAK, WeaponDefinition.DefaultRifle);
            AddBuiltIn(ContentIds.TestPistol, WeaponDefinition.DefaultPistol);
        }

        /// <summary>Builds the id lookup. Bad or repeated ids are skipped with a warning.</summary>
        public static Dictionary<string, T> Index<T>(IEnumerable<T> defs) where T : ContentDefinition
        {
            var map = new Dictionary<string, T>();
            foreach (var d in defs)
            {
                if (d == null) continue;
                if (!ContentValidation.IsValidId(d.id))
                    Debug.LogWarning($"[OA] {d.name} has no valid id and was skipped. Run Orbital Ante > Validate Content.");
                else if (map.ContainsKey(d.id))
                    Debug.LogWarning($"[OA] {d.name} repeats the id \"{d.id}\" and was skipped. Run Orbital Ante > Validate Content.");
                else
                    map[d.id] = d;
            }
            return map;
        }

        static void AddBuiltIn(string id, System.Func<WeaponDefinition> make)
        {
            if (!weapons.ContainsKey(id)) weapons[id] = make();
        }
    }
}
