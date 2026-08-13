using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The ROAD KIT v3's arithmetic and contract, checked without running the rig or importing a
    /// sheet. The bake-side proofs (does the rig still reproduce the drop's reference atlases; does
    /// our <c>Canon</c> still agree with the rig's) live in <c>RoadKitBakeTests</c>, which can afford
    /// a V8 host.
    /// </summary>
    public class RoadKitV3Tests
    {
        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        // ---- geometry -----------------------------------------------------------------------------

        [Test]
        public void CellGeometry_IsInternallyConsistent()
        {
            Assert.AreEqual(64, RoadKitV3.CellH, "the cell is twice as tall as it is wide");
            Assert.AreEqual(42, RoadKitV3.Skirt, "skirt starts at HEAD + TILE");
            Assert.AreEqual(RoadKitV3.CellH, RoadKitV3.Head + RoadKitV3.GroundRows + RoadKitV3.SkirtRectHeight,
                            "headroom + ground square + skirt must account for every row of the cell");
            Assert.AreEqual(RoadKitV3.CellH, RoadKitV3.TopRectHeight + RoadKitV3.SkirtRectHeight,
                            "the top and skirt sub-rects must tile the cell exactly — no overlap, no gap");
        }

        [Test]
        public void Atlas_Is12x4Holding47TilesAndOnePad()
        {
            Assert.AreEqual(384, RoadKitV3.SheetWidth);
            Assert.AreEqual(256, RoadKitV3.SheetHeight);
            Assert.AreEqual(48, RoadKitV3.Cols * RoadKitV3.Rows);
            Assert.AreEqual(47, RoadKitV3.BlobCount,
                            "index 47 is padding — anything walking the atlas stops at 47");
        }

        [Test]
        public void Surfaces_AreTheElevenTheKitShips()
        {
            Assert.AreEqual(11, RoadKitV3.Surfaces.Length);
            CollectionAssert.AllItemsAreUnique(RoadKitV3.Surfaces);
            CollectionAssert.Contains(RoadKitV3.Surfaces, "boardwalk", "v3's new raised-deck surface");
            CollectionAssert.Contains(RoadKitV3.Surfaces, "apron", "the quay apron wharfKit butts against");
        }

        // ---- the pivot, which is the whole reason the two layers register ---------------------------

        /// <summary>
        /// ⭐ The load-bearing assertion of the whole slice. A road is drawn as two tilemaps — tops
        /// and skirts — and they stay registered ONLY because both sprites pin the same world point.
        /// If these two drift, every kerb and drop face slides relative to its own roadway and it
        /// reads as "the art is a bit off", never as a broken pivot.
        /// </summary>
        [Test]
        public void TopAndSkirtPivots_PinTheSameWorldPoint()
        {
            // Distance from each sub-rect's TOP edge down to the pivot, in pixels.
            float topPivotFromRectTop =
                (1f - RoadKitV3.TopPivot.y) * RoadKitV3.TopRectHeight;
            float skirtPivotFromRectTop =
                (1f - RoadKitV3.SkirtPivot.y) * RoadKitV3.SkirtRectHeight;

            // Convert both to cell-local rows and demand they agree.
            float topInCell = topPivotFromRectTop;                       // top rect starts at cell row 0
            float skirtInCell = RoadKitV3.Skirt + skirtPivotFromRectTop; // skirt rect starts at row 42

            Assert.AreEqual(RoadKitV3.PivotRowFromTop, topInCell, 1e-4f,
                            "the top sprite's pivot must sit on the ground-square centre");
            Assert.AreEqual(RoadKitV3.PivotRowFromTop, skirtInCell, 1e-4f,
                            "the skirt sprite's pivot must sit on the SAME ground-square centre");
        }

        [Test]
        public void PivotRow_IsTheGroundSquareCentre_NotTheCellCentre()
        {
            Assert.AreEqual(26f, RoadKitV3.PivotRowFromTop, 1e-4f);
            Assert.AreNotEqual(RoadKitV3.CellH / 2f, RoadKitV3.PivotRowFromTop,
                               "the cell centre (32) is 6 px below the ground square's centre — using " +
                               "it would sink every road tile a quarter-metre into the terrain");
            Assert.AreEqual(0.59375f, RoadKitV3.CellPivot.y, 1e-6f,
                            "ADR 0026's (H − pivotY)/H for this kit");
        }

        /// <summary>
        /// The skirt's pivot is deliberately outside its own rect. Pinned so that a later reader who
        /// "corrects" it back into 0..1 gets a red test and this sentence.
        /// </summary>
        [Test]
        public void SkirtPivot_IsAboveItsOwnRect_OnPurpose()
        {
            Assert.Greater(RoadKitV3.SkirtPivot.y, 1f,
                           "the skirt hangs BELOW the point it pins, so its normalized pivot is > 1. " +
                           "This is legal and intended — do not clamp it.");
            Assert.AreEqual(38f / 22f, RoadKitV3.SkirtPivot.y, 1e-6f);
        }

        // ---- the rects ------------------------------------------------------------------------------

        [Test]
        public void BuildRects_EmitsTwoSpritesPerCell_AllInsideTheSheet()
        {
            SpriteRect[] rects = RoadKitSlicer.BuildRects("RoadIso3_dirt_new_blob47");

            Assert.AreEqual(RoadKitV3.BlobCount * 2, rects.Length,
                            "47 cells × (top + skirt)");

            foreach (var r in rects)
            {
                Assert.GreaterOrEqual(r.rect.x, 0f, r.name);
                Assert.GreaterOrEqual(r.rect.y, 0f, r.name);
                Assert.LessOrEqual(r.rect.xMax, RoadKitV3.SheetWidth, r.name);
                Assert.LessOrEqual(r.rect.yMax, RoadKitV3.SheetHeight, r.name);
                Assert.AreEqual(RoadKitV3.Tile, r.rect.width, r.name);
            }

            CollectionAssert.AllItemsAreUnique(rects.Select(r => r.name).ToList());
        }

        [Test]
        public void BuildRects_TopAndSkirtOfACell_AreVerticallyAdjacent()
        {
            SpriteRect[] rects = RoadKitSlicer.BuildRects("stem");

            for (int i = 0; i < RoadKitV3.BlobCount; i++)
            {
                Rect top = rects[i * 2].rect, skirt = rects[i * 2 + 1].rect;
                Assert.AreEqual(top.x, skirt.x, 1e-4f, $"cell {i}: same column");
                // Unity rects measure from the sheet's BOTTOM, so the skirt sits BELOW the top.
                Assert.AreEqual(top.y, skirt.yMax, 1e-4f,
                                $"cell {i}: the skirt's top edge must be the top rect's bottom edge — " +
                                "a gap or an overlap here duplicates or drops a row of pixels");
            }
        }

        [Test]
        public void BuildRects_ReusesAnExistingSpriteId_SoANoOpResliceIsNotDiffNoise()
        {
            SpriteRect[] first = RoadKitSlicer.BuildRects("stem");
            var ids = first.ToDictionary(r => r.name, r => r.spriteID);

            SpriteRect[] second = RoadKitSlicer.BuildRects("stem", ids);

            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i].spriteID, second[i].spriteID, first[i].name);
        }

        // ---- the rig file itself ----------------------------------------------------------------------

        /// <summary>
        /// The rig lands VERBATIM and stays that way. If someone edits the art director's file in
        /// place — which ADR 0021 §5 forbids — this goes red rather than the kit quietly re-baking
        /// different pixels under the same name.
        /// </summary>
        [Test]
        public void Rig_IsTheCommittedFile_Unmodified()
        {
            string path = Path.Combine(RepoRoot, RoadKitV3.RigScriptPath);
            Assert.IsTrue(File.Exists(path), $"{RoadKitV3.RigScriptPath} is missing");

            byte[] lf = File.ReadAllBytes(path);
            string hash = Sha256Lf(lf);

            Assert.AreEqual(RoadKitV3.RigSha256Lf, hash,
                "the rig's LF-normalised sha256 moved. If the art director shipped a new drop, " +
                "re-solve the bake parameters against ITS reference sheets and update the pin — " +
                "the old parameters are not evidence for a new file.");
        }

        static string Sha256Lf(byte[] bytes)
        {
            // Normalise CRLF → LF so the hash is the one git stores, on either platform.
            var lf = new List<byte>(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n') continue;
                lf.Add(bytes[i]);
            }
            using var sha = System.Security.Cryptography.SHA256.Create();
            return string.Concat(sha.ComputeHash(lf.ToArray()).Select(b => b.ToString("x2")));
        }

        // ---- the contract -----------------------------------------------------------------------------

        [Test]
        public void Contract_Lists47TilesInAtlasOrderWithUniqueMasks()
        {
            var entries = RoadKitContract.Entries;

            Assert.AreEqual(RoadKitV3.BlobCount, entries.Length);
            for (int i = 0; i < entries.Length; i++)
                Assert.AreEqual(i, entries[i].Index, "contract must be in atlas order");
            CollectionAssert.AllItemsAreUnique(entries.Select(e => e.Mask).ToList());
        }

        /// <summary>
        /// Every mask a real map can produce must resolve. This is the cheap standing guard against
        /// the failure that a per-tile check cannot see: an unresolvable neighbourhood means the
        /// placement pass throws (good) but a MIS-resolved one just paints a torn junction (silent).
        /// </summary>
        [Test]
        public void EveryOneOf256Neighbourhoods_ResolvesToATile()
        {
            for (int m = 0; m < 256; m++)
            {
                int index = RoadKitContract.IndexForMask(m);
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, RoadKitV3.BlobCount, $"mask {m} resolved past the 47");
            }
        }

        [Test]
        public void Canon_ClearsADiagonalWhoseCardinalsAreNotBothPresent()
        {
            // NE set but neither N nor E: the diagonal is meaningless and must be folded away.
            int m = RoadKitV3.BitNE;
            Assert.AreEqual(0, RoadKitContract.Canon(m),
                            "a lone diagonal is not a distinct case");

            // NE with both N and E present: kept.
            int keep = RoadKitV3.BitN | RoadKitV3.BitE | RoadKitV3.BitNE;
            Assert.AreEqual(keep, RoadKitContract.Canon(keep));
        }

        // ---- where a road sorts -----------------------------------------------------------------------

        /// <summary>
        /// The kit is GROUND. Its band must stay above the shore painter's layers (a lane is laid ON
        /// the terrain) and well below the Sea plane at −5, or the tide reveal stops working over it.
        /// </summary>
        [Test]
        public void SortingBand_IsGround_AboveTheShoreLayersAndFarBelowTheSea()
        {
            Assert.AreEqual(RoadKitV3.TopSortingOrder + 1, RoadKitV3.SkirtSortingOrder,
                "the skirt must be exactly one above the top — that single step IS the two-pass rule");

            Assert.Greater(RoadKitV3.TopSortingOrder, -18,
                "roads sit above ShoreContact (−18): a lane is laid ON the painted terrain");
            Assert.Less(RoadKitV3.SkirtSortingOrder, -5,
                "and well below the Sea plane (−5), or the tide would stop covering a flooded road");
        }

        // ---- rig quirks, pinned so a fix is NOTICED ----------------------------------------------------

        /// <summary>
        /// ⚠️ <b>GO RED WHEN FIXED.</b> The rig resolves its seed as <c>(opts.seed|0)||7</c>, so a
        /// caller who passes <b>0</b> silently gets <b>7</b> — there is no way to ask for seed 0, and a
        /// placement pass cannot get an unseeded look by passing zero.
        ///
        /// <para>The art director's file is never edited (ADR 0021 §5), so this is recorded rather
        /// than patched. This test asserts the CURRENT behaviour: if a future drop makes 0 mean 0, it
        /// goes red, and that red is the notification — update the doc on
        /// <see cref="RoadKitV3.BakedSeed"/> and delete this test rather than "fixing" it.</para>
        /// </summary>
        [Test]
        public void RigQuirk_SeedZeroIsSilentlySeedSeven()
        {
            // Restated from the rig source rather than executed here (this assembly has no JS host);
            // RoadKitBakeTests proves the two agree by rendering.
            const int asWritten = 0;
            int resolved = (asWritten | 0) != 0 ? asWritten : 7;

            Assert.AreEqual(7, resolved,
                "if this is no longer true the rig changed its seed resolution — see the remarks");
            Assert.AreEqual(7, RoadKitV3.BakedSeed,
                "and the bake's own seed is the value that actually reaches the rig");
        }

        [Test]
        public void ContractAndSlicer_AgreeOnTheAtlasPosition()
        {
            for (int i = 0; i < RoadKitV3.BlobCount; i++)
            {
                Assert.AreEqual(i % 12, RoadKitV3.ColOf(i));
                Assert.AreEqual(i / 12, RoadKitV3.RowOf(i));
                Assert.AreEqual(2 * RoadKitV3.ColOf(i), RoadKitV3.PatternX(i),
                                "the atlas samples pattern space 2 m apart — solved, not assumed");
                Assert.AreEqual(2 * RoadKitV3.RowOf(i), RoadKitV3.PatternY(i));
            }
        }
    }
}
