using UnityEngine;
using UnityEngine.UI;

namespace FlareSystem
{
    public class RiskPanelController : MonoBehaviour
    {
        public GameObject panel;
        public Component modeText;
        public Component parametersText;
        public Component probabilityText;
        public Component recommendationText;
        public Component flameStateText;
        public Image riskZoneImage;
        public Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

            Hide();
        }

        public void Open()
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void UpdatePanel(FlareMode mode, FlareDataRecord record, float probability, RiskLevel riskLevel, string recommendation, string flameState)
        {
            string modeLabel = mode == FlareMode.Archive ? "Архив" : "Раннее предупреждение";
            FlareUIController.SetText(modeText, $"Режим: {modeLabel}");

            if (record != null)
            {
                FlareUIController.SetText(parametersText,
                    $"P_purge: {record.P_purge:0.###} МПа\nQ_purge: {record.Q_purge:0.###} м3/ч\nP_flare: {record.P_flare:0.###} МПа");
            }
            else
            {
                FlareUIController.SetText(parametersText, "Параметры не выбраны");
            }

            FlareUIController.SetText(probabilityText, $"Вероятность отрыва: {probability:P1}\nЗона риска: {RiskToText(riskLevel)}");
            FlareUIController.SetText(recommendationText, recommendation);
            FlareUIController.SetText(flameStateText, $"Пламя: {flameState}");

            if (riskZoneImage != null)
            {
                riskZoneImage.color = FlareConstants.GetColor(riskLevel);
            }
        }

        private static string RiskToText(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Alarm:
                    return "красная аварийная";
                case RiskLevel.Danger:
                    return "красная";
                case RiskLevel.Warning:
                    return "жёлтая";
                default:
                    return "зелёная";
            }
        }
    }
}
