using UnityEngine;

namespace FlareSystem
{
    public class LogisticRegressionRiskModel : MonoBehaviour
    {
        [SerializeField] public float b0 = 1169.107147f;
        [SerializeField] public float b1 = 5.798965f;
        [SerializeField] public float b2 = -57.489005f;
        [SerializeField] public float b3 = 33.698909f;

        public float CalculateProbability(float pPurge, float qPurge, float pFlare)
        {
            float z = b0 + b1 * pPurge + b2 * qPurge + b3 * pFlare;
            z = Mathf.Clamp(z, -60f, 60f);
            return 1f / (1f + Mathf.Exp(-z));
        }

        public RiskLevel GetRiskLevel(float probability)
        {
            return FlareConstants.ProbabilityToRiskLevel(probability);
        }
    }
}
