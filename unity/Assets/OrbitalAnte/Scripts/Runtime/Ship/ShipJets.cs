using System;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// Which of the lander's small thrusters are firing, and how hard, for a given push and turn. Plain numbers,
    /// no engine, so it is tested outside Unity.
    ///
    /// Matthew asked on 15 Nov 2026 for every direction the ship moves in to show its thrusters firing. The big
    /// engines underneath already show a flame for an upward push (see Launch/LaunchFx). Everything else (left,
    /// right, forward, back, down, and turning) is done by the four thruster clusters on their stalks, like the
    /// Apollo Lunar Module's quads: each cluster has a nozzle pointing up, one down and one each way along the hull
    /// (<see cref="ShipProps.Thruster"/> draws them). A nozzle fires when the push it gives helps what the pilot
    /// asked for: its flame points away from the way the ship is meant to go, or it twists the ship the way it is
    /// being turned. So a push to the right lights the nozzles pointing left, and a turn to the left lights one
    /// side nozzle on each cluster, in a ring.
    /// </summary>
    public static class ShipJets
    {
        public const int Clusters = 4;
        public const int PerCluster = 4;
        public const int Count = Clusters * PerCluster;

        /// <summary>How far out along its own direction a nozzle's mouth is from the middle of its cluster.</summary>
        public const float MouthOut = 0.3f;

        /// <summary>The point the ship is thought of as turning about: the middle of the cabin at the clusters' height.</summary>
        public static readonly P3 Pivot = new P3(0f, 1.15f, 0f);

        /// <summary>A turn this fast (radians a second) fires the turning nozzles at full strength.</summary>
        public const float FullTurnRate = 1.2f;

        /// <summary>Turns slower than this light nothing, so a resting hand on the mouse does not flicker the jets.</summary>
        public const float LeastTurnRate = 0.04f;

        /// <summary>A nozzle only helps a turn when its twist lines up with it at least this well (0 to 1).</summary>
        public const float TurnAlignment = 0.5f;

        /// <summary>Which cluster nozzle <paramref name="nozzle"/> belongs to (it is the same index as the thruster part).</summary>
        public static int ClusterOf(int nozzle) => nozzle / PerCluster;

        /// <summary>The way nozzle <paramref name="nozzle"/>'s flame points, in ship space (a unit vector).</summary>
        public static P3 Way(int nozzle)
        {
            int cluster = nozzle / PerCluster;
            double a = ShipLayout.LegAngle(cluster);
            var side = new P3((float)Math.Cos(a), 0f, -(float)Math.Sin(a));
            switch (nozzle % PerCluster)
            {
                case 0: return P3.Up;
                case 1: return -P3.Up;
                case 2: return side;
                default: return -side;
            }
        }

        /// <summary>Where nozzle <paramref name="nozzle"/>'s flame starts, in ship space.</summary>
        public static P3 Mouth(int nozzle) => ShipLayout.Thruster(nozzle / PerCluster) + Way(nozzle) * MouthOut;

        /// <summary>
        /// How hard each nozzle fires, 0 to 1, written into <paramref name="into"/> (length <see cref="Count"/>).
        /// <paramref name="push"/>: the push in the ship's own directions (right, up, nose), each -1 to 1, already
        /// scaled by how hard the engines are working (<c>TestCraft.Firing</c>). An upward push is the big engines'
        /// job and lights nothing here. <paramref name="turnRate"/>: how fast the pilot is turning the ship, in
        /// radians a second about its own right, up and nose axes (the same sense as Unity's angular velocity).
        /// <paramref name="working"/>: for each cluster, whether it is still on the ship; a lost one never fires.
        /// </summary>
        public static void Drive(P3 push, P3 turnRate, Func<int, bool> working, float[] into)
        {
            if (into == null || into.Length < Count) throw new ArgumentException("Needs room for every nozzle.", nameof(into));
            var wanted = new P3(Clamp(push.X), Math.Min(0f, Clamp(push.Y)), Clamp(push.Z));

            float rate = turnRate.Length;
            float turn = rate < LeastTurnRate ? 0f : Math.Min(1f, rate / FullTurnRate);
            P3 turnWay = rate > 1e-6f ? turnRate * (1f / rate) : new P3(0f, 0f, 0f);

            for (int n = 0; n < Count; n++)
            {
                if (working != null && !working(ClusterOf(n))) { into[n] = 0f; continue; }
                P3 force = -Way(n);   // the ship is pushed the other way from the flame
                float drive = Math.Max(0f, P3.Dot(force, wanted));

                if (turn > 0f)
                {
                    P3 twist = P3.Cross(Mouth(n) - Pivot, force);
                    float length = twist.Length;
                    if (length > 1e-4f)
                    {
                        float alignment = P3.Dot(twist * (1f / length), turnWay);
                        if (alignment >= TurnAlignment) drive += turn * alignment;
                    }
                }
                into[n] = Math.Min(1f, drive);
            }
        }

        static float Clamp(float v) => Math.Max(-1f, Math.Min(1f, v));
    }
}
