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
    public static class SceneScreenshotCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

        public static void CaptureScreenshots()
        {
            if (SceneManager.GetActiveScene().path != FlareSceneRebuildEditor.RebuiltScenePath)
            {
                EditorSceneManager.OpenScene(FlareSceneRebuildEditor.RebuiltScenePath, OpenSceneMode.Single);
            }

            string outputDir = Path.Combine(Application.dataPath, "..", "Screenshots", "UnityRebuild");
            Directory.CreateDirectory(outputDir);

            Camera camera = Camera.main;
            if (camera == null)
            {
                throw new InvalidOperationException("Main Camera not found.");
            }

            GameObject model = GameObject.Find("Combined_Flare_Installation_Model");
            Bounds bounds = FlareSceneRebuildEditor.CalculateBounds(model);
            Vector3 flareTop = EstimateFlareTop(model, bounds);
            Vector3 target = new Vector3(bounds.center.x, Mathf.Max(1.2f, bounds.center.y * 0.45f), bounds.center.z);
            float radius = Mathf.Max(bounds.extents.magnitude, 10f);

            FlareInstallationController controller = UnityEngine.Object.FindAnyObjectByType<FlareInstallationController>(FindObjectsInactive.Include);
            EarlyWarningController warning = UnityEngine.Object.FindAnyObjectByType<EarlyWarningController>(FindObjectsInactive.Include);
            RiskPanelController riskPanel = UnityEngine.Object.FindAnyObjectByType<RiskPanelController>(FindObjectsInactive.Include);
            FlameController flame = UnityEngine.Object.FindAnyObjectByType<FlameController>(FindObjectsInactive.Include);

            if (controller != null)
            {
                controller.EnsureReferences();
                controller.SetArchiveMode();
            }

            if (flame != null)
            {
                flame.SetNormal();
            }

            SimulateParticles();

            SetCanvasesActive(false);
            Capture(camera, outputDir, "overview_scene.png", bounds.center + new Vector3(radius * 0.85f, radius * 0.42f, -radius * 0.9f), target, 44f);
            Capture(camera, outputDir, "flare_top_fixed.png", flareTop + new Vector3(4.2f, 1.7f, -5.2f), flareTop + Vector3.up * 0.45f, 34f);
            Capture(camera, outputDir, "lighting_materials_check.png", bounds.center + new Vector3(-radius * 0.7f, radius * 0.3f, -radius * 0.45f), new Vector3(bounds.center.x - bounds.extents.x * 0.25f, bounds.center.y * 0.38f, bounds.center.z), 38f);
            SetCanvasesActive(true);

            if (controller != null)
            {
                controller.SetArchiveMode();
            }
            Capture(camera, outputDir, "ui_archive_mode.png", bounds.center + new Vector3(radius * 0.72f, radius * 0.42f, -radius * 0.82f), target, 46f);

            if (warning != null)
            {
                if (controller != null)
                {
                    controller.SetEarlyWarningMode();
                }
                warning.SetInputs(0.21f, 13f, 0.024f);
            }
            Capture(camera, outputDir, "ui_warning_mode.png", bounds.center + new Vector3(radius * 0.72f, radius * 0.42f, -radius * 0.82f), target, 46f);

            if (flame != null)
            {
                flame.SetAlarm();
            }
            SimulateParticles();
            SetCanvasesActive(false);
            Capture(camera, outputDir, "particles_and_flame.png", bounds.center + new Vector3(radius * 0.42f, radius * 0.5f, -radius * 0.72f), new Vector3(flareTop.x, flareTop.y * 0.55f, flareTop.z), 38f);
            SetCanvasesActive(true);

            if (riskPanel != null && controller != null)
            {
                controller.OpenRiskPanel();
            }
            Capture(camera, outputDir, "interaction_risk_panel.png", bounds.center + new Vector3(radius * 0.72f, radius * 0.42f, -radius * 0.82f), target, 46f);

            Debug.Log("Flare Installation screenshots captured to: " + outputDir);
        }

        private static void Capture(Camera camera, string outputDir, string fileName, Vector3 position, Vector3 lookAt, float fieldOfView)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);
            camera.fieldOfView = fieldOfView;

            RenderTexture rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            var previousModes = new Dictionary<Canvas, RenderMode>();
            var previousCameras = new Dictionary<Canvas, Camera>();
            var previousDistances = new Dictionary<Canvas, float>();

            foreach (Canvas canvas in canvases)
            {
                previousModes[canvas] = canvas.renderMode;
                previousCameras[canvas] = canvas.worldCamera;
                previousDistances[canvas] = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }

            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = rt;
            RenderTexture.active = rt;
            camera.Render();
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();

            File.WriteAllBytes(Path.Combine(outputDir, fileName), texture.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = previous;
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = previousModes[canvas];
                canvas.worldCamera = previousCameras[canvas];
                canvas.planeDistance = previousDistances[canvas];
            }

            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(rt);
        }

        private static void SetCanvasesActive(bool active)
        {
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas.gameObject.scene.IsValid())
                {
                    canvas.gameObject.SetActive(active);
                }
            }
        }

        private static void SimulateParticles()
        {
            foreach (ParticleSystem system in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
            {
                system.gameObject.SetActive(true);
                system.Simulate(1.2f, true, true, true);
            }
        }

        private static Vector3 EstimateFlareTop(GameObject model, Bounds bounds)
        {
            Renderer highest = model != null
                ? model.GetComponentsInChildren<Renderer>(true).OrderByDescending(r => r.bounds.max.y).FirstOrDefault()
                : null;

            if (highest == null)
            {
                return bounds.center + Vector3.up * bounds.extents.y;
            }

            return new Vector3(highest.bounds.center.x, highest.bounds.max.y + 0.35f, highest.bounds.center.z);
        }
    }
}
