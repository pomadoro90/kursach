using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FlareSystem.Tests
{
    public class VisualFixSceneTests
    {
        [UnityTest]
        public IEnumerator VisualFixObjectsAreConfigured()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("FlareInstallationScene_Rebuilt", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "FlareInstallationScene_Rebuilt must be enabled in Build Settings.");
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;

            PipeFlowController pipeFlow = Object.FindAnyObjectByType<PipeFlowController>();
            Assert.That(pipeFlow, Is.Not.Null);
            Assert.That(pipeFlow.flowPaths.Count, Is.GreaterThanOrEqualTo(4));

            foreach (PipeFlowPath path in pipeFlow.flowPaths)
            {
                Assert.That(path, Is.Not.Null);
                Assert.That(path.waypoints.Count, Is.GreaterThanOrEqualTo(3), path.flowName + " should have a waypoint route.");
            }

            Assert.That(GameObject.Find("FlameMesh"), Is.Null);
            Assert.That(GameObject.Find("FlameEffect"), Is.Not.Null);

            SensorIndicator[] sensors = Object.FindObjectsByType<SensorIndicator>(FindObjectsSortMode.None);
            Assert.That(sensors.Length, Is.GreaterThanOrEqualTo(6));
            foreach (SensorIndicator sensor in sensors)
            {
                Assert.That(sensor.indicatorRenderer, Is.Not.Null);
                Assert.That(sensor.transform.localScale.magnitude, Is.LessThan(0.45f), sensor.name + " should be a compact indicator box.");
            }

            FlareUIController ui = Object.FindAnyObjectByType<FlareUIController>();
            Assert.That(ui, Is.Not.Null);
            Assert.That(ui.recordSlider, Is.Not.Null);
            Assert.That(ui.pPurgeSlider, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<RiskPanelController>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SensorTooltipController>(), Is.Not.Null);

            RectTransform warningPanel = ui.warningPanel.GetComponent<RectTransform>();
            RectTransform pPurgeSlider = ui.pPurgeSlider.GetComponent<RectTransform>();
            Assert.That(pPurgeSlider.rect.width, Is.LessThan(warningPanel.rect.width));
        }
    }
}
