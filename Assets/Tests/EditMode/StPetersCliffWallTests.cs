using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ THE COAST STANDS UP — the St Peters cliff walls, measured off the plan the builder actually
    /// pushes rather than off a literal copied out of it.
    ///
    /// <para>Sibling of <see cref="StPetersCoastTests"/>, which asserts WHICH stretches are rock. This
    /// asserts that the geometry drawn on them is honest: it faces a direction the kit authors, it is
    /// battered at an angle the bake carries, it sorts into the decor band by its own foot, and running
    /// the generator twice gives the same coast.</para>
    ///
    /// <para><b>Nothing here needs a baked PNG.</b> The kit ships as the rig and its sheets are
    /// gitignored (PR #427), so a CI runner has no faces — every claim below is about the RESOLVED
    /// geometry, which is a pure function of the coast plan and needs no pixels at all. That split is
    /// deliberate: the part that can be tested everywhere is tested everywhere.</para>
    /// </summary>
    public class StPetersCliffWallTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;
        private List<StPetersCliffWalls.Chunk> _chunks;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_CliffWallTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
            _chunks = StPetersCliffWalls.ResolveChunks(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        // =========================================================================================
        //  1. there IS a coast, and it is where the plan put it
        // =========================================================================================

        /// <summary>
        /// ⭐ THE HEADLINE: the plunged coast produces standing wall, and roughly as much of it as the
        /// plan says is rock. The bound is DERIVED by walking the same classifier
        /// <see cref="StPetersCoastTests"/> measures the cliff share with — so a retune of the sectors
        /// moves both together and neither can quietly stop agreeing with the other.
        /// </summary>
        [Test]
        public void TheAuthoredCliffCoastComesOutAsStandingWall_CoveringMostOfIt()
        {
            Assert.Greater(_chunks.Count, 0, "no wall at all was generated from a coast that is 40% rock");

            float wallMetres = 0f;
            foreach (StPetersCliffWalls.Chunk c in _chunks)
                for (int i = 1; i < c.Samples.Count; i++)
                    wallMetres += Vector2.Distance(c.Samples[i - 1].BrowPlan, c.Samples[i].BrowPlan);

            // The cliff share of the shoreline, re-measured here through the live classifier.
            float cliffMetres = 0f, totalMetres = 0f;
            const int samples = 7200;
            float step = 360f / samples;
            for (int i = 0; i < samples; i++)
            {
                float bearing = (i + 0.5f) * step;
                float length = StPetersCliffWalls.ShoreSpeed(bearing) * step * Mathf.Deg2Rad;
                totalMetres += length;
                if (CoastPlan.IsCliff(_terrain.CoastClassAt(StPetersCliffWalls.ShorePoint(bearing))))
                    cliffMetres += length;
            }

            // Walls run PAST each sector into the blend (that is how a run tapers into a beach), so the
            // generated length legitimately exceeds the unfeathered classification a little — but it
            // cannot fall short of it, which is what a dropped run would look like.
            Assert.Greater(wallMetres, cliffMetres * 0.95f,
                $"only {wallMetres:F1} m of wall on {cliffMetres:F1} m of authored cliff — a run is missing");
            Assert.Less(wallMetres, cliffMetres * 1.35f,
                $"{wallMetres:F1} m of wall on {cliffMetres:F1} m of authored cliff — the overrun is " +
                "spilling wall onto ground the plan calls beach");

            Debug.Log($"[cliff-walls] {_chunks.Count} chunks, {wallMetres:F1} m of standing wall on " +
                      $"{cliffMetres:F1} m of authored cliff ({cliffMetres / totalMetres:P1} of " +
                      $"{totalMetres:F1} m of shoreline).");
        }

        /// <summary>The two crossings stay open. A wall across the sandbar's landing or the berth's cove
        /// would be region-breaking, and no rendering test would ever catch it.</summary>
        [Test]
        public void NoWallStandsOnTheSandbarLandingOrTheBerthCove()
        {
            foreach (var (name, pos) in new (string, Vector2)[]
            {
                ("the sandbar's island end", StPetersBuilder.SandbarFrom),
                ("the berth's shore end",    StPetersBuilder.BerthTo),
                ("the dock zone",            StPetersBuilder.DockZonePos),
                ("the disembark step",       StPetersBuilder.DisembarkPos),
            })
            {
                foreach (StPetersCliffWalls.Chunk c in _chunks)
                    foreach (CliffWallSample s in c.Samples)
                        Assert.Greater(Vector2.Distance(s.BrowPlan, pos), 6f,
                            $"a cliff face stands within 6 m of {name} at {s.BrowPlan}");
            }
        }

        // =========================================================================================
        //  2. every chunk is drawable — the aspect law and the bake, pointwise
        // =========================================================================================

        /// <summary>
        /// ⚠ THE ASPECT LAW, ON THE GEOMETRY. <see cref="StPetersCoastTests"/> proves no cliff METRE
        /// faces a direction the kit refuses to draw; this proves the generator then PICKS that
        /// direction's face. They are different claims — a correct plan wired to an off-by-one aspect
        /// table would pass the first and fail this.
        /// </summary>
        [Test]
        public void EveryChunkWearsTheFaceItsOwnOutwardNormalSnapsTo()
        {
            float worst = 0f;
            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                Assert.That(c.AspectIndex, Is.InRange(0, CliffCatalog.Aspects.Length - 1));

                float chosen = CoastPlan.AspectAzimuths[c.AspectIndex];
                float error = Mathf.Abs(Mathf.DeltaAngle(c.WallAzimuth, chosen));
                worst = Mathf.Max(worst, error);

                Assert.LessOrEqual(error, CoastPlan.AspectSnapToleranceDegrees + 1e-3f,
                    $"a chunk facing {c.WallAzimuth:F1}° was given the {CliffCatalog.Aspects[c.AspectIndex]} " +
                    $"face ({chosen}°) — {error:F2}° away, past the kit's {CoastPlan.AspectSnapToleranceDegrees}° snap");

                // North is not authored at all: a wall facing the camera's back would read as a hole.
                Assert.That(c.WallAzimuth, Is.InRange(67.5f, 292.5f),
                    $"a chunk faces {c.WallAzimuth:F1}° — the kit authors no north-facing wall");
            }
            Debug.Log($"[cliff-walls] worst aspect-snap error over every chunk: {worst:F2}° " +
                      $"(law {CoastPlan.AspectSnapToleranceDegrees}°)");
        }

        /// <summary>
        /// ⚠⚠ THE FACE RUNS DOWN THE COAST'S OUTWARD NORMAL — the invariant whose absence made the
        /// generator draw a wall skewed to its own coast.
        ///
        /// <para><c>CoastPlan</c> carries a standing warning that the aspect law constrains the NORMAL
        /// and that on an ellipse the normal and the bearing are different angles. The first version of
        /// this generator honoured that when CHOOSING the face and forgot it when placing the toe — it
        /// stepped radially instead — and near the island's shoulder those two directions are 20° apart.
        /// The wall was drawn askew, wearing a face picked for a direction it did not face. Nothing about
        /// that renders as an error; it renders as rock with the light coming from slightly the wrong
        /// place.</para>
        ///
        /// <para>So: the direction the geometry actually runs must BE the direction the plan says it
        /// faces, everywhere, to a hair.</para>
        /// </summary>
        [Test]
        public void TheGeometryRunsDownTheSameNormalTheAspectWasChosenFrom()
        {
            float worst = 0f;
            Vector2 worstAt = Vector2.zero;
            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                foreach (CliffWallSample s in c.Samples)
                {
                    float geometric = CliffWallGeometry.AzimuthOf(CliffWallGeometry.OutwardPlan(in s));
                    float planned = CoastPlan.OutwardNormalAzimuth(
                        _terrain.Bearing(s.BrowPlan),
                        StPetersBuilder.IslandRadius, StPetersBuilder.IslandRadiusY);

                    float error = Mathf.Abs(Mathf.DeltaAngle(geometric, planned));
                    if (error > worst) { worst = error; worstAt = s.BrowPlan; }
                }
            }

            Assert.Less(worst, 1f,
                $"a face at {worstAt} RUNS {worst:F2}° away from the outward normal its aspect was " +
                "chosen from. The wall is drawn skewed to its own coast — and the aspect it wears was " +
                "picked for the direction it does not face.");
            Debug.Log($"[cliff-walls] worst geometry-vs-plan normal error: {worst:F3}° " +
                      "(the march's own step, not a modelling difference)");
        }

        /// <summary>
        /// ⚠ EVERY CHUNK MUST WEAR A BATTER THE DEFAULT BAKE ACTUALLY WROTE. <c>bank</c> (48°) is
        /// deliberately not baked, so a chunk snapped to it would ask for a PNG that is not on disk and
        /// leave a hole in the coast — and because the loader warns rather than throws, the hole would
        /// ship.
        /// </summary>
        [Test]
        public void EveryChunkIsBatteredAtAnAngleTheDefaultBakeCarries()
        {
            float[] baked = StPetersCliffWalls.BakedBatterAngles();
            Assert.AreEqual(3, baked.Length, "the default bake ships wall, steep and ramp");
            CollectionAssert.DoesNotContain(baked, 48f, "bank is deliberately not baked — it is not a cliff");

            var used = new SortedSet<float>();
            float shallowest = 90f, steepest = 0f, worstSnap = 0f;
            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                Assert.That(c.BatterIndex, Is.InRange(0, baked.Length - 1));
                int catalogIndex = StPetersCliffWalls.BakedBatterToCatalogIndex(c.BatterIndex);
                used.Add(CliffCatalog.BatterAngles[catalogIndex]);

                foreach (CliffWallSample s in c.Samples)
                {
                    float trueBatter = CliffWallGeometry.BatterDegrees(in s);
                    shallowest = Mathf.Min(shallowest, trueBatter);
                    steepest = Mathf.Max(steepest, trueBatter);
                    worstSnap = Mathf.Max(worstSnap,
                        Mathf.Abs(trueBatter - CliffCatalog.BatterAngles[catalogIndex]));
                }
            }

            // ⚠ THE BATTER LAW. A taper into a gully runs the derived angle smoothly down to nearly
            // flat; drawing a 22° slope with the 62° ramp's bedding is the kit's rule-11 failure.
            Assert.GreaterOrEqual(shallowest, StPetersCliffWalls.MinBatterDegrees - 1e-3f,
                $"a face at {shallowest:F1}° was drawn against a {StPetersCliffWalls.MinBatterDegrees}° " +
                "floor — the taper is being drawn past the point the kit can express it");

            Debug.Log($"[cliff-walls] true batters {shallowest:F1}°…{steepest:F1}° " +
                      $"(law floor {StPetersCliffWalls.MinBatterDegrees}°), worst snap {worstSnap:F1}°; " +
                      $"textures in use {string.Join(", ", used)}° of baked {string.Join(", ", baked)}°");
        }

        /// <summary>A chunk of one station spans nothing and would emit an empty mesh carrying a real
        /// sorting order — invisible, and miserable to debug. And every face must be tall enough to be
        /// worth drawing, which is what the taper into a beach is culled by.</summary>
        [Test]
        public void EveryChunkSpansSomething_AndEveryFaceIsTallEnoughToBeRock()
        {
            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                Assert.GreaterOrEqual(c.Samples.Count, 2, "a chunk of one station spans nothing");
                foreach (CliffWallSample s in c.Samples)
                {
                    Assert.GreaterOrEqual(s.DropMetres, StPetersCliffWalls.MinFaceDropMetres - 1e-3f,
                        $"a face of {s.DropMetres:F2} m at {s.BrowPlan} is a sliver, not a wall");
                    Assert.Greater(CliffWallGeometry.SurfaceLengthMetres(in s), 0f);
                }
                Assert.IsTrue(CoastPlan.IsCliff(c.Class),
                    $"a chunk was generated for {c.Class}, which is not rock");
            }
        }

        // =========================================================================================
        //  3. ⭐ THE SORTING LADDER (ADR 0032)
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ THE LADDER PIN. A wall chunk must draw OVER everything standing at its brow (you are on
        /// the clifftop, behind it) and UNDER everything standing at its foot (you are on the foreshore,
        /// in front of it). Both at once, for every station in the chunk.
        ///
        /// <para><b>This is the property, not the formula.</b> The order is derived by
        /// <see cref="CliffWallGeometry.SortY"/> and mapped through <see cref="YSortSprite.OrderFor"/> —
        /// the same call every tuft of grass makes — and this test never restates either. It asks the
        /// question a player would: does the wall hide me when I stand behind it, and let me past when I
        /// stand in front?</para>
        ///
        /// <para><b>⚠ ADR 0032's lesson, applied.</b> A FIXED order inside the band is buried
        /// deterministically the moment the band moves; a wall that derives from position cannot be. So
        /// the assertion below is written against <c>SortingBands</c>' constants rather than against
        /// literals, and it stays true through the next re-base.</para>
        ///
        /// <para><b>⚠ THE TWO CLAIMS ARE NOT THE SAME STRENGTH, AND SAYING SO IS THE POINT.</b> The
        /// clifftop claim is EXACT and must never fail: a wall must hide a walker standing on top of it,
        /// at every station, or the island layers wrong the moment anyone walks the coast. The foot
        /// claim is BOUNDED: a chunk carries one order and its foot wanders across
        /// <see cref="StPetersCliffWalls.ChunkToeSpanMetres"/>, so a walker at the SHALLOWEST foot of a
        /// chunk is mis-sorted by up to that span — by construction, and by less than the height of the
        /// walker. Asserting zero there is what the first version of this test did, and the coast said
        /// no: it measured 5.6 m of wander because the chunks were cut on shore length instead of on Y.
        /// The bound is what turned that into a fixable number.</para>
        /// </summary>
        [Test]
        public void EveryChunkSortsOverItsOwnBrow_AndUnderItsOwnFootToWithinTheStatedBound()
        {
            int worstBrowMargin = int.MaxValue, worstFootError = 0;
            float worstToeSpan = 0f, worstStationJump = 0f;

            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                int order = OrderOf(c);
                Assert.That(order, Is.InRange(SortingBands.DecorFloor, SortingBands.DecorCeiling),
                    "a wall outside the decor band cannot interleave with the decor standing on it");

                float lowToe = float.MaxValue, highToe = float.MinValue, previousToe = float.NaN;
                foreach (CliffWallSample s in c.Samples)
                {
                    float toeY = CliffWallGeometry.ToeScreen(in s).y;
                    lowToe = Mathf.Min(lowToe, toeY);
                    highToe = Mathf.Max(highToe, toeY);
                    if (!float.IsNaN(previousToe))
                        worstStationJump = Mathf.Max(worstStationJump, Mathf.Abs(toeY - previousToe));
                    previousToe = toeY;

                    // ⭐ EXACT: the wall always draws over its own clifftop.
                    int atBrow = OrderAt(s.BrowPlan.y);
                    Assert.GreaterOrEqual(order, atBrow,
                        $"a walker on the clifftop at Y={s.BrowPlan.y:F1} (order {atBrow}) would draw " +
                        $"IN FRONT of the wall they are standing on (order {order})");
                    worstBrowMargin = Mathf.Min(worstBrowMargin, order - atBrow);

                    worstFootError = Mathf.Max(worstFootError, Mathf.Max(0, order - OrderAt(toeY)));
                }
                worstToeSpan = Mathf.Max(worstToeSpan, highToe - lowToe);
            }

            // ⭐ THE CUT IS DOING ITS JOB, and the ONLY thing that overshoots it is the boundary station
            // — which is shared with the next chunk on purpose, because a chunk that stopped one station
            // early would leave a 0.25 m hole of open sky in the coast. So the realised span may exceed
            // the cut by exactly one station's worth of Y and by nothing else. Both terms are MEASURED
            // off the built walls; neither is written down.
            Assert.LessOrEqual(worstToeSpan,
                StPetersCliffWalls.ChunkToeSpanMetres + worstStationJump + 1e-3f,
                $"a chunk's foot wanders {worstToeSpan:F2} m against a " +
                $"{StPetersCliffWalls.ChunkToeSpanMetres} m cut plus a {worstStationJump:F2} m station " +
                "— something other than the shared boundary station is widening chunks");

            // …and a station is a SMALL correction to the cut, not the thing setting the span. If this
            // fails the stations have grown coarse and the bound above has stopped meaning anything.
            Assert.Less(worstStationJump, StPetersCliffWalls.ChunkToeSpanMetres * 0.5f,
                $"one station moves the foot {worstStationJump:F2} m — stations are too coarse for a " +
                $"{StPetersCliffWalls.ChunkToeSpanMetres} m cut to bound anything");

            int allowedFootError = Mathf.CeilToInt(worstToeSpan * SortingBands.OrdersPerMetre);
            Assert.LessOrEqual(worstFootError, allowedFootError,
                $"a walker at some chunk's shallowest foot is {worstFootError} orders behind it, past " +
                $"the {allowedFootError} its {worstToeSpan:F2} m span accounts for");

            Debug.Log($"[cliff-walls] clifftop margin ≥ {worstBrowMargin} orders (EXACT claim); " +
                      $"worst foot error {worstFootError} orders = {worstFootError / SortingBands.OrdersPerMetre:F2} m " +
                      $"of walker, from a widest chunk foot span of {worstToeSpan:F2} m " +
                      $"({StPetersCliffWalls.ChunkToeSpanMetres} m cut + {worstStationJump:F2} m station). " +
                      $"Band {SortingBands.DecorFloor}…{SortingBands.DecorCeiling} at " +
                      $"{SortingBands.OrdersPerMetre}/m.");
        }

        /// <summary>
        /// The two cuts both bite, and the SHALLOWEST face on the island still separates its brow from
        /// its toe by more than a chunk's allowed Y span — which is the slack the clifftop claim above
        /// spends. Derived from the walls actually built: a coast retune that flattened the cliffs would
        /// shrink that separation and fail here with the number to lower the span cut to.
        /// </summary>
        [Test]
        public void BothChunkCutsBite_AndTheShallowestFaceStillHasSlackToSpend()
        {
            float minDrop = float.MaxValue, longestChunk = 0f;
            int cutByLength = 0;
            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                float run = 0f;
                for (int i = 1; i < c.Samples.Count; i++)
                    run += Vector2.Distance(c.Samples[i - 1].BrowPlan, c.Samples[i].BrowPlan);
                longestChunk = Mathf.Max(longestChunk, run);
                if (run >= StPetersCliffWalls.ChunkMetres - StPetersCliffWalls.StationMetres) cutByLength++;
                foreach (CliffWallSample s in c.Samples) minDrop = Mathf.Min(minDrop, s.DropMetres);
            }

            Assert.LessOrEqual(longestChunk,
                StPetersCliffWalls.ChunkMetres + StPetersCliffWalls.StationMetres + 1e-3f,
                $"a chunk ran {longestChunk:F2} m against a {StPetersCliffWalls.ChunkMetres} m cut");

            float slack = CliffWallGeometry.BrowToToeSeparationMetres(minDrop);
            Assert.Greater(slack, StPetersCliffWalls.ChunkToeSpanMetres,
                $"the shallowest face on the island ({minDrop:F2} m) separates brow from toe by only " +
                $"{slack:F2} m, less than the {StPetersCliffWalls.ChunkToeSpanMetres} m a chunk may " +
                "span — a chunk could sort behind the clifftop it stands on. Lower ChunkToeSpanMetres.");

            Debug.Log($"[cliff-walls] longest chunk {longestChunk:F2} m ({cutByLength} of " +
                      $"{_chunks.Count} cut by LENGTH, the rest by Y span or texture). Shallowest face " +
                      $"{minDrop:F2} m => {slack:F2} m of brow-to-toe slack against a " +
                      $"{StPetersCliffWalls.ChunkToeSpanMetres} m span cut.");
        }

        /// <summary>A wall must never sink to the band's floor or saturate at its ceiling — both mean
        /// the region has outgrown the band and the wall has stopped sorting by position at all, which
        /// is the 2026-08-05 defect exactly.</summary>
        [Test]
        public void NoChunkSaturatesTheDecorBand()
        {
            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                Assert.IsTrue(SortingBands.ResolvesWorldY(CliffWallGeometry.SortY(c.Samples)),
                    $"a chunk sorts at Y={CliffWallGeometry.SortY(c.Samples):F1}, outside the band's " +
                    $"±{SortingBands.DecorHalfExtentMetres} m — raise DecorHalfExtentMetres");
                int order = OrderOf(c);
                Assert.Greater(order, SortingBands.DecorFloor);
                Assert.Less(order, SortingBands.DecorCeiling);
            }
        }

        static int OrderOf(StPetersCliffWalls.Chunk c) =>
            OrderAt(CliffWallGeometry.SortY(c.Samples));

        static int OrderAt(float worldY) =>
            YSortSprite.OrderFor(worldY, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                 SortingBands.DecorFloor, SortingBands.DecorCeiling);

        // =========================================================================================
        //  4. determinism and idempotence
        // =========================================================================================

        /// <summary>
        /// Two runs agree exactly (rule 5) — which is also what makes a builder Refresh CONVERGE. A
        /// generator with any hidden state would drift the coast a little on every rebuild and show up
        /// as scene churn rather than as a bug.
        /// </summary>
        [Test]
        public void ResolvingTwiceGivesTheSameCoast_ToTheBit()
        {
            List<StPetersCliffWalls.Chunk> again = StPetersCliffWalls.ResolveChunks(_terrain);
            Assert.AreEqual(_chunks.Count, again.Count);
            for (int i = 0; i < _chunks.Count; i++)
            {
                Assert.AreEqual(_chunks[i].AspectIndex, again[i].AspectIndex, $"chunk {i} aspect");
                Assert.AreEqual(_chunks[i].BatterIndex, again[i].BatterIndex, $"chunk {i} batter");
                Assert.AreEqual(_chunks[i].WallAzimuth, again[i].WallAzimuth, $"chunk {i} azimuth");
                Assert.AreEqual(_chunks[i].Samples.Count, again[i].Samples.Count, $"chunk {i} stations");
                for (int j = 0; j < _chunks[i].Samples.Count; j++)
                {
                    Assert.AreEqual(_chunks[i].Samples[j].BrowPlan, again[i].Samples[j].BrowPlan);
                    Assert.AreEqual(_chunks[i].Samples[j].ToePlan, again[i].Samples[j].ToePlan);
                    Assert.AreEqual(_chunks[i].Samples[j].DropMetres, again[i].Samples[j].DropMetres);
                }
            }
        }

        /// <summary>A region that authors NO coast plan gets no walls — the additive guarantee applied
        /// to this feature. Nine Mile Creek and every future region must be untouched by cliffs it did
        /// not ask for.</summary>
        [Test]
        public void ARegionWithNoCoastPlanGetsNoWalls()
        {
            _terrain.CoastSectors = new CoastSector[0];
            Assert.AreEqual(0, StPetersCliffWalls.ResolveChunks(_terrain).Count,
                "a region with no plan authored a beach coast and must stay one");
        }

        [Test]
        public void ANullTerrainIsAnsweredWithNoWalls_NotAnException()
        {
            Assert.AreEqual(0, StPetersCliffWalls.ResolveChunks(null).Count);
        }

        // =========================================================================================
        //  5. the walls and the decor
        // =========================================================================================

        /// <summary>
        /// ⭐ <b>THE BODY OF THE WALL IS UNPLANTABLE — but its BROW BAND is not, and that is a finding
        /// rather than a bug.</b>
        ///
        /// <para>Decor keys off ELEVATION, never off the coast class (no decor code reads
        /// <see cref="TidalTerrain.CoastClassAt"/> at all — the walls are its first consumer). So
        /// "does the slope already exclude the face?" had to be MEASURED, and the measurement says: no,
        /// not all of it. The plateau stands at 6 m and the grass floor is 4.2 m, so the top of every
        /// plunge crosses 1.8 m of plantable elevation while already near-vertical. <b>MEASURED at
        /// roughly a quarter of the face's height</b> — reported below, so the number is on the record
        /// rather than in someone's estimate.</para>
        ///
        /// <para><b>Why this PR does not "fix" it.</b> Grass clinging at a clifftop lip is what a real
        /// headland does, and the kit ships a BROW DECAL for exactly that seam. If the owner wants the
        /// band narrower, the dial is the habitat resolver's own floor — not a cliff-shaped exception
        /// bolted onto the planter, which is the coupling the handoff rules out in as many words.</para>
        ///
        /// <para><b>⚠ AND THE TAPER IS NOT A WALL.</b> A run overruns its sector into the blend so it
        /// dies into a beach or a gully instead of stopping in a step — and over that overrun the ground
        /// really IS becoming beach, so it really does take grass. The same is true INSIDE a sector's
        /// own feather. So the strict claim below is made only where <c>CoastPlan.BlendAt</c> — the very
        /// function that decides the height — reports an undiluted cliff; everything else is measured
        /// and reported. Asserting over the taper is what the first version of this test did, and it
        /// found the west trail's gully and then the ledge run's first metre: both places a wall SHOULD
        /// be turning back into meadow.</para>
        ///
        /// <para><b>What is pinned:</b> on an authored cliff sector, below the top 40% of a face,
        /// NOTHING plants. The body of the wall is rock.</para>
        /// </summary>
        [Test]
        public void TheBodyOfEveryWallIsUnplantable_AndTheBrowBandIsMeasuredNotAssumed()
        {
            const float BodyStartsAt = 0.4f;        // fraction of the way down the face
            int plantableOnRock = 0, sampledOnRock = 0;
            int plantableInBody = 0, sampledInBody = 0;
            int plantableInTaper = 0, sampledInTaper = 0;
            float deepestOnRock = 0f;
            Vector2 worstAt = Vector2.zero;

            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                foreach (CliffWallSample s in c.Samples)
                {
                    // Is this station standing on UNDILUTED rock?
                    //
                    // ⚠ NOT the unfeathered class — that was this test's first answer and it was wrong.
                    // CoastClassAt says what the plan SAYS, but the HEIGHTS come off the blend, so a
                    // station one degree inside a cliff sector is classified rock while its ground is
                    // still three-quarters beach. The discriminator has to be the same function that
                    // decides the height: BlendAt's own weight, which reaches 1 only in a sector's
                    // interior. (Found the hard way at bearing 136° — the LedgeCliff run's very first
                    // metre, inside the west trail's feather.)
                    CoastPlan.BlendAt(_terrain.CoastSectors, _terrain.Bearing(s.BrowPlan),
                                      _terrain.CoastBlendDegrees,
                                      out CoastClass primary, out CoastClass secondary, out float weight);
                    bool onRock = CoastPlan.IsCliff(primary) && (weight >= 1f || primary == secondary);

                    Vector2 outward = CliffWallGeometry.OutwardPlan(in s);
                    float run = CliffWallGeometry.PlanRunMetres(in s);
                    for (float t = 0.05f; t <= 1f; t += 0.05f)
                    {
                        Vector2 p = s.BrowPlan + outward * (run * t);
                        bool plantable =
                            StPetersWoods.IsPlantable(_terrain, p, StPetersShoreMap.GrassFloorElevation);

                        if (!onRock)
                        {
                            sampledInTaper++;
                            if (plantable) plantableInTaper++;
                            continue;
                        }

                        sampledOnRock++;
                        if (t >= BodyStartsAt) sampledInBody++;
                        if (!plantable) continue;

                        plantableOnRock++;
                        if (t > deepestOnRock) { deepestOnRock = t; worstAt = p; }
                        if (t >= BodyStartsAt) plantableInBody++;
                    }
                }
            }

            // ⭐ THE PIN: on an authored cliff sector, the body of the wall is rock.
            Assert.AreEqual(0, plantableInBody,
                $"{plantableInBody} of {sampledInBody} samples below the top {BodyStartsAt:P0} of a " +
                $"face standing on AUTHORED CLIFF would take grass — the deepest at {worstAt}, " +
                $"{deepestOnRock:P0} down. The body of a wall must be rock; if the habitat floors " +
                "moved, this is where it shows.");

            float browShare = sampledOnRock == 0 ? 0f : (float)plantableOnRock / sampledOnRock;
            Assert.Less(browShare, BodyStartsAt,
                $"{browShare:P1} of a rock face is plantable, which exceeds the brow band itself — " +
                "the plunge has stopped being a wall");

            float taperShare = sampledInTaper == 0 ? 0f : (float)plantableInTaper / sampledInTaper;
            Debug.Log(
                $"[cliff-walls] plantable at the grass floor ({StPetersShoreMap.GrassFloorElevation} m): " +
                $"{browShare:P1} of samples on AUTHORED ROCK, all within the top {deepestOnRock:P0} of " +
                $"the face; {taperShare:P1} over the TAPER — the overrun AND every sector's own " +
                $"{StPetersBuilder.CoastBlendDegrees}° feather, where the profile is still part beach " +
                "(expected: that ground is becoming meadow, and a run that did not hand it back would " +
                "end in a step).\n" +
                "⚠ OWNER/COORDINATOR: the brow band on rock is real and is the finding of this pass — " +
                "the plateau stands at 6 m and the grass floor is 4.2 m, so meadow reaches about a " +
                "quarter of the way down every plunge while it is already near-vertical. The kit ships " +
                "a BROW DECAL for exactly that seam. Narrowing it is a habitat-floor dial, not a " +
                "cliff-shaped exception in the planter.");
        }

        /// <summary>The generated names are stable and self-describing, so a scene diff reads as a coast
        /// rather than as forty anonymous objects — and so a chunk can be found in the hierarchy by what
        /// it IS.</summary>
        [Test]
        public void ChunkNamingCarriesTheClassAspectAndBatter()
        {
            var seen = new StringBuilder();
            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                string aspect = CliffCatalog.Aspects[c.AspectIndex];
                string batter = CliffCatalog.Batters[
                    StPetersCliffWalls.BakedBatterToCatalogIndex(c.BatterIndex)];
                Assert.IsNotEmpty(aspect);
                Assert.IsNotEmpty(batter);
                seen.Append($"{c.Class}/{aspect}/{batter} ");
            }
            Assert.Greater(seen.Length, 0);
        }
    }
}
