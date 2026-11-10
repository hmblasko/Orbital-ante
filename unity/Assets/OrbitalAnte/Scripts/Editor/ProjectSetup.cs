using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Gore;
using OrbitalAnte.Player;
using OrbitalAnte.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OrbitalAnte.EditorTools
{
    /// <summary>
    /// One-click project setup, so nothing has to be configured by hand in the editor.
    /// Runs by itself the first time the project is opened, and again from the menu:
    /// Orbital Ante > Set Up Project.
    ///
    /// Every step is safe to repeat: it only creates what is missing.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/TestArena.unity";
        const string SettingsFolder = "Assets/Settings";
        const string RendererPath = SettingsFolder + "/OA_UniversalRenderer.asset";
        const string PipelinePath = SettingsFolder + "/OA_UniversalRenderPipeline.asset";
        const string ResourcesFolder = "Assets/OrbitalAnte/Resources";
        const string ConfigFolder = ResourcesFolder + "/Config";
        const string MaterialsFolder = ResourcesFolder + "/Materials";
        const string GoreConfigPath = ConfigFolder + "/GoreConfig.asset";
        const string DecalMaterialPath = MaterialsFolder + "/OA_Decal.mat";
        const string WeaponsFolder = ResourcesFolder + "/Weapons";
        const string ThirdPartyWeaponsFolder = "Assets/ThirdParty/Weapons";
        const string TriedThisSessionKey = "OrbitalAnte.SetupTriedThisSession";

        static ProjectSetup()
        {
            // Asset work is not allowed while scripts are still loading, so wait for the editor to settle.
            EditorApplication.delayCall += AutoRun;
        }

        static void AutoRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(TriedThisSessionKey, false)) return;
            SessionState.SetBool(TriedThisSessionKey, true);
            if (!NeedsSetup()) return;
            Run(false);
        }

        static bool NeedsSetup()
        {
            return !File.Exists(ScenePath)
                   || GraphicsSettings.defaultRenderPipeline == null
                   || AssetDatabase.LoadAssetAtPath<MannequinConfig>(ConfigFolder + "/MannequinConfig.asset") == null
                   || AssetDatabase.LoadAssetAtPath<PlayerConfig>(ConfigFolder + "/PlayerConfig.asset") == null
                   || AssetDatabase.LoadAssetAtPath<GoreConfig>(GoreConfigPath) == null
                   || AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponsFolder + "/" + WeaponData.RifleId + ".asset") == null
                   || AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponsFolder + "/" + WeaponData.PistolId + ".asset") == null
                   || AssetDatabase.LoadAssetAtPath<Material>(DecalMaterialPath) == null;
        }

        [MenuItem("Orbital Ante/Set Up Project", priority = 0)]
        static void RunFromMenu()
        {
            Run(true);
        }

        [MenuItem("Orbital Ante/Open Test Arena", priority = 1)]
        static void OpenTestArena()
        {
            if (!File.Exists(ScenePath))
            {
                Run(true);
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        static void Run(bool interactive)
        {
            var problems = new List<string>();
            bool needsRestart = false;

            Step("render pipeline (URP)", problems, SetUpRenderPipeline);
            Step("base materials", problems, SetUpMaterials);
            Step("config assets", problems, SetUpConfigAssets);
            Step("weapon data", problems, SetUpWeapons);
            Step("blood decals", problems, SetUpDecals);
            Step("layer names", problems, SetUpLayerNames);
            Step("player settings", problems, SetUpPlayerSettings);
            Step("test arena scene", problems, () => SetUpScene(interactive));
            Step("input system", problems, () => needsRestart = SetUpInputHandling());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (problems.Count == 0)
            {
                Debug.Log("Orbital Ante: project is set up. Open Assets/Scenes/TestArena and press Play.");
            }
            else
            {
                Debug.LogWarning("Orbital Ante: setup finished with " + problems.Count + " problem(s):\n - " + string.Join("\n - ", problems) +
                                 "\nYou can run it again from the menu: Orbital Ante > Set Up Project.");
            }

            if (needsRestart)
            {
                bool restart = EditorUtility.DisplayDialog(
                    "Orbital Ante",
                    "Unity needs to restart once to switch on the new Input System (keyboard, mouse and controller support).",
                    "Restart now", "Later");
                if (restart)
                {
                    EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                }
            }
            else if (interactive)
            {
                EditorUtility.DisplayDialog("Orbital Ante",
                    problems.Count == 0
                        ? "Project is set up.\n\nPress the Play button at the top of the editor to try the test arena."
                        : "Setup finished with problems. See the Console window for details.",
                    "OK");
            }
        }

        static void Step(string name, List<string> problems, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                problems.Add(name + ": " + e.Message);
                Debug.LogException(e);
            }
        }

        // ------------------------------------------------------------------ steps

        // URP is reached by name rather than by a compile-time reference. If a future Unity version renames
        // something here, the worst case is a clear message in the Console instead of a project that will not compile.
        const string UrpAssembly = "Unity.RenderPipelines.Universal.Runtime";
        const string UrpNamespace = "UnityEngine.Rendering.Universal.";

        static Type UrpType(string name)
        {
            Type type = Type.GetType(UrpNamespace + name + ", " + UrpAssembly);
            if (type == null)
            {
                throw new InvalidOperationException(
                    "Could not find " + name + ". Is the Universal RP package installed? (Window > Package Manager)");
            }
            return type;
        }

        static void SetUpRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;

            EnsureFolder(SettingsFolder);
            Type pipelineType = UrpType("UniversalRenderPipelineAsset");
            Type rendererType = UrpType("UniversalRendererData");
            Type rendererBaseType = UrpType("ScriptableRendererData");

            var pipeline = AssetDatabase.LoadAssetAtPath(PipelinePath, pipelineType) as RenderPipelineAsset;
            if (pipeline == null)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath(RendererPath, rendererType) as ScriptableObject;
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance(rendererType);
                    AssetDatabase.CreateAsset(rendererData, RendererPath);
                }

                // Preferred route: URP's own factory method.
                MethodInfo create = pipelineType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { rendererBaseType }, null);
                if (create != null)
                {
                    pipeline = create.Invoke(null, new object[] { rendererData }) as RenderPipelineAsset;
                }

                // Fallback: make the asset directly and point it at the renderer.
                if (pipeline == null)
                {
                    pipeline = ScriptableObject.CreateInstance(pipelineType) as RenderPipelineAsset;
                    if (pipeline == null) throw new InvalidOperationException("Could not create a URP pipeline asset.");
                    var serialized = new SerializedObject(pipeline);
                    SerializedProperty list = serialized.FindProperty("m_RendererDataList");
                    if (list != null && list.isArray)
                    {
                        if (list.arraySize == 0) list.arraySize = 1;
                        list.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }

        static void SetUpMaterials()
        {
            // Saved material assets make sure the shaders are included when the game is built for Steam Deck.
            EnsureFolder(MaterialsFolder);
            bool urp = GraphicsSettings.defaultRenderPipeline != null;
            CreateMaterialIfMissing(MaterialsFolder + "/OA_Lit.mat", urp ? "Universal Render Pipeline/Lit" : "Standard");
            CreateMaterialIfMissing(MaterialsFolder + "/OA_Unlit.mat", urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        }

        static void CreateMaterialIfMissing(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + shaderName);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Repair a material that was created before the render pipeline was ready.
                if (existing.shader != shader)
                {
                    existing.shader = shader;
                    EditorUtility.SetDirty(existing);
                }
                return;
            }
            AssetDatabase.CreateAsset(new Material(shader), path);
        }

        static void SetUpConfigAssets()
        {
            EnsureFolder(ConfigFolder);
            CreateAssetIfMissing<MannequinConfig>(ConfigFolder + "/MannequinConfig.asset");
            CreateAssetIfMissing<PlayerConfig>(ConfigFolder + "/PlayerConfig.asset");
            CreateAssetIfMissing<GoreConfig>(GoreConfigPath);
        }

        /// <summary>
        /// One data asset per gun, plus the folder where downloaded CC0 gun models go.
        /// </summary>
        static void SetUpWeapons()
        {
            EnsureFolder(WeaponsFolder);
            EnsureFolder(ThirdPartyWeaponsFolder);

            string riflePath = WeaponsFolder + "/" + WeaponData.RifleId + ".asset";
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(riflePath) == null)
            {
                AssetDatabase.CreateAsset(WeaponData.CreateRifleDefaults(), riflePath);
            }
            string pistolPath = WeaponsFolder + "/" + WeaponData.PistolId + ".asset";
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(pistolPath) == null)
            {
                AssetDatabase.CreateAsset(WeaponData.CreatePistolDefaults(), pistolPath);
            }
        }

        /// <summary>
        /// Blood splats use URP Decal Projectors. Those need two things: the "Decal" feature switched on in the
        /// renderer, and a material that uses the decal shader. If either cannot be set up, the game keeps
        /// working and draws flat splat meshes instead.
        /// </summary>
        static void SetUpDecals()
        {
            var config = AssetDatabase.LoadAssetAtPath<GoreConfig>(GoreConfigPath);
            bool ready = false;
            try
            {
                if (GraphicsSettings.defaultRenderPipeline == null)
                {
                    throw new InvalidOperationException("URP is not the active render pipeline.");
                }

                bool featureOn = EnsureDecalFeature();

                Shader shader = Shader.Find("Shader Graphs/Decal");
                if (shader == null) throw new InvalidOperationException("URP's decal shader (Shader Graphs/Decal) was not found.");
                EnsureFolder(MaterialsFolder);
                var material = AssetDatabase.LoadAssetAtPath<Material>(DecalMaterialPath);
                if (material == null)
                {
                    material = new Material(shader) { enableInstancing = true };
                    AssetDatabase.CreateAsset(material, DecalMaterialPath);
                }
                else if (material.shader != shader)
                {
                    material.shader = shader;
                    EditorUtility.SetDirty(material);
                }

                ready = featureOn;
            }
            finally
            {
                if (config != null && config.decalProjectorsReady != ready)
                {
                    config.decalProjectorsReady = ready;
                    EditorUtility.SetDirty(config);
                }
            }
        }

        /// <summary>Adds URP's Decal renderer feature to the renderer the project uses, if it is not there yet.</summary>
        static bool EnsureDecalFeature()
        {
            Type featureType = UrpType("DecalRendererFeature");
            Type rendererType = UrpType("UniversalRendererData");

            var rendererData = AssetDatabase.LoadAssetAtPath(RendererPath, rendererType) as ScriptableObject;
            if (rendererData == null)
            {
                // The project is using a pipeline asset this tool did not create; ask it for its renderer.
                RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
                PropertyInfo dataProp = pipeline.GetType().GetProperty("scriptableRendererData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                rendererData = dataProp != null ? dataProp.GetValue(pipeline) as ScriptableObject : null;
            }
            if (rendererData == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(rendererData)))
            {
                throw new InvalidOperationException(
                    "Could not find the URP renderer asset to add the Decal feature to. See docs/SETUP.md, \"Blood decals\".");
            }

            var serialized = new SerializedObject(rendererData);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            if (features == null || !features.isArray)
            {
                throw new InvalidOperationException("This URP version stores renderer features differently. See docs/SETUP.md, \"Blood decals\".");
            }

            for (int i = 0; i < features.arraySize; i++)
            {
                UnityEngine.Object existing = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null && featureType.IsInstanceOfType(existing)) return true;
            }

            // The same steps the "Add Renderer Feature" button performs.
            ScriptableObject feature = ScriptableObject.CreateInstance(featureType);
            if (feature == null) throw new InvalidOperationException("Could not create the Decal renderer feature.");
            feature.name = "Decal";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            if (map != null && map.isArray)
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
            return true;
        }

        static void CreateAssetIfMissing<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }

        static void SetUpLayerNames()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray) return;

            bool changed = false;
            for (int i = 0; i < Layers.Names.Length; i++)
            {
                int index = Layers.FirstNamedLayer + i;
                if (index >= layers.arraySize) break;
                SerializedProperty element = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = Layers.Names[i];
                    changed = true;
                }
            }
            if (changed) tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetUpPlayerSettings()
        {
            if (PlayerSettings.productName != "Orbital Ante") PlayerSettings.productName = "Orbital Ante";
            // Steam Deck's native resolution.
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
        }

        static void SetUpScene(bool interactive)
        {
            EnsureFolder("Assets/Scenes");

            if (!File.Exists(ScenePath))
            {
                Scene active = SceneManager.GetActiveScene();
                if (active.isDirty && !string.IsNullOrEmpty(active.path))
                {
                    // Never throw away the user's unsaved work.
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                }

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else
            {
                // Scene exists. If the editor is sitting in a blank untitled scene, open the arena for convenience.
                Scene active = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }

            // Make sure the scene is in the build list.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool listed = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath) listed = true;
            }
            if (!listed)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        /// <summary>Switches the project to the new Input System. Returns true if the editor must restart.</summary>
        static bool SetUpInputHandling()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return false;
            var settings = new SerializedObject(assets[0]);
            SerializedProperty handler = settings.FindProperty("activeInputHandler");
            if (handler == null) return false;

            // 0 = old Input Manager only, 1 = new Input System only, 2 = both.
            if (handler.intValue != 0) return false;
            handler.intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
