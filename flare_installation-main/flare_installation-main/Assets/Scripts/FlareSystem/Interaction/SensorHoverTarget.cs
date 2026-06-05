using UnityEngine;

namespace FlareSystem
{
    [RequireComponent(typeof(Collider))]
    public class SensorHoverTarget : MonoBehaviour
    {
        public SensorIndicator indicator;

        private void Awake()
        {
            if (indicator == null)
            {
                indicator = GetComponentInParent<SensorIndicator>();
            }
        }

        public string GetTooltipText()
        {
            return indicator != null ? indicator.BuildTooltipText() : name;
        }
    }
}
