using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// THE KEYLINE GATE (ADR 0031 — the keyline retirement), proven HEADLESS. The 1 px outline is
    /// retired from the world-art style; the mesh fleet converts through one data dial
    /// (<see cref="GameConfig.HullKeylineFlood"/>, ship default OFF) that gates ONLY the resolve
    /// shader's flood rule — the depth-edge darkening that keeps overlapping parts of one boat
    /// readable always runs.
    ///
    /// <para><b>What can be proven without a GPU, and what cannot.</b> CI runs on the Null device,
    /// so no pixel here is real. What IS CPU-side — and what this class pins — is the whole decision
    /// chain: the style default in data, the <see cref="GameServices"/> resolution, the ONE apply
    /// method the production render func calls (<see cref="IsoFacetKeylineGate.Apply"/>) against a
    /// material built from the REAL resolve shader, and the shader source consuming the uniform to
    /// gate the flood and only the flood. The pixel truth (gate off removes exactly the outline,
    /// byte-identical everywhere else) lives in
    /// <c>IsoFacetUrpPassTests.KeylineGate_Off_RemovesTheFloodAndOnlyTheFlood</c>, GPU-gated like
    /// every rendering acceptance — it skips loudly on CI and bites on any dev machine.</para>
    ///
    /// <para><b>The sabotage arms are the point of the class</b> (the fish-school honesty pattern,
    /// PR #406): a gate check that cannot fail proves nothing, and this repo has shipped exactly
    /// that mistake — the tell was a guard whose self-check reported "SABOTAGE NOT DETECTED". So
    /// <see cref="AGateThatStopsGating_IsCaught"/> and <see cref="AShaderThatDropsTheGate_IsCaught"/>
    /// break the mechanism deliberately and require the checks to THROW, and each pin that rig 9's
    /// ink added to the source check (character PR 2a) has an arm of its own.</para>
    ///
    /// <para>⚠️ The source pin is textual on purpose — the shader-branch half of the gate has no
    /// other headless observable. A legitimate refactor of the resolve shader is expected to touch
    /// this test CONSCIOUSLY (that is what a guard is for); the strings pinned are the uniform
    /// declaration, the gate branch, the two rules' texture loads and the helpers that hold them
    /// (<c>HHSolidRgb</c>, and rig 9's ring <c>HHFigureKeyline</c> with its two tests), nothing
    /// cosmetic. Comments are stripped before any pin reads the source.</para>
    /// </summary>
    public class IsoFacetKeylineGateTests
    {
        const string ResolveShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacetResolve.shader";
        const string ResolveShaderName = "Hidden/HiddenHarbours/IsoFacetResolve";

        // What the source pin anchors on. Rig 9's ink (character PR 2a, ADR 0044 §9.5) moved rule 1 into
        // HHSolidRgb and added a second keyline read, its ring, in HHFigureKeyline. Both helpers sit
        // ABOVE frag, so the first load in the file no longer says which side of frag's gate a rule
        // runs on: the pin reads frag's own body, and names each helper.
        const string FragSignature = "float4 frag (";
        const string SolidSignature = "float3 HHSolidRgb(";
        const string Ring = "HHFigureKeyline(";
        const string RingSignature = "bool " + Ring;
        const string GateBranch = "if (_HHKeylineFlood < 0.5)";
        const string InkIsLive = "if (_HHFigureInk.z > 0.5 && ";
        const string RingSkipsAHull = "if (_HHDarkTex.Load(int3(q, 0)).a >= 0.5) continue;";

        // The arms' anchors: the flood's own read and frag's solid path, as frag writes them.
        const string FloodSearch = "int2 q0 = p;";
        const string FloodRead = "return float4(_HHKeyTex.Load(int3(q0, 0)).rgb, a0);";
        const string SolidTest = "if (c.a > 0)";
        const string SolidPath = "return float4(HHSolidRgb(p, c, w, h), c.a);";

        static readonly char[] StatementEnds = { ';', '{', '}' };

        private GameConfig _prevConfig;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void StartUnwired()
        {
            _prevConfig = GameServices.Config;
            GameServices.Config = null;
        }

        [TearDown]
        public void RestoreTheWiredConfig()
        {
            GameServices.Config = _prevConfig;
            foreach (Object o in _cleanup)
                if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        // ---- the style decision, as data ---------------------------------------------------------

        [Test]
        public void TheShipDefault_IsOff_TheKeylineIsRetired()
        {
            Assert.IsFalse(GameConfig.DefaultHullKeylineFlood,
                "ADR 0031: the retired outline IS the ship default. Flipping this const flips the " +
                "whole mesh fleet's style — that is an owner decision, not a tidy-up.");

            var fresh = ScriptableObject.CreateInstance<GameConfig>();
            _cleanup.Add(fresh);
            Assert.IsFalse(fresh.HullKeylineFlood,
                "a fresh GameConfig must carry the retired default — GameConfig.asset lags the " +
                "code, so the CODE default is what actually ships");

            Assert.IsFalse(GameServices.HullKeylineFlood,
                "unwired (EditMode, a bare art scene, a test rig) must resolve to the SAME retired " +
                "style the shipped game has — never the legacy outline");
        }

        [Test]
        public void TheOwnerDial_ResolvesThroughGameServices()
        {
            GameServices.Config = NewConfig(flood: true);
            Assert.IsTrue(GameServices.HullKeylineFlood,
                "the owner's ON (the byte-identical legacy A/B) must reach the feature");

            GameServices.Config = NewConfig(flood: false);
            Assert.IsFalse(GameServices.HullKeylineFlood,
                "the owner's OFF must reach the feature");
        }

        // ---- the CPU write: the REAL apply, on the REAL shader -----------------------------------

        [Test]
        public void Apply_WritesTheGate_OntoTheRealResolveMaterial()
        {
            Material material = NewResolveMaterial();

            Assert.IsTrue(material.HasProperty(IsoFacetShaderIds.KeylineFlood),
                "the production resolve shader must DECLARE _HHKeylineFlood — otherwise the " +
                "feature's per-frame write lands on a uniform that does not exist and the gate is " +
                "theatre");
            Assert.AreEqual(0f, material.GetFloat(IsoFacetShaderIds.KeylineFlood), 0f,
                "a material the feature never touched must default to 0 — a bypassed write fails " +
                "to the SHIPPED (outline-free) style, never back to outlines");

            IsoFacetKeylineGate.Apply(material, floodEnabled: true);
            AssertMaterialHonoursTheGate(material, floodEnabled: true);

            IsoFacetKeylineGate.Apply(material, floodEnabled: false);
            AssertMaterialHonoursTheGate(material, floodEnabled: false);
        }

        [Test]
        public void AGateThatStopsGating_IsCaught()
        {
            Material material = NewResolveMaterial();

            // The saboteur: a feature refactor stops writing the uniform from the dial (or writes
            // it from somewhere else), leaving the material saying ON while the style says OFF.
            material.SetFloat(IsoFacetShaderIds.KeylineFlood, 1f);

            Assert.Throws<AssertionException>(
                () => AssertMaterialHonoursTheGate(material, floodEnabled: false),
                "SABOTAGE NOT DETECTED — the material check no longer notices a gate value that " +
                "contradicts the style dial, and every green run above is worth less than it looks.");
        }

        // ---- the shader consumes it: the mechanism, not the symptom ------------------------------

        [Test]
        public void TheResolveShaderSource_GatesTheFlood_AndOnlyTheFlood()
        {
            AssertSourceGatesTheFlood(LoadResolveShaderSource());
        }

        [Test]
        public void AShaderThatDropsTheGate_IsCaught()
        {
            // The saboteur: "tidying away" the gate — the uniform and its branch vanish from the
            // HLSL while the C# side keeps writing a float the shader no longer reads. That would
            // ship a keyline that cannot be turned off (or, worse, one that cannot be turned ON
            // for the oracle fixtures) with every headless check still green.
            string sabotaged = LoadResolveShaderSource().Replace("_HHKeylineFlood", "_HHRemovedGate");

            Assert.Throws<AssertionException>(
                () => AssertSourceGatesTheFlood(sabotaged),
                "SABOTAGE NOT DETECTED — the source pin no longer notices the resolve shader " +
                "dropping the gate.");
        }

        // ---- rig 9's ink put reads around the gate: an arm for each pin (ADR 0044 §9.5) ----------

        [Test]
        public void AShaderThatFloodsAboveTheGate_IsCaught()
        {
            // The saboteur: the flood, its neighbour search and its own keyline read, hoisted above
            // the gate branch, so the outline draws whichever way the owner's dial sits. Rig 9's ring
            // reads the keyline above the gate too, and the pin must tell the two apart.
            string sabotaged = Move(LoadResolveShaderSource(), FloodSearch, FloodRead, GateBranch);

            Assert.Throws<AssertionException>(
                () => AssertSourceGatesTheFlood(sabotaged),
                "SABOTAGE NOT DETECTED — the source pin no longer notices the flood reading its " +
                "keyline colour above the gate.");
        }

        [Test]
        public void AShaderThatDarkensBehindTheGate_IsCaught()
        {
            // The saboteur: frag's solid path (rule 1's darkening, through HHSolidRgb) moved below the
            // gate branch. HHSolidRgb itself still sits above frag, so only frag's own call can tell.
            string sabotaged = Move(LoadResolveShaderSource(), SolidTest, SolidPath, FloodSearch);

            Assert.Throws<AssertionException>(
                () => AssertSourceGatesTheFlood(sabotaged),
                "SABOTAGE NOT DETECTED — the source pin no longer notices rule 1 moving behind the " +
                "gate.");
        }

        [Test]
        public void AShaderThatAsksForTheRingWithNoFigureInked_IsCaught()
        {
            // The saboteur: the live-ink test dropped from in front of rig 9's ring, so its keyline
            // read runs on a frame with no figure inked, the frame that must be the gated program alone.
            string sabotaged = Sabotage(LoadResolveShaderSource(), InkIsLive + Ring, "if (" + Ring);

            Assert.Throws<AssertionException>(
                () => AssertSourceGatesTheFlood(sabotaged),
                "SABOTAGE NOT DETECTED — the source pin no longer notices rig 9's ring asked for " +
                "without the live-ink test.");
        }

        [Test]
        public void AShaderWhoseRingTakesAHull_IsCaught()
        {
            // The saboteur: the ring stops skipping a hull's pixel, so its keyline read outlines a HULL
            // with the gate off, on every frame a figure is inked.
            string sabotaged = Sabotage(LoadResolveShaderSource(), RingSkipsAHull, string.Empty);

            Assert.Throws<AssertionException>(
                () => AssertSourceGatesTheFlood(sabotaged),
                "SABOTAGE NOT DETECTED — the source pin no longer notices rig 9's ring taking a " +
                "hull's pixel.");
        }

        // ---- the predicates (every arm runs through these; their correctness is the test) --------

        static void AssertMaterialHonoursTheGate(Material material, bool floodEnabled)
        {
            Assert.AreEqual(floodEnabled ? 1f : 0f,
                material.GetFloat(IsoFacetShaderIds.KeylineFlood), 0f,
                "the resolve material's _HHKeylineFlood must be exactly the gate's value — the " +
                "flood is either verbatim-ON (the rig look the oracles pin) or retired-OFF, never " +
                "a blend and never its own opinion");
        }

        /// <summary>The shader-branch half of the gate, pinned in source with its comments stripped:
        /// <list type="bullet">
        ///   <item>the uniform is declared, and <c>frag</c> branches on it;</item>
        ///   <item>the depth-edge darkening (rule 1, the <c>_HHDarkTex</c> load in <c>HHSolidRgb</c>)
        ///   sits in FRONT of the branch: <c>frag</c> draws a solid pixel through <c>HHSolidRgb</c>
        ///   before it. Gated darkening would break every overlapping-parts read on every boat, which
        ///   is precisely what ADR 0031 promises never happens;</item>
        ///   <item>the flood (rule 2, the <c>_HHKeyTex</c> load) sits BEHIND it: every keyline read in
        ///   <c>frag</c> comes after the branch;</item>
        ///   <item>and the one keyline read anywhere else is rig 9's own ring (ADR 0044 §9.5), which
        ///   cannot outline a hull: <c>HHFigureKeyline</c> skips a hull's pixel, and <c>frag</c> asks
        ///   for it only behind <c>_HHFigureInk.z &gt; 0.5</c>, so a frame with no figure inked runs
        ///   the gated program alone.</item>
        /// </list></summary>
        static void AssertSourceGatesTheFlood(string source)
        {
            string code = WithoutComments(source);

            int uniform = code.IndexOf("float _HHKeylineFlood", StringComparison.Ordinal);
            Assert.GreaterOrEqual(uniform, 0,
                "the resolve shader no longer declares the _HHKeylineFlood uniform — the gate has " +
                "no shader half");

            (int Start, int End) frag = BodyOf(code, FragSignature);
            int gate = IndexIn(code, frag, GateBranch);
            Assert.GreaterOrEqual(gate, 0,
                "the resolve shader no longer branches on the gate — the flood would run (or not) " +
                "regardless of the owner's dial");

            Assert.GreaterOrEqual(IndexIn(code, BodyOf(code, SolidSignature), "_HHDarkTex.Load"), 0,
                "rule 1's darkened-colour load is gone from HHSolidRgb?");
            int darken = IndexIn(code, frag, "HHSolidRgb(");
            Assert.GreaterOrEqual(darken, 0,
                "frag no longer draws a solid pixel through HHSolidRgb — rule 1 is gone from the resolve?");
            Assert.Less(darken, gate,
                "rule 1 (depth-edge darkening) reads AFTER the gate branch — the interior rule " +
                "must never be behind the keyline gate (ADR 0031: darkening stays, always)");

            (int Start, int End) ring = BodyOf(code, RingSignature);
            int floods = 0;
            foreach (int load in IndicesOf(code, "_HHKeyTex.Load"))
            {
                if (Inside(frag, load))
                {
                    Assert.Greater(load, gate,
                        "rule 2 (the 1 px flood) reads BEFORE the gate branch — the outline would draw " +
                        "with the gate off");
                    floods++;
                }
                else
                {
                    Assert.IsTrue(Inside(ring, load),
                        "a keyline-colour load outside frag and outside rig 9's ring (HHFigureKeyline) — " +
                        "nothing pins when it runs, so the outline could draw with the gate off");
                }
            }
            Assert.Greater(floods, 0, "rule 2's keyline-colour load is gone from the resolve?");

            Assert.GreaterOrEqual(IndexIn(code, ring, RingSkipsAHull), 0,
                "rig 9's ring no longer skips a hull's pixel — its keyline load would outline a HULL " +
                "with the gate off, on every frame a figure is inked");

            int definition = code.IndexOf(RingSignature, StringComparison.Ordinal)
                             + RingSignature.Length - Ring.Length;
            foreach (int ask in IndicesOf(code, Ring))
            {
                if (ask == definition) continue;
                int test = code.LastIndexOf("if (", ask, StringComparison.Ordinal);
                string condition = test < 0 ? string.Empty : code.Substring(test, ask - test);
                Assert.IsTrue(Inside(frag, ask)
                              && condition.StartsWith(InkIsLive, StringComparison.Ordinal)
                              && condition.IndexOfAny(StatementEnds) < 0
                              && !condition.Contains("||"),
                    "rig 9's ring is asked for without '_HHFigureInk.z > 0.5 &&' leading its test in " +
                    "frag — its keyline load would run on a frame with no figure inked: " +
                    $"'{condition.Trim()}…'");
            }
        }

        /// <summary>The source with its <c>//</c> and <c>/* */</c> comments dropped and its strings
        /// kept whole: the pins read code, and a comment that names a load or holds a brace moves
        /// nothing.</summary>
        static string WithoutComments(string source)
        {
            var code = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';
                if (c == '"')
                {
                    int close = source.IndexOf('"', i + 1);
                    int end = close < 0 ? source.Length : close + 1;
                    code.Append(source, i, end - i);
                    i = end - 1;
                }
                else if (c == '/' && next == '/')
                {
                    int eol = source.IndexOf('\n', i);
                    i = (eol < 0 ? source.Length : eol) - 1;
                }
                else if (c == '/' && next == '*')
                {
                    int close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = (close < 0 ? source.Length : close + 2) - 1;
                    code.Append(' ');
                }
                else
                {
                    code.Append(c);
                }
            }
            return code.ToString();
        }

        /// <summary>Where one function's body runs, from its opening brace to the brace that closes it.
        /// Its signature must occur once: with two, the pins could read the wrong one.</summary>
        static (int Start, int End) BodyOf(string code, string signature)
        {
            int at = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0,
                $"the resolve shader no longer defines '{signature}…' — the source pin reads its body");
            Assert.AreEqual(1, Occurrences(code, signature),
                $"the resolve shader defines '{signature}…' more than once?");
            int open = code.IndexOf('{', at);
            Assert.GreaterOrEqual(open, 0, $"'{signature}…' has no body?");
            for (int i = open, depth = 0; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) return (open, i + 1);
            }
            Assert.Fail($"'{signature}…' never closes its body");
            return (open, code.Length);
        }

        static bool Inside((int Start, int End) body, int at) => at > body.Start && at < body.End;

        static int IndexIn(string code, (int Start, int End) body, string what) =>
            code.IndexOf(what, body.Start, body.End - body.Start, StringComparison.Ordinal);

        static IEnumerable<int> IndicesOf(string text, string what)
        {
            for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0;
                 at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
                yield return at;
        }

        static int Occurrences(string text, string what)
        {
            int n = 0;
            foreach (int unused in IndicesOf(text, what)) n++;
            return n;
        }

        // ---- the saboteurs' tools (each anchor must match once, or the arm is not the arm) ---------

        /// <summary>The source with <paramref name="find"/> replaced. A saboteur that matched nothing
        /// would hand back the real source, and its arm would go red for the wrong reason.</summary>
        static string Sabotage(string source, string find, string replace)
        {
            AssertOnce(source, find);
            return source.Replace(find, replace);
        }

        /// <summary>The source with the stretch from <paramref name="from"/> through
        /// <paramref name="through"/> cut out and put back in front of <paramref name="before"/>.</summary>
        static string Move(string source, string from, string through, string before)
        {
            AssertOnce(source, from);
            AssertOnce(source, through);
            AssertOnce(source, before);
            int start = source.IndexOf(from, StringComparison.Ordinal);
            int end = source.IndexOf(through, StringComparison.Ordinal) + through.Length;
            Assert.Greater(end, start, $"'{through}' no longer follows '{from}' in the resolve shader");
            string moved = source.Substring(start, end - start);
            string rest = source.Remove(start, end - start);
            return rest.Insert(rest.IndexOf(before, StringComparison.Ordinal), moved + "\n");
        }

        static void AssertOnce(string source, string anchor)
        {
            Assert.AreEqual(1, Occurrences(source, anchor),
                $"the saboteur's anchor '{anchor}' no longer occurs exactly once — the resolve shader " +
                "changed, and the arm must move with it");
        }

        // ---- plumbing ----------------------------------------------------------------------------

        GameConfig NewConfig(bool flood)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.HullKeylineFlood = flood;
            _cleanup.Add(config);
            return config;
        }

        Material NewResolveMaterial()
        {
            var material = new Material(LoadResolveShader());
            _cleanup.Add(material);
            return material;
        }

        static Shader LoadResolveShader()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ResolveShaderPath);
            Assert.IsNotNull(shader,
                $"no shader at {ResolveShaderPath} — the resolve moved? The gate suite (and the " +
                "compile guard) must move with it.");
            Assert.AreEqual(ResolveShaderName, shader.name,
                $"'{ResolveShaderPath}' no longer declares '{ResolveShaderName}' — Shader.Find in " +
                "IsoFacetHullFeature would silently stop finding it");
            return shader;
        }

        static string LoadResolveShaderSource()
        {
            LoadResolveShader();   // path + name pinned before the raw read
            return File.ReadAllText(ResolveShaderPath);
        }
    }
}
