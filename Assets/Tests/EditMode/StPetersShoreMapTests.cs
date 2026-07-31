using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE PAINTED COAST OF ST PETERS.</b> <see cref="StPetersShoreMap"/> turns the region's authored
    /// <see cref="TidalTerrain"/> into the shoreline-ISO v8 vocabulary, and because it is pure these tests
    /// assert the coast the scene is painted from without loading a scene — the same arrangement
    /// <c>StPetersBuilder.ScatterClamHoles</c> has.
    ///
    /// <para>What is worth pinning, and why each one is here:</para>
    /// <list type="bullet">
    /// <item>The <b>defect this closes, measured</b>: the island's ground used to be two tiled patches
    /// authored for a 44 m greybox disc and never moved when the island became 450 × 260 m, so over 99% of
    /// the landmass had no ground at all.</item>
    /// <item>The <b>vocabulary</b> is the kit's, not a parallel copy — a re-bake that renames or adds a
    /// ground row must fail here rather than throw at paint time.</item>
    /// <item>The <b>bands are tide states</b>, stated against St Peters' own spring and neap water, so
    /// re-tuning the tide re-checks the coast.</item>
    /// <item>The <b>sector split is the docs'</b> (scene-sizing §5.1: cliffs/rock south-east, beaches and
    /// flats west-north) rather than a uniform ring.</item>
    /// <item>The <b>ground column is a world position, not a hash</b> — the four columns are adjacent tiles
    /// of a seamless noise field, and hashing them would put a seam on every cell boundary. Sabotage-measured.</item>
    /// <item>The <b>rock never lands on a crossing</b>: not in the one door through the reef, not on the
    /// walking bar. St Peters authors no new hazard (§6.0 hazards: "no rocks").</item>
    /// <item>The <b>footprint has a budget</b>, because six figures of tiles is what a 450 m island costs at
    /// 1 m and it must not quietly grow past what a scene can hold (rule 7).</item>
    /// </list>
    /// Nothing here touches the sim or the save: the coast is authored geometry, recomputed not persisted
    /// (CLAUDE.md rule 5).
    /// </summary>
    public class StPetersShoreMapTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;

        // St Peters' tide, from the builder: mean 0, amplitude 3.5 m, neap fraction 0.45 (GameConfig).
        const float SpringHighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   //  3.5
        const float SpringLowWater  = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;   // -3.5
        const float NeapHighWater   = 0.45f * StPetersBuilder.TideAmplitude;                      //  1.575

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_ShoreMapTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);   // the SAME call the scene is built from
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // =================================================================================
        //  THE DEFECT THIS CLOSES
        // =================================================================================

        [Test]
        public void TheWholeLandmassHasGround_NotJustTheOldGreyboxPatch()
        {
            // Walk the island's land (inside the plateau edge) on a 5 m grid. Every sample must classify to
            // a real material: the plateau sits at +6 m, far above the paint floor, so "unpainted land" can
            // only mean the classifier has a hole in it.
            var bare = new List<Vector2>();
            int sampled = 0;
            for (float x = -160f; x <= 300f; x += 5f)
            for (float y = -135f; y <= 135f; y += 5f)
            {
                var p = new Vector2(x, y);
                float d = TidalTerrain.IslandDistance(p, StPetersBuilder.IslandCenter,
                                                      StPetersBuilder.IslandRadius,
                                                      StPetersBuilder.IslandRadiusY);
                if (d > StPetersBuilder.IslandRadius) continue;   // not land
                sampled++;
                if (StPetersShoreMap.MaterialAt(_terrain, p) == ShoreMaterial.None) bare.Add(p);
            }

            Assert.Greater(sampled, 800,
                "sanity: the 240 x 140 m island (re-ruled 2026-07-30) should give ~1,000 land samples");
            Assert.IsEmpty(bare,
                $"{bare.Count} of {sampled} land samples came back unpainted (e.g. {(bare.Count > 0 ? bare[0].ToString() : "-")}). " +
                "Every square metre of dry island must carry a ground material — the water shader clips " +
                "itself transparent over dry land, so an unpainted cell renders as the camera background.");
        }

        [Test]
        public void Sabotage_TheOldGreyboxGroundPatches_CoveredUnderOnePercentOfTheIsland()
        {
            // What the builder used to place: a 26 x 26 m tiled sand patch with a 20 x 20 m grass patch on
            // top, both centred at (-40, 0) — authored when the whole island WAS a 44 m disc at that centre
            // (StPetersBuilder, pre-#328). The rescale moved the island to (70, 0) with semi-axes 225 x 130
            // and left the patches where they were. This measures how little of THAT island they actually
            // covered, so the fix is shown landing on a real hole and not on a tidy-up.
            //
            // ⚠ HISTORICAL semi-axes on purpose (225 x 130, the island as it was when the defect lived),
            // not the current constants: the 2026-07-30 re-ruling shrank the island to 120 x 70, and this
            // sabotage documents a defect of the ERA it happened in. Re-deriving it from today's smaller
            // island would inflate the covered fraction past 1% and quietly turn a fixed measurement into
            // a moving one.
            const float oldPatchSide = 26f;                       // the larger of the two
            float patchArea = oldPatchSide * oldPatchSide;        // 676 m²
            const float eraRadiusX = 225f, eraRadiusY = 130f;     // the pre-2026-07-30 island
            float landArea = Mathf.PI * eraRadiusX * eraRadiusY;

            float covered = patchArea / landArea;
            Assert.Less(covered, 0.01f,
                $"the retired greybox patches covered {covered:P2} of that island's {landArea:N0} m² of land " +
                "— under 1%, which is the defect the painted coast closed. If this ever rises, the patches " +
                "were resized rather than retired and the comparison is no longer the one being made.");

            // And the patch was not even ON the part of the island the player started from: the spawn of
            // that era sat at x = -100, sixty metres west of the patch's western edge. (Historical numbers
            // again — today's spawn moved east with the 2026-07-30 shrink.)
            const float eraSpawnX = -100f;
            float patchWestEdge = -40f - oldPatchSide * 0.5f;      // -53
            Assert.Less(eraSpawnX, patchWestEdge,
                "the player spawned WEST of the old ground patch entirely — they never stood on it");
        }

        // =================================================================================
        //  THE VOCABULARY IS THE KIT'S
        // =================================================================================

        [Test]
        public void EveryMaterialIsAKitGroundRow_AndTheKitHasNoRowWeCannotName()
        {
            var mine = System.Enum.GetValues(typeof(ShoreMaterial))
                                  .Cast<ShoreMaterial>()
                                  .Where(m => m != ShoreMaterial.None)
                                  .Select(StPetersShoreMap.MaterialName)
                                  .ToArray();

            CollectionAssert.AreEquivalent(ShorelineIso2Catalog.GroundMaterials, mine,
                "the classifier's materials and the kit's Ground.png rows must be the SAME SET. A re-bake " +
                "that renames a row (or adds one) has to fail here, where the message says so, rather than " +
                "throw out of GroundIndex halfway through painting a coast.");
        }

        [Test]
        public void TheFringeMaterialsAreExactlyTheKitsThreeSoftRows()
        {
            var mine = System.Enum.GetValues(typeof(ShoreMaterial))
                                  .Cast<ShoreMaterial>()
                                  .Where(StPetersShoreMap.HasFringe)
                                  .Select(StPetersShoreMap.MaterialName)
                                  .ToArray();

            CollectionAssert.AreEquivalent(ShorelineIso2Catalog.FringeMaterials, mine,
                "only the kit's three SOFT materials bake a fringe row — asking for a shingle or shelf " +
                "fringe throws, so HasFringe must agree with the sheet exactly.");
        }

        [Test]
        public void TheGroundVariantCountIsTheKits()
        {
            Assert.AreEqual(ShorelineIso2Catalog.GroundVariants, StPetersShoreMap.ShorelineIso2Variants,
                "v7 shipped three ground columns and v8 ships four; the classifier's modulus must be the " +
                "kit's or the world field wraps at the wrong period.");
        }

        // =================================================================================
        //  THE BANDS ARE TIDE STATES
        // =================================================================================

        [Test]
        public void TheBandsAreOrderedAndEachOneIsATideState()
        {
            // Ordering: a strictly descending ladder, or two bands overlap and the higher one wins silently.
            Assert.Greater(StPetersShoreMap.GrassFloorElevation, StPetersShoreMap.MarramFloorElevation);
            Assert.Greater(StPetersShoreMap.MarramFloorElevation, StPetersShoreMap.SandFloorElevation);
            Assert.Greater(StPetersShoreMap.SandFloorElevation, StPetersShoreMap.RippleFloorElevation);
            Assert.Greater(StPetersShoreMap.RippleFloorElevation, StPetersShoreMap.PaintFloorElevation);

            // Grass is ground the sea never reaches, even at the top of the biggest spring tide.
            Assert.Greater(StPetersShoreMap.GrassFloorElevation, SpringHighWater,
                "the meadow must sit above spring high water — grass does not grow where the sea washes");

            // Marram is the band that is dry all neap week and washed on the springs. That is where marram
            // actually grows, and it is why the number is 1.6 and not a taste.
            Assert.Greater(StPetersShoreMap.MarramFloorElevation, NeapHighWater,
                "the dune band must clear NEAP high water (1.575 m) or it is just more beach");
            Assert.Less(StPetersShoreMap.MarramFloorElevation, SpringHighWater,
                "the dune band must be reached by SPRING high water or it is just more meadow");

            // The paint floor stops above the flat deep-harbour floor, so the open sea is never painted —
            // by construction, not by a special case in the painter.
            Assert.Greater(StPetersShoreMap.PaintFloorElevation, StPetersBuilder.DeepHarbourElevation,
                "the -4.0 m harbour floor must fall below the paint floor so deep water stays unpainted");
            Assert.Greater(StPetersShoreMap.PaintFloorElevation, SpringLowWater,
                "the paint floor sits above spring low water: painted ground is ground that either bares " +
                "or is shallow enough to read through the column");
        }

        [Test]
        public void TheIslandsHighGroundIsMeadow_AndTheHarbourFloorIsNeverPainted()
        {
            Assert.AreEqual(ShoreMaterial.Grass,
                StPetersShoreMap.MaterialAt(_terrain, StPetersBuilder.IslandCenter),
                "the plateau sits at +6 m — dry at every tide, so it is the reverting meadow (§5.1)");

            // Far out in the open harbour, west of the bar's far tip and south of everything.
            Assert.AreEqual(ShoreMaterial.None,
                StPetersShoreMap.MaterialAt(_terrain, new Vector2(0f, -240f)),
                "the deep harbour floor must stay unpainted — a tile there is a tile nobody ever sees");
        }

        // =================================================================================
        //  THE TWO COASTS
        // =================================================================================

        [Test]
        public void TheWeatherCoastIsSouthAndEast_TheShelteredCoastWestAndNorth()
        {
            var c = StPetersBuilder.IslandCenter;

            Assert.IsTrue(StPetersShoreMap.IsWeatherCoast(c + new Vector2(0f, -200f)), "due south is weather");
            Assert.IsTrue(StPetersShoreMap.IsWeatherCoast(c + new Vector2(300f, 0f)),  "due east is weather");
            Assert.IsFalse(StPetersShoreMap.IsWeatherCoast(c + new Vector2(0f, 200f)), "due north is sheltered");
            Assert.IsFalse(StPetersShoreMap.IsWeatherCoast(c + new Vector2(-300f, 0f)), "due west is sheltered");

            // The two things the geography turns on: the crossing leaves the SHELTERED west (that is where
            // the beaches and clam flats are, §5.1), and the one dock is cut into the WEATHER east.
            Assert.IsFalse(StPetersShoreMap.IsWeatherCoast(StPetersBuilder.SandbarTo),
                "the bar leaves the island's west end — the sheltered coast, where the flats are");
            Assert.IsTrue(StPetersShoreMap.IsWeatherCoast(StPetersBuilder.BerthFrom),
                "the berth is on the east end (§5.1a) — a slip dredged into the weather coast");
        }

        [Test]
        public void TheWeatherCoastSheddsItsSandAndTheShelteredCoastKeepsIt()
        {
            // Same elevations, opposite coasts, different materials — the distinction §5.3 asks to be drawn
            // in the land: cobble and rock platform where the sea pounds, dune and beach where it does not.
            for (float e = StPetersShoreMap.PaintFloorElevation; e < StPetersShoreMap.GrassFloorElevation; e += 0.2f)
            {
                Assert.AreNotEqual(ShoreMaterial.Marram, StPetersShoreMap.WeatherBand(e),
                    $"no dune on the weather coast (elevation {e:0.0})");
                Assert.AreNotEqual(ShoreMaterial.Sand, StPetersShoreMap.WeatherBand(e),
                    $"no sand beach on the weather coast (elevation {e:0.0})");
            }

            Assert.AreEqual(ShoreMaterial.Marram, StPetersShoreMap.ShelteredBand(2f),
                "above neap high water on the sheltered coast: the marram dune band");
            Assert.AreEqual(ShoreMaterial.Sand, StPetersShoreMap.ShelteredBand(0.5f),
                "the upper intertidal on the sheltered coast: beach sand");
            // -1.0 m is the reef shelf's INNER elevation, so this pair is the reef apron on each coast:
            // the sheltered side silts up into rippled sand, the weather side is scoured to bare platform.
            // That difference is the whole point of splitting the perimeter.
            Assert.AreEqual(ShoreMaterial.Ripple, StPetersShoreMap.ShelteredBand(-1f),
                "the sheltered apron at -1.0 m: rippled sand — the dig ground");
            Assert.AreEqual(ShoreMaterial.Shelf, StPetersShoreMap.WeatherBand(-1f),
                "the SAME -1.0 m on the weather coast is bare rock platform, not sand: below the low-water " +
                "mark the exposed coast keeps no sediment at all");
            // And the weather coast's cobble is the band ABOVE that mark — the storm beach, which runs from
            // the low-water line right up over ordinary high water.
            Assert.AreEqual(ShoreMaterial.Shingle, StPetersShoreMap.WeatherBand(0f),
                "at chart datum the weather coast is its cobble storm beach");
            Assert.AreEqual(ShoreMaterial.Shingle, StPetersShoreMap.WeatherBand(3f),
                "and it is still cobble just under spring high water — no dune, no sand");
        }

        // =================================================================================
        //  THE BAR IS A COBBLE-AND-SAND BAR
        // =================================================================================

        [Test]
        public void TheBarIsACobbleSpineWithSandFlatsEitherSide()
        {
            // Straight down the crest, clear of the channel cut.
            Vector2 alongBar = Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, 0.2f);
            Assert.AreEqual(ShoreMaterial.Shingle, StPetersShoreMap.MaterialAt(_terrain, alongBar),
                "the world docs call it 'a cobble-and-sand bar' (§6.0): the walking line down the crest is " +
                "shingle, so the crossing reads as a path you can see rather than an undifferentiated flat");

            // Step off the spine onto the flanks — the digging ground.
            var flank = alongBar + new Vector2(0f, StPetersShoreMap.BarSpineHalfWidth + 4f);
            var onFlank = StPetersShoreMap.MaterialAt(_terrain, flank);
            Assert.That(onFlank, Is.EqualTo(ShoreMaterial.Sand).Or.EqualTo(ShoreMaterial.Ripple),
                $"the bar's flanks are the sand/ripple flats the clams live in, got {onFlank}");

            // The bar keeps its own vocabulary even though it leaves the island's west (sheltered) end —
            // if the sector table won, the spine would just be more beach.
            Assert.IsFalse(StPetersShoreMap.IsWeatherCoast(alongBar));
            Assert.AreNotEqual(StPetersShoreMap.ShelteredBand(_terrain.ElevationAt(alongBar)),
                               StPetersShoreMap.MaterialAt(_terrain, alongBar),
                "the bar overrides the sheltered band on its spine — that override IS the path");
        }

        [Test]
        public void TheChannelCutReadsAsAGutAcrossTheBar()
        {
            Vector2 crossing = Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo,
                                            StPetersBuilder.ChannelAlong);
            var inChannel = StPetersShoreMap.MaterialAt(_terrain, crossing);
            Assert.AreNotEqual(ShoreMaterial.Shingle, inChannel,
                "the channel bed is carved to -0.6 m, below the cobble spine's floor — the boat passage must " +
                "not paint as part of the walking path, or the one place a boat can cross looks walkable");
            Assert.That(inChannel, Is.EqualTo(ShoreMaterial.Ripple).Or.EqualTo(ShoreMaterial.Shelf),
                $"the gut is a low-water/subtidal material, got {inChannel}");
        }

        // =================================================================================
        //  THE GROUND COLUMN IS A WORLD POSITION, NOT A HASH
        // =================================================================================

        [Test]
        public void GroundVariant_WalksTheKitsWorldFieldContinuously()
        {
            int n = StPetersShoreMap.ShorelineIso2Variants;
            for (int x = -400; x <= 400; x++)
            {
                int v = StPetersShoreMap.GroundVariant(x);
                Assert.GreaterOrEqual(v, 0, $"variant at x={x} must be a real column");
                Assert.Less(v, n, $"variant at x={x} must be a real column");
                Assert.AreEqual((StPetersShoreMap.GroundVariant(x) + 1) % n,
                                StPetersShoreMap.GroundVariant(x + 1),
                    $"the columns are 'gx 0..3 (adjacent world tiles)' per the kit contract, so stepping one " +
                    $"metre east must step one column east — broke at x={x}");
            }
        }

        [Test]
        public void Sabotage_AHashedGroundVariant_WouldSeamAlmostEveryCellBoundary()
        {
            // The obvious wrong choice, and the reason the doc comment on GroundVariant is so insistent:
            // pick the column by hashing the cell instead of by its world X. The tiles still "look varied",
            // and every single boundary is a seam, because the four columns are four samples of ONE
            // continuous field rather than four interchangeable variants.
            int n = StPetersShoreMap.ShorelineIso2Variants;
            int Hashed(int x) => Mathf.Min(n - 1, (int)(StPetersShoreMap.Hash01(x, 0, 1) * n));

            int seams = 0, steps = 0;
            for (int x = -400; x < 400; x++, steps++)
                if (Hashed(x + 1) != (Hashed(x) + 1) % n) seams++;

            Assert.Greater(seams, steps * 3 / 4,
                $"a hashed column seams {seams}/{steps} boundaries — the sabotage must fail loudly, which is " +
                "what makes the world-X keying a decision and not an accident");
            Assert.AreEqual(0, Enumerable.Range(-400, 800)
                                         .Count(x => StPetersShoreMap.GroundVariant(x + 1)
                                                     != (StPetersShoreMap.GroundVariant(x) + 1) % n),
                "and the shipped keying seams none of them");
        }

        // =================================================================================
        //  THE FRINGE AUTOTILER
        // =================================================================================

        [Test]
        public void FringePiece_IsTotal_AndOnlyEverNamesAPieceTheKitBakes()
        {
            var known = new HashSet<string>(ShorelineIso2Catalog.FringePieces);

            for (int mask = 0; mask < 256; mask++)
            {
                bool B(int bit) => (mask & (1 << bit)) != 0;
                string piece = StPetersShoreMap.FringePiece(
                    n: B(0), e: B(1), s: B(2), w: B(3), ne: B(4), se: B(5), sw: B(6), nw: B(7));

                if (mask == 0)
                {
                    Assert.IsNull(piece, "nothing laps onto a cell with no lapping neighbours");
                    continue;
                }
                Assert.IsNotNull(piece, $"mask {mask} has a lapping neighbour but got no fringe piece — a " +
                                        "hole in the autotiler is a hard seam in the finished coast");
                Assert.IsTrue(known.Contains(piece),
                    $"mask {mask} asked for '{piece}', which is not one of the kit's 12 fringe columns");
            }
        }

        [Test]
        public void FringePiece_PrefersTheCornerThatCoversMore_AndReadsTheDiagonalAsAnOuterCorner()
        {
            Assert.AreEqual("inNE", StPetersShoreMap.FringePiece(true, true, false, false,
                                                                false, false, false, false),
                "two adjacent cardinals means the higher material wraps an INSIDE corner — the piece that " +
                "covers both edges, not one of them");
            Assert.AreEqual("edN", StPetersShoreMap.FringePiece(true, false, false, false,
                                                               false, false, false, false),
                "one cardinal is a plain edge tongue");
            Assert.AreEqual("coNE", StPetersShoreMap.FringePiece(false, false, false, false,
                                                                true, false, false, false),
                "only the diagonal touches, so the higher material rounds an OUTSIDE corner");
            Assert.IsNotNull(StPetersShoreMap.FringePiece(true, false, true, false,
                                                         false, false, false, false),
                "opposite cardinals with no adjacent pair (a one-metre gully) still resolves — " +
                "deterministically, and it is a metre wide");
        }

        [Test]
        public void TheLappingOrderIsPhysical_NotTheSheetsRowOrder()
        {
            // The sheet lists ripple BEFORE shingle. Physically a cobble storm beach sits above the rippled
            // flats it fronts, so the rank table deliberately departs from the row order — pinned here so a
            // later "tidy-up" that re-derives Rank from the sheet is caught.
            Assert.Greater(StPetersShoreMap.Rank(ShoreMaterial.Shingle),
                           StPetersShoreMap.Rank(ShoreMaterial.Ripple),
                "cobble laps onto ripple, not the other way round");

            int rippleRow = System.Array.IndexOf(ShorelineIso2Catalog.GroundMaterials, "ripple");
            int shingleRow = System.Array.IndexOf(ShorelineIso2Catalog.GroundMaterials, "shingle");
            Assert.Less(rippleRow, shingleRow,
                "and the sheet really does list them the other way round — which is why Rank is its own table");

            // Ordering top to bottom, so the highest material always wins the lap.
            Assert.Greater(StPetersShoreMap.Rank(ShoreMaterial.Grass),  StPetersShoreMap.Rank(ShoreMaterial.Marram));
            Assert.Greater(StPetersShoreMap.Rank(ShoreMaterial.Marram), StPetersShoreMap.Rank(ShoreMaterial.Sand));
            Assert.Greater(StPetersShoreMap.Rank(ShoreMaterial.Sand),   StPetersShoreMap.Rank(ShoreMaterial.Shingle));
            Assert.Greater(StPetersShoreMap.Rank(ShoreMaterial.Ripple), StPetersShoreMap.Rank(ShoreMaterial.Shelf));
            Assert.Greater(StPetersShoreMap.Rank(ShoreMaterial.Shelf),  StPetersShoreMap.Rank(ShoreMaterial.None),
                "None must rank below every real material or an unpainted neighbour would lap onto ground");
        }

        // =================================================================================
        //  THE LOOK-ONLY WIGGLE (the bullseye fix)
        // =================================================================================

        [Test]
        public void TheWiggleIsCoherent_NotSpeckle_AndStaysInRange()
        {
            // Rendering the first version of this classifier produced a TARGET, not an island: the terrain
            // is an analytic function of elliptical distance, so every band boundary came out a perfect
            // concentric ellipse. The wiggle breaks that — but only if it MEANDERS. Per-cell white noise
            // would dither each boundary into salt-and-pepper, which is worse than the rings.
            int incoherent = 0, samples = 0;
            for (float x = -200f; x < 200f; x += 1f)
            for (float y = -100f; y < 100f; y += 7f)
            {
                float a = StPetersShoreMap.Wiggle(new Vector2(x, y));
                float b = StPetersShoreMap.Wiggle(new Vector2(x + 1f, y));
                Assert.GreaterOrEqual(a, -1.0001f, "the wiggle is a [-1,1] field");
                Assert.LessOrEqual(a, 1.0001f, "the wiggle is a [-1,1] field");
                samples++;
                if (Mathf.Abs(a - b) > 0.5f) incoherent++;   // a metre apart should barely differ
            }

            Assert.Greater(samples, 5000, "sanity: enough samples to mean something");
            Assert.Less(incoherent, samples / 100,
                $"{incoherent}/{samples} one-metre steps jumped more than half the field's range — the " +
                "noise is speckling rather than meandering, so every band edge would come out dithered");
        }

        [Test]
        public void Sabotage_PerCellWhiteNoise_WouldDitherEveryBandEdge()
        {
            // The obvious wrong implementation, measured: hash the cell directly instead of interpolating
            // between lattice corners. This is what the coherence assertion above is protecting against.
            int incoherent = 0, samples = 0;
            for (float x = -200f; x < 200f; x += 1f)
            for (float y = -100f; y < 100f; y += 7f)
            {
                float a = StPetersShoreMap.Hash01((int)x, (int)y, 3) * 2f - 1f;
                float b = StPetersShoreMap.Hash01((int)x + 1, (int)y, 3) * 2f - 1f;
                samples++;
                if (Mathf.Abs(a - b) > 0.5f) incoherent++;
            }
            Assert.Greater(incoherent, samples / 3,
                $"white noise jumped on only {incoherent}/{samples} steps — if the sabotage does not " +
                "misbehave, the coherence test above is not measuring anything");
        }

        [Test]
        public void TheWiggleIsLookOnly_ItNeverMovesTheGroundOrPunchesAHoleInIt()
        {
            // The load-bearing property. The wiggle offsets which MATERIAL is drawn; it must never change
            // the terrain (the water level, the walk gate, the clam field and the reef's depth gate all
            // read TidalTerrain directly), and it must never turn dry land into an unpainted cell.
            var probe = new GameObject("StPetersTerrain_WiggleProbe");
            try
            {
                var second = probe.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(second);

                for (float x = -370f; x < 370f; x += 11f)
                for (float y = -250f; y < 250f; y += 11f)
                {
                    var p = new Vector2(x, y);
                    Assert.AreEqual(_terrain.ElevationAt(p), second.ElevationAt(p), 1e-6f,
                        $"the terrain at {p} must be untouched by anything the painter does");

                    // Nothing above the paint floor may come back unpainted: the paint decision reads the
                    // RAW elevation precisely so the wiggle cannot bite a hole in the coast.
                    if (_terrain.ElevationAt(p) >= StPetersShoreMap.PaintFloorElevation)
                        Assert.AreNotEqual(ShoreMaterial.None, StPetersShoreMap.MaterialAt(_terrain, p),
                            $"{p} is above the paint floor but classified as unpainted — the wiggle has " +
                            "leaked into the footprint decision");
                }
            }
            finally { Object.DestroyImmediate(probe); }
        }

        [Test]
        public void TheCrossingIsExemptFromTheWiggle_BecauseAPathIsSignage()
        {
            // The bar's cobble spine is what tells the player where to walk, and the channel is what tells
            // a boat where it can cross. Both must read cleanly along their whole length — a weathered edge
            // on scenery is atmosphere, on signage it is a lie. So the spine is continuous end to end.
            int gaps = 0, steps = 0;
            for (float t = 0.02f; t <= 0.98f; t += 0.01f, steps++)
            {
                Vector2 p = Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, t);

                // Skip the channel cut itself — it is SUPPOSED to interrupt the path.
                float alongFromCut = Mathf.Abs(t - StPetersBuilder.ChannelAlong)
                                     * Vector2.Distance(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo);
                if (alongFromCut <= StPetersBuilder.ChannelHalfWidth) continue;

                if (StPetersShoreMap.MaterialAt(_terrain, p) != ShoreMaterial.Shingle) gaps++;
            }
            Assert.Greater(steps, 50, "sanity: the walk was actually sampled");
            Assert.AreEqual(0, gaps,
                $"{gaps} samples along the bar's crest were not cobble — the walking line has holes in it, " +
                "which means the wiggle has reached the crossing");
        }

        // =================================================================================
        //  THE REEF'S ROCK
        // =================================================================================

        [Test]
        public void TheReefCarriesItsRock_OnTheShelf_AndEveryPieceIsAKitSprite()
        {
            var rocks = StPetersShoreMap.ScatterRocks(_terrain);
            Assert.Greater(rocks.Count, 10,
                "§5.1a: 'the reef should be legible from the deck before it is legible from the depth " +
                "sounder' — there has to be visible rock on the ring");

            var known = new HashSet<string>(ShorelineIsoCatalog.RockSprites);
            foreach (var r in rocks)
                Assert.IsTrue(known.Contains(r.Sprite),
                    $"'{r.Sprite}' is not an item in the ShoreIsoSprites sheet");

            float beachEnd = StPetersBuilder.IslandRadius + StPetersBuilder.IslandFalloff;
            float shelfEnd = beachEnd + StPetersBuilder.ReefShelfWidth;
            var stacks = rocks.Where(r => r.Sprite == "s" || r.Sprite == "m" || r.Sprite == "l").ToArray();
            Assert.IsNotEmpty(stacks, "the sea stacks are the tell §5.1a names");
            foreach (var r in stacks)
            {
                float d = TidalTerrain.IslandDistance(r.Position, StPetersBuilder.IslandCenter,
                                                      StPetersBuilder.IslandRadius,
                                                      StPetersBuilder.IslandRadiusY);
                Assert.GreaterOrEqual(d, beachEnd,
                    "a sea stack stands ON the shelf, not up the beach");
                Assert.LessOrEqual(d, shelfEnd,
                    "a sea stack stands ON the shelf, not out past its lip in the drop-off");
            }
        }

        [Test]
        public void NoRockEverBlocksTheOneDoorInTheReef_OrTheWalkingBar()
        {
            // §6.0 hazards: St Peters is the gentlest tutorial in the game and authors NO rocks as a hazard.
            // The reef's gate is its DEPTH; the rock is only how you see it. So the two crossings — the one
            // dredged slip and the tide-gated bar — must stay clear, or the decoration becomes a hazard.
            foreach (var r in StPetersShoreMap.ScatterRocks(_terrain))
            {
                float toBerth = StPetersShoreMap.DistanceToSegment(
                    r.Position, StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo);
                Assert.Greater(toBerth, StPetersBuilder.BerthHalfWidth,
                    $"a {r.Sprite} at {r.Position} sits inside the berth channel — that is the ONE way in " +
                    "for anything that floats (§5.1a)");

                float toBar = StPetersShoreMap.DistanceToSegment(
                    r.Position, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo);
                Assert.Greater(toBar, StPetersBuilder.SandbarHalfWidth,
                    $"a {r.Sprite} at {r.Position} sits on the sandbar — the crossing is on foot and must " +
                    "stay soft and forgiving");

                Assert.GreaterOrEqual(_terrain.ElevationAt(r.Position), StPetersShoreMap.PaintFloorElevation,
                    $"a {r.Sprite} at {r.Position} stands in water too deep to have painted ground under it");
            }
        }

        [Test]
        public void TheBouldersStayOnTheWeatherCoast_WhereASandstoneShoreSheddsThem()
        {
            var boulders = StPetersShoreMap.ScatterRocks(_terrain)
                                           .Where(r => r.Sprite.StartsWith("b")).ToArray();
            Assert.IsNotEmpty(boulders, "the slab boulders are the weather coast's own dressing");
            foreach (var b in boulders)
                Assert.IsTrue(StPetersShoreMap.IsWeatherCoast(b.Position),
                    $"a boulder at {b.Position} landed on the sheltered coast — the beaches and flats are " +
                    "west/north (§5.1) and a slab boulder there reads as an obstacle on the dig ground");
        }

        // =================================================================================
        //  DETERMINISM + THE FOOTPRINT BUDGET
        // =================================================================================

        [Test]
        public void TheWholeCoastIsDeterministic_NoRng()
        {
            var probes = new Vector2[]
            {
                StPetersBuilder.IslandCenter,
                StPetersBuilder.StartSpawnPos,
                StPetersBuilder.DockZonePos,
                StPetersBuilder.SandbarFrom,
                StPetersBuilder.SandbarTo,
                new Vector2(70f, -260f),
                new Vector2(-300f, 40f),
            };
            foreach (var p in probes)
                Assert.AreEqual(StPetersShoreMap.MaterialAt(_terrain, p),
                                StPetersShoreMap.MaterialAt(_terrain, p),
                    $"the material at {p} must be a pure function of position (rule 5)");

            var a = StPetersShoreMap.ScatterRocks(_terrain);
            var b = StPetersShoreMap.ScatterRocks(_terrain);
            Assert.AreEqual(a.Count, b.Count, "the rock scatter must reproduce exactly on a rebuild");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Sprite, b[i].Sprite);
                Assert.AreEqual(a[i].Position.x, b[i].Position.x, 1e-6f);
                Assert.AreEqual(a[i].Position.y, b[i].Position.y, 1e-6f);
            }
        }

        [Test]
        public void ThePaintedFootprintStaysInsideItsBudget()
        {
            // Six figures of tiles is simply what a 450 x 260 m island costs on a 1 m grid, and a Tilemap is
            // the only vehicle that pays for it (one mesh per chunk, only on-screen chunks drawn). But the
            // count must not creep: it is the scene's size and the builder's runtime. Sampled on a 2 m grid
            // and scaled, so the test is fast and the number is still the real one to within a percent.
            const float step = 2f;
            int samples = 0;
            var min = StPetersBuilder.RegionWorldCenter - StPetersBuilder.RegionWorldSize * 0.5f;
            var max = StPetersBuilder.RegionWorldCenter + StPetersBuilder.RegionWorldSize * 0.5f;

            for (float x = min.x; x < max.x; x += step)
            for (float y = min.y; y < max.y; y += step)
                if (StPetersShoreMap.MaterialAt(_terrain, new Vector2(x, y)) != ShoreMaterial.None) samples++;

            int cells = Mathf.RoundToInt(samples * step * step);
            // ⚠ Budget re-pinned 2026-07-30 with the island shrink: measured ~67,000 painted cells —
            // ~26,000 m² of land plus its (longer) bar and intertidal — against the old island's
            // ~162,000. MOST of the region is now unpainted deep water, which is the ruling's point.
            Assert.Greater(cells, 45_000,
                $"only {cells:N0} painted cells — the island alone is ~26,000 m² of land plus its " +
                "intertidal and a ~305 m bar, so a number this low means whole bands have stopped " +
                "classifying");
            Assert.Less(cells, 110_000,
                $"{cells:N0} painted cells is past the budget. Well over three quarters of a 760 x 520 m " +
                "region is deep harbour that must stay unpainted; if the footprint has grown into it, " +
                "raise StPetersShoreMap.PaintFloorElevation rather than accepting the scene size.");
        }
    }
}
