using UnityEngine;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// The ante tasks' dials (Matthew, 20 Nov 2026: tasks the player picks and does their own way, all for points
    /// toward the ante). Edit the asset at Assets/OrbitalAnte/Resources/Config/TaskConfig; if it is missing the
    /// values written here are used. The points themselves are on the AnteBalance asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Task Config", fileName = "TaskConfig")]
    public sealed class TaskConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/TaskConfig";

        [Tooltip("enabled: untick to play a run with no ante timer, no score and no tasks (as before 20 Nov 2026).")]
        public bool enabled = true;

        [Tooltip("Streak, base clears, signal masts, salvage, supply drops, marked targets, landings and how many lit tasks to keep near you.")]
        public TaskRules rules = new TaskRules();

        [Header("How it looks")]
        [Tooltip("The ante readout and the points that pop up.")]
        public Color scoreColour = new Color(1f, 0.82f, 0.38f);
        [Tooltip("The streak and style labels.")]
        public Color styleColour = new Color(0.55f, 0.92f, 1f);
        [Tooltip("The column of light over a task you have not done yet: base clears and masts.")]
        public Color fightBeacon = new Color(1f, 0.42f, 0.22f, 0.55f);
        [Tooltip("The column of light over salvage and supply drops.")]
        public Color haulBeacon = new Color(0.40f, 0.85f, 1f, 0.55f);
        [Tooltip("Real seconds a '+points' line stays on screen.")]
        public float popSeconds = 2.4f;
        [Tooltip("How fast the shown score counts up to the real one, points per second (it never takes over 1.2 s).")]
        public float countUpPerSecond = 600f;

        static TaskConfig _cached;

        public static TaskConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<TaskConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<TaskConfig>();
                _cached.name = "TaskConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
