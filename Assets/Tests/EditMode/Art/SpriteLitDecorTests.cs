using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The <b>SHARED lit-sprite path</b> — the generalization of what used to be the tree's private
    /// fragment stage into something the shoreline plants, the shrubs and (next) the cliffs all run.
    ///
    /// <para><b>What is actually at risk here, and what these tests are for.</b> Three things, in
    /// descending order of how badly they would hurt:</para>
    /// <list type="number">
    /// <item><b>The trees regress.</b> A forest was placed and approved against the old code path. Moving
    /// it into a shared file must not move a pixel. Pinned below by the ONE difference the move
    /// introduces — a multiply by the rim gate — being provably exactly 1.0 for a tree.</item>
    /// <item><b>The generalization is fake.</b> If the shared path still demanded both sheets, the plants
    /// and shrubs (whose rigs bake no normal) would light exactly as well as they did before this work:
    /// not at all. So the mask-only case is asserted as a first-class path, not as a fallback.</item>
    /// <item><b>The rim gate reads the wrong channel.</b> The two rigs put their no-rim flag in DIFFERENT
    /// channels and both contracts say in so many words "this is the branch, read it, do not infer it".
    /// A consumer that guessed would be right for one family and silently wrong for the other.</item>
    /// </list>
    ///
    /// <para><b>Why these are headless twin tests rather than renders.</b> Same discipline as
    /// <see cref="SpriteLightResponseTests"/>: every function in the shared HLSL has a line-for-line C#
    /// twin, and the twin is what CI can measure. A render proof is the owner's GPU and no headless run
    /// stands in for it — the proofs are listed in the PR for exactly that reason.</para>
    /// </summary>
    public class SpriteLitDecorTests
    {
        const string TreeMaterialPath = "Assets/_Project/Art/Materials/Tree.mat";
        const string LitShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursLitSprite.shader";
        const string DecorIncludePath = "Assets/_Project/Art/Shaders/Include/SpriteLitDecor.hlsl";

        // A texel standing in for "body material, well lit, near the camera, fully covered" — the case
        // where every term is live and a mistake in any of them shows.
        static readonly Vector4 LitBodyTexel = new Vector4(0.8f, 0.6f, 0.9f, 1f);

        // =========================================================================================
        //  1. THE RIM GATE — the one piece of maths the shared path added
        // =========================================================================================

        /// <summary>
        /// The two shipped rigs disagree about WHERE the no-rim flag lives, so the selector has to carry
        /// the answer. This is that mapping, asserted against the CONTRACTS rather than against itself:
        /// shrubs put the flag in <c>_calendar.png</c> RED, shoreline plants in <c>_tide.png</c> BLUE.
        /// </summary>
        [Test]
        public void TheRimGateSelector_NamesTheChannelEachRigActuallyUses()
        {
            // A texel that is 1 in RED only — a shrub's veil pixel, flagged no-rim.
            var veil = new Vector4(1f, 0f, 0f, 0f);
            // A texel that is 1 in BLUE only — a shoreline plant's strap pixel, flagged no-rim.
            var strap = new Vector4(0f, 0f, 1f, 0f);

            Assert.AreEqual(1f, SpriteLightMath.RimGateFlag(veil, SpriteLightBinding.RimGateRed), 1e-6f,
                "The SHRUB rig's no-rim flag is its calendar sheet's RED channel (255 on veil pixels). " +
                "Reading anything else lets a veil rim like a trunk.");
            Assert.AreEqual(1f, SpriteLightMath.RimGateFlag(strap, SpriteLightBinding.RimGateBlue), 1e-6f,
                "The SHORELINE-PLANT rig's no-rim flag is its tide sheet's BLUE channel (255 on strap " +
                "pixels: blades, culms, fronds).");

            // 🔴 The cross-check is the point: swap the two selectors and BOTH go quiet. That is what a
            // consumer that inferred the channel instead of reading the contract would have shipped.
            Assert.AreEqual(0f, SpriteLightMath.RimGateFlag(veil, SpriteLightBinding.RimGateBlue), 1e-6f,
                "A shrub's veil flag read through the PLANT's channel is 0 — the gate silently stops " +
                "gating. This is the failure the published selector exists to prevent.");
            Assert.AreEqual(0f, SpriteLightMath.RimGateFlag(strap, SpriteLightBinding.RimGateRed), 1e-6f,
                "A plant's strap flag read through the SHRUB's channel is 0 — same failure, other rig.");
        }

        /// <summary>
        /// The gate must actually SUPPRESS a rim, and the negative control must actually show one. A test
        /// that only asserts the gated case passes just as happily when the rim was zero all along.
        /// </summary>
        [Test]
        public void TheGate_SuppressesTheRim_AndTheUngatedControlKeepsIt()
        {
            // A light BEHIND the sprite, which is the only place a back rim exists at all.
            Vector3 behind = SpriteLightMath.LightViewDirection(new Vector2(0f, 1f), 0.25f);

            float rim = SpriteLightMath.RimResponse(
                maskRim: 0.6f, maskDepth: 0.9f, maskCoverage: 1f,
                normal: Vector3.zero, lightDir: behind,
                steer: 0.8f, frontBand: SpriteLightMath.DefaultFrontBand, depthBias: 0.45f);

            // NEGATIVE CONTROL, observed rather than assumed: without a gate there IS a rim to lose.
            Assert.Greater(rim, 0.01f,
                "The control produced no rim, so the gated assertion below would pass for the wrong " +
                "reason. Fix the control (light behind? mask G non-zero?) before trusting the gate.");

            float ungated = rim * (1f - SpriteLightMath.RimGateFlag(LitBodyTexel, SpriteLightBinding.RimGateNone));
            Assert.AreEqual(rim, ungated, 1e-6f,
                "With NO gate bound the selector is zero, so the gate resolves to exactly 1.0 and the " +
                "rim passes through untouched. This is the tree, and it is why the tree is bit-identical.");

            var strap = new Vector4(0f, 0f, 1f, 0f);
            float gated = rim * (1f - SpriteLightMath.RimGateFlag(strap, SpriteLightBinding.RimGateBlue));
            Assert.AreEqual(0f, gated, 1e-6f,
                "A strap pixel still rimmed. The rig bakes blades, culms and fronds as strap material " +
                "precisely because a blade of eelgrass with a tree trunk's silhouette rim reads as the " +
                "mistake it is.");
        }

        // =========================================================================================
        //  2. THE GENERALIZATION — a rig with NO normal sheet is a first-class consumer
        // =========================================================================================

        /// <summary>
        /// 🔴 <b>The whole point of this work.</b> Before it, the response ran only when BOTH sheets were
        /// bound — so the shoreline plants and the shrubs, whose rigs resolve their normals AT BAKE and
        /// ship the light channel alone, could never light no matter what they baked. This asserts the
        /// mask-only path is live, and that it produces a real response rather than a zero one.
        /// </summary>
        [Test]
        public void ALightSheetAlone_IsEnoughToLight_WhichIsTheEntirePointOfSharingThePath()
        {
            var go = new GameObject("lit-decor-test-plant");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var binder = go.AddComponent<SpriteLightBinder>();

                var lightSheet = new Texture2D(2, 2);
                binder.SetSheets(lightSheet);      // no normal, no gate — the plant and shrub case

                Assert.IsTrue(binder.HasLightSheet, "The binder did not take the light sheet.");
                Assert.IsFalse(binder.HasNormalSheet,
                    "This case is defined by having no normal sheet — the test is not exercising it.");
                Assert.AreEqual(1f, SpriteLightBinding.ChannelsOn(sr), 1e-6f,
                    "🔴 The channels flag stayed DOWN with a light sheet bound, so the shader would skip " +
                    "the response entirely. That is precisely the old both-sheets-or-nothing rule, and " +
                    "with it the shoreline plants and shrubs stay unlit forever.");

                Assert.AreSame(lightSheet, SpriteLightBinding.MaskOn(sr),
                    "The light sheet did not reach the renderer's property block.");

                Object.DestroyImmediate(lightSheet);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// With no normal, every term falls back to the BAKED mask — the same path the tree's keyline ring
        /// has always taken. Asserted as a real, non-zero response so "supported" means lit, not tolerated.
        /// </summary>
        [Test]
        public void WithNoNormal_TheResponseCollapsesToTheBakedMask_AndIsStillAResponse()
        {
            Vector3 sun = SpriteLightMath.LightViewDirection(new Vector2(-0.45f, -0.89f), 0.72f);

            float lit = SpriteLightMath.KeyResponse(
                maskKey: 0.8f, maskDepth: 0.9f, maskCoverage: 1f,
                normal: Vector3.zero,                 // the plants and shrubs: no normal sheet at all
                lightDir: sun, relight: 0.75f,
                frontBand: SpriteLightMath.DefaultFrontBand, depthBias: 0.45f);

            Assert.Greater(lit, 0.01f,
                "A sprite with no normal sheet produced NO key response. The mask-only path is what the " +
                "shoreline plants and shrubs run on — if it is dead, they are dead.");

            // And it is genuinely the BAKED value driving it: drop mask R and the response goes with it.
            float dark = SpriteLightMath.KeyResponse(
                maskKey: 0f, maskDepth: 0.9f, maskCoverage: 1f,
                normal: Vector3.zero, lightDir: sun, relight: 0.75f,
                frontBand: SpriteLightMath.DefaultFrontBand, depthBias: 0.45f);
            Assert.Less(dark, lit * 0.05f,
                "Zeroing the baked KEY channel barely changed the response, so something other than the " +
                "bake is driving it — the fallback is not reading the mask it claims to read.");
        }

        // =========================================================================================
        //  3. DAY vs NIGHT — the sun term must genuinely swing, not sit still
        // =========================================================================================

        /// <summary>
        /// The differential the acceptance criteria ask for, at the only place headless can measure it:
        /// the sun's catch must be strong at noon and gone at night, because it is added PRE-grade and
        /// has to dim with the world (ADR 0013). A response that read the same at both would be a
        /// constant wearing a lighting model's clothes.
        /// </summary>
        [Test]
        public void TheSunCatch_IsStrongAtNoon_AndGoneAtNight()
        {
            const float sunrise = 6f, sunset = 20f;

            float noonElev = DayNightMath.SunElevation(12f, sunrise, sunset);
            float nightElev = DayNightMath.SunElevation(1f, sunrise, sunset);

            Assert.Greater(noonElev, 0f, "sanity: noon is above the horizon");
            Assert.Less(nightElev, 0f, "sanity: 01:00 is below the horizon");

            float noon = SunCatch(noonElev);
            float night = SunCatch(nightElev);

            Assert.Greater(noon, 0.02f,
                "The midday sun put almost no catch on the sprite. The whole read of this feature is a " +
                "lit decor field at noon.");
            Assert.AreEqual(0f, night, 1e-6f,
                "The sun still lit the sprite at 01:00. The catch rides `sunAmount = saturate(elevation)` " +
                "so it goes to nothing under the horizon — at night the MOON reaches this decor through " +
                "the day/night tint's own moonlight lift, not as a directional term here.");

            // Dusk sits BETWEEN the two rather than snapping: the grazing hour is the look this buys.
            float dusk = SunCatch(DayNightMath.SunElevation(19.5f, sunrise, sunset));
            Assert.Greater(dusk, 0f, "Dusk went fully dark before the sun set — the fade is a step.");
            Assert.Less(dusk, noon, "Dusk was not dimmer than noon.");
        }

        /// <summary>The sun's key term at one elevation, mask-only (the plant and shrub case), folded with
        /// the elevation gate exactly as <c>SpriteLitDecorResponse</c> folds it.</summary>
        static float SunCatch(float elevation)
        {
            Vector2 ground = DayNightMath.SunDirection(
                elevation >= 0f ? 12f : 1f, 6f, 20f, southBias: 0.5f, noonLift: 0.35f);

            // ⚠️ A near-zero ground direction means "nobody published a sun", and SunDirection then
            // substitutes a plausible mid-morning one — which would quietly hand the NIGHT case a
            // daylight elevation and make the assertion below pass for the wrong reason.
            Assert.Greater(ground.sqrMagnitude, 1e-6f,
                "The sun's ground direction came out degenerate, so the cycle-off fallback would take " +
                "over and this measurement would not be of the sun under test.");

            Vector3 dir = SpriteLightMath.SunDirection(ground, elevation, out float sunAmount);
            float key = SpriteLightMath.KeyResponse(
                maskKey: LitBodyTexel.x, maskDepth: LitBodyTexel.z, maskCoverage: LitBodyTexel.w,
                normal: Vector3.zero, lightDir: dir, relight: 0.75f,
                frontBand: SpriteLightMath.DefaultFrontBand, depthBias: 0.45f);
            return key * sunAmount;
        }

        // =========================================================================================
        //  4. THE TREE REGRESSION PIN
        // =========================================================================================

        /// <summary>
        /// 🔴 <b>The tree must come through the shared path unchanged.</b> The move introduces exactly one
        /// new operation on the tree's arithmetic — the rim is multiplied by the gate — and this pins the
        /// gate at exactly 1.0 for a tree, at both ends: the MATERIAL ships a zero selector, and a
        /// configured tree publishes a zero selector over it. Multiplying by exactly 1.0 is the IEEE 754
        /// multiplicative identity, so the rendered result is bit-identical, not merely close.
        /// </summary>
        [Test]
        public void ATreeBindsNoRimGate_SoItsRimIsMultipliedByExactlyOne()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TreeMaterialPath);
            Assert.IsNotNull(mat, $"The tree material ('{TreeMaterialPath}') was not found.");

            Assert.IsTrue(mat.HasVector(SpriteLightBinding.RimGateChannelProperty),
                "Tree.mat no longer carries the rim-gate selector, so the shared CBUFFER layout has " +
                "drifted from the shader's.");
            Assert.AreEqual(Vector4.zero, mat.GetVector(SpriteLightBinding.RimGateChannelProperty),
                "🔴 Tree.mat ships a NON-ZERO rim-gate selector. Trees have no no-rim pixels, so any " +
                "non-zero selector would start subtracting rim from a forest that was approved with it.");

            var go = new GameObject("lit-decor-test-tree");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var anchor = go.AddComponent<TreeTrunkAnchor>();
                var mask = new Texture2D(2, 2);
                var normal = new Texture2D(2, 2);
                anchor.SetLightSheets(mask, normal);

                Assert.AreEqual(Vector4.zero, SpriteLightBinding.RimGateChannelOn(sr),
                    "A configured TREE published a rim-gate selector. It must publish none — that is " +
                    "what makes the gate exactly 1.0 and the tree bit-identical through the shared path.");
                Assert.IsNull(SpriteLightBinding.RimGateOn(sr),
                    "A tree bound a rim-gate SHEET. No tree pixel is forbidden a rim.");

                // And the rest of the tree's binding still arrives, through the shared writer.
                Assert.AreEqual(1f, TreeTrunkAnchor.LightChannelsOn(sr), 1e-6f,
                    "A tree with both sheets no longer raises the channels flag.");
                Assert.AreSame(mask, TreeTrunkAnchor.MaskOn(sr), "The tree's mask did not reach the block.");
                Assert.AreSame(normal, TreeTrunkAnchor.NormalOn(sr), "The tree's normal did not reach the block.");
                Assert.AreEqual(1f, TreeTrunkAnchor.RootOn(sr).w, 1e-6f,
                    "The tree stopped publishing its stand position, so the boat lamp would light every " +
                    "tree in the scene from the world origin.");

                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(normal);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// A TREE is still held to both sheets even though the shared path would light it off the mask
        /// alone — its rig DOES bake a normal, every tuned strength was measured with that normal present,
        /// and a half-bound tree is a bake that went wrong rather than a rig family that ships without one.
        /// </summary>
        [Test]
        public void AHalfBoundTree_IsStillRefused_BecauseThatIsABrokenBakeAndNotARigFamily()
        {
            var go = new GameObject("lit-decor-test-half-tree");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var anchor = go.AddComponent<TreeTrunkAnchor>();
                var mask = new Texture2D(2, 2);
                anchor.SetLightSheets(mask, null);      // mask but no normal: a bake that went wrong

                Assert.IsFalse(anchor.HasLightSheets,
                    "A tree with half its pair reported itself lit.");
                Assert.AreEqual(0f, TreeTrunkAnchor.LightChannelsOn(sr), 1e-6f,
                    "A half-bound TREE raised the channels flag. The shared writer would accept the mask " +
                    "alone — correctly, for the plants and shrubs — so the tree's stricter rule has to be " +
                    "enforced where the tree binds, which is what this asserts.");

                Object.DestroyImmediate(mask);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // =========================================================================================
        //  5. THE TRAP THAT WOULD HAVE COST A DAY
        // =========================================================================================

        /// <summary>
        /// 🔴 Unity's fallback <c>"black"</c> texture is <b>(0, 0, 0, 1)</b> — its ALPHA is 1, not 0. So a
        /// consumer that published an alpha-channel selector without binding a gate sheet would read
        /// "no rim anywhere" off the fallback and silently lose every rim it had. The binder makes that
        /// unrepresentable by zeroing the selector whenever the sheet is absent; this is that guarantee.
        /// </summary>
        [Test]
        public void AnUnboundGateSheet_ForcesTheSelectorToZero_WhateverChannelWasAskedFor()
        {
            var go = new GameObject("lit-decor-test-ungated");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var binder = go.AddComponent<SpriteLightBinder>();
                var lightSheet = new Texture2D(2, 2);

                // Ask for a gate CHANNEL while binding no gate SHEET — the mistake this guards.
                binder.SetSheets(lightSheet, normalSheet: null, rimGateSheet: null,
                                 rimGateChannel: SpriteLightBinder.RimGateChannel.Blue);

                Assert.AreEqual(SpriteLightBinder.RimGateChannel.None, binder.GateChannel,
                    "The binder reported a live gate channel with no gate sheet bound.");
                Assert.AreEqual(Vector4.zero, SpriteLightBinding.RimGateChannelOn(sr),
                    "🔴 A non-zero selector reached the shader with no gate sheet bound. It would be " +
                    "dotted against the FALLBACK texture — whose alpha is 1 — and an alpha selector would " +
                    "read as 'no rim anywhere', silently deleting every rim on the sprite.");

                Object.DestroyImmediate(lightSheet);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // =========================================================================================
        //  6. THE PATH IS ACTUALLY SHARED
        // =========================================================================================

        /// <summary>
        /// A generalization that leaves the original behind as a special case is not one. Both shaders
        /// must call the SAME response out of the SAME file — a second copy is exactly the duplicated-maths
        /// problem the HLSL-twin discipline exists to prevent.
        /// </summary>
        [Test]
        public void BothShaders_CallTheOneSharedResponse_RatherThanCarryingACopy()
        {
            Assert.IsTrue(System.IO.File.Exists(DecorIncludePath),
                $"The shared lit-decor include is missing from '{DecorIncludePath}'.");

            foreach (string shaderPath in new[]
                     { "Assets/_Project/Art/Shaders/HiddenHarboursTreeWind.shader", LitShaderPath })
            {
                string src = System.IO.File.ReadAllText(shaderPath);
                StringAssert.Contains(DecorIncludePath, src,
                    $"'{shaderPath}' does not include the shared lit-decor response.");
                StringAssert.Contains("SpriteLitDecorResponse", src,
                    $"'{shaderPath}' includes the shared file but does not CALL its response — the " +
                    "sharing is nominal.");
                StringAssert.Contains("SPRITE_LIT_DECOR_MATERIAL_ROWS", src,
                    $"'{shaderPath}' declares its own lighting rows instead of taking the shared macro. " +
                    "Two hand-kept lists is how the layouts drift.");
            }

            // The tree keeps a SECOND pass (the HHReflect mirror) and the SRP Batcher requires one
            // UnityPerMaterial layout across a shader's passes — so the macro must appear TWICE there.
            string tree = System.IO.File.ReadAllText(
                "Assets/_Project/Art/Shaders/HiddenHarboursTreeWind.shader");
            int uses = tree.Split(
                new[] { "SPRITE_LIT_DECOR_MATERIAL_ROWS" }, System.StringSplitOptions.None).Length - 1;
            Assert.GreaterOrEqual(uses, 2,
                "The tree shader pastes the shared rows into only one of its passes. The SRP Batcher " +
                "requires UnityPerMaterial to be IDENTICAL across every pass of a shader and silently " +
                "drops the shader when it is not — which reads as a mysterious batching regression, not " +
                "as an error.");
        }
    }
}
