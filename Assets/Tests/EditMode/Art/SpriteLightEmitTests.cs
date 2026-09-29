using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The <b>EMIT term</b> of the shared light law: a rig's own night glow, read from an emitter sheet of
    /// its own (village return drop 14, L2; the owner's ruling of 2026-09-27). The ruling named three
    /// pins, and each has its section here:
    /// <list type="number">
    /// <item><b>The twin matches the HLSL for the new term.</b> The HLSL cannot run headless, so it is
    /// pinned as TEXT, line for line against the transcription the C# twin implements, and the twin is
    /// then run over every one of the 256 bytes a texel can hold.</item>
    /// <item><b>The mask order is unchanged.</b> The glow is its own sheet; it neither reads the mask nor
    /// moves a channel of it.</item>
    /// <item><b>With no emitter sheet bound, every sprite draws exactly as it does today.</b> The glow is
    /// opt-in at three levels (a shader that pastes its rows, a renderer that binds its sheet, a flag
    /// that defaults to 0), and each level is asserted, down to the twin adding exactly zero.</item>
    /// </list>
    /// </summary>
    public class SpriteLightEmitTests
    {
        const string ResponsePath = "Assets/_Project/Art/Shaders/Include/SpriteLightResponse.hlsl";
        const string DecorPath = "Assets/_Project/Art/Shaders/Include/SpriteLitDecor.hlsl";
        const string LitShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursLitSprite.shader";
        const string TreeShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursTreeWind.shader";

        // The shipped deepest night (skyTint × the 0.18 floor), the same number the lamp's own survival
        // test uses, and a full-day white.
        static readonly Color DeepNight = new Color(0.0216f, 0.0288f, 0.0612f, 1f);
        static readonly Color FullDay = new Color(1f, 1f, 1f, 1f);
        static readonly Color Unset = new Color(0f, 0f, 0f, 0f);

        // The rig's window light (houseIsoRig lightRig '#ffc673') and the gate the material ships.
        static readonly Color Glow = new Color(1f, 0.7765f, 0.451f, 1f);
        const float Threshold = 0.35f, Softness = 0.3f;

        // =========================================================================================
        //  1. THE TWIN MATCHES THE HLSL
        // =========================================================================================

        /// <summary>
        /// The HLSL of the new term, pinned line for line. Each line below is what the C# twin in
        /// <see cref="SpriteLightMath"/> transcribes, in the same float arithmetic; an edit to either
        /// side without the other fails here or in the exhaustive decode below.
        /// </summary>
        [Test]
        public void TheEmitTwin_MatchesTheHlsl_ConstantForConstantAndLineForLine()
        {
            string response = File.ReadAllText(ResponsePath);

            Assert.AreEqual(SpriteLightMath.EmitSourceStride,
                            DefineValue(response, "SPRITE_LIGHT_EMIT_SOURCE_STRIDE"), 0f,
                            "The byte's SOURCE stride differs between the HLSL and SpriteLightMath.EmitSourceStride.");
            Assert.AreEqual(SpriteLightMath.EmitLevelMax,
                            DefineValue(response, "SPRITE_LIGHT_EMIT_LEVEL_MAX"), 0f,
                            "The byte's top LEVEL step differs between the HLSL and SpriteLightMath.EmitLevelMax.");

            // SpriteLightMath.EmitByte
            AssertBody(response, "float SpriteLightEmitByte(float texel)",
                       "return floor(saturate(texel) * 255.0 + 0.5);");
            // SpriteLightMath.EmitLevel
            AssertBody(response, "float SpriteLightEmitLevel(float texel)",
                       "float b = SpriteLightEmitByte(texel); " +
                       "return (b - SPRITE_LIGHT_EMIT_SOURCE_STRIDE * floor(b / SPRITE_LIGHT_EMIT_SOURCE_STRIDE)) " +
                       "/ SPRITE_LIGHT_EMIT_LEVEL_MAX;");
            // SpriteLightMath.EmitSource
            AssertBody(response, "float SpriteLightEmitSource(float texel)",
                       "return floor(SpriteLightEmitByte(texel) / SPRITE_LIGHT_EMIT_SOURCE_STRIDE);");
            // SpriteLightMath.Emit
            AssertBody(response, "float SpriteLightEmit(float texel, float gate)",
                       "return SpriteLightEmitLevel(texel) * saturate(gate);");

            // SpriteLightMath.EmitResponse: the gate is the boat lamp's (SpriteLightNightGate, twinned by
            // LightMath.NightGateWithFallback), the strength is floored at 0, and the glow rides the same
            // day/night compensation (LightMath.CompensateForDayNightTint).
            AssertBody(File.ReadAllText(DecorPath),
                       "float3 SpriteLitDecorEmit(float2 uv, SpriteLitDecorEmitParams e)",
                       "float texel = SAMPLE_TEXTURE2D(_LightEmit, sampler_LightEmit, uv).r; " +
                       "float gate = SpriteLightNightGate(_DayNightTint.rgb, e.gateThreshold, e.gateSoftness, e.gateNoCycle); " +
                       "float3 glow = e.color * (max(0.0, e.strength) * SpriteLightEmit(texel, gate)); " +
                       "return SpriteLightCompensateForDayNight(glow, _DayNightTint.rgb);");
        }

        /// <summary>
        /// Every byte a texel can hold, decoded by the twin: the LEVEL is its low five bits over 31 and the
        /// SOURCE its high three, exactly, and a sampler's UNORM8 read either side of the byte (well inside
        /// half a step) decodes to the same byte. So a level never leaks into a source or back.
        /// </summary>
        [Test]
        public void EveryByte_DecodesToItsLevelAndItsSource_AndNeitherLeaksIntoTheOther()
        {
            for (int b = 0; b < 256; b++)
            {
                foreach (float nudge in new[] { 0f, -0.4f / 255f, 0.4f / 255f })
                {
                    float texel = b / 255f + nudge;
                    Assert.AreEqual(b, SpriteLightMath.EmitByte(texel), 0f,
                                    $"byte {b} (nudge {nudge}) did not come back from its texel");
                    Assert.AreEqual((b & 31) / 31f, SpriteLightMath.EmitLevel(texel), 1e-6f,
                                    $"byte {b}: the LEVEL is not its low five bits over 31");
                    Assert.AreEqual(b >> 5, SpriteLightMath.EmitSource(texel), 0f,
                                    $"byte {b}: the SOURCE is not its high three bits");
                }
            }

            // And the bake's encoder is the decoder's exact inverse on every level step and source.
            for (int l = 0; l <= SpriteLightMath.EmitLevelMax; l++)
            {
                for (int s = 0; s <= SpriteLightMath.EmitSourceMax; s++)
                {
                    byte b = SpriteLightMath.EncodeEmit(l / (float)SpriteLightMath.EmitLevelMax, s);
                    float texel = b / 255f;
                    Assert.AreEqual(l / (float)SpriteLightMath.EmitLevelMax, SpriteLightMath.EmitLevel(texel), 1e-6f,
                                    $"level step {l} at source {s} did not survive the byte");
                    Assert.AreEqual(l == 0 ? 0 : s, SpriteLightMath.EmitSource(texel), 0f,
                                    $"source {s} at level step {l}: a glowing texel keeps its source, a dark one has none");
                }
            }
        }

        /// <summary>The term itself: the baked level times the gate, and nothing at all for a texel of 0.</summary>
        [Test]
        public void TheEmitTerm_IsTheLevelTimesTheGate_AndNothingForATexelOfZero()
        {
            float full = SpriteLightMath.EncodeEmit(1f, SpriteLightMath.EmitSourceParlour) / 255f;
            float half = SpriteLightMath.EncodeEmit(16f / 31f, SpriteLightMath.EmitSourceUpper) / 255f;

            Assert.AreEqual(1f, SpriteLightMath.Emit(full, 1f), 1e-6f, "a full level at a full gate is the whole glow");
            Assert.AreEqual(0.5f, SpriteLightMath.Emit(full, 0.5f), 1e-6f, "the gate scales the glow linearly");
            Assert.AreEqual(16f / 31f, SpriteLightMath.Emit(half, 1f), 1e-6f, "the level scales the glow linearly");
            Assert.AreEqual(0f, SpriteLightMath.Emit(full, 0f), 0f, "a closed gate shows no glow");
            Assert.AreEqual(1f, SpriteLightMath.Emit(full, 7f), 1e-6f, "the gate is saturated, like the HLSL's");

            for (float gate = 0f; gate <= 1f; gate += 0.125f)
                Assert.AreEqual(0f, SpriteLightMath.Emit(0f, gate), 0f,
                                "a texel of 0 (no emitter, or an unbound sheet's black) must add nothing at any gate");
        }

        /// <summary>
        /// The glow comes on with the dark and survives it. Off at a full-day tint (it cannot wash out a
        /// sunlit wall), on at the deepest shipped night, and there the overlay's multiply lands it back at
        /// exactly its authored colour × strength × level: the same survival the boat lamp is held to.
        /// </summary>
        [Test]
        public void TheGlow_IsOffInDaylight_OnInTheDark_AndSurvivesTheNightOverlay()
        {
            float full = SpriteLightMath.EncodeEmit(1f, SpriteLightMath.EmitSourceKitchen) / 255f;
            const float strength = 1.25f;

            Color day = SpriteLightMath.EmitResponse(full, FullDay, Glow, strength, Threshold, Softness, 0f);
            Assert.AreEqual(0f, day.r + day.g + day.b, 1e-6f, "a lit window must not show at full day");

            Color night = SpriteLightMath.EmitResponse(full, DeepNight, Glow, strength, Threshold, Softness, 0f);
            Color onScreen = night * DeepNight;
            Assert.AreEqual(Glow.r * strength, onScreen.r, 1e-4f, "the overlay must land the glow at its authored red");
            Assert.AreEqual(Glow.g * strength, onScreen.g, 1e-4f, "…green");
            Assert.AreEqual(Glow.b * strength, onScreen.b, 1e-4f, "…blue");

            // With no cycle running the gate is the material's own fallback, and there is no overlay to
            // compensate for, so the glow passes through at exactly the fallback.
            Color bareOff = SpriteLightMath.EmitResponse(full, Unset, Glow, strength, Threshold, Softness, 0f);
            Assert.AreEqual(Color.clear, bareOff, "the shipped fallback (0) shows no glow in a bare scene");
            Color bareOn = SpriteLightMath.EmitResponse(full, Unset, Glow, strength, Threshold, Softness, 1f);
            Assert.AreEqual(Glow.r * strength, bareOn.r, 1e-6f, "a fallback of 1 shows the glow uncompensated");

            // A negative strength is floored, never a subtraction.
            Color negative = SpriteLightMath.EmitResponse(full, DeepNight, Glow, -1f, Threshold, Softness, 0f);
            Assert.AreEqual(0f, negative.r + negative.g + negative.b, 0f, "a negative strength must add nothing");
        }

        // =========================================================================================
        //  2. THE MASK ORDER IS UNCHANGED
        // =========================================================================================

        /// <summary>
        /// The glow did not move a channel of the mask and does not read it. The order is the trees'
        /// (R key, G back rim, B depth, A coverage), pinned again here at both the twin and the HLSL's
        /// accessors, and the emit's own functions touch neither the mask accessors nor the mask sheet.
        /// </summary>
        [Test]
        public void TheMaskOrder_IsUnchanged_AndTheGlowNeverReadsTheMask()
        {
            Assert.AreEqual(0, SpriteLightMath.MaskKey, "R is still the KEY light.");
            Assert.AreEqual(1, SpriteLightMath.MaskRim, "G is still the BACK RIM.");
            Assert.AreEqual(2, SpriteLightMath.MaskDepth, "B is still DEPTH.");
            Assert.AreEqual(3, SpriteLightMath.MaskCoverage, "A is still COVERAGE.");

            string response = File.ReadAllText(ResponsePath);
            StringAssert.Contains("#define SPRITE_LIGHT_MASK_KEY(m)      ((m).r)", response);
            StringAssert.Contains("#define SPRITE_LIGHT_MASK_RIM(m)      ((m).g)", response);
            StringAssert.Contains("#define SPRITE_LIGHT_MASK_DEPTH(m)    ((m).b)", response);
            StringAssert.Contains("#define SPRITE_LIGHT_MASK_COVERAGE(m) ((m).a)", response);

            string emitLaw = response.Substring(response.IndexOf("#define SPRITE_LIGHT_EMIT_SOURCE_STRIDE",
                                                                 System.StringComparison.Ordinal));
            StringAssert.DoesNotContain("SPRITE_LIGHT_MASK_", emitLaw, "the emit law must not read a mask channel");

            string decor = File.ReadAllText(DecorPath);
            string emitBody = Body(decor, "float3 SpriteLitDecorEmit(float2 uv, SpriteLitDecorEmitParams e)");
            StringAssert.DoesNotContain("_LightMask", emitBody, "the glow must read its OWN sheet, never the mask");
            StringAssert.DoesNotContain("_LightNormal", emitBody, "the glow is not a surface catching a light");
        }

        // =========================================================================================
        //  3. NO EMITTER SHEET BOUND: EVERY SPRITE DRAWS EXACTLY AS IT DOES TODAY
        // =========================================================================================

        /// <summary>
        /// At the shader level. The lit sprite computes the glow only behind a flag that defaults to 0, and
        /// the tree neither pastes the glow's rows nor calls it, so its layout and every pixel are what
        /// they were.
        /// </summary>
        [Test]
        public void WithNoEmitterSheet_TheShadersNeverComputeAGlow_AndTheTreeIsUntouched()
        {
            var lit = Shader.Find("HiddenHarbours/LitSprite");
            Assert.IsNotNull(lit, "HiddenHarbours/LitSprite did not load.");
            int flag = lit.FindPropertyIndex(SpriteLightBinding.EmitChannelsProperty);
            Assert.GreaterOrEqual(flag, 0, "The lit sprite lost its emitter flag property.");
            Assert.AreEqual(0f, lit.GetPropertyDefaultFloatValue(flag), 0f,
                            "The emitter flag must default to 0: at 0 the glow is never computed.");

            string litSrc = File.ReadAllText(LitShaderPath);
            int gate = litSrc.IndexOf("if (_LightEmitChannels > 0.5)", System.StringComparison.Ordinal);
            int call = litSrc.IndexOf("SpriteLitDecorEmit(IN.uv", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(gate, 0, "The lit sprite no longer gates the glow on its flag.");
            Assert.Greater(call, gate, "The lit sprite computes the glow outside its flag's gate.");
            Assert.AreEqual(call, litSrc.LastIndexOf("SpriteLitDecorEmit(", System.StringComparison.Ordinal),
                            "The lit sprite computes the glow in more than one place.");

            string tree = File.ReadAllText(TreeShaderPath);
            foreach (string s in new[] { "SPRITE_LIT_DECOR_EMIT_ROWS", "SpriteLitDecorEmit", "_LightEmit" })
                StringAssert.DoesNotContain(s, tree,
                    $"The tree shader now reads '{s}'. The tree was approved without a glow and bakes no " +
                    "emitter sheet, so it must neither paste the rows nor call the term.");
        }

        /// <summary>
        /// At the twin. A texel of 0, which is both "no emitter here" and the unbound sheet's black
        /// fallback, adds EXACTLY zero at every tint, gate and strength: colour + 0 is the colour, bit for
        /// bit.
        /// </summary>
        [Test]
        public void WithNoEmitterSheet_TheTwinAddsExactlyZero_AtEveryTintAndGate()
        {
            var tints = new[] { FullDay, DeepNight, Unset, new Color(0.9f, 0.62f, 0.4f, 1f), new Color(0.3f, 0.35f, 0.5f, 1f) };
            foreach (var tint in tints)
                foreach (float fallback in new[] { 0f, 1f })
                    foreach (float strength in new[] { 0f, 1f, 3f })
                        Assert.AreEqual(Color.clear,
                                        SpriteLightMath.EmitResponse(0f, tint, Glow, strength, Threshold, Softness, fallback),
                                        $"a texel of 0 added a glow at tint {tint}, fallback {fallback}, strength {strength}");
        }

        /// <summary>
        /// At the renderer. Every existing way of binding (the binder without an emitter sheet, the tree's
        /// anchor) publishes the flag at 0 and binds no emitter texture; a binder given one raises it; and
        /// taking the sheet away lowers it again, so a stale texture can never glow.
        /// </summary>
        [Test]
        public void WithNoEmitterSheet_EveryBindingPublishesTheFlagAtZero_AndTheSheetRaisesIt()
        {
            var go = new GameObject("emit-test-binder");
            var treeGo = new GameObject("emit-test-tree");
            var light = new Texture2D(2, 2);
            var normal = new Texture2D(2, 2);
            var emit = new Texture2D(2, 2, TextureFormat.R8, false, true);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var binder = go.AddComponent<SpriteLightBinder>();

                binder.SetSheets(light);
                Assert.AreEqual(0f, SpriteLightBinding.EmitChannelsOn(sr), 0f,
                                "A binder with no emitter sheet must publish the glow flag at 0.");
                Assert.IsNull(SpriteLightBinding.EmitOn(sr), "A binder with no emitter sheet bound one anyway.");
                Assert.IsFalse(binder.HasEmitSheet);

                binder.SetSheets(light, normal, emitSheet: emit);
                Assert.AreEqual(1f, SpriteLightBinding.EmitChannelsOn(sr), 0f,
                                "A binder given an emitter sheet did not raise the glow flag.");
                Assert.AreSame(emit, SpriteLightBinding.EmitOn(sr), "The emitter sheet did not reach the block.");
                Assert.AreEqual(1f, SpriteLightBinding.ChannelsOn(sr), 0f,
                                "Binding an emitter sheet disturbed the light sheet's own flag.");

                binder.SetSheets(light, normal);
                Assert.AreEqual(0f, SpriteLightBinding.EmitChannelsOn(sr), 0f,
                                "Taking the emitter sheet away left the glow flag up, so a stale texture " +
                                "would keep glowing.");

                // The tree binds through its own anchor and the old overload: flag at 0, no emitter.
                var treeSr = treeGo.AddComponent<SpriteRenderer>();
                treeGo.AddComponent<TreeTrunkAnchor>().SetLightSheets(light, normal);
                Assert.AreEqual(0f, SpriteLightBinding.EmitChannelsOn(treeSr), 0f,
                                "A configured tree published a glow flag other than 0.");
                Assert.IsNull(SpriteLightBinding.EmitOn(treeSr), "A configured tree bound an emitter sheet.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(treeGo);
                Object.DestroyImmediate(light);
                Object.DestroyImmediate(normal);
                Object.DestroyImmediate(emit);
            }
        }

        // ---- helpers ----------------------------------------------------------------------------

        static float DefineValue(string src, string name)
        {
            var m = Regex.Match(src, @"#define\s+" + name + @"\s+([0-9.]+)");
            Assert.IsTrue(m.Success, $"#define {name} is gone from the HLSL.");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        static void AssertBody(string src, string signature, string expected) =>
            Assert.AreEqual(expected, Body(src, signature),
                $"The HLSL body of '{signature}' is not the line its C# twin transcribes. Change the HLSL, " +
                "the twin in SpriteLightMath and this pin together, in the same commit.");

        // The text between the signature's braces, whitespace collapsed to single spaces.
        static string Body(string src, string signature)
        {
            int at = src.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, $"'{signature}' is gone from the HLSL.");
            int open = src.IndexOf('{', at);
            int depth = 0, close = -1;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}' && --depth == 0) { close = i; break; }
            }
            Assert.Greater(close, open, $"'{signature}' has no closing brace.");
            return Regex.Replace(src.Substring(open + 1, close - open - 1), @"\s+", " ").Trim();
        }
    }
}
