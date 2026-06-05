using UnityEngine;

namespace FlareSystem
{
    public class FlowParticleMover : MonoBehaviour
    {
        public PipeFlowPath path;
        public float offset;
        public float speedMultiplier = 1f;
        public Renderer markerRenderer;

        private void Awake()
        {
            if (markerRenderer == null)
            {
                markerRenderer = GetComponentInChildren<Renderer>(true);
            }
        }

        private void Update()
        {
            if (path == null)
            {
                return;
            }

            float length = Mathf.Max(path.GetLength(), 0.1f);
            offset += Time.deltaTime * path.speed * speedMultiplier / length;
            if (path.loop)
            {
                offset = Mathf.Repeat(offset, 1f);
            }
            else
            {
                offset = Mathf.Clamp01(offset);
            }

            transform.position = path.Evaluate(offset);
        }

        public void SetColor(Color color)
        {
            if (markerRenderer == null)
            {
                markerRenderer = GetComponentInChildren<Renderer>(true);
            }

            if (markerRenderer != null)
            {
                markerRenderer.material.color = color;
            }
        }
    }
}
