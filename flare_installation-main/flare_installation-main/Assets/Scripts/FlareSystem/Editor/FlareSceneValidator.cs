using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlareSystem.Editor
{
    public static class FlareSceneValidator
    {
        private const string ReportPath = "Reports/unity_scene_validation_report.txt";

        [MenuItem("Tools/Flare Installation/Validate Scene")]
        public static void ValidateScene()
        {
            if (File.Exists(ProjectPath(FlareSceneRebuildEditor.RebuiltScenePath)) && SceneManager.GetActiveScene().path != FlareSceneRebuildEditor.RebuiltScenePath)
            {
                EditorSceneManager.OpenScene(FlareSceneRebuildEditor.RebuiltScenePath, OpenSceneMode.Single);
            }

            List<string> lines = new List<string>();
            int errors = 0;
            int warnings = 0;

            Check(File.Exists(ProjectPath(FlareSceneRebuildEditor.RebuiltScenePath)), "сцена существует: " + FlareSceneRebuildEditor.RebuiltScenePath, true);
            Check(FindSceneObject("Combined_Flare_Installation_Model") != null, "FBX объект находится в сцене", true);
            Check(!HasFloatingImportedRenderer(out string floatingDetails), "нет отдельного объекта факела, висящего в воздухе", true, floatingDetails);
            Check(Camera.main != null, "есть Main Camera", true);
            Check(FindObjects<Light>().Any(l => l.type == LightType.Directional), "есть Directional Light", true);
            Check(FindObject<Canvas>() != null, "есть Canvas", true);
            Check(FindObject<EventSystem>() != null, "есть EventSystem", true);
            Check(FindSceneObject("ArchivePanel") != null, "есть ArchivePanel", true);
            Check(FindSceneObject("WarningPanel") != null, "есть WarningPanel", true);
            Check(FindSceneObject("FlameEffect") != null, "есть FlameEffect", true);
            Check(FindObject<PipeFlowController>() != null, "есть PipeFlowController", true);
            Check(FindSceneObject("FlowWaypointsRoot") != null, "есть FlowWaypointsRoot", true);
            Check(File.Exists(ProjectPath(FlareSceneRebuildEditor.CsvPath)), "CSV найден: " + FlareSceneRebuildEditor.CsvPath, true);
            Check(UiButtonsAssigned(out string uiDetails), "UI кнопки назначены", true, uiDetails);
            Check(CountMissingScripts() == 0, "нет Missing Script", true, CountMissingScripts() + " missing script references");
            Check(CanStartControllers(out string startDetails), "контроллеры стартуют без NullReferenceException в edit validation", true, startDetails);

            string report = "Unity Scene Validation Report\n" +
                            "Scene: " + SceneManager.GetActiveScene().path + "\n" +
                            "Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n" +
                            string.Join("\n", lines);

            string fullReportPath = Path.Combine(Application.dataPath, "..", ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath));
            File.WriteAllText(fullReportPath, report, Encoding.UTF8);

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
                    return;
                }

                lines.Add((critical ? "[ERROR] " : "[WARN] ") + message + (string.IsNullOrWhiteSpace(details) ? string.Empty : " — " + details));
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

        private static bool HasFloatingImportedRenderer(out string details)
        {
            details = string.Empty;
            GameObject imported = FindSceneObject("ImportedModelRoot");
            if (imported == null)
            {
                details = "ImportedModelRoot не найден";
                return true;
            }

            Renderer[] renderers = imported.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy)
                .OrderByDescending(r => r.bounds.max.y)
                .ToArray();

            if (renderers.Length < 2)
            {
                return false;
            }

            Renderer highest = renderers[0];
            float nextMax = renderers.Skip(1).Max(r => r.bounds.max.y);
            float verticalGap = highest.bounds.min.y - nextMax;
            if (verticalGap > 2.5f)
            {
                details = $"{highest.name}: gap {verticalGap:0.00} above the rest of model";
                return true;
            }

            return false;
        }

        private static bool UiButtonsAssigned(out string details)
        {
            details = string.Empty;
            FlareUIController ui = FindObject<FlareUIController>();
            if (ui == null)
            {
                details = "FlareUIController не найден";
                return false;
            }

            bool ok = ui.archiveModeButton != null &&
                      ui.earlyWarningModeButton != null &&
                      ui.previousButton != null &&
                      ui.nextButton != null &&
                      ui.playPauseButton != null &&
                      ui.recordSlider != null &&
                      ui.pPurgeSlider != null &&
                      ui.qPurgeSlider != null &&
                      ui.pFlareSlider != null;

            if (!ok)
            {
                details = "не все Button/Slider поля заполнены";
            }

            return ok;
        }

        private static bool CanStartControllers(out string details)
        {
            details = string.Empty;
            try
            {
                FlareInstallationController controller = FindObject<FlareInstallationController>();
                if (controller == null)
                {
                    details = "FlareInstallationController не найден";
                    return false;
                }

                controller.EnsureReferences();
                controller.SetArchiveMode();
                controller.SetEarlyWarningMode();
                controller.SetArchiveMode();
                return true;
            }
            catch (Exception ex)
            {
                details = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static int CountMissingScripts()
        {
            int count = 0;
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene.IsValid())
                {
                    count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                }
            }

            return count;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene.IsValid() && go.name == objectName)
                {
                    return go;
                }
            }

            return null;
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

        private static string ProjectPath(string assetPath)
        {
            if (assetPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                string relative = assetPath.Substring("Assets".Length).TrimStart('/', '\\');
                return Path.Combine(Application.dataPath, relative);
            }

            return Path.Combine(Application.dataPath, "..", assetPath);
        }
    }
}
