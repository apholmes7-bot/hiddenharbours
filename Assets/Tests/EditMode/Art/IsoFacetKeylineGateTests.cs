using System;
using System.Collections.Generic;
using System.IO;
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
    /// break the mechanism deliberately and require the checks to THROW.</para>
    ///
    /// <para>⚠️ The source pin is textual on purpose — the shader-branch half of the gate has no
    /// other headless observable. A legitimate refactor of the resolve shader is expected to touch
    /// this test CONSCIOUSLY (that is what a guard is for); the strings pinned are the uniform
    /// declaration, the gate branch, and the two rules' texture loads, nothing cosmetic.</para>
    /// </summary>
    public class IsoFacetKeylineGateTests
    {
        const string ResolveShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacetResolve.shader";
        const string ResolveShaderName = "Hidden/HiddenHarbours/IsoFacetResolve";

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

        // ---- the predicates (both arms run through these; their correctness is the test) ---------

        static void AssertMaterialHonoursTheGate(Material material, bool floodEnabled)
        {
            Assert.AreEqual(floodEnabled ? 1f : 0f,
                material.GetFloat(IsoFacetShaderIds.KeylineFlood), 0f,
                "the resolve material's _HHKeylineFlood must be exactly the gate's value — the " +
                "flood is either verbatim-ON (the rig look the oracles pin) or retired-OFF, never " +
                "a blend and never its own opinion");
        }

        /// <summary>The shader-branch half of the gate, pinned in source: the uniform is declared,
        /// the flood (rule 2, the <c>_HHKeyTex</c> load) sits BEHIND the gate branch, and the
        /// depth-edge darkening (rule 1, the <c>_HHDarkTex</c> load) sits in FRONT of it — gated
        /// darkening would break every overlapping-parts read on every boat, which is precisely
        /// what ADR 0031 promises never happens.</summary>
        static void AssertSourceGatesTheFlood(string source)
        {
            int uniform = source.IndexOf("float _HHKeylineFlood", StringComparison.Ordinal);
            Assert.GreaterOrEqual(uniform, 0,
                "the resolve shader no longer declares the _HHKeylineFlood uniform — the gate has " +
                "no shader half");

            int gate = source.IndexOf("if (_HHKeylineFlood < 0.5)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(gate, 0,
                "the resolve shader no longer branches on the gate — the flood would run (or not) " +
                "regardless of the owner's dial");

            int darken = source.IndexOf("_HHDarkTex.Load", StringComparison.Ordinal);
            Assert.GreaterOrEqual(darken, 0, "rule 1's darkened-colour load is gone from the resolve?");
            Assert.Less(darken, gate,
                "rule 1 (depth-edge darkening) reads AFTER the gate branch — the interior rule " +
                "must never be behind the keyline gate (ADR 0031: darkening stays, always)");

            int flood = source.IndexOf("_HHKeyTex.Load", StringComparison.Ordinal);
            Assert.GreaterOrEqual(flood, 0, "rule 2's keyline-colour load is gone from the resolve?");
            Assert.Greater(flood, gate,
                "rule 2 (the 1 px flood) reads BEFORE the gate branch — the outline would draw " +
                "with the gate off");
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
