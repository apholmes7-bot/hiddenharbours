using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ADR 0022 phase 3 — the facet shader as a REAL URP pass, adjudicated in pixels.
    ///
    /// <para><b>What is being proved.</b> Phase 2 proved (headless, exactly) that the extracted
    /// mesh describes what the rig draws. This fixture proves the remaining, GPU-only claim: that
    /// the production render path — IsoFacetHullRenderer's transform + materials, the 2D
    /// renderer's render graph, IsoFacetHullFeature's facet MRT pass, the fullscreen keyline
    /// resolve and the in-scene overlay quad — reproduces the SAME cell the trusted CPU oracle
    /// (<see cref="RigMeshReferenceRasterizer"/>, keyline and dither included) produces from the
    /// same mesh. Every render goes through <c>Camera.Render()</c> with the project's own 2D
    /// renderer asset: nothing here bypasses URP.</para>
    ///
    /// <para><b>Why CI cannot adjudicate this, and what that means.</b> CI runs Unity with NO
    /// graphics device ("Null Device"); a render there does not fail, it CRASHES the editor
    /// (exit 1, no results XML). Every test therefore gates on
    /// <see cref="RequireAGraphicsDevice"/> FIRST and skips loudly — a green CI run carries no
    /// evidence about this fixture. The headless compile guard
    /// (IsoFacetShaderCompileGuardTests) still catches the magenta class on CI; the pixels need
    /// a machine with a GPU, which is where this fixture runs and bites.</para>
    ///
    /// <para><b>The acceptance metric is CONNECTED CLUSTER SIZE, not a percentage</b> — phase 2's
    /// lesson, inherited deliberately: a whole-cell percentage dilutes a localised defect on
    /// exactly the big hulls this ADR is for. The GPU comparison has a real noise floor the CPU
    /// one does not (hardware fill rules vs the rig's slack fill rule, float32 interpolation at
    /// facet boundaries — all of it lives ON edges, so it forms short connected RUNS, not
    /// singletons; see <see cref="MaxGpuNoiseCluster"/>). The sabotage cases are therefore the
    /// phase-3-shaped defect classes (a convention flipped end to end), each proven to land far
    /// above that floor — a single interior facet's winding is phase 2's catch, at exact
    /// arithmetic, not this fixture's.</para>
    /// </summary>
    public class IsoFacetUrpPassTests
    {
        /// <summary>Everything renders on this otherwise-unused layer — EditMode fixtures share a
        /// scene, and other tests' leftovers must not photobomb the readback (learned by the
        /// sprite-matrix guard).</summary>
        const int ProbeLayer = 31;

        /// <summary>
        /// Largest connected run of GPU-vs-oracle differing pixels accepted as noise.
        ///
        /// ⚠️ MEASURED (D3D11, 2026-07-21), then pinned. The GPU floor sits far above phase 2's
        /// cluster-1 because hardware top-left fill vs the rig's slack double-covering fill
        /// disagrees along facet EDGES, and an edge is connected by nature. The shape of the
        /// measurement says exactly that: CARDINAL headings, whose hull edges run axis-aligned
        /// for hundreds of pixels, produce the long runs — fractional and rocked views break the
        /// same disagreement into short ones:
        /// <code>
        ///   lobster dir 0      cluster 114 (silhouette  14)   2.58%
        ///   lobster dir 2      cluster 253 (silhouette 150)   3.04%   ← beam-on, longest straight edges
        ///   lobster dir 5.31   cluster  22 (silhouette  33)   2.62%
        ///   lobster rocked     cluster  17 (silhouette  44)   2.69%
        ///   dragger dir 3      cluster  51 (silhouette 110)   2.86%
        /// </code>
        /// All of it is the ADR's "facet- and dither-boundary single-step noise" class (the
        /// percentages recover the spike's 1.3–4.4% band) — one ramp step or one coverage pixel
        /// along a 1 px edge line, invisible at play scale. The sabotage floor is 5–200× higher:
        /// Bayer phase 1263 · unflipped light 34,978 · mirrored heading 57,356. If a legitimate
        /// change nudges a run past this, re-measure and re-verify the sabotage margins before
        /// relaxing — a threshold nobody has seen fail is a decoration.
        /// </summary>
        const int MaxGpuNoiseCluster = 300;

        /// <summary>Whole-cell backstop for comparability with ADR 0022's 1.3–4.4% shader figure.
        /// NOT the real criterion (see the fixture doc).</summary>
        const double MaxGpuPercent = 5.0;

        static RigMeshData s_Lobster;
        static Mesh s_LobsterMesh;

        [OneTimeTearDown]
        public void TearDown()
        {
            if (s_LobsterMesh != null) Object.DestroyImmediate(s_LobsterMesh);
            s_LobsterMesh = null;
            s_Lobster = null;
        }

        /// <summary>Must be the FIRST statement of every test: on a Null Device the crash happens
        /// in native rendering code that no assertion can intercept — never allocate first.</summary>
        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null " +
                    "Device), so the URP facet pass could not render and proved nothing. Expected " +
                    "on CI; the phase 3 rendering acceptance only runs on a machine with a GPU.");
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

        // ------------------------------------------------------------------ the golden master

        [Test]
        public void UrpPass_ReproducesTheOracle_AcrossHeadingsAndRock()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var views = new[]
            {
                new RigViewOptions(0, s_Lobster.DefaultElev),
                new RigViewOptions(2, s_Lobster.DefaultElev),
                new RigViewOptions(5.31, s_Lobster.DefaultElev),      // continuous heading — the point
                new RigViewOptions(1, s_Lobster.DefaultElev,
                                   rollDegrees: 2.8, pitchDegrees: 1.6, heavePixels: 1.2),
            };

            // Measure EVERY view before asserting, so a red run still reports the full picture
            // (a first-view abort once hid three quarters of the measurement).
            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            int worstCluster = 0;
            double worstPercent = 0;
            var report = new System.Text.StringBuilder();
            foreach (var view in views)
            {
                scene.SetPose(view);
                byte[] gpu = scene.Render();
                byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(
                    s_Lobster, s_LobsterMesh, view);
                var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, s_Lobster.W, s_Lobster.H);
                Debug.Log($"[iso-facet-urp] lobster {view.ToJsArgs()}: {diff}");
                report.AppendLine($"  {view.ToJsArgs()}: {diff}");
                worstCluster = Math.Max(worstCluster, diff.LargestDifferingCluster);
                worstPercent = Math.Max(worstPercent, diff.PercentDiffering);
            }

            Assert.LessOrEqual(worstCluster, MaxGpuNoiseCluster,
                "The URP pass diverged from the oracle by a connected patch beyond the measured " +
                $"GPU noise floor:\n{report}GPU noise is thin single-ramp-step runs along facet " +
                "and darkening edges; a patch beyond the floor is a real defect in the pass, the " +
                "resolve or the overlay.");
            Assert.Less(worstPercent, MaxGpuPercent,
                $"Whole-cell divergence beyond ADR 0022's measured shader class:\n{report}");
        }

        /// <summary>The hull that motivated the ADR — one heading, to prove the class scales.</summary>
        [Test]
        public void UrpPass_SideDragger_ReproducesTheOracle()
        {
            RequireAGraphicsDevice();

            RigMeshData data;
            using (var host = RigScriptHostFactory.Create())
                data = RigMeshExtractor.ExtractFrom(
                    host, "docs/art/rigs/sideDraggerIsoRig.js", "SideDraggerIso");
            var build = RigMeshBuilder.Build(data);
            try
            {
                using var scene = new HullScene(data, build.Mesh);
                var view = new RigViewOptions(3, data.DefaultElev);
                scene.SetPose(view);
                byte[] gpu = scene.Render();
                byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(data, build.Mesh, view);
                var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, data.W, data.H);
                Debug.Log($"[iso-facet-urp] dragger {view.ToJsArgs()}: {diff}");

                Assert.LessOrEqual(diff.LargestDifferingCluster, MaxGpuNoiseCluster,
                    $"dragger {view.ToJsArgs()}: connected divergence ({diff}) on the 25 m hull.");
                Assert.Less(diff.PercentDiffering, MaxGpuPercent, $"dragger: {diff}");
            }
            finally
            {
                Object.DestroyImmediate(build.Mesh);
            }
        }

        // ------------------------------------------------------------------ dither lock

        /// <summary>
        /// The production analogue of ADR 0022's 0.00% dither crawl: translate hull AND camera
        /// together by a non-multiple-of-4 pixel offset and the image must be BYTE-IDENTICAL.
        /// Screen-pinned dither (13–16% crawl in the spike's measurement) fails this by exactly
        /// the pixels whose Bayer index changed.
        /// </summary>
        [Test]
        public void Dither_IsIndexedInTheHullFrame_NotTheScreen()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(1, s_Lobster.DefaultElev);

            // Scenes are SEQUENTIAL, never simultaneous — a second live hull is culled into the
            // same renderer list and photobombs the frame (found the hard way: 23.8% "crawl"
            // that was really two overlapping hulls).
            byte[] a;
            using (var sceneA = new HullScene(s_Lobster, s_LobsterMesh))
            {
                sceneA.SetPose(view);
                a = sceneA.Render();
            }

            // (7,3) px: odd offsets in both axes, deliberately not a multiple of the 4x4 Bayer tile.
            var offset = new Vector3(7f / s_Lobster.PxPerMetre, 3f / s_Lobster.PxPerMetre, 0f);
            byte[] b;
            using (var sceneB = new HullScene(s_Lobster, s_LobsterMesh, worldOrigin: offset))
            {
                sceneB.SetPose(view);
                b = sceneB.Render();
            }

            var diff = RigMeshReferenceRasterizer.Compare(a, b, s_Lobster.W, s_Lobster.H);
            Debug.Log($"[iso-facet-urp] dither lock under (7,3)px translation: {diff}");
            Assert.AreEqual(0, diff.DifferingPixels,
                $"Translating hull+camera together changed {diff} — the dither (or something " +
                "else) is pinned to the SCREEN, not the hull frame. This is the 13–16% crawl " +
                "class ADR 0022 measured; in motion it shimmers on every moving boat.");
        }

        // ------------------------------------------------------------------ sorting

        /// <summary>
        /// The mesh hull must sort against SpriteRenderers exactly as well as a baked sprite —
        /// whole-object, via the SortingGroup workaround (ADR 0022 "Unchanged"). Above-sprite
        /// covers hull AND keyline (a fullscreen-composited keyline would paint over the sprite
        /// — the defect this quad architecture exists to prevent); below-sprite is covered by
        /// hull AND keyline.
        /// </summary>
        [Test]
        public void OverlayQuad_SortsAgainstSprites_WholeObject()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);

            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(view);
            // Coverage truth for the sorting question is the GPU's OWN baseline (what the hull
            // actually drew) — the oracle's silhouette differs by a handful of fill-rule edge
            // pixels, and those are the golden master's business, not sorting's.
            byte[] baseline = scene.Render();

            var red = new Color32(255, 0, 0, 255);

            // ABOVE: the sprite covers everything it overlaps — hull, darkening, keyline.
            var above = scene.AddCoveringSprite(red, sortingOrder: 10);
            byte[] withAbove = scene.Render();
            Object.DestroyImmediate(above);
            int hullVisibleOverAbove = 0;
            ForEachPixel(withAbove, (i, px) => { if (!Equal(px, red)) hullVisibleOverAbove++; });
            Assert.AreEqual(0, hullVisibleOverAbove,
                $"{hullVisibleOverAbove} px of hull/keyline drew OVER a sprite with a higher " +
                "sorting order. The hull (keyline included) must sort as one object under the " +
                "SortingGroup — if the keyline leaks it is compositing after the scene instead " +
                "of through the overlay quad.");

            // BELOW: the hull covers the sprite wherever the oracle inks (keyline included);
            // the sprite shows only where the cell is empty.
            var below = scene.AddCoveringSprite(red, sortingOrder: -10);
            byte[] withBelow = scene.Render();
            Object.DestroyImmediate(below);
            int wrongOverHull = 0, wrongInEmpty = 0;
            for (int i = 0; i < baseline.Length; i += 4)
            {
                bool inked = baseline[i + 3] > 0;
                var got = new Color32(withBelow[i], withBelow[i + 1], withBelow[i + 2], withBelow[i + 3]);
                var expectHull = new Color32(baseline[i], baseline[i + 1], baseline[i + 2], baseline[i + 3]);
                if (inked && !Equal(got, expectHull)) wrongOverHull++;
                if (!inked && !Equal(got, red)) wrongInEmpty++;
            }
            Assert.AreEqual(0, wrongOverHull,
                $"{wrongOverHull} inked px changed when a sprite slid UNDER the hull — the hull " +
                "must cover it completely where it draws.");
            Assert.AreEqual(0, wrongInEmpty,
                $"{wrongInEmpty} empty-cell px did not show the sprite below — something is " +
                "drawing where the hull should draw nothing.");
        }

        // ------------------------------------------------------------------ the deck contract

        /// <summary>
        /// The owner's deck-walking contract (2026-07-21): a renderer drawn through the
        /// HHHullDeck list is depth-tested PER-PIXEL against the hull's private z-buffer. Probed
        /// in all three regimes — decisively in front (wins everywhere), decisively behind
        /// (loses everywhere), and intersecting (wins and loses within one draw). The
        /// front/behind pair also proves the z-DIRECTION convention: plain ZTest LEqual under
        /// the render-graph camera path, no hand-flipped reversed-Z (the spike's GEqual/clear-0
        /// convention belonged to its hand-built command buffer and must NOT carry over).
        /// </summary>
        [Test]
        public void DeckRenderers_AreDepthTestedAgainstTheHull_PerPixel()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);
            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.SetPose(view);
            byte[] baseline = scene.Render();

            var magenta = new Color32(255, 0, 255, 255);
            // A 2x2 m probe centred on the hull origin — midship, solid hull all around, with
            // geometry both nearer and farther than the z=0 plane.
            var rect = new Rect(-1f, -1f, 2f, 2f);

            // BEHIND everything (z = +50, camera looks along +Z): the hull wins every pixel.
            byte[] behind = scene.RenderWithDeckProbe(rect, z: 50f, magenta);
            var diffBehind = RigMeshReferenceRasterizer.Compare(baseline, behind, s_Lobster.W, s_Lobster.H);
            Assert.AreEqual(0, diffBehind.DifferingPixels,
                $"A deck probe 50 m BEHIND the hull changed {diffBehind} — it must lose the " +
                "depth test everywhere. If it painted over the hull, the z-direction convention " +
                "is inverted (the spike's hand-built GEqual/clear-0 does NOT apply to the " +
                "render-graph camera path).");

            // IN FRONT of everything (z = -50): the probe wins every pixel of its footprint.
            byte[] front = scene.RenderWithDeckProbe(rect, z: -50f, magenta);
            int rectWrong = 0;
            ForEachPixelInWorldRect(scene, rect, shrinkPx: 1, (i) =>
            {
                if (!(front[i] == magenta.r && front[i + 1] == magenta.g && front[i + 2] == magenta.b))
                    rectWrong++;
            });
            Assert.AreEqual(0, rectWrong,
                $"{rectWrong} px inside a probe 50 m IN FRONT of the hull were not probe-coloured " +
                "— it must win the depth test everywhere it covers.");

            // INTERSECTING (z = 0): the hull is nearer in places and farther in others, so ONE
            // quad must both occlude and be occluded — the per-pixel claim itself.
            byte[] mixed = scene.RenderWithDeckProbe(rect, z: 0f, magenta);
            int probeWins = 0, hullWins = 0;
            ForEachPixelInWorldRect(scene, rect, shrinkPx: 1, (i) =>
            {
                bool isProbe = mixed[i] == magenta.r && mixed[i + 1] == magenta.g && mixed[i + 2] == magenta.b;
                bool sameAsBaseline = mixed[i] == baseline[i] && mixed[i + 1] == baseline[i + 1] &&
                                      mixed[i + 2] == baseline[i + 2];
                if (isProbe) probeWins++;
                else if (sameAsBaseline) hullWins++;
            });
            Debug.Log($"[iso-facet-urp] deck probe at z=0: probe wins {probeWins} px, hull wins {hullWins} px");
            Assert.Greater(probeWins, 0,
                "An intersecting deck probe never won the depth test — per-pixel deck occlusion " +
                "is not happening (whole-object sorting would look exactly like this).");
            Assert.Greater(hullWins, 0,
                "An intersecting deck probe won EVERYWHERE — the hull never occluded it, so the " +
                "z-buffer is not being tested per pixel.");
        }

        // ------------------------------------------------------------------ SABOTAGE

        /// <summary>
        /// ⚠️ A golden master nobody has seen fail is a decoration. These flip the three
        /// conventions phase 3 itself is responsible for — the reflected-frame light sign, the
        /// hull-frame dither phase, and the heading mirror — and assert the cluster metric
        /// catches each, with the measured margin on the record. (A single facet's winding is
        /// deliberately NOT a case here: that is extraction-level damage, caught by phase 2's
        /// EXACT arithmetic where it produces clusters of 2 against a floor of 1; under the GPU
        /// floor it would be dishonest theatre.)
        /// </summary>
        [Test]
        public void Sabotage_UnflippedLightZ_IsCaught()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);
            byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(s_Lobster, s_LobsterMesh, view);

            // Hand the component a pre-negated LN: its own reflection flip then restores the
            // rig-space vector — i.e. the shader dots with the UNreflected light, the exact
            // mistake "cleaning up the weird minus sign" would make.
            var setup = HullScene.SetupFrom(s_Lobster, s_LobsterMesh);
            setup.LightN = new Vector3(setup.LightN.x, setup.LightN.y, -setup.LightN.z);

            AssertSabotageCaught(setup, view, oracle, "light z-flip removed (reflection convention)");
        }

        [Test]
        public void Sabotage_ScreenPhaseDither_IsCaught()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            var view = new RigViewOptions(0, s_Lobster.DefaultElev);
            byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(s_Lobster, s_LobsterMesh, view);

            // The spike's (0,1) phase offset applied where it does not belong — ADR 0022's
            // dither-crawl defect class on a still image.
            var setup = HullScene.SetupFrom(s_Lobster, s_LobsterMesh);
            var shifted = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    shifted[x * 4 + y] = setup.Bayer16[x * 4 + ((y + 1) & 3)];
            setup.Bayer16 = shifted;

            AssertSabotageCaught(setup, view, oracle, "Bayer grid phase-shifted +1 in y");
        }

        [Test]
        public void Sabotage_MirroredHeading_IsCaught()
        {
            RequireAGraphicsDevice();
            EnsureLobster();

            // The iso-art mirror saga's defect class: heading sign flipped end to end. dir 1 vs
            // dir -1 differ by 90° of turntable — the bow points the wrong way.
            var view = new RigViewOptions(1, s_Lobster.DefaultElev);
            byte[] oracle = RigMeshReferenceRasterizer.RenderFromMesh(s_Lobster, s_LobsterMesh, view);

            using var scene = new HullScene(s_Lobster, s_LobsterMesh);
            scene.Hull.HeadingDirUnits = -1f;
            scene.Hull.ApplyPose();
            byte[] gpu = scene.Render();
            var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, s_Lobster.W, s_Lobster.H);
            Debug.Log($"[iso-facet-urp][SABOTAGE] mirrored heading: {diff}");
            Assert.Greater(diff.LargestDifferingCluster, MaxGpuNoiseCluster,
                "SABOTAGE NOT DETECTED — a mirrored heading stayed under the noise floor. The " +
                "golden master cannot see the CCW defect class and every green run above is " +
                "worth less than it looks.");
        }

        void AssertSabotageCaught(IsoFacetHullSetup setup, RigViewOptions view, byte[] oracle, string what)
        {
            using var scene = new HullScene(s_Lobster, s_LobsterMesh, setup);
            scene.SetPose(view);
            byte[] gpu = scene.Render();
            var diff = RigMeshReferenceRasterizer.Compare(oracle, gpu, s_Lobster.W, s_Lobster.H);
            Debug.Log($"[iso-facet-urp][SABOTAGE] {what}: {diff}");
            Assert.Greater(diff.LargestDifferingCluster, MaxGpuNoiseCluster,
                $"SABOTAGE NOT DETECTED — {what} produced {diff}, within the noise this fixture " +
                "tolerates. The golden master cannot see this defect class.");
        }

        // ------------------------------------------------------------------ plumbing

        static bool Equal(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        static void ForEachPixel(byte[] rgba, Action<int, Color32> visit)
        {
            for (int i = 0; i < rgba.Length; i += 4)
                visit(i, new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]));
        }

        /// <summary>Visit every byte-index whose pixel lies inside a WORLD-space rect (hull-origin
        /// relative), shrunk by <paramref name="shrinkPx"/> to keep assertions off the exact edge.</summary>
        static void ForEachPixelInWorldRect(HullScene scene, Rect rect, int shrinkPx, Action<int> visit)
        {
            var d = scene.Data;
            int x0 = Mathf.CeilToInt((float)d.PivotX + rect.xMin * d.PxPerMetre) + shrinkPx;
            int x1 = Mathf.FloorToInt((float)d.PivotX + rect.xMax * d.PxPerMetre) - shrinkPx;
            int y0 = Mathf.CeilToInt((float)d.PivotY - rect.yMax * d.PxPerMetre) + shrinkPx;
            int y1 = Mathf.FloorToInt((float)d.PivotY - rect.yMin * d.PxPerMetre) - shrinkPx;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    visit((y * d.W + x) * 4);
        }

        /// <summary>
        /// A self-cleaning render harness: one configured hull, one camera aimed so the rig
        /// pivot lands on its exact cell pixel (the spike's framing), readback flipped to the
        /// rig's top-left orientation. Waits out shader compilation before measuring — the
        /// cold-shader-cache trap fakes exactly the regressions this fixture hunts.
        /// </summary>
        sealed class HullScene : IDisposable
        {
            public readonly RigMeshData Data;
            public readonly IsoFacetHullRenderer Hull;
            readonly GameObject _hullGo;
            readonly GameObject _camGo;
            readonly Camera _cam;
            readonly RenderTexture _rt;
            readonly Vector3 _origin;
            bool _warm;

            public HullScene(RigMeshData data, Mesh mesh,
                             IsoFacetHullSetup setup = null, Vector3 worldOrigin = default)
            {
                Data = data;
                _origin = worldOrigin;

                _hullGo = new GameObject("TestHull");
                _hullGo.transform.position = worldOrigin;
                Hull = _hullGo.AddComponent<IsoFacetHullRenderer>();
                Hull.Configure(setup ?? SetupFrom(data, mesh));
                SetLayerRecursive(_hullGo.transform, ProbeLayer);

                float ppu = data.PxPerMetre;
                float ox = (float)((data.PivotX - data.W / 2.0) / ppu);
                float oy = (float)((data.H / 2.0 - data.PivotY) / ppu);
                _camGo = new GameObject("TestHullCam");
                _cam = _camGo.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.orthographicSize = data.H / (2f * ppu);
                _cam.transform.position = worldOrigin + new Vector3(-ox, -oy, -100f);
                _cam.nearClipPlane = 1f;
                _cam.farClipPlane = 400f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = Color.clear;
                _cam.cullingMask = 1 << ProbeLayer;
                _cam.allowHDR = false;    // byte-exact palette needs the 8-bit sRGB path
                _cam.allowMSAA = false;

                _rt = new RenderTexture(data.W, data.H, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point,
                };
                _cam.targetTexture = _rt;
            }

            public static IsoFacetHullSetup SetupFrom(RigMeshData data, Mesh mesh)
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

            public void SetPose(RigViewOptions view)
            {
                Hull.HeadingDirUnits = (float)view.Dir;
                Hull.RollDegrees = (float)view.RollDegrees;
                Hull.PitchDegrees = (float)view.PitchDegrees;
                Hull.HeavePixels = (float)view.HeavePixels;
                Hull.ApplyPose();
            }

            public byte[] Render()
            {
                EnsureVariantsCompiled();
                _cam.Render();
                return ReadBackTopLeft();
            }

            /// <summary>A full-frame sprite (plain SpriteRenderer, default 2D material) for the
            /// sorting proof. Caller destroys it.</summary>
            public GameObject AddCoveringSprite(Color32 tint, int sortingOrder)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false, true);
                var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 0.1f);

                var go = new GameObject("CoveringSprite") { layer = ProbeLayer };
                go.transform.position = _origin;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;                    // 40x40 world units — covers any cell here
                sr.color = tint;
                sr.sortingOrder = sortingOrder;
                // The project's DEFAULT sprite material is the LIT one, and this scene has no
                // Light2D — a lit sprite would render black and fake a sorting failure. The
                // sorting question is identical either way; ask it with the unlit material.
                var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                Assert.IsNotNull(unlit, "URP's Sprite-Unlit-Default shader is missing?");
                sr.sharedMaterial = new Material(unlit);
                _warm = false;                         // new material variant may need compiling
                return go;
            }

            /// <summary>Render with a flat HHHullDeck probe quad at world z (hull-origin frame).</summary>
            public byte[] RenderWithDeckProbe(Rect rect, float z, Color32 color)
            {
                var shader = Shader.Find("HiddenHarbours/_HullDeckProbe");
                Assert.IsNotNull(shader, "HullDeckProbe.shader missing — the deck contract has no probe.");

                var mesh = new Mesh { name = "DeckProbeQuad" };
                mesh.SetVertices(new[]
                {
                    new Vector3(rect.xMin, rect.yMin, 0), new Vector3(rect.xMax, rect.yMin, 0),
                    new Vector3(rect.xMax, rect.yMax, 0), new Vector3(rect.xMin, rect.yMax, 0),
                });
                mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

                var mat = new Material(shader);
                mat.SetColor("_ProbeColor", ((Color)color).linear);
                mat.SetColor("_KeyColor", ((Color)Data.Keyline).linear);

                var go = new GameObject("DeckProbe") { layer = ProbeLayer };
                go.transform.position = _origin + new Vector3(0, 0, z);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                var props = new MaterialPropertyBlock();
                props.SetFloat(IsoFacetShaderIds.HullId, Hull.HullId / 255f);
                mr.SetPropertyBlock(props);

                try
                {
                    _warm = false;
                    return Render();
                }
                finally
                {
                    Object.DestroyImmediate(go);
                    Object.DestroyImmediate(mat);
                    Object.DestroyImmediate(mesh);
                }
            }

            /// <summary>Block until a render stops triggering shader compilation (the cold-cache
            /// trap: URP's async-compile placeholder produces a wrong image that is
            /// indistinguishable from a real regression — see the sprite-matrix guard's history).</summary>
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
                        "HULL SHADERS NEVER FINISHED COMPILING — this is NOT a facet-pass " +
                        $"regression. After {renders} warm-up render(s) and " +
                        $"{clock.Elapsed.TotalSeconds:F1}s, the compiler was still busy, so a " +
                        "measuring render would land on the async placeholder and produce a fake " +
                        "diff. Re-run with a warm shader cache; if it never settles, check the " +
                        "console for compile errors in the IsoFacet shaders.");
                _warm = true;
            }

            byte[] ReadBackTopLeft()
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _rt;
                var tex = new Texture2D(Data.W, Data.H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Data.W, Data.H), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                var px = tex.GetPixels32();
                Object.DestroyImmediate(tex);

                // GetPixels32 is BOTTOM-left origin; the rig's cell is TOP-left. Flip once, here.
                int w = Data.W, h = Data.H;
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
                RenderTexture.active = null;
                if (_cam != null) _cam.targetTexture = null;
                if (_camGo != null) Object.DestroyImmediate(_camGo);
                if (_hullGo != null) Object.DestroyImmediate(_hullGo);
                if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
            }
        }
    }
}
