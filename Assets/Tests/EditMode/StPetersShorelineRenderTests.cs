using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters SHORELINE RENDER config (ADR 0012 — "converge the live shoreline on the shader path"): the
    /// blocky tide-flat was a redundant 2 m colour-cell grid (the retired <c>TidalFlatVisual</c>) stamped ON
    /// TOP of the smooth layered WaterSurface shader, which already renders the SAME deterministic tide over
    /// the SAME tidal flat. Retiring the grid lets the shader show; these pin the builder's shader-shore
    /// config so the smoothing decision is the single source of truth the scene is built from:
    /// <list type="bullet">
    /// <item>the seabed height map bakes at the FINER 192² resolution (ADR 0012 §A step 1 — ~0.83 m texel
    ///   over the 160×120 m plane, half the old 96²/~1.67 m), so the shader's wet edge follows a fine grid
    ///   instead of reading as ~1.5 m rectangular steps; and</item>
    /// <item>the bake rectangle spans the visible water (160×120 m, centred), so the depth/foam shoreline
    ///   lines up with the St Peters TidalTerrain.</item>
    /// </list>
    /// Pure config assertions via the builder's public <see cref="StPetersBuilder.ConfigureWaterSurface"/>
    /// helper (the same call the scene uses) read back through <see cref="SerializedObject"/> — engine-light,
    /// no scene loaded. (Since ADR 0014 made <see cref="WaterSurface"/> <c>[ExecuteAlways]</c> for the
    /// edit-mode coast preview, adding the component DOES fire OnEnable/the bake in EditMode — harmless here:
    /// it bakes the distance-to-land fallback into a throwaway texture, and these tests assert the SERIALIZED
    /// config the builder writes afterward, not runtime state.) This is render config only; it touches no sim
    /// and no save (CLAUDE.md rule 5).
    /// </summary>
    public class StPetersShorelineRenderTests
    {
        private WaterSurface _surface;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            // WaterSurface has [RequireComponent(typeof(Renderer))], and Renderer is abstract — Unity
            // can't auto-add it, so AddComponent<WaterSurface>() on a bare GameObject returns null. Create
            // the GameObject WITH a concrete MeshRenderer so the requirement is satisfied up front.
            _go = new GameObject("WaterSurface_Test", typeof(MeshRenderer), typeof(WaterSurface));
            _surface = _go.GetComponent<WaterSurface>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void ConfigureWaterSurface_BakesAtTheSmoothed192Resolution()
        {
            // The exact call the builder makes for the St Peters Sea plane.
            StPetersBuilder.ConfigureWaterSurface(_surface, new Vector2(0f, 0f), new Vector2(160f, 120f), 192);

            var so = new SerializedObject(_surface);
            int res = so.FindProperty("_heightResolution").intValue;
            Assert.AreEqual(192, res,
                "ADR 0012: the seabed height map bakes at 192 (finer than the old 96) so the shader shoreline " +
                "is smooth, not blocky — this is the headline fix and must not silently regress to 96.");
        }

        [Test]
        public void ConfigureWaterSurface_BakeRectSpansTheVisibleWater()
        {
            StPetersBuilder.ConfigureWaterSurface(_surface, new Vector2(0f, 0f), new Vector2(160f, 120f), 192);

            var so = new SerializedObject(_surface);
            Vector2 size = so.FindProperty("_heightWorldSize").vector2Value;
            Vector2 centre = so.FindProperty("_heightWorldCenter").vector2Value;
            Assert.AreEqual(160f, size.x, 1e-4f, "height bake spans the Sea plane width");
            Assert.AreEqual(120f, size.y, 1e-4f, "height bake spans the Sea plane height");
            Assert.AreEqual(0f, centre.x, 1e-4f, "bake centred on the region");
            Assert.AreEqual(0f, centre.y, 1e-4f, "bake centred on the region");
        }

        [Test]
        public void ConfigureWaterSurface_ResolutionIsClampedToTheComponentRange()
        {
            // The component declares [Range(16, 256)]; the helper clamps so an out-of-range ask can't write a
            // value the bake would itself clamp differently (256 stays the ceiling if the crest ever needs it).
            StPetersBuilder.ConfigureWaterSurface(_surface, Vector2.zero, new Vector2(160f, 120f), 1000);
            var so = new SerializedObject(_surface);
            Assert.AreEqual(256, so.FindProperty("_heightResolution").intValue,
                "an over-range resolution clamps to the component's 256 ceiling");

            StPetersBuilder.ConfigureWaterSurface(_surface, Vector2.zero, new Vector2(160f, 120f), 4);
            so = new SerializedObject(_surface);
            Assert.AreEqual(16, so.FindProperty("_heightResolution").intValue,
                "an under-range resolution clamps to the component's 16 floor");
        }
    }
}
