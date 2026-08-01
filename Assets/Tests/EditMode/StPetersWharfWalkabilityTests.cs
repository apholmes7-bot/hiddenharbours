using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Player;
using HiddenHarbours.Boats;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE PIER MUST BE WALKABLE — the sail-home beat.</b> St Peters is tide-gated
    /// (<c>TideGatedWalk = true</c>) and its one dock stands over a dredged <b>−1.0 m</b> slip, so with the
    /// on-foot sim reading only (ground vs water level) the ratified disembark at <c>(328, 0)</c> was open
    /// water for most of every tide: you sailed home to your own island and swam across your own pier.
    ///
    /// <para>These assert the fix against the <b>REAL authored terrain</b> (the same
    /// <see cref="StPetersBuilder.ConfigureTidalTerrain"/> the builder applies) rather than against the
    /// numbers — a test that re-states the constant it is checking proves nothing, which is the discipline
    /// the pier's own root measurement already follows. The <c>Before_</c> test is the sabotage: it measures
    /// how deep the water was, so the fix is shown landing on a real defect.</para>
    /// </summary>
    public class StPetersWharfWalkabilityTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;
        private GameObject _pierGo;
        private StandablePlatform _pier;
        private GameConfig _config;

        const float SpringHighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   //  2.2
        const float SpringLowWater  = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;   // -2.2

        // The owner's wade tunables, read off a fresh GameConfig (its own field defaults) rather than
        // restated here — re-tuning WadeDepth/SwimLimit re-times these tests instead of stranding them
        // on numbers the game no longer uses (rule 6).
        private float WadeDepth => _config.WadeDepth;
        private float SwimLimit => _config.SwimLimit;

        /// <summary>A still environment at a chosen water level — the tide, held where a test wants it.</summary>
        private sealed class FixedTide : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        [SetUp]
        public void SetUp()
        {
            StandableSurfaces.Clear();
            _config = ScriptableObject.CreateInstance<GameConfig>();

            _go = new GameObject("StPetersTerrain_WharfWalkTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);

            // Exactly what StPetersWharf.Place() registers — the deck footprint derived from the pier's own
            // cell geometry and the deck height MEASURED off the terrain at the pier root.
            _pierGo = new GameObject("StPetersWharf_Test");
            _pier = _pierGo.AddComponent<StandablePlatform>();
            _pier.Configure(StPetersWharf.SurfaceId,
                            StPetersWharf.DeckFootprint(),
                            StPetersWharf.DeckElevationFrom(_terrain));
        }

        [TearDown]
        public void TearDown()
        {
            StandableSurfaces.Clear();
            if (_pierGo != null) Object.DestroyImmediate(_pierGo);
            if (_go != null) Object.DestroyImmediate(_go);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private IStandableSurface[] Deck => new IStandableSurface[] { _pier };

        // =================================================================================
        //  THE DEFECT, MEASURED
        // =================================================================================

        [Test]
        public void Before_TheSimSawOpenWaterOverThePierForMostOfEveryTide()
        {
            Vector2 disembark = StPetersBuilder.DisembarkPos;
            var highTide = new FixedTide { Level = SpringHighWater };

            // The pre-seam read: no standable structures anywhere, so the answer is the dredged slip bed.
            float depth = TidalWalkability.DepthAt(_terrain, highTide, 0.0, disembark);
            Assert.Greater(depth, SwimLimit,
                $"the ratified disembark point read {depth:0.00} m of water at spring high — deeper than the " +
                $"{SwimLimit:0.#} m boat-only limit, i.e. the sim thought the pier was open sea. If this ever " +
                "goes green the defect is gone and this sabotage no longer means anything.");
            Assert.AreEqual(DepthBand.Deep,
                TidalWalkability.BandAt(_terrain, highTide, 0.0, disembark, WadeDepth, SwimLimit),
                "and it classified as the BOAT-ONLY band — on the planks");

            // It was not merely a high-water problem: the water only leaves the planks near spring low.
            float bed = _terrain.ElevationAt(disembark);
            Assert.Less(bed, 0f,
                $"the ground under the disembark point is the dredged slip at {bed:0.00} m — below datum, so " +
                "it is wet through most of the tide by design (boats need the depth; the fix is not to fill it in)");
            Assert.Greater(bed, SpringLowWater,
                "…and only bares near spring low water, which is the window the player used to have");
        }

        // =================================================================================
        //  THE DECK IS FLOOR
        // =================================================================================

        [Test]
        public void TheWholeDeckIsWalkableAtEveryTide_IncludingSpringHighWater()
        {
            // Every deck cell, at the highest and the lowest water the region ever reaches.
            foreach (var cell in StPetersWharf.DeckCellsBackToFront())
            {
                var p = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                foreach (float level in new[] { SpringHighWater, 0f, SpringLowWater })
                {
                    var tide = new FixedTide { Level = level };
                    Assert.IsTrue(TidalWalkability.IsWalkable(_terrain, tide, Deck, 0.0, p),
                        $"deck cell {cell} is not walkable at water level {level:0.#} m — a wharf you can only " +
                        "use at some tides is not a wharf");
                    Assert.AreEqual(DepthBand.Dry,
                        TidalWalkability.BandAt(_terrain, tide, Deck, 0.0, p, WadeDepth, SwimLimit),
                        $"deck cell {cell} reads as water at level {level:0.#} m — the fisher would crawl at the " +
                        "wade/swim factor and the submerge shader would paint her neck-deep on dry planks");
                }
            }
        }

        [Test]
        public void TheRatifiedDisembarkPoint_LandsOnTheDeck_AndIsDryTheMomentYouStepOff()
        {
            Vector2 disembark = StPetersBuilder.DisembarkPos;
            Assert.IsTrue(StandablePlatform.Contains(StPetersWharf.DeckFootprint(), disembark),
                $"the ratified disembark {disembark} must fall inside the deck footprint " +
                $"{StPetersWharf.DeckFootprint()} — this is the whole reason the pier is this long");

            var highTide = new FixedTide { Level = SpringHighWater };
            Assert.IsTrue(TidalWalkability.IsWalkable(_terrain, highTide, Deck, 0.0, disembark),
                "stepping off the boat at spring high water lands you on dry planks");
            Assert.AreEqual(DepthBand.Dry,
                TidalWalkability.BandAt(_terrain, highTide, Deck, 0.0, disembark, WadeDepth, SwimLimit),
                "at full walking speed, not swimming");
        }

        [Test]
        public void OneMetreOffTheDeckEdge_TheSlipIsStillTheSlip()
        {
            var highTide = new FixedTide { Level = SpringHighWater };
            Rect deck = StPetersWharf.DeckFootprint();

            // A metre beyond each of the three water-facing lips (the west lip meets the beach, so it is
            // dry ground rather than water and is not part of this check).
            //
            // ⚠ THE THREE LIPS ARE NOT OVER THE SAME GROUND, and this test used to claim they were. Only
            // the EAST lip, off the pier head, stands over the dredged slip: measured bed −1.05 m. The
            // north and south lips are over the shoal beside it, at +1.09 m — a metre ABOVE datum. They
            // read as Deep under the old ±3.5 m tide only because it put 2.4 m of water over them, not
            // because anything was dredged there; the 2026-08-01 amplitude ruling leaves 1.1 m and they
            // read as Swim. So the shared invariant is the one that was always true and always mattered —
            // YOU CANNOT WALK OFF THE PLANKS — and the boat-only claim is asserted where it is a fact.
            var offEdges = new[]
            {
                new Vector2(deck.xMax + 1f, 0f),                       // east, off the pier head
                new Vector2(deck.center.x, deck.yMax + 1f),            // north
                new Vector2(deck.center.x, deck.yMin - 1f),            // south, the mooring side
            };

            foreach (var p in offEdges)
            {
                Assert.IsFalse(TidalWalkability.IsWalkable(_terrain, highTide, Deck, 0.0, p),
                    $"{p} is one metre off the planks and must read as water — a deck that leaked its dryness " +
                    "into the slip beside it would let the player walk out over the berth");
                Assert.AreNotEqual(DepthBand.Dry,
                    TidalWalkability.BandAt(_terrain, highTide, Deck, 0.0, p, WadeDepth, SwimLimit),
                    $"{p} is a metre off the deck and must be water at high tide, not ground");
            }

            // The pier HEAD is the one that is genuinely over the dredged slip, and it is the one the
            // sail-home beat needs to stay boat-only: this is where you bring her alongside.
            var offHead = new Vector2(deck.xMax + 1f, 0f);
            Assert.AreEqual(DepthBand.Deep,
                TidalWalkability.BandAt(_terrain, highTide, Deck, 0.0, offHead, WadeDepth, SwimLimit),
                $"{offHead} is over the dredged slip at high water — boat-only, so the soft wall stops " +
                "the player at the pier head rather than letting them wade out to the mooring");
        }

        // =================================================================================
        //  THE DECK HEIGHT IS MEASURED, AND IT HAS TO CLEAR THE TIDE
        // =================================================================================

        [Test]
        public void TheDeckHeightIsTheGroundAtThePierRoot_SoThePlanksMeetTheShoreLevel()
        {
            float deck = StPetersWharf.DeckElevationFrom(_terrain);
            float root = _terrain.ElevationAt(new Vector2(StPetersWharf.RootCellX + 0.5f, 0f));
            Assert.AreEqual(root, deck, 1e-4f,
                "the deck is level with the last dry ground the pier launches from — measured off the " +
                "terrain, so a terrain edit moves the planks with it instead of leaving them floating");
        }

        [Test]
        public void TheDeckStandsClearOfTheHighestWaterTheRegionEverReaches()
        {
            // ⚠ THE GUARD-RAIL. Everything above depends on this one number being above spring high water.
            // Author the deck too low and the pier silently floods again with nothing else failing — which
            // is precisely how the pier root's own +3.15 m mistake happened (see StPetersWharf's note).
            float deck = StPetersWharf.DeckElevationFrom(_terrain);
            Assert.Greater(deck, SpringHighWater,
                $"the wharf deck is at {deck:0.00} m and spring high water is {SpringHighWater:0.00} m — the " +
                "planks must stand clear of the highest water the region reaches, or the dock floods");
            Assert.Greater(deck - SpringHighWater, 1f,
                $"only {deck - SpringHighWater:0.00} m of freeboard at spring high — too thin a margin for a " +
                "working wharf, and one terrain nudge from being wet");
        }

        // =================================================================================
        //  WHAT MUST NOT CHANGE
        // =================================================================================

        [Test]
        public void TheSeabedUnderThePierIsUntouched_SoABoatStillFloatsInTheSlipItWasDredgedFor()
        {
            // The rejected alternative was carving a dry apron into the terrain — which would have falsified
            // the painted seabed and taken the depth the boat needs with it. The deck is an ON-FOOT read
            // only: the boat-cross/grounding depth must be bit-identical over the planks.
            var highTide = new FixedTide { Level = SpringHighWater };
            foreach (var cell in StPetersWharf.DeckCellsBackToFront())
            {
                var p = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                float boatDepth = BoatCrossing.DepthAt(_terrain, highTide, 0.0, p);
                float bare = TidalExposure.WaterDepth(highTide.Level, _terrain.ElevationAt(p));
                Assert.AreEqual(bare, boatDepth,
                    $"the boat-side depth at {p} moved — a pier over the water must not shoal it");
            }

            // And the berth still floats the skiff tier it was dredged for at high water.
            float atMooring = BoatCrossing.DepthAt(_terrain, highTide, 0.0, StPetersBuilder.DoryMooredPos);
            Assert.Greater(atMooring, 0.6f,
                $"the mooring reads {atMooring:0.00} m at high water — the slip must still float a hull");
        }

        [Test]
        public void TheTideGateAwayFromThePier_IsBitIdenticalWithTheDeckRegistered()
        {
            // The sandbar crossing is the region's defining mechanic and it is 400 m from the pier. Sweep
            // it (and the whole east-west centre-line) at three tide states and demand bit equality against
            // the pure terrain-vs-water answer — the seam must be invisible everywhere it is not standing.
            foreach (float level in new[] { SpringHighWater, 0f, SpringLowWater })
            {
                var tide = new FixedTide { Level = level };
                for (float x = -360f; x <= 360f; x += 1f)
                {
                    var p = new Vector2(x, 0f);
                    if (StandablePlatform.Contains(StPetersWharf.DeckFootprint(), p)) continue;   // on the planks

                    float legacy = TidalExposure.WaterDepth(level, _terrain.ElevationAt(p));
                    Assert.AreEqual(legacy, TidalWalkability.DepthAt(_terrain, tide, Deck, 0.0, p),
                        $"depth at ({x}, 0) at water level {level:0.#} changed with the wharf registered");
                }
            }
        }

        [Test]
        public void TheSandbarCrossingStillOpensAndClosesWithTheTide()
        {
            // The one behaviour a walkability change could plausibly break. The bar's crest is authored just
            // below neap high water, so the gate must exist at every point in the month.
            var barCrest = Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, 0.2f);
            float crest = _terrain.ElevationAt(barCrest);

            Assert.IsFalse(TidalWalkability.IsWalkable(_terrain, new FixedTide { Level = crest + 0.5f },
                                                       Deck, 0.0, barCrest),
                "the bar must still flood as the tide rises over its crest");
            Assert.IsTrue(TidalWalkability.IsWalkable(_terrain, new FixedTide { Level = crest - 0.5f },
                                                      Deck, 0.0, barCrest),
                "…and still bare as it falls — the region's defining mechanic is untouched");
        }

        // =================================================================================
        //  THE FOOTPRINT IS DERIVED, NOT A SECOND COPY OF THE GEOMETRY
        // =================================================================================

        [Test]
        public void TheDeckFootprintCoversExactlyThePlacedDeckCells()
        {
            Rect deck = StPetersWharf.DeckFootprint();
            var cells = StPetersWharf.DeckCellsBackToFront().ToList();

            foreach (var cell in cells)
            {
                // A cell occupies [x, x+1) × [y, y+1); every corner and the centre must be on the deck.
                foreach (var probe in new[]
                {
                    new Vector2(cell.x,        cell.y),
                    new Vector2(cell.x + 1f,   cell.y),
                    new Vector2(cell.x,        cell.y + 1f),
                    new Vector2(cell.x + 1f,   cell.y + 1f),
                    new Vector2(cell.x + 0.5f, cell.y + 0.5f),
                })
                    Assert.IsTrue(StandablePlatform.Contains(deck, probe),
                        $"{probe} belongs to placed deck cell {cell} but is outside the registered footprint " +
                        $"{deck} — the sprites and the floor have drifted apart");
            }

            // …and nothing beyond them. The footprint must be the deck, not a generous box around it.
            Assert.AreEqual(StPetersWharf.RootCellX, deck.xMin, 1e-4f, "the footprint starts at the pier root");
            Assert.AreEqual(StPetersWharf.HeadCellX + 1, deck.xMax, 1e-4f, "…and ends at the far edge of the head cell");
            Assert.AreEqual(StPetersWharf.MinCellY, deck.yMin, 1e-4f, "the footprint's south lip is the deck's");
            Assert.AreEqual(StPetersWharf.MaxCellY + 1, deck.yMax, 1e-4f, "…and its north lip too");
            Assert.AreEqual(cells.Count, Mathf.RoundToInt(deck.width * deck.height),
                "one square metre of footprint per placed deck tile — the rect is DERIVED from the cell " +
                "geometry, so it cannot drift from the sprites the way a second copy of the numbers would");
        }
    }
}
