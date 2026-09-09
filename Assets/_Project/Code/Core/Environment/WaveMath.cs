using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One directional wave train — a single member of the shared deterministic wave field
    /// (ADR 0018): a travel direction, a wavelength, an amplitude and a phase offset.
    ///
    /// <para><b>Dispersion is canon (owner ruling, ADR 0018 §(1)).</b> <see cref="PhaseSpeed"/> is
    /// <b>derived</b> in the constructor from the deep-water dispersion relation
    /// <c>c = √(g·λ / 2π)</c> — "the larger the distance between crests, the faster the wave" — so
    /// long swells visibly outrun short chop and a mixed sea reads true. A train carries its
    /// wavelength; its speed is <b>never an independent tunable</b>. There is deliberately no way to
    /// construct a train with a free-standing speed.</para>
    /// </summary>
    public readonly struct WaveTrain
    {
        /// <summary>Guard floor for <see cref="Wavelength"/> (metres) so a degenerate/zeroed input can
        /// never divide by zero in the wave number <c>k = 2π/λ</c>. A guard, not a tunable.</summary>
        public const float MinWavelengthMeters = 0.01f;

        /// <summary>Unit direction of travel in world space — the way the crests advance
        /// (downwind for the primary train). Normalized by the constructor; a (near-)zero input falls
        /// back to +Y (north) as a defined, deterministic direction.</summary>
        public readonly Vector2 Direction;

        /// <summary>Crest-to-crest wavelength λ (metres), clamped ≥ <see cref="MinWavelengthMeters"/>.</summary>
        public readonly float Wavelength;

        /// <summary>Amplitude (metres) — half the crest-to-trough height, clamped ≥ 0. Exactly 0 means
        /// this train contributes nothing (glass calm is sacred: no minimum swell — ADR 0018 §(1)).</summary>
        public readonly float Amplitude;

        /// <summary>Phase speed c (m/s) — <b>derived</b> from <see cref="Wavelength"/> via
        /// <c>c = √(g·λ / 2π)</c> at construction. Read-only by design; see the struct doc.</summary>
        public readonly float PhaseSpeed;

        /// <summary>Phase offset (radians) — deterministic variety so the trains do not all crest
        /// together at t = 0. Hashed from the train index + a seed, never random (rule 5).</summary>
        public readonly float PhaseOffset;

        /// <summary>
        /// Build a train. <paramref name="direction"/> is normalized (near-zero → +Y), the wavelength
        /// and amplitude are clamped to sane floors, and the phase speed is derived from
        /// <paramref name="gravity"/> + wavelength via the dispersion relation — the one place that
        /// formula lives on the C# side (the HLSL twin receives the derived speed as data and never
        /// re-derives it, so the relation cannot fork).
        /// </summary>
        public WaveTrain(Vector2 direction, float wavelengthMeters, float amplitudeMeters,
                         float phaseOffsetRadians, float gravity)
        {
            float sqrMagnitude = direction.x * direction.x + direction.y * direction.y;
            if (sqrMagnitude < 1e-12f)
            {
                Direction = Vector2.up;
            }
            else
            {
                float invMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
                Direction = new Vector2(direction.x * invMagnitude, direction.y * invMagnitude);
            }

            Wavelength = Mathf.Max(MinWavelengthMeters, wavelengthMeters);
            Amplitude = Mathf.Max(0f, amplitudeMeters);
            PhaseSpeed = Mathf.Sqrt(Mathf.Max(0f, gravity) * Wavelength / (2f * Mathf.PI));
            PhaseOffset = phaseOffsetRadians;
        }
    }

    /// <summary>
    /// The whole wave field for one moment's weather: a fixed, allocation-free container of up to
    /// <see cref="MaxTrains"/> <see cref="WaveTrain"/>s plus the crest-sharpening factor they share.
    /// Produced by <see cref="WaveMath.TrainsFrom"/> on the throttled sim cadence and handed to
    /// <see cref="WaveMath.Sample"/> (sim side) or published as shader globals by the Art-side bridge
    /// (B1) — the same trains on both sides, by construction.
    /// </summary>
    public readonly struct WaveTrains
    {
        /// <summary>
        /// Hard capacity of the container (and of the shader-global arrays the HLSL twin reads).
        ///
        /// <para><b>Widened 4 → 8 for ADR 0027 P2</b> (the ADR 0018 amendment). ADR 0018 fixed the
        /// model at 3–4 trains because a hand-authored cross-chop is all four slots can express; a
        /// JONSWAP-shaped <em>spectrum</em> needs enough frequencies to carry a shape AND to let
        /// neighbouring ones beat into wave GROUPS, and four cannot do both. 8 is a measured choice
        /// — see the cost report in the PR and the amendment.</para>
        ///
        /// <para>⚠️ <b>This constant is the seam's width, and it is load-bearing in four places at
        /// once</b>: this container, <c>WaveFieldBridge</c>'s uniform push, the shader's
        /// <c>WAVE_MAX_TRAINS</c> fixed loop bound, and <c>DisplacedWaterMath</c>'s clamp scan. They
        /// widen together, in one commit, or the sea the hull rides stops being the sea the shader
        /// draws.</para>
        /// </summary>
        public const int MaxTrains = 8;

        private readonly WaveTrain _train0;
        private readonly WaveTrain _train1;
        private readonly WaveTrain _train2;
        private readonly WaveTrain _train3;
        private readonly WaveTrain _train4;
        private readonly WaveTrain _train5;
        private readonly WaveTrain _train6;
        private readonly WaveTrain _train7;

        /// <summary>How many of the slots are live, in [0, <see cref="MaxTrains"/>]. Slots at or
        /// beyond this index are undefined and must not be read.</summary>
        public readonly int Count;

        /// <summary>Crest sharpening p ≥ 1 (clamped at construction): the cheap shaping exponent that
        /// pinches crests narrow above broad troughs (ADR 0018 §(1)). 1 = pure sine, higher = spikier
        /// crests. Also concentrates <see cref="WaveSample.CrestFactor"/> toward the crest tips.</summary>
        public readonly float CrestSharpening;

        /// <summary>
        /// The slot carrying the <b>spectral peak</b> — the train whose phase the rocking consumers
        /// read forward (<see cref="WaveMath.TrainPhaseDegrees"/>, <c>BoatWaveMotion</c>, the buoys,
        /// the drift weed) and whose face sign the shader's whitecap lifecycle keys on. Clamped into
        /// [0, <see cref="Count"/>).
        ///
        /// <para><b>Why this is a field and not "slot 0 by convention".</b> Under the flat weighting
        /// the biggest train is always the downwind primary in slot 0, so every consumer took
        /// <c>trains[0]</c> and was right by accident. A JONSWAP re-weighting moves the peak, and the
        /// accidental convention would then have the hull rocking to one train while the foam broke
        /// on another — silently, with nothing red. Naming the choice is what stops that; under
        /// today's flat weighting it is still 0, and a test pins exactly that.</para>
        /// </summary>
        public readonly int DominantIndex;

        /// <summary>Assemble a field from all <see cref="MaxTrains"/> slots. <paramref name="count"/>
        /// is clamped into [0, <see cref="MaxTrains"/>], <paramref name="crestSharpening"/> ≥ 1, and
        /// <paramref name="dominantIndex"/> into the live range. Unused slots may be
        /// <c>default</c>.</summary>
        public WaveTrains(in WaveTrain train0, in WaveTrain train1, in WaveTrain train2,
                          in WaveTrain train3, in WaveTrain train4, in WaveTrain train5,
                          in WaveTrain train6, in WaveTrain train7,
                          int count, float crestSharpening, int dominantIndex)
        {
            _train0 = train0;
            _train1 = train1;
            _train2 = train2;
            _train3 = train3;
            _train4 = train4;
            _train5 = train5;
            _train6 = train6;
            _train7 = train7;
            Count = Mathf.Clamp(count, 0, MaxTrains);
            CrestSharpening = Mathf.Max(1f, crestSharpening);
            DominantIndex = Count > 0 ? Mathf.Clamp(dominantIndex, 0, Count - 1) : 0;
        }

        /// <summary>The narrow assembly — four trains, peak in slot 0. Kept because the hand-authored
        /// primary + three-secondary field IS four trains and reads clearest that way, and because it
        /// is the shape every pre-P2 caller was written against.</summary>
        public WaveTrains(in WaveTrain train0, in WaveTrain train1, in WaveTrain train2,
                          in WaveTrain train3, int count, float crestSharpening)
            : this(train0, train1, train2, train3, default, default, default, default,
                   count, crestSharpening, 0)
        {
        }

        /// <summary>
        /// Assemble from a caller-owned buffer of at least <paramref name="count"/> trains — the
        /// shape the spectrum derivation and <see cref="WaveFieldAnimator"/> want (both already hold
        /// per-slot arrays, so this spares them an eight-argument call). Allocation-free: the buffer
        /// is READ, never retained (rule 7).
        /// </summary>
        public static WaveTrains From(WaveTrain[] source, int count, float crestSharpening,
                                      int dominantIndex)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return From((ReadOnlySpan<WaveTrain>)source, count, crestSharpening, dominantIndex);
        }

        /// <summary>The same assembly from a span — what lets the spectrum derivation build its slots
        /// in <c>stackalloc</c> scratch and stay allocation-free on the sim cadence (rule 7).</summary>
        public static WaveTrains From(ReadOnlySpan<WaveTrain> source, int count, float crestSharpening,
                                      int dominantIndex)
        {
            int live = Mathf.Clamp(count, 0, Mathf.Min(MaxTrains, source.Length));
            return new WaveTrains(
                live > 0 ? source[0] : default, live > 1 ? source[1] : default,
                live > 2 ? source[2] : default, live > 3 ? source[3] : default,
                live > 4 ? source[4] : default, live > 5 ? source[5] : default,
                live > 6 ? source[6] : default, live > 7 ? source[7] : default,
                live, crestSharpening, dominantIndex);
        }

        /// <summary>The train in a live slot (index in [0, <see cref="Count"/>)).</summary>
        public WaveTrain this[int index] => index switch
        {
            0 => _train0,
            1 => _train1,
            2 => _train2,
            3 => _train3,
            4 => _train4,
            5 => _train5,
            6 => _train6,
            7 => _train7,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index,
                     "WaveTrains holds at most " + MaxTrains + " trains."),
        };

        /// <summary>The spectral-peak train (<see cref="DominantIndex"/>) — what the phase consumers
        /// read instead of <c>trains[0]</c>. A silent (zero-amplitude) peak still has a defined
        /// direction, wavelength and phase; an EMPTY field returns <c>default</c>.</summary>
        public WaveTrain Dominant => Count > 0 ? this[DominantIndex] : default;

        /// <summary>Sum of the live trains' amplitudes (metres) — the field's height envelope: the
        /// tallest possible crest above (and deepest trough below) the tide level. 0 = dead glass.
        /// The same normalizer <see cref="WaveMath.Sample"/> uses for the crest factor.</summary>
        public float TotalAmplitude
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Count; i++) total += this[i].Amplitude;
                return total;
            }
        }

        /// <summary>The empty field (no trains, dead glass). Equivalent to <c>default</c>.</summary>
        public static readonly WaveTrains None = default;
    }

    /// <summary>
    /// The wave surface at one position and time — what a hull probe or a water pixel reads.
    /// Heights are metres <b>about the tide level</b>: the field rides ON
    /// <c>IEnvironmentService.WaterLevelAt(t)</c> and never moves the walkability waterline
    /// (the ADR 0009 tide/exposure seam is untouched by waves).
    /// </summary>
    public readonly struct WaveSample
    {
        /// <summary>Surface offset (metres) about the tide level: positive on a crest, negative in a
        /// trough, bounded by ±<see cref="WaveTrains.TotalAmplitude"/>.</summary>
        public readonly float Height;

        /// <summary>Surface gradient ∂Height/∂(x,y) — the analytic spatial derivative of
        /// <see cref="Height"/>, not a finite difference. The "which way does the deck tilt" read:
        /// B2 rolls a beam sea and pitches a head sea from this against the hull's heading.</summary>
        public readonly Vector2 Slope;

        /// <summary>0..1, concentrated at sharpened crest tips and 0 through the troughs — the foam
        /// driver B1 keys the whitecap lifecycle off (form → break → streak → fade). Exactly 0 on a
        /// dead-glass sea.</summary>
        public readonly float CrestFactor;

        public WaveSample(float height, Vector2 slope, float crestFactor)
        {
            Height = height;
            Slope = slope;
            CrestFactor = crestFactor;
        }

        /// <summary>The flat, dead-calm surface (glass). Equivalent to <c>default</c>.</summary>
        public static readonly WaveSample Flat = default;
    }

    /// <summary>
    /// Every constant of the wind → wave-train derivation, named and owner-tunable (rule 6) — how
    /// many secondary trains, where they sit relative to the wind, how their wavelengths and
    /// amplitudes scale off the primary, how the dominant wavelength grows with wind speed, the
    /// crest sharpening, and g. Serializable so the Arc B consumer PRs can surface it on
    /// <c>GameConfig</c> for the owner; until then <see cref="Default"/> is the reference tuning
    /// (start from it — a zeroed struct is a dead flat sea).
    /// </summary>
    [Serializable]
    public struct WaveFieldSettings
    {
        /// <summary>
        /// How many secondary trains this struct can actually DESCRIBE — the three
        /// <c>SecondaryN*</c> triples below. <see cref="SecondaryTrainCount"/> clamps against this,
        /// <b>not</b> against <see cref="WaveTrains.MaxTrains"/>.
        ///
        /// <para>⚠️ <b>The subtlest edge of the P2 widening.</b> The clamp used to read
        /// <c>MaxTrains - 1</c>, which was the same number by coincidence: the container held four
        /// slots and the settings described four trains. Widening the container to 8 without pinning
        /// this would let an over-set <c>SecondaryTrainCount</c> report live slots that were never
        /// derived — the shader would read <c>default</c> (all-zero) trains as live, and
        /// <see cref="WaveTrains.TotalAmplitude"/>, the crest-factor normalizer the whitecaps and the
        /// hull clamp both divide by, would be summed over trains that do not exist. Pinned by
        /// <c>WaveSpectrumPassthroughTests</c>.</para>
        /// </summary>
        public const int DerivedSecondaryTrainSlots = 3;

        [Tooltip("Gravitational acceleration g (m/s²) for the deep-water dispersion relation c = √(g·λ/2π). Earth = 9.81. Not a style knob — change only if the sea should read heavier/lighter wholesale.")]
        public float Gravity;

        [Tooltip("How many cross-chop trains ride with the primary downwind train (0–3). 3 → the full 4-train field; 2 → 3 trains (cheaper, reads slightly cleaner). ADR 0018 leaves 3-vs-4 to B1 tuning.")]
        public int SecondaryTrainCount;

        [Tooltip("Dominant (primary-train) wavelength in metres when the wind is dead calm.")]
        public float DominantWavelengthBase;

        [Tooltip("Extra dominant wavelength (metres) per m/s of wind speed — stronger wind builds a longer, faster primary swell.")]
        public float DominantWavelengthPerWindSpeed;

        [Tooltip("Ceiling on the dominant wavelength (metres) so a gale cannot stretch the swell absurdly.")]
        public float DominantWavelengthMax;

        [Tooltip("OWNER RULING 2026-09-06 (\"make it realistic\"): the SYNOPTIC fetch, in kilometres — " +
             "how much open water the wind has crossed before it reaches you. It is what decides how " +
             "developed the sea is, and therefore how long its waves are.\n\n0 = OFF, the legacy " +
             "linear law (DominantWavelengthBase + PerWindSpeed * U) EXACTLY. Above 0 the peak is " +
             "derived instead: the JONSWAP fetch-limited growth law, capped at the Pierson-Moskowitz " +
             "fully-developed limit for that wind.\n\nThis is a SETTING ABOUT THE PLACE, not a look " +
             "dial. ~25 km is an inshore strait; ~100 km is a wide gulf; open ocean is effectively " +
             "infinite and gives Pierson-Moskowitz everywhere. ⚠️ It is NOT the local shelter fetch " +
             "WaveFetch marches off the height map — that one modulates AMPLITUDE only and is " +
             "unaffected.")]
        public float SeaFetchKilometres;

        [Tooltip("A whole-sea WAVELENGTH scale applied LAST, after the peak law and its cap - the " +
             "owner’s dial for a sea that still reads too fast once he has played the realistic " +
             "one. 1 = the derived sea, untouched (shipped)." +
             "\n\nWARNING: speed and period go as the SQUARE ROOT of this. 0.5 gives 0.71x the " +
             "crest speed, and a true HALF speed is 0.25 - and shorter waves also arrive MORE " +
             "often, which is the opposite of realistic. See docs/design/water-rendering.md 39." +
             "\n\nA value of 0 or less is read as 1: this field did not exist before " +
             "2026-09-06, so an asset serialized before then deserializes it as ZERO, and a plain " +
             "multiply would flatten every wavelength onto the floor.")]
        public float DominantWavelengthScale;

        [Tooltip("Primary-train amplitude (metres) at full sea state (SeaState01 = 1). Everything scales down from here; at SeaState01 = 0 all amplitudes are exactly 0 (glass is sacred).")]
        public float PrimaryAmplitude;

        [Tooltip("Response curve of amplitude to SeaState01: amplitudeScale = SeaState01^exponent. 1 = linear; >1 keeps low sea states gentler and lets the top end arrive late.\n\n" +
                 "⚠ SUPERSEDED as the HEIGHT law by HeightFromFetch (register row 33, owner ruling " +
                 "2026-09-09). When that is on, this exponent shapes only the GLASS GATE below " +
                 "GlassGateSeaState - it no longer sets how tall the sea gets.")]
        public float SeaStateAmplitudeExponent;

        // ---- the HEIGHT from the FETCH (register row 33, owner ruling 2026-09-09) -----------------
        // #762 moved the WAVELENGTH onto a fetch-limited growth curve and left the HEIGHT on a tuned
        // pair (PrimaryAmplitude 0.8 x SeaState01^exponent). The two halves could then drift apart,
        // and had: measured against the same JONSWAP growth curves the wavelength already uses, the
        // drawn sea is 1.32x too tall at 3 m/s and 1.83x at 5.70 m/s (2.42x at a gale). The owner
        // ruled "height from fetch" so the two halves come from ONE law and cannot drift again.
        //
        // The relation is the height twin of #762's wavelength law, from the same fetch-limited
        // JONSWAP family:   g*Hs/U^2 = FetchHeightCoefficient * sqrt(g*X/U^2)
        // capped by the fully-developed Pierson-Moskowitz ceiling  Hs = FullyDevelopedHeightCoefficient * U^2,
        // because a strait cannot raise a bigger sea than the open ocean would.

        [Tooltip("ON: significant wave height comes from the same fetch-limited JONSWAP curve as the " +
                 "wavelength (register row 33). OFF (0): the superseded tuned pair, PrimaryAmplitude " +
                 "x SeaState01^exponent, bit-for-bit - the passthrough every pre-2026-09-09 asset " +
                 "deserializes to, since an absent key reads ZERO.")]
        public bool HeightFromFetch;

        [Tooltip("JONSWAP fetch-limited height coefficient: g*Hs/U^2 = c * sqrt(g*X/U^2). 0.0016 is " +
                 "the published value and the one #762's wavelength twin was taken from.")]
        public float FetchHeightCoefficient;

        [Tooltip("Pierson-Moskowitz fully-developed ceiling: Hs = c * U^2 (0.0246). A fetch-limited " +
                 "sea can never be taller than the fully developed one, so this caps the curve.")]
        public float FullyDevelopedHeightCoefficient;

        [Tooltip("A pure STYLE multiplier on the derived height. 1 = the physical sea. The owner's " +
                 "dial if he wants the sea taller or flatter than nature after playing it - and the " +
                 "only place a stylised height should live once the law is doing the deriving.")]
        public float HeightStyleScale;

        [Tooltip("Sea state at and above which the derived height is delivered in full. Below it the " +
                 "height ramps to EXACTLY 0 at glass, so the mirror is still sacred (ADR 0018). " +
                 "Ships at 0.05, well under the 0.143 floor the wind law can reach (#797), so it " +
                 "never bites in play - its only job is to keep glass exactly glass.")]
        public float GlassGateSeaState;

        [Tooltip("Crest sharpening p (≥1): pinches crests narrow above broad troughs. 1 = pure sine mush; ~2–3 reads as real crests.")]
        public float CrestSharpening;

        [Tooltip("Deterministic seed for the trains' phase offsets (hashed, never random). Consumers may feed the world seed here; the default 0 is fine — it only de-synchronizes the trains' crests.")]
        public int PhaseSeed;

        [Tooltip("Secondary train 1: signed angle off the downwind direction, degrees (counter-clockwise positive, math convention).")]
        public float Secondary1AngleDegrees;
        [Tooltip("Secondary train 1: wavelength as a fraction of the dominant wavelength (<1 = shorter chop).")]
        public float Secondary1WavelengthRatio;
        [Tooltip("Secondary train 1: amplitude as a fraction of the primary amplitude.")]
        public float Secondary1AmplitudeRatio;

        [Tooltip("Secondary train 2: signed angle off the downwind direction, degrees.")]
        public float Secondary2AngleDegrees;
        [Tooltip("Secondary train 2: wavelength fraction of the dominant.")]
        public float Secondary2WavelengthRatio;
        [Tooltip("Secondary train 2: amplitude fraction of the primary.")]
        public float Secondary2AmplitudeRatio;

        [Tooltip("Secondary train 3: signed angle off the downwind direction, degrees.")]
        public float Secondary3AngleDegrees;
        [Tooltip("Secondary train 3: wavelength fraction of the dominant.")]
        public float Secondary3WavelengthRatio;
        [Tooltip("Secondary train 3: amplitude fraction of the primary.")]
        public float Secondary3AmplitudeRatio;

        // ---- the JONSWAP spectrum (ADR 0027 #5, P2) ------------------------------------------------
        // ⚠️ THESE FIELDS DID NOT EXIST BEFORE 2026-07-29, so any prefab or scene serialized before
        // then deserializes them as ZERO. SpectrumBlend = 0 is exactly the passthrough, so a stale
        // asset is SAFE — it keeps the shipped 4-train sea. But the shape parameters would also be 0,
        // which is why WaveSpectrum floors the ones for which zero is degenerate rather than
        // meaningful: dialling the blend up on a stale asset must not produce a nonsense sea.

        [Tooltip("0 = today's hand-authored 4-train field, EXACTLY (the passthrough). 1 = the full " +
                 "JONSWAP spectrum: amplitudes from a spectral curve, a continuous fan of directions, " +
                 "and neighbouring frequencies that beat into wave GROUPS. Dial it continuously — the " +
                 "sea morphs, it does not switch.\n\n" +
                 "⚠️ Keep this IDENTICAL on the WaveFieldBridge host and on BoatWaveMotion, or the " +
                 "water and the hull will ride different seas (ADR 0018's one-sea rule holds only if " +
                 "both consumers derive from the same settings).")]
        [Range(0f, 1f)] public float SpectrumBlend;

        [Tooltip("JONSWAP peak enhancement γ. 1 = Pierson-Moskowitz, a broad open-ocean swell with no " +
                 "dominant size; 3.3 = the fetch-limited standard, a taller narrower peak (one wave " +
                 "size clearly dominates). Lower it for a more confused sea. Clamped ≥ 1.")]
        public float SpectrumPeakEnhancement;

        [Tooltip("JONSWAP peak width σ — how far around the peak the enhancement reaches.")]
        public float SpectrumPeakWidth;

        [Tooltip("Frequency spacing between neighbouring spectrum trains, as a fraction of the peak " +
                 "frequency. THIS IS THE WAVE-GROUP DIAL: the group (beat) period is 2π/(ω_p·spacing), " +
                 "so smaller = longer, slower sets. 0.08 puts the period at roughly 25 s in calm air " +
                 "and 60 s in a gale.")]
        public float SpectrumFrequencySpacing;

        [Tooltip("Half-width of the directional fan (degrees) the spectrum trains spread across, " +
                 "either side of downwind. 0 = every train follows the wind exactly.")]
        public float SpectrumSpreadDegrees;

        // ---- the FIXED LADDER (register row 34, owner ruling 2026-09-09) -------------------------
        // The bins' wavelengths, and so their frequencies, are these three numbers and nothing else.
        // No wind term: that is the whole of row 34's fix. The wind moves only WHICH bins carry the
        // amplitude, by walking the JONSWAP peak across a ladder that stands still.
        //
        // ⚠️ These fields postdate 2026-09-09, so an asset serialized before then deserializes them
        // as ZERO — and a zero ladder is degenerate, not merely small. WaveSpectrum floors every one
        // of them (2 bins minimum, ends widened off each other) for exactly that reason, and
        // SpectrumBlend = 0 remains the untouched 4-train passthrough regardless.

        [Tooltip("How many fixed frequency bins the spectrum sea is built from (2..8). More bins " +
                 "cover more of the wind range at the same group rhythm, and cost trig per water " +
                 "pixel AND per hull probe — rule 7. Raising it past 8 needs the shader globals and " +
                 "the bridge widened too (_WaveTrain0..7).")]
        public int SpectrumBinCount;

        [Tooltip("Shortest wave in the fixed ladder (metres) — the ladder's SHORT end. Below this " +
                 "the sea simply has no bin, so a near-calm day is drawn with longer waves than the " +
                 "fetch law wants, at an amplitude near zero.")]
        public float SpectrumLadderMinWavelengthMeters;

        [Tooltip("Longest wave in the fixed ladder (metres) — the ladder's LONG end, and the longest " +
                 "wave the sea can ever draw. Widening the ladder at a fixed bin count spends group " +
                 "rhythm to buy wind coverage: the beat period is inverse in the derived spacing.")]
        public float SpectrumLadderMaxWavelengthMeters;

        [Tooltip("Directional spreading exponent s in cos^2s(θ). 0 = energy spread evenly across the " +
                 "fan (a fully confused sea); larger = energy pulled into a narrow following sea.")]
        public float SpectrumSpreadExponent;

        /// <summary>
        /// The reference tuning (ADR 0018 Arc B starting point): a 4-train field — the primary
        /// downwind swell plus three shorter, smaller cross-chop trains at asymmetric offsets
        /// (+32°, −47°, +11°) so the sea never reads as a symmetric interference pattern. Dominant
        /// wavelength 6 m in calm air growing 1.5 m per m/s of wind (capped 40 m); primary amplitude
        /// 0.8 m at full sea; amplitude response SeaState01^1.35; crest sharpening 2.2.
        /// </summary>
        public static WaveFieldSettings Default => new WaveFieldSettings
        {
            Gravity = 9.81f,
            SecondaryTrainCount = 3,
            DominantWavelengthBase = 6f,
            DominantWavelengthPerWindSpeed = 1.5f,
            DominantWavelengthMax = 40f,
            SeaFetchKilometres = 0f,          // the legacy linear law, bit-for-bit
            DominantWavelengthScale = 1f,     // the derived sea, untouched
            PrimaryAmplitude = 0.8f,
            SeaStateAmplitudeExponent = 1.35f,
            CrestSharpening = 2.2f,
            PhaseSeed = 0,
            Secondary1AngleDegrees = 32f,
            Secondary1WavelengthRatio = 0.55f,
            Secondary1AmplitudeRatio = 0.45f,
            Secondary2AngleDegrees = -47f,
            Secondary2WavelengthRatio = 0.38f,
            Secondary2AmplitudeRatio = 0.30f,
            Secondary3AngleDegrees = 11f,
            Secondary3WavelengthRatio = 0.22f,
            Secondary3AmplitudeRatio = 0.18f,

            // The spectrum ships OFF (ADR 0027: every item defaults to passthrough, so the tuned sea
            // stays byte-identical until the owner dials it in). The shape values below are the
            // reference tuning that blend > 0 uses — γ = 3.3 is JONSWAP's fetch-limited standard, and
            // the 0.08 spacing is chosen so the wave-group period lands at ~25 s calm / ~60 s gale
            // (WaveSpectrumTests pins that band).
            SpectrumBlend = 0f,
            SpectrumPeakEnhancement = 3.3f,
            SpectrumPeakWidth = 0.08f,
            SpectrumFrequencySpacing = 0.08f,
            SpectrumSpreadDegrees = 55f,
            SpectrumSpreadExponent = 2f,

            // Row 34's ladder: 5-30 m over 8 bins, a derived relative spacing of ~0.137. The long
            // end carries the peak at the top of the wind the weather can reach (#797 caps it at
            // 5.7 m/s, where the fetch law's peak is 15.6 m); the short end is where it is because
            // anything wider stops the sea GROUPING - measured, table in
            // WaveSpectrum.DefaultLadderMinWavelengthMeters.
            // OFF in the reference tuning, ON in GameConfig.asset - ADR 0027's discipline, and
            // the reason this PR moves the SHIPPED sea without moving a single frozen baseline:
            // `.Default` is what 129 pre-2026-09-09 assets deserialize to and what the pinned-ULP
            // and passthrough guards compare against. The asset is authoritative (#600), so the
            // owner plays the derived height while the reference sea stays byte-identical.
            HeightFromFetch = false,
            FetchHeightCoefficient = 0.0016f,          // JONSWAP, the published value
            FullyDevelopedHeightCoefficient = 0.0246f, // Pierson-Moskowitz
            HeightStyleScale = 1f,                     // the physical sea until the owner says otherwise
            GlassGateSeaState = 0.05f,                 // under the 0.143 the wind law floors at

            SpectrumBinCount = 8,
            SpectrumLadderMinWavelengthMeters = WaveSpectrum.DefaultLadderMinWavelengthMeters,
            SpectrumLadderMaxWavelengthMeters = WaveSpectrum.DefaultLadderMaxWavelengthMeters,
        };
    }

    /// <summary>
    /// The <b>one shared deterministic wave field</b> (ADR 0018): a small sum of directional wave
    /// trains derived purely from the deterministic wind + the continuous sea-state axis, sampled by
    /// BOTH the simulation (seakeeping — a beam sea rocks the vessel, P1/P5) and the water shader
    /// (swell displacement, whitecaps riding real crests). It lives in Core because two feature
    /// lanes consume it (Boats and Art each reference only Core — rule 4); the placement precedent
    /// is <see cref="TidalExposure"/> / <see cref="BoatKinematics"/>.
    ///
    /// <para><b>Determinism (rule 5).</b> Pure, stateless, allocation-free: same
    /// <c>(windVector, seaState01)</c> → the same trains; same <c>(position, time, trains)</c> → the
    /// same sample — forever, on every machine. The wind is already deterministic from
    /// <c>(worldSeed, gameTime)</c> (WeatherModel), so the field is too. No RNG (phase variety is an
    /// integer hash, the WeatherModel discipline), <b>nothing is ever saved</b> — waves are
    /// recomputed, like tide and wind. Consumers read wind + sea state from
    /// <c>EnvironmentSample</c> (<c>WindVector</c>, <c>SeaState01</c>) via <c>GameServices</c>;
    /// this class deliberately takes plain values so it stays engine-light and headless-testable.</para>
    ///
    /// <para><b>The HLSL twin contract (ADR 0018 §(4)).</b> This class is the <b>reference
    /// implementation</b>. The water shader carries a line-by-line HLSL transcription of
    /// <see cref="Sample"/> (landing with the B1 swell/whitecap rework) reading the same trains as
    /// packed shader globals published by the Art-side bridge — <b>any change to the math here must
    /// change the shader twin in the same PR</b>, exactly as <c>DayNightMath</c>/<c>MoonMath</c>/
    /// <c>WaterReflection</c> are kept in lockstep. The twin receives each train's
    /// <see cref="WaveTrain.PhaseSpeed"/> as data and never re-derives it. Parity is visual, not
    /// bitwise (the GPU runs fast-math float): the pinned-grid EditMode tests
    /// (<c>WaveMathTests</c>) are the numbers a twin review diffs against.</para>
    ///
    /// <para><b>Owner rulings baked in (ADR 0018, 2026-07-02):</b> dispersion is canon (speed derives
    /// from wavelength — see <see cref="WaveTrain"/>); glass calm is sacred (at
    /// <c>seaState01 = 0</c> every amplitude is exactly 0 — no minimum swell, the sea is the full
    /// mirror for the reflection layers).</para>
    ///
    /// <para><b>What waves are NOT.</b> The height is an offset <em>about</em> the tide level —
    /// <see cref="TidalExposure"/> and the walkability waterline never read it. Note the sharpened
    /// profile spends longer in its broad troughs than at its narrow crests, so the time-average
    /// surface sits a touch below the tide level at high sea states — a look/feel detail, invisible
    /// to the exposure seam by construction.</para>
    /// </summary>
    public static class WaveMath
    {
        /// <summary>
        /// 🔴 <b>ROW 33's law: the fetch-limited significant wave height, the HEIGHT twin of the
        /// wavelength law #762 shipped.</b> Both come from the same JONSWAP fetch-limited growth
        /// curves, which is the whole point of the owner's 2026-09-09 ruling — two halves of one sea
        /// derived from one relation cannot drift apart again.
        ///
        /// <para><c>g·Hs/U² = c·√(g·X/U²)</c> with <c>c = FetchHeightCoefficient</c> (0.0016
        /// published), capped by the fully-developed Pierson–Moskowitz ceiling
        /// <c>Hs = 0.0246·U²</c> — a 25 km strait cannot raise a bigger sea than the open ocean
        /// would in the same wind, and without the cap the fetch curve crosses it at high wind.</para>
        ///
        /// <para>⚠️ <b>The lee envelope is NOT folded in here.</b> <c>WaveFetch.EnvelopeAt</c> is a
        /// SPATIAL multiplier applied at sample time, and folding it into the trains' amplitudes
        /// would put it inside <c>TotalAmplitude</c> — which is the crest-factor normalizer and the
        /// bound the watertight hull clamp scans against. <c>WaveFetch.cs</c> §"What falls out for
        /// free" says why. This function is the OPEN-water height; the lee is applied over it.</para>
        /// </summary>
        /// <param name="windSpeed">Wind speed (m/s).</param>
        /// <param name="settings">Carries the fetch (km) and the two coefficients.</param>
        public static float FetchLimitedSignificantHeightMeters(float windSpeed,
                                                                in WaveFieldSettings settings)
        {
            float u = Mathf.Max(windSpeed, 0f);
            float fetchKm = settings.SeaFetchKilometres;
            if (u <= MinWindForDerivedPeak || fetchKm <= 0f) return 0f;

            float g = Gravity(in settings);
            float c = settings.FetchHeightCoefficient > 0f
                ? settings.FetchHeightCoefficient : DefaultFetchHeightCoefficient;
            float pm = settings.FullyDevelopedHeightCoefficient > 0f
                ? settings.FullyDevelopedHeightCoefficient : DefaultFullyDevelopedHeightCoefficient;

            float dimensionlessFetch = g * (fetchKm * 1000f) / (u * u);
            float fetchLimited = c * Mathf.Sqrt(dimensionlessFetch) * u * u / g;
            return Mathf.Min(fetchLimited, pm * u * u);
        }

        /// <summary>
        /// The significant height a field of sinusoids carries: <c>Hs = 4·σ</c> with
        /// <c>σ² = Σa²/2</c> — the standard definition, and the one
        /// <c>WaveAmplitudeMeasurementTests</c> already compares the oceanography against.
        ///
        /// <para>⚠️ This is the OPEN-water height. <c>WaveFetch.EnvelopeAt</c>'s lee multiplier is
        /// applied per SAMPLE, never folded in here: folding it in would put it inside
        /// <see cref="WaveTrains.TotalAmplitude"/>, which is the whitecap crest-factor normalizer AND
        /// the bound the watertight hull clamp scans against (<c>WaveFetch.cs</c> §"What falls out
        /// for free").</para>
        /// </summary>
        public static float SignificantHeightMeters(in WaveTrains trains)
        {
            float sumSq = 0f;
            for (int i = 0; i < trains.Count; i++)
            {
                float a = trains[i].Amplitude;
                sumSq += a * a;
            }
            return 4f * Mathf.Sqrt(sumSq * 0.5f);
        }

        /// <summary>
        /// The same field, scaled so its <see cref="SignificantHeightMeters"/> is
        /// <paramref name="targetHs"/>. Every amplitude is exactly proportional to the primary's, so
        /// one multiply per slot is exact — no re-derivation, no trig; wavelengths, directions,
        /// phases and the dominant index are untouched (row 34's standing ladder included).
        ///
        /// <para>A target of 0 gives amplitudes of <b>exactly</b> 0, which is how glass stays sacred
        /// (ADR 0018) now that the height is a function of the WIND rather than of the sea state.</para>
        /// </summary>
        public static WaveTrains AtSignificantHeight(in WaveTrains trains, float targetHs,
                                                     float gravity, float crestSharpening)
        {
            float current = SignificantHeightMeters(in trains);
            if (current <= GlassAmplitudeMeters) return trains;      // already flat: nothing to scale

            float scale = Mathf.Max(0f, targetHs) / current;
            Span<WaveTrain> scaled = stackalloc WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < trains.Count; i++)
                scaled[i] = new WaveTrain(trains[i].Direction, trains[i].Wavelength,
                                          trains[i].Amplitude * scale, trains[i].PhaseOffset, gravity);

            return WaveTrains.From(scaled, trains.Count, crestSharpening, trains.DominantIndex);
        }

        /// <summary>
        /// The glass gate: 1 at and above <see cref="WaveFieldSettings.GlassGateSeaState"/>, ramping
        /// to <b>exactly 0</b> at sea state 0. Glass is sacred (ADR 0018) and the derived height is a
        /// function of the WIND, so something has to carry that ruling once the sea-state exponent
        /// stops setting the height. Ships at 0.05 — under the 0.143 floor the wind law can reach
        /// (#797), so in play it is always exactly 1 and the height is purely the fetch law's.
        /// </summary>
        public static float GlassGate(float seaState01, in WaveFieldSettings settings)
        {
            float sea = Mathf.Clamp01(seaState01);
            if (sea <= 0f) return 0f;                       // exactly 0: the mirror, bit for bit
            float full = settings.GlassGateSeaState > 0f
                ? settings.GlassGateSeaState : DefaultGlassGateSeaState;
            float e = Mathf.Max(0.01f, settings.SeaStateAmplitudeExponent);
            return Mathf.Pow(Mathf.Clamp01(sea / full), e);
        }

        /// <summary>JONSWAP's published fetch-limited height coefficient.</summary>
        public const float DefaultFetchHeightCoefficient = 0.0016f;
        /// <summary>Pierson-Moskowitz's fully-developed height coefficient.</summary>
        public const float DefaultFullyDevelopedHeightCoefficient = 0.0246f;
        /// <summary>Where the glass gate reaches full height. Under #797's reachable sea-state floor.</summary>
        public const float DefaultGlassGateSeaState = 0.05f;

        /// <summary>Below this total amplitude (metres) the sea is treated as dead glass and
        /// <see cref="WaveSample.CrestFactor"/> is exactly 0 (guards the 0/0 of normalizing height by
        /// the amplitude envelope). A guard, not a tunable.</summary>
        public const float GlassAmplitudeMeters = 1e-6f;

        private const double TwoPi = Math.PI * 2.0;

        /// <summary>The same constant in float, for the single-precision derivation path
        /// (the ladder's dispersion). Declared beside its double twin so the two cannot
        /// drift.</summary>
        private const float TwoPiF = (float)(Math.PI * 2.0);

        /// <summary>
        /// Derive the wave field from the weather — the pure function at the heart of ADR 0018 §(1).
        /// The primary train runs <b>downwind</b> at the dominant wavelength (which grows with wind
        /// speed); up to three secondary trains sit at the settings' angular offsets with shorter
        /// wavelengths and smaller amplitudes (the cross-chop that makes a real sea read). Every
        /// amplitude scales with <c>seaState01^exponent</c> and is <b>exactly 0 at
        /// <paramref name="seaState01"/> = 0</b> (glass is sacred). Phase offsets hash off the train
        /// index + <see cref="WaveFieldSettings.PhaseSeed"/> — deterministic, no RNG.
        /// </summary>
        /// <param name="windVector">Wind as direction × strength (m/s) — <c>EnvironmentSample.WindVector</c>.
        /// A dead-calm (zero) wind keeps a defined downwind of +Y; amplitudes are what silence a calm sea.</param>
        /// <param name="seaState01">The continuous sea-state axis, 0 = glass .. 1 = full storm
        /// (<c>EnvironmentSample.SeaState01</c>). Clamped to [0, 1].</param>
        /// <param name="settings">The derivation constants — start from <see cref="WaveFieldSettings.Default"/>.</param>
        public static WaveTrains TrainsFrom(Vector2 windVector, float seaState01, in WaveFieldSettings settings)
        {
            float sea = Mathf.Clamp01(seaState01);

            // Glass calm is sacred: 0^e == 0 exactly for e > 0, so the whole field flattens to a
            // true mirror at sea state 0 — no minimum swell, no floor (owner ruling, ADR 0018 §(1)).
            float amplitudeScale = Mathf.Pow(sea, Mathf.Max(0.01f, settings.SeaStateAmplitudeExponent));

            float windSpeed = Mathf.Sqrt(windVector.x * windVector.x + windVector.y * windVector.y);
            Vector2 downwind = windSpeed > 1e-6f
                ? new Vector2(windVector.x / windSpeed, windVector.y / windSpeed)
                : Vector2.up;

            float wavelengthCeiling = Mathf.Max(WaveTrain.MinWavelengthMeters, settings.DominantWavelengthMax);
            float dominantWavelength = Mathf.Clamp(
                PeakWavelengthMeters(windSpeed, in settings),
                WaveTrain.MinWavelengthMeters, wavelengthCeiling);

            // The owner's dial, applied LAST so it scales whatever the peak law produced. Ships at 1
            // (a no-op) — it exists because he may still want the sea slower after playing the realistic
            // one, and this is the single number that does it. ⚠️ Speed goes as its SQUARE ROOT, and a
            // shorter sea is a LESS realistic one; §39 carries both numbers.
            //
            // ⚠️ Zero is read as 1. The field postdates every asset serialized before 2026-09-06, and a
            // plain multiply would collapse the sea onto MinWavelengthMeters — measured at 0.010 m with
            // the floor removed. Same discipline as WaveSpectrum's floors.
            float wavelengthScale = settings.DominantWavelengthScale > 0f
                ? settings.DominantWavelengthScale : 1f;
            // Clamped AGAIN, against the same ceiling. The dial is applied after the first clamp so it
            // scales whatever the peak law produced — but that would let a value above 1 walk straight
            // through the ceiling the rail exists to be. A rail that one knob can step over is not a
            // rail. (Below 1, which is what the dial is for, this second clamp is a no-op.)
            dominantWavelength = Mathf.Clamp(dominantWavelength * wavelengthScale,
                                             WaveTrain.MinWavelengthMeters, wavelengthCeiling);

            // 🔴 ROW 33: the height comes from the fetch, the way the length already does.
            //
            // The old line was `PrimaryAmplitude * sea^exponent` — a tuned pair with no relation to
            // the wavelength law, which is how the two halves drifted 1.83x apart. Now the total
            // height is the published fetch-limited curve at THIS wind, and the sea-state term is
            // demoted to what it is actually needed for: keeping glass exactly glass.
            //
            // ⚠️ Why the wind may carry the height while `sea` only gates it: in production
            // `SeaState01` IS a pure function of wind speed (`WeatherModel.SeaFromWind`), so the two
            // are not independent inputs — the old exponent was the wind's height law wearing the
            // sea state as a proxy. They come apart only in fixtures and in `DevSeaState01`, which is
            // exactly where a glass gate has to hold, so it does.
            // ⚠️ Under the fetch law the field is built at UNIT primary amplitude and its
            // height is set afterwards, on the FINISHED trains. The obvious shortcut — convert the
            // target Hs into a primary amplitude using the secondaries' ratios — is wrong whenever
            // the spectrum is on, and measurably so: the spectrum preserves the amplitude ENVELOPE
            // (Σa) while spreading it across eight bins, and Hs goes as √Σa², which spreading
            // REDUCES. Written that way the sea came out at 0.67x its own reference — the law
            // derived one height and the field drew another. Every amplitude is exactly proportional
            // to the primary's, so scaling the finished field is exact, costs one trig-free pass over
            // eight slots, and is right on BOTH the legacy and the spectral path.
            float primaryAmplitude = settings.HeightFromFetch
                ? 1f
                : Mathf.Max(0f, settings.PrimaryAmplitude) * amplitudeScale;
            float gravity = settings.Gravity;

            var primary = new WaveTrain(
                downwind, dominantWavelength, primaryAmplitude,
                PhaseOffsetRadians(0, settings.PhaseSeed), gravity);

            var secondary1 = new WaveTrain(
                Rotate(downwind, settings.Secondary1AngleDegrees),
                dominantWavelength * settings.Secondary1WavelengthRatio,
                primaryAmplitude * Mathf.Max(0f, settings.Secondary1AmplitudeRatio),
                PhaseOffsetRadians(1, settings.PhaseSeed), gravity);

            var secondary2 = new WaveTrain(
                Rotate(downwind, settings.Secondary2AngleDegrees),
                dominantWavelength * settings.Secondary2WavelengthRatio,
                primaryAmplitude * Mathf.Max(0f, settings.Secondary2AmplitudeRatio),
                PhaseOffsetRadians(2, settings.PhaseSeed), gravity);

            var secondary3 = new WaveTrain(
                Rotate(downwind, settings.Secondary3AngleDegrees),
                dominantWavelength * settings.Secondary3WavelengthRatio,
                primaryAmplitude * Mathf.Max(0f, settings.Secondary3AmplitudeRatio),
                PhaseOffsetRadians(3, settings.PhaseSeed), gravity);

            // ⚠️ Clamped against what the SETTINGS can describe (three secondaries), never against
            // WaveTrains.MaxTrains — those were the same number before P2 widened the container, and
            // following MaxTrains here would publish underived slots as live. See
            // WaveFieldSettings.DerivedSecondaryTrainSlots.
            int count = 1 + Mathf.Clamp(settings.SecondaryTrainCount, 0,
                                        WaveFieldSettings.DerivedSecondaryTrainSlots);

            float blend = Mathf.Clamp01(settings.SpectrumBlend);
            WaveTrains field = blend <= 0f
                ? new WaveTrains(primary, secondary1, secondary2, secondary3,
                                 count, settings.CrestSharpening)
                // NB: the primary amplitude is deliberately NOT passed — the spectrum normalizes
                // onto the legacy trains' TOTAL envelope, not onto the primary alone.
                : SpectrumTrainsFrom(downwind, dominantWavelength, gravity, blend,
                                     count, in settings,
                                     primary, secondary1, secondary2, secondary3);

            if (!settings.HeightFromFetch) return field;

            // Row 33: the height the fetch law asks for, set on whatever field row 34's ladder built.
            return AtSignificantHeight(
                in field,
                FetchLimitedSignificantHeightMeters(windSpeed, in settings)
                    * Mathf.Max(0f, settings.HeightStyleScale)
                    * GlassGate(sea, in settings),
                gravity, settings.CrestSharpening);
        }

        /// <summary>
        /// The ADR 0027 #5 spectrum: the hand-authored field morphed toward a JONSWAP-shaped,
        /// directionally-spread, group-producing one by <c>blend</c>. Split out of
        /// <see cref="TrainsFrom"/> only for readability — it is the same pure function, and
        /// <c>blend = 0</c> never reaches here (the caller returns the legacy field bit-for-bit).
        ///
        /// <para><b>Every slot is a LERP from its legacy self toward its spectral self</b>, so the sea
        /// morphs continuously as the dial turns instead of switching between two seas. Slots 0–3 have
        /// a legacy counterpart; slots 4–7 do not, so they lerp up from <b>zero amplitude</b> — which
        /// is why the field is continuous at blend → 0 even though the live slot count jumps from 4 to
        /// 8 there (four silent trains contribute exactly nothing).</para>
        ///
        /// <para><b>The amplitude ENVELOPE is preserved</b> (Σ amplitudes unchanged). This is a
        /// deliberate choice with feel consequences, and the alternative was rejected on evidence:
        /// preserving <em>energy</em> (Σ A²) is the more physical normalization, but it grows Σ A —
        /// and Σ A is the crest-factor normalizer the whitecap lifecycle divides by AND the bound the
        /// watertight hull clamp scans against. Growing it would quietly reduce foam and raise every
        /// hull. Preserving the envelope instead means the spectrum's peaks reach the SAME height the
        /// hand-authored sea reached, and what the spectrum adds is the lulls between them — which is
        /// precisely the owner's *"waves building and collapsing"*, at zero risk to two calibrated
        /// systems.</para>
        /// </summary>
        private static WaveTrains SpectrumTrainsFrom(
            Vector2 downwind, float dominantWavelength, float gravity,
            float blend, int legacyCount, in WaveFieldSettings settings,
            in WaveTrain legacy0, in WaveTrain legacy1, in WaveTrain legacy2, in WaveTrain legacy3)
        {
            int seed = settings.PhaseSeed;
            float maxSpread = Mathf.Max(0f, settings.SpectrumSpreadDegrees) * Mathf.Deg2Rad;

            // ---- (1) the FIXED ladder, and the weights the wind puts on it --------------------------
            // 🔴 ROW 34 (owner ruling 2026-09-09). The superseded code read the shape at a FIXED ratio
            // per slot and scaled every wavelength by λ_p(U) — so the bins walked under a stationary
            // peak, every ω was a function of the wind, and φ = k·x − ω·t jumped by Δω·t at every mood
            // change (27 000–47 500 radians per bin at three hours of play). It is now the other way
            // round: THE BINS STAND STILL AND THE PEAK WALKS ACROSS THEM. λ_i has no wind term at all;
            // the wind enters only here, in the ratio ω_i/ω_p(U) the JONSWAP shape is read at.
            //
            // amplitude ∝ √(S(ω)) · cos^s(θ): the JONSWAP shape times the directional spreading, both
            // taken as AMPLITUDE weights (√energy). Δω is common to every slot and normalizes away.
            int bins = Mathf.Clamp(settings.SpectrumBinCount <= 0
                                       ? WaveTrains.MaxTrains : settings.SpectrumBinCount,
                                   WaveSpectrum.MinLadderBins, WaveTrains.MaxTrains);
            float ladderMin = settings.SpectrumLadderMinWavelengthMeters > 0f
                ? settings.SpectrumLadderMinWavelengthMeters
                : WaveSpectrum.DefaultLadderMinWavelengthMeters;
            float ladderMax = settings.SpectrumLadderMaxWavelengthMeters > 0f
                ? settings.SpectrumLadderMaxWavelengthMeters
                : WaveSpectrum.DefaultLadderMaxWavelengthMeters;

            // ω_p of the CURRENT sea — the only place the wind touches the spectrum now. λ_p is the
            // fetch law's peak, unchanged by this PR; it no longer scales anything, it only says
            // where on the standing ladder the energy sits.
            float omegaPeak = Mathf.Sqrt(TwoPiF * gravity
                                         / Mathf.Max(dominantWavelength, WaveTrain.MinWavelengthMeters));

            // ⚠️ AND THE PEAK IS PINNED TO THE LADDER IT WALKS ON. Measured 2026-09-09: at 0.5 m/s
            // the fetch law's peak is 0.21 m, far below the ladder's short end, so EVERY bin sat at
            // ω_i/ω_p < 0.27 — where the JONSWAP shape carries exp(-1.25·r⁻⁴), which underflows to
            // exactly zero. weightTotal went with it, the normalizer with that, and the sea lost the
            // whole 65 % spectral share of its height (total amplitude 0.0173 -> 0.0060 m at 0.5 m/s).
            // Clamping the peak into the ladder's own frequency range makes the energy pile onto the
            // nearest END bin instead of falling off the edge — which is exactly what a sea whose
            // waves are shorter than the shortest bin should look like: drawn a little long, at the
            // right height. That is the stated price of a finite ladder, not a fudge.
            float omegaLadderLong = Mathf.Sqrt(TwoPiF * gravity
                / Mathf.Max(WaveSpectrum.BinWavelengthMeters(0, bins, ladderMin, ladderMax, seed),
                            WaveTrain.MinWavelengthMeters));
            float omegaLadderShort = Mathf.Sqrt(TwoPiF * gravity
                / Mathf.Max(WaveSpectrum.BinWavelengthMeters(bins - 1, bins, ladderMin, ladderMax, seed),
                            WaveTrain.MinWavelengthMeters));
            omegaPeak = Mathf.Clamp(omegaPeak,
                                    Mathf.Min(omegaLadderLong, omegaLadderShort),
                                    Mathf.Max(omegaLadderLong, omegaLadderShort));

            Span<float> weight = stackalloc float[WaveTrains.MaxTrains];
            Span<float> angle = stackalloc float[WaveTrains.MaxTrains];
            Span<float> binLambda = stackalloc float[WaveTrains.MaxTrains];
            float weightTotal = 0f;
            for (int i = 0; i < WaveTrains.MaxTrains; i++)
            {
                // Slots past the ladder's bin count fold onto its short end and are silenced by the
                // shape; they stay in the loop so the container's arity never depends on the tuning.
                binLambda[i] = WaveSpectrum.BinWavelengthMeters(i, bins, ladderMin, ladderMax, seed);
                angle[i] = WaveSpectrum.AngleOffsetRadians(i, seed, maxSpread);

                float omegaBin = Mathf.Sqrt(TwoPiF * gravity / binLambda[i]);
                float shape = WaveSpectrum.JonswapShape(
                    omegaBin / Mathf.Max(omegaPeak, 1e-6f),
                    settings.SpectrumPeakEnhancement, settings.SpectrumPeakWidth);
                weight[i] = (i < bins ? 1f : 0f)
                          * Mathf.Sqrt(Mathf.Max(0f, shape))
                          * WaveSpectrum.DirectionalWeight(angle[i], settings.SpectrumSpreadExponent);
                weightTotal += weight[i];
            }

            // ---- (2) normalize onto the legacy envelope --------------------------------------------
            // Σ(spectrum amplitudes) == Σ(legacy amplitudes), so the lerp preserves the envelope at
            // EVERY blend value, not just at the ends (a lerp of two equal sums is that same sum).
            float legacyTotal = 0f;
            for (int i = 0; i < legacyCount; i++)
                legacyTotal += LegacySlot(i, legacy0, legacy1, legacy2, legacy3).Amplitude;

            float normalizer = weightTotal > 1e-9f ? legacyTotal / weightTotal : 0f;

            // ---- (3) morph each slot ---------------------------------------------------------------
            // stackalloc, not a heap array: TrainsFrom runs on the throttled sim cadence for the
            // bridge AND for every boat, and rule 7 forbids a per-tick allocation there.
            Span<WaveTrain> trains = stackalloc WaveTrain[WaveTrains.MaxTrains];
            int dominantIndex = 0;
            float dominantAmplitude = -1f;
            for (int i = 0; i < WaveTrains.MaxTrains; i++)
            {
                bool hasLegacy = i < legacyCount;
                WaveTrain legacy = hasLegacy ? LegacySlot(i, legacy0, legacy1, legacy2, legacy3) : default;

                float spectrumAmplitude = weight[i] * normalizer;

                // ⚠️ THE WAVELENGTH IS NO LONGER MORPHED, and that is deliberate (row 34). The
                // superseded line was `Lerp(legacy.Wavelength, spectrumWavelength, blend)` — and
                // legacy.Wavelength is λ_p(U)·ratio, so ANY blend below 1 kept a wind term in ω and
                // therefore kept a share of the vibration: at the shipped 0.65 it would still have
                // carried 35 % of it. A wavelength that follows the wind IS the defect; there is no
                // fraction of it worth morphing through.
                //
                // The price, stated rather than hidden: the field is no longer CONTINUOUS in λ as
                // `blend` → 0⁺ — it steps from the hand-authored wavelengths to the ladder's. That is
                // harmless because `SpectrumBlend` is a static tuning value, never animated: nothing
                // in the game moves it while the player is looking. (`blend <= 0` still returns the
                // 4-train field bit-for-bit from the caller, so every passthrough guard holds.)
                float wavelength = binLambda[i];
                float amplitude = Mathf.Lerp(hasLegacy ? legacy.Amplitude : 0f, spectrumAmplitude, blend);

                // Directions morph as an ANGLE off downwind, never as a lerped vector — lerping two
                // unit vectors shortens the chord and would drag the direction through a shrinking
                // magnitude the WaveTrain ctor then renormalizes non-uniformly.
                float legacyAngle = hasLegacy ? LegacyAngleDegrees(i, in settings) * Mathf.Deg2Rad : angle[i];
                float slotAngle = Mathf.Lerp(legacyAngle, angle[i], blend);

                trains[i] = new WaveTrain(RotateRadians(downwind, slotAngle),
                                          wavelength, amplitude,
                                          PhaseOffsetRadians(i, seed), gravity);

                // The spectral peak is found, not assumed. The shape puts it at slot 0 by
                // construction (JONSWAP peaks at ω/ω_p = 1, and slot 0's directional weight is the
                // maximum cos^s(0) = 1) — but the phase consumers must follow the REAL peak if a
                // tuning ever moves it, which is exactly what WaveTrains.DominantIndex is for.
                if (amplitude > dominantAmplitude)
                {
                    dominantAmplitude = amplitude;
                    dominantIndex = i;
                }
            }

            return WaveTrains.From(trains, WaveTrains.MaxTrains, settings.CrestSharpening, dominantIndex);
        }

        private static WaveTrain LegacySlot(int index, in WaveTrain t0, in WaveTrain t1,
                                            in WaveTrain t2, in WaveTrain t3) => index switch
        {
            0 => t0,
            1 => t1,
            2 => t2,
            _ => t3,
        };

        private static float LegacyAngleDegrees(int index, in WaveFieldSettings settings) => index switch
        {
            0 => 0f,                                   // the primary runs downwind
            1 => settings.Secondary1AngleDegrees,
            2 => settings.Secondary2AngleDegrees,
            _ => settings.Secondary3AngleDegrees,
        };

        /// <summary>
        /// Sample the wave surface at a world position and time — ADR 0018 §(2), the read both a hull
        /// probe (B2/B3) and, via the HLSL twin, every water pixel (B1) perform. Pure, stateless,
        /// allocation-free. Per train the phase is <c>θ = k·(d·pos − c·t) + φ</c>; the profile is the
        /// sharpened sine <c>A·(2·((sin θ + 1)/2)^p − 1)</c> (p = <see cref="WaveTrains.CrestSharpening"/>
        /// — narrow crests over broad troughs, the cheap Gerstner-style read sanctioned by the ADR);
        /// the slope is its <b>analytic</b> gradient <c>A·p·s^(p−1)·cos θ·k·d</c>, summed over trains;
        /// and the crest factor is the height normalized by the amplitude envelope, sharpened by the
        /// same p and clamped to [0, 1] — 1 only where the trains crest together, exactly 0 through
        /// the troughs and on a dead-glass sea.
        /// </summary>
        /// <param name="worldPos">World-space position (the same frame the wind field uses).</param>
        /// <param name="timeSeconds">Game time in seconds (<c>IClockService</c> total seconds). Double
        /// on purpose: the phase is accumulated in double and wrapped to [0, 2π) BEFORE dropping to
        /// float trig, so a long-running game never loses the wave to float drift.</param>
        /// <param name="trains">The field to sample — derive it via <see cref="TrainsFrom"/>.</param>
        public static WaveSample Sample(Vector2 worldPos, double timeSeconds, in WaveTrains trains)
            => Sample(worldPos, timeSeconds, in trains, 1f);

        /// <summary>
        /// The same sample through the <b>wind-fetch envelope</b> (ADR 0027 #1) — a spatial amplitude
        /// multiplier in (0, 1] from <see cref="WaveFetch.EnvelopeAt"/>, so waves are smaller in the
        /// lee of a headland and full-sized on an exposed shore.
        ///
        /// <para><b>envelope = 1 is the exact passthrough</b> (and the overload above passes exactly
        /// that), which is what lets the model ship OFF with the sea byte-identical.</para>
        ///
        /// <para><b>It scales the HEIGHT and the SLOPE, never the amplitude envelope.</b> The crest
        /// factor divides the scaled height by the UNSCALED <c>TotalAmplitude</c>, so a lee shore
        /// loses its whitecaps for free — correct, deliberate, and the reason the fetch term must not
        /// be folded into the trains themselves. The slope takes only the <c>E·∇h</c> term of
        /// <c>∇(E·h)</c>; see <see cref="WaveFetch"/> for why the <c>h·∇E</c> term is negligible by
        /// construction.</para>
        ///
        /// <para>⚠️ The HLSL twin applies the envelope in the SAME place (inside
        /// <c>WaveFieldSample</c>, after the train loop, before the crest factor). Change one, change
        /// both in the same PR.</para>
        /// </summary>
        /// <param name="fetchEnvelope01">The fetch amplitude multiplier at <paramref name="worldPos"/>.
        /// Clamped to [0, 1]; 1 = no fetch limiting.</param>
        public static WaveSample Sample(Vector2 worldPos, double timeSeconds, in WaveTrains trains,
                                        float fetchEnvelope01)
        {
            float height = 0f;
            float slopeX = 0f;
            float slopeY = 0f;
            float totalAmplitude = 0f;
            float sharpening = trains.CrestSharpening;
            int count = trains.Count;

            for (int i = 0; i < count; i++)
            {
                WaveTrain train = trains[i];
                float amplitude = train.Amplitude;
                totalAmplitude += amplitude;
                if (amplitude <= 0f) continue; // a silent train contributes exactly nothing

                float waveNumber = (2f * Mathf.PI) / train.Wavelength;

                // Phase in double, wrapped to [0, 2π), THEN float trig — see timeSeconds doc.
                double travel = (double)train.Direction.x * worldPos.x
                              + (double)train.Direction.y * worldPos.y
                              - (double)train.PhaseSpeed * timeSeconds;
                double phase = waveNumber * travel + train.PhaseOffset;
                phase -= Math.Floor(phase / TwoPi) * TwoPi;
                float theta = (float)phase;

                float sin = Mathf.Sin(theta);
                float cos = Mathf.Cos(theta);

                float s = (sin + 1f) * 0.5f;                       // 0 in the trough .. 1 at the crest
                float shaped = Mathf.Pow(s, sharpening);            // pinch: narrow crest, broad trough
                height += amplitude * (2f * shaped - 1f);

                // d/dpos of the height term above — the ANALYTIC derivative (chain rule), not a
                // finite difference. At p = 1 this collapses to the pure-sine slope A·cosθ·k·d.
                float slopeMagnitude = amplitude * sharpening * Mathf.Pow(s, sharpening - 1f) * cos * waveNumber;
                slopeX += slopeMagnitude * train.Direction.x;
                slopeY += slopeMagnitude * train.Direction.y;
            }

            // The FETCH envelope (ADR 0027 #1): a spatial amplitude multiplier applied to the summed
            // field. Height and slope both scale; TotalAmplitude deliberately does NOT, so the crest
            // factor below collapses in a lee and the whitecaps go with it.
            float fetch = Mathf.Clamp01(fetchEnvelope01);
            if (fetch < 1f)
            {
                height *= fetch;
                slopeX *= fetch;
                slopeY *= fetch;
            }

            float crestFactor = 0f;
            if (totalAmplitude > GlassAmplitudeMeters)
            {
                // Height normalized by the envelope: 1 only where every train crests at once. The
                // clamp zeroes everything below the mean surface; the pow concentrates what's left
                // toward the crest tips (the whitecap driver, B1).
                crestFactor = Mathf.Pow(Mathf.Clamp01(height / totalAmplitude), sharpening);
            }

            return new WaveSample(height, new Vector2(slopeX, slopeY), crestFactor);
        }

        /// <summary>
        /// The phase θ (degrees in [0, 360)) of ONE train at a position and time — <b>crest at 90°,
        /// trough at 270°</b>: the convention <see cref="Sample"/>'s own profile is built on
        /// (<c>height = A·(2·((sin θ + 1)/2)^p − 1)</c> peaks at θ = 90°), and the one
        /// <c>DoryRockMath.PhaseDegrees</c> reconstructs toward.
        ///
        /// <para><b>Why this exists (ADR 0022 phase 5).</b> Recovering the phase from a SAMPLED
        /// surface — <c>atan2(height, (slope·d)/k)</c> — is only valid for a single PURE SINE train.
        /// Fed the real field (up to four superposed trains, crest-sharpened p &gt; 1) it is not a
        /// phase at all: measured over a 10 s sail it advances 6.4× faster at some moments than
        /// others and REVERSES direction on 1.7% of frames. A sprite hull hides that behind its
        /// 8-frame quantiser; a mesh hull poses it continuously, and the owner saw the difference as
        /// <i>"the rocking was a little stuttery"</i>. Computing the phase FORWARD from the train
        /// instead is exact, strictly monotone, and free.</para>
        ///
        /// <para><b>Cannot drift from <see cref="Sample"/>:</b> this is character-for-character the
        /// same phase expression (<c>θ = k·(d·pos − c·t) + φ</c>, accumulated in double and wrapped
        /// before the float drop) — an EditMode test asserts the two agree against
        /// <see cref="Sample"/>'s own height. Pure, allocation-free and deterministic like everything
        /// else here (rule 5); a silent (zero-amplitude) train still has a defined phase.</para>
        /// </summary>
        /// <param name="train">The train to read — typically <c>trains[0]</c>, the dominant swell.</param>
        /// <param name="worldPos">World-space position (the same frame <see cref="Sample"/> uses).</param>
        /// <param name="timeSeconds">Game time. Pass 0 for trains that came from
        /// <c>WaveFieldAnimator</c>, which bakes accumulated travel into
        /// <see cref="WaveTrain.PhaseOffset"/> — exactly as its <c>Sample</c> sugar does.</param>
        public static float TrainPhaseDegrees(in WaveTrain train, Vector2 worldPos, double timeSeconds)
        {
            float waveNumber = (2f * Mathf.PI) / train.Wavelength;
            double travel = (double)train.Direction.x * worldPos.x
                          + (double)train.Direction.y * worldPos.y
                          - (double)train.PhaseSpeed * timeSeconds;
            double phase = waveNumber * travel + train.PhaseOffset;
            phase -= Math.Floor(phase / TwoPi) * TwoPi;
            return (float)(phase * (180.0 / Math.PI));
        }

        // ---- deterministic helpers (no RNG anywhere — rule 5) -----------------------------------

        /// <summary>
        /// 🔴 <b>THE PEAK WAVELENGTH — how long the waves are for this wind (owner ruling 2026-09-06,
        /// "make it realistic", register row 30).</b>
        ///
        /// <para><b>At <see cref="WaveFieldSettings.SeaFetchKilometres"/> ≤ 0 this is the legacy linear
        /// law, bit for bit</b> — <c>Base + PerWindSpeed·U</c>. Above 0 the peak is DERIVED from how far
        /// the wind has blown over open water:</para>
        ///
        /// <list type="number">
        /// <item><b>JONSWAP fetch-limited growth.</b> With dimensionless fetch <c>X̃ = gX/U²</c>, the
        /// peak frequency is <c>f_p·U/g = 3.5·X̃^−0.33</c>, and <c>λ = g/(2π f_p²)</c>. A sea that has
        /// not run far enough is SHORTER, which is the whole reason an inshore island does not get
        /// ocean swell.</item>
        /// <item><b>Capped at Pierson–Moskowitz.</b> JONSWAP's growth law has no ceiling of its own —
        /// it would keep lengthening forever with fetch — so it is capped at the fully-developed peak
        /// <c>λ = 2πU²/(0.877²g)</c>, which is what a wind eventually builds no matter how far it
        /// blows. <b>PM is therefore the infinite-fetch LIMIT of this function, not a rival to it.</b></item>
        /// </list>
        ///
        /// <para>⚠️ <b>Why PM alone was measured and rejected.</b> Full development needs
        /// <c>gX/U² ≈ 17 400</c> — <b>58 km of open water at a blow and 298 km at a gale</b>. This
        /// setting is an inshore island; it has neither. Shipped at 25 km the derived peak lands within
        /// ~7 % of the legacy line at blow and gale (15.6 m vs 14.6, 27.3 m vs 25.4) while correcting
        /// the light-airs end, where the legacy line is nearly four times too long. Bare PM would put a
        /// 140 m wave on a 52 m frame at a gale — less than half a wavelength on screen, crossing it
        /// FASTER than today, at a third of a real sea's steepness. See
        /// <c>docs/design/water-rendering.md</c> §39.</para>
        ///
        /// <para>Pure and deterministic in its arguments; no clock, no RNG (rule 5).</para>
        /// </summary>
        /// <param name="windSpeed">Wind strength (m/s).</param>
        /// <param name="settings">The derivation constants.</param>
        public static float PeakWavelengthMeters(float windSpeed, in WaveFieldSettings settings)
        {
            float fetchKm = settings.SeaFetchKilometres;
            if (fetchKm <= 0f)                                  // OFF: the legacy line, bit for bit
                return settings.DominantWavelengthBase
                     + settings.DominantWavelengthPerWindSpeed * windSpeed;

            float u = Mathf.Max(windSpeed, 0f);
            if (u <= MinWindForDerivedPeak)                     // no wind builds no wave; the trains are
                return WaveTrain.MinWavelengthMeters;           // silent here anyway (glass is sacred)

            float fetchMetres = fetchKm * 1000f;
            // JONSWAP: f_p = 3.5 * (g X / U^2)^-0.33 * g / U
            float dimensionlessFetch = Gravity(in settings) * fetchMetres / (u * u);
            float peakFrequency = 3.5f * Mathf.Pow(dimensionlessFetch, -0.33f)
                                * Gravity(in settings) / u;
            float fetchLimited = Gravity(in settings)
                               / (2f * Mathf.PI * peakFrequency * peakFrequency);

            // ...capped at the fully developed sea that wind can ever build.
            float fullyDeveloped = 2f * Mathf.PI * u * u
                                 / (0.877f * 0.877f * Gravity(in settings));
            return Mathf.Min(fetchLimited, fullyDeveloped);
        }

        /// <summary>Below this wind speed the derived peak is not evaluated: the growth law divides by
        /// the wind, and every amplitude is already 0 down here (glass is sacred).</summary>
        public const float MinWindForDerivedPeak = 0.05f;

        static float Gravity(in WaveFieldSettings settings)
            => settings.Gravity > 0f ? settings.Gravity : 9.81f;

        /// <summary>Deterministic phase offset in [0, 2π) for a train slot: the WeatherModel-style
        /// integer hash (same constants, same discipline — one deterministic noise family across the
        /// sim), mapped to an angle. Same (index, seed) → same phase, forever.</summary>
        private static float PhaseOffsetRadians(int trainIndex, int seed)
        {
            unchecked
            {
                int n = (trainIndex * 73856093) ^ (seed * 19349663) ^ 0x5f3759df;
                n = (n << 13) ^ n;
                int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
                return (m / 2147483648f) * (2f * Mathf.PI); // [0, 1) → [0, 2π)
            }
        }

        /// <summary>Rotate a vector by a signed angle in degrees (counter-clockwise positive, the
        /// standard math convention — a NEGATIVE settings angle therefore reads clockwise on screen).</summary>
        private static Vector2 Rotate(Vector2 v, float degrees) => RotateRadians(v, degrees * Mathf.Deg2Rad);

        /// <summary>The same rotation taking radians. The spectrum works in radians throughout (its
        /// directional weight is a <c>cos</c>), so routing it through the degrees entry point would
        /// convert to degrees and straight back — a round trip that costs precision for nothing.
        /// ⚠️ <see cref="Rotate"/> must remain the exact expression it was: the passthrough proof
        /// compares it against a frozen copy, and this refactor deliberately does not touch it.</summary>
        private static Vector2 RotateRadians(Vector2 v, float radians)
        {
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
        }
    }
}
