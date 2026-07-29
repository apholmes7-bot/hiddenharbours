using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ REGION EXTENT IS DATA, NOT A LITERAL (docs/design/scene-sizing-and-world-scale.md §6.1–§6.2).
    ///
    /// <para>The region's rectangle used to be <c>new Vector2(160f, 120f)</c> written out in six places —
    /// four in <see cref="StPetersBuilder"/> (the tiled sea sprite, the flat-backdrop scale, the shader's
    /// height bake and the displaced mesh) and two in <c>TerrainPaintTool</c> — so the sea plane, the
    /// painted seabed and the terrain each carried their own copy of the same decision. Resizing a region
    /// meant finding all six and getting every one right, and St Peters' ruled growth to 760 × 520 m is
    /// exactly the change that blocked on it.</para>
    ///
    /// <para>These hold the new arrangement: the builder authors the extent once, publishes it on the
    /// <see cref="RegionDef"/>, and everything downstream reads it from there.</para>
    /// </summary>
    public class RegionExtentTests
    {
        private static RegionDef StPeters() =>
            AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/StPeters.asset");

        // ---- the extent round-trips from the builder onto the def ---------------------------------

        [Test]
        public void StPetersRegionDef_PublishesTheBuildersAuthoredExtent()
        {
            var region = StPeters();
            Assert.IsNotNull(region, "the St Peters RegionDef must exist (built by StPetersBuilder)");

            Assert.AreEqual(StPetersBuilder.RegionWorldCenter.x, region.WorldCenter.x, 1e-4f);
            Assert.AreEqual(StPetersBuilder.RegionWorldCenter.y, region.WorldCenter.y, 1e-4f);
            Assert.AreEqual(StPetersBuilder.RegionWorldSize.x, region.WorldSizeMeters.x, 1e-4f,
                "the def must carry the builder's authored width — the sea plane is scaled to it");
            Assert.AreEqual(StPetersBuilder.RegionWorldSize.y, region.WorldSizeMeters.y, 1e-4f);
            Assert.AreEqual(StPetersBuilder.SeabedPixelsPerMetre, region.SeabedPixelsPerMetre, 1e-4f,
                "and the resolution, so a map minted by the paint tool matches the shader's bake");

            Debug.Log(
                $"[region-extent] {region.DisplayName}: {region.WorldSizeMeters.x:0.#} × " +
                $"{region.WorldSizeMeters.y:0.#} m at {region.SeabedPixelsPerMetre:0.##} px/m → " +
                $"{region.SeabedTexels.x} × {region.SeabedTexels.y} texels " +
                $"({region.SeabedBytes / 1024f:0.#} KiB R8), water bake " +
                $"{StPetersBuilder.WaterHeightBakeResolution}².");
        }

        // ---- the texel grid is DERIVED, and follows a non-square region ---------------------------

        /// <summary>
        /// ⚠ The bug the derivation removes. A square 192² over a 160 × 120 m rect is 1.2 px/m across
        /// and 1.6 px/m up — the two axes sampled at different densities, chosen by nobody. A grid
        /// derived from size × px/m cannot do that.
        /// </summary>
        [Test]
        public void TheTexelGridFollowsTheRegionsAspect_NotASquare()
        {
            var region = ScriptableObject.CreateInstance<RegionDef>();
            try
            {
                region.WorldSizeMeters = new Vector2(760f, 520f);   // St Peters at its ruled size
                region.SeabedPixelsPerMetre = 2f;

                Assert.AreEqual(1520, region.SeabedTexels.x);
                Assert.AreEqual(1040, region.SeabedTexels.y);
                Assert.IsTrue(region.HasUsableExtent, "1520 × 1040 is well inside the cap");

                // The density is the SAME on both axes — the whole point.
                Assert.AreEqual(region.SeabedTexels.x / region.WorldSizeMeters.x,
                                region.SeabedTexels.y / region.WorldSizeMeters.y, 1e-4f,
                                "a derived grid samples x and y at one density");

                // …and the square-192 arrangement it replaces did not.
                float oldX = 192f / 160f, oldY = 192f / 120f;
                Assert.AreNotEqual(oldX, oldY,
                    "SABOTAGE CHECK — if the old square grid had been isotropic there would have been " +
                    "nothing to fix; it sampled 1.2 px/m across and 1.6 px/m up.");

                Debug.Log($"[region-extent] the old square 192² over 160 × 120 m sampled {oldX:0.##} px/m " +
                          $"across and {oldY:0.##} px/m up ({oldY / oldX:0.##}× anisotropy). A derived " +
                          "grid is isotropic by construction.");
            }
            finally { Object.DestroyImmediate(region); }
        }

        [Test]
        public void OffshoreRegionsCanRunAtOnePixelPerMetre_AndTheCostIsVisible()
        {
            var region = ScriptableObject.CreateInstance<RegionDef>();
            try
            {
                region.WorldSizeMeters = new Vector2(1600f, 1200f);   // an offshore ground
                region.SeabedPixelsPerMetre = 1f;

                Assert.AreEqual(new Vector2Int(1600, 1200), region.SeabedTexels);
                Assert.IsTrue(region.HasUsableExtent);
                Assert.AreEqual(1600 * 1200, region.SeabedBytes,
                    "R8, so one byte per texel — the number the resolution decision is made on");
            }
            finally { Object.DestroyImmediate(region); }
        }

        // ---- SABOTAGE: an unusable extent is refused, not quietly clamped -------------------------

        /// <summary>
        /// The guard exists because the extent is now editable data. A mistyped pixels-per-metre is
        /// the realistic failure: 200 instead of 2 turns a 760 m region into a 152 000-texel axis, and
        /// a tool that silently allocated it would take the editor down rather than say so.
        /// </summary>
        [Test]
        public void Sabotage_AnUnusableExtent_IsRejectedByTheGuard()
        {
            var region = ScriptableObject.CreateInstance<RegionDef>();
            try
            {
                region.WorldSizeMeters = new Vector2(760f, 520f);
                region.SeabedPixelsPerMetre = 2f;
                Assert.IsTrue(region.HasUsableExtent, "the honest extent must pass");

                region.SeabedPixelsPerMetre = 200f;          // the mistyped 2
                Assert.IsFalse(region.HasUsableExtent,
                    "SABOTAGE NOT DETECTED — 200 px/m over 760 m is 152 000 texels on one axis.");
                Assert.Greater(region.SeabedTexels.x, RegionDef.MaxSeabedTexels);

                region.SeabedPixelsPerMetre = 0f;            // the unset field
                Assert.IsFalse(region.HasUsableExtent,
                    "SABOTAGE NOT DETECTED — 0 px/m would mint a 1 × 1 map.");
                Assert.AreEqual(new Vector2Int(1, 1), region.SeabedTexels,
                    "…and it must not be allowed to produce a zero-sized texture either");

                region.SeabedPixelsPerMetre = 2f;
                region.WorldSizeMeters = new Vector2(0f, 520f);   // an unset size
                Assert.IsFalse(region.HasUsableExtent,
                    "SABOTAGE NOT DETECTED — a zero-width region has no rectangle to paint.");

                Debug.Log("[region-extent] SABOTAGE — the extent guard refuses 200 px/m (152 000 texels), " +
                          "0 px/m (a 1 × 1 map) and a zero-width region; the shipped extent passes.");
            }
            finally { Object.DestroyImmediate(region); }
        }

        [Test]
        public void TheDefaultExtentOnAFreshRegionDef_IsUsable()
        {
            // A region authored by hand in the inspector must be paintable without touching the extent
            // block first — the defaults are the greybox rect at the ruled inshore resolution.
            var region = ScriptableObject.CreateInstance<RegionDef>();
            try
            {
                Assert.IsTrue(region.HasUsableExtent,
                    "a fresh RegionDef must have a usable extent, or every new region starts broken");
                Assert.Greater(region.SeabedPixelsPerMetre, 0f);
            }
            finally { Object.DestroyImmediate(region); }
        }

        // ---- the water bake follows the extent too ------------------------------------------------

        [Test]
        public void TheWaterHeightBakeResolution_IsDerivedFromTheExtent_NotALiteral()
        {
            // It was a literal 192 sitting beside a literal 160 × 120, so growing the region would have
            // silently coarsened the shader's waterline instead of keeping its texel size.
            float longest = Mathf.Max(StPetersBuilder.RegionWorldSize.x, StPetersBuilder.RegionWorldSize.y);
            int expected = Mathf.CeilToInt(longest * StPetersBuilder.SeabedPixelsPerMetre);

            Assert.AreEqual(expected, StPetersBuilder.WaterHeightBakeResolution,
                "the bake grid must follow the region's own size and resolution");
            Assert.LessOrEqual(StPetersBuilder.WaterHeightBakeResolution, RegionDef.MaxSeabedTexels,
                "…while staying inside the cap");

            float texelMetres = longest / StPetersBuilder.WaterHeightBakeResolution;
            Assert.LessOrEqual(texelMetres, 1f,
                "a texel coarser than a metre reads as rectangular steps on the near-flat bar crest");

            Debug.Log($"[region-extent] water height bake {StPetersBuilder.WaterHeightBakeResolution}² over " +
                      $"{longest:0.#} m = a {texelMetres:0.##} m texel.");
        }
    }
}
