using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE PATCH OF GROUND A LAMP MAKES BRIGHTER</b> (ADR 0016 amendment, world-lighting PR 2c) — the
    /// maths headless, the ladder pinned, and the rules about which lamps pool at all.
    ///
    /// <para>#733 took the disc off the SOURCE, on the owner's ruling that a lamp should <i>"glow from within
    /// the lamp"</i>, and left the pier honestly dark. This is the other half: what the lamp does to the
    /// ground. It is a different picture of the same lamp, and the numbers below are what stop it becoming
    /// the disc again by another route.</para>
    /// </summary>
    public class LampPoolTests
    {
        private const float Eps = 1e-4f;

        // ---- the shape: a lamp is a POINT at a HEIGHT ------------------------------------------------

        /// <summary>
        /// <b>Directly under the lamp the ground is struck square-on; out at the rim it is grazed.</b>
        /// <c>h/√(h²+d²)</c> is the cosine between the lamp's ray and the ground's normal, and it is the
        /// whole reason a pool has a shape rather than being a disc: the sun is a direction at infinity and
        /// strikes every pixel of ground alike, a lamp does not.
        /// </summary>
        [Test]
        public void GroundIncidence_IsOneUnderTheLamp_AndFallsAwayWithDistance()
        {
            Assert.AreEqual(1f, LightMath.GroundIncidence(2.5f, 0f), Eps, "straight down is square-on");

            // At d = h the ray is at 45°: exactly 1/√2.
            Assert.AreEqual(0.70710678f, LightMath.GroundIncidence(2.5f, 2.5f), Eps);
            Assert.AreEqual(0.70710678f, LightMath.GroundIncidence(7.8f, 7.8f), Eps, "scale-free, as a cosine is");

            float prev = float.MaxValue;
            for (float d = 0f; d <= 12f; d += 0.5f)
            {
                float v = LightMath.GroundIncidence(3f, d);
                Assert.LessOrEqual(v, prev + Eps, $"incidence must never rise with distance (d={d})");
                prev = v;
            }
            Assert.Less(LightMath.GroundIncidence(3f, 100f), 0.05f, "far away, a lamp barely grazes the ground");
        }

        /// <summary>
        /// <b>A TALL lamp pools broad and even; a LOW one pools tight and drops away.</b> Nothing tunes this —
        /// it is the same one line — and it is what makes a 7.8 m flood mast and a 2.46 m lantern post read
        /// as different KINDS of light rather than as the same disc at two sizes.
        /// </summary>
        [Test]
        public void ATallLampPoolsFlatter_AndALowOneDropsAwayFaster()
        {
            const float d = 3f;
            float lantern = LightMath.GroundIncidence(2.46f, d);   // wharfDecor lanternPost
            float mast = LightMath.GroundIncidence(7.8f, d);       // utilityIso floodMast

            Assert.Greater(mast, lantern,
                "at the same ground distance the taller lamp strikes more squarely — that is the whole " +
                "difference between a mast lighting a yard and a lantern lighting its own feet");

            // And the drop-off over the same span is steeper for the low lamp.
            float lanternDrop = LightMath.GroundIncidence(2.46f, 1f) - LightMath.GroundIncidence(2.46f, 4f);
            float mastDrop = LightMath.GroundIncidence(7.8f, 1f) - LightMath.GroundIncidence(7.8f, 4f);
            Assert.Greater(lanternDrop, mastDrop, "the low lamp's pool has the harder edge, from geometry alone");
        }

        /// <summary>
        /// <b>A lamp that will not say how high it is draws NOTHING.</b> Zero is not "assume 2.5 m" and it is
        /// certainly not "draw a flat disc" — a disc is precisely the picture the owner refused, and falling
        /// back to it on missing data is how it would come back.
        /// </summary>
        [Test]
        public void ALampWithNoHeight_HasNoOpinionAboutTheGround()
        {
            Assert.AreEqual(0f, LightMath.GroundIncidence(0f, 0f), Eps);
            Assert.AreEqual(0f, LightMath.GroundIncidence(0f, 3f), Eps);
            Assert.AreEqual(0f, LightMath.PoolGain(baseGain: 5f, lampHeightMetres: 0f,
                                                   groundDistanceMetres: 0f, reachMetres: 3.6f,
                                                   edgeSoftness: 0.5f), Eps,
                "no height ⇒ no gain anywhere, at any brightness");
        }

        // ---- the edge and the gate --------------------------------------------------------------------

        [Test]
        public void PoolFalloff_IsOneInsideTheSoftBand_AndZeroAtTheReach()
        {
            // softness 0.5 on a 4 m reach: full out to 2 m, gone by 4 m.
            Assert.AreEqual(1f, LightMath.PoolFalloff(0f, 4f, 0.5f), Eps);
            Assert.AreEqual(1f, LightMath.PoolFalloff(2f, 4f, 0.5f), Eps);
            Assert.AreEqual(0f, LightMath.PoolFalloff(4f, 4f, 0.5f), Eps, "a pool ends at its reach");
            Assert.AreEqual(0f, LightMath.PoolFalloff(40f, 4f, 0.5f), Eps);

            float mid = LightMath.PoolFalloff(3f, 4f, 0.5f);
            Assert.Greater(mid, 0f); Assert.Less(mid, 1f);

            // A hard edge is available and is what a decal looks like; the shipped profile does not use it.
            Assert.AreEqual(1f, LightMath.PoolFalloff(3.99f, 4f, 0f), Eps, "softness 0 = a hard rim");
        }

        /// <summary>
        /// <b>A round lamp lights the ground the same way in every direction: no slice is missing.</b> A radial
        /// lamp ships cosHalf −1 and its axis up the screen; fed through the cone's feather, the ground straight
        /// south of it (cos −1) came out dark. The twin and the shader both branch the radial case to 1.
        /// </summary>
        [Test]
        public void ARoundLampLightsTheGroundOnEverySide_ItsSouthToo()
        {
            const float soft = 0.35f;   // the shipped PoolEdgeSoftness
            for (int deg = 0; deg < 360; deg += 15)
            {
                float cosAngle = Mathf.Cos(deg * Mathf.Deg2Rad);
                Assert.AreEqual(1f, LightMath.PoolCone(cosAngle, -1f, soft), Eps, $"radial lamp at {deg} deg from its axis");
            }

            // An aimed lamp still pools a feathered wedge.
            float cosHalf = Mathf.Cos(30f * Mathf.Deg2Rad);
            Assert.AreEqual(1f, LightMath.PoolCone(1f, cosHalf, soft), Eps, "on the beam's axis");
            Assert.AreEqual(0f, LightMath.PoolCone(Mathf.Cos(40f * Mathf.Deg2Rad), cosHalf, soft), Eps, "outside the beam");
            Assert.AreEqual(0f, LightMath.PoolCone(-1f, cosHalf, soft), Eps, "behind the beam");

            string shader = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,
                "_Project/Art/Shaders/HiddenHarboursLampPool.shader"));
            StringAssert.Contains("cosHalf <= -0.9999 ? 1.0 :", shader, "the shader keeps the twin's radial branch");
            Assert.AreEqual(-0.9999f, LightMath.RadialCosHalf, 0f, "the twin's radial threshold matches the shader's");
        }

        // ---- a lamp lights the FACES of a mesh it can see -------------------------------------------

        private const float Elev = 40f;
        private static readonly float Se = Mathf.Sin(Elev * Mathf.Deg2Rad), Ce = Mathf.Cos(Elev * Mathf.Deg2Rad);
        private const float RefY = -60f, BaseZ = 0.3f, HullY = 12f;

        private static float Shear => HiddenHarbours.Art.DisplacedWaterMath.HullDepthShear(Elev);
        private static Vector4 Plane => LightMath.HullHeightPlane(RefY, Ce, Se, BaseZ, Shear);

        // A point of a hull moored at world y HullY, `north` metres along the ground and `up` metres above the
        // still sea, as the facet pass draws it: the rig's projection rows (y = n·sin + h·cos, z = n·cos −
        // h·sin) plus her calibrated root constant (HullDepthBias with ADR 0033's shear compensation).
        private static Vector3 HullWorld(float east, float north, float up)
        {
            float rootZ = BaseZ + (HullY - RefY) * (Ce + Shear);
            return new Vector3(east, HullY + north * Se + up * Ce, rootZ + north * Ce - up * Se);
        }

        private static Vector3 Point(Vector3 w) => LightMath.HullPoint(w.x, w.y, w.z, Plane, Ce);

        /// <summary>
        /// <b>The hull depth gives back each pixel's true height above the sea, and the ground point under
        /// it.</b> The pools light a mesh in their own frame (world x, the world y of the ground under the
        /// pixel, metres up), so the plane must undo the facet pass's projection exactly — heading-free.
        /// </summary>
        [Test]
        public void AHullPixelsDepthGivesItsHeightAboveTheSeaAndTheGroundUnderIt()
        {
            foreach (var (n, h) in new[] { (0f, 0f), (3f, 0f), (-4f, 1.2f), (2.5f, 3.4f), (-1f, 7.8f) })
            {
                Vector3 p = Point(HullWorld(1f, n, h));
                Assert.AreEqual(h, p.z, 1e-3f, $"height at north {n}, up {h}");
                Assert.AreEqual(HullY + n * Se, p.y, 1e-3f, "the ground under her is where her plan draws");
            }
        }

        /// <summary>
        /// <b>The face at a pixel is rebuilt from its neighbours, and it faces the camera.</b> A deck reads
        /// straight up, a wall turned toward the camera reads toward the camera's side, whichever order the
        /// neighbours come in.
        /// </summary>
        [Test]
        public void AFaceIsRebuiltFromTheNeighbouringPixels_AndTurnedToTheCamera()
        {
            Vector3 view = LightMath.HullViewDirection(Plane, Ce);
            Vector3 c = Point(HullWorld(0f, 0f, 1f));

            // A deck: one pixel across is a step east; one pixel down-screen is a step south on the deck.
            Vector3 ax = Point(HullWorld(0.03f, 0f, 1f)) - c, ay = Point(HullWorld(0f, -0.05f, 1f)) - c;
            Vector3 deck = LightMath.HullFaceNormal(ax, ay, view);
            Assert.AreEqual(1f, deck.z, 1e-3f, "a deck faces up");
            Assert.AreEqual(deck, LightMath.HullFaceNormal(ay, ax, view), "order of the taps does not matter");

            // The south wall of a cabin: down-screen is down the wall.
            Vector3 wy = Point(HullWorld(0f, 0f, 0.95f)) - c;
            Vector3 wall = LightMath.HullFaceNormal(ax, wy, view);
            Assert.AreEqual(-1f, wall.y, 1e-3f, "a wall the camera sees faces south, toward the camera");
        }

        /// <summary>
        /// <b>A lamp lights the faces that look at it and none that look away.</b> The cabin's own deck near a
        /// lamp lifts; the wall turned from it, and a roof above it, stay dark; and a deck seen square under
        /// the lamp reads exactly the ground pool's number, because the ground is the same sum with its
        /// normal straight up.
        /// </summary>
        [Test]
        public void ALampLightsTheFacesThatLookAtIt_AndNotTheOnesThatLookAway()
        {
            const float gain = 2f, reach = 4f, soft = 0.55f;
            Vector2 foot = new Vector2(0f, HullY);
            const float lampUp = 2.2f;
            Vector3 up = Vector3.forward, south = Vector3.down, north = Vector3.up;

            Vector3 deckNear = Point(HullWorld(0.8f, -0.5f, 0.9f));
            float lit = LightMath.HullFaceGain(gain, up, deckNear, foot, lampUp, reach, soft, -1f, Vector2.up);
            Assert.Greater(lit, 0.5f, "the deck beside the lamp lifts");

            Vector3 roof = Point(HullWorld(0.5f, 1f, 2.6f));
            Assert.AreEqual(0f, LightMath.HullFaceGain(gain, up, roof, foot, lampUp, reach, soft, -1f, Vector2.up),
                            1e-6f, "a roof above the lamp is not lit from below");

            // A wall north of the lamp, facing north (away from it): the far side of the cabin.
            Vector3 farWall = Point(HullWorld(0f, 1.2f, 1.5f));
            Assert.AreEqual(0f, LightMath.HullFaceGain(gain, north, farWall, foot, lampUp, reach, soft, -1f, Vector2.up),
                            1e-6f, "the far side of the cabin stays dark");
            Assert.Greater(LightMath.HullFaceGain(gain, south, farWall, foot, lampUp, reach, soft, -1f, Vector2.up), 0f,
                           "the same wall's lamp-side face is lit");

            // The ground is the special case: height 0, normal straight up.
            for (float d = 0f; d < reach; d += 0.5f)
            {
                var ground = new Vector3(d, HullY, 0f);
                float face = LightMath.HullFaceGain(gain, up, ground, foot, lampUp, reach, soft, -1f, Vector2.up);
                float pool = LightMath.PoolGain(gain, lampUp, d, reach, soft);
                Assert.AreEqual(pool, face, 1e-5f, $"at {d} m the ground reads the ground pool's number");
            }
        }

        static string ShaderText(string file) => System.IO.File.ReadAllText(System.IO.Path.Combine(
            Application.dataPath, "_Project/Art/Shaders/" + file)).Replace("\r\n", "\n");

        /// <summary>The hull's own overlay keeps the twin's face path: the published depth, the height plane,
        /// the face from three taps, and N·L toward the fitting. It lives in the OVERLAY because the overlay
        /// draws at her place in the sort: a pixel of her that something stands in front of is never hers to
        /// light (the dory behind the St Peters dock, 2026-11-25).</summary>
        [Test]
        public void TheHullOverlayLightsAMeshPixelByItsFace_AsTheTwinDoes()
        {
            string shader = ShaderText("HiddenHarboursIsoFacetOverlay.shader");
            StringAssert.Contains("_HHHullDepthTex", shader);
            StringAssert.Contains("_HHHullDepthLive < 0.5", shader);
            StringAssert.Contains("_HHLampHullPlane.y * world.y + _HHLampHullPlane.z * z + _HHLampHullPlane.w", shader);
            StringAssert.Contains("world.y - h * _HHLampFaceScale", shader);
            StringAssert.Contains("float3 n = cross(sx, sy);", shader);
            StringAssert.Contains("if (dot(n, view) > 0.0) n = -n;", shader);
            StringAssert.Contains("saturate(dot(n, toLamp) / dl)", shader);
            // what she carries is face over ground, so the pool's ground multiply leaves her lit by her face
            StringAssert.Contains("ratio *= (1.0 + col * f) / (1.0 + col * g);", shader);
            StringAssert.Contains("c.rgb *= HHLampFaceRatio(", shader);
            StringAssert.Contains("#define HH_LAMP_FACE_MAX 32", shader);
            Assert.AreEqual(32, LampPoolSystem.MaxFaceLamps, "the arrays and the shader disagree on their size");
        }

        /// <summary>The ceiling pool lights every pixel as GROUND: it never reads the hull pass, which holds her
        /// even where the dock is drawn over her.</summary>
        [Test]
        public void ThePoolLightsEveryPixelAsGround_AndNeverReadsTheHullPass()
        {
            string pool = ShaderText("HiddenHarboursLampPool.shader");
            StringAssert.DoesNotContain("_HHHullDepthTex", pool);
            StringAssert.DoesNotContain("_HHHullScreenTex", pool);
            StringAssert.Contains("float incidence = h * rsqrt(h * h + d * d);", pool);
        }

        /// <summary>The pool's term is written twice, once per shader. The two must be the same text, or the
        /// overlay's ground term would not cancel the pool's and every hull near a lamp would change.</summary>
        [Test]
        public void ThePoolGainIsTheSameTextInThePoolAndTheOverlay()
        {
            string Body(string s)
            {
                int a = s.IndexOf("float HHLampPoolGain(", System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(a, 0, "HHLampPoolGain is missing");
                int b = s.IndexOf("return gain <= 1e-4 ? 0.0 : gain;", a, System.StringComparison.Ordinal);
                Assert.Greater(b, a, "HHLampPoolGain has no end");
                return s.Substring(a, b - a);
            }
            Assert.AreEqual(Body(ShaderText("HiddenHarboursLampPool.shader")),
                            Body(ShaderText("HiddenHarboursIsoFacetOverlay.shader")));
        }

        /// <summary>
        /// <b>The night gate is a factor, so a pool is a night thing by the same machinery every other light
        /// in the project gates on.</b> No preset, no profile and no pool may reach around it.
        /// </summary>
        [Test]
        public void PoolGain_IsGatedByTheNight_AndCappedSoItCannotFlattenTheGround()
        {
            const float night = 0.134f;   // the shipped 02:00 tint's luminance, measured on the pier
            float lit = LightMath.PoolBaseGain(0.6f, 1.3f, nightGate: 1f, tintLuminance: night);
            Assert.Greater(lit, 0f);
            Assert.AreEqual(0f, LightMath.PoolBaseGain(0.6f, 1.3f, 0f, night), Eps,
                "by day a lamp does nothing to the ground");
            Assert.AreEqual(lit * 0.5f, LightMath.PoolBaseGain(0.6f, 1.3f, 0.5f, night), Eps,
                "and it fades through dusk rather than switching");

            Assert.LessOrEqual(LightMath.PoolBaseGain(1f, 100f, 1f, 0.001f), LightMath.MaxPoolGain + Eps,
                "a runaway intensity over a near-black tint must not be able to clip the ground to white — " +
                "that is the flattening the additive disc was refused for, arriving through the other door");
        }

        /// <summary>
        /// <b>⚠⚠ THE DIVISION BY THE NIGHT IS WHAT MAKES THE POOL VISIBLE AT ALL, and leaving it out
        /// produced a pass that was working and invisible.</b> A multiply is bounded by what it multiplies,
        /// and ADR 0013's tint has crushed the pier to a mean luminance around 0.04 by the time the pool
        /// draws — so a naive <c>dst × 1.6</c> lifts a plank by six values in 255. Measured on the real pier
        /// before this existed: ZERO pixels changed.
        ///
        /// <para>The factor that reconstructs <c>albedo × (ambient + lamp)</c> from a frame holding
        /// <c>albedo × ambient</c> is exactly <c>1 + lamp/ambient</c> — so the DARKER the night, the LARGER
        /// the multiplier, and the lit ground lands in the same place either way. That is the property this
        /// pins.</para>
        /// </summary>
        [Test]
        public void TheGain_RisesAsTheNightDeepens_SoTheLitGroundLandsInTheSamePlace()
        {
            const float strength = 0.6f, intensity = 1.3f, albedo = 0.5f;
            float dusk = LightMath.PoolBaseGain(strength, intensity, 1f, tintLuminance: 0.40f);
            float night = LightMath.PoolBaseGain(strength, intensity, 1f, tintLuminance: 0.134f);
            float moonless = LightMath.PoolBaseGain(strength, intensity, 1f, tintLuminance: 0.04f);

            Assert.Less(dusk, night, "a deeper night needs a bigger multiplier to reach the same light");
            Assert.Less(night, moonless);

            // ⭐ THE INVARIANT is the LAMP'S OWN CONTRIBUTION — albedo × tint × gain — which the division
            // makes independent of the tint: it comes out as albedo × strength × intensity, every time.
            // (What the pool sits IN still changes with the night, and should: dusk has more ambient in it.)
            float LampAdds(float tint, float gain) => albedo * tint * gain;
            Assert.AreEqual(albedo * strength * intensity, LampAdds(0.40f, dusk), 1e-4f);
            Assert.AreEqual(albedo * strength * intensity, LampAdds(0.134f, night), 1e-4f,
                "the lamp puts the same light on the same planks at dusk and at midnight — the night " +
                "changes what is AROUND the pool, not the pool");

            // ⚠ And the CAP deliberately breaks that identity in the dark, which is the trade it exists to
            // make: past MaxPoolGain a lamp stops reaching for a factor of fifty and settles for eight.
            Assert.AreEqual(LightMath.MaxPoolGain, moonless, Eps, "a moonless night hits the ceiling");
            Assert.Less(LampAdds(0.04f, moonless), albedo * strength * intensity,
                "so a moonless pool IS dimmer than a moonlit one — stated, because the alternative is a " +
                "runaway multiplier clipping the ground to white");

            Assert.AreEqual(LightMath.PoolBaseGain(strength, intensity, 1f, LightMath.MinPoolAmbientLuminance),
                            LightMath.PoolBaseGain(strength, intensity, 1f, 0f), Eps,
                "a tint of zero is a frame with no cycle running, not an infinitely dark one — the floor " +
                "stops the division exploding");
        }

        /// <summary>
        /// <b>⭐ THE CLAIM THE WHOLE DESIGN RESTS ON: a multiply cannot flatten what it lights.</b> The pass
        /// is <c>Blend DstColor One</c>, i.e. <c>dst × (1 + gain)</c>. Relative contrast is a RATIO — a step
        /// between neighbours over the local mean — and a uniform scale multiplies both of its terms, so it
        /// cancels exactly. Two planks and the dark gap between them come back in the same proportion they
        /// went in, however bright the pool. An additive term cannot make that promise at any strength that
        /// reads, and that difference is the entire reason this is not the disc again.
        /// </summary>
        [Test]
        public void AMultiplyPreservesContrast_WhichIsWhyThePlanksSurviveIt()
        {
            // A scrap of plank: light board, dark seam, light board.
            float[] ground = { 0.42f, 0.11f, 0.44f, 0.10f, 0.40f };
            float baseGain = LightMath.PoolBaseGain(0.6f, 1.3f, 1f, 0.134f);
            float gain = LightMath.PoolGain(baseGain, 2.5f, 0.5f, 3.6f, 0.55f);
            Assert.Greater(gain, 0.1f, "the fixture needs a pool that actually lights, or it proves nothing");

            float ContrastOf(float[] v)
            {
                float step = 0f, mean = 0f;
                for (int i = 0; i < v.Length; i++)
                {
                    mean += v[i];
                    if (i > 0) step += Mathf.Abs(v[i] - v[i - 1]);
                }
                return step / mean;
            }

            var lit = new float[ground.Length];
            for (int i = 0; i < ground.Length; i++) lit[i] = ground[i] * (1f + gain);

            Assert.AreEqual(ContrastOf(ground), ContrastOf(lit), 1e-5f,
                "the multiply must leave relative local contrast EXACTLY where it found it");

            // The additive arm, for contrast — the same light energy added instead of scaled.
            var added = new float[ground.Length];
            for (int i = 0; i < ground.Length; i++) added[i] = ground[i] + gain * 0.3f;
            Assert.Less(ContrastOf(added), ContrastOf(ground) * 0.85f,
                "and an ADDITIVE term of the same order flattens it — which is what the owner was looking at");
        }

        // ---- the ladder ---------------------------------------------------------------------------------

        /// <summary>
        /// <b>The three lamp quads draw in the only order that composes.</b> All three sit at the same
        /// sorting order, so the 2D renderer breaks the tie back-to-front along the view axis and the DEPTH
        /// PINS are the whole ordering. Farther draws first:
        /// <list type="number">
        ///   <item>the POOL multiplies the ground up;</item>
        ///   <item>the BLOOM adds the lit fitting, so the lamp itself stays the hottest thing in frame;</item>
        ///   <item>the SHADOWS multiply back down, so a bollard's shadow is cut into the light the pool just
        ///         laid — which is what makes them one picture instead of two.</item>
        /// </list>
        /// Get this backwards and a shadow lands under the pool that is supposed to erase it.
        /// </summary>
        [Test]
        public void TheDepthPins_PutThePoolUnderTheBloom_AndTheShadowsOverBoth()
        {
            Assert.Greater(LampPoolSystem.PoolDepthOffset, SceneLight.DefaultCameraDepthOffset,
                "the pool must be FARTHER from the camera than the bloom, so it draws first");
            Assert.Greater(SceneLight.DefaultCameraDepthOffset, LampShadowSystem.ShadowDepthOffset,
                "and the shadows nearest of all, so they draw last and multiply both");

            Assert.AreEqual(0.14f, LampPoolSystem.PoolDepthOffset, 1e-6f);
        }

        // ---- which lamps pool at all --------------------------------------------------------------------

        /// <summary>
        /// <b>A lamp pools only if it says it lights the ground AND says from how high.</b> Both are real
        /// answers rather than missing ones: a boat's sidelight is a SIGNAL and lighting the sea under it
        /// would be a lie about what it is for, and a lamp with no height cannot be shaped.
        /// </summary>
        [Test]
        public void OnlyALampThatClaimsAReachAndAHeight_CastsAPool()
        {
            var go = new GameObject("poolCandidate") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var light = go.AddComponent<SceneLight>();
                light.Intensity = 1f;
                light.LampHeightMeters = 2.5f;
                light.ReachMetres = 3.6f;

                Assert.IsTrue(LampPoolSystem.PoolsLight(light, 0.1f, true, out float gate));
                Assert.Greater(gate, 0f);

                light.ReachMetres = 0f;
                Assert.IsFalse(LampPoolSystem.PoolsLight(light, 0.1f, true, out _),
                    "no reach ⇒ no pool: a sidelight is a signal, not a floodlight");

                light.ReachMetres = 3.6f;
                light.LampHeightMeters = 0f;
                Assert.IsFalse(LampPoolSystem.PoolsLight(light, 0.1f, true, out _),
                    "no height ⇒ no pool, rather than a flat disc");

                light.LampHeightMeters = 2.5f;
                light.Intensity = 0f;
                Assert.IsFalse(LampPoolSystem.PoolsLight(light, 0.1f, true, out _));

                light.Intensity = 1f;
                Assert.IsFalse(LampPoolSystem.PoolsLight(light, 1f, true, out _),
                    "and by day the night gate closes it, like every other light in the project");

                Assert.IsFalse(LampPoolSystem.PoolsLight(null, 0.1f, true, out _), "null is not a lamp");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// <b>A placed lamp carries its REACH into the runtime, not just into the siting.</b> #733 split the
        /// bloom from the pool in the preset LIBRARY, where the builders could read it; the thing that draws
        /// a pool is handed a <see cref="SceneLight"/> and never a preset kind, so the reach has to make the
        /// crossing. Without this the split would exist and nothing would be able to use it.
        /// </summary>
        [Test]
        public void ApplyingAPreset_CarriesTheReachOntoTheLight_NotJustTheBloom()
        {
            var go = new GameObject("reachCarry") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var light = go.AddComponent<SceneLight>();

                LightPresets.Apply(light, LightPresets.Kind.Lightpost);
                Assert.AreEqual(LightPresets.ReachMetres(LightPresets.Kind.Lightpost), light.ReachMetres, Eps,
                    "the lamp post lights 3.6 m of ground");
                Assert.AreEqual(LightPresets.For(LightPresets.Kind.Lightpost).Range, light.Range, Eps,
                    "and blooms at its 0.40 m lantern — the two are different numbers and both arrive");

                LightPresets.ApplyFitting(light, LightPresets.Kind.Floodlight, 1.49f);
                Assert.AreEqual(LightPresets.ReachMetres(LightPresets.Kind.Floodlight), light.ReachMetres, Eps,
                    "and a per-piece fitting overrides the BLOOM without disturbing the reach");
                Assert.AreEqual(1.49f, light.Range, Eps);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// <b>A boat's lamps light no ground, and that is a decision.</b> <c>BoatLampPresets</c> stamps
        /// through <c>LightPresets.Stamp</c>, which writes a <c>Config</c> — and a <c>Config</c> carries the
        /// bloom only. So every navigation lamp leaves its reach at zero and casts no pool: a sidelight is
        /// how another skipper reads your aspect, and a red pool on the sea under it would be a lie about
        /// what the lamp is for.
        /// </summary>
        [Test]
        public void ABoatsNavigationLamps_CastNoPoolOnTheSea()
        {
            var go = new GameObject("navLamp") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var light = go.AddComponent<SceneLight>();
                light.ReachMetres = 99f;                    // prove the stamp is what leaves it alone
                BoatLampPresets.Apply(light, HiddenHarbours.Core.HullLampKind.PortSidelight);

                Assert.AreEqual(99f, light.ReachMetres, Eps,
                    "Stamp writes the Config and nothing else — a boat lamp's reach is simply never set, " +
                    "so it stays whatever it was, and on a freshly added light that is zero");

                var fresh = new GameObject("freshNav") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var l2 = fresh.AddComponent<SceneLight>();
                    BoatLampPresets.Apply(l2, HiddenHarbours.Core.HullLampKind.Masthead);
                    Assert.AreEqual(0f, l2.ReachMetres, Eps, "a masthead lights the night, not the water");
                    Assert.IsFalse(LampPoolSystem.PoolsLight(l2, 0.1f, true, out _));
                }
                finally { Object.DestroyImmediate(fresh); }
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- the dial ------------------------------------------------------------------------------------

        /// <summary>
        /// <b>The way back is one field.</b> The pool makes a trade — a screen-space multiply brightens
        /// whatever occupies the pixel, including something passing OVER a pool rather than standing in it —
        /// and the owner has to be able to refuse it without a build. Pinned at the shipped values so a
        /// change to them is deliberate.
        /// </summary>
        [Test]
        public void TheProfileShipsThePoolOn_WithTheWayBackAtOneField()
        {
            var p = LampShadowProfile.CreateDefault();
            try
            {
                Assert.IsTrue(p.PoolsEnabled, "the owner asked for the ground to be lit");
                Assert.AreEqual(0.6f, p.PoolStrength, Eps);
                Assert.AreEqual(0.55f, p.PoolEdgeSoftness, Eps);
                Assert.AreEqual(8, p.MaxPools);

                p.PoolsEnabled = false;
                Assert.IsFalse(p.PoolsEnabled, "and can turn it off again");
                p.PoolStrength = 5f;
                Assert.AreEqual(1f, p.PoolStrength, Eps, "clamped: the ground may double, not quintuple");
            }
            finally { Object.DestroyImmediate(p); }
        }

        // ---- a lamp ON A WALL lights what is in front of the wall, never the eave, canopy or roof ------

        /// <summary>
        /// <b>Light from a window falls out and down.</b> The owner, at Ginny's cottage: the window light
        /// was "glowing the roof which would be obstructed by the houses eave". A wall lamp's pool is cut at
        /// the wall line: ground in front of it is lit as before, and everything behind it (in this view the
        /// wall above the lamp, the eave, a canopy and the roof) is not.
        /// </summary>
        [Test]
        public void AWallLampLightsTheGroundInFrontOfItsWall_AndNothingBehindIt()
        {
            Vector2 south = Vector2.down;
            Assert.AreEqual(1f, LightMath.PoolWallGate(new Vector2(0f, -2f), south, 0.3f, 0.35f), Eps,
                "two metres out in front of the wall is fully lit");
            Assert.AreEqual(1f, LightMath.PoolWallGate(new Vector2(1.5f, -1f), south, 0.3f, 0.35f), Eps,
                "and to either side in front of it");
            Assert.AreEqual(0f, LightMath.PoolWallGate(new Vector2(0f, 1.5f), south, 0.3f, 0.35f), Eps,
                "a metre and a half behind the lamp's foot (the wall above it, the eave) is not lit at all");
            Assert.AreEqual(0.5f, LightMath.PoolWallGate(new Vector2(0f, 0.3f), south, 0.3f, 0.35f), Eps,
                "the wall line itself is the middle of the soft edge");
            Assert.AreEqual(1f, LightMath.PoolWallGate(new Vector2(0f, 1.5f), Vector2.zero, 0.3f, 0.35f), Eps,
                "a free-standing lamp (no wall) lights every side, as before");
            Assert.AreEqual(0f, LightMath.PoolWallGate(new Vector2(-1.5f, 0f), Vector2.right, 0f, 0.35f), Eps,
                "a wall facing east lights nothing west of it");
        }

        // ---- a lamp on his BROW lights the ground ahead of him, not at his feet (2026-11-25) -----------

        /// <summary>
        /// <b>The light starts at the lamp on his head.</b> The owner, 2026-11-25: "the light appears to
        /// originate from the players feet and not from their headlamp". A lamp on a brow, tilted down,
        /// lights the ground from where its beam's lower edge lands to where its axis lands: at the lens's
        /// own height, the shipped tilt and the headlamp's half angle, that is past his toes, and the gate
        /// is 0 at his feet and behind him, 1 at and beyond the axis, and the same in any facing.
        /// </summary>
        [Test]
        public void AHeadlampLightsTheGroundAheadOfHisFeet_NotUnderThem()
        {
            float half = LightPresets.ConeHalfDegrees(LightPresets.Kind.Headlamp);
            float tilt = GlassLook.Current.HeadlampTiltDownDegrees;
            LightMath.HeadlampThrow(1.7f, tilt, half, out float start, out float full);
            Assert.AreEqual(1.7f / Mathf.Tan((tilt + half) * Mathf.Deg2Rad), start, Eps, "lit from the beam's lower edge");
            Assert.AreEqual(1.7f / Mathf.Tan(tilt * Mathf.Deg2Rad), full, Eps, "full where its axis lands");
            Assert.Greater(start, 0.5f, "the lit ground begins clear of his toes");
            Assert.Less(full, LightPresets.ReachMetres(LightPresets.Kind.Headlamp), "and is full well inside the reach");

            foreach (Vector2 axis in new[] { Vector2.up, Vector2.right, Vector2.down, new Vector2(-1f, 1f).normalized })
            {
                Assert.AreEqual(0f, LightMath.ThrowGate(Vector2.zero, axis, start, full), Eps, "nothing at his feet");
                Assert.AreEqual(0f, LightMath.ThrowGate(-axis * 2f, axis, start, full), Eps, "nothing behind him");
                Assert.AreEqual(0f, LightMath.ThrowGate(axis * (start * 0.9f), axis, start, full), Eps, "nothing short of the lower edge");
                Assert.AreEqual(1f, LightMath.ThrowGate(axis * full, axis, start, full), Eps, "full on the axis");
                Assert.AreEqual(1f, LightMath.ThrowGate(axis * (full + 3f), axis, start, full), Eps, "and beyond it");
                float mid = LightMath.ThrowGate(axis * ((start + full) * 0.5f), axis, start, full);
                Assert.AreEqual(0.5f, mid, Eps, "the ramp's middle");
            }

            Assert.AreEqual(1f, LightMath.ThrowGate(Vector2.zero, Vector2.up, 0f, 0f), Eps,
                "a lamp with no throw (every placed lamp) lights from its foot, as before");
            LightMath.HeadlampThrow(0.9f, tilt, half, out float lowStart, out _);
            Assert.Less(lowStart, start, "a lamp worn lower (crouched, sat at the oars) lands nearer");
        }

        /// <summary>
        /// <b>The mounts are data.</b> Every window spill is a wall lamp; doorstep lanterns are matched by
        /// their placement id; the general store's lantern hangs under its canopy, so its glow draws below the
        /// canopy's edge; a lamp post matches nothing and stays free-standing.
        /// </summary>
        [Test]
        public void TheShippedMountsPutWindowsAndDoorstepsOnWalls_AndTheStoreLanternUnderItsCanopy()
        {
            LampMounts mounts = Resources.Load<LampMounts>(LampMounts.ResourcePath);
            Assert.IsNotNull(mounts, "Resources/LampMounts.asset ships");
            var spill = mounts.For("WindowGlow", LightPresets.Kind.WindowGlow);
            Assert.IsTrue(spill.HasWall, "a window is in a wall");
            Assert.Less(spill.Facing.y, 0f, "and its light falls toward the camera, out and down");

            var store = mounts.For("light.st_peters.doorstep_lantern_general_store", LightPresets.Kind.HungLantern);
            Assert.IsTrue(store.HasWall);
            Assert.IsTrue(store.HangsUnderCanopy, "the store lantern hangs under the canopy");

            var house = mounts.For("light.st_peters.doorstep_lantern_school", LightPresets.Kind.HungLantern);
            Assert.IsTrue(house.HasWall, "a doorstep lantern is on its house wall");
            Assert.IsFalse(house.HangsUnderCanopy, "and nothing hangs over it");

            var post = mounts.For("light.st_peters.lantern_post_green", LightPresets.Kind.HungLantern);
            Assert.IsFalse(post.HasWall, "a lamp post stands free and lights every side");
            Assert.IsFalse(mounts.For("lanternPost_1", LightPresets.Kind.HungLantern).HasWall,
                "the St Peters dock lamp stands free");

            SceneLight light = new GameObject("mount-store") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<SceneLight>();
            try
            {
                light.transform.position = new Vector3(1.64f, 32.05f, 0f);
                LightPresets.Apply(light, LightPresets.Kind.HungLantern);
                light.BloomLiftMetres = 2.06832f;   // what the scene's placement measured: up on the canopy
                int atFoot = light.BloomSortingOrder;
                mounts.ApplyTo(light, "light.st_peters.doorstep_lantern_general_store", LightPresets.Kind.HungLantern);
                Assert.AreEqual(store.Facing.normalized, light.WallFacing);
                Assert.AreEqual(store.BloomLiftMetres, light.BloomLiftMetres, Eps,
                    "its glow draws below the canopy's edge, in front of the doorway");
                Assert.Less(light.BloomLiftMetres, 0.6f, "low enough to clear the canopy it hangs under");
                Assert.AreEqual(atFoot, light.BloomSortingOrder, "and still sorts at its own foot, over the shop front");

                light.BloomLiftMetres = 2.06832f;
                mounts.ApplyTo(light, "light.st_peters.doorstep_lantern_school", LightPresets.Kind.HungLantern);
                Assert.AreEqual(2.06832f, light.BloomLiftMetres, Eps, "a lantern with no canopy keeps its measured height");
            }
            finally { Object.DestroyImmediate(light.gameObject); }
        }

        /// <summary>The shader keeps the twin's wall line.</summary>
        [Test]
        public void ThePoolShaderCutsAWallLampAtItsWall_AsTheTwinDoes()
        {
            string shader = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,
                "_Project/Art/Shaders/HiddenHarboursLampPool.shader"));
            StringAssert.Contains("float ahead = dot(toFrag, normalize(_PoolWall.xy)) + _PoolWall.z;", shader);
            StringAssert.Contains("wall = smoothstep(-ws, ws, ahead);", shader);
            StringAssert.Contains("incidence * edge * cone * wall *", shader);
        }
    }
}
