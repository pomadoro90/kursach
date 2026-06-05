using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlareSystem.Editor
{
    public static class FlareSceneSetupEditor
    {
        private const string ScenePath = "Assets/Scenes/FlareInstallationScene.unity";
        private const string ModelPath = "Assets/Models/combined_flare_installation.fbx";
        private const string CsvPath = "Assets/StreamingAssets/variant_3_15.csv";
        private const string MaterialFolder = "Assets/Materials/FlareSystem";
        private const string RootName = "FlareSystemRoot";

        public static void SetupSceneFromMenu()
        {
            EnsureFolders();
            AssetDatabase.Refresh();

            Scene scene = File.Exists(ToProjectFullPath(ScenePath))
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            Materials materials = EnsureMaterials();
            GameObject root = FindOrCreateRoot();

            FlareInstallationController controller = GetOrAdd<FlareInstallationController>(root);
            ArchiveModeController archive = GetOrAdd<ArchiveModeController>(root);
            EarlyWarningController warning = GetOrAdd<EarlyWarningController>(root);
            LogisticRegressionRiskModel riskModel = GetOrAdd<LogisticRegressionRiskModel>(root);
            FlareObjectResolver resolver = GetOrAdd<FlareObjectResolver>(root);
            PipeFlowController pipeFlow = GetOrAdd<PipeFlowController>(root);
            ValveAnimator valveAnimator = GetOrAdd<ValveAnimator>(root);
            AlarmVisualController alarm = GetOrAdd<AlarmVisualController>(root);

            GameObject modelInstance = EnsureModelInstance(root.transform);
            resolver.modelRoot = modelInstance != null ? modelInstance.transform : null;
            Bounds bounds = CalculateBounds(modelInstance);

            Camera mainCamera = EnsureCamera(bounds, out CameraController cameraController);
            Light directionalLight = EnsureDirectionalLight();
            Light alarmLight = EnsureAlarmLight(bounds);

            FlameController flame = EnsureFlameController(root.transform, resolver, bounds, materials);
            List<PipeFlowPath> paths = EnsurePipeFlows(root.transform, bounds);
            List<SensorIndicator> sensors = EnsureSensorIndicators(root.transform, bounds, materials);
            EnsureFlareStackClickTarget(resolver, bounds, controller);
            EnsureValveTargets(root.transform, resolver, bounds, valveAnimator, materials);

            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();
            FlareUIController ui = BuildUi(canvas, controller, archive, warning, alarm, out SensorTooltipController tooltip, out RiskPanelController riskPanel);

            controller.archiveModeController = archive;
            controller.earlyWarningController = warning;
            controller.riskModel = riskModel;
            controller.flameController = flame;
            controller.pipeFlowController = pipeFlow;
            controller.valveAnimator = valveAnimator;
            controller.alarmVisualController = alarm;
            controller.uiController = ui;
            controller.riskPanelController = riskPanel;
            controller.sensorIndicators = sensors;

            archive.controller = controller;
            warning.controller = controller;
            warning.riskModel = riskModel;
            warning.pPurgeSlider = ui.pPurgeSlider;
            warning.qPurgeSlider = ui.qPurgeSlider;
            warning.pFlareSlider = ui.pFlareSlider;

            pipeFlow.flowPaths = paths;
            pipeFlow.BuildRuntimeFlows();

            alarm.alarmLight = alarmLight;
            alarm.uiAlarmImage = ui.alarmIndicatorImage;
            alarm.alarmText = ui.statusText;

            tooltip.raycastCamera = mainCamera;
            riskPanel.UpdatePanel(FlareMode.Archive, null, 0f, RiskLevel.Normal, "Параметры в допустимых пределах", "Normal");

            if (cameraController != null)
            {
                cameraController.controlledCamera = mainCamera;
                cameraController.orbitTarget = EnsureCameraTarget(root.transform, bounds);
            }

            if (directionalLight == null)
            {
                Debug.LogWarning("Directional Light не создан. Проверьте сцену вручную.");
            }

            MarkDirty(root, canvas.gameObject);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Flare Installation: сцена настроена и сохранена: " + ScenePath);
        }

        public static void ValidateSceneFromMenu()
        {
            if (File.Exists(ToProjectFullPath(ScenePath)) && SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var lines = new List<string>();
            int errors = 0;
            int warnings = 0;

            Check(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null, "модель существует: " + ModelPath, true);
            Check(File.Exists(ToProjectFullPath(CsvPath)), "CSV существует: " + CsvPath, true);
            Check(FindObject<FlareInstallationController>() != null, "главный контроллер существует", true);
            Check(FindObject<FlareUIController>() != null, "UI существует", true);
            Check(FindObject<FlameController>() != null, "FlameController существует", true);
            Check(FindObject<ArchiveModeController>() != null, "Archive Mode существует", true);
            Check(FindObject<EarlyWarningController>() != null, "Early Warning Mode существует", true);
            Check(Camera.main != null || FindObject<Camera>() != null, "камера существует", true);
            Check(FindObjects<Light>().Length > 0, "источники света существуют", true);
            Check(FindObjects<PipeFlowPath>().Length > 0, "есть хотя бы один pipe flow", true);
            Check(FindObject<SensorTooltipController>() != null, "tooltip system существует", true);
            Check(FindObject<RiskPanelController>() != null, "risk panel существует", true);

            int missingScripts = CountMissingScripts();
            Check(missingScripts == 0, "нет missing script references", true, missingScripts + " missing scripts");

            string report = "Flare Installation Validate Scene\n" + string.Join("\n", lines);
            if (errors > 0)
            {
                Debug.LogError(report);
            }
            else if (warnings > 0)
            {
                Debug.LogWarning(report);
            }
            else
            {
                Debug.Log(report);
            }

            void Check(bool condition, string message, bool critical, string details = null)
            {
                if (condition)
                {
                    lines.Add("[OK] " + message);
                }
                else
                {
                    lines.Add((critical ? "[ERROR] " : "[WARN] ") + message + (string.IsNullOrWhiteSpace(details) ? string.Empty : ": " + details));
                    if (critical)
                    {
                        errors++;
                    }
                    else
                    {
                        warnings++;
                    }
                }
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Models");
            EnsureFolder("Assets", "StreamingAssets");
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "FlareSystem");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "FlareSystem");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static GameObject FindOrCreateRoot()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
            }

            return root;
        }

        private static GameObject EnsureModelInstance(Transform root)
        {
            GameObject existing = GameObject.Find("Combined_Flare_Installation_Model");
            if (existing != null)
            {
                return existing;
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null)
            {
                Debug.LogWarning("FBX модель не найдена в Unity-проекте: " + ModelPath + ". Setup Scene создаст учебные маркеры, модель можно добавить позже.");
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(asset);
            }

            instance.name = "Combined_Flare_Installation_Model";
            instance.transform.SetParent(root, false);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static Bounds CalculateBounds(GameObject model)
        {
            if (model == null)
            {
                return new Bounds(Vector3.zero, new Vector3(12f, 8f, 12f));
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(model.transform.position, new Vector3(12f, 8f, 12f));
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.magnitude < 0.01f)
            {
                bounds.size = new Vector3(12f, 8f, 12f);
            }

            return bounds;
        }

        private static Camera EnsureCamera(Bounds bounds, out CameraController cameraController)
        {
            GameObject cameraObject = GameObject.FindWithTag("MainCamera");
            if (cameraObject == null)
            {
                cameraObject = GameObject.Find("Main Camera");
            }

            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
            }

            Camera camera = GetOrAdd<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;

            Vector3 center = bounds.center;
            float radius = Mathf.Max(bounds.extents.magnitude, 8f);
            camera.transform.position = center + new Vector3(radius * 0.7f, radius * 0.45f, -radius * 0.9f);
            camera.transform.LookAt(center + Vector3.up * radius * 0.12f);

            cameraController = GetOrAdd<CameraController>(cameraObject);
            cameraController.controlledCamera = camera;
            cameraController.presets.Clear();
            cameraController.presets.Add(new CameraPreset("Общий вид", camera.transform.position, camera.transform.rotation.eulerAngles, 48f));
            cameraController.presets.Add(new CameraPreset("Сепаратор", center + new Vector3(-radius * 0.55f, radius * 0.28f, -radius * 0.45f), new Vector3(24f, -22f, 0f), 42f));
            cameraController.presets.Add(new CameraPreset("Факельная труба", center + new Vector3(radius * 0.35f, radius * 0.65f, -radius * 0.45f), new Vector3(36f, -30f, 0f), 38f));
            cameraController.presets.Add(new CameraPreset("Панель управления", center + new Vector3(-radius * 0.6f, radius * 0.25f, radius * 0.25f), new Vector3(24f, -130f, 0f), 44f));
            cameraController.presets.Add(new CameraPreset("Трубопроводы", center + new Vector3(0f, radius * 0.35f, -radius * 0.75f), new Vector3(30f, 0f, 0f), 46f));

            return camera;
        }

        private static Transform EnsureCameraTarget(Transform root, Bounds bounds)
        {
            Transform target = root.Find("Camera_Target");
            if (target == null)
            {
                target = new GameObject("Camera_Target").transform;
                target.SetParent(root, false);
            }

            target.position = bounds.center;
            return target;
        }

        private static Light EnsureDirectionalLight()
        {
            Light[] lights = FindObjects<Light>();
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light directional = lightObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.1f;
            directional.color = new Color(1f, 0.96f, 0.88f);
            return directional;
        }

        private static Light EnsureAlarmLight(Bounds bounds)
        {
            GameObject alarmObject = GameObject.Find("Alarm Light");
            if (alarmObject == null)
            {
                alarmObject = new GameObject("Alarm Light");
            }

            alarmObject.transform.position = bounds.center + Vector3.up * Mathf.Max(4f, bounds.extents.y * 1.2f);
            Light alarm = GetOrAdd<Light>(alarmObject);
            alarm.type = LightType.Point;
            alarm.color = FlareConstants.DangerColor;
            alarm.range = 18f;
            alarm.intensity = 0f;
            alarm.enabled = false;
            return alarm;
        }

        private static FlameController EnsureFlameController(Transform root, FlareObjectResolver resolver, Bounds bounds, Materials materials)
        {
            FlameController flame = root.GetComponentInChildren<FlameController>(true);
            if (flame == null)
            {
                GameObject flameControllerObject = new GameObject("FlameController");
                flameControllerObject.transform.SetParent(root, false);
                flame = flameControllerObject.AddComponent<FlameController>();
            }

            Transform anchor = resolver != null ? resolver.FindFlameAnchor() : null;
            if (anchor == null)
            {
                anchor = flame.transform.Find("FlameEffect");
            }

            if (anchor == null)
            {
                anchor = new GameObject("FlameEffect").transform;
                anchor.SetParent(flame.transform, false);
            }

            Vector3 top = bounds.center + Vector3.up * bounds.extents.y;
            anchor.position = top + Vector3.up * 0.6f;
            flame.flameRoot = anchor;
            flame.normalMaterial = materials.FlameNormal;
            flame.warningMaterial = materials.FlameWarning;
            flame.dangerMaterial = materials.FlameDanger;
            flame.EnsureEffect();
            return flame;
        }

        private static List<PipeFlowPath> EnsurePipeFlows(Transform root, Bounds bounds)
        {
            Transform container = root.Find("PipeFlowPaths");
            if (container == null)
            {
                container = new GameObject("PipeFlowPaths").transform;
                container.SetParent(root, false);
            }

            var paths = new List<PipeFlowPath>
            {
                EnsurePath(container, "ReliefGas", FlareConstants.FlowReliefGasColor, 1.4f, 0.09f, new[]
                {
                    bounds.center + new Vector3(-bounds.extents.x * 0.75f, -bounds.extents.y * 0.18f, 0f),
                    bounds.center + new Vector3(-bounds.extents.x * 0.15f, -bounds.extents.y * 0.12f, 0f),
                    bounds.center + new Vector3(bounds.extents.x * 0.45f, bounds.extents.y * 0.25f, 0f)
                }),
                EnsurePath(container, "PurgeGas", FlareConstants.FlowPurgeGasColor, 1.8f, 0.065f, new[]
                {
                    bounds.center + new Vector3(-bounds.extents.x * 0.5f, -bounds.extents.y * 0.35f, bounds.extents.z * 0.22f),
                    bounds.center + new Vector3(bounds.extents.x * 0.2f, -bounds.extents.y * 0.2f, bounds.extents.z * 0.18f),
                    bounds.center + new Vector3(bounds.extents.x * 0.48f, bounds.extents.y * 0.35f, bounds.extents.z * 0.08f)
                }),
                EnsurePath(container, "Steam", FlareConstants.FlowSteamColor, 1.2f, 0.075f, new[]
                {
                    bounds.center + new Vector3(0f, -bounds.extents.y * 0.28f, -bounds.extents.z * 0.2f),
                    bounds.center + new Vector3(bounds.extents.x * 0.35f, bounds.extents.y * 0.15f, -bounds.extents.z * 0.12f),
                    bounds.center + new Vector3(bounds.extents.x * 0.46f, bounds.extents.y * 0.75f, 0f)
                })
            };

            RenameWaypoints(paths[0], "Flow_ReliefGas");
            RenameWaypoints(paths[1], "Flow_PurgeGas");
            RenameWaypoints(paths[2], "Flow_Steam");
            return paths;
        }

        private static PipeFlowPath EnsurePath(Transform container, string flowName, Color color, float speed, float particleSize, Vector3[] positions)
        {
            Transform pathTransform = container.Find("FlowPath_" + flowName);
            if (pathTransform == null)
            {
                pathTransform = new GameObject("FlowPath_" + flowName).transform;
                pathTransform.SetParent(container, false);
            }

            PipeFlowPath path = GetOrAdd<PipeFlowPath>(pathTransform.gameObject);
            path.flowName = flowName;
            path.flowColor = color;
            path.speed = speed;
            path.particleSize = particleSize;
            path.loop = true;
            path.waypoints.Clear();

            for (int i = 0; i < positions.Length; i++)
            {
                string waypointName = "Waypoint_" + (i + 1).ToString("00");
                Transform waypoint = pathTransform.Find(waypointName);
                if (waypoint == null)
                {
                    waypoint = new GameObject(waypointName).transform;
                    waypoint.SetParent(pathTransform, false);
                }

                waypoint.position = positions[i];
                path.waypoints.Add(waypoint);
            }

            return path;
        }

        private static void RenameWaypoints(PipeFlowPath path, string prefix)
        {
            for (int i = 0; i < path.waypoints.Count; i++)
            {
                if (path.waypoints[i] != null)
                {
                    path.waypoints[i].name = prefix + "_" + (i + 1).ToString("00");
                }
            }
        }

        private static List<SensorIndicator> EnsureSensorIndicators(Transform root, Bounds bounds, Materials materials)
        {
            Transform container = root.Find("SensorIndicators");
            if (container == null)
            {
                container = new GameObject("SensorIndicators").transform;
                container.SetParent(root, false);
            }

            var sensors = new List<SensorIndicator>
            {
                EnsureSensor(container, "Датчик P_flare", "P_flare", bounds.center + new Vector3(-bounds.extents.x * 0.55f, bounds.extents.y * 0.05f, bounds.extents.z * 0.35f), materials),
                EnsureSensor(container, "Датчик Q_flare", "Q_flare", bounds.center + new Vector3(-bounds.extents.x * 0.35f, bounds.extents.y * 0.02f, bounds.extents.z * 0.32f), materials),
                EnsureSensor(container, "Датчик P_purge", "P_purge", bounds.center + new Vector3(bounds.extents.x * 0.28f, bounds.extents.y * 0.38f, bounds.extents.z * 0.32f), materials),
                EnsureSensor(container, "Датчик Q_purge", "Q_purge", bounds.center + new Vector3(bounds.extents.x * 0.05f, -bounds.extents.y * 0.2f, bounds.extents.z * 0.42f), materials),
                EnsureSensor(container, "Датчик T_flame", "T_flame", bounds.center + new Vector3(bounds.extents.x * 0.48f, bounds.extents.y * 0.78f, bounds.extents.z * 0.12f), materials),
                EnsureSensor(container, "Датчик Steam_Q", "Steam_Q", bounds.center + new Vector3(bounds.extents.x * 0.2f, bounds.extents.y * 0.18f, -bounds.extents.z * 0.35f), materials),
                EnsureSensor(container, "Датчик Level", "Level", bounds.center + new Vector3(-bounds.extents.x * 0.65f, -bounds.extents.y * 0.22f, -bounds.extents.z * 0.2f), materials)
            };

            return sensors;
        }

        private static SensorIndicator EnsureSensor(Transform container, string sensorName, string parameterKey, Vector3 position, Materials materials)
        {
            string objectName = "Sensor_" + parameterKey;
            Transform sensorTransform = container.Find(objectName);
            if (sensorTransform == null)
            {
                GameObject sensorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sensorObject.name = objectName;
                sensorTransform = sensorObject.transform;
                sensorTransform.SetParent(container, false);
                sensorTransform.localScale = Vector3.one * 0.22f;
            }

            sensorTransform.position = position;

            SensorIndicator sensor = GetOrAdd<SensorIndicator>(sensorTransform.gameObject);
            sensor.sensorName = sensorName;
            sensor.parameterKey = parameterKey;
            sensor.normalRangeText = FlareConstants.GetNormalRangeText(parameterKey);
            sensor.indicatorRenderer = sensorTransform.GetComponentInChildren<Renderer>(true);
            sensor.normalMaterial = materials.Normal;
            sensor.warningMaterial = materials.Warning;
            sensor.alarmMaterial = materials.Alarm;

            Light light = sensorTransform.GetComponentInChildren<Light>(true);
            if (light == null)
            {
                GameObject lightObject = new GameObject("Sensor Light");
                lightObject.transform.SetParent(sensorTransform, false);
                lightObject.transform.localPosition = Vector3.zero;
                light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 2.2f;
            }

            sensor.indicatorLight = light;

            Collider collider = sensorTransform.GetComponent<Collider>();
            if (collider == null)
            {
                collider = sensorTransform.gameObject.AddComponent<SphereCollider>();
            }

            SensorHoverTarget hover = GetOrAdd<SensorHoverTarget>(sensorTransform.gameObject);
            hover.indicator = sensor;
            return sensor;
        }

        private static void EnsureFlareStackClickTarget(FlareObjectResolver resolver, Bounds bounds, FlareInstallationController controller)
        {
            Transform stack = resolver != null ? resolver.FindFlareStack() : null;
            GameObject targetObject;
            if (stack != null)
            {
                targetObject = stack.gameObject;
            }
            else
            {
                targetObject = GameObject.Find("FlareStack_Click_Target");
                if (targetObject == null)
                {
                    targetObject = new GameObject("FlareStack_Click_Target");
                }

                targetObject.transform.position = bounds.center + new Vector3(bounds.extents.x * 0.45f, bounds.extents.y * 0.35f, 0f);
            }

            Collider collider = targetObject.GetComponent<Collider>();
            if (collider == null)
            {
                BoxCollider box = targetObject.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, Mathf.Max(4f, bounds.size.y), 1f);
                box.center = stack != null ? Vector3.zero : Vector3.up * bounds.extents.y * 0.2f;
            }

            FlareStackClickTarget clickTarget = GetOrAdd<FlareStackClickTarget>(targetObject);
            clickTarget.controller = controller;
            clickTarget.flareStackObject = targetObject;
        }

        private static void EnsureValveTargets(Transform root, FlareObjectResolver resolver, Bounds bounds, ValveAnimator valveAnimator, Materials materials)
        {
            if (valveAnimator == null)
            {
                return;
            }

            List<Transform> valves = resolver != null ? resolver.FindValves() : new List<Transform>();
            if (valves.Count == 0)
            {
                Transform container = root.Find("ValvePlaceholders");
                if (container == null)
                {
                    container = new GameObject("ValvePlaceholders").transform;
                    container.SetParent(root, false);
                }

                valves.Add(EnsureValvePlaceholder(container, "Valve_Handwheel_PurgeGas", bounds.center + new Vector3(0f, -bounds.extents.y * 0.22f, bounds.extents.z * 0.5f), materials.Warning));
                valves.Add(EnsureValvePlaceholder(container, "Valve_Handwheel_ReliefGas", bounds.center + new Vector3(-bounds.extents.x * 0.35f, -bounds.extents.y * 0.08f, bounds.extents.z * 0.5f), materials.Warning));
            }

            valveAnimator.handwheels = valves;
        }

        private static Transform EnsureValvePlaceholder(Transform container, string name, Vector3 position, Material material)
        {
            Transform valve = container.Find(name);
            if (valve == null)
            {
                GameObject valveObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                valveObject.name = name;
                valve = valveObject.transform;
                valve.SetParent(container, false);
                valve.localScale = new Vector3(0.35f, 0.05f, 0.35f);
                valve.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            valve.position = position;
            Renderer renderer = valve.GetComponentInChildren<Renderer>(true);
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return valve;
        }

        private static Canvas EnsureCanvas()
        {
            Canvas canvas = FindObject<Canvas>();
            if (canvas == null || canvas.name != "FlareCanvas")
            {
                GameObject canvasObject = GameObject.Find("FlareCanvas");
                if (canvasObject == null)
                {
                    canvasObject = new GameObject("FlareCanvas");
                }

                canvas = GetOrAdd<Canvas>(canvasObject);
            }

            canvas.name = "FlareCanvas";
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvas.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(canvas.gameObject);
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindObject<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static FlareUIController BuildUi(
            Canvas canvas,
            FlareInstallationController controller,
            ArchiveModeController archive,
            EarlyWarningController warning,
            AlarmVisualController alarm,
            out SensorTooltipController tooltip,
            out RiskPanelController riskPanel)
        {
            ClearUiChildren(canvas.transform);
            FlareUIController ui = GetOrAdd<FlareUIController>(canvas.gameObject);
            ui.controller = controller;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();

            GameObject topPanel = CreatePanel(canvasRect, "TopPanel", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), Vector2.zero, new Color(0.08f, 0.1f, 0.12f, 0.92f));
            HorizontalLayoutGroup topLayout = topPanel.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(18, 18, 10, 10);
            topLayout.spacing = 12f;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.childControlHeight = true;
            topLayout.childControlWidth = false;

            ui.titleText = CreateText(topPanel.transform, "Интерактивная модель факельной установки", 26, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(620f, 48f));
            ui.archiveModeButton = CreateButton(topPanel.transform, "BtnArchiveMode", "Архив", new Vector2(180f, 44f));
            ui.earlyWarningModeButton = CreateButton(topPanel.transform, "BtnWarningMode", "Раннее предупреждение", new Vector2(280f, 44f));
            ui.statusImage = CreateImage(topPanel.transform, "StatusColor", FlareConstants.NormalColor, new Vector2(44f, 44f));
            ui.statusText = CreateText(topPanel.transform, "Статус: Норма", 20, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(260f, 44f));

            ui.archivePanel = CreatePanel(canvasRect, "ArchivePanel", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 18f), new Vector2(470f, -88f), new Color(0.12f, 0.14f, 0.16f, 0.88f));
            VerticalLayoutGroup archiveLayout = AddVerticalLayout(ui.archivePanel, 14);
            CreateText(ui.archivePanel.transform, "Архив технологических параметров", 22, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(410f, 34f));
            ui.recordIndexText = CreateText(ui.archivePanel.transform, "Запись 1 из 100", 18, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, new Vector2(410f, 30f));

            GameObject archiveButtons = CreateLayoutRow(ui.archivePanel.transform, "ArchiveButtons", 44f);
            ui.previousButton = CreateButton(archiveButtons.transform, "BtnPrevious", "Назад", new Vector2(130f, 40f));
            ui.nextButton = CreateButton(archiveButtons.transform, "BtnNext", "Вперёд", new Vector2(130f, 40f));
            ui.playPauseButton = CreateButton(archiveButtons.transform, "BtnPlayPause", "Пуск", new Vector2(130f, 40f));
            ui.playPauseButtonText = ui.playPauseButton.GetComponentInChildren<Text>();
            ui.recordSlider = CreateSlider(ui.archivePanel.transform, "RecordSlider", new Vector2(410f, 32f));
            ui.archiveValuesText = CreateText(ui.archivePanel.transform, "Параметры не загружены", 17, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, new Vector2(410f, 220f));
            ui.archiveViolationsText = CreateText(ui.archivePanel.transform, "Нарушений нет", 17, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 0.9f, 0.45f), new Vector2(410f, 120f));
            ui.archiveRecommendationText = CreateText(ui.archivePanel.transform, "Параметры в допустимых пределах", 17, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.75f, 0.92f, 1f), new Vector2(410f, 135f));
            archiveLayout.enabled = true;

            ui.warningPanel = CreatePanel(canvasRect, "EarlyWarningPanel", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 18f), new Vector2(470f, -88f), new Color(0.12f, 0.14f, 0.16f, 0.88f));
            VerticalLayoutGroup warningLayout = AddVerticalLayout(ui.warningPanel, 12);
            CreateText(ui.warningPanel.transform, "Раннее предупреждение", 22, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(410f, 34f));
            ui.pPurgeSlider = CreateSliderRow(ui.warningPanel.transform, "P_purge", "P_purge", "0.35 МПа", out ui.pPurgeValueText);
            ui.qPurgeSlider = CreateSliderRow(ui.warningPanel.transform, "Q_purge", "Q_purge", "30 м3/ч", out ui.qPurgeValueText);
            ui.pFlareSlider = CreateSliderRow(ui.warningPanel.transform, "P_flare", "P_flare", "0.012 МПа", out ui.pFlareValueText);
            ui.probabilityText = CreateText(ui.warningPanel.transform, "Вероятность отрыва: 0.0%", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(410f, 34f));
            GameObject riskBar = CreatePanel(ui.warningPanel.transform as RectTransform, "RiskBar", new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero, new Color(0.18f, 0.2f, 0.22f, 1f));
            LayoutElement riskBarLayout = riskBar.AddComponent<LayoutElement>();
            riskBarLayout.preferredWidth = 410f;
            riskBarLayout.preferredHeight = 24f;
            Image riskFill = CreateImage(riskBar.transform, "RiskBarFill", FlareConstants.NormalColor, new Vector2(410f, 24f));
            RectTransform riskFillRect = riskFill.GetComponent<RectTransform>();
            riskFillRect.anchorMin = new Vector2(0f, 0f);
            riskFillRect.anchorMax = new Vector2(1f, 1f);
            riskFillRect.offsetMin = Vector2.zero;
            riskFillRect.offsetMax = Vector2.zero;
            riskFill.type = Image.Type.Filled;
            riskFill.fillMethod = Image.FillMethod.Horizontal;
            riskFill.fillAmount = 0f;
            ui.riskBarFill = riskFill;
            ui.riskZoneText = CreateText(ui.warningPanel.transform, "Зона риска: норма", 18, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, new Vector2(410f, 32f));
            ui.warningRecommendationText = CreateText(ui.warningPanel.transform, "Параметры в допустимых пределах", 17, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.75f, 0.92f, 1f), new Vector2(410f, 115f));
            ui.alarmIndicatorImage = CreateImage(ui.warningPanel.transform, "AlarmIndicator", FlareConstants.DangerColor, new Vector2(410f, 30f));
            ui.alarmIndicatorImage.enabled = false;
            warningLayout.enabled = true;
            ui.warningPanel.SetActive(false);

            GameObject tooltipPanel = CreatePanel(canvasRect, "TooltipPanel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(300f, 130f), new Color(0.02f, 0.03f, 0.04f, 0.92f));
            tooltipPanel.SetActive(false);
            Component tooltipText = CreateText(tooltipPanel.transform, string.Empty, 16, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, new Vector2(280f, 110f));
            tooltip = GetOrAdd<SensorTooltipController>(canvas.gameObject);
            tooltip.tooltipPanel = tooltipPanel;
            tooltip.tooltipText = tooltipText;
            tooltip.tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            ui.tooltipPanel = tooltipPanel;

            GameObject riskPanelObject = CreatePanel(canvasRect, "RiskPanel", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-440f, 18f), new Vector2(-16f, -88f), new Color(0.1f, 0.11f, 0.13f, 0.92f));
            VerticalLayoutGroup riskLayout = AddVerticalLayout(riskPanelObject, 12);
            CreateText(riskPanelObject.transform, "Панель риска факельной трубы", 22, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(390f, 34f));
            riskPanel = GetOrAdd<RiskPanelController>(canvas.gameObject);
            riskPanel.panel = riskPanelObject;
            riskPanel.modeText = CreateText(riskPanelObject.transform, "Режим: Архив", 17, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, new Vector2(390f, 30f));
            riskPanel.parametersText = CreateText(riskPanelObject.transform, "Параметры не выбраны", 17, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, new Vector2(390f, 95f));
            riskPanel.probabilityText = CreateText(riskPanelObject.transform, "Вероятность отрыва: 0.0%", 17, FontStyle.Bold, TextAnchor.UpperLeft, Color.white, new Vector2(390f, 70f));
            riskPanel.riskZoneImage = CreateImage(riskPanelObject.transform, "RiskZoneColor", FlareConstants.NormalColor, new Vector2(390f, 24f));
            riskPanel.recommendationText = CreateText(riskPanelObject.transform, "Параметры в допустимых пределах", 17, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.75f, 0.92f, 1f), new Vector2(390f, 130f));
            riskPanel.flameStateText = CreateText(riskPanelObject.transform, "Пламя: Normal", 17, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, new Vector2(390f, 30f));
            riskPanel.closeButton = CreateButton(riskPanelObject.transform, "BtnCloseRisk", "Закрыть", new Vector2(140f, 40f));
            riskLayout.enabled = true;
            riskPanelObject.SetActive(false);
            ui.riskPanel = riskPanelObject;

            ui.BindButtons();
            warning.pPurgeSlider = ui.pPurgeSlider;
            warning.qPurgeSlider = ui.qPurgeSlider;
            warning.pFlareSlider = ui.pFlareSlider;
            warning.ConfigureSliders();
            archive.controller = controller;
            alarm.uiAlarmImage = ui.alarmIndicatorImage;
            return ui;
        }

        private static void ClearUiChildren(Transform canvasTransform)
        {
            var toDestroy = new List<GameObject>();
            foreach (Transform child in canvasTransform)
            {
                toDestroy.Add(child.gameObject);
            }

            foreach (GameObject child in toDestroy)
            {
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        private static GameObject CreatePanel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
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

        private static GameObject CreateLayoutRow(Transform parent, string name, float height)
        {
            GameObject row = new GameObject(name);
            row.transform.SetParent(parent, false);
            RectTransform rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(410f, height);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            LayoutElement element = row.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.preferredWidth = 410f;
            return row;
        }

        private static VerticalLayoutGroup AddVerticalLayout(GameObject panel, int padding)
        {
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = panel.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.28f, 0.34f, 0.96f);
            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.32f, 0.4f, 0.48f, 1f);
            colors.pressedColor = new Color(0.12f, 0.16f, 0.2f, 1f);
            button.colors = colors;

            Component text = CreateText(buttonObject.transform, label, 17, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, size);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 2f);
            textRect.offsetMax = new Vector2(-6f, -2f);

            LayoutElement element = buttonObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 size)
        {
            GameObject sliderObject = new GameObject(name);
            sliderObject.transform.SetParent(parent, false);
            RectTransform rect = sliderObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            LayoutElement element = sliderObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;

            GameObject background = CreatePanel(rect, "Background", Vector2.zero, Vector2.one, new Vector2(0f, 10f), new Vector2(0f, -10f), new Color(0.22f, 0.24f, 0.27f, 1f));
            GameObject fillArea = CreatePanel(rect, "Fill Area", Vector2.zero, Vector2.one, new Vector2(8f, 10f), new Vector2(-8f, -10f), Color.clear);
            Image fill = CreateImage(fillArea.transform, "Fill", FlareConstants.NormalColor, new Vector2(size.x, 12f));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            GameObject handleArea = CreatePanel(rect, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-10f, -4f), Color.clear);
            Image handle = CreateImage(handleArea.transform, "Handle", Color.white, new Vector2(22f, 22f));
            slider.targetGraphic = handle;
            slider.fillRect = fillRect;
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            return slider;
        }

        private static Slider CreateSliderRow(Transform parent, string name, string label, string value, out Component valueText)
        {
            GameObject row = new GameObject("Row_" + name);
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(410f, 74f);
            LayoutElement rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredWidth = 410f;
            rowElement.preferredHeight = 74f;
            VerticalLayoutGroup vertical = row.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 4f;
            vertical.childControlWidth = true;
            vertical.childControlHeight = false;

            GameObject labelRow = CreateLayoutRow(row.transform, "Label_" + name, 28f);
            CreateText(labelRow.transform, label, 16, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(190f, 26f));
            valueText = CreateText(labelRow.transform, value, 16, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.75f, 0.92f, 1f), new Vector2(190f, 26f));
            Slider slider = CreateSlider(row.transform, "Slider_" + name, new Vector2(390f, 32f));
            return slider;
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 size)
        {
            GameObject imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            LayoutElement element = imageObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            return image;
        }

        private static Component CreateText(Transform parent, string value, int fontSize, FontStyle style, TextAnchor anchor, Color color, Vector2 size)
        {
            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            LayoutElement element = textObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;


            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.font = font;
            return text;
        }

        private static Materials EnsureMaterials()
        {
            return new Materials
            {
                Normal = EnsureMaterial("MAT_Normal_Green.mat", FlareConstants.NormalColor, false),
                Warning = EnsureMaterial("MAT_Warning_Yellow.mat", FlareConstants.WarningColor, false),
                Alarm = EnsureMaterial("MAT_Alarm_Red.mat", FlareConstants.DangerColor, false),
                FlameNormal = EnsureMaterial("MAT_Flame_Normal.mat", new Color(0.24f, 1f, 0.72f), true),
                FlameWarning = EnsureMaterial("MAT_Flame_Warning.mat", new Color(1f, 0.78f, 0.18f), true),
                FlameDanger = EnsureMaterial("MAT_Flame_Danger.mat", new Color(1f, 0.24f, 0.08f), true),
                FlowRelief = EnsureMaterial("MAT_Flow_ReliefGas.mat", FlareConstants.FlowReliefGasColor, true),
                FlowPurge = EnsureMaterial("MAT_Flow_PurgeGas.mat", FlareConstants.FlowPurgeGasColor, true),
                FlowSteam = EnsureMaterial("MAT_Flow_Steam.mat", FlareConstants.FlowSteamColor, true)
            };
        }

        private static Material EnsureMaterial(string fileName, Color color, bool emission)
        {
            string path = MaterialFolder + "/" + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Standard");
            material.color = color;
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.2f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        private static T FindObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
#else
            return UnityEngine.Object.FindObjectOfType<T>(true);
#endif
        }

        private static T[] FindObjects<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
#else
            return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
        }

        private static int CountMissingScripts()
        {
            int count = 0;
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.IsValid())
                {
                    continue;
                }

                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            }

            return count;
        }

        private static string ToProjectFullPath(string assetPath)
        {
            string relative = assetPath.StartsWith("Assets", StringComparison.Ordinal)
                ? assetPath.Substring("Assets".Length).TrimStart('/', '\\')
                : assetPath;
            return Path.Combine(Application.dataPath, relative);
        }


        private static void MarkDirty(params GameObject[] objects)
        {
            foreach (GameObject go in objects)
            {
                if (go != null)
                {
                    EditorUtility.SetDirty(go);
                }
            }
        }

        private struct Materials
        {
            public Material Normal;
            public Material Warning;
            public Material Alarm;
            public Material FlameNormal;
            public Material FlameWarning;
            public Material FlameDanger;
            public Material FlowRelief;
            public Material FlowPurge;
            public Material FlowSteam;
        }
    }
}
