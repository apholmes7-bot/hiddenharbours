using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The owner's silhouette dial, end to end and headless (owner ruling, 2026-08-16: dense forest
    /// must never lose the player). Twin of <see cref="IsoFacetKeylineGateTests"/> and built on the
    /// same discipline: drive the mechanism THAT SHIPS —
    /// <see cref="FoliageSilhouetteGate.Apply"/> — never a re-statement of it, because a guard that
    /// exercises its own copy of the rule proves nothing about the rule that ships.
    ///
    /// <para><b>The sabotage arms are the point of the class.</b> A gate check that cannot fail proves
    /// nothing, and this repo has shipped exactly that mistake — the tell was a guard whose self-check
    /// reported "SABOTAGE NOT DETECTED". So <see cref="AGateThatStopsGating_IsCaught"/> and
    /// <see cref="AShaderThatDropsTheRead_IsCaught"/> break the mechanism deliberately and require the
    /// checks to THROW.</para>
    ///
    /// <para>⚠️ The shader source pin is textual on purpose — the fragment-branch half of the gate has
    /// no headless observable at all (CI has no graphics device; the pixels are adjudicated on the
    /// 4060 by <c>FoliageSilhouettePlayTests</c>). A legitimate refactor of the include is expected to
    /// touch this test CONSCIOUSLY, which is what a guard is for; the strings pinned are the two
    /// global declarations, the strength branch and the Load, nothing cosmetic.</para>
    /// </summary>
    public class FoliageSilhouetteGateTests
    {
        const string IncludePath = "Assets/_Project/Art/Shaders/Include/SpriteLitDecor.hlsl";
        const string TreePath = "Assets/_Project/Art/Shaders/HiddenHarboursTreeWind.shader";
        const string LitSpritePath = "Assets/_Project/Art/Shaders/HiddenHarboursLitSprite.shader";
        const string RendererPath = "Assets/Settings/Renderer2D.asset";
        const string ShippedConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

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
            // Never leave a dial up for the next fixture: the global is process-wide.
            SilhouetteRegistry.BindIdle();
            foreach (Object o in _cleanup)
                if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        // ---- the style decision, as data -----------------------------------------------------------

        [Test]
        public void TheShipDefault_IsOn_TheFisherIsNeverLost()
        {
            Assert.IsTrue(GameConfig.DefaultFoliageSilhouette,
                "The density pass was designed around this being ON. Flipping this const means deep " +
                "woods can swallow the fisher — that is an owner decision, not a tidy-up.");
            Assert.Greater(GameConfig.DefaultFoliageSilhouetteStrength, 0f,
                "a zero ship strength is the feature OFF wearing an ON flag");

            var fresh = ScriptableObject.CreateInstance<GameConfig>();
            _cleanup.Add(fresh);
            Assert.IsTrue(fresh.FoliageSilhouette,
                "a fresh GameConfig must carry the shipped default — GameConfig.asset lags the code, " +
                "so the CODE default is what actually ships");
            Assert.AreEqual(GameConfig.DefaultFoliageSilhouetteStrength, fresh.FoliageSilhouetteStrength,
                            1e-6f);

            Assert.IsTrue(GameServices.FoliageSilhouetteEnabled,
                "unwired (EditMode, a bare art scene, a test rig) must resolve to the SAME look the " +
                "shipped game has");
        }

        /// <summary>
        /// ⚠️ THE "GameConfig.asset LAGS THE CODE" GUARD, aimed at the exact failure mode that made
        /// these three flat fields rather than one tidy settings struct. The shipped asset has never
        /// heard of them; this asserts that what the OWNER's actual asset resolves to is the intended
        /// look, not a silent zero. If this ever goes red the cure is to re-serialise the asset, never
        /// to lower the expectation.
        /// </summary>
        [Test]
        public void TheShippedConfigAsset_ResolvesToTheIntendedLook()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>(ShippedConfigPath);
            if (shipped == null)
                Assert.Ignore($"No GameConfig at '{ShippedConfigPath}' in this checkout.");

            GameServices.Config = shipped;

            Assert.IsTrue(GameServices.FoliageSilhouetteEnabled,
                $"'{ShippedConfigPath}' resolves the silhouette OFF. A field the YAML has never heard " +
                "of should keep its code initializer — if it did not, the asset needs re-serialising.");
            Assert.Greater(GameServices.FoliageSilhouetteStrength, 0f,
                "the shipped asset resolves strength 0, which is the feature off. See above.");
            Assert.Greater(GameServices.FoliageSilhouetteTint.maxColorComponent, 0f,
                "the shipped asset resolves a BLACK tint — the silhouette would read as a hole in the " +
                "canopy rather than as the fisher.");
        }

        // ---- the CPU write: the REAL Apply, onto the REAL global ------------------------------------

        [Test]
        public void Apply_PacksTheTintAndStrength_OntoTheGlobal()
        {
            GameServices.Config = NewConfig(on: true, tint: new Color(0.2f, 0.4f, 0.6f), strength: 0.75f);

            FoliageSilhouetteGate.Apply(live: true);

            Vector4 packed = Shader.GetGlobalVector(SilhouetteShaderIds.Params);
            Assert.AreEqual(0.2f, packed.x, 1e-4f, "tint.r must reach the shader");
            Assert.AreEqual(0.4f, packed.y, 1e-4f, "tint.g must reach the shader");
            Assert.AreEqual(0.6f, packed.z, 1e-4f, "tint.b must reach the shader");
            Assert.AreEqual(0.75f, packed.w, 1e-4f,
                "strength must reach the shader in w — it is the amount AND the gate");
        }

        [Test]
        public void Apply_WithNoLiveBody_ZeroesTheStrength()
        {
            GameServices.Config = NewConfig(on: true, tint: Color.white, strength: 0.9f);

            FoliageSilhouetteGate.Apply(live: false);

            Assert.AreEqual(0f, Shader.GetGlobalVector(SilhouetteShaderIds.Params).w, 1e-6f,
                "with no body on screen the strength must be ZERO, not merely a cleared mask: a " +
                "buffer needs a source and a zero must be a strength. Left up, every foliage " +
                "fragment in both regions would buy a texture read that can only return 0.");
        }

        [Test]
        public void Apply_WithTheOwnerDialOff_ZeroesTheStrength()
        {
            GameServices.Config = NewConfig(on: false, tint: Color.white, strength: 0.9f);

            FoliageSilhouetteGate.Apply(live: true);

            Assert.AreEqual(0f, Shader.GetGlobalVector(SilhouetteShaderIds.Params).w, 1e-6f,
                "the owner's OFF must reach the shader as a zero STRENGTH, which is what makes it " +
                "genuinely free rather than merely invisible");
        }

        [Test]
        public void Apply_ClampsAStrengthOutOfRange()
        {
            GameServices.Config = NewConfig(on: true, tint: Color.white, strength: 4f);
            FoliageSilhouetteGate.Apply(live: true);
            Assert.AreEqual(1f, Shader.GetGlobalVector(SilhouetteShaderIds.Params).w, 1e-6f,
                "an over-range strength must clamp, not multiply the lerp past the tint");
        }

        [Test]
        public void BindIdle_LeavesAReadableMask_AndAZeroStrength()
        {
            GameServices.Config = NewConfig(on: true, tint: Color.white, strength: 0.9f);
            FoliageSilhouetteGate.Apply(live: true);

            SilhouetteRegistry.BindIdle();

            Assert.AreEqual(0f, Shader.GetGlobalVector(SilhouetteShaderIds.Params).w, 1e-6f,
                "going idle must take the strength down with it — otherwise the last frame's " +
                "silhouette stays frozen into every tree on screen");
        }

        // ---- the sabotage arms ---------------------------------------------------------------------

        [Test]
        public void AGateThatStopsGating_IsCaught()
        {
            // Break the mechanism the only way it can break silently: write the dial WITHOUT
            // consulting the live flag, i.e. what Apply would do if its `live &&` were dropped.
            GameServices.Config = NewConfig(on: true, tint: Color.white, strength: 0.9f);
            Shader.SetGlobalVector(SilhouetteShaderIds.Params, new Vector4(1f, 1f, 1f, 0.9f));

            Assert.Throws<AssertionException>(() =>
                Assert.AreEqual(0f, Shader.GetGlobalVector(SilhouetteShaderIds.Params).w, 1e-6f,
                                "no-live-body must zero the strength"),
                "SABOTAGE NOT DETECTED: a gate that ignores the live flag went unnoticed, so the " +
                "no-body check above cannot see the defect it exists to pin.");
        }

        [Test]
        public void AShaderThatDropsTheRead_IsCaught()
        {
            string include = ReadOrFail(IncludePath);
            string sabotaged = include.Replace("_HHSilhouetteMaskTex.Load", "// removed .Load");

            Assert.AreNotEqual(include, sabotaged,
                "SABOTAGE NOT DETECTED: the source pin below no longer matches the shipped include, " +
                "so it is guarding nothing.");
            Assert.Throws<AssertionException>(
                () => AssertCarriesTheRead(sabotaged, "sabotaged"),
                "SABOTAGE NOT DETECTED: the source pin passes on an include whose texture read has " +
                "been removed.");
        }

        // ---- the shader half, pinned textually ------------------------------------------------------

        [Test]
        public void TheSharedInclude_DeclaresAndReadsTheMask()
        {
            AssertCarriesTheRead(ReadOrFail(IncludePath), IncludePath);
        }

        [Test]
        public void BothFoliageConsumers_CallTheSharedFunction()
        {
            foreach (string path in new[] { TreePath, LitSpritePath })
            {
                string src = ReadOrFail(path);
                StringAssert.Contains("SilhouetteThrough(col.rgb", src,
                    $"'{path}' no longer composites the silhouette. The fisher would be lost behind " +
                    "this foliage family while the others still show her — worse than the feature " +
                    "being off, because the inconsistency reads as a bug in the world.");
            }
        }

        [Test]
        public void TheReflectionPass_DoesNotStampTheFisherIntoTheSea()
        {
            string tree = ReadOrFail(TreePath);
            int reflectAt = tree.IndexOf("\"LightMode\" = \"HHReflect\"", System.StringComparison.Ordinal);
            Assert.Greater(reflectAt, 0, "the tree's HHReflect pass has moved or been renamed");

            string reflectPass = tree.Substring(reflectAt);
            StringAssert.DoesNotContain("SilhouetteThrough", reflectPass,
                "the HHReflect pass must NOT composite the silhouette: a tree's reflection is a " +
                "mirrored picture of the TREE, and stamping the fisher into it would put her in the " +
                "sea, upside down, wherever she happens to stand on the shore.");
        }

        static void AssertCarriesTheRead(string src, string label)
        {
            StringAssert.Contains("Texture2D<float4> _HHSilhouetteMaskTex;", src,
                $"[{label}] the mask texture declaration is gone");
            StringAssert.Contains("float4 _HHSilhouetteParams;", src,
                $"[{label}] the packed dial declaration is gone");
            StringAssert.Contains("if (strength <= 1e-4) return col;", src,
                $"[{label}] the uniform strength branch is gone — with it goes the zero-cost-when-off " +
                "guarantee, and every foliage fragment pays for a lookup it cannot use");
            StringAssert.Contains("_HHSilhouetteMaskTex.Load", src,
                $"[{label}] the mask read is gone — the silhouette can never appear");
        }

        // ---- the wiring: the feature must actually be on the renderer -------------------------------

        /// <summary>
        /// ⚠️ THE PLUMBED-BUT-UNREAD GUARD. Every other test in this class can pass with the render
        /// pass never running once: the gate would write its dial, the shader would carry its read, and
        /// the mask texture would stay the 1x1 clear fallback forever — a feature that is on in every
        /// observable except the screen. The renderer asset is the one place that cannot be inferred
        /// from code, so it is asserted directly.
        /// </summary>
        [Test]
        public void TheMaskFeature_IsOnTheRenderer_AndActive()
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(RendererPath);
            Assert.IsNotNull(subAssets, $"Could not load '{RendererPath}'.");

            SilhouetteMaskFeature feature = null;
            foreach (Object o in subAssets)
                if (o is SilhouetteMaskFeature f) { feature = f; break; }

            Assert.IsNotNull(feature,
                $"'{RendererPath}' carries no SilhouetteMaskFeature. Without it the mask pass never " +
                "records, _HHSilhouetteMaskTex stays the 1x1 clear fallback, and the fisher is lost " +
                "behind dense foliage — with every other guard in this file still green.");
            Assert.IsTrue(feature.isActive,
                "the SilhouetteMaskFeature is present but DISABLED on the renderer, which is the same " +
                "screen result as it being absent.");

            // …and it must be in the renderer's own feature LIST, not merely a stray sub-asset: an
            // orphaned sub-asset loads exactly like a wired one through LoadAllAssetsAtPath.
            string yaml = ReadOrFail(RendererPath);
            long fileId = GetLocalId(feature);
            int listAt = yaml.IndexOf("m_RendererFeatures:", System.StringComparison.Ordinal);
            Assert.Greater(listAt, 0, "Renderer2D.asset has no m_RendererFeatures block");
            int listEnd = yaml.IndexOf("m_RendererFeatureMap:", System.StringComparison.Ordinal);
            string list = yaml.Substring(listAt, System.Math.Max(0, listEnd - listAt));
            StringAssert.Contains(fileId.ToString(), list,
                "the SilhouetteMaskFeature exists as a sub-asset but is NOT listed in " +
                "m_RendererFeatures, so URP never adds its pass.");
        }

        static long GetLocalId(Object o)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out _, out long localId);
            return localId;
        }

        static string ReadOrFail(string path)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), path);
            Assert.IsTrue(File.Exists(full), $"Missing '{path}'.");
            return File.ReadAllText(full);
        }

        GameConfig NewConfig(bool on, Color tint, float strength)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            cfg.FoliageSilhouette = on;
            cfg.FoliageSilhouetteTint = tint;
            cfg.FoliageSilhouetteStrength = strength;
            _cleanup.Add(cfg);
            return cfg;
        }
    }
}
