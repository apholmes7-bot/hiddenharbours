using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The working quay at Nine Mile Creek — the wharf tile kit's second consumer, and the first one on
    /// a region that was already authored around its wharf.
    ///
    /// <para><b>What these tests are actually protecting.</b> The quay was re-DRESSED, not re-sited: the
    /// shoreline fence dips around the same rectangle, the dock zone sits on its east tip, and the boat
    /// parks 3 m off that tip so <c>ControlSwitcher</c>'s pure distance test lets you disembark (owner
    /// playtest gap #52). Three separate pieces of the builder therefore agree on one rectangle, and the
    /// cheapest way for that to stop being true is for someone to move the deck by a metre. So the deck
    /// footprint is checked AGAINST the fence and the dock geometry rather than against a copy of
    /// itself.</para>
    ///
    /// <para>The rest is the kit's own contract: back-to-front draw order with one sorting order per row
    /// (the 24 px face overhangs the cell below, so a wrong order means a face drawn over its neighbour's
    /// deck), variants the atlas actually has, and a deck height MEASURED off the authored terrain rather
    /// than asserted from a comment — the #345 lesson, which is how a pier rooted in the sea was
    /// caught.</para>
    /// </summary>
    public class NineMileCreekWharfTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private RectTidalTerrain MakeCreekTerrain()
        {
            var go = new GameObject("TidalTerrain");
            _spawned.Add(go);
            var terrain = go.AddComponent<RectTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            return terrain;
        }

        // ---- 1. the deck is the rectangle the region is already authored around --------------------

        [Test]
        public void DeckFootprint_MatchesTheTerrainPlateauTheRegionWasAuthoredWith()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();
            Vector2 c = NineMileCreekBuilder.NineMileCreekWharfCenter;
            Vector2 h = NineMileCreekBuilder.NineMileCreekWharfHalfSize;

            Assert.AreEqual(c.x - h.x, deck.xMin, 1e-4f, "the deck's west root must meet the wharf plateau's west edge");
            Assert.AreEqual(c.x + h.x, deck.xMax, 1e-4f, "the deck's east head must meet the wharf plateau's east edge");
            Assert.AreEqual(c.y - h.y, deck.yMin, 1e-4f, "the deck's south lip must meet the wharf plateau's south edge");
            Assert.AreEqual(c.y + h.y, deck.yMax, 1e-4f, "the deck's north curb must meet the wharf plateau's north edge");
        }

        [Test]
        public void DockZone_IsOnTheDeckHead_AndDisembark_IsOnThePlanks()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();

            Assert.AreEqual(deck.xMax, NineMileCreekBuilder.DockZonePos.x, 1e-4f,
                "the dock zone IS the deck's seaward head — you stop the boat against the concrete, " +
                "so if the deck moves the dock zone has moved with it or the two have come apart");
            Assert.IsTrue(deck.yMin <= NineMileCreekBuilder.DockZonePos.y &&
                          NineMileCreekBuilder.DockZonePos.y <= deck.yMax,
                "the dock zone must sit against the head, not off one of its corners");

            Vector2 disembark = NineMileCreekBuilder.DisembarkPos;
            Assert.IsTrue(deck.Contains(disembark),
                $"the disembark spot {disembark} must land the on-foot player ON the deck {deck} — " +
                "stepping ashore into the dredged harbour is the bug this rectangle exists to prevent");
        }

        [Test]
        public void ShorelineFence_DipsAroundExactlyTheDeck()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();
            var pts = NineMileCreekBuilder.ShorelinePoints;

            // The fence traces the deck's north edge out to the head, down the head, and back along the
            // south edge — the dip that makes the wharf a solid peninsula. Every one of those four
            // corners has to be a corner of THIS rectangle.
            foreach (var corner in new[]
                     {
                         new Vector2(deck.xMin, deck.yMax),
                         new Vector2(deck.xMax, deck.yMax),
                         new Vector2(deck.xMax, deck.yMin),
                         new Vector2(deck.xMin, deck.yMin),
                     })
                Assert.IsTrue(pts.Any(p => Vector2.Distance(p, corner) < 1e-4f),
                    $"the shoreline fence must turn at the deck corner {corner} — the fence and the " +
                    $"planks have drifted apart, so the boat can sail through a wharf that is drawn " +
                    $"there. Fence: {string.Join(" ", pts)}");
        }

        // ---- 2. the kit's draw rule, as numbers ---------------------------------------------------

        [Test]
        public void EveryDeckRow_GetsItsOwnSortingOrder_SouthDrawnLast()
        {
            var orders = new List<int>();
            for (int y = NineMileCreekWharf.MaxCellY; y >= NineMileCreekWharf.MinCellY; y--)
                orders.Add(NineMileCreekWharf.SortingOrderFor(y));

            Assert.AreEqual(NineMileCreekWharf.WidthCells, orders.Distinct().Count(),
                "the deck's 24 px face overhangs the cell below it, so every row needs its OWN order or " +
                "a face is drawn over its southern neighbour's deck. The band is only six wide — a " +
                "wharf that outgrows it has to stop being a plain sprite set");

            for (int i = 1; i < orders.Count; i++)
                Assert.Greater(orders[i], orders[i - 1],
                    "north rows draw first/behind and south rows last/in front — the kit's back-to-front rule");

            foreach (int o in orders)
            {
                Assert.GreaterOrEqual(o, NineMileCreekWharf.SortingOrderMin);
                Assert.LessOrEqual(o, NineMileCreekWharf.SortingOrderMax);
            }
        }

        [Test]
        public void TheWholeDeckBand_SitsAboveTheSeaPlane_AndBelowTheCharacters()
        {
            // The Sea plane the builder lays down, and the first order YSortSprite's character band uses.
            const int seaSortingOrder = -5;
            const int characterBandStart = 2;

            Assert.Greater(NineMileCreekWharf.SortingOrderMin, seaSortingOrder,
                "a wharf stands OVER the water: if the deck's northmost row is not above the Sea plane " +
                "the harbour is drawn on top of its own quay");
            Assert.Less(NineMileCreekWharf.SortingOrderMax, characterBandStart,
                "and under the people standing on it");
            Assert.Greater(NineMileCreekWharf.BreakwaterSortingOrder, seaSortingOrder,
                "the breakwater has to be above the sea it is armour against, or the harbour is drawn " +
                "over its own arm");
        }

        [Test]
        public void DeckCellsBackToFront_CoverEveryCell_NorthRowsFirst()
        {
            var cells = NineMileCreekWharf.DeckCellsBackToFront().ToList();

            Assert.AreEqual(NineMileCreekWharf.LengthCells * NineMileCreekWharf.WidthCells, cells.Count,
                "every cell of the rectangle is placed exactly once");
            Assert.AreEqual(cells.Count, cells.Distinct().Count(), "no cell is placed twice");

            for (int i = 1; i < cells.Count; i++)
                Assert.LessOrEqual(cells[i].y, cells[i - 1].y,
                    "the iteration order IS the draw order — it must never step back north");
        }

        // ---- 3. the variants are ones the atlas actually has --------------------------------------

        [Test]
        public void EveryDeckCell_AsksTheAtlasForASliceItHas()
        {
            foreach (var cell in NineMileCreekWharf.DeckCellsBackToFront())
            {
                string variant = NineMileCreekWharf.DeckVariantAt(cell.x, cell.y);
                Assert.Contains(variant, WharfKitCatalog.DeckVariants,
                    $"cell {cell} asks for a variant the kit does not publish");

                // Throws on an unknown material/variant, which is the point: a typo here would place a
                // silently wrong tile rather than fail.
                int index = WharfKitCatalog.AtlasIndex(NineMileCreekWharf.DeckMaterial, variant);
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, WharfKitCatalog.AtlasCols * WharfKitCatalog.AtlasRows);
            }
        }

        [Test]
        public void ARectangleNeverNeedsACapOrADiagonal()
        {
            var used = NineMileCreekWharf.DeckCellsBackToFront()
                                         .Select(c => NineMileCreekWharf.DeckVariantAt(c.x, c.y))
                                         .Distinct()
                                         .ToList();

            foreach (string v in used)
                Assert.IsFalse(v.StartsWith("cap") || v.StartsWith("di"),
                    $"'{v}' is an end cap or a 45° cut — reaching for one means the quay has stopped " +
                    "being a rectangle, and the fence and dock geometry have not been told");

            Assert.Contains("ctr", used, "an 8 × 6 deck has an interior");
            foreach (string corner in new[] { "coNW", "coNE", "coSW", "coSE" })
                Assert.Contains(corner, used, "…and four outer corners");
        }

        [Test]
        public void TheDeckMaterial_IsTheConcreteRow_NotTheIslandsTimber()
        {
            Assert.Contains(NineMileCreekWharf.DeckMaterial, WharfKitCatalog.DeckMaterials);
            Assert.AreEqual("quay", NineMileCreekWharf.DeckMaterial,
                "design/nine-mile-creek-wharf.md §3's build table names the concrete 'quay' row for this " +
                "wharf. The island's own dock is deliberately the rung below it in the same kit's " +
                "age/means gradient, so the two harbours are told apart by their material before a " +
                "single label is read — changing this collapses that read");
        }

        // ---- 4. the deck height is MEASURED, not claimed -------------------------------------------

        [Test]
        public void DeckElevation_IsMeasuredOffTheTerrain_AndIsDryAtEveryTide()
        {
            var terrain = MakeCreekTerrain();
            float deck = NineMileCreekWharf.DeckElevationFrom(terrain);

            Assert.AreEqual(NineMileCreekBuilder.NineMileCreekLandElevation, deck, 0.01f,
                "the deck is level with the ground it launches from — the wharf plateau");

            // The live tide is the START region's, not this region's own: nothing re-points the tide per
            // region yet, so the start scene's profile is what actually runs here — RegionValidation's
            // WidestSwing exists for exactly this, and names the Nine Mile Creek builder's caveat as its
            // reason. The creek's own authored swing (±0.8 m, the gentle market harbour) is strictly
            // inside the island's, so the island's is the envelope that has to be cleared.
            var live = RegionValidation.SwingOf(StPetersBuilder.TideMean, StPetersBuilder.TideAmplitude);

            Assert.Greater(deck, live.High,
                $"the quay stands at {deck:0.00} m against a spring high of {live.High:0.00} m — a " +
                "working wharf does not go under. Move the deck; do not lower the tide");
        }

        [Test]
        public void TheDeckHeadStandsOverWater_SoTheQuayIsAWharfAndNotACauseway()
        {
            var terrain = MakeCreekTerrain();

            // Just seaward of the head: the dredged harbour, which is what a boat comes alongside into.
            float seabed = terrain.ElevationAt(new Vector2(NineMileCreekBuilder.DockZonePos.x + 1.5f, 0f));
            Assert.Less(seabed, 0f,
                $"the ground {seabed:0.00} m off the wharf head is above chart datum — the boat would " +
                "ground where it is supposed to lie alongside");
        }

        // ---- 5. the breakwater shelters the basin without closing the approach ---------------------

        [Test]
        public void TheBreakwater_IsAContiguousCribRun_CappedAtTheSeawardTip()
        {
            var blocks = NineMileCreekWharf.BreakwaterBlocks();
            Assert.AreEqual(NineMileCreekWharf.BreakwaterBlockCount, blocks.Count);
            Assert.Greater(blocks.Count, 1, "an arm of one block is not an arm");

            Assert.Contains(NineMileCreekWharf.BreakwaterArmour, WharfKitCatalog.ArmourTypes);

            for (int i = 0; i < blocks.Count; i++)
            {
                string expected = i == blocks.Count - 1 ? "end" : "straight";
                Assert.AreEqual(expected, blocks[i].Name,
                    "the tileable run carries the arm and the battered cap finishes it");
                Assert.Contains(blocks[i].Name, WharfKitCatalog.ArmourVariants);
                Assert.GreaterOrEqual(
                    WharfKitCatalog.BreakwaterIndex(NineMileCreekWharf.BreakwaterArmour, blocks[i].Name), 0);
            }

            // The positions are CENTRES (the armour sheet pivots top-centre), so the run must butt the
            // arm's west end and stay inside its east end — a half-block on the beach is the failure.
            float half = NineMileCreekWharf.ArmourWidthMetres * 0.5f;
            Assert.AreEqual(NineMileCreekWharf.BreakwaterWestX, blocks[0].Position.x - half, 1e-4f);
            Assert.LessOrEqual(blocks[blocks.Count - 1].Position.x + half,
                               NineMileCreekWharf.BreakwaterEastX + 1e-4f);
            for (int i = 1; i < blocks.Count; i++)
                Assert.AreEqual(NineMileCreekWharf.ArmourWidthMetres,
                                blocks[i].Position.x - blocks[i - 1].Position.x, 1e-4f,
                                "gaps in a breakwater are holes the sea comes through");
        }

        [Test]
        public void TheBreakwater_LeavesTheApproachAndTheDeckClear()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();
            Assert.Less(NineMileCreekWharf.BreakwaterY, deck.yMin,
                "the arm lies south of the basin, not across the quay it shelters");

            // You come in from the EAST and stop against the head. Neither the park nor the dock zone may
            // be south of the arm, or the arrival is walled off from the wharf it arrives at.
            foreach (var p in new[] { NineMileCreekBuilder.ArrivalPos, NineMileCreekBuilder.DockZonePos,
                                      NineMileCreekBuilder.DisembarkPos })
                Assert.Greater(p.y, NineMileCreekWharf.BreakwaterY,
                    $"{p} is on the seaward side of the breakwater — the boat would have to cross it");

            // The return passage east to the cove has to stay outside the arm's reach too.
            Assert.Greater(NineMileCreekBuilder.ToCovePassagePos.x, NineMileCreekWharf.BreakwaterEastX,
                "the passage home must be clear of the arm's seaward tip");
        }
    }
}
