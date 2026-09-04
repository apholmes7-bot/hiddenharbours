using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE TREES UNDER THE SUN</b> — the owner's 2026-09-03 ask, in his words: <i>"tree lighting is
    /// my concern, this should be noticable in day too with the changing sun, and shadows, not jsut
    /// night lighting."</i>
    ///
    /// <para>Three separable claims live in that sentence, and each gets its own pin here:</para>
    /// <list type="number">
    /// <item><b>The trees respond at all.</b> <c>Tree.mat</c> shipped at <c>_LightResponse 0</c> by an
    /// explicit earlier decision — the flat look was the one the owner had placed a forest against, and
    /// turning it on was named as HIS call. He has made it. The pin flips with the ruling.</item>
    /// <item><b>The lit side SWINGS with the sun.</b> Not "is brighter at noon" — the catch has to move
    /// ACROSS the crown as the sun crosses the sky, or a tree reads as a lamp on a timer.</item>
    /// <item><b>The lit side and the shadow AGREE.</b> One published sun feeds both, and — new in this
    /// PR — one published <c>_ShadowStrength</c> fades both. Before it, a storm faded the shadow to
    /// 0.49 while the lit side held at 1.00.</item>
    /// </list>
    ///
    /// <para><b>Headless twin tests, deliberately.</b> Same discipline as <see cref="SpriteLitDecorTests"/>:
    /// every function in the shared HLSL has a line-for-line C# twin and the twin is what CI can measure
    /// (CI has no graphics device). The render proofs are the owner's eye and are listed in the PR.</para>
    /// </summary>
    public class TreeSunLightingTests
    {
        const string TreeMaterialPath = "Assets/_Project/Art/Materials/Tree.mat";
        const string ShrubMaterialPath = "Assets/_Project/Art/Materials/LitShrub.mat";
        const string PlantMaterialPath = "Assets/_Project/Art/Materials/LitShorePlant.mat";
        const string DecorIncludePath = "Assets/_Project/Art/Shaders/Include/SpriteLitDecor.hlsl";
        const string ResponseIncludePath = "Assets/_Project/Art/Shaders/Include/SpriteLightResponse.hlsl";

        // The shipped DayNightProfile's day, so every hour below is an hour the game really has.
        const float Sunrise = 6f, Sunset = 20f, SouthBias = 0.2f, NoonLift = 0.9f;
        const float OvercastFadesShadow = 0.85f;   // DayNightProfile._overcastFadesShadow
        const float WeatherDimMax = 0.6f;          // DayNightProfile._weatherDimMax — the storm ceiling

        static Vector2 Ground(float hour) =>
            DayNightMath.SunDirection(hour, Sunrise, Sunset, SouthBias, NoonLift);

        static Material Load(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(m, $"'{path}' was not found.");
            return m;
        }

        // =========================================================================================
        //  1. THE RESPONSE IS ON — and at the dials every shipped family already uses
        // =========================================================================================

        /// <summary>
        /// 🔴 <b>OWNER RULING 2026-09-03: the tree light response comes ON.</b> This test is the inverse
        /// of the one it replaces (<c>TreeMaterial_ExposesTheLightResponse_AndShipsItOff</c>), and the
        /// inversion is the whole record of the decision — the earlier test said in so many words that
        /// raising this dial was the owner's call and not the feature's, so the guard has to move when
        /// he makes it, or it guards a preference he no longer holds.
        /// </summary>
        [Test]
        public void TheTreeLightResponse_IsOn_ByOwnerRuling()
        {
            Assert.AreEqual(1f, Load(TreeMaterialPath).GetFloat("_LightResponse"), 1e-6f,
                "🔴 Tree.mat's light response is not 1. The owner ruled on 2026-09-03 that the trees " +
                "must read the sun by day; at 0 the shader's whole light block is compiled past with " +
                "two scalar compares and a planted forest is flat at every hour of the day.");
        }

        /// <summary>
        /// <b>MEASURED, not assumed: a canopy needs no canopy-special numbers.</b> The brief expected the
        /// shrub dials to read "invisible or plastic" on a 442 px crown. They do neither, and the reason
        /// is that the response is PER TEXEL and therefore scale-free — a spruce is not a big shrub to
        /// this shader, it is more texels of the same shrub.
        ///
        /// <para>Measured on the pass-3 sheets, sun catch as a fraction of the texel's own albedo
        /// luminance at 13:00: shore plants 0.35–0.71 (Bayberry 0.71), shrubs 0.23–0.56 — both families
        /// SHIPPED at <c>_LightResponse 1</c> and accepted by the owner — and the trees at the same
        /// dials 0.54–0.69. The trees land INSIDE the accepted range. Raising the strength for the
        /// canopy would have made the woods the brightest foliage in the game.</para>
        ///
        /// <para>So the dials are pinned EQUAL across the three families rather than pinned to numbers:
        /// what matters is that one lighting model lights the coast, and that a future re-tune moves all
        /// of it or is a deliberate, visible exception.</para>
        /// </summary>
        [Test]
        public void TheTreeSpendsTheSameSunDials_AsEveryFamilyOnTheSharedPath()
        {
            Material tree = Load(TreeMaterialPath), shrub = Load(ShrubMaterialPath), plant = Load(PlantMaterialPath);

            foreach (string dial in new[] { "_SunKeyStrength", "_SunRimStrength", "_KeyRelight",
                                            "_RimSteerAmount", "_LightFrontBand", "_LightDepthBias" })
            {
                Assert.AreEqual(shrub.GetFloat(dial), tree.GetFloat(dial), 1e-6f,
                    $"Tree.mat's {dial} has drifted from LitShrub.mat's. The sun catch is a per-texel " +
                    "response, so a crown is not a big shrub to it — measured, the trees at the shrub " +
                    "dials sit inside the range the shore plants and shrubs already ship at. A tree-only " +
                    "value is a deliberate look exception and needs to be argued, not inherited.");
                Assert.AreEqual(shrub.GetFloat(dial), plant.GetFloat(dial), 1e-6f,
                    $"LitShorePlant.mat's {dial} has drifted from LitShrub.mat's.");
            }

            Assert.AreEqual(shrub.GetColor("_SunKeyColor"), tree.GetColor("_SunKeyColor"),
                "Tree.mat's sun colour has drifted from the shared one. One sun, one colour: two would " +
                "read as two suns wherever a shrub stands under a tree.");
        }

        // =========================================================================================
        //  2. THE LIT SIDE SWINGS
        // =========================================================================================

        /// <summary>
        /// <b>The sun genuinely changes SIDE across the day.</b> The catch is <c>pow(max(0, n·l), 1.35)</c>
        /// against a view-space normal, so "which side is lit" is decided by the sign of the light's
        /// view-space x. This walks the shipped day and asserts that a crown texel facing screen-LEFT and
        /// one facing screen-RIGHT swap which of them is brighter between morning and evening.
        ///
        /// <para>⚠️ Note what is NOT asserted: that the two halves of a crown differ by much. Measured on
        /// the real sheets, the mean over a fixed left half barely moves (~5 % of the catch) while the
        /// catch's own CENTROID sweeps 15 px of a 269 px white pine and 30 px of a 331 px red oak. A
        /// moving highlight is not measured by a fixed split — the mean over half a crown averages the
        /// gradient away. Per-normal is the honest instrument, and it is what the shader does.</para>
        /// </summary>
        [Test]
        public void TheLitSideOfACrown_SwapsSidesBetweenMorningAndEvening()
        {
            // Two crown texels of the same material, turned to opposite flanks, both tilted toward the
            // camera the way the pass-3 normals actually sit (measured nz: mean +0.71, p5 +0.26).
            var facingLeft = new Vector3(-0.7f, 0.1f, 0.7f).normalized;
            var facingRight = new Vector3(0.7f, 0.1f, 0.7f).normalized;

            float Catch(float hour, Vector3 n)
            {
                Vector2 g = Ground(hour);
                Assert.Greater(g.sqrMagnitude, 1e-6f,
                    "The sun's ground direction came out degenerate, so the cycle-off fallback would " +
                    "take over and this measurement would not be of the sun under test.");
                float elev = DayNightMath.SunElevation(hour, Sunrise, Sunset);
                Vector3 l = SpriteLightMath.SunDirection(g, elev, out float amount);
                return SpriteLightMath.KeyResponse(
                    maskKey: 0.33f, maskDepth: 0.61f, maskCoverage: 1f,   // measured pass-3 crown means
                    normal: n, lightDir: l, relight: 0.75f,
                    frontBand: SpriteLightMath.DefaultFrontBand, depthBias: 0.45f) * amount;
            }

            float mornL = Catch(8.5f, facingLeft), mornR = Catch(8.5f, facingRight);
            float eveL = Catch(17.5f, facingLeft), eveR = Catch(17.5f, facingRight);

            Assert.Greater(mornR, mornL,
                "In the MORNING the sun is in the east — screen right under this camera — and the " +
                "right flank of a crown must be the lit one.");
            Assert.Greater(eveL, eveR,
                "In the EVENING the sun is in the west and the LEFT flank must be the lit one. If this " +
                "fails while the morning passes, the catch is not swinging: it is the rig's own baked " +
                "key showing through, which is what _KeyRelight below 1 deliberately leaves some of.");

            // And it is a real reversal, not two near-ties. Measured: 0.364 on the lit flank against
            // 0.046 on the dark one — an 8x ratio, not a nudge.
            Assert.Greater(mornR, mornL * 4f,
                "The lit flank is less than 4x the dark one, so the crown is barely turning. The " +
                "measured shipped ratio at 08:30 is about 8x; anything near 1 means the baked key is " +
                "carrying the read and the live sun is not.");

            // 🔴 The two hours are EXACT MIRRORS, so this is asserted at 1e-6 and not with slack.
            // SolarX is symmetric about solar noon, so 17:30's ground direction is 08:30's negated in x
            // exactly (float negation is exact); LightViewDirection carries x through untouched and
            // derives y and z from the other two components alone; and the two test normals mirror in x,
            // so every dot product is (-a)(-b) = ab — equal to the last bit. A percentage tolerance here
            // would be slack sitting on a quantity that cannot legitimately differ, which is how a guard
            // ends up tolerating the drift it was written to catch.
            Assert.AreEqual(mornR, eveL, 1e-6f,
                "The morning's lit flank and the evening's are not mirror images. Something un-mirrored " +
                "— the rig's baked key, the back rim band — is carrying the swing instead of the sun.");
            Assert.AreEqual(mornL, eveR, 1e-6f,
                "The morning's dark flank and the evening's are not mirror images.");
        }

        /// <summary>
        /// <b>The lit side and the shadow read ONE sun.</b> They are computed in different places — the
        /// catch in the fragment stage off <c>_SunDir</c>, the shadow's shear in
        /// <see cref="SpriteShadow"/> off <see cref="DayNightMath.ShadowDirection"/> — so nothing but a
        /// test stops them drifting into two suns, which would read as a tree lit from one side and
        /// shadowed from the same side.
        /// </summary>
        [Test]
        public void TheCatchAndTheCastShadow_ComeFromExactlyOneSunDirection()
        {
            for (float hour = 6f; hour <= 20f; hour += 0.5f)
            {
                Vector2 sun = DayNightMath.SunDirection(hour, Sunrise, Sunset, SouthBias, NoonLift);
                Vector2 shadow = DayNightMath.ShadowDirection(hour, Sunrise, Sunset, SouthBias, NoonLift);
                Assert.AreEqual(-sun.x, shadow.x, 1e-6f, $"At {hour:00.0}h the shadow is not opposite the sun in x.");
                Assert.AreEqual(-sun.y, shadow.y, 1e-6f, $"At {hour:00.0}h the shadow is not opposite the sun in y.");
            }
        }

        // =========================================================================================
        //  3. THE WEATHER AGREEMENT — the defect this PR fixes
        // =========================================================================================

        /// <summary>
        /// 🔴 <b>The defect, stated as a measurement.</b> Before this PR the sun catch was gated on
        /// <c>saturate(elevation)</c> alone while the cast shadow was gated on <c>_ShadowStrength</c>,
        /// which also folds the weather. Under the shipped profile's heaviest storm that is a lit side
        /// at 1.00 over a shadow at 0.49 — half the shadow gone and the light that cast it still full.
        ///
        /// <para>This asserts the OLD reading really was wrong (so the fix is not decoration) and the new
        /// one really does agree.</para>
        /// </summary>
        [Test]
        public void UnderCloud_TheSunCatchAndTheCastShadow_FadeByTheSameNumber()
        {
            const float hour = 13f;
            Vector2 g = Ground(hour);
            float elev = DayNightMath.SunElevation(hour, Sunrise, Sunset);
            SpriteLightMath.SunDirection(g, elev, out float elevationOnly);

            foreach (float weatherDim in new[] { 0.2f, 0.4f, WeatherDimMax })
            {
                float strength = DayNightMath.ShadowStrength(hour, Sunrise, Sunset, weatherDim, OvercastFadesShadow);
                float amount = SpriteLightMath.SunAmount(g, elevationOnly, strength);

                Assert.AreEqual(strength, amount, 1e-6f,
                    $"At weatherDim {weatherDim} the sun catch and the cast shadow are spending " +
                    "different numbers. They must be the SAME published _ShadowStrength — one publisher, " +
                    "no second read of the sim.");
                Assert.Less(amount, elevationOnly - 1e-3f,
                    $"At weatherDim {weatherDim} the catch did not fade at all. That is the pre-fix " +
                    "reading (elevation only): a full lit side under a faded shadow.");
                // And the shadow's own alpha is that same number, so the two are one dial in practice.
                Assert.AreEqual(DayNightMath.ShadowAlpha(1f, amount),
                                DayNightMath.ShadowAlpha(1f, strength), 1e-6f,
                    "The shadow alpha and the sun amount diverged.");
            }
        }

        /// <summary>
        /// 🔴 <b>A clear day is BIT-identical, not merely close.</b> The include is SHARED — the shrubs
        /// and the shoreline plants render through it too, and they were placed and approved against the
        /// elevation-only reading. <c>weatherDim 0</c> makes the weather factor exactly <c>1f</c> and
        /// <c>x * 1f</c> is the IEEE 754 multiplicative identity, so nothing on a clear day moves by a
        /// bit. Asserted with <c>==</c> on the raw floats across the whole shipped day, not with a
        /// tolerance: a tolerance here would hide exactly the drift it exists to catch.
        /// </summary>
        [Test]
        public void OnAClearDay_TheWeatherFold_IsBitIdenticalToTheReadingItReplaces()
        {
            for (float hour = 0f; hour < 24f; hour += 0.25f)
            {
                Vector2 g = Ground(hour);
                float elev = DayNightMath.SunElevation(hour, Sunrise, Sunset);
                SpriteLightMath.SunDirection(g, elev, out float elevationOnly);
                float clear = DayNightMath.ShadowStrength(hour, Sunrise, Sunset, 0f, OvercastFadesShadow);

                Assert.IsTrue(SpriteLightMath.SunAmount(g, elevationOnly, clear) == elevationOnly,
                    $"At {hour:00.00}h a CLEAR sky changed the sun catch. The shrubs and the shoreline " +
                    "plants share this include and were approved against the old reading; on a clear " +
                    "day they must be bit-identical, and _ShadowStrength is saturate(elevation) exactly " +
                    "when the weather factor is 1f.");
            }
        }

        /// <summary>
        /// <b>Off the cycle the fallback sun survives.</b> <see cref="DayNightController"/> publishes
        /// <c>_SunDir</c>, <c>_SunElevation</c> and <c>_ShadowStrength</c> at RUNTIME only; in edit mode
        /// and bare art scenes all three read 0. Taking <c>_ShadowStrength</c> at face value there would
        /// multiply the fallback mid-morning sun by zero and leave the owner tuning a response that is
        /// dead in the scene view — the exact failure the fallback exists to prevent.
        /// </summary>
        [Test]
        public void OffTheCycle_TheFallbackSunIsNotKilledByAnUnpublishedShadowStrength()
        {
            SpriteLightMath.SunDirection(Vector2.zero, 0f, out float fallback);
            Assert.Greater(fallback, 0f, "The cycle-off fallback sun is not lit at all.");

            Assert.AreEqual(fallback, SpriteLightMath.SunAmount(Vector2.zero, fallback, 0f), 1e-6f,
                "An unpublished _ShadowStrength (0) killed the cycle-off fallback sun. The unset test " +
                "must be made on _SunDir — the same global SunDirection tests — so the pair can never " +
                "disagree about whether the cycle is running.");
        }

        // =========================================================================================
        //  4. THE SHADER SPENDS THE SAME NUMBER (structural — the twin above is only a twin)
        // =========================================================================================

        /// <summary>
        /// The C# above is a TWIN; this is what pins the shader to it. Three reads, each naming a way the
        /// two could drift apart while every arithmetic test still passed.
        /// </summary>
        [Test]
        public void TheSharedIncludeSpendsTheShadowStrengthGlobal_OnTheSunCatch()
        {
            string response = File.ReadAllText(ResponseIncludePath);
            StringAssert.Contains("float SpriteLightSunAmount(", response,
                $"'{ResponseIncludePath}' has lost SpriteLightSunAmount — the shader-side twin of " +
                "SpriteLightMath.SunAmount. Without it the sun catch is back to elevation only.");

            string decor = File.ReadAllText(DecorIncludePath);
            StringAssert.Contains("float  _ShadowStrength;", decor,
                $"'{DecorIncludePath}' no longer declares _ShadowStrength. It is a Shader.SetGlobalFloat " +
                "published by DayNightController and must be declared OUTSIDE UnityPerMaterial — a " +
                "global folded into the batched block breaks the SRP batcher's layout for every decor " +
                "material.");
            StringAssert.Contains("SpriteLightSunAmount(_SunDir.xy, sunUp, _ShadowStrength)", decor,
                "The shared response no longer folds the weather into the sun's amount.");
            StringAssert.Contains("p.sunKeyColor * sunAmount", decor,
                "The sun's additive is scaled by something other than the weather-folded amount. " +
                "Scaling by the raw sunUp is the pre-fix reading: a full lit side under a faded shadow.");
        }
    }
}
