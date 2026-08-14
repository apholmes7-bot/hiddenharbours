using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Nine Mile Creek starter splat. The placement rules are pure functions of a
    /// <see cref="NineMileCreekStarterSplat.GroundSample"/>, so every invariant below runs headless with no
    /// terrain, no textures and no editor — and the one that needs a terrain (idempotence) builds a
    /// transient one from the region's own plan.
    ///
    /// <para>⭐ <b>The acceptance the handoff names is
    /// <see cref="ThePass_IsIdempotent_RunningItTwiceReproducesTheSamePixels"/>.</b> Every stroke in this
    /// pass is EXCLUSIVE, and an exclusive stroke lerps its own channel from whatever is beneath — so a
    /// second run converges toward the first instead of reproducing it unless the pass clears first. That
    /// is not a formality: it is the defect St Peters shipped with and had to fix.</para>
    /// </summary>
    public class NineMileCreekStarterSplatTests
    {
        // ============================ THE CHANNEL MAP ============================

        [Test]
        public void MaterialIndices_MatchTheCanonicalOrder()
        {
            // A splat index is a CHANNEL in a committed PNG. Getting one wrong paints silt where dirt
            // belongs, in a file nothing else re-derives.
            Assert.AreEqual("Sand", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Sand]);
            Assert.AreEqual("Shingle", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Shingle]);
            Assert.AreEqual("Silt", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Silt]);
            Assert.AreEqual("Dirt", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Dirt]);
            Assert.AreEqual("Marsh", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Marsh]);
            Assert.AreEqual("Sedge", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Sedge]);
            Assert.AreEqual("Foreshore", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Foreshore]);
            Assert.AreEqual("Talus", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Talus]);
            Assert.AreEqual("Ledge", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Ledge]);
            Assert.AreEqual("Rockweed", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Rockweed]);
            Assert.AreEqual("Musselbed", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Musselbed]);
            Assert.AreEqual("Oysterreef", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Oysterreef]);
            Assert.AreEqual("Eelgrass", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Eelgrass]);
            Assert.AreEqual("Irishmoss", TerrainSplatBrush.MaterialNames[NineMileCreekStarterSplat.Irishmoss]);
        }

        [Test]
        public void TheSplatStemIsThisRegionsOwn_NotTheIslands()
        {
            // ⭐ THE WALL #522 REMOVED, held open. Every TerrainSplatAssets entry point defaults to the
            // St Peters stem; a pass that forgot to pass its own would paint over the ISLAND'S maps, at
            // the island's resolution, on the island's world frame — and report success.
            Assert.AreNotEqual(TerrainSplatAssets.StPetersBaseName,
                NineMileCreekStarterSplat.SplatBaseName);
            StringAssert.StartsWith(TerrainSplatAssets.Dir + "/NineMileCreekSplat",
                TerrainSplatAssets.PathOf(0, NineMileCreekStarterSplat.SplatBaseName));
        }

        // ============================ THE TIDE ============================

        private static float MeanWater => NineMileCreekMainland.TideMean;

        private static NineMileCreekStarterSplat.GroundSample At(
            float elevation, bool rockCoast = false, float slope = 0f,
            float madeGround = 0f, float pondGround = 0f,
            ShoreMaterial substrate = ShoreMaterial.Sand) =>
            new NineMileCreekStarterSplat.GroundSample(
                elevation, slope, substrate, rockCoast, madeGround, pondGround);

        [Test]
        public void TheTideLevels_DeriveFromTheLiveAmplitude_NotFromLiterals()
        {
            // If the owner rules a new amplitude, every window in this pass moves with it — that is the
            // whole point of authoring them as fractions.
            Assert.AreEqual(NineMileCreekMainland.TideMean - NineMileCreekMainland.TideAmplitude,
                NineMileCreekStarterSplat.SpringLowWater, 1e-4f);
            Assert.AreEqual(NineMileCreekMainland.TideMean + NineMileCreekMainland.TideAmplitude,
                NineMileCreekStarterSplat.SpringHighWater, 1e-4f);
            Assert.Less(NineMileCreekStarterSplat.SpringLowWater, NineMileCreekStarterSplat.NeapHighWater);
            Assert.Less(NineMileCreekStarterSplat.NeapHighWater, NineMileCreekStarterSplat.SpringHighWater);

            Assert.AreEqual(NineMileCreekStarterSplat.SpringLowWater,
                NineMileCreekStarterSplat.TideElevation(-1f), 1e-4f);
            Assert.AreEqual(NineMileCreekStarterSplat.SpringHighWater,
                NineMileCreekStarterSplat.TideElevation(1f), 1e-4f);
        }

        // ============================ THE NATURAL SHORE ============================

        [Test]
        public void Foreshore_IsConfinedToItsTideBand_AndToTheSoftCoast()
        {
            Assert.Greater(NineMileCreekStarterSplat.ForeshoreCoverage(At(MeanWater)), 0.5f,
                "foreshore should be strongest at mean water, where the sea spends most of its time");

            Assert.AreEqual(0f, NineMileCreekStarterSplat.ForeshoreCoverage(
                At(NineMileCreekStarterSplat.SpringHighWater + 0.01f)), 1e-6f,
                "foreshore above the highest water is ground the sea never works");
            Assert.AreEqual(0f, NineMileCreekStarterSplat.ForeshoreCoverage(
                At(NineMileCreekStarterSplat.SpringLowWater - 0.01f)), 1e-6f,
                "foreshore below the lowest water is permanently drowned seabed");

            // ⭐ The two gates that make this a MAINLAND rule rather than an island one.
            Assert.AreEqual(0f, NineMileCreekStarterSplat.ForeshoreCoverage(
                At(MeanWater, rockCoast: true)), 1e-6f,
                "a cliff coast has no wave-worked sand — the sea takes it away on the side it pounds");
            Assert.AreEqual(0f, NineMileCreekStarterSplat.ForeshoreCoverage(
                At(MeanWater, madeGround: 1f)), 1e-6f,
                "the wharf yard is made ground: a foreshore there would be a yard the sea had taken back");
            Assert.AreEqual(0f, NineMileCreekStarterSplat.ForeshoreCoverage(
                At(MeanWater, pondGround: 1f)), 1e-6f,
                "a barachois is a lagoon behind a spit, not a surf-worked strand");

            // Feathered, not a stripe.
            float nearTop = NineMileCreekStarterSplat.ForeshoreCoverage(
                At(NineMileCreekStarterSplat.SpringHighWater - 0.05f));
            Assert.That(nearTop, Is.GreaterThan(0f).And.LessThan(0.2f),
                "the band's edge must feather — a hard edge reads as a painted stripe");
        }

        [Test]
        public void Rockweed_OnlyOnThePlansRock_AndStopsAtTheDryingLine()
        {
            // ⭐ The kit's rule: a frond needs something to hold. On this coast "something to hold" is
            // what the COAST PLAN declares, not what a bearing guesses.
            foreach (var e in new[] { NineMileCreekStarterSplat.SpringLowWater + 0.1f, MeanWater,
                                      NineMileCreekStarterSplat.NeapHighWater - 0.1f })
                Assert.AreEqual(0f, NineMileCreekStarterSplat.RockweedCoverage(At(e)), 1e-6f,
                    $"rockweed on the soft coast at {e:0.##} m — nothing there for a holdfast");

            Assert.Greater(NineMileCreekStarterSplat.RockweedCoverage(
                At(NineMileCreekStarterSplat.NeapHighWater - 0.3f, rockCoast: true)), 0.5f);
            Assert.AreEqual(0f, NineMileCreekStarterSplat.RockweedCoverage(
                At(NineMileCreekStarterSplat.SpringHighWater + 0.01f, rockCoast: true)), 1e-6f,
                "above the highest water the belt is gone entirely");

            // The belt is densest BELOW its own drying ceiling, not at it.
            float atNeap = NineMileCreekStarterSplat.RockweedCoverage(
                At(NineMileCreekStarterSplat.NeapHighWater, rockCoast: true));
            float belowNeap = NineMileCreekStarterSplat.RockweedCoverage(
                At(NineMileCreekStarterSplat.NeapHighWater - 0.3f, rockCoast: true));
            Assert.Greater(belowNeap, atNeap);

            // And never on the wharf: a weed belt on a quay deck is a wet plank, not a shore.
            Assert.AreEqual(0f, NineMileCreekStarterSplat.RockweedCoverage(
                At(MeanWater, rockCoast: true, madeGround: 1f)), 1e-6f);
        }

        [Test]
        public void LedgeAndTalus_AreDisjointBySlope_AndBothNeedRock()
        {
            float flat = 0f;
            float steep = NineMileCreekStarterSplat.TalusSlopeThreshold *
                          NineMileCreekStarterSplat.TalusSlopeFullFactor * 1.2f;

            // ⭐ ONE slope term, used two ways — the two can never both claim a texel, which is what
            // keeps them disjoint by construction rather than by two thresholds kept in step by hand.
            Assert.AreEqual(0f, NineMileCreekStarterSplat.Steepness(At(0f, true, flat)), 1e-6f);
            Assert.AreEqual(1f, NineMileCreekStarterSplat.Steepness(At(0f, true, steep)), 1e-6f);

            // Pavement on the flat rock bench, low in the tide.
            Assert.Greater(NineMileCreekStarterSplat.LedgeCoverage(
                At(NineMileCreekShoreMap.SandFloorElevation, rockCoast: true, slope: flat)), 0.5f);
            // …and none of it where the face is shedding.
            Assert.AreEqual(0f, NineMileCreekStarterSplat.LedgeCoverage(
                At(NineMileCreekShoreMap.SandFloorElevation, rockCoast: true, slope: steep)), 1e-6f);

            // Scree only above the highest water (a wetted apron is shingle, which the bands draw).
            float aproned = (NineMileCreekStarterSplat.SpringHighWater +
                             NineMileCreekShoreMap.GrassFloorElevation) * 0.5f;
            Assert.Greater(NineMileCreekStarterSplat.TalusCoverage(
                At(aproned, rockCoast: true, slope: steep)), 0.5f);
            Assert.AreEqual(0f, NineMileCreekStarterSplat.TalusCoverage(
                At(NineMileCreekStarterSplat.SpringHighWater - 0.01f, rockCoast: true, slope: steep)),
                1e-6f);

            // Both are rock-coast families and both stay off the built ground.
            Assert.AreEqual(0f, NineMileCreekStarterSplat.LedgeCoverage(
                At(NineMileCreekShoreMap.SandFloorElevation, rockCoast: false, slope: flat)), 1e-6f);
            Assert.AreEqual(0f, NineMileCreekStarterSplat.TalusCoverage(
                At(aproned, rockCoast: true, slope: steep, madeGround: 1f)), 1e-6f);
        }

        [Test]
        public void TheTalusThresholdIsThisCoastsOwnBeachGradient()
        {
            // ⚠ Pinned to what the profile can actually PRODUCE. A smoothstep falloff's gradient peaks at
            // exactly 1.5× its mean, so a threshold set above that could never be reached anywhere and
            // talus would be arithmetically incapable of appearing — how St Peters' first pass shipped.
            float expected = (NineMileCreekMainland.LandElevation -
                              NineMileCreekMainland.ShelfInnerElevation) /
                             NineMileCreekMainland.ShoreFalloff;
            Assert.AreEqual(expected, NineMileCreekStarterSplat.BeachGradient, 1e-4f);

            // The cliff classes must be able to exceed it, or no face on this coast ever sheds.
            var go = new GameObject("NMCTalusGradientTest");
            try
            {
                var terrain = go.AddComponent<MainlandTidalTerrain>();
                NineMileCreekMainland.ConfigureTerrain(terrain);
                Assert.Greater(terrain.ClassGradient(CoastClass.Cliff),
                    NineMileCreekStarterSplat.TalusSlopeThreshold *
                    NineMileCreekStarterSplat.TalusSlopeFullFactor,
                    "the plan's cliff cannot reach full scree — talus would never close anywhere");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ============================ THE BEDS ============================

        [Test]
        public void EveryBedWindowTopsOutUnderHighWater_SoTheTideBaresAndDrownsIt()
        {
            // ⭐ THE WHOLE MECHANISM. There is no bed-tide component and no reveal script: the sea plane
            // covers ground below the live waterline, so a bed painted low in the tide bares and drowns
            // twice a day for free. A window reaching above high water would be a bed that never drowns.
            foreach (var (lo, hi, name) in new[]
                     {
                         (NineMileCreekStarterSplat.EelgrassLoFraction,
                          NineMileCreekStarterSplat.EelgrassHiFraction, "eelgrass"),
                         (NineMileCreekStarterSplat.OysterLoFraction,
                          NineMileCreekStarterSplat.OysterHiFraction, "oysterreef"),
                         (NineMileCreekStarterSplat.MusselLoFraction,
                          NineMileCreekStarterSplat.MusselHiFraction, "musselbed"),
                         (NineMileCreekStarterSplat.IrishmossLoFraction,
                          NineMileCreekStarterSplat.IrishmossHiFraction, "irishmoss"),
                     })
            {
                Assert.Less(lo, hi, $"{name}'s window is inverted");
                Assert.Less(NineMileCreekStarterSplat.TideElevation(hi),
                    NineMileCreekStarterSplat.SpringHighWater,
                    $"{name} reaches above the highest water — it would never drown");
            }
        }

        [Test]
        public void EveryBedPeakSitsAboveThePaintFloor()
        {
            // ⚠ The paint floor (−1.95 m) sits ABOVE spring low (−2.2 m), so a peak underneath it would
            // cap that bed's coverage at a fraction of its own intensity EVERYWHERE — a permanently
            // sparse bed that looks like a painting bug.
            foreach (var (peak, name) in new[]
                     {
                         (NineMileCreekStarterSplat.EelgrassPeakFraction, "eelgrass"),
                         (NineMileCreekStarterSplat.OysterPeakFraction, "oysterreef"),
                         (NineMileCreekStarterSplat.MusselPeakFraction, "musselbed"),
                         (NineMileCreekStarterSplat.IrishmossPeakFraction, "irishmoss"),
                     })
                Assert.Greater(NineMileCreekStarterSplat.TideElevation(peak),
                    NineMileCreekShoreMap.PaintFloorElevation,
                    $"{name}'s peak is below the paint floor — the bed can never reach full strength");
        }

        [Test]
        public void TheMusselBedIsTheOneAnOrdinaryLowTideBares()
        {
            // ⭐ Nine Mile Creek works mussels. Its bed is the highest of the four, so it is the one the
            // player actually meets on the flats rather than one that needs a spring low.
            Assert.Greater(NineMileCreekStarterSplat.MusselHiFraction,
                NineMileCreekStarterSplat.OysterHiFraction);
            Assert.Greater(NineMileCreekStarterSplat.MusselHiFraction,
                NineMileCreekStarterSplat.EelgrassHiFraction);
            Assert.Greater(NineMileCreekStarterSplat.MusselbedCoverage(
                At(NineMileCreekStarterSplat.TideElevation(
                    NineMileCreekStarterSplat.MusselPeakFraction))), 0.9f);

            // …and it is soft-ground only, off the built ground.
            Assert.AreEqual(0f, NineMileCreekStarterSplat.MusselbedCoverage(
                At(NineMileCreekStarterSplat.TideElevation(
                    NineMileCreekStarterSplat.MusselPeakFraction), rockCoast: true)), 1e-6f);
            Assert.AreEqual(0f, NineMileCreekStarterSplat.MusselbedCoverage(
                At(NineMileCreekStarterSplat.TideElevation(
                    NineMileCreekStarterSplat.MusselPeakFraction), madeGround: 1f)), 1e-6f);
        }

        [Test]
        public void OnlyTheBedsArePatchGated()
        {
            // A bed is a PLACE; a band is a zone. Gating a band would punch holes in the coast.
            var somewhere = new Vector2(120f, 60f);
            foreach (var fam in NineMileCreekStarterSplat.Families)
            {
                float gate = NineMileCreekStarterSplat.PatchGateOf(fam.Material, somewhere);
                if (NineMileCreekStarterSplat.IsBed(fam.Material))
                    Assert.That(gate, Is.InRange(0f, 1f));
                else
                    Assert.AreEqual(1f, gate, 1e-6f,
                        $"{fam.Name} is a band, not a bed — gating it would punch holes in the coast");
            }
        }

        // ============================ THE WORKED GROUND ============================

        [Test]
        public void TheYardIsDirtAndTheDecksAreStone()
        {
            Assert.AreEqual(1f, NineMileCreekStarterSplat.YardDirtCoverage(
                NineMileCreekMainland.SpitFill.Center), 1e-6f, "the spit's red-earth yard");
            Assert.AreEqual(0f, NineMileCreekStarterSplat.WharfGravelCoverage(
                NineMileCreekMainland.SpitFill.Center), 1e-6f,
                "the yard is earth, not ballast — the two vocabularies must not overlap");

            foreach (var deck in new[] { NineMileCreekMainland.NorthWallFill,
                                         NineMileCreekMainland.WestWallFill,
                                         NineMileCreekMainland.BreakwaterFill })
                Assert.AreEqual(1f, NineMileCreekStarterSplat.WharfGravelCoverage(deck.Center), 1e-6f);

            // The fields are neither.
            var fields = new Vector2(-250f, 0f);
            Assert.AreEqual(0f, NineMileCreekStarterSplat.YardDirtCoverage(fields), 1e-6f);
            Assert.AreEqual(0f, NineMileCreekStarterSplat.WharfGravelCoverage(fields), 1e-6f);
        }

        [Test]
        public void ThePondsGradeMudToMarshToSedge_AndNoneOfItLeavesThePond()
        {
            float mud = NineMileCreekShoreMap.SandFloorElevation - 1.5f;
            float rim = NineMileCreekShoreMap.SandFloorElevation +
                        NineMileCreekStarterSplat.PondFringeMetres;
            float above = NineMileCreekShoreMap.MarramFloorElevation;

            Assert.Greater(NineMileCreekStarterSplat.PondSiltCoverage(At(mud, pondGround: 1f)), 0.9f);
            Assert.Greater(NineMileCreekStarterSplat.PondMarshCoverage(At(rim, pondGround: 1f)), 0.9f);
            Assert.Greater(NineMileCreekStarterSplat.PondSedgeCoverage(At(above, pondGround: 1f)), 0.9f);

            // ⭐ Nothing here escapes the carve. A marsh that leaked onto the fields would put salt
            // marsh across a hay field, which is the wrong side of the shore entirely.
            foreach (var e in new[] { mud, rim, above })
            {
                Assert.AreEqual(0f, NineMileCreekStarterSplat.PondSiltCoverage(At(e)), 1e-6f);
                Assert.AreEqual(0f, NineMileCreekStarterSplat.PondMarshCoverage(At(e)), 1e-6f);
                Assert.AreEqual(0f, NineMileCreekStarterSplat.PondSedgeCoverage(At(e)), 1e-6f);
            }
        }

        // ============================ THE PASS ORDER ============================

        [Test]
        public void Families_ArePaintedInCanonicalOrder_WithNoDuplicates()
        {
            var seen = new HashSet<int>();
            foreach (var fam in NineMileCreekStarterSplat.Families)
            {
                Assert.IsTrue(seen.Add(fam.Material), $"{fam.Name} listed twice");
                Assert.That(fam.Intensity, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f),
                    $"{fam.Name}'s ladder position is outside 0..1");
            }

            // The stacking, stated: the natural shore first, then the beds ON it, then the ponds over
            // both. Ledge before rockweed in particular — the weed belt draws ledge's upper edge.
            var order = new List<int>();
            foreach (var fam in NineMileCreekStarterSplat.Families) order.Add(fam.Material);
            Assert.Less(order.IndexOf(NineMileCreekStarterSplat.Ledge),
                        order.IndexOf(NineMileCreekStarterSplat.Rockweed));
            Assert.Less(order.IndexOf(NineMileCreekStarterSplat.Rockweed),
                        order.IndexOf(NineMileCreekStarterSplat.Irishmoss),
                        "the moss must claim the low rock the weed belt would otherwise drape all the " +
                        "way down");
            Assert.Less(order.IndexOf(NineMileCreekStarterSplat.Eelgrass),
                        order.IndexOf(NineMileCreekStarterSplat.Musselbed),
                        "the beds stack lowest-first — that order IS the zonation");
        }

        // ============================ IDEMPOTENCE — THE ACCEPTANCE ============================

        private const int W = 76, H = 56;   // 10 m per texel over the region — coarse but real geography
        private static Vector2 WorldSize => NineMileCreekMainland.RegionWorldSize;
        private static Vector2 WorldMin =>
            NineMileCreekMainland.RegionWorldCenter - NineMileCreekMainland.RegionWorldSize * 0.5f;

        [Test]
        public void ThePass_IsIdempotent_RunningItTwiceReproducesTheSamePixels()
        {
            // ⭐ THE ACCEPTANCE. Every stroke here is EXCLUSIVE, and an exclusive stroke lerps its own
            // channel from whatever is beneath — so painting over a previous pass converges toward the
            // target instead of reproducing it, and run 2 differs from run 1. A pass that must
            // "re-derive after a seabed re-bake" cannot blend the new answer into the old one.
            var go = new GameObject("NMCTerrain_IdempotenceTest");
            try
            {
                var terrain = go.AddComponent<MainlandTidalTerrain>();
                NineMileCreekMainland.ConfigureTerrain(terrain);

                Color[][] Run()
                {
                    var layers = new Color[TerrainSplatBrush.TextureCount][];
                    for (int t = 0; t < layers.Length; t++) layers[t] = new Color[W * H];
                    NineMileCreekStarterSplat.PaintInto(layers, W, H, WorldMin, WorldSize, terrain);
                    return layers;
                }

                Color[][] first = Run();
                Color[][] second = Run();
                for (int t = 0; t < first.Length; t++)
                    CollectionAssert.AreEqual(first[t], second[t],
                        $"Splat{TerrainSplatBrush.TextureSuffixes[t]} differs between two clean runs");

                // And re-running INTO the previous result — the way the menu actually re-runs — must land
                // on the same pixels too, not converge toward them.
                Color[][] reused = first;
                NineMileCreekStarterSplat.PaintInto(reused, W, H, WorldMin, WorldSize, terrain);
                for (int t = 0; t < reused.Length; t++)
                    CollectionAssert.AreEqual(second[t], reused[t],
                        $"Splat{TerrainSplatBrush.TextureSuffixes[t]} accumulated when re-run over its " +
                        "own output — the pass is not idempotent");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ThePass_ActuallyPaintsSomething_AndTheYardWinsAgainstTheShore()
        {
            // ⚠ An idempotence test passes trivially on a pass that paints NOTHING. This is the guard
            // that says the maps are not simply blank, and it checks the one ordering decision that
            // makes this place read as a working wharf: the made ground goes down LAST.
            var go = new GameObject("NMCTerrain_CoverageTest");
            try
            {
                var terrain = go.AddComponent<MainlandTidalTerrain>();
                NineMileCreekMainland.ConfigureTerrain(terrain);

                var layers = new Color[TerrainSplatBrush.TextureCount][];
                for (int t = 0; t < layers.Length; t++) layers[t] = new Color[W * H];
                NineMileCreekStarterSplat.PaintInto(layers, W, H, WorldMin, WorldSize, terrain);

                int painted = 0;
                for (int i = 0; i < W * H; i++)
                    for (int t = 0; t < layers.Length; t++)
                        if (layers[t][i].r + layers[t][i].g + layers[t][i].b + layers[t][i].a > 1e-4f)
                        { painted++; break; }
                Assert.Greater(painted, W * H / 20,
                    "fewer than 5% of texels carry any paint at all — the pass is drawing nothing");

                // The middle of the spit's yard: dirt, and NOT foreshore.
                int yard = TexelAt(NineMileCreekMainland.SpitFill.Center);
                Assert.Greater(ChannelAt(layers, NineMileCreekStarterSplat.Dirt, yard), 0f,
                    "the spit's yard carries no dirt — the wharf reads as whatever shore it was built on");
                Assert.AreEqual(0f, ChannelAt(layers, NineMileCreekStarterSplat.Foreshore, yard), 1e-6f,
                    "the yard still shows foreshore through it — a yard the sea had taken back");

                // The breakwater crest: stone.
                int crest = TexelAt(NineMileCreekMainland.BreakwaterFill.Center);
                Assert.Greater(ChannelAt(layers, NineMileCreekStarterSplat.Shingle, crest), 0f,
                    "the crib breakwater carries no stone");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The texel index a world position falls in, on the test grid.</summary>
        private static int TexelAt(Vector2 worldPos)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - WorldMin.x) / WorldSize.x * W), 0, W - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((worldPos.y - WorldMin.y) / WorldSize.y * H), 0, H - 1);
            return y * W + x;
        }

        /// <summary>One material's painted channel at a texel, across the five maps.</summary>
        private static float ChannelAt(Color[][] layers, int material, int texel) =>
            TerrainSplatBrush.GetChannel(
                layers[TerrainSplatBrush.TextureOf(material)][texel],
                TerrainSplatBrush.ChannelOf(material));

        // ============================ THE COMMITTED MAPS ============================

        [Test]
        public void CommittedSplatMaps_ImportLinear()
        {
            // ⚠ THE IMPORT SETTINGS ARE LOAD-BEARING. A splat channel is a blend WEIGHT and a ladder
            // POSITION, not a colour: an sRGB import would gamma-warp every painted value the shader
            // reads (0.5 "base" would arrive as ~0.21).
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
            {
                string path = TerrainSplatAssets.PathOf(i, NineMileCreekStarterSplat.SplatBaseName);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Assert.Ignore($"{path} is not committed yet — nothing to pin.");
                    return;
                }
                Assert.IsFalse(importer.sRGBTexture, $"{path} imports as sRGB — every weight is warped");
                Assert.IsTrue(importer.isReadable, $"{path} is not CPU-readable — the brush cannot edit it");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression,
                    $"{path} is compressed — painted bytes will not survive exactly");
            }
        }
    }
}
