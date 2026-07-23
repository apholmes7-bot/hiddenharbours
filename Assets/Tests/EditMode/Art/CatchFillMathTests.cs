using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The container-fill recipe's owner-visible contracts: SEEDED DETERMINISM (same seed in, the
    /// same heap out, every session — rule 5 applied to the look), MONOTONICITY (a growing fill
    /// never moves or re-dresses the catch already showing) and capacity-aware heaping. These are
    /// pure-math tests; byte-level parity with the live rig's <c>fillItems</c> is pinned
    /// separately in <c>CatchStorageBakeTests</c> (RigBaking suite, V8 host).
    /// </summary>
    public class CatchFillMathTests
    {
        private static readonly string[] UniformKinds =
            { "cod", "haddock", "pollock", "mackerel", "lobster", "crab", "mussel", "clam" };

        // ---- determinism ----------------------------------------------------------------------

        [Test]
        public void FillItems_SameSeed_SameHeap_EveryTime()
        {
            foreach (string kind in UniformKinds)
            {
                var a = CatchFillMath.FillItems(kind, CatchFillBand.Full, seed: 1234, capacity: 32);
                var b = CatchFillMath.FillItems(kind, CatchFillBand.Full, seed: 1234, capacity: 32);
                Assert.AreEqual(a.Count, b.Count, kind);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(a[i].Kind, b[i].Kind, $"{kind}[{i}]");
                    Assert.AreEqual(a[i].Variant, b[i].Variant, $"{kind}[{i}]");
                    Assert.AreEqual(a[i].Scale, b[i].Scale, $"{kind}[{i}]");
                }
            }
        }

        [Test]
        public void FillItems_DifferentSeeds_DifferentHeaps()
        {
            var a = CatchFillMath.FillItems("cod", CatchFillBand.Full, seed: 1, capacity: 32);
            var b = CatchFillMath.FillItems("cod", CatchFillBand.Full, seed: 2, capacity: 32);
            // Not a certainty pixel-by-pixel, but 27 items × (variant + scale) matching across two
            // seeds would mean the seed is dead — assert at least one differs.
            bool anyDiff = a.Where((t, i) => t.Variant != b[i].Variant || t.Scale != b[i].Scale).Any();
            Assert.IsTrue(anyDiff, "two different seeds produced identical heaps — is the seed wired?");
        }

        [Test]
        public void FillItems_SeedZero_FallsToTheRigsDefaultSeven()
        {
            // catchKit.js: mulberry((seed||7)*...) — seed 0 and seed 7 are the same stream.
            var zero = CatchFillMath.FillItems("crab", CatchFillBand.Half, seed: 0, capacity: 32);
            var seven = CatchFillMath.FillItems("crab", CatchFillBand.Half, seed: 7, capacity: 32);
            CollectionAssert.AreEqual(zero.Select(i => i.Variant).ToArray(),
                                      seven.Select(i => i.Variant).ToArray());
        }

        // ---- monotonicity: THE owner-visible contract ------------------------------------------

        [Test]
        public void FillItems_GrowingTheBand_NeverChangesEarlierItems()
        {
            var bands = new[] { CatchFillBand.Few, CatchFillBand.Half, CatchFillBand.Full, CatchFillBand.Brim };
            foreach (string kind in UniformKinds.Concat(new[] { "mixed" }))
            {
                List<CatchFillItem> previous = null;
                foreach (var band in bands)
                {
                    var current = CatchFillMath.FillItems(kind, band, seed: 42, capacity: 32);
                    if (previous != null)
                    {
                        Assert.GreaterOrEqual(current.Count, previous.Count, $"{kind}: {band} shrank");
                        for (int i = 0; i < previous.Count; i++)
                        {
                            Assert.AreEqual(previous[i].Kind, current[i].Kind,
                                $"{kind}: item {i} changed KIND when the fill grew to {band} — " +
                                "the catch already showing must never transform");
                            Assert.AreEqual(previous[i].Variant, current[i].Variant,
                                $"{kind}: item {i} changed lay when the fill grew to {band}");
                            Assert.AreEqual(previous[i].Scale, current[i].Scale,
                                $"{kind}: item {i} changed scale when the fill grew to {band}");
                        }
                    }
                    previous = current;
                }
            }
        }

        [Test]
        public void AppearancesFor_AppendingCatch_NeverRedressesEarlierItems()
        {
            var few = new List<string> { "cod", "lobster", "mussel" };
            var more = new List<string>(few) { "crab", "cod", "clam", "haddock" };

            var a = new List<CatchFillItem>();
            var b = new List<CatchFillItem>();
            CatchFillMath.AppearancesFor(few, seed: 9, a);
            CatchFillMath.AppearancesFor(more, seed: 9, b);

            Assert.AreEqual(few.Count, a.Count);
            Assert.AreEqual(more.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind, $"item {i}");
                Assert.AreEqual(a[i].Variant, b[i].Variant, $"item {i} re-dressed after more catch landed");
                Assert.AreEqual(a[i].Scale, b[i].Scale, $"item {i} re-scaled after more catch landed");
            }
        }

        [Test]
        public void AppearancesFor_SharesTheFillItemsStream()
        {
            // The two paths must dress item i identically for a uniform catch — one seeded truth.
            var kinds = Enumerable.Repeat("pollock", 10).ToList();
            var viaList = new List<CatchFillItem>();
            CatchFillMath.AppearancesFor(kinds, seed: 5, viaList);
            var viaBand = CatchFillMath.FillItems("pollock", CatchFillBand.Brim, seed: 5, capacity: 10);

            Assert.AreEqual(viaBand.Count, viaList.Count);
            for (int i = 0; i < viaList.Count; i++)
            {
                Assert.AreEqual(viaBand[i].Variant, viaList[i].Variant, $"item {i}");
                Assert.AreEqual(viaBand[i].Scale, viaList[i].Scale, $"item {i}");
            }
        }

        // ---- the item recipe -------------------------------------------------------------------

        [Test]
        public void Items_VariantsAndScales_StayInTheRigsRanges()
        {
            foreach (string kind in UniformKinds.Concat(new[] { "mixed" }))
            {
                foreach (var item in CatchFillMath.FillItems(kind, CatchFillBand.Brim, seed: 77, capacity: 32))
                {
                    Assert.That(item.Variant, Is.InRange(0, CatchFillMath.Variants - 1), kind);
                    if (item.Kind == "mussel" || item.Kind == "clam")
                        Assert.AreEqual(1f, item.Scale, $"{kind}: shellfish lie flat at scale 1");
                    else
                        Assert.That(item.Scale, Is.InRange(0.82f, 1.12f), kind);
                }
            }
        }

        [Test]
        public void MixedCatch_DrawsOnlyFromTheRigsPool()
        {
            string[] pool = { "mackerel", "haddock", "lobster", "crab", "pollock", "cod" };
            foreach (var item in CatchFillMath.FillItems("mixed", CatchFillBand.Brim, seed: 3, capacity: 32))
                CollectionAssert.Contains(pool, item.Kind);
        }

        // ---- counts & capacity -----------------------------------------------------------------

        [Test]
        public void ItemCount_UsesTheBandFractions_HalfUpLikeTheRig()
        {
            // frac × capacity, JS Math.round: full (0.85) × 32 = 27.2 → 27; half (0.55) × 32 =
            // 17.6 → 18; few (0.25) × 18 = 4.5 → rounds UP to 5 (banker's rounding would say 4).
            Assert.AreEqual(0, CatchFillMath.ItemCount("cod", CatchFillBand.Empty, 32));
            Assert.AreEqual(8, CatchFillMath.ItemCount("cod", CatchFillBand.Few, 32));
            Assert.AreEqual(18, CatchFillMath.ItemCount("cod", CatchFillBand.Half, 32));
            Assert.AreEqual(27, CatchFillMath.ItemCount("cod", CatchFillBand.Full, 32));
            Assert.AreEqual(32, CatchFillMath.ItemCount("cod", CatchFillBand.Brim, 32));
            Assert.AreEqual(5, CatchFillMath.ItemCount("cod", CatchFillBand.Few, 18));
        }

        [Test]
        public void ItemCount_WithoutCapacity_FallsToTheKindsBudget()
        {
            // MAXN: cod 18 → brim 18; unknown kind → the rig's default 8.
            Assert.AreEqual(18, CatchFillMath.ItemCount("cod", CatchFillBand.Brim));
            Assert.AreEqual(24, CatchFillMath.ItemCount("mackerel", CatchFillBand.Brim));
            Assert.AreEqual(22, CatchFillMath.ItemCount("mixed", CatchFillBand.Brim));
            Assert.AreEqual(8, CatchFillMath.ItemCount("sea_monster", CatchFillBand.Brim));
        }

        [Test]
        public void ItemCount_ZeroCapacity_DrawsNothing()
        {
            Assert.AreEqual(0, CatchFillMath.ItemCount("cod", CatchFillBand.Brim, 0));
            Assert.AreEqual(0, CatchFillMath.FillItems("cod", CatchFillBand.Brim, 1, 0).Count);
        }

        // ---- VisibleCount: the continuous-fill mapping -----------------------------------------

        [Test]
        public void VisibleCount_PinsTheEnds_AndFloorsAtOne()
        {
            Assert.AreEqual(0, CatchFillMath.VisibleCount(0.0, 32), "empty shows nothing");
            Assert.AreEqual(0, CatchFillMath.VisibleCount(-1.0, 32));
            Assert.AreEqual(1, CatchFillMath.VisibleCount(0.001, 32), "any catch at all must show");
            Assert.AreEqual(32, CatchFillMath.VisibleCount(1.0, 32), "full heaps every slot");
            Assert.AreEqual(32, CatchFillMath.VisibleCount(2.0, 32));
            Assert.AreEqual(0, CatchFillMath.VisibleCount(0.5, 0), "no slots, nothing to draw");
        }

        [Test]
        public void VisibleCount_IsMonotonic_AcrossTheWholeRange()
        {
            int previous = 0;
            for (int step = 0; step <= 1000; step++)
            {
                int n = CatchFillMath.VisibleCount(step / 1000.0, 32);
                Assert.GreaterOrEqual(n, previous, $"fill {step / 1000.0}: count shrank");
                previous = n;
            }
            Assert.AreEqual(32, previous);
        }

        // ---- BandFor: the baked-fill-state mapping (buckets) -----------------------------------

        [Test]
        public void BandFor_PinsEmptyAndBrim_ToTheTruth()
        {
            Assert.AreEqual(CatchFillBand.Empty, CatchFillMath.BandFor(0.0, usedUnits: 0));
            Assert.AreEqual(CatchFillBand.Empty, CatchFillMath.BandFor(0.4, usedUnits: 0),
                "no catch aboard can never read as filled");
            Assert.AreEqual(CatchFillBand.Brim, CatchFillMath.BandFor(1.0, usedUnits: 20));
            Assert.AreNotEqual(CatchFillBand.Empty, CatchFillMath.BandFor(0.01, usedUnits: 1),
                "one banded keeper always shows");
            Assert.AreNotEqual(CatchFillBand.Brim, CatchFillMath.BandFor(0.99, usedUnits: 19),
                "only a truly full hold reads brim");
        }

        [Test]
        public void BandFor_PartialsReadTheNearestBand()
        {
            Assert.AreEqual(CatchFillBand.Few, CatchFillMath.BandFor(0.25, 5));
            Assert.AreEqual(CatchFillBand.Half, CatchFillMath.BandFor(0.55, 11));
            Assert.AreEqual(CatchFillBand.Full, CatchFillMath.BandFor(0.85, 17));
            Assert.AreEqual(CatchFillBand.Few, CatchFillMath.BandFor(0.30, 6));
            Assert.AreEqual(CatchFillBand.Half, CatchFillMath.BandFor(0.50, 10));
            Assert.AreEqual(CatchFillBand.Full, CatchFillMath.BandFor(0.95, 19));
        }
    }
}
