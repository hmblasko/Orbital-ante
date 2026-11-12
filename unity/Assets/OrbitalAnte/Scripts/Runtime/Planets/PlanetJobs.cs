using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Runs heavy sums (shaping ground, painting a planet's picture) on spare processor cores so the game never
    /// stops to think. Work is queued with a priority; the most urgent starts first; and the result is handed
    /// back on the main thread, a few per frame, because only the main thread may touch Unity objects.
    /// </summary>
    public static class PlanetJobs
    {
        public sealed class Handle
        {
            internal Func<object> Work;
            internal Action<object> Done;
            internal Func<float> Priority;
            internal bool Patient;
            internal Task<object> Task;
            public bool Cancelled { get; private set; }
            public bool Finished { get; internal set; }

            /// <summary>The result is thrown away when it arrives. Work that has not started never starts.</summary>
            public void Cancel() => Cancelled = true;
        }

        static readonly List<Handle> Waiting = new List<Handle>();
        static readonly List<Handle> Running = new List<Handle>();

        public static int MaxRunning { get; set; } = 3;
        public static int WaitingCount => Waiting.Count;
        public static int RunningCount => Running.Count;
        public static bool Idle => Waiting.Count == 0 && Running.Count == 0;

        public static void ResetStatics()
        {
            for (int i = 0; i < Waiting.Count; i++) Waiting[i].Cancel();
            for (int i = 0; i < Running.Count; i++) Running[i].Cancel();
            Waiting.Clear();
            Running.Clear();
        }

        /// <summary>
        /// Queues work. <paramref name="work"/> runs on a background thread and must not touch Unity objects.
        /// <paramref name="done"/> runs later on the main thread with its result. <paramref name="priority"/> is
        /// asked each time a slot comes free: the lowest number goes first (distance to the player works well).
        /// </summary>
        /// <param name="patient">
        /// True for long work nobody is waiting on (painting far-away pictures). Patient work never takes the
        /// last free core, so urgent work such as the ground under the player can always start at once.
        /// </param>
        public static Handle Run<T>(Func<T> work, Action<T> done, Func<float> priority, bool patient = false) where T : class
        {
            var handle = new Handle
            {
                Work = () => work(),
                Done = result => done((T)result),
                Priority = priority,
                Patient = patient,
            };
            Waiting.Add(handle);
            return handle;
        }

        /// <summary>
        /// Call once a frame from the main thread. Hands back up to <paramref name="maxResults"/> finished
        /// results and starts waiting work on free cores.
        /// </summary>
        public static void Pump(int maxResults)
        {
            int handed = 0;
            for (int i = 0; i < Running.Count && handed < maxResults; i++)
            {
                Handle h = Running[i];
                if (!h.Task.IsCompleted) continue;
                Running.RemoveAt(i--);
                if (Finish(h)) handed++;
            }

            for (int i = Waiting.Count - 1; i >= 0; i--)
            {
                if (Waiting[i].Cancelled) Waiting.RemoveAt(i);
            }
            int limit = Mathf.Max(1, MaxRunning);
            while (Running.Count < limit && Waiting.Count > 0)
            {
                bool lastCore = limit > 1 && Running.Count >= limit - 1;
                int best = -1;
                float bestPriority = float.MaxValue;
                for (int i = 0; i < Waiting.Count; i++)
                {
                    if (lastCore && Waiting[i].Patient) continue;
                    float p = Waiting[i].Priority != null ? Waiting[i].Priority() : 0f;
                    if (p < bestPriority || best < 0)
                    {
                        bestPriority = p;
                        best = i;
                    }
                }
                if (best < 0) break;
                Handle next = Waiting[best];
                Waiting.RemoveAt(best);
                next.Task = Task.Run(next.Work);
                Running.Add(next);
            }
        }

        /// <summary>
        /// Stops and waits until every queued piece of work has finished and been handed back, including work
        /// queued by those results. Used when a world is first built and by tests; never during play.
        /// </summary>
        public static void FinishAll()
        {
            int guard = 0;
            int oldLimit = MaxRunning;
            // While everything is being waited for, there is no urgent work to keep a core free for.
            MaxRunning = Mathf.Max(2, MaxRunning) + 1;
            while (!Idle && guard++ < 100000)
            {
                Pump(int.MaxValue);
                if (Running.Count > 0)
                {
                    try
                    {
                        Task.WaitAny(TasksOf(Running));
                    }
                    catch (AggregateException)
                    {
                        // Reported when the result is collected.
                    }
                }
            }
            MaxRunning = oldLimit;
        }

        static Task[] TasksOf(List<Handle> handles)
        {
            var tasks = new Task[handles.Count];
            for (int i = 0; i < tasks.Length; i++) tasks[i] = handles[i].Task;
            return tasks;
        }

        /// <summary>True if a result was actually delivered (a cancelled one costs nothing and does not count).</summary>
        static bool Finish(Handle h)
        {
            h.Finished = true;
            if (h.Task.IsFaulted)
            {
                Debug.LogException(h.Task.Exception != null ? h.Task.Exception.GetBaseException() : new Exception("Planet work failed."));
                return false;
            }
            if (h.Cancelled) return false;
            try
            {
                h.Done(h.Task.Result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return true;
        }
    }
}
