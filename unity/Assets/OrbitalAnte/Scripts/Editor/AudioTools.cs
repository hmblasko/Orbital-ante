using System;
using System.Collections.Generic;
using System.Reflection;
using OrbitalAnte.Audio;
using UnityEditor;
using UnityEngine;

namespace OrbitalAnte.EditorTools
{
    /// <summary>Menu items for the audio: check every file the manifest names, and (optionally) make a mixer asset.</summary>
    public static class AudioTools
    {
        const string MixerPath = "Assets/OrbitalAnte/Resources/Audio/OrbitalAnteMixer.mixer";

        [MenuItem("Orbital Ante/Audio/Check Audio Files", priority = 40)]
        static void CheckAudio()
        {
            TextAsset json = Resources.Load<TextAsset>(AudioManifest.ResourcePath);
            if (json == null)
            {
                EditorUtility.DisplayDialog("Orbital Ante audio", "The audio manifest is missing (Resources/Audio/audio_manifest.json).", "OK");
                return;
            }
            var problems = new List<string>();
            AudioManifest manifest;
            try
            {
                manifest = AudioManifest.Parse(json.text);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Orbital Ante audio", "The audio manifest could not be read:\n" + e.Message, "OK");
                return;
            }
            manifest.Validate(problems, path => Resources.Load<AudioClip>(path) != null);
            foreach (string p in problems) Debug.LogWarning("[Audio] " + p);
            string summary = manifest.Events.Count + " sound events, " + manifest.Tracks.Count + " pieces of music, " + manifest.Cues.Count + " transition cues.\n" +
                             (problems.Count == 0 ? "Every file is present." : problems.Count + " problem(s): see the Console.");
            Debug.Log("[Audio] " + summary);
            EditorUtility.DisplayDialog("Orbital Ante audio", summary, "OK");
        }

        /// <summary>
        /// Optional. Makes an AudioMixer asset with one group per bus, so levels can be watched and effects added
        /// in Unity's Audio Mixer window. The game works without it (it mixes in code); when the asset exists,
        /// every sound is routed to its group. Unity has no public way to create a mixer from a script, so this
        /// reaches into the editor's internals and may stop working in a future Unity version: if it fails it
        /// says so and changes nothing.
        /// </summary>
        [MenuItem("Orbital Ante/Audio/Create Mixer Asset (optional)", priority = 41)]
        static void CreateMixer()
        {
            if (AssetDatabase.LoadMainAssetAtPath(MixerPath) != null)
            {
                EditorUtility.DisplayDialog("Orbital Ante audio", "The mixer asset already exists:\n" + MixerPath, "OK");
                return;
            }
            try
            {
                Type controller = typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
                if (controller == null) throw new InvalidOperationException("This Unity version has no AudioMixerController.");
                const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                MethodInfo create = controller.GetMethod("CreateMixerControllerAtPath", any);
                MethodInfo newGroup = controller.GetMethod("CreateNewGroup", any);
                MethodInfo addChild = controller.GetMethod("AddChildToParent", any);
                MethodInfo addToView = controller.GetMethod("AddGroupToCurrentView", any);
                PropertyInfo master = controller.GetProperty("masterGroup", any);
                if (create == null || newGroup == null || addChild == null || master == null)
                    throw new InvalidOperationException("Unity's internal mixer methods have changed.");

                object mixer = create.Invoke(null, new object[] { MixerPath });
                object masterGroup = master.GetValue(mixer, null);
                foreach (string bus in Enum.GetNames(typeof(AudioBus)))
                {
                    object group = newGroup.Invoke(mixer, new object[] { bus, false });
                    addChild.Invoke(mixer, new[] { group, masterGroup });
                    if (addToView != null) addToView.Invoke(mixer, new[] { group });
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Orbital Ante audio", "Mixer created with one group per bus:\n" + MixerPath, "OK");
            }
            catch (Exception e)
            {
                AssetDatabase.DeleteAsset(MixerPath);
                Exception inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                Debug.LogWarning("[Audio] Could not create the mixer asset: " + inner.Message);
                EditorUtility.DisplayDialog("Orbital Ante audio", "The mixer asset could not be created in this Unity version:\n" + inner.Message +
                    "\n\nNothing was changed. The game does not need it.", "OK");
            }
        }
    }
}
