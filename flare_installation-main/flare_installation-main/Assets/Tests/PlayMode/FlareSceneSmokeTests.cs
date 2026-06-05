using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlareSystem.Tests
{
    public class FlareSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator RebuiltSceneLoadsWithCoreObjects()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("FlareInstallationScene_Rebuilt", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "FlareInstallationScene_Rebuilt must be enabled in Build Settings.");
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;

            Assert.That(GameObject.Find("FlareSystemRoot"), Is.Not.Null);
            Assert.That(GameObject.Find("UI Canvas"), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(GameObject.Find("FlameEffect"), Is.Not.Null);
        }
    }
}
