using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ADR 0059: a hull sorts among the scenery by where she lies. The owner saw rockweed, rocks and grass
    /// draw over a Cape Islander run aground at St Peters ("it seem that sprites appear above the boat when
    /// they should be below/behind"), because every hull sorted at a fixed 1, under the whole decor band.
    /// These pin the Core rule the boat's sorting group will use: the decor band's Y-sort at her waterline
    /// pivot, one rung behind, and the owner's three calls of 2026-11-17 (accept the ADR, covered weed
    /// under the sea, the sort point is the waterline pivot).
    /// </summary>
    public sealed class HullSortingTests
    {
        /// <summary>Where the owner's Cape Islander lay aground on the St Peters shoal.</summary>
        static readonly Vector2 Aground = new Vector2(189.67f, -55.88f);

        /// <summary>A Cape Islander's length and half beam, near enough.</summary>
        const float LengthM = 12.9f, HalfBeamM = 2.4f;

        /// <summary>The order a rock (a static <c>YSortSprite</c>) standing at this Y sorts at.</summary>
        static int RockAt(float y) => YSortSprite.OrderFor(y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                                           SortingBands.DecorFloor, SortingBands.DecorCeiling);

        /// <summary>The order the grass-field chunk holding a tuft at this Y sorts at.</summary>
        static int GrassAt(float y, int rowOrderSteps) =>
            RockAt(GrassField.RowAnchorY(GrassField.RowOf(y, rowOrderSteps), rowOrderSteps));

        [Test] public void TheHullYieldsExactlyOneRung_TheVehicleRule()
        {
            Assert.AreEqual(1, SortingBands.HullYieldRungs);
        }

        [Test] public void TheOrder_IsTheDecorBandsYSortAtHerPivot_OneRungBehind()
        {
            foreach (float y in new[] { -250f, -55.88f, -0.13f, 0f, 0.125f, 3.3f, 120f, 250f })
                Assert.AreEqual(RockAt(y) - SortingBands.HullYieldRungs, HullSorting.OrderAt(y), $"y = {y}");
        }

        [Test] public void TheRule_IsTheMeshVehiclesRule_HeldOnce()
        {
            for (float y = -299f; y <= 299f; y += 0.0937f)
                Assert.AreEqual(VehicleMeshDriver.OrderAt(y), HullSorting.OrderAt(y), $"y = {y}");
        }

        [Test] public void ARockNorthOfHerPivot_SortsUnderHer_OneSouth_SortsOverHer()
        {
            int hull = HullSorting.OrderAt(Aground.y);
            Assert.Less(RockAt(Aground.y + 0.5f), hull, "a rock half a metre north (behind her) draws under her");
            Assert.Less(RockAt(Aground.y + 6f), hull, "a rock by her bow, up the screen, draws under her");
            Assert.Greater(RockAt(Aground.y - 0.5f), hull, "a rock half a metre south (nearer the camera) draws over her");
            Assert.Greater(RockAt(Aground.y), hull,
                "a rock exactly level with her pivot draws over her: she yields the rung");
        }

        [Test] public void AGrassTuftNorthOfHerPivot_SortsUnderHer_OneSouth_SortsOverHer()
        {
            int hull = HullSorting.OrderAt(Aground.y);
            Assert.Less(GrassAt(Aground.y + 0.5f, 1), hull, "one order per row");
            Assert.Greater(GrassAt(Aground.y - 0.5f, 1), hull, "one order per row");
            // A coarser chunk row is off by at most half a row (two orders at four steps, half a metre).
            Assert.Less(GrassAt(Aground.y + 1.5f, 4), hull, "four orders per row");
            Assert.Greater(GrassAt(Aground.y - 1.5f, 4), hull, "four orders per row");
        }

        [Test] public void TwoHulls_SortByWhereTheyLie_TheyNoLongerTie()
        {
            int north = HullSorting.OrderAt(10f);
            int south = HullSorting.OrderAt(4f);
            Assert.Less(north, south, "the hull further up the screen draws first, under the nearer one");
        }

        [Test] public void TheSortPoint_IsHerWaterlinePivot_WhateverHerHeadingOrLength()
        {
            var northbound = HullFootprint.FromHeading(Aground, 0f, LengthM, HalfBeamM);
            Assert.AreEqual(Aground.y, HullSorting.SortPointY(northbound), 1e-5f);
            int expected = HullSorting.OrderAt(Aground.y);
            foreach (float heading in new[] { 0f, 37f, 90f, 180f, 211f, 270f, 359f })
            {
                var lying = HullFootprint.FromHeading(Aground, heading, LengthM, HalfBeamM);
                Assert.AreEqual(Aground.y, HullSorting.SortPointY(lying), 1e-5f, $"heading {heading}");
                Assert.AreEqual(expected, HullSorting.OrderFor(lying), $"heading {heading}");
            }
            var seiner = HullFootprint.FromHeading(Aground, 211f, 27f, 4f);
            var dory = HullFootprint.FromHeading(Aground, 211f, 4.5f, 0.8f);
            Assert.AreEqual(expected, HullSorting.OrderFor(seiner), "a 27 m seiner on the same pivot");
            Assert.AreEqual(expected, HullSorting.OrderFor(dory), "a dory on the same pivot");
        }

        [Test] public void MovingHerPivotAQuarterMetreSouth_MovesHerExactlyOneRungForward()
        {
            for (float y = -40f; y <= 40f; y += 1f)
                Assert.AreEqual(HullSorting.OrderAt(y) + 1, HullSorting.OrderAt(y - 1f / SortingBands.OrdersPerMetre),
                                $"y = {y}");
        }

        [Test] public void SheStaysInsideTheDecorBand_EvenUnplaced()
        {
            foreach (float y in new[] { -1e6f, -301f, 301f, 1e6f, float.NaN,
                                        float.PositiveInfinity, float.NegativeInfinity })
            {
                int order = HullSorting.OrderAt(y);
                Assert.GreaterOrEqual(order, SortingBands.DecorFloor, $"y = {y}");
                Assert.LessOrEqual(order, SortingBands.DecorCeiling, $"y = {y}");
            }
            Assert.AreEqual(HullSorting.OrderAt(0f), HullSorting.OrderAt(float.NaN), "unplaced sorts mid-band");
            Assert.AreEqual(HullSorting.OrderAt(0f), HullSorting.OrderAt(float.NegativeInfinity), "unplaced sorts mid-band");
        }

        [Test] public void CoveredWeed_LiesUnderTheSea_AndUnderEveryHull()
        {
            // The owner, 2026-11-17: weed under a hull is covered, under the sea surface.
            Assert.Less(SortingBands.UnderHulls, SortingBands.Sea, "lying weed sits under the sea plane");
            foreach (float y in new[] { -250f, Aground.y, 0f, 120f, 250f })
            {
                int weed = PlantHullSorting.OrderFor(y, submerged: false, SortingBands.SubmergedShorePlant,
                                                     liesFlat: true, behindAHull: false);
                Assert.AreEqual(SortingBands.UnderHulls, weed, $"y = {y}");
                Assert.Less(weed, HullSorting.OrderAt(y), $"weed at y = {y} is under a hull lying there");
                Assert.Less(weed, HullSorting.OrderAt(y + 20f), $"weed at y = {y} is under a hull north of it");
            }
        }

        // ---- step 3: a situational rule is an input, not a second writer ---------------------------

        sealed class FixedInput : IHullSortingInput
        {
            public bool Applies; public int Order; public Vector2 AskedAt;
            public bool TryOrderAt(Vector2 at, out int order) { AskedAt = at; order = Order; return Applies; }
        }

        [Test] public void WithNoRule_OrSilentRule_SheSortsByWhereSheLies()
        {
            var at = new Vector2(12f, Aground.y);
            Assert.AreEqual(HullSorting.OrderAt(at.y), HullSorting.OrderFor(at, null), "no rule");
            var silent = new FixedInput { Applies = false, Order = -1 };
            Assert.AreEqual(HullSorting.OrderAt(at.y), HullSorting.OrderFor(at, silent), "a rule that stands aside");
            Assert.AreEqual(at, silent.AskedAt, "the rule is asked at her waterline pivot");
        }

        [Test] public void ARuleThatApplies_DecidesHerOrder()
        {
            var behind = new FixedInput { Applies = true, Order = -1 };
            Assert.AreEqual(-1, HullSorting.OrderFor(new Vector2(0f, 30f), behind));
        }

        // ---- the tide moves the sort point, by the quantised step -----------------------------------

        [Test] public void NoTide_LeavesTheSortPointAtTheRoot()
        {
            var root = new Vector2(189.67f, -55.88f);
            Assert.AreEqual(root, HullSorting.SortPoint(root, 0f));
            Assert.AreEqual(HullSorting.OrderAt(root.y), HullSorting.OrderFor(HullSorting.SortPoint(root, 0f), null));
            Assert.AreEqual(root, HullSorting.SortPoint(root, float.NaN), "a non-finite rise is no rise");
            Assert.AreEqual(root, HullSorting.SortPoint(root, float.PositiveInfinity));
        }

        [Test] public void TheTidesRise_MovesHerKey_ByOneRungPerQuarterMetre()
        {
            var root = new Vector2(0f, 1.1f);   // clear of a rounding edge either way
            int atRoot = HullSorting.OrderAt(root.y);
            foreach (float rise in new[] { 0.25f, 1f, -1f, 2.5f, -4f })
            {
                int steps = Mathf.RoundToInt(rise * SortingBands.OrdersPerMetre);
                Assert.AreEqual(atRoot - steps, HullSorting.OrderFor(HullSorting.SortPoint(root, rise), null),
                    $"a rise of {rise} m moves her {steps} rungs (up-screen = further back)");
            }
            Assert.AreEqual(root.x, HullSorting.SortPoint(root, 3f).x, "the tide never moves her sideways");
        }

        [Test] public void TheRuleIsAskedAtTheRisenPoint()
        {
            var rule = new FixedInput { Applies = false };
            var root = new Vector2(4f, -2f);
            HullSorting.OrderFor(HullSorting.SortPoint(root, 1.5f), rule);
            Assert.AreEqual(new Vector2(4f, -0.5f), rule.AskedAt);
        }
    }
}
