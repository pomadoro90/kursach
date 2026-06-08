using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace FlareSystem
{
    public class FieldAutoSetup : MonoBehaviour
    {
        private bool configured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapScene()
        {
            GameObject root = GameObject.Find(FieldModelConstants.FieldRootName);
            if (root == null)
            {
                root = new GameObject(FieldModelConstants.FieldRootName);
            }

            FieldAutoSetup setup = GetOrAdd<FieldAutoSetup>(root);
            setup.ConfigureScene();
        }

        private void Awake()
        {
            ConfigureScene();
        }

        private void Start()
        {
            ConfigureScene();
        }

        public void ConfigureScene()
        {
            if (configured)
            {
                return;
            }

            configured = true;

            GameObject root = gameObject.name == FieldModelConstants.FieldRootName
                ? gameObject
                : FindOrCreate(FieldModelConstants.FieldRootName);

            if (root != gameObject && root.GetComponent<FieldAutoSetup>() == null)
            {
                root.AddComponent<FieldAutoSetup>().ConfigureScene();
                return;
            }

            FlareInstallationController controller = GetOrAdd<FlareInstallationController>(root);
            ArchiveModeController archive = GetOrAdd<ArchiveModeController>(root);
            EarlyWarningController warning = GetOrAdd<EarlyWarningController>(root);
            LogisticRegressionRiskModel risk = GetOrAdd<LogisticRegressionRiskModel>(root);
            FieldObjectResolver resolver = GetOrAdd<FieldObjectResolver>(root);
            FieldPipeFlowController pipeFlow = GetOrAdd<FieldPipeFlowController>(root);
            FieldFlameController flame = EnsureFieldFlame(root.transform);

            Transform modelRoot = FindModelRoot(root.transform);
            if (modelRoot != null)
            {
                resolver.modelRoot = modelRoot;
            }
            else if (resolver.modelRoot == null)
            {
                resolver.modelRoot = root.transform;
            }

            controller.archiveModeController = archive;
            controller.earlyWarningController = warning;
            controller.riskModel = risk;
            controller.flameController = flame;
            controller.pipeFlowController = pipeFlow;
            archive.controller = controller;
            archive.csvFileName = FieldModelConstants.DefaultCsvFileName;
            warning.controller = controller;
            warning.riskModel = risk;

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
            Transform flareStackBase = EnsureFacilityAnchor(root.transform, "FlareStackBase", FieldModelConstants.FlareStackBasePosition);

            RiskPanelController riskPanel = EnsureRiskPanel();
            controller.riskPanelController = riskPanel;

            Camera camera = EnsureCamera(root.transform);
            EnsureClickTarget(resolver, flareStackBase, controller, riskPanel);
            EnsureLight();
            EnsureEventSystem();

            if (camera != null)
            {
                camera.gameObject.SetActive(true);
            }

            controller.EnsureReferences();
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

        private static Transform EnsureFacilityAnchor(Transform root, string name, Vector3 position)
        {
            Transform existing = root.Find(name);
            if (existing != null)
            {
                existing.position = position;
                return existing;
            }

            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(root, true);
            anchor.transform.position = position;
            return anchor.transform;
        }

        private static Transform FindModelRoot(Transform setupRoot)
        {
            Transform named = FindTransformByName("combined_field_model");
            if (named != null && named != setupRoot)
            {
                return named;
            }

            MeshRenderer renderer = FindObject<MeshRenderer>();
            if (renderer == null)
            {
                return null;
            }

            Transform candidate = renderer.transform;
            while (candidate.parent != null && candidate.parent != setupRoot)
            {
                candidate = candidate.parent;
            }

            return candidate != setupRoot ? candidate : renderer.transform;
        }

        private static Transform FindTransformByName(string objectName)
        {
#if UNITY_2023_1_OR_NEWER
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
#endif
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static Camera EnsureCamera(Transform root)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = FindObject<Camera>();
            }

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

            CameraController cameraController = GetOrAdd<CameraController>(camera.gameObject);
            cameraController.controlledCamera = camera;
            if (cameraController.orbitTarget == null)
            {
                Transform target = root.Find("CameraOrbitTarget");
                if (target == null)
                {
                    GameObject targetObject = new GameObject("CameraOrbitTarget");
                    targetObject.transform.SetParent(root, true);
                    targetObject.transform.position = new Vector3(12f, 8f, 4f);
                    target = targetObject.transform;
                }

                cameraController.orbitTarget = target;
            }

            return camera;
        }

        private static void EnsureClickTarget(FieldObjectResolver resolver, Transform fallback, FlareInstallationController controller, RiskPanelController riskPanel)
        {
            Transform target = resolver != null ? resolver.FindFieldFlareStack() : null;
            if (target == null)
            {
                target = fallback;
            }

            if (target == null)
            {
                return;
            }

            Collider collider = target.GetComponent<Collider>();
            if (collider == null)
            {
                BoxCollider box = target.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(2f, 2f, 12f);
                box.center = Vector3.up * 6f;
            }

            FlareStackClickTarget clickTarget = GetOrAdd<FlareStackClickTarget>(target.gameObject);
            clickTarget.controller = controller;
            clickTarget.riskPanel = riskPanel;
            clickTarget.flareStackObject = target.gameObject;
        }

        private static RiskPanelController EnsureRiskPanel()
        {
            RiskPanelController existing = FindObject<RiskPanelController>();
            if (existing != null)
            {
                return existing;
            }

            GameObject canvasObject = new GameObject("Risk Panel Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("RiskPanel");
            panel.transform.SetParent(canvasObject.transform, false);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.07f, 0.08f, 0.9f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-24f, 0f);
            panelRect.sizeDelta = new Vector2(360f, 300f);

            RiskPanelController riskPanel = canvasObject.AddComponent<RiskPanelController>();
            riskPanel.panel = panel;
            riskPanel.modeText = CreateText(panel.transform, "ModeText", new Vector2(20f, -22f), new Vector2(320f, 28f), 18, TextAnchor.MiddleLeft);
            riskPanel.parametersText = CreateText(panel.transform, "ParametersText", new Vector2(20f, -62f), new Vector2(320f, 76f), 15, TextAnchor.UpperLeft);
            riskPanel.probabilityText = CreateText(panel.transform, "ProbabilityText", new Vector2(20f, -144f), new Vector2(320f, 48f), 15, TextAnchor.UpperLeft);
            riskPanel.recommendationText = CreateText(panel.transform, "RecommendationText", new Vector2(20f, -198f), new Vector2(320f, 44f), 15, TextAnchor.UpperLeft);
            riskPanel.flameStateText = CreateText(panel.transform, "FlameStateText", new Vector2(20f, -246f), new Vector2(220f, 28f), 15, TextAnchor.MiddleLeft);

            GameObject zone = new GameObject("RiskZone");
            zone.transform.SetParent(panel.transform, false);
            riskPanel.riskZoneImage = zone.AddComponent<Image>();
            RectTransform zoneRect = zone.GetComponent<RectTransform>();
            zoneRect.anchorMin = new Vector2(1f, 0f);
            zoneRect.anchorMax = new Vector2(1f, 0f);
            zoneRect.pivot = new Vector2(1f, 0f);
            zoneRect.anchoredPosition = new Vector2(-20f, 20f);
            zoneRect.sizeDelta = new Vector2(44f, 12f);

            riskPanel.closeButton = CreateCloseButton(panel.transform);
            riskPanel.closeButton.onClick.AddListener(riskPanel.Hide);
            riskPanel.Hide();
            return riskPanel;
        }

        private static Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        private static Button CreateCloseButton(Transform parent)
        {
            GameObject buttonObject = new GameObject("CloseButton");
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.22f, 1f);
            Button button = buttonObject.AddComponent<Button>();

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-12f, -12f);
            rect.sizeDelta = new Vector2(28f, 24f);

            Text label = CreateText(buttonObject.transform, "Text", Vector2.zero, new Vector2(28f, 24f), 16, TextAnchor.MiddleCenter);
            label.text = "x";
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;

            return button;
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

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindObject<EventSystem>();
            if (eventSystem != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
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
    }
}
