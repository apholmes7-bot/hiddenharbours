using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The pure maths of a LAMP-CAST SHADOW (ADR 0016, lights PR B), pinned headless — no scene, no
    /// GPU (CLAUDE.md rule 5). What has to be true for the owner's sentence to hold: the shadow runs
    /// AWAY from the lamp through the feet; it rakes LONGER the farther the caster stands from the
    /// lamp and the LOWER the lamp sits; it is exactly as strong as the light it blocks, and exactly
    /// nothing at strength 0; and the per-pixel un-shear the shader runs is the exact inverse of the
    /// shear the C# predicts — so the two cannot disagree about which caster point a shadow pixel
    /// belongs to. The source guards at the bottom hold the HLSL to these expressions.
    /// </summary>
    public class LampShadowMathTests
    {
        private const float Eps = 1e-4f;

        // ---- direction ------------------------------------------------------------------------------

        [Test]
        public void ShadowDirection_RunsFromTheLampThroughTheFeet()
        {
            Vector2 d = LampShadowMath.ShadowDirection(Vector2.zero, new Vector2(3f, 4f), Vector2.up);
            Assert.AreEqual(0.6f, d.x, Eps);
            Assert.AreEqual(0.8f, d.y, Eps);

            // A lamp on the far side throws the other way — the radial fact a sun can never show.
            Vector2 back = LampShadowMath.ShadowDirection(new Vector2(6f, 8f), new Vector2(3f, 4f), Vector2.up);
            Assert.AreEqual(-0.6f, back.x, Eps);
            Assert.AreEqual(-0.8f, back.y, Eps);
        }

        [Test]
        public void ShadowDirection_UnderTheLamp_FallsBackToTheBeam_ThenToDownScreen()
        {
            Vector2 beam = LampShadowMath.ShadowDirection(Vector2.one, Vector2.one, new Vector2(2f, 0f));
            Assert.AreEqual(1f, beam.x, Eps);
            Assert.AreEqual(0f, beam.y, Eps);

            Vector2 none = LampShadowMath.ShadowDirection(Vector2.one, Vector2.one, Vector2.zero);
            Assert.AreEqual(Vector2.down, none);
        }

        // ---- elevation and length ------------------------------------------------------------------

        [Test]
        public void LampElevation_IsTheSineOfTheAltitude_HigherForANearerCasterOrAHigherLamp()
        {
            float near = LampShadowMath.LampElevation(2.5f, 1f, 0.5f);
            float far = LampShadowMath.LampElevation(2.5f, 8f, 0.5f);
            float high = LampShadowMath.LampElevation(10f, 8f, 0.5f);
            Assert.Greater(near, far, "a nearer caster sees the lamp higher");
            Assert.Greater(high, far, "a higher lamp is higher");
            Assert.AreEqual(1f, LampShadowMath.LampElevation(2.5f, 0f, 0.5f), Eps, "straight overhead");
            Assert.AreEqual(2.5f / Mathf.Sqrt(2.5f * 2.5f + 64f), far, Eps, "h / sqrt(h² + d²)");
        }

        [Test]
        public void LampElevation_FloorsTheHeight_SoAGroundLampThrowsABoundedRake()
        {
            float e = LampShadowMath.LampElevation(0f, 5f, 0.5f);
            Assert.AreEqual(0.5f / Mathf.Sqrt(0.25f + 25f), e, Eps);
            Assert.Greater(e, 0f);
        }

        [Test]
        public void ShadowLength_GrowsWithDistance_ShrinksWithLampHeight_AndStartsAtTheNoonStub()
        {
            float L(float lampHeight, float dist) => LampShadowMath.ShadowLengthMultiple(
                LampShadowMath.LampElevation(lampHeight, dist, 0.5f), 0.35f, 5f, 7f);

            Assert.Less(L(2.5f, 1f), L(2.5f, 4f));
            Assert.Less(L(2.5f, 4f), L(2.5f, 8f));
            Assert.Greater(L(0.5f, 8f), L(2.5f, 8f), "a low lamp rakes longer than a high one");
            Assert.AreEqual(0.35f, L(2.5f, 0f), Eps, "overhead: the noon stub, not nothing");
            Assert.Greater(L(2.5f, 8f), 0f, "a lamp that is on always throws");
            Assert.LessOrEqual(L(0.5f, 1000f), 5f + Eps, "never past the horizon length");
        }

        [Test]
        public void ShadowLength_IsCappedAtMaxLength_AndNeverZeroForALitLamp()
        {
            Assert.AreEqual(7f, LampShadowMath.ShadowLengthMultiple(1e-4f, 0.35f, 50f, 7f), Eps, "the cap bites");
            // The sun's curve returns 0 for a sun at/below the horizon; a lamp's elevation is floored
            // above it, so even a caller passing 0 gets the horizon rake rather than no shadow.
            Assert.Greater(LampShadowMath.ShadowLengthMultiple(0f, 0.35f, 5f, 7f), 4.9f);
        }

        // ---- the fold ------------------------------------------------------------------------------

        [Test]
        public void ClampShearFold_LeavesUpwardAndSidewaysShadowsAlone()
        {
            Assert.AreEqual(5f, LampShadowMath.ClampShearFold(Vector2.up, 5f, 0.2f), Eps);
            Assert.AreEqual(5f, LampShadowMath.ClampShearFold(Vector2.right, 5f, 0.2f), Eps);
            Assert.AreEqual(5f, LampShadowMath.ClampShearFold(new Vector2(0.6f, 0.8f), 5f, 0.2f), Eps);
        }

        [Test]
        public void ClampShearFold_BoundsADownScreenShadow_SoTheUnshearStaysInvertible()
        {
            var dir = new Vector2(0.6f, -0.8f);
            float L = LampShadowMath.ClampShearFold(dir, 5f, 0.2f);
            Assert.AreEqual((1f - 0.2f) / 0.8f, L, Eps);
            Assert.GreaterOrEqual(1f + dir.y * L, 0.2f - Eps, "the vertical scale never falls below the floor");
            Assert.AreEqual(0.5f, LampShadowMath.ClampShearFold(dir, 0.5f, 0.2f), Eps, "a short rake is untouched");
        }

        // ---- shear and its inverse -------------------------------------------------------------------

        [Test]
        public void Shear_LeavesTheFeetWhereTheyAre_AndRakesTheOtherWayBelowThem()
        {
            var foot = new Vector2(2f, 1f);
            Assert.AreEqual(foot, LampShadowMath.Shear(foot, foot, Vector2.right, 3f));
            Vector2 above = LampShadowMath.Shear(new Vector2(2f, 2f), foot, Vector2.right, 3f);
            Assert.AreEqual(5f, above.x, Eps, "one metre up lands L metres along the direction");
            Vector2 below = LampShadowMath.Shear(new Vector2(2f, 0f), foot, Vector2.right, 3f);
            Assert.AreEqual(-1f, below.x, Eps, "rows below the feet rake the other way — the sun shader's negative upFrac");
        }

        [Test]
        public void Unshear_IsTheExactInverseOfShear_OverAGridOfPoints_InEveryDirection()
        {
            var foot = new Vector2(3.25f, -1.5f);
            Vector2[] dirs = { Vector2.right, Vector2.up, new Vector2(-0.6f, 0.8f), new Vector2(0.6f, -0.8f), Vector2.down };
            float[] lengths = { 0.35f, 2.7f, 5f };
            foreach (Vector2 dir in dirs)
            foreach (float len in lengths)
            {
                float L = LampShadowMath.ClampShearFold(dir, len, 0.2f);
                for (float x = -4f; x <= 4f; x += 1f)
                for (float y = -4f; y <= 4f; y += 1f)
                {
                    var c = new Vector2(x, y);
                    Vector2 s = LampShadowMath.Shear(c, foot, dir, L);
                    Vector2 back = LampShadowMath.Unshear(s, foot, dir, L);
                    Assert.AreEqual(c.x, back.x, 1e-4f, $"dir {dir} L {L} at {c}");
                    Assert.AreEqual(c.y, back.y, 1e-4f, $"dir {dir} L {L} at {c}");
                }
            }
        }

        [Test]
        public void ShearedBounds_ContainsEveryShearedCorner_AndIsExactForARectangle()
        {
            var min = new Vector2(-0.5f, -0.25f);
            var max = new Vector2(0.5f, 2f);
            var foot = new Vector2(0f, 0f);
            var dir = new Vector2(0.8f, 0.6f);
            LampShadowMath.ShearedBounds(min, max, foot, dir, 2.5f, out Vector2 bmin, out Vector2 bmax);

            Vector2[] corners =
            {
                new Vector2(min.x, min.y), new Vector2(max.x, min.y), new Vector2(min.x, max.y), new Vector2(max.x, max.y),
            };
            foreach (Vector2 c in corners)
            {
                Vector2 s = LampShadowMath.Shear(c, foot, dir, 2.5f);
                Assert.That(s.x, Is.InRange(bmin.x - Eps, bmax.x + Eps));
                Assert.That(s.y, Is.InRange(bmin.y - Eps, bmax.y + Eps));
            }
            // The top-right corner sheared is the far edge: (0.5 + 0.8·2.5·2, 2 + 0.6·2.5·2).
            Assert.AreEqual(4.5f, bmax.x, Eps);
            Assert.AreEqual(5f, bmax.y, Eps);
        }

        // ---- strength: as strong as the light it blocks ---------------------------------------------

        [Test]
        public void LampShapeAtFoot_IsFullOnTheAxis_ZeroOffTheCone_ZeroBeyondTheRange_AndFeathersAtTheEdge()
        {
            Vector2 lamp = Vector2.zero, beam = Vector2.up;
            float on = LampShadowMath.LampShapeAtFoot(lamp, beam, 9f, 26f, 0.45f, 0.7f, new Vector2(0f, 2f));
            float off = LampShadowMath.LampShapeAtFoot(lamp, beam, 9f, 26f, 0.45f, 0.7f, new Vector2(2f, 0f));
            float far = LampShadowMath.LampShapeAtFoot(lamp, beam, 9f, 26f, 0.45f, 0.7f, new Vector2(0f, 12f));
            // 22° off the axis: inside the feathered band (the inner cone ends at 26·(1−0.45) = 14.3°).
            float edge = LampShadowMath.LampShapeAtFoot(lamp, beam, 9f, 26f, 0.45f, 0.7f,
                                                        new Vector2(2f * Mathf.Sin(22f * Mathf.Deg2Rad), 2f * Mathf.Cos(22f * Mathf.Deg2Rad)));

            Assert.Greater(on, 0.5f, "on the axis, near the lamp");
            Assert.AreEqual(0f, off, Eps, "90° off a 26° cone is dark");
            Assert.AreEqual(0f, far, Eps, "beyond the range is dark");
            Assert.Greater(edge, 0f);
            Assert.Less(edge, on, "the cone's soft edge feathers the shadow with the beam");

            float round = LampShadowMath.LampShapeAtFoot(lamp, beam, 9f, 180f, 0.45f, 0.7f, new Vector2(2f, 0f));
            Assert.Greater(round, 0f, "a round lamp has no angular cut");
        }

        [Test]
        public void ShadowAlpha_IsExactlyZeroAtStrengthZero_AndNeverExceedsTheLightThere()
        {
            Assert.AreEqual(0f, LampShadowMath.ShadowAlpha(0f, 1f, 1f, 1f), "no tolerance: the passthrough is exact");
            Assert.AreEqual(0f, LampShadowMath.ShadowAlpha(0f, 0.3f, 0.7f, 1f));
            Assert.AreEqual(0.4f, LampShadowMath.ShadowAlpha(0.8f, 0.5f, 1f, 1f), Eps);
            Assert.LessOrEqual(LampShadowMath.ShadowAlpha(1f, 0.5f, 1f, 1f), 0.5f, "never more than the lamp's shape at the feet");
            Assert.AreEqual(0f, LampShadowMath.ShadowAlpha(1f, 1f, 0f, 1f), Eps, "gated off by day");
            Assert.AreEqual(0.2f, LampShadowMath.ShadowAlpha(1f, 1f, 1f, 0.2f), Eps, "a dimmed searchlight fades its shadows");
        }

        [Test]
        public void IntensityShare_ClampsAHotLampToFull_AndADimOneToItself()
        {
            Assert.AreEqual(1f, LampShadowMath.IntensityShare(1.5f), Eps);
            Assert.AreEqual(0.225f, LampShadowMath.IntensityShare(0.225f), Eps);
            Assert.AreEqual(0f, LampShadowMath.IntensityShare(-1f), Eps);
        }

        // ---- the sprite silhouette mapping -----------------------------------------------------------

        [Test]
        public void SpriteWorldRect_PlacesTheCellAroundThePivot_AndAFlipMirrorsItAboutThePivot()
        {
            var cell = new Rect(0f, 0f, 16f, 48f);
            var pivot = new Vector2(4f, 4f);   // deliberately off-centre so a flip has something to move
            var at = new Vector2(10f, 20f);

            LampShadowMath.SpriteWorldRect(at, Vector2.one, cell, pivot, 32f, false, false, out Vector2 min, out Vector2 max);
            Assert.AreEqual(10f - 4f / 32f, min.x, Eps);
            Assert.AreEqual(20f - 4f / 32f, min.y, Eps);
            Assert.AreEqual(min.x + 0.5f, max.x, Eps);
            Assert.AreEqual(min.y + 1.5f, max.y, Eps);

            LampShadowMath.SpriteWorldRect(at, Vector2.one, cell, pivot, 32f, true, false, out Vector2 fmin, out Vector2 fmax);
            Assert.AreEqual(10f + (4f - 16f) / 32f, fmin.x, Eps, "flipX: the cell hangs the other side of the pivot");
            Assert.AreEqual(fmin.x + 0.5f, fmax.x, Eps);
            Assert.AreEqual(min.y, fmin.y, Eps, "flipX leaves y alone");

            LampShadowMath.SpriteWorldRect(at, new Vector2(2f, 2f), cell, pivot, 32f, false, true, out Vector2 ymin, out Vector2 ymax);
            Assert.AreEqual(20f + (4f - 48f) / 32f * 2f, ymin.y, Eps, "flipY, at scale 2");
            Assert.AreEqual(ymin.y + 3f, ymax.y, Eps);
            Assert.AreEqual(10f - 4f / 32f * 2f, ymin.x, Eps);
        }

        [Test]
        public void SpriteUvRect_MapsTheCell_AndFoldsAFlipInAsANegativeExtent()
        {
            var cell = new Rect(16f, 32f, 16f, 48f);
            Vector4 uv = LampShadowMath.SpriteUvRect(cell, 64, 128, false, false);
            Assert.AreEqual(new Vector4(0.25f, 0.25f, 0.25f, 0.375f), uv);

            Vector4 fx = LampShadowMath.SpriteUvRect(cell, 64, 128, true, false);
            Assert.AreEqual(0.5f, fx.x, Eps);
            Assert.AreEqual(-0.25f, fx.z, Eps);
            Assert.AreEqual(uv.y, fx.y, Eps);

            Vector4 fy = LampShadowMath.SpriteUvRect(cell, 64, 128, false, true);
            Assert.AreEqual(0.625f, fy.y, Eps);
            Assert.AreEqual(-0.375f, fy.w, Eps);
        }

        [Test]
        public void SnapToPixels_SnapsToTheGrid()
        {
            Vector2 s = LampShadowMath.SnapToPixels(new Vector2(1.017f, -0.49f), 32f);
            Assert.AreEqual(Mathf.Round(1.017f * 32f) / 32f, s.x, 1e-6f);
            Assert.AreEqual(Mathf.Round(-0.49f * 32f) / 32f, s.y, 1e-6f);
        }

        // ---- the shader and the materials, held to the maths ------------------------------------------

        /// <summary>
        /// The fragment stage runs the un-shear these tests pin, multiplies above the glow, and
        /// compiles both silhouette variants. A drift in any of these lines is a drift between what
        /// the C# predicts and what the GPU draws.
        /// </summary>
        [Test]
        public void TheShader_RunsTheUnshearTheseTestsPin_AndMultipliesAboveTheGlow()
        {
            string src = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursLampShadow.shader");
            StringAssert.Contains("#pragma multi_compile_local _ " + LampShadowSystem.HullKeyword, src,
                "both silhouette variants must be compiled — a shader_feature would strip the one no asset enables");
            StringAssert.Contains("Blend Zero SrcColor", src, "the shadow MULTIPLIES the frame; it is not a dark sprite");
            StringAssert.Contains("ZTest Always", src);
            StringAssert.Contains("float denom = 1.0 + dir.y * L;", src);
            StringAssert.Contains("float h = (p.y - foot.y) / denom;", src);
            StringAssert.Contains("return float2(p.x - dir.x * L * h, foot.y + h);", src);
            StringAssert.Contains("if (a <= 0.0) discard;", src, "alpha 0 must write nothing at all");
            StringAssert.Contains("Texture2D<float4> _HHHullScreenTex;", src, "the hull silhouette is the resolved screen texture");
        }

        [Test]
        public void TheShippedMaterials_UseTheShader_AndOnlyTheHullOneCarriesTheKeyword()
        {
            var sprite = Resources.Load<Material>(LampShadowSystem.SpriteMaterialPath);
            var hull = Resources.Load<Material>(LampShadowSystem.HullMaterialPath);
            Assert.IsNotNull(sprite, "Resources/LampShadow.mat is missing");
            Assert.IsNotNull(hull, "Resources/LampShadowHull.mat is missing");
            Assert.AreEqual(LampShadowSystem.ShaderName, sprite.shader.name);
            Assert.AreEqual(LampShadowSystem.ShaderName, hull.shader.name);
            Assert.IsFalse(sprite.IsKeywordEnabled(LampShadowSystem.HullKeyword), "the sprite material samples the sheet");
            Assert.IsTrue(hull.IsKeywordEnabled(LampShadowSystem.HullKeyword), "the hull material reads the screen texture");
        }

        /// <summary>
        /// The sorting law in numbers: overlay, then shadows, then glow — nearer draws later at an
        /// equal order, so the shadow quad must be pinned strictly between the overlay and the light
        /// quads. If any of the three constants moves past another, the shadows silently vanish
        /// under the glow (or the overlay darkens them into the world), so the ordering is pinned.
        /// </summary>
        [Test]
        public void TheDepthPins_AreOrdered_OverlayThenShadowsThenGlow()
        {
            Assert.Less(DayNightController.OverlayNearOffset, LampShadowSystem.ShadowDepthOffset);
            Assert.Less(LampShadowSystem.ShadowDepthOffset, SceneLight.DefaultCameraDepthOffset);

            const float camZ = -10f, near = 0.3f;
            float shadowZ = LightMath.CameraDepthZ(camZ, 1f, near, LampShadowSystem.ShadowDepthOffset);
            float glowZ = LightMath.CameraDepthZ(camZ, 1f, near, SceneLight.DefaultCameraDepthOffset);
            float overlayZ = LightMath.CameraDepthZ(camZ, 1f, near, DayNightController.OverlayNearOffset);
            Assert.Less(shadowZ, glowZ, "the shadow quad sits nearer the camera than a light quad");
            Assert.Less(overlayZ, shadowZ, "and farther than the overlay");
        }
    }
}
