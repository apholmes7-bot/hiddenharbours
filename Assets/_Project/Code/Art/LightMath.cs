using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The PURE, deterministic maths of the additive 2D LIGHTS (ADR 0016): the NIGHT-GATE ramp (how much a
    /// light shows given the day/night darkness), the CONE/RADIAL falloff used to shape the glow, and the
    /// deterministic FLICKER. Everything here is a <b>pure function</b> of its arguments — no scene, no GPU,
    /// no time-of-call state, no <see cref="System.Random"/> — so it is unit-tested headless (the determinism
    /// guard, CLAUDE.md rule 5), and the runtime <see cref="SceneLight"/> / shader are thin shells that call
    /// these (or mirror them in HLSL).
    ///
    /// <para><b>Why a separate static class.</b> Mirrors the project's "extract the math, test it headless"
    /// discipline (<see cref="DayNightMath"/>, <see cref="GrassWindBridge.WindToShaderVector"/>). These
    /// functions only use <see cref="Mathf"/> / <see cref="Color"/> — all evaluable in EditMode with no editor
    /// or GPU — so the whole light model is verified without opening Unity.</para>
    ///
    /// <para><b>The night-gate (the heart of ADR 0016).</b> Sprites are Sprite-UNLIT and night is a
    /// full-screen MULTIPLY darkening overlay (ADR 0013), so an additive light only makes sense WHEN the frame
    /// is dark — otherwise it would wash daytime out. <see cref="NightGate"/> reads the published
    /// <c>_DayNightTint</c> luminance and scales the light from ~0 at a bright noon to full in a dark night.
    /// The shader mirrors this exact ramp so the gate is GPU-side (zero per-light C# coupling to the cycle);
    /// it is duplicated here ONLY so it can be unit-tested headless.</para>
    /// </summary>
    public static class LightMath
    {
        /// <summary>
        /// Perceptual luminance of a colour (Rec. 601 weights), <c>0</c> (black) .. ~<c>1</c> (white). The
        /// day/night tint's luminance is the single "how bright is the frame right now" scalar the night-gate
        /// keys off. Clamped non-negative; HDR tints above 1 are allowed to read as "very bright" (gate → 0).
        /// </summary>
        public static float Luminance(Color c)
            => Mathf.Max(0f, 0.299f * c.r + 0.587f * c.g + 0.114f * c.b);

        /// <summary>
        /// The NIGHT-GATE: how much an additive light should show right now, <c>0</c> (full daylight — the
        /// light is invisible so it can't wash the scene out) .. <c>1</c> (deep dark — the light is at full
        /// strength and cuts through). It is a smooth ramp on the day/night frame DARKNESS
        /// <c>(1 − luminance(tint))</c>: below <paramref name="threshold"/> darkness the light is off; it
        /// fades in across a <paramref name="softness"/>-wide band above the threshold to full. Monotonic in
        /// darkness (darker ⇒ never less light). Mirrors the HLSL in the additive-light shader exactly.
        ///
        /// <para><b>Cycle-off / edit-mode fallback (mirrors the water shader's unset-tint handling).</b> When
        /// the cycle isn't running the tint global is unset/near-black (luminance ≈ 0), which a naive gate
        /// would read as "deep night → full light" — which is actually what we WANT for the demo + the
        /// edit-mode preview (so the beam shows). The separate
        /// <see cref="NightGateWithFallback"/> makes that explicit + tunable.</para>
        ///
        /// <param name="tintLuminance">Luminance of the published <c>_DayNightTint</c> (see <see cref="Luminance"/>).</param>
        /// <param name="threshold">Frame darkness (0..1) at/below which the light is fully off (won't show in daytime).</param>
        /// <param name="softness">Width of the fade-in band above the threshold (small ⇒ a hard switch at dusk).</param>
        /// </summary>
        public static float NightGate(float tintLuminance, float threshold, float softness)
        {
            float darkness = Mathf.Clamp01(1f - Mathf.Max(0f, tintLuminance));
            float lo = Mathf.Clamp01(threshold);
            float hi = Mathf.Clamp01(threshold + Mathf.Max(softness, 1e-4f));
            return SmoothStep01(lo, hi, darkness);
        }

        /// <summary>
        /// The night-gate WITH the cycle-off fallback baked in: when the tint is unset/near-black (no cycle —
        /// EditMode, a bare art scene, the demo before Play), the day/night system isn't darkening anything,
        /// so a literal gate would be ambiguous. We then return <paramref name="fallbackWhenNoCycle"/> (default
        /// 1 = SHOW the light) so the beam is visible for tuning + the demo. Otherwise it is the normal
        /// <see cref="NightGate"/>. <paramref name="cycleActive"/> is the caller's "is the day/night cycle
        /// pushing a tint right now" decision (e.g. the tint global being meaningfully non-black).
        /// </summary>
        public static float NightGateWithFallback(float tintLuminance, float threshold, float softness,
                                                  bool cycleActive, float fallbackWhenNoCycle)
        {
            if (!cycleActive) return Mathf.Clamp01(fallbackWhenNoCycle);
            return NightGate(tintLuminance, threshold, softness);
        }

        /// <summary>
        /// RADIAL (distance) falloff of the glow, <c>1</c> at the centre .. <c>0</c> at/beyond
        /// <paramref name="range"/>. <paramref name="normalizedDistance"/> is distance/range in 0..1 (the
        /// shader works in normalized light space). The fade is a smooth <c>(1 − d)</c> shaped by
        /// <paramref name="edgeSoftness"/>: 0 ⇒ a fairly hard disc, 1 ⇒ a very soft halo. Monotonically
        /// decreasing in distance. Pure / deterministic — this is the exact curve the shader applies, mirrored
        /// here for the tests.
        /// </summary>
        public static float RadialFalloff(float normalizedDistance, float edgeSoftness)
        {
            float d = Mathf.Clamp01(normalizedDistance);
            float linear = 1f - d;                                  // 1 at centre, 0 at the edge
            // Soften: raise toward a gentler curve as softness rises (a soft halo) vs a near-linear disc.
            float power = Mathf.Lerp(2f, 0.6f, Mathf.Clamp01(edgeSoftness));
            return Mathf.Pow(Mathf.Clamp01(linear), power);
        }

        /// <summary>
        /// ANGULAR (cone) falloff of the glow, <c>1</c> on the beam axis .. <c>0</c> outside the cone. The
        /// cone is defined by its HALF-ANGLE: a point at angle <paramref name="angleFromAxisDeg"/> off the
        /// axis is fully lit inside the inner cone and fades to nothing by the
        /// <paramref name="coneHalfAngleDeg"/> edge across an <paramref name="angularSoftness"/> band (as a
        /// fraction of the half-angle). A half-angle of 180° (or more) is a full RADIAL glow (always 1 — the
        /// round lantern). A small half-angle is a tight BEAM. Monotonically non-increasing as the point swings
        /// off-axis. Pure / deterministic; mirrors the shader.
        ///
        /// <param name="angleFromAxisDeg">Angle (deg, ≥0) between the beam axis and the point being lit.</param>
        /// <param name="coneHalfAngleDeg">The cone's half-angle (deg). ≥180 ⇒ full radial (no angular cut).</param>
        /// <param name="angularSoftness">Fraction (0..1) of the half-angle over which the edge feathers in.</param>
        /// </summary>
        public static float ConeFalloff(float angleFromAxisDeg, float coneHalfAngleDeg, float angularSoftness)
        {
            float half = Mathf.Max(coneHalfAngleDeg, 0f);
            if (half >= 180f) return 1f;                            // full radial: no angular gate
            float a = Mathf.Abs(angleFromAxisDeg);
            float soft = Mathf.Clamp01(angularSoftness);
            float inner = half * (1f - soft);                      // fully lit out to here
            // Fade from the inner edge (1) to the half-angle (0); outside the cone → 0.
            return 1f - SmoothStep01(inner, half, a);
        }

        /// <summary>
        /// The full glow shape at a point: <see cref="RadialFalloff"/> × <see cref="ConeFalloff"/>, in
        /// <c>0..1</c>. The combined intensity profile of the light before colour, master intensity, the
        /// night-gate and flicker are applied. Pure / deterministic; the convenience the tests pin the shape
        /// with.
        /// </summary>
        public static float ShapeIntensity(float normalizedDistance, float edgeSoftness,
                                           float angleFromAxisDeg, float coneHalfAngleDeg, float angularSoftness)
            => RadialFalloff(normalizedDistance, edgeSoftness)
               * ConeFalloff(angleFromAxisDeg, coneHalfAngleDeg, angularSoftness);

        // ---- the WATER spotlight term (lit FROM WITHIN the water shader; ADR 0016) -----------------------
        //
        // The boat spotlight's additive QUAD lights LAND, but the URP 2D renderer draws the custom-shader WATER
        // SpriteRenderer over the MeshRenderer quad regardless of sortingOrder/depth (two quad-sort fixes failed).
        // The robust fix: the water shader LIGHTS ITSELF — the boat spotlight publishes its world-space cone as
        // GLOBAL shader uniforms (like _SunDir / _DayNightTint already are), and the water FRAGMENT adds the cone
        // illumination to its own col.rgb. Computed inside the water's own rendering, there is NO sorting
        // dependency — it cannot fail the way the quad did, and it composes naturally with the water's own
        // reflection/foam/palette layers. These pure functions are the EXACT maths the HLSL term mirrors, lifted
        // here so the cone/range/gate behaviour is unit-tested headless (the determinism guard, CLAUDE.md rule 5).
        //
        // COSINE-BASED cone (what the shader does, cheaper than degrees in HLSL): the beam axis is a unit vector
        // and a pixel is "inside the cone" when cos(angle from the axis) >= cos(halfAngle). The angular falloff is
        // a smoothstep on that cosine from the OUTER edge (cosHalf, fully off) up toward the INNER edge (cosInner,
        // fully on, the inner cone the soft band feathers from). BoatSpotlight publishes BOTH cosines so the
        // softness semantics match the existing ConeFalloff (a fraction of the half-angle) without recomputing
        // trig per pixel.

        /// <summary>
        /// The cosine of a cone HALF-ANGLE (degrees). The boat spotlight publishes this so the water shader can
        /// test "inside the cone" as <c>dot(beamDir, toPixelDir) >= cosHalfAngle</c> — a single dot, no per-pixel
        /// trig. Clamped to a valid half-angle. Pure / deterministic.
        /// </summary>
        public static float CosFromHalfAngleDeg(float halfAngleDeg)
            => Mathf.Cos(Mathf.Clamp(halfAngleDeg, 0f, 180f) * Mathf.Deg2Rad);

        /// <summary>
        /// ANGULAR (cone) falloff from the COSINE of the angle off the beam axis, <c>1</c> inside the inner cone
        /// .. <c>0</c> outside the half-angle. Mirrors <see cref="ConeFalloff"/> but in cosine space (what the
        /// shader uses): a smoothstep from <paramref name="cosHalfAngle"/> (the outer edge, off) up to
        /// <paramref name="cosInnerAngle"/> (fully on). Because cosine DECREASES as the angle grows, a point on
        /// the axis (cos = 1) is fully lit and a point at the half-angle (cos = cosHalfAngle) is off — and beyond
        /// the half-angle the smoothstep saturates to 0. Caller guarantees <c>cosInnerAngle >= cosHalfAngle</c>
        /// (a smaller inner angle ⇒ a larger cosine). Pure / deterministic.
        /// </summary>
        public static float ConeFalloffCos(float cosAngle, float cosHalfAngle, float cosInnerAngle)
            => SmoothStep01(cosHalfAngle, Mathf.Max(cosInnerAngle, cosHalfAngle + 1e-4f), cosAngle);

        /// <summary>
        /// The full WATER spotlight term at a world-space water pixel, in <c>0..1</c> (before colour, intensity
        /// and the night-gate): the radial falloff over the range × the cosine cone falloff. This is the exact
        /// shape the water fragment adds to <c>col.rgb</c> (scaled by colour × intensity × night-gate). Returns 0
        /// when the pixel is beyond the range or outside the cone; at the lamp itself the direction is undefined
        /// so it reads as on-axis (the bright core). Pure / deterministic — mirrors the HLSL <c>BoatLightTerm</c>.
        ///
        /// <param name="lampX">Lamp world X (the bow anchor).</param>
        /// <param name="lampY">Lamp world Y.</param>
        /// <param name="dirX">Beam axis world X (the boat heading, transform.up — assumed ~unit length).</param>
        /// <param name="dirY">Beam axis world Y.</param>
        /// <param name="pixelX">Water pixel world X.</param>
        /// <param name="pixelY">Water pixel world Y.</param>
        /// <param name="range">Throw distance (m); beyond this the term is 0.</param>
        /// <param name="cosHalfAngle">Cosine of the cone half-angle (the outer edge).</param>
        /// <param name="cosInnerAngle">Cosine of the inner (fully-lit) cone angle (>= cosHalfAngle).</param>
        /// <param name="edgeSoftness">Radial edge softness (0 hard disc .. 1 soft halo).</param>
        /// </summary>
        public static float WaterConeTerm(float lampX, float lampY, float dirX, float dirY,
                                          float pixelX, float pixelY, float range,
                                          float cosHalfAngle, float cosInnerAngle, float edgeSoftness)
        {
            float r = Mathf.Max(range, 1e-4f);
            float toX = pixelX - lampX;
            float toY = pixelY - lampY;
            float dist = Mathf.Sqrt(toX * toX + toY * toY);
            if (dist >= r) return 0f;                              // beyond the throw -> dark

            float nd = dist / r;                                  // 0 at the lamp .. 1 at the range edge
            float radial = RadialFalloff(nd, edgeSoftness);

            // direction lamp->pixel; at the lamp itself the direction is undefined -> treat as on-axis (core).
            float invDist = dist > 1e-5f ? 1f / dist : 0f;
            float ndx = toX * invDist;
            float ndy = toY * invDist;
            // normalize the beam axis defensively (the publisher sends ~unit, but be NaN-safe).
            float dlen = Mathf.Sqrt(dirX * dirX + dirY * dirY);
            float bdx = dlen > 1e-5f ? dirX / dlen : 0f;
            float bdy = dlen > 1e-5f ? dirY / dlen : 1f;
            float cosAngle = dist > 1e-5f ? (ndx * bdx + ndy * bdy) : 1f;   // 1 = on the beam axis
            float cone = ConeFalloffCos(cosAngle, cosHalfAngle, cosInnerAngle);

            return Mathf.Clamp01(radial * cone);
        }

        // ---- DAY/NIGHT PRE-COMPENSATION (the complete-dark fix) ------------------------------------------
        //
        // The day/night overlay MULTIPLIES the whole frame by _DayNightTint after the water renders (ADR 0013).
        // Anything the water shader ADDS to itself — the boat-spotlight beam, the reflected moon/glitter/stars —
        // was therefore crushed by the same multiply: at the shipped deepest night the tint is
        // skyTint(0.12, 0.16, 0.34) × the intensity floor 0.18 ≈ (0.022, 0.029, 0.061), so an authored beam
        // survived on screen at ~3-6%, blue-shifted (the owner's "the spotlight/moon vanish in complete dark").
        // The fix divides the additive term by the tint BEFORE the overlay so the multiply cancels — the same
        // pre-compensation pattern the palette guard-rail's day/night value floor already uses (ADR 0015). The
        // shader performs this in frag() AFTER the palette grade (the grade's saturated deep-night floor would
        // otherwise flatten lit-vs-unlit contrast, and its value ceiling would clamp the >1 compensated values);
        // this is the exact headless twin of that divide, tested in LightMathTests.

        /// <summary>
        /// The per-channel FLOOR of the day/night pre-compensation divide — mirrors the shader's
        /// <c>DN_COMP_MIN_CHANNEL</c>. It bounds the boost at ≤ <c>1/0.02 = 50×</c> so a near-zero tint channel
        /// can't explode the divide toward infinity. The shipped deepest-night tint channels
        /// (≈ 0.022, 0.029, 0.061) all EXCEED this floor, so at deepest night the cancellation is EXACT — no
        /// hue shift. NOTE: the whole scheme depends on HDR being ON (UniversalRP.asset <c>m_SupportsHDR: 1</c>)
        /// so the compensated &gt;1 values survive the framebuffer to reach the overlay's multiply.
        /// </summary>
        public const float DayNightCompensationMinChannel = 0.02f;

        /// <summary>
        /// PRE-COMPENSATE an additive light term for the day/night multiply overlay: divide each channel by
        /// <c>max(tint, minChannel)</c> so the overlay's downstream multiply by <paramref name="tint"/> cancels
        /// and the term reads ON SCREEN at its authored brightness regardless of how dark the night is. Mirrors
        /// the water shader's post-grade add EXACTLY (the headless determinism twin):
        /// <c>col.rgb += (dnSum &gt; 1e-3) ? add / max(tint.rgb, DN_COMP_MIN_CHANNEL) : add</c>.
        ///
        /// <para>Behaviour pinned by the tests: a ~white tint (cycle at full day) is a NO-OP (divide by 1);
        /// a near-black/unset tint (sum ≤ 1e-3 ⇒ the cycle is OFF — edit mode, a bare art scene, the demo)
        /// returns the term UNTOUCHED (there is no overlay to compensate for); otherwise the on-screen product
        /// <c>compensated × tint</c> equals the authored term whenever every tint channel ≥
        /// <paramref name="minChannel"/>, and the boost never exceeds <c>1/minChannel</c> per channel.</para>
        /// </summary>
        /// <param name="additive">The authored additive RGB term (beam / moon / stars), before the overlay.</param>
        /// <param name="tint">The published day/night multiply tint (<c>_DayNightTint</c>).</param>
        /// <param name="minChannel">Per-channel divide floor (use <see cref="DayNightCompensationMinChannel"/>).</param>
        public static Color CompensateForDayNightTint(Color additive, Color tint, float minChannel)
        {
            float sum = tint.r + tint.g + tint.b;
            if (sum <= 1e-3f) return additive;                     // cycle off/unset -> nothing to compensate
            float floorCh = Mathf.Max(minChannel, 1e-4f);          // defensive: a zero floor must never divide by 0
            return new Color(
                additive.r / Mathf.Max(tint.r, floorCh),
                additive.g / Mathf.Max(tint.g, floorCh),
                additive.b / Mathf.Max(tint.b, floorCh),
                additive.a);
        }

        /// <summary>
        /// A DETERMINISTIC flicker multiplier in <c>[1 − amount, 1]</c> — a lantern/torch's living wobble with
        /// NO <see cref="System.Random"/> (rule 5). It is a pure function of <c>(seed, time)</c>: a couple of
        /// hashed sine waves at incommensurate rates give an organic, non-repeating-looking but fully
        /// reproducible flicker. <paramref name="amount"/> 0 ⇒ steady (always 1); 1 ⇒ flickers down to 0.
        /// <paramref name="speed"/> scales how fast it wobbles. Same <c>(seed, time)</c> ⇒ same value, every
        /// time, on every machine — so two runs of the demo flicker identically.
        ///
        /// <param name="seed">Per-light constant (e.g. its instance id hashed) so two lights flicker out of phase.</param>
        /// <param name="time">A monotonic time (seconds). The component feeds the deterministic game/real time.</param>
        /// </summary>
        public static float Flicker(int seed, float time, float amount, float speed)
        {
            float amt = Mathf.Clamp01(amount);
            if (amt <= 0f) return 1f;
            float phase = Hash01(seed) * Mathf.PI * 2f;             // per-light phase offset (no two in lockstep)
            float s = Mathf.Max(speed, 0f);
            // Two incommensurate sines → an organic, non-obviously-periodic wobble. Map to 0..1.
            float w = 0.6f * Mathf.Sin(time * (6.3f * s) + phase)
                    + 0.4f * Mathf.Sin(time * (11.1f * s) + phase * 1.7f);
            float n01 = (w + 1f) * 0.5f;                            // -1..1 → 0..1
            return 1f - amt * (1f - n01);                           // [1-amt .. 1]
        }

        /// <summary>
        /// The final additive RGB an additive light contributes at a point, before the GPU adds it to the
        /// frame: <c>color × intensity × shape × nightGate × flicker</c>. The single composition the shader
        /// performs, exposed here so the tests pin the whole pipeline (e.g. invisible at noon, full at night).
        /// Alpha carries the same scalar (for an SrcAlpha-One additive blend). Pure / deterministic.
        /// </summary>
        public static Color AdditiveContribution(Color color, float intensity, float shape, float nightGate,
                                                 float flicker)
        {
            float k = Mathf.Max(intensity, 0f) * Mathf.Clamp01(shape) * Mathf.Clamp01(nightGate)
                    * Mathf.Clamp01(flicker);
            return new Color(color.r * k, color.g * k, color.b * k, k);
        }

        /// <summary>
        /// The world-space DEPTH (Z) at which to place the additive-light quad so it composites ABOVE the world
        /// sprites: just in front of the camera, mirroring how the day/night overlay sits at the camera near
        /// plane. This is the world-depth half of the "the beam lit land but NOT the water" fix (ADR 0016): in
        /// the URP 2D renderer a MeshRenderer at the SAME world depth as a big water/ground SPRITE can be
        /// overdrawn by it regardless of sorting order, so we pull the light to the camera's (closest) depth —
        /// the trick the overlay already uses to reliably draw over the water. Pure function of the camera's
        /// Z + forward-Z, the near plane and the offset, so it is unit-tested headless. For a 2D ortho camera
        /// (forward ≈ +Z, camera behind the scene at a negative Z) this resolves to
        /// <c>cameraZ + (nearClip + offset)</c> — a small step from the camera toward the scene. Look-direction
        /// agnostic via <paramref name="cameraForwardZ"/>. Presentation only: the light is ZTest Always +
        /// additive, so changing the quad's depth never changes the LOOK under an orthographic camera (depth
        /// doesn't affect screen X/Y) — only the compositing order.
        /// </summary>
        public static float CameraDepthZ(float cameraZ, float cameraForwardZ, float nearClip, float offset)
            => cameraZ + cameraForwardZ * (Mathf.Max(nearClip, 0f) + Mathf.Max(offset, 0f));

        // ---- the BOAT-SPOTLIGHT BOUNCE (the lamp rocks with the hull; ADR 0018 B2 read) ------------------
        //
        // The boat spotlight lives on the STEADY physics ROOT (correct for AIMING — the cone follows the bow
        // and does NOT inherit the visual's counter-rotation), so it does not share the wave ROCK the hull's
        // visual gets (BoatWaveMotion bobs/rolls the FishingBoatVisual child, B2). A lamp mounted on a rolling
        // deck should BOB and SWAY with that rock — so BoatSpotlight reads the hull's current bob (a screen-Y
        // offset) + roll (a small tilt) off the visual and feeds them here to nudge the PUBLISHED beam:
        //   • the lamp POSITION offsets vertically by the bob (a mounted lamp rides up/down with the deck), and
        //   • the beam DIRECTION sways by a small angle proportional to the roll (the cone leans as the deck
        //     tilts) — subtle, a mounted light sways a little, not wildly.
        // PURE + deterministic (no scene/GPU/RNG): a headless twin of what the component publishes, so the
        // bob→offset + roll→sway mapping is unit-tested (rule 5). Zero rock (glass calm) => zero offset AND the
        // beam direction returned UNCHANGED (the cone sits exactly still) — the identity the tests pin.

        /// <summary>
        /// The vertical (world-Y) POSITION offset to add to the published lamp position from the hull's current
        /// BOB, in world units: simply <c>bob × scale</c>. A mounted lamp rides up and down with the deck. Zero
        /// bob (or zero scale) ⇒ zero offset (glass calm ⇒ the lamp sits still). Linear + monotonic in bob;
        /// pure / deterministic. Kept separate from the sway so each half is pinned independently by the tests.
        /// </summary>
        public static float BounceLampYOffset(float bob, float scale)
            => bob * scale;

        /// <summary>
        /// Sway the beam DIRECTION by the hull's current ROLL: rotate <paramref name="beamDir"/> about the
        /// screen normal by <c>roll × degreesPerRoll</c> degrees (a subtle lean — a mounted light sways a
        /// LITTLE with the deck, not wildly). Zero roll (or zero degreesPerRoll) ⇒ the direction is returned
        /// UNCHANGED (glass calm ⇒ the cone points exactly where it aimed). The input need not be unit length;
        /// the output preserves its magnitude (a pure rotation). Pure / deterministic — the headless twin of the
        /// sway the component applies before publishing. A degenerate (near-zero) beamDir is returned as-is.
        ///
        /// <param name="beamDir">The aimed beam axis (the boat heading, transform.up) — rotated, not normalized.</param>
        /// <param name="rollDegrees">The hull's current visual roll (degrees; sign carries the lean direction).</param>
        /// <param name="degreesPerRoll">Sway gain: swayed angle = rollDegrees × this. Small (e.g. 0.5) = subtle.</param>
        /// </summary>
        public static Vector2 BounceSwayDir(Vector2 beamDir, float rollDegrees, float degreesPerRoll)
        {
            if (beamDir.sqrMagnitude < 1e-12f) return beamDir;         // no aim to sway
            float swayDeg = rollDegrees * degreesPerRoll;
            if (Mathf.Abs(swayDeg) < 1e-6f) return beamDir;            // glass calm ⇒ exactly unchanged
            float rad = swayDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            // Standard 2D rotation (CCW for +angle): preserves |beamDir| (pure rotation, no scale).
            return new Vector2(beamDir.x * c - beamDir.y * s,
                               beamDir.x * s + beamDir.y * c);
        }

        // ---- small pure helpers -------------------------------------------------------------------------

        /// <summary>Smoothstep returning 0 below <paramref name="a"/>, 1 above <paramref name="b"/>, smooth between. Order-safe.</summary>
        public static float SmoothStep01(float a, float b, float x)
        {
            if (b <= a) return x >= b ? 1f : 0f;                    // degenerate band → a hard step
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// A stable, reproducible hash of an int to <c>[0,1)</c> (no <see cref="System.Random"/>). Used only
        /// to give each light a constant flicker phase offset. Pure / deterministic.
        /// </summary>
        public static float Hash01(int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= 2747636419u;
                h *= 2654435769u;
                h ^= h >> 16;
                h *= 2654435769u;
                h ^= h >> 16;
                h *= 2654435769u;
                return (h & 0xFFFFFF) / (float)0x1000000;           // top bits, mapped to [0,1)
            }
        }
    }
}
