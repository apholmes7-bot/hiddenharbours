using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The BUILDER's wiring of St Peters' painted ground quad (ADR 0028) — specifically its EXTENT.
    ///
    /// <para>⭐⭐ <b>The save/reload dependency #527 found, closed on the island too.</b>
    /// TerrainSplatSurface is [ExecuteAlways], so AddComponent fires OnEnable IMMEDIATELY and builds the
    /// quad's mesh from the component's serialized default extent (160 × 120 m); Configure() afterwards
    /// updates the fields but EnsureBuilt() early-returns once the mesh exists. StPetersBuilder used the
    /// naive order and survived ONLY because it saves the scene — the next load re-ran OnEnable with the
    /// correct serialized extent — which made the ground's coverage depend on a save/reload round-trip
    /// nobody wrote down. The builder now creates the GameObject inactive, configures, and only then
    /// activates (the NineMileCreekBuilder.BuildSplatGround pattern); this file pins that order so a
    /// fresh Build covers the whole region WITHOUT a scene reload.</para>
    ///
    /// <para>The naive order's brokenness itself is pinned once, in
    /// <c>NineMileCreekSplatGroundTests.TheGroundQuadCoversTheWholeRegion_WhichNeedsConfigureBeforeTheFirstEnable</c>
    /// — if that guard ever reports the trap fixed upstream, both builders' SetActive dances can go
    /// together.</para>
    /// </summary>
    public class StPetersSplatGroundTests
    {
        [Test]
        public void TheGroundQuadCoversTheWholeIsland_WithoutASceneReload()
        {
            // Sanity first: the pin below only proves something because the region's extent differs from
            // the component's serialized default — a default-sized quad here is unmistakably wrong.
            var probeGo = new GameObject("StPetersSplatDefaultProbe");
            try
            {
                probeGo.SetActive(false);
                probeGo.AddComponent<TerrainSplatSurface>();
                probeGo.SetActive(true);
                Assert.That(QuadWidth(probeGo),
                    Is.Not.EqualTo(StPetersBuilder.RegionWorldSize.x).Within(0.5f),
                    "the component's default extent now equals St Peters' region size — this test can no " +
                    "longer tell a configured quad from a default one; pick a different probe");
            }
            finally { Object.DestroyImmediate(probeGo); }

            // The builder's order — inactive, configure, activate — must yield the region's real extent
            // on the FIRST enable, with no save/reload round-trip in between.
            var go = new GameObject("StPetersSplatBuilderOrder");
            try
            {
                go.SetActive(false);
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.Configure(StPetersBuilder.RegionWorldCenter, StPetersBuilder.RegionWorldSize,
                                null, TerrainSplatSurface.DefaultSortingOrder);
                go.SetActive(true);

                Assert.AreEqual(StPetersBuilder.RegionWorldSize.x, QuadWidth(go), 0.5f,
                    "the ground quad is not the region's width — configure the component BEFORE its " +
                    "first OnEnable (create the GameObject inactive), or St Peters' ground only covers " +
                    "the island after a scene save + reload");
                Assert.AreEqual(StPetersBuilder.RegionWorldSize.y, QuadHeight(go), 0.5f,
                    "the ground quad is not the region's height");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The generated quad child's world-metre width (built with HideFlags.DontSave, so it is
        /// a child MeshFilter rather than anything the scene serializes).</summary>
        private static float QuadWidth(GameObject host)
        {
            var mf = host.GetComponentInChildren<MeshFilter>(true);
            return mf == null || mf.sharedMesh == null ? -1f : mf.sharedMesh.bounds.size.x;
        }

        /// <inheritdoc cref="QuadWidth"/>
        private static float QuadHeight(GameObject host)
        {
            var mf = host.GetComponentInChildren<MeshFilter>(true);
            return mf == null || mf.sharedMesh == null ? -1f : mf.sharedMesh.bounds.size.y;
        }
    }
}
