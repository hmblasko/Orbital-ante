using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Every loaded object of a type, unsorted. Newer Unity 6 releases mark the sort-mode overload of
    /// FindObjectsByType obsolete, but 6000.0 has nothing else, so the call is kept here once with the
    /// warning silenced instead of warning from every caller.
    /// </summary>
    public static class SceneQuery
    {
        public static T[] All<T>() where T : Object
        {
#pragma warning disable CS0618
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#pragma warning restore CS0618
        }
    }
}
