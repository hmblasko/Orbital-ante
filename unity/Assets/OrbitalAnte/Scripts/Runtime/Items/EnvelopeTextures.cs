using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// The envelope's pictures as textures. Each piece is painted by <see cref="EnvelopeArt"/> the first time it is
    /// needed and kept for the session (about 5 MB in all, never compressed, so the gold and the dark paper keep
    /// their gradients). <see cref="Warm"/> paints them ahead of time so a run's first frame does not wait.
    /// </summary>
    public static class EnvelopeTextures
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static Dictionary<string, System.Func<ArtImage>> _makers;

        public static void ResetStatics()
        {
            foreach (var pair in Cache) if (pair.Value != null) Object.Destroy(pair.Value);
            Cache.Clear();
            _makers = null;
        }

        /// <summary>A fixed piece by its scene name, or null if there is no such piece.</summary>
        public static Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out Texture2D tex) && tex != null) return tex;
            if (_makers == null) _makers = EnvelopeArt.Pieces();
            if (!_makers.TryGetValue(name, out var make)) return null;
            tex = ToTexture(make(), name);
            Cache[name] = tex;
            return tex;
        }

        /// <summary>Paints every fixed piece now.</summary>
        public static void Warm()
        {
            if (_makers == null) _makers = EnvelopeArt.Pieces();
            foreach (string name in _makers.Keys) Get(name);
        }

        /// <summary>Paints a prize card and keeps it under <paramref name="key"/> (replacing any older card with that key).</summary>
        public static Texture2D Card(string key, ItemSpec item, ItemRarity rarity, float[] effectColour)
        {
            if (Cache.TryGetValue(key, out Texture2D old) && old != null) Object.Destroy(old);
            Texture2D tex = ToTexture(EnvelopeArt.Card(item, rarity, effectColour), key);
            Cache[key] = tex;
            return tex;
        }

        /// <summary>Copies a picture into a texture, flipping it (pictures start at the top, textures at the bottom).</summary>
        public static Texture2D ToTexture(ArtImage img, string name)
        {
            var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false, false)
            {
                name = "Envelope " + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[img.Width * img.Height];
            float[] p = img.Pixels;
            for (int y = 0; y < img.Height; y++)
            {
                int row = (img.Height - 1 - y) * img.Width;
                for (int x = 0; x < img.Width; x++)
                {
                    int i = (y * img.Width + x) * 4;
                    pixels[row + x] = new Color32(Byte(p[i]), Byte(p[i + 1]), Byte(p[i + 2]), Byte(p[i + 3]));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static byte Byte(float v) => (byte)(v <= 0f ? 0 : (v >= 1f ? 255 : (int)(v * 255f + 0.5f)));
    }
}
