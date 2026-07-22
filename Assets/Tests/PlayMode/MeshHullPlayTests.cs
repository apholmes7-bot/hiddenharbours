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

        /// <summary>
        /// <b>The side dragger sails as a MESH-ONLY hull (ADR 0022 phase 5).</b> The hull that
        /// motivated the ADR — 25 m, 433.1 MiB of sheets replaced by 143.9 KB of mesh — and the first
        /// hull in the game with no baked sheet at all. That is what makes this worth a live test:
        /// the skinner falls back to the sprite path for any hull it cannot present as a mesh, and
        /// for her that fallback draws <b>nothing</b> (no compass, no fallback sprite). So "she
        /// appears" and "she appears as a mesh" are the same assertion here — unlike the lobster,
        /// who would merely have looked older.
        /// </summary>
        [UnityTest]
        public IEnumerator SideDragger_SailsAsAMeshOnlyHull()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed dragger.");
            yield break;
#else
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                "Assets/_Project/Data/Boats/Visuals/SideDraggerIso.asset");
            Assert.IsNotNull(visual, "missing the committed SideDraggerIso visual def — bake her first");
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(
                "Assets/_Project/Data/Boats/SideDragger.asset");
            Assert.IsNotNull(hull, "missing the committed SideDragger hull def");
            Assert.AreSame(visual, hull.Visual, "her hull def must point at her visual def");

            Assert.AreEqual(BoatHullVariant.Mesh, visual.Variant, "she is the Mesh variant in data");
            Assert.IsTrue(visual.HasHullMesh(), "her committed HullMeshDef must be usable");
            Assert.IsFalse(visual.HasFullCompass(),
                "she is MESH-ONLY — a compass here would mean somebody baked her a sheet, which is " +
                "the 433.1 MiB this ADR exists to avoid");

            _root = new GameObject("SideDragger");
            var rig = BoatHullSkinner.Apply(_root, visual, boat: null);

            Assert.IsTrue(rig.Skinned,
                "she must skin — with no sheet there is no sprite fallback, so an unskinned dragger " +
                "is an INVISIBLE boat, not a degraded one");
            Assert.AreEqual(BoatHullVariant.Mesh, rig.Presenter.Variant);
            var renderer = rig.Visual.GetComponent<IsoFacetHullRenderer>();
            Assert.IsNotNull(renderer, "the facet renderer rides her visual child");
            Assert.IsTrue(renderer.IsConfigured, "configured from her committed HullMeshDef");

            // Continuous heading at angles no facing grid could ever have drawn — the point of the
            // whole ADR, on the hull whose sheet set was never affordable in the first place.
            var def = visual.HullMesh;
            foreach (float heading in new[] { 0f, 17.3f, 101.25f, 222.2f, 359f })
            {
                _root.transform.rotation = Quaternion.Euler(0f, 0f, -heading);
                yield return null;   // one real LateUpdate

                float expected = HullMeshMath.HeadingToDirUnits(heading, visual.ZeroHeadingDegrees,
                                                                def.AzimuthCounterClockwise);
                Assert.AreEqual(expected, renderer.HeadingDirUnits, 1e-3f,
                    $"continuous heading at {heading}° on the 25 m hull");
            }

            // Calm sea (no environment service in this scene) ⇒ level, no phantom rock.
            yield return null;
            Assert.AreEqual(0f, renderer.RollDegrees, 1e-3f, "glass calm = level");
            Assert.AreEqual(0f, renderer.HeavePixels, 1e-3f);
#endif
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
