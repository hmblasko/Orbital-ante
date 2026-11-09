using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Content
{
    /// <summary>
    /// Base for every piece of game content defined as data (weapons now; enemies, items and
    /// ship parts later). Each has a stable <see cref="id"/>: saves and other content refer to
    /// it by that id, never by file name, so files can be renamed and moved freely.
    /// </summary>
    public abstract class ContentDefinition : ScriptableObject
    {
        [Tooltip("Stable id, for example weapon.rifle_ak. Lowercase letters, digits, dots and underscores. Never change it once saves exist.")]
        public string id;

        /// <summary>Add a line to <paramref name="problems"/> for anything wrong with this asset's values.</summary>
        public virtual void Validate(List<string> problems) { }
    }
}
