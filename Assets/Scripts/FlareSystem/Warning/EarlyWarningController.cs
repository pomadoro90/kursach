using UnityEngine;
using UnityEngine.UI;

namespace FlareSystem
{
    public class EarlyWarningController : MonoBehaviour
    {
        public FlareInstallationController controller;
        public LogisticRegressionRiskModel riskModel;

        [Header("UI Sliders")]
        public Slider pPurgeSlider;
        public Slider qPurgeSlider;
        public Slider pFlareSlider;

        [Header("Current Inputs")]
        public float currentPPurge = 0.35f;
        public float currentQPurge = 30f;
        public float currentPFlare = 0.012f;

        public float CurrentProbability { get; private set; }
        public RiskLevel CurrentRiskLevel { get; private set; } = RiskLevel.Normal;
        public string CurrentRecommendation { get; private set; } = "Параметры в допустимых пределах";

        private bool suppressSliderEvents;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<FlareInstallationController>();
            }

            if (riskModel == null)
            {
                riskModel = GetComponent<LogisticRegressionRiskModel>();
            }
        }

        private void Start()
        {
            ConfigureSliders();
            Recalculate();
        }

        public void ConfigureSliders()
        {
            suppressSliderEvents = true;
            ConfigureSlider(pPurgeSlider, 0.14f, 0.60f, currentPPurge);
            ConfigureSlider(qPurgeSlider, 7f, 50f, currentQPurge);
            ConfigureSlider(pFlareSlider, 0.003f, 0.028f, currentPFlare);
            suppressSliderEvents = false;

            if (pPurgeSlider != null)
            {
                pPurgeSlider.onValueChanged.RemoveListener(OnSliderChanged);
                pPurgeSlider.onValueChanged.AddListener(OnSliderChanged);
            }

            if (qPurgeSlider != null)
            {
                qPurgeSlider.onValueChanged.RemoveListener(OnSliderChanged);
                qPurgeSlider.onValueChanged.AddListener(OnSliderChanged);
            }

            if (pFlareSlider != null)
            {
                pFlareSlider.onValueChanged.RemoveListener(OnSliderChanged);
                pFlareSlider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        public void SetInputs(float pPurge, float qPurge, float pFlare)
        {
            currentPPurge = Mathf.Clamp(pPurge, 0.14f, 0.60f);
            currentQPurge = Mathf.Clamp(qPurge, 7f, 50f);
            currentPFlare = Mathf.Clamp(pFlare, 0.003f, 0.028f);

            suppressSliderEvents = true;
            if (pPurgeSlider != null)
            {
                pPurgeSlider.value = currentPPurge;
            }

            if (qPurgeSlider != null)
            {
                qPurgeSlider.value = currentQPurge;
            }

            if (pFlareSlider != null)
            {
                pFlareSlider.value = currentPFlare;
            }

            suppressSliderEvents = false;
            Recalculate();
        }

        public void OnSliderChanged(float _)
        {
            if (suppressSliderEvents)
            {
                return;
            }

            if (pPurgeSlider != null)
            {
                currentPPurge = pPurgeSlider.value;
            }

            if (qPurgeSlider != null)
            {
                currentQPurge = qPurgeSlider.value;
            }

            if (pFlareSlider != null)
            {
                currentPFlare = pFlareSlider.value;
            }

            Recalculate();
        }

        public void Recalculate()
        {
            if (riskModel == null)
            {
                riskModel = GetComponent<LogisticRegressionRiskModel>();
            }

            CurrentProbability = riskModel != null
                ? riskModel.CalculateProbability(currentPPurge, currentQPurge, currentPFlare)
                : 0f;

            CurrentRiskLevel = FlareConstants.ProbabilityToRiskLevel(CurrentProbability);
            CurrentRecommendation = BuildRecommendation();

            if (controller != null && controller.CurrentMode == FlareMode.EarlyWarning)
            {
                controller.ApplyRisk(CurrentProbability);
            }
        }

        public FlareDataRecord BuildSyntheticRecord()
        {
            return new FlareDataRecord
            {
                N = 0,
                P_flare = currentPFlare,
                Q_flare = 2.2f,
                P_purge = currentPPurge,
                Q_purge = currentQPurge,
                T_flame = CurrentProbability > 0.7f ? 760f : 980f,
                Steam_Q = 110f,
                otriv = CurrentProbability > 0.8f ? 1 : 0,
                hlopok = currentPFlare > FlareConstants.HlopokPFlareAlarm ? 1 : 0
            };
        }

        public string BuildRecommendation()
        {
            if (CurrentProbability < 0.3f)
            {
                return "Параметры в допустимых пределах";
            }

            if (currentPPurge < 0.28f)
            {
                return "Увеличить давление продувочного газа";
            }

            if (currentQPurge < 22f)
            {
                return "Увеличить расход продувочного газа";
            }

            if (currentPFlare > 0.022f)
            {
                return "Снизить давление сбросного газа";
            }

            return "Проверить режим продувки и устойчивость горения";
        }

        private static void ConfigureSlider(Slider slider, float min, float max, float value)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp(value, min, max);
        }
    }
}
