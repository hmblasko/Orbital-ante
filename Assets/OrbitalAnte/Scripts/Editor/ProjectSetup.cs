using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OrbitalAnte.Characters;
using OrbitalAnte.Combat;
using OrbitalAnte.Core;
using OrbitalAnte.Feel;
using OrbitalAnte.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OrbitalAnte.EditorTools
{
    /// <summary>
    /// One-click project setup: URP asset, Steam Deck player settings, template materials,
    /// tunable config assets and the two scenes. Safe to run again; it only creates what is missing.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Root = "Assets/OrbitalAnte";
        const string SettingsDir = Root + "/Settings";
        const string ConfigDir = Root + "/Resources/Config";
        const string MaterialsDir = Root + "/Resources/Materials";
        const string ScenesDir = Root + "/Scenes";
        const string MainMenuPath = ScenesDir + "/MainMenu.unity";
        const string ArenaPath = ScenesDir + "/TestArena.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("oa.setupPrompted", false)) return;
                SessionState.SetBool("oa.setupPrompted", true);
                if (!NeedsSetup()) return;
                if (EditorUtility.DisplayDialog("Orbital Ante setup",
                        "This project still needs its one-time setup (render pipeline, Steam Deck settings, scenes and config files).\n\nRun it now?",
                        "Run setup", "Later"))
                    RunSetup();
            };
        }

        static bool NeedsSetup() =>
            GraphicsSettings.defaultRenderPipeline == null ||
            !File.Exists(ArenaPath) || !File.Exists(MainMenuPath) ||
            !File.Exists(ConfigDir + "/MannequinConfig.asset") ||
            !File.Exists(ConfigDir + "/GameFeelConfig.asset") ||
            !File.Exists(MaterialsDir + "/OA_Particle.mat") ||
            !File.Exists(MaterialsDir + "/OA_Decal.mat");

        [MenuItem("Orbital Ante/Run Project Setup", priority = 0)]
        public static void RunSetup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(SettingsDir);
            EnsureFolder(ConfigDir);
            EnsureFolder(MaterialsDir);
            EnsureFolder(ScenesDir);

            SetupRenderPipeline();
            SetupPlayerSettings();
            SetupMaterials();
            SetupConfigs();
            SetupScenes();
            bool restart = SetupInputHandling();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ArenaPath);
            Debug.Log("[OA] Project setup finished. Press Play to run the test arena.");

            if (restart)
            {
                if (EditorUtility.DisplayDialog("Restart needed",
                        "Unity must restart once to switch on the new Input System. Restart now?", "Restart", "Later"))
                    EditorApplication.OpenProject(Directory.GetCurrentDirectory());
            }
            else
            {
                EditorUtility.DisplayDialog("Orbital Ante setup", "Setup finished.\n\nPress the Play button (top middle) to try the test arena.", "OK");
            }
        }

        [MenuItem("Orbital Ante/Open Test Arena", priority = 20)]
        public static void OpenArena()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ArenaPath);
        }

        [MenuItem("Orbital Ante/Open Main Menu", priority = 21)]
        public static void OpenMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(MainMenuPath);
        }

        [MenuItem("Orbital Ante/Reset Saved Key Bindings", priority = 40)]
        public static void ResetBindings()
        {
            PlayerPrefs.DeleteKey("oa.bindings.v1");
            PlayerPrefs.Save();
            Debug.Log("[OA] Saved key bindings cleared.");
        }

        // ---------------------------------------------------------------------------------

        static void SetupRenderPipeline()
        {
            var existing = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(a => a != null);

            var urp = existing;
            if (urp == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                var ppGuid = AssetDatabase.FindAssets("t:PostProcessData", new[] { "Packages/com.unity.render-pipelines.universal" }).FirstOrDefault();
                if (ppGuid != null)
                    renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(AssetDatabase.GUIDToAssetPath(ppGuid));
                AssetDatabase.CreateAsset(renderer, SettingsDir + "/OA_URP_Renderer.asset");

                urp = UniversalRenderPipelineAsset.Create(renderer);
                urp.name = "OA_URP";
                AssetDatabase.CreateAsset(urp, SettingsDir + "/OA_URP.asset");

                // Steam Deck friendly defaults; tune in the asset's Inspector.
                urp.renderScale = 1f;
                urp.msaaSampleCount = 2;
                urp.shadowDistance = 60f;
                urp.supportsHDR = true;
                EditorUtility.SetDirty(urp);
            }

            EnsureDecalFeature();
            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "Orbital Ante";
            PlayerSettings.productName = "Orbital Ante";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = DeckPlatform.TargetWidth;
            PlayerSettings.defaultScreenHeight = DeckPlatform.TargetHeight;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = false;
        }

        static void SetupMaterials()
        {
            CreateMaterialIfMissing(MaterialsDir + "/OA_Lit.mat", "Universal Render Pipeline/Lit");
            CreateMaterialIfMissing(MaterialsDir + "/OA_Unlit.mat", "Universal Render Pipeline/Unlit");

            // Blood droplets: opaque with alpha clipping, so the round texture cuts out cleanly and
            // the clipped shader variant is kept in builds.
            var particle = CreateMaterialIfMissing(MaterialsDir + "/OA_Particle.mat", "Universal Render Pipeline/Particles/Unlit");
            if (particle != null)
            {
                particle.SetFloat("_AlphaClip", 1f);
                particle.SetFloat("_Cutoff", 0.5f);
                particle.EnableKeyword("_ALPHATEST_ON");
                EditorUtility.SetDirty(particle);
            }
            // Blood splats: URP's built-in decal shader.
            CreateMaterialIfMissing(MaterialsDir + "/OA_Decal.mat", "Shader Graphs/Decal");
        }

        /// <summary>Creates the material if missing. Returns it only when newly created.</summary>
        static Material CreateMaterialIfMissing(string path, string shaderName)
        {
            if (File.Exists(path)) return null;
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[OA] Shader not found: " + shaderName + ". Is the URP package installed?");
                return null;
            }
            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Blood decals need URP's Decal renderer feature on the renderer.</summary>
        static void EnsureDecalFeature()
        {
            var data = AssetDatabase.FindAssets("t:UniversalRendererData")
                .Select(g => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(d => d != null);
            if (data == null)
            {
                Debug.LogWarning("[OA] No URP renderer found; blood decals will not show.");
                return;
            }
            if (data.rendererFeatures.Any(f => f is DecalRendererFeature)) return;

            var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = "Decal";
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);

            // URP keeps a private list of feature file IDs next to the list; keep it in sync so the
            // renderer Inspector doesn't report a missing feature.
            var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance);
            if (mapField != null && mapField.GetValue(data) is List<long> map &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
                map.Add(localId);

            data.SetDirty();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        static void SetupConfigs()
        {
            CreateIfMissing<MannequinConfig>(ConfigDir + "/MannequinConfig.asset");
            CreateIfMissing<PlayerConfig>(ConfigDir + "/PlayerConfig.asset");
            CreateIfMissing<DebugGunConfig>(ConfigDir + "/DebugGunConfig.asset");
            CreateIfMissing<GameFeelConfig>(ConfigDir + "/GameFeelConfig.asset");
        }

        static void CreateIfMissing<T>(string path) where T : ScriptableObject
        {
            if (File.Exists(path)) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }

        static void SetupScenes()
        {
            CreateSceneIfMissing(MainMenuPath, BootMode.MainMenu);
            CreateSceneIfMissing(ArenaPath, BootMode.TestArena);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(ArenaPath, true),
            };
        }

        static void CreateSceneIfMissing(string path, BootMode mode)
        {
            if (File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Bootstrap");
            go.AddComponent<GameBootstrap>().Mode = mode;
            EditorSceneManager.SaveScene(scene, path);
        }

        /// <summary>Switches Active Input Handling to the new Input System. Returns true if a restart is needed.</summary>
        static bool SetupInputHandling()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (settings == null) return false;
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue != 0) return false; // 0 = old only, 1 = new, 2 = both
            prop.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
