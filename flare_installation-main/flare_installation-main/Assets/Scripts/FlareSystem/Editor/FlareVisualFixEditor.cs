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
    public static class FlareVisualFixEditor
    {
        private const string ScenePath = "Assets/Scenes/FlareInstallationScene_Rebuilt.unity";
        private const string BackupPath = "Assets/Scenes/FlareInstallationScene_Rebuilt_Before_FlowSensorUiFix.unity";
        private const string ScreenshotFolder = "Screenshots/UnityFix_FlowsSensorsUi";
        private const string ReportPath = "Reports/unity_visual_fix_report.txt";

        private static Font builtinFont;

        public static void ApplyVisualFixes()
        {
            ApplyVisualFixesInternal(true);
        }

        public static void CaptureVisualFixScreenshots()
        {
            Scene scene = OpenTargetScene();
            GameObject modelRoot = FindModelRoot();
            Bounds modelBounds = CalculateBounds(modelRoot);
            Vector3 flareTop = ResolveFlareTop(modelRoot, modelBounds);
            Bounds separatorBounds = ResolveSeparatorBounds(modelRoot, modelBounds);

            FlareInstallationController controller = FindSceneComponent<FlareInstallationController>();
            if (controller != null)
            {
                controller.EnsureReferences();
                controller.SetArchiveMode();
            }

            SimulateParticles();

            string fullFolder = ProjectPath(ScreenshotFolder);
            Directory.CreateDirectory(fullFolder);

            Canvas canvas = FindSceneComponent<Canvas>();
            Camera camera = Camera.main != null ? Camera.main : FindSceneComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError("[FlareVisualFix] Cannot capture screenshots: no camera found.");
                return;
            }

            CaptureCameraShot(camera, canvas, "flows_overview_fixed.png",
                modelBounds.center + new Vector3(18f, 15f, -28f),
                new Vector3(modelBounds.center.x, modelBounds.min.y + 4.2f, modelBounds.center.z), 62f, false, false, false, false);

            CaptureCameraShot(camera, canvas, "relief_gas_flow_fixed.png",
                separatorBounds.center + new Vector3(4.8f, 3.2f, -6.0f),
                Vector3.Lerp(separatorBounds.center, new Vector3(flareTop.x, separatorBounds.center.y, flareTop.z), 0.45f), 36f,
                false, false, false, false);

            CaptureCameraShot(camera, canvas, "blue_purge_flow_fixed.png",
                separatorBounds.center + new Vector3(3.5f, 2.0f, -5.2f),
                separatorBounds.center + new Vector3(1.8f, 0.1f, 1.2f), 34f,
                false, false, false, false);

            CaptureCameraShot(camera, canvas, "sensor_indicators_fixed.png",
                separatorBounds.center + new Vector3(4.5f, 2.6f, -5.6f),
                separatorBounds.center + new Vector3(0f, 0.45f, 0f), 31f,
                false, false, false, false);

            FlameController flame = FindSceneComponent<FlameController>();
            if (flame != null)
            {
                flame.SetNormal();
                SimulateParticles();
            }

            CaptureCameraShot(camera, canvas, "flame_fixed.png",
                flareTop + new Vector3(2.8f, 2.0f, -4.2f),
                flareTop + new Vector3(0f, 0.35f, 0f), 26f,
                false, false, false, false);

            if (controller != null)
            {
                controller.SetArchiveMode();
            }

            CaptureCameraShot(camera, canvas, "ui_archive_fixed.png",
                modelBounds.center + new Vector3(10f, 14f, -18f),
                modelBounds.center + new Vector3(0f, 4f, 0f), 45f, true, false, false, false);

            if (controller != null)
            {
                controller.SetEarlyWarningMode();
                if (controller.earlyWarningController != null)
                {
                    controller.earlyWarningController.SetInputs(0.22f, 18f, 0.024f);
                }
            }

            CaptureCameraShot(camera, canvas, "ui_warning_fixed.png",
                modelBounds.center + new Vector3(10f, 14f, -18f),
                modelBounds.center + new Vector3(0f, 4f, 0f), 45f, false, true, false, false);

            if (controller != null)
            {
                controller.OpenRiskPanel();
            }

            CaptureCameraShot(camera, canvas, "interaction_tooltip_or_risk_fixed.png",
                modelBounds.center + new Vector3(10f, 13f, -18f),
                modelBounds.center + new Vector3(0f, 4f, 0f), 45f, false, true, true, true);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[FlareVisualFix] Screenshots saved to " + fullFolder);
        }

        private static void ApplyVisualFixesInternal(bool writeReport)
        {
            Scene scene = OpenTargetScene();
            EnsureBackup();

            GameObject root = FindOrCreateRoot("FlareSystemRoot");
            GameObject modelRoot = FindModelRoot();
            Bounds modelBounds = CalculateBounds(modelRoot);
            Vector3 flareTop = ResolveFlareTop(modelRoot, modelBounds);
            Bounds separatorBounds = ResolveSeparatorBounds(modelRoot, modelBounds);

            Material normal = LoadMaterial("MAT_Sensor_Green");
            Material warning = LoadMaterial("MAT_Sensor_Yellow");
            Material alarm = LoadMaterial("MAT_Sensor_Red");
            Material relief = LoadMaterial("MAT_Flow_ReliefGas");
            Material purge = LoadMaterial("MAT_Flow_PurgeGas");
            Material steam = LoadMaterial("MAT_Flow_Steam");
            Material blue = LoadMaterial("MAT_Pipe_Blue");
            Material flameGreen = LoadMaterial("MAT_Flame_Green");
            Material flameYellow = LoadMaterial("MAT_Flame_Yellow");
            Material flameRed = LoadMaterial("MAT_Flame_Red");

            RemoveOldFlowObjects();
            DeleteGeneratedSensorMarkers();
            RemoveFlameMeshObjects();

            PipeFlowController pipeFlowController = RebuildFlowPaths(root.transform, modelBounds, separatorBounds, flareTop, relief, purge, steam, blue);
            List<SensorIndicator> sensors = RebuildSensorIndicators(root.transform, modelRoot, modelBounds, separatorBounds, flareTop, normal, warning, alarm);
            FlameController flameController = FixFlameEffect(root.transform, flareTop, flameGreen, flameYellow, flameRed);
            FixUiLayout();
            WireControllers(pipeFlowController, sensors, flameController);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (writeReport)
            {
                WriteReport(sensors.Count);
            }

            Debug.Log("[FlareVisualFix] Visual fixes applied and scene saved: " + ScenePath);
        }

        private static Scene OpenTargetScene()
        {
            if (!File.Exists(ProjectPath(ScenePath)))
            {
                throw new FileNotFoundException("Target scene not found", ScenePath);
            }

            Scene current = SceneManager.GetActiveScene();
            if (current.path != ScenePath)
            {
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            return current;
        }

        private static void EnsureBackup()
        {
            string source = ProjectPath(ScenePath);
            string backup = ProjectPath(BackupPath);
            if (File.Exists(backup))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(source, backup, false);

            string sourceMeta = source + ".meta";
            string backupMeta = backup + ".meta";
            if (File.Exists(sourceMeta) && !File.Exists(backupMeta))
            {
                File.Copy(sourceMeta, backupMeta, false);
            }

            AssetDatabase.Refresh();
        }

        private static PipeFlowController RebuildFlowPaths(Transform root, Bounds modelBounds, Bounds separatorBounds, Vector3 flareTop, Material relief, Material purge, Material steam, Material condensate)
        {
            GameObject flowRoot = new GameObject("FlowWaypointsRoot");
            flowRoot.transform.SetParent(root, false);

            PipeFlowController controller = flowRoot.AddComponent<PipeFlowController>();
            controller.reliefGasMaterial = relief;
            controller.purgeGasMaterial = purge;
            controller.steamMaterial = steam;
            controller.createVisibleMarkers = true;
            controller.markersPerPath = 9;
            controller.globalIntensity = 0.85f;

            float pipeY = Mathf.Max(separatorBounds.center.y + separatorBounds.extents.y * 0.55f, modelBounds.min.y + 0.65f);
            float purgeY = Mathf.Max(modelBounds.min.y + 0.82f, separatorBounds.min.y + 0.34f);
            float frontZ = separatorBounds.center.z - Mathf.Max(separatorBounds.extents.z * 0.72f, 0.65f);
            float blueZ = separatorBounds.center.z + Mathf.Max(separatorBounds.extents.z * 0.88f, 0.72f);
            Vector3 stackLow = new Vector3(flareTop.x, modelBounds.min.y + 0.75f, flareTop.z);

            AddPath(controller, flowRoot.transform, "ReliefGasFlow", "ReliefGas", new Color(0.82f, 0.88f, 0.92f, 0.85f), 1.25f, 0.042f, relief,
                new[]
                {
                    new Vector3(separatorBounds.center.x - separatorBounds.extents.x * 0.92f, pipeY, separatorBounds.center.z),
                    new Vector3(separatorBounds.center.x - separatorBounds.extents.x * 0.08f, pipeY, separatorBounds.center.z),
                    new Vector3(Mathf.Lerp(separatorBounds.center.x, flareTop.x, 0.55f), pipeY, separatorBounds.center.z),
                    new Vector3(flareTop.x + 0.62f, pipeY, separatorBounds.center.z),
                    new Vector3(flareTop.x + 0.38f, pipeY, flareTop.z),
                    new Vector3(flareTop.x + 0.18f, modelBounds.min.y + modelBounds.size.y * 0.24f, flareTop.z)
                });

            AddPath(controller, flowRoot.transform, "PurgeGasFlow", "PurgeGas", new Color(0.12f, 0.55f, 1f, 0.9f), 1.45f, 0.038f, purge,
                new[]
                {
                    new Vector3(separatorBounds.center.x + separatorBounds.extents.x * 0.55f, purgeY, blueZ),
                    new Vector3(separatorBounds.center.x - separatorBounds.extents.x * 0.55f, purgeY, blueZ),
                    new Vector3(Mathf.Lerp(separatorBounds.center.x, flareTop.x, 0.38f), purgeY, blueZ),
                    new Vector3(flareTop.x + 0.76f, purgeY, blueZ),
                    new Vector3(flareTop.x + 0.76f, purgeY, flareTop.z + 0.42f),
                    stackLow + new Vector3(0.28f, 0f, 0.42f)
                });

            AddPath(controller, flowRoot.transform, "SteamFlow", "Steam", new Color(0.82f, 0.96f, 1f, 0.86f), 1.05f, 0.036f, steam,
                new[]
                {
                    stackLow + new Vector3(-0.34f, 0.22f, -0.36f),
                    new Vector3(flareTop.x - 0.34f, modelBounds.min.y + modelBounds.size.y * 0.34f, flareTop.z - 0.36f),
                    new Vector3(flareTop.x - 0.30f, modelBounds.min.y + modelBounds.size.y * 0.60f, flareTop.z - 0.30f),
                    new Vector3(flareTop.x - 0.22f, flareTop.y - 1.18f, flareTop.z - 0.18f),
                    flareTop + new Vector3(-0.10f, -0.20f, -0.08f)
                });

            AddPath(controller, flowRoot.transform, "CondensateFlow", "Condensate", new Color(0.25f, 0.68f, 0.92f, 0.82f), 0.82f, 0.032f, condensate,
                new[]
                {
                    new Vector3(separatorBounds.center.x + separatorBounds.extents.x * 0.28f, modelBounds.min.y + 0.24f, frontZ),
                    new Vector3(separatorBounds.center.x - separatorBounds.extents.x * 0.82f, modelBounds.min.y + 0.22f, frontZ),
                    new Vector3(Mathf.Lerp(separatorBounds.center.x, flareTop.x, 0.48f), modelBounds.min.y + 0.22f, frontZ),
                    new Vector3(flareTop.x + 0.58f, modelBounds.min.y + 0.22f, frontZ)
                });

            controller.BuildRuntimeFlows();
            controller.StartFlow();
            return controller;
        }

        private static void AddPath(PipeFlowController controller, Transform parent, string objectName, string flowName, Color color, float speed, float size, Material material, IReadOnlyList<Vector3> points)
        {
            GameObject pathObject = new GameObject(objectName);
            pathObject.transform.SetParent(parent, false);
            PipeFlowPath path = pathObject.AddComponent<PipeFlowPath>();
            path.flowName = flowName;
            path.flowColor = color;
            path.speed = speed;
            path.particleSize = size;
            path.loop = true;

            for (int i = 0; i < points.Count; i++)
            {
                GameObject waypoint = new GameObject(flowName + "_WP_" + (i + 1).ToString("00"));
                waypoint.transform.SetParent(pathObject.transform, true);
                waypoint.transform.position = points[i];
                path.waypoints.Add(waypoint.transform);
            }

            LineRenderer line = pathObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points.Count;
            line.startWidth = Mathf.Max(0.008f, size * 0.55f);
            line.endWidth = line.startWidth;
            line.startColor = color;
            line.endColor = color;
            line.material = material;
            for (int i = 0; i < points.Count; i++)
            {
                line.SetPosition(i, points[i]);
            }

            controller.flowPaths.Add(path);
        }

        private static List<SensorIndicator> RebuildSensorIndicators(Transform root, GameObject modelRoot, Bounds modelBounds, Bounds separatorBounds, Vector3 flareTop, Material normal, Material warning, Material alarm)
        {
            GameObject sensorRoot = new GameObject("SensorIndicatorBoxes");
            sensorRoot.transform.SetParent(root, false);

            float pipeY = Mathf.Max(modelBounds.min.y + 0.50f, separatorBounds.min.y + 0.18f);
            float blueZ = separatorBounds.center.z + Mathf.Max(separatorBounds.extents.z * 0.88f, 0.72f);
            List<SensorIndicator> sensors = new List<SensorIndicator>();

            sensors.Add(CreateSensor(sensorRoot.transform, modelRoot, "Indicator_P_flare", "Датчик P_flare", "P_flare",
                separatorBounds.center + new Vector3(-separatorBounds.extents.x * 0.48f, separatorBounds.extents.y + 0.10f, separatorBounds.extents.z + 0.05f), normal, warning, alarm));
            sensors.Add(CreateSensor(sensorRoot.transform, modelRoot, "Indicator_Q_flare", "Датчик Q_flare", "Q_flare",
                separatorBounds.center + new Vector3(0.12f, separatorBounds.extents.y + 0.10f, separatorBounds.extents.z + 0.05f), normal, warning, alarm));
            sensors.Add(CreateSensor(sensorRoot.transform, modelRoot, "Indicator_P_purge", "Датчик P_purge", "P_purge",
                new Vector3(flareTop.x + 0.08f, modelBounds.min.y + modelBounds.size.y * 0.52f, flareTop.z + 0.08f), normal, warning, alarm));
            sensors.Add(CreateSensor(sensorRoot.transform, modelRoot, "Indicator_Q_purge", "Датчик Q_purge", "Q_purge",
                new Vector3(Mathf.Lerp(separatorBounds.center.x, flareTop.x, 0.38f), pipeY + 0.20f, blueZ), normal, warning, alarm));
            sensors.Add(CreateSensor(sensorRoot.transform, modelRoot, "Indicator_T_flame", "Датчик T_flame", "T_flame",
                new Vector3(flareTop.x + 0.08f, flareTop.y - 1.35f, flareTop.z + 0.08f), normal, warning, alarm));
            sensors.Add(CreateSensor(sensorRoot.transform, modelRoot, "Indicator_Steam_Q", "Датчик Steam_Q", "Steam_Q",
                new Vector3(flareTop.x - 0.08f, modelBounds.min.y + modelBounds.size.y * 0.68f, flareTop.z - 0.08f), normal, warning, alarm));
            sensors.Add(CreateSensor(sensorRoot.transform, modelRoot, "Indicator_Level", "Датчик Level", "Level",
                separatorBounds.center + new Vector3(separatorBounds.extents.x * 0.55f, -separatorBounds.extents.y * 0.25f, separatorBounds.extents.z + 0.06f), normal, warning, alarm));

            return sensors;
        }

        private static SensorIndicator CreateSensor(Transform parent, GameObject modelRoot, string objectName, string label, string key, Vector3 position, Material normal, Material warning, Material alarm)
        {
            GameObject sensor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sensor.name = objectName;
            sensor.transform.SetParent(parent, true);
            sensor.transform.position = SnapToNearestModelSurface(modelRoot, position);
            sensor.transform.localScale = new Vector3(0.16f, 0.11f, 0.055f);

            Renderer renderer = sensor.GetComponent<Renderer>();
            if (renderer != null && normal != null)
            {
                renderer.sharedMaterial = normal;
            }

            SensorIndicator indicator = sensor.AddComponent<SensorIndicator>();
            indicator.sensorName = label;
            indicator.parameterKey = key;
            indicator.normalRangeText = FlareConstants.GetNormalRangeText(key);
            indicator.indicatorRenderer = renderer;
            indicator.normalMaterial = normal;
            indicator.warningMaterial = warning;
            indicator.alarmMaterial = alarm;

            SensorHoverTarget hover = sensor.AddComponent<SensorHoverTarget>();
            hover.indicator = indicator;

            BoxCollider collider = sensor.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.isTrigger = false;
                collider.size = Vector3.one * 1.15f;
            }

            return indicator;
        }

        private static Vector3 SnapToNearestModelSurface(GameObject modelRoot, Vector3 preferredPosition)
        {
            if (modelRoot == null)
            {
                return preferredPosition;
            }

            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            Vector3 bestPoint = preferredPosition;
            float bestDistance = float.PositiveInfinity;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (bounds.size.y < 0.08f && bounds.size.x > 1.5f && bounds.size.z > 1.5f)
                {
                    continue;
                }

                Vector3 point = bounds.ClosestPoint(preferredPosition);
                float distance = Vector3.SqrMagnitude(point - preferredPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPoint = point;
                }
            }

            Vector3 outward = preferredPosition - bestPoint;
            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = Vector3.up;
            }

            return bestPoint + outward.normalized * 0.035f;
        }

        private static FlameController FixFlameEffect(Transform root, Vector3 flareTop, Material normal, Material warning, Material danger)
        {
            FlameController controller = FindSceneComponent<FlameController>();
            if (controller == null)
            {
                GameObject controllerObject = new GameObject("FlameController");
                controllerObject.transform.SetParent(root, false);
                controller = controllerObject.AddComponent<FlameController>();
            }

            GameObject flameEffect = FindSceneObject("FlameEffect");
            if (flameEffect == null)
            {
                flameEffect = new GameObject("FlameEffect");
            }

            flameEffect.transform.SetParent(controller.transform, true);
            flameEffect.transform.position = flareTop + Vector3.up * 0.22f;
            flameEffect.transform.localScale = Vector3.one * 0.78f;

            for (int i = flameEffect.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = flameEffect.transform.GetChild(i);
                if (child.GetComponent<MeshRenderer>() != null || child.GetComponent<MeshFilter>() != null)
                {
                    DestroyImmediateSafe(child.gameObject);
                }
            }

            ParticleSystem particles = flameEffect.GetComponent<ParticleSystem>();
            if (particles == null)
            {
                particles = flameEffect.AddComponent<ParticleSystem>();
            }

            Light light = flameEffect.GetComponentInChildren<Light>(true);
            if (light == null)
            {
                GameObject lightObject = new GameObject("Flame Point Light");
                lightObject.transform.SetParent(flameEffect.transform, false);
                lightObject.transform.localPosition = Vector3.up * 0.45f;
                light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 12f;
            }

            controller.flameRoot = flameEffect.transform;
            controller.flameParticles = particles;
            controller.flameLight = light;
            controller.flameRenderer = null;
            controller.normalMaterial = normal;
            controller.warningMaterial = warning;
            controller.dangerMaterial = danger;
            controller.EnsureEffect();
            controller.SetNormal();
            return controller;
        }

        private static void FixUiLayout()
        {
            Canvas canvas = FindSceneComponent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("UI Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (FindSceneComponent<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            FlareUIController ui = FindSceneComponent<FlareUIController>();
            if (ui == null)
            {
                ui = canvas.gameObject.AddComponent<FlareUIController>();
            }

            GameObject archivePanel = FindOrCreatePanel(canvas.transform, "ArchivePanel");
            GameObject warningPanel = FindOrCreatePanel(canvas.transform, "WarningPanel");
            GameObject bottomPanel = FindOrCreatePanel(canvas.transform, "BottomPanel");

            SetRect(archivePanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 122f), new Vector2(432f, -86f));
            SetRect(warningPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-520f, 122f), new Vector2(-16f, -86f));
            SetRect(bottomPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 16f), new Vector2(-16f, 104f));

            RebuildArchivePanel(archivePanel.transform, ui);
            RebuildWarningPanel(warningPanel.transform, ui);
            RebuildBottomPanel(bottomPanel.transform, ui);

            ui.archivePanel = archivePanel;
            ui.warningPanel = warningPanel;
            ui.controller = FindSceneComponent<FlareInstallationController>();
            ui.BindButtons();
            archivePanel.SetActive(true);
            warningPanel.SetActive(false);
        }

        private static void RebuildArchivePanel(Transform panel, FlareUIController ui)
        {
            RemoveLayoutComponents(panel);
            ClearChildren(panel);
            CreateText(panel, "ArchiveTitle", "Архив", 16, FontStyle.Bold, TextAnchor.MiddleLeft, 16, -14, 380, 26);
            ui.previousButton = CreateButton(panel, "BtnPrevious", "Назад", 16, -54, 118, 34);
            ui.nextButton = CreateButton(panel, "BtnNext", "Вперёд", 145, -54, 118, 34);
            ui.playPauseButton = CreateButton(panel, "BtnPlayPause", "Пуск", 274, -54, 118, 34);
            ui.playPauseButtonText = ui.playPauseButton.GetComponentInChildren<Text>();
            ui.recordSlider = CreateSlider(panel, "RecordSlider", 16, -104, 376, 24);
            ui.recordIndexText = CreateText(panel, "RecordIndexText", "Запись 1 / 100", 15, FontStyle.Bold, TextAnchor.MiddleLeft, 16, -138, 376, 24);
            ui.archiveValuesText = CreateText(panel, "ArchiveValuesText", "P_flare: -- МПа\nQ_flare: -- м3/ч\nP_purge: -- МПа\nQ_purge: -- м3/ч\nT_flame: -- °C\nSteam_Q: -- кг/ч\notriv: --\nhlopok: --", 14, FontStyle.Normal, TextAnchor.UpperLeft, 16, -174, 376, 168);
            ui.archiveViolationsText = CreateText(panel, "ArchiveViolationsText", "Нарушений нет", 14, FontStyle.Bold, TextAnchor.UpperLeft, 16, -356, 376, 52, new Color(1f, 0.88f, 0.35f));
            ui.archiveRecommendationText = CreateText(panel, "ArchiveRecommendationText", "Параметры в допустимых пределах", 14, FontStyle.Normal, TextAnchor.UpperLeft, 16, -420, 376, 62, new Color(0.72f, 0.93f, 1f));
        }

        private static void RebuildWarningPanel(Transform panel, FlareUIController ui)
        {
            RemoveLayoutComponents(panel);
            ClearChildren(panel);
            CreateText(panel, "WarningTitle", "Раннее предупреждение", 16, FontStyle.Bold, TextAnchor.MiddleLeft, 18, -14, 458, 26);
            ui.pPurgeSlider = CreateWarningRow(panel, "P_purge", "0.35 МПа", -58, out Component pPurgeValue);
            ui.qPurgeSlider = CreateWarningRow(panel, "Q_purge", "30 м3/ч", -112, out Component qPurgeValue);
            ui.pFlareSlider = CreateWarningRow(panel, "P_flare", "0.012 МПа", -166, out Component pFlareValue);
            ui.pPurgeValueText = pPurgeValue;
            ui.qPurgeValueText = qPurgeValue;
            ui.pFlareValueText = pFlareValue;
            ui.probabilityText = CreateText(panel, "ProbabilityText", "Вероятность отрыва: 0,0%", 14, FontStyle.Bold, TextAnchor.MiddleLeft, 18, -220, 458, 24);

            GameObject riskBar = CreateImage(panel, "RiskBar", new Color(0.15f, 0.15f, 0.16f, 0.92f), 18, -256, 458, 22);
            GameObject fill = CreateImage(riskBar.transform, "RiskBarFill", FlareConstants.NormalColor, 0, 0, 458, 22);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0.25f;
            ui.riskBarFill = fillImage;

            ui.riskZoneText = CreateText(panel, "RiskZoneText", "Зона риска: зелёная", 14, FontStyle.Bold, TextAnchor.MiddleLeft, 18, -292, 458, 24);
            ui.warningRecommendationText = CreateText(panel, "WarningRecommendationText", "Параметры в допустимых пределах", 14, FontStyle.Normal, TextAnchor.UpperLeft, 18, -328, 458, 84, new Color(0.72f, 0.93f, 1f));
            ui.alarmIndicatorImage = CreateImage(panel, "AlarmIndicator", new Color(1f, 0.12f, 0.05f, 0.92f), 18, -430, 458, 26).GetComponent<Image>();
            ui.alarmIndicatorImage.enabled = false;
        }

        private static void RebuildBottomPanel(Transform panel, FlareUIController ui)
        {
            RemoveLayoutComponents(panel);
            ClearChildren(panel);
            ui.bottomViolationsText = CreateText(panel, "BottomViolationsText", "Нарушений нет", 14, FontStyle.Bold, TextAnchor.UpperLeft, 16, -12, 460, 56, new Color(1f, 0.88f, 0.35f));
            ui.bottomRecommendationText = CreateText(panel, "BottomRecommendationText", "Параметры в допустимых пределах", 14, FontStyle.Normal, TextAnchor.UpperLeft, 520, -12, 620, 56, new Color(0.72f, 0.93f, 1f));
            ui.bottomAlarmText = CreateText(panel, "BottomAlarmText", string.Empty, 16, FontStyle.Bold, TextAnchor.MiddleRight, 1240, -12, 620, 56, new Color(1f, 0.32f, 0.24f));
        }

        private static Slider CreateWarningRow(Transform parent, string label, string value, float y, out Component valueText)
        {
            GameObject row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetRect(row.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, y - 34f), new Vector2(-18f, y));

            CreateText(row.transform, "Label_" + label, label, 13, FontStyle.Bold, TextAnchor.MiddleLeft, 0, -3, 96, 24);
            Slider slider = CreateSlider(row.transform, "Slider_" + label, 112, -4, 250, 24);
            valueText = CreateText(row.transform, "Value_" + label, value, 13, FontStyle.Bold, TextAnchor.MiddleRight, 368, -3, 88, 24, new Color(0.72f, 0.93f, 1f));
            return slider;
        }

        private static void WireControllers(PipeFlowController pipeFlowController, List<SensorIndicator> sensors, FlameController flameController)
        {
            FlareInstallationController controller = FindSceneComponent<FlareInstallationController>();
            if (controller == null)
            {
                GameObject root = FindOrCreateRoot("FlareSystemRoot");
                controller = root.AddComponent<FlareInstallationController>();
            }

            controller.pipeFlowController = pipeFlowController;
            controller.flameController = flameController;
            controller.uiController = FindSceneComponent<FlareUIController>();
            controller.sensorIndicators = sensors;

            ArchiveModeController archive = FindSceneComponent<ArchiveModeController>();
            EarlyWarningController warning = FindSceneComponent<EarlyWarningController>();
            LogisticRegressionRiskModel model = FindSceneComponent<LogisticRegressionRiskModel>();
            if (archive != null)
            {
                archive.controller = controller;
                controller.archiveModeController = archive;
            }

            if (warning != null)
            {
                warning.controller = controller;
                warning.riskModel = model;
                warning.pPurgeSlider = controller.uiController != null ? controller.uiController.pPurgeSlider : warning.pPurgeSlider;
                warning.qPurgeSlider = controller.uiController != null ? controller.uiController.qPurgeSlider : warning.qPurgeSlider;
                warning.pFlareSlider = controller.uiController != null ? controller.uiController.pFlareSlider : warning.pFlareSlider;
                warning.ConfigureSliders();
                controller.earlyWarningController = warning;
            }

            controller.riskModel = model;
            controller.riskPanelController = FindSceneComponent<RiskPanelController>();
            if (controller.uiController != null)
            {
                controller.uiController.controller = controller;
                controller.uiController.BindButtons();
            }

            if (archive != null)
            {
                archive.LoadRecords();
            }

            controller.SetArchiveMode();
        }

        private static void RemoveOldFlowObjects()
        {
            DestroyByExactName("FlowWaypointsRoot");
        }

        private static void DeleteGeneratedSensorMarkers()
        {
            string[] exact = { "SensorMarkers", "SensorIndicatorBoxes" };
            for (int i = 0; i < exact.Length; i++)
            {
                DestroyByExactName(exact[i]);
            }

            List<GameObject> toDelete = new List<GameObject>();
            foreach (GameObject go in SceneObjects())
            {
                if (go.name.StartsWith("Sensor_", StringComparison.Ordinal) ||
                    go.name.StartsWith("Indicator_", StringComparison.Ordinal) ||
                    go.name.EndsWith("_Light", StringComparison.Ordinal))
                {
                    if (go.GetComponentInParent<Canvas>() == null)
                    {
                        toDelete.Add(go);
                    }
                }
            }

            foreach (GameObject go in toDelete)
            {
                DestroyImmediateSafe(go);
            }
        }

        private static void RemoveFlameMeshObjects()
        {
            List<GameObject> toDelete = new List<GameObject>();
            foreach (GameObject go in SceneObjects())
            {
                if (go.name.IndexOf("FlameMesh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    go.name.IndexOf("Flame Placeholder Mesh", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    toDelete.Add(go);
                }
            }

            foreach (GameObject go in toDelete)
            {
                DestroyImmediateSafe(go);
            }
        }

        private static Bounds ResolveSeparatorBounds(GameObject modelRoot, Bounds modelBounds)
        {
            if (TryFindNamedBounds(modelRoot, out Bounds bounds, "separator", "knockout", "drum", "vessel", "sep"))
            {
                return bounds;
            }

            Vector3 center = new Vector3(modelBounds.min.x + modelBounds.size.x * 0.20f, modelBounds.min.y + modelBounds.size.y * 0.065f, modelBounds.center.z);
            Vector3 size = new Vector3(Mathf.Max(2.4f, modelBounds.size.x * 0.13f), Mathf.Max(0.9f, modelBounds.size.y * 0.035f), Mathf.Max(1.2f, modelBounds.size.z * 0.18f));
            return new Bounds(center, size);
        }

        private static Vector3 ResolveFlareTop(GameObject modelRoot, Bounds modelBounds)
        {
            GameObject existing = FindSceneObject("FlameEffect");
            if (existing != null)
            {
                Vector3 current = existing.transform.position;
                return new Vector3(current.x, Mathf.Max(current.y, modelBounds.max.y), current.z);
            }

            if (TryFindNamedBounds(modelRoot, out Bounds stackBounds, "flare", "stack", "chimney", "tower", "tip"))
            {
                return new Vector3(stackBounds.center.x, stackBounds.max.y + 0.15f, stackBounds.center.z);
            }

            return new Vector3(modelBounds.center.x, modelBounds.max.y + 0.15f, modelBounds.center.z);
        }

        private static bool TryFindNamedBounds(GameObject root, out Bounds bounds, params string[] names)
        {
            bounds = new Bounds();
            bool hasAny = false;
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                string name = BuildHierarchyName(renderer.transform).ToLowerInvariant();
                bool match = false;
                for (int i = 0; i < names.Length; i++)
                {
                    if (name.Contains(names[i]))
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    continue;
                }

                if (!hasAny)
                {
                    bounds = renderer.bounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasAny;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            if (root == null)
            {
                return new Bounds(Vector3.zero, new Vector3(16f, 34f, 10f));
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(root.transform.position, Vector3.one);
            bool hasAny = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!hasAny)
                {
                    bounds = renderer.bounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private static GameObject FindModelRoot()
        {
            GameObject model = FindSceneObject("Combined_Flare_Installation_Model");
            if (model != null)
            {
                return model;
            }

            GameObject imported = FindSceneObject("ImportedModelRoot");
            if (imported != null && imported.transform.childCount > 0)
            {
                return imported.transform.GetChild(0).gameObject;
            }

            return imported;
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            GameObject root = FindSceneObject(name);
            if (root != null)
            {
                return root;
            }

            return new GameObject(name);
        }

        private static Material LoadMaterial(string name)
        {
            string path = "Assets/Materials/FlareSystem/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Standard"));
            material.name = name;
            return material;
        }

        private static GameObject FindOrCreatePanel(Transform canvas, string name)
        {
            GameObject panel = FindSceneObject(name);
            if (panel == null)
            {
                panel = new GameObject(name, typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(canvas, false);
            }

            RectTransform rect = panel.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = panel.AddComponent<RectTransform>();
            }

            Image image = panel.GetComponent<Image>();
            if (image == null)
            {
                image = panel.AddComponent<Image>();
            }

            image.color = new Color(0.05f, 0.05f, 0.07f, 0.86f);
            panel.transform.SetParent(canvas, false);
            return panel;
        }

        private static Component CreateText(Transform parent, string name, string value, int fontSize, FontStyle style, TextAnchor alignment, float x, float y, float width, float height)
        {
            return CreateText(parent, name, value, fontSize, style, alignment, x, y, width, height, Color.white);
        }

        private static Component CreateText(Transform parent, string name, string value, int fontSize, FontStyle style, TextAnchor alignment, float x, float y, float width, float height, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y - height), new Vector2(x + width, y));
            Text text = go.GetComponent<Text>();
            text.text = value;
            text.font = GetFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, float x, float y, float width, float height)
        {
            GameObject go = CreateImage(parent, name, new Color(0.02f, 0.025f, 0.04f, 0.98f), x, y, width, height);
            Button button = go.AddComponent<Button>();
            Text text = (Text)CreateText(go.transform, "Text", label, 13, FontStyle.Bold, TextAnchor.MiddleCenter, 0, 0, width, height);
            text.raycastTarget = false;
            button.targetGraphic = go.GetComponent<Image>();
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, float x, float y, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y - height), new Vector2(x + width, y));

            GameObject background = CreateImage(go.transform, "Background", new Color(0.11f, 0.12f, 0.14f, 0.95f), 0, 0, width, height);
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            SetRect(fillArea.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(7f, 5f), new Vector2(-7f, -5f));

            GameObject fill = CreateImage(fillArea.transform, "Fill", new Color(0.18f, 0.82f, 0.36f, 0.95f), 0, 0, width - 14f, height - 10f);
            SetStretch(fill.GetComponent<RectTransform>());

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            SetRect(handleArea.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(7f, 0f), new Vector2(-7f, 0f));
            GameObject handle = CreateImage(handleArea.transform, "Handle", Color.white, 0, 0, 18f, height);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(18f, height);
            handleRect.anchoredPosition = Vector2.zero;

            Slider slider = go.GetComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            background.GetComponent<Image>().raycastTarget = true;
            return slider;
        }

        private static GameObject CreateImage(Transform parent, string name, Color color, float x, float y, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y - height), new Vector2(x + width, y));
            Image image = go.GetComponent<Image>();
            image.color = color;
            return go;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediateSafe(parent.GetChild(i).gameObject);
            }
        }

        private static void RemoveLayoutComponents(Transform target)
        {
            foreach (LayoutGroup layout in target.GetComponents<LayoutGroup>())
            {
                DestroyImmediateSafe(layout);
            }

            foreach (ContentSizeFitter fitter in target.GetComponents<ContentSizeFitter>())
            {
                DestroyImmediateSafe(fitter);
            }

            foreach (LayoutElement element in target.GetComponents<LayoutElement>())
            {
                DestroyImmediateSafe(element);
            }
        }

        private static Font GetFont()
        {
            if (builtinFont == null)
            {
                builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return builtinFont;
        }

        private static void CaptureCameraShot(Camera camera, Canvas canvas, string fileName, Vector3 cameraPosition, Vector3 target, float fov, bool archivePanel, bool warningPanel, bool riskPanel, bool tooltip)
        {
            GameObject archive = FindSceneObject("ArchivePanel");
            GameObject warning = FindSceneObject("WarningPanel");
            GameObject risk = FindSceneObject("RiskPanel");
            SensorTooltipController tooltipController = FindSceneComponent<SensorTooltipController>();
            SensorHoverTarget hoverTarget = FindSceneComponent<SensorHoverTarget>();

            if (archive != null)
            {
                archive.SetActive(archivePanel);
            }

            if (warning != null)
            {
                warning.SetActive(warningPanel);
            }

            if (risk != null)
            {
                risk.SetActive(riskPanel);
            }

            if (tooltip && tooltipController != null && hoverTarget != null)
            {
                tooltipController.Show(hoverTarget);
            }
            else if (tooltipController != null)
            {
                tooltipController.Hide();
            }

            camera.transform.position = cameraPosition;
            camera.transform.rotation = Quaternion.LookRotation((target - cameraPosition).normalized, Vector3.up);
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 500f;

            RenderMode oldMode = RenderMode.ScreenSpaceOverlay;
            Camera oldCanvasCamera = null;
            float oldPlaneDistance = 1f;
            if (canvas != null)
            {
                oldMode = canvas.renderMode;
                oldCanvasCamera = canvas.worldCamera;
                oldPlaneDistance = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.gameObject.SetActive(archivePanel || warningPanel || riskPanel || tooltip);
            }

            Canvas.ForceUpdateCanvases();

            RenderTexture texture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            camera.targetTexture = texture;
            RenderTexture.active = texture;
            camera.Render();
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();

            File.WriteAllBytes(Path.Combine(ProjectPath(ScreenshotFolder), fileName), image.EncodeToPNG());

            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(image);

            if (canvas != null)
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCanvasCamera;
                canvas.planeDistance = oldPlaneDistance;
                canvas.gameObject.SetActive(true);
            }
        }

        private static void SimulateParticles()
        {
            foreach (ParticleSystem particleSystem in Resources.FindObjectsOfTypeAll<ParticleSystem>())
            {
                if (particleSystem == null || !particleSystem.gameObject.scene.IsValid())
                {
                    continue;
                }

                particleSystem.Simulate(1.2f, true, true, true);
                particleSystem.Play();
            }
        }

        private static IEnumerable<GameObject> SceneObjects()
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.scene.IsValid() && !EditorUtility.IsPersistent(go))
                {
                    yield return go;
                }
            }
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (GameObject go in SceneObjects())
            {
                if (go.name == name)
                {
                    return go;
                }
            }

            return null;
        }

        private static T FindSceneComponent<T>() where T : Component
        {
            foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            {
                if (component != null && component.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(component))
                {
                    return component;
                }
            }

            return null;
        }

        private static void DestroyByExactName(string name)
        {
            GameObject go = FindSceneObject(name);
            if (go != null)
            {
                DestroyImmediateSafe(go);
            }
        }

        private static void DestroyImmediateSafe(UnityEngine.Object target)
        {
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static string BuildHierarchyName(Transform transform)
        {
            string result = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                result = parent.name + "/" + result;
                parent = parent.parent;
            }

            return result;
        }

        private static void WriteReport(int sensorCount)
        {
            string fullPath = ProjectPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string report =
                "Unity visual fix report\n" +
                "Scene: " + ScenePath + "\n" +
                "Backup: " + BackupPath + "\n\n" +
                "Flow paths fixed:\n" +
                "- ReliefGasFlow recreated from separator toward flare stack on the large gas line.\n" +
                "- PurgeGasFlow recreated as a compact blue route along the low blue/pilot line.\n" +
                "- SteamFlow recreated close to the flare stack up to the tip.\n" +
                "- CondensateFlow recreated as a low route near the base/foundation line.\n\n" +
                "Waypoint objects:\n" +
                "- Old FlowWaypointsRoot and generated flow markers were removed.\n" +
                "- New waypoint transforms were created under FlowWaypointsRoot with small runtime markers only.\n\n" +
                "Sensors:\n" +
                "- Old floating Sensor_* spheres and oversized Sensor_*_Light objects were removed.\n" +
                "- " + sensorCount + " small indicator boxes were placed on or close to the separator, stack and pipe surfaces.\n" +
                "- Hover targets now use these boxes instead of floating markers.\n\n" +
                "Flame:\n" +
                "- FlameMesh/placeholder mesh was removed from the nozzle.\n" +
                "- FlameEffect uses ParticleSystem billboard particles and a Point Light over the flare tip.\n\n" +
                "UI:\n" +
                "- ArchivePanel record slider and buttons were rebuilt within panel bounds.\n" +
                "- WarningPanel rows now use Label -> Slider -> Value layout.\n" +
                "- CanvasScaler is Scale With Screen Size, 1920x1080, Match 0.5.\n\n" +
                "Screenshots:\n" +
                "- " + ProjectPath(ScreenshotFolder) + "\n\n" +
                "Remaining checks:\n" +
                "- Fine manual waypoint nudging can still be done in Scene View if exact pipe centerline differs from the generated route.\n";

            File.WriteAllText(fullPath, report);
            Debug.Log("[FlareVisualFix] Report written to " + fullPath);
        }

        private static string ProjectPath(string relative)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), relative.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
