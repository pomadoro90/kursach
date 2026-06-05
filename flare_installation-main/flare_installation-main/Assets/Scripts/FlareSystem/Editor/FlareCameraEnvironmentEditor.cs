using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlareSystem.Editor
{
    public static class FlareCameraEnvironmentEditor
    {
        private const string ScenePath = "Assets/Scenes/FlareInstallationScene_Rebuilt.unity";
        private const string BackupPath = "Assets/Scenes/FlareInstallationScene_Rebuilt_Before_CameraEnvironmentFix.unity";
        private const string ScreenshotFolder = "Screenshots/UnityCamera";
        private const string ReportPath = "Reports/unity_camera_environment_report.txt";
        private const int ScreenshotWidth = 1920;
        private const int ScreenshotHeight = 1080;

        public static void ApplyCameraEnvironmentFix()
        {
            Scene scene = OpenScene();
            EnsureBackup();

            GameObject root = FindOrCreateRoot();
            GameObject modelRoot = FindModelRoot();
            Bounds modelBounds = CalculateBounds(modelRoot);
            Bounds separatorBounds = ResolveNamedBounds(modelRoot, modelBounds, "separator", "knockout", "drum");
            Vector3 flareTop = ResolveFlareTop(modelRoot, modelBounds);

            MaterialSet materials = LoadMaterials();
            CreateEnvironment(root.transform, modelBounds, materials);
            SetupLighting(modelBounds, flareTop);
            SetupCameraRig(root.transform, modelBounds, separatorBounds, flareTop);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteReport(modelBounds);

            Debug.Log("[FlareCameraEnvironment] Camera, camera targets and low-poly industrial environment applied.");
        }

        public static void CaptureCameraScreenshots()
        {
            OpenScene();

            Camera camera = Camera.main;
            CameraController controller = UnityEngine.Object.FindAnyObjectByType<CameraController>(FindObjectsInactive.Include);
            if (camera == null)
            {
                Debug.LogError("[FlareCameraEnvironment] Cannot capture screenshots: Main Camera not found.");
                return;
            }

            GameObject modelRoot = FindModelRoot();
            Bounds modelBounds = CalculateBounds(modelRoot);
            string outputDir = ProjectPath(ScreenshotFolder);
            Directory.CreateDirectory(outputDir);

            FlareInstallationController installation = UnityEngine.Object.FindAnyObjectByType<FlareInstallationController>(FindObjectsInactive.Include);
            EarlyWarningController warning = UnityEngine.Object.FindAnyObjectByType<EarlyWarningController>(FindObjectsInactive.Include);
            FlameController flame = UnityEngine.Object.FindAnyObjectByType<FlameController>(FindObjectsInactive.Include);
            if (installation != null)
            {
                installation.EnsureReferences();
                installation.SetArchiveMode();
            }

            if (flame != null)
            {
                flame.SetNormal();
            }

            SimulateParticles();

            CapturePreset(camera, controller, 0, "overview.png", false);
            CapturePreset(camera, controller, 1, "separator.png", false);
            CapturePreset(camera, controller, 2, "flare_stack.png", false);

            if (flame != null)
            {
                flame.SetWarning();
            }
            CapturePreset(camera, controller, 3, "flare_top.png", false);

            if (flame != null)
            {
                flame.SetNormal();
            }
            SimulateParticles();
            CapturePreset(camera, controller, 4, "piping.png", false);

            if (installation != null)
            {
                installation.SetArchiveMode();
            }
            CapturePreset(camera, controller, 5, "ui_overview.png", true);

            if (installation != null)
            {
                installation.SetEarlyWarningMode();
            }
            if (warning != null)
            {
                warning.SetInputs(0.21f, 13f, 0.024f);
            }
            CapturePreset(camera, controller, 5, "ui_warning_current.png", true);

            if (installation != null)
            {
                installation.OpenRiskPanel();
            }
            CapturePreset(camera, controller, 5, "risk_panel_current.png", true);

            Debug.Log("[FlareCameraEnvironment] Camera screenshots saved to: " + outputDir);
        }

        private static Scene OpenScene()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            return SceneManager.GetActiveScene();
        }

        private static void EnsureBackup()
        {
            if (File.Exists(ProjectPath(BackupPath)))
            {
                return;
            }

            if (File.Exists(ProjectPath(ScenePath)))
            {
                File.Copy(ProjectPath(ScenePath), ProjectPath(BackupPath), false);
                AssetDatabase.ImportAsset(BackupPath);
                Debug.Log("[FlareCameraEnvironment] Backup created: " + BackupPath);
            }
        }

        private static GameObject FindOrCreateRoot()
        {
            GameObject root = GameObject.Find("FlareSystemRoot");
            if (root == null)
            {
                root = new GameObject("FlareSystemRoot");
            }

            return root;
        }

        private static GameObject FindModelRoot()
        {
            GameObject model = GameObject.Find("Combined_Flare_Installation_Model");
            if (model != null)
            {
                return model;
            }

            GameObject importedRoot = GameObject.Find("ImportedModelRoot");
            if (importedRoot != null)
            {
                Renderer renderer = importedRoot.GetComponentInChildren<Renderer>(true);
                return renderer != null ? renderer.transform.root.gameObject : importedRoot;
            }

            return null;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            if (root == null)
            {
                return new Bounds(Vector3.zero, new Vector3(18f, 34f, 14f));
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.one);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? bounds : new Bounds(root.transform.position, new Vector3(18f, 34f, 14f));
        }

        private static Bounds ResolveNamedBounds(GameObject root, Bounds fallback, params string[] names)
        {
            if (root == null)
            {
                return fallback;
            }

            bool hasBounds = false;
            Bounds bounds = fallback;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                string lower = renderer.name.ToLowerInvariant();
                if (!names.Any(lower.Contains))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? bounds : fallback;
        }

        private static Vector3 ResolveFlareTop(GameObject modelRoot, Bounds modelBounds)
        {
            GameObject flame = GameObject.Find("FlameEffect");
            if (flame != null)
            {
                return flame.transform.position;
            }

            Bounds stackBounds = ResolveNamedBounds(modelRoot, modelBounds, "flare", "stack", "chimney", "tower", "tip");
            return new Vector3(stackBounds.center.x, stackBounds.max.y + 0.25f, stackBounds.center.z);
        }

        private static void SetupCameraRig(Transform root, Bounds modelBounds, Bounds separatorBounds, Vector3 flareTop)
        {
            DeleteSceneObject("CameraRig");

            GameObject rig = new GameObject("CameraRig");
            rig.transform.SetParent(root, false);
            GameObject targetsRoot = new GameObject("CameraTargets");
            targetsRoot.transform.SetParent(rig.transform, false);

            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            camera.name = "Main Camera";
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.fieldOfView = 48f;

            CameraController controller = camera.GetComponent<CameraController>();
            if (controller == null)
            {
                controller = camera.gameObject.AddComponent<CameraController>();
            }

            Vector3 overviewTarget = new Vector3(modelBounds.center.x, modelBounds.min.y + modelBounds.size.y * 0.43f, modelBounds.center.z);
            Vector3 separatorTarget = new Vector3(separatorBounds.center.x, separatorBounds.center.y + separatorBounds.extents.y * 0.15f, separatorBounds.center.z);
            Vector3 stackTarget = new Vector3(flareTop.x, modelBounds.min.y + modelBounds.size.y * 0.52f, flareTop.z);
            Vector3 flareTopTarget = flareTop + Vector3.up * 0.35f;
            Vector3 pipingTarget = new Vector3(modelBounds.center.x, modelBounds.min.y + 2.4f, modelBounds.center.z - modelBounds.extents.z * 0.15f);
            Vector3 uiTarget = overviewTarget + Vector3.up * 0.4f;

            CreateTarget(targetsRoot.transform, "OverviewTarget", overviewTarget);
            CreateTarget(targetsRoot.transform, "SeparatorTarget", separatorTarget);
            CreateTarget(targetsRoot.transform, "FlareStackTarget", stackTarget);
            CreateTarget(targetsRoot.transform, "FlareTopTarget", flareTopTarget);
            CreateTarget(targetsRoot.transform, "PipingTarget", pipingTarget);
            CreateTarget(targetsRoot.transform, "UiOverviewTarget", uiTarget);

            float height = Mathf.Max(modelBounds.size.y, 12f);
            float span = Mathf.Max(modelBounds.size.x, modelBounds.size.z);
            float viewDistance = Mathf.Clamp(Mathf.Max(span * 1.2f, height * 0.62f), 16f, 34f);

            var presets = new List<CameraPreset>
            {
                MakePreset("Overview", overviewTarget + new Vector3(viewDistance * 0.84f, height * 0.31f, -viewDistance * 0.9f), overviewTarget, 54f),
                MakePreset("Separator", separatorTarget + new Vector3(7.2f, 3.8f, -8.4f), separatorTarget, 42f),
                MakePreset("Flare Stack", stackTarget + new Vector3(10.5f, 6.8f, -12.0f), stackTarget, 44f),
                MakePreset("Flare Top / Flame", flareTopTarget + new Vector3(3.5f, 1.8f, -4.8f), flareTopTarget, 28f),
                MakePreset("Piping / Flows", pipingTarget + new Vector3(7.6f, 4.6f, -9.2f), pipingTarget, 40f),
                MakePreset("UI Overview", uiTarget + new Vector3(viewDistance * 0.95f, height * 0.32f, -viewDistance * 0.98f), uiTarget, 56f)
            };

            controller.controlledCamera = camera;
            controller.presets.Clear();
            controller.presets.AddRange(presets);
            controller.orbitTarget = targetsRoot.transform.Find("OverviewTarget");
            controller.orbitSensitivity = 4.2f;
            controller.zoomSensitivity = Mathf.Max(5f, viewDistance * 0.18f);
            controller.panSpeed = Mathf.Max(4f, viewDistance * 0.22f);
            controller.minDistance = 3.5f;
            controller.maxDistance = Mathf.Max(45f, viewDistance * 2.5f);
            controller.transitionDuration = 0.7f;
            controller.pitchLimits = new Vector2(8f, 76f);
            controller.ignoreMouseWhenPointerOverUi = true;
            controller.ApplyPresetImmediate(0);

            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(controller);
        }

        private static CameraPreset MakePreset(string name, Vector3 position, Vector3 target, float fov)
        {
            Quaternion rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
            return new CameraPreset(name, position, rotation.eulerAngles, fov, target);
        }

        private static void CreateTarget(Transform parent, string name, Vector3 position)
        {
            GameObject target = new GameObject(name);
            target.transform.SetParent(parent, false);
            target.transform.position = position;
        }

        private static void SetupLighting(Bounds modelBounds, Vector3 flareTop)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.44f, 0.46f, 0.48f);
            RenderSettings.reflectionIntensity = 0.45f;

            Light directional = FindLight("Directional Light");
            if (directional == null)
            {
                GameObject lightObject = new GameObject("Directional Light");
                directional = lightObject.AddComponent<Light>();
                directional.type = LightType.Directional;
            }

            directional.type = LightType.Directional;
            directional.intensity = 0.72f;
            directional.color = new Color(1f, 0.96f, 0.88f);
            directional.transform.rotation = Quaternion.Euler(48f, -36f, 0f);

            Light fillLeft = EnsurePointLight("Environment Fill Light Left", modelBounds.center + new Vector3(-16f, 8f, -12f));
            fillLeft.intensity = 0.18f;
            fillLeft.range = 36f;
            fillLeft.color = new Color(0.62f, 0.76f, 1f);

            Light fillRight = EnsurePointLight("Environment Fill Light Right", modelBounds.center + new Vector3(14f, 7f, 10f));
            fillRight.intensity = 0.14f;
            fillRight.range = 32f;
            fillRight.color = new Color(1f, 0.86f, 0.66f);

            Light flameLight = FindLight("Flame Point Light");
            if (flameLight != null)
            {
                flameLight.transform.position = flareTop + Vector3.up * 0.5f;
                flameLight.range = Mathf.Max(flameLight.range, 8f);
            }
        }

        private static void CreateEnvironment(Transform root, Bounds modelBounds, MaterialSet materials)
        {
            DeleteSceneObject("EnvironmentRoot");
            DeleteLooseGeneratedObject("ConcreteBase");

            GameObject environment = new GameObject("EnvironmentRoot");
            environment.transform.SetParent(root, false);

            float padX = Mathf.Max(14f, modelBounds.size.x * 0.65f);
            float padZ = Mathf.Max(16f, modelBounds.size.z * 0.9f);
            float baseWidth = modelBounds.size.x + padX * 2f;
            float baseDepth = modelBounds.size.z + padZ * 2f;
            Vector3 baseCenter = new Vector3(modelBounds.center.x, modelBounds.min.y - 0.08f, modelBounds.center.z);

            GameObject concrete = CreateCube("ConcreteBase", environment.transform, baseCenter, new Vector3(baseWidth, 0.12f, baseDepth), materials.Concrete);
            concrete.isStatic = true;

            float roadZ = modelBounds.min.z - padZ * 0.58f;
            CreateCube("AsphaltRoad", environment.transform,
                new Vector3(modelBounds.center.x, modelBounds.min.y + 0.01f, roadZ),
                new Vector3(baseWidth * 0.9f, 0.035f, 3.2f), materials.Asphalt);

            GameObject fence = new GameObject("SafetyFence");
            fence.transform.SetParent(environment.transform, false);
            CreateFence(fence.transform, baseCenter, baseWidth * 0.96f, baseDepth * 0.96f, materials.DarkMetal, materials.SafetyYellow);

            GameObject lightPoles = new GameObject("LightPoles");
            lightPoles.transform.SetParent(environment.transform, false);
            CreateLightPoles(lightPoles.transform, modelBounds, baseWidth, baseDepth, materials);

            GameObject tanks = new GameObject("BackgroundStorageTanks");
            tanks.transform.SetParent(environment.transform, false);
            CreateStorageTanks(tanks.transform, modelBounds, materials);

            GameObject pipeRack = new GameObject("BackgroundPipeRack");
            pipeRack.transform.SetParent(environment.transform, false);
            CreatePipeRack(pipeRack.transform, modelBounds, materials);

            GameObject utility = new GameObject("UtilityDetails");
            utility.transform.SetParent(environment.transform, false);
            CreateUtilityDetails(utility.transform, modelBounds, materials);
        }

        private static void CreateFence(Transform parent, Vector3 center, float width, float depth, Material postMaterial, Material railMaterial)
        {
            float y = center.y + 0.8f;
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            Vector3[] corners =
            {
                new Vector3(center.x - halfW, y, center.z - halfD),
                new Vector3(center.x + halfW, y, center.z - halfD),
                new Vector3(center.x + halfW, y, center.z + halfD),
                new Vector3(center.x - halfW, y, center.z + halfD)
            };

            for (int side = 0; side < 4; side++)
            {
                Vector3 a = corners[side];
                Vector3 b = corners[(side + 1) % 4];
                CreateBeam("Fence_Rail_" + side + "_Top", parent, a + Vector3.up * 0.55f, b + Vector3.up * 0.55f, 0.045f, railMaterial);
                CreateBeam("Fence_Rail_" + side + "_Mid", parent, a, b, 0.035f, postMaterial);

                int posts = Mathf.Max(3, Mathf.CeilToInt(Vector3.Distance(a, b) / 4.2f));
                for (int i = 0; i <= posts; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)posts);
                    CreateCube("Fence_Post", parent, p + Vector3.down * 0.15f, new Vector3(0.08f, 1.45f, 0.08f), postMaterial);
                }
            }
        }

        private static void CreateLightPoles(Transform parent, Bounds modelBounds, float baseWidth, float baseDepth, MaterialSet materials)
        {
            Vector3 center = modelBounds.center;
            float x = baseWidth * 0.38f;
            float z = baseDepth * 0.36f;
            Vector3[] positions =
            {
                new Vector3(center.x - x, modelBounds.min.y, center.z - z),
                new Vector3(center.x + x, modelBounds.min.y, center.z - z),
                new Vector3(center.x - x, modelBounds.min.y, center.z + z),
                new Vector3(center.x + x, modelBounds.min.y, center.z + z)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject pole = new GameObject("LightPole_" + (i + 1).ToString("00"));
                pole.transform.SetParent(parent, false);
                Vector3 basePosition = positions[i];
                CreateCylinder("Pole", pole.transform, basePosition + Vector3.up * 2.2f, 0.055f, 4.4f, materials.DarkMetal);
                CreateCube("LampHead", pole.transform, basePosition + new Vector3(0f, 4.5f, 0.22f), new Vector3(0.42f, 0.16f, 0.28f), materials.LightMetal);
                CreateCube("LampGlow", pole.transform, basePosition + new Vector3(0f, 4.42f, 0.36f), new Vector3(0.34f, 0.04f, 0.18f), materials.LampGlow);

                if (i < 2)
                {
                    Light light = EnsurePointLight("LightPole_" + (i + 1).ToString("00") + "_Fill", basePosition + new Vector3(0f, 4.25f, 0.2f));
                    light.intensity = 0.22f;
                    light.range = 8f;
                    light.color = new Color(1f, 0.88f, 0.66f);
                }
            }
        }

        private static void CreateStorageTanks(Transform parent, Bounds modelBounds, MaterialSet materials)
        {
            float startX = modelBounds.max.x + Mathf.Max(10f, modelBounds.size.x * 0.45f);
            float z = modelBounds.max.z + Mathf.Max(7f, modelBounds.size.z * 0.55f);
            for (int i = 0; i < 3; i++)
            {
                GameObject tank = new GameObject("BackgroundTank_" + (i + 1).ToString("00"));
                tank.transform.SetParent(parent, false);
                Vector3 position = new Vector3(startX + i * 3.8f, modelBounds.min.y + 1.35f, z + (i % 2) * 1.4f);
                CreateCylinder("TankBody", tank.transform, position, 1.15f, 2.7f, materials.LightMetal);
                CreateCylinder("TankRoof", tank.transform, position + Vector3.up * 1.42f, 1.1f, 0.1f, materials.DarkMetal);
                CreateCube("TankBase", tank.transform, position + Vector3.down * 1.35f, new Vector3(2.45f, 0.12f, 2.45f), materials.Concrete);
            }
        }

        private static void CreatePipeRack(Transform parent, Bounds modelBounds, MaterialSet materials)
        {
            float z = modelBounds.max.z + Mathf.Max(5f, modelBounds.size.z * 0.45f);
            float startX = modelBounds.min.x - 2f;
            float endX = modelBounds.max.x + 8f;
            float y0 = modelBounds.min.y;
            int columns = 5;
            for (int i = 0; i < columns; i++)
            {
                float t = i / (float)(columns - 1);
                float x = Mathf.Lerp(startX, endX, t);
                CreateCube("PipeRack_Column", parent, new Vector3(x, y0 + 1.45f, z), new Vector3(0.12f, 2.9f, 0.12f), materials.DarkMetal);
            }

            CreateBeam("PipeRack_TopBeam", parent, new Vector3(startX, y0 + 2.9f, z), new Vector3(endX, y0 + 2.9f, z), 0.07f, materials.DarkMetal);
            CreateBeam("PipeRack_MidBeam", parent, new Vector3(startX, y0 + 1.9f, z), new Vector3(endX, y0 + 1.9f, z), 0.055f, materials.DarkMetal);
            CreateBeam("BackgroundPipe_01", parent, new Vector3(startX, y0 + 2.55f, z - 0.25f), new Vector3(endX, y0 + 2.55f, z - 0.25f), 0.08f, materials.PipeGas);
            CreateBeam("BackgroundPipe_02", parent, new Vector3(startX, y0 + 2.25f, z + 0.15f), new Vector3(endX, y0 + 2.25f, z + 0.15f), 0.065f, materials.PipeBlue);
        }

        private static void CreateUtilityDetails(Transform parent, Bounds modelBounds, MaterialSet materials)
        {
            Vector3 basePosition = new Vector3(modelBounds.min.x - 5f, modelBounds.min.y + 0.45f, modelBounds.max.z + 3.5f);
            CreateCube("UtilityBox_01", parent, basePosition, new Vector3(1.0f, 0.9f, 0.65f), materials.DarkMetal);
            CreateCube("UtilityBox_02", parent, basePosition + new Vector3(1.35f, -0.08f, 0.15f), new Vector3(0.75f, 0.74f, 0.55f), materials.LightMetal);
            CreateBeam("CableTray_Background", parent, basePosition + new Vector3(-1.2f, 0.55f, -0.35f), basePosition + new Vector3(2.6f, 0.55f, -0.35f), 0.055f, materials.SafetyYellow);
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            AssignMaterial(cube, material);
            RemoveCollider(cube);
            return cube;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 position, float radius, float height, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.position = position;
            cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            AssignMaterial(cylinder, material);
            RemoveCollider(cylinder);
            return cylinder;
        }

        private static GameObject CreateBeam(string name, Transform parent, Vector3 start, Vector3 end, float radius, Material material)
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = name;
            beam.transform.SetParent(parent, false);
            Vector3 direction = end - start;
            beam.transform.position = (start + end) * 0.5f;
            beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            beam.transform.localScale = new Vector3(radius * 2f, direction.magnitude * 0.5f, radius * 2f);
            AssignMaterial(beam, material);
            RemoveCollider(beam);
            return beam;
        }

        private static void AssignMaterial(GameObject gameObject, Material material)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void CapturePreset(Camera camera, CameraController controller, int index, string fileName, bool showUi)
        {
            if (controller != null && controller.presets.Count > index)
            {
                controller.ApplyPresetImmediate(index);
            }

            Capture(camera, ProjectPath(ScreenshotFolder), fileName, showUi);
        }

        private static void Capture(Camera camera, string outputDir, string fileName, bool showUi)
        {
            Directory.CreateDirectory(outputDir);
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            Dictionary<Canvas, bool> activeStates = canvases.ToDictionary(c => c, c => c.gameObject.activeSelf);
            Dictionary<Canvas, RenderMode> modes = canvases.ToDictionary(c => c, c => c.renderMode);
            Dictionary<Canvas, Camera> cameras = canvases.ToDictionary(c => c, c => c.worldCamera);
            Dictionary<Canvas, float> distances = canvases.ToDictionary(c => c, c => c.planeDistance);

            foreach (Canvas canvas in canvases)
            {
                canvas.gameObject.SetActive(showUi);
                if (showUi)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                }
            }

            RenderTexture target = new RenderTexture(ScreenshotWidth, ScreenshotHeight, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(ScreenshotWidth, ScreenshotHeight, TextureFormat.RGB24, false);
            RenderTexture oldActive = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;

            camera.targetTexture = target;
            RenderTexture.active = target;
            camera.Render();
            image.ReadPixels(new Rect(0, 0, ScreenshotWidth, ScreenshotHeight), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(outputDir, fileName), image.EncodeToPNG());

            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = modes[canvas];
                canvas.worldCamera = cameras[canvas];
                canvas.planeDistance = distances[canvas];
                canvas.gameObject.SetActive(activeStates[canvas]);
            }

            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static void SimulateParticles()
        {
            foreach (ParticleSystem system in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
            {
                system.gameObject.SetActive(true);
                system.Simulate(1.4f, true, true, true);
            }
        }

        private static Light FindLight(string name)
        {
            GameObject gameObject = GameObject.Find(name);
            return gameObject != null ? gameObject.GetComponent<Light>() : null;
        }

        private static Light EnsurePointLight(string name, Vector3 position)
        {
            Light light = FindLight(name);
            if (light == null)
            {
                GameObject gameObject = new GameObject(name);
                light = gameObject.AddComponent<Light>();
            }

            light.type = LightType.Point;
            light.transform.position = position;
            return light;
        }

        private static void DeleteSceneObject(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        private static void DeleteLooseGeneratedObject(string name)
        {
            GameObject modelRoot = FindModelRoot();
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject == null || !gameObject.scene.IsValid() || gameObject.name != name)
                {
                    continue;
                }

                if (modelRoot != null && gameObject.transform.IsChildOf(modelRoot.transform))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static MaterialSet LoadMaterials()
        {
            return new MaterialSet
            {
                Concrete = GetOrCreateMaterial("MAT_Concrete", new Color(0.42f, 0.42f, 0.38f), 0f, 0.18f, false),
                Asphalt = GetOrCreateMaterial("MAT_Industrial_Asphalt", new Color(0.055f, 0.058f, 0.06f), 0f, 0.22f, false),
                DarkMetal = GetOrCreateMaterial("MAT_Metal_Dark", new Color(0.20f, 0.22f, 0.23f), 0.65f, 0.34f, false),
                LightMetal = GetOrCreateMaterial("MAT_Metal_Light_Gray", new Color(0.56f, 0.58f, 0.58f), 0.55f, 0.38f, false),
                SafetyYellow = GetOrCreateMaterial("MAT_Yellow_Railing", new Color(0.95f, 0.68f, 0.08f), 0.25f, 0.32f, false),
                PipeGas = GetOrCreateMaterial("MAT_Pipe_Gas", new Color(0.43f, 0.46f, 0.48f), 0.48f, 0.35f, false),
                PipeBlue = GetOrCreateMaterial("MAT_Pipe_Blue", new Color(0.12f, 0.32f, 0.72f), 0.35f, 0.38f, false),
                LampGlow = GetOrCreateMaterial("MAT_Lamp_Glow", new Color(1f, 0.82f, 0.42f), 0f, 0.2f, true)
            };
        }

        private static Material GetOrCreateMaterial(string name, Color color, float metallic, float smoothness, bool emission)
        {
            string path = "Assets/Materials/FlareSystem/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(ProjectPath("Assets/Materials/FlareSystem"));
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Standard");
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.4f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void WriteReport(Bounds modelBounds)
        {
            string fullPath = ProjectPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath,
                "Unity camera and environment fix report\n" +
                "Scene: " + ScenePath + "\n" +
                "Backup: " + BackupPath + "\n\n" +
                "Camera:\n" +
                "- Main Camera now uses near clip 0.1, far clip 1000, FOV from camera presets.\n" +
                "- CameraController has six smooth presets: Overview, Separator, Flare Stack, Flare Top / Flame, Piping / Flows, UI Overview.\n" +
                "- Mouse controls: RMB orbit, wheel zoom, MMB or Alt+LMB pan, with distance and pitch limits.\n" +
                "- Mouse camera input is ignored while the pointer is over Unity UI.\n\n" +
                "Environment:\n" +
                "- EnvironmentRoot created with ConcreteBase, AsphaltRoad, SafetyFence, LightPoles, BackgroundStorageTanks, BackgroundPipeRack and UtilityDetails.\n" +
                "- Environment is made from Unity primitives and Built-in Standard materials only.\n" +
                "- Environment colliders were removed to avoid blocking sensor hover and flare stack clicks.\n\n" +
                "Lighting:\n" +
                "- Ambient light and directional/fill intensities were balanced for non-overexposed industrial metal.\n\n" +
                "Model bounds used for framing: center " + modelBounds.center + ", size " + modelBounds.size + "\n" +
                "Screenshots target folder: " + ProjectPath(ScreenshotFolder) + "\n");
        }

        private struct MaterialSet
        {
            public Material Concrete;
            public Material Asphalt;
            public Material DarkMetal;
            public Material LightMetal;
            public Material SafetyYellow;
            public Material PipeGas;
            public Material PipeBlue;
            public Material LampGlow;
        }
    }
}
