using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>Builds the shapes space is made of: smooth spheres, lumpy rocks, ring bands and the star field.</summary>
    public static class SpaceMeshes
    {
        static readonly Dictionary<int, Mesh> Spheres = new Dictionary<int, Mesh>();
        static Mesh[] _rocks;

        /// <summary>
        /// A sphere of radius 1 with far more faces than Unity's built-in one, so a planet's edge stays round when
        /// you are close to it. Shared between all bodies that ask for the same detail.
        /// </summary>
        public static Mesh Sphere(int segmentsAround)
        {
            if (Spheres.TryGetValue(segmentsAround, out Mesh cached) && cached != null) return cached;

            int around = Mathf.Max(8, segmentsAround);
            int down = around / 2;
            var vertices = new Vector3[(around + 1) * (down + 1)];
            var uvs = new Vector2[vertices.Length];
            for (int y = 0; y <= down; y++)
            {
                float v = y / (float)down;
                float polar = v * Mathf.PI;
                for (int x = 0; x <= around; x++)
                {
                    float u = x / (float)around;
                    float azimuth = u * Mathf.PI * 2f;
                    int i = y * (around + 1) + x;
                    vertices[i] = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    uvs[i] = new Vector2(u, 1f - v);
                }
            }

            var triangles = new int[around * down * 6];
            int t = 0;
            for (int y = 0; y < down; y++)
            {
                for (int x = 0; x < around; x++)
                {
                    int a = y * (around + 1) + x;
                    int b = a + around + 1;
                    triangles[t++] = a;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;
                    triangles[t++] = b + 1;
                    triangles[t++] = b;
                }
            }

            var mesh = new Mesh { name = "Sphere" + around };
            mesh.vertices = vertices;
            mesh.normals = vertices;   // On a unit sphere the outward direction is the position itself.
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            Spheres[segmentsAround] = mesh;
            return mesh;
        }

        /// <summary>One of a few lumpy rock shapes of radius about 1. The shapes are the same every run.</summary>
        public static Mesh Rock(int variant)
        {
            if (_rocks == null || _rocks[0] == null)
            {
                _rocks = new Mesh[4];
                for (int i = 0; i < _rocks.Length; i++) _rocks[i] = BuildRock(i);
            }
            return _rocks[((variant % _rocks.Length) + _rocks.Length) % _rocks.Length];
        }

        static Mesh BuildRock(int variant)
        {
            const int around = 14;
            const int down = 9;
            var rng = new SeededRandom(9100 + variant);
            Vector3 offset = new Vector3(rng.Range(-50f, 50f), rng.Range(-50f, 50f), rng.Range(-50f, 50f));
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var grid = new Vector3[(around + 1) * (down + 1)];
            for (int y = 0; y <= down; y++)
            {
                float polar = y / (float)down * Mathf.PI;
                for (int x = 0; x <= around; x++)
                {
                    float azimuth = (x % around) / (float)around * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    float lump = 0.72f + 0.42f * PlanetTextures.Noise(dir * 1.6f + offset) + 0.14f * PlanetTextures.Noise(dir * 4.3f - offset);
                    grid[y * (around + 1) + x] = dir * lump;
                }
            }
            // Separate corners for every face, so each face is flat-shaded and the rock looks chipped, not blobby.
            for (int y = 0; y < down; y++)
            {
                for (int x = 0; x < around; x++)
                {
                    Vector3 a = grid[y * (around + 1) + x];
                    Vector3 b = grid[y * (around + 1) + x + 1];
                    Vector3 c = grid[(y + 1) * (around + 1) + x];
                    Vector3 d = grid[(y + 1) * (around + 1) + x + 1];
                    AddTriangle(vertices, triangles, a, b, c);
                    AddTriangle(vertices, triangles, b, d, c);
                }
            }
            var mesh = new Mesh { name = "Rock" + variant };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            if ((a - b).sqrMagnitude < 1e-8f || (b - c).sqrMagnitude < 1e-8f || (a - c).sqrMagnitude < 1e-8f) return;
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        /// <summary>A flat band (washer shape) lying in the X-Z plane, visible from above and below.</summary>
        public static Mesh RingBand(float inner, float outer, int segments = 96)
        {
            var vertices = new Vector3[(segments + 1) * 4];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * 12];
            int t = 0;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int v = i * 4;
                vertices[v] = dir * inner;
                vertices[v + 1] = dir * outer;
                vertices[v + 2] = dir * inner;
                vertices[v + 3] = dir * outer;
                normals[v] = normals[v + 1] = Vector3.up;
                normals[v + 2] = normals[v + 3] = Vector3.down;
                if (i == segments) continue;
                int n = v + 4;
                // Top face.
                triangles[t++] = v;
                triangles[t++] = n;
                triangles[t++] = v + 1;
                triangles[t++] = v + 1;
                triangles[t++] = n;
                triangles[t++] = n + 1;
                // Bottom face, wound the other way.
                triangles[t++] = v + 2;
                triangles[t++] = v + 3;
                triangles[t++] = n + 2;
                triangles[t++] = v + 3;
                triangles[t++] = n + 3;
                triangles[t++] = n + 2;
            }
            var mesh = new Mesh { name = "RingBand" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Stars as small squares on the inside of a huge ball. The sky is the same every run: it is the one
        /// thing in the game that never moves, which makes it the reference you judge all other motion against.
        /// </summary>
        public static Mesh StarField(int count, float distance, float minSize, float maxSize, int seed)
        {
            var rng = new SeededRandom(seed);
            var vertices = new Vector3[count * 4];
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                // An even spread over the whole sky.
                float y = rng.Range(-1f, 1f);
                float azimuth = rng.Range(0f, Mathf.PI * 2f);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var dir = new Vector3(ring * Mathf.Cos(azimuth), y, ring * Mathf.Sin(azimuth));
                // Mostly faint, a few bright.
                float bright = rng.NextFloat();
                float size = Mathf.Lerp(minSize, maxSize, bright * bright * bright) * 0.5f;
                Vector3 right = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized * size;
                Vector3 up = Vector3.Cross(right, dir).normalized * size;
                Vector3 centre = dir * distance;
                int v = i * 4;
                vertices[v] = centre - right - up;
                vertices[v + 1] = centre - right + up;
                vertices[v + 2] = centre + right + up;
                vertices[v + 3] = centre + right - up;
                int t = i * 6;
                // Wound to face the centre, where the camera is.
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }
            var mesh = new Mesh { name = "StarField" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * distance * 2.2f);
            return mesh;
        }
    }
}
