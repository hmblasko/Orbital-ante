using System.Collections.Generic;
using OrbitalAnte.Content;
using UnityEngine;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// One prize item, as data. Assets live in Assets/OrbitalAnte/Resources/Items (made by the setup tool from
    /// <see cref="ItemCatalog"/>) and can be edited in the Inspector; the file name is the id, which the save refers
    /// to, so renaming an asset renames the item and players lose it. If an asset is missing the built-in one is used.
    /// The rules read a plain copy (<see cref="ToSpec"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Prize Item", fileName = "Item")]
    public sealed class ItemData : ContentDefinition
    {
        public const string ResourceFolder = "Items";

        [Tooltip("Everything about the item. Names and rarities are placeholders until Matthew sets them; the art is Fable's.")]
        public ItemSpec spec = new ItemSpec();

        public override string DisplayName => spec != null ? spec.displayName : name;

        public override void Validate(List<string> problems)
        {
            if (spec == null) problems.Add("it has no item settings.");
            else spec.Validate(problems);
        }

        /// <summary>A plain copy for the rules, with the id taken from the asset's name.</summary>
        public ItemSpec ToSpec()
        {
            ItemSpec copy = spec.Copy();
            copy.id = name;
            return copy;
        }

        public static ItemData Create(ItemSpec spec)
        {
            var data = CreateInstance<ItemData>();
            data.name = spec.id;
            data.spec = spec.Copy();
            return data;
        }
    }
}
