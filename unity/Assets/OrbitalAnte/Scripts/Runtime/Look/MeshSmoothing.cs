using System;
using System.Collections.Generic;

namespace OrbitalAnte.Look
{
    /// <summary>
    /// Works out which way each corner of a mesh faces so that curved things shade round, even when neighbouring
    /// panels do not share corners (they keep their own so each can have its own colour). Corners in the same
    /// place share their facing unless the surfaces meet at more than a set angle, so real creases stay crisp.
    /// Plain numbers, no Unity types.
    /// </summary>
    public static class MeshSmoothing
    {
        /// <param name="positions">Three numbers per corner.</param>
        /// <param name="triangles">Corner numbers, three per triangle, in Unity's order (front is where (b - a) x (c - a) points).</param>
        /// <param name="creaseDegrees">Surfaces meeting at more than this stay a hard edge.</param>
        /// <returns>Three numbers per corner: its facing, length one.</returns>
        public static float[] Normals(float[] positions, int[] triangles, float creaseDegrees)
        {
            int count = positions.Length / 3;
            var own = new float[count * 3];
            int faces = triangles.Length / 3;
            var faceNormal = new float[faces * 3];

            for (int f = 0; f < faces; f++)
            {
                int a = triangles[f * 3] * 3, b = triangles[f * 3 + 1] * 3, c = triangles[f * 3 + 2] * 3;
                float ux = positions[b] - positions[a], uy = positions[b + 1] - positions[a + 1], uz = positions[b + 2] - positions[a + 2];
                float vx = positions[c] - positions[a], vy = positions[c + 1] - positions[a + 1], vz = positions[c + 2] - positions[a + 2];
                // Left as long as the triangle is big, so big faces count for more.
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                faceNormal[f * 3] = nx; faceNormal[f * 3 + 1] = ny; faceNormal[f * 3 + 2] = nz;
                for (int k = 0; k < 3; k++)
                {
                    int v = triangles[f * 3 + k] * 3;
                    own[v] += nx; own[v + 1] += ny; own[v + 2] += nz;
                }
            }
            Normalise(own);

            // Corners that sit in the same place, and the triangles that touch each such place.
            var place = new Dictionary<long, List<int>>();
            var placeOf = new long[count];
            for (int i = 0; i < count; i++)
            {
                long key = Key(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
                placeOf[i] = key;
                if (!place.TryGetValue(key, out List<int> list)) place[key] = list = new List<int>();
                list.Add(i);
            }
            var facesAt = new Dictionary<long, List<int>>();
            for (int f = 0; f < faces; f++)
            {
                for (int k = 0; k < 3; k++)
                {
                    long key = placeOf[triangles[f * 3 + k]];
                    if (!facesAt.TryGetValue(key, out List<int> list)) facesAt[key] = list = new List<int>();
                    if (list.Count == 0 || list[list.Count - 1] != f) list.Add(f);
                }
            }

            float limit = (float)Math.Cos(creaseDegrees * Math.PI / 180.0);
            var result = new float[count * 3];
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                float sx = 0f, sy = 0f, sz = 0f;
                List<int> touching = facesAt.TryGetValue(placeOf[i], out List<int> found) ? found : null;
                if (touching != null)
                {
                    for (int t = 0; t < touching.Count; t++)
                    {
                        int f = touching[t] * 3;
                        float nx = faceNormal[f], ny = faceNormal[f + 1], nz = faceNormal[f + 2];
                        float length = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        if (length < 1e-12f) continue;
                        float agree = (nx * own[o] + ny * own[o + 1] + nz * own[o + 2]) / length;
                        if (agree >= limit) { sx += nx; sy += ny; sz += nz; }
                    }
                }
                if (sx * sx + sy * sy + sz * sz < 1e-20f) { sx = own[o]; sy = own[o + 1]; sz = own[o + 2]; }
                result[o] = sx; result[o + 1] = sy; result[o + 2] = sz;
            }
            Normalise(result);
            return result;
        }

        static long Key(float x, float y, float z)
        {
            // Half-millimetre cells: far finer than any real gap between panels.
            long qx = (long)Math.Round(x * 2000.0), qy = (long)Math.Round(y * 2000.0), qz = (long)Math.Round(z * 2000.0);
            return (qx & 0x1FFFFF) | ((qy & 0x1FFFFF) << 21) | ((qz & 0x1FFFFF) << 42);
        }

        static void Normalise(float[] v)
        {
            for (int i = 0; i < v.Length; i += 3)
            {
                float length = (float)Math.Sqrt(v[i] * v[i] + v[i + 1] * v[i + 1] + v[i + 2] * v[i + 2]);
                if (length < 1e-12f) { v[i] = 0f; v[i + 1] = 1f; v[i + 2] = 0f; continue; }
                v[i] /= length; v[i + 1] /= length; v[i + 2] /= length;
            }
        }
    }
}
