#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public class WaveBathymetryLoadingTests
    {
        readonly HashSet<GameObject> _persistentBefore = new HashSet<GameObject>();

        static GameObject[] PersistentRoots()
        {
            var probe = new GameObject("BathymetryLifetimeProbe");
            Object.DontDestroyOnLoad(probe);
            Scene scene = probe.scene;
            Object.DestroyImmediate(probe);
            return scene.GetRootGameObjects();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.IsNotEmpty(System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable),
                "The existing assembly save redirect must be armed before entering play mode.");
            foreach (GameObject root in PersistentRoots()) _persistentBefore.Add(root);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            var clean = SceneManager.CreateScene("BathymetryLifetimeCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene != clean) yield return SceneManager.UnloadSceneAsync(scene);
            }
            foreach (GameObject root in PersistentRoots())
                if (root != null && !_persistentBefore.Contains(root)) Object.DestroyImmediate(root);
            GameServices.Reset();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePlayableRegions_LoadNoBathymetry_AndBuildScenesDependOnNoRecipe()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.IsNotEmpty(scenes);
            Assert.IsFalse(AssetDatabase.GetDependencies(scenes, true).Any(path => path.EndsWith(".hhbathymetry")),
                "Stage D part a must add no bake dependency to the playable scenes.");
            foreach (string region in new[] { "StPeters", "NineMileCreek", "RicePoint", "Cumberland" })
            {
                // Scene startup annotations are checked by the existing scene fixtures.
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.LoadSceneAsync(region, LoadSceneMode.Single);
                for (int frame = 0; frame < 4; frame++) yield return null;
                LogAssert.ignoreFailingMessages = false;
                yield return Resources.UnloadUnusedAssets();
                Assert.IsEmpty(Resources.FindObjectsOfTypeAll<WaveBathymetryData>(), region + ": CPU bake became resident.");
                Assert.IsFalse(Resources.FindObjectsOfTypeAll<Texture2DArray>().Any(array => array.name == "BathymetryGpu"),
                    region + ": GPU bake became resident.");
                Debug.Log("[W4-6a] PLAYMODE_NO_BAKE " + region);
            }
            Debug.Log("[W4-6a] PLAYMODE_CHECK_COMPLETE");
        }
    }
}
#endif
