using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the WATER PALETTE GUARD-RAIL — the final colour-grade stage the water
    /// shader (HiddenHarboursWater.shader) applies to the composited <c>col.rgb</c> right before it returns
    /// (ADR 0015). The guard-rail keeps the sea's final output inside an art-directed palette so the dynamic,
    /// sea-state-driven look (depth tint / fbm / swell / caustics / specular / reflection / foam) can never
    /// wash out (too bright) or go muddy (too dark), while preserving the diversity. The owner chose SOFT
    /// guard-rails — bound the extremes and gently PULL toward the palette, not a hard lock.
    ///
    /// <para><b>Four coupled operations, in order (each a pure function of <c>col.rgb</c> only):</b>
    /// <list type="number">
    /// <item><b>VALUE (luminance) FLOOR + CEILING</b> — no mud, no blowout. The floor is DAY/NIGHT-AWARE
    /// (see <see cref="ValueFloorDayNight"/>): it PRE-COMPENSATES for the day/night overlay's downstream
    /// MULTIPLY so DAYLIGHT/overcast never goes muddy while TRUE NIGHT still goes genuinely dark.</item>
    /// <item><b>SATURATION CAP</b> — pull chroma toward grey only when it exceeds the cap, so an over-saturated
    /// layer can't scream past the palette.</item>
    /// <item><b>ANCHOR PULL</b> — gently lerp the colour toward the nearest palette ANCHOR (deep / mid /
    /// shallow / foam, chosen by luminance) at a soft strength (a rail, not a cage).</item>
    /// </list>
    /// All three are scaled by the master <see cref="WaterPaletteGradeParams.Strength"/> so
    /// <c>Strength = 0</c> is an EXACT passthrough (today's look) and the feature is opt-in + revertible.</para>
    ///
    /// <para><b>The day/night interaction (the subtle part — ADR 0015 §"Day/night floor").</b> The day/night
    /// system (ADR 0013) draws a full-screen MULTIPLY overlay ABOVE the water, so whatever the water shader
    /// emits is multiplied downstream by the day/night tint's luminance. A naive constant floor in the water
    /// shader would therefore be DARKENED AWAY by the overlay and could either (a) leave daylight muddy if set
    /// low, or (b) kill the owner's "genuinely dark nights" if set high. The fix: floor the water's value at
    /// <c>min(1, paletteFloor / max(dayNightLuma, eps))</c> so AFTER the overlay's <c>×dayNightLuma</c> the
    /// result lands at ~<c>paletteFloor</c> in daylight (dayNightLuma ≈ 1), but at deep night (dayNightLuma
    /// small) the pre-compensated floor saturates at 1 and the overlay still darkens the water to genuine
    /// dark. A <see cref="WaterPaletteGradeParams.NightFloor"/> knob caps how low the EFFECTIVE post-overlay
    /// floor is allowed to ride at night (0 = let night go as dark as the overlay takes it; higher = keep a
    /// faint readable floor even at night), so the night behaviour is tunable.</para>
    ///
    /// <para><b>P1 / determinism (rule 5).</b> This is a pure <c>col.rgb</c> operation — it NEVER touches
    /// depth / clip / <c>_WaterLevel</c> / the height read / the sim. It saves nothing and feeds no
    /// simulation, exactly like the §5.6–§11 cosmetic water layers. Tunable (rule 6): every bound is a
    /// material property. The shader mirrors this math exactly; this twin locks it headless (the determinism
    /// guard, mirroring <see cref="WaterReflection"/> / <see cref="DayNightMath"/>). Uses only
    /// <see cref="Mathf"/> / <see cref="Vector3"/> (no engine state) so it evaluates in EditMode.</para>
    /// </summary>
    public static class WaterPaletteGrade
    {
        // Rec.601 luma weights — the SAME weights the water shader already uses (e.g. the painted-foam
        // luminance fallback dot(rgb, float3(0.299, 0.587, 0.114))), so the twin and the shader agree.
        private const float LumaR = 0.299f;
        private const float LumaG = 0.587f;
        private const float LumaB = 0.114f;

        /// <summary>Perceptual luminance (Rec.601) of an RGB colour — the SAME weights the shader uses.</summary>
        public static float Luminance(Vector3 rgb) => rgb.x * LumaR + rgb.y * LumaG + rgb.z * LumaB;

        /// <summary>
        /// The DAY/NIGHT-AWARE value floor the water shader pre-compensates to. Given the art-directed
        /// <paramref name="paletteFloor"/> (the luminance the FINAL on-screen water should not drop below in
        /// daylight) and the day/night overlay's luminance <paramref name="dayNightLuma"/> (1 = full daylight,
        /// small = deep night — the luminance of the global <c>_DayNightTint</c> the overlay multiplies the
        /// frame by), returns the floor to clamp the WATER's pre-overlay luminance to:
        /// <c>min(1, paletteFloor / max(dayNightLuma, eps))</c>.
        ///
        /// <para>In DAYLIGHT (<paramref name="dayNightLuma"/> ≈ 1) this ≈ <paramref name="paletteFloor"/>, so
        /// after the overlay multiply the on-screen water lands at ~<paramref name="paletteFloor"/> — never
        /// muddy. At TRUE NIGHT (<paramref name="dayNightLuma"/> small) the quotient saturates at 1, so the
        /// water floor is "full bright" PRE-overlay and the overlay still darkens it to genuine dark — the
        /// owner's dark-nights vision is preserved. <paramref name="nightFloorPostOverlay"/> (0..1) lets the
        /// owner keep a faint readable post-overlay floor at night: the effective pre-overlay floor is also
        /// held at least <c>nightFloorPostOverlay / max(dayNightLuma, eps)</c> so the on-screen night water
        /// never drops below that. <c>nightFloorPostOverlay = 0</c> = let night go fully dark.</para>
        /// </summary>
        /// <summary>
        /// The floor pre-compensation's DAY KNEE default (<c>_PaletteFloorKnee</c>): the day/night luminance
        /// at/above which the floor pre-compensates exactly as ADR 0015 shipped (daylight + overcast land
        /// on-screen at ~<c>paletteFloor</c>), and below which the ON-SCREEN floor rides DOWN with the scene.
        /// 0.45 sits just under the dimmest storm-overcast daylight tint, so no daylight look moves.
        /// <b>Why (owner playtest 2026-07-23, "the whole sea becomes white"):</b> the un-kneed quotient
        /// saturated toward 1 through DUSK — at a dusk tint (~0.17..0.34 luma) it clamped most of the sea's
        /// pre-overlay values to one high floor, so the on-screen sea held DAYLIGHT-floor brightness while
        /// the scene dimmed around it and lost its value structure to the clamp — a uniform flat bright
        /// sheet (the dusk-storm repro frame measured 99.7% flat at the floor). <c>FloorKnee = 0</c> is the
        /// pre-fix saturating curve EXACTLY (the legacy passthrough contract).
        /// </summary>
        public const float DefaultFloorDayKnee = 0.45f;

        /// <param name="paletteFloor">Daylight on-screen luminance floor (<c>_PaletteValueFloor</c>).</param>
        /// <param name="dayNightLuma">Luminance of the day/night multiply tint (1 day .. ~0 night).</param>
        /// <param name="nightFloorPostOverlay">On-screen luminance floor permitted at night
        /// (<c>_PaletteNightFloor</c>); 0 lets night go as dark as the overlay takes it.</param>
        /// <param name="floorDayKnee">The day knee (<c>_PaletteFloorKnee</c>, see
        /// <see cref="DefaultFloorDayKnee"/>): dnLuma at/above it keeps the exact shipped pre-compensation;
        /// below it the divisor holds at the knee so the on-screen floor dims with the scene. 0 = the
        /// pre-fix saturating curve exactly.</param>
        public static float ValueFloorDayNight(float paletteFloor, float dayNightLuma,
                                               float nightFloorPostOverlay, float floorDayKnee)
        {
            float dn = Mathf.Max(dayNightLuma, 1e-3f);
            // Daylight target: pre-compensate so post-overlay value lands at paletteFloor (capped at full
            // bright) — but the divisor never falls below the day knee, so past dusk the pre-overlay floor
            // stops growing and the ON-SCREEN floor (this × dnLuma) rides down with the scene instead of
            // holding daylight brightness (the 2026-07-23 white-out; see DefaultFloorDayKnee).
            float kneeDn = Mathf.Max(dn, Mathf.Clamp01(floorDayKnee));
            float dayPre = Mathf.Min(1f, Mathf.Max(paletteFloor, 0f) / kneeDn);
            // Night target: a (usually small) on-screen floor the owner can keep readable at night, also
            // pre-compensated. Its whole job is to SURVIVE deep night, so it keeps the saturating divide
            // (no knee): at dayNightLuma ≈ 1 this <= dayPre (paletteFloor >= nightFloor by design) so it
            // is inert in daylight; at small dayNightLuma it rises toward 1 ONLY if the owner asks.
            float nightPre = Mathf.Min(1f, Mathf.Max(nightFloorPostOverlay, 0f) / dn);
            return Mathf.Min(1f, Mathf.Max(dayPre, nightPre));
        }

        /// <summary>The pre-knee overload — the exact ADR 0015 curve (knee 0). Kept so legacy callers and
        /// the passthrough contract stay expressible; production reads the material's knee (default
        /// <see cref="DefaultFloorDayKnee"/>).</summary>
        public static float ValueFloorDayNight(float paletteFloor, float dayNightLuma, float nightFloorPostOverlay)
            => ValueFloorDayNight(paletteFloor, dayNightLuma, nightFloorPostOverlay, 0f);

        /// <summary>
        /// Apply the full soft palette guard-rail to a composited water colour and return the graded colour.
        /// The exact mirror of the shader's final grade stage. <paramref name="dayNightLuma"/> is the
        /// luminance of the day/night multiply tint at this moment (1 = full daylight; pass 1 when the cycle
        /// is not running). <c>p.Strength = 0</c> returns <paramref name="rgb"/> UNCHANGED (today's look).
        /// </summary>
        public static Vector3 Grade(Vector3 rgb, in WaterPaletteGradeParams p, float dayNightLuma)
        {
            float strength = Mathf.Clamp01(p.Strength);
            if (strength <= 0f) return rgb;   // exact passthrough — opt-in, revertible (rule 6)

            Vector3 graded = rgb;

            // ---- (1) VALUE clamp: day/night-aware FLOOR + CEILING (no mud, no blowout) -------------------
            float luma = Luminance(graded);
            float floorPre = ValueFloorDayNight(p.ValueFloor, dayNightLuma, p.NightFloor, p.FloorKnee);
            float ceil = Mathf.Max(p.ValueCeil, floorPre);   // guard: ceiling never below the floor
            float targetLuma = Mathf.Clamp(luma, floorPre, ceil);
            graded = ScaleToLuminance(graded, luma, targetLuma);

            // ---- (2) SATURATION CAP: pull chroma toward grey only above the cap ---------------------------
            graded = CapSaturation(graded, p.SatCap);

            // ---- (3) ANCHOR PULL: gently lerp toward the nearest palette anchor (by luminance) ------------
            Vector3 anchor = AnchorForLuma(Luminance(graded), p);
            graded = Vector3.Lerp(graded, anchor, Mathf.Clamp01(p.PullStrength));

            // ---- master STRENGTH: lerp the whole grade back toward the raw colour (soft rail) ------------
            return Vector3.Lerp(rgb, graded, strength);
        }

        /// <summary>
        /// Re-scale an RGB colour so its luminance moves from <paramref name="fromLuma"/> to
        /// <paramref name="toLuma"/> while keeping its HUE/CHROMA ratios (a multiplicative scale, not a
        /// desaturating lerp toward grey). Falls back to a neutral grey of the target luminance when the
        /// source is (near) black, so a floor can lift a black pixel without a divide-by-zero.
        /// </summary>
        public static Vector3 ScaleToLuminance(Vector3 rgb, float fromLuma, float toLuma)
        {
            if (fromLuma <= 1e-4f)
                return new Vector3(toLuma, toLuma, toLuma);          // lift black to neutral grey of the target
            float k = toLuma / fromLuma;
            return new Vector3(rgb.x * k, rgb.y * k, rgb.z * k);
        }

        /// <summary>
        /// Cap a colour's SATURATION at <paramref name="satCap"/> (0..1): if the colour's HSV-style
        /// <c>(max-min)/max</c> exceeds the cap, pull every channel toward the colour's own grey (its
        /// luminance) by exactly the amount that lands the resulting saturation ON the cap; otherwise return
        /// it unchanged. Pulling toward the LUMINANCE (not the channel midpoint) keeps the perceived
        /// brightness near-constant — the cap desaturates without darkening/brightening. <c>satCap = 1</c> is
        /// a no-op. The desaturation factor solves <c>newSat = cap</c> in closed form (so the cap is EXACT,
        /// not approximate — a guard-rail should actually enforce the cap).
        /// </summary>
        public static Vector3 CapSaturation(Vector3 rgb, float satCap)
        {
            float cap = Mathf.Clamp01(satCap);
            float mx = Mathf.Max(rgb.x, Mathf.Max(rgb.y, rgb.z));
            float mn = Mathf.Min(rgb.x, Mathf.Min(rgb.y, rgb.z));
            float chroma = mx - mn;
            float sat = mx <= 1e-5f ? 0f : chroma / mx;
            if (sat <= cap || sat <= 1e-5f) return rgb;   // already within the cap (or fully grey)

            float grey = Luminance(rgb);
            // Lerp each channel toward grey by f so newSat == cap. With
            //   newMax = mx - f(mx-grey),  newMin = mn - f(mn-grey),  newSat = (chroma)(1-f) / newMax,
            // setting newSat = cap and solving:  f = (chroma - cap*mx) / (chroma - cap*(mx-grey)).
            float denom = chroma - cap * (mx - grey);
            float f = Mathf.Abs(denom) < 1e-6f ? 1f : Mathf.Clamp01((chroma - cap * mx) / denom);
            return new Vector3(
                Mathf.Lerp(rgb.x, grey, f),
                Mathf.Lerp(rgb.y, grey, f),
                Mathf.Lerp(rgb.z, grey, f));
        }

        /// <summary>
        /// HSV-style saturation of an RGB colour: <c>(max - min) / max</c> (0 = grey, 1 = fully saturated),
        /// 0 for black. The SAME definition the shader's CapSaturation mirrors.
        /// </summary>
        public static float SaturationOf(Vector3 rgb)
        {
            float mx = Mathf.Max(rgb.x, Mathf.Max(rgb.y, rgb.z));
            float mn = Mathf.Min(rgb.x, Mathf.Min(rgb.y, rgb.z));
            return mx <= 1e-5f ? 0f : (mx - mn) / mx;
        }

        /// <summary>
        /// Pick the palette ANCHOR colour to pull toward, by the colour's luminance: the darkest water pulls
        /// toward <see cref="WaterPaletteGradeParams.Deep"/>, then <see cref="WaterPaletteGradeParams.Mid"/>,
        /// then <see cref="WaterPaletteGradeParams.Shallow"/>, and the brightest (foam/glints) toward
        /// <see cref="WaterPaletteGradeParams.Foam"/>. A piecewise-linear blend across the four anchors by
        /// luminance, so the pull is continuous (no banding) and a mid-tone reads as a smooth mix of the two
        /// nearest anchors. The luminance breakpoints are the anchors' OWN luminances (so a deep-blue pixel
        /// pulls toward the deep anchor, etc.), guarded to be strictly increasing.
        /// </summary>
        public static Vector3 AnchorForLuma(float luma, in WaterPaletteGradeParams p)
        {
            // breakpoints = the anchors' own luminances, forced strictly increasing so the lerps are stable.
            float lDeep = Luminance(p.Deep);
            float lMid = Mathf.Max(Luminance(p.Mid), lDeep + 1e-3f);
            float lShallow = Mathf.Max(Luminance(p.Shallow), lMid + 1e-3f);
            float lFoam = Mathf.Max(Luminance(p.Foam), lShallow + 1e-3f);

            if (luma <= lDeep) return p.Deep;
            if (luma < lMid) return Vector3.Lerp(p.Deep, p.Mid, (luma - lDeep) / (lMid - lDeep));
            if (luma < lShallow) return Vector3.Lerp(p.Mid, p.Shallow, (luma - lMid) / (lShallow - lMid));
            if (luma < lFoam) return Vector3.Lerp(p.Shallow, p.Foam, (luma - lShallow) / (lFoam - lShallow));
            return p.Foam;
        }
    }

    /// <summary>
    /// The tunable bounds + anchor colours of a single water PALETTE (the guard-rail's per-material settings,
    /// mirroring the shader's <c>_Palette*</c> properties — ADR 0015). A palette is its four anchor colours
    /// (deep / mid / shallow / foam) plus its soft bounds (value floor/ceiling, saturation cap, anchor-pull
    /// strength, night floor) and the master strength. Stored on the Water material so a material variant
    /// carries its palette; this struct is the headless mirror used by <see cref="WaterPaletteGrade"/>.
    /// </summary>
    public struct WaterPaletteGradeParams
    {
        /// <summary>Master grade strength (<c>_PaletteGradeStrength</c>); 0 = exact passthrough (today's look).</summary>
        public float Strength;
        /// <summary>Daylight on-screen luminance FLOOR (<c>_PaletteValueFloor</c>) — no mud below this in daylight.</summary>
        public float ValueFloor;
        /// <summary>Luminance CEILING (<c>_PaletteValueCeil</c>) — no blowout above this.</summary>
        public float ValueCeil;
        /// <summary>HSV-style saturation CAP (<c>_PaletteSatCap</c>) — chroma is pulled toward grey above this.</summary>
        public float SatCap;
        /// <summary>Anchor PULL strength (<c>_PalettePullStrength</c>) — soft (~0.3–0.4), a rail not a cage.</summary>
        public float PullStrength;
        /// <summary>On-screen luminance floor permitted at NIGHT (<c>_PaletteNightFloor</c>); 0 = night goes fully dark.</summary>
        public float NightFloor;
        /// <summary>The floor's DAY KNEE (<c>_PaletteFloorKnee</c>; see
        /// <see cref="WaterPaletteGrade.DefaultFloorDayKnee"/>): dnLuma at/above it keeps the exact shipped
        /// pre-compensation, below it the on-screen floor dims with the scene. 0 = the pre-fix curve.</summary>
        public float FloorKnee;

        /// <summary>The DEEP-water anchor colour (<c>_PaletteDeep</c>).</summary>
        public Vector3 Deep;
        /// <summary>The MID-water anchor colour (<c>_PaletteMid</c>).</summary>
        public Vector3 Mid;
        /// <summary>The SHALLOW-water anchor colour (<c>_PaletteShallow</c>).</summary>
        public Vector3 Shallow;
        /// <summary>The FOAM / highlight anchor colour (<c>_PaletteFoam</c>).</summary>
        public Vector3 Foam;
    }
}
