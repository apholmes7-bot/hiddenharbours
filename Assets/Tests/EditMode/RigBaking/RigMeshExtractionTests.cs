using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ADR 0022 phase 2 — mesh extraction.
    ///
    /// <para><b>What CI can and cannot prove here.</b> Everything in this fixture is pure CPU
    /// arithmetic through V8 and managed code — no graphics device is touched, so it runs headless
    /// and CI DOES adjudicate it. That is deliberate: the spike's GPU comparison would CRASH the
    /// CI editor ("Null Device": exit 1, no results XML), and a check CI cannot run is a check that
    /// quietly stops running. What CI cannot prove is the phase-3 question — whether the URP facet
    /// shader reproduces the same image on a GPU. That belongs to art-pipeline and needs a human at
    /// a machine with a graphics device.</para>
    ///
    /// <para><b>The bar.</b> ADR 0022 quotes 1.3–4.4% inked-pixel difference. That is a SHADER
    /// number — GPU float precision, fill rules and dither boundaries. Extraction has no business
    /// hiding behind it, so the bar here is roughly ninety times tighter: see
    /// <see cref="MaxResidualPercent"/> for the measured residual, the three causes that were removed
    /// to get there, and the silhouette assert that does the real geometric policing.</para>
    /// </summary>
    public class RigMeshExtractionTests
    {
        /// <summary>The two hulls ADR 0022 measured, plus the punt — the rig whose sprite bake is
        /// byte-identical to the hand export, so a defect here cannot be blamed on the rig.</summary>
        static readonly (string label, string path, string global)[] Hulls =
        {
            ("lobsterBoat", "docs/art/rigs/lobsterBoatIsoRig.js", "LobsterBoatIso"),
            ("sideDragger", "docs/art/rigs/sideDraggerIsoRig.js", "SideDraggerIso"),
            ("punt",        "docs/art/rigs/puntIsoRig.js",        "PuntIso"),
        };

        /// <summary>
        /// Per-cell budget for last-ULP disagreement between V8's double arithmetic and .NET's.
        ///
        /// <para><b>Not a fudge factor, and not fitted to whatever the code happened to produce.</b>
        /// The first version of these tests asserted EXACT and found 1–3 px per cell. Three
        /// candidate causes were each removed and re-measured:</para>
        /// <list type="bullet">
        /// <item><b>Trig.</b> V8 and .NET disagree on <c>cos</c>/<c>sin</c> in the last ULP. Sourcing
        /// the basis from the engine (<see cref="RigTrigBasis.FromScriptHost"/>) made most cells
        /// exact. Not all.</item>
        /// <item><b>Normal derivation.</b> The rig takes its normal from the ROTATED vertices;
        /// rotating an object-space normal instead is <c>R(u×v)</c> vs <c>(Ru)×(Rv)</c> — equal in
        /// exact arithmetic only. Matching the rig made more cells exact. Not all.</item>
        /// <item><b>Normalisation.</b> <c>Math.hypot</c> is not <c>sqrt(x²+y²+z²)</c>; the rig uses
        /// the former, transcribed in <see cref="RigMeshBuilder.Hypot3"/>. ⚠️ This did NOT eliminate
        /// the residual — it MOVED it (the side dragger's worst cell shifted from dir 0 to dir 1).
        /// Kept because it is the faithful transcription, not because it helped.</item>
        /// </list>
        /// <para>What survives is a handful of pixels per cell — worst measured 11/150,077 on the
        /// side dragger and 1/5,536 on the punt — never on the silhouette, always a single shade
        /// step at a facet or dither boundary, and stable run to run.</para>
        ///
        /// <para><b>Why a percentage and not a pixel count.</b> A count was tried first and is the
        /// wrong instrument: the punt's single stray pixel is four times more significant, per inked
        /// pixel, than eleven on the side dragger, whose cell is 27× larger. A budget in pixels
        /// silently tightens as hulls grow.</para>
        ///
        /// <para>0.05% is a few times the worst observed and still ~90× tighter than ADR 0022's
        /// 1.3–4.4% shader figure. The companion assert on
        /// <see cref="RigPixelDiff.CoverageOnlyDifferences"/> is what actually polices geometry:
        /// rounding changes a pixel's colour, it never changes whether a pixel is there.</para>
        /// </summary>
        /// <summary>
        /// Largest connected run of differing pixels that last-ULP arithmetic may produce.
        ///
        /// <para><b>MEASURED, not guessed:</b> across 3 hulls × 8 headings — 24 cells, including
        /// ones with 11 differing pixels — the largest connected cluster was <b>never above 1</b>.
        /// Noise is isolated pixels on facet and dither boundaries, by its nature. Meanwhile every
        /// sabotage in this fixture produces at least 2 (winding flip 2, one vertex moved a third of
        /// a pixel 2, moved 1.6 px 4, face dropped 46, dither phase shifted 864).</para>
        ///
        /// <para>⚠️ Deliberately set at the measured maximum, with no margin. A golden master's job
        /// is to be sensitive; the winding-flip case is only caught at 1 and is invisible at 2. If a
        /// future rig legitimately produces two ADJACENT noise pixels this goes red — which is the
        /// correct outcome, because the alternative is a check that cannot see a flipped facet. Re-
        /// measure before relaxing it, and if you do relax it, verify the sabotage cases still fail.</para>
        /// </summary>
        const int MaxNoiseCluster = 1;

        /// <summary>Whole-cell backstop, kept only so a catastrophic divergence fails loudly with a
        /// comparable-to-ADR-0022 number. ⚠️ It is NOT the real criterion — see
        /// <see cref="RigPixelDiff"/> for why a percentage cannot see a localised defect.</summary>
        const double MaxResidualPercent = 0.05;

        static IEnumerable<TestCaseData> HullCases() =>
            Hulls.Select(h => new TestCaseData(h.path, h.global).SetName($"Hull_{h.label}"));

        // ------------------------------------------------------------------ extraction basics

        [TestCaseSource(nameof(HullCases))]
        public void Extraction_PullsGeometryMaterialsAndLight(string path, string global)
        {
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, path, global);

            Assert.Greater(data.Faces.Count, 100, "A hull rig builds hundreds of facets.");
            Assert.Greater(data.Materials.Count, 2, "A hull rig has a material table.");
            Assert.Greater(data.W, 0);
            Assert.Greater(data.H, 0);
            Assert.Greater(data.PxPerMetre, 0);
            Assert.AreNotEqual(0.0, data.Gain, "GAIN is what turns shade into a ramp index.");

            double lnLen = Math.Sqrt(data.LightN.X * data.LightN.X +
                                     data.LightN.Y * data.LightN.Y +
                                     data.LightN.Z * data.LightN.Z);
            Assert.AreEqual(1.0, lnLen, 1e-9, "The rig normalises LN itself; extraction must not rescale it.");

            foreach (var f in data.Faces)
                Assert.GreaterOrEqual(f.V.Length, 3, "Every face must be fan-triangulable.");

            Debug.Log($"[rig-mesh] {data}");
        }

        [TestCaseSource(nameof(HullCases))]
        public void BuiltMesh_HasFlatPerFaceNormalsAndAttributes(string path, string global)
        {
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, path, global);
            var build = RigMeshBuilder.Build(data);
            try
            {
                Assert.AreEqual(data.VertexCount, build.Vertices);
                Assert.AreEqual(data.TriangleCount, build.Triangles);

                var norms = build.Mesh.normals;
                var uvs = new List<Vector4>();
                build.Mesh.GetUVs(RigMeshBuilder.AttrUvChannel, uvs);
                Assert.AreEqual(build.Vertices, norms.Length);
                Assert.AreEqual(build.Vertices, uvs.Count);

                // Flat: every vertex of a face carries that face's single normal and attrs.
                int v = 0;
                foreach (var f in data.Faces)
                {
                    Vector3 n0 = norms[v];
                    Vector4 a0 = uvs[v];
                    for (int k = 0; k < f.V.Length; k++, v++)
                    {
                        Assert.AreEqual(n0, norms[v], "Normals must be flat across a face.");
                        Assert.AreEqual(a0, uvs[v], "Face attrs must be flat across a face.");
                    }
                    Assert.AreEqual(f.Mat, Mathf.RoundToInt(a0.x), "Material id lost in the attr pack.");
                }

                Debug.Log($"[rig-mesh] {global}: {build}");
            }
            finally { UnityEngine.Object.DestroyImmediate(build.Mesh); }
        }

        // ------------------------------------------------------- the art director's source is ours to READ

        /// <summary>
        /// ⚠️ THE NON-NEGOTIABLE ONE. <c>docs/art/rigs/**</c> is the art director's source (ADR 0021
        /// §5). The shim widens an in-memory COPY; if it ever widened the file instead, the next
        /// hand-export would carry our hack and nobody would know where it came from. Hashes every
        /// rig in the folder, not just the ones extracted, because a stray write is exactly the kind
        /// of bug that lands on a file nobody was looking at.
        /// </summary>
        [Test]
        public void RigSourceFiles_AreByteIdentical_AfterExtraction()
        {
            string folder = Path.Combine(RigCatalog.RepoRoot, "docs/art/rigs");
            Assert.IsTrue(Directory.Exists(folder), $"Rig folder missing at {folder}.");

            var files = Directory.GetFiles(folder, "*.js", SearchOption.AllDirectories)
                                 .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Assert.Greater(files.Length, 30, "Expected the full imported rig set.");

            var before = files.ToDictionary(p => p, Sha256, StringComparer.Ordinal);
            var beforeStamps = files.ToDictionary(p => p, p => File.GetLastWriteTimeUtc(p), StringComparer.Ordinal);

            foreach (var (_, path, global) in Hulls)
            {
                using var host = RigScriptHostFactory.Create();
                var data = RigMeshExtractor.ExtractFrom(host, path, global);
                Assert.Greater(data.Faces.Count, 0);
            }

            foreach (string p in files)
            {
                Assert.AreEqual(before[p], Sha256(p),
                    $"⚠️ {Path.GetFileName(p)} CHANGED ON DISK during extraction. The shim must only " +
                    "ever widen an in-memory copy.");
                Assert.AreEqual(beforeStamps[p], File.GetLastWriteTimeUtc(p),
                    $"{Path.GetFileName(p)} was rewritten with identical content — still a write.");
            }
        }

        static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(fs));
        }

        // ------------------------------------------------------------------ the shim, in isolation

        [Test]
        public void Shim_InsertsOnlyTheMissingSymbols()
        {
            const string src = "(function(root){ const F=[]; root.Foo = { W, H, render }; })(globalThis);";

            string widened = RigMeshExtractor.WidenExportedLiteral(
                src, "Foo", new[] { "F", "MATS" }, "test");

            StringAssert.Contains("root.Foo = { F:F, MATS:MATS, W, H, render };", widened);
            Assert.AreEqual(src.Length + " F:F, MATS:MATS,".Length, widened.Length,
                "The shim must be a single insertion and nothing else.");
        }

        /// <summary>The end state this code is written to reach: nothing missing ⇒ nothing inserted
        /// ⇒ the source is handed to the engine untouched, byte for byte.</summary>
        [Test]
        public void Shim_IsDeadCode_WhenNothingIsMissing()
        {
            const string src = "(function(root){ root.Foo = { F, MATS, GAIN, BIAS, LN }; })(globalThis);";
            Assert.AreSame(src, RigMeshExtractor.WidenExportedLiteral(
                src, "Foo", Array.Empty<string>(), "test"));
        }

        [Test]
        public void Shim_RefusesToGuess_WhenTheAnchorIsNotUnique()
        {
            const string src = "root.Foo = { a }; root.Foo = { b };";
            var e = Assert.Throws<InvalidOperationException>(() =>
                RigMeshExtractor.WidenExportedLiteral(src, "Foo", new[] { "F" }, "test.js"));
            StringAssert.Contains("exactly one", e.Message);
            StringAssert.Contains("test.js", e.Message, "The failure must name the rig.");
        }

        /// <summary>
        /// Proves the EXPORTED path is real and is preferred — i.e. that the day the art director
        /// adds <c>F, MATS, GAIN, BIAS, LN</c> the shim stops running with no edit here. Uses a
        /// synthetic rig under Temp/ (gitignored) so the real sources stay untouched.
        /// </summary>
        [Test]
        public void ExportedSymbols_ArePreferred_AndTheShimNeverRuns()
        {
            string rel = "Temp/hh-rig-mesh-tests/exportingRig.js";
            string abs = Path.Combine(RigCatalog.RepoRoot, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            File.WriteAllText(abs, @"
(function (root) {
  const PX = 32, W = 8, H = 8, cx = 4, cy = 4;
  const RAMP = ['#101010','#808080','#f0f0f0'];
  const MATS = { hull:{ramp:RAMP,off:0} };
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (function(){ var v=[-0.42,0.72,0.52], m=Math.hypot(v[0],v[1],v[2]);
                          return [v[0]/m,v[1]/m,v[2]/m]; })();
  const F = [ {v:[[-1,-1,0],[1,-1,0],[1,1,0],[-1,1,0]], mat:'hull', b:0, db:0} ];
  function render(){ return new Uint8ClampedArray(W*H*4); }
  root.ExportingRig = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:40,
                        F, MATS, GAIN, BIAS, LN, KEY:'#0d1418', render };
})(globalThis);");

            try
            {
                using var host = RigScriptHostFactory.Create();
                var data = RigMeshExtractor.ExtractFrom(host, rel, "ExportingRig");

                CollectionAssert.IsEmpty(data.ShimmedSymbols,
                    "The rig exports everything, so the widening must not have run at all.");
                Assert.AreEqual(1, data.Faces.Count);
                Assert.AreEqual(4, data.Faces[0].V.Length);
                Assert.AreEqual(1, data.Materials.Count);
                Assert.AreEqual(2.7, data.Bias, 1e-12);
            }
            finally
            {
                try { Directory.Delete(Path.GetDirectoryName(abs), true); } catch { /* best effort */ }
            }
        }

        /// <summary>
        /// Records, as an executable statement of fact, which symbols the real rigs export today.
        /// When the art director lands them this test goes RED and the message says so — which is
        /// the right way to be told that the shim has become dead code.
        /// </summary>
        [Test]
        public void RealRigs_StillNeedTheShim_AsOfThisCommit()
        {
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, Hulls[0].path, Hulls[0].global);

            CollectionAssert.AreEquivalent(RigMeshSymbols.Required, data.ShimmedSymbols,
                "Expected all five of F, MATS, GAIN, BIAS, LN to still be closure-private. If this " +
                "failed because FEWER are shimmed, the art director has started exporting them — " +
                "delete the corresponding expectation, and when the list is empty delete " +
                "RigMeshExtractor.WidenExportedLiteral and this test with it. ⚠️ Note this is FIVE " +
                "symbols; ADR 0022 open question #4 says the delta is one property (`F,`) and that " +
                "is measurably wrong.");
        }

        // ------------------------------------------------------------------ the golden master

        [TestCaseSource(nameof(HullCases))]
        public void ExtractedFaces_ReproduceTheRigsOwnRender(string path, string global)
        {
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, path, global);

            double worst = 0;
            for (int dir = 0; dir < 8; dir++)
            {
                var view = new RigViewOptions(dir, data.DefaultElev);
                var basis = RigTrigBasis.FromScriptHost(host, view);
                byte[] truth = RenderTruth(host, global, view, data);
                byte[] mine = RigMeshReferenceRasterizer.RenderFromFaces(data, view, basis);
                var diff = RigMeshReferenceRasterizer.Compare(truth, mine, data.W, data.H);
                Debug.Log($"[rig-mesh] {global} dir {dir} (f64 faces): {diff}");
                worst = Math.Max(worst, diff.PercentDiffering);

                // See MaxResidualPixels for why this is not zero, and for what was ruled out
                // before settling on a tolerance.
                Assert.LessOrEqual(diff.LargestDifferingCluster, MaxNoiseCluster,
                    $"{global} dir {dir}: the extracted face list does not reproduce the rig's own " +
                    $"cell ({diff}). A CONNECTED patch of differing pixels is a face in the wrong " +
                    "place or missing outright; last-ULP arithmetic only ever produces scattered " +
                    "singletons.");
                Assert.Less(diff.PercentDiffering, MaxResidualPercent,
                    $"{global} dir {dir}: whole-cell divergence {diff}.");
                Assert.AreEqual(0, diff.CoverageOnlyDifferences,
                    $"{global} dir {dir}: the silhouette differs. Rounding moves a pixel's SHADE; " +
                    "it does not add or remove coverage. That is a geometry defect.");
            }
            Debug.Log($"[rig-mesh] {global}: worst f64-face difference {worst:F4}%");
        }

        [TestCaseSource(nameof(HullCases))]
        public void BuiltMesh_ReproducesTheRigsOwnRender_WithinFloat32Noise(string path, string global)
        {
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, path, global);
            var build = RigMeshBuilder.Build(data);
            try
            {
                double worst = 0;
                for (int dir = 0; dir < 8; dir++)
                {
                    var view = new RigViewOptions(dir, data.DefaultElev);
                    var basis = RigTrigBasis.FromScriptHost(host, view);
                    byte[] truth = RenderTruth(host, global, view, data);
                    byte[] mine = RigMeshReferenceRasterizer.RenderFromMesh(data, build.Mesh, view, basis);
                    var diff = RigMeshReferenceRasterizer.Compare(truth, mine, data.W, data.H);
                    Debug.Log($"[rig-mesh] {global} dir {dir} (f32 mesh): {diff}");
                    worst = Math.Max(worst, diff.PercentDiffering);
                    Assert.LessOrEqual(diff.LargestDifferingCluster, MaxNoiseCluster,
                        $"{global} dir {dir}: float32 vertex storage should only ever nudge isolated " +
                        $"boundary pixels, but produced a connected patch ({diff}).");
                }

                // A priori bar, ~90× tighter than the ADR's 1.3–4.4% shader residual. The only
                // error source between the two paths is float32 vertex storage.
                Assert.Less(worst, 0.05,
                    $"{global}: worst mesh-vs-rig difference {worst:F4}% of inked pixels. Anything " +
                    "at this scale is no longer float32 rounding at a facet edge.");
                Debug.Log($"[rig-mesh] {global}: worst f32-mesh difference {worst:F4}% " +
                          "(ADR 0022 quotes 1.3–4.4% for the GPU shader path)");
            }
            finally { UnityEngine.Object.DestroyImmediate(build.Mesh); }
        }

        /// <summary>Rock frames exercise roll/pitch/heave, and a fractional dir exercises the
        /// intermediate headings that are the entire point of a mesh hull.</summary>
        [Test]
        public void ExtractedFaces_ReproduceTheRig_UnderRockAndFractionalHeadings()
        {
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, Hulls[0].path, Hulls[0].global);

            var views = new[]
            {
                new RigViewOptions(0.25, data.DefaultElev),
                new RigViewOptions(3.5, data.DefaultElev),
                new RigViewOptions(1, data.DefaultElev, rollDegrees: 2.8, pitchDegrees: 1.6, heavePixels: 1.2),
                new RigViewOptions(5, data.DefaultElev, rollDegrees: -2.8, pitchDegrees: -1.6, heavePixels: -1.2),
                new RigViewOptions(2, 55),   // elevation is an opt too
            };

            foreach (var view in views)
            {
                var basis = RigTrigBasis.FromScriptHost(host, view);
                byte[] truth = RenderTruth(host, Hulls[0].global, view, data);
                var diff = RigMeshReferenceRasterizer.Compare(
                    truth, RigMeshReferenceRasterizer.RenderFromFaces(data, view, basis), data.W, data.H);
                Debug.Log($"[rig-mesh] lobster {view.ToJsArgs()}: {diff}");
                Assert.LessOrEqual(diff.LargestDifferingCluster, MaxNoiseCluster,
                    $"Rock/fractional-heading view {view.ToJsArgs()} diverged: {diff}. Continuous " +
                    "rocking is what ADR 0022 buys; it has to hold at arbitrary angles, not only at " +
                    "the eight baked ones.");
                Assert.AreEqual(0, diff.CoverageOnlyDifferences,
                    $"Silhouette differs at {view.ToJsArgs()} — that is geometry, not rounding.");
            }
        }

        /// <summary>Renders through the rig's own API and asserts it actually drew — the ICU/skipped-
        /// assembly trap makes "it returned something" worth checking every time.</summary>
        static byte[] RenderTruth(IRigScriptHost host, string global, RigViewOptions view, RigMeshData data)
        {
            byte[] rgba = host.EvaluateBytes($"{global}.render({view.ToJsArgs()})");
            Assert.AreEqual(data.W * data.H * 4, rgba.Length,
                "The rig's cell is not W×H — the geometry read or the readback is wrong.");
            int opaque = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 0) opaque++;
            Assert.Greater(opaque, 1000,
                $"{global}.render({view.ToJsArgs()}) produced {opaque} opaque px. The comparison " +
                "below would be 'empty vs empty' and would pass while proving nothing.");
            return rgba;
        }

        /// <summary>
        /// Quantifies the ONE confound the golden masters above deliberately remove, so that the
        /// removal is documented rather than convenient.
        ///
        /// <para>V8 and .NET do not agree to the last bit on <c>cos</c>/<c>sin</c> — V8 ships its own
        /// fdlibm port, .NET calls the platform CRT. Feed the rasteriser a .NET-computed basis and a
        /// handful of pixels sitting exactly on a facet or dither threshold flip. That is a property
        /// of two <c>Math.cos</c> implementations, not of the extracted mesh, and it is worth knowing
        /// because the GPU will have the SAME class of disagreement in phase 3 — very likely a large
        /// part of the 1.3–4.4% ADR 0022 measured there.</para>
        /// </summary>
        [TestCaseSource(nameof(HullCases))]
        public void DotNetTrig_VersusEngineTrig_CostsOnlyAHandfulOfPixels(string path, string global)
        {
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, path, global);

            double worst = 0;
            int worstPixels = 0;
            for (int dir = 0; dir < 8; dir++)
            {
                var view = new RigViewOptions(dir, data.DefaultElev);
                byte[] truth = RenderTruth(host, global, view, data);
                // No basis override ⇒ .NET trig.
                var diff = RigMeshReferenceRasterizer.Compare(
                    truth, RigMeshReferenceRasterizer.RenderFromFaces(data, view), data.W, data.H);
                if (diff.PercentDiffering > worst)
                { worst = diff.PercentDiffering; worstPixels = diff.DifferingPixels; }
            }

            Debug.Log($"[rig-mesh] {global}: .NET-trig basis costs at most {worstPixels} px " +
                      $"({worst:F4}% of inked). With the engine's own basis the same comparison is exact.");
            Assert.Less(worst, 0.05,
                $"{global}: {worst:F4}% is far more than last-ULP trig disagreement can explain — " +
                "something else is wrong.");
        }

        // ------------------------------------------------------------------ SABOTAGE

        /// <summary>
        /// ⚠️ A golden master nobody has seen fail is a decoration. These deliberately corrupt the
        /// extracted data in the four ways that matter — geometry, completeness, winding and dither
        /// phase — and assert the check goes RED. The measured deltas are logged so the alarm's
        /// sensitivity is on the record rather than assumed.
        ///
        /// <para>⚠️ The first draft of these sabotaged <c>Faces[Count/2]</c> and TWO OF THEM PASSED
        /// SILENTLY — that face is interior, the z-buffer discards it, and corrupting it genuinely
        /// changes nothing. The lesson generalises past this fixture: a sabotage test must first
        /// prove the thing it is about to break is observable. <see cref="FindVisibleFace"/> does
        /// that, and every case below goes through it.</para>
        /// </summary>
        [Test]
        public void Sabotage_PerturbedVertex_IsCaught()
        {
            using var host = RigScriptHostFactory.Create();
            var (data, view, basis, truth) = Fixture(host);
            int i = FindVisibleFace(data, view, basis);

            // ONE vertex of one VISIBLE face, moved by 1 cm — a THIRD OF A PIXEL at 32 px/m, and
            // well inside the whole-cell percentage noise (it shifts 0.0079% of the cell against a
            // 0.05% noise floor). Clustering is what makes a sub-pixel error detectable at all: the
            // three pixels it moves are contiguous, and arithmetic noise never is.
            var f = data.Faces[i];
            var v0 = f.V[0];
            f.V[0] = new Vector3d(v0.X + 0.01, v0.Y, v0.Z);

            AssertSabotageIsCaught(data, view, basis, truth,
                                   $"face #{i} vertex 0 moved +1 cm (a third of a pixel) in x");
        }

        [Test]
        public void Sabotage_DroppedFace_IsCaught()
        {
            using var host = RigScriptHostFactory.Create();
            var (data, view, basis, truth) = Fixture(host);
            int i = FindVisibleFace(data, view, basis);

            data.Faces.RemoveAt(i);

            AssertSabotageIsCaught(data, view, basis, truth, $"face #{i} dropped entirely");
        }

        [Test]
        public void Sabotage_FlippedFaceNormal_IsCaught()
        {
            using var host = RigScriptHostFactory.Create();
            var (data, view, basis, truth) = Fixture(host);
            int i = FindVisibleFace(data, view, basis);

            // Reversing the winding flips the normal — the shading defect a wrong-handed
            // extraction would produce everywhere.
            Array.Reverse(data.Faces[i].V);

            AssertSabotageIsCaught(data, view, basis, truth, $"face #{i} winding reversed");
        }

        [Test]
        public void Sabotage_WrongDitherPhase_IsCaught()
        {
            using var host = RigScriptHostFactory.Create();
            var (data, view, basis, truth) = Fixture(host);

            // The spike's (0,1) Bayer phase offset applied where it does NOT belong. ADR 0022
            // measured this class of defect as 13–16% dither crawl in MOTION; on a still it is far
            // subtler, which is exactly why it needs a machine to catch it rather than an eye.
            var shifted = new double[4, 4];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    shifted[x, y] = data.Bayer[x, (y + 1) & 3];
            data.Bayer = shifted;

            AssertSabotageIsCaught(data, view, basis, truth, "Bayer dither grid phase-shifted +1 in y");
        }

        // ------------------------------------------------------------------ sabotage plumbing

        static (RigMeshData data, RigViewOptions view, RigTrigBasis basis, byte[] truth) Fixture(
            IRigScriptHost host)
        {
            var data = RigMeshExtractor.ExtractFrom(host, Hulls[0].path, Hulls[0].global);
            var view = new RigViewOptions(0, data.DefaultElev);
            var basis = RigTrigBasis.FromScriptHost(host, view);
            byte[] truth = RenderTruth(host, Hulls[0].global, view, data);

            // The baseline must be clean, or "sabotage detected" would be indistinguishable from
            // "the golden master was already red".
            var clean = RigMeshReferenceRasterizer.Compare(
                truth, RigMeshReferenceRasterizer.RenderFromFaces(data, view, basis), data.W, data.H);
            Assert.LessOrEqual(clean.LargestDifferingCluster, MaxNoiseCluster,
                $"The UNSABOTAGED baseline already differs by more than last-ULP noise ({clean}). " +
                "Fix that before trusting any sabotage result below.");
            // Every sabotage below moves the count far past this, so the baseline is recorded and
            // the sabotage asserts against it rather than against zero.
            Assert.AreEqual(0, clean.CoverageOnlyDifferences, "Baseline silhouette already differs.");
            return (data, view, basis, truth);
        }

        /// <summary>
        /// Returns a face whose removal demonstrably changes the rendered cell — i.e. one that is
        /// actually visible from this heading. Corrupting an occluded interior face proves nothing:
        /// the z-buffer throws it away and the golden master rightly stays green.
        /// </summary>
        static int FindVisibleFace(RigMeshData data, RigViewOptions view, RigTrigBasis basis)
        {
            byte[] baseline = RigMeshReferenceRasterizer.RenderFromFaces(data, view, basis);
            for (int i = 0; i < data.Faces.Count; i++)
            {
                var removed = data.Faces[i];
                data.Faces.RemoveAt(i);
                bool visible = RigMeshReferenceRasterizer
                    .Compare(baseline, RigMeshReferenceRasterizer.RenderFromFaces(data, view, basis), data.W, data.H)
                    .DifferingPixels > 0;
                data.Faces.Insert(i, removed);
                if (visible) return i;
            }
            Assert.Fail("No single face changes the image — the rasteriser is drawing nothing.");
            return -1;
        }

        static void AssertSabotageIsCaught(RigMeshData data, RigViewOptions view, RigTrigBasis basis,
                                           byte[] truth, string what)
        {
            var diff = RigMeshReferenceRasterizer.Compare(
                truth, RigMeshReferenceRasterizer.RenderFromFaces(data, view, basis), data.W, data.H);
            Debug.Log($"[rig-mesh][SABOTAGE] {what}: {diff}");
            Assert.Greater(diff.LargestDifferingCluster, MaxNoiseCluster,
                $"SABOTAGE NOT DETECTED — {what} produced {diff}, which is within the arithmetic " +
                "noise this fixture tolerates. The golden master cannot see this class of defect, " +
                "and every green run above is worth less than it looks.");
        }
    }
}
