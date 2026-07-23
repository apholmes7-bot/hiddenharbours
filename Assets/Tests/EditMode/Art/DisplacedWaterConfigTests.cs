using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// ADR 0023 phase 2 step 3 — the GameConfig exposure of the displaced-water tunables, pinned
    /// three ways so config, shader and twin can never disagree silently:
    ///
    /// <list type="number">
    /// <item><b>The lockstep pins.</b> <see cref="DisplacedWaterSettings.Default"/> must equal the
    /// Core/Art twin constants (<see cref="ShoreFadeMath.RecommendedBandCoefficient"/>,
    /// <see cref="WhitecapSalienceMath.DefaultEnvelopeThreshold"/>, the ADR's ×1.5) AND the shader's
    /// scraped property defaults — the third side of the <c>WhitecapSalienceMathTests</c> scrape
    /// (which keeps pinning shader == twin). Wiring the config into a scene therefore changes
    /// NOTHING until the owner actually tunes it.</item>
    /// <item><b>The resolution seam.</b> <c>DisplacedWaterSurface</c> resolves its effective
    /// exaggeration/coefficient from the wired config (live, per tick) and falls back to its
    /// serialized fields unwired; <c>WaterSurface</c> pushes the three owner salience knobs onto the
    /// flat renderer's MaterialPropertyBlock — the exact block the displaced pass copies, so one
    /// push covers both passes.</item>
    /// <item><b>The shipped-asset guard-rail.</b> The owner's tuned GameConfig.asset must stay at
    /// or above the analytically tear-safe band coefficient (the RodFight guard-rail pattern:
    /// bounds on the shipped asset, exact pins only on the code defaults — his tuning is his).</item>
    /// </list>
    /// </summary>
    public class DisplacedWaterConfigTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private static string ShaderSource()
        {
            string path = Path.Combine(Application.dataPath,
                "_Project/Art/Shaders/HiddenHarboursWater.shader");
            Assert.IsTrue(File.Exists(path), "HiddenHarboursWater.shader not found at " + path);
            return File.ReadAllText(path);
        }

        // ---- (1) the three-way lockstep --------------------------------------------------------

        [Test]
        public void ConfigDefaults_MatchTheCoreAndTwinConstants()
        {
            var d = DisplacedWaterSettings.Default;
            Assert.AreEqual(1.5f, d.WaveExaggeration,
                "ADR 0023 §(2): ×1.5 is THE default readability exaggeration (sweet spot 1.5–2, " +
                "shear-free at the coast) — the config default must cite it exactly.");
            Assert.AreEqual(ShoreFadeMath.RecommendedBandCoefficient, d.ShoreBandCoefficient,
                "The config's band coefficient must default to Core's proven tear-safe constant — " +
                "never a drifting copy.");
            Assert.AreEqual(1f, d.CapSalienceStrength,
                "The salience master defaults to the FULL retune (ADR 0023 folds it into the arc; " +
                "0 is the legacy-even-foam escape hatch, not the default).");
            Assert.AreEqual(WhitecapSalienceMath.DefaultEnvelopeThreshold, d.CapEnvelopeThreshold,
                "The config's envelope threshold must equal the spike-tuned twin constant " +
                "(WhitecapSalienceMath.DefaultEnvelopeThreshold) — config and twin in lockstep.");
        }

        [Test]
        public void ConfigDefaults_MatchTheShaderPropertyDefaults()
        {
            // The shader's Properties-block defaults are what an UNWIRED scene renders with; the
            // config defaults are what a WIRED scene pushes. They must be byte-equal, so wiring the
            // config is a visual no-op until the owner tunes it. (WhitecapSalienceMathTests already
            // pins shader == twin for the style constants; this closes the triangle for the four
            // owner-facing knobs.)
            string src = ShaderSource();
            var d = DisplacedWaterSettings.Default;
            AssertShaderDefault(src, "_WaveExaggeration", d.WaveExaggeration);
            AssertShaderDefault(src, "_CapSalienceStrength", d.CapSalienceStrength);
            AssertShaderDefault(src, "_CapEnvelopeThreshold", d.CapEnvelopeThreshold);
            AssertShaderDefault(src, "_EnvelopeBandStrength", d.EnvelopeBandStrength);
        }

        [Test]
        public void SharedExaggerationAccessor_ReadsTheLiveStruct_NotACopy()
        {
            // GameConfig.WaveExaggeration is THE accessor phase 3's hull-heave consumers call
            // (ADR 0023 §(2): one shared constant, a parameter not a copy). It must track a live
            // edit of the struct — the owner tuning in Play reaches every consumer.
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(1.5f, config.WaveExaggeration, "a fresh config reads the default");
                config.DisplacedWater.WaveExaggeration = 2.25f;
                Assert.AreEqual(2.25f, config.WaveExaggeration,
                    "the accessor must read the LIVE struct value — a cached/copied value would " +
                    "desync hull heave from the sea mid-tune (the overlay-pose lesson).");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ShippedConfigAsset_StaysAtOrAboveTheTearSafeBandCoefficient()
        {
            // The guard-rail, not an exact pin: the owner may tune the shipped asset freely, but the
            // band coefficient has an ANALYTIC floor — 1.5 is exactly marginal against the worst
            // in-band fold (ADR 0023 §The shore seam), so anything below it risks a visibly torn
            // coast. Fail loudly before a tuning session ships a tear.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config, "the shared GameConfig asset must exist at " + ConfigAssetPath);
            Assert.GreaterOrEqual(config.DisplacedWater.ShoreBandCoefficient, 1.5f,
                "GameConfig.DisplacedWater.ShoreBandCoefficient is below 1.5 — the analytic " +
                "tear-safety floor (ShoreFadeMath: 1.125 overlap, 1.5 fold-marginal). The coast can " +
                "tear at a crest. Keep it at 2 (the proven default) or above.");
        }

        // ---- (2) DisplacedWaterSurface resolves the config -------------------------------------

        [Test]
        public void DisplacedWaterSurface_ResolvesConfigValues_AndFallsBackUnwired()
        {
            var go = new GameObject("DisplacedWaterConfigTests.Surface", typeof(MeshRenderer));
            GameConfig config = null;
            try
            {
                var surface = go.AddComponent<DisplacedWaterSurface>();

                // UNWIRED: the serialized fallbacks (which mirror the defaults) apply.
                Assert.AreEqual(1.5f, surface.Exaggeration, "unwired fallback = the ADR default");
                Assert.AreEqual(ShoreFadeMath.RecommendedBandCoefficient, surface.BandCoefficient,
                    "unwired fallback = the Core constant");

                // WIRED: the config is the live source — the exact values SyncUniforms pushes
                // (it reads these same properties each tick).
                config = ScriptableObject.CreateInstance<GameConfig>();
                config.DisplacedWater = new DisplacedWaterSettings
                {
                    WaveExaggeration = 1.75f,
                    ShoreBandCoefficient = 2.5f,
                    CapSalienceStrength = 1f,
                    CapEnvelopeThreshold = 0.62f,
                    EnvelopeBandStrength = 0.35f,
                };
                SetPrivateField(surface, "_config", config);
                Assert.AreEqual(1.75f, surface.Exaggeration,
                    "with a config wired, GameConfig.DisplacedWater.WaveExaggeration is the live source");
                Assert.AreEqual(2.5f, surface.BandCoefficient,
                    "with a config wired, GameConfig.DisplacedWater.ShoreBandCoefficient is the live source");

                // LIVE: a later edit of the config (the owner tuning in Play) is picked up on the
                // very next read — no re-wire, no restart.
                config.DisplacedWater.WaveExaggeration = 2f;
                Assert.AreEqual(2f, surface.Exaggeration,
                    "config edits must reach the surface within a tick — the reads are live, not cached");
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (config != null) Object.DestroyImmediate(config);
            }
        }

        // ---- (3) WaterSurface pushes the salience knobs on the flat block ----------------------

        [Test]
        public void WaterSurface_PushesTheSalienceKnobs_OntoTheFlatRenderersBlock()
        {
            // The delivery seam for BOTH passes: WaterSurface pushes the three owner knobs onto the
            // flat renderer's MaterialPropertyBlock, and the displaced pass COPIES that block each
            // tick — so proving the flat push proves the displaced side's source too.
            GameServices.Reset();   // force the edit-mode path (no sim), like a fresh EditMode scene
            var go = new GameObject("DisplacedWaterConfigTests.Water", typeof(MeshRenderer));
            GameConfig config = null;
            try
            {
                var surface = go.AddComponent<WaterSurface>();   // [ExecuteAlways] — Awake/OnEnable ran
                var renderer = go.GetComponent<Renderer>();
                var read = new MaterialPropertyBlock();

                // UNWIRED: the push must NOT touch the salience keys (the material/shader defaults
                // stay authoritative; an unset float reads 0 from the block).
                InvokePushUniforms(surface);
                renderer.GetPropertyBlock(read);
                Assert.AreEqual(0f, read.GetFloat("_CapSalienceStrength"),
                    "with no config wired the salience keys must stay OFF the block — the material " +
                    "defaults rule (wiring is opt-in, never a stealth override).");

                // WIRED: all three knobs land on the block with the config's exact values.
                config = ScriptableObject.CreateInstance<GameConfig>();
                config.DisplacedWater = new DisplacedWaterSettings
                {
                    WaveExaggeration = 1.5f,
                    ShoreBandCoefficient = 2f,
                    CapSalienceStrength = 0.7f,
                    CapEnvelopeThreshold = 0.55f,
                    EnvelopeBandStrength = 0.2f,
                };
                SetPrivateField(surface, "_config", config);
                InvokePushUniforms(surface);
                renderer.GetPropertyBlock(read);
                Assert.AreEqual(0.7f, read.GetFloat("_CapSalienceStrength"),
                    "_CapSalienceStrength must be pushed from GameConfig.DisplacedWater");
                Assert.AreEqual(0.55f, read.GetFloat("_CapEnvelopeThreshold"),
                    "_CapEnvelopeThreshold must be pushed from GameConfig.DisplacedWater");
                Assert.AreEqual(0.2f, read.GetFloat("_EnvelopeBandStrength"),
                    "_EnvelopeBandStrength must be pushed from GameConfig.DisplacedWater");

                // LIVE: a config edit reaches the block on the next push (the owner tunes in Play).
                config.DisplacedWater.EnvelopeBandStrength = 0.5f;
                InvokePushUniforms(surface);
                renderer.GetPropertyBlock(read);
                Assert.AreEqual(0.5f, read.GetFloat("_EnvelopeBandStrength"),
                    "config edits must reach the block within one push — live reads, not Awake copies");
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (config != null) Object.DestroyImmediate(config);
                GameServices.Reset();
            }
        }

        // ---- helpers ---------------------------------------------------------------------------

        private static void SetPrivateField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field '{field}' not found on {target.GetType().Name} — the " +
                "config-consumption seam moved; update this test with it.");
            f.SetValue(target, value);
        }

        private static void InvokePushUniforms(WaterSurface surface)
        {
            var m = typeof(WaterSurface).GetMethod("PushUniforms",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, "WaterSurface.PushUniforms not found — the push seam moved; " +
                "update this test with it.");
            m.Invoke(surface, null);
        }

        private static void AssertShaderDefault(string shaderSource, string property, float expected)
        {
            // Matches the Properties-block line:  _Name ("label", Range(a,b)) = 0.62   (or Float)
            var m = Regex.Match(shaderSource,
                property + @"\s*\(""[^""]*"",\s*(?:Range\([^)]*\)|Float)\)\s*=\s*([0-9.]+)");
            Assert.IsTrue(m.Success,
                $"Shader property '{property}' not found in HiddenHarboursWater.shader's Properties " +
                "block — the displaced/salience stages must keep their named material knobs (rule 6).");
            float actual = float.Parse(m.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(actual, Is.EqualTo(expected).Within(1e-5f),
                $"Shader default for '{property}' differs from DisplacedWaterSettings.Default — " +
                "config, shader and twin are kept in LOCKSTEP (change all sides in the same commit), " +
                "so a wired config must be a visual no-op until the owner tunes it.");
        }
    }
}
