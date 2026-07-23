using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
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

        static RigMeshData s_Lobster;
        static Mesh s_LobsterMesh;

        [OneTimeTearDown]
        public void TearDown()
        {
            if (s_LobsterMesh != null) Object.DestroyImmediate(s_LobsterMesh);
            s_LobsterMesh = null;
            s_Lobster = null;
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

        // ------------------------------------------------------------- the reference phases

        static readonly Vector2 ReferenceWind = new Vector2(-5.4f, -9.33f);
        const float ReferenceSeaState = 0.75f;

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
            var shifted = new WaveTrains(
                n > 0 ? Shifted(0) : default, n > 1 ? Shifted(1) : default,
                n > 2 ? Shifted(2) : default, n > 3 ? Shifted(3) : default,
                n, trains.CrestSharpening);
            WaveFieldBridge.Pack(in shifted, out Vector4 t0, out Vector4 t1, out Vector4 t2,
                                 out Vector4 t3, out Vector4 phases, out Vector4 fieldParams);
            Shader.SetGlobalVector("_WaveTrain0", t0);
            Shader.SetGlobalVector("_WaveTrain1", t1);
            Shader.SetGlobalVector("_WaveTrain2", t2);
            Shader.SetGlobalVector("_WaveTrain3", t3);
            Shader.SetGlobalVector("_WavePhases", phases);
            Shader.SetGlobalVector("_WaveFieldParams", fieldParams);
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
            DumpEvidence("baseline", baseline);
            DumpEvidence("low", low);
            DumpEvidence("high", high);

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

            public override string ToString() =>
                $"(visible {VisiblePx}, submerged {SubmergedPx}, runs med {RunMedianPx}px " +
                $"p90 {RunP90Px}px over {Columns} cols, highest covered row {HighestCoveredRow} " +
                $"vs cutoff {UpperCutoffRow})";
        }

        /// <summary>
        /// Compare a composed hull+water frame against the hull-only baseline.
        ///
        /// <para><b>The waterline signal is the per-column BOTTOM-CONTIGUOUS covered run:</b>
        /// starting at the column's deepest baseline-inked pixel, count upward while pixels stay
        /// inked and covered (≠ baseline). That is the band of planking the lifted surface truly
        /// took (the emergent keyline at its top counts as covered — it replaced planking). It
        /// deliberately ignores SEPARATE covered bands higher in the column: the flooded cockpit
        /// sole / far-side interior at a big crest is truthful occlusion of the hull's LOW
        /// surfaces, not waterline signal (the deck sits below a 1.4 m lift; the eye reads it as
        /// shipped green water).</para>
        ///
        /// <para>Columns measured are the central half of the silhouette's x-range with ≥ 20
        /// inked px (bow/stern tips carry no planking run). Rows are top-left origin, so UP the
        /// planking = SMALLER row. <see cref="WaterlineMeasure.UpperCutoffRow"/> marks the top
        /// 40% of the silhouette (wheelhouse/mast country): no covered pixel may sit above it —
        /// the sea cannot reach there at reference exaggeration, whatever the residuals.</para>
        /// </summary>
        static WaterlineMeasure Measure(byte[] baseline, byte[] composed, int w, int h)
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
                if (!measured || bottom < 0) continue;
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

        /// <summary>Opt-in visual evidence (set HH_WATERLINE_DUMP to a directory): the three
        /// adjudicated frames as PNGs, for a human eye on a red run. Never writes by default.</summary>
        static void DumpEvidence(string name, byte[] topLeftRgba)
        {
            string dir = Environment.GetEnvironmentVariable("HH_WATERLINE_DUMP");
            if (string.IsNullOrEmpty(dir) || s_Lobster == null) return;
            int w = s_Lobster.W, h = s_Lobster.H;
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

            public Vector2 HullWorldPos => Vector2.zero;

            public WaterlineScene(RigMeshData data, Mesh mesh)
            {
                _data = data;

                _hullGo = new GameObject("WaterlineTestHull");
                _hull = _hullGo.AddComponent<IsoFacetHullRenderer>();
                _hull.Configure(SetupFrom(data, mesh));
                SetLayerRecursive(_hullGo.transform, ProbeLayer);

                float ppu = data.PxPerMetre;
                float ox = (float)((data.PivotX - data.W / 2.0) / ppu);
                float oy = (float)((data.H / 2.0 - data.PivotY) / ppu);
                _camGo = new GameObject("WaterlineTestCam");
                _cam = _camGo.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.orthographicSize = data.H / (2f * ppu);
                _cam.transform.position = new Vector3(-ox, -oy, -100f);
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

            public void SetPose(float headingDirUnits)
            {
                _hull.HeadingDirUnits = headingDirUnits;
                _hull.RollDegrees = 0f;
                _hull.PitchDegrees = 0f;
                _hull.HeavePixels = 0f;
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

                // The sea rect: the camera view padded past the tallest possible lift.
                Vector3 c = _cam.transform.position;
                float halfW = _data.W / (2f * _data.PxPerMetre) + 2f;
                float halfH = _data.H / (2f * _data.PxPerMetre) + 4f;
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
                // THE MATERIAL — the same reads DisplacedWaterSurface.PublishIsoDepthFrame does.
                _surface = _waterGo.AddComponent<DisplacedWaterSurface>();
                DisplacedWaterRegistry.Register(_surface);
                Vector4 isoDepth = _waterMat.GetVector("_WaterIsoDepth");
                Vector4 heightMin = _waterMat.GetVector("_HeightWorldMin");
                DisplacedWaterRegistry.PublishIsoDepthFrame(_surface,
                    new WaterIsoDepthFrame(heightMin.y, isoDepth.x, isoDepth.y,
                                           _waterGo.transform.position.z));
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
                EnsureVariantsCompiled();
                _cam.Render();
                return ReadBackTopLeft();
            }

            static IsoFacetHullSetup SetupFrom(RigMeshData data, Mesh mesh)
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
