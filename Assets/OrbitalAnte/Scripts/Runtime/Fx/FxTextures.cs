using UnityEngine;

namespace OrbitalAnte.Fx
{
    /// <summary>Small textures generated in code so the effects need no imported art yet.</summary>
    public static class FxTextures
    {
        static Texture2D dot, splat;

        /// <summary>Soft round dot for blood droplets.</summary>
        public static Texture2D Dot
        {
            get
            {
                if (dot != null) return dot;
                const int n = 64;
                dot = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "FxDot", wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * 3f);
                    float shade = Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(dy * 0.5f + 0.5f));
                    px[y * n + x] = new Color(shade, shade, shade, a);
                }
                dot.SetPixels(px);
                dot.Apply(true);
                return dot;
            }
        }

        /// <summary>Cartoon blood splat: a blob with droplets around it, colour baked in.</summary>
        public static Texture2D Splat(Color wet, Color dark)
        {
            if (splat != null) return splat;
            const int n = 256;
            splat = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "FxSplat", wrapMode = TextureWrapMode.Clamp };
            var rng = new System.Random(1337);
            var blobs = new Vector3[18];
            blobs[0] = new Vector3(0.5f, 0.5f, 0.24f);
            for (int i = 1; i < blobs.Length; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 0.12f + (float)rng.NextDouble() * 0.3f;
                float r = i < 7 ? 0.07f + (float)rng.NextDouble() * 0.07f : 0.015f + (float)rng.NextDouble() * 0.03f;
                blobs[i] = new Vector3(0.5f + Mathf.Cos(ang) * dist, 0.5f + Mathf.Sin(ang) * dist, r);
            }
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float field = 0f;
                foreach (var b in blobs)
                {
                    float dx = u - b.x, dy = v - b.y;
                    field += b.z * b.z / Mathf.Max(0.00001f, dx * dx + dy * dy);
                }
                float a = Mathf.Clamp01((field - 1f) * 4f);
                float edge = Mathf.Clamp01((field - 1f) * 0.6f);
                var c = Color.Lerp(dark, wet, edge);
                // keep the very border of the texture empty so the splat never looks square
                float border = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                if (border < 0.02f) a = 0f;
                c.a = a;
                px[y * n + x] = c;
            }
            splat.SetPixels(px);
            splat.Apply(true);
            return splat;
        }
    }
}
