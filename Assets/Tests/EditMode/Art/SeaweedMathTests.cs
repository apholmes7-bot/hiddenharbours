using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for <see cref="SeaweedMath"/> — the pure maths behind the drifting seaweed
    /// (owner ask: weed that drifts with the real sea, groups into clumps, and sticks on things).
    /// Every behaviour decision the <see cref="SeaweedPresenter"/> shell applies is pinned here: the
    /// drift composition (current + wind + trough-seek, clamped), the strand/refloat hysteresis, the
    /// buoy snag geometry, and the neighbour-merge pass (who absorbs whom, tier growth, dormancy).
    /// No Unity scene needed — pure, deterministic feel-math, the AmbientFleetSteering precedent.
    /// </summary>
    public class SeaweedMathTests
    {
        private const float Eps = 1e-4f;

        // ==== drift: the sea moves it, never a private random walk =====================================

        [Test]
        public void DriftVelocity_ComposesFlowWindAndTroughSeek()
        {
            // flow 0.2 east at response 1; wind 0.5 north at response 0.2 (=0.1 north);
            // slope +x 0.1 at seek 1 (=0.1 WEST — downslope, toward the trough).
            Vector2 v = SeaweedMath.DriftVelocity(
                new Vector2(0.2f, 0f), 1f,
                new Vector2(0f, 0.5f), 0.2f,
                new Vector2(0.1f, 0f), 1f, 10f);
            Assert.AreEqual(0.1f, v.x, Eps, "flow east minus downslope west");
            Assert.AreEqual(0.1f, v.y, Eps, "wind carry north");
        }

        [Test]
        public void DriftVelocity_SlidesDownTheWaveSlope_TowardTheTrough()
        {
            // The convergence term: the surface gradient points UP toward the crest, weed slides DOWN.
            Vector2 v = SeaweedMath.DriftVelocity(Vector2.zero, 1f, Vector2.zero, 1f,
                                                  new Vector2(0.3f, -0.2f), 0.5f, 10f);
            Assert.Less(v.x, 0f, "slope +x → drift -x (downhill)");
            Assert.Greater(v.y, 0f, "slope -y → drift +y (downhill)");
        }

        [Test]
        public void DriftVelocity_IsClampedToMaxSpeed()
        {
            Vector2 v = SeaweedMath.DriftVelocity(new Vector2(9f, 0f), 1f, Vector2.zero, 0f,
                                                  Vector2.zero, 0f, 0.4f);
            Assert.AreEqual(0.4f, v.magnitude, Eps, "a gale can't fling the wrack across the harbour");
            Assert.Greater(v.x, 0f, "clamp keeps the direction");
        }

        [Test]
        public void DriftVelocity_DeadCalm_IsZero()
        {
            Vector2 v = SeaweedMath.DriftVelocity(Vector2.zero, 1f, Vector2.zero, 1f,
                                                  Vector2.zero, 1f, 0.4f);
            Assert.AreEqual(0f, v.magnitude, Eps);
        }

        // ==== stranding: beach on the ebb, refloat on the flood, never flicker =========================

        [Test]
        public void NextStranded_FloatingPiece_StrandsAtTheStrandDepth()
        {
            Assert.IsFalse(SeaweedMath.NextStranded(false, 0.5f, 0.08f, 0.25f), "deep water — floats");
            Assert.IsFalse(SeaweedMath.NextStranded(false, 0.09f, 0.08f, 0.25f), "just above the gate");
            Assert.IsTrue(SeaweedMath.NextStranded(false, 0.08f, 0.08f, 0.25f), "at the gate — beached");
            Assert.IsTrue(SeaweedMath.NextStranded(false, -0.3f, 0.08f, 0.25f), "dry ground — beached");
        }

        [Test]
        public void NextStranded_StrandedPiece_NeedsTheRefloatDepth_TheHysteresisGap()
        {
            Assert.IsTrue(SeaweedMath.NextStranded(true, 0.1f, 0.08f, 0.25f),
                          "above the strand depth but below the refloat depth — still beached (no waterline flicker)");
            Assert.IsFalse(SeaweedMath.NextStranded(true, 0.25f, 0.08f, 0.25f), "the flood reached it — refloats");
        }

        // ==== snagging on the player's gear =============================================================

        [Test]
        public void NearestWithin_FindsTheClosestBuoyInReach_OrMinusOne()
        {
            var buoys = new[] { new Vector2(5f, 0f), new Vector2(1.5f, 0f), new Vector2(0f, 4f) };
            Assert.AreEqual(1, SeaweedMath.NearestWithin(Vector2.zero, buoys, buoys.Length, 2f));
            Assert.AreEqual(-1, SeaweedMath.NearestWithin(Vector2.zero, buoys, buoys.Length, 1f), "nothing in reach");
            Assert.AreEqual(-1, SeaweedMath.NearestWithin(Vector2.zero, buoys, 1, 2f), "only the packed count is live");
        }

        [Test]
        public void SnagAnchor_RestsOnTheRim_AlongTheDriftInDirection()
        {
            Vector2 a = SeaweedMath.SnagAnchor(new Vector2(2f, 0f), Vector2.zero, 0.35f);
            Assert.AreEqual(0.35f, a.x, Eps, "on the rest rim, on the side it drifted in from");
            Assert.AreEqual(0f, a.y, Eps);
        }

        [Test]
        public void SnagAnchor_DegeneratePieceOnTheBuoy_NeverNaN()
        {
            Vector2 a = SeaweedMath.SnagAnchor(Vector2.zero, Vector2.zero, 0.35f);
            Assert.IsFalse(float.IsNaN(a.x) || float.IsNaN(a.y));
            Assert.AreEqual(0.35f, a.magnitude, Eps, "still rests on the rim");
        }

        // ==== bounds recycling ==========================================================================

        [Test]
        public void OutsideBounds_PadsTheBedRect()
        {
            var bed = new Rect(-10f, -10f, 20f, 20f);
            Assert.IsFalse(SeaweedMath.OutsideBounds(new Vector2(11f, 0f), bed, 2f), "inside the padding");
            Assert.IsTrue(SeaweedMath.OutsideBounds(new Vector2(13f, 0f), bed, 2f), "past the padding");
            Assert.IsTrue(SeaweedMath.OutsideBounds(new Vector2(0f, -12.5f), bed, 2f));
        }

        // ==== the wave-borne look =======================================================================

        [Test]
        public void BobOffset_IsLinearAndCapped()
        {
            Assert.AreEqual(0.1f, SeaweedMath.BobOffset(0.5f, 0.2f, 0.35f), Eps);
            Assert.AreEqual(0.35f, SeaweedMath.BobOffset(9f, 0.2f, 0.35f), Eps, "capped up");
            Assert.AreEqual(-0.35f, SeaweedMath.BobOffset(-9f, 0.2f, 0.35f), Eps, "capped down");
        }

        [Test]
        public void Wobble_ScalesWithTheEnvelope_AndIsStillOnGlass()
        {
            Assert.AreEqual(0f, SeaweedMath.Wobble(0.3f, 0f, 9f), Eps, "GLASS IS SACRED — still wrack on a mirror");
            Assert.AreEqual(4.5f, SeaweedMath.Wobble(0.25f, 0.5f, 9f), Eps, "half the envelope → half the rock");
            Assert.AreEqual(9f, SeaweedMath.Wobble(2f, 0.5f, 9f), Eps, "clamped at the max");
            Assert.AreEqual(-9f, SeaweedMath.Wobble(-2f, 0.5f, 9f), Eps);
        }

        // ==== seeded placement (deterministic, never System.Random) ====================================

        [Test]
        public void SpawnPoint_LandsInsideTheBed_AndIsDeterministic()
        {
            uint seed = SeaweedMath.BedSeed(12345, "decor.seaweed_st_peters");
            var bed = new Rect(-40f, -43f, 90f, 26f);
            for (int i = 0; i < 32; i++)
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector2 p = SeaweedMath.SpawnPoint(seed, i, attempt, bed);
                Assert.IsTrue(bed.Contains(p), $"piece {i} attempt {attempt} landed at {p}, outside {bed}");
                Assert.AreEqual(p, SeaweedMath.SpawnPoint(seed, i, attempt, bed), "same key → same spot");
            }
        }

        [Test]
        public void BedSeed_SeparatesWorldsAndBeds()
        {
            Assert.AreNotEqual(SeaweedMath.BedSeed(1, "decor.a"), SeaweedMath.BedSeed(2, "decor.a"));
            Assert.AreNotEqual(SeaweedMath.BedSeed(1, "decor.a"), SeaweedMath.BedSeed(1, "decor.b"));
            Assert.AreEqual(SeaweedMath.BedSeed(7, "decor.a"), SeaweedMath.BedSeed(7, "decor.a"));
        }

        [Test]
        public void SpawnPoint_RejectedAttemptHashesToAFreshSpot()
        {
            uint seed = SeaweedMath.BedSeed(99, "decor.x");
            var bed = new Rect(0f, 0f, 50f, 50f);
            Assert.AreNotEqual(SeaweedMath.SpawnPoint(seed, 3, 0, bed),
                               SeaweedMath.SpawnPoint(seed, 3, 1, bed));
        }

        // ==== clumping: the neighbour merge =============================================================

        private static (Vector2[] pos, byte[] state, int[] tier, int[] absorbedBy) Bed(params Vector2[] positions)
        {
            var state = new byte[positions.Length];      // all StateDrifting (0)
            var tier = new int[positions.Length];
            var absorbedBy = new int[positions.Length];
            return (positions, state, tier, absorbedBy);
        }

        [Test]
        public void MergePass_TwoNearbyDrifters_BecomeOneBiggerClump()
        {
            var (pos, state, tier, absorbedBy) = Bed(new Vector2(0f, 0f), new Vector2(0.5f, 0f));
            int merges = SeaweedMath.MergePass(pos, state, tier, 2, 0.8f, 2, absorbedBy);

            Assert.AreEqual(1, merges);
            Assert.AreEqual(SeaweedMath.StateDrifting, state[0], "the lower index absorbs on a tie");
            Assert.AreEqual(1, tier[0], "the absorber grew a size tier");
            Assert.AreEqual(SeaweedMath.StateDormant, state[1], "the absorbed piece went dormant (pool-friendly)");
            Assert.AreEqual(0, absorbedBy[1], "absorbed by piece 0");
            Assert.AreEqual(-1, absorbedBy[0]);
        }

        [Test]
        public void MergePass_OutOfReach_NothingHappens()
        {
            var (pos, state, tier, absorbedBy) = Bed(new Vector2(0f, 0f), new Vector2(2f, 0f));
            Assert.AreEqual(0, SeaweedMath.MergePass(pos, state, tier, 2, 0.8f, 2, absorbedBy));
            Assert.AreEqual(SeaweedMath.StateDrifting, state[1]);
            Assert.AreEqual(0, tier[0]);
        }

        [Test]
        public void MergePass_BiggerTierAbsorbs_AndGrowthCapsAtMaxTier()
        {
            var (pos, state, tier, absorbedBy) = Bed(new Vector2(0f, 0f), new Vector2(0.5f, 0f));
            tier[1] = 2;
            int merges = SeaweedMath.MergePass(pos, state, tier, 2, 0.8f, 2, absorbedBy);

            Assert.AreEqual(1, merges);
            Assert.AreEqual(SeaweedMath.StateDormant, state[0], "the small piece feeds the big clump");
            Assert.AreEqual(SeaweedMath.StateDrifting, state[1]);
            Assert.AreEqual(2, tier[1], "already at the top tier — growth caps, never overflows the ladder");
        }

        [Test]
        public void MergePass_AnchoredClumpCollectsTheDrifter_AndNeverMoves()
        {
            var (pos, state, tier, absorbedBy) = Bed(new Vector2(0f, 0f), new Vector2(0.5f, 0f));
            state[0] = SeaweedMath.StateSnagged;
            Vector2 anchor = pos[0];

            int merges = SeaweedMath.MergePass(pos, state, tier, 2, 0.8f, 2, absorbedBy);

            Assert.AreEqual(1, merges);
            Assert.AreEqual(SeaweedMath.StateSnagged, state[0], "the snag holds — and grew");
            Assert.AreEqual(1, tier[0]);
            Assert.AreEqual(anchor, pos[0], "the anchor point never moves");
            Assert.AreEqual(SeaweedMath.StateDormant, state[1]);
        }

        [Test]
        public void MergePass_TwoAnchoredPieces_NeverMerge()
        {
            var (pos, state, tier, absorbedBy) = Bed(new Vector2(0f, 0f), new Vector2(0.5f, 0f));
            state[0] = SeaweedMath.StateSnagged;
            state[1] = SeaweedMath.StateStranded;
            Assert.AreEqual(0, SeaweedMath.MergePass(pos, state, tier, 2, 0.8f, 2, absorbedBy),
                            "each is stuck to its own thing — anchored wrack doesn't crawl together");
        }

        [Test]
        public void MergePass_DormantPiecesAreInvisibleToTheMerge()
        {
            var (pos, state, tier, absorbedBy) = Bed(new Vector2(0f, 0f), new Vector2(0.5f, 0f));
            state[1] = SeaweedMath.StateDormant;
            Assert.AreEqual(0, SeaweedMath.MergePass(pos, state, tier, 2, 0.8f, 2, absorbedBy));
        }

        [Test]
        public void MergePass_ThreeConverged_ChainIntoOneClump()
        {
            var (pos, state, tier, absorbedBy) = Bed(
                new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f));
            int merges = SeaweedMath.MergePass(pos, state, tier, 3, 0.8f, 2, absorbedBy);

            Assert.AreEqual(2, merges, "the trough gathered all three");
            Assert.AreEqual(SeaweedMath.StateDrifting, state[0]);
            Assert.AreEqual(2, tier[0], "grew twice");
            Assert.AreEqual(SeaweedMath.StateDormant, state[1]);
            Assert.AreEqual(SeaweedMath.StateDormant, state[2]);
        }
    }
}
