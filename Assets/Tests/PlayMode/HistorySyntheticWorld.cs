using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // Only for the synthetic lifecycle suite. Never use this to hide authored gameplay effects.
    internal sealed class HistorySyntheticWorld
    {
        sealed class Saved
        {
            internal GameObject Object;
            internal bool Active;
            internal Transform Parent;
            internal Scene Scene;
            internal Vector3 Position,Scale;
            internal Quaternion Rotation;
        }
        readonly List<Saved> _objects=new List<Saved>();
        readonly List<GameObject> _roots=new List<GameObject>();
        Scene _origin,_world;
        bool _began;
        internal void Begin()
        {
            _origin=SceneManager.GetActiveScene(); _began=true;
            var roots=new List<GameObject>();
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(scene.isLoaded) roots.AddRange(scene.GetRootGameObjects());
            }
            // Existing test convention: only the probe is created/destroyed; persistent user roots stay alive.
            var probe=new GameObject("History DDOL inventory probe");
            Object.DontDestroyOnLoad(probe);
            foreach(var root in probe.scene.GetRootGameObjects()) if(root!=probe) roots.Add(root);
            Object.DestroyImmediate(probe);
            foreach(var root in roots)
            {
                bool application=false,runner=false;
                foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(behaviour==null) continue;
                    string ns=behaviour.GetType().Namespace ?? "";
                    application |= ns.StartsWith("HiddenHarbours.") && !ns.StartsWith("HiddenHarbours.Tests.");
                    runner |= ns.Contains("TestRunner") || ns.StartsWith("UnityEngine.TestTools");
                }
                if(!application) continue;
                Assert.IsFalse(runner,"Application and test runner share a root; isolation needs a narrower design");
                _roots.Add(root);
                foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                    _objects.Add(new Saved {Object=transform.gameObject,Active=transform.gameObject.activeSelf,
                        Parent=transform.parent,Scene=transform.gameObject.scene,Position=transform.localPosition,
                        Rotation=transform.localRotation,Scale=transform.localScale});
            }
            foreach(var root in _roots)
            {
                TestContext.WriteLine($"HISTORY_ISOLATE root={root.name} scene={root.scene.name} active={root.activeSelf}");
                root.SetActive(false);
            }
            _world=SceneManager.CreateScene("History synthetic world");
            Assert.IsTrue(SceneManager.SetActiveScene(_world));
            Assert.AreEqual(0,_world.rootCount,"Owned scene must begin empty");
            AssertSuspended();
            TestContext.WriteLine($"HISTORY_ISOLATION roots={_roots.Count} objects={_objects.Count} emptyScene=True");
        }
        internal void AssertSuspended()
        {
            foreach(var root in _roots) { Assert.IsNotNull(root); Assert.IsFalse(root.activeSelf); }
            Assert.AreEqual(_world,SceneManager.GetActiveScene());
        }
        internal IEnumerator Restore()
        {
            if(!_began) yield break;
            // The caller restores pipeline references and removes test cameras first.
            if(_origin.IsValid() && _origin.isLoaded) Assert.IsTrue(SceneManager.SetActiveScene(_origin));
            if(_world.IsValid() && _world.isLoaded)
            {
                var unload=SceneManager.UnloadSceneAsync(_world);
                float start=Time.realtimeSinceStartup;
                while(!unload.isDone && Time.realtimeSinceStartup-start<5) yield return new WaitForSecondsRealtime(.025f);
                Assert.IsTrue(unload.isDone,"Owned scene did not unload");
            }
            // Restore child state before enabling roots so it is not exposed half-restored.
            foreach(var saved in _objects)
            {
                Assert.IsNotNull(saved.Object,"Isolation must never destroy application objects");
                var t=saved.Object.transform;
                Assert.AreEqual(saved.Scene,saved.Object.scene,"Application object moved scenes");
                Assert.AreSame(saved.Parent,t.parent,"Application hierarchy changed");
                t.localPosition=saved.Position;t.localRotation=saved.Rotation;t.localScale=saved.Scale;
                if(saved.Parent!=null) saved.Object.SetActive(saved.Active);
            }
            foreach(var saved in _objects) if(saved.Parent==null) saved.Object.SetActive(saved.Active);
            foreach(var saved in _objects)
            {
                Assert.AreEqual(saved.Active,saved.Object.activeSelf,"Application active state not restored");
                Assert.AreEqual(saved.Position,saved.Object.transform.localPosition);
                Assert.AreEqual(saved.Rotation,saved.Object.transform.localRotation);
                Assert.AreEqual(saved.Scale,saved.Object.transform.localScale);
            }
            Assert.AreEqual(_origin,SceneManager.GetActiveScene());
            TestContext.WriteLine($"HISTORY_WORLD_RESTORED activeScene=True roots={_roots.Count} objects={_objects.Count} objectStates=True ownedSceneUnloaded=True");
            _objects.Clear();_roots.Clear();_began=false;
        }
    }
}
