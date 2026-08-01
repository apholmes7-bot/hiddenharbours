using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Every constant of the WIND-FETCH model (ADR 0027 #1), named and owner-tunable (rule 6).
    /// Serialized on <c>GameConfig.WaveFetch</c> and read through <c>GameServices.WaveFetch</c>, so
    /// the shader bridge, the hull rocking, the seakeeping forces and the clamp all resolve the SAME
    /// numbers — the ADR 0018 §(5) one-settings-instance discipline that keeps the sea the hull rides
    /// identical to the sea the shader draws.
    ///
    /// <para><b>⚠️ These fields did not exist before 2026-07-31</b>, so any asset serialized before
    /// then deserializes them as ZERO. <see cref="Strength"/> = 0 is exactly the passthrough (the
    /// envelope is identically 1), so a stale asset is SAFE — it keeps today's sea. The shape values
    /// are floored where zero would be degenerate rather than meaningful, so dialling the strength up
    /// on a stale asset cannot produce a nonsense sea.</para>
    /// </summary>
    [Serializable]
    public struct WaveFetchSettings
    {
        [Tooltip("Master dial, 0..1. 0 = OFF: the fetch envelope is identically 1 and the sea is " +
                 "byte-identical to today's (the passthrough this ships at). 1 = the full model — " +
                 "lee shores go calm, exposed shores build.\n\n" +
                 "⚠️ This is ONE field, not a look: it scales the waves the HULL rides as well as " +
                 "the ones the shader draws. Turning it up changes boat feel.")]
        [Range(0f, 1f)] public float Strength;

        [Tooltip("Length of one upwind march step, metres. The model's total reach is this times " +
                 "WaveFetch.MarchSteps (24), so 4 m reaches 96 m upwind — comparable to a region " +
                 "rect. Longer steps see further for the same cost but stride over narrow islands.")]
        public float StepMeters;

        [Tooltip("Amplitude multiplier in a FULL lee (fetch 0) — how calm the water right behind a " +
                 "headland gets. Never 0 by default: a real lee is sheltered, not glass.")]
        [Range(0f, 1f)] public float LeeFloor;

        [Tooltip("Response curve: amplitude follows fetch^exponent. 1 = linear; >1 keeps the lee " +
                 "reaching further out before the sea builds; <1 builds it back quickly.")]
        public float Exponent;

        [Tooltip("Water depth (metres) over which the land→water gate opens. A hard cutoff would " +
                 "make the fetch POP as the tide crosses a shoal; this smooths that crossing.")]
        public float ShoreBandMeters;

        [Tooltip("Posterize the marched fetch into this many steps. 0 (or 1) = leave it SMOOTH, " +
                 "which is the default and DELIBERATE — see WaveFetch.Band01. Unlike the col.rgb " +
                 "layers, this band quantizes GEOMETRY the hull rides.")]
        public float Bands;

        /// <summary>
        /// The reference tuning — <b>OFF</b> (<see cref="Strength"/> 0), which is the exact
        /// passthrough. The shape values are the ones a review judged sane for St Peters' 160 × 120 m
        /// rect: a 96 m reach (24 × 4 m), a lee that keeps a quarter of the open-water amplitude, and
        /// a mildly super-linear response so the calm reaches a little way out from the headland.
        /// </summary>
        public static WaveFetchSettings Default => new WaveFetchSettings
        {
            Strength = 0f,          // ships OFF — the owner dials it in (rule 6, the ADR 0027 discipline)
            StepMeters = 4f,
            LeeFloor = 0.25f,
            Exponent = 1.3f,
            ShoreBandMeters = 0.5f,
            Bands = 0f,             // smooth: this band is geometry, not colour (see Band01)
        };
    }

    /// <summary>
    /// <b>ADR 0027 #1 — wind fetch, read from the height map.</b> The pure, deterministic,
    /// headless-testable model: how far the wind has blown over open water before it reaches a
    /// position, and what that does to the waves there. <b>Lee shores go calm, exposed shores
    /// build</b> — the single most gameplay-legible item on the realness pass for an island game.
    ///
    /// <para><b>⚠️ This is TIER B: ONE field, seen == felt.</b> ADR 0027 originally phased #1 as
    /// "visual first, promoted into the field later", on the reasoning that a visual-only fetch is
    /// *"an acceptable intermediate, and an explicit one"*. That intermediate is <b>not built</b>, and
    /// the reason is measured rather than argued: a shader-only damp is exactly the
    /// <c>_OceanSwellScale</c> incident — the drawn sea ran at one amplitude while the sampler behind
    /// the watertight hull clamp evaluated another, and the real crests boarded the boat on every
    /// hull, worse the rougher it got (owner playtest 2026-07-25). A fetch that calms the lee only in
    /// <c>col.rgb</c> would reintroduce that class of defect deliberately: the player would see glass
    /// behind the headland and feel the open-water swell in it. The envelope therefore lands in the
    /// ONE field both consumers read, or it does not land. Recorded as an amendment to ADR 0018.</para>
    ///
    /// <para><b>What it is, precisely: a spatial AMPLITUDE envelope on the shared field.</b>
    /// <see cref="Fetch01"/> marches upwind over the authored seabed and returns the fraction of that
    /// march that was open water; <see cref="Amplitude01"/> maps it to a multiplier in
    /// [<c>LeeFloor</c>, 1]; every consumer multiplies the field's height and slope by it.</para>
    ///
    /// <para><b>⚠️ Fetch modulates AMPLITUDE ONLY — never wavelength, never speed.</b> Real fetch sets
    /// both, and the temptation to scale wavelength here is strong. It is wrong three times over.
    /// (a) A per-POSITION wavelength is not a wave field: the analytic slope
    /// <c>A·p·s^(p−1)·cos θ·k·d</c>, the phase continuity the animator accumulates, and the
    /// crest-factor normalizer all assume ONE k per train across space — vary k with position and the
    /// slope stops being the derivative of the height, which is the number the deck tilt and the
    /// windward gates read. (b) Speed is not ours to set: <see cref="WaveTrain.PhaseSpeed"/> already
    /// IS the dispersion relation, derived from the wavelength at construction, and ADR 0027 P2's
    /// audit found that re-applying the visual-octave laws to the field would DOUBLE-apply them.
    /// (c) <c>freqScale</c> is likewise off limits — it is the displaced vertex stage's own knob, and
    /// the clamp scans against it. Amplitude is the one term that can be varied spatially and stay a
    /// coherent field.</para>
    ///
    /// <para><b>The slowly-varying-envelope approximation, stated honestly.</b> Scaling the height by
    /// <c>E(pos)</c> makes the true gradient <c>∇(E·h) = E·∇h + h·∇E</c>; consumers apply only the
    /// first term. The error is bounded by how fast E varies against how fast h does: E turns over the
    /// FETCH scale (tens of metres — <see cref="MarchSteps"/> × <c>StepMeters</c>) while h turns over
    /// the wavelength (metres), so <c>|h·∇E| ≪ |E·∇h|</c> everywhere the model is used. Computing ∇E
    /// honestly would cost two more 24-step marches per sample for a term that is small by
    /// construction. The same approximation is what licenses evaluating ONE envelope per hull rather
    /// than one per probe: a hull is ~5 m and the envelope does not meaningfully turn over that span.</para>
    ///
    /// <para><b>What falls out for free.</b> The crest factor is <c>height / totalAmplitude</c>, and
    /// the envelope scales the numerator but not the denominator — so a lee shore loses its whitecaps
    /// without anything being wired to do that. That is correct physics and it is deliberate; it is
    /// also why the envelope must NOT be folded into <c>TotalAmplitude</c>.</para>
    ///
    /// <para><b>Why the clamp is safe.</b> The envelope is ≤ 1 by construction, so the fetched sea is
    /// never taller than the unfetched one the watertight hull clamp was calibrated against. The clamp
    /// therefore stays a valid bound at any strength — it can only ever over-dry, never flood, which
    /// is the direction ADR 0023 already chose for the shore fade.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Pure and allocation-free: a function of (position, wind,
    /// water level, authored seabed, settings). The wind is already deterministic from
    /// <c>(worldSeed, gameTime)</c> and the seabed is authored data, so the envelope is too. No RNG,
    /// nothing saved — recomputed like tide and wind.</para>
    ///
    /// <para><b>The HLSL twin contract.</b> <c>HiddenHarboursWater.shader</c> carries a line-for-line
    /// transcription (<c>FetchMarch01</c> / <c>FetchAmplitude01</c> / <c>FetchBand01</c> /
    /// <c>FetchEnvelope01</c>) — change one, change BOTH in the same PR, the
    /// <c>WaveMath</c>/<c>WaterRipple</c> discipline. <c>WaveFetchTests</c> pins this side.</para>
    ///
    /// <para><b>⚠️ The C#-twin trap.</b> <c>Mathf.SmoothStep(a, b, t)</c> is a smooth LERP between two
    /// VALUES — it is NOT HLSL's <c>smoothstep(e0, e1, x)</c>, which is an EDGE GATE returning 0..1.
    /// <see cref="SmoothstepEdge"/> spells the edge form out; using the Mathf overload would silently
    /// pin the wrong law.</para>
    /// </summary>
    public static class WaveFetch
    {
        /// <summary>
        /// The march's <b>FIXED</b> iteration count — the shader's <c>[unroll]</c> bound.
        ///
        /// <para>⚠️ ADR 0027 states this as an implementation constraint, not a preference:
        /// <c>WaterShaderCompileGuardTests</c> guards the magenta class, and <c>[unroll]</c> over a
        /// RUNTIME bound is one of its known traps (the #96 magenta incident). The upwind reach is
        /// therefore tuned through <c>WaveFetchSettings.StepMeters</c>, never by marching a variable
        /// number of steps. This constant is one half of a seam: its HLSL counterpart is
        /// <c>FETCH_MARCH_STEPS</c>, and they move together in one commit.</para>
        /// </summary>
        public const int MarchSteps = 24;

        /// <summary>
        /// World pixels per unit for the march's coordinate quantization — the PPU the project is
        /// actually locked to (<c>PixelPerfectCamera.assetsPPU</c> = 32, mirrored by
        /// <c>CameraFollow.AssetsPPU</c>). The march samples on <b>world</b>-quantized coordinates so
        /// the fetch cannot crawl under camera translation (ADR 0027's crawl law).
        ///
        /// <para><b>⚠️ This is NOT the shader's <c>_PixelsPerUnit</c>, and must never be wired to it.</b>
        /// That property is an ART knob — the shipped <c>Water.mat</c> sets it 24 and the presets set 12,
        /// so its Properties-block default of 32 never ships. Quantizing the march through it would put
        /// the two sides on grids centimetres apart per step, flip a wet/dry step at a hard painted coast,
        /// and land the DRAWN lee boundary on a different line than the FELT one — the seen != felt split
        /// this item exists to close, handed back to a tuning knob. The shader therefore carries its own
        /// <c>FETCH_MARCH_PPU</c> / <c>FetchPixelize</c>; this constant is the other half of that seam and
        /// they move together in one commit (pinned by <c>MarchPixelGrid_MatchesTheShader</c>).</para>
        /// </summary>
        public const float PixelsPerUnit = 32f;

        /// <summary>Narrowest band a gate's two edges may collapse to (mirrors the shader's
        /// <c>max(hi, lo + 1e-3)</c> guards) — an <c>smoothstep</c> with <c>e0 == e1</c> is undefined
        /// on some GPUs.</summary>
        public const float MinGateBand = 1e-3f;

        /// <summary>Fewest posterize steps that mean anything (the <c>_DepthBands</c> /
        /// <c>_AbsorptionBands</c> / <c>_RippleBands</c> "0 = off" idiom).</summary>
        public const float MinBands = 2f;

        /// <summary>Floor on the amplitude curve's <c>pow</c> base, mirroring the shader's
        /// <c>max(saturate(f), 1e-6)</c>. <c>pow(0, e)</c> is undefined in HLSL, so the shader floors it;
        /// this twin floors it identically rather than diverging in a dead lee. A guard, not a
        /// tunable.</summary>
        public const float MinPowBase = 1e-6f;

        /// <summary>Floor on the march step (metres) so a zeroed/stale settings struct cannot collapse
        /// the march onto a single point. A guard, not a tunable.</summary>
        public const float MinStepMeters = 0.05f;

        /// <summary>Wind speeds at or below this (m/s) are treated as dead calm: the march has no
        /// defined direction, so the envelope is 1 and the field's own amplitudes — which are what
        /// silence a calm sea — decide the look. A guard, not a tunable.</summary>
        public const float CalmWindSpeed = 1e-4f;

        /// <summary>
        /// HLSL's <c>smoothstep(e0, e1, x)</c>, spelled out. <b>NOT <c>Mathf.SmoothStep</c></b> — see
        /// the class note. 0 at/below <paramref name="edge0"/>, 1 at/above <paramref name="edge1"/>,
        /// Hermite between.
        /// </summary>
        public static float SmoothstepEdge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, MinGateBand));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The shader's <c>Pixelize</c>, character for character: snap a world position to
        /// the world PPU grid (<c>floor(p·ppu)/ppu</c>). World-locked, so it cannot crawl under camera
        /// translation.</summary>
        public static Vector2 Pixelize(Vector2 p)
        {
            const float ppu = PixelsPerUnit;
            return new Vector2(Mathf.Floor(p.x * ppu) / ppu, Mathf.Floor(p.y * ppu) / ppu);
        }

        /// <summary>
        /// <b>THE MARCH.</b> Step upwind from <paramref name="worldPos"/> in <see cref="MarchSteps"/>
        /// FIXED steps of <c>settings.StepMeters</c>, and return the fraction of that span that was
        /// open water — 1 = the whole reach is sea (a fully exposed shore), 0 = land immediately
        /// upwind (a deep lee).
        ///
        /// <para><b>Land shadows everything behind it.</b> The accumulator is a PRODUCT of per-step
        /// wetness, not a count of wet samples: once the march crosses a headland the running
        /// <c>blocked</c> term collapses and every step beyond it contributes nothing, however deep
        /// the water out there is. That is what fetch means — open water on the far side of an island
        /// is not fetch for this position. It is also branch-free, which is what lets the HLSL twin
        /// keep a fixed <c>[unroll]</c> with no early exit.</para>
        ///
        /// <para><b>The wetness gate is smooth, not a cutoff.</b> A hard <c>depth &gt; 0</c> test
        /// would make the whole envelope POP as the tide crossed a shoal — a discontinuity in the
        /// waves the hull rides, arriving on the tide's schedule. <c>ShoreBandMeters</c> is the depth
        /// over which the gate opens.</para>
        ///
        /// <para><b>Dead calm returns 1</b>: with no wind there is no upwind direction to march, and
        /// the field's own amplitudes already silence a calm sea (glass is sacred, ADR 0018 §(1)).
        /// A null <paramref name="terrain"/> also returns 1 — no height map means no land to shelter
        /// anything, the same "everywhere deep" fallback the shader takes without
        /// <c>_USE_HEIGHTTEX</c>.</para>
        /// </summary>
        /// <param name="worldPos">Where the fetch is being asked about (world XY).</param>
        /// <param name="windVector">Wind as direction × strength (m/s) — <c>EnvironmentSample.WindVector</c>.
        /// The march runs along <b>−wind</b>: into the wind, toward where the weather came from.</param>
        /// <param name="waterLevelMeters">The active region's water level (metres above chart datum) —
        /// <c>IEnvironmentService.WaterLevelAt</c>. Land is seabed at or above this.</param>
        /// <param name="terrain">The authored seabed (<c>GameServices.TidalTerrain</c>). Sampled
        /// <see cref="MarchSteps"/> times; null = open water everywhere.</param>
        /// <param name="settings">The model's tunables.</param>
        public static float Fetch01(Vector2 worldPos, Vector2 windVector, float waterLevelMeters,
                                    ITidalTerrain terrain, in WaveFetchSettings settings)
        {
            if (terrain == null) return 1f;

            float windSpeed = Mathf.Sqrt(windVector.x * windVector.x + windVector.y * windVector.y);
            if (windSpeed <= CalmWindSpeed) return 1f;

            // Upwind: the direction the weather came FROM. The primary train runs downwind, so this
            // is anti-parallel to the way the crests advance.
            float invSpeed = 1f / windSpeed;
            Vector2 upwind = new Vector2(-windVector.x * invSpeed, -windVector.y * invSpeed);

            float step = Mathf.Max(MinStepMeters, settings.StepMeters);
            float band = Mathf.Max(MinGateBand, settings.ShoreBandMeters);

            float open = 0f;
            float blocked = 1f;
            for (int i = 1; i <= MarchSteps; i++)          // FIXED bound — the HLSL [unroll] contract
            {
                Vector2 p = Pixelize(new Vector2(worldPos.x + upwind.x * (step * i),
                                                 worldPos.y + upwind.y * (step * i)));
                float depth = waterLevelMeters - terrain.ElevationAt(p);
                float wet = SmoothstepEdge(0f, band, depth);
                blocked *= wet;                            // land shadows everything beyond it
                open += blocked;
            }

            return open / MarchSteps;
        }

        /// <summary>
        /// Map a marched fetch to the AMPLITUDE multiplier: <c>lerp(leeFloor, 1, fetch^exponent)</c>.
        /// Monotone increasing in fetch, exactly <c>leeFloor</c> at fetch 0 and exactly 1 at fetch 1
        /// — so a fully exposed shore is untouched, which is what keeps the open sea identical to the
        /// sea the field was tuned against.
        /// </summary>
        public static float Amplitude01(float fetch01, float exponent, float leeFloor)
        {
            // MinPowBase mirrors the shader's max(saturate(f), 1e-6) — pow(0, e) is a documented
            // undefined-behaviour corner in HLSL, so the shader floors the base and this twin must too or
            // the two sides diverge (~1e-8) exactly in a dead lee. Line-for-line means line-for-line.
            float f = Mathf.Pow(Mathf.Max(Mathf.Clamp01(fetch01), MinPowBase), Mathf.Max(0.01f, exponent));
            return Mathf.Lerp(Mathf.Clamp01(leeFloor), 1f, f);
        }

        /// <summary>
        /// The model's own quantization — ADR 0027's per-layer pixelation control, carried as the ADR
        /// requires. Posterizes 0..1 into <paramref name="bands"/> solid steps (round-to-nearest);
        /// fewer than <see cref="MinBands"/> leaves the value SMOOTH.
        ///
        /// <para><b>⚠️ This one defaults OFF, and that is a deliberate departure from the ADR's
        /// "defaulting ON".</b> That mandate exists so <c>col.rgb</c> layers do not read as an
        /// airbrushed gradient over a base ramp that carries no pixel character of its own
        /// (<c>_DepthBands</c> is 0). This envelope is <b>not drawn</b> — it scales GEOMETRY that is
        /// then drawn by layers which are already pixelized downstream, so the pixel character is
        /// paid for either way. Banding it ON by default would stair-step the surface the hull rides,
        /// and the boat would feel the steps. There is also, deliberately, <b>no dither here</b>
        /// (unlike <c>WaterRipple.Band01</c>): a dithered amplitude would have neighbouring pixels of
        /// the sea riding different wave heights, and the hull twin cannot dither at all.</para>
        /// </summary>
        public static float Band01(float v01, float bands)
        {
            if (bands < MinBands) return Mathf.Clamp01(v01);
            float steps = bands - 1f;
            // ⚠️ floor(x + 0.5), NOT Mathf.Round: Mathf.Round is BANKER'S rounding (half-to-even) while
            // HLSL's round() is half-away-from-zero. Exact half-band inputs are reachable — the march
            // yields exact n/24-class fractions wherever the wetness smoothstep saturates — so the two
            // sides would pick ADJACENT bands. Spelled identically in the shader's FetchBand01.
            return Mathf.Floor(Mathf.Clamp01(v01) * steps + 0.5f) / steps;
        }

        /// <summary>
        /// <b>THE ENVELOPE</b> — the finished multiplier every consumer applies to the shared field's
        /// height and slope: the banded fetch through the amplitude curve, blended from 1 by the
        /// master strength.
        ///
        /// <para><b><paramref name="strength"/> = 0 returns EXACTLY 1</b> — the passthrough this ships
        /// at, so the sea (drawn AND ridden) is byte-identical until the owner dials it in. Bounded to
        /// (0, 1] for any input, which is the property that keeps the watertight hull clamp a valid
        /// bound at every strength (see the class note).</para>
        /// </summary>
        public static float Envelope01(float fetch01, in WaveFetchSettings settings)
        {
            float strength = Mathf.Clamp01(settings.Strength);
            if (strength <= 0f) return 1f;                 // exact passthrough, no float round-trip
            float banded = Band01(fetch01, settings.Bands);
            float amplitude = Amplitude01(banded, settings.Exponent, settings.LeeFloor);
            return Mathf.Lerp(1f, amplitude, strength);
        }

        /// <summary>
        /// March and map in one call — the whole model at a position. This is the entry point the
        /// consumers use; <see cref="Fetch01"/> and <see cref="Envelope01"/> are split out so each
        /// half can be pinned on its own.
        /// </summary>
        public static float EnvelopeAt(Vector2 worldPos, Vector2 windVector, float waterLevelMeters,
                                       ITidalTerrain terrain, in WaveFetchSettings settings)
        {
            // Cheapest possible OFF: skip the 24 terrain samples entirely when the dial is down.
            if (Mathf.Clamp01(settings.Strength) <= 0f) return 1f;
            return Envelope01(Fetch01(worldPos, windVector, waterLevelMeters, terrain, in settings),
                              in settings);
        }
    }
}
