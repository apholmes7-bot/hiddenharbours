using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>ADR 0022 phase 8 acceptance: the eighteen lobster variants, drawn by the MESH RENDERER,
    /// against the rig's own picture of the same hull.</b>
    ///
    /// <para><b>Why this fixture had to exist rather than reusing the phase-4 one.</b>
    /// <c>IsoFacetLobsterEndToEndTests</c> compares the mesh render against the hull's BAKED
    /// 32-facing SHEET, which is the strongest truth available — it is a picture that predates the
    /// mesh path entirely. None of these eighteen has a sheet and none ever will (that is what
    /// mesh-only means), so the truth side here is the rig's own <c>render(dir, opts)</c> for that
    /// variant, evaluated live in the repo's V8 host. Same shape of comparison, one round-trip
    /// shorter.</para>
    ///
    /// <para><b>What it is actually proving, and why "it looks like a boat" would not do.</b> A
    /// builder can bake the sprite fallback invisibly — this project has shipped that — so a
    /// screenshot of a lobster boat proves nothing about which path drew it. The subject here is a
    /// live <see cref="IsoFacetHullRenderer"/> configured from the COMMITTED
    /// <see cref="HullMeshDef"/> through the production
    /// <c>IsoFacetHullPresentationService.ToSetup</c>; if the mesh were missing or unusable there
    /// would be no geometry in the cell at all, and the whole-cell backstop below would be 100%.</para>
    ///
    /// <para><b>And the sabotage is the part that makes the green mean something.</b> Eighteen hulls
    /// off one generator look alike, and the rig resolves an unknown axis id to its DEFAULT
    /// silently — so the failure this bake could plausibly ship is one variant's def under another
    /// variant's name. <see cref="Sabotage_ASisterHullsDef_IsCaught"/> renders a NEIGHBOUR's
    /// committed def against this variant's rig picture and requires it to blow past the floor. If
    /// that ever passes, every green above is worth nothing, because the comparison cannot tell the
    /// eighteen apart.</para>
    ///
    /// <para><b>Noise floor — MEASURED on this family, not inherited.</b> The first run of this
    /// fixture reused <c>IsoFacetLobsterEndToEndTests</c>'s floors (cardinal 300 / fractional 150)
    /// and went red at 327 — on the two OFFSHORE hulls, at both broadside dirs, at exactly the same
    /// value on two different variants. That is the signature of an edge-length effect, not a
    /// defect: those hulls are 14.6 m against the 12.0 m the old floors were measured on, and the
    /// phase-4 doc already names beam-on cardinals as the worst class ("longest straight edges",
    /// cluster 253 at 12 m — 327/253 = 1.29 against a length ratio of 1.22).</para>
    ///
    /// <para>Re-measured across all 32 views here (RTX 4060, D3D12, 2026-08-15): whole-cell
    /// divergence <b>2.14–4.22%</b>, inside ADR 0022's own 1.3–4.4% band; worst cardinal cluster
    /// <b>327</b>, worst fractional <b>204</b>; silhouette (opaque-vs-transparent) disagreement
    /// 4–78 px on hulls of 16k–66k inked pixels. Floors set a fifth above the measurements.</para>
    ///
    /// <para><b>And the floors are known to sit far below a real defect</b>, because the sabotage
    /// arm measures exactly that on every run. Smallest available lie — same hull, wrong ROOF —
    /// cluster <b>2,367</b> (11.5%); wrong region <b>24,012</b> (54.9%); wrong size <b>43,743</b>
    /// (71.2%). So the cardinal floor is ~6× below the quietest defect this bake could ship and
    /// ~134× below the loudest.</para>
    ///
    /// <para>CI has no GPU: every test here gates on a graphics device and skips loudly.</para>
    /// </summary>
    public class IsoFacetLobsterVariantAcceptanceTests
    {
        const int ProbeLayer = 31;
        /// <summary>Measured worst 327 (offshore, beam-on); smallest sabotage 2,367.</summary>
        const int MaxCardinalNoiseCluster = 400;
        /// <summary>Measured worst 204 (offshore, dir 5.75); smallest sabotage 2,367.</summary>
        const int MaxFractionalNoiseCluster = 260;
        /// <summary>Measured worst 4.2243%; smallest sabotage 11.47%.</summary>
        const double MaxPercent = 5.0;

        /// <summary>
        /// The cells this fixture photographs. Not all eighteen: each is eight GPU renders plus eight
        /// V8 renders of a 480×420 cell, and a fixture the owner learns to dread is a fixture that
        /// gets disabled. These four span every axis value at least once — both styles, all three
        /// sizes, all three regions — so a bake that got one axis wrong cannot hide.
        ///
        /// <para>The whole-fleet question (are all eighteen committed meshes distinct, and does each
        /// def record the cell its id claims) is a TEST's job elsewhere, on the CPU, where it can
        /// afford all eighteen: <c>LobsterVariantFleetTests</c>.</para>
        /// </summary>
        static readonly LobsterVariant[] Photographed =
        {
            new LobsterVariant("standard", "hardtop", "northumberland"),  // IS the shipped lobster boat's hull form
            new LobsterVariant("inshore",  "open",    "fundy"),
            new LobsterVariant("offshore", "hardtop", "newfoundland"),
            new LobsterVariant("offshore", "open",    "newfoundland"),    // the off-centre masthead cell
        };

        GameConfig _keylineConfig;
        GameConfig _prevConfig;

        /// <summary>ADR 0031 ships the mesh fleet's keyline OFF, but the rig's own render draws one,
        /// so the mesh render must carry it for the comparison to be about geometry. Forced through
        /// the owner's real dial, never a parallel test-only path.</summary>
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
            if (_keylineConfig != null) UnityEngine.Object.DestroyImmediate(_keylineConfig);
            _keylineConfig = null;
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null Device), " +
                    "so the mesh-path acceptance could not render and proved nothing. Expected on CI; " +
                    "run on a machine with a GPU to actually verify phase 8.");
        }

        static HullMeshDef LoadDef(LobsterVariant v)
        {
            string path = $"Assets/_Project/Data/Boats/HullMeshes/{v.AssetName}HullMesh.asset";
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(path);
            Assert.IsNotNull(def, $"missing {path} — bake it first (3D Hulls ▸ Bake the 18 lobster " +
                                  "variant hull meshes)");
            Assert.IsTrue(def.IsUsable(), $"{v.Variant}: the committed def must be usable");
            Assert.AreEqual(v.MeshId, def.Id, $"{v.Variant}: wrong def at {path}");
            return def;
        }

        // ------------------------------------------------------------------ the acceptance

        /// <summary>
        /// <b>The mesh renderer draws THIS hull</b> — four cells, eight views each, against the rig's
        /// own render of the same variant at the same view.
        ///
        /// <para>Views are chosen the way the phase-4 fixture chose sheet cells: the four cardinals,
        /// where the long straight edges make the facet noise worst, plus four fractional dirs that a
        /// 32-facing sheet would have had cells for and an 8-way stepping would have rounded away.
        /// </para>
        /// </summary>
        [Test]
        public void EveryPhotographedVariant_RendersAsHerOwnRig()
        {
            RequireAGraphicsDevice();

            var report = new StringBuilder();
            int worstCardinal = 0, worstFractional = 0;
            double worstPercent = 0;

            foreach (LobsterVariant v in Photographed)
            {
                HullMeshDef def = LoadDef(v);
                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(RigCatalog.RepoRoot, LobsterVariantFleet.ScriptPath)));

                foreach (double dir in new[] { 0.0, 2.0, 4.0, 6.0, 1.5, 3.25, 5.75, 7.125 })
                {
                    byte[] truth = RigRender(host, v, def, dir);
                    byte[] gpu = RenderMeshAtDir(def, dir);
                    var diff = RigMeshReferenceRasterizer.Compare(truth, gpu, def.CellW, def.CellH);

                    bool cardinal = dir == Math.Floor(dir) && ((int)dir % 2) == 0;
                    report.AppendLine($"  {v.Variant} dir {dir} ({(cardinal ? "cardinal" : "fractional")}): {diff}");
                    Debug.Log($"[lobster-variant-e2e] {v.Variant} dir {dir}: {diff}");

                    if (cardinal) worstCardinal = Math.Max(worstCardinal, diff.LargestDifferingCluster);
                    else worstFractional = Math.Max(worstFractional, diff.LargestDifferingCluster);
                    worstPercent = Math.Max(worstPercent, diff.PercentDiffering);
                }
            }

            Assert.LessOrEqual(worstCardinal, MaxCardinalNoiseCluster,
                $"The mesh render diverged from the rig beyond the cardinal noise floor:\n{report}" +
                "A connected patch this size is a real defect — in the committed def, in the mesh " +
                "path, or in which variant was baked under this id.");
            Assert.LessOrEqual(worstFractional, MaxFractionalNoiseCluster,
                $"Beyond the fractional-heading noise floor:\n{report}");
            Assert.Less(worstPercent, MaxPercent,
                $"Whole-cell backstop:\n{report}A figure near 100% means NOTHING was drawn — the " +
                "renderer fell back rather than skinning the mesh.");
        }

        // ------------------------------------------------------------------ SABOTAGE

        /// <summary>
        /// ⚠️ <b>Can this comparison actually tell two lobster variants apart?</b>
        ///
        /// <para>The failure a bake of eighteen near-identical hulls off one generator can plausibly
        /// ship is not a broken renderer — it is the RIGHT renderer drawing the WRONG sister, because
        /// the rig swallows an unrecognised axis id and hands back its default. Feed this variant's
        /// rig picture a NEIGHBOUR's committed def (one axis apart, the smallest lie available) and
        /// the comparison must blow past the floor.</para>
        ///
        /// <para>Both directions of the pair are measured, and the margin is logged, because a
        /// sabotage that only just trips is one hull reshape away from not tripping at all.</para>
        /// </summary>
        [Test]
        public void Sabotage_ASisterHullsDef_IsCaught()
        {
            RequireAGraphicsDevice();

            var pairs = new[]
            {
                // one axis apart in each direction: style, then region, then size
                (new LobsterVariant("standard", "hardtop", "northumberland"),
                 new LobsterVariant("standard", "open",    "northumberland")),
                (new LobsterVariant("standard", "hardtop", "northumberland"),
                 new LobsterVariant("standard", "hardtop", "fundy")),
                (new LobsterVariant("standard", "hardtop", "fundy"),
                 new LobsterVariant("offshore", "hardtop", "fundy")),
            };

            var report = new StringBuilder();
            var tooQuiet = new List<string>();

            foreach ((LobsterVariant truthOf, LobsterVariant drawnAs) in pairs)
            {
                HullMeshDef wrong = LoadDef(drawnAs);
                HullMeshDef right = LoadDef(truthOf);
                Assert.AreEqual(right.CellW, wrong.CellW, "the two cells must be comparable at all");

                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(RigCatalog.RepoRoot, LobsterVariantFleet.ScriptPath)));

                const double dir = 2.0;                       // broadside: most hull on show
                byte[] truth = RigRender(host, truthOf, right, dir);
                byte[] gpu = RenderMeshAtDir(wrong, dir);
                var diff = RigMeshReferenceRasterizer.Compare(truth, gpu, right.CellW, right.CellH);

                report.AppendLine($"  {truthOf.Variant} drawn as {drawnAs.Variant}: {diff}");
                Debug.Log($"[lobster-variant-e2e][SABOTAGE] {truthOf.Variant} drawn as " +
                          $"{drawnAs.Variant}: {diff}");

                if (diff.LargestDifferingCluster <= MaxCardinalNoiseCluster)
                    tooQuiet.Add($"{truthOf.Variant} vs {drawnAs.Variant} " +
                                 $"(cluster {diff.LargestDifferingCluster} ≤ {MaxCardinalNoiseCluster})");
            }

            CollectionAssert.IsEmpty(tooQuiet,
                "SABOTAGE NOT DETECTED — a SISTER VARIANT'S committed def stayed inside the noise " +
                "floor, so the acceptance above cannot tell two of the eighteen apart and every " +
                $"green run is worth less than it looks:\n{report}");
        }

        // ------------------------------------------------------------------ plumbing

        /// <summary>The rig's own picture of this variant, at this dir — the truth side. The elevation
        /// is the def's, so the two sides are photographed through one camera.</summary>
        static byte[] RigRender(IRigScriptHost host, LobsterVariant v, HullMeshDef def, double dir)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            string opts = $"Object.assign({{}},{v.JsDescriptor},{{elev:{def.ElevationDeg.ToString("R", c)}}})";
            byte[] rgba = host.EvaluateBytes(
                $"{LobsterVariantFleet.GlobalName}.render({dir.ToString("R", c)},{opts})");
            Assert.AreEqual(def.CellW * def.CellH * 4, rgba.Length,
                $"{v.Variant}: the rig's cell is not the def's cell — the comparison would be nonsense");
            return rgba;
        }

        /// <summary>Render the committed def through the REAL production components at a rig dir.
        /// Posed by dir rather than by compass heading on purpose: this fixture is asking whether the
        /// MESH PATH draws this hull, and routing through the heading mapping would fold the def's own
        /// azimuth flag into both sides of the comparison and prove nothing about either.</summary>
        static byte[] RenderMeshAtDir(HullMeshDef def, double dir)
        {
            var go = new GameObject("VariantHull") { layer = ProbeLayer };
            try
            {
                var renderer = go.AddComponent<IsoFacetHullRenderer>();
                renderer.Configure(IsoFacetHullPresentationService.ToSetup(def));
                renderer.HeadingDirUnits = (float)dir;
                renderer.ApplyPose();
                SetLayerRecursive(go.transform, ProbeLayer);
                return RenderCell(def, go);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>The spike's framing: an ortho camera sized to the cell, offset so the rig pivot
        /// lands on its exact cell pixel; readback flipped to the rig's top-left orientation.</summary>
        static byte[] RenderCell(HullMeshDef def, GameObject subject)
        {
            float ppu = def.PxPerMetre;
            float ox = (def.PivotPx.x - def.CellW / 2f) / ppu;
            float oy = (def.CellH / 2f - def.PivotPx.y) / ppu;

            var camGo = new GameObject("VariantCam");
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
                UnityEngine.Object.DestroyImmediate(camGo);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
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
            UnityEngine.Object.DestroyImmediate(tex);

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
