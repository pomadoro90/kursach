using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace FlareSystem
{
    public class FlareUIController : MonoBehaviour
    {
        [Header("Controller")]
        public FlareInstallationController controller;

        [Header("Root Panels")]
        public GameObject archivePanel;
        public GameObject warningPanel;
        public GameObject tooltipPanel;
        public GameObject riskPanel;

        [Header("Top")]
        public Button archiveModeButton;
        public Button earlyWarningModeButton;
        public Component titleText;
        public Component modeText;
        public Component statusText;
        public Image statusImage;

        [Header("Archive")]
        public Button previousButton;
        public Button nextButton;
        public Button playPauseButton;
        public Slider recordSlider;
        public Component playPauseButtonText;
        public Component recordIndexText;
        public Component archiveValuesText;
        public Component archiveViolationsText;
        public Component archiveRecommendationText;

        [Header("Early Warning")]
        public Slider pPurgeSlider;
        public Slider qPurgeSlider;
        public Slider pFlareSlider;
        public Component pPurgeValueText;
        public Component qPurgeValueText;
        public Component pFlareValueText;
        public Component probabilityText;
        public Component riskZoneText;
        public Image riskBarFill;
        public Image alarmIndicatorImage;
        public Component warningRecommendationText;

        [Header("Bottom")]
        public Component bottomViolationsText;
        public Component bottomRecommendationText;
        public Component bottomAlarmText;

        private bool suppressSliderEvents;

        private void Awake()
        {
            if (controller == null)
            {
#if UNITY_2023_1_OR_NEWER
                controller = Object.FindAnyObjectByType<FlareInstallationController>(FindObjectsInactive.Include);
#else
                controller = Object.FindObjectOfType<FlareInstallationController>(true);
#endif
            }

            BindButtons();
            SetText(titleText, "Интерактивная модель факельной установки");
        }

        public void BindButtons()
        {
            if (archiveModeButton != null)
            {
                archiveModeButton.onClick.RemoveAllListeners();
                archiveModeButton.onClick.AddListener(() => controller?.SetArchiveMode());
            }

            if (earlyWarningModeButton != null)
            {
                earlyWarningModeButton.onClick.RemoveAllListeners();
                earlyWarningModeButton.onClick.AddListener(() => controller?.SetEarlyWarningMode());
            }

            if (previousButton != null)
            {
                previousButton.onClick.RemoveAllListeners();
                previousButton.onClick.AddListener(() => controller?.archiveModeController?.PreviousRecord());
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(() => controller?.archiveModeController?.NextRecord());
            }

            if (playPauseButton != null)
            {
                playPauseButton.onClick.RemoveAllListeners();
                playPauseButton.onClick.AddListener(() => controller?.archiveModeController?.TogglePlayPause());
            }

            if (recordSlider != null)
            {
                recordSlider.onValueChanged.RemoveAllListeners();
                recordSlider.onValueChanged.AddListener(OnRecordSliderChanged);
            }
        }

        public void ShowMode(FlareMode mode)
        {
            if (archivePanel != null)
            {
                archivePanel.SetActive(mode == FlareMode.Archive);
            }

            if (warningPanel != null)
            {
                warningPanel.SetActive(mode == FlareMode.EarlyWarning);
            }

            SetText(modeText, mode == FlareMode.Archive ? "Режим: Архив" : "Режим: Раннее предупреждение");
        }

        public void UpdateArchive(FlareDataRecord record, FlareSystemState state, int index, int count, bool isPlaying)
        {
            suppressSliderEvents = true;
            if (recordSlider != null)
            {
                recordSlider.minValue = 0f;
                recordSlider.maxValue = Mathf.Max(0, count - 1);
                recordSlider.wholeNumbers = true;
                recordSlider.value = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
            }
            suppressSliderEvents = false;

            SetText(playPauseButtonText, isPlaying ? "Пауза" : "Пуск");
            SetText(recordIndexText, count > 0 ? $"Запись {index + 1} из {count}" : "Записи не загружены");

            if (record != null)
            {
                SetText(archiveValuesText,
                    $"P_flare: {record.P_flare:0.###} МПа\n" +
                    $"Q_flare: {record.Q_flare:0.###} м3/ч\n" +
                    $"P_purge: {record.P_purge:0.###} МПа\n" +
                    $"Q_purge: {record.Q_purge:0.###} м3/ч\n" +
                    $"T_flame: {record.T_flame:0.#} °C\n" +
                    $"Steam_Q: {record.Steam_Q:0.###} кг/ч\n" +
                    $"otriv: {record.otriv}\n" +
                    $"hlopok: {record.hlopok}");
            }

            SetText(archiveViolationsText, state != null ? state.ViolationsText : "Нарушений нет");
            SetText(archiveRecommendationText, state != null ? state.Recommendation : "Параметры в допустимых пределах");
            SetText(bottomViolationsText, state != null ? state.ViolationsText : "Нарушений нет");
            SetText(bottomRecommendationText, state != null ? state.Recommendation : "Параметры в допустимых пределах");
            UpdateStatus(state);
        }

        public void UpdateEarlyWarning(float pPurge, float qPurge, float pFlare, float probability, RiskLevel riskLevel, string recommendation)
        {
            SetText(pPurgeValueText, $"{pPurge:0.###} МПа");
            SetText(qPurgeValueText, $"{qPurge:0.###} м3/ч");
            SetText(pFlareValueText, $"{pFlare:0.###} МПа");
            SetText(probabilityText, $"Вероятность отрыва: {probability:P1}");
            SetText(riskZoneText, $"Зона риска: {RiskToText(riskLevel)}");
            SetText(warningRecommendationText, recommendation);
            SetText(bottomRecommendationText, recommendation);

            if (riskBarFill != null)
            {
                riskBarFill.fillAmount = Mathf.Clamp01(probability);
                riskBarFill.color = FlareConstants.GetColor(riskLevel);
            }

            if (alarmIndicatorImage != null)
            {
                alarmIndicatorImage.enabled = riskLevel == RiskLevel.Alarm;
                alarmIndicatorImage.color = FlareConstants.DangerColor;
            }

            if (controller != null && controller.CurrentState != null)
            {
                UpdateStatus(controller.CurrentState);
            }
        }

        public void UpdateStatus(FlareSystemState state)
        {
            if (state == null)
            {
                SetText(statusText, "Статус: не инициализировано");
                if (statusImage != null)
                {
                    statusImage.color = Color.gray;
                }

                return;
            }

            SetText(statusText, $"Статус: {state.StatusText}");
            SetText(bottomAlarmText, state.IsAlarm ? "АВАРИЯ: высокий риск отрыва пламени" : string.Empty);
            if (statusImage != null)
            {
                statusImage.color = FlareConstants.GetColor(state.RiskLevel);
            }
        }

        public void OnRecordSliderChanged(float value)
        {
            if (suppressSliderEvents || controller == null || controller.archiveModeController == null)
            {
                return;
            }

            controller.archiveModeController.SetRecordIndex(Mathf.RoundToInt(value));
        }

        public static void SetText(Component textComponent, string value)
        {
            if (textComponent == null)
            {
                return;
            }

            if (textComponent is Text uiText)
            {
                uiText.text = value;
                return;
            }

            PropertyInfo property = textComponent.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite && property.PropertyType == typeof(string))
            {
                property.SetValue(textComponent, value, null);
            }
        }

        public static void SetTextColor(Component textComponent, Color color)
        {
            if (textComponent == null)
            {
                return;
            }

            if (textComponent is Graphic graphic)
            {
                graphic.color = color;
                return;
            }

            PropertyInfo property = textComponent.GetType().GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite && property.PropertyType == typeof(Color))
            {
                property.SetValue(textComponent, color, null);
            }
        }

        private static string RiskToText(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Alarm:
                    return "аварийная";
                case RiskLevel.Danger:
                    return "опасная";
                case RiskLevel.Warning:
                    return "предупреждение";
                default:
                    return "норма";
            }
        }
    }
}
