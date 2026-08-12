using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ADR 0023 phase 3, step 1 — THE WATERLINE ON THE HULL, adjudicated in pixels through the
    /// PRODUCTION path: the lobster-boat mesh (IsoFacetHullRenderer, facet MRT + keyline resolve
    /// + overlay quad) and the displaced sea (the water shader's HHWaterDisplaced pass, drawn by
    /// IsoFacetHullFeature's water renderer list into its own target against the SHARED private
    /// depth buffer, composed in-scene by the WaterOverlay quad under the hull's sorting slot).
    /// Every render goes through <c>Camera.Render()</c> with the project's own 2D renderer.
    ///
    /// <para><b>What is being proved.</b> The calibrated cross-object iso-depth convention
    /// (DisplacedWaterRegistry.WaterIsoDepthFrame): with the hull translated into the water's
    /// depth frame, the lifted surface truthfully covers the planking below it — the waterline
    /// CLIMBS the planking as the reference sea's swell passes (the spike's probe,
    /// productionised), the upper hull stays intact, and turning the displaced sea off restores
    /// today's render byte-for-byte. The sabotage flips the z convention (the sign of the
    /// water's <c>_WaterIsoDepth</c> height term) and asserts the climb metric goes red.</para>
    ///
    /// <para><b>Determinism.</b> The sea is the reference scenario (wind (−5.4, −9.33) m/s,
    /// seaState 0.75, WaveFieldSettings.Default — ShoreFadeMathTests' pinned sea), evaluated by
    /// the Core WaveMath twin to CHOOSE the two phases (the highest and lowest surface at the
    /// hull over a fixed window), then published to the shader through the production packing
    /// (WaveFieldBridge.Pack, phases baked at the chosen time in double — the WaveFieldBridge
    /// discipline, as ShoreSeamProof does).</para>
    ///
    /// <para><b>Harness traps honoured.</b> No Water.mat (a FRESH material: the baked St Peters
    /// height map trap cannot fire), _USE_HEIGHTTEX off AND a black 1×1 height texture bound
    /// (belt and braces), uniform-deep sea (depth ≫ band ⇒ shore fade exactly 1); the
    /// render-graph camera path with plain LEqual (no hand-rolled reversed-Z — ADR 0023 trap
    /// (1) applies to raw command buffers only); shader warm-up before every measurement (the
    /// cold-cache trap); Null-Device gate FIRST (CI has no GPU and would CRASH, not fail).</para>
    /// </summary>
    public class HullWaterlineAcceptanceTests
    {
        const int ProbeLayer = 31;

        /// <summary>
        /// The waterline bar: the MEDIAN per-column height (px) of the water's bottom-contiguous
        /// covered run up the planking at the reference crest. ⚠️ MEASURED (RTX 4060, D3D12,
        /// 2026-07-23), then pinned: at the crest (h 0.950 m × 1.5) every one of the 202 measured
        /// columns wears a run — median 10 px, p90 13 px, 8,719 submerged planking px — while the
        /// trough (h −1.046 m) is bone dry (0 covered px; the rig's origin is the KEEL, so the
        /// whole trough swing opens air under the boat). The flipped-z sabotage lands at exactly
        /// 0. Bars sit at just over half the measured medians so a real regression (no z-bite, or
        /// wrong draw order) fails loudly and hardware fill noise cannot.
        /// </summary>
        const int MinMedianRunPx = 6;
        const int MinP90RunPx = 10;
        const int MinSubmergedPx = 2000;

        /// <summary>
        /// The WATERTIGHT bars (owner playtest 2026-07-23 — "water enters hull on the mesh
        /// models"). The storm scenario (reference wind ×2.2 ≈ 23.7 m/s, sea state 1.0 —
        /// a full gale) drives lifts far past every interior surface; the shipped
        /// <c>WatertightDeckHeightMeters</c> clamp must keep BOARDED water (covered hull pixels
        /// disconnected from the bottom-contiguous waterline run — the flooded sole / hold /
        /// inner-bulwark read) at SPECK LEVEL (<see cref="MaxBoardedResiduePx"/>, 30–150× under
        /// the measured defect class) at every adjudicated instant. The exterior
        /// climb's survival is pinned where it belongs — the REFERENCE sea with the production
        /// clamp (a gale's footprint bound may legitimately pin a sole-at-the-waterline hull at
        /// her marks; a daily sea must not dry her). The unclamped control (deck height 0 — the
        /// pre-fix state) must show the defect loudly, or the metric proved nothing. Bars sit
        /// far below the measured healthy values (logged by every run) so hardware fill noise
        /// cannot trip them while a real regression still fails loudly — measured numbers live
        /// in the tests' Debug.Log lines and the PR that shipped this fix.
        /// </summary>
        const int StormBoardedGapRows = 2;
        const int StormMinUnclampedBoardedPx = 1900;
        /// <summary>Speck tolerance on CLAMPED boarded water: the discrete scan leaves 1–2 px
        /// wide residues on thin rigging/rail features at single instants (measured 0–40 px
        /// across every clamped scenario, 2026-07-23 — hugging the aft-quarter rigging, invisible
        /// in the dumps at 1:1). The DEFECT class — solid water painted over sole/hold/deck —
        /// measures 1,800–9,900 px, 30–150× this bar, so a real flood still fails loudly. Zero
        /// would cost real over-drying of the exterior climb chasing invisible pixels.</summary>
        const int MaxBoardedResiduePx = 64;
        /// <summary>The DAILY-SEA bars on the freeboard hull (dragger): the reference crest must
        /// keep a live waterline band with the production clamp — the owner's climb. p90, not
        /// median: a 25 m hull beam-on spans more sea than one reference crest, so most of her
        /// 417 columns are honestly dry at any instant even unclamped (her daily demands
        /// measured ≈ 0 — the clamp barely engages in the reference sea). Measured healthy:
        /// p90 3 px, 686 covered px.</summary>
        const int DailySeaMinCrestP90RunPx = 2;
        const int DailySeaMinSubmergedPx = 300;

        GameConfig _keylineConfig;
        GameConfig _prevConfig;

        /// <summary>ADR 0031: production ships the mesh fleet's keyline OFF, but every pinned
        /// number in this fixture was measured WITH it — the silhouette's inked extent includes
        /// the 1 px ring, and <see cref="Measure"/>'s waterline accounting explicitly counts the
        /// emergent keyline at a run's top as covered planking. Forcing the legacy look through
        /// the owner's real dial keeps the #263 pins bit-stable rather than re-baselining them;
        /// the waterline claim itself is gate-independent (the z-test happens in the facet MRT,
        /// before the resolve ever runs).</summary>
        [SetUp]
        public void ForceTheKeylineOn()
        {
            _prevConfig = GameServices.Config;
            _keylineConfig = ScriptableObject.CreateInstance<GameConfig>();
            _keylineConfig.HullKeylineFlood = true;
            GameServices.Config = _keylineConfig;
        }

        [TearDown]
        public void RestoreTheConfig()
        {
            GameServices.Config = _prevConfig;
            if (_keylineConfig != null) Object.DestroyImmediate(_keylineConfig);
            _keylineConfig = null;
        }

        static RigMeshData s_Lobster;
        static Mesh s_LobsterMesh;
        static RigMeshData s_Dragger;
        static Mesh s_DraggerMesh;

        [OneTimeTearDown]
        public void TearDown()
        {
            if (s_LobsterMesh != null) Object.DestroyImmediate(s_LobsterMesh);
            s_LobsterMesh = null;
            s_Lobster = null;
            if (s_DraggerMesh != null) Object.DestroyImmediate(s_DraggerMesh);
            s_DraggerMesh = null;
            s_Dragger = null;
        }

        /// <summary>Must be the FIRST statement of every GPU test — on a Null Device the crash
        /// happens in native rendering code no assertion can intercept.</summary>
        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null " +
                    "Device), so the hull-waterline acceptance could not render and proved " +
                    "nothing. Expected on CI; the phase 3 pixels only run on a machine with a GPU.");
            }
        }

        static void EnsureLobster()
        {
            if (s_Lobster != null) return;
            using var host = RigScriptHostFactory.Create();
            s_Lobster = RigMeshExtractor.ExtractFrom(
                host, "docs/art/rigs/lobsterBoatIsoRig.js", "LobsterBoatIso");
            s_LobsterMesh = RigMeshBuilder.Build(s_Lobster).Mesh;
        }

        static void EnsureDragger()
        {
            if (s_Dragger != null) return;
            using var host = RigScriptHostFactory.Create();
            s_Dragger = RigMeshExtractor.ExtractFrom(
                host, "docs/art/rigs/sideDraggerIsoRig.js", "SideDraggerIso");
            s_DraggerMesh = RigMeshBuilder.Build(s_Dragger).Mesh;
        }

        // ------------------------------------------------------------- the reference phases

        static readonly Vector2 ReferenceWind = new Vector2(-5.4f, -9.33f);
        const float ReferenceSeaState = 0.75f;

        /// <summary>The STORM scenario: the reference wind scaled to a full gale (≈23.7 m/s) at
        /// sea state 1.0 — the seas the owner was sailing when the dragger read as flooding.
        /// Deterministic like the reference sea (rule 5): derived, scanned, never authored.</summary>
        static readonly Vector2 StormWind = new Vector2(-5.4f, -9.33f) * 2.2f;
        const float StormSeaState = 1.0f;

        /// <summary>Scan a fixed deterministic window for the instants the surface at the hull is
        /// highest and lowest — found, not authored (the spike's discipline).</summary>
        static void FindReferencePhases(Vector2 hullPos, in WaveTrains trains,
                                        out double tHigh, out double tLow,
                                        out float hHigh, out float hLow)
        {
            tHigh = tLow = 0;
            hHigh = float.MinValue;
            hLow = float.MaxValue;
            for (double t = 0; t <= 120.0; t += 0.25)
            {
                float h = WaveMath.Sample(hullPos, t, in trains).Height;
                if (h > hHigh) { hHigh = h; tHigh = t; }
                if (h < hLow) { hLow = h; tLow = t; }
            }
        }

        /// <summary>Publish the field for a game time through the production packing — phases
        /// baked at t in DOUBLE (the WaveFieldBridge discipline; ShoreSeamProof's twin).</summary>
        static void PublishSea(in WaveTrains trains, double timeSeconds)
        {
            const double twoPi = Math.PI * 2.0;
            WaveTrains src = trains;
            WaveTrain Shifted(int i)
            {
                WaveTrain tr = src[i];
                double k = twoPi / tr.Wavelength;
                double phase = tr.PhaseOffset - k * tr.PhaseSpeed * timeSeconds;
                phase -= Math.Floor(phase / twoPi) * twoPi;
                return new WaveTrain(tr.Direction, tr.Wavelength, tr.Amplitude, (float)phase,
                                     WaveFieldSettings.Default.Gravity);
            }

            int n = trains.Count;
            var buffer = new WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < n; i++) buffer[i] = Shifted(i);
            var shifted = WaveTrains.From(buffer, n, trains.CrestSharpening, trains.DominantIndex);
            // Through the bridge's own publisher — never a hand-written copy of the uniform names,
            // or this harness starts driving a narrower field than the shipped shader reads.
            WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in shifted));
        }

        // ------------------------------------------------------------- headless (CI-safe)

        /// <summary>
        /// The pure convention pin, GPU-free so CI adjudicates it: the hull bias is the water's
        /// own vertex-stage depth applied to the hull's ground anchor and heave, and at the
        /// contact line (equal ground anchor) the z-compare reduces EXACTLY to heights — the
        /// hull point is nearer than the surface iff it sits higher than the lift.
        /// </summary>
        [Test]
        public void HullDepthBias_IsTheWaterVertexDepth_AndReducesToHeightsAtTheContactLine()
        {
            var frame = new WaterIsoDepthFrame(referenceY: -60f, cosElev: 0.766f,
                                               sinElev: 0.643f, baseZ: 0.25f);

            // The formula, literal (the HHWaterDisplaced twin applied to a hull anchor).
            Assert.AreEqual(0.25f + (12.5f - -60f) * 0.766f - 0.4f * 0.643f,
                            DisplacedWaterMath.HullDepthBias(12.5f, 0.4f, in frame), 1e-5f,
                            "HullDepthBias must be baseZ + (y − refY)·cosElev − heave·sinElev — " +
                            "the water's own vertex depth, or the shared z-buffer is not one convention.");

            // Contact-line reduction: water vertex depth at the same ground anchor with lift L
            // vs the hull at height H. Nearer = smaller z (the 2D camera looks along +Z).
            float WaterZ(float groundY, float lift) =>
                frame.BaseZ + (groundY - frame.ReferenceY) * frame.CosElev - lift * frame.SinElev;

            foreach (float y in new[] { -3f, 0f, 41.5f })
            foreach (float lift in new[] { -0.9f, 0f, 1.35f })
            {
                float above = DisplacedWaterMath.HullDepthBias(y, lift + 0.5f, in frame);
                float below = DisplacedWaterMath.HullDepthBias(y, lift - 0.5f, in frame);
                Assert.Less(above, WaterZ(y, lift),
                    $"a hull point ABOVE the surface (y {y}, lift {lift}) must be NEARER than the water");
                Assert.Greater(below, WaterZ(y, lift),
                    $"a hull point BELOW the surface (y {y}, lift {lift}) must be FARTHER than the water" +
                    " — this ordering is the waterline; flipped, the sea would never cover the planking.");
            }
        }

        // ------------------------------------------------------------- watertight (CI-safe)

        /// <summary>The watertight clamp's PER-POINT law, GPU-free so CI adjudicates it, pinned
        /// against an independent reconstruction over the reference sea's packed field: a water
        /// sample at ground offset Δ with lift L fights the height <c>r_f − tan(elev)·ry</c> on
        /// EACH ground line ry, so every fight reaching the boat's INSIDE (root-line fought
        /// height r_f = (Δ+L−H)/cos ≥ deckHeight) demands protection of the WORST line within the
        /// half-beam — <c>ry* = min(halfBeam, (r_f − deckHeight)/tan)</c>; fights against the open
        /// planking demand NOTHING (the climb keeps every truthful centimetre the interior allows).
        /// Measured lineage 2026-07-23: 1:1 cut flooded the cockpit; blanket-max cut dry-docked
        /// the dragger; root-line-only per-point cut re-flooded the far rail.
        ///
        /// <para>⚠️ <b>RE-DERIVED UNDER ADR 0033 and the shape of the law changed.</b> The protected
        /// height's coefficient <c>(cos²+sin)</c> became <c>1/sin</c>, and the explicit beam residual
        /// <c>ry*·cos·(1−sin)</c> — "§24's beam residual" — <b>cancelled to exactly zero</b> against
        /// the y→z shear. It was never a shave to be tightened: it was the unit error that put a
        /// north-sailing stern 1.64 m in front of the sea (#491), and the shear pays it off at the
        /// beam as well as fore-and-aft. ry* still chooses WHICH height on the fought line is worst;
        /// only the charge for standing on that line has gone. Net: the clamp demands strictly LESS
        /// heave than before, so it shoves a hull toward the camera less — never more, so this
        /// cannot newly flood one.</para>
        [Test]
        public void WatertightZHeave_IsThePerPointFootprintLaw()
        {
            const float c = 0.766f, s = 0.643f;
            const float tan = s / c;
            // The hull's own screen heave. ⚠️ It is NEGATIVE here on purpose and that is the whole
            // point of this test: the ride subtracts the resting draft and the sharpened field sits
            // below still water most of its period, so a hull is DOWN in a trough with the sea
            // standing over her far more often than she is up on a crest. The law shipped without
            // its two H terms and was therefore short by H·cot(elev) ≈ 1.19·|H| — every hull,
            // always, by more than the 0.4 m safety could absorb (owner playtest 2026-07-25).
            const float H = -1.1f;

            WaveTrains trains = WaveMath.TrainsFrom(ReferenceWind, ReferenceSeaState,
                                                    WaveFieldSettings.Default);
            PackedWaveField field = WaveFieldBridge.Pack(in trains);

            // Both frequency scales, because the clamp must guard the sea that is DRAWN. 1 is the
            // shader's shipped default; 2.8 is what every one of the owner's water presets actually
            // carries (_OceanSwellScale 0.07 / WAVE_LEGACY_SCALE_REF 0.025), and the clamp used to
            // scan at 1 regardless — hunting crests 2.8× too far apart.
            foreach (float freqScale in new[] { 1f, 2.8f })
            {
                var frame = new WaterIsoDepthFrame(referenceY: -60f, cosElev: 0.766f,
                                                   sinElev: 0.643f, baseZ: 0.25f,
                                                   exaggeration: 1.5f, freqScale: freqScale);

                foreach (Vector2 center in new[] { Vector2.zero, new Vector2(37.5f, -12.25f) })
                foreach (float halfWidth in new[] { 7.125f, 14f })
                foreach ((float deckH, float halfBeam) in new[] { (0.5f, 2.2f), (2.05f, 3.5f) })
                {
                    float demand = float.MinValue;
                    // The scan refines with the frequency: the fixed 2 m x-step was sized against
                    // λ ≥ ~10 m, and at 2.8 those land near 3.6 m — below Nyquist.
                    float scan = Mathf.Max(1f, freqScale);
                    int nx = Mathf.CeilToInt(halfWidth * scan / DisplacedWaterMath.FootprintScanStepMeters);
                    int ny = Mathf.CeilToInt(DisplacedWaterMath.FootprintScanHalfHeightMeters * scan
                                             / DisplacedWaterMath.FootprintScanRowStepMeters);
                    for (int ix = -nx; ix <= nx; ix++)
                    for (int iy = -ny; iy <= ny; iy++)
                    {
                        float dy = DisplacedWaterMath.FootprintScanHalfHeightMeters * iy / (float)ny;
                        var p = new Vector2(center.x + halfWidth * ix / (float)nx, center.y + dy);
                        float lift = 1.5f * WaveFieldBridge.ShaderTwinSample(
                            p, in field, freqScale).Height;
                        float foughtR = (dy + lift - H) / c;
                        if (foughtR < deckH) continue;
                        float ryStar = Mathf.Min(halfBeam, (foughtR - deckH) / tan);
                        float protectedR = foughtR - tan * ryStar;
                        float need = (lift * (c + s) - protectedR / s - H * c) / s;
                        if (need > demand) demand = need;
                    }
                    // The engagement-ramped safety, mirrored (zero at the boundary, full when binding).
                    float expected = demand <= H
                        ? H
                        : demand + Mathf.Min(DisplacedWaterMath.WatertightDemandSafetyMeters,
                                             DisplacedWaterMath.WatertightSafetyRampSlope
                                             * (demand - H));

                    float actual = DisplacedWaterMath.WatertightZHeaveMeters(
                        H, deckH, halfBeam, center, halfWidth, in field, in frame);
                    Assert.AreEqual(expected, actual, 1e-4f,
                        $"WatertightZHeaveMeters at {center} halfWidth={halfWidth} deckH={deckH} " +
                        $"halfBeam={halfBeam} freqScale={freqScale} must be the per-point footprint " +
                        "law over the same published field the shader draws — a drifted grid, gate, " +
                        "worst-line, H term or demand formula un-proves every storm pixel.");
                    Assert.GreaterOrEqual(actual, H, "the clamp may only ever RAISE the z heave.");
                }
            }
        }

        /// <summary>The clamp's OFF states, bit-exact: deck height 0 (an unset def) and the
        /// silent field (no bridge — the globals read zero) must both hand the honest heave
        /// through untouched — the pre-fix render, byte-identical.</summary>
        [Test]
        public void WatertightZHeave_ZeroDeckHeightAndSilentField_Disable()
        {
            var frame = new WaterIsoDepthFrame(referenceY: -60f, cosElev: 0.766f,
                                               sinElev: 0.643f, baseZ: 0.25f, exaggeration: 1.5f);
            PackedWaveField silent = PackedWaveField.Empty;

            WaveTrains trains = WaveMath.TrainsFrom(ReferenceWind, ReferenceSeaState,
                                                    WaveFieldSettings.Default);
            PackedWaveField field = WaveFieldBridge.Pack(in trains);

            Assert.AreEqual(-1.1f, DisplacedWaterMath.WatertightZHeaveMeters(
                -1.1f, 0f, 2.2f, Vector2.zero, 7f, in field, in frame),
                "deck height 0 must disable the clamp exactly — an unset def must render " +
                "byte-identically to before this fix.");
            Assert.AreEqual(-1.1f, DisplacedWaterMath.WatertightZHeaveMeters(
                -1.1f, 2.05f, 3.5f, Vector2.zero, 7f, in silent, in frame),
                "a silent field (every height 0) must demand nothing — the clamp stays inert " +
                "in edit mode and bridge-less scenes.");
        }

        /// <summary>The shipped fix is DATA: both committed hull defs must carry a watertight
        /// line (the rig sources' own deck constants, shaved by the storm acceptance). A zeroed
        /// field here is the owner's defect shipping again.</summary>
        [Test]
        public void CommittedHullMeshDefs_CarryTheWatertightLine()
        {
            var lobster = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset");
            var dragger = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                "Assets/_Project/Data/Boats/HullMeshes/SideDraggerIsoHullMesh.asset");
            Assert.IsNotNull(lobster, "missing the committed lobster hull-mesh def");
            Assert.IsNotNull(dragger, "missing the committed side-dragger hull-mesh def");

            Assert.AreEqual(0.5f, lobster.WatertightDeckHeightMeters, 1e-4f,
                "the lobster boat's watertight line moved — her rig's cockpit sole sits at " +
                "DECK = 0.50 m above the keel (lobsterBoatIsoRig.js); update this pin ONLY " +
                "with a storm-acceptance run proving the new value keeps her dry.");
            Assert.AreEqual(2.5f, lobster.WatertightHalfBeamMeters, 1e-4f,
                "the lobster boat's watertight half-beam moved — her rig's amidships station " +
                "carries 2.20 rig-m of half-beam (lobsterBoatIsoRig.js), committed GENEROUS at " +
                "2.5 (the washboards ride the sheer OUTSIDE the station line, and the capped " +
                "protection branch only answers to this value — a 2.2 run left a measured " +
                "far-washboard streak at an off-root storm crest, 2026-07-23); update only " +
                "with a green storm run.");
            Assert.AreEqual(2.05f, dragger.WatertightDeckHeightMeters, 1e-4f,
                "the side dragger's watertight line moved — her rig's working deck sits at " +
                "DECK = 2.05 m above the keel (sideDraggerIsoRig.js); update this pin ONLY " +
                "with a storm-acceptance run proving the new value keeps her dry.");
            Assert.AreEqual(3.5f, dragger.WatertightHalfBeamMeters, 1e-4f,
                "the side dragger's watertight half-beam moved — her rig's amidships station " +
                "carries 3.50 rig-m of half-beam ('max beam 7 m', sideDraggerIsoRig.js); the " +
                "far-rail residual is EXACT through this value, so update it only with a green " +
                "storm run.");
        }

        // ------------------------------------------------------------- the waterline (GPU)

        [Test]
        public void Waterline_ClimbsThePlanking_AsTheReferenceSwellPasses()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            using var scene = new WaterlineScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(headingDirUnits: 2f);              // beam-on: the longest planking run
            byte[] baseline = scene.Render();                // hull only — today's render

            WaveTrains trains = WaveMath.TrainsFrom(ReferenceWind, ReferenceSeaState,
                                                    WaveFieldSettings.Default);
            FindReferencePhases(scene.HullWorldPos, in trains,
                                out double tHigh, out double tLow, out float hHigh, out float hLow);
            Assert.Greater(hHigh - hLow, 0.8f,
                "the reference window no longer swings the surface — the scenario moved?");

            scene.AttachWater(sabotageIsoDepthSign: false);

            PublishSea(in trains, tLow);
            byte[] low = scene.Render();
            PublishSea(in trains, tHigh);
            byte[] high = scene.Render();

            var mLow = Measure(baseline, low, s_Lobster.W, s_Lobster.H);
            var mHigh = Measure(baseline, high, s_Lobster.W, s_Lobster.H);
            Debug.Log($"[hull-waterline] tLow={tLow:F2}s h={hLow:F3}m -> {mLow}; " +
                      $"tHigh={tHigh:F2}s h={hHigh:F3}m -> {mHigh} " +
                      $"(surface swing {(hHigh - hLow):F2}m x1.5 exaggeration)");
            DumpEvidence("baseline", baseline, s_Lobster);
            DumpEvidence("low", low, s_Lobster);
            DumpEvidence("high", high, s_Lobster);

            // (1) At the trough the surface sits below the keel (the rig's origin IS the keel):
            // the hull must be bone dry — water may never paint over planking it sits under.
            Assert.LessOrEqual(mLow.SubmergedPx, 50,
                $"at the reference trough {mLow.SubmergedPx} planking px were covered — the " +
                "surface is below the whole hull there; covering anything means the z convention " +
                "is biased or flipped.");

            // (2) At the crest the sea genuinely takes the lower planking, and the hull survives.
            Assert.Greater(mHigh.SubmergedPx, MinSubmergedPx,
                $"at the reference crest only {mHigh.SubmergedPx} planking px were covered (bar " +
                $"{MinSubmergedPx}) — the shared z-test is not biting (the un-calibrated state: " +
                "hull z≈0 vs water z≈(y−refY)·cos, never comparable — exactly what this step " +
                "exists to fix), or the water pass no longer records before the hulls.");
            Assert.Greater(mHigh.VisiblePx, 1000,
                "at the reference crest the hull all but vanished — the water is winning where " +
                "the hull is HIGHER than the surface; the z convention is not calibrated.");

            // (3) The waterline MOVES up the planking between trough and crest — the owner's
            // ask. Dry trough (run 0) to a crest where EVERY measured column wears a covered
            // band, median at least MinMedianRunPx up the planking.
            Assert.AreEqual(0, mLow.RunMedianPx,
                "the trough should leave the planking dry (see (1)) — a nonzero median run " +
                "there means the resting waterline is riding up the hull.");
            Assert.GreaterOrEqual(mHigh.RunMedianPx, MinMedianRunPx,
                $"the waterline did not climb the planking at the reference crest (median run " +
                $"{mHigh.RunMedianPx}px, bar {MinMedianRunPx}px; measured healthy: 10px). Either " +
                "the water pass no longer records before the hulls (draw order is the waterline) " +
                "or the iso-depth frame is not being applied.");
            Assert.GreaterOrEqual(mHigh.RunP90Px, MinP90RunPx,
                $"the crest's wettest columns barely submerged (p90 {mHigh.RunP90Px}px, bar " +
                $"{MinP90RunPx}px; measured healthy: 13px) — the climb is not reaching the " +
                "spike-proven band.");

            // (4) The sea can NEVER reach wheelhouse/mast country: no covered pixel in the top
            // 40% of the silhouette (the crest tops out ~2 rig-metres above the keel; measured
            // margin 38 rows). The flooded cockpit sole / far-side interior BELOW that line is
            // truthful occlusion of low hull surfaces, not a defect.
            Assert.Greater(mHigh.HighestCoveredRow, mHigh.UpperCutoffRow,
                $"water covered hull pixels up at row {mHigh.HighestCoveredRow}, above the " +
                $"top-40% cutoff ({mHigh.UpperCutoffRow}) — the sea is climbing hull surfaces " +
                "it cannot physically reach; the z convention (or the overlay composition) is wrong.");

            // (5) The A/B contract at pixel level: displaced OFF restores today's render exactly.
            scene.DetachWater();
            byte[] restored = scene.Render();
            int offDiff = CountDifferingRgb(baseline, restored);
            Assert.AreEqual(0, offDiff,
                $"{offDiff} px differ from today's render after the displaced sea was turned " +
                "OFF — phase 3 must ride ONLY while the surface is active (the byte-identity " +
                "contract of the owner's A/B).");
        }

        // ------------------------------------------------------ ADR 0033: the 8-facing sweep (GPU)

        /// <summary>
        /// <b>THE OWNER'S COMPLAINT, ADJUDICATED AT EVERY HEADING</b> (ADR 0033 acceptance §2;
        /// reports 2026-08-11 "the boat is visually in front of the water when sailing north" and
        /// 2026-07-25 "when the bow faces south you see water at the stern").
        ///
        /// <para><b>Why this test did not exist before, and that is the lesson.</b> Every GPU
        /// acceptance in this file poses the hull at <c>headingDirUnits: 2</c> — beam-on, "the
        /// longest planking run". Beam-on is the ONE facing where the fore-aft depth residual is
        /// exactly zero (the hull's keel lies along screen x, so her half-length buys no world-y
        /// offset). The suite was framed on the only heading at which the defect is invisible, which
        /// is why ten months of green runs never saw it. The sweep is the fix for the suite as much
        /// as the shear is the fix for the render.</para>
        ///
        /// <para><b>What each facing must show.</b> Rig +Y is the bow, so at dir 0 (north) the stern
        /// is the hull's DEEPEST screen row — exactly where <see cref="Measure"/> starts its
        /// bottom-contiguous run, so the run median IS the stern's waterline there. Unsheared, that
        /// stern carried −1.64 m of false depth: it won the z-test against any wave at any tide and
        /// any phase, so the run could never be anything but 0. At dir 4 (south) the sign flips, the
        /// stern is up-screen, and the sea paints planking that is out of the water — which lands in
        /// <see cref="WaterlineMeasure.BoardedPx"/>, covered pixels detached from the waterline run.
        /// So the two assertions below are the owner's two sentences, one each.</para>
        ///
        /// <para><b>Tide and sea state.</b> A floating hull rides the tide, so the only thing a tide
        /// can change about her waterline is WHERE ON THE SHARED DEPTH RAMP she is calibrated — the
        /// scene is therefore built at two world-y positions 40 m apart while the sea's ground-y
        /// reference stays put, and the answer must be the same at both. Two sea states run the
        /// same sweep at a moderate and a calm swell, and <see cref="Waterline_ClimbsThePlanking_AsTheReferenceSwellPasses"/>
        /// carries the displaced-OFF byte-identity control.</para>
        ///
        /// <para><b>The sea under test is FLAT, and that is the whole design of the metric.</b> A
        /// wave sea cannot adjudicate this: the water sharing a pixel with a bow-on hull's stern is
        /// the water four metres AFT of her, which at a 10 m wavelength is most of a wave away from
        /// the crest that lifted her — so a per-heading run measured on a swell is dominated by
        /// which part of the wave each end of the boat happens to be standing in. (That confound is
        /// real, wanted, and is the waterline BREATHING; it is just not a depth-contract signal.)
        /// A flat sea with the hull sunk by her own design waterline makes the answer pure geometry:
        /// the sea must cover the planking below a FIXED rig height, and it must do so identically
        /// whichever way she is pointing. The wave seas below run for the owner's eye and for the
        /// trough-dry check, not for the bar.</para>
        ///
        /// <para>Set <c>HH_WATERLINE_DUMP</c> to a directory to get the whole sweep as PNGs — that
        /// is the owner-facing artefact this test exists to produce.</para>
        /// </summary>
        [Test]
        public void Waterline_AtEveryFacing_TheSeaReachesHerPlanking_AndStaysOutsideHer()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            // (tide label, world y). 40 m apart: far enough that a depth ramp that failed to cancel
            // its ground-y reference would be screaming, close enough to stay in one wave field.
            var tides = new[] { ("lowtide", 0f), ("hightide", 40f) };
            const float DesignWaterlineMeters = 0.5f;    // the committed lobster boat's datum
            float sink = HullSettleMath.AppliedSinkMeters(DesignWaterlineMeters, 40f);

            var failures = new System.Collections.Generic.List<string>();
            var report = new System.Text.StringBuilder(
                "[adr-0033 sweep] lobster boat, flat sea, sunk by her design waterline\n");
            var deepCovered = new System.Collections.Generic.List<float>();
            var submergedFrac = new System.Collections.Generic.List<float>();

            foreach ((string tideName, float worldY) in tides)
            for (int dir = 0; dir < 8; dir++)
            {
                using var scene = new WaterlineScene(s_Lobster, s_LobsterMesh,
                                                     worldYMeters: worldY);
                scene.SetPose(dir, heavePixels: -sink * s_Lobster.PxPerMetre);
                byte[] baseline = scene.Render();               // hull only, this heading

                scene.AttachWater(sabotageIsoDepthSign: false);
                WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);   // a dead flat sea
                byte[] flat = scene.Render();

                var m = Measure(baseline, flat, s_Lobster.W, s_Lobster.H);
                float deep = DeepestPlankingCoveredFraction(baseline, flat, s_Lobster.W, s_Lobster.H,
                                                            out int inked);
                float frac = m.SubmergedPx / (float)Mathf.Max(1, inked);
                deepCovered.Add(deep);
                submergedFrac.Add(frac);

                string tag = $"{tideName}_flat_dir{dir}";
                report.Append($"  {tag,-24} deepest-planking covered {deep * 100f,5:F1}%  " +
                              $"submerged {m.SubmergedPx,6}px of {inked,6} inked ({frac * 100f,4:F1}%)  " +
                              $"run med {m.RunMedianPx,3}px\n");
                DumpEvidence($"adr0033_{tag}_baseline", baseline, s_Lobster);
                DumpEvidence($"adr0033_{tag}_flat", flat, s_Lobster);

                // THE OWNER'S TWO SENTENCES, one assertion. On a flat sea standing above her keel,
                // the DEEPEST planking on screen is under water by construction — at every heading,
                // because "deepest on screen" is a statement about the same projection the water is
                // drawn through. Unsheared, at dir 0 the deepest planking IS the stern, carrying
                // −1.64 m of false depth: it won the z-test against any sea at any level, so this
                // read ~0 % and no wave could ever have reached it. At dir 4 the sign flips and the
                // sea instead climbs planking that is out of the water, which shows as a submerged
                // FRACTION far above the beam-on facings.
                if (deep < SweepMinDeepestCovered)
                    failures.Add($"{tag}: only {deep * 100f:F1}% of her deepest planking is under a " +
                                 $"flat sea standing above her keel (bar {SweepMinDeepestCovered * 100f:F0}%) " +
                                 "— the hull reads NEARER than the water that should be lapping it " +
                                 "(the owner's 2026-08-11 report)");
            }

            // AND THE HEADING-INDEPENDENCE ITSELF: the residual was a pure function of how much of
            // her fore-aft axis lay along world y, so a surviving one shows up as a SPREAD across
            // the eight facings — north starved, south drowned, east/west correct. One number.
            float lo = Mathf.Min(submergedFrac.ToArray()), hi = Mathf.Max(submergedFrac.ToArray());
            report.Append($"  submerged fraction across all facings: {lo * 100f:F1}% .. {hi * 100f:F1}% " +
                          $"(spread {(hi - lo) * 100f:F1} points, bar {SweepMaxSubmergedSpread * 100f:F0})\n");
            Debug.Log(report.ToString());

            if (hi - lo > SweepMaxSubmergedSpread)
                failures.Add($"the submerged fraction spans {(hi - lo) * 100f:F1} points across the " +
                             $"eight facings (bar {SweepMaxSubmergedSpread * 100f:F0}) — the waterline " +
                             "still depends on which way she is pointing, which is the defect itself");

            Assert.IsEmpty(failures,
                "ADR 0033's 8-facing sweep failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// The owner-facing sweep on REAL seas: eight headings × calm and moderate × two tides, at
        /// the crest and the trough, dumped as PNGs for his eye (<c>HH_WATERLINE_DUMP</c>). The one
        /// thing asserted here is the property a wave sea CAN adjudicate without phase confounds —
        /// at a trough below her keel she must be bone dry, at every heading. A heading-dependent
        /// depth bias shows there first, because there is no wave to hide behind.
        /// </summary>
        [Test]
        public void Waterline_AtEveryFacing_TheTroughStillBaresHer_AndTheSweepIsDumped()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var tides = new[] { ("lowtide", 0f), ("hightide", 40f) };
            var seas = new[] { ("moderate", ReferenceSeaState), ("calm", 0.28f) };
            var failures = new System.Collections.Generic.List<string>();
            var report = new System.Text.StringBuilder("[adr-0033 sweep] real seas, lobster boat\n");

            foreach ((string tideName, float worldY) in tides)
            foreach ((string seaName, float seaState) in seas)
            {
                WaveTrains trains = WaveMath.TrainsFrom(ReferenceWind, seaState,
                                                        WaveFieldSettings.Default);
                for (int dir = 0; dir < 8; dir++)
                {
                    using var scene = new WaterlineScene(s_Lobster, s_LobsterMesh,
                                                         worldYMeters: worldY);
                    scene.SetPose(dir);
                    byte[] baseline = scene.Render();

                    FindReferencePhases(scene.HullWorldPos, in trains,
                                        out double tHigh, out double tLow,
                                        out float hHigh, out float hLow);
                    scene.AttachWater(sabotageIsoDepthSign: false);

                    PublishSea(in trains, tHigh);
                    byte[] high = scene.Render();
                    PublishSea(in trains, tLow);
                    byte[] low = scene.Render();

                    var mHigh = Measure(baseline, high, s_Lobster.W, s_Lobster.H);
                    var mLow = Measure(baseline, low, s_Lobster.W, s_Lobster.H);

                    string tag = $"{tideName}_{seaName}_dir{dir}";
                    report.Append($"  {tag,-28} crest h={hHigh,5:F2}m sub {mHigh.SubmergedPx,6}px " +
                                  $"run med {mHigh.RunMedianPx,3}px | trough h={hLow,5:F2}m " +
                                  $"sub {mLow.SubmergedPx,6}px\n");
                    DumpEvidence($"adr0033_{tag}_crest", high, s_Lobster);
                    DumpEvidence($"adr0033_{tag}_trough", low, s_Lobster);

                    if (mLow.SubmergedPx > SweepMaxTroughSubmergedPx)
                        failures.Add($"{tag}: {mLow.SubmergedPx}px covered at a trough {hLow:F2} m " +
                                     $"below still water (bar {SweepMaxTroughSubmergedPx}px) — the " +
                                     "surface is under her keel there, so covering anything means " +
                                     "the depth ramp carries a heading-dependent bias");
                }
            }

            Debug.Log(report.ToString());
            Assert.IsEmpty(failures,
                "ADR 0033's real-sea sweep failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>What fraction of the hull's DEEPEST planking pixels the sea has taken. "Deepest"
        /// is per column — the lowest inked pixel of each inked column — so it follows the silhouette
        /// at any heading instead of assuming one. That is the whole waterline question reduced to a
        /// number that does not care what shape the boat presents.</summary>
        static float DeepestPlankingCoveredFraction(byte[] baseline, byte[] composed, int w, int h,
                                                    out int inkedPx)
        {
            int deepest = 0, covered = 0;
            inkedPx = 0;
            for (int x = 0; x < w; x++)
            {
                int bottom = -1;
                for (int y = 0; y < h; y++)
                    if (baseline[(y * w + x) * 4 + 3] > 0) { bottom = y; inkedPx++; }
                if (bottom < 0) continue;
                deepest++;
                int i = (bottom * w + x) * 4;
                bool same = composed[i] == baseline[i] && composed[i + 1] == baseline[i + 1] &&
                            composed[i + 2] == baseline[i + 2];
                if (!same) covered++;
            }
            return deepest == 0 ? 0f : covered / (float)deepest;
        }

        /// <summary>
        /// The sweep's bars. ⚠️ <b>MEASURED on the RTX 4060 (D3D12) both ways</b> — with the shear
        /// live and with <c>DisplacedWaterMath.HullDepthShear</c> forced to 0 — and set so the
        /// sheared render clears them and the unsheared one does not. That A/B is the whole reason
        /// they are numbers and not opinions:
        ///
        /// <code>
        /// deepest planking covered, flat sea, sunk to her datum   no shear  →  sheared
        ///   dir 0  NORTH  (the 2026-08-11 report)                    0.0 %  →   72.2 %
        ///   dir 1 / 7                                               47.0 %  →   95.5 %
        ///   dir 2 / 6  EAST-WEST (the control)                      97.5 %  →   97.3 %
        ///   dir 3 / 5                                               64.1 %  →   95.7 %
        ///   dir 4  SOUTH  (the 2026-07-25 report)                    0.0 %  →   23.6 %
        ///   submerged fraction, spread across the eight facings     20.7 pt →    6.0 pt
        /// </code>
        ///
        /// <para>Read the two ends of that table together and it is the diagnosis, in pixels: at
        /// NORTH the sea could not touch her deepest planking AT ALL — not "rarely", zero — while
        /// EAST-WEST, the one pair with no fore-aft component along world y, does not move at all
        /// (97.5 → 97.3, the half-beam term). SOUTH is the same defect wearing the other sign: 22.9 %
        /// of a bow-on silhouette drowned on a flat sea at half a metre of draft, now 2.8 %.</para>
        ///
        /// <para><b>Why the deepest-covered bar sits at 15 % and not at 90 %.</b> "The deepest
        /// planking on screen" is a shape fact as well as a depth fact: bow-on (dir 4) a lobster
        /// boat shows her stem, which rises, so most columns' lowest visible pixel is genuinely
        /// ABOVE her waterline and 23.6 % is the correct answer. The bar has to clear the honest
        /// shape floor while still being infinitely above the unsheared 0.0 %, and the SPREAD bar is
        /// what guards the six facings in between.</para>
        /// </summary>
        const float SweepMinDeepestCovered = 0.15f;
        const float SweepMaxSubmergedSpread = 0.10f;
        const int SweepMaxTroughSubmergedPx = 200;

        // ------------------------------------------------------------- sabotage (GPU)

        /// <summary>
        /// ⚠️ Flip the z convention and watch it fail: the water material's <c>_WaterIsoDepth</c>
        /// height sign is negated (a lifted crest steps FARTHER instead of nearer — the exact
        /// disagreement class a partial calibration would ship), the frame republished from the
        /// same material as production would. The crest then cannot cover the planking and the
        /// climb metric collapses/reverses — proof the acceptance above can see this defect.
        /// </summary>
        [Test]
        public void Sabotage_FlippedIsoDepthHeightSign_IsCaught()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            using var scene = new WaterlineScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(headingDirUnits: 2f);
            byte[] baseline = scene.Render();

            WaveTrains trains = WaveMath.TrainsFrom(ReferenceWind, ReferenceSeaState,
                                                    WaveFieldSettings.Default);
            FindReferencePhases(scene.HullWorldPos, in trains,
                                out double tHigh, out double tLow, out _, out _);

            scene.AttachWater(sabotageIsoDepthSign: true);

            PublishSea(in trains, tLow);
            byte[] low = scene.Render();
            PublishSea(in trains, tHigh);
            byte[] high = scene.Render();

            var mLow = Measure(baseline, low, s_Lobster.W, s_Lobster.H);
            var mHigh = Measure(baseline, high, s_Lobster.W, s_Lobster.H);
            Debug.Log($"[hull-waterline][SABOTAGE] flipped height sign: low {mLow}; " +
                      $"high {mHigh} (healthy: crest median run >= {MinMedianRunPx}px, measured 0 here)");

            Assert.Less(mHigh.RunMedianPx, MinMedianRunPx,
                "SABOTAGE NOT DETECTED — with the water's height-vs-depth sign flipped the " +
                "waterline still climbed at the crest. The acceptance cannot see a flipped z " +
                "convention and every green run above is worth less than it looks.");
            Assert.Less(mHigh.SubmergedPx, MinSubmergedPx,
                "SABOTAGE NOT DETECTED — a flipped-z sea still submerged real planking at the crest.");
        }

        // ------------------------------------------------------------- watertight storm (GPU)

        /// <summary>The instants a storm is most dangerous to an unclamped hull: the surface's
        /// highest moments AT the hull and at four footprint offsets (a crest OFF the root is
        /// what the single-point ride cannot follow — the slope-flooding case). Scanned over a
        /// fixed deterministic window, deduped, root's crest first.</summary>
        static double[] StormInstants(Vector2 hullPos, in WaveTrains trains, float offsetMeters)
        {
            var candidates = new System.Collections.Generic.List<double>();
            var probes = new[]
            {
                hullPos,
                hullPos + new Vector2(offsetMeters, 0f), hullPos - new Vector2(offsetMeters, 0f),
                hullPos + new Vector2(0f, offsetMeters), hullPos - new Vector2(0f, offsetMeters),
            };
            foreach (Vector2 p in probes)
            {
                FindReferencePhases(p, in trains, out double tHigh, out _, out float hHigh, out _);
                Assert.Greater(hHigh, 0.5f, $"the storm scan at {p} found no real crest — scenario broke?");
                bool duplicate = false;
                foreach (double t in candidates)
                    if (Math.Abs(t - tHigh) < 1.0) { duplicate = true; break; }
                if (!duplicate) candidates.Add(tHigh);
            }
            return candidates.ToArray();
        }

        /// <summary>
        /// THE OWNER'S DEFECT, adjudicated (playtest 2026-07-23: "water enters hull on the mesh
        /// models"): in a full gale the shipped watertight line must keep every interior surface
        /// — cockpit sole, hold floor, inner bulwarks, top deck — free of water at every
        /// adjudicated instant (BOARDED px == 0), while the exterior waterline still climbs the
        /// planking (the effect the owner loves stays alive) and the wheelhouse country stays
        /// untouched. The deck height driven here is the COMMITTED def's value — this is the
        /// production data on trial, not a test fixture.
        /// </summary>
        [Test]
        public void Storm_LobsterBoat_IsWatertight_TheWaterlineNeverBoardsHer()
        {
            RequireAGraphicsDevice();
            EnsureLobster();
            StormWatertightAcceptance(s_Lobster, s_LobsterMesh,
                "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset", "lobster");
        }

        /// <summary>The boat the owner NAMED in the playtest — her open working deck and hold
        /// are the biggest interior a storm can paint over. Same law as the lobster. (The
        /// CLIMB's survival is deliberately NOT demanded in the gale — interior protection may
        /// legitimately occupy the whole freeboard there; the daily-sea reference tests below
        /// own the climb contract.)</summary>
        [Test]
        public void Storm_SideDragger_IsWatertight_TheWaterlineNeverBoardsHer()
        {
            RequireAGraphicsDevice();
            EnsureDragger();
            StormWatertightAcceptance(s_Dragger, s_DraggerMesh,
                "Assets/_Project/Data/Boats/HullMeshes/SideDraggerIsoHullMesh.asset", "dragger");
        }

        static void StormWatertightAcceptance(RigMeshData data, Mesh mesh, string defPath,
                                              string label)
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(defPath);
            Assert.IsNotNull(def, $"missing the committed def at {defPath}");
            Assert.Greater(def.WatertightDeckHeightMeters, 0f,
                $"{label}: the committed def carries NO watertight line — the owner's flooding " +
                "defect is shipping again (WatertightDeckHeightMeters must be > 0).");
            Assert.Greater(def.WatertightHalfBeamMeters, 0f,
                $"{label}: the committed def carries NO watertight half-beam — the far-rail " +
                "residual is unprotected (WatertightHalfBeamMeters must be > 0).");

            using var scene = new WaterlineScene(data, mesh, def.WatertightDeckHeightMeters,
                                                 def.WatertightHalfBeamMeters);
            scene.SetPose(headingDirUnits: 2f);              // beam-on: the longest planking run
            byte[] baseline = scene.Render();

            WaveTrains trains = WaveMath.TrainsFrom(StormWind, StormSeaState,
                                                    WaveFieldSettings.Default);
            float offset = 0.35f * data.W / data.PxPerMetre;   // ~half the hull off the root
            double[] instants = StormInstants(scene.HullWorldPos, in trains, offset);

            scene.AttachWater(sabotageIsoDepthSign: false);

            for (int i = 0; i < instants.Length; i++)
            {
                PublishSea(in trains, instants[i]);
                byte[] frame = scene.Render();
                var mask = new bool[data.W * data.H];
                var m = Measure(baseline, frame, data.W, data.H, mask);
                Debug.Log($"[hull-waterline][storm:{label}] t={instants[i]:F2}s -> {m}");
                DumpEvidence($"{label}_storm_{i}", frame, data);
                if (m.BoardedPx > 0)
                    DumpBoardedEvidence($"{label}_storm_{i}_boarded", baseline, mask, data);

                // THE FIX: no visible water inside the boat — no flooded sole, no water inside
                // the bulwarks, no wet top deck; only the speck-tolerance for 1–2 px thin-
                // feature residue (see MaxBoardedResiduePx). (The storm demands DRYNESS only:
                // a gale's protection can legitimately occupy the whole freeboard — the climb's
                // survival in daily seas is pinned by the reference tests, not here.)
                Assert.LessOrEqual(m.BoardedPx, MaxBoardedResiduePx,
                    $"{label}: {m.BoardedPx} px of BOARDED water at storm instant {i} " +
                    $"(t={instants[i]:F2}s, bar {MaxBoardedResiduePx}) — the sea is painting " +
                    "the interior again (the owner's 2026-07-23 defect). Either the watertight " +
                    "clamp is not engaging or the committed deck height / half-beam is too " +
                    "generous (re-run with HH_WATERLINE_DUMP to see the leaking pixels in red).");

                // The sea still cannot reach wheelhouse/mast country.
                Assert.Greater(m.HighestCoveredRow, m.UpperCutoffRow,
                    $"{label}: storm water reached row {m.HighestCoveredRow}, above the top-40% " +
                    $"cutoff ({m.UpperCutoffRow}) — the clamp is not bounding the climb.");
            }
        }

        /// <summary>
        /// THE OTHER HALF OF THE OWNER'S CONTRACT, stated honestly per hull. The lobster's
        /// cockpit sole sits AT her design waterline (rig DECK 0.5 = her 0.5 m draft), so
        /// watertightness pins her crest-side waterline at her marks — HER share of the living
        /// waterline is the trough swing (the sea drops away and bares her planking, then
        /// returns to her marks), which this proves in the daily reference sea along with
        /// zero boarding. The CLIMB's survival is pinned on the hull that has freeboard to
        /// spare — the dragger's live storm band in Storm_SideDragger above. (A per-face
        /// interior mask in the facet shader is the known upgrade that would give a
        /// sole-at-the-waterline hull an over-the-marks climb too — out of scope here.)
        /// </summary>
        [Test]
        public void ReferenceSea_ProductionClamp_TroughStillBares_AndNothingBoards()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset");
            Assert.IsNotNull(def, "missing the committed lobster hull-mesh def");
            Assert.Greater(def.WatertightDeckHeightMeters, 0f, "the fix is unwired");

            using var scene = new WaterlineScene(s_Lobster, s_LobsterMesh,
                                                 def.WatertightDeckHeightMeters,
                                                 def.WatertightHalfBeamMeters);
            scene.SetPose(headingDirUnits: 2f);
            byte[] baseline = scene.Render();

            WaveTrains trains = WaveMath.TrainsFrom(ReferenceWind, ReferenceSeaState,
                                                    WaveFieldSettings.Default);
            FindReferencePhases(scene.HullWorldPos, in trains,
                                out double tHigh, out double tLow, out _, out _);

            scene.AttachWater(sabotageIsoDepthSign: false);

            PublishSea(in trains, tLow);
            var mLow = Measure(baseline, scene.Render(), s_Lobster.W, s_Lobster.H);
            PublishSea(in trains, tHigh);
            byte[] high = scene.Render();
            var maskHigh = new bool[s_Lobster.W * s_Lobster.H];
            var mHigh = Measure(baseline, high, s_Lobster.W, s_Lobster.H, maskHigh);
            Debug.Log($"[hull-waterline][reference+clamp] low -> {mLow}; high -> {mHigh}");
            DumpEvidence("lobster_reference_clamped_high", high, s_Lobster);
            if (mHigh.BoardedPx > 0)
                DumpBoardedEvidence("lobster_reference_clamped_high_boarded",
                                    baseline, maskHigh, s_Lobster);

            Assert.AreEqual(0, mLow.RunMedianPx,
                "the reference trough must still bare the planking with the production clamp — " +
                "a resting run means the clamp broke the trough side of the living waterline.");
            Assert.AreEqual(0, mLow.SubmergedPx,
                "the reference trough covered planking on a clamped hull — the surface is " +
                "below her whole keel there; nothing may be covered.");
            Assert.LessOrEqual(mLow.BoardedPx + mHigh.BoardedPx, MaxBoardedResiduePx,
                $"the reference sea boarded the clamped lobster ({mLow.BoardedPx}+" +
                $"{mHigh.BoardedPx} px, bar {MaxBoardedResiduePx}; measured healthy: 0) — the " +
                "committed deck height leaks in DAILY seas.");
        }

        /// <summary>
        /// THE OWNER'S CLIMB, pinned where it lives: the dragger — real freeboard (2.05 m deck
        /// over 1.1 m draft) — must keep a LIVE median waterline band at the reference crest
        /// with the full production clamp, and nothing may board her even here. This is the
        /// assert that fails if a future "safer" clamp quietly costs the daily-sea effect the
        /// whole waterline system exists for.
        /// </summary>
        [Test]
        public void ReferenceSea_ProductionClamp_TheDraggerKeepsHerClimb()
        {
            RequireAGraphicsDevice();
            EnsureDragger();

            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                "Assets/_Project/Data/Boats/HullMeshes/SideDraggerIsoHullMesh.asset");
            Assert.IsNotNull(def, "missing the committed side-dragger hull-mesh def");
            Assert.Greater(def.WatertightDeckHeightMeters, 0f, "the fix is unwired");

            using var scene = new WaterlineScene(s_Dragger, s_DraggerMesh,
                                                 def.WatertightDeckHeightMeters,
                                                 def.WatertightHalfBeamMeters);
            scene.SetPose(headingDirUnits: 2f);
            byte[] baseline = scene.Render();

            WaveTrains trains = WaveMath.TrainsFrom(ReferenceWind, ReferenceSeaState,
                                                    WaveFieldSettings.Default);
            FindReferencePhases(scene.HullWorldPos, in trains,
                                out double tHigh, out double tLow, out _, out _);

            scene.AttachWater(sabotageIsoDepthSign: false);

            PublishSea(in trains, tLow);
            var mLow = Measure(baseline, scene.Render(), s_Dragger.W, s_Dragger.H);
            PublishSea(in trains, tHigh);
            byte[] high = scene.Render();
            var maskHigh = new bool[s_Dragger.W * s_Dragger.H];
            var mHigh = Measure(baseline, high, s_Dragger.W, s_Dragger.H, maskHigh);
            Debug.Log($"[hull-waterline][reference+clamp:dragger] low -> {mLow}; high -> {mHigh}");
            DumpEvidence("dragger_reference_clamped_high", high, s_Dragger);
            if (mHigh.BoardedPx > 0)
                DumpBoardedEvidence("dragger_reference_clamped_high_boarded",
                                    baseline, maskHigh, s_Dragger);

            Assert.GreaterOrEqual(mHigh.RunP90Px, DailySeaMinCrestP90RunPx,
                $"the owner's climb DIED in daily seas on the freeboard hull (reference crest " +
                $"p90 run {mHigh.RunP90Px}px, bar {DailySeaMinCrestP90RunPx}px) — the " +
                "watertight clamp is over-drying an ordinary sea; the fix must not cost the " +
                "effect it protects.");
            Assert.Greater(mHigh.SubmergedPx, DailySeaMinSubmergedPx,
                $"the reference crest barely wets the dragger ({mHigh.SubmergedPx} covered px, " +
                $"bar {DailySeaMinSubmergedPx}; measured healthy: 686) — the daily waterline " +
                "is not visibly alive on the freeboard hull.");
            Assert.LessOrEqual(mLow.BoardedPx + mHigh.BoardedPx, MaxBoardedResiduePx,
                $"the reference sea boarded the clamped dragger ({mLow.BoardedPx}+" +
                $"{mHigh.BoardedPx} px, bar {MaxBoardedResiduePx}) — the committed values " +
                "leak in DAILY seas.");
        }

        /// <summary>
        /// ⚠️ The control that keeps the storm assert honest: the SAME storm with the clamp OFF
        /// (deck height 0 — exactly the pre-fix production state) must flood the interior
        /// loudly. Proves both that the scenario genuinely reproduces the owner's defect and
        /// that the BOARDED metric can see it — a green watertight run above is only worth
        /// something because this goes red without the clamp.
        /// </summary>
        [Test]
        public void Storm_UnclampedHull_Floods_TheDefectAndTheMetricAreReal()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            using var scene = new WaterlineScene(s_Lobster, s_LobsterMesh,
                                                 watertightDeckHeightMeters: 0f);
            scene.SetPose(headingDirUnits: 2f);
            byte[] baseline = scene.Render();

            WaveTrains trains = WaveMath.TrainsFrom(StormWind, StormSeaState,
                                                    WaveFieldSettings.Default);
            float offset = 0.35f * s_Lobster.W / s_Lobster.PxPerMetre;
            double[] instants = StormInstants(scene.HullWorldPos, in trains, offset);

            scene.AttachWater(sabotageIsoDepthSign: false);

            int worstBoarded = 0;
            double worstT = instants[0];
            for (int i = 0; i < instants.Length; i++)
            {
                PublishSea(in trains, instants[i]);
                var m = Measure(baseline, scene.Render(), s_Lobster.W, s_Lobster.H);
                Debug.Log($"[hull-waterline][storm:UNCLAMPED] t={instants[i]:F2}s -> {m}");
                if (m.BoardedPx > worstBoarded) { worstBoarded = m.BoardedPx; worstT = instants[i]; }
            }

            Assert.Greater(worstBoarded, StormMinUnclampedBoardedPx,
                $"CONTROL FAILED — the unclamped hull boarded only {worstBoarded} px in the " +
                $"storm (bar {StormMinUnclampedBoardedPx}, worst t={worstT:F2}s). Either the " +
                "storm scenario no longer floods (owner's defect not reproduced) or the BOARDED " +
                "metric cannot see interior water — every watertight green above is unproven.");
        }

        // ------------------------------------------------------------- metrics

        struct WaterlineMeasure
        {
            public int SubmergedPx;        // baseline-inked px no longer showing the hull's pixels
            public int VisiblePx;          // baseline-inked px still byte-equal to baseline
            public int Columns;            // measured planking columns
            public int RunMedianPx;        // median bottom-contiguous covered run (the waterline band)
            public int RunP90Px;
            public int HighestCoveredRow;  // smallest y of any covered inked px (int.MaxValue: none)
            public int UpperCutoffRow;     // silhouetteTop + 40% of height — no coverage above this
            public int BoardedPx;          // covered inked px DISCONNECTED from the waterline run
                                           // (> StormBoardedGapRows above it, any column) — water
                                           // INSIDE the boat: flooded sole/hold/inner bulwarks

            public override string ToString() =>
                $"(visible {VisiblePx}, submerged {SubmergedPx}, runs med {RunMedianPx}px " +
                $"p90 {RunP90Px}px over {Columns} cols, highest covered row {HighestCoveredRow} " +
                $"vs cutoff {UpperCutoffRow}, boarded {BoardedPx}px)";
        }

        /// <summary>
        /// Compare a composed hull+water frame against the hull-only baseline.
        ///
        /// <para><b>The waterline signal is the per-column BOTTOM-CONTIGUOUS covered run:</b>
        /// starting at the column's deepest baseline-inked pixel, count upward while pixels stay
        /// inked and covered (≠ baseline). That is the band of planking the lifted surface truly
        /// took (the emergent keyline at its top counts as covered — it replaced planking).</para>
        ///
        /// <para><b>SEPARATE covered bands higher in the column are BOARDED water</b>
        /// (<see cref="WaterlineMeasure.BoardedPx"/>: covered inked px more than
        /// <see cref="StormBoardedGapRows"/> rows above the run's top, counted over EVERY inked
        /// column): water painted over the cockpit sole / hold floor / inner bulwarks — the
        /// owner's 2026-07-23 flooding defect. The z-test produces it "truthfully" (those are
        /// low hull surfaces below a big lift), but a real boat is watertight — the shipped
        /// <c>WatertightDeckHeightMeters</c> clamp must hold it at zero (the storm tests); the
        /// reference-sea scenes in this file run UNCLAMPED (deck height 0) to keep the #263 pins
        /// bit-stable, so their runs deliberately ignore what the storm tests forbid.</para>
        ///
        /// <para>Run columns measured are the central half of the silhouette's x-range with ≥ 20
        /// inked px (bow/stern tips carry no planking run). Rows are top-left origin, so UP the
        /// planking = SMALLER row. <see cref="WaterlineMeasure.UpperCutoffRow"/> marks the top
        /// 40% of the silhouette (wheelhouse/mast country): no covered pixel may sit above it —
        /// the sea cannot reach there at reference exaggeration, whatever the residuals.</para>
        /// </summary>
        static WaterlineMeasure Measure(byte[] baseline, byte[] composed, int w, int h,
                                        bool[] boardedMask = null)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int silTop = int.MaxValue, silBottom = int.MinValue;
            var inkedPerCol = new int[w];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                    if (baseline[(y * w + x) * 4 + 3] > 0)
                    {
                        inkedPerCol[x]++;
                        silTop = Math.Min(silTop, y);
                        silBottom = Math.Max(silBottom, y);
                    }
                if (inkedPerCol[x] > 0) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); }
            }
            Assert.Greater(maxX, minX, "baseline hull silhouette is empty?");
            int span = maxX - minX;
            int x0 = minX + span / 4, x1 = maxX - span / 4;

            var m = new WaterlineMeasure
            {
                HighestCoveredRow = int.MaxValue,
                UpperCutoffRow = silTop + (int)(0.4f * (silBottom - silTop)),
            };
            var runs = new System.Collections.Generic.List<int>();
            for (int x = 0; x < w; x++)
            {
                bool measured = x >= x0 && x <= x1 && inkedPerCol[x] >= 20;
                int bottom = -1;
                for (int y = 0; y < h; y++)
                {
                    int i = (y * w + x) * 4;
                    if (baseline[i + 3] == 0) continue;
                    bottom = y;
                    bool same = composed[i] == baseline[i] && composed[i + 1] == baseline[i + 1] &&
                                composed[i + 2] == baseline[i + 2];
                    if (same) m.VisiblePx++;
                    else
                    {
                        m.SubmergedPx++;
                        m.HighestCoveredRow = Math.Min(m.HighestCoveredRow, y);
                    }
                }
                if (bottom < 0) continue;

                // BOARDED water (the watertight law): the column's waterline is its lowest
                // VISIBLE planking pixel (silhouette gaps — rudder apertures, overhangs — are
                // neutral, they terminate nothing); any covered pixel more than the gap
                // allowance ABOVE it is water painted over the boat's inside.
                int firstVisible = -1;
                for (int y = bottom; y >= 0; y--)
                {
                    int i = (y * w + x) * 4;
                    if (baseline[i + 3] == 0) continue;
                    bool same = composed[i] == baseline[i] && composed[i + 1] == baseline[i + 1] &&
                                composed[i + 2] == baseline[i + 2];
                    if (same) { firstVisible = y; break; }
                }
                if (firstVisible > 0)
                {
                    for (int y = 0; y < firstVisible - StormBoardedGapRows; y++)
                    {
                        int i = (y * w + x) * 4;
                        if (baseline[i + 3] == 0) continue;
                        bool same = composed[i] == baseline[i] && composed[i + 1] == baseline[i + 1] &&
                                    composed[i + 2] == baseline[i + 2];
                        if (!same)
                        {
                            m.BoardedPx++;
                            if (boardedMask != null) boardedMask[y * w + x] = true;
                        }
                    }
                }

                if (!measured) continue;
                m.Columns++;
                int run = 0;
                for (int y = bottom; y >= 0; y--)
                {
                    int i = (y * w + x) * 4;
                    if (baseline[i + 3] == 0) break;      // off the silhouette: the run is done
                    bool same = composed[i] == baseline[i] && composed[i + 1] == baseline[i + 1] &&
                                composed[i + 2] == baseline[i + 2];
                    if (same) break;                      // visible planking: the waterline
                    run++;
                }
                runs.Add(run);
            }
            Assert.Greater(m.Columns, 20, "too few measurable planking columns — framing broke?");
            runs.Sort();
            m.RunMedianPx = runs[runs.Count / 2];
            m.RunP90Px = runs[(int)(0.9f * (runs.Count - 1))];
            return m;
        }

        /// <summary>Opt-in visual evidence (set HH_WATERLINE_DUMP to a directory): the
        /// adjudicated frames as PNGs, for a human eye on a red run. Never writes by default.</summary>
        static void DumpEvidence(string name, byte[] topLeftRgba, RigMeshData data)
        {
            string dir = Environment.GetEnvironmentVariable("HH_WATERLINE_DUMP");
            if (string.IsNullOrEmpty(dir) || data == null) return;
            int w = data.W, h = data.H;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)                      // top-left bytes -> bottom-left tex
                for (int x = 0; x < w; x++)
                {
                    int i = ((h - 1 - y) * w + x) * 4;
                    px[y * w + x] = new Color32(topLeftRgba[i], topLeftRgba[i + 1],
                                                topLeftRgba[i + 2], 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"waterline_{name}.png"),
                                         tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Opt-in evidence for a red watertight run: the baseline dimmed, with every
        /// BOARDED pixel painted pure red — shows exactly WHERE the sea is boarding the boat
        /// (far-rail residual vs interior plane vs metric artefact). Same gate as
        /// <see cref="DumpEvidence"/>.</summary>
        static void DumpBoardedEvidence(string name, byte[] baselineTopLeft, bool[] boardedMask,
                                        RigMeshData data)
        {
            string dir = Environment.GetEnvironmentVariable("HH_WATERLINE_DUMP");
            if (string.IsNullOrEmpty(dir) || data == null) return;
            int w = data.W, h = data.H;
            var bytes = new byte[w * h * 4];
            for (int p = 0; p < w * h; p++)
            {
                int i = p * 4;
                if (boardedMask[p])
                {
                    bytes[i] = 255; bytes[i + 1] = 0; bytes[i + 2] = 0; bytes[i + 3] = 255;
                }
                else
                {
                    bytes[i] = (byte)(baselineTopLeft[i] / 3);
                    bytes[i + 1] = (byte)(baselineTopLeft[i + 1] / 3);
                    bytes[i + 2] = (byte)(baselineTopLeft[i + 2] / 3);
                    bytes[i + 3] = baselineTopLeft[i + 3];
                }
            }
            DumpEvidence(name, bytes, data);
        }

        static int CountDifferingRgb(byte[] a, byte[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2]) n++;
            return n;
        }

        // ------------------------------------------------------------- the harness

        /// <summary>
        /// A self-cleaning production-path scene: one configured lobster hull (the
        /// IsoFacetUrpPassTests framing — the rig pivot on its exact cell pixel), plus, when
        /// attached, the displaced sea exactly as DisplacedWaterSurface builds it in play: a
        /// world-metre grid mesh carrying the water material's HHWater pass on the displaced
        /// rendering layer, an in-scene WaterOverlay quad sorted UNDER the hull, the registry
        /// registration that turns the feature's water pass on, and the calibrated iso-depth
        /// frame read from the material — the production seam, driven through its internals
        /// because Activate() is play-gated.
        /// </summary>
        sealed class WaterlineScene : IDisposable
        {
            readonly RigMeshData _data;
            readonly GameObject _hullGo;
            readonly IsoFacetHullRenderer _hull;
            readonly GameObject _camGo;
            readonly Camera _cam;
            readonly RenderTexture _rt;

            GameObject _waterGo;
            GameObject _overlayGo;
            DisplacedWaterSurface _surface;
            Material _waterMat;
            Material _overlayMat;
            Mesh _gridMesh;
            Mesh _overlayQuad;
            Texture2D _blackHeight;
            bool _warm;
            readonly float _worldY;

            public Vector2 HullWorldPos => new Vector2(0f, _worldY);

            /// <param name="watertightDeckHeightMeters">The watertight line driven through the
            /// production setup (0 = unclamped, the pre-fix state — what the #263 reference
            /// scenes deliberately run; the storm tests drive the committed def's values).</param>
            /// <param name="watertightHalfBeamMeters">The clamp's half-beam reach (the exact
            /// far-rail residual term), from the committed def.</param>
            /// <param name="worldYMeters">Where in the WORLD this scene sits — hull and camera
            /// together, the water rect following the camera. The sea's own ground-y reference
            /// (<c>_HeightWorldMin</c>) does NOT move with them, so this walks the whole depth ramp
            /// <c>(y − refY)·cos</c> to a different place on it. That is what "at low and high tide"
            /// means for a floating hull: she rides the level, so the only thing a tide can change
            /// about her waterline is which part of the shared depth ramp she is calibrated on —
            /// and the answer must be "nothing" (ADR 0033's sweep).</param>
            public WaterlineScene(RigMeshData data, Mesh mesh,
                                  float watertightDeckHeightMeters = 0f,
                                  float watertightHalfBeamMeters = 0f,
                                  float worldYMeters = 0f)
            {
                _data = data;
                _worldY = worldYMeters;

                _hullGo = new GameObject("WaterlineTestHull");
                _hull = _hullGo.AddComponent<IsoFacetHullRenderer>();
                _hull.Configure(SetupFrom(data, mesh, watertightDeckHeightMeters,
                                          watertightHalfBeamMeters));
                _hullGo.transform.position = new Vector3(0f, worldYMeters, 0f);
                SetLayerRecursive(_hullGo.transform, ProbeLayer);

                float ppu = data.PxPerMetre;
                float ox = (float)((data.PivotX - data.W / 2.0) / ppu);
                float oy = (float)((data.H / 2.0 - data.PivotY) / ppu);
                _camGo = new GameObject("WaterlineTestCam");
                _cam = _camGo.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.orthographicSize = data.H / (2f * ppu);
                _cam.transform.position = new Vector3(-ox, -oy + worldYMeters, -100f);
                _cam.nearClipPlane = 1f;
                _cam.farClipPlane = 400f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = Color.clear;
                _cam.cullingMask = 1 << ProbeLayer;
                _cam.allowHDR = false;
                _cam.allowMSAA = false;

                _rt = new RenderTexture(data.W, data.H, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point,
                };
                _cam.targetTexture = _rt;
            }

            public void SetPose(float headingDirUnits, float heavePixels = 0f)
            {
                _hull.HeadingDirUnits = headingDirUnits;
                _hull.RollDegrees = 0f;
                _hull.PitchDegrees = 0f;
                _hull.HeavePixels = heavePixels;
                _hull.ApplyPose();
            }

            /// <summary>
            /// Build + register the displaced sea. With <paramref name="sabotageIsoDepthSign"/>
            /// the material's _WaterIsoDepth height term is negated BEFORE the frame is read from
            /// it — the honest end-to-end convention flip (frame and shader stay mutually
            /// consistent the way production reads them; only the cross-object convention lies).
            /// </summary>
            public void AttachWater(bool sabotageIsoDepthSign)
            {
                var waterShader = Shader.Find("HiddenHarbours/Water");
                var overlayShader = Shader.Find("HiddenHarbours/WaterOverlay");
                Assert.IsNotNull(waterShader, "HiddenHarbours/Water shader missing");
                Assert.IsNotNull(overlayShader, "HiddenHarbours/WaterOverlay shader missing");

                // A FRESH material — never the owner's Water.mat (ADR 0023 harness trap (2):
                // its baked height map reads as land in an abstract viewport). Uniform-deep sea:
                // keyword off AND a black height texture bound, depth ≫ band ⇒ shore fade 1.
                _waterMat = new Material(waterShader) { hideFlags = HideFlags.HideAndDontSave };
                _waterMat.SetShaderPassEnabled("Universal2D", false);   // off-screen pass only
                _blackHeight = new Texture2D(1, 1, TextureFormat.R8, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _blackHeight.SetPixel(0, 0, Color.black);
                _blackHeight.Apply(false, true);
                _waterMat.SetTexture("_HeightTex", _blackHeight);
                _waterMat.SetFloat("_WaterLevel", 0f);
                _waterMat.SetFloat("_HeightMin", -8f);                  // uniform-deep fallback
                _waterMat.SetFloat("_WaveExaggeration", 1.5f);          // the ADR default
                _waterMat.SetFloat("_ShoreFadeBand", 0.5f);             // depth 8 ⇒ fade exactly 1
                _waterMat.SetFloat("_OceanSwellScale", 0.025f);         // freqScale 1: the C# twin's frame
                // Distinct palette anchors so no water band can byte-collide with hull paint.
                _waterMat.SetColor("_PaletteDeep", new Color(0.05f, 0.15f, 0.45f));
                _waterMat.SetColor("_PaletteMid", new Color(0.10f, 0.30f, 0.60f));
                _waterMat.SetColor("_PaletteShallow", new Color(0.20f, 0.50f, 0.75f));
                _waterMat.SetColor("_PaletteFoam", new Color(0.55f, 0.80f, 0.95f));
                if (sabotageIsoDepthSign)
                {
                    Vector4 iso = _waterMat.GetVector("_WaterIsoDepth");
                    _waterMat.SetVector("_WaterIsoDepth", new Vector4(iso.x, -iso.y, 0f, 0f));
                }

                // The sea rect: the camera view padded past the tallest possible lift (the
                // storm scenario reaches ~3 m of raw height × 1.5 exaggeration, so the vertical
                // pad must cover it — a crest rising into view from a ground row below the rect
                // would otherwise be missing water).
                Vector3 c = _cam.transform.position;
                float halfW = _data.W / (2f * _data.PxPerMetre) + 2f;
                float halfH = _data.H / (2f * _data.PxPerMetre) + 8f;
                var rect = Rect.MinMaxRect(c.x - halfW, c.y - halfH, c.x + halfW, c.y + halfH);

                _gridMesh = BuildGrid(rect, cell: 0.25f);
                _waterGo = new GameObject("DisplacedSea") { layer = ProbeLayer };
                _waterGo.AddComponent<MeshFilter>().sharedMesh = _gridMesh;
                var mr = _waterGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _waterMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.renderingLayerMask = DisplacedWaterRegistry.RenderingLayer;

                // The in-scene face, sorted UNDER the hull (the flat sea's slot).
                _overlayQuad = new Mesh { name = "WaterlineOverlayQuad" };
                _overlayQuad.SetVertices(new[]
                {
                    new Vector3(rect.xMin, rect.yMin, 0f), new Vector3(rect.xMax, rect.yMin, 0f),
                    new Vector3(rect.xMax, rect.yMax, 0f), new Vector3(rect.xMin, rect.yMax, 0f),
                });
                _overlayQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                _overlayMat = new Material(overlayShader) { hideFlags = HideFlags.HideAndDontSave };
                _overlayGo = new GameObject("WaterlineOverlay") { layer = ProbeLayer };
                _overlayGo.AddComponent<MeshFilter>().sharedMesh = _overlayQuad;
                var omr = _overlayGo.AddComponent<MeshRenderer>();
                omr.sharedMaterial = _overlayMat;
                omr.shadowCastingMode = ShadowCastingMode.Off;
                omr.receiveShadows = false;
                omr.lightProbeUsage = LightProbeUsage.Off;
                var group = _overlayGo.AddComponent<UnityEngine.Rendering.SortingGroup>();
                group.sortingOrder = -10;
                omr.sortingOrder = -10;

                // The production seam, driven through its internals (Activate is play-gated):
                // register (the feature's DrawWater gate) and publish the calibrated frame FROM
                // THE MATERIAL — the same reads DisplacedWaterSurface.PublishIsoDepthFrame does,
                // exaggeration included (the watertight clamp must bound the same lift the
                // vertex stage draws).
                _surface = _waterGo.AddComponent<DisplacedWaterSurface>();
                DisplacedWaterRegistry.Register(_surface);
                Vector4 isoDepth = _waterMat.GetVector("_WaterIsoDepth");
                Vector4 heightMin = _waterMat.GetVector("_HeightWorldMin");
                DisplacedWaterRegistry.PublishIsoDepthFrame(_surface,
                    new WaterIsoDepthFrame(heightMin.y, isoDepth.x, isoDepth.y,
                                           _waterGo.transform.position.z,
                                           _waterMat.GetFloat("_WaveExaggeration")));
                _hull.ApplyPose();     // EditMode has no LateUpdate — land the calibrated z now
                _warm = false;         // new shader variants may need compiling
            }

            /// <summary>The production OFF path: unregister (clears the frame), hide the sea's
            /// objects (Deactivate's contract), restore the hull's uncalibrated pose.</summary>
            public void DetachWater()
            {
                if (_surface != null) DisplacedWaterRegistry.Unregister(_surface);
                if (_waterGo != null) _waterGo.SetActive(false);
                if (_overlayGo != null) _overlayGo.SetActive(false);
                _hull.ApplyPose();
            }

            public byte[] Render()
            {
                // Production runs ApplyPose in LateUpdate every frame before rendering; EditMode
                // has no player loop, so land the pose here. Load-bearing since the watertight
                // clamp: the hull's calibrated z now reads the PUBLISHED wave field, which the
                // storm tests move between renders.
                _hull.ApplyPose();
                EnsureVariantsCompiled();
                _cam.Render();
                return ReadBackTopLeft();
            }

            static IsoFacetHullSetup SetupFrom(RigMeshData data, Mesh mesh,
                                               float watertightDeckHeightMeters,
                                               float watertightHalfBeamMeters)
            {
                var ramps = new Color32[data.Materials.Count][];
                var offs = new int[data.Materials.Count];
                for (int m = 0; m < data.Materials.Count; m++)
                {
                    ramps[m] = data.Materials[m].Ramp;
                    offs[m] = data.Materials[m].Off;
                }
                var bayer = new float[16];
                for (int x = 0; x < 4; x++)
                    for (int y = 0; y < 4; y++)
                        bayer[x * 4 + y] = (float)data.Bayer[x, y];
                return new IsoFacetHullSetup
                {
                    Mesh = mesh,
                    Ramps = ramps,
                    RampOffsets = offs,
                    LightN = new Vector3((float)data.LightN.X, (float)data.LightN.Y, (float)data.LightN.Z),
                    Gain = (float)data.Gain,
                    Bias = (float)data.Bias,
                    Bayer16 = bayer,
                    Keyline = data.Keyline,
                    PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY),
                    PxPerMetre = data.PxPerMetre,
                    CellW = data.W,
                    CellH = data.H,
                    ElevationDeg = (float)data.DefaultElev,
                    WatertightDeckHeightMeters = watertightDeckHeightMeters,
                    WatertightHalfBeamMeters = watertightHalfBeamMeters,
                };
            }

            static Mesh BuildGrid(Rect rect, float cell)
            {
                int nx = Mathf.Max(1, Mathf.CeilToInt(rect.width / cell));
                int ny = Mathf.Max(1, Mathf.CeilToInt(rect.height / cell));
                var verts = new Vector3[(nx + 1) * (ny + 1)];
                for (int j = 0; j <= ny; j++)
                    for (int i = 0; i <= nx; i++)
                        verts[j * (nx + 1) + i] = new Vector3(
                            rect.xMin + rect.width * (i / (float)nx),
                            rect.yMin + rect.height * (j / (float)ny), 0f);
                var tris = new int[nx * ny * 6];
                int t = 0;
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int a = j * (nx + 1) + i, b = a + 1, cIdx = a + nx + 1, d = cIdx + 1;
                        tris[t++] = a; tris[t++] = cIdx; tris[t++] = b;
                        tris[t++] = b; tris[t++] = cIdx; tris[t++] = d;
                    }
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "WaterlineSeaGrid" };
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                // Lifted crests must not be frustum-culled off the flat rect (the production
                // chunks pad their bounds the same way).
                Bounds bnds = mesh.bounds;
                bnds.Expand(8f);
                mesh.bounds = bnds;
                return mesh;
            }

            void EnsureVariantsCompiled()
            {
                if (_warm) return;
                const double timeoutSeconds = 180.0;
                const int maxWarmUps = 10;
                var clock = Stopwatch.StartNew();
                int renders = 0;
                for (; renders < maxWarmUps; renders++)
                {
                    _cam.Render();
                    if (!ShaderUtil.anythingCompiling) break;
                    while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < timeoutSeconds)
                        Thread.Sleep(25);
                }
                if (ShaderUtil.anythingCompiling || renders >= maxWarmUps)
                    Assert.Fail(
                        "SHADERS NEVER FINISHED COMPILING — this is NOT a waterline regression. " +
                        $"After {renders} warm-up render(s) and {clock.Elapsed.TotalSeconds:F1}s " +
                        "the compiler was still busy; a measuring render would land on the async " +
                        "placeholder (the cold-cache trap). Re-run with a warm cache.");
                _warm = true;
            }

            byte[] ReadBackTopLeft()
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _rt;
                var tex = new Texture2D(_data.W, _data.H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, _data.W, _data.H), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                var px = tex.GetPixels32();
                Object.DestroyImmediate(tex);

                int w = _data.W, h = _data.H;
                var bytes = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                {
                    int srcRow = (h - 1 - y) * w;
                    int dstRow = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[srcRow + x];
                        int d = (dstRow + x) * 4;
                        bytes[d] = c.r; bytes[d + 1] = c.g; bytes[d + 2] = c.b; bytes[d + 3] = c.a;
                    }
                }
                return bytes;
            }

            static void SetLayerRecursive(Transform t, int layer)
            {
                t.gameObject.layer = layer;
                for (int i = 0; i < t.childCount; i++)
                    SetLayerRecursive(t.GetChild(i), layer);
            }

            public void Dispose()
            {
                if (_surface != null) DisplacedWaterRegistry.Unregister(_surface);
                RenderTexture.active = null;
                if (_cam != null) _cam.targetTexture = null;
                if (_camGo != null) Object.DestroyImmediate(_camGo);
                if (_hullGo != null) Object.DestroyImmediate(_hullGo);
                if (_waterGo != null) Object.DestroyImmediate(_waterGo);
                if (_overlayGo != null) Object.DestroyImmediate(_overlayGo);
                if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                if (_waterMat != null) Object.DestroyImmediate(_waterMat);
                if (_overlayMat != null) Object.DestroyImmediate(_overlayMat);
                if (_gridMesh != null) Object.DestroyImmediate(_gridMesh);
                if (_overlayQuad != null) Object.DestroyImmediate(_overlayQuad);
                if (_blackHeight != null) Object.DestroyImmediate(_blackHeight);
            }
        }
    }
}
