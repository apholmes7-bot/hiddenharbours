using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The <b>differential day-vs-night guard</b> for the cliff face, plus the pin that keeps the
    /// shipped material's numbers equal to the model's.
    ///
    /// <para><b>Why this exists next to a compile guard.</b>
    /// <see cref="CliffShaderCompileGuardTests"/> proves the shader BUILDS; nothing proves it is
    /// RIGHT, and a CI runner has no GPU to ask. So the lighting model lives in
    /// <see cref="CliffLightMath"/> as engine-light C#, the shader mirrors it term for term, and this
    /// suite asserts the properties the owner's ask actually depends on. It is the
    /// <c>TerrainSplatBandPinTests</c> pattern applied to lighting.</para>
    ///
    /// <para><b>The owner's ask, restated as tests.</b> Verticality is the point of the whole
    /// exercise: a cliff must read as a tall standing wall, "lit like a face and not like ground". Two
    /// halves follow — the wall must respond to the sun's direction (so aspects differ, and a face
    /// turns through the day), and it must still be a wall at night rather than a hole in the world.
    /// The second half is the one that is easy to ship broken, because it only shows after dark.</para>
    ///
    /// <para><b>Observed failing under sabotage</b> (each re-verified by hand before this landed):
    /// setting <see cref="CliffLightMath.SkyFloor"/> to 0 fails
    /// <see cref="ANightWallStillReadsAsRock"/>; dropping the daylight gate (using 1 in place of
    /// <c>saturate(sunElevation)</c>) fails <see cref="NoonAndMidnightAreNotTheSameWall"/>; and
    /// resolving L in the untipped frame fails <see cref="ABatteredFaceCatchesAHighSunAVerticalOneMisses"/>.</para>
    /// </summary>
    public class CliffLightMathTests
    {
        const string CliffMaterialPath = "Assets/_Project/Art/Materials/CliffFace.mat";

        /// <summary>A texel facing straight out of the face, half-occluded, carrying the aspect's own
        /// key in its mask — the ordinary case, so a failure is about the model rather than about a
        /// pathological texel.</summary>
        static readonly Vector3 FlatTexel = new Vector3(0f, 0f, 1f);
        const float Ao = 0.5f;
        const float MaskKey = 0.6f;

        /// <summary>The kit's S-aspect bake key in tangent space (rig <c>ASPECT.S.L</c>), which is what
        /// the shipped material carries as <c>_BakeL</c>.</summary>
        static readonly Vector3 BakeS = new Vector3(-0.6f, -0.55f, 0.58f);

        /// <summary>Due south, the seaward bearing of the St Peters weather coast.</summary>
        const float SouthWall = 180f;
        const float Vertical = 90f;

        // =====================================================================================
        //  the day/night differential
        // =====================================================================================

        /// <summary>
        /// The sun sets and the wall must change. With the sun high and to the south, a south-facing
        /// vertical wall takes direct light; at midnight it takes none, and only sky and bounce remain.
        /// </summary>
        [Test]
        public void NoonAndMidnightAreNotTheSameWall()
        {
            Vector2 fromSouth = new Vector2(0f, -1f);   // ground direction TOWARD a southern sun

            float noon = CliffLightMath.ShadeWall(SouthWall, Vertical, fromSouth,
                                                  sunElevation: 0.85f, FlatTexel, Ao, MaskKey, BakeS);
            float midnight = CliffLightMath.ShadeWall(SouthWall, Vertical, fromSouth,
                                                      sunElevation: -0.7f, FlatTexel, Ao, MaskKey, BakeS);

            Assert.Greater(noon, midnight * 1.5f,
                $"A south wall shades {noon:F3} at noon and {midnight:F3} at midnight — barely a " +
                "change. The direct term is gated by saturate(sunElevation); if that gate is missing " +
                "the wall is lit by a sun that is under the sea, and the cliff sits visibly still " +
                "while the water and the sod around it go dark.");
        }

        /// <summary>
        /// 🔴 <b>The night floor.</b> ADR 0013 applies day/night as a global MULTIPLY overlay and folds
        /// the moonlight lift into that same tint. So a wall that has already reached zero on its own
        /// has nothing left for the moonlight to lift — it multiplies to black either way, and a
        /// moonlit cliff reads as a hole punched in the coast. The model must therefore keep a positive
        /// residue after dark, which is exactly what <see cref="CliffLightMath.SkyFloor"/> is for.
        /// </summary>
        [Test]
        public void ANightWallStillReadsAsRock()
        {
            float midnight = CliffLightMath.ShadeWall(SouthWall, Vertical, new Vector2(0f, -1f),
                                                      sunElevation: -0.9f, FlatTexel, Ao, MaskKey, BakeS);

            Assert.Greater(midnight, 0.05f,
                $"A midnight wall shades {midnight:F4} — effectively black BEFORE ADR 0013's overlay " +
                "multiplies it. The moonlight lift rides in _DayNightTint, so it can only brighten " +
                "something that is still there. Keep CliffLightMath.SkyFloor positive.");

            // ...and a fully-occluded texel — the deepest cleft on the wall — must survive too, or the
            // cliff goes to holes in its own re-entrants at night rather than going dark evenly.
            float occluded = CliffLightMath.ShadeWall(SouthWall, Vertical, new Vector2(0f, -1f),
                                                      sunElevation: -0.9f, FlatTexel, ao: 0f,
                                                      maskKey: 0f, bakeLightTangent: BakeS);
            Assert.Greater(occluded, 0f,
                "A fully-occluded texel reaches exactly zero at night. The clefts would read as holes " +
                "rather than as deep shadow.");
        }

        /// <summary>
        /// The aspect law has to be a real lighting difference, not just five different textures. With
        /// the sun in the south-west, a wall facing it takes more light than one facing away — and if
        /// it does not, the plan azimuth is not reaching the shade at all.
        /// </summary>
        [Test]
        public void AWallFacingTheSunTakesMoreLightThanOneFacingAway()
        {
            Vector2 fromSouthWest = new Vector2(-0.707f, -0.707f);
            const float el = 0.6f;

            float facing = CliffLightMath.ShadeWall(225f, Vertical, fromSouthWest, el,
                                                    FlatTexel, Ao, MaskKey, BakeS);
            float away = CliffLightMath.ShadeWall(45f, Vertical, fromSouthWest, el,
                                                  FlatTexel, Ao, MaskKey, BakeS);

            Assert.Greater(facing, away,
                $"A SW wall ({facing:F3}) is not brighter than a NE wall ({away:F3}) under a SW sun. " +
                "The wall's plan azimuth is not reaching the lighting, so every aspect would shade " +
                "identically and the coast would read as one flat colour of rock.");
        }

        /// <summary>
        /// <b>The batter must rotate the frame, not scale a term</b> — the kit's rule 11. A face tipped
        /// back genuinely presents itself to a high sun; a vertical face of the same aspect does not.
        /// Take the sun almost overhead and put the wall's back to it: the vertical wall gets nothing
        /// direct, the bank still catches some.
        /// </summary>
        [Test]
        public void ABatteredFaceCatchesAHighSunAVerticalOneMisses()
        {
            // Sun nearly overhead, slightly to the north — i.e. BEHIND a south-facing wall.
            Vector2 fromNorth = new Vector2(0f, 1f);
            const float high = 0.97f;

            float wall = CliffLightMath.ShadeWall(SouthWall, 90f, fromNorth, high,
                                                  FlatTexel, Ao, MaskKey, BakeS);
            float bank = CliffLightMath.ShadeWall(SouthWall, 48f, fromNorth, high,
                                                  FlatTexel, Ao, MaskKey, BakeS);

            Assert.Greater(bank, wall,
                $"A 48° bank ({bank:F3}) does not out-shade a 90° wall ({wall:F3}) under a near-zenith " +
                "sun behind them both. L is not being resolved in the TIPPED frame, so the batter is " +
                "acting as a shading multiplier — which is the one thing the kit's rule 11 says it " +
                "must never be, and it reads in play as a lighting bug rather than as a slope.");
        }

        /// <summary>
        /// 🔴 <b>The up-axis sign.</b> The wall's "up the face" vector is <c>cross(tangent, normal)</c>;
        /// the other order yields a DOWNWARD axis, which silently flips the sign of every texel's
        /// <c>t</c> component. The consequences are both wrong and quiet: the ground bounce lights the
        /// sky-facing texels instead of the foreshore-facing ones, and under a high sun the wall's
        /// relief reads inside-out — bed lips shaded where they should catch, hollows lit where they
        /// should be dark. A cliff whose relief is inverted still looks like rock; it just stops
        /// reading as a wall, which is precisely the failure this whole exercise exists to fix.
        ///
        /// <para>It is invisible on a texel whose normal points straight out of the wall, because
        /// <c>t</c> is 0 there and the axis never enters the dot product — which is why this test uses
        /// TILTED texels and the others do not catch it. Measured: with the sign right, the up-tilted
        /// texel shades 1.150 against the down-tilted 0.575; with it wrong, 0.475 against 1.135.</para>
        /// </summary>
        [Test]
        public void ATexelTiltedUpTheFaceCatchesAHighSunBetterThanOneTiltedDown()
        {
            var tiltedUp = new Vector3(0f, 0.6f, 0.8f);
            var tiltedDown = new Vector3(0f, -0.6f, 0.8f);
            Vector2 fromSouth = new Vector2(0f, -1f);
            const float el = 0.75f;   // high, and in FRONT of a south wall, so it comes down onto it

            float up = CliffLightMath.ShadeWall(SouthWall, Vertical, fromSouth, el,
                                                tiltedUp, Ao, MaskKey, BakeS);
            float down = CliffLightMath.ShadeWall(SouthWall, Vertical, fromSouth, el,
                                                  tiltedDown, Ao, MaskKey, BakeS);

            Assert.Greater(up, down,
                $"A texel tilted UP the face shades {up:F3} while one tilted DOWN shades {down:F3}. " +
                "Under a high sun in front of the wall the up-tilted one must catch more — if it does " +
                "not, the wall's up-axis is inverted (cross(normal, tangent) instead of " +
                "cross(tangent, normal)) and the whole face's relief is reading inside-out.");
        }

        /// <summary>
        /// With no day/night controller in the scene the project's convention is that <c>_SunDir</c>
        /// reads zero, and every shader that uses it falls back rather than going black (the water
        /// shader's own <c>cycleOn</c> rule). The model's twin of that: a zero ground direction must
        /// still produce a lit wall, so an art scene or a fresh material is not a black rectangle.
        /// </summary>
        [Test]
        public void AnUnsetSunLeavesTheWallLitRatherThanBlack()
        {
            float unset = CliffLightMath.ShadeWall(SouthWall, Vertical, Vector2.zero, 0f,
                                                   FlatTexel, Ao, MaskKey, BakeS);
            Assert.Greater(unset, 0.05f,
                "With _SunDir unset the wall shades to nothing. The shader additionally falls back to " +
                "the aspect's bake key here (so an art scene keeps its direct term); the model does " +
                "not, and does not need to — what BOTH must guarantee is that the sky floor keeps the " +
                "wall visible when no cycle is driving it.");
        }

        // =====================================================================================
        //  the pin — the shipped material must carry the model's numbers
        // =====================================================================================

        /// <summary>
        /// The shader's tunables are material properties (rule 6), and the model's are C# constants.
        /// Nothing makes them equal except this test — and if they drift, the thing that ships is lit
        /// by numbers no test ever checked.
        /// </summary>
        [Test]
        public void TheShippedMaterialCarriesTheModelsNumbers()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CliffMaterialPath);
            Assert.IsNotNull(mat,
                $"'{CliffMaterialPath}' is missing — the cliff's shipped look would be unpinned.");

            AssertFloat(mat, "_SkyFloor", CliffLightMath.SkyFloor);
            AssertFloat(mat, "_Ambient", CliffLightMath.Ambient);
            AssertFloat(mat, "_Bounce", CliffLightMath.Bounce);
            AssertFloat(mat, "_CastStrength", CliffLightMath.CastStrength);
            AssertFloat(mat, "_SunStrength", CliffLightMath.SunStrength);
        }

        static void AssertFloat(Material mat, string property, float expected)
        {
            Assert.IsTrue(mat.HasProperty(property),
                $"CliffFace.mat has no '{property}'. The shader and the material have diverged.");
            Assert.AreEqual(expected, mat.GetFloat(property), 1e-4f,
                $"CliffFace.mat's {property} is {mat.GetFloat(property)} but CliffLightMath says " +
                $"{expected}. The shader is the thing that ships and the model is the thing that is " +
                "tested — they have to be the same numbers or neither is true.");
        }
    }
}
