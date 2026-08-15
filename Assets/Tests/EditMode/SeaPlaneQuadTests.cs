#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A region's sea is ONE QUAD, and every builder makes it the same way.</b>
    ///
    /// <para><b>What went wrong.</b> Every sea plane was a <c>SpriteDrawMode.Tiled</c> renderer sized to
    /// the region rect. <c>SeaTile.png</c> is 64 px at 32 px/unit — a 2 × 2 m tile — so Nine Mile Creek's
    /// 760 × 560 m rect asked for 380 × 280 = 106,400 tiles, and Unity's tiling generator refused past a
    /// 16-bit index buffer, logging on every build of the region:
    /// <code>
    ///   Cannot generate 9 slice most likely because the size is too big.
    ///   Requires 425600 vertices and 638400 indices
    /// </code>
    /// 106,400 × 4 verts and × 6 indices are those two numbers exactly. St Peters and Greywick were over
    /// the same limit; only the greybox cove was under it.</para>
    ///
    /// <para><b>Why the fix is "stop tiling" and not "tile differently".</b> The tile was never DRAWN, at
    /// any size: <c>HiddenHarbours/Water</c> declares no <c>_MainTex</c> and <c>Water.mat</c> has no slot
    /// for one, and the shader's <c>OUT.uv = IN.uv</c> is read by no fragment stage — every layer runs off
    /// <c>worldXY</c>. A SpriteRenderer submits its mesh already in WORLD space, so a Simple sprite scaled
    /// to the rect hands the fragment stage the same four world corners the tiled one would. The plane's
    /// job is to be that quad and to carry Water.mat, the sorting slot and the property block — and with
    /// the displaced sea on (default since 2026-08-05) <c>DisplacedWaterSurface</c> disables this renderer
    /// outright and reads only those three things off it.</para>
    ///
    /// <para>Pure editor-object construction: no scene is loaded and none is written.</para>
    /// </summary>
    public class SeaPlaneQuadTests
    {
        const string SeaTilePath = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string SquarePath  = WaterSceneTemplate.ArtSprites + "/Square.png";

        const string NineMileCreekBuilderPath = "Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs";
        const string StPetersBuilderPath      = "Assets/_Project/Code/App/Editor/StPetersBuilder.cs";
        const string GreyboxBuilderPath       = "Assets/_Project/Code/App/Editor/GreyboxBuilder.cs";
        const string WaterSceneTemplatePath   = "Assets/_Project/Code/App/Editor/WaterSceneTemplate.cs";

        /// <summary>Unity's sprite tiling generator builds a 16-bit index buffer, so it refuses once the
        /// grid needs more vertices than one can address. The exact threshold is the engine's business;
        /// what these tests assert is that the shipped rects are nowhere near the right side of it.</summary>
        const int SixteenBitVertexCeiling = 65_535;

        GameObject _sea;

        [TearDown]
        public void TearDown()
        {
            if (_sea != null) Object.DestroyImmediate(_sea);
            _sea = null;
            GameServices.TidalTerrain = null;   // static service: leave nothing for the next fixture
        }

        /// <summary>Every sea a builder ships, by the constant the builder itself sizes from — so a region
        /// that is re-sized is re-measured here rather than measured against a number typed twice.</summary>
        static readonly (string Region, Vector2 Size)[] ShippedSeas =
        {
            ("Nine Mile Creek", NineMileCreekBuilder.NineMileCreekSeaSize),
            ("St Peters",       StPetersBuilder.RegionWorldSize),
            ("Greywick",        WestWaterPlan.RegionWorldSize),
            ("Greybox cove",    GreyboxBuilder.CoveSeaSize),
        };

        // =============================================================================================
        //  1. The shared call makes a quad, and the quad covers the region
        // =============================================================================================

        [Test]
        public void TheSeaPlaneIsOneQuad_NotAGridOfTiles()
        {
            var sr = StandUpASeaPlane(SeaTilePath);

            WaterSceneTemplate.ConfigureSeaPlane(sr, NineMileCreekBuilder.NineMileCreekSeaSize);

            Assert.That(sr.drawMode, Is.EqualTo(SpriteDrawMode.Simple),
                "the sea plane must be a single Simple quad. A Tiled draw over a region rect is what asked " +
                "Unity for 425,600 vertices and got a refusal logged on every build — and it bought nothing, " +
                "because the water shader never samples the tile.");
        }

        [Test]
        public void TheSeaPlaneCoversTheWholeRegionRect()
        {
            // The assertion that matters for the LOOK. The water shader is entirely world-space: it draws
            // wherever this quad is and nowhere else, so "covers the rect" is the whole contract the plane
            // owes the region. A quad short of the rect is a bay with a visible edge of nothing.
            foreach (var (region, size) in ShippedSeas)
            {
                var sr = StandUpASeaPlane(SeaTilePath);

                WaterSceneTemplate.ConfigureSeaPlane(sr, size);

                Vector3 drawn = sr.bounds.size;
                Assert.That(drawn.x, Is.EqualTo(size.x).Within(1e-3f), $"{region}: the sea plane is {drawn.x} m " +
                            $"wide against a {size.x} m region");
                Assert.That(drawn.y, Is.EqualTo(size.y).Within(1e-3f), $"{region}: the sea plane is {drawn.y} m " +
                            $"tall against a {size.y} m region");

                Object.DestroyImmediate(_sea);
                _sea = null;
            }
        }

        [Test]
        public void TheNoMaterialBackdropCoversTheRegionToo_AndUsedToCoverAQuarterOfIt()
        {
            // ⚠ A SECOND defect the same line carried, fixed by the same call. All four builders scaled the
            // fallback Square by (worldSize.x, worldSize.y, 1) — but Square.png is 16 px at 32 px/unit, i.e.
            // 0.5 m, so the greybox backdrop came out HALF-SIZE on each axis: a quarter of the region's
            // area. Deriving the factor from sprite.bounds.size is what makes one call right for a 2 m tile
            // and a 0.5 m square alike.
            var square = WaterSceneTemplate.LoadSpriteAny(SquarePath);
            if (square == null) Assert.Ignore($"SKIPPED, NOT VERIFIED — {SquarePath} has not imported.");

            Assert.That(square.bounds.size.x, Is.LessThan(1f),
                "this test is only meaningful while Square.png is smaller than a metre — which is the whole " +
                "reason the old (size.x, size.y, 1) scale was wrong. Re-check the arithmetic if it changed.");

            var sr = StandUpASeaPlane(SquarePath);

            WaterSceneTemplate.ConfigureSeaPlane(sr, NineMileCreekBuilder.NineMileCreekSeaSize);

            Assert.That(sr.bounds.size.x, Is.EqualTo(NineMileCreekBuilder.NineMileCreekSeaSize.x).Within(1e-3f));
            Assert.That(sr.bounds.size.y, Is.EqualTo(NineMileCreekBuilder.NineMileCreekSeaSize.y).Within(1e-3f));
        }

        [Test]
        public void ConfiguringASeaPlaneIsIdempotent()
        {
            // A builder re-run must converge, not compound — the scale is set from the SPRITE's own size
            // every time, never multiplied onto whatever the transform already carried.
            var sr = StandUpASeaPlane(SeaTilePath);
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;

            WaterSceneTemplate.ConfigureSeaPlane(sr, size);
            Vector3 once = sr.transform.localScale;
            WaterSceneTemplate.ConfigureSeaPlane(sr, size);

            Assert.That(sr.transform.localScale, Is.EqualTo(once),
                "a second pass moved the scale — a builder re-run would grow the sea every click");
        }

        // =============================================================================================
        //  2. WHY it cannot go back to tiling
        // =============================================================================================

        [Test]
        public void TilingTheShippedRegionsWouldBlowUnitysVertexLimit()
        {
            // The arithmetic, pinned, so the reason survives the comment. If someone re-sizes a region or
            // re-imports the tile and reaches for a Tiled draw again, this is the number they are up
            // against — and for the three big regions it is not close.
            var tile = WaterSceneTemplate.LoadSpriteAny(SeaTilePath);
            if (tile == null) Assert.Ignore($"SKIPPED, NOT VERIFIED — {SeaTilePath} has not imported.");

            Vector3 tileMetres = tile.bounds.size;
            Assert.That(tileMetres.x, Is.GreaterThan(0f), "the sea tile must have a world size");

            foreach (var (region, size) in ShippedSeas)
            {
                long tiles = (long)Mathf.Ceil(size.x / tileMetres.x) * (long)Mathf.Ceil(size.y / tileMetres.y);
                long verts = tiles * 4;

                if (region == "Greybox cove")
                {
                    // The one sea small enough that the old draw worked. It is here so the test states the
                    // whole picture rather than only the broken part: the cove was changed to the quad for
                    // consistency, not because it was failing.
                    Assert.That(verts, Is.LessThan(SixteenBitVertexCeiling),
                        $"{region} was the region under the limit — if it is no longer, the note in " +
                        "GreyboxBuilder that says so is now wrong.");
                    continue;
                }

                Assert.That(verts, Is.GreaterThan(SixteenBitVertexCeiling),
                    $"{region} ({size.x} × {size.y} m at a {tileMetres.x} × {tileMetres.y} m tile) would need " +
                    $"{tiles:N0} tiles = {verts:N0} vertices, which is meant to be over Unity's ceiling. If it " +
                    "is not any more, the sea plane could be tiled again — but there is still no reason to, " +
                    "because the water shader never samples the tile.");
            }

            // Nine Mile Creek's own numbers, as the engine reported them — the corroboration that it is the
            // tiling doing the counting and not something else in the scene.
            long creekTiles = (long)Mathf.Ceil(NineMileCreekBuilder.NineMileCreekSeaSize.x / tileMetres.x)
                            * (long)Mathf.Ceil(NineMileCreekBuilder.NineMileCreekSeaSize.y / tileMetres.y);
            Assert.That(creekTiles * 4, Is.EqualTo(425_600L),
                "the vertex count the editor refused to generate for Nine Mile Creek");
            Assert.That(creekTiles * 6, Is.EqualTo(638_400L),
                "the index count the editor refused to generate for Nine Mile Creek");
        }

        // =============================================================================================
        //  3. Every builder goes through the one call — and so does anything handed to the shared wiring
        // =============================================================================================

        [Test]
        public void TheSharedLandRegionWiring_NormalisesASeaThatArrivesTiled()
        {
            // ⭐ THE GUARD THAT COVERS HAND-ROLLED SEAS. A fixture or a future builder that stands a Sea up
            // itself and reaches for the shared wiring gets the quad, rather than re-introducing the
            // refusal one call site at a time. (Built INACTIVE and activated after: WaterSurface is
            // [ExecuteAlways], and a Tiled 760 × 560 renderer that goes live before it is corrected is the
            // very thing being prevented.)
            _sea = new GameObject("Sea");
            _sea.SetActive(false);
            var sr = _sea.AddComponent<SpriteRenderer>();
            var tile = WaterSceneTemplate.LoadSpriteAny(SeaTilePath);
            if (tile == null) Assert.Ignore($"SKIPPED, NOT VERIFIED — {SeaTilePath} has not imported.");
            sr.sprite = tile;
            sr.drawMode = SpriteDrawMode.Tiled;                              // the old hand-rolled shape
            sr.size = NineMileCreekBuilder.NineMileCreekSeaSize;
            _sea.AddComponent<WaterSurface>();

            WaterSceneTemplate.ConfigureLandRegionWater(
                _sea, NineMileCreekBuilder.NineMileCreekSeaCenter,
                NineMileCreekBuilder.NineMileCreekSeaSize,
                NineMileCreekBuilder.NineMileCreekHeightResolution,
                NineMileCreekBuilder.NineMileCreekHeightMin,
                NineMileCreekBuilder.NineMileCreekHeightMax,
                /*maxShoreGradient*/ 0.25f);
            _sea.SetActive(true);

            Assert.That(sr.drawMode, Is.EqualTo(SpriteDrawMode.Simple),
                "the shared land-region wiring must leave the sea plane a quad however it arrived");
            Assert.That(sr.bounds.size.x,
                        Is.EqualTo(NineMileCreekBuilder.NineMileCreekSeaSize.x).Within(1e-3f));
            Assert.That(sr.bounds.size.y,
                        Is.EqualTo(NineMileCreekBuilder.NineMileCreekSeaSize.y).Within(1e-3f));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NoBuilderTilesItsSeaPlane()
        {
            // Source-text, on the NineMileCreekWaterParityTests precedent, because the alternative is
            // running a builder — and a builder replaces the open scene. Four builders drew a sea and all
            // four hand-rolled the same Tiled block; this is what stops a fifth copy being written.
            foreach (string path in new[]
                     {
                         NineMileCreekBuilderPath, StPetersBuilderPath,
                         GreyboxBuilderPath, WaterSceneTemplatePath,
                     })
            {
                Assert.IsTrue(File.Exists(path), $"{path} not found");
                string src = File.ReadAllText(path);
                string file = Path.GetFileName(path);

                Assert.IsTrue(src.IndexOf("ConfigureSeaPlane", StringComparison.Ordinal) >= 0,
                    $"{file} must size its Sea through WaterSceneTemplate.ConfigureSeaPlane — a local copy " +
                    "of that block is how all four builders came to be asking Unity for a tiling it refuses.");

                // Scoped to the SEA's own renderer variable (`wsr` in the three land seas, `seaSr` in the
                // cove) rather than banning the draw mode outright: the ground strips beside them are
                // legitimately tiled, because their sprites actually get drawn.
                foreach (string seaRenderer in new[] { "wsr", "seaSr" })
                    Assert.IsFalse(src.IndexOf(seaRenderer + ".drawMode = SpriteDrawMode.Tiled",
                                               StringComparison.Ordinal) >= 0,
                        $"{file} tiles its Sea renderer again. The tile is not sampled by the water shader, " +
                        "and a region-sized tiling is refused by the engine.");
            }
        }

        // ---- helpers ---------------------------------------------------------------------------------

        /// <summary>A bare sea plane: a SpriteRenderer with the sprite assigned and nothing else on it, so
        /// the quad is measured without an <c>[ExecuteAlways]</c> surface baking beside it.</summary>
        SpriteRenderer StandUpASeaPlane(string spritePath)
        {
            var sprite = WaterSceneTemplate.LoadSpriteAny(spritePath);
            if (sprite == null) Assert.Ignore($"SKIPPED, NOT VERIFIED — {spritePath} has not imported.");

            _sea = new GameObject("Sea", typeof(SpriteRenderer));
            var sr = _sea.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            return sr;
        }
    }
}
#endif
