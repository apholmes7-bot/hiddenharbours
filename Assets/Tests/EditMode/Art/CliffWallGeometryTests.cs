using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// ⭐ THE WALL IS BUILT AT THE RIGHT SCALE — the geometry law behind
    /// <see cref="CliffWallGeometry"/>, asserted headless.
    ///
    /// <para><b>Why this file is load-bearing.</b> Every failure mode it covers is SILENT. A wall sized
    /// by its drop instead of by its surface still renders perfectly and merely draws slightly wrong
    /// rock; a wall projected with <c>sin 40°</c> instead of <c>cos 40°</c> is a consistent 16% too
    /// short and looks like an art choice. Nothing but arithmetic catches either, which is exactly the
    /// case for arithmetic that can be tested.</para>
    ///
    /// <para>Sibling of <see cref="CliffLightMathTests"/>: that one pins how a face is LIT, this one
    /// pins where it IS.</para>
    /// </summary>
    public class CliffWallGeometryTests
    {
        const float Eps = 1e-4f;

        static CliffWallSample Sample(float runEast, float runNorth, float drop) =>
            new CliffWallSample(Vector2.zero, new Vector2(runEast, runNorth), drop);

        // =========================================================================================
        //  1. the projection — height and ground travel are NOT the same term
        // =========================================================================================

        /// <summary>
        /// ⭐ A metre of HEIGHT draws <c>cos 40°</c> up the screen, and it is the only term the drop
        /// gets. The plan run is world XY already and is not re-projected — double-projecting it is the
        /// mistake that would make every cliff on the island 1.5× too tall and look deliberate.
        /// </summary>
        [Test]
        public void TheDropDrawsAtTheSharedCameraHeightScale_AndThePlanRunIsNotReProjected()
        {
            // A wall with no plan run at all: the whole of its drawn height must be the projection.
            var vertical = Sample(0f, 0f, 6f);
            Vector2 toe = CliffWallGeometry.ToeScreen(in vertical);
            Assert.AreEqual(0f, toe.x, Eps, "a wall with no plan run may not move sideways");
            Assert.AreEqual(-6f * SpriteLightMath.HeightScale, toe.y, Eps,
                "6 m of cliff must draw 6 x cos(40) world units down-screen — SpriteLightMath.HeightScale " +
                "is the project's own definition and every sprite in the scene already obeys it");

            // …and a run southward ADDS to that, because the top of the cliff genuinely recedes.
            var southFacing = Sample(0f, -1.75f, 6f);
            Vector2 southToe = CliffWallGeometry.ToeScreen(in southFacing);
            Assert.AreEqual(-1.75f - 6f * SpriteLightMath.HeightScale, southToe.y, Eps,
                "a south-facing face recedes in plan AND drops; both push down-screen");

            // An EAST-facing face shears instead of lengthening — that shear is what a 3/4 view of an
            // east wall looks like, and squaring it up is what makes a cliff read as a painted band.
            var eastFacing = Sample(3f, 0f, 6f);
            Vector2 eastToe = CliffWallGeometry.ToeScreen(in eastFacing);
            Assert.AreEqual(3f, eastToe.x, Eps, "an east-facing face reaches east in plan");
            Assert.AreEqual(-6f * SpriteLightMath.HeightScale, eastToe.y, Eps);
        }

        /// <summary>The toe is ALWAYS below the brow, whatever the facing — the invariant the whole
        /// sorting design rests on (a chunk sorts at its toe and must therefore sit under everything at
        /// its brow). A wall that could draw its foot above its lip would break that silently.</summary>
        [Test]
        public void TheToeIsAlwaysBelowTheBrow_ForEveryFacingTheKitAuthors()
        {
            foreach (float azimuth in CoastPlanAzimuths())
            {
                float r = azimuth * Mathf.Deg2Rad;
                var s = new CliffWallSample(Vector2.zero,
                                            new Vector2(Mathf.Sin(r), Mathf.Cos(r)) * 3f, 6f);
                float toeY = CliffWallGeometry.ToeScreen(in s).y;
                Assert.Less(toeY, 0f,
                    $"a face at azimuth {azimuth} draws its toe at Y={toeY:F3}, not below its brow");
            }
        }

        static float[] CoastPlanAzimuths() => CliffCatalog.AspectAzimuths;

        // =========================================================================================
        //  2. surface metres — the rule the kit states twice
        // =========================================================================================

        /// <summary>
        /// ⭐ THE README'S OWN WORKED EXAMPLE: "an 8 m bank at 48° needs 8/sin(48°) = 10.8 m of t,
        /// i.e. 1.2 tiles." Reproduced here from the geometry rather than from the number, so the
        /// example and the code cannot drift.
        /// </summary>
        [Test]
        public void AnEightMetreBankAtFortyEightDegreesIsOnePointTwoTiles_NotZeroPointNine()
        {
            const float drop = 8f;
            const float batter = 48f;
            float run = drop / Mathf.Tan(batter * Mathf.Deg2Rad);
            var s = Sample(0f, -run, drop);

            Assert.AreEqual(batter, CliffWallGeometry.BatterDegrees(in s), 1e-2f,
                "the batter must come back out of the geometry it was built from");

            float surface = CliffWallGeometry.SurfaceLengthMetres(in s);
            Assert.AreEqual(drop / Mathf.Sin(batter * Mathf.Deg2Rad), surface, 1e-3f);
            Assert.AreEqual(10.8f, surface, 0.05f, "the README's 10.8 m of surface");

            float tiles = CliffWallGeometry.TileV(surface, CliffCatalog.FaceMetresT);
            Assert.AreEqual(1.2f, tiles, 0.01f, "the README's 1.2 tiles");

            // The mistake the rule exists to prevent, stated as the thing that must NOT be equal.
            float byHeight = CliffWallGeometry.TileV(drop, CliffCatalog.FaceMetresT);
            Assert.Less(byHeight, tiles,
                "sizing by height under-counts the tiling — that is the 'tilted vertical texture' read");
        }

        /// <summary>The surface length is the hypotenuse, so it is never shorter than either leg. A cheap
        /// property, but it is the one that fails first if the run and the drop are ever added in the
        /// wrong space.</summary>
        [Test]
        public void SurfaceLengthDominatesBothTheDropAndTheRun()
        {
            for (float drop = 1f; drop <= 10f; drop += 1.5f)
            for (float run = 0f; run <= 6f; run += 0.75f)
            {
                var s = Sample(run, 0f, drop);
                float surface = CliffWallGeometry.SurfaceLengthMetres(in s);
                Assert.GreaterOrEqual(surface, drop - Eps, $"drop {drop} run {run}");
                Assert.GreaterOrEqual(surface, run - Eps, $"drop {drop} run {run}");
            }
        }

        // =========================================================================================
        //  3. the batter — derived, then snapped to what is actually on disk
        // =========================================================================================

        [Test]
        public void TheBatterIsDerivedFromTheProfile_NinetyWhenThereIsNoRun()
        {
            Assert.AreEqual(90f, CliffWallGeometry.BatterDegrees(Sample(0f, 0f, 6f)), Eps);
            Assert.AreEqual(45f, CliffWallGeometry.BatterDegrees(Sample(0f, -6f, 6f)), Eps);
            Assert.AreEqual(45f, CliffWallGeometry.BatterDegrees(Sample(6f, 0f, 6f)), Eps,
                "the batter is about rise over RUN and does not care which way the run points");
        }

        /// <summary>
        /// ⚠ THE SNAP MAY ONLY REACH A BAKED ANGLE. <c>bank</c> (48°) is deliberately absent from
        /// <c>CliffBaker.DefaultBatters</c> — "a 48° slope is not a cliff, and the owner's complaint is
        /// precisely that the current coast reads as one" — so a snap that could return it would pick a
        /// texture that is not on disk and leave a hole in the coast.
        /// </summary>
        [Test]
        public void TheBatterSnapsOnlyToAnAngleTheDefaultBakeActuallyCarries()
        {
            float[] baked = { 90f, 76f, 62f };          // wall, steep, ramp — CliffBaker.DefaultBatters
            for (float angle = 30f; angle <= 90f; angle += 0.5f)
            {
                int i = CliffWallGeometry.SnapBatterIndex(angle, baked);
                Assert.That(i, Is.InRange(0, baked.Length - 1), $"angle {angle}");
                // …and it really is the NEAREST, not merely a legal one.
                for (int j = 0; j < baked.Length; j++)
                    Assert.LessOrEqual(Mathf.Abs(angle - baked[i]), Mathf.Abs(angle - baked[j]) + Eps,
                        $"angle {angle} snapped to {baked[i]} with {baked[j]} nearer");
            }
        }

        [Test]
        public void AnEmptyBakeListCannotThrow_ItAnswersTheWall()
        {
            Assert.AreEqual(0, CliffWallGeometry.SnapBatterIndex(62f, null));
            Assert.AreEqual(0, CliffWallGeometry.SnapBatterIndex(62f, new float[0]));
        }

        // =========================================================================================
        //  4. the compass, and the displacement decode
        // =========================================================================================

        /// <summary>N = 0 and clockwise — <c>atan2(east, north)</c>. A mislabelled compass is this
        /// repo's most-repeated art defect (five kits), so it is pinned on all four cardinals.</summary>
        [Test]
        public void TheAzimuthIsACompassBearing_NorthIsZeroAndItRunsClockwise()
        {
            Assert.AreEqual(0f, CliffWallGeometry.AzimuthOf(Vector2.up), Eps, "north");
            Assert.AreEqual(90f, CliffWallGeometry.AzimuthOf(Vector2.right), Eps, "east");
            Assert.AreEqual(180f, CliffWallGeometry.AzimuthOf(Vector2.down), Eps, "south");
            Assert.AreEqual(270f, CliffWallGeometry.AzimuthOf(Vector2.left), Eps, "west");
            Assert.AreEqual(135f, CliffWallGeometry.AzimuthOf(new Vector2(1f, -1f)), Eps, "south-east");
        }

        /// <summary>
        /// The profile decode is the baker's encode run backwards: the baker writes
        /// <c>128 + disp/1.15 × 127</c> and throws if the rig's own metres disagree with the catalog's.
        /// Getting this backwards scales every displaced vertex silently — it reads as "the profile does
        /// nothing" rather than as an error.
        /// </summary>
        [Test]
        public void TheProfileDecodeMirrorsTheBakersGreyEncoding()
        {
            const float m = CliffCatalog.ProfileMetres;
            Assert.AreEqual(0f, CliffWallGeometry.ProfileMetresFromGrey(0.5f, m), Eps, "128 is zero");
            Assert.AreEqual(m, CliffWallGeometry.ProfileMetresFromGrey(1f, m), Eps);
            Assert.AreEqual(-m, CliffWallGeometry.ProfileMetresFromGrey(0f, m), Eps);

            // Round-trip through the baker's own integer encoding, at the rig's measured range.
            foreach (float disp in new[] { -0.61f, -0.2f, 0f, 0.35f, 0.69f })
            {
                int grey = Mathf.RoundToInt(128f + disp / m * 127f);
                float back = CliffWallGeometry.ProfileMetresFromGrey(grey / 255f, m);
                Assert.AreEqual(disp, back, 0.02f, $"displacement {disp} m did not survive the encoding");
            }
        }

        [Test]
        public void TheOutwardNormalIsUnitLength_AndADegenerateSampleFallsBackToSouth()
        {
            var s = Sample(3f, -4f, 6f);
            Assert.AreEqual(1f, CliffWallGeometry.OutwardPlan(in s).magnitude, Eps);

            var degenerate = Sample(0f, 0f, 6f);
            Assert.AreEqual(Vector2.down, CliffWallGeometry.OutwardPlan(in degenerate),
                "a wall with no plan run has no derivable facing; south is the kit's canonical one");
        }

        // =========================================================================================
        //  5. subdivision and the chunk bound
        // =========================================================================================

        [Test]
        public void SubdivisionIsAtLeastOneRow_AndFineEnoughForTheProfile()
        {
            Assert.GreaterOrEqual(CliffWallGeometry.RowsFor(0f, 0.25f), 1,
                "a degenerate face must still yield a quad, not an empty mesh");
            Assert.AreEqual(36, CliffWallGeometry.RowsFor(9f, 0.25f),
                "9 m of surface at the kit's 0.25 m subdivision");
            Assert.GreaterOrEqual(CliffWallGeometry.RowsFor(9f, CliffCatalog.ProfileSubdivideMetres), 36);
        }

        /// <summary>
        /// ⭐ THE SORT POINT IS THE LOWEST TOE, and it must lie below every brow in the chunk — that is
        /// the whole correctness condition for giving a strip of wall ONE sorting order. Asserted on a
        /// chunk whose brow line CLIMBS across it, which is the case that breaks first.
        /// </summary>
        [Test]
        public void AChunkSortsAtItsLowestToe_WhichSitsBelowEveryBrowInIt()
        {
            var samples = new System.Collections.Generic.List<CliffWallSample>();
            for (int i = 0; i < 25; i++)
            {
                // 6 m of shore with the brow climbing a full metre per metre — the steepest the coast
                // can present, and the case the chunk length is bounded against.
                var brow = new Vector2(i * 0.25f, i * 0.25f);
                samples.Add(new CliffWallSample(brow, brow + new Vector2(0f, -1.75f), 6f));
            }

            float sortY = CliffWallGeometry.SortY(samples);
            foreach (CliffWallSample s in samples)
            {
                Assert.LessOrEqual(sortY, CliffWallGeometry.ToeScreen(in s).y + Eps,
                    "the sort point must be at or below every toe");
                Assert.Less(sortY, s.BrowPlan.y,
                    "the sort point must be below every BROW, or a clifftop walker sorts in front " +
                    "of the wall they are standing on");
            }

            // The slack the sort point has: how far below the brow the toe is DRAWN.
            Assert.AreEqual(6f * SpriteLightMath.HeightScale,
                            CliffWallGeometry.BrowToToeSeparationMetres(6f), Eps,
                "the separation is the drop's own projection — nothing else");
            Assert.AreEqual(0f, CliffWallGeometry.BrowToToeSeparationMetres(-1f), Eps,
                "a negative drop is not a face; it must not report negative slack");
        }

        [Test]
        public void SortYOfNothingIsZero_RatherThanFloatMaxValue()
        {
            Assert.AreEqual(0f, CliffWallGeometry.SortY(null));
            Assert.AreEqual(0f, CliffWallGeometry.SortY(new CliffWallSample[0]));
        }

        // =========================================================================================
        //  ⭐ 5. A CHUNK IS A SLICE OF A RUN — the two quantities the B3 gaps came from
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ <b>THE WRAP BELONGS TO THE CPU READ AND NOWHERE ELSE.</b> A run is a dozen tiles long, so
        /// its <c>u</c> runs well past 1; the GPU wraps a Repeat texture itself and interpolates
        /// 0.98 → 1.02 correctly across the period. Wrapping a mesh UV instead would emit 0.98 → 0.02 on
        /// two adjacent columns and run the whole texture BACKWARDS between them — a defect that reads as
        /// a mirrored stripe of rock rather than as a bug.
        ///
        /// <para>So this is asserted as the identity it is: wrapping lands in [0,1), is idempotent, and
        /// preserves the position within the period of any tile coordinate however large.</para>
        /// </summary>
        [Test]
        public void WrappingATileCoordinateKeepsItsPlaceInThePeriod_AndIsIdempotent()
        {
            foreach (float u in new[] { 0f, 0.25f, 0.999f, 1f, 1.25f, 12.03f, 137.5f })
            {
                float w = CliffWallGeometry.WrapTile01(u);
                Assert.That(w, Is.InRange(0f, 1f), $"u={u} wrapped outside [0,1]");
                Assert.AreEqual(w, CliffWallGeometry.WrapTile01(w), Eps, "wrapping is not idempotent");
                Assert.AreEqual(0f, Mathf.Repeat(u - w, 1f), 1e-3f,
                    $"u={u} wrapped to {w}, which is not the same point in the period");
            }

            // A negative tile cannot arise from an arc length, but a coordinate that slipped below zero
            // would otherwise read the texture's far edge — so the wrap is total, not a clamp.
            Assert.That(CliffWallGeometry.WrapTile01(-0.25f), Is.InRange(0f, 1f));
        }

        /// <summary>
        /// The row-count basis is the RUN's longest face, never the chunk's — so two chunks meeting at a
        /// shared station approximate their shared displaced edge with the same polyline. Measured before
        /// the fix: 26 of the coast's 76 cuts joined chunks with different row counts.
        /// </summary>
        [Test]
        public void TheRowsBasisIsTheLongestFaceInTheRun_SoEveryChunkOfItSubdividesAlike()
        {
            var run = new[] { Sample(0f, -2f, 4f), Sample(0f, -2f, 9f), Sample(0f, -2f, 3f) };
            float basis = CliffWallGeometry.RowsBasisSurfaceMetres(run);

            float longest = 0f;
            foreach (CliffWallSample s in run)
                longest = Mathf.Max(longest, CliffWallGeometry.SurfaceLengthMetres(in s));
            Assert.AreEqual(longest, basis, Eps, "the basis is not the longest face in the run");

            // …and a chunk holding only the SHORT stations re-derives a smaller number, which is exactly
            // why the builder has to carry the run's own basis across every cut instead of letting each
            // chunk work one out locally.
            Assert.Greater(basis, CliffWallGeometry.RowsBasisSurfaceMetres(new[] { run[0], run[2] }),
                "a sub-slice re-derives a smaller basis — the value must come from the RUN");

            Assert.AreEqual(0f, CliffWallGeometry.RowsBasisSurfaceMetres(null));
        }

        // =========================================================================================
        //  ⭐ 6. THE STRATA — a soil horizon is a DEPTH, and a face is measured along its surface
        // =========================================================================================

        /// <summary>
        /// ⭐ A vertical wall presents exactly its soil horizon; anything battered presents MORE of it,
        /// by <c>1/sin</c> — the same conversion the kit's README applies to a bank's height. Get it
        /// backwards and a shallow face would show a THINNER soil band than a vertical one, which is the
        /// opposite of what a slope does and would read as "the strata look wrong on the ramps".
        /// </summary>
        [Test]
        public void TheSoilHorizonPresentsMoreSurfaceOnAShallowerFace_ByOneOverSin()
        {
            const float Horizon = 1.8f;

            Assert.AreEqual(Horizon, CliffWallGeometry.OverburdenSurfaceMetres(Horizon, 90f), Eps,
                "a vertical wall presents its horizon at true depth — no conversion at all");

            float steep = CliffWallGeometry.OverburdenSurfaceMetres(Horizon, 76f);
            float ramp = CliffWallGeometry.OverburdenSurfaceMetres(Horizon, 62f);
            Assert.Greater(steep, Horizon, "a battered face presents MORE surface, not less");
            Assert.Greater(ramp, steep, "the shallower the face, the more of its surface the soil takes");
            Assert.AreEqual(Horizon / Mathf.Sin(62f * Mathf.Deg2Rad), ramp, Eps);

            // Degenerate inputs must not hand back an infinity that would place a band at NaN depth.
            Assert.AreEqual(0f, CliffWallGeometry.OverburdenSurfaceMetres(-1f, 90f), Eps,
                "a negative horizon is not a band");
            Assert.IsTrue(float.IsFinite(CliffWallGeometry.OverburdenSurfaceMetres(Horizon, 0f)),
                "a flat 'face' must still return a finite depth rather than dividing by zero");
        }

        // =========================================================================================
        //  ⭐ 7. THE DECALS — where the kit's own finishers sit against a face
        // =========================================================================================

        /// <summary>
        /// The brow strip STRADDLES the lip and its two halves are the whole strip: above the sod line it
        /// draws plan-view clifftop, below it the face. Split it any other way and the sod line — the one
        /// feature the strip exists to draw — lands somewhere that is not the brow.
        /// </summary>
        [Test]
        public void TheBrowStripStraddlesTheLip_AndItsTwoHalvesAreTheWholeStrip()
        {
            float h = CliffCatalog.StripMetresT, at = CliffCatalog.BrowLineAt;
            float inland = CliffWallGeometry.BrowDecalInlandMetres(h, at);
            float face = CliffWallGeometry.BrowDecalFaceMetres(h, at);

            Assert.AreEqual(h, inland + face, Eps,
                "the strip's plan half and its face half do not add up to the strip");
            Assert.AreEqual(h * at, inland, Eps,
                "the sod line does not sit where CliffCatalog.BrowLineAt says it does");
            Assert.Greater(face, inland,
                "the kit anchors its sod line above centre, so more of the strip hangs on the face than " +
                "lies on the clifftop — which is where the undercut shade and the soil wash are drawn");

            // The toe strip straddles nothing: it IS the last of the face, seated on the drawn toe.
            Assert.AreEqual(h, CliffWallGeometry.ToeDecalFaceMetres(h), Eps);
        }
    }
}
