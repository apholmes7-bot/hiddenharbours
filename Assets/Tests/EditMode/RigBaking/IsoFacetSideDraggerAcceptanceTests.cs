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
    /// <b>ADR 0022 phase 5 acceptance: the SIDE DRAGGER, the hull that motivated the whole ADR.</b>
    /// 25 m of riveted steel whose baked sheet set would have been <b>433.1 MiB</b> at 32 facings ×
    /// 4 rock frames. Her mesh is 143.9 KB.
    ///
    /// <para><b>She has no sheet, and needs none — so the oracle is different from phase 4's.</b> The
    /// lobster could be compared against her own baked 32-facing sheet. The dragger has nothing to
    /// compare to except the art director's own renderer, so the truth here is phase 2's CPU
    /// reference rasterizer (<see cref="RigMeshReferenceRasterizer"/>) — a transcription of the rig's
    /// z-buffered, flat-facet, ordered-dither pipeline. That is a STRONGER oracle in one way (it is
    /// the art itself, not a bake of it) and a weaker one in another, which this fixture is explicit
    /// about below.</para>
    ///
    /// <para><b>The four things proved, and the one that cannot be:</b></para>
    /// <list type="number">
    ///   <item><b>The committed bake is not stale</b> (headless — runs on CI): the mesh inside
    ///   <c>SideDraggerIsoHullMesh.asset</c> is re-derived from the rig and must match what is
    ///   committed. This is the failure mode this repo has hit most often: builder-generated assets
    ///   go stale and the "broken boat" is debugged in the code.</item>
    ///   <item><b>The azimuth convention is not stale</b> (headless — runs on CI): the committed
    ///   <see cref="HullMeshDef.AzimuthCounterClockwise"/> is re-MEASURED off the live rig with
    ///   <see cref="RigAzimuthProbe"/>. Never read off the rig's declared facing order, which has
    ///   shipped mirrored boats five times.</item>
    ///   <item><b>The GPU reproduces the oracle</b> through the committed def, across cardinal and
    ///   fractional headings, driven by compass heading through
    ///   <see cref="HullMeshMath.HeadingToDirUnits"/> — the production mapping, not a hand-set dir.</item>
    ///   <item><b>The mapping SIGN is load-bearing</b> — the flipped-azimuth sabotage.</item>
    /// </list>
    /// <para>⚠️ <b>What this fixture cannot prove:</b> that the measured convention is itself right.
    /// Items 3–4 render the oracle at the dir the mapping produced, so they are sensitive to the sign
    /// being CHANGED but not to the probe being wrong in the first place. Item 2 is the guard for
    /// that, and the probe is the same measurement the sprite pipeline has been checked against
    /// (<c>RigAzimuthConventionTests</c>). Stated plainly rather than left for a reader to discover.</para>
    ///
    /// <para><b>Noise floors — MEASURED for HER, not inherited.</b> Phase 4 pinned the lobster at
    /// cardinal 300 / fractional 150; a 25 m hull carries much longer straight edges and far larger
    /// flat panels (the cream lower house is the biggest uninterrupted panel in the whole kit), so
    /// her dither- and facet-boundary runs are longer. Measured on D3D11, 2026-07-22 — see the
    /// constants for the numbers and the sabotage margin.</para>
    ///
    /// <para><b>CI has no GPU.</b> Every rendering test gates on <see cref="RequireAGraphicsDevice"/>
    /// and skips LOUDLY; only items 1 and 2 actually execute there.</para>
    /// </summary>
    public class IsoFacetSideDraggerAcceptanceTests
    {
        const string RigPath = "docs/art/rigs/sideDraggerIsoRig.js";
        const string RigGlobal = "SideDraggerIso";
        const string HullMeshAssetPath =
            "Assets/_Project/Data/Boats/HullMeshes/SideDraggerIsoHullMesh.asset";
        const int ProbeLayer = 31;

        // MEASURED FLOORS (D3D11, 2026-07-22) — HERS, not the lobster's. Per-heading clusters:
        //   cardinal    0°  262 |  90°  505 | 180°  323 | 270°  378   → worst 505
        //   fractional 33.75° 103 | 146.25° 36 | 247.5° 254 | 326.25° 103 → worst 254
        //   worst whole-cell divergence 3.312% (ADR 0022 measured her at 2.47–4.81%)
        // Roughly DOUBLE the lobster's (253 / 112) — as expected, and worth stating: a 25 m hull runs
        // much longer straight edges and much larger flat panels (her cream lower house is the biggest
        // uninterrupted panel in the kit), so a single-ramp-step dither boundary runs correspondingly
        // longer. Inheriting phase 4's floors would have been wrong in both directions.
        /// <summary>Cardinal floor: worst measured 505, +29% headroom.</summary>
        const int MaxCardinalNoiseCluster = 650;
        /// <summary>Fractional-heading floor: worst measured 254, +38% headroom.</summary>
        const int MaxFractionalNoiseCluster = 350;
        /// <summary>Whole-cell backstop; ADR 0022 measured her at 2.47–4.81%.</summary>
        const double MaxPercent = 5.0;

        static RigMeshData s_Rig;

        GameConfig _keylineConfig;
        GameConfig _prevConfig;

        /// <summary>ADR 0031: production ships the mesh fleet's keyline OFF, but this fixture's
        /// oracle is the CPU reference rasteriser — the rig's own pipeline, keyline included — so
        /// the GPU renders must carry it too. Forced through the owner's real dial
        /// (<c>GameServices.Config</c>), never a parallel test-only path. Harmless around the
        /// headless items (1 and 2), which render nothing.</summary>
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

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null Device), " +
                    "so the side dragger's mesh could not render and this proved nothing. Expected on " +
                    "CI; run on a machine with a GPU to actually verify phase 5.");
            }
        }

        /// <summary>
        /// A fresh extraction of her rig, <b>through the baker's own path</b> — including
        /// <see cref="RigMeshAssetBaker.AppendMeshInteriorIfConverted"/>, which appends her ROOM.
        ///
        /// <para>She is a converted hull (ADR 0041 rollout PR 2). Extracting the exterior alone here
        /// would make "the committed bake is not stale" compare 5812 committed vertices against 3448
        /// fresh ones and report a stale bake on a bake that is exactly current — and the obvious fix,
        /// re-baking, would not move it. One method decides what a hull's mesh contains, and both the
        /// bake and every re-derivation call it; <c>HullMeshFleetTests</c> is factored the same way for
        /// the same reason.</para>
        /// </summary>
        static RigMeshData Rig()
        {
            if (s_Rig != null) return s_Rig;
            using var host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, RigPath, RigGlobal);
            RigMeshAssetBaker.AppendMeshInteriorIfConverted(host, RigGlobal, data);
            s_Rig = data;
            return s_Rig;
        }

        /// <summary>
        /// <b>The oracle's subject: her mesh with the ROOM stripped out.</b>
        ///
        /// <para><see cref="RigMeshReferenceRasterizer"/> is a transcription of the rig's own
        /// z-buffered flat-facet pipeline, and the rig draws no interior — it has no level gate and no
        /// concept of one. The GPU arm, meanwhile, keeps <c>HH_LEVEL_GATE</c> on for a converted hull
        /// and discards every room face while nothing is cut
        /// (<c>IsoFacetHullRenderer.ApplyCutawayKeyword</c>). Handing the CPU side the whole mesh makes
        /// it draw her cabin through her own topsides: measured at 23.7–34.3% of inked pixels, clusters
        /// of 21k–44k, at every one of the eight headings. So the two arms are made comparable by
        /// removing from the oracle exactly what the shader removes from the picture — no floors moved,
        /// no keyword forced, and her EXTERIOR is still the whole subject, which is what this fixture
        /// has always been about.</para>
        /// </summary>
        static Mesh ExteriorOnly(HullMeshDef def)
        {
            Mesh stripped = ConvertedInteriors.MeshWithoutTheRoom(def.Mesh, out int roomVerts, out _);
            Assert.Greater(roomVerts, 0,
                "the dragger's committed mesh carries no room-flagged vertices — she is on " +
                "RigMeshAssetBaker.MeshInteriorHulls, so either she was not re-baked or the room flag " +
                "stopped being written, and this fixture would be comparing the same mesh twice.");
            return stripped;
        }

        static HullMeshDef LoadDef()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(HullMeshAssetPath);
            Assert.IsNotNull(def, $"missing {HullMeshAssetPath} — bake it first " +
                                  "(Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake Side Dragger hull-mesh asset)");
            Assert.IsTrue(def.IsUsable(), "the committed def must be usable");
            return def;
        }

        // ------------------------------------------------------ 1. the bake is not stale (headless)

        [Test]
        public void CommittedBake_StillMatchesTheRig()
        {
            var def = LoadDef();
            RigMeshData rig = Rig();
            Mesh fresh = RigMeshBuilder.Build(rig, "FreshSideDragger").Mesh;
            try
            {
                Assert.AreEqual(rig.W, def.CellW, "cell width drifted from the rig");
                Assert.AreEqual(rig.H, def.CellH, "cell height drifted from the rig");
                Assert.AreEqual((float)rig.DefaultElev, def.ElevationDeg, 1e-4f, "bake elevation drifted");
                Assert.AreEqual(rig.PxPerMetre, def.PxPerMetre, 1e-4f, "px-per-metre drifted");
                Assert.AreEqual(fresh.vertexCount, def.Mesh.vertexCount,
                    "the committed mesh has a different vertex count than the rig now produces — the " +
                    "bake is STALE. Re-run the baker before debugging anything else about this boat.");
                Assert.AreEqual(fresh.triangles.Length, def.Mesh.triangles.Length,
                    "the committed mesh has a different triangle count than the rig now produces — stale bake.");

                // Geometry, not just counts: the CPU oracle rendered from each must be identical.
                var view = new RigViewOptions(3, rig.DefaultElev);
                byte[] committed = RigMeshReferenceRasterizer.RenderFromMesh(rig, def.Mesh, view);
                byte[] rebuilt = RigMeshReferenceRasterizer.RenderFromMesh(rig, fresh, view);
                var diff = RigMeshReferenceRasterizer.Compare(committed, rebuilt, rig.W, rig.H);
                Debug.Log($"[dragger] committed bake vs fresh rig extraction: {diff}");
                Assert.AreEqual(0, diff.LargestDifferingCluster,
                    $"the committed mesh renders differently from a fresh extraction ({diff}) — the " +
                    "bake is stale or the rig changed. Both sides are CPU, so this is exact, not noisy.");
            }
            finally { Object.DestroyImmediate(fresh); }
        }

        // ---------------------------------------- 1b. her rock is HERS, not the lobster's (headless)

        /// <summary>
        /// The rock amplitudes on the committed def must be the ones HER OWN rig declares. This is a
        /// copy-paste guard as much as a staleness guard: the obvious way to add a second mesh hull is
        /// to duplicate the first one's def, and the lobster's rock (2.8 / 1.6 / 1.2) on a 25 m steel
        /// dragger would read as a small boat's quick lively roll on a hull whose rig deliberately
        /// says <i>"25 m of steel — slow, stiff offshore roll"</i> (2.0 / 1.1 / 1.0). Both sets are
        /// plausible numbers, so nothing else in the pipeline would ever notice.
        /// </summary>
        [Test]
        public void CommittedRockAmplitudes_ComeFromHerOwnRig()
        {
            var def = LoadDef();
            using var host = RigScriptHostFactory.Create();
            RigMeshExtractor.ExtractFrom(host, RigPath, RigGlobal);   // loads the rig into this host

            Assert.IsTrue(host.EvaluateBool($"typeof {RigGlobal}.ROCK === 'object' && {RigGlobal}.ROCK !== null"),
                "her rig exports no ROCK block — a mesh hull with no rock amplitudes never rocks");
            float rollA = (float)host.EvaluateNumber($"{RigGlobal}.ROCK.rollA || 0");
            float pitchA = (float)host.EvaluateNumber($"{RigGlobal}.ROCK.pitchA || 0");
            float heaveA = (float)host.EvaluateNumber($"{RigGlobal}.ROCK.heaveA || 0");
            Debug.Log($"[dragger] rig ROCK = ({rollA}, {pitchA}, {heaveA}); " +
                      $"committed = ({def.RockRollDegrees}, {def.RockPitchDegrees}, {def.RockHeavePixels})");

            Assert.AreEqual(rollA, def.RockRollDegrees, 1e-4f, "roll amplitude is not her rig's");
            Assert.AreEqual(pitchA, def.RockPitchDegrees, 1e-4f, "pitch amplitude is not her rig's");
            Assert.AreEqual(heaveA, def.RockHeavePixels, 1e-4f, "heave amplitude is not her rig's");

            Assert.Less(rollA, 2.8f,
                "sanity: her roll must be GENTLER than the 12 m lobster's 2.8°. If this ever trips, " +
                "the def was probably copied from the lobster rather than baked from her rig.");
        }

        // ------------------------------------------- 2. the azimuth convention is not stale (headless)

        [Test]
        public void CommittedAzimuthConvention_StillMatchesTheMeasuredRig()
        {
            var def = LoadDef();

            // ONE host, EXTRACTED INTO FIRST. The rig's global does not exist in a fresh engine until
            // its script has been evaluated, so the render below must run in the host the extract
            // loaded — the same order the baker uses. (A fresh host throws "SideDraggerIso is not
            // defined", which is how this was found.)
            using var host = RigScriptHostFactory.Create();
            RigMeshData rig = RigMeshExtractor.ExtractFrom(host, RigPath, RigGlobal);

            var quarter = new RigViewOptions(2, rig.DefaultElev);   // broadside: least ambiguous
            byte[] rgba = host.EvaluateBytes($"{RigGlobal}.render({quarter.ToJsArgs()})");
            RigAzimuthProbe.Result probe = RigAzimuthProbe.MeasureFromQuarterTurn(rgba, rig.W, rig.H);
            Debug.Log($"[dragger] azimuth probe:\n{probe.Report}");

            bool measuredCcw = probe.Convention == AzimuthConvention.CounterClockwise;
            Assert.AreEqual(measuredCcw, def.AzimuthCounterClockwise,
                "the committed AzimuthCounterClockwise disagrees with a fresh MEASUREMENT of the rig. " +
                "Every heading this hull draws is mirrored. Re-bake. (This is the defect class that " +
                "has shipped five times in sprite form — never trust a rig's declared facing order.)");
        }

        // -------------------------------------------------- 3. the GPU reproduces the oracle (GPU)

        [Test]
        public void MeshRender_ReproducesTheRigsOwnRender_AcrossHeadings()
        {
            RequireAGraphicsDevice();
            var def = LoadDef();
            RigMeshData rig = Rig();
            Mesh exterior = ExteriorOnly(def);

            // Compass headings, mapped to rig dir by the PRODUCTION mapping — cardinals plus
            // deliberate fractionals no facing grid could ever have drawn.
            var cardinals = new[] { 0f, 90f, 180f, 270f };
            var fractionals = new[] { 33.75f, 146.25f, 247.5f, 326.25f };

            var report = new System.Text.StringBuilder();
            int worstCardinal = 0, worstFractional = 0;
            double worstPercent = 0;

            foreach (float heading in Concat(cardinals, fractionals))
            {
                bool cardinal = Array.IndexOf(cardinals, heading) >= 0;
                float dir = HullMeshMath.HeadingToDirUnits(heading, 0f, def.AzimuthCounterClockwise);
                var view = new RigViewOptions(dir, def.ElevationDeg);

                byte[] gpu = RenderMesh(def, heading, def.AzimuthCounterClockwise);
                byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(rig, exterior, view);
                var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, def.CellW, def.CellH);

                report.AppendLine($"  heading {heading:0.##}° (dir {dir:0.###}, " +
                                  $"{(cardinal ? "cardinal" : "fractional")}): {diff}");
                Debug.Log($"[dragger] heading {heading:0.##}° → dir {dir:0.###}: {diff}");
                if (cardinal) worstCardinal = Math.Max(worstCardinal, diff.LargestDifferingCluster);
                else worstFractional = Math.Max(worstFractional, diff.LargestDifferingCluster);
                worstPercent = Math.Max(worstPercent, diff.PercentDiffering);
            }

            Object.DestroyImmediate(exterior);
            Debug.Log($"[dragger] worst cardinal cluster {worstCardinal}, worst fractional " +
                      $"{worstFractional}, worst percent {worstPercent:0.###}%");
            Assert.LessOrEqual(worstCardinal, MaxCardinalNoiseCluster,
                $"the 25 m hull diverged from the rig's own render beyond her cardinal noise " +
                $"floor:\n{report}A connected patch this size is a real defect — in the committed def, " +
                "the heading mapping, or the cell framing.");
            Assert.LessOrEqual(worstFractional, MaxFractionalNoiseCluster,
                $"beyond her fractional-heading noise floor:\n{report}");
            Assert.Less(worstPercent, MaxPercent,
                $"whole-cell backstop — ADR 0022 measured her at 2.47–4.81%:\n{report}");
        }

        // ------------------------------------------------------------------ 4. SABOTAGE (GPU)

        [Test]
        public void Sabotage_FlippedAzimuthMapping_IsCaught()
        {
            RequireAGraphicsDevice();
            var def = LoadDef();
            RigMeshData rig = Rig();

            const float heading = 90f;    // East — where the mirror is a full 180° of drawn heading
            float trueDir = HullMeshMath.HeadingToDirUnits(heading, 0f, def.AzimuthCounterClockwise);
            Mesh exterior = ExteriorOnly(def);
            byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(
                rig, exterior, new RigViewOptions(trueDir, def.ElevationDeg));
            Object.DestroyImmediate(exterior);

            // The GPU draws her through the WRONG convention — the exact mistake "tidying up the
            // minus sign", or trusting the rig's declared order over the measurement, would ship.
            byte[] gpu = RenderMesh(def, heading, !def.AzimuthCounterClockwise);
            var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, def.CellW, def.CellH);
            Debug.Log($"[dragger][SABOTAGE] flipped azimuth mapping @ {heading}°: {diff}");

            Assert.Greater(diff.LargestDifferingCluster, MaxCardinalNoiseCluster,
                "SABOTAGE NOT DETECTED — a mirrored heading mapping stayed under the noise floor. " +
                "The acceptance cannot see the CCW defect class and every green run above is worth " +
                "less than it looks.");
        }

        // ------------------------------------------------------------------ plumbing

        static float[] Concat(float[] a, float[] b)
        {
            var r = new float[a.Length + b.Length];
            Array.Copy(a, r, a.Length);
            Array.Copy(b, 0, r, a.Length, b.Length);
            return r;
        }

        /// <summary>Render the committed def through the REAL production renderer at a COMPASS
        /// heading, mapped through <see cref="HullMeshMath.HeadingToDirUnits"/> exactly as
        /// <c>MeshHullDriver</c> does. Scenes are sequential, never simultaneous (two live hulls
        /// photobomb one renderer list — phase 3's lesson).</summary>
        static byte[] RenderMesh(HullMeshDef def, float headingDeg, bool azimuthCcw)
        {
            var go = new GameObject("DraggerHull") { layer = ProbeLayer };
            try
            {
                var renderer = go.AddComponent<IsoFacetHullRenderer>();
                renderer.Configure(IsoFacetHullPresentationService.ToSetup(def));
                renderer.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(headingDeg, 0f, azimuthCcw);
                renderer.ApplyPose();
                SetLayerRecursive(go.transform, ProbeLayer);
                return RenderCell(def, go);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The spike's framing: an ortho camera sized to the cell, offset so the rig pivot
        /// lands on its exact cell pixel; readback flipped to the rig's top-left orientation. Waits
        /// out shader compilation (the cold-cache trap fakes exactly the regressions we hunt).</summary>
        static byte[] RenderCell(HullMeshDef def, GameObject subject)
        {
            float ppu = def.PxPerMetre;
            float ox = (def.PivotPx.x - def.CellW / 2f) / ppu;
            float oy = (def.CellH / 2f - def.PivotPx.y) / ppu;

            var camGo = new GameObject("DraggerCam");
            var rt = new RenderTexture(def.CellW, def.CellH, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
            };
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = def.CellH / (2f * ppu);
                cam.transform.position = new Vector3(-ox, -oy, -100f);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
                cam.cullingMask = 1 << ProbeLayer;
                cam.allowHDR = false;      // byte-exact palette needs the 8-bit sRGB path
                cam.allowMSAA = false;
                cam.targetTexture = rt;

                WaitOutShaderCompilation(cam);
                cam.Render();
                return ReadBackTopLeft(rt, def.CellW, def.CellH);
            }
            finally
            {
                RenderTexture.active = null;
                camGo.GetComponent<Camera>().targetTexture = null;
                Object.DestroyImmediate(camGo);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        static void WaitOutShaderCompilation(Camera cam)
        {
            const double timeoutSeconds = 180.0;
            const int maxWarmUps = 10;
            var clock = Stopwatch.StartNew();
            int renders = 0;
            for (; renders < maxWarmUps; renders++)
            {
                cam.Render();
                if (!ShaderUtil.anythingCompiling) break;
                while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < timeoutSeconds)
                    Thread.Sleep(25);
            }
            if (ShaderUtil.anythingCompiling || renders >= maxWarmUps)
                Assert.Fail(
                    "SHADERS NEVER FINISHED COMPILING — not an acceptance regression. Re-run with a " +
                    "warm shader cache (the cold-cache trap fakes exactly this class of red).");
        }

        static byte[] ReadBackTopLeft(RenderTexture rt, int w, int h)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);

            var bytes = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int srcRow = (h - 1 - y) * w;   // GetPixels32 is bottom-left; the cell is top-left
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
    }
}
