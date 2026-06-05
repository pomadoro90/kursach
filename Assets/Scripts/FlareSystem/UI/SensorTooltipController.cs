using UnityEngine;

namespace FlareSystem
{
    public class SensorTooltipController : MonoBehaviour
    {
        public GameObject tooltipPanel;
        public Component tooltipText;
        public RectTransform tooltipRect;
        public Camera raycastCamera;
        public LayerMask raycastMask = ~0;
        public Vector2 screenOffset = new Vector2(18f, -18f);

        private SensorHoverTarget currentTarget;

        private void Awake()
        {
            Hide();
        }

        private void Update()
        {
            UpdateRaycast();

            if (tooltipPanel != null && tooltipPanel.activeSelf && tooltipRect != null)
            {
                if (FlareInput.TryGetPointerPosition(out Vector2 pointerPosition))
                {
                    tooltipRect.position = pointerPosition + screenOffset;
                }
            }
        }

        public void Show(SensorHoverTarget target)
        {
            currentTarget = target;
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(true);
            }

            FlareUIController.SetText(tooltipText, target != null ? target.GetTooltipText() : string.Empty);
        }

        public void Hide()
        {
            currentTarget = null;
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }

        private void UpdateRaycast()
        {
            Camera cameraToUse = raycastCamera != null ? raycastCamera : Camera.main;
            if (cameraToUse == null)
            {
                return;
            }

            if (!FlareInput.TryGetPointerPosition(out Vector2 pointerPosition))
            {
                return;
            }

            Ray ray = cameraToUse.ScreenPointToRay(pointerPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, raycastMask, QueryTriggerInteraction.Collide))
            {
                SensorHoverTarget target = hit.collider.GetComponentInParent<SensorHoverTarget>();
                if (target != null)
                {
                    if (currentTarget != target)
                    {
                        Show(target);
                    }

                    return;
                }
            }

            if (currentTarget != null)
            {
                Hide();
            }
        }
    }
}
