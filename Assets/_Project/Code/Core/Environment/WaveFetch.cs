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

        [Tooltip("Shortest-train amplitude floor in a lee. Tapers to zero at FloorMaxWavelengthMeters. " +
                 "A silent field remains silent regardless of this factor.")]
        [Range(0f, 1f)] public float LeeFloor;

        [Tooltip("Legacy uniform-envelope exponent; retained for scalar fixtures. Spectral growth uses a smoothstep.")]
        public float Exponent;

        [Tooltip("Water depth (metres) over which the land→water gate opens. A hard cutoff would " +
                 "make the fetch POP as the tide crosses a shoal; this smooths that crossing.")]
        public float ShoreBandMeters;

        [Tooltip("Posterize the marched fetch into this many steps. 0 (or 1) = leave it SMOOTH, " +
                 "which is the default and DELIBERATE — see WaveFetch.Band01. Unlike the col.rgb " +
                 "layers, this band quantizes GEOMETRY the hull rides.")]
        public float Bands;

        [Tooltip("Shortest wavelength of the fetch curve, metres; zero uses 6.5.")]
        [Range(1f, 100f)] public float ShortWavelengthMeters;
        [Tooltip("Wavelength needing the entire march reach, metres; zero uses 30.")]
        [Range(1f, 100f)] public float LongWavelengthMeters;
        [Tooltip("Clear fetch needed by the shortest train, metres; zero uses 8.")]
        [Range(0.05f, 288f)] public float ShortFetchMeters;
        [Tooltip("Wavelength at which the short-chop lee floor reaches zero; zero uses 8.5 m.")]
        [Range(1f, 100f)] public float FloorMaxWavelengthMeters;
        [Tooltip("Exponent of the wavelength-to-fetch distance curve; zero uses 0.25.")]
        [Range(0.05f, 4f)] public float GrowthExponent;
        [Tooltip("Strength reaching full spectral shelter; zero uses 0.8. Lower values reach it sooner.")]
        [Range(0.01f, 1f)] public float FullShelterStrength;

        [Tooltip("The shore fringe in a dead lee: the wind roughness its foam keeps there (0..1, the wind " +
                 "over 12 m/s). Zero uses 0.2, a light breeze of about 2.4 m/s; 1 = the fringe ignores the " +
                 "lee. Look only: the hull never reads it.")]
        [Range(0f, 1f)] public float FringeLeeRoughness;
        [Tooltip("The same for the swash, the drawn edge's and the fringe's, on the sea-state axis (0 glass " +
                 ".. 1 storm). Zero uses 0.315, the same light breeze; 1 = the swash ignores the lee. Look only.")]
        [Range(0f, 1f)] public float FringeLeeSeaState;
        [Tooltip("The dominant train's fetch factor at and above which the shore fringe and the drawn edge's " +
                 "swash read as open water; below it their lee grows linearly to a dead lee at 0. Zero uses 0.9: " +
                 "a shallow first march step reads an open beach's wet edge a little under 1. Look only.")]
        [Range(0f, 1f)] public float FringeOpenFactor;

        /// <summary>Code defaults keep fetch off. The serialized config enables spectral shelter.
        /// The shortest band recovers over eight metres; longer waves need the broader reach.</summary>
        public static WaveFetchSettings Default => new WaveFetchSettings
        {
            Strength = 0f,          // ships OFF — the owner dials it in (rule 6, the ADR 0027 discipline)
            StepMeters = 4f,
            LeeFloor = 0.25f,
            Exponent = 1.3f,
            ShoreBandMeters = 0.5f,
            ShortWavelengthMeters = 6.5f,
            LongWavelengthMeters = 30f,
            ShortFetchMeters = 8f,
            FloorMaxWavelengthMeters = 8.5f,
            FullShelterStrength = 0.8f,
            GrowthExponent = 0.25f,
            Bands = 0f,             // smooth: this band is geometry, not colour (see Band01)
            FringeLeeRoughness = 0.2f,
            FringeLeeSeaState = 0.315f,
            FringeOpenFactor = 0.9f,
        };
    }

    /// <summary>One fixed march supplies a clear-reach fraction to every wavelength.
    /// Spectral fetch scales amplitudes, never wavelength, speed or phase. Crest normalization
    /// retains the unfetched sum. Slope reports E times the wave gradient; the omitted h times
    /// envelope gradient is documented and bounded in ADR 0018 Stage B.</summary>
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
            Vector2 upwind = Upwind(windVector);

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

        public static Vector2 Upwind(Vector2 windVector)
        {
            float speed = Mathf.Sqrt(windVector.x * windVector.x + windVector.y * windVector.y);
            if (speed <= CalmWindSpeed) return Vector2.zero;
            float inverse = 1f / speed;
            return new Vector2(-windVector.x * inverse, -windVector.y * inverse);
        }

        public static float Positive(float value, float fallback, float minimum, float maximum)
            => Mathf.Clamp(value > 0f ? value : fallback, minimum, maximum);

        public static Vector4 GrowthShape(in WaveFetchSettings s) => new Vector4(
            Positive(s.ShortWavelengthMeters, 6.5f, 1f, 100f),
            Positive(s.LongWavelengthMeters, 30f, 1f, 100f),
            Positive(s.ShortFetchMeters, 8f, MinStepMeters, 288f),
            Positive(s.FloorMaxWavelengthMeters, 8.5f, 1f, 100f));

        public static float GrowthPower(in WaveFetchSettings s)
            => Positive(s.GrowthExponent, 0.25f, 0.05f, 4f);

        public static float FullStrength(in WaveFetchSettings s)
            => Positive(s.FullShelterStrength, 0.8f, 0.01f, 1f);

        /// <summary>The shore fringe's lee, as the shader's <c>_WaveFetchFringe</c>: x = the roughness its
        /// foam keeps in a dead lee, y = the sea state its swash keeps there, z = the fetch factor at and above
        /// which it reads as open water. Zero fields use the defaults, so z is never zero here; the shader
        /// reads z = 0 as unpublished (no lee).</summary>
        public static Vector4 FringeLee(in WaveFetchSettings s) => new Vector4(
            Positive(s.FringeLeeRoughness, 0.2f, 0f, 1f),
            Positive(s.FringeLeeSeaState, 0.315f, 0f, 1f),
            Positive(s.FringeOpenFactor, 0.9f, 0.01f, 1f),
            0f);

        /// <summary>How open the shore is for the fringe, from the dominant train's fetch factor: 1 at and
        /// above <paramref name="openFactor"/>, falling linearly to 0 in a dead lee; a zero
        /// <paramref name="openFactor"/> reads as open. Twin: the shader's FringeLeeOpen01.</summary>
        public static float FringeLeeOpen01(float fetchFactor, float openFactor)
            => openFactor > 0f ? Mathf.Clamp01(fetchFactor / openFactor) : 1f;

        /// <summary>A fringe reading in the lee: above <paramref name="ceiling"/> it falls toward it by the
        /// lee's share; at or under the ceiling, or on open water, it is returned as is, to the bit.
        /// Twin: the shader's FringeLeeReading.</summary>
        public static float FringeLeeReading(float reading, float ceiling, float open01)
            => (open01 < 1f && reading > ceiling) ? Mathf.Lerp(ceiling, reading, open01) : reading;

        /// <summary>A stylised fetch-limited growth law. The required distance grows by a data exponent
        /// between the two wavelength endpoints, then a Hermite gate grows each train over that
        /// distance. Full reach and zero strength return exactly one before any curve arithmetic.</summary>
        public static float TrainEnvelope01(float fetch01, float wavelength, in WaveFetchSettings settings)
        {
            if (settings.Strength <= 0f || fetch01 >= 1f) return 1f;
            Vector4 shape = GrowthShape(in settings);
            float reach = MarchSteps * Mathf.Max(MinStepMeters, settings.StepMeters);
            float u = Mathf.Clamp01((wavelength - shape.x) / Mathf.Max(shape.y - shape.x, MinGateBand));
            float curve = u > 0f ? Mathf.Pow(u, GrowthPower(in settings)) : 0f;
            float needed = Mathf.Lerp(Mathf.Min(shape.z, reach), reach, curve);
            float floor = Mathf.Clamp01(settings.LeeFloor)
                        * (1f - SmoothstepEdge(shape.x, Mathf.Max(shape.w, shape.x + MinGateBand), wavelength));
            float growth = SmoothstepEdge(0f, needed, Band01(fetch01, settings.Bands) * reach);
            float amplitude = Mathf.Lerp(floor, 1f, growth);
            float strength = Mathf.Clamp01(settings.Strength / FullStrength(in settings));
            return Mathf.Lerp(1f, amplitude, strength);
        }

        public static WaveFetchSample At(Vector2 worldPos, Vector2 windVector, float waterLevelMeters,
                                          ITidalTerrain terrain, in WaveFetchSettings settings)
        {
            if (settings.Strength <= 0f) return WaveFetchSample.Open;
            return new WaveFetchSample(Fetch01(worldPos, windVector, waterLevelMeters, terrain, in settings),
                                       in settings);
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
        /// Legacy uniform envelope, retained for scalar fixtures. Runtime readers use TrainEnvelope01.
        /// The finished multiplier applies to the shared field's
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

    /// <summary>Allocation-free fetch payload. One march, any number of train evaluations.
    /// The scalar conversion preserves callers supplying an explicitly uniform envelope.</summary>
    public readonly struct WaveFetchSample
    {
        public readonly float Fraction;
        private readonly WaveFetchSettings _settings;
        private readonly float _uniform;
        private readonly bool _spectral;
        private readonly bool _hasSwell;
        private readonly float _swell;
        public static WaveFetchSample Open => new WaveFetchSample(1f);
        private WaveFetchSample(float uniform)
        {
            Fraction = 1f; _settings = default; _uniform = Mathf.Clamp01(uniform); _spectral = false;
            _hasSwell = false; _swell = 1f;
        }
        public WaveFetchSample(float fraction, in WaveFetchSettings settings)
        {
            Fraction = fraction; _settings = settings; _uniform = 1f; _spectral = true;
            _hasSwell = false; _swell = 1f;
        }
        private WaveFetchSample(in WaveFetchSample wind, float swell)
        {
            Fraction = wind.Fraction; _settings = wind._settings; _uniform = wind._uniform;
            _spectral = wind._spectral; _hasSwell = true; _swell = Mathf.Clamp01(swell);
        }
        public WaveFetchSample WithSwell(float shelter) => new WaveFetchSample(in this, shelter);
        public float ForTrain(int slot, float wavelength)
            => _hasSwell && slot >= SwellMath.FirstSlot ? _swell : ForWavelength(wavelength);
        public float ForWavelength(float wavelength)
            => _spectral ? WaveFetch.TrainEnvelope01(Fraction, wavelength, in _settings) : _uniform;
        public float Uniform => _spectral || _hasSwell ? 1f : _uniform;
        public bool Spectral => _spectral || _hasSwell;
        public bool HasSwell => _hasSwell;
        public static implicit operator WaveFetchSample(float uniform) => new WaveFetchSample(uniform);
    }

}
