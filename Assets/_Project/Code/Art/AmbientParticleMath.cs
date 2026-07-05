using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The PURE, deterministic maths behind every LIVING-COAST AMBIENT particle (sea mist, chimney smoke,
    /// gulls, dust motes). Like <see cref="WaterSurface"/> / <see cref="DayNightMath"/> /
    /// <see cref="GrassWindBridge.WindToShaderVector"/> the feel-math lives here as <b>pure, side-effect-free,
    /// EditMode-testable</b> functions of their inputs so the spawn/drift/lifecycle/day-night curves can be
    /// verified headless (CLAUDE.md rule 5) and the MonoBehaviour shells stay thin.
    ///
    /// <para><b>Determinism (rule 5).</b> All "random-ish" variation comes from a stable integer hash
    /// (<see cref="Hash01"/>), never <see cref="System.Random"/>, so identical inputs reproduce identical
    /// motion — and crucially these drive NO simulation and save NOTHING; they are visual only. Cosmetic
    /// motion may read <c>Time</c>/the clock and the hash for variety, which is allowed for VFX.</para>
    ///
    /// <para><b>Shared-wind cohesion.</b> The ambient particles read the SAME wind the grass and water read
    /// — published as the global <c>_WindWorld</c> by <see cref="GrassWindBridge"/> (direction · 0..1
    /// strength) and/or sampled from <see cref="HiddenHarbours.Core.GameServices.Environment"/> — so a gust
    /// drifts the mist, bends the smoke, and pushes the gulls the SAME way it leans the grass and ruffles the
    /// sea. The day/night look reads the global <c>_DayNightTint</c> so everything dims/warms together at
    /// dusk (mist faintly catches moonlight, motes fade after dark).</para>
    /// </summary>
    public static class AmbientParticleMath
    {
        // ==== deterministic hash (no System.Random — rule 5) ==============================================

        /// <summary>A stable 0..1 hash of a uint (the per-particle jitter source). Same avalanche as the
        /// wake's so the two systems share a proven, allocation-free integer hash. Deterministic, no RNG.</summary>
        public static float Hash01(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352du;
            x ^= x >> 15; x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / (float)0x1000000;   // 24-bit mantissa → [0,1)
        }

        /// <summary>A stable 0..1 hash of two ints (e.g. a particle index + a salt) → folded into
        /// <see cref="Hash01"/>. Lets each particle pull several decorrelated values without RNG.</summary>
        public static float Hash01(int index, int salt)
        {
            unchecked
            {
                uint h = (uint)(index * 374761393 + salt * 668265263 + 0x9E3779B1);
                return Hash01(h);
            }
        }

        // ==== life curve (fade in, hold, fade out) ========================================================

        /// <summary>Normalised life 0..1 (0 = just born, 1 = at end of lifetime). Pure + static.</summary>
        public static float Life01(float age, float lifetime)
            => lifetime <= 0f ? 1f : Mathf.Clamp01(age / lifetime);

        /// <summary>
        /// A soft "fade in then fade out" opacity envelope over a particle's life: ramps 0→1 across the first
        /// <paramref name="fadeIn01"/> of life, holds at 1, then ramps 1→0 across the last
        /// <paramref name="fadeOut01"/> of life. So a wisp/puff appears gently, lives, and dissolves rather
        /// than popping in or snapping off. Smoothstepped on each ramp for a soft shoulder. Clamped, NaN-safe,
        /// and 0 at both endpoints (life 0 and 1) so a particle is invisible exactly at birth and death. Pure.
        /// </summary>
        public static float LifeEnvelope(float life01, float fadeIn01, float fadeOut01)
        {
            float t = Mathf.Clamp01(life01);
            float fin = Mathf.Clamp(fadeIn01, 0f, 1f);
            float fout = Mathf.Clamp(fadeOut01, 0f, 1f);

            float rampIn = fin <= 1e-4f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fin));
            float rampOut = fout <= 1e-4f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / fout));
            return Mathf.Clamp01(rampIn * rampOut);
        }

        // ==== drift integration (shared wind + own velocity) ==============================================

        /// <summary>
        /// Advance a drifting particle's position one tick: <c>pos += (ownVel + wind · windResponse) · dt</c>.
        /// The wind term is the SAME shared wind the grass/water read, scaled by <paramref name="windResponse"/>
        /// (m/s of drift per unit of the 0..1 shader wind) so a wisp/mote carries downwind in step with the
        /// scene. Pure + static (the drift the tests pin).
        /// </summary>
        public static Vector2 Drift(Vector2 pos, Vector2 ownVel, Vector2 wind, float windResponse, float dt)
            => pos + (ownVel + wind * windResponse) * Mathf.Max(0f, dt);

        // ==== sea-mist density response (visibility / sea-state up the mist) ===============================

        /// <summary>
        /// A 0..1 "mist intensity" the sea mist scales its spawn-rate + opacity by, folding the live sea mood:
        /// <list type="bullet">
        /// <item><description>LOW visibility (fog) → MORE mist: <c>(1 - visibility)</c> weighted by
        /// <paramref name="fogWeight"/>.</description></item>
        /// <item><description>HIGHER sea-state (spray/whitecaps kicking up) → MORE mist: the CONTINUOUS
        /// <c>EnvironmentSample.SeaState01</c> axis weighted by <paramref name="seaStateWeight"/> — smooth
        /// in the wind, so the mist thickens gradually instead of stepping at an enum band
        /// edge.</description></item>
        /// </list>
        /// Always at least <paramref name="baseline"/> so there is a faint ambient shimmer even on a clear,
        /// glassy day (subtle by default, as briefed). Clamped 0..1. Pure + static. Mirrors the wake's
        /// <c>SeaStateRoughness</c> linear scale so mist thickens exactly when the sea does.
        /// </summary>
        public static float MistIntensity(float visibility, float seaState01,
                                          float baseline, float fogWeight, float seaStateWeight)
        {
            float fog = Mathf.Clamp01(1f - Mathf.Clamp01(visibility));
            float sea = Mathf.Clamp01(seaState01);
            float extra = fog * Mathf.Max(0f, fogWeight) + sea * Mathf.Max(0f, seaStateWeight);
            return Mathf.Clamp01(Mathf.Clamp01(baseline) + extra);
        }

        // ==== rain intensity (a squall = chop kicked up AND the light gone murky) ==========================

        /// <summary>
        /// A 0..1 "rain intensity" the FALLING rain scales its spawn-rate + opacity by — and the SHARED source
        /// of truth a later water-shader pass reads for surface rain-rings, so the drops and the rings always
        /// agree. Rain is DERIVED, art-only: there is NO precipitation signal in the sim, so we read a squall
        /// off the two mood axes the environment already publishes (both pure functions of
        /// <c>(worldSeed, gameTime)</c> — no save, no determinism concern):
        /// <list type="bullet">
        /// <item><description>HIGHER sea-state (the wind is up, chop building) is the DRIVER: the continuous
        /// <c>EnvironmentSample.SeaState01</c> axis weighted by <paramref name="seaStateWeight"/>.</description></item>
        /// <item><description>LOW visibility (the light gone murky) is the GATE: a rough-but-clear sea is a
        /// blustery bright day, NOT a downpour — so fog UNLOCKS the rain. The gate rises from
        /// <c>(1 - visibilityGate)</c> in clear air to 1 in thick fog, i.e. clear skies throttle the rain to a
        /// fraction and murk opens it fully. With <paramref name="visibilityGate"/> = 1 the sea must go murky
        /// before it rains at all; = 0 removes the gate (rain purely on chop).</description></item>
        /// </list>
        /// So <c>intensity = baseline + seaStateWeight · seaState01 · gate(visibility)</c>. Monotonic
        /// increasing in sea-state, increasing as visibility falls, ~<paramref name="baseline"/> on a glassy
        /// clear day (default baseline 0 = the feature is OFF until the owner dials it in). Clamped 0..1. Pure +
        /// static — the intensity the tests pin and the shader will share.
        /// </summary>
        public static float RainIntensity(float visibility, float seaState01,
                                          float baseline, float seaStateWeight, float visibilityGate)
        {
            float sea = Mathf.Clamp01(seaState01);
            float fog = Mathf.Clamp01(1f - Mathf.Clamp01(visibility));   // 0 clear .. 1 thick murk
            float g = Mathf.Clamp01(visibilityGate);
            // Gate: clear air lets through (1 - g); murk opens it to 1. g=0 → gate is always 1 (no gate).
            float gate = Mathf.Clamp01((1f - g) + g * fog);
            float driven = Mathf.Max(0f, seaStateWeight) * sea * gate;
            return Mathf.Clamp01(Mathf.Clamp01(baseline) + driven);
        }

        // ==== spray intensity (a whitecapping sea flings torn spray off the crests) =========================

        /// <summary>
        /// A 0..1 "spray intensity" the wind-blown SPRAY emitter scales its spawn-rate + opacity by — a
        /// scene-level "is the sea whitecapping?" gate. Spray is DERIVED, art-only and, unlike the falling rain,
        /// keys off ONE axis: the sea-state. Whitecaps (and the torn spray they throw) are a THRESHOLD
        /// phenomenon — a glassy or gently rippled sea throws no spray at all, then past a wind/sea-state onset
        /// the crests start to break and spray is flung hard and rising fast into a gale. Since the real
        /// per-pixel wave crest-factor lives GPU-side (the shader whitecaps of ADR 0018 B1), the particle lane
        /// uses the scene-level <c>EnvironmentSample.SeaState01</c> axis as its whitecapping proxy so the
        /// scene-wide spray and the on-crest shader foam agree on WHEN the sea is breaking.
        /// <list type="bullet">
        /// <item><description>BELOW <paramref name="threshold"/> (calm → light chop) the gate is ~0: no spray,
        /// so a glassy or breezy-but-unbroken sea stays clean.</description></item>
        /// <item><description>PAST <paramref name="threshold"/> it ramps up STEEPLY (smootherstep across the
        /// remaining <c>[threshold, 1]</c> band) so spray comes on sharply as the sea starts to break and keeps
        /// building to a gale, weighted by <paramref name="seaStateWeight"/>.</description></item>
        /// </list>
        /// So spray = <c>baseline + seaStateWeight · onset(seaState01, threshold)</c> where <c>onset</c> is 0
        /// below the threshold and a steep smootherstep 0→1 above it. Monotonic non-decreasing in sea-state,
        /// ~<paramref name="baseline"/> on a calm sea (default baseline 0 = OFF until the owner dials it in),
        /// clamped 0..1. Pure + static — no RNG (rule 5), the intensity the tests pin.
        /// </summary>
        public static float SprayIntensity(float seaState01, float baseline, float seaStateWeight, float threshold)
        {
            float sea = Mathf.Clamp01(seaState01);
            float th = Mathf.Clamp01(threshold);
            // Onset: 0 at/below the whitecap threshold, a steep smootherstep to 1 across the band above it. If
            // the threshold is pinned at 1 there is no band left, so the onset is simply 0 (never whitecaps).
            float band = 1f - th;
            float onset;
            if (band <= 1e-4f)
            {
                onset = 0f;
            }
            else
            {
                float t = Mathf.Clamp01((sea - th) / band);           // 0 at the threshold, 1 at a full gale
                onset = t * t * t * (t * (t * 6f - 15f) + 10f);       // smootherstep — flat foot, steep rise
            }
            float driven = Mathf.Max(0f, seaStateWeight) * onset;
            return Mathf.Clamp01(Mathf.Clamp01(baseline) + driven);
        }

        // ==== day/night response (read the global _DayNightTint, never write it) ===========================

        /// <summary>
        /// The perceived BRIGHTNESS (0..1) of the global day/night MULTIPLY tint — the luminance of
        /// <paramref name="dayNightTint"/> (Rec.709 weights). 1 at high noon (white tint), low at night
        /// (dark-blue tint). The ambient particles use this to know "how dark is it right now" without
        /// re-reading the clock — they only READ the tint the <see cref="DayNightController"/> publishes
        /// (rule 4). Clamped, NaN-safe. Pure + static.
        /// </summary>
        public static float DayNightBrightness(Color dayNightTint)
        {
            float l = 0.2126f * dayNightTint.r + 0.7152f * dayNightTint.g + 0.0722f * dayNightTint.b;
            return Mathf.Clamp01(l);
        }

        /// <summary>
        /// Map the day/night brightness to a particle's opacity multiplier, so an effect can fade with the
        /// light. <paramref name="nightFade"/> in 0..1 is how strongly night kills the effect: 0 = ignore the
        /// time of day (always full); 1 = the effect tracks brightness exactly (gone in full dark). Returns a
        /// 0..1 factor: <c>lerp(1, brightness, nightFade)</c>. So dust motes (high nightFade) vanish after
        /// dark, while mist (low nightFade, with a moonlight floor — see <see cref="MoonlightCatch"/>) only
        /// dims. Pure + static.
        /// </summary>
        public static float DayNightOpacity(float brightness, float nightFade)
            => Mathf.Clamp01(Mathf.Lerp(1f, Mathf.Clamp01(brightness), Mathf.Clamp01(nightFade)));

        /// <summary>
        /// A small additive opacity floor so mist FAINTLY catches moonlight at night instead of disappearing:
        /// strongest in the dark (low brightness), zero by day. <paramref name="moonCatch"/> in 0..1 is the
        /// max floor. Returns <c>moonCatch · (1 - brightness)</c>. The mist combines this with
        /// <see cref="DayNightOpacity"/> so it dims by day-darkness yet never blacks out entirely under a
        /// moon. Pure + static.
        /// </summary>
        public static float MoonlightCatch(float brightness, float moonCatch)
            => Mathf.Clamp01(Mathf.Max(0f, moonCatch) * (1f - Mathf.Clamp01(brightness)));

        // ==== chimney smoke column (rise + downwind bend) =================================================

        /// <summary>
        /// The world position of one smoke puff <paramref name="age"/> seconds after leaving the chimney at
        /// <paramref name="origin"/>. It RISES at <paramref name="riseSpeed"/> (m/s up the screen) and BENDS
        /// DOWNWIND: the lateral drift accelerates with height/age — a puff just out of the flue barely leans,
        /// one higher up has been pushed far downwind — giving the column its characteristic bent-over plume.
        /// The downwind push is <c>wind · windResponse · age</c> integrated as <c>0.5·a·t²</c>-style growth so
        /// it curves, not shears. <paramref name="wind"/> is the shared scene wind. Pure + static (the plume
        /// shape the tests pin).
        /// </summary>
        public static Vector2 SmokePosition(Vector2 origin, float age, float riseSpeed,
                                            Vector2 wind, float windResponse, float swayAmp, float swaySeed)
        {
            float t = Mathf.Max(0f, age);
            // Rise straight up (screen +y); buoyant plumes also slow slightly as they cool — a gentle sqrt-ish
            // ease keeps the base dense and the top lazy without needing per-puff state.
            float height = riseSpeed * t;
            // Downwind bend grows with the SQUARE of age (constant wind "acceleration" of the lateral drift),
            // so the higher/older the puff the further downwind it has been carried — the column bends over.
            Vector2 bend = wind * (windResponse * 0.5f * t * t);
            // A faint per-puff sinusoidal sway so the column breathes rather than reading as a rigid arc.
            float sway = Mathf.Sin(t * 1.7f + swaySeed * 6.2831853f) * swayAmp * Mathf.Min(1f, t * 0.5f);
            return new Vector2(origin.x + bend.x + sway, origin.y + height + bend.y);
        }

        // ==== gull flight path (looping, varied wheeling) =================================================

        /// <summary>
        /// A gull's position on a smooth, looping, wheeling path at parameter <paramref name="phase"/>
        /// (0..1 = one full loop). The path is a wandering ellipse (a Lissajous-style figure) centred on
        /// <paramref name="center"/> with semi-axes (<paramref name="radiusX"/>, <paramref name="radiusY"/>),
        /// the two axes ticking at different harmonics so the bird carves a varied, never-quite-repeating
        /// wheel rather than a plain circle. <paramref name="variant"/> (a per-gull deterministic 0..1) phase-
        /// shifts and reshapes each bird's loop so a small flock spreads out. A gentle DOWNWIND lean
        /// (<paramref name="wind"/> · <paramref name="windDrift"/>) skews the whole loop so the gulls ride the
        /// same breeze as the rest of the coast. Pure + static.
        /// </summary>
        public static Vector2 GullPosition(Vector2 center, float radiusX, float radiusY,
                                           float phase, float variant, Vector2 wind, float windDrift)
        {
            float a = phase * Mathf.PI * 2f;
            float vshift = variant * Mathf.PI * 2f;
            // Different harmonics on x and y → a wheeling figure-eight-ish wander, not a flat circle.
            float x = Mathf.Cos(a + vshift);
            float y = Mathf.Sin(a * 2f + vshift) * 0.5f + Mathf.Sin(a + vshift * 0.5f) * 0.5f;
            Vector2 onLoop = new Vector2(center.x + x * radiusX, center.y + y * radiusY);
            // Ride the breeze: a steady downwind skew of the whole loop.
            return onLoop + wind * windDrift;
        }

        /// <summary>
        /// The gull's facing (unit heading) along its path — the finite-difference tangent of
        /// <see cref="GullPosition"/>, so the sprite turns into its turns and we can flip it to face its travel
        /// direction (gulls don't fly backwards). Returns +x when degenerate so a paused bird faces east
        /// rather than NaN. Pure + static.
        /// </summary>
        public static Vector2 GullHeading(Vector2 center, float radiusX, float radiusY,
                                          float phase, float variant, Vector2 wind, float windDrift)
        {
            const float h = 1e-3f;
            Vector2 p0 = GullPosition(center, radiusX, radiusY, phase, variant, wind, windDrift);
            Vector2 p1 = GullPosition(center, radiusX, radiusY, phase + h, variant, wind, windDrift);
            Vector2 d = p1 - p0;
            return d.sqrMagnitude > 1e-10f ? d.normalized : Vector2.right;
        }

        // ==== motes (slow buoyant drift) ==================================================================

        /// <summary>
        /// A dust mote / pollen speck's vertical bob: a slow sinusoidal rise-and-fall layered on the wind
        /// drift so the motes hang and shimmer in the air rather than falling like rain. Returns the y-offset
        /// (m) to add to the drifted position. <paramref name="bobAmp"/> is the bob height, <paramref name="t"/>
        /// time, <paramref name="seed"/> the per-mote phase. Pure + static.
        /// </summary>
        public static float MoteBob(float t, float bobAmp, float seed, float bobSpeed)
            => Mathf.Sin(t * Mathf.Max(0f, bobSpeed) + seed * 6.2831853f) * bobAmp;
    }
}
