using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// Paints a simple surface for each body from its two colours: bands for gas giants, blotchy ground for
    /// rocky worlds, seas and ice caps for Earth. This is a stand-in so every world is tell-apart-able and you
    /// can see it turning. The proper look of each planet is Milestone 6.
    /// </summary>
    public static class PlanetTextures
    {
        public static Texture2D Build(BodySpec spec, int width)
        {
            int height = width / 2;
            Color a = SpaceConvert.ParseColor(spec.colorA, Color.gray);
            Color b = SpaceConvert.ParseColor(spec.colorB, Color.gray * 0.7f);
            // The pattern depends only on the name, so Mars looks like the same Mars every run.
            Vector3 offset = OffsetFor(spec.id);
            var pixels = new Color32[width * height];
            // Worked out once, not once per pixel.
            bool seas = spec.id == "Earth";
            float capEdge = spec.id == "Earth" ? 0.86f : spec.id == "Mars" ? 0.9f : 2f;

            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                float polar = (1f - v) * Mathf.PI;
                float latitude = Mathf.Cos(polar);   // -1 at the south pole, +1 at the north
                float ring = Mathf.Sin(polar);
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float azimuth = u * Mathf.PI * 2f;
                    // Sample the pattern on the ball itself, so there is no seam and no pinching at the poles.
                    var p = new Vector3(ring * Mathf.Cos(azimuth), latitude, ring * Mathf.Sin(azimuth));
                    pixels[y * width + x] = Shade(spec.kind, seas, capEdge, a, b, p, latitude, offset);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = spec.id + " surface",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static Color Shade(BodyKind kind, bool seas, float capEdge, Color a, Color b, Vector3 p, float latitude, Vector3 offset)
        {
            switch (kind)
            {
                case BodyKind.Star:
                {
                    float n = Fractal(p * 5f + offset, 3);
                    return Color.Lerp(b, a, Mathf.SmoothStep(0.25f, 0.75f, n));
                }
                case BodyKind.GasGiant:
                {
                    // Bands by latitude, pushed about a little so the edges swirl.
                    float wobble = (Fractal(p * 3f + offset, 3) - 0.5f) * 0.22f;
                    float bands = Mathf.Sin((latitude + wobble) * 14f + offset.x) * 0.5f + 0.5f;
                    float fine = Mathf.Sin((latitude + wobble * 0.5f) * 41f) * 0.5f + 0.5f;
                    return Color.Lerp(a, b, Mathf.Clamp01(bands * 0.75f + fine * 0.25f));
                }
                default:
                {
                    float n = Fractal(p * 2.4f + offset, 4);
                    Color ground;
                    if (seas)
                    {
                        // Colour A is sea, colour B is land.
                        float land = Mathf.SmoothStep(0.5f, 0.56f, n);
                        Color soil = Color.Lerp(b, new Color(0.62f, 0.55f, 0.36f), Mathf.SmoothStep(0.6f, 0.8f, n));
                        ground = Color.Lerp(a, soil, land);
                    }
                    else
                    {
                        ground = Color.Lerp(b, a, Mathf.SmoothStep(0.3f, 0.7f, n));
                        // Darker pocks so airless worlds do not look like smooth marbles.
                        float pock = Noise(p * 11f - offset);
                        ground *= 1f - 0.22f * Mathf.SmoothStep(0.62f, 0.8f, pock);
                    }
                    if (capEdge < 1.5f)
                    {
                        float cap = Mathf.SmoothStep(capEdge, capEdge + 0.05f, Mathf.Abs(latitude) + (n - 0.5f) * 0.08f);
                        ground = Color.Lerp(ground, new Color(0.95f, 0.96f, 0.98f), cap);
                    }
                    ground.a = 1f;
                    return ground;
                }
            }
        }

        static Vector3 OffsetFor(string id)
        {
            uint h = 2166136261;
            for (int i = 0; i < id.Length; i++) h = (h ^ id[i]) * 16777619;
            return new Vector3((h & 1023) * 0.37f, ((h >> 10) & 1023) * 0.37f, ((h >> 20) & 1023) * 0.37f);
        }

        /// <summary>Several layers of <see cref="Noise"/>, each finer and fainter. Returns about 0 to 1.</summary>
        public static float Fractal(Vector3 p, int layers)
        {
            float total = 0f;
            float weight = 0.5f;
            float sum = 0f;
            for (int i = 0; i < layers; i++)
            {
                total += Noise(p) * weight;
                sum += weight;
                p *= 2.03f;
                weight *= 0.5f;
            }
            return total / sum;
        }

        /// <summary>Smooth random values in 3D, 0 to 1. The same input always gives the same output, on any machine.</summary>
        public static float Noise(Vector3 p)
        {
            int xi = Mathf.FloorToInt(p.x);
            int yi = Mathf.FloorToInt(p.y);
            int zi = Mathf.FloorToInt(p.z);
            float fx = p.x - xi;
            float fy = p.y - yi;
            float fz = p.z - zi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);

            float x00 = Mathf.Lerp(Corner(xi, yi, zi), Corner(xi + 1, yi, zi), fx);
            float x10 = Mathf.Lerp(Corner(xi, yi + 1, zi), Corner(xi + 1, yi + 1, zi), fx);
            float x01 = Mathf.Lerp(Corner(xi, yi, zi + 1), Corner(xi + 1, yi, zi + 1), fx);
            float x11 = Mathf.Lerp(Corner(xi, yi + 1, zi + 1), Corner(xi + 1, yi + 1, zi + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        static float Corner(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)z * 2147483647u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
