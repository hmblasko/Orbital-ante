using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Content
{
    /// <summary>
    /// Base for every piece of game content that is defined as data: weapons now; enemies, items and factions
    /// later. One asset per thing. The asset's file name is its id, which is what saves and other data refer to,
    /// so renaming an asset is renaming the thing.
    /// </summary>
    public abstract class ContentDefinition : ScriptableObject
    {
        /// <summary>Stable id used by saves and by other data. It is the asset's file name.</summary>
        public string Id => name;

        /// <summary>The name shown to the player.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Adds a plain-language line to <paramref name="problems"/> for anything wrong with this asset.</summary>
        public abstract void Validate(List<string> problems);
    }
}
