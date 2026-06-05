using NUnit.Framework;
using UnityEngine;

namespace FlareSystem.Tests
{
    public class CameraControllerTests
    {
        [Test]
        public void ApplyPresetImmediate_SetsCameraAndOrbitTarget()
        {
            GameObject cameraObject = new GameObject("CameraControllerTestCamera");
            GameObject targetObject = new GameObject("CameraControllerTestTarget");
            try
            {
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.AddComponent<Camera>();
                CameraController controller = cameraObject.AddComponent<CameraController>();
                controller.controlledCamera = camera;
                controller.orbitTarget = targetObject.transform;
                controller.presets.Clear();
                controller.presets.Add(new CameraPreset(
                    "Overview",
                    new Vector3(10f, 8f, -12f),
                    Quaternion.LookRotation((Vector3.up * 3f - new Vector3(10f, 8f, -12f)).normalized, Vector3.up).eulerAngles,
                    48f,
                    Vector3.up * 3f));

                controller.ApplyPresetImmediate(0);

                Assert.That(camera.nearClipPlane, Is.EqualTo(0.1f).Within(0.001f));
                Assert.That(camera.farClipPlane, Is.GreaterThanOrEqualTo(1000f));
                Assert.That(camera.transform.position, Is.EqualTo(new Vector3(10f, 8f, -12f)));
                Assert.That(targetObject.transform.position, Is.EqualTo(Vector3.up * 3f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void CameraController_CanStoreSixCourseworkPresets()
        {
            GameObject cameraObject = new GameObject("CameraPresetCountTest");
            try
            {
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();
                CameraController controller = cameraObject.AddComponent<CameraController>();
                controller.presets.Clear();

                for (int i = 0; i < 6; i++)
                {
                    controller.presets.Add(new CameraPreset("Preset " + i, new Vector3(i, 2f, -6f), new Vector3(20f, i * 10f, 0f), 45f, Vector3.zero));
                }

                Assert.That(controller.presets, Has.Count.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
