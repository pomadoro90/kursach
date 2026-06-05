using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FlareSystem
{
    public class CameraController : MonoBehaviour
    {
        private static readonly KeyCode[] PresetKeys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Alpha6
        };

        public Camera controlledCamera;
        public Transform orbitTarget;
        public List<CameraPreset> presets = new List<CameraPreset>();
        public float orbitSensitivity = 4f;
        public float zoomSensitivity = 10f;
        public float panSpeed = 8f;
        public float minDistance = 4f;
        public float maxDistance = 80f;
        public float transitionDuration = 0.65f;
        public Vector2 pitchLimits = new Vector2(8f, 78f);
        public bool ignoreMouseWhenPointerOverUi = true;

        private int currentPresetIndex;
        private bool isTransitioning;
        private float transitionElapsed;
        private Vector3 transitionStartPosition;
        private Quaternion transitionStartRotation;
        private float transitionStartFov;
        private Vector3 transitionStartPivot;
        private Vector3 transitionTargetPosition;
        private Quaternion transitionTargetRotation;
        private float transitionTargetFov;
        private Vector3 transitionTargetPivot;
        private float orbitYaw;
        private float orbitPitch = 35f;
        private float orbitDistance = 20f;

        private void Awake()
        {
            if (controlledCamera == null)
            {
                controlledCamera = Camera.main;
            }

            if (controlledCamera == null)
            {
                controlledCamera = gameObject.AddComponent<Camera>();
                gameObject.tag = "MainCamera";
            }

            EnsureCameraClipping();

            if (orbitTarget == null)
            {
                GameObject target = new GameObject("CameraOrbitTarget");
                target.transform.SetParent(transform.parent, true);
                target.transform.position = transform.position + transform.forward * 10f;
                orbitTarget = target.transform;
            }

            if (presets.Count == 0)
            {
                CreateDefaultPresets();
            }
        }

        private void Start()
        {
            ApplyPresetImmediate(0);
        }

        private void Update()
        {
            HandlePresetKeys();
            UpdateTransition();
            HandleMouse();
            HandleKeyboardMove();
        }

        public void ApplyPreset(int index)
        {
            ApplyPreset(index, false);
        }

        public void ApplyPresetImmediate(int index)
        {
            ApplyPreset(index, true);
        }

        private void ApplyPreset(int index, bool immediate)
        {
            if (controlledCamera == null || presets.Count == 0)
            {
                return;
            }

            EnsureCameraClipping();
            currentPresetIndex = Mathf.Clamp(index, 0, presets.Count - 1);
            CameraPreset preset = presets[currentPresetIndex];
            Vector3 pivot = ResolvePresetPivot(preset);
            Quaternion targetRotation = Quaternion.Euler(preset.eulerAngles);

            if (immediate || transitionDuration <= 0.01f)
            {
                controlledCamera.transform.position = preset.position;
                controlledCamera.transform.rotation = targetRotation;
                controlledCamera.fieldOfView = preset.fieldOfView;
                if (orbitTarget != null)
                {
                    orbitTarget.position = pivot;
                }

                isTransitioning = false;
                SyncOrbitState();
                return;
            }

            transitionElapsed = 0f;
            transitionStartPosition = controlledCamera.transform.position;
            transitionStartRotation = controlledCamera.transform.rotation;
            transitionStartFov = controlledCamera.fieldOfView;
            transitionStartPivot = orbitTarget != null ? orbitTarget.position : pivot;
            transitionTargetPosition = preset.position;
            transitionTargetRotation = targetRotation;
            transitionTargetFov = preset.fieldOfView;
            transitionTargetPivot = pivot;
            isTransitioning = true;
        }

        private void HandlePresetKeys()
        {
            int count = Mathf.Min(PresetKeys.Length, presets.Count);
            for (int i = 0; i < count; i++)
            {
                if (FlareInput.GetKeyDown(PresetKeys[i]))
                {
                    ApplyPreset(i);
                }
            }
        }

        private void UpdateTransition()
        {
            if (!isTransitioning || controlledCamera == null)
            {
                return;
            }

            transitionElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(transitionElapsed / Mathf.Max(0.01f, transitionDuration));
            t = t * t * (3f - 2f * t);

            controlledCamera.transform.position = Vector3.Lerp(transitionStartPosition, transitionTargetPosition, t);
            controlledCamera.transform.rotation = Quaternion.Slerp(transitionStartRotation, transitionTargetRotation, t);
            controlledCamera.fieldOfView = Mathf.Lerp(transitionStartFov, transitionTargetFov, t);
            if (orbitTarget != null)
            {
                orbitTarget.position = Vector3.Lerp(transitionStartPivot, transitionTargetPivot, t);
            }

            if (t >= 0.999f)
            {
                isTransitioning = false;
                SyncOrbitState();
            }
        }

        private void HandleMouse()
        {
            if (controlledCamera == null || orbitTarget == null)
            {
                return;
            }

            if (ignoreMouseWhenPointerOverUi && IsPointerOverUi())
            {
                return;
            }

            bool orbiting = FlareInput.SecondaryPointerPressed();
            bool panning = FlareInput.MiddlePointerPressed() ||
                (FlareInput.PrimaryPointerPressed() && FlareInput.AltPressed());

            if (orbiting || panning)
            {
                isTransitioning = false;
            }

            if (orbiting)
            {
                Vector2 delta = FlareInput.PointerDelta();
                orbitYaw += delta.x * orbitSensitivity * 0.08f;
                orbitPitch = Mathf.Clamp(orbitPitch - delta.y * orbitSensitivity * 0.08f, pitchLimits.x, pitchLimits.y);
                ApplyOrbitPose();
            }

            if (panning)
            {
                Vector2 delta = FlareInput.PointerDelta();
                float scaledPan = panSpeed * Mathf.Max(orbitDistance, 1f) * 0.0008f;
                Vector3 move = (-controlledCamera.transform.right * delta.x - controlledCamera.transform.up * delta.y) * scaledPan;
                controlledCamera.transform.position += move;
                orbitTarget.position += move;
            }

            float scroll = FlareInput.ScrollY();
            if (Mathf.Abs(scroll) > 0.001f)
            {
                isTransitioning = false;
                orbitDistance = Mathf.Clamp(orbitDistance - scroll * zoomSensitivity, minDistance, maxDistance);
                ApplyOrbitPose();
            }
        }

        private void HandleKeyboardMove()
        {
            if (controlledCamera == null || orbitTarget == null)
            {
                return;
            }

            Vector3 input = Vector3.zero;
            if (FlareInput.GetKey(KeyCode.W) || FlareInput.GetKey(KeyCode.UpArrow))
            {
                input += Vector3.ProjectOnPlane(controlledCamera.transform.forward, Vector3.up).normalized;
            }

            if (FlareInput.GetKey(KeyCode.S) || FlareInput.GetKey(KeyCode.DownArrow))
            {
                input -= Vector3.ProjectOnPlane(controlledCamera.transform.forward, Vector3.up).normalized;
            }

            if (FlareInput.GetKey(KeyCode.D) || FlareInput.GetKey(KeyCode.RightArrow))
            {
                input += Vector3.ProjectOnPlane(controlledCamera.transform.right, Vector3.up).normalized;
            }

            if (FlareInput.GetKey(KeyCode.A) || FlareInput.GetKey(KeyCode.LeftArrow))
            {
                input -= Vector3.ProjectOnPlane(controlledCamera.transform.right, Vector3.up).normalized;
            }

            if (input.sqrMagnitude > 0.001f)
            {
                isTransitioning = false;
                Vector3 delta = input.normalized * panSpeed * Time.deltaTime;
                controlledCamera.transform.position += delta;
                orbitTarget.position += delta;
            }
        }

        private bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private Vector3 ResolvePresetPivot(CameraPreset preset)
        {
            if (preset.useTargetPosition)
            {
                return preset.targetPosition;
            }

            if (orbitTarget != null)
            {
                return orbitTarget.position;
            }

            Quaternion rotation = Quaternion.Euler(preset.eulerAngles);
            return preset.position + rotation * Vector3.forward * Mathf.Clamp(orbitDistance, minDistance, maxDistance);
        }

        private void SyncOrbitState()
        {
            if (controlledCamera == null || orbitTarget == null)
            {
                return;
            }

            Vector3 offset = controlledCamera.transform.position - orbitTarget.position;
            orbitDistance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
            Vector3 euler = controlledCamera.transform.rotation.eulerAngles;
            orbitYaw = euler.y;
            orbitPitch = NormalizePitch(euler.x);
            orbitPitch = Mathf.Clamp(orbitPitch, pitchLimits.x, pitchLimits.y);
        }

        private void ApplyOrbitPose()
        {
            Quaternion orbit = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            controlledCamera.transform.position = orbitTarget.position + orbit * (Vector3.back * orbitDistance);
            controlledCamera.transform.rotation = Quaternion.LookRotation(orbitTarget.position - controlledCamera.transform.position, Vector3.up);
        }

        private static float NormalizePitch(float pitch)
        {
            if (pitch > 180f)
            {
                pitch -= 360f;
            }

            return Mathf.Abs(pitch);
        }

        private void CreateDefaultPresets()
        {
            Vector3 pivot = orbitTarget != null ? orbitTarget.position : Vector3.zero;
            presets.Add(new CameraPreset("Overview", new Vector3(10f, 9f, -14f), new Vector3(32f, -35f, 0f), 48f, pivot + new Vector3(0f, 3f, 0f)));
            presets.Add(new CameraPreset("Separator", new Vector3(-8f, 4f, -7f), new Vector3(24f, -18f, 0f), 42f, pivot + new Vector3(-4f, 1.8f, -1f)));
            presets.Add(new CameraPreset("Flare Stack", new Vector3(6f, 12f, -8f), new Vector3(38f, -28f, 0f), 38f, pivot + new Vector3(2f, 8f, 0f)));
            presets.Add(new CameraPreset("Flare Top / Flame", new Vector3(4f, 24f, -5f), new Vector3(20f, -32f, 0f), 30f, pivot + new Vector3(2f, 18f, 0f)));
            presets.Add(new CameraPreset("Piping / Flows", new Vector3(0f, 6f, -10f), new Vector3(28f, 0f, 0f), 42f, pivot + new Vector3(0f, 2.5f, 0f)));
            presets.Add(new CameraPreset("UI Overview", new Vector3(12f, 10f, -16f), new Vector3(30f, -38f, 0f), 52f, pivot + new Vector3(0f, 3f, 0f)));
        }

        private void EnsureCameraClipping()
        {
            if (controlledCamera == null)
            {
                return;
            }

            controlledCamera.nearClipPlane = 0.1f;
            controlledCamera.farClipPlane = Mathf.Max(controlledCamera.farClipPlane, 1000f);
        }
    }

    [Serializable]
    public class CameraPreset
    {
        public string presetName;
        public Vector3 position;
        public Vector3 eulerAngles;
        public float fieldOfView = 50f;
        public bool useTargetPosition;
        public Vector3 targetPosition;

        public CameraPreset(string presetName, Vector3 position, Vector3 eulerAngles, float fieldOfView)
        {
            this.presetName = presetName;
            this.position = position;
            this.eulerAngles = eulerAngles;
            this.fieldOfView = fieldOfView;
        }

        public CameraPreset(string presetName, Vector3 position, Vector3 eulerAngles, float fieldOfView, Vector3 targetPosition)
            : this(presetName, position, eulerAngles, fieldOfView)
        {
            this.useTargetPosition = true;
            this.targetPosition = targetPosition;
        }
    }
}
