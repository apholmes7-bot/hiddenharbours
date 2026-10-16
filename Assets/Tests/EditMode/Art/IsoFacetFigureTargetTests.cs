using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The figure pixel look's hand-ins and shaders (route 1, <see cref="IsoFacetFigureTarget"/>, behind
    /// <see cref="GameConfig.FigurePixelTarget"/>), everything that needs no graphics device: a figure's
    /// facet material moves to the figure queue only while ON and comes back when OFF; the full-size list
    /// and the figure list split the whole queue range with nothing lost; the overlays' map is zero
    /// unless published; the target shader compiles with its four passes where the code expects them; the
    /// overlays take a texel only behind a non-zero map; the export's stencils keep her, and of what
    /// covers her only the texel beside her (the owner's D4 of 10-15, ruled down to that edge on 10-16),
    /// while never exporting a context pixel alone; and the edge's neighbourhood is the four texels that
    /// share an edge.
    /// </summary>
    public class IsoFacetFigureTargetTests
    {
        private const string FacetShaderName = "HiddenHarbours/IsoFacet";
        private const string TargetShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursFigurePixelTarget.shader";
        private const string OverlayShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacetOverlay.shader";

        private static object _sink;
        private static bool _live;

        private GameConfig _previous;
        private GameConfig _config;
        private Material _facet;

        [SetUp]
        public void SetUp()
        {
            _previous = GameServices.Config;
            _config = ScriptableObject.CreateInstance<GameConfig>();
            GameServices.Config = _config;
            Shader facet = Shader.Find(FacetShaderName);
            Assert.IsNotNull(facet, $"'{FacetShaderName}' is missing; a figure's facet material is made from it");
            _facet = new Material(facet) { hideFlags = HideFlags.HideAndDontSave };
            IsoFacetFigureTarget.ResetLive();
        }

        [TearDown]
        public void TearDown()
        {
            IsoFacetFigureTarget.ResetLive();
            IsoFacetFigureTarget.Unpublish();
            GameServices.Config = _previous;
            if (_facet != null) Object.DestroyImmediate(_facet);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        // ==================================================================== the queue hand-in

        [Test]
        public void Sync_MovesAFiguresFacetMaterial_ToTheFigureQueue_OnlyWhileOn()
        {
            int own = _facet.shader.renderQueue;
            Assert.AreEqual(own, _facet.renderQueue, "control: a fresh facet material sits at its shader's queue");
            Assert.AreNotEqual(IsoFacetFigureTarget.FigureRenderQueue, own, "control: the shader's queue is not the figure queue");

            _config.FigurePixelTarget = false;
            IsoFacetFigureTarget.Sync(_facet, true);
            Assert.AreEqual(own, _facet.renderQueue, "OFF: nothing moves");

            _config.FigurePixelTarget = true;
            IsoFacetFigureTarget.Sync(_facet, true);
            Assert.AreEqual(IsoFacetFigureTarget.FigureRenderQueue, _facet.renderQueue, "ON: the figure queue");
            IsoFacetFigureTarget.Sync(_facet, false);
            Assert.AreEqual(IsoFacetFigureTarget.FigureRenderQueue, _facet.renderQueue, "ON and hidden: still the figure queue");

            _config.FigurePixelTarget = false;
            IsoFacetFigureTarget.Sync(_facet, true);
            Assert.AreEqual(own, _facet.renderQueue, "OFF again: back to the shader's own queue");

            _facet.renderQueue = 3100;
            IsoFacetFigureTarget.Sync(_facet, true);
            Assert.AreEqual(3100, _facet.renderQueue, "OFF leaves any other explicit queue alone");

            _config.FigurePixelTarget = true;
            Assert.DoesNotThrow(() => IsoFacetFigureTarget.Sync(null, true), "a figure with no material yet is a no-op");
        }

        [Test]
        public void TheFigureAndSceneQueues_SplitTheWholeRange()
        {
            RenderQueueRange all = RenderQueueRange.all;
            Assert.AreEqual(all.lowerBound, IsoFacetFigureTarget.SceneQueue.lowerBound, "the scene list starts where every list does");
            Assert.AreEqual(IsoFacetFigureTarget.SceneQueue.upperBound + 1, IsoFacetFigureTarget.FigureQueue.lowerBound,
                            "no queue between the two lists, none in both");
            Assert.AreEqual(all.upperBound, IsoFacetFigureTarget.FigureQueue.upperBound, "the figure list ends where every list does");
            Assert.AreEqual(IsoFacetFigureTarget.FigureRenderQueue, IsoFacetFigureTarget.FigureQueue.lowerBound,
                            "the figure list is the figure queue alone");
            Assert.LessOrEqual(_facet.shader.renderQueue, IsoFacetFigureTarget.SceneQueue.upperBound,
                               "the facet shader's own queue is in the scene list, so OFF draws every facet full size");
        }

        [Test]
        public void TheFiguresAreLive_OnlyWhenAVisibleFigureSyncedWhileOn()
        {
            Assert.IsFalse(IsoFacetFigureTarget.FiguresLive, "reset: nobody has synced");

            _config.FigurePixelTarget = false;
            IsoFacetFigureTarget.Sync(_facet, true);
            Assert.IsFalse(IsoFacetFigureTarget.FiguresLive, "OFF: a visible figure does not light the passes");

            _config.FigurePixelTarget = true;
            IsoFacetFigureTarget.Sync(_facet, false);
            Assert.IsFalse(IsoFacetFigureTarget.FiguresLive, "ON: a hidden figure does not light the passes");

            IsoFacetFigureTarget.Sync(_facet, true);
            Assert.IsTrue(IsoFacetFigureTarget.FiguresLive, "ON: a visible figure lights them this frame");
        }

        // ==================================================================== the overlays' map

        [Test]
        public void ThePixelMap_IsZero_UnlessPublished()
        {
            var map = new Vector4(-3f, -4f, 1f / 3f, 3f);
            IsoFacetFigureTarget.Publish(map);
            Assert.AreEqual(map, Shader.GetGlobalVector(IsoFacetFigureTarget.PixelMapId), "published: the plan's map");

            IsoFacetFigureTarget.Unpublish();
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(IsoFacetFigureTarget.PixelMapId),
                            "unpublished: zero, the overlays' untouched path");
            IsoFacetFigureTarget.Unpublish();
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(IsoFacetFigureTarget.PixelMapId), "a second unpublish: still zero");
        }

        // ==================================================================== the shaders

        [Test]
        public void TheTargetShader_CompilesEveryPass_WhereTheCodeExpectsThem()
        {
            // The importer logs shader errors to the console; without this the runner would fail on
            // "Unhandled log message" instead of via the assertion below.
            LogAssert.ignoreFailingMessages = true;

            AssetDatabase.ImportAsset(TargetShaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(TargetShaderPath);
            Assert.IsNotNull(shader, $"could not load '{TargetShaderPath}'");
            Assert.AreEqual(IsoFacetFigureTarget.ShaderName, shader.name,
                            "the feature finds the shader by this name; a rename leaves the figures at screen size");

            var errors = new StringBuilder();
            Collect(errors, ShaderUtil.GetShaderMessages(shader));
            var material = new Material(shader);
            try
            {
                Assert.AreEqual(4, material.passCount, "four passes: the depth transfer, the export, the coverage and the edge");
                Assert.AreEqual(IsoFacetFigureTarget.DepthPass, material.FindPass("HHFigureTargetDepth"), "the depth pass's index");
                Assert.AreEqual(IsoFacetFigureTarget.ExportPass, material.FindPass("HHFigureTargetExport"), "the export pass's index");
                Assert.AreEqual(IsoFacetFigureTarget.CoveragePass, material.FindPass("HHFigureTargetCoverage"), "the coverage pass's index");
                Assert.AreEqual(IsoFacetFigureTarget.EdgePass, material.FindPass("HHFigureTargetEdge"), "the edge pass's index");
                for (int pass = 0; pass < material.passCount; pass++)
                    ShaderUtil.CompilePass(material, pass, true);
                Collect(errors, ShaderUtil.GetShaderMessages(shader));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
            Assert.IsEmpty(errors.ToString(), "the figure target shader reported a COMPILER ERROR:\n" + errors);
        }

        [Test]
        public void TheOverlays_TakeATexel_OnlyBehindANonZeroMap_AndBeforeTheirIdTest()
        {
            string source = File.ReadAllText(OverlayShaderPath);
            Assert.AreEqual(2, Regex.Matches(source, Regex.Escape("if (_HHFigurePixelMap.w < 0.5) return c;")).Count,
                            "both overlay passes return the pixel untouched while the map is zero");
            AssertBefore(source, "float4 c = HHFigureOver(", "clip(HHIsThisHull(c.a)", "the in-scene pass");
            AssertBefore(source, "float4 c = HHFigureOverReflect(", "clip(HHIsThisHullReflect(c.a)", "the reflection pass");
            Assert.IsFalse(Regex.IsMatch(source, @"_HHFigure(PixelMap|ScreenTex)\s*\("),
                           "the map and the texture are globals; a Properties entry would shadow them per material");
        }

        [Test]
        public void TheExportStencils_KeepHerAndTheEdgeBesideHer_NeverContextAlone()
        {
            string source = File.ReadAllText(TargetShaderPath);
            var tests = new Dictionary<string, StencilTest>();
            foreach (Match pass in Regex.Matches(source,
                         @"Name\s+""(\w+)""[^}]*?Stencil\s*\{\s*Ref\s+(\d+)\s+ReadMask\s+(\d+)\s+Comp\s+(\w+)\s*\}"))
                tests[pass.Groups[1].Value] = new StencilTest(int.Parse(pass.Groups[2].Value),
                                                              int.Parse(pass.Groups[3].Value), pass.Groups[4].Value);
            Assert.AreEqual(3, tests.Count, "three stencil-tested passes: the export, the coverage and the edge");
            Assert.IsTrue(tests.TryGetValue("HHFigureTargetExport", out StencilTest export), "the export pass's stencil block is not where it was");
            Assert.IsTrue(tests.TryGetValue("HHFigureTargetCoverage", out StencilTest coverage), "the coverage pass's stencil block is not where it was");
            Assert.IsTrue(tests.TryGetValue("HHFigureTargetEdge", out StencilTest edge), "the edge pass's stencil block is not where it was");

            int her = IsoFacetFigureTarget.FigureStencil, context = IsoFacetFigureTarget.ContextStencil;
            Assert.IsTrue(export.Passes(0), "export: 0, empty or her ink ring");
            Assert.IsTrue(export.Passes(her), "export: her");
            Assert.IsFalse(export.Passes(her | context), "export: her under the house is the edge pass's alone");
            Assert.IsFalse(export.Passes(context), "export: context alone NEVER, so no hull pixel away from her moves");

            Assert.IsFalse(coverage.Passes(0), "coverage: not on an empty texel or her ink ring");
            Assert.IsTrue(coverage.Passes(her), "coverage: where she shows");
            Assert.IsFalse(coverage.Passes(her | context), "coverage: not where the house covers her");
            Assert.IsFalse(coverage.Passes(context), "coverage: not on context alone");

            Assert.IsFalse(edge.Passes(0), "edge: not on an empty texel or her ink ring (the export has it)");
            Assert.IsFalse(edge.Passes(her), "edge: not where she shows (the export has it)");
            Assert.IsTrue(edge.Passes(her | context), "edge: her under the house, kept only beside her (D4; the ruling of 10-16)");
            Assert.IsFalse(edge.Passes(context), "edge: context alone NEVER");
        }

        [Test]
        public void TheEdge_KeepsACoveredTexel_OnlyWhenOneOfTheFourBesideItIsHers()
        {
            string source = File.ReadAllText(TargetShaderPath);
            Match beside = Regex.Match(source, @"static\s+const\s+int2\s+kBeside\[(\d+)\]\s*=\s*\{([^}]*)\}");
            Assert.IsTrue(beside.Success, "the edge's neighbourhood (kBeside) is not where it was");
            var offsets = new List<Vector2Int>();
            foreach (Match offset in Regex.Matches(beside.Groups[2].Value, @"int2\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)"))
                offsets.Add(new Vector2Int(int.Parse(offset.Groups[1].Value), int.Parse(offset.Groups[2].Value)));
            Assert.AreEqual(int.Parse(beside.Groups[1].Value), offsets.Count, "the array's bound is its offsets' count");
            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) }, offsets,
                "the 4-neighbourhood: the four texels that share an edge with it, no diagonal, none twice");

            Match loop = Regex.Match(source,
                @"for\s*\(\s*int\s+k\s*=\s*0\s*;\s*k\s*<\s*(\d+)\s*;\s*k\+\+\s*\)\s*\{\s*int2\s+q\s*=\s*t\s*\+\s*kBeside\[k\]");
            Assert.IsTrue(loop.Success, "the edge's loop over kBeside is not where it was");
            Assert.AreEqual(offsets.Count, int.Parse(loop.Groups[1].Value), "the loop visits every one of the four");
            Assert.AreEqual(1, Regex.Matches(source, Regex.Escape("if (!beside) discard;")).Count,
                            "a covered texel with nothing of hers beside it is not exported: the full-size house stays");
        }

        // ==================================================================== rule 7

        [Test]
        public void SyncingEveryFrame_AllocatesNothing()
        {
            _config.FigurePixelTarget = true;
            Frames(4);   // warm-up

            // The recorder must see an allocation here, or the negative below would pass on anything.
            Assert.That(() => { _sink = new object[64]; }, Is.AllocatingGCMemory(),
                        "harness: the GC.Alloc recorder saw no allocation at all");
            Assert.That(() => Frames(256), Is.Not.AllocatingGCMemory(), "a figure's per-frame hand-in allocated (rule 7)");
            Assert.AreEqual(IsoFacetFigureTarget.FigureRenderQueue, _facet.renderQueue, "the loop measured the ON path");
        }

        private void Frames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                IsoFacetFigureTarget.Sync(_facet, (i & 1) == 0);
                IsoFacetFigureTarget.Unpublish();
                _live = IsoFacetFigureTarget.FiguresLive;
            }
        }

        // ==================================================================== helpers

        // A pass's fixed-function stencil test: pass when (ref & mask) <comp> (stencil & mask).
        private readonly struct StencilTest
        {
            private readonly int _reference, _mask;
            private readonly string _comp;

            public StencilTest(int reference, int mask, string comp)
            {
                _reference = reference;
                _mask = mask;
                _comp = comp;
            }

            public bool Passes(int stencil)
            {
                int a = _reference & _mask, b = stencil & _mask;
                if (_comp == "Equal") return a == b;
                if (_comp == "NotEqual") return a != b;
                Assert.Fail($"a stencil comparison this test does not read: '{_comp}'");
                return false;
            }
        }

        private static void AssertBefore(string source, string first, string then, string what)
        {
            int a = source.IndexOf(first, System.StringComparison.Ordinal);
            int b = source.IndexOf(then, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(a, 0, $"{what}: '{first}' is missing");
            Assert.GreaterOrEqual(b, 0, $"{what}: '{then}' is missing");
            Assert.Less(a, b, $"{what}: the texel must replace the pixel BEFORE the id test keeps or drops it");
        }

        private static void Collect(StringBuilder errors, ShaderMessage[] messages)
        {
            foreach (ShaderMessage message in messages)
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                    errors.AppendLine($"[ERROR] {message.message} {message.messageDetails} platform={message.platform} line={message.line}");
        }
    }
}
