using UnityEngine;

namespace FlareSystem
{
    [RequireComponent(typeof(Collider))]
    public class FlareStackClickTarget : MonoBehaviour
    {
        public FlareInstallationController controller;
        public RiskPanelController riskPanel;
        public GameObject flareStackObject;

        private void Awake()
        {
            if (controller == null)
            {
#if UNITY_2023_1_OR_NEWER
                controller = Object.FindAnyObjectByType<FlareInstallationController>(FindObjectsInactive.Include);
#else
                controller = Object.FindObjectOfType<FlareInstallationController>();
#endif
            }

            if (riskPanel == null)
            {
#if UNITY_2023_1_OR_NEWER
                riskPanel = Object.FindAnyObjectByType<RiskPanelController>(FindObjectsInactive.Include);
#else
                riskPanel = Object.FindObjectOfType<RiskPanelController>();
#endif
            }
        }

        private void OnMouseDown()
        {
            OpenRiskPanel();
        }

        private void Update()
        {
            if (!FlareInput.PrimaryPointerPressedThisFrame())
            {
                return;
            }

            Camera cameraToUse = Camera.main;
            if (cameraToUse == null || !FlareInput.TryGetPointerPosition(out Vector2 pointerPosition))
            {
                return;
            }

            Ray ray = cameraToUse.ScreenPointToRay(pointerPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Collide))
            {
                FlareStackClickTarget target = hit.collider.GetComponentInParent<FlareStackClickTarget>();
                if (target == this)
                {
                    OpenRiskPanel();
                }
            }
        }

        public void OpenRiskPanel()
        {
            if (controller != null)
            {
                controller.OpenRiskPanel();
            }
            else if (riskPanel != null)
            {
                riskPanel.Open();
            }
            else
            {
                Debug.LogWarning("FlareStackClickTarget: RiskPanelController не найден.");
            }
        }
    }
}
