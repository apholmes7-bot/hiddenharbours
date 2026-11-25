using System.Collections;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class BoatMeshHandoffPlayTests
    {
        GameObject _root;
        IEnvironmentService _previousEnvironment;
        GameConfig _previousConfig;

        [SetUp]
        public void SetUp()
        {
            _previousEnvironment = GameServices.Environment;
            _previousConfig = GameServices.Config;
            GameServices.Environment = null;
            GameServices.Config = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            GameServices.Environment = _previousEnvironment;
            GameServices.Config = _previousConfig;
        }

        [UnityTest]
        public IEnumerator SpriteToMesh_NeverLeavesTheLegacyHullOrOverlaysDrawing()
        {
#if UNITY_EDITOR
            var hulls = AssetDatabase.FindAssets("t:BoatHullDef", new[] { "Assets/_Project/Data/Boats" })
                .Select(id => AssetDatabase.LoadAssetAtPath<BoatHullDef>(AssetDatabase.GUIDToAssetPath(id)))
                .Where(h => h != null && h.Visual != null && h.Visual.HasFullCompass() && h.Visual.HasHullMesh())
                .GroupBy(h => h.Visual).Select(group => group.First()).ToArray();
            Assert.Greater(hulls.Length, 0, "the test needs real hulls with both presentations");
            foreach (var hull in hulls)
            {
                _root = new GameObject("HullHandoff");
                var boat = _root.AddComponent<BoatController>();
                boat.SetHull(hull);
                boat.enabled = false;
                var baseRenderer = _root.AddComponent<SpriteRenderer>();
                var sprite = BoatHullSkinner.ApplyHull(_root, baseRenderer, hull, boat,
                    new BoatHullSkinner.Options { VariantOverride = BoatHullVariant.Sprite, SkipWaveMotion = true });
                var legacy = sprite.Visual.GetComponentsInChildren<SpriteRenderer>(true);
                var compass = sprite.Directional;
                Assert.Greater(legacy.Length, 0, hull.Id + " must start with its actual sprite presentation");
                Assert.IsTrue(sprite.Renderer.enabled);
                yield return null;

                var mesh = BoatHullSkinner.ApplyHull(_root, baseRenderer, hull, boat,
                    new BoatHullSkinner.Options { VariantOverride = BoatHullVariant.Mesh, SkipWaveMotion = true });
                Assert.AreEqual(BoatHullVariant.Mesh, mesh.Presenter.Variant, hull.Id);
                Assert.IsFalse(baseRenderer.enabled, "the root picture must remain hidden");
                Assert.IsTrue(compass == null || !compass.enabled, "the old compass must stop writing on this call");
                foreach (var renderer in legacy)
                    Assert.IsTrue(renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy,
                        hull.Id + ": a legacy sprite must stop drawing before the call returns");

                yield return null;
                Assert.IsTrue(compass == null, "deferred component cleanup must still complete");
                foreach (var renderer in legacy) Assert.IsTrue(renderer == null);
                Object.DestroyImmediate(_root);
                _root = null;
            }
#else
            Assert.Ignore("Requires the committed asset database.");
            yield break;
#endif
        }
    }
}
