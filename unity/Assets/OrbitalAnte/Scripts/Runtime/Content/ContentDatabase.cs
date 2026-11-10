using System;
using System.Collections.Generic;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Content
{
    /// <summary>
    /// The one place game content is looked up. It loads every content asset from the Resources folders listed
    /// below, checks each one, and hands them out by type and id. Adding a gun is adding an asset to a folder:
    /// no code needs to know about it.
    ///
    /// If an expected asset is missing (the setup tool has not been run yet) a built-in default is used, so the
    /// game still starts.
    /// </summary>
    public static class ContentDatabase
    {
        /// <summary>Resources sub-folders that hold content assets. New kinds of content add a line here.</summary>
        static readonly string[] Folders = { WeaponData.ResourceFolder, Space.SolarSystemData.ResourceFolder };

        static readonly Dictionary<Type, Dictionary<string, ContentDefinition>> ByType =
            new Dictionary<Type, Dictionary<string, ContentDefinition>>();
        static readonly List<string> ProblemList = new List<string>();
        static bool _loaded;

        /// <summary>Everything wrong with the content that was loaded, in plain language. Empty when all is well.</summary>
        public static IReadOnlyList<string> Problems
        {
            get
            {
                EnsureLoaded();
                return ProblemList;
            }
        }

        public static void ResetStatics()
        {
            ByType.Clear();
            ProblemList.Clear();
            _loaded = false;
        }

        static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        /// <summary>Loads (or reloads) all content and validates it.</summary>
        public static void Load()
        {
            ByType.Clear();
            ProblemList.Clear();
            _loaded = true;

            for (int f = 0; f < Folders.Length; f++)
            {
                ContentDefinition[] assets = Resources.LoadAll<ContentDefinition>(Folders[f]);
                for (int i = 0; i < assets.Length; i++) Register(assets[i], Folders[f]);
            }

            // The two Milestone 3 guns must always exist.
            if (Find<WeaponData>(WeaponData.RifleId) == null) Register(WeaponData.CreateRifleDefaults(), "built-in");
            if (Find<WeaponData>(WeaponData.PistolId) == null) Register(WeaponData.CreatePistolDefaults(), "built-in");
            // ...and so must the solar system.
            if (Find<Space.SolarSystemData>(Space.SolarSystemData.DefaultId) == null) Register(Space.SolarSystemData.CreateDefaults(), "built-in");

            if (ProblemList.Count > 0)
            {
                Debug.LogWarning("Orbital Ante content has " + ProblemList.Count + " problem(s):\n - " + string.Join("\n - ", ProblemList));
            }
        }

        static void Register(ContentDefinition asset, string source)
        {
            if (asset == null) return;
            Type type = asset.GetType();
            if (!ByType.TryGetValue(type, out var byId))
            {
                byId = new Dictionary<string, ContentDefinition>(StringComparer.Ordinal);
                ByType[type] = byId;
            }

            if (string.IsNullOrEmpty(asset.Id))
            {
                ProblemList.Add("A " + type.Name + " in " + source + " has no name, so it has no id.");
                return;
            }
            if (byId.ContainsKey(asset.Id))
            {
                ProblemList.Add("Two " + type.Name + " assets are both called '" + asset.Id + "'. Ids must be unique; one was ignored.");
                return;
            }
            byId[asset.Id] = asset;

            int before = ProblemList.Count;
            asset.Validate(ProblemList);
            for (int i = before; i < ProblemList.Count; i++) ProblemList[i] = type.Name + " '" + asset.Id + "': " + ProblemList[i];
        }

        static T Find<T>(string id) where T : ContentDefinition
        {
            if (id != null && ByType.TryGetValue(typeof(T), out var byId) && byId.TryGetValue(id, out var asset)) return asset as T;
            return null;
        }

        /// <summary>The asset of type <typeparamref name="T"/> with this id, or null.</summary>
        public static T Get<T>(string id) where T : ContentDefinition
        {
            EnsureLoaded();
            return Find<T>(id);
        }

        /// <summary>Every asset of type <typeparamref name="T"/>, sorted by id so the order is the same every time.</summary>
        public static List<T> All<T>() where T : ContentDefinition
        {
            EnsureLoaded();
            var list = new List<T>();
            if (ByType.TryGetValue(typeof(T), out var byId))
            {
                foreach (var pair in byId) list.Add((T)pair.Value);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }
    }
}
