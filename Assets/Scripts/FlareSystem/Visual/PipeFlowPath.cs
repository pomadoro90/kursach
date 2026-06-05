using System.Collections.Generic;
using UnityEngine;

namespace FlareSystem
{
    public class PipeFlowPath : MonoBehaviour
    {
        public string flowName = "Flow";
        public List<Transform> waypoints = new List<Transform>();
        public Color flowColor = Color.white;
        public float speed = 1f;
        public float particleSize = 0.08f;
        public bool loop = true;

        public Vector3 Evaluate(float normalizedDistance)
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                return transform.position;
            }

            if (waypoints.Count == 1)
            {
                return waypoints[0].position;
            }

            float totalLength = GetLength();
            if (totalLength <= 0.001f)
            {
                return waypoints[0].position;
            }

            float targetDistance = Mathf.Repeat(normalizedDistance, 1f) * totalLength;
            float walked = 0f;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 a = waypoints[i].position;
                Vector3 b = waypoints[i + 1].position;
                float segment = Vector3.Distance(a, b);
                if (walked + segment >= targetDistance)
                {
                    float segmentT = Mathf.InverseLerp(walked, walked + segment, targetDistance);
                    return Vector3.Lerp(a, b, segmentT);
                }

                walked += segment;
            }

            return waypoints[waypoints.Count - 1].position;
        }

        public float GetLength()
        {
            if (waypoints == null || waypoints.Count < 2)
            {
                return 0f;
            }

            float length = 0f;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    length += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
                }
            }

            return length;
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Count < 2)
            {
                return;
            }

            Gizmos.color = flowColor;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                    Gizmos.DrawSphere(waypoints[i].position, 0.035f);
                }
            }

            Gizmos.DrawSphere(waypoints[waypoints.Count - 1].position, 0.035f);
        }
    }
}
