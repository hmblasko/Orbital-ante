using UnityEditor;
using UnityEngine;

namespace OrbitalAnte.EditorTools
{
    /// <summary>
    /// Sets how Unity stores the models' texture pictures (Resources/Models/Textures), automatically, the moment a
    /// picture is imported, so nobody clicks through import settings and they are right for the model shader:
    ///
    /// - <c>X_Color</c>: the surface's colour, as seen on screen (sRGB).
    /// - <c>X_Normal</c>: a normal map (tangent space, green up, as Blender bakes for OpenGL).
    /// - <c>X_Mask</c>: hollows, smoothness and bare metal in red, green and blue: plain numbers, so not sRGB.
    ///
    /// All three get mip maps (a body is seen from 2 m to 40 m), trilinear filtering with a little anisotropic
    /// filtering (guns are seen at a slant in the hand), high-quality compression, and a size cap of 2048 so a
    /// picture made larger by mistake cannot blow the Steam Deck's memory budget.
    /// </summary>
    public sealed class ModelTextureImportRules : AssetPostprocessor
    {
        const string Root = "Assets/OrbitalAnte/Resources/Models/Textures/";
        const string Stamp = "oa-model-texture-2";
        public const int MaxSize = 2048;

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            // Only decide once, the first time Unity sees the file: after that, anything changed by hand in the
            // Inspector is left alone.
            if (!importer.importSettingsMissing || importer.userData == Stamp) return;
            importer.userData = Stamp;

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            bool normal = name.EndsWith("_Normal");
            bool mask = name.EndsWith("_Mask");

            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !mask;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            // Only the colour gains much from sharper filtering at a slant; each extra tap is paid per pixel.
            importer.anisoLevel = normal || mask ? 1 : 4;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.isReadable = false;
            // The Deck build (Linux standalone): bumps as BC5, made for two-channel normal maps and much quicker to
            // import than BC7; colour and mask as BC7. All one byte a pixel.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                maxTextureSize = MaxSize,
                format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7,
                textureCompression = TextureImporterCompression.CompressedHQ,
            });
        }
    }
}
