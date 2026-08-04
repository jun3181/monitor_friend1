#if UNITY_EDITOR
using Mate.Runtime.Face;
using Mate.Runtime.Look;
using Mate.Runtime.Voice.Configuration;
using Mate.Runtime.Voice.Core;
using Mate.Runtime.Voice.Interaction;
using Mate.Runtime.Voice.Presentation;
using Mate.Runtime.Voice.Recognition;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mate.Editor
{
    public static class MatePhase2SceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Mate/Scenes/MatePhase1.unity";
        private const string ProfilePath = "Assets/_Project/Mate/Data/SpeechRecognitionProfile.asset";

        [MenuItem("Mate/Phase 2/Install Speech Recognition")]
        public static void InstallPhase2Components()
        {
            if (!EnsureSceneIsOpen())
            {
                return;
            }

            var mate = GameObject.Find("Mate_VRM");
            if (mate == null)
            {
                Debug.LogError("Mate Phase 2 speech recognition was not installed. Mate_VRM was not found.");
                return;
            }

            var profile = EnsureProfileAsset();
            var scene = SceneManager.GetActiveScene();
            var camera = Camera.main;
            var speechObject = EnsureSceneObject("Speech Recognition", scene);
            var bubbleObject = EnsureSceneObject("User Speech Bubble", scene);

            var provider = EnsureComponent<WhisperSpeechToTextProvider>(speechObject);
            var coordinator = EnsureComponent<SpeechRecognitionCoordinator>(speechObject);
            var ptt = EnsureComponent<PushToTalkController>(speechObject);
            var debugView = EnsureComponent<SpeechRecognitionDebugView>(speechObject);
            var screenAnchor = EnsureComponent<MateScreenAnchor>(bubbleObject);
            var bubbleView = EnsureComponent<UserSpeechBubbleView>(bubbleObject);
            var bridge = EnsureComponent<MateSpeechExpressionBridge>(mate);
            var look = mate.GetComponent<MateLookController>();
            var blink = mate.GetComponent<MateBlinkController>();
            var anchor = EnsureBubbleAnchor(mate);

            coordinator.Configure(profile, provider);
            ptt.Configure(coordinator, profile);
            debugView.Configure(coordinator);
            screenAnchor.Configure(camera, anchor, null);
            bubbleView.Configure(coordinator, profile, screenAnchor);
            bridge.Configure(coordinator, look, blink);

            AssignObjectReference(coordinator, "profile", profile);
            AssignObjectReference(coordinator, "whisperProvider", provider);
            AssignObjectReference(ptt, "coordinator", coordinator);
            AssignObjectReference(ptt, "profile", profile);
            AssignObjectReference(debugView, "coordinator", coordinator);
            AssignObjectReference(screenAnchor, "targetCamera", camera);
            AssignObjectReference(screenAnchor, "worldAnchor", anchor);
            AssignObjectReference(bubbleView, "coordinator", coordinator);
            AssignObjectReference(bubbleView, "profile", profile);
            AssignObjectReference(bubbleView, "screenAnchor", screenAnchor);
            AssignObjectReference(bridge, "coordinator", coordinator);
            AssignObjectReference(bridge, "lookController", look);
            AssignObjectReference(bridge, "blinkController", blink);

            EditorUtility.SetDirty(speechObject);
            EditorUtility.SetDirty(bubbleObject);
            EditorUtility.SetDirty(mate);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Installed Mate Phase 2 speech recognition components.");
        }

        private static bool EnsureSceneIsOpen()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == ScenePath)
            {
                return true;
            }

            if (activeScene.isDirty)
            {
                Debug.LogWarning($"Mate Phase 2 scene was not opened because the current scene has unsaved changes: {ScenePath}");
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError($"Mate Phase 1 scene does not exist yet: {ScenePath}");
                return false;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        private static SpeechRecognitionProfile EnsureProfileAsset()
        {
            var profile = AssetDatabase.LoadAssetAtPath<SpeechRecognitionProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            var folder = System.IO.Path.GetDirectoryName(ProfilePath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                EnsureAssetFolder(folder);
            }

            profile = ScriptableObject.CreateInstance<SpeechRecognitionProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static GameObject EnsureSceneObject(string name, Scene scene)
        {
            var found = GameObject.Find(name);
            if (found != null)
            {
                return found;
            }

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private static Transform EnsureBubbleAnchor(GameObject mate)
        {
            var existing = mate.transform.Find("Speech Bubble Anchor");
            if (existing != null)
            {
                return existing;
            }

            var anchorObject = new GameObject("Speech Bubble Anchor");
            Undo.RegisterCreatedObjectUndo(anchorObject, "Create Speech Bubble Anchor");
            anchorObject.transform.SetParent(mate.transform, false);

            var bounds = CalculateRendererBounds(mate)
                ?? new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1f, 1.8f, 1f));
            var worldPosition = new Vector3(bounds.center.x, bounds.max.y + 0.12f, bounds.center.z);
            anchorObject.transform.localPosition = mate.transform.InverseTransformPoint(worldPosition);
            anchorObject.transform.localRotation = Quaternion.identity;
            anchorObject.transform.localScale = Vector3.one;
            return anchorObject.transform;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            component = Undo.AddComponent<T>(target);
            EditorUtility.SetDirty(target);
            return component;
        }

        private static void AssignObjectReference(Object target, string propertyName, Object value)
        {
            if (target == null || value == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static Bounds? CalculateRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return null;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }
    }

    [InitializeOnLoad]
    internal static class MatePhase2AutoInstaller
    {
        private const string ScenePath = "Assets/_Project/Mate/Scenes/MatePhase1.unity";
        private const string AutoInstallEditorPrefKey = "Mate.Phase2.Speech.AutoInstalled.v1";

        static MatePhase2AutoInstaller()
        {
            EditorApplication.delayCall += TryInstallOnce;
        }

        private static void TryInstallOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            var projectKey = $"{AutoInstallEditorPrefKey}.{Application.dataPath}";
            if (EditorPrefs.GetBool(projectKey, false))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath && activeScene.isDirty)
            {
                Debug.Log($"Mate Phase 2 speech setup is ready. Current scene has unsaved changes, so install it manually: {ScenePath}");
                return;
            }

            MatePhase2SceneBuilder.InstallPhase2Components();
            EditorPrefs.SetBool(projectKey, true);
        }
    }
}
#endif
