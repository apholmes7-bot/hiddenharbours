using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The <b>cliff face lighting model</b> — the exact terms
    /// <c>Assets/_Project/Art/Shaders/HiddenHarboursCliffFace.shader</c> evaluates per pixel, written
    /// once in engine-light C# so they can be asserted headless.
    ///
    /// <para><b>Why a C# twin of a shader.</b> A shader cannot be unit-tested on a CI runner with no
    /// GPU: the magenta guard proves it COMPILES, and nothing proves it is RIGHT. This is the
    /// <c>TerrainSplatBandPinTests</c> pattern applied to lighting — the model lives here, the shader
    /// mirrors it line for line, and <c>CliffLightMathTests</c> asserts the properties that matter
    /// (a face is lit differently at noon than at midnight; a night face never collapses to black
    /// before the day/night overlay reaches it; a shadowed aspect still reads as rock). Sabotage either
    /// side and a test goes red.</para>
    ///
    /// <para><b>Where this sits in ADR 0013's lighting.</b> The project's day/night is a single global
    /// MULTIPLY overlay (<c>_DayNightTint</c>) drawn over the whole composited frame, and the moonlight
    /// lift is already folded into that tint by <see cref="DayNightMath"/>. So this model does NOT tint
    /// for time of day and does NOT read the moon: it computes how the sun strikes THIS wall right now,
    /// and the overlay darkens and moon-lifts the result along with the sea and the sod. The one thing
    /// it must never do is reach zero on its own — a face that goes black before the overlay has
    /// nothing left for the moonlight to lift, which is exactly how a cliff stops reading as a standing
    /// wall at night. <see cref="SkyFloor"/> is what stops it.</para>
    ///
    /// <para><b>Frames.</b> Compass azimuth in degrees, N = 0, clockwise (the rig's own convention —
    /// <c>CliffCatalog.AspectAzimuths</c>). The sun arrives as the project's two globals: <c>_SunDir.xy</c>
    /// is the ground-plane direction TOWARD the sun and <c>_SunElevation</c> is <b>sin</b> of its
    /// elevation angle in [−1, 1] (1 at zenith, 0 at the horizon, negative at night —
    /// <see cref="DayNightMath"/>).</para>
    /// </summary>
    public static class CliffLightMath
    {
        // =====================================================================================
        //  TUNABLES — mirrored as the shader's property DEFAULTS (CliffShaderPinTests holds them equal)
        // =====================================================================================

        /// <summary>
        /// How much sky light a fully-occluded texel still receives, as a fraction of the open-sky
        /// amount. The README's own lighting recipe uses <c>0.34 + ao × 0.66</c>; this is that 0.34.
        ///
        /// <para><b>⚠ This number is why a cliff survives midnight.</b> It is the floor under the whole
        /// multiply chain: with the sun down, a face is lit by <c>sky × ambient</c> alone, and if that
        /// reached zero the wall would be pure black BEFORE ADR 0013's overlay multiplied it — so the
        /// moonlight lift would have nothing to act on and a moonlit cliff would read as a hole in the
        /// world. Lowering it is a look decision; taking it to 0 is a bug.</para>
        /// </summary>
        public const float SkyFloor = 0.34f;

        /// <summary>Overall sky-dome (ambient) strength.</summary>
        public const float Ambient = 0.62f;

        /// <summary>Ground-bounce strength — light kicked back up onto the lower face from the
        /// foreshore. Falls away as the wall tips back (the kit's own <c>batterEffects.overhang</c>
        /// note: "ground bounce falls away").</summary>
        public const float Bounce = 0.28f;

        /// <summary>How much of the BAKED cast shadow to borrow. <c>mask.R</c> carries the one thing a
        /// normal map cannot reproduce — the cast shadow of the buttress ribs and bench lips — but it is
        /// baked at the aspect's own key, so borrowing it whole under a travelling sun over-darkens.
        /// The README's <c>castStrength</c>.</summary>
        public const float CastStrength = 0.7f;

        /// <summary>Floor on the bake's own N·L when un-dividing the baked cast shadow, so a texel that
        /// faced away from the bake key does not explode the ratio.</summary>
        public const float BakeNdlFloor = 0.02f;

        /// <summary>Direct sunlight strength at full elevation.</summary>
        public const float SunStrength = 1.0f;

        // =====================================================================================
        //  THE WALL BASIS
        // =====================================================================================

        /// <summary>
        /// The wall's tangent basis from its plan azimuth and its batter: <paramref name="tangent"/>
        /// runs along the cliff, <paramref name="up"/> is "t-up" ON the tipped face, and
        /// <paramref name="normal"/> points out of it. World Z is up.
        ///
        /// <para><b>⚠ The batter must ROTATE THE FRAME, not scale a shading term</b> (the rig's rule 11:
        /// "A batter is not a shading multiplier… any one of those alone reads as a lighting bug"). A
        /// 48° bank tipped back genuinely sees more sky and genuinely catches a high sun that a vertical
        /// wall of the same aspect does not — and that only falls out if L is resolved in the tipped
        /// frame.</para>
        /// </summary>
        /// <param name="azimuthDegrees">Compass azimuth of the wall's OUTWARD normal (N = 0, clockwise).</param>
        /// <param name="batterDegrees">Angle from horizontal: 90 is a vertical wall, 48 a bank.</param>
        public static void WallBasis(float azimuthDegrees, float batterDegrees,
                                     out Vector3 tangent, out Vector3 up, out Vector3 normal)
        {
            float az = azimuthDegrees * Mathf.Deg2Rad;
            float b  = batterDegrees * Mathf.Deg2Rad;

            // Ground-plane outward normal from a compass bearing: N (0°) is +Y, E (90°) is +X.
            var planN = new Vector2(Mathf.Sin(az), Mathf.Cos(az));
            // Along the cliff, right-handed about world up.
            tangent = new Vector3(planN.y, -planN.x, 0f);
            // Tip the outward normal up out of the ground plane by (90 - batter).
            normal = new Vector3(planN.x * Mathf.Sin(b), planN.y * Mathf.Sin(b), Mathf.Cos(b));
            // "Up the face" completes the frame.
            // ⚠ ORDER MATTERS AND THE OTHER ONE IS SILENT. cross(normal, tangent) yields a DOWNWARD
            // up-axis, which flips the sign of every texel's t component: the ground bounce then lights
            // the texels facing the sky instead of the ones facing the foreshore, and a face's relief
            // reads inside-out under a high sun. It is invisible on a texel whose normal is straight out
            // of the wall (t = 0), which is why CliffLightMathTests pins it with a TILTED texel.
            up = Vector3.Cross(tangent, normal);
        }

        /// <summary>
        /// The sun as a world direction from the project's two globals. <paramref name="sunElevation"/>
        /// is <b>sin</b> of the elevation angle, so its horizontal companion is
        /// <c>sqrt(1 − e²)</c> — no trig, and exactly consistent with
        /// <see cref="DayNightMath"/>'s [−1, 1] range.
        /// </summary>
        public static Vector3 SunWorld(Vector2 sunGroundDir, float sunElevation)
        {
            float e = Mathf.Clamp(sunElevation, -1f, 1f);
            float horiz = Mathf.Sqrt(Mathf.Max(0f, 1f - e * e));
            Vector2 d = sunGroundDir.sqrMagnitude > 1e-8f ? sunGroundDir.normalized : Vector2.zero;
            return new Vector3(d.x * horiz, d.y * horiz, e);
        }

        /// <summary>Resolve a world direction into the wall's tangent frame — the README's
        /// <c>L = float3(dot(S, Ts), S.z, dot(S, Nw))</c>, with the middle term taken against the real
        /// tipped up-vector rather than world Z so a battered face answers honestly.</summary>
        public static Vector3 ToTangent(Vector3 world, Vector3 tangent, Vector3 up, Vector3 normal) =>
            new Vector3(Vector3.Dot(world, tangent), Vector3.Dot(world, up), Vector3.Dot(world, normal));

        // =====================================================================================
        //  THE SHADE
        // =====================================================================================

        /// <summary>
        /// The multiplier the shader applies to the <c>_unlit</c> albedo at one texel — sky plus bounce
        /// plus direct sun, with the baked cast shadow borrowed onto the direct term.
        ///
        /// <para><b>Daylight gate.</b> The direct term scales by <c>saturate(sunElevation)</c>, which is
        /// 0 the moment the sun is at or below the horizon — so dusk removes the sun from the wall
        /// smoothly and midnight leaves only sky and bounce. That residue is deliberately non-zero; see
        /// <see cref="SkyFloor"/>.</para>
        /// </summary>
        /// <param name="normalTexel">Tangent-space normal, already decoded to [−1, 1].</param>
        /// <param name="ao">Cavity plus macro AO (the normal map's alpha), 0..1.</param>
        /// <param name="maskKey">The baked key-light term including the form cast shadow (mask.R), 0..1.</param>
        /// <param name="lightTangent">The sun in the wall's tangent frame.</param>
        /// <param name="bakeLightTangent">The tangent-frame light the aspect was BAKED at.</param>
        /// <param name="sunElevation">sin of the sun's elevation, [−1, 1].</param>
        public static float Shade(Vector3 normalTexel, float ao, float maskKey,
                                  Vector3 lightTangent, Vector3 bakeLightTangent, float sunElevation)
        {
            Vector3 n = normalTexel.sqrMagnitude > 1e-8f ? normalTexel.normalized : Vector3.forward;
            Vector3 l = lightTangent.sqrMagnitude > 1e-8f ? lightTangent.normalized : Vector3.forward;

            float ndl = Mathf.Clamp01(Vector3.Dot(n, l));

            // Borrow the baked cast shadow: mask.R already contains (N·Lbake × castShadow), so dividing
            // by the bake's own N·L leaves the shadow alone — which is the one term a normal map cannot
            // reproduce. Floored so a texel that faced away from the bake key cannot blow the ratio up.
            float bakeNdl = Mathf.Max(Mathf.Clamp01(Vector3.Dot(n, bakeLightTangent.normalized)), BakeNdlFloor);
            float cast = Mathf.Lerp(1f, Mathf.Clamp01(maskKey / bakeNdl), CastStrength);

            float sky = (SkyFloor + Mathf.Clamp01(ao) * (1f - SkyFloor)) * Ambient;

            // Bounce comes off the ground, so it lights texels facing DOWN the face (negative up
            // component) and fades as the wall tips back and stops looking at the foreshore.
            float bnc = Mathf.Clamp01(-n.y * 0.85f + 0.15f) * (0.35f + Mathf.Clamp01(ao) * 0.5f) * Bounce;

            float day = Mathf.Clamp01(sunElevation);
            float sun = ndl * cast * day * SunStrength;

            return sky + bnc + sun;
        }

        /// <summary>
        /// Convenience: the shade at a texel for a wall of a given azimuth and batter under the
        /// project's sun globals. This is the whole chain the shader runs, in one call, so a test can
        /// assert an end-to-end property (noon vs midnight) without re-deriving the frames.
        /// </summary>
        public static float ShadeWall(float azimuthDegrees, float batterDegrees,
                                      Vector2 sunGroundDir, float sunElevation,
                                      Vector3 normalTexel, float ao, float maskKey,
                                      Vector3 bakeLightTangent)
        {
            WallBasis(azimuthDegrees, batterDegrees, out Vector3 t, out Vector3 u, out Vector3 n);
            Vector3 lt = ToTangent(SunWorld(sunGroundDir, sunElevation), t, u, n);
            return Shade(normalTexel, ao, maskKey, lt, bakeLightTangent, sunElevation);
        }
    }
}
