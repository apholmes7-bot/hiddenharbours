#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE GREYBOX GROUND IS ONE SCALED QUAD, AND IT SORTS UNDER THE ROADS.</b>
    ///
    /// <para><b>What went wrong.</b> <c>NineMileCreekBuilder</c>'s ground fallback was a
    /// <c>SpriteDrawMode.Tiled</c> renderer sized to the region rect, drawing
    /// <c>Art/Tilesets/Grass.png</c> — 32 px at 32 px/unit, so a <b>1 × 1 m</b> tile. Over Nine Mile
    /// Creek's 760 × 560 m that is <b>425,600 tiles = 1,702,400 vertices</b>, and Unity's tiling
    /// generator refuses past a 16-bit index buffer ("Cannot generate 9 slice most likely because the
    /// size is too big"). The renderer drew NOTHING. The fallback for a checkout that cannot paint its
    /// ground had never once put ground on the screen at this region's size.</para>
    ///
    /// <para><b>It was right when it was written.</b> The helper drew a 24 × 40 m town strip beside the
    /// quay — 960 tiles, comfortably inside the ceiling. It became impossible the day the ground was
    /// re-pointed at the WHOLE region, which multiplied the tile count by 443 without touching the line
    /// that tiled it. Same shape of failure as the sea planes, one rect at a time.</para>
    ///
    /// <para><b>⭐ THE SECOND HALF, WHICH IS WHY THIS IS NOT ONLY A DRAW FIX.</b> The fallback sat at
    /// <c>GroundSortingOrder = SortingBands.Sea - 2</c> (−7) — above the road kit's two layers (−17 /
    /// −16) and above a submerged shore plant (−8). The builder's own comment flagged that it "would
    /// cover the roads" and excused it as rare. It was only ever invisible because the tiling was
    /// refused, so making the quad DRAW without re-basing the band would have switched a known defect
    /// on. <see cref="SortingBands.PaintedSeabedMax"/> (−18) is the tightest rung satisfying both
    /// standing rules and the first one under the roads.</para>
    ///
    /// <para><b>Reachability — why this was fixed and not deleted.</b> The path fires when
    /// <c>BuildSplatGround</c> returns false: a null/extent-less RegionDef, or a missing
    /// <c>TerrainSplat.mat</c>. Both are broken checkouts — but the fallback is not hypothetical, it is
    /// ON DISK: the banked <c>Scenes/NineMileCreek.unity</c> carries a <c>Ground</c> SpriteRenderer with
    /// <c>m_DrawMode: 2</c> and <c>m_Size: {x: 760, y: 560}</c> at order −7, because the bank (#518)
    /// predates <c>BuildSplatGround</c> (#527). The owner's next Build + re-bank replaces it; these
    /// tests are what stop the builder minting another one.</para>
    ///
    /// <para>Pure editor-object construction — sprites are made in memory, no scene is loaded, and
    /// nothing is written to the project.</para>
    /// </summary>
    public class NineMileCreekGreyboxGroundTests
    {
        const string NineMileCreekBuilderPath = "Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs";
        const string StPetersBuilderPath      = "Assets/_Project/Code/App/Editor/StPetersBuilder.cs";
        const string GreyboxBuilderPath       = "Assets/_Project/Code/App/Editor/GreyboxBuilder.cs";

        /// <summary>Unity's sprite tiling generator builds a 16-bit index buffer, so it refuses once the
        /// grid needs more vertices than one can address. The exact threshold is the engine's business;
        /// what these tests assert is that a region rect is nowhere near the right side of it.</summary>
        const int SixteenBitVertexCeiling = 65_535;

        /// <summary>The ground tile the fallback used to tile: <c>Art/Tilesets/Grass.png</c>, 32 px at
        /// <c>spritePixelsToUnits: 32</c>. One metre — which is what makes the tile count enormous.</summary>
        const float GrassTileMetres = 1f;

        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // =============================================================================================
        //  1. The quad — one draw, covering the region, at any sprite size
        // =============================================================================================

        [Test]
        public void TheGreyboxGroundIsOneQuad_NotAGridOfTiles()
        {
            var sr = BuildGround(NineMileCreekBuilder.NineMileCreekSeaSize, HalfMetreSquare());

            Assert.That(sr.drawMode, Is.EqualTo(SpriteDrawMode.Simple),
                "the greybox ground must be a single Simple quad. A Tiled draw over the region rect is " +
                "what asked Unity for 1,702,400 vertices and got a refusal — and drew no ground at all, " +
                "which is the opposite of what a fallback is for.");
        }

        [Test]
        public void TheGreyboxGroundCoversTheWholeRegionRect()
        {
            // The assertion that matters for the LOOK: the whole job of a stand-in ground is to be under
            // everything the region puts on it. A quad short of the rect is a bay with objects standing
            // on nothing.
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;
            var sr = BuildGround(size, HalfMetreSquare());

            Assert.That(sr.bounds.size.x, Is.EqualTo(size.x).Within(1e-3f),
                $"the greybox ground is {sr.bounds.size.x} m wide against a {size.x} m region");
            Assert.That(sr.bounds.size.y, Is.EqualTo(size.y).Within(1e-3f),
                $"the greybox ground is {sr.bounds.size.y} m tall against a {size.y} m region");
        }

        [Test]
        public void TheScaleIsDerivedFromTheSprite_NotAHardCodedDoubling()
        {
            // ⚠ THE DEFECT THIS SHAPE PREVENTS. The old fallback branch scaled by `size * 2f` — correct
            // only because Square.png happens to be 16 px at 32 px/unit, i.e. 0.5 m. Feed the same call a
            // 1 m sprite and a 0.5 m sprite: both must land on the region rect, which a hard-coded factor
            // cannot do. (Four sea planes shipped the mirror-image of this bug and covered a QUARTER of
            // their regions — see SeaPlaneQuadTests.)
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;

            foreach (var (label, sprite) in new[]
                     {
                         ("a 0.5 m square", HalfMetreSquare()),
                         ("a 1 m square",   OneMetreSquare()),
                     })
            {
                var sr = BuildGround(size, sprite);

                Assert.That(sr.bounds.size.x, Is.EqualTo(size.x).Within(1e-3f),
                    $"{label}: the ground covers {sr.bounds.size.x} m of a {size.x} m region — the scale " +
                    "is being assumed rather than read off the sprite");
                Assert.That(sr.bounds.size.y, Is.EqualTo(size.y).Within(1e-3f),
                    $"{label}: the ground covers {sr.bounds.size.y} m of a {size.y} m region");
            }
        }

        [Test]
        public void BuildingTheGreyboxGroundTwiceConverges()
        {
            // A builder re-run must converge, not compound — the scale is computed from the SPRITE's own
            // size every time, never multiplied onto whatever the transform already carried.
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;
            var sprite = HalfMetreSquare();

            Vector3 once  = BuildGround(size, sprite).transform.localScale;
            Vector3 twice = BuildGround(size, sprite).transform.localScale;

            Assert.That(twice, Is.EqualTo(once),
                "a second build moved the scale — the owner's next click would grow the ground");
        }

        [Test]
        public void AMissingSquareLeavesNoGroundRatherThanThrowing()
        {
            // The fallback's own fallback. A checkout broken enough to lose BOTH the splat material and
            // the square is past the point of drawing anything, but the builder must survive it and say
            // so — a NullReferenceException here would abort the rest of the region's build.
            var go = NineMileCreekBuilder.MakeGreyboxGround(
                "Ground", null, NineMileCreekBuilder.NineMileCreekSeaCenter,
                NineMileCreekBuilder.NineMileCreekSeaSize,
                NineMileCreekBuilder.GroundSortingOrder, Color.green);
            _spawned.Add(go);

            Assert.IsNotNull(go, "the builder must still get an object back to keep building around");
            Assert.IsNull(go.GetComponent<SpriteRenderer>().sprite,
                "nothing to draw, and nothing pretending to");
        }

        // =============================================================================================
        //  2. The band — under the roads it used to cover
        // =============================================================================================

        [Test]
        public void TheGreyboxGroundSortsUnderTheRoadsItUsedToCover()
        {
            // ⭐ THE REGRESSION GUARD FOR THE HALF THAT IS NOT ABOUT VERTICES. Wharf Road, the bar road and
            // the gully path are laid by NineMileCreekRoadPainter ON the ground; a stand-in ground above
            // them is an opaque rectangle over the region's published routes.
            Assert.That(NineMileCreekBuilder.GroundSortingOrder,
                Is.LessThan(RoadKitV3.TopSortingOrder),
                $"the greybox ground sorts at {NineMileCreekBuilder.GroundSortingOrder} and the road kit's " +
                $"lower layer at {RoadKitV3.TopSortingOrder} — the ground would draw OVER the roads. It sat " +
                "at Sea - 2 (−7) for exactly this reason, and the builder's own comment said so and left it.");

            Assert.That(NineMileCreekBuilder.GroundSortingOrder,
                Is.LessThan(RoadKitV3.SkirtSortingOrder),
                "…and under the road's skirt layer, which is the rung above its top");

            // The painted ground it stands in for. The stand-in belongs in the same part of the stack —
            // at or just above the surface it replaces, never up among the things laid on top of it.
            Assert.That(NineMileCreekBuilder.GroundSortingOrder,
                Is.GreaterThanOrEqualTo(TerrainSplatSurface.DefaultSortingOrder),
                "the greybox ground stands in for the painted ground, so it cannot sort BELOW it");
            Assert.That(NineMileCreekBuilder.GroundSortingOrder,
                Is.LessThan(SortingBands.Sea),
                "…and must stay under the sea plane, or the wet-dry clip stops deciding what you see");
        }

        // =============================================================================================
        //  3. WHY it cannot go back to tiling
        // =============================================================================================

        [Test]
        public void TilingAGroundTileAcrossTheseRegionsWouldBlowUnitysVertexLimit()
        {
            // The arithmetic, pinned, so the reason survives the comment. If someone re-sizes a region or
            // reaches for a Tiled ground again, this is the number they are up against.
            var regions = new[]
            {
                ("Nine Mile Creek", NineMileCreekBuilder.NineMileCreekSeaSize),
                ("St Peters",       StPetersBuilder.RegionWorldSize),
            };

            foreach (var (region, size) in regions)
            {
                long tiles = (long)Mathf.Ceil(size.x / GrassTileMetres) * (long)Mathf.Ceil(size.y / GrassTileMetres);

                Assert.That(tiles * 4, Is.GreaterThan(SixteenBitVertexCeiling),
                    $"{region} ({size.x} × {size.y} m at a {GrassTileMetres} m tile) would need {tiles:N0} " +
                    $"tiles = {tiles * 4:N0} vertices, which is meant to be far over Unity's ceiling. If it " +
                    "is not any more, a tiled ground is still the wrong shape for a greybox stand-in.");
            }

            // Nine Mile Creek's own numbers — the ones the shipped scene's Ground renderer still asks for.
            long creek = (long)Mathf.Ceil(NineMileCreekBuilder.NineMileCreekSeaSize.x / GrassTileMetres)
                       * (long)Mathf.Ceil(NineMileCreekBuilder.NineMileCreekSeaSize.y / GrassTileMetres);
            Assert.That(creek, Is.EqualTo(425_600L), "tiles the old fallback asked Unity to generate");
            Assert.That(creek * 4, Is.EqualTo(1_702_400L), "…and the vertices they come to");
        }

        // =============================================================================================
        //  4. No builder lays a region-scale tiled GROUND — the ground path, not the sea's
        // =============================================================================================

        [Test]
        public void NoBuilderTilesAGroundPlane()
        {
            // Source-text, on the NineMileCreekWaterParityTests precedent, because the alternative is
            // running a builder — and a builder replaces the open scene.
            //
            // ⚠ SCOPED TO THE GROUND, DELIBERATELY. The remaining Tiled draws in these files are SEA
            // planes on `wsr` / `seaSr`, which are a separate fix with its own guard (SeaPlaneQuadTests
            // .NoBuilderTilesItsSeaPlane). This asserts every Tiled draw that survives is one of those —
            // so it passes whether or not the sea fix has landed, and fails the moment a GROUND is tiled
            // again. `sr` is the ground helper's own renderer variable.
            var seaRenderers = new HashSet<string> { "wsr", "seaSr" };

            foreach (string path in new[] { NineMileCreekBuilderPath, StPetersBuilderPath, GreyboxBuilderPath })
            {
                Assert.IsTrue(File.Exists(path), $"{path} not found");
                string file = Path.GetFileName(path);
                string[] lines = File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    int at = lines[i].IndexOf("drawMode = SpriteDrawMode.Tiled", StringComparison.Ordinal);
                    if (at < 0) continue;

                    string owner = RendererVariableBefore(lines[i], at);
                    Assert.IsTrue(seaRenderers.Contains(owner),
                        $"{file}:{i + 1} tiles '{owner}', which is not one of the sea planes " +
                        $"({string.Join("/", seaRenderers)}). A region-scale tiled ground is refused by the " +
                        "engine and draws nothing — scale one quad instead, as MakeGreyboxGround does.");
                }
            }
        }

        [Test]
        public void OnlyNineMileCreekEverHadAGroundFallbackToFix()
        {
            // The sibling builders, checked because "does this bug live anywhere else" is the first
            // question the fix has to answer. St Peters RETIRED its MakeTiledGround — its island ground is
            // painted by StPetersShorePainter, which lays TILEMAPS (chunked by the engine, no 16-bit
            // ceiling to hit). The greybox cove draws no ground plane at all.
            string stPeters = File.ReadAllText(StPetersBuilderPath);
            Assert.IsFalse(stPeters.Contains("MakeTiledGround("),
                "StPetersBuilder has called no ground-tiling helper since the painted shore landed — if it " +
                "does again, it is a 760 × 520 m region and the arithmetic above applies to it too.");

            string greybox = File.ReadAllText(GreyboxBuilderPath);
            Assert.IsFalse(greybox.Contains("MakeTiledGround("),
                "the greybox cove draws no ground plane; if one is added, size it as a quad");

            // And the helper that DID tile is gone by name, so a re-introduction reads as a new decision
            // rather than a revival.
            string creek = File.ReadAllText(NineMileCreekBuilderPath);
            Assert.IsFalse(creek.Contains("MakeTiledGround("),
                "NineMileCreekBuilder's tiled ground helper is retired — MakeGreyboxGround replaced it");
            Assert.IsTrue(creek.Contains("MakeGreyboxGround("),
                "…and the region still lays a stand-in ground when the splat cannot cover");
        }

        // ---- helpers ---------------------------------------------------------------------------------

        /// <summary>Run the builder's real helper and hand back the renderer it made.</summary>
        SpriteRenderer BuildGround(Vector2 size, Sprite square)
        {
            var go = NineMileCreekBuilder.MakeGreyboxGround(
                "Ground", square, NineMileCreekBuilder.NineMileCreekSeaCenter, size,
                NineMileCreekBuilder.GroundSortingOrder, new Color(0.40f, 0.46f, 0.40f));
            _spawned.Add(go);
            return go.GetComponent<SpriteRenderer>();
        }

        /// <summary>Square.png's shipped geometry: 16 px at 32 px/unit.</summary>
        Sprite HalfMetreSquare() => MakeSquare(16, 32f);

        /// <summary>A metre-square sprite — the same call, a different sprite, to prove the scale is read
        /// rather than assumed.</summary>
        Sprite OneMetreSquare() => MakeSquare(32, 32f);

        Sprite MakeSquare(int pixels, float pixelsPerUnit)
        {
            var tex = new Texture2D(pixels, pixels);
            _spawned.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, pixels, pixels),
                                       new Vector2(0.5f, 0.5f), pixelsPerUnit);
            _spawned.Add(sprite);
            return sprite;
        }

        /// <summary>The identifier immediately left of a <c>.drawMode = …</c> assignment.</summary>
        static string RendererVariableBefore(string line, int dotAt)
        {
            int end = line.LastIndexOf('.', dotAt);        // the '.' of ".drawMode"
            if (end <= 0) return line.Trim();
            int start = end;
            while (start > 0 && (char.IsLetterOrDigit(line[start - 1]) || line[start - 1] == '_')) start--;
            return line.Substring(start, end - start);
        }
    }
}
#endif
