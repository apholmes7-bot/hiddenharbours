using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ADR 0040 B2 — the bounded curling sheet's contract.</b> The curl is a read of the bore the
    /// water already draws: it emits only on plunging surf the seabed and the tide have earned, keeps
    /// to the picked stretch, runs on the published bore clock and is a pure function of the sea and
    /// that clock. It writes nothing a hull or the surf reads; its pool is fixed and empties on calm,
    /// on the zero dial and on unload; and a steady frame allocates nothing.
    ///
    /// <para>The fixture is a straight beach rising toward +y under one swell from the south: steep
    /// enough to plunge at <see cref="PlungingSlope"/>, a spilling and a surging beach either side
    /// of it, and the site centred where the still water is <see cref="SiteDepth"/> deep.</para>
    /// </summary>
    public class CurlingBreakerTests
    {
        private const float G = 9.81f;
        private const float Wavelength = 30f;
        private const float Amplitude = 0.3f;
        private const float Sharpening = 2.6f;
        private const float BeachBase = -1.5f;
        private const float PlungingSlope = 0.15f;
        private const float SpillingSlope = 0.02f;
        private const float SurgingSlope = 0.6f;
        private const float SiteDepth = 0.6f;
        private const float StretchMeters = 6f;
        private const float LipThrowPerHeight = 1.2f;
        private const float LipWidthMeters = 0.15f;
        private const float FrameSeconds = 1f / 60f;
        private const float ShoreFadeBand = 0.5f;
        private const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursCurlingBreaker.shader";
        private const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        // An iso frame of the water's own shape (a 37° camera); the curl only has to share it.
        private static readonly WaterIsoDepthFrame Frame = new WaterIsoDepthFrame(0f, 0.8f, 0.6f, 0f);
        private static readonly DisplacedSeaState Sea = new DisplacedSeaState(1f, ShoreFadeBand, 1f);
        private static readonly CurlingBreakerLook Look =
            new CurlingBreakerLook(1f, 1f, 1f, LipThrowPerHeight, LipWidthMeters);

        private readonly object _publisher = new object();
        private GameObject _hostGo;
        private CurlingBreakerSiteDef[] _sites = Array.Empty<CurlingBreakerSiteDef>();
        private object _sink;

        /// <summary>A straight beach: elevation = <see cref="BeachBase"/> + slope·y, with an optional
        /// dry rock spit across the surf zone (x in <c>[spitFrom, spitTo]</c>).</summary>
        private sealed class Beach : ITidalTerrain
        {
            private readonly float _slope;
            private readonly float _spitFrom;
            private readonly float _spitTo;

            public Beach(float slope, float spitFrom = float.NaN, float spitTo = float.NaN)
            {
                _slope = slope;
                _spitFrom = spitFrom;
                _spitTo = spitTo;
            }

            public float ElevationAt(Vector2 p)
                => p.x >= _spitFrom && p.x <= _spitTo ? 1f : BeachBase + _slope * p.y;
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostGo != null) Object.DestroyImmediate(_hostGo);
            _hostGo = null;
            for (int i = 0; i < _sites.Length; i++)
                if (_sites[i] != null) Object.DestroyImmediate(_sites[i]);
            _sites = Array.Empty<CurlingBreakerSiteDef>();
            SharedWaveField.Clear(_publisher);
            DisplacedSea.Clear(_publisher);
            CurlingBreakerPasses.Lower();
            _sink = null;
        }

        // =========================================================================================
        //  Where it may draw
        // =========================================================================================

        [Test]
        public void OnlyPlungingStateEmits()
        {
            CurlingBreakerSettings settings = On();
            Assert.Greater(LiveFrames(new Beach(PlungingSlope), SiteOn(PlungingSlope), in settings), 0,
                "premise: the plunging beach earns a curl");

            // Spilling and surging beaches carry WORKING surf at the site — the plunging gate refuses them.
            foreach (float slope in new[] { SpillingSlope, SurgingSlope })
            {
                var beach = new Beach(slope);
                Vector2 centre = SiteOn(slope);
                SurfState surf = SurfOf(beach, centre, LiveTimeOr(beach, centre, 0d));
                Assert.IsTrue(surf.IsWorking, $"premise: the slope {slope} beach has surf at its site");
                Assert.Less(surf.PlungingWeight01, settings.MinPlungingWeight,
                    $"premise: the slope {slope} beach does not plunge");
                Assert.AreEqual(0, LiveFrames(beach, centre, in settings), $"the slope {slope} beach drew a curl");
            }

            var plunging = new Beach(PlungingSlope);
            Assert.AreEqual(0, LiveFrames(plunging, new Vector2(0f, ShoreY(PlungingSlope) + 2f), in settings),
                "dry land drew a curl");
            Assert.AreEqual(0, LiveFrames(plunging, new Vector2(0f, -20f), in settings), "deep water drew a curl");
        }

        [Test]
        public void PhaseAndBoundsMatchBore()
        {
            CurlingBreakerSettings settings = On();
            int rows = CurlingBreakerSampler.RowsFor(in settings);
            var vertices = new Vector3[CurlingBreakerPool.Slot.MaxSheetVertices];
            var uvs = new Vector4[CurlingBreakerPool.Slot.MaxSheetVertices];
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            float period = BreakerMath.PeriodSeconds(Train(0f));
            var pose = new CurlingBreakerPose();

            // Two tides: the bore runs at √(g·d) for the depth under the site, whatever that depth is.
            foreach (float level in new[] { 0f, 0.3f })
            {
                int live = 0, preCrest = 0;
                for (int f = 0; f * FrameSeconds < period; f++)
                {
                    double t = f * FrameSeconds;
                    CurlingBreakerInputs inputs = Inputs(beach, t, level);
                    CurlingBreakerSampler.Sample(in inputs, WaveFetchSample.Open, centre, StretchMeters,
                        in settings, in Look, pose);
                    SurfState mid = CurlingBreakerSampler.SurfAt(in inputs, WaveFetchSample.Open, centre);
                    float q = CurlingBreakerMath.PhaseFraction(mid.BorePhaseDegrees, period);
                    if (CurlingBreakerMath.StageAt(q, in settings).Cover01 <= 0f)
                    {
                        Assert.IsFalse(pose.Live, $"level {level}, frame {f}: a curl outside its event (q {q})");
                        continue;
                    }
                    if (!pose.Live) continue;
                    live++;
                    Assert.AreEqual(q, pose.Phase, $"level {level}, frame {f}: the curl left the bore clock");

                    int middle = pose.Columns / 2;
                    float ahead = Vector2.Dot(centre - pose.Front[middle], pose.Shoreward);
                    float expected = -q * period * Mathf.Sqrt(G * Mathf.Max(mid.DepthMeters, BreakerMath.MinDepthMeters));
                    Assert.AreEqual(expected, ahead, 1e-3f, $"level {level}, frame {f}: the front is not the bore's");
                    if (q < 0f)
                    {
                        preCrest++;
                        Assert.Greater(ahead, 0f, $"level {level}, frame {f}: before the crest the front is seaward");
                    }

                    Assert.LessOrEqual(pose.Throw, pose.FullThrow + 1e-6f);
                    Assert.LessOrEqual(pose.Peak, settings.HeightFraction * mid.StandingHeightMeters + 1e-6f);
                    Assert.AreEqual(0f, pose.Shoreward.x, 1e-4f, "premise: the beach faces +y");
                    Bounds bounds = CurlingBreakerSampler.FillSheet(pose, in Frame, rows, vertices, uvs);
                    bounds.Expand(1e-3f);
                    for (int i = 0; i < pose.Columns * rows; i++)
                    {
                        Assert.IsTrue(bounds.Contains(vertices[i]), $"level {level}, frame {f}: vertex {i} outside its bounds");
                        Assert.LessOrEqual(Mathf.Abs(vertices[i].x - centre.x), StretchMeters * 0.5f + 1e-4f,
                            $"level {level}, frame {f}: vertex {i} left the picked stretch");
                    }
                }
                Assert.Greater(live, 0, $"premise: level {level} earns a curl");
                Assert.Greater(preCrest, 0, $"premise: level {level} shows the curl before its crest");
            }
        }

        [Test]
        public void EndTapersStayInsidePickedStretch()
        {
            CurlingBreakerSettings settings = On();
            Vector2 centre = SiteOn(PlungingSlope);
            float half = StretchMeters * 0.5f;
            var pose = new CurlingBreakerPose();
            var beach = new Beach(PlungingSlope);
            double t = LiveTime(beach, centre);
            Sample(beach, t, centre, pose);
            Assert.IsTrue(pose.Live, "premise: live");
            for (int c = 0; c < pose.Columns; c++)
            {
                Assert.LessOrEqual(Mathf.Abs(pose.Along[c]), half + 1e-5f, $"column {c} outside the stretch");
                float inside = half - Mathf.Abs(pose.Along[c]);
                if (inside <= 0f) Assert.AreEqual(0f, CurlingBreakerSampler.ColumnAlpha(pose, c), $"end column {c} drew");
                else if (inside < settings.EndTaperMeters) Assert.Less(pose.Taper[c], 1f, $"column {c} not tapered");
                else Assert.AreEqual(1f, pose.Taper[c], $"interior column {c} tapered");
            }

            // A dry spit under one interior column: that column draws nothing, the rest still do.
            float spitX = pose.Front[pose.Columns - 3].x;
            var spit = new Beach(PlungingSlope, spitX - 0.1f, spitX + 0.1f);
            Sample(spit, t, centre, pose);
            Assert.IsTrue(pose.Live, "premise: a spit off the centre and ends leaves the curl live");
            Assert.IsFalse(pose.Valid[pose.Columns - 3], "the column over the spit bridged dry land");
            Assert.AreEqual(0f, CurlingBreakerSampler.ColumnAlpha(pose, pose.Columns - 3), "the curl drew over dry land");
            Assert.Greater(CurlingBreakerSampler.ColumnAlpha(pose, pose.Columns / 2), 0f, "the wet centre stopped drawing");

            // A dry END: the stretch has not earned its curl, so the site yields whole.
            var headland = new Beach(PlungingSlope, centre.x + half - 0.1f, centre.x + half + 10f);
            Assert.AreEqual(0, LiveFrames(headland, centre, in settings), "a stretch with a dry end drew a curl");
        }

        // =========================================================================================
        //  The clock, and purity
        // =========================================================================================

        [Test]
        public void UsesThePublishedBoreClock()
        {
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            double live = LiveTime(beach, centre);
            var onClock = new CurlingBreakerPose();
            var published = new CurlingBreakerPose();
            // The water reads the PUBLISHED phases at time 0; the static field at game time t is the same sea.
            foreach (double t in new[] { live, live + FrameSeconds * 7, live + FrameSeconds * 19 })
            {
                CurlingBreakerSettings settings = On();
                CurlingBreakerInputs a = Inputs(beach, t, 0f);
                CurlingBreakerInputs b = Inputs(beach, 0d, 0f, TrainAt(t));
                CurlingBreakerSampler.Sample(in a, WaveFetchSample.Open, centre, StretchMeters, in settings, in Look, onClock);
                CurlingBreakerSampler.Sample(in b, WaveFetchSample.Open, centre, StretchMeters, in settings, in Look, published);
                Assert.AreEqual(onClock.Live, published.Live, $"t {t}: live differs between the clocks");
                Assert.AreEqual(onClock.Phase, published.Phase, 1e-4f, $"t {t}: the bore clocks differ");
                for (int c = 0; c < onClock.Columns; c++)
                    Assert.AreEqual(0f, (onClock.Front[c] - published.Front[c]).magnitude, 1e-3f, $"t {t}: column {c}");
            }

            // The live read takes the published trains at time 0 on the drawn wave scale.
            float strength = FoamInjectionRegistry.SurfDepositStrength;
            float scale = FoamInjectionRegistry.DrawnWaveScale;
            try
            {
                const float drawnScale = 1.25f;
                WaveTrains trains = Trains(TrainAt(live));
                SharedWaveField.Publish(_publisher, in trains);
                DisplacedSea.Publish(_publisher, in Sea);
                FoamInjectionRegistry.PublishSurfDeposit(strength, drawnScale);
                CurlingBreakerRegion host = MakeHost(Site("breaker_site.clock", centre, true));
                Assert.IsTrue(host.TryReadLive(beach, 0f, out CurlingBreakerInputs inputs), "premise: a published sea reads");
                Assert.AreEqual(0d, inputs.TimeSeconds, "the live read is not at time 0");
                Assert.AreEqual(drawnScale, inputs.FreqScale, "the live read is not on the drawn wave scale");
                Assert.AreEqual(trains.Dominant.PhaseOffset, inputs.Trains.Dominant.PhaseOffset,
                    "the live read is not the published field");
            }
            finally
            {
                FoamInjectionRegistry.PublishSurfDeposit(strength, scale);
            }
        }

        [Test]
        public void IsAPureFunctionOfSeedTimeAndPosition()
        {
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            double t = LiveTime(beach, centre);
            var first = new CurlingBreakerPose();
            var other = new CurlingBreakerPose();
            Sample(beach, t, centre, first);
            Assert.IsTrue(first.Live, "premise: live");

            // A seek far ahead, another site, then back: the same inputs give the same pose, exactly.
            Sample(beach, t + 1000d, centre, other);
            Sample(beach, t, centre + new Vector2(2f, 0f), other);
            Sample(beach, t, centre, other);
            AssertSamePose(first, other);

            // And a fresh pose sampled once, in any order relative to the rest.
            var fresh = new CurlingBreakerPose();
            Sample(beach, t, centre, fresh);
            AssertSamePose(first, fresh);
        }

        [Test]
        public void LeavesTheGameplaySurfUntouched()
        {
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            double t = LiveTime(beach, centre);
            WaveTrains trains = Trains(TrainAt(t));
            SharedWaveField.Publish(_publisher, in trains);
            DisplacedSea.Publish(_publisher, in Sea);
            CurlingBreakerRegion host = MakeHost(Site("breaker_site.identity", centre, true));
            CurlingBreakerSettings settings = On();
            Vector2[] probes =
            {
                centre, centre + new Vector2(-3f, 0f), centre + new Vector2(3f, 0f),
                centre + new Vector2(0f, -1.5f), centre + new Vector2(1f, 1f), centre + new Vector2(0f, -6f),
            };
            CurlingBreakerInputs inputs = Inputs(beach, 0d, 0f, TrainAt(t));
            var before = new SurfState[probes.Length];
            for (int i = 0; i < probes.Length; i++) before[i] = GameplaySurf(beach, in trains, probes[i]);

            // Interleaved: a curl frame, a gameplay read, a curl frame, the live read, a gameplay read.
            for (int round = 0; round < 3; round++)
            {
                host.Step(in inputs, in Frame, in settings, in Look);
                Assert.AreEqual(1, host.LiveCount, "premise: the curl is live while the surf is read");
                for (int i = 0; i < probes.Length; i++)
                    AssertSameSurf(before[i], GameplaySurf(beach, in trains, probes[i]), $"round {round}, probe {i}");
                Assert.IsTrue(host.TryReadLive(beach, 0f, out _), "premise: the live read");
            }

            Assert.IsTrue(SharedWaveField.TryGet(out WaveTrains after), "the published field went away");
            Assert.AreEqual(trains.Count, after.Count, "the published field changed");
            for (int i = 0; i < trains.Count; i++)
            {
                Assert.AreEqual(trains[i].PhaseOffset, after[i].PhaseOffset, $"published train {i} phase changed");
                Assert.AreEqual(trains[i].Amplitude, after[i].Amplitude, $"published train {i} amplitude changed");
            }
            Assert.IsTrue(DisplacedSea.TryGet(out DisplacedSeaState sea), "the displaced sea went away");
            Assert.AreEqual(Sea.Exaggeration, sea.Exaggeration, "the displaced sea changed");
        }

        // =========================================================================================
        //  The pool, the dial and the passes
        // =========================================================================================

        [Test]
        public void CalmReleasesPool()
        {
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            double t = LiveTime(beach, centre);
            CurlingBreakerSiteDef site = Site("breaker_site.calm", centre, true);
            CurlingBreakerRegion host = MakeHost(site);
            CurlingBreakerSettings settings = On();
            CurlingBreakerPool.Slot slot = host.Pool[0];
            CurlingBreakerInputs live = Inputs(beach, t, 0f);
            CurlingBreakerInputs calm = Inputs(beach, t, 0f, new WaveTrain(Vector2.up, Wavelength, 0f, 0f, G));

            host.Step(in live, in Frame, in settings, in Look);
            Assert.AreEqual(1, host.LiveCount, "premise: a live frame fills the slot");
            Assert.AreSame(site, slot.Site);

            host.Step(in calm, in Frame, in settings, in Look);
            Assert.AreEqual(0, host.LiveCount, "a calm sea kept a curl");
            Assert.AreEqual(0, host.Pool.ClaimedCount, "a calm sea kept its slot claimed");

            host.Step(in live, in Frame, in settings, in Look);
            Assert.AreEqual(1, host.LiveCount, "premise: the swell returns, so does the curl");
            host.Step(in live, in Frame, CurlingBreakerSettings.Default, in Look);
            Assert.AreEqual(0, host.Pool.ClaimedCount, "the master at 0 kept a slot");

            // Two picked sites, one slot: the second yields and the pool does not grow.
            host.Configure(new[] { site, Site("breaker_site.calm_second", centre, true) }, null, null);
            host.Step(in live, in Frame, in settings, in Look);
            Assert.AreEqual(1, host.LiveCount, "the second site took a slot the pool does not have");
            Assert.AreEqual(1, CurlingBreakerPool.Capacity);
            Assert.AreSame(slot, host.Pool[0], "the pool remade its slot");
            Assert.Throws<IndexOutOfRangeException>(() => _sink = host.Pool[1], "the pool grew");
        }

        [Test]
        public void NoSiteDrawsNothing()
        {
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            CurlingBreakerInputs live = Inputs(beach, LiveTime(beach, centre), 0f);
            CurlingBreakerSettings settings = On();
            CurlingBreakerRegion host = MakeHost();

            host.Step(in live, in Frame, in settings, in Look);
            AssertDrawsNothing(host, in settings, "no site");

            host.Configure(new[] { Site("breaker_site.off", centre, false) }, null, null);
            host.Step(in live, in Frame, in settings, in Look);
            AssertDrawsNothing(host, in settings, "a disabled site");

            host.Configure(new[] { Site("breaker_site.zero", centre, true) }, null, null);
            host.Step(in live, in Frame, CurlingBreakerSettings.Default, in Look);
            AssertDrawsNothing(host, CurlingBreakerSettings.Default, "the master at 0");
        }

        [Test]
        public void ZeroDialRecordsNoPass()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(shipped, "premise: the shipped GameConfig");
            Assert.AreEqual(0, shipped.CurlingBreakers.Master, "the curl's master must ship at 0");
            Assert.AreEqual(0, CurlingBreakerSettings.Default.Master, "the curl's default must be 0");

            const int width = 1920, height = 1080;
            Assert.IsFalse(CurlingBreakerPasses.Plan(shipped.CurlingBreakers, 1, width, height).Record,
                "the shipped dial recorded a curl pass");
            Assert.IsFalse(CurlingBreakerPasses.Plan(On(), 0, width, height).Record, "no live curl recorded a pass");
            CurlingBreakerSettings on = On();
            CurlPassPlan plan = CurlingBreakerPasses.Plan(in on, 1, width, height);
            Assert.IsTrue(plan.Record, "premise: a live curl at master 1 records");
            Assert.AreEqual(Mathf.RoundToInt(width * on.MaskScale), plan.MaskWidth);
            Assert.AreEqual(Mathf.RoundToInt(height * on.MaskScale), plan.MaskHeight);

            // The live frame at the shipped dial leaves no slot for a pass to draw.
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            CurlingBreakerInputs live = Inputs(beach, LiveTime(beach, centre), 0f);
            CurlingBreakerRegion host = MakeHost(Site("breaker_site.dial", centre, true));
            host.Step(in live, in Frame, shipped.CurlingBreakers, in Look);
            Assert.AreEqual(0, CurlingBreakerRegistry.LiveCurls, "the shipped dial left a curl live");
            CurlingBreakerPasses.Lower();
            Assert.AreEqual(0f, Shader.GetGlobalFloat(CurlingBreakerPasses.EnabledId), "the handoff flag stayed up");
        }

        [Test]
        public void HandoffLeavesNoDoubleLip()
        {
            CurlingBreakerSettings settings = On();
            int rows = CurlingBreakerSampler.RowsFor(in settings);
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            var pose = new CurlingBreakerPose();
            Sample(beach, LiveTime(beach, centre), centre, pose);
            Assert.IsTrue(pose.Live, "premise: live");

            // The mask hides the flat lip with the very alpha the sheet draws with, column by column.
            var sheet = new Vector3[CurlingBreakerPool.Slot.MaxSheetVertices];
            var sheetUv = new Vector4[CurlingBreakerPool.Slot.MaxSheetVertices];
            var mask = new Vector3[CurlingBreakerPool.Slot.MaxMaskVertices];
            var maskUv = new Vector4[CurlingBreakerPool.Slot.MaxMaskVertices];
            CurlingBreakerSampler.FillSheet(pose, in Frame, rows, sheet, sheetUv);
            CurlingBreakerSampler.FillMask(pose, in Frame, LipWidthMeters, mask, maskUv);
            for (int c = 0; c < pose.Columns; c++)
                for (int r = 0; r < rows; r++)
                    for (int m = 0; m < 2; m++)
                        Assert.AreEqual(sheetUv[c * rows + r].z, maskUv[c * 2 + m].z, $"column {c}: mask and sheet differ");

            // The band covers the flat lip ([T − W, T + W]) and barrel ([0, 1.15 T]) at every throw it draws.
            Vector2 band = CurlingBreakerMath.HandoffBand(pose.FullThrow, LipWidthMeters);
            float width = Mathf.Max(LipWidthMeters, CurlingBreakerMath.LipWidthFloorMeters);
            for (int k = 0; k <= 4; k++)
            {
                float throwMeters = pose.FullThrow * k / 4f;
                Assert.LessOrEqual(band.x, throwMeters - width, $"throw {throwMeters}: the lip starts before the band");
                Assert.GreaterOrEqual(band.y, throwMeters + width, $"throw {throwMeters}: the lip ends past the band");
                Assert.LessOrEqual(band.x, 0f);
                Assert.GreaterOrEqual(band.y, CurlingBreakerMath.BarrelReachThrows * throwMeters,
                    $"throw {throwMeters}: the barrel ends past the band");
            }

            // The water's handoff touches only the lip and the barrel, and only under the flag.
            string water = File.ReadAllText(WaterShaderPath);
            Match block = Regex.Match(water, @"if \(_HHCurlEnabled > 0\.5\)\s*\{(?<body>[^}]*)\}");
            Assert.IsTrue(block.Success, "the water shader has no curl handoff block");
            Assert.AreEqual(1, Regex.Matches(water, @"_HHCurlEnabled > 0\.5").Count, "more than one handoff");
            foreach (Match assignment in Regex.Matches(block.Groups["body"].Value, @"(?<target>\w+)\s*(\*|\+|-|/)?=[^=]"))
                CollectionAssert.Contains(new[] { "curlUV", "curlCover", "surfLip", "surfBarrel" },
                    assignment.Groups["target"].Value, "the handoff wrote something other than the lip and barrel");
            StringAssert.Contains("surfLip *= 1.0 - curlCover;", block.Value);
            StringAssert.Contains("surfBarrel *= 1.0 - curlCover;", block.Value);
        }

        [Test]
        public void SceneUnloadClearsPoolAndMask()
        {
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            CurlingBreakerInputs live = Inputs(beach, LiveTime(beach, centre), 0f);
            CurlingBreakerSettings settings = On();
            CurlingBreakerSiteDef site = Site("breaker_site.unload", centre, true);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader, "premise: the curl shader");

            CurlingBreakerRegion host = MakeHost(site);
            host.Configure(new[] { site }, null, shader);
            host.Step(in live, in Frame, in settings, in Look);
            CurlingBreakerPool.Slot slot = host.Pool[0];
            Assert.IsTrue(slot.Live && slot.Sheet != null && slot.Mask != null && host.Material != null,
                "premise: a live slot with its meshes and material");
            var before = new CurlingBreakerPose();
            Copy(slot.Pose, before);
            Mesh sheet = slot.Sheet;
            Material material = host.Material;

            Object.DestroyImmediate(_hostGo);
            _hostGo = null;
            Assert.AreEqual(0, CurlingBreakerRegistry.Count, "the unloaded host stayed registered");
            Assert.AreEqual(0, CurlingBreakerRegistry.LiveCurls, "an unloaded scene left a curl live");
            Assert.IsTrue(sheet == null && material == null, "the unloaded host left its mesh or material");
            Assert.IsFalse(slot.Live, "the unloaded slot is still live");
            Assert.IsFalse(CurlingBreakerPasses.Plan(in settings, CurlingBreakerRegistry.LiveCurls, 1920, 1080).Record,
                "an unloaded scene still plans a mask");
            CurlingBreakerPasses.Lower();
            Assert.AreEqual(0f, Shader.GetGlobalFloat(CurlingBreakerPasses.EnabledId), "the handoff flag stayed up");

            // Loading the scene again draws the same curl.
            CurlingBreakerRegion again = MakeHost(site);
            again.Step(in live, in Frame, in settings, in Look);
            Assert.IsTrue(again.Pool[0].Live, "the reloaded host drew no curl");
            AssertSamePose(before, again.Pool[0].Pose);
        }

        [Test]
        public void PoolAndMeshUpdatesAllocateNothing()
        {
            var beach = new Beach(PlungingSlope);
            Vector2 centre = SiteOn(PlungingSlope);
            CurlingBreakerSettings settings = On();
            float period = BreakerMath.PeriodSeconds(Train(0f));
            int frames = Mathf.CeilToInt(period / FrameSeconds);
            var inputs = new CurlingBreakerInputs[frames];
            int liveFrames = 0;
            for (int f = 0; f < frames; f++) inputs[f] = Inputs(beach, f * FrameSeconds, 0f);
            CurlingBreakerRegion host = MakeHost(Site("breaker_site.alloc", centre, true));

            // Warm-up: one period builds the meshes and their topology; a period holds live and idle frames.
            for (int f = 0; f < frames; f++)
            {
                host.Step(in inputs[f], in Frame, in settings, in Look);
                if (host.LiveCount > 0) liveFrames++;
            }
            Assert.Greater(liveFrames, 0, "premise: the period draws a curl");
            Assert.Less(liveFrames, frames, "premise: the period has idle frames too");

            Assert.That(() => { _sink = new object[64]; }, Is.AllocatingGCMemory(),
                "harness: the GC.Alloc recorder saw no allocation at all");
            Assert.That(() => StepAll(host, inputs, in settings), Is.Not.AllocatingGCMemory(),
                "a period of curl frames allocated (rule 7: nothing per frame)");

            long before = GC.GetAllocatedBytesForCurrentThread();
            _sink = new object[64];
            if (GC.GetAllocatedBytesForCurrentThread() - before <= 0)
            {
                Debug.Log("[CurlingBreakerTests] GC.GetAllocatedBytesForCurrentThread counts nothing on this " +
                          "runtime, so its arm is skipped. The GC.Alloc recorder above is the measurement.");
                return;
            }
            before = GC.GetAllocatedBytesForCurrentThread();
            StepAll(host, inputs, in settings);
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before,
                "a period of curl frames allocated bytes on the thread's own counter");
        }

        // =========================================================================================
        //  Fixture
        // =========================================================================================

        private static CurlingBreakerSettings On()
        {
            CurlingBreakerSettings settings = CurlingBreakerSettings.Default;
            settings.Master = 1;
            return settings;
        }

        private static float ShoreY(float slope) => -BeachBase / slope;

        /// <summary>The site on a beach: where the still water is <see cref="SiteDepth"/> deep.</summary>
        private static Vector2 SiteOn(float slope) => new Vector2(0f, (-BeachBase - SiteDepth) / slope);

        private static WaveTrain Train(float phaseOffset) => new WaveTrain(Vector2.up, Wavelength, Amplitude, phaseOffset, G);

        /// <summary>The swell as published at game time <paramref name="t"/>: its phase carried on by ω·t,
        /// read at time 0 (what the field publisher hands the water and every reader of it).</summary>
        private static WaveTrain TrainAt(double t)
        {
            WaveTrain still = Train(0f);
            float omega = 2f * Mathf.PI / still.Wavelength * still.PhaseSpeed;
            return Train((float)(still.PhaseOffset - omega * t));
        }

        private static WaveTrains Trains(in WaveTrain swell)
        {
            var silent = new WaveTrain(swell.Direction, swell.Wavelength, 0f, 0f, G);
            return new WaveTrains(swell, silent, silent, silent, 1, Sharpening);
        }

        private static CurlingBreakerInputs Inputs(ITidalTerrain terrain, double t, float level)
            => Inputs(terrain, t, level, Train(0f));

        private static CurlingBreakerInputs Inputs(ITidalTerrain terrain, double t, float level, in WaveTrain swell)
        {
            BreakerSettings breakers = BreakerSettings.Default;
            WaveTrains trains = Trains(in swell);
            BreakerContour contour = BreakerMath.ContourFor(in swell, 1f, in breakers);
            return new CurlingBreakerInputs(terrain, level, in contour, in trains, WaveFieldBridge.Pack(in trains), G,
                in breakers, 1f, t, in Sea);
        }

        private static void Sample(ITidalTerrain terrain, double t, Vector2 centre, CurlingBreakerPose pose)
        {
            CurlingBreakerSettings settings = On();
            CurlingBreakerInputs inputs = Inputs(terrain, t, 0f);
            CurlingBreakerSampler.Sample(in inputs, WaveFetchSample.Open, centre, StretchMeters, in settings, in Look, pose);
        }

        private static SurfState SurfOf(ITidalTerrain terrain, Vector2 at, double t)
        {
            CurlingBreakerInputs inputs = Inputs(terrain, t, 0f);
            return CurlingBreakerSampler.SurfAt(in inputs, WaveFetchSample.Open, at);
        }

        /// <summary>The surf a hull reads: the same field and terrain, straight from Core.</summary>
        private static SurfState GameplaySurf(ITidalTerrain terrain, in WaveTrains trains, Vector2 at)
        {
            BreakerSettings breakers = BreakerSettings.Default;
            WaveTrain dominant = trains.Dominant;
            BreakerContour contour = BreakerMath.ContourFor(in dominant, 1f, in breakers);
            return BreakerMath.SurfAt(at, 0f, terrain, in contour, WaveFetchSample.Open, in trains, G, in breakers, 1f, 0d);
        }

        /// <summary>How many 60 Hz frames of one period draw a curl at <paramref name="centre"/>.</summary>
        private static int LiveFrames(ITidalTerrain terrain, Vector2 centre, in CurlingBreakerSettings settings)
        {
            float period = BreakerMath.PeriodSeconds(Train(0f));
            var pose = new CurlingBreakerPose();
            int live = 0;
            for (int f = 0; f * FrameSeconds < period; f++)
            {
                CurlingBreakerInputs inputs = Inputs(terrain, f * FrameSeconds, 0f);
                CurlingBreakerSampler.Sample(in inputs, WaveFetchSample.Open, centre, StretchMeters, in settings,
                    in Look, pose);
                if (pose.Live) live++;
            }
            return live;
        }

        /// <summary>The first frame of one period with a live curl, else <paramref name="fallback"/>.</summary>
        private static double LiveTimeOr(ITidalTerrain terrain, Vector2 centre, double fallback)
        {
            float period = BreakerMath.PeriodSeconds(Train(0f));
            var pose = new CurlingBreakerPose();
            for (int f = 0; f * FrameSeconds < period; f++)
            {
                Sample(terrain, f * FrameSeconds, centre, pose);
                if (pose.Live) return f * FrameSeconds;
            }
            return fallback;
        }

        private static double LiveTime(ITidalTerrain terrain, Vector2 centre)
        {
            double t = LiveTimeOr(terrain, centre, double.NaN);
            Assert.IsFalse(double.IsNaN(t), "premise: the plunging beach draws a curl within one period");
            return t;
        }

        private CurlingBreakerSiteDef Site(string id, Vector2 centre, bool enabled)
        {
            var site = ScriptableObject.CreateInstance<CurlingBreakerSiteDef>();
            site.Id = id;
            site.RegionId = "region.curl_fixture";
            site.Centre = centre;
            site.LengthMeters = StretchMeters;
            site.Enabled = enabled;
            Array.Resize(ref _sites, _sites.Length + 1);
            _sites[_sites.Length - 1] = site;
            return site;
        }

        private CurlingBreakerRegion MakeHost(params CurlingBreakerSiteDef[] sites)
        {
            _hostGo = new GameObject("CurlingBreakerTests host");
            var host = _hostGo.AddComponent<CurlingBreakerRegion>();
            host.Configure(sites, null, null);
            return host;
        }

        private static void StepAll(CurlingBreakerRegion host, CurlingBreakerInputs[] inputs,
                                    in CurlingBreakerSettings settings)
        {
            for (int f = 0; f < inputs.Length; f++) host.Step(in inputs[f], in Frame, in settings, in Look);
        }

        private static void AssertDrawsNothing(CurlingBreakerRegion host, in CurlingBreakerSettings settings, string label)
        {
            Assert.AreEqual(0, host.LiveCount, label + ": a slot is live");
            Assert.AreEqual(0, host.Pool.ClaimedCount, label + ": a slot is claimed");
            Assert.AreEqual(0, CurlingBreakerRegistry.LiveCurls, label + ": the registry counts a curl");
            Assert.IsFalse(CurlingBreakerPasses.Plan(in settings, CurlingBreakerRegistry.LiveCurls, 1920, 1080).Record,
                label + ": a pass would record");
        }

        private static void Copy(CurlingBreakerPose from, CurlingBreakerPose to)
        {
            Array.Copy(from.Front, to.Front, CurlingBreakerPose.MaxColumns);
            Array.Copy(from.Along, to.Along, CurlingBreakerPose.MaxColumns);
            Array.Copy(from.Taper, to.Taper, CurlingBreakerPose.MaxColumns);
            Array.Copy(from.Valid, to.Valid, CurlingBreakerPose.MaxColumns);
            Array.Copy(from.LiftFront, to.LiftFront, CurlingBreakerPose.MaxColumns);
            Array.Copy(from.LiftLand, to.LiftLand, CurlingBreakerPose.MaxColumns);
            to.Columns = from.Columns;
            to.Live = from.Live;
            to.Centre = from.Centre;
            to.Shoreward = from.Shoreward;
            to.Phase = from.Phase;
            to.Period = from.Period;
            to.FullThrow = from.FullThrow;
            to.Throw = from.Throw;
            to.Peak = from.Peak;
            to.Cover = from.Cover;
            to.Whiten = from.Whiten;
        }

        private static void AssertSamePose(CurlingBreakerPose a, CurlingBreakerPose b)
        {
            Assert.AreEqual(a.Live, b.Live, "live");
            Assert.AreEqual(a.Columns, b.Columns, "columns");
            AssertBits(a.Phase, b.Phase, "phase");
            AssertBits(a.Period, b.Period, "period");
            AssertBits(a.FullThrow, b.FullThrow, "full throw");
            AssertBits(a.Throw, b.Throw, "throw");
            AssertBits(a.Peak, b.Peak, "peak");
            AssertBits(a.Cover, b.Cover, "cover");
            AssertBits(a.Whiten, b.Whiten, "whiten");
            AssertBits(a.Shoreward.x, b.Shoreward.x, "shoreward x");
            AssertBits(a.Shoreward.y, b.Shoreward.y, "shoreward y");
            for (int c = 0; c < a.Columns; c++)
            {
                AssertBits(a.Front[c].x, b.Front[c].x, $"column {c} front x");
                AssertBits(a.Front[c].y, b.Front[c].y, $"column {c} front y");
                AssertBits(a.Along[c], b.Along[c], $"column {c} along");
                AssertBits(a.Taper[c], b.Taper[c], $"column {c} taper");
                Assert.AreEqual(a.Valid[c], b.Valid[c], $"column {c} valid");
                AssertBits(a.LiftFront[c], b.LiftFront[c], $"column {c} front lift");
                AssertBits(a.LiftLand[c], b.LiftLand[c], $"column {c} landing lift");
            }
        }

        private static void AssertSameSurf(in SurfState a, in SurfState b, string label)
        {
            AssertBits(a.DepthMeters, b.DepthMeters, label + " depth");
            AssertBits(a.ShorewardDirection.x, b.ShorewardDirection.x, label + " shoreward x");
            AssertBits(a.ShorewardDirection.y, b.ShorewardDirection.y, label + " shoreward y");
            AssertBits(a.Breaking01, b.Breaking01, label + " breaking");
            AssertBits(a.Whitewater01, b.Whitewater01, label + " whitewater");
            AssertBits(a.StandingHeightMeters, b.StandingHeightMeters, label + " standing height");
            AssertBits(a.PlungingWeight01, b.PlungingWeight01, label + " plunging weight");
            AssertBits(a.Bore01, b.Bore01, label + " bore");
            AssertBits(a.BorePhaseDegrees, b.BorePhaseDegrees, label + " bore phase");
            AssertBits(a.TravelSeconds, b.TravelSeconds, label + " travel");
            AssertBits(a.BirthEnergy01, b.BirthEnergy01, label + " birth energy");
            AssertBits(a.RunUpMeters, b.RunUpMeters, label + " run-up");
        }

        private static void AssertBits(float a, float b, string label)
            => Assert.AreEqual(BitConverter.SingleToInt32Bits(a), BitConverter.SingleToInt32Bits(b), $"{label}: {a} vs {b}");
    }
}
