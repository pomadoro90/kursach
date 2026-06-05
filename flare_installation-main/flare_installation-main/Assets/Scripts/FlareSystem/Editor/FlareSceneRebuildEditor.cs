using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlareSystem.Editor
{
    public static class FlareSceneRebuildEditor
    {
        public const string OldScenePath = "Assets/Scenes/FlareInstallationScene.unity";
        public const string BackupScenePath = "Assets/Scenes/FlareInstallationScene_Before_Rebuild_Backup.unity";
        public const string RebuiltScenePath = "Assets/Scenes/FlareInstallationScene_Rebuilt.unity";
        public const string ModelPath = "Assets/Models/combined_flare_installation.fbx";
        public const string CsvPath = "Assets/StreamingAssets/variant_3_15.csv";
        public const string MaterialFolder = "Assets/Materials/FlareSystem";

        public static void RebuildScene()
        {
            EnsureBackup();
            EnsureFolders();
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "FlareInstallationScene_Rebuilt";
            ConfigureRenderSettings();

            MaterialSet materials = CreateMaterials();

            GameObject root = new GameObject("FlareSystemRoot");
            GameObject importedRoot = new GameObject("ImportedModelRoot");
            importedRoot.transform.SetParent(root.transform, false);

            GameObject model = InstantiateModel(importedRoot.transform);
            Bounds modelBounds = NormalizeModelToGround(model, importedRoot.transform);
            AssignIndustrialMaterials(model, materials);

            Transform sceneCenter = CreateSceneCenter(root.transform, modelBounds);
            Vector3 flareTop = EstimateFlareTop(model, modelBounds);
            Vector3 separatorPoint = EstimateSeparatorPoint(modelBounds, flareTop);

            CreateGround(modelBounds, materials);
            Camera mainCamera = CreateCamera(modelBounds, sceneCenter);
            CreateScreenshotCamera(modelBounds, sceneCenter);
            Light directional = CreateDirectionalLight();
            CreateFillLights(modelBounds);
            Light alarmLight = CreateAlarmLight(flareTop);

            FlareInstallationController controller = root.AddComponent<FlareInstallationController>();
            ArchiveModeController archive = root.AddComponent<ArchiveModeController>();
            EarlyWarningController warning = root.AddComponent<EarlyWarningController>();
            LogisticRegressionRiskModel risk = root.AddComponent<LogisticRegressionRiskModel>();
            AlarmVisualController alarm = root.AddComponent<AlarmVisualController>();
            ValveAnimator valveAnimator = root.AddComponent<ValveAnimator>();

            GameObject flameObject = CreateFlameEffect(root.transform, flareTop, materials, out FlameController flame);
            GameObject flowRoot = CreateFlowSystem(root.transform, modelBounds, separatorPoint, flareTop, materials, out PipeFlowController pipeFlow);
            List<SensorIndicator> sensors = CreateSensorMarkers(root.transform, modelBounds, flareTop, separatorPoint, materials);
            CreateValveFallback(root.transform, modelBounds, materials, valveAnimator);
            CreateFlareClickTarget(root.transform, flareTop, modelBounds, controller);

            Canvas canvas = CreateUi(controller, archive, warning, alarm, sensors, out FlareUIController ui, out SensorTooltipController tooltip, out RiskPanelController riskPanel);
            EnsureEventSystem();

            controller.archiveModeController = archive;
            controller.earlyWarningController = warning;
            controller.riskModel = risk;
            controller.flameController = flame;
            controller.pipeFlowController = pipeFlow;
            controller.valveAnimator = valveAnimator;
            controller.alarmVisualController = alarm;
            controller.uiController = ui;
            controller.riskPanelController = riskPanel;
            controller.sensorIndicators = sensors;

            archive.controller = controller;
            warning.controller = controller;
            warning.riskModel = risk;
            warning.pPurgeSlider = ui.pPurgeSlider;
            warning.qPurgeSlider = ui.qPurgeSlider;
            warning.pFlareSlider = ui.pFlareSlider;
            alarm.alarmLight = alarmLight;
            alarm.alarmText = ui.bottomAlarmText;
            alarm.uiAlarmImage = ui.alarmIndicatorImage;
            tooltip.raycastCamera = mainCamera;

            flame.EnsureEffect();
            pipeFlow.BuildRuntimeFlows();
            warning.ConfigureSliders();
            controller.EnsureReferences();
            archive.LoadRecords();
            controller.SetArchiveMode();
            riskPanel.Hide();

            CreateScreenshotCaptureMarker(root.transform);

            EditorSceneManager.SaveScene(scene, RebuiltScenePath);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Flare Installation Rebuild: created " + RebuiltScenePath);
        }

        private static void EnsureBackup()
        {
            if (File.Exists(ProjectPath(BackupScenePath)))
            {
                return;
            }

            if (!File.Exists(ProjectPath(OldScenePath)))
            {
                Debug.LogWarning("Old scene not found, backup skipped: " + OldScenePath);
                return;
            }

            File.Copy(ProjectPath(OldScenePath), ProjectPath(BackupScenePath), false);
            string oldMeta = ProjectPath(OldScenePath + ".meta");
            string backupMeta = ProjectPath(BackupScenePath + ".meta");
            if (File.Exists(oldMeta) && !File.Exists(backupMeta))
            {
                File.Copy(oldMeta, backupMeta, false);
            }

            Debug.Log("Backup scene created: " + BackupScenePath);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "FlareSystem");
            EnsureFolder("Assets", "Tests");
            EnsureFolder("Assets/Tests", "EditMode");
            EnsureFolder("Assets/Tests", "PlayMode");
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", "Screenshots", "UnityRebuild"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", "Reports"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", "Logs"));
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void ConfigureRenderSettings()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.36f, 0.38f);
            RenderSettings.ambientIntensity = 0.65f;
            RenderSettings.reflectionIntensity = 0.35f;
            RenderSettings.fog = false;
        }

        private static GameObject InstantiateModel(Transform parent)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null)
            {
                Debug.LogError("FBX model not found: " + ModelPath);
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(asset);
            }

            instance.name = "Combined_Flare_Installation_Model";
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static Bounds NormalizeModelToGround(GameObject model, Transform importedRoot)
        {
            Bounds bounds = CalculateBounds(model);
            importedRoot.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = CalculateBounds(model);

            if (Mathf.Abs(bounds.min.y) > 0.01f)
            {
                importedRoot.position += Vector3.down * bounds.min.y;
                bounds = CalculateBounds(model);
            }

            return bounds;
        }

        public static Bounds CalculateBounds(GameObject root)
        {
            if (root == null)
            {
                return new Bounds(Vector3.zero, new Vector3(12f, 18f, 12f));
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, new Vector3(12f, 18f, 12f));
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static Transform CreateSceneCenter(Transform root, Bounds bounds)
        {
            GameObject center = new GameObject("SceneCenter");
            center.transform.SetParent(root, false);
            center.transform.position = new Vector3(bounds.center.x, Mathf.Max(1.2f, bounds.center.y * 0.45f), bounds.center.z);
            return center.transform;
        }

        private static Vector3 EstimateFlareTop(GameObject model, Bounds bounds)
        {
            Renderer[] renderers = model != null ? model.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            Renderer highest = null;
            foreach (Renderer renderer in renderers)
            {
                if (highest == null || renderer.bounds.max.y > highest.bounds.max.y)
                {
                    highest = renderer;
                }
            }

            if (highest == null)
            {
                return new Vector3(bounds.center.x + bounds.extents.x * 0.35f, bounds.max.y, bounds.center.z);
            }

            Vector3 center = highest.bounds.center;
            return new Vector3(center.x, highest.bounds.max.y + 0.35f, center.z);
        }

        private static Vector3 EstimateSeparatorPoint(Bounds bounds, Vector3 flareTop)
        {
            float x = bounds.center.x - bounds.extents.x * 0.48f;
            float z = bounds.center.z;
            float y = Mathf.Max(1.2f, bounds.min.y + bounds.size.y * 0.18f);
            return new Vector3(x, y, z);
        }

        private static void AssignIndustrialMaterials(GameObject model, MaterialSet materials)
        {
            if (model == null)
            {
                return;
            }

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name.ToLowerInvariant();
                Material material = materials.MetalLightGray;

                if (name.Contains("rail") || name.Contains("handrail") || name.Contains("ladder") || name.Contains("перил"))
                {
                    material = materials.YellowRailing;
                }
                else if (name.Contains("concrete") || name.Contains("foundation") || name.Contains("base") || name.Contains("slab") || name.Contains("бетон"))
                {
                    material = materials.Concrete;
                }
                else if (name.Contains("purge") || name.Contains("pilot") || name.Contains("blue"))
                {
                    material = materials.PipeBlue;
                }
                else if (name.Contains("pipe") || name.Contains("tube") || name.Contains("gas") || name.Contains("steam"))
                {
                    material = materials.PipeGas;
                }
                else if (name.Contains("support") || name.Contains("frame") || name.Contains("leg") || name.Contains("steel"))
                {
                    material = materials.MetalDark;
                }
                else if (name.Contains("black"))
                {
                    material = materials.BlackSteel;
                }

                renderer.sharedMaterial = material;
            }
        }

        private static void CreateGround(Bounds bounds, MaterialSet materials)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "ConcreteBase";
            ground.transform.position = new Vector3(bounds.center.x, -0.06f, bounds.center.z);
            ground.transform.localScale = new Vector3(Mathf.Max(18f, bounds.size.x + 5f), 0.12f, Mathf.Max(14f, bounds.size.z + 5f));
            ground.GetComponent<Renderer>().sharedMaterial = materials.Concrete;
        }

        private static Camera CreateCamera(Bounds bounds, Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.18f, 0.22f, 0.27f);
            camera.fieldOfView = 44f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            camera.allowHDR = false;
            cameraObject.AddComponent<AudioListener>();

            float radius = Mathf.Max(bounds.extents.magnitude, 10f);
            cameraObject.transform.position = bounds.center + new Vector3(radius * 0.85f, radius * 0.42f, -radius * 0.9f);
            cameraObject.transform.LookAt(target.position);

            CameraController controller = cameraObject.AddComponent<CameraController>();
            controller.controlledCamera = camera;
            controller.orbitTarget = target;
            controller.zoomSensitivity = radius * 0.08f;
            controller.panSpeed = Mathf.Max(4f, radius * 0.16f);
            controller.minDistance = radius * 0.18f;
            controller.maxDistance = radius * 2.5f;
            controller.presets.Clear();
            controller.presets.Add(MakePreset("Overview", bounds.center + new Vector3(radius * 0.85f, radius * 0.42f, -radius * 0.9f), target.position, 44f));
            controller.presets.Add(MakePreset("Separator", bounds.center + new Vector3(-radius * 0.75f, radius * 0.22f, -radius * 0.4f), new Vector3(bounds.center.x - bounds.extents.x * 0.4f, bounds.center.y * 0.4f, bounds.center.z), 38f));
            controller.presets.Add(MakePreset("FlareStack", bounds.center + new Vector3(bounds.extents.x * 0.7f, radius * 0.75f, -radius * 0.35f), new Vector3(bounds.center.x + bounds.extents.x * 0.35f, bounds.max.y * 0.75f, bounds.center.z), 34f));
            controller.presets.Add(MakePreset("ControlPanel", bounds.center + new Vector3(-radius * 0.6f, radius * 0.22f, radius * 0.45f), target.position, 40f));
            controller.presets.Add(MakePreset("Piping", bounds.center + new Vector3(0f, radius * 0.35f, -radius * 0.7f), new Vector3(bounds.center.x, bounds.center.y * 0.36f, bounds.center.z), 40f));
            return camera;
        }

        private static void CreateScreenshotCamera(Bounds bounds, Transform target)
        {
            GameObject cameraObject = new GameObject("ScreenshotCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            float radius = Mathf.Max(bounds.extents.magnitude, 10f);
            cameraObject.transform.position = bounds.center + new Vector3(radius * 0.75f, radius * 0.45f, -radius * 0.75f);
            cameraObject.transform.LookAt(target.position);
        }

        private static CameraPreset MakePreset(string name, Vector3 position, Vector3 lookAt, float fov)
        {
            Quaternion rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);
            return new CameraPreset(name, position, rotation.eulerAngles, fov);
        }

        private static Light CreateDirectionalLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.75f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            return light;
        }

        private static void CreateFillLights(Bounds bounds)
        {
            CreatePointLight("Fill Light Left", bounds.center + new Vector3(-bounds.extents.x * 0.8f, bounds.extents.y * 0.45f, -bounds.extents.z * 0.6f), new Color(0.7f, 0.82f, 1f), 0.35f, Mathf.Max(12f, bounds.size.magnitude));
            CreatePointLight("Fill Light Right", bounds.center + new Vector3(bounds.extents.x * 0.8f, bounds.extents.y * 0.35f, bounds.extents.z * 0.45f), new Color(1f, 0.88f, 0.72f), 0.25f, Mathf.Max(10f, bounds.size.magnitude * 0.8f));
        }

        private static Light CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Light CreateAlarmLight(Vector3 flareTop)
        {
            Light light = CreatePointLight("AlarmLight", flareTop + Vector3.up * 1.5f, FlareConstants.DangerColor, 0f, 16f);
            light.enabled = false;
            return light;
        }

        private static GameObject CreateFlameEffect(Transform root, Vector3 flareTop, MaterialSet materials, out FlameController flame)
        {
            GameObject flameControllerObject = new GameObject("FlameController");
            flameControllerObject.transform.SetParent(root, false);
            flame = flameControllerObject.AddComponent<FlameController>();
            flame.normalMaterial = materials.FlameGreen;
            flame.warningMaterial = materials.FlameYellow;
            flame.dangerMaterial = materials.FlameRed;

            GameObject effect = new GameObject("FlameEffect");
            effect.transform.SetParent(flameControllerObject.transform, false);
            effect.transform.position = flareTop + Vector3.up * 0.35f;
            effect.transform.localScale = Vector3.one * 0.95f;
            flame.flameRoot = effect.transform;

            GameObject flameMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            flameMesh.name = "FlameMesh";
            flameMesh.transform.SetParent(effect.transform, false);
            flameMesh.transform.localPosition = Vector3.up * 0.45f;
            flameMesh.transform.localScale = new Vector3(0.28f, 0.52f, 0.28f);
            Collider flameCollider = flameMesh.GetComponent<Collider>();
            if (flameCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(flameCollider);
            }

            Renderer meshRenderer = flameMesh.GetComponent<Renderer>();
            meshRenderer.sharedMaterial = materials.FlameGreen;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            flame.flameRenderer = meshRenderer;

            flame.EnsureEffect();
            if (flame.flameLight != null)
            {
                flame.flameLight.color = new Color(0.24f, 1f, 0.72f);
                flame.flameLight.intensity = 1.4f;
                flame.flameLight.range = 9f;
            }

            return effect;
        }

        private static GameObject CreateFlowSystem(Transform root, Bounds bounds, Vector3 separatorPoint, Vector3 flareTop, MaterialSet materials, out PipeFlowController controller)
        {
            GameObject flowRoot = new GameObject("FlowWaypointsRoot");
            flowRoot.transform.SetParent(root, false);
            controller = flowRoot.AddComponent<PipeFlowController>();
            controller.reliefGasMaterial = materials.FlowRelief;
            controller.purgeGasMaterial = materials.FlowPurge;
            controller.steamMaterial = materials.FlowSteam;
            controller.markersPerPath = 9;

            Vector3 stackBase = new Vector3(flareTop.x, Mathf.Max(1.0f, bounds.size.y * 0.2f), flareTop.z);
            controller.flowPaths.Add(CreateFlowPath(flowRoot.transform, "ReliefGasFlow", "ReliefGas_WP_", FlareConstants.FlowReliefGasColor, 1.2f, 0.08f, materials.FlowRelief, new[]
            {
                separatorPoint + new Vector3(-bounds.extents.x * 0.35f, 0f, 0f),
                separatorPoint + new Vector3(bounds.extents.x * 0.08f, 0.25f, 0f),
                stackBase + new Vector3(0f, 0.45f, 0f)
            }));

            controller.flowPaths.Add(CreateFlowPath(flowRoot.transform, "PurgeGasFlow", "PurgeGas_WP_", FlareConstants.FlowPurgeGasColor, 1.5f, 0.07f, materials.FlowPurge, new[]
            {
                bounds.center + new Vector3(-bounds.extents.x * 0.35f, 0.65f, bounds.extents.z * 0.32f),
                bounds.center + new Vector3(bounds.extents.x * 0.12f, 0.8f, bounds.extents.z * 0.34f),
                stackBase + new Vector3(0f, 0.7f, bounds.extents.z * 0.12f)
            }));

            controller.flowPaths.Add(CreateFlowPath(flowRoot.transform, "SteamFlow", "Steam_WP_", FlareConstants.FlowSteamColor, 1.0f, 0.075f, materials.FlowSteam, new[]
            {
                bounds.center + new Vector3(0f, 0.9f, -bounds.extents.z * 0.35f),
                new Vector3(flareTop.x - bounds.extents.x * 0.08f, Mathf.Lerp(bounds.min.y, flareTop.y, 0.62f), flareTop.z - bounds.extents.z * 0.12f),
                flareTop + new Vector3(0f, -0.35f, 0f)
            }));

            return flowRoot;
        }

        private static PipeFlowPath CreateFlowPath(Transform root, string pathName, string waypointPrefix, Color color, float speed, float size, Material material, Vector3[] points)
        {
            GameObject pathObject = new GameObject(pathName);
            pathObject.transform.SetParent(root, false);
            PipeFlowPath path = pathObject.AddComponent<PipeFlowPath>();
            path.flowName = pathName;
            path.flowColor = color;
            path.speed = speed;
            path.particleSize = size;
            path.loop = true;

            for (int i = 0; i < points.Length; i++)
            {
                GameObject waypoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                waypoint.name = waypointPrefix + (i + 1).ToString("00");
                waypoint.transform.SetParent(pathObject.transform, false);
                waypoint.transform.position = points[i];
                waypoint.transform.localScale = Vector3.one * 0.18f;
                waypoint.GetComponent<Renderer>().sharedMaterial = material;
                UnityEngine.Object.DestroyImmediate(waypoint.GetComponent<Collider>());
                path.waypoints.Add(waypoint.transform);
            }

            LineRenderer line = pathObject.AddComponent<LineRenderer>();
            line.positionCount = points.Length;
            line.useWorldSpace = true;
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;
            line.material = material;
            line.startColor = color;
            line.endColor = color;
            for (int i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, points[i]);
            }

            return path;
        }

        private static List<SensorIndicator> CreateSensorMarkers(Transform root, Bounds bounds, Vector3 flareTop, Vector3 separatorPoint, MaterialSet materials)
        {
            GameObject container = new GameObject("SensorMarkers");
            container.transform.SetParent(root, false);
            var sensors = new List<SensorIndicator>
            {
                CreateSensor(container.transform, "Датчик P_flare", "P_flare", separatorPoint + new Vector3(-0.55f, 0.65f, 0.65f), materials),
                CreateSensor(container.transform, "Датчик Q_flare", "Q_flare", separatorPoint + new Vector3(0.45f, 0.52f, 0.65f), materials),
                CreateSensor(container.transform, "Датчик P_purge", "P_purge", new Vector3(flareTop.x - 0.4f, Mathf.Lerp(bounds.min.y, flareTop.y, 0.55f), flareTop.z + 0.55f), materials),
                CreateSensor(container.transform, "Датчик Q_purge", "Q_purge", bounds.center + new Vector3(bounds.extents.x * 0.05f, 0.85f, bounds.extents.z * 0.55f), materials),
                CreateSensor(container.transform, "Датчик T_flame", "T_flame", flareTop + new Vector3(0.45f, -0.55f, 0.25f), materials),
                CreateSensor(container.transform, "Датчик Steam_Q", "Steam_Q", new Vector3(flareTop.x - 0.9f, Mathf.Lerp(bounds.min.y, flareTop.y, 0.72f), flareTop.z - 0.45f), materials),
                CreateSensor(container.transform, "Датчик Level", "Level", bounds.center + new Vector3(-bounds.extents.x * 0.55f, 0.65f, -bounds.extents.z * 0.35f), materials)
            };
            return sensors;
        }

        private static SensorIndicator CreateSensor(Transform parent, string sensorName, string key, Vector3 position, MaterialSet materials)
        {
            GameObject sensorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sensorObject.name = "Sensor_" + key;
            sensorObject.transform.SetParent(parent, false);
            sensorObject.transform.position = position;
            sensorObject.transform.localScale = Vector3.one * 0.28f;
            sensorObject.GetComponent<Renderer>().sharedMaterial = materials.SensorGreen;

            Light light = CreatePointLight(sensorObject.name + "_Light", position, FlareConstants.NormalColor, 0.65f, 2.4f);
            light.transform.SetParent(sensorObject.transform, true);

            SensorIndicator indicator = sensorObject.AddComponent<SensorIndicator>();
            indicator.sensorName = sensorName;
            indicator.parameterKey = key;
            indicator.normalRangeText = FlareConstants.GetNormalRangeText(key);
            indicator.indicatorRenderer = sensorObject.GetComponent<Renderer>();
            indicator.indicatorLight = light;
            indicator.normalMaterial = materials.SensorGreen;
            indicator.warningMaterial = materials.SensorYellow;
            indicator.alarmMaterial = materials.SensorRed;

            SensorHoverTarget hover = sensorObject.AddComponent<SensorHoverTarget>();
            hover.indicator = indicator;
            return indicator;
        }

        private static void CreateValveFallback(Transform root, Bounds bounds, MaterialSet materials, ValveAnimator animator)
        {
            GameObject valve = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            valve.name = "DemoValve_Handwheel";
            valve.transform.SetParent(root, false);
            valve.transform.position = bounds.center + new Vector3(-bounds.extents.x * 0.15f, 0.85f, bounds.extents.z * 0.62f);
            valve.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            valve.transform.localScale = new Vector3(0.42f, 0.055f, 0.42f);
            valve.GetComponent<Renderer>().sharedMaterial = materials.YellowRailing;
            UnityEngine.Object.DestroyImmediate(valve.GetComponent<Collider>());
            animator.handwheels.Clear();
            animator.handwheels.Add(valve.transform);
            animator.rotationSpeed = 150f;
        }

        private static void CreateFlareClickTarget(Transform root, Vector3 flareTop, Bounds bounds, FlareInstallationController controller)
        {
            GameObject target = new GameObject("FlareStack_Click_Target");
            target.transform.SetParent(root, false);
            target.transform.position = new Vector3(flareTop.x, Mathf.Max(1.2f, flareTop.y * 0.45f), flareTop.z);
            BoxCollider collider = target.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.8f, Mathf.Max(6f, flareTop.y), 1.8f);
            collider.center = Vector3.zero;
            FlareStackClickTarget click = target.AddComponent<FlareStackClickTarget>();
            click.controller = controller;
            click.flareStackObject = target;
        }

        private static Canvas CreateUi(
            FlareInstallationController controller,
            ArchiveModeController archive,
            EarlyWarningController warning,
            AlarmVisualController alarm,
            List<SensorIndicator> sensors,
            out FlareUIController ui,
            out SensorTooltipController tooltip,
            out RiskPanelController riskPanel)
        {
            GameObject canvasObject = new GameObject("UI Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            ui = canvasObject.AddComponent<FlareUIController>();
            ui.controller = controller;

            RectTransform rootRect = canvasObject.GetComponent<RectTransform>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            GameObject top = Panel(rootRect, "TopPanel", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), Vector2.zero, new Color(0.055f, 0.065f, 0.075f, 0.94f));
            HorizontalLayoutGroup topLayout = top.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(18, 18, 10, 10);
            topLayout.spacing = 12;
            topLayout.childControlHeight = true;
            topLayout.childControlWidth = false;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            ui.titleText = Text(top.transform, font, "Факельная установка — интерактивная модель", 25, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(610f, 50f));
            ui.archiveModeButton = Button(top.transform, font, "BtnArchiveMode", "Архив", new Vector2(145f, 44f));
            ui.earlyWarningModeButton = Button(top.transform, font, "BtnWarningMode", "Раннее предупреждение", new Vector2(260f, 44f));
            ui.modeText = Text(top.transform, font, "Режим: Архив", 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.75f, 0.9f, 1f), new Vector2(240f, 44f));
            ui.statusImage = Image(top.transform, "StatusDot", FlareConstants.NormalColor, new Vector2(36f, 36f));
            ui.statusText = Text(top.transform, font, "Статус: Норма", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(230f, 44f));

            ui.archivePanel = Panel(rootRect, "ArchivePanel", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 156f), new Vector2(500f, -88f), new Color(0.07f, 0.085f, 0.1f, 0.9f));
            VerticalLayoutGroup archiveLayout = Vertical(ui.archivePanel, 16, 10);
            Text(ui.archivePanel.transform, font, "Архив", 22, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(448f, 34f));
            GameObject archiveButtons = Row(ui.archivePanel.transform, "ArchiveButtons", 44f);
            ui.previousButton = Button(archiveButtons.transform, font, "BtnPrevious", "Назад", new Vector2(132f, 40f));
            ui.nextButton = Button(archiveButtons.transform, font, "BtnNext", "Вперёд", new Vector2(132f, 40f));
            ui.playPauseButton = Button(archiveButtons.transform, font, "BtnPlayPause", "Пуск/Пауза", new Vector2(150f, 40f));
            ui.playPauseButtonText = ui.playPauseButton.GetComponentInChildren<Text>();
            ui.recordSlider = Slider(ui.archivePanel.transform, "RecordSlider", new Vector2(448f, 34f));
            ui.recordIndexText = Text(ui.archivePanel.transform, font, "Запись 1 / 100", 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.92f, 1f), new Vector2(448f, 30f));
            ui.archiveValuesText = Text(ui.archivePanel.transform, font, "P_flare: —\nQ_flare: —\nP_purge: —\nQ_purge: —\nT_flame: —\nSteam_Q: —\notriv: —\nhlopok: —", 17, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, new Vector2(448f, 250f));
            ui.archiveViolationsText = Text(ui.archivePanel.transform, font, "Нарушений нет", 16, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 0.86f, 0.38f), new Vector2(448f, 92f));
            ui.archiveRecommendationText = Text(ui.archivePanel.transform, font, "Параметры в допустимых пределах", 16, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.74f, 0.94f, 1f), new Vector2(448f, 92f));
            archiveLayout.enabled = true;

            ui.warningPanel = Panel(rootRect, "WarningPanel", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-500f, 156f), new Vector2(-16f, -88f), new Color(0.07f, 0.085f, 0.1f, 0.9f));
            VerticalLayoutGroup warningLayout = Vertical(ui.warningPanel, 16, 11);
            Text(ui.warningPanel.transform, font, "Раннее предупреждение", 22, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(448f, 34f));
            ui.pPurgeSlider = SliderRow(ui.warningPanel.transform, font, "P_purge", "P_purge", "0.350 МПа", out ui.pPurgeValueText);
            ui.qPurgeSlider = SliderRow(ui.warningPanel.transform, font, "Q_purge", "Q_purge", "30.0 м3/ч", out ui.qPurgeValueText);
            ui.pFlareSlider = SliderRow(ui.warningPanel.transform, font, "P_flare", "P_flare", "0.012 МПа", out ui.pFlareValueText);
            ui.probabilityText = Text(ui.warningPanel.transform, font, "Вероятность отрыва: 0.0%", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(448f, 30f));
            ui.riskBarFill = RiskBar(ui.warningPanel.transform);
            ui.riskZoneText = Text(ui.warningPanel.transform, font, "Зона риска: норма", 17, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(448f, 30f));
            ui.warningRecommendationText = Text(ui.warningPanel.transform, font, "Параметры в допустимых пределах", 16, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.74f, 0.94f, 1f), new Vector2(448f, 110f));
            ui.alarmIndicatorImage = Image(ui.warningPanel.transform, "AlarmIndicator", new Color(1f, 0.15f, 0.08f, 0.9f), new Vector2(448f, 30f));
            ui.alarmIndicatorImage.enabled = false;
            warningLayout.enabled = true;
            ui.warningPanel.SetActive(false);

            GameObject bottom = Panel(rootRect, "BottomPanel", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 16f), new Vector2(-16f, 138f), new Color(0.055f, 0.065f, 0.075f, 0.92f));
            HorizontalLayoutGroup bottomLayout = bottom.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.padding = new RectOffset(16, 16, 12, 12);
            bottomLayout.spacing = 16;
            bottomLayout.childControlHeight = true;
            bottomLayout.childControlWidth = false;
            ui.bottomViolationsText = Text(bottom.transform, font, "Нарушений нет", 16, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 0.88f, 0.42f), new Vector2(500f, 104f));
            ui.bottomRecommendationText = Text(bottom.transform, font, "Параметры в допустимых пределах", 16, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.74f, 0.94f, 1f), new Vector2(700f, 104f));
            ui.bottomAlarmText = Text(bottom.transform, font, "", 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.22f, 0.14f), new Vector2(560f, 104f));

            GameObject tooltipPanel = Panel(rootRect, "TooltipPanel", new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(320f, 132f), new Color(0.015f, 0.02f, 0.025f, 0.95f));
            tooltipPanel.SetActive(false);
            Text tooltipText = Text(tooltipPanel.transform, font, "", 15, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, new Vector2(296f, 110f));
            tooltip = canvasObject.AddComponent<SensorTooltipController>();
            tooltip.tooltipPanel = tooltipPanel;
            tooltip.tooltipText = tooltipText;
            tooltip.tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            ui.tooltipPanel = tooltipPanel;

            GameObject riskPanelObject = Panel(rootRect, "RiskPanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-468f, -210f), new Vector2(-16f, 210f), new Color(0.06f, 0.07f, 0.08f, 0.95f));
            VerticalLayoutGroup riskLayout = Vertical(riskPanelObject, 16, 10);
            Text(riskPanelObject.transform, font, "Панель риска факельной трубы", 21, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(420f, 34f));
            riskPanel = canvasObject.AddComponent<RiskPanelController>();
            riskPanel.panel = riskPanelObject;
            riskPanel.modeText = Text(riskPanelObject.transform, font, "Режим: Архив", 16, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, new Vector2(420f, 28f));
            riskPanel.parametersText = Text(riskPanelObject.transform, font, "Параметры не выбраны", 16, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, new Vector2(420f, 84f));
            riskPanel.probabilityText = Text(riskPanelObject.transform, font, "Вероятность отрыва: 0.0%", 16, FontStyle.Bold, TextAnchor.UpperLeft, Color.white, new Vector2(420f, 58f));
            riskPanel.riskZoneImage = Image(riskPanelObject.transform, "RiskZoneColor", FlareConstants.NormalColor, new Vector2(420f, 24f));
            riskPanel.recommendationText = Text(riskPanelObject.transform, font, "Параметры в допустимых пределах", 16, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.74f, 0.94f, 1f), new Vector2(420f, 98f));
            riskPanel.flameStateText = Text(riskPanelObject.transform, font, "Пламя: Normal", 16, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, new Vector2(420f, 28f));
            riskPanel.closeButton = Button(riskPanelObject.transform, font, "BtnCloseRisk", "Закрыть", new Vector2(138f, 38f));
            riskLayout.enabled = true;
            riskPanelObject.SetActive(false);
            ui.riskPanel = riskPanelObject;

            ui.BindButtons();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            Type inputSystemUiModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemUiModule != null)
            {
                eventSystem.AddComponent(inputSystemUiModule);
            }
            else
            {
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }

        private static GameObject Panel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = panel.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        private static VerticalLayoutGroup Vertical(GameObject panel, int padding, int spacing)
        {
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static GameObject Row(Transform parent, string name, float height)
        {
            GameObject row = new GameObject(name);
            row.transform.SetParent(parent, false);
            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(448f, height);
            LayoutElement element = row.AddComponent<LayoutElement>();
            element.preferredWidth = 448f;
            element.preferredHeight = height;
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            return row;
        }

        private static Text Text(Transform parent, Font font, string value, int size, FontStyle style, TextAnchor anchor, Color color, Vector2 dimensions)
        {
            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.sizeDelta = dimensions;
            LayoutElement element = textObject.AddComponent<LayoutElement>();
            element.preferredWidth = dimensions.x;
            element.preferredHeight = dimensions.y;
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button Button(Transform parent, Font font, string name, string label, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            LayoutElement element = buttonObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.17f, 0.23f, 0.29f, 0.98f);
            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.25f, 0.34f, 0.42f, 1f);
            colors.pressedColor = new Color(0.1f, 0.15f, 0.2f, 1f);
            button.colors = colors;
            Text labelText = Text(buttonObject.transform, font, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, size);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 2f);
            labelRect.offsetMax = new Vector2(-6f, -2f);
            return button;
        }

        private static Slider Slider(Transform parent, string name, Vector2 size)
        {
            GameObject sliderObject = new GameObject(name);
            sliderObject.transform.SetParent(parent, false);
            RectTransform rect = sliderObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            LayoutElement element = sliderObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            Slider slider = sliderObject.AddComponent<Slider>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;

            Image background = Image(sliderObject.transform, "Background", new Color(0.18f, 0.2f, 0.22f, 1f), new Vector2(size.x, 12f));
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(1f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;

            Image fill = Image(sliderObject.transform, "Fill", FlareConstants.NormalColor, new Vector2(size.x, 12f));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;

            Image handle = Image(sliderObject.transform, "Handle", Color.white, new Vector2(22f, 22f));
            slider.targetGraphic = handle;
            slider.fillRect = fillRect;
            slider.handleRect = handle.GetComponent<RectTransform>();
            return slider;
        }

        private static Slider SliderRow(Transform parent, Font font, string name, string label, string value, out Component valueText)
        {
            GameObject row = new GameObject("Row_" + name);
            row.transform.SetParent(parent, false);
            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(448f, 74f);
            LayoutElement rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredWidth = 448f;
            rowElement.preferredHeight = 74f;
            VerticalLayoutGroup layout = row.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            GameObject labelRow = Row(row.transform, "Labels_" + name, 28f);
            Text(labelRow.transform, font, label, 16, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(210f, 26f));
            valueText = Text(labelRow.transform, font, value, 16, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.74f, 0.94f, 1f), new Vector2(210f, 26f));
            return Slider(row.transform, "Slider_" + name, new Vector2(428f, 32f));
        }

        private static Image Image(Transform parent, string name, Color color, Vector2 size)
        {
            GameObject imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            LayoutElement element = imageObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Image RiskBar(Transform parent)
        {
            GameObject bar = new GameObject("RiskBar");
            bar.transform.SetParent(parent, false);
            RectTransform rect = bar.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(448f, 26f);
            LayoutElement element = bar.AddComponent<LayoutElement>();
            element.preferredWidth = 448f;
            element.preferredHeight = 26f;
            Image background = bar.AddComponent<Image>();
            background.color = new Color(0.18f, 0.2f, 0.22f, 1f);

            Image fill = Image(bar.transform, "RiskBarFill", FlareConstants.NormalColor, new Vector2(448f, 26f));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            return fill;
        }

        private static MaterialSet CreateMaterials()
        {
            MaterialSet set = new MaterialSet
            {
                MetalLightGray = Material("MAT_Metal_Light_Gray", new Color(0.56f, 0.58f, 0.58f), 0.55f, 0.38f, false),
                MetalDark = Material("MAT_Metal_Dark", new Color(0.20f, 0.22f, 0.23f), 0.65f, 0.34f, false),
                BlackSteel = Material("MAT_Black_Steel", new Color(0.06f, 0.065f, 0.07f), 0.7f, 0.28f, false),
                YellowRailing = Material("MAT_Yellow_Railing", new Color(0.95f, 0.68f, 0.08f), 0.25f, 0.32f, false),
                Concrete = Material("MAT_Concrete", new Color(0.42f, 0.42f, 0.38f), 0f, 0.18f, false),
                PipeGas = Material("MAT_Pipe_Gas", new Color(0.43f, 0.46f, 0.48f), 0.48f, 0.35f, false),
                PipeBlue = Material("MAT_Pipe_Blue", new Color(0.12f, 0.32f, 0.72f), 0.35f, 0.38f, false),
                FlameGreen = Material("MAT_Flame_Green", new Color(0.2f, 1f, 0.62f), 0f, 0.45f, true),
                FlameYellow = Material("MAT_Flame_Yellow", new Color(1f, 0.78f, 0.12f), 0f, 0.45f, true),
                FlameRed = Material("MAT_Flame_Red", new Color(1f, 0.16f, 0.08f), 0f, 0.45f, true),
                SensorGreen = Material("MAT_Sensor_Green", FlareConstants.NormalColor, 0f, 0.5f, true),
                SensorYellow = Material("MAT_Sensor_Yellow", FlareConstants.WarningColor, 0f, 0.5f, true),
                SensorRed = Material("MAT_Sensor_Red", FlareConstants.DangerColor, 0f, 0.5f, true),
                UiDark = Material("MAT_UI_DarkPanel", new Color(0.055f, 0.065f, 0.075f), 0f, 0.2f, false),
                FlowRelief = Material("MAT_Flow_ReliefGas", FlareConstants.FlowReliefGasColor, 0f, 0.4f, true),
                FlowPurge = Material("MAT_Flow_PurgeGas", FlareConstants.FlowPurgeGasColor, 0f, 0.4f, true),
                FlowSteam = Material("MAT_Flow_Steam", FlareConstants.FlowSteamColor, 0f, 0.4f, true)
            };
            return set;
        }

        private static Material Material(string name, Color color, float metallic, float smoothness, bool emission)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
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
                material.SetColor("_EmissionColor", color * 1.35f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateScreenshotCaptureMarker(Transform root)
        {
            GameObject marker = new GameObject("ScreenshotCapture");
            marker.transform.SetParent(root, false);
        }

        private static void EnsureSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == RebuiltScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(RebuiltScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            else
            {
                foreach (EditorBuildSettingsScene scene in scenes)
                {
                    if (scene.path == RebuiltScenePath)
                    {
                        scene.enabled = true;
                    }
                }

                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static string ProjectPath(string assetPath)
        {
            string relative = assetPath.StartsWith("Assets", StringComparison.Ordinal) ? assetPath.Substring("Assets".Length).TrimStart('/', '\\') : assetPath;
            return Path.Combine(Application.dataPath, relative);
        }

        private struct MaterialSet
        {
            public Material MetalLightGray;
            public Material MetalDark;
            public Material BlackSteel;
            public Material YellowRailing;
            public Material Concrete;
            public Material PipeGas;
            public Material PipeBlue;
            public Material FlameGreen;
            public Material FlameYellow;
            public Material FlameRed;
            public Material SensorGreen;
            public Material SensorYellow;
            public Material SensorRed;
            public Material UiDark;
            public Material FlowRelief;
            public Material FlowPurge;
            public Material FlowSteam;
        }
    }
}
