#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlareSystem
{
    public static class FieldSceneSetupEditor
    {
        private const string ScenePath = "Assets/Scenes/PetroleumFieldFlareScene.unity";
        private const string CsvPath = "Assets/StreamingAssets/variant_3_4.csv";

        [MenuItem("Flare System/Field Model/Setup Petroleum Field Scene")]
        public static void SetupSceneFromMenu()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Scripts");
            EnsureFolder("Assets", "StreamingAssets");

            Scene scene = File.Exists(ToProjectFullPath(ScenePath))
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = FindOrCreate(FieldModelConstants.FieldRootName);
            FlareInstallationController controller = GetOrAdd<FlareInstallationController>(root);
            ArchiveModeController archive = GetOrAdd<ArchiveModeController>(root);
            EarlyWarningController warning = GetOrAdd<EarlyWarningController>(root);
            LogisticRegressionRiskModel risk = GetOrAdd<LogisticRegressionRiskModel>(root);
            FieldObjectResolver resolver = GetOrAdd<FieldObjectResolver>(root);
            FieldPipeFlowController pipeFlow = GetOrAdd<FieldPipeFlowController>(root);
            FieldFlameController flame = EnsureFieldFlame(root.transform);

            controller.archiveModeController = archive;
            controller.earlyWarningController = warning;
            controller.riskModel = risk;
            controller.flameController = flame;
            controller.pipeFlowController = pipeFlow;
            archive.controller = controller;
            archive.csvFileName = FieldModelConstants.DefaultCsvFileName;
            warning.controller = controller;
            warning.riskModel = risk;

            resolver.modelRoot = root.transform;
            pipeFlow.rebuildRoutesOnAwake = true;
            pipeFlow.BuildFieldRoutes();
            flame.EnsureFieldPlacement();
            flame.EnsureEffect();

            EnsureFacilityAnchor(root.transform, "DNS", FieldModelConstants.DNSPosition);
            EnsureFacilityAnchor(root.transform, "UPSV", FieldModelConstants.UPSVPosition);
            EnsureFacilityAnchor(root.transform, "UPN", FieldModelConstants.UPNPosition);
            EnsureFacilityAnchor(root.transform, "KNS", FieldModelConstants.KNSPosition);
            EnsureFacilityAnchor(root.transform, "BKNS", FieldModelConstants.BKNSPosition);
            EnsureFacilityAnchor(root.transform, "FlareSeparator", FieldModelConstants.SeparatorPosition);
            EnsureFacilityAnchor(root.transform, "FlareStackBase", FieldModelConstants.FlareStackBasePosition);

            EnsureCamera();
            EnsureLight();

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Petroleum field flare scene configured: " + ScenePath);
        }

        [MenuItem("Flare System/Field Model/Rebuild Field Pipe Flows")]
        public static void RebuildPipeFlowsFromMenu()
        {
            FieldPipeFlowController flow = FindObject<FieldPipeFlowController>();
            if (flow == null)
            {
                GameObject root = FindOrCreate(FieldModelConstants.FieldRootName);
                flow = GetOrAdd<FieldPipeFlowController>(root);
            }

            flow.BuildFieldRoutes();
            EditorUtility.SetDirty(flow);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Field pipe flow routes rebuilt from FieldModelConstants.");
        }

        [MenuItem("Flare System/Field Model/Validate Field Assets")]
        public static void ValidateFieldAssetsFromMenu()
        {
            int errors = 0;
            errors += Check(File.Exists(ToProjectFullPath(CsvPath)), "CSV exists: " + CsvPath);
            errors += Check(FindObject<FieldPipeFlowController>() != null, "FieldPipeFlowController exists in scene");
            errors += Check(FindObject<FieldFlameController>() != null, "FieldFlameController exists in scene");
            errors += Check(FieldModelConstants.Routes.Length == 11, "11 field pipe routes are defined");

            if (errors == 0)
            {
                Debug.Log("Petroleum field flare validation passed.");
            }
            else
            {
                Debug.LogError("Petroleum field flare validation failed with " + errors + " error(s).");
            }
        }

        private static FieldFlameController EnsureFieldFlame(Transform root)
        {
            FieldFlameController flame = FindObject<FieldFlameController>();
            if (flame != null)
            {
                return flame;
            }

            GameObject flameObject = new GameObject(FieldModelConstants.FlameAnchorName);
            flameObject.transform.SetParent(root, true);
            flameObject.transform.position = FieldModelConstants.FlareTipPosition;
            return flameObject.AddComponent<FieldFlameController>();
        }

        private static void EnsureFacilityAnchor(Transform root, string name, Vector3 position)
        {
            Transform existing = root.Find(name);
            if (existing != null)
            {
                existing.position = position;
                return;
            }

            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(root, true);
            anchor.transform.position = position;
        }

        private static Camera EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.transform.position = new Vector3(42f, -24f, 30f);
            camera.transform.rotation = Quaternion.Euler(58f, -37f, 0f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            return camera;
        }

        private static Light EnsureLight()
        {
            Light light = FindObject<Light>();
            if (light == null)
            {
                GameObject lightObject = new GameObject("Directional Light");
                light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.2f;
            return light;
        }

        private static GameObject FindOrCreate(string name)
        {
            GameObject existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static T FindObject<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
#else
            return Object.FindObjectOfType<T>(true);
#endif
        }

        private static int Check(bool condition, string message)
        {
            if (condition)
            {
                Debug.Log("[OK] " + message);
                return 0;
            }

            Debug.LogError("[ERROR] " + message);
            return 1;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static string ToProjectFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath);
        }
    }
}
#endif
