using UnityEditor;
using UnityEngine;

namespace OrbitalAnte.EditorTools
{
    /// <summary>
    /// Sets how Unity stores the baked picture of the sky (Resources/Sky/*.png), automatically, the moment it is
    /// imported, so nobody clicks through import settings: full size (4096 wide; Unity's default cap of 2048 would
    /// halve it and blur the Milky Way), colours as seen on screen (sRGB), no smaller copies (the sky shader always
    /// reads the sharpest one), wrapped round left to right and held at the poles, and compressed as BC7, which
    /// keeps dark, smooth sky free of the blocks and bands the cheaper formats leave. 8 MB on the graphics card.
    /// </summary>
    public sealed class SkyTextureImportRules : AssetPostprocessor
    {
        const string Root = "Assets/OrbitalAnte/Resources/Sky/";
        const string Stamp = "oa-sky-texture-1";
        public const int MaxSize = 4096;

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            // Decide once: after the stamp is on, anything changed by hand in the Inspector is left alone.
            if (importer.userData == Stamp) return;
            Apply(importer);
        }

        /// <summary>
        /// Called by project setup. If a sky picture was first imported before these rules could run (the editor
        /// scripts did not compile that time, say), Unity gave it its own defaults (half size, blocky compression)
        /// and would keep them for good. This finds any picture without the stamp and imports it again properly.
        /// </summary>
        public static void Ensure()
        {
            if (!AssetDatabase.IsValidFolder(Root.TrimEnd('/'))) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.userData == Stamp) continue;
                Apply(importer);
                importer.SaveAndReimport();
            }
        }

        static void Apply(TextureImporter importer)
        {
            importer.userData = Stamp;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                maxTextureSize = MaxSize,
                format = TextureImporterFormat.BC7,
                textureCompression = TextureImporterCompression.Compressed,
            });
        }
    }
}
