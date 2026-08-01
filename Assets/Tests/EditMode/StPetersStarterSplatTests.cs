using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The St Peters starter splat (owner request 2026-07-30): the stroke PLANS are pure functions
    /// of the builder's constants, so where the paths, silt, marsh and sedge land is pinned here
    /// headlessly — and the splat IMPORT is pinned LINEAR, because an sRGB import would gamma-warp
    /// every painted weight the shader reads (0.5 "base" would arrive as ~0.21).
    /// </summary>
    public class StPetersStarterSplatTests
    {
        // ============================ THE STROKE PLANS ============================

        [Test]
        public void MaterialIndices_MatchTheCanonicalOrder()
        {
            Assert.AreEqual("Silt", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Silt]);
            Assert.AreEqual("Dirt", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Dirt]);
            Assert.AreEqual("Marsh", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Marsh]);
            Assert.AreEqual("Sedge", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Sedge]);
            Assert.AreEqual("Foreshore", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Foreshore]);
            Assert.AreEqual("Talus", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Talus]);
            Assert.AreEqual("Ledge", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Ledge]);
            Assert.AreEqual("Rockweed", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Rockweed]);
        }

        // ============================ THE KIT V2 SHORE BANDS ============================
        //  Placement is a pure function of a GroundSample, so every invariant below runs headless
        //  with no terrain, no textures and no editor. Every threshold is read from the LIVE
        //  constants — a literal here would pass while the shore sat metres out of place.

        private const ShoreMaterial Sand = ShoreMaterial.Sand;
        private const ShoreMaterial Ripple = ShoreMaterial.Ripple;
        private const ShoreMaterial Shelf = ShoreMaterial.Shelf;
        private const ShoreMaterial Shingle = ShoreMaterial.Shingle;
        private const ShoreMaterial Grass = ShoreMaterial.Grass;

        private static StPetersStarterSplat.GroundSample At(
            float elevation, ShoreMaterial substrate,
            float slope = 0f, bool weatherCoast = false) =>
            new StPetersStarterSplat.GroundSample(elevation, slope, substrate, weatherCoast);

        /// <summary>Mid-tide, the middle of the intertidal — where every band is at its widest.</summary>
        private static float MeanWater => StPetersBuilder.TideMean;

        [Test]
        public void TheTideLevels_DeriveFromTheLiveAmplitude_NotFromLiterals()
        {
            // If the owner rules a new amplitude, these move with it — that is the whole point.
            Assert.AreEqual(StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude,
                StPetersStarterSplat.SpringLowWater, 1e-4f);
            Assert.AreEqual(StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude,
                StPetersStarterSplat.SpringHighWater, 1e-4f);
            Assert.AreEqual(
                StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude
                    * GameConfig.DefaultNeapAmplitudeFraction,
                StPetersStarterSplat.NeapHighWater, 1e-4f);

            // The ladder the bands assume: low < neap high < spring high, and the weed belt's
            // ceiling is strictly inside the intertidal.
            Assert.Less(StPetersStarterSplat.SpringLowWater, StPetersStarterSplat.NeapHighWater);
            Assert.Less(StPetersStarterSplat.NeapHighWater, StPetersStarterSplat.SpringHighWater);
        }

        [Test]
        public void Foreshore_IsConfinedToItsTideBand_AndToSand()
        {
            // Inside the band on sand: painted.
            Assert.Greater(StPetersStarterSplat.ForeshoreCoverage(At(MeanWater, Sand)), 0.5f,
                "foreshore should be strongest at mean water, where the sea spends most of its time");
            Assert.Greater(StPetersStarterSplat.ForeshoreCoverage(At(MeanWater, Ripple)), 0.5f);

            // Outside the tide band: nothing, at either end.
            Assert.AreEqual(0f, StPetersStarterSplat.ForeshoreCoverage(
                At(StPetersStarterSplat.SpringHighWater + 0.01f, Sand)), 1e-6f,
                "foreshore above the highest water is ground the sea never works");
            Assert.AreEqual(0f, StPetersStarterSplat.ForeshoreCoverage(
                At(StPetersStarterSplat.SpringLowWater - 0.01f, Sand)), 1e-6f,
                "foreshore below the lowest water is permanently drowned seabed");

            // Feathered, not a stripe: the edges must approach zero, not step to it.
            float nearTop = StPetersStarterSplat.ForeshoreCoverage(
                At(StPetersStarterSplat.SpringHighWater - 0.05f, Sand));
            Assert.That(nearTop, Is.GreaterThan(0f).And.LessThan(0.2f),
                "the band's edge must feather — a hard edge reads as a painted stripe");
        }

        [Test]
        public void Rockweed_NeverGrowsOnOpenSand_AndStopsAtTheDryingLine()
        {
            // ⭐ The rule the kit is explicit about: a frond needs something to hold.
            foreach (var e in new[] { StPetersStarterSplat.SpringLowWater + 0.1f, MeanWater,
                                      StPetersStarterSplat.NeapHighWater - 0.1f })
            {
                Assert.AreEqual(0f, StPetersStarterSplat.RockweedCoverage(At(e, Sand)), 1e-6f,
                    $"rockweed on open sand at {e:0.##} m — nothing there for a holdfast");
                Assert.AreEqual(0f, StPetersStarterSplat.RockweedCoverage(At(e, Ripple)), 1e-6f,
                    $"rockweed on rippled sand flats at {e:0.##} m");
            }

            // On rock, inside the belt: painted.
            Assert.Greater(StPetersStarterSplat.RockweedCoverage(
                At(StPetersStarterSplat.NeapHighWater - 0.3f, Shelf)), 0.5f);
            Assert.Greater(StPetersStarterSplat.RockweedCoverage(At(MeanWater, Shingle)), 0.3f);

            // Above the highest water it is gone entirely, and it is already thinning at neap high
            // (the belt is densest BELOW its own drying ceiling).
            Assert.AreEqual(0f, StPetersStarterSplat.RockweedCoverage(
                At(StPetersStarterSplat.SpringHighWater + 0.01f, Shelf)), 1e-6f,
                "rockweed above the highest spring water would never be wetted");
            Assert.Less(
                StPetersStarterSplat.RockweedCoverage(At(StPetersStarterSplat.NeapHighWater, Shelf)),
                StPetersStarterSplat.RockweedCoverage(
                    At(StPetersStarterSplat.NeapHighWater
                       - (StPetersStarterSplat.NeapHighWater - StPetersBuilder.TideMean)
                         * StPetersStarterSplat.RockweedPeakDrop, Shelf)),
                "the canopy must be densest just BELOW neap high, not at it");
        }

        [Test]
        public void Ramp_IsHlslSmoothstep_NotMathfSmoothStep()
        {
            // ⭐ The bug this pins: Mathf.SmoothStep(a, b, t) INTERPOLATES BETWEEN a and b — it does
            // not map t from [a,b] onto [0,1]. Used as a gate it never returns 0, so "flat ground"
            // scored 0.44 scree. A gate must bottom out at exactly zero.
            Assert.AreEqual(0f, StPetersStarterSplat.Ramp(0.35f, 0.52f, 0f), 1e-6f);
            Assert.AreEqual(0f, StPetersStarterSplat.Ramp(0.35f, 0.52f, 0.35f), 1e-6f);
            Assert.AreEqual(1f, StPetersStarterSplat.Ramp(0.35f, 0.52f, 0.52f), 1e-6f);
            Assert.AreEqual(1f, StPetersStarterSplat.Ramp(0.35f, 0.52f, 99f), 1e-6f);
            Assert.AreEqual(0.5f, StPetersStarterSplat.Ramp(0f, 1f, 0.5f), 1e-6f);

            // Descending edges (edge1 < edge0) ramp DOWN — the form the band tops use.
            Assert.AreEqual(1f, StPetersStarterSplat.Ramp(4.2f, 3.2f, 3.2f), 1e-6f);
            Assert.AreEqual(0f, StPetersStarterSplat.Ramp(4.2f, 3.2f, 4.2f), 1e-6f);
            Assert.AreEqual(0f, StPetersStarterSplat.Ramp(4.2f, 3.2f, 5f), 1e-6f);
        }

        [Test]
        public void TheSlopeSplit_CanActuallyReachFullScree_OnThisIslandsProfile()
        {
            // A smoothstep falloff's gradient peaks at exactly 1.5x its mean. If "fully scree" were
            // set above that, talus could never exceed half strength ANYWHERE on St Peters — which
            // is precisely what the first cut of this pass shipped.
            float steepestOnTheIsland = StPetersStarterSplat.BeachGradient * 1.5f;
            Assert.GreaterOrEqual(steepestOnTheIsland,
                StPetersStarterSplat.TalusSlopeThreshold * StPetersStarterSplat.TalusSlopeFullFactor,
                "the scree threshold is set above the steepest ground this island has — talus can " +
                "never reach full coverage");

            var atSteepest = At(StPetersStarterSplat.SteepestElevation, Shingle,
                                steepestOnTheIsland, weatherCoast: true);
            Assert.AreEqual(1f, StPetersStarterSplat.Steepness(atSteepest), 1e-4f);
            Assert.Greater(StPetersStarterSplat.TalusCoverage(atSteepest), 0.95f,
                "the steepest metre of the coast should be full scree");
        }

        [Test]
        public void Talus_NeedsSteepGround_AndStaysAboveTheHighestWater()
        {
            float steep = StPetersStarterSplat.TalusSlopeThreshold
                          * StPetersStarterSplat.TalusSlopeFullFactor;
            float apron = StPetersStarterSplat.SteepestElevation;

            Assert.Greater(
                StPetersStarterSplat.TalusCoverage(At(apron, Shingle, steep, weatherCoast: true)), 0.5f,
                "steep weather-coast ground above high water is exactly where scree collects");

            // Flat ground gets none, however right the elevation is.
            Assert.AreEqual(0f,
                StPetersStarterSplat.TalusCoverage(At(apron, Shingle, 0f, weatherCoast: true)), 1e-6f,
                "talus on flat ground is not talus — a blockfield needs something to have fallen off");

            // Below the highest water it is gone: a wetted apron is shingle, which the band ladder
            // already draws for itself.
            Assert.AreEqual(0f, StPetersStarterSplat.TalusCoverage(
                At(StPetersStarterSplat.SpringHighWater - 0.01f, Shingle, steep, weatherCoast: true)),
                1e-6f, "talus below the highest water");

            // The sheltered side is beach and dune — no eroding face to shed slabs.
            Assert.AreEqual(0f,
                StPetersStarterSplat.TalusCoverage(At(apron, Shingle, steep, weatherCoast: false)),
                1e-6f, "talus on the sheltered coast");
        }

        [Test]
        public void LedgeAndTalus_SplitTheSameGroundBySlope_AndNeverBothClaimIt()
        {
            float mid = (StPetersStarterSplat.NeapHighWater
                         + StPetersShoreMap.GrassFloorElevation) * 0.5f;

            // Ledge is the FLAT half of the split, talus the steep half. Sampled across the whole
            // slope range, their coverages never sum past one texel's worth.
            for (float slope = 0f; slope <= StPetersStarterSplat.TalusSlopeThreshold * 2f; slope += 0.02f)
            {
                var g = At(mid, Shingle, slope, weatherCoast: true);
                float sum = StPetersStarterSplat.LedgeCoverage(g) + StPetersStarterSplat.TalusCoverage(g);
                Assert.LessOrEqual(sum, 1f + 1e-4f,
                    $"ledge + talus over-claim at slope {slope:0.###} — the split is not complementary");
            }

            Assert.Greater(StPetersStarterSplat.LedgeCoverage(At(mid, Shingle, 0f, true)), 0.5f,
                "flat weather-coast rock is bare pavement");
            Assert.AreEqual(0f, StPetersStarterSplat.LedgeCoverage(
                At(mid, Shingle, StPetersStarterSplat.TalusSlopeThreshold
                                 * StPetersStarterSplat.TalusSlopeFullFactor, true)), 1e-6f,
                "fully-steep ground is scree, not pavement");

            // Ledge reaches the flat rock PLATFORM below the weed belt — the reef apron, where the
            // gradient is near zero. A tide-banded ledge could not, and that is why it has no band.
            Assert.Greater(StPetersStarterSplat.LedgeCoverage(
                At(StPetersBuilder.ReefShelfInnerElevation, Shelf, 0.02f, true)), 0.5f,
                "the flat reef platform is exactly where bevelled pavement shows");

            // Ledge is rock-and-weather-coast only, and stops below the meadow.
            Assert.AreEqual(0f, StPetersStarterSplat.LedgeCoverage(At(mid, Sand, 0f, true)), 1e-6f);
            Assert.AreEqual(0f, StPetersStarterSplat.LedgeCoverage(At(mid, Shingle, 0f, false)), 1e-6f);
            Assert.AreEqual(0f, StPetersStarterSplat.LedgeCoverage(
                At(StPetersShoreMap.GrassFloorElevation + 0.01f, Shingle, 0f, true)), 1e-6f,
                "ledge must hand off to the meadow at the grass floor");
        }

        [Test]
        public void EveryCoverage_StaysInRange_AndIsSilentOnUnpaintedSeabed()
        {
            // A coverage is a takeover fraction: outside 0..1 it would either erase more than the
            // texel holds or read as negative paint.
            foreach (var fam in StPetersStarterSplat.KitV2Families)
            for (float e = StPetersShoreMap.PaintFloorElevation - 1f;
                 e <= StPetersShoreMap.GrassFloorElevation + 1f; e += 0.1f)
            foreach (var sub in new[] { Sand, Ripple, Shelf, Shingle, Grass })
            foreach (var slope in new[] { 0f, StPetersStarterSplat.TalusSlopeThreshold, 2f })
            foreach (var weather in new[] { true, false })
            {
                float c = StPetersStarterSplat.CoverageOf(fam.Material, At(e, sub, slope, weather));
                Assert.That(c, Is.InRange(0f, 1f),
                    $"{fam.Name} coverage {c} out of range at e={e:0.##} {sub} slope={slope}");
            }

            // Grass/marram ground is the meadow — none of the four shore families belong on it.
            foreach (var fam in StPetersStarterSplat.KitV2Families)
                Assert.AreEqual(0f, StPetersStarterSplat.CoverageOf(fam.Material, At(MeanWater, Grass)),
                    1e-6f, $"{fam.Name} painted onto meadow ground");
        }

        [Test]
        public void CoverageIsDeterministic_AndTheLadderStaysInTheStarterRange()
        {
            var g = At(MeanWater, Shelf, 0.3f, weatherCoast: true);
            foreach (var fam in StPetersStarterSplat.KitV2Families)
            {
                Assert.AreEqual(StPetersStarterSplat.CoverageOf(fam.Material, g),
                                StPetersStarterSplat.CoverageOf(fam.Material, g), 0f,
                    $"{fam.Name} coverage is not a pure function of the sample");

                // The handoff's restraint: a substrate for the owner to paint over, so the ladder
                // sits around base (0.5) and never reaches the _Hi rank step.
                Assert.That(fam.Intensity, Is.InRange(0.4f, 0.6f),
                    $"{fam.Name} ladder intensity {fam.Intensity} is outside the starter range");
            }
        }

        [Test]
        public void PaintingTheV2BandsFirst_LeavesTheV1FeatureChannelsUntouched()
        {
            // ⭐ The ordering claim the pass depends on, tested on the mechanism itself. An
            // exclusive stroke lerps its OWN channel from whatever is there and fades the rest — so
            // as long as the bands never touch dirt/silt/marsh/sedge, laying them down first cannot
            // move where those four land. Reverse the order and the bands would erase the features.
            const int W = 8, H = 8;
            var worldMin = new Vector2(0f, 0f);
            var worldSize = new Vector2(8f, 8f);
            var centre = new Vector2(4f, 4f);

            Color[][] Blank()
            {
                var l = new Color[TerrainSplatBrush.TextureCount][];
                for (int t = 0; t < l.Length; t++) l[t] = new Color[W * H];
                return l;
            }

            // A: the v1 dirt stroke on virgin ground (what the pass produced before this change).
            var v1Only = Blank();
            TerrainSplatBrush.Dab(v1Only, W, H, worldMin, worldSize, centre, 3f,
                StPetersStarterSplat.PathFalloff, StPetersStarterSplat.Dirt,
                StPetersStarterSplat.SlipPathIntensity, 1f, exclusive: true);

            // B: the v2 bands first — full coverage everywhere, the harshest case — then the same
            //    stroke. Two families, on both of the maps the bands live on (C and D).
            var withBands = Blank();
            var full = new float[W * H];
            for (int i = 0; i < full.Length; i++) full[i] = 1f;
            TerrainSplatBrush.PaintField(withBands, W, H, StPetersStarterSplat.Foreshore,
                StPetersStarterSplat.ForeshoreIntensity, full, exclusive: true);
            TerrainSplatBrush.PaintField(withBands, W, H, StPetersStarterSplat.Rockweed,
                StPetersStarterSplat.RockweedIntensity, full, exclusive: true);
            TerrainSplatBrush.Dab(withBands, W, H, worldMin, worldSize, centre, 3f,
                StPetersStarterSplat.PathFalloff, StPetersStarterSplat.Dirt,
                StPetersStarterSplat.SlipPathIntensity, 1f, exclusive: true);

            foreach (int material in new[] { StPetersStarterSplat.Silt, StPetersStarterSplat.Dirt,
                                             StPetersStarterSplat.Marsh, StPetersStarterSplat.Sedge })
            {
                int tex = TerrainSplatBrush.TextureOf(material);
                int ch = TerrainSplatBrush.ChannelOf(material);
                for (int i = 0; i < W * H; i++)
                    Assert.AreEqual(
                        TerrainSplatBrush.GetChannel(v1Only[tex][i], ch),
                        TerrainSplatBrush.GetChannel(withBands[tex][i], ch), 1e-6f,
                        $"{TerrainSplatBrush.MaterialNames[material]} moved at texel {i} because the " +
                        "v2 bands were painted underneath it");
            }
        }

        [Test]
        public void PaintField_FeathersWithCoverage_AndYieldsTheTexelWhereItIsFull()
        {
            const int W = 4, H = 1;
            var layers = new Color[TerrainSplatBrush.TextureCount][];
            for (int t = 0; t < layers.Length; t++) layers[t] = new Color[W * H];

            // Pre-existing grass everywhere, so the exclusive takeover is visible.
            for (int i = 0; i < W * H; i++) layers[0][i].r = 1f;

            var coverage = new[] { 0f, 0.25f, 0.5f, 1f };
            TerrainSplatBrush.PaintField(layers, W, H, StPetersStarterSplat.Foreshore,
                StPetersStarterSplat.ForeshoreIntensity, coverage, exclusive: true);

            // The channel value IS intensity x coverage — one number serving as blend weight and
            // ladder position at once (kit README section 2).
            for (int i = 0; i < W; i++)
            {
                Assert.AreEqual(StPetersStarterSplat.ForeshoreIntensity * coverage[i],
                    layers[2][i].b, 1e-5f, $"foreshore value wrong at coverage {coverage[i]}");
                Assert.AreEqual(1f - coverage[i], layers[0][i].r, 1e-5f,
                    $"grass did not yield in proportion to coverage {coverage[i]}");
            }
        }

        [Test]
        public void TheBarSpine_StaysBareCobble_SoTheCrossingKeepsReading()
        {
            // ⭐ Caught by measuring the painted map, not by reasoning: the spine reports as
            // Shingle (rock), so rockweed drapes the low-tide walking line — the one strip of
            // ground the player reads to decide whether the crossing is on. StPetersShoreMap
            // exempts the spine from the band wiggle for exactly this reason.
            Vector2 crossing = Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, 0.5f);
            float onCrest = StPetersShoreMap.BarSpineFloorElevation + 0.1f;

            Assert.IsTrue(StPetersStarterSplat.IsBarSpine(crossing, onCrest),
                "the midpoint of the bar at crest height should BE the spine");
            Assert.IsFalse(StPetersStarterSplat.IsBarSpine(crossing,
                StPetersShoreMap.BarSpineFloorElevation - 0.1f),
                "below the spine floor the bar has fallen away into its sandy flanks");
            Assert.IsFalse(StPetersStarterSplat.IsBarSpine(
                crossing + Vector2.up * (StPetersShoreMap.BarSpineHalfWidth + 1f), onCrest),
                "off the crest, the flanks are not the spine");

            // The rule the pass applies: on the spine, nothing is painted over the cobble.
            var spine = new StPetersStarterSplat.GroundSample(
                onCrest, 0f, Shingle, weatherCoast: false, onBarSpine: true);
            var flank = new StPetersStarterSplat.GroundSample(
                onCrest, 0f, Shingle, weatherCoast: false, onBarSpine: false);
            Assert.Greater(StPetersStarterSplat.RockweedCoverage(flank), 0f,
                "cobble at crest height is otherwise prime rockweed ground — which is the trap");
            Assert.IsTrue(spine.OnBarSpine,
                "GroundSample must carry the spine flag for BuildCoverage to skip the texel");
        }

        [Test]
        public void ThePass_IsIdempotent_RunningItTwiceReproducesTheSamePixels()
        {
            // ⭐ The acceptance the handoff names, and a real bug this caught: every stroke in the
            // pass is EXCLUSIVE, and an exclusive stroke lerps its own channel from whatever is
            // beneath. Painting over a previous pass therefore converges toward the target instead
            // of reproducing it — run 2 differed from run 1 in three of the four maps until
            // PaintInto started clearing. A pass that must "re-derive after a seabed re-bake"
            // cannot blend the new answer into the old one.
            const int W = 40, H = 28;
            var worldSize = new Vector2(760f, 520f);
            var worldMin = new Vector2(-380f, -260f);

            var go = new GameObject("TidalTerrain_IdempotenceTest");
            try
            {
                var terrain = go.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);

                Color[][] Run()
                {
                    var layers = new Color[TerrainSplatBrush.TextureCount][];
                    for (int t = 0; t < layers.Length; t++) layers[t] = new Color[W * H];
                    StPetersStarterSplat.PaintInto(layers, W, H, worldMin, worldSize, terrain);
                    return layers;
                }

                Color[][] first = Run();
                Color[][] second = Run();
                for (int t = 0; t < first.Length; t++)
                    CollectionAssert.AreEqual(first[t], second[t],
                        $"Splat{TerrainSplatBrush.TextureSuffixes[t]} differs between two clean runs");

                // And re-running INTO the previous result — the way the menu actually re-runs —
                // must land on the same pixels too, not converge toward them.
                Color[][] reused = first;
                StPetersStarterSplat.PaintInto(reused, W, H, worldMin, worldSize, terrain);
                for (int t = 0; t < reused.Length; t++)
                    CollectionAssert.AreEqual(second[t], reused[t],
                        $"Splat{TerrainSplatBrush.TextureSuffixes[t]} accumulated when re-run over " +
                        "its own output — the pass is not idempotent");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void KitV2Families_ArePaintedInCanonicalOrder_WithNoDuplicates()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var fam in StPetersStarterSplat.KitV2Families)
            {
                Assert.IsTrue(seen.Add(fam.Material), $"{fam.Name} listed twice");
                Assert.AreEqual(fam.Name, TerrainSplatBrush.MaterialNames[fam.Material],
                    "KitV2Families name disagrees with the canonical material at that index");
            }
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void SlipPath_RunsFromTheVillageGreenToTheSlip()
        {
            Vector2[] path = StPetersStarterSplat.VillageToSlipPath();
            Assert.GreaterOrEqual(path.Length, 4, "the ask is a gentle curve — 2-3 bends, not a straight line");
            Assert.AreEqual(StPetersBuilder.VillageGreen, path[0]);
            Assert.AreEqual(new Vector2(StPetersBuilder.BerthTo.x, StPetersBuilder.BerthTo.y),
                            path[path.Length - 1], "the path must end at the slip's shoreline head");
        }

        [Test]
        public void BarHeadPath_RunsFromTheVillageGreenToTheBarHead()
        {
            Vector2[] path = StPetersStarterSplat.VillageToBarHeadPath();
            Assert.GreaterOrEqual(path.Length, 3);
            Assert.AreEqual(StPetersBuilder.VillageGreen, path[0]);
            Assert.AreEqual(StPetersBuilder.SandbarFrom, path[path.Length - 1]);
        }

        [Test]
        public void BentPath_BendsStayWithinTheAmplitude_AndAreDeterministic()
        {
            Vector2 from = new Vector2(0f, 0f), to = new Vector2(100f, 0f);
            Vector2[] p1 = StPetersStarterSplat.BentPath(from, to, 3, 8f, 41);
            Vector2[] p2 = StPetersStarterSplat.BentPath(from, to, 3, 8f, 41);
            CollectionAssert.AreEqual(p1, p2, "hash-jittered bends must be deterministic (rule 5)");

            for (int i = 1; i < p1.Length - 1; i++)
            {
                float t = i / (float)(p1.Length - 1);
                Vector2 onLine = Vector2.Lerp(from, to, t);
                Assert.LessOrEqual(Vector2.Distance(p1[i], onLine), 8f + 1e-3f,
                    $"bend {i} strayed past the amplitude");
            }
        }

        [Test]
        public void SiltBlobs_HugTheChannelEdges_OnTheFlats()
        {
            Vector2 crossing = StPetersStarterSplat.ChannelCrossing();
            Assert.AreEqual(
                Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo,
                             StPetersBuilder.ChannelAlong),
                crossing, "the crossing must be the terrain's own channel lerp");

            Vector2 barDir = (StPetersBuilder.SandbarTo - StPetersBuilder.SandbarFrom).normalized;
            Vector2 perp = new Vector2(-barDir.y, barDir.x);

            StPetersStarterSplat.Blob[] blobs = StPetersStarterSplat.SiltBlobs();
            Assert.AreEqual(6, blobs.Length, "three blobs per side of the channel");

            foreach (var blob in blobs)
            {
                float along = Vector2.Dot(blob.Center - crossing, barDir);
                float across = Vector2.Dot(blob.Center - crossing, perp);

                Assert.GreaterOrEqual(Mathf.Abs(along) - blob.Radius, StPetersBuilder.ChannelHalfWidth - 1e-3f,
                    "a silt blob reached into the boat channel — it must HUG the edge, not sit in the gut");
                Assert.LessOrEqual(Mathf.Abs(along), StPetersBuilder.ChannelHalfWidth + 20f,
                    "a silt blob drifted far from the channel it is supposed to flank");
                Assert.LessOrEqual(Mathf.Abs(across), StPetersBuilder.SandbarHalfWidth,
                    "a silt blob left the bar's flats");
                Assert.That(blob.Radius, Is.InRange(StPetersStarterSplat.SiltRadiusMin,
                                                    StPetersStarterSplat.SiltRadiusMax));
                Assert.That(blob.Intensity, Is.InRange(StPetersStarterSplat.SiltIntensityMin,
                                                       StPetersStarterSplat.SiltIntensityMax));
            }

            CollectionAssert.AreEqual(blobs, StPetersStarterSplat.SiltBlobs(),
                "the blob plan must be deterministic (rule 5)");
        }

        [Test]
        public void MarshPocket_SitsNorthWest_InTheUpperSandBand()
        {
            var go = new GameObject("TidalTerrain_StarterSplatTest");
            try
            {
                var terrain = go.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);

                Vector2 pocket = StPetersStarterSplat.FindMarshPocket(terrain.ElevationAtZones);
                Assert.AreNotEqual(StPetersBuilder.IslandCenter, pocket, "no pocket found at all");
                Assert.Less(pocket.x, StPetersBuilder.IslandCenter.x, "the pocket must lie WEST of the centre");
                Assert.Greater(pocket.y, 0f, "the pocket must lie NORTH — the sheltered side");

                float elev = terrain.ElevationAtZones(pocket);
                Assert.LessOrEqual(elev, StPetersStarterSplat.MarshPocketElevation + 1e-3f,
                    "the pocket must sit at/below the marsh elevation (the first crossing)");
                Assert.GreaterOrEqual(elev, StPetersShoreMap.SandFloorElevation,
                    "the pocket fell below the sand floor — that is flats, not a marsh hollow");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SedgeFringe_RingsThePocketJustOutsideItsRim()
        {
            Vector2 centre = new Vector2(3f, 67f);
            Vector2[] ring = StPetersStarterSplat.SedgeFringe(centre);
            Assert.AreEqual(StPetersStarterSplat.SedgeFringeCount, ring.Length);
            foreach (Vector2 p in ring)
                Assert.AreEqual(StPetersStarterSplat.MarshRadiusMetres + 2f,
                                Vector2.Distance(p, centre), 1e-3f);
        }

        // ============================ THE LINEAR-IMPORT TRAP ============================

        [Test]
        public void ConfigureImporter_PinsLinearReadableUncompressed()
        {
            // Prove the importer the commit path applies actually lands the DATA settings — on a
            // throwaway PNG, so this guards the behaviour even before any splat is committed.
            const string probePath = "Assets/TempSplatImporterProbe.png";
            try
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
                File.WriteAllBytes(probePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(probePath, ImportAssetOptions.ForceSynchronousImport);

                TerrainSplatAssets.ConfigureImporter(probePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(probePath);
                Assert.IsNotNull(importer);
                Assert.IsFalse(importer.sRGBTexture,
                    "SPLAT MAPS MUST IMPORT LINEAR — sRGB would gamma-warp every painted weight");
                Assert.IsTrue(importer.isReadable, "the brush edits the pixels in place");
                Assert.IsFalse(importer.mipmapEnabled);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            }
            finally
            {
                AssetDatabase.DeleteAsset(probePath);
            }
        }

        [Test]
        public void CommittedSplatMaps_ImportLinear()
        {
            // Pins the COMMITTED assets once the starter paint (or any brush commit) has produced
            // them. Absent maps are not a failure — the menu creates them on first run.
            bool anyExists = false;
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
            {
                string path = TerrainSplatAssets.PathOf(i);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                anyExists = true;
                Assert.IsFalse(importer.sRGBTexture,
                    $"'{path}' imports sRGB — the shader would read gamma-warped weights. Re-commit " +
                    "through the tool (TerrainSplatAssets.ConfigureImporter).");
                Assert.IsTrue(importer.isReadable, $"'{path}' must stay CPU-readable for the brush");
                Assert.IsFalse(importer.mipmapEnabled, $"'{path}' must not carry mips");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression,
                    $"'{path}' must stay uncompressed — block compression mangles painted weights");
            }
            if (!anyExists)
                Assert.Ignore("No splat maps committed yet — run Hidden Harbours ▸ Tools ▸ " +
                              "Paint St Peters Starter Splat (or paint with the Material brush) first.");
        }

        // ============================ THE SPINE EXEMPTION, ENFORCED ============================

        /// <summary>
        /// #391 review follow-up F1: the spine skip inside <c>BuildCoverage</c> was only ever tested
        /// through its PREDICATE — deleting the <c>continue</c> left every test green. This runs the
        /// real sweep over the crossing and pins the outcome: the walking line carries NONE of the four
        /// shore families, while its flanks (the trap #391 caught and removed 13,056 texels of) do.
        /// </summary>
        [Test]
        public void TheCoverageSweep_LeavesTheSpineBare_AndDressesTheFlanks()
        {
            var go = new GameObject("StPetersTerrain_SpineSweep");
            try
            {
                var terrain = go.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);

                // A coarse texel grid over the bar only — resolution-parametric by design, so the test
                // does not need the full 1520x1040 sweep to prove the rule.
                var worldMin = new Vector2(StPetersBuilder.SandbarTo.x - 5f,
                                           -(StPetersBuilder.SandbarHalfWidth + 8f));
                var worldSize = new Vector2(
                    (StPetersBuilder.SandbarFrom.x + 5f) - worldMin.x,
                    2f * (StPetersBuilder.SandbarHalfWidth + 8f));
                const int w = 156, h = 38;

                float[][] maps = StPetersStarterSplat.BuildCoverage(terrain, w, h, worldMin, worldSize);

                int spineTexels = 0, dressedFlankTexels = 0;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var pos = new Vector2(worldMin.x + (x + 0.5f) / w * worldSize.x,
                                          worldMin.y + (y + 0.5f) / h * worldSize.y);
                    float e = terrain.ElevationAt(pos);
                    int idx = y * w + x;

                    if (StPetersShoreMap.IsBarSpine(pos, e))
                    {
                        spineTexels++;
                        for (int f = 0; f < StPetersStarterSplat.KitV2Families.Length; f++)
                            Assert.AreEqual(0f, maps[f][idx],
                                $"{StPetersStarterSplat.KitV2Families[f].Name} paints the bar spine at " +
                                $"{pos} — the walking line is SIGNAGE, and the BuildCoverage skip that " +
                                "keeps it bare has been removed or bypassed.");
                    }
                    else
                    {
                        for (int f = 0; f < StPetersStarterSplat.KitV2Families.Length; f++)
                            if (maps[f][idx] > 0f) { dressedFlankTexels++; break; }
                    }
                }

                Assert.Greater(spineTexels, 50, "sanity: the sweep must actually cross the spine");
                Assert.Greater(dressedFlankTexels, 50,
                    "sanity: the flanks must carry SOME shore family, or a bare spine proves nothing " +
                    "(the whole bar being bare would pass the assert above vacuously)");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>#391 review follow-up F3: the spine predicate is HOISTED — one definition in
        /// <see cref="StPetersShoreMap.IsBarSpine"/>, drawn by <c>MaterialAt</c> and read back by the
        /// splat pass. This pins the delegation so the two can never drift again.</summary>
        [Test]
        public void TheSpinePredicate_HasOneDefinition()
        {
            foreach (var probe in new[]
            {
                (pos: new Vector2(-200f, 0f), e: 0.5f),    // mid-bar, on the crest height
                (pos: new Vector2(-200f, 0f), e: 0.2f),    // mid-bar, below the spine floor
                (pos: new Vector2(-200f, 20f), e: 0.5f),   // flank sand
                (pos: new Vector2(70f, 0f), e: 5f),        // the island, nowhere near the bar
            })
                Assert.AreEqual(StPetersShoreMap.IsBarSpine(probe.pos, probe.e),
                                StPetersStarterSplat.IsBarSpine(probe.pos, probe.e),
                    $"the two IsBarSpine answers disagree at {probe.pos} e={probe.e} — the hoist broke");
        }
    }
}
