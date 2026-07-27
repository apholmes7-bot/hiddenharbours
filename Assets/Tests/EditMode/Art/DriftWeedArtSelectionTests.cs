using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The PURE half of wiring the painted drift weed: which clump a piece wears for a given tier.
    /// No assets, no scene — <see cref="SeaweedMath.PickWeedArt"/> and
    /// <see cref="SeaweedMath.PickRamp"/> over plain arrays, the same way the drift/merge maths is
    /// tested.
    ///
    /// <para>The behaviour that matters and why: the kit's clumps are drawn at 0.45–2.0 m, so the
    /// presenter must PICK the clump nearest the tier it wants and draw it at scale 1. If selection
    /// ever returned an arbitrary clump the presenter would be back to resampling pixel art, which is
    /// the exact thing this wiring exists to avoid.</para>
    /// </summary>
    public class DriftWeedArtSelectionTests
    {
        // The real kit's shape: 4 species × 3–4 variants × 3 ramp rows. Sizes are the sidecar's.
        static readonly float[] Bladderwrack = { 0.9f, 1.25f, 0.6f, 1.05f };
        static readonly float[] SugarKelp = { 1.6f, 2.0f, 1.1f };
        static readonly float[] Eelgrass = { 0.7f, 0.9f, 0.45f, 0.8f };
        static readonly float[] TornMat = { 1.4f, 2.0f, 1.15f };
        static readonly float[][] Kit = { Bladderwrack, SugarKelp, Eelgrass, TornMat };
        const int RampRows = 3;

        struct Flat
        {
            public float[] Sizes;
            public int[] Species;
            public int[] Ramps;
            public int Count;
        }

        /// <summary>Flattens the kit exactly as <c>DriftWeedKitBuilder</c> does: ramp-major per species.</summary>
        static Flat BuildFlat()
        {
            var sizes = new List<float>();
            var species = new List<int>();
            var ramps = new List<int>();
            for (int s = 0; s < Kit.Length; s++)
            for (int r = 0; r < RampRows; r++)
            foreach (float m in Kit[s])
            {
                sizes.Add(m); species.Add(s); ramps.Add(r);
            }
            return new Flat
            {
                Sizes = sizes.ToArray(), Species = species.ToArray(),
                Ramps = ramps.ToArray(), Count = sizes.Count,
            };
        }

        [Test]
        public void TheFlattenedKit_IsTheShapeTheSidecarDeclares()
        {
            var f = BuildFlat();
            Assert.AreEqual((4 + 3 + 4 + 3) * RampRows, f.Count, "14 variants × 3 ramp rows = 42 clumps.");
            Assert.AreEqual(0.45f, f.Sizes.Min(), 1e-5f);
            Assert.AreEqual(2.0f, f.Sizes.Max(), 1e-5f);
        }

        // ---- the core promise: nearest drawn size, so the presenter never has to rescale ----------

        [Test]
        public void PickWeedArt_ChoosesAClumpDrawnNearTheTierFootprint_ForEveryTierInTheLadder()
        {
            var f = BuildFlat();
            // The shipped ladder on StPetersSeaweed.
            float[] ladder = { 0.45f, 0.75f, 1.15f };

            foreach (float target in ladder)
            {
                // Best possible error for this target, computed independently of the function.
                float bestPossible = f.Sizes.Min(s => Mathf.Abs(s - target));

                for (int key = 0; key < 200; key++)
                {
                    int ramp = SeaweedMath.PickRamp(new[] { 1f, 0.3f, 0.12f }, RampRows, key);
                    int pick = SeaweedMath.PickWeedArt(target, f.Sizes, f.Species, f.Ramps, f.Count,
                                                       null, ramp, key);
                    Assert.GreaterOrEqual(pick, 0, $"target {target}m key {key}: nothing picked");

                    float err = Mathf.Abs(f.Sizes[pick] - target);
                    Assert.LessOrEqual(err, bestPossible + SeaweedMath.ArtSizeTieMeters + 1e-5f,
                        $"target {target}m key {key}: picked {f.Sizes[pick]}m (err {err:F3}) when " +
                        $"{bestPossible:F3} was available. A pick outside the tie window means the " +
                        "presenter would have to rescale the art.");

                    // The scale the presenter would apply is 1 — that is the whole point.
                    Assert.LessOrEqual(err / target, 0.34f,
                        $"target {target}m: {f.Sizes[pick]}m is too far off to draw at native size.");
                }
            }
        }

        [Test]
        public void PickWeedArt_HonoursTheRampRow_SoAClumpKeepsItsColour()
        {
            var f = BuildFlat();
            for (int ramp = 0; ramp < RampRows; ramp++)
            for (int key = 0; key < 60; key++)
            {
                int pick = SeaweedMath.PickWeedArt(0.75f, f.Sizes, f.Species, f.Ramps, f.Count,
                                                  null, ramp, key);
                Assert.AreEqual(ramp, f.Ramps[pick], $"ramp {ramp} key {key}: got row {f.Ramps[pick]}");
            }
        }

        [Test]
        public void PickWeedArt_IsStableForAKey_AndVariesAcrossKeys()
        {
            var f = BuildFlat();
            int a = SeaweedMath.PickWeedArt(0.75f, f.Sizes, f.Species, f.Ramps, f.Count, null, 0, 12345);
            int b = SeaweedMath.PickWeedArt(0.75f, f.Sizes, f.Species, f.Ramps, f.Count, null, 0, 12345);
            Assert.AreEqual(a, b, "same key must give the same clump — a piece may not flicker.");

            var seen = new HashSet<int>();
            for (int key = 0; key < 400; key++)
                seen.Add(SeaweedMath.PickWeedArt(0.75f, f.Sizes, f.Species, f.Ramps, f.Count, null, 0, key));
            Assert.Greater(seen.Count, 1,
                "every piece picking the identical clump would make a bed of clones — the tie window " +
                "exists to give variety among equally-good sizes.");
        }

        // ---- the species filter -----------------------------------------------------------------

        [Test]
        public void PickWeedArt_RespectsASpeciesFilter_WhenThatSpeciesCanServeTheTier()
        {
            var f = BuildFlat();
            var onlyEelgrass = new[] { false, false, true, false };   // Eelgrass = index 2

            for (int key = 0; key < 60; key++)
            {
                int pick = SeaweedMath.PickWeedArt(0.75f, f.Sizes, f.Species, f.Ramps, f.Count,
                                                  onlyEelgrass, 0, key);
                Assert.AreEqual(2, f.Species[pick], $"key {key}: filter allowed only Eelgrass");
            }
        }

        [Test]
        public void PickWeedArt_FallsBackRatherThanDrawingNothing_WhenAFilterExcludesEverything()
        {
            var f = BuildFlat();
            var nothingAllowed = new[] { false, false, false, false };

            int pick = SeaweedMath.PickWeedArt(0.75f, f.Sizes, f.Species, f.Ramps, f.Count,
                                               nothingAllowed, 0, 7);
            Assert.GreaterOrEqual(pick, 0,
                "a bed whose filter excludes every species must still show weed — an invisible bed " +
                "reads as a bug, not as a configuration choice.");
        }

        [Test]
        public void PickWeedArt_FallsBackAcrossRamps_WhenTheRequestedRowIsMissing()
        {
            // One species, living row only — asking for 'bleached' must still return something.
            var sizes = new[] { 0.5f, 1.0f };
            var species = new[] { 0, 0 };
            var ramps = new[] { 0, 0 };

            int pick = SeaweedMath.PickWeedArt(1.0f, sizes, species, ramps, 2, null, ramp: 2, key: 3);
            Assert.GreaterOrEqual(pick, 0);
            Assert.AreEqual(1.0f, sizes[pick], 1e-5f, "size fidelity must survive the ramp fallback");
        }

        [Test]
        public void PickWeedArt_ReturnsMinusOne_OnAnEmptyKit()
        {
            Assert.AreEqual(-1, SeaweedMath.PickWeedArt(1f, null, null, null, 0, null, 0, 1));
            Assert.AreEqual(-1, SeaweedMath.PickWeedArt(1f, new float[0], new int[0], new int[0], 0, null, 0, 1));
        }

        // ---- ramp weighting ---------------------------------------------------------------------

        [Test]
        public void PickRamp_FollowsTheWeights_AndLivingDominatesAtTheShippedDefaults()
        {
            var counts = new int[RampRows];
            for (int key = 0; key < 6000; key++)
                counts[SeaweedMath.PickRamp(new[] { 1f, 0.3f, 0.12f }, RampRows, key)]++;

            // Expected shares 1/1.42, 0.3/1.42, 0.12/1.42 = 70.4% / 21.1% / 8.5%.
            Assert.Greater(counts[0], counts[1], "living must dominate");
            Assert.Greater(counts[1], counts[2], "golden must beat bleached");
            Assert.AreEqual(0.704f, counts[0] / 6000f, 0.04f);
            Assert.AreEqual(0.211f, counts[1] / 6000f, 0.04f);
            Assert.AreEqual(0.085f, counts[2] / 6000f, 0.04f);
        }

        [Test]
        public void PickRamp_TreatsAShortOrEmptyWeightListPredictably()
        {
            // {1} = living only: the missing rows weigh nothing.
            for (int key = 0; key < 200; key++)
                Assert.AreEqual(0, SeaweedMath.PickRamp(new[] { 1f }, RampRows, key));

            // Null / all-zero must not draw nothing — row 0 is the safe default.
            Assert.AreEqual(0, SeaweedMath.PickRamp(null, RampRows, 5));
            Assert.AreEqual(0, SeaweedMath.PickRamp(new[] { 0f, 0f, 0f }, RampRows, 5));

            // A weight on ONE row must pin every piece to it.
            for (int key = 0; key < 200; key++)
                Assert.AreEqual(2, SeaweedMath.PickRamp(new[] { 0f, 0f, 1f }, RampRows, key));
        }

        // ---- the tint, which is the quiet way this wiring could look wrong ------------------------

        [Test]
        public void PieceTint_IsWhiteForPaintedArt_SoTheKitsOwnRampsSurvive()
        {
            var palette = new[]
            {
                new Color(0.23f, 0.29f, 0.16f), new Color(0.18f, 0.24f, 0.15f),
                new Color(0.30f, 0.26f, 0.13f), new Color(0.16f, 0.21f, 0.17f),
            };

            for (int key = 0; key < 300; key++)
                Assert.AreEqual(Color.white, SeaweedMath.PieceTint(true, palette, key),
                    $"key {key}: painted weed must ride white. The palette exists to colour the " +
                    "white-with-alpha greybox blob; multiplying it over finished art muds every clump.");
        }

        [Test]
        public void PieceTint_StillTintsTheGreyboxBlobFromThePalette()
        {
            var palette = new[] { Color.red, Color.green, Color.blue };

            var used = new HashSet<Color>();
            for (int key = 0; key < 400; key++)
            {
                Color c = SeaweedMath.PieceTint(false, palette, key);
                Assert.Contains(c, palette, $"key {key}: greybox tint must come from the palette");
                used.Add(c);
            }
            Assert.AreEqual(3, used.Count, "all palette tones should get used across a bed");

            // Same key, same tone — a blob may not flicker between tones.
            Assert.AreEqual(SeaweedMath.PieceTint(false, palette, 99),
                            SeaweedMath.PieceTint(false, palette, 99));
        }

        [Test]
        public void PieceTint_FallsBackToWhiteWithNoPalette()
        {
            Assert.AreEqual(Color.white, SeaweedMath.PieceTint(false, null, 1));
            Assert.AreEqual(Color.white, SeaweedMath.PieceTint(false, new Color[0], 1));
        }

        [Test]
        public void PickRamp_NeverReturnsAnOutOfRangeRow()
        {
            for (int key = 0; key < 2000; key++)
            {
                int r = SeaweedMath.PickRamp(new[] { 1f, 1f, 1f }, RampRows, key);
                Assert.GreaterOrEqual(r, 0);
                Assert.Less(r, RampRows);
            }
            Assert.AreEqual(-1, SeaweedMath.PickRamp(new[] { 1f }, 0, 1), "no rows = no choice");
        }
    }
}
