using System.IO;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The waterline wraps a round float (<see cref="BuoyWaveMath.WaterlineArc"/> and its shader twin
    /// <see cref="BuoyWaveMath.WaterlineCut"/>). A round body meets the water in a circle the 3/4 camera
    /// draws as an ellipse: the sides on the float line, the near point lower by the ground squash times
    /// the radius. These pin the arithmetic headless: the centre sits lower than the edges by exactly that
    /// amount, a zero arc is the straight row bit for bit, an atlas cell reads the same as a lone sprite,
    /// and the drop clamps so a buoy awash still goes fully under.
    /// </summary>
    public class BuoyWaterlineArcTests
    {
        private const float Eps = 1e-5f;
        private const float Ppu = 32f;

        // The 1.75 m nav can (s18): a 56 x 92 cell, pivot on the still waterline at (28, 33).
        private static readonly Rect CanRect = new Rect(0f, 0f, 56f, 92f);
        private static readonly Vector2 CanPivot = new Vector2(28f, 33f);
        private static readonly Vector2 CanTex = new Vector2(56f, 92f);
        private const float CanRadius = 0.875f;
        private const float FloatLine = 33f / 92f;

        private static Vector4 CanArc(float radius = CanRadius) =>
            BuoyWaveMath.WaterlineArc(radius, Ppu, CanRect, CanPivot, CanTex, IsoGround.GroundDepthScale);

        /// <summary>u at the centre of texel column <paramref name="col"/> of a texture <paramref name="w"/> wide.</summary>
        private static float Col(int col, float w) => (col + 0.5f) / w;

        // ---- 0 = the straight row ------------------------------------------------------------------

        [Test]
        public void AZeroRadiusIsTheStraightRowBitForBit()
        {
            Vector4 arc = CanArc(0f);
            Assert.AreEqual(Vector4.zero, arc, "a radius of 0 must pack to the zero arc");
            for (int c = 0; c < 56; c++)
            {
                float u = Col(c, 56f);
                Assert.AreEqual(FloatLine, BuoyWaveMath.WaterlineCut(u, FloatLine, arc),
                                $"column {c}: the zero arc moved the line");
            }
            Assert.AreEqual(0f, BuoyWaveMath.WaterlineCut(0.5f, 0f, arc), "dry stays dry");
        }

        [Test]
        public void DegenerateInputsPackToTheStraightRow()
        {
            Assert.AreEqual(Vector4.zero, CanArc(-1f), "negative radius");
            Assert.AreEqual(Vector4.zero, CanArc(float.NaN), "NaN radius");
            Assert.AreEqual(Vector4.zero, CanArc(float.PositiveInfinity), "infinite radius");
            Assert.AreEqual(Vector4.zero,
                BuoyWaveMath.WaterlineArc(CanRadius, 0f, CanRect, CanPivot, CanTex, IsoGround.GroundDepthScale), "no PPU");
            Assert.AreEqual(Vector4.zero,
                BuoyWaveMath.WaterlineArc(CanRadius, Ppu, CanRect, CanPivot, Vector2.zero, IsoGround.GroundDepthScale), "no texture");
            Assert.AreEqual(Vector4.zero,
                BuoyWaveMath.WaterlineArc(CanRadius, Ppu, CanRect, CanPivot, CanTex, 0f), "no ground squash");
        }

        // ---- the centre sits lower than the edges, by the camera's squash times the radius -----------

        [Test]
        public void TheCentreSitsLowerThanTheEdgesByTheGroundSquashTimesTheRadius()
        {
            Vector4 arc = CanArc();
            float expectedDropV = IsoGround.GroundDepthScale * CanRadius * Ppu / CanTex.y;
            Assert.AreEqual(expectedDropV, arc.x, Eps, "the near point's drop is not 0.643 r");

            // The body is 56 px across, centred on the pivot: columns 27 and 28 straddle the middle.
            float centre = BuoyWaveMath.WaterlineCut(Col(28, 56f), FloatLine, arc);
            float halfTexel = 0.5f / 28f;
            float expectedCentre = FloatLine - expectedDropV * Mathf.Sqrt(1f - halfTexel * halfTexel);
            Assert.AreEqual(expectedCentre, centre, Eps, "the centre column is not on the ellipse");
            Assert.Less(centre, FloatLine - 0.95f * expectedDropV, "the centre did not bow down");

            // Beyond the body the line is the float line itself: the sides wrap up to it.
            Vector4 wide = BuoyWaveMath.WaterlineArc(CanRadius, Ppu, new Rect(0, 0, 80, 92), new Vector2(40, 33),
                                                     new Vector2(80, 92), IsoGround.GroundDepthScale);
            Assert.AreEqual(FloatLine, BuoyWaveMath.WaterlineCut(Col(0, 80f), FloatLine, wide), Eps,
                            "outside the body the line must be the straight float line");
            Assert.AreEqual(FloatLine, BuoyWaveMath.WaterlineCut(Col(79, 80f), FloatLine, wide), Eps);

            // The outermost body columns are well above the centre: it wraps up toward the sides.
            float edge = BuoyWaveMath.WaterlineCut(Col(0, 56f), FloatLine, arc);
            Assert.Greater(edge - centre, 0.75f * expectedDropV, "the edges did not wrap up");
        }

        [Test]
        public void TheArcIsSymmetricAndFallsMonotonicallyToTheCentre()
        {
            Vector4 arc = CanArc();
            float prev = float.PositiveInfinity;
            for (int c = 0; c < 28; c++)
            {
                float left = BuoyWaveMath.WaterlineCut(Col(c, 56f), FloatLine, arc);
                float right = BuoyWaveMath.WaterlineCut(Col(55 - c, 56f), FloatLine, arc);
                Assert.AreEqual(left, right, Eps, $"column {c} and its mirror disagree");
                Assert.LessOrEqual(left, prev + Eps, $"column {c} climbed on the way to the centre");
                prev = left;
            }
        }

        [Test]
        public void ItIsSnappedToTheTexelColumn()
        {
            Vector4 arc = CanArc();
            // Anywhere inside one texel column reads the same line: a pixel staircase, never a smear.
            float a = BuoyWaveMath.WaterlineCut(10.05f / 56f, FloatLine, arc);
            float b = BuoyWaveMath.WaterlineCut(10.95f / 56f, FloatLine, arc);
            Assert.AreEqual(a, b, 0f, "two points in one texel column cut at different rows");
        }

        // ---- an atlas cell reads as the lone sprite ---------------------------------------------------

        [Test]
        public void AnAtlasCellReadsTheSameArcAsALoneSprite()
        {
            Vector4 lone = CanArc();
            // Facing 4 of the 8-wide sheet: x 224 in a 448 x 92 texture.
            Vector4 cell = BuoyWaveMath.WaterlineArc(CanRadius, Ppu, new Rect(224f, 0f, 56f, 92f), CanPivot,
                                                     new Vector2(448f, 92f), IsoGround.GroundDepthScale);
            Assert.AreEqual(lone.x, cell.x, Eps, "the drop depends on where the cell sits");
            Assert.AreEqual((224f + 28f) / 448f, cell.y, Eps, "the centre is not the cell's pivot column");
            for (int c = 0; c < 56; c++)
            {
                float uLone = Col(c, 56f);
                float uCell = Col(224 + c, 448f);
                Assert.AreEqual(BuoyWaveMath.WaterlineCut(uLone, FloatLine, lone),
                                BuoyWaveMath.WaterlineCut(uCell, FloatLine, cell), Eps, $"column {c}");
            }
        }

        // ---- clamps: awash goes fully under, a trough keeps the arc -----------------------------------

        [Test]
        public void ABuoyAwashStillGoesFullyUnder()
        {
            Vector4 arc = CanArc();
            for (int c = 0; c < 56; c++)
                Assert.AreEqual(1f, BuoyWaveMath.WaterlineCut(Col(c, 56f), 1f, arc), Eps,
                                $"column {c}: the arc kept a dry notch on a buoy fully awash");

            // Nearly awash: the drop is capped at what is left above the line, and never goes negative.
            float nearTop = 0.97f;
            float cut = BuoyWaveMath.WaterlineCut(Col(28, 56f), nearTop, arc);
            Assert.GreaterOrEqual(cut, nearTop - (1f - nearTop) - Eps, "the drop was not capped near the top");
            Assert.LessOrEqual(cut, nearTop + Eps);
        }

        [Test]
        public void TheArcRidesTheLineUpAndDownWithTheWave()
        {
            Vector4 arc = CanArc();
            float troughLine = BuoyWaveMath.WaterlineFraction(-0.3f, FloatLine, 0f, 2.84375f);
            float crestLine = BuoyWaveMath.WaterlineFraction(+0.3f, FloatLine, 0f, 2.84375f);
            float u = Col(28, 56f);
            float dropTrough = troughLine - BuoyWaveMath.WaterlineCut(u, troughLine, arc);
            float dropCrest = crestLine - BuoyWaveMath.WaterlineCut(u, crestLine, arc);
            Assert.AreEqual(dropTrough, dropCrest, Eps, "the bow changed shape with the wave height");
            Assert.Greater(BuoyWaveMath.WaterlineCut(u, crestLine, arc),
                           BuoyWaveMath.WaterlineCut(u, troughLine, arc), "a crest must lift the whole arc");
        }

        // ---- the component carries it, the marks and floats opt in from data -------------------------

        [Test]
        public void TheFloatPushesTheArcItsSpriteDescribes()
        {
            var tex = new Texture2D(56, 92, TextureFormat.RGBA32, false);
            Sprite sprite = Sprite.Create(tex, CanRect, new Vector2(0.5f, 33f / 92f), Ppu);
            var go = new GameObject("arc-buoy");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(go.transform, false);
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                var buoy = go.AddComponent<BuoyWaveVisual>();
                buoy.Configure(sr, visual.transform);

                buoy.ApplyWaveHeight(0f, 0f);
                Assert.AreEqual(Vector4.zero, buoy.WaterlineArc, "a float that never opted in must stay straight");

                buoy.ConfigureWaterlineArc(CanRadius);
                buoy.ApplyWaveHeight(0f, 0f);
                Vector4 expected = CanArc();
                Assert.AreEqual(expected.x, buoy.WaterlineArc.x, Eps);
                Assert.AreEqual(expected.y, buoy.WaterlineArc.y, Eps);
                Assert.AreEqual(expected.z, buoy.WaterlineArc.z, 1e-3f);
                Assert.AreEqual(expected.w, buoy.WaterlineArc.w, Eps);

                var mpb = new MaterialPropertyBlock();
                sr.GetPropertyBlock(mpb);
                Assert.AreEqual(expected.x, mpb.GetVector("_WaterlineArc").x, Eps, "the arc never reached the renderer");

                buoy.ConfigureWaterlineArc(0f);
                buoy.ApplyWaveHeight(0f, 0f);
                Assert.AreEqual(Vector4.zero, buoy.WaterlineArc, "opting back out must restore the straight row");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void ANavMarkWrapsAtHalfHerRungsDiameter()
        {
            var def = ScriptableObject.CreateInstance<NavBuoyDef>();
            def.Sizes.Add(new NavBuoyDef.SizeEntry
            {
                SizeId = "s24", DiameterMeters = 2.4f, SpriteHeightMeters = 3.5f, FloatLineFraction = 0.3f,
                SlopeFollow = 0.2f, Facings = new Sprite[8],
            });
            def.DefaultSizeIndex = 0;
            var root = new GameObject("arc-mark");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.AddComponent<SpriteRenderer>();
                var bob = root.AddComponent<BuoyWaveVisual>();
                root.AddComponent<NavBuoyMooring>();
                var mark = root.AddComponent<NavBuoyVisual>();
                mark.Configure(def, "s24", 0);
                Assert.AreEqual(1.2f, bob.WaterlineRadiusMeters, Eps, "the mark's waterline is not her own hull");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void TheShippedConfigCarriesThePaintedFloatsRadius()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config, "no shipped GameConfig");
            Assert.AreEqual(0.30f, config.LobsterFloatRadiusMeters, 1e-4f,
                            "the painted float's radius (19 px widest row at 32 PPU) did not ship");
        }

        // ---- the shader cuts, tints and draws the pale line on ONE curve ------------------------------

        [Test]
        public void TheShaderCutsTintsAndFoamsOnTheSameCurve()
        {
            string src = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursPlayerSubmerge.shader");
            StringAssert.Contains("float4 _WaterlineArc;", src, "the arc is not in the per-material CBUFFER");
            StringAssert.Contains("float cut = WaterlineCut(uv.x, _WaterlineFrac);", src);
            StringAssert.Contains("float below = saturate(cut - bodyFrac);", src, "the tint/dim/refraction gate ignores the arc");
            StringAssert.Contains("float dLine = abs(bodyFrac - cut);", src, "the pale line ignores the arc");
            StringAssert.Contains("return frac - min(drop, max(0.0, 1.0 - frac));", src,
                                  "the shader's cut is no longer BuoyWaveMath.WaterlineCut's");
            StringAssert.DoesNotContain("#pragma multi_compile", src, "the arc must cost no shader variant");
            StringAssert.DoesNotContain("#pragma shader_feature", src, "the arc must cost no shader variant");
        }
    }
}
