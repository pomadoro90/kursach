using UnityEngine;

namespace FlareSystem
{
    public class SensorIndicator : MonoBehaviour
    {
        public string sensorName = "Датчик";
        public string parameterKey = "P_purge";
        public float currentValue;
        public string normalRangeText = "Не задано";
        public Renderer indicatorRenderer;
        public Light indicatorLight;
        public Material normalMaterial;
        public Material warningMaterial;
        public Material alarmMaterial;

        public RiskLevel CurrentLevel { get; private set; } = RiskLevel.Normal;

        private void Awake()
        {
            if (indicatorRenderer == null)
            {
                indicatorRenderer = GetComponentInChildren<Renderer>(true);
            }

            if (indicatorLight == null)
            {
                indicatorLight = GetComponentInChildren<Light>(true);
            }

            if (string.IsNullOrWhiteSpace(normalRangeText) || normalRangeText == "Не задано")
            {
                normalRangeText = FlareConstants.GetNormalRangeText(parameterKey);
            }

            ApplyColor(FlareConstants.NormalColor);
        }

        public void UpdateFromRecord(FlareDataRecord record)
        {
            currentValue = FlareConstants.GetParameterValue(record, parameterKey);
            SetValue(currentValue, FlareConstants.GetParameterRisk(record, parameterKey));
        }

        public void SetValue(float value, RiskLevel level)
        {
            currentValue = value;
            CurrentLevel = level;
            ApplyColor(FlareConstants.GetColor(level));
        }

        public string BuildTooltipText()
        {
            return $"{sensorName}\nПараметр: {parameterKey}\nЗначение: {FormatValue(currentValue)}\nНорма: {normalRangeText}\nСтатус: {GetStatusText()}";
        }

        public string GetStatusText()
        {
            switch (CurrentLevel)
            {
                case RiskLevel.Alarm:
                    return "авария";
                case RiskLevel.Danger:
                    return "опасная зона";
                case RiskLevel.Warning:
                    return "ниже/выше нормы";
                default:
                    return "норма";
            }
        }

        private void ApplyColor(Color color)
        {
            Material material = CurrentLevel == RiskLevel.Alarm || CurrentLevel == RiskLevel.Danger
                ? alarmMaterial
                : CurrentLevel == RiskLevel.Warning
                    ? warningMaterial
                    : normalMaterial;

            if (indicatorRenderer != null)
            {
                if (material != null)
                {
                    indicatorRenderer.sharedMaterial = material;
                }
                else
                {
                    indicatorRenderer.material.color = color;
                }
            }

            if (indicatorLight != null)
            {
                indicatorLight.color = color;
                indicatorLight.intensity = CurrentLevel == RiskLevel.Normal ? 0.8f : 2.2f;
                indicatorLight.enabled = true;
            }
        }

        private static string FormatValue(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
