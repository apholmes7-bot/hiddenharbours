using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The lobster boat as a live mesh hull, in play mode (ADR 0022 phase 4).</b> The REAL wiring:
    /// the committed LobsterBoatIso visual def, the self-registered Art presentation service, the
    /// skinner's mesh branch, the driver's LateUpdate — running in the actual player loop. Headless-
    /// safe by construction: no camera is created, so nothing renders (CI has no graphics device and
    /// a render there CRASHES the editor) — the pixels are the EditMode GPU fixture's business; this
    /// proves the components behave over real frames.
    /// </summary>
    public class MeshHullPlayTests
    {
        const string VisualPath = "Assets/_Project/Data/Boats/Visuals/LobsterBoatIso.asset";

        GameObject _root;

        static BoatVisualDef LoadLobsterVisual()
        {
#if UNITY_EDITOR
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualPath);
            Assert.IsNotNull(visual, $"missing {VisualPath}");
            return visual;
#else
            Assert.Ignore("Needs the AssetDatabase: these assert the REAL committed lobster, not a mirror.");
            return null;
#endif
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        [UnityTest]
        public IEnumerator LobsterBoat_SailsAsAMesh_WithContinuousHeading()
        {
            var visual = LoadLobsterVisual();
            Assert.IsNotNull(HullMeshPresentation.Service,
                "the Art presentation service must self-register at runtime load — without it every " +
                "mesh hull silently falls back to sprite in the shipped game");

            _root = new GameObject("LobsterBoat");
            var rig = BoatHullSkinner.Apply(_root, visual, boat: null);

            Assert.IsTrue(rig.Skinned);
            Assert.AreEqual(BoatHullVariant.Mesh, rig.Presenter.Variant,
                "the committed lobster visual is the MESH variant end-to-end");
            var renderer = rig.Visual.GetComponent<IsoFacetHullRenderer>();
            Assert.IsNotNull(renderer, "the facet renderer rides the visual child");
            Assert.IsTrue(renderer.IsConfigured, "configured from the committed HullMeshDef");
            Assert.Greater(renderer.HullId, 0, "registered with the URP feature's hull registry");

            // Turn her through headings NO 32-facing sheet can draw and prove the pose is CONTINUOUS
            // — the spike verdict this ADR shipped on. Consumers-of-heading rule: the driver reads
            // the PHYSICS ROOT (the visual child is stomped to screen identity every LateUpdate).
            var def = visual.HullMesh;
            foreach (float heading in new[] { 0f, 17.3f, 101.25f, 222.2f, 359f })
            {
                _root.transform.rotation = Quaternion.Euler(0f, 0f, -heading);
                yield return null;   // one real LateUpdate

                float expected = HullMeshMath.HeadingToDirUnits(heading, visual.ZeroHeadingDegrees,
                                                                def.AzimuthCounterClockwise);
                Assert.AreEqual(expected, renderer.HeadingDirUnits, 1e-3f,
                    $"continuous heading at {heading}° — no facing snap on the mesh path");
                Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, rig.Visual.rotation), 1e-2f,
                    "the visual child is held at screen identity while the body carries the yaw");
            }

            // Calm sea (no environment service in this scene) ⇒ the wave rider must hold her LEVEL:
            // no phantom rocking on glass, mesh path included.
            yield return null;
            Assert.AreEqual(0f, renderer.RollDegrees, 1e-3f, "glass calm = level (no phantom rock)");
            Assert.AreEqual(0f, renderer.HeavePixels, 1e-3f);
        }

        [UnityTest]
        public IEnumerator VariantToggle_FlipsHerBetweenMeshAndSprite_InPlace()
        {
            var visual = LoadLobsterVisual();
            _root = new GameObject("LobsterBoat");

            var mesh = BoatHullSkinner.Apply(_root, visual, null);
            Assert.AreEqual(BoatHullVariant.Mesh, mesh.Presenter.Variant);
            yield return null;

            // The owner's V-press, mechanically: force the sprite look on the same boat.
            var sprite = BoatHullSkinner.Apply(_root, visual, null,
                new BoatHullSkinner.Options { VariantOverride = BoatHullVariant.Sprite });
            Assert.AreEqual(BoatHullVariant.Sprite, sprite.Presenter.Variant);
            Assert.IsNotNull(sprite.Renderer, "her 32-facing compass picture is back");
            Assert.AreEqual(32, sprite.Presenter.FacingCount, "the baked sheet, not a stand-in");
            yield return null;   // deferred Destroy of the mesh components lands end-of-frame
            Assert.IsTrue(sprite.Visual.GetComponent<IsoFacetHullRenderer>() == null,
                "the facet renderer is gone — two hulls must never draw at once");
            Assert.IsTrue(_root.GetComponent<MeshHullDriver>() == null, "the driver too");

            // ...and back to mesh, same boat, same spot.
            var meshAgain = BoatHullSkinner.Apply(_root, visual, null,
                new BoatHullSkinner.Options { VariantOverride = BoatHullVariant.Mesh });
            Assert.AreEqual(BoatHullVariant.Mesh, meshAgain.Presenter.Variant);
            yield return null;
            Assert.IsNotNull(meshAgain.Visual.GetComponent<IsoFacetHullRenderer>());
            Assert.IsTrue(meshAgain.Visual.GetComponent<SpriteRenderer>() == null,
                "the sprite picture is gone again");
            Assert.AreSame(meshAgain.Presenter, _root.GetComponent<BoatHullPresenterHost>().Presenter,
                "the host publishes the CURRENT presenter across the whole A/B cycle");
        }
    }
}
