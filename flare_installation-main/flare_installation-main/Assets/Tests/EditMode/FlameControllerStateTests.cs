using NUnit.Framework;
using UnityEngine;

namespace FlareSystem.Tests
{
    public class FlameControllerStateTests
    {
        [Test]
        public void FlameControllerSwitchesTrafficLightStates()
        {
            GameObject root = new GameObject("FlameControllerTest");
            FlameController controller = root.AddComponent<FlameController>();

            controller.SetNormal();
            Assert.That(controller.CurrentFlameState, Is.EqualTo("Normal"));

            controller.SetWarning();
            Assert.That(controller.CurrentFlameState, Is.EqualTo("Warning"));

            controller.SetDanger();
            Assert.That(controller.CurrentFlameState, Is.EqualTo("Danger"));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void AlarmVisualControllerCreatesGeneratedAudioClip()
        {
            GameObject root = new GameObject("AlarmVisualControllerTest");
            try
            {
                AlarmVisualController controller = root.AddComponent<AlarmVisualController>();
                controller.SetAlarm(true);

                Assert.That(controller.audioSource, Is.Not.Null);
                Assert.That(controller.audioSource.clip, Is.Not.Null);
                Assert.That(controller.audioSource.clip.samples, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
