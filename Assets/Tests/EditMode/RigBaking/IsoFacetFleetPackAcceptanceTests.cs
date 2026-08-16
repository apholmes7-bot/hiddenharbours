using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The five fleet-pack hulls, drawn by the real mesh path, against their own rigs' pictures.</b>
    ///
    /// <para>The sibling of <see cref="IsoFacetLobsterVariantAcceptanceTests"/> and the same
    /// argument: a committed <c>HullMeshDef</c> is an opaque binary blob, so the only thing that can
    /// say it is THIS boat is photographing it through the production renderer and comparing against
    /// the rig that made it. Everything else this PR asserts — ids, cells, face counts, fingerprints
    /// — is bookkeeping about a mesh nobody has looked at.</para>
    ///
    /// <para><b>Why these five need their own fixture rather than rows in hers.</b> Three different
    /// rig SHAPES draw here, and the truth side has to be fetched three different ways:</para>
    /// <list type="bullet">
    ///   <item>the zodiac takes a build descriptor on her global — <c>render(dir,{build:'frc'})</c>;</item>
    ///   <item>the sport skiff Mk2 takes no descriptor at all, like the eleven;</item>
    ///   <item>the sport fisher is a REGISTRY, and her renderer is on the HULL —
    ///   <c>byId('skybridge').render(dir,…)</c>. Her global has none.</item>
    /// </list>
    /// <para>That is exactly the split <see cref="RigHullExtraction.ScopeOr"/> and
    /// <see cref="RigHullExtraction.ViewOptions"/> encode, so this fixture asks the fleet rows for
    /// both rather than restating them — which also means a wrong scope in the fleet table fails
    /// HERE, in pixels, and not only in a CPU fingerprint.</para>
    /// </summary>
    public class IsoFacetFleetPackAcceptanceTests
    {
        const int ProbeLayer = 31;

        /// <summary>⚠️ MEASURED 2026-08-16 on an RTX 4060, then set just above the worst honest
        /// disagreement observed. On the hulls held to this floor: sport fisher 53ft 4.00%
        /// (cluster 292), 90ft 2.71% (cluster 605), and the shipped v1 sport skiff CONTROL 5.43%
        /// (cluster 111). Worst cluster on any strict-floor hull: 605 cardinal and 471 fractional,
        /// both the 90ft skybridge, whose 1200x1170 cell simply has more long straight edge than
        /// anything else in the fleet. The sabotage arm clears these floors by 11-20x, which is what
        /// makes them worth having.</summary>
        const int MaxCardinalNoiseCluster = 700;
        const int MaxFractionalNoiseCluster = 550;
        const double MaxPercent = 6.0;

        /// <summary>
        /// ⚠️⚠️ <b>THE TWO HULLS THIS FIXTURE CANNOT HOLD TO THE FLOOR ABOVE, AND WHY.</b>
        ///
        /// <para>Both are pass-2 rigs whose material tables carry a per-material <c>dith</c> weight
        /// — <c>0</c> means "round to the nearest ramp step: crisp banded gelcoat", <c>1</c> means
        /// the full 4x4 Bayer. <b>The mesh path does not model it.</b>
        /// <c>RigMeshData.Materials</c> is <c>{ramp, off}</c> and the rasteriser thresholds against
        /// one uniform Bayer value, so the mesh dithers panels the rig draws crisp. On the Mk2 that
        /// is ELEVEN OF HER THIRTEEN materials at <c>dith:0</c> — i.e. most of the boat.</para>
        ///
        /// <para><b>This is not new and it is not this bake’s doing.</b>
        /// <c>RigMeshSymbols.Reconstructions</c> has recorded it since the pass-2 small craft
        /// landed, in the entry for the punt and the console: "a real, visible gap and it is a
        /// separate piece of work — one mesh per scheme, or ramps derived at runtime, is an
        /// architecture decision, not a bake flag." The already-shipped console skiff carries the
        /// same gap. Closing it is an ADR-level call and is out of this PR’s scope.</para>
        ///
        /// <para><b>MEASURED, and the control is what makes this a diagnosis rather than an
        /// excuse.</b> The shipped v1 sport skiff has NO <c>dith</c> and reads <b>5.43%</b>; the Mk2
        /// carries it on all thirteen materials and reads <b>22.29%</b> — at the IDENTICAL cell
        /// (244x216, 32 px/m, elev 40), through this identical harness, on the same run. The
        /// zodiac’s two builds read 24.97% and 27.20%. Same signature on all three: the silhouette
        /// agrees (0-5 px on the Mk2) and the interior colour does not.</para>
        ///
        /// <para>⚠️ <b>The looser bound is NOT a pass.</b> It is wide enough to admit a real defect,
        /// which is why the sabotage arm below runs against these SAME hulls and must clear it — a
        /// sister hull’s def reads 7,984-13,944 against this 900. Until the dither is modelled these
        /// two are pixel-checked for "the right boat, roughly the right colours", not for the
        /// agreement the rest of the fleet is held to. Say so in any review that leans on this
        /// fixture.</para>
        /// </summary>
        const double MaxPercentUnmodelledDither = 30.0;
        const int MaxClusterUnmodelledDither = 900;

        /// <summary>The hulls the bound above applies to. Their premise is CHECKED by
        /// <see cref="TheDitherExemption_StillDescribesTheseRigs"/>, so the exemption cannot outlive
        /// the gap that justifies it.</summary>
        static readonly string[] CarryUnmodelledDither =
            { "sportSkiffMk2", "zodiacHurricane", "zodiacFrc" };

        /// <summary>One photographed hull, and the three things that differ between rig shapes.</summary>
        readonly struct Subject
        {
            public readonly string Key, Label, ScriptPath, GlobalName, AssetName;
            public readonly RigHullExtraction Extraction;   // null = a one-hull rig, like the eleven

            public Subject(string key, string label, string scriptPath, string globalName,
                           string assetName, RigHullExtraction extraction)
            {
                Key = key; Label = label; ScriptPath = scriptPath; GlobalName = globalName;
                AssetName = assetName; Extraction = extraction;
            }

            /// <summary>Where this hull's <c>render</c> lives — the global, or the hull object on a
            /// registry rig.</summary>
            public string RenderScope =>
                Extraction != null ? Extraction.ScopeOr(GlobalName) : GlobalName;

            /// <summary>The descriptor her public entry points take, or <c>{}</c>.</summary>
            public string ViewOptions =>
                Extraction != null && !string.IsNullOrEmpty(Extraction.ViewOptions)
                    ? Extraction.ViewOptions
                    : "{}";
        }

        static IEnumerable<Subject> Photographed()
        {
            foreach (ZodiacBuild b in ZodiacFleet.All)
                yield return new Subject(b.Key, b.Label, ZodiacFleet.ScriptPath,
                                         ZodiacFleet.GlobalName, b.AssetName, b.Extraction);

            foreach (SportFisherHull h in SportFisherFleet.All)
                yield return new Subject(h.Key, h.Label, SportFisherFleet.ScriptPath,
                                         SportFisherFleet.GlobalName, h.AssetName, h.Extraction);

            yield return new Subject("sportSkiffMk2", "sport skiff Mk2",
                                     "docs/art/rigs/sportSkiffMk2IsoRig.js", "SportSkiffMk2Iso",
                                     "SportSkiffMk2Iso", null);

            // ⚠️ THE CONTROL, and it is not decoration. She is the SHIPPED v1 sport skiff: committed
            // long before this branch, untouched by it, and baked through the same extractor. If she
            // reads the same disagreement as the hulls this PR added, then the number is a property
            // of this HARNESS and not of the bake — which is the only way to tell those two apart
            // without guessing. She is also the Mk2's sister at the identical cell, so she costs
            // nothing extra to photograph.
            yield return new Subject("sportSkiff", "shipped v1 sport skiff (CONTROL)",
                                     "docs/art/rigs/sportSkiffIsoRig.js", "SportSkiffIso",
                                     "SportSkiffIso", null);
        }

        GameConfig _keylineConfig;
        GameConfig _prevConfig;

        /// <summary>
        /// <b>Does THIS rig's own render draw a keyline?</b> Read from the rig, never assumed.
        ///
        /// <para>⚠️ This started as a fixture-wide "force it on", copied from the lobster fixture
        /// where it is correct, and it made the zodiac read 24-30% differing pixels with a nearly
        /// perfect silhouette — the signature of a ring the mesh drew and the rig did not. Her header
        /// says why in as many words: "NO KEYLINE. Per ADR 0031 she bakes ringless from birth", and
        /// she exports <c>KEYLINE_DEFAULT = false</c> to prove it. Every other rig here draws one
        /// unconditionally.</para>
        /// </summary>
        static bool RigDrawsAKeyline(IRigScriptHost host, string globalName) =>
            !host.EvaluateBool($"{globalName}.KEYLINE_DEFAULT === false");

        /// <summary>ADR 0031 ships the mesh fleet's keyline OFF, but the rigs' own renders draw one,
        /// so the mesh render must carry it for the comparison to be about GEOMETRY. Forced through
        /// the owner's real dial, never a parallel test-only path.</summary>
        [SetUp]
        public void CaptureTheConfig()
        {
            _prevConfig = GameServices.Config;
            _keylineConfig = ScriptableObject.CreateInstance<GameConfig>();
            GameServices.Config = _keylineConfig;
        }

        /// <summary>Match the mesh side's keyline to what this rig's own render does — through the
        /// owner's real dial, never a parallel test-only path.</summary>
        void MatchKeylineTo(bool rigDrawsOne) => _keylineConfig.HullKeylineFlood = rigDrawsOne;

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
                    "run on a machine with a GPU to actually verify these five.");
        }

        static HullMeshDef LoadDef(string assetName, string who)
        {
            string path = $"Assets/_Project/Data/Boats/HullMeshes/{assetName}HullMesh.asset";
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(path);
            Assert.IsNotNull(def, $"missing {path} — bake it first (3D Hulls ▸ Bake the 5 " +
                                  "fleet-pack hull meshes)");
            Assert.IsTrue(def.IsUsable(), $"{who}: the committed def must be usable");
            return def;
        }

        // ------------------------------------------------------------------ the acceptance

        /// <summary>
        /// <b>The mesh renderer draws THIS hull</b> — five hulls, eight views each, against each
        /// rig's own render at the same view.
        ///
        /// <para>Views follow the phase-4 fixture: the four cardinals, where long straight edges make
        /// the facet noise worst, plus four fractional dirs that an 8-way stepping would have rounded
        /// away.</para>
        /// </summary>
        [Test]
        public void EveryFleetPackHull_RendersAsHerOwnRig()
        {
            RequireAGraphicsDevice();

            var report = new StringBuilder();
            int worstCardinal = 0, worstFractional = 0;
            double worstPercent = 0;
            int worstDitherCluster = 0;
            double worstDitherPercent = 0;

            foreach (Subject s in Photographed())
            {
                HullMeshDef def = LoadDef(s.AssetName, s.Label);
                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(RigCatalog.RepoRoot, s.ScriptPath)));
                MatchKeylineTo(RigDrawsAKeyline(host, s.GlobalName));

                foreach (double dir in new[] { 0.0, 2.0, 4.0, 6.0, 1.5, 3.25, 5.75, 7.125 })
                {
                    byte[] truth = RigRender(host, s, def, dir);
                    byte[] gpu = RenderMeshAtDir(def, dir);
                    var diff = RigMeshReferenceRasterizer.Compare(truth, gpu, def.CellW, def.CellH);

                    bool cardinal = dir == Math.Floor(dir) && ((int)dir % 2) == 0;
                    bool dither = Array.IndexOf(CarryUnmodelledDither, s.Key) >= 0;
                    report.AppendLine($"  {s.Label} dir {dir} " +
                                      $"({(cardinal ? "cardinal" : "fractional")}" +
                                      $"{(dither ? ", UNMODELLED DITHER" : "")}): {diff}");
                    Debug.Log($"[fleet-pack-e2e] {s.Key} dir {dir}: {diff}");

                    if (dither)
                    {
                        worstDitherCluster = Math.Max(worstDitherCluster, diff.LargestDifferingCluster);
                        worstDitherPercent = Math.Max(worstDitherPercent, diff.PercentDiffering);
                        continue;
                    }

                    if (cardinal) worstCardinal = Math.Max(worstCardinal, diff.LargestDifferingCluster);
                    else worstFractional = Math.Max(worstFractional, diff.LargestDifferingCluster);
                    worstPercent = Math.Max(worstPercent, diff.PercentDiffering);
                }
            }

            Assert.LessOrEqual(worstCardinal, MaxCardinalNoiseCluster,
                $"The mesh render diverged from the rig beyond the cardinal noise floor:\n{report}" +
                "A connected patch this size is a real defect — in the committed def, in the mesh " +
                "path, or in WHICH hull was baked under this id.");
            Assert.LessOrEqual(worstFractional, MaxFractionalNoiseCluster,
                $"Beyond the fractional-heading noise floor:\n{report}");
            Assert.Less(worstPercent, MaxPercent,
                $"Whole-cell backstop:\n{report}A figure near 100% means NOTHING was drawn — the " +
                "renderer fell back rather than skinning the mesh.");

            // The dither-carrying hulls, held to their own documented bound. Deliberately still
            // ASSERTED rather than skipped: a bound this loose cannot prove agreement, but it does
            // catch a hull that stopped drawing, and it fails loudly if the gap ever WIDENS.
            Assert.Less(worstDitherPercent, MaxPercentUnmodelledDither,
                $"The unmodelled-dither hulls exceeded even their own widened bound:\n{report}" +
                "That is worse than the known gap explains — see MaxPercentUnmodelledDither.");
            Assert.LessOrEqual(worstDitherCluster, MaxClusterUnmodelledDither,
                $"The unmodelled-dither hulls exceeded their widened cluster bound:\n{report}");
        }

        /// <summary>
        /// <b>The exemption above must not outlive the gap it claims.</b>
        ///
        /// <para>A widened bound justified by a rig property is worth exactly as much as that
        /// property still being true. So this reads the rigs: each exempted hull must still declare
        /// a per-material <c>dith</c> weight, and the CONTROL — the shipped v1 sport skiff, which
        /// meets the strict floor — must still not. The day the mesh path models dither, or an art
        /// drop removes it, this fails and the exemption comes out.</para>
        /// </summary>
        [Test]
        public void TheDitherExemption_StillDescribesTheseRigs()
        {
            foreach (Subject s in Photographed())
            {
                string source = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(RigCatalog.RepoRoot, s.ScriptPath));
                bool declaresDither = source.Contains("dith:");
                bool exempted = Array.IndexOf(CarryUnmodelledDither, s.Key) >= 0;

                Assert.AreEqual(exempted, declaresDither,
                    exempted
                        ? $"{s.Label} is exempted from the strict pixel floor because her rig carries "
                          + "a per-material dith weight the mesh path does not model — and her rig "
                          + "no longer declares one. Take her out of CarryUnmodelledDither and hold "
                          + "her to the floor."
                        : $"{s.Label} now declares a per-material dith weight, which the mesh path "
                          + "does not model. Either she belongs in CarryUnmodelledDither with the "
                          + "others, or the dither is finally modelled and the whole exemption goes.");
            }
        }

        // ------------------------------------------------------------------ SABOTAGE

        /// <summary>
        /// ⚠️ <b>Can this comparison tell two hulls of the same size apart?</b>
        ///
        /// <para>The two pairs here are the realistic confusions in this bake, and the first is the
        /// most realistic defect in the whole PR:</para>
        /// <list type="bullet">
        ///   <item><b>sport skiff Mk2 against the shipped v1.</b> Same boat, reshaped; the SAME cell
        ///   (244×216 at 32 px/m, elev 40); near-identical rig file names; and the art drop's own
        ///   sidecar for the Mk2 arrived NAMED for the v1 and pinning the v1's rig hash. If anything
        ///   in this PR wired one to the other, this is the arm that says so.</item>
        ///   <item><b>zodiac hurricane against frc.</b> Sister builds off one loft in one cell, which
        ///   is the same shape of risk the eighteen lobster variants carry — and her generator
        ///   swallows an unknown build id exactly as theirs does.</item>
        /// </list>
        ///
        /// <para><b>The sport fisher is deliberately NOT here, and her absence is a property rather
        /// than a gap.</b> Her two hulls are 820×770 and 1200×1170, so drawing one as the other is
        /// not a subtle pixel difference — the cell sizes disagree and the comparison cannot even be
        /// set up. Her equivalent risk is at EXTRACTION, where <c>byId</c> falls back silently, and
        /// that is caught on the CPU by
        /// <c>FleetPackHullTests.TheFiveCommittedMeshes_AreFiveDifferentBoats</c>.</para>
        /// </summary>
        [Test]
        public void Sabotage_ASisterHullsDef_IsCaught()
        {
            RequireAGraphicsDevice();

            var report = new StringBuilder();
            var tooQuiet = new List<string>();

            // (truth: whose rig picture, drawnAs: whose committed def)
            var pairs = new List<(Subject Truth, string DrawnAsAsset, string DrawnAsLabel)>
            {
                (new Subject("sportSkiffMk2", "sport skiff Mk2",
                             "docs/art/rigs/sportSkiffMk2IsoRig.js", "SportSkiffMk2Iso",
                             "SportSkiffMk2Iso", null),
                 "SportSkiffIso", "the shipped v1 sport skiff"),
            };
            foreach (ZodiacBuild b in ZodiacFleet.All)
            {
                ZodiacBuild other = b.Build == "hurricane"
                    ? ZodiacFleet.Get("zodiacFrc") : ZodiacFleet.Get("zodiacHurricane");
                pairs.Add((new Subject(b.Key, b.Label, ZodiacFleet.ScriptPath, ZodiacFleet.GlobalName,
                                       b.AssetName, b.Extraction),
                           other.AssetName, other.Label));
            }

            foreach (var (truthOf, drawnAsAsset, drawnAsLabel) in pairs)
            {
                HullMeshDef right = LoadDef(truthOf.AssetName, truthOf.Label);
                HullMeshDef wrong = LoadDef(drawnAsAsset, drawnAsLabel);
                Assert.AreEqual(right.CellW, wrong.CellW,
                    "the two cells must be comparable at all for this arm to mean anything");
                Assert.AreEqual(right.CellH, wrong.CellH, "same");

                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(System.IO.File.ReadAllText(
                    System.IO.Path.Combine(RigCatalog.RepoRoot, truthOf.ScriptPath)));
                MatchKeylineTo(RigDrawsAKeyline(host, truthOf.GlobalName));

                const double dir = 2.0;                       // broadside: most hull on show
                byte[] truth = RigRender(host, truthOf, right, dir);
                byte[] gpu = RenderMeshAtDir(wrong, dir);
                var diff = RigMeshReferenceRasterizer.Compare(truth, gpu, right.CellW, right.CellH);

                report.AppendLine($"  {truthOf.Label} drawn as {drawnAsLabel}: {diff}");
                Debug.Log($"[fleet-pack-e2e][SABOTAGE] {truthOf.Key} drawn as {drawnAsLabel}: {diff}");

                if (diff.LargestDifferingCluster <= MaxCardinalNoiseCluster)
                    tooQuiet.Add($"{truthOf.Label} vs {drawnAsLabel} " +
                                 $"(cluster {diff.LargestDifferingCluster} ≤ {MaxCardinalNoiseCluster})");
            }

            CollectionAssert.IsEmpty(tooQuiet,
                "SABOTAGE NOT DETECTED — another hull's committed def stayed inside the noise floor, " +
                "so the acceptance above cannot tell these two apart and every green run is worth " +
                $"less than it looks:\n{report}");
        }

        // ------------------------------------------------------------------ plumbing

        /// <summary>
        /// The rig's own picture of this hull, at this dir — the truth side. Elevation is the DEF's,
        /// so both sides are photographed through one camera.
        ///
        /// <para>⚠️ The scope is the hull's, not the global's. On the sport fisher
        /// <c>SportFisherIso2.render</c> is <c>undefined</c>, and calling it would throw rather than
        /// draw the wrong boat — but the cell assert below is what would catch a scope that resolved
        /// to a DIFFERENT hull, which is the failure that could pass quietly.</para>
        /// </summary>
        static byte[] RigRender(IRigScriptHost host, Subject s, HullMeshDef def, double dir)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            string opts = $"Object.assign({{}},{s.ViewOptions},{{elev:{def.ElevationDeg.ToString("R", c)}}})";
            byte[] rgba = host.EvaluateBytes(
                $"{s.RenderScope}.render({dir.ToString("R", c)},{opts})");
            Assert.AreEqual(def.CellW * def.CellH * 4, rgba.Length,
                $"{s.Label}: the rig's cell is not the def's cell — the comparison would be nonsense, " +
                "and on a registry rig this is what a HullScope pointing at the wrong hull looks like.");
            return rgba;
        }

        /// <summary>Render the committed def through the REAL production components at a rig dir.
        /// Posed by dir rather than by compass heading on purpose: this asks whether the MESH PATH
        /// draws this hull, and routing through the heading mapping would fold the def's own azimuth
        /// flag into both sides and prove nothing about either.</summary>
        static byte[] RenderMeshAtDir(HullMeshDef def, double dir)
        {
            var go = new GameObject("FleetPackHull") { layer = ProbeLayer };
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

        static byte[] RenderCell(HullMeshDef def, GameObject subject)
        {
            float ppu = def.PxPerMetre;
            float ox = (def.PivotPx.x - def.CellW / 2f) / ppu;
            float oy = (def.CellH / 2f - def.PivotPx.y) / ppu;

            var camGo = new GameObject("FleetPackCam");
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

        /// <summary>A cold shader cache draws nothing on the first frame and fakes a regression —
        /// see the repo's own note on that. Render until the picture stops changing.</summary>
        static void WaitOutShaderCompilation(Camera cam)
        {
            for (int i = 0; i < 8; i++) cam.Render();
        }

        static byte[] ReadBackTopLeft(RenderTexture rt, int w, int h)
        {
            var prev = RenderTexture.active;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                Color32[] px = tex.GetPixels32();

                // GetPixels32 is bottom-up; the rig's buffer is top-left origin.
                var outBytes = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c = px[(h - 1 - y) * w + x];
                    int i = (y * w + x) * 4;
                    outBytes[i] = c.r; outBytes[i + 1] = c.g;
                    outBytes[i + 2] = c.b; outBytes[i + 3] = c.a;
                }
                return outBytes;
            }
            finally
            {
                RenderTexture.active = prev;
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }
    }
}
