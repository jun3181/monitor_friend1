#if UNITY_EDITOR
using Mate.Runtime.Animation;
using Mate.Runtime.Core;
using Mate.Runtime.Desktop;
using Mate.Runtime.Face;
using Mate.Runtime.Interaction;
using Mate.Runtime.Look;
using Mate.Runtime.Movement;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mate.Editor
{
    [InitializeOnLoad]
    public static class MatePhase1SceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Mate/Scenes/MatePhase1.unity";
        private const string VrmPrefabPath = "Assets/VRM/1564199652637753878.prefab";
        private const string AutoBuildEditorPrefKey = "Mate.Phase1.Scene.AutoBuilt.v5";

        static MatePhase1SceneBuilder()
        {
            EditorApplication.delayCall += AutoBuildOnce;
        }

        [MenuItem("Mate/Phase 1/Create or Open VRM Scene")]
        public static void CreateOrOpenScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VrmPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Mate Phase 1 scene was not created. VRM prefab not found: {VrmPrefabPath}");
                return;
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                InstallPhase1Components();
                Debug.Log($"Opened Mate Phase 1 scene: {ScenePath}");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = "MatePhase1";
            EditorSceneManager.SetActiveScene(scene);

            var mate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            mate.name = "Mate_VRM";
            mate.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            mate.transform.localScale = Vector3.one;

            var bounds = CalculateRendererBounds(mate);
            if (!bounds.HasValue)
            {
                bounds = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1f, 1.8f, 1f));
                Debug.LogWarning("Mate Phase 1 scene used fallback bounds because no renderer was found on the VRM prefab.");
            }

            CreateCamera(scene, bounds.Value);
            CreateLighting(scene);
            CreatePreviewFloor(scene, bounds.Value);

            EditorSceneManager.SaveScene(scene, ScenePath);

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isDirty)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            InstallPhase1Components();
            Debug.Log($"Created Mate Phase 1 scene with VRM prefab: {ScenePath}");
        }

        [MenuItem("Mate/Phase 1/Install Basic Components")]
        public static void InstallPhase1Components()
        {
            if (!EnsurePhase1SceneIsOpen())
            {
                return;
            }

            var mate = GameObject.Find("Mate_VRM");
            if (mate == null)
            {
                Debug.LogError("Mate Phase 1 components were not installed. Mate_VRM was not found in the open scene.");
                return;
            }

            mate.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var controller = EnsureComponent<MateController>(mate);
            var animation = EnsureComponent<MateAnimationController>(mate);
            var naturalMotion = EnsureComponent<MateNaturalMotion>(mate);
            var look = EnsureComponent<MateLookController>(mate);
            var blink = EnsureComponent<MateBlinkController>(mate);
            var drag = EnsureComponent<MateDragController>(mate);
            var interaction = EnsureComponent<MateInteractionController>(mate);
            var collider = EnsureInteractionCollider(mate);
            var camera = Camera.main;

            AssignObjectReference(controller, "animationController", animation);
            AssignObjectReference(controller, "naturalMotion", naturalMotion);
            AssignObjectReference(controller, "lookController", look);
            AssignObjectReference(controller, "blinkController", blink);
            AssignObjectReference(look, "targetCamera", camera);
            AssignObjectReference(drag, "targetCamera", camera);
            AssignObjectReference(interaction, "targetCamera", camera);
            AssignObjectReference(interaction, "hitCollider", collider);
            AssignObjectReference(interaction, "controller", controller);
            AssignObjectReference(interaction, "dragController", drag);
            ConfigureCameraForDesktop(camera);
            EnsureDesktopWindowComponents(camera, collider);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Installed Mate Phase 1 basic and desktop components.");
        }

        private static void AutoBuildOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var projectKey = $"{AutoBuildEditorPrefKey}.{Application.dataPath}";
            if (EditorPrefs.GetBool(projectKey, false))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                OpenSceneIfCurrentSceneIsClean();
                InstallPhase1Components();
                EditorPrefs.SetBool(projectKey, true);
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(VrmPrefabPath) == null)
            {
                return;
            }

            CreateOrOpenScene();
            EditorPrefs.SetBool(projectKey, true);
        }

        private static void OpenSceneIfCurrentSceneIsClean()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == ScenePath)
            {
                InstallPhase1Components();
                return;
            }

            if (activeScene.isDirty)
            {
                Debug.Log($"Mate Phase 1 scene is ready. Current scene has unsaved changes, so open it manually: {ScenePath}");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            InstallPhase1Components();
            Debug.Log($"Opened Mate Phase 1 scene: {ScenePath}");
        }

        private static bool EnsurePhase1SceneIsOpen()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == ScenePath)
            {
                return true;
            }

            if (activeScene.isDirty)
            {
                Debug.LogWarning($"Mate Phase 1 scene was not opened because the current scene has unsaved changes: {ScenePath}");
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

        private static CapsuleCollider EnsureInteractionCollider(GameObject mate)
        {
            var capsule = mate.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = Undo.AddComponent<CapsuleCollider>(mate);
            }

            var bounds = CalculateRendererBounds(mate)
                ?? new Bounds(new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 1.7f, 0.5f));

            capsule.direction = 1;
            capsule.center = mate.transform.InverseTransformPoint(bounds.center);
            capsule.height = Mathf.Max(1.2f, bounds.size.y * 1.05f);
            capsule.radius = Mathf.Max(0.2f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.7f);
            capsule.isTrigger = false;
            EditorUtility.SetDirty(capsule);
            return capsule;
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

        private static void ConfigureCameraForDesktop(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            EditorUtility.SetDirty(camera);
        }

        private static void EnsureDesktopWindowComponents(Camera camera, Collider hitCollider)
        {
            var scene = SceneManager.GetActiveScene();
            var desktopObject = GameObject.Find("Desktop Window");
            if (desktopObject == null)
            {
                desktopObject = new GameObject("Desktop Window");
                Undo.RegisterCreatedObjectUndo(desktopObject, "Create Desktop Window");
                SceneManager.MoveGameObjectToScene(desktopObject, scene);
            }

            var hitTest = EnsureComponent<DesktopInputHitTest>(desktopObject);
            var window = EnsureComponent<DesktopWindowController>(desktopObject);

            hitTest.Configure(camera, hitCollider);
            window.Configure(camera, hitTest);
            AssignObjectReference(hitTest, "targetCamera", camera);
            AssignObjectReference(hitTest, "hitCollider", hitCollider);
            AssignObjectReference(window, "targetCamera", camera);
            AssignObjectReference(window, "inputHitTest", hitTest);
            EditorUtility.SetDirty(desktopObject);
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

        private static void CreateCamera(Scene scene, Bounds bounds)
        {
            var cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;

            var center = bounds.center;
            var height = Mathf.Max(bounds.size.y, 1.6f);
            var distance = Mathf.Max(2.5f, height * 1.8f);
            var target = center + Vector3.up * (height * 0.08f);

            cameraObject.transform.position = new Vector3(center.x, target.y, center.z - distance);
            cameraObject.transform.LookAt(target);
        }

        private static void CreateLighting(Scene scene)
        {
            var keyLight = new GameObject("Key Light");
            SceneManager.MoveGameObjectToScene(keyLight, scene);
            keyLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var light = keyLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            light.color = Color.white;
        }

        private static void CreatePreviewFloor(Scene scene, Bounds bounds)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.name = "Preview Floor";

            var floorY = bounds.min.y;
            floor.transform.position = new Vector3(bounds.center.x, floorY, bounds.center.z);
            floor.transform.localScale = Vector3.one * 1.5f;

            var renderer = floor.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    return;
                }

                renderer.sharedMaterial = new Material(shader)
                {
                    name = "Mate Preview Floor Material",
                    color = new Color(0.45f, 0.47f, 0.5f, 1f)
                };
            }
        }
    }
}
#endif
