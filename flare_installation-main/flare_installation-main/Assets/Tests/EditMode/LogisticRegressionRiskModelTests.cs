using NUnit.Framework;
using UnityEngine;

namespace FlareSystem.Tests
{
    public class LogisticRegressionRiskModelTests
    {
        [Test]
        public void CalculateProbability_ReturnsValueBetweenZeroAndOne()
        {
            GameObject go = new GameObject("RiskModelTest");
            try
            {
                var model = go.AddComponent<LogisticRegressionRiskModel>();
                float probability = model.CalculateProbability(0.35f, 30f, 0.012f);

                Assert.That(probability, Is.GreaterThanOrEqualTo(0f));
                Assert.That(probability, Is.LessThanOrEqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CalculateProbability_LowPurgeAndHighFlareIsHigherThanNormal()
        {
            GameObject go = new GameObject("RiskModelTest");
            try
            {
                var model = go.AddComponent<LogisticRegressionRiskModel>();
                float normal = model.CalculateProbability(0.45f, 38f, 0.008f);
                float high = model.CalculateProbability(0.2f, 10f, 0.024f);

                Assert.That(high, Is.GreaterThan(normal));
                Assert.That(high, Is.GreaterThan(0.7f));
                Assert.That(normal, Is.LessThan(0.3f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
