using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FlareSystem
{
    public class ValveAnimator : MonoBehaviour
    {
        public List<Transform> handwheels = new List<Transform>();
        public float rotationSpeed = 120f;
        public Vector3 localRotationAxis = Vector3.forward;
        public bool rotateOnStart;

        private bool rotating;

        private void Awake()
        {
            if (handwheels.Count == 0)
            {
                AutoFindHandwheels();
            }

            if (rotateOnStart)
            {
                StartRotation();
            }
        }

        private void Update()
        {
            if (!rotating)
            {
                return;
            }

            float delta = rotationSpeed * Time.deltaTime;
            foreach (Transform wheel in handwheels)
            {
                if (wheel != null)
                {
                    wheel.Rotate(localRotationAxis, delta, Space.Self);
                }
            }
        }

        public void AutoFindHandwheels()
        {
            handwheels.Clear();
#if UNITY_2023_1_OR_NEWER
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
#else
            Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
#endif
            foreach (Transform t in transforms)
            {
                string lower = t.name.ToLowerInvariant();
                if (lower.Contains("valve") || lower.Contains("handwheel") || lower.Contains("wheel") || lower.Contains("вентиль") || lower.Contains("задвижка"))
                {
                    handwheels.Add(t);
                }
            }

            if (handwheels.Count == 0)
            {
                Debug.LogWarning("ValveAnimator: handwheel объекты не найдены. Заполните список handwheels вручную или запустите Setup Scene.");
            }
        }

        public void StartRotation()
        {
            rotating = true;
        }

        public void StopRotation()
        {
            rotating = false;
        }

        public void SetSpeed(float speed)
        {
            rotationSpeed = speed;
        }

        public void RotateOnce(float degrees)
        {
            StartCoroutine(RotateOnceRoutine(degrees));
        }

        private IEnumerator RotateOnceRoutine(float degrees)
        {
            float remaining = Mathf.Abs(degrees);
            float sign = Mathf.Sign(degrees);

            while (remaining > 0.1f)
            {
                float step = Mathf.Min(remaining, Mathf.Abs(rotationSpeed) * Time.deltaTime);
                foreach (Transform wheel in handwheels)
                {
                    if (wheel != null)
                    {
                        wheel.Rotate(localRotationAxis, step * sign, Space.Self);
                    }
                }

                remaining -= step;
                yield return null;
            }
        }
    }
}
