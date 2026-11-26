using UnityEditor;
using UnityEngine;

namespace OrbitalAnte.EditorTools
{
    /// <summary>
    /// Sets how Unity stores the painted pictures of the far planets (Resources/Planets/*.png: gas giants' cloud
    /// tops, cloud layers, Saturn's rings), automatically, the moment they are imported, so nobody clicks through
    /// import settings: full size, colours as seen on screen (sRGB), alpha kept (it is how much cloud or ring there
    /// is), smaller copies for when the planet is small on screen, wrapped round left to right and held at the
    /// poles, compressed as BC7. The rings' strip is tiny and is kept exactly, with no compression.
    /// </summary>
    public sealed class PlanetTextureImportRules : AssetPostprocessor
    {
        const string Root = "Assets/OrbitalAnte/Resources/Planets/";
        const string Stamp = "oa-planet-texture-1";
        public const int MaxSize = 2048;

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            // Decide once: after the stamp is on, anything changed by hand in the Inspector is left alone.
            if (importer.userData == Stamp) return;
            Apply(importer, path);
        }

        /// <summary>Called by project setup: imports again any planet picture that was first imported without these rules.</summary>
        public static void Ensure()
        {
            string folder = Root.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.userData == Stamp) continue;
                Apply(importer, path);
                importer.SaveAndReimport();
            }
        }

        static void Apply(TextureImporter importer, string path)
        {
            bool strip = path.EndsWith("Rings.png");
            importer.userData = Stamp;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = !strip;
            importer.wrapModeU = strip ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = strip ? FilterMode.Bilinear : FilterMode.Trilinear;
            importer.anisoLevel = strip ? 1 : 4;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = strip ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                maxTextureSize = MaxSize,
                format = strip ? TextureImporterFormat.RGBA32 : TextureImporterFormat.BC7,
                textureCompression = strip ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed,
            });
        }
    }
}
