using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>Turns the simulation's double-precision values into Unity's, and back.</summary>
    public static class SpaceConvert
    {
        public static Vector3 ToVector3(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        public static Vec3d ToVec3d(Vector3 v) => new Vec3d(v.x, v.y, v.z);
        public static Quaternion ToQuaternion(QuatD q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);

        public static Color ParseColor(string web, Color fallback)
        {
            return !string.IsNullOrEmpty(web) && ColorUtility.TryParseHtmlString(web, out Color c) ? c : fallback;
        }

        /// <summary>"850 m" or "12.4 km".</summary>
        public static string Distance(double metres)
        {
            double size = System.Math.Abs(metres);
            if (size < 1000) return System.Math.Round(metres).ToString("0") + " m";
            if (size < 100000) return (metres / 1000.0).ToString("0.0") + " km";
            return (metres / 1000.0).ToString("0") + " km";
        }
    }
}
