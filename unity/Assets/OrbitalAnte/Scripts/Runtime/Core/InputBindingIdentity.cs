using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Core
{
    /// <summary>Assigns stable binding IDs before a code-built map is enabled or its saved overrides are loaded.</summary>
    public static class InputBindingIdentity
    {
        public static void Assign(InputActionMap map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (map.enabled) throw new InvalidOperationException("Assign binding identities before enabling the map.");

            var ids = new HashSet<Guid>();
            foreach (InputAction action in map.actions)
            {
                string composite = null;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (binding.isComposite) composite = binding.name;
                    else if (!binding.isPartOfComposite) composite = null;
                    string kind = binding.isComposite ? "composite" : binding.isPartOfComposite ? "part" : "binding";
                    string groups = CanonicalGroups(binding.groups);
                    Guid id = BindingIdentity.Create(map.name, action.name, groups, binding.name, kind,
                        binding.isPartOfComposite ? composite : null);
                    // Never let inserting a second slot redirect a saved control to an arbitrary binding.
                    // Multiple slots in one group must have explicit, unique names (including composite roots).
                    if (!ids.Add(id))
                        throw new InvalidOperationException("Ambiguous saved control slot in " + map.name + "/" + action.name + ". Give each binding a unique name.");

                    binding.id = id;
                    action.ChangeBinding(i).To(binding);
                }
            }
        }

        static string CanonicalGroups(string groups)
        {
            if (string.IsNullOrEmpty(groups)) return string.Empty;
            string[] parts = groups.Split(InputBinding.Separator);
            Array.Sort(parts, StringComparer.Ordinal);
            return string.Join(InputBinding.Separator.ToString(), parts);
        }
    }
}
