using System.Collections.Generic;
using System.Linq;
using OrbitalAnte.Content;
using UnityEditor;
using UnityEngine;

namespace OrbitalAnte.EditorTools
{
    /// <summary>Menu item that checks every content asset (weapons now, more later) for mistakes.</summary>
    public static class ContentTools
    {
        [MenuItem("Orbital Ante/Validate Content", priority = 60)]
        public static void ValidateFromMenu()
        {
            var all = LoadAll();
            var problems = ContentValidation.FindProblems(all);
            if (problems.Count == 0)
            {
                Debug.Log($"[OA] Content check passed: {all.Count} asset(s), no problems.");
                EditorUtility.DisplayDialog("Content check", $"All good. Checked {all.Count} content asset(s).", "OK");
                return;
            }
            foreach (var p in problems) Debug.LogError("[OA] Content problem: " + p);
            EditorUtility.DisplayDialog("Content check",
                $"Found {problems.Count} problem(s) in {all.Count} asset(s):\n\n" + string.Join("\n", problems.Take(12)) +
                (problems.Count > 12 ? "\n..." : "") + "\n\nThe full list is in the Console.", "OK");
        }

        /// <summary>Every content asset in the project, wherever it is saved.</summary>
        public static List<ContentDefinition> LoadAll()
        {
            return AssetDatabase.FindAssets("t:ContentDefinition", new[] { "Assets" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ContentDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null)
                .ToList();
        }
    }
}
