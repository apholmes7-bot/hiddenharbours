using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// How a wave breaks, decided by the <b>bathymetry</b> — the surf-similarity (Iribarren)
    /// classification, ADR 0040. The owner's words map onto these members one for one: a
    /// <see cref="Plunging"/> breaker is the one that throws a LIP and encloses a BARREL; a
    /// <see cref="Spilling"/> breaker just crumbles down its face into whitewater.
    ///
    /// <para><b>Ordered by steepness of the bed</b> (gentle → steep), so a simple <c>&gt;</c>/<c>&lt;</c>
    /// compares "how violently does it break here": <see cref="Spilling"/> &lt; <see cref="Plunging"/>
    /// &lt; <see cref="Collapsing"/> &lt; <see cref="Surging"/>. <see cref="None"/> sorts first and means
    /// "not breaking here at all" — deep water, dry ground, or a glass sea.</para>
    /// </summary>
    public enum BreakerClass
    {
        /// <summary>Not breaking: the water is too deep for this wave, the ground is dry, or the sea is
        /// glass. Glass calm is sacred (ADR 0018 §(1)) — a zero-amplitude field breaks nowhere.</summary>
        None = 0,

        /// <summary>Gentle bed (ξ₀ &lt; <see cref="BreakerSettings.SpillingLimit"/>): the crest crumbles
        /// forward down its own face over a long distance and turns steadily into whitewater. The
        /// common case on every sandy shoal — <b>whitewater</b> is what this one produces.</summary>
        Spilling = 1,

        /// <summary>Steep bed (<see cref="BreakerSettings.SpillingLimit"/> ≤ ξ₀ &lt;
        /// <see cref="BreakerSettings.PlungingLimit"/>): the crest outruns the base, the top edge is
        /// thrown forward as a <b>LIP</b>, and the curl encloses a hollow <b>BARREL</b> with the
        /// peeling <b>POCKET</b> beside it. The showpiece — and it appears only where the painted
        /// seabed earns it.</summary>
        Plunging = 2,

        /// <summary>Steeper still (<see cref="BreakerSettings.PlungingLimit"/> ≤ ξ₀ &lt;
        /// <see cref="BreakerSettings.CollapsingLimit"/>): the face collapses over the lower half with
        /// no proper curl — a short, violent, foam-poor break against a steep bank.</summary>
        Collapsing = 3,

        /// <summary>Near-vertical bed (ξ₀ ≥ <see cref="BreakerSettings.CollapsingLimit"/>): the wave
        /// surges up the face and back down almost without breaking. A quay wall or a rock ledge —
        /// almost no whitewater, which is why a harbour wall does not boil.</summary>
        Surging = 4,
    }

    /// <summary>
    /// Every constant of the BREAKING-WAVE model (ADR 0040), named and owner-tunable (rule 6):
    /// the breaker index γ, the width of the smooth break gate, the three Iribarren thresholds that
    /// separate spilling from plunging from surging, the bed-slope probe span, and the whitewater
    /// march + decay. Serialized on <c>GameConfig.Breakers</c> so the owner tunes the <b>asset</b>,
    /// not the code.
    ///
    /// <para><b>Why it lives in Core, beside <see cref="WaveFieldSettings"/> and
    /// <see cref="WaveFetchSettings"/>.</b> Three lanes consume it — Art draws the breakers, Boats
    /// takes the shove, World owns the seabed it reads — and a feature module may never reference
    /// another's concrete classes (rule 4). The tunables and the maths therefore land in Core, the
    /// same split <see cref="SeakeepingSettings"/> already uses.</para>
    ///
    /// <para><b>⚠️ These fields did not exist before 2026-08-27</b>, so any <c>GameConfig</c> asset
    /// serialized before then deserializes them as ZERO. A zeroed struct is <b>inert, not wrong</b>:
    /// <see cref="BreakerIndex"/> = 0 makes the depth-limited height 0 everywhere, so nothing breaks
    /// and nothing draws — the same "stale asset keeps today's sea" property
    /// <see cref="WaveFetchSettings"/> ships under. Every shape value is floored where zero would be
    /// degenerate, so dialling γ up on a stale asset cannot produce a nonsense surf.</para>
    /// </summary>
    [Serializable]
    public struct BreakerSettings
    {
        [Tooltip("The BREAKER INDEX γ. A wave breaks where its height H reaches γ × the water depth — " +
                 "the classic depth-limited criterion, and after breaking its height is held AT γ·d, " +
                 "which is why surf gets smaller as it runs up a beach.\n\n" +
                 "0.78 is the textbook solitary-wave value and the physical default. Lower = waves " +
                 "break further out in deeper water (a wider, tamer surf zone); higher = they carry " +
                 "further in and break harder in close.")]
        [Range(0f, 1.5f)] public float BreakerIndex;

        [Tooltip("Width of the smooth break gate, as a FRACTION of the criterion. A hard H ≥ γ·d test " +
                 "would make the whole surf line POP on and off as the tide crossed a bar — a " +
                 "discontinuity arriving on the tide's schedule. This is the band over which the gate " +
                 "opens instead. 0.15 = the last 15% before the criterion.")]
        [Range(0.01f, 0.9f)] public float BreakBandRatio;

        [Header("Breaker TYPE — the Iribarren (surf-similarity) thresholds, ξ₀ = tanβ / √(H₀/L₀)")]
        [Tooltip("Below this ξ₀ the break is SPILLING — it crumbles into whitewater over a long, " +
                 "gentle shoal. Battjes' standard value is 0.5.")]
        public float SpillingLimit;

        [Tooltip("Between SpillingLimit and this, the break is PLUNGING — the lip is thrown forward " +
                 "and the barrel curls. THIS IS THE SHOWPIECE BAND, and widening it puts barrels on " +
                 "shoals that have not earned them. Battjes' standard value is 3.3.")]
        public float PlungingLimit;

        [Tooltip("Between PlungingLimit and this, the break COLLAPSES against a steep bank; above it " +
                 "the wave SURGES up the face almost without breaking (a quay wall). Battjes' " +
                 "standard value is 5.0.")]
        public float CollapsingLimit;

        [Tooltip("Half-span (metres) of the central difference that measures the bed slope tanβ along " +
                 "the wave's travel. Wider = smoother, blind to a narrow ledge; narrower = it reads " +
                 "single painted texels and the breaker type flickers between neighbours. 2 m spans a " +
                 "few texels of the painted seabed.")]
        public float SlopeProbeMeters;

        [Header("Whitewater — the foam left after the break, advecting shoreward and decaying")]
        [Tooltip("Time constant τ (seconds) of the post-break energy decay, E = exp(−t/τ). This is a " +
                 "TIME, and the age it consumes is derived from DISTANCE past the break line ÷ the " +
                 "bore speed — never accumulated. Bigger = foam streaks further in before it dies.")]
        public float WhitewaterDecaySeconds;

        [Tooltip("Length of one step of the upwave march that measures how far past the break line a " +
                 "position is. Total reach is this × BreakerMath.MarchSteps (16), so 2 m reaches 32 m " +
                 "back — wider than any surf zone these shores carry. Longer steps see further for " +
                 "the same cost but stride over a narrow bar.")]
        public float WhitewaterStepMeters;

        /// <summary>
        /// The reference tuning: <b>the textbook physics</b>. γ = 0.78 (the solitary-wave breaker
        /// index), Battjes' 1974 surf-similarity thresholds unchanged at 0.5 / 3.3 / 5.0, a 2 m slope
        /// probe matched to the painted seabed's texel scale, and a whitewater decay of 3.5 s over a
        /// 32 m march — foam that streaks a boat-length or two in before it dies.
        ///
        /// <para>These are <b>measured constants of the sea</b>, not art direction. The owner's dials
        /// for how the surf reads are in the consumer PRs (what it draws, how hard it shoves); these
        /// decide only <em>where</em> and <em>what kind</em>, and the bathymetry does that.</para>
        /// </summary>
        public static BreakerSettings Default => new BreakerSettings
        {
            BreakerIndex = 0.78f,
            BreakBandRatio = 0.15f,
            SpillingLimit = 0.5f,
            PlungingLimit = 3.3f,
            CollapsingLimit = 5f,
            SlopeProbeMeters = 2f,
            WhitewaterDecaySeconds = 3.5f,
            WhitewaterStepMeters = 2f,
        };
    }

    /// <summary>
    /// What one wave train is doing at one position — the whole shoaling/breaking state, read in a
    /// single call. Everything here is a pure function of (train, position, water level, seabed,
    /// settings); nothing is stored and nothing is saved (rule 5).
    /// </summary>
    public readonly struct BreakerSample
    {
        /// <summary>Water depth (metres) — <c>waterLevel − seabed</c>, the same single number
        /// <see cref="TidalExposure.WaterDepth"/> gives walkability and boat-cross. <b>≤ 0 is dry.</b>
        /// This is the term the TIDE moves, and moving it is what walks the breaker line in and out
        /// over the day.</summary>
        public readonly float DepthMeters;

        /// <summary>The SHOALED wavelength (metres) — shorter than the train's deep-water λ, because a
        /// wave entering shallow water slows while its period is conserved.</summary>
        public readonly float LocalWavelength;

        /// <summary>The SHOALED phase speed (m/s) — <c>c = c₀·(L/L₀)</c>, tending to the shallow-water
        /// limit <c>√(g·d)</c>. Slower in the shallows: that is what makes a train bend toward the
        /// beach and pile up.</summary>
        public readonly float Celerity;

        /// <summary>Green's-law shoaling coefficient <c>Ks</c> — the factor the deep-water height is
        /// multiplied by here. 1 in deep water, growing as <c>d^(−1/4)</c> in the shallows because the
        /// energy flux has nowhere to go but up.</summary>
        public readonly float ShoalingCoefficient;

        /// <summary>The wave height (crest to trough, metres) actually standing here: the shoaled
        /// height, then <b>depth-limited to γ·d</b> once it breaks. Surf shrinks as it runs up the
        /// beach for this reason and no other.</summary>
        public readonly float WaveHeight;

        /// <summary>The unlimited shoaled height (metres) before the depth limit — what the wave
        /// <em>would</em> stand at if it did not break. The ratio of this to <c>γ·d</c> is the break
        /// criterion itself.</summary>
        public readonly float ShoaledHeight;

        /// <summary>0..1: <b>is it breaking here?</b> A smooth gate, not a cutoff — 0 well outside the
        /// surf zone, 1 at and past the criterion. Smooth on purpose: a hard test would pop the whole
        /// surf line on and off as the tide crossed a bar.</summary>
        public readonly float Breaking01;

        /// <summary>Bed slope <c>tanβ</c> along the direction of travel, clamped ≥ 0 (a wave running
        /// into deeper water is climbing nothing). Read from the painted seabed — this is the term
        /// that decides the breaker TYPE.</summary>
        public readonly float BedSlope;

        /// <summary>The Iribarren / surf-similarity number ξ₀ = <c>tanβ / √(H₀/L₀)</c>. Steep bed and
        /// long swell push it up (plunging); gentle bed and short chop pull it down (spilling).</summary>
        public readonly float Iribarren;

        /// <summary>Which kind of breaker the bathymetry has earned here. <see cref="BreakerClass.None"/>
        /// wherever <see cref="Breaking01"/> is 0.</summary>
        public readonly BreakerClass Class;

        public BreakerSample(float depthMeters, float localWavelength, float celerity,
                             float shoalingCoefficient, float waveHeight, float shoaledHeight,
                             float breaking01, float bedSlope, float iribarren, BreakerClass breakerClass)
        {
            DepthMeters = depthMeters;
            LocalWavelength = localWavelength;
            Celerity = celerity;
            ShoalingCoefficient = shoalingCoefficient;
            WaveHeight = waveHeight;
            ShoaledHeight = shoaledHeight;
            Breaking01 = breaking01;
            BedSlope = bedSlope;
            Iribarren = iribarren;
            Class = breakerClass;
        }

        /// <summary>Deep, dry or glass — nothing breaking. Equivalent to <c>default</c>.</summary>
        public static readonly BreakerSample NotBreaking = default;
    }

    /// <summary>
    /// <b>ADR 0040 — waves that BREAK, from the wave field × the painted depth × the tide.</b> The
    /// pure, deterministic, headless-testable model behind the owner's four words: the <b>LIP</b>
    /// thrown forward, the <b>BARREL</b> it encloses, the <b>POCKET</b> peeling beside it, and the
    /// <b>WHITEWATER</b> left behind. Owner, 2026-08-27: <i>"our waves are missing something. i want
    /// them to be even more physics based."</i>
    ///
    /// <para><b>Nothing here is invented.</b> Every input is already owned and already deterministic
    /// from <c>(worldSeed, gameTime)</c> — the wave field (<see cref="WaveMath"/>, ADR 0018), the
    /// painted seabed (<see cref="ITidalTerrain"/>, ADR 0014) and the tide
    /// (<c>IEnvironmentService.WaterLevelAt</c>, ADR 0009). Breaking is what those three <em>already
    /// imply</em>; this class only reads it out. There is no new authored data, no saved state and no
    /// RNG (rule 5).</para>
    ///
    /// <para><b>The four steps, and where each one's physics comes from.</b></para>
    /// <list type="number">
    /// <item><description><b>Shoaling</b> (<see cref="ShoaledWavelength"/>,
    /// <see cref="ShoalingCoefficient"/>): the train's PERIOD is conserved as it crosses onto a shoal,
    /// so as depth falls the wavelength shortens, the celerity drops toward <c>√(g·d)</c>, and the
    /// height GROWS because the energy flux has nowhere else to go — Green's law. The local wavelength
    /// comes from Fenton &amp; McKee's closed form so the twin never has to iterate a dispersion
    /// solve.</description></item>
    /// <item><description><b>Breaking</b> (<see cref="Breaking01"/>): a wave breaks where its height
    /// reaches <c>γ·d</c>, γ ≈ 0.78. <b>Depth is <c>waterLevel − seabed</c>, so the whole criterion
    /// MOVES WITH THE TIDE for free</b> — the bar that boils at half-ebb sleeps at high water, and
    /// nothing had to be wired to make that happen (P1 made visible, P5 made honest).</description></item>
    /// <item><description><b>Type</b> (<see cref="Iribarren"/>, <see cref="ClassFor"/>): the surf
    /// similarity parameter off the painted bed's SLOPE decides spilling vs plunging vs surging.
    /// <b>Barrels appear only where the bathymetry earns them.</b> Nobody paints a barrel in; the
    /// seabed either produces one or it does not.</description></item>
    /// <item><description><b>Whitewater</b> (<see cref="MetersSinceBreak"/>,
    /// <see cref="WhitewaterEnergy01"/>): post-break energy advecting shoreward and
    /// decaying.</description></item>
    /// </list>
    ///
    /// <para><b>⚠️⚠️ The whitewater age is DERIVED FROM GEOMETRY, never accumulated.</b> This is the
    /// one place ADR 0040 could have repeated the round-1/round-2 wake defect twice over. An age read
    /// out of a buffer that accumulates, saturates and is then posterized is not an age — the signal
    /// dies in the pipeline that consumes it, and the foam stays white forever. So the age here is
    /// <c>distance past the break line ÷ bore speed</c>: <see cref="MetersSinceBreak"/> marches the
    /// geometry and is <b>linear in position with no clamp before the exponential</b>, and the
    /// exponential itself has no plateau. Equally: <see cref="Breaking01"/> is a <b>GATE</b> (1 where
    /// the sea is breaking, 0 elsewhere) and is never used as a scale on the age — a clock scaled by
    /// intensity is the same error one level down, and it is what made a dory's brand-new churn born
    /// half-aged. <c>BreakerWhitewaterAgeMeasurementTests</c> holds this as a MEASUREMENT, not an
    /// argument.</para>
    ///
    /// <para><b>⚠️ Any phase used past the break is the field's PUBLISHED phase.</b> Nothing here
    /// reconstructs a phase from a sampled surface — <c>atan2(height, slope·d/k)</c> is exact for one
    /// pure sine and is not a phase at all when fed the real four-train sharpened field (it reverses
    /// on 1.7% of frames). Consumers that need a phase past the break read
    /// <see cref="WaveMath.TrainPhaseDegrees"/> forward off the train, and take the train from
    /// <see cref="SharedWaveField"/> — a stateful smoother can only be relied on to agree with
    /// itself.</para>
    ///
    /// <para><b>What this class does NOT touch.</b> <see cref="WaveMath"/> and
    /// <c>WaveFieldAnimator</c> are load-bearing and unchanged — the living wake reads their published
    /// phase and their contracts are frozen. Breaking is a <b>read</b> layered over the field, exactly
    /// as <see cref="WaveFetch"/> is: it consumes trains, it never rewrites them. The walkability
    /// waterline (<see cref="TidalExposure"/>) is likewise untouched; surf rides ON the tide level and
    /// never moves it.</para>
    ///
    /// <para><b>The HLSL twin contract (PR 2).</b> Where a quantity is evaluated per-pixel the shader
    /// carries a line-for-line transcription and <b>this side stays the pinned reference</b> — change
    /// one, change both in the same PR, the <see cref="WaveMath"/>/<see cref="WaveFetch"/> discipline.
    /// Parity is by visual epsilon and ULP, never bit equality: two transcriptions of one formula
    /// cannot be made bit-identical. <see cref="MarchSteps"/> is the fixed <c>[unroll]</c> bound its
    /// counterpart must match.</para>
    /// </summary>
    public static class BreakerMath
    {
        /// <summary>
        /// The whitewater march's <b>FIXED</b> iteration count — the shader's <c>[unroll]</c> bound,
        /// the same constraint <see cref="WaveFetch.MarchSteps"/> ships under (an <c>[unroll]</c> over
        /// a RUNTIME bound is one of the known magenta traps). The reach back upwave is tuned through
        /// <see cref="BreakerSettings.WhitewaterStepMeters"/>, never by marching a variable number of
        /// steps.
        /// </summary>
        public const int MarchSteps = 16;

        /// <summary>Guard floor on water depth (metres) used inside the shoaling maths so a position
        /// at the very waterline cannot divide by zero or send Green's law to infinity. It bounds the
        /// intermediate only — the visible height is still depth-limited to <c>γ·d</c>, which goes to
        /// zero at the water's edge as it should. A guard, not a tunable.</summary>
        public const float MinDepthMeters = 0.02f;

        /// <summary>Above this value of <c>2·k·d</c> the water is deep for this wave and the
        /// group-velocity ratio is exactly its deep-water limit ½. Guards the <c>sinh</c> overflow that
        /// would otherwise take out a long swell in deep water (<c>sinh(2kd)</c> passes float range
        /// near 89). A guard, not a tunable.</summary>
        public const float DeepWaterKd2 = 20f;

        /// <summary>Floor on the deep-water steepness <c>H₀/L₀</c> under the Iribarren square root, so
        /// a silent (zero-amplitude) train gives a defined ξ rather than a division by zero. A glass
        /// sea is reported as <see cref="BreakerClass.None"/> by the gate above it, not by this floor.
        /// A guard, not a tunable.</summary>
        public const float MinSteepness = 1e-6f;

        /// <summary>Floor on the whitewater march step (metres) so a zeroed/stale settings struct
        /// cannot collapse the march onto a single point. A guard, not a tunable.</summary>
        public const float MinStepMeters = 0.05f;

        /// <summary>Floor on the whitewater decay time constant (seconds). A guard, not a tunable.</summary>
        public const float MinDecaySeconds = 1e-3f;

        // ---- shoaling: the wave feels the bottom -------------------------------------------------

        /// <summary>
        /// The SHOALED wavelength (metres) in depth <paramref name="depthMeters"/> for a train whose
        /// deep-water wavelength is <paramref name="deepWavelength"/> — <b>Fenton &amp; McKee (1990)</b>:
        /// <c>L = L₀ · tanh((k₀·d)^(3/4))^(2/3)</c>, with <c>k₀ = 2π/L₀</c>.
        ///
        /// <para><b>Why this form.</b> The exact relation <c>ω² = g·k·tanh(k·d)</c> at conserved period
        /// has no closed-form solution for k, and a Newton iteration is exactly the thing an HLSL twin
        /// must not carry. Fenton &amp; McKee is the standard explicit approximation, within ~1.7%
        /// everywhere, monotone, and it is <b>exact in both limits that matter</b>: <c>L → L₀</c> in
        /// deep water, and <c>L → √(2π·L₀·d)</c> — i.e. <c>c → √(g·d)</c> — in the shallows.</para>
        /// </summary>
        public static float ShoaledWavelength(float deepWavelength, float depthMeters)
        {
            float l0 = Mathf.Max(WaveTrain.MinWavelengthMeters, deepWavelength);
            float d = Mathf.Max(MinDepthMeters, depthMeters);
            float k0d = (2f * Mathf.PI / l0) * d;
            float t = (float)Math.Tanh(Math.Pow(k0d, 0.75));
            return l0 * Mathf.Pow(t, 2f / 3f);
        }

        /// <summary>
        /// The ratio of group speed to phase speed, <c>n = ½·(1 + 2kd/sinh(2kd))</c> — ½ in deep water,
        /// 1 in the shallows where the whole wave travels at the celerity. The term that turns energy
        /// conservation into Green's law.
        /// </summary>
        public static float GroupSpeedRatio(float localWavelength, float depthMeters)
        {
            float l = Mathf.Max(WaveTrain.MinWavelengthMeters, localWavelength);
            float d = Mathf.Max(MinDepthMeters, depthMeters);
            float kd2 = 2f * (2f * Mathf.PI / l) * d;
            if (kd2 >= DeepWaterKd2) return 0.5f;              // sinh guard — deep water for this wave
            float sinh = (float)Math.Sinh(kd2);
            if (sinh <= 1e-12f) return 1f;                     // kd → 0: the shallow-water limit
            return 0.5f * (1f + kd2 / sinh);
        }

        /// <summary>
        /// <b>GREEN'S LAW</b> — the factor the deep-water height is multiplied by at this depth:
        /// <c>Ks = √(cg₀ / cg)</c>, energy flux conserved. Exactly 1 in deep water (so the open sea is
        /// untouched, the property that keeps the shipped field's tuning valid); growing as
        /// <c>d^(−1/4)</c> in the shallows, which is why a swell that is knee-high offshore stands
        /// head-high on the bar.
        /// </summary>
        /// <param name="deepCelerity">The train's deep-water phase speed c₀ —
        /// <see cref="WaveTrain.PhaseSpeed"/>, already the dispersion relation. Never re-derived here:
        /// that formula lives in exactly one place (<see cref="WaveTrain"/>'s constructor).</param>
        /// <param name="localWavelength">The shoaled wavelength from <see cref="ShoaledWavelength"/>.</param>
        /// <param name="deepWavelength">The train's deep-water wavelength L₀.</param>
        /// <param name="depthMeters">Water depth here (metres).</param>
        public static float ShoalingCoefficient(float deepCelerity, float deepWavelength,
                                                float localWavelength, float depthMeters)
        {
            float l0 = Mathf.Max(WaveTrain.MinWavelengthMeters, deepWavelength);
            float l = Mathf.Max(WaveTrain.MinWavelengthMeters, localWavelength);
            float c0 = Mathf.Max(1e-6f, deepCelerity);

            float c = c0 * (l / l0);                            // period conserved ⇒ c scales with L
            float cg = GroupSpeedRatio(l, depthMeters) * c;
            if (cg <= 1e-9f) return 1f;
            return Mathf.Sqrt((c0 * 0.5f) / cg);                // cg₀ = c₀/2 in deep water
        }

        /// <summary>The SHOALED phase speed (m/s): <c>c = c₀·(L/L₀)</c>. Tends to <c>√(g·d)</c> in the
        /// shallows by construction of <see cref="ShoaledWavelength"/> — the celerity the bore travels
        /// at once the wave has broken.</summary>
        public static float ShoaledCelerity(float deepCelerity, float deepWavelength, float localWavelength)
        {
            float l0 = Mathf.Max(WaveTrain.MinWavelengthMeters, deepWavelength);
            float l = Mathf.Max(WaveTrain.MinWavelengthMeters, localWavelength);
            return Mathf.Max(0f, deepCelerity) * (l / l0);
        }

        // ---- the break criterion: where the sea gives out -----------------------------------------

        /// <summary>
        /// <b>THE CRITERION.</b> How far a wave of height <paramref name="shoaledHeight"/> has got
        /// toward breaking in depth <paramref name="depthMeters"/>: the ratio <c>H / (γ·d)</c>. 1 is
        /// the break itself; below 1 the wave is still standing; above 1 it has broken and is running
        /// as a bore.
        ///
        /// <para><b>Depth is the tide's term</b> (<c>waterLevel − seabed</c>), which is the whole
        /// reason the surf line walks in and out over the day without anything animating it.</para>
        /// </summary>
        public static float BreakRatio(float shoaledHeight, float depthMeters, float breakerIndex)
        {
            if (depthMeters <= 0f) return 0f;                   // dry ground breaks nothing
            float limit = Mathf.Max(0f, breakerIndex) * depthMeters;
            if (limit <= 1e-9f) return 0f;                      // γ = 0 (a stale asset) is inert
            return Mathf.Max(0f, shoaledHeight) / limit;
        }

        /// <summary>
        /// The smooth break GATE, 0..1 — 0 well outside the surf zone, 1 at and past the criterion.
        ///
        /// <para><b>Smooth, not a cutoff, and that is a physics decision not a polish one.</b> A hard
        /// <c>H ≥ γ·d</c> test would make the entire surf line appear and vanish as the tide crossed a
        /// bar: a discontinuity in the water the hull rides, arriving on the tide's schedule. The gate
        /// opens over <see cref="BreakerSettings.BreakBandRatio"/> of the criterion instead — the same
        /// reasoning that made <see cref="WaveFetch"/>'s shore gate a smoothstep.</para>
        ///
        /// <para><b>⚠️ This is a GATE, never a scale on an age.</b> It saturates at 1, which is correct
        /// for "is it breaking" and fatal for "how long ago did it break". See the class note.</para>
        /// </summary>
        public static float Breaking01(float shoaledHeight, float depthMeters, in BreakerSettings settings)
        {
            if (depthMeters <= 0f) return 0f;
            float ratio = BreakRatio(shoaledHeight, depthMeters, settings.BreakerIndex);
            float band = Mathf.Clamp(settings.BreakBandRatio, 0.01f, 0.9f);
            return WaveFetch.SmoothstepEdge(1f - band, 1f, ratio);
        }

        /// <summary>
        /// The height that actually stands here: the shoaled height <b>depth-limited to γ·d</b>. A
        /// broken wave cannot be taller than the water it is running over — which is exactly why surf
        /// gets smaller as it runs up a beach, and why a big day and a small day look alike in the last
        /// few metres.
        /// </summary>
        public static float DepthLimitedHeight(float shoaledHeight, float depthMeters, float breakerIndex)
        {
            if (depthMeters <= 0f) return 0f;
            float limit = Mathf.Max(0f, breakerIndex) * depthMeters;
            return Mathf.Min(Mathf.Max(0f, shoaledHeight), limit);
        }

        // ---- breaker TYPE: the bathymetry decides -------------------------------------------------

        /// <summary>
        /// The bed slope <c>tanβ</c> along the wave's direction of travel, by central difference over
        /// ±<paramref name="probeMeters"/> on the painted seabed. Positive when the bed rises ahead of
        /// the wave (shoaling); <b>clamped to ≥ 0</b>, because a wave running out into deeper water is
        /// climbing nothing and has no surf-similarity to speak of.
        ///
        /// <para>Sampled on the world PPU grid (<see cref="WaveFetch.Pixelize"/>) for the same reason
        /// the fetch march is: a slope read on unquantized coordinates would crawl under camera
        /// translation, and the drawn breaker line would part company with the felt one. A null
        /// terrain means open water — no bed, no slope, slope 0.</para>
        /// </summary>
        public static float BedSlopeAlong(Vector2 worldPos, Vector2 travelDirection, float probeMeters,
                                          ITidalTerrain terrain)
        {
            if (terrain == null) return 0f;

            float span = Mathf.Max(0.01f, probeMeters);
            Vector2 d = travelDirection;
            float sqrMagnitude = d.x * d.x + d.y * d.y;
            if (sqrMagnitude < 1e-12f) return 0f;
            float inv = 1f / Mathf.Sqrt(sqrMagnitude);
            d = new Vector2(d.x * inv, d.y * inv);

            Vector2 ahead = WaveFetch.Pixelize(new Vector2(worldPos.x + d.x * span, worldPos.y + d.y * span));
            Vector2 astern = WaveFetch.Pixelize(new Vector2(worldPos.x - d.x * span, worldPos.y - d.y * span));

            float rise = terrain.ElevationAt(ahead) - terrain.ElevationAt(astern);
            return Mathf.Max(0f, rise / (2f * span));
        }

        /// <summary>
        /// The <b>Iribarren number</b> (surf-similarity parameter) ξ₀ = <c>tanβ / √(H₀/L₀)</c> — the
        /// single dimensionless number that decides what KIND of breaker a place makes. Steep bed and
        /// long swell push it up toward a plunging barrel; a gentle shoal under short chop pulls it
        /// down to a spilling crumble.
        /// </summary>
        /// <param name="bedSlope">tanβ from <see cref="BedSlopeAlong"/>.</param>
        /// <param name="deepHeight">Deep-water wave height H₀ (metres) — <b>twice</b> the train's
        /// amplitude, through the fetch envelope if one is in play.</param>
        /// <param name="deepWavelength">Deep-water wavelength L₀ (metres).</param>
        public static float Iribarren(float bedSlope, float deepHeight, float deepWavelength)
        {
            float l0 = Mathf.Max(WaveTrain.MinWavelengthMeters, deepWavelength);
            float steepness = Mathf.Max(MinSteepness, Mathf.Max(0f, deepHeight) / l0);
            return Mathf.Max(0f, bedSlope) / Mathf.Sqrt(steepness);
        }

        /// <summary>
        /// Battjes' classification of ξ₀ into the four breaker types — the table that turns a number
        /// into the owner's vocabulary. <see cref="BreakerClass.None"/> is <b>not</b> produced here;
        /// "is it breaking at all" is <see cref="Breaking01"/>'s question, and
        /// <see cref="SampleAt"/> composes the two.
        /// </summary>
        public static BreakerClass ClassFor(float iribarren, in BreakerSettings settings)
        {
            float spilling = Mathf.Max(0f, settings.SpillingLimit);
            float plunging = Mathf.Max(spilling, settings.PlungingLimit);
            float collapsing = Mathf.Max(plunging, settings.CollapsingLimit);

            if (iribarren < spilling) return BreakerClass.Spilling;
            if (iribarren < plunging) return BreakerClass.Plunging;
            if (iribarren < collapsing) return BreakerClass.Collapsing;
            return BreakerClass.Surging;
        }

        // ---- whitewater: what the break leaves behind ---------------------------------------------

        /// <summary>
        /// <b>How far past the break line this position is</b>, in metres of contiguous breaking water
        /// upwave — the geometric quantity the whitewater age is derived FROM.
        ///
        /// <para>March back against the train's travel in <see cref="MarchSteps"/> FIXED steps and
        /// accumulate a RUNNING PRODUCT of the break gate, exactly the shape
        /// <see cref="WaveFetch.Fetch01"/>'s land shadow uses: the moment the march steps out of
        /// breaking water the product collapses and nothing beyond it counts. So this measures the age
        /// of <em>this</em> bore, not the total surf crossed — foam from an outer bar that has already
        /// died in a lagoon does not make the shorebreak look older than it is. Branch-free, which is
        /// what lets the twin keep a fixed <c>[unroll]</c> with no early exit.</para>
        ///
        /// <para><b>⚠️⚠️ Linear in position, with no clamp, no threshold and no posterize before the
        /// decay consumes it.</b> That is the whole design: an age derived through a saturating,
        /// thresholded, quantized chain is not an age at all — measured, not argued, in
        /// <c>BreakerWhitewaterAgeMeasurementTests</c>.</para>
        ///
        /// <para><b>The cap is real and stated.</b> The march reaches
        /// <see cref="MarchSteps"/> × <see cref="BreakerSettings.WhitewaterStepMeters"/> (32 m at the
        /// default tuning) and saturates there. At the default 3.5 s decay a bore has lost over 99% of
        /// its energy long before 32 m, so the cap is not reachable in a visible quantity — but it is a
        /// cap, and a surf zone wider than the reach would read as uniformly old at its inshore end.</para>
        /// </summary>
        public static float MetersSinceBreak(Vector2 worldPos, in WaveTrain train, float waterLevelMeters,
                                             ITidalTerrain terrain, float fetchEnvelope01,
                                             in BreakerSettings settings)
        {
            if (terrain == null) return 0f;                     // no seabed ⇒ nothing shoals, nothing breaks
            if (train.Amplitude <= 0f) return 0f;               // glass is sacred

            float step = Mathf.Max(MinStepMeters, settings.WhitewaterStepMeters);
            Vector2 back = new Vector2(-train.Direction.x, -train.Direction.y);

            float contiguous = 1f;
            float age = 0f;
            for (int i = 1; i <= MarchSteps; i++)                // FIXED bound — the HLSL [unroll] contract
            {
                Vector2 p = WaveFetch.Pixelize(new Vector2(worldPos.x + back.x * (step * i),
                                                           worldPos.y + back.y * (step * i)));
                float depth = waterLevelMeters - terrain.ElevationAt(p);
                float shoaled = ShoaledHeightAt(in train, depth, fetchEnvelope01);
                contiguous *= Breaking01(shoaled, depth, in settings);
                age += contiguous;                              // still inside the same bore
            }

            return step * age;
        }

        /// <summary>
        /// The whitewater's remaining energy, 0..1 — <c>exp(−t/τ)</c>, where the age
        /// <c>t = metersSinceBreak / boreSpeed</c> and the bore travels at the shallow-water celerity
        /// <c>√(g·d)</c>. 1 in the boil right at the break, decaying smoothly shoreward with no
        /// plateau anywhere in the range.
        ///
        /// <para>The <b>distance</b> is geometry and the <b>speed</b> is physics, so the age is a
        /// genuine time and a retune of the decay moves the whole streak instead of choosing which flat
        /// shade it draws in.</para>
        /// </summary>
        public static float WhitewaterEnergy01(float metersSinceBreak, float depthMeters, float gravity,
                                               in BreakerSettings settings)
        {
            float d = Mathf.Max(MinDepthMeters, depthMeters);
            float boreSpeed = Mathf.Sqrt(Mathf.Max(0f, gravity) * d);
            if (boreSpeed <= 1e-6f) return 0f;

            float tau = Mathf.Max(MinDecaySeconds, settings.WhitewaterDecaySeconds);
            float age = Mathf.Max(0f, metersSinceBreak) / boreSpeed;
            return Mathf.Exp(-age / tau);
        }

        // ---- the whole model at a position ---------------------------------------------------------

        /// <summary>
        /// The shoaled deep-water height of one train at a depth: <c>H = 2·A·envelope·Ks</c>. Split out
        /// because the whitewater march needs it at every step without paying for a slope probe.
        /// </summary>
        public static float ShoaledHeightAt(in WaveTrain train, float depthMeters, float fetchEnvelope01)
        {
            if (depthMeters <= 0f) return 0f;
            float deepHeight = 2f * train.Amplitude * Mathf.Clamp01(fetchEnvelope01);
            if (deepHeight <= 0f) return 0f;

            float local = ShoaledWavelength(train.Wavelength, depthMeters);
            float ks = ShoalingCoefficient(train.PhaseSpeed, train.Wavelength, local, depthMeters);
            return deepHeight * ks;
        }

        /// <summary>
        /// <b>THE MODEL AT A POSITION</b> — shoal, break, classify, in one call. This is the entry
        /// point the consumers use; the pieces are split out above so each half can be pinned on its
        /// own (the <see cref="WaveFetch"/> discipline).
        ///
        /// <para>Costs <b>three</b> terrain samples: one for the depth here, two for the slope probe.
        /// The whitewater march (<see cref="MetersSinceBreak"/>) is a separate call so a consumer that
        /// only wants "is it breaking" never pays the 16 extra samples.</para>
        ///
        /// <para><b>Dry ground and glass both return <see cref="BreakerSample.NotBreaking"/></b> — a
        /// zero-amplitude field breaks nowhere, which is glass calm staying sacred (ADR 0018 §(1)).
        /// A null <paramref name="terrain"/> means open water everywhere: no bed, nothing to shoal on,
        /// nothing breaks — the same "everywhere deep" fallback <see cref="WaveFetch"/> takes.</para>
        /// </summary>
        /// <param name="worldPos">Where to ask (world XY).</param>
        /// <param name="train">The train that is breaking — normally the field's
        /// <see cref="WaveTrains.Dominant"/>, taken from <see cref="SharedWaveField"/> so the surf and
        /// the drawn sea are the same sea.</param>
        /// <param name="waterLevelMeters">The active region's water level (metres above chart datum) —
        /// <c>IEnvironmentService.WaterLevelAt</c>. <b>This is the term that moves the surf line over
        /// the day.</b></param>
        /// <param name="terrain">The authored seabed (<c>GameServices.TidalTerrain</c>); null = open
        /// water everywhere.</param>
        /// <param name="fetchEnvelope01">The wind-fetch amplitude multiplier at this position
        /// (<see cref="WaveFetch.EnvelopeAt"/>); 1 = no fetch limiting, which is the exact passthrough.</param>
        /// <param name="settings">The model's tunables.</param>
        public static BreakerSample SampleAt(Vector2 worldPos, in WaveTrain train, float waterLevelMeters,
                                             ITidalTerrain terrain, float fetchEnvelope01,
                                             in BreakerSettings settings)
        {
            if (terrain == null) return BreakerSample.NotBreaking;
            if (train.Amplitude <= 0f) return BreakerSample.NotBreaking;   // glass is sacred

            float depth = waterLevelMeters - terrain.ElevationAt(WaveFetch.Pixelize(worldPos));
            if (depth <= 0f) return BreakerSample.NotBreaking;             // dry ground

            float deepHeight = 2f * train.Amplitude * Mathf.Clamp01(fetchEnvelope01);
            float local = ShoaledWavelength(train.Wavelength, depth);
            float celerity = ShoaledCelerity(train.PhaseSpeed, train.Wavelength, local);
            float ks = ShoalingCoefficient(train.PhaseSpeed, train.Wavelength, local, depth);
            float shoaled = deepHeight * ks;

            float breaking = Breaking01(shoaled, depth, in settings);
            float height = DepthLimitedHeight(shoaled, depth, settings.BreakerIndex);

            float slope = BedSlopeAlong(worldPos, train.Direction, settings.SlopeProbeMeters, terrain);
            float xi = Iribarren(slope, deepHeight, train.Wavelength);
            BreakerClass cls = breaking > 0f ? ClassFor(xi, in settings) : BreakerClass.None;

            return new BreakerSample(depth, local, celerity, ks, height, shoaled, breaking, slope, xi, cls);
        }

        // ==== THE CONTOUR: invert the criterion once per tick, not once per pixel (PR 2) ============
        //
        // Everything above runs FORWARD — given a depth, shoal the wave and ask whether H >= gamma*d.
        // That is the physical definition and it stays the definition. But it costs a tanh, two pows, a
        // sinh and a sqrt, and MetersSinceBreak needs the answer at MarchSteps points per pixel. The
        // renderer therefore INVERTS it once on the sim tick (see BreakerContour for the full why) and
        // asks the cheap question per pixel instead: is the water shallower than the break depth?
        //
        // ⚠️ The C# and the HLSL run the SAME interpolation, so the twin is exact to float epsilon.
        // The approximation these carry is the piecewise-in-envelope fit, and it is MEASURED and pinned
        // in BreakerContourTests (2.77% of the break depth at the shipped lee floor) rather than
        // asserted — a twin divergence would be a bug, a stated approximation is a cost.

        /// <summary>The break criterion evaluated forward at one depth — the definition
        /// <see cref="SolveBreakDepth"/> inverts. Strictly decreasing in depth, which is what makes the
        /// inversion single-valued.</summary>
        public static float BreakRatioAtDepth(in WaveTrain train, float depthMeters, float fetchEnvelope01,
                                              float breakerIndex)
            => BreakRatio(ShoaledHeightAt(in train, depthMeters, fetchEnvelope01), depthMeters, breakerIndex);

        /// <summary>
        /// The depth (metres) at which this train reaches <paramref name="ratioTarget"/> of the break
        /// criterion — bisected over a fixed bracket in a fixed
        /// <see cref="BreakerContour.SolveIterations"/> steps, so the solve is bounded and deterministic
        /// (rule 5) rather than iterating to a tolerance.
        ///
        /// <para>Returns <b>0</b> when the wave never reaches the target even at the shallowest depth
        /// the model admits — a train too small to break on this shore, which is a real answer and not
        /// a failure.</para>
        /// </summary>
        public static float SolveBreakDepth(in WaveTrain train, float fetchEnvelope01, float breakerIndex,
                                            float ratioTarget)
        {
            float lo = MinDepthMeters;
            float hi = BreakerContour.MaxSolveDepthMeters;

            // ratio is strictly DECREASING in depth, so lo is the "breaking" end of the bracket.
            if (BreakRatioAtDepth(in train, lo, fetchEnvelope01, breakerIndex) < ratioTarget) return 0f;
            if (BreakRatioAtDepth(in train, hi, fetchEnvelope01, breakerIndex) >= ratioTarget) return hi;

            for (int i = 0; i < BreakerContour.SolveIterations; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (BreakRatioAtDepth(in train, mid, fetchEnvelope01, breakerIndex) >= ratioTarget) lo = mid;
                else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        /// <summary>
        /// Solve the whole contour for one train: the break depth and the gate's outer depth at
        /// envelope 1, at the midpoint, and at the fetch lee floor. Call it on the sim tick and hand the
        /// result to the renderer.
        /// </summary>
        /// <param name="train">The breaking train — normally the field's
        /// <see cref="WaveTrains.Dominant"/>, taken from <see cref="SharedWaveField"/> so the surf and
        /// the drawn sea are the same sea.</param>
        /// <param name="leeEnvelope01">The fetch model's lee floor (<see cref="WaveFetchSettings"/>'s
        /// effective floor, or 1 when fetch is off) — the lower anchor of the interpolation.</param>
        public static BreakerContour ContourFor(in WaveTrain train, float leeEnvelope01,
                                                in BreakerSettings settings)
        {
            if (train.Amplitude <= 0f) return BreakerContour.None;      // glass is sacred

            float gamma = Mathf.Max(0f, settings.BreakerIndex);
            if (gamma <= 0f) return BreakerContour.None;                // a stale settings struct is inert

            float band = Mathf.Clamp(settings.BreakBandRatio, 0.01f, 0.9f);
            float lee = Mathf.Clamp(leeEnvelope01, 0.01f, 1f);
            float mid = MidEnvelopeFor(lee);

            var breaks = new Vector3(SolveBreakDepth(in train, 1f, gamma, 1f),
                                     SolveBreakDepth(in train, mid, gamma, 1f),
                                     SolveBreakDepth(in train, lee, gamma, 1f));
            var outer = new Vector3(SolveBreakDepth(in train, 1f, gamma, 1f - band),
                                    SolveBreakDepth(in train, mid, gamma, 1f - band),
                                    SolveBreakDepth(in train, lee, gamma, 1f - band));

            return new BreakerContour(breaks, outer, lee, breaks.x > 0f);
        }

        /// <summary>The middle envelope a contour is solved at — the midpoint between the lee floor and
        /// 1, so only the lee floor has to travel to the shader. Deriving it rather than publishing it
        /// is what keeps the packing to two vectors.</summary>
        public static float MidEnvelopeFor(float leeEnvelope01) => (1f + Mathf.Clamp01(leeEnvelope01)) * 0.5f;

        /// <summary>
        /// Read one of a contour's depth triples at a position's own fetch envelope — the piecewise
        /// interpolation the HLSL twin spells identically.
        ///
        /// <para>A lee floor of 1 (fetch off) collapses to the single anchor, so the model is a
        /// byte-exact no-op when the owner has the fetch dial down.</para>
        /// </summary>
        public static float DepthAtEnvelope(Vector3 depths, float leeEnvelope01, float envelope01)
        {
            float lee = Mathf.Clamp01(leeEnvelope01);
            if (lee >= 1f - 1e-4f) return depths.x;          // fetch off: one anchor, no interpolation

            float e = Mathf.Clamp01(envelope01);
            float mid = MidEnvelopeFor(lee);
            if (e >= mid)
                return Mathf.Lerp(depths.y, depths.x, Mathf.Clamp01((e - mid) / Mathf.Max(1f - mid, 1e-4f)));
            return Mathf.Lerp(depths.z, depths.y, Mathf.Clamp01((e - lee) / Mathf.Max(mid - lee, 1e-4f)));
        }

        /// <summary>
        /// <b>The cheap break gate</b> — the same smooth 0..1 <see cref="Breaking01"/> answers, read off
        /// the pre-solved contour instead of shoaling the wave again. 1 where the water is shallower
        /// than the break depth, 0 out past the gate's outer edge, Hermite between.
        ///
        /// <para><b>⚠️ The gate's in-band SHAPE differs from <see cref="Breaking01"/>'s.</b> That one is
        /// a Hermite in ratio-space; this is a Hermite in depth-space, and <c>ratio(depth)</c> is not
        /// linear, so the two agree exactly at both edges and differ inside the band. Both are the same
        /// physical criterion with the same break line — the band is a smoothing choice, not a claim —
        /// and the divergence is measured and pinned rather than left for someone to trip over.</para>
        ///
        /// <para>⚠️ Still a GATE, never a scale on an age (the class note).</para>
        /// </summary>
        public static float Breaking01FromContour(float depthMeters, in BreakerContour contour,
                                                  float fetchEnvelope01)
        {
            if (!contour.Breaks) return 0f;
            if (depthMeters <= 0f) return 0f;                // dry ground breaks nothing

            float breakDepth = DepthAtEnvelope(contour.BreakDepths, contour.LeeEnvelope, fetchEnvelope01);
            if (breakDepth <= 0f) return 0f;
            float outerDepth = Mathf.Max(DepthAtEnvelope(contour.OuterDepths, contour.LeeEnvelope, fetchEnvelope01),
                                         breakDepth + WaveFetch.MinGateBand);

            // Shallower = more broken, so the gate runs the other way round from a depth smoothstep.
            return 1f - WaveFetch.SmoothstepEdge(breakDepth, outerDepth, depthMeters);
        }

        // ==== THE PLUNGING BAND: where the bathymetry earns a lip and a barrel (PR 2 drop 2) =======

        /// <summary>
        /// <b>How plunging is this break, 0..1</b> — <see cref="ClassFor"/>'s hard table, softened into a
        /// weight so the anatomy can fade in and out instead of snapping between breaker types as the
        /// tide moves the slope under a wave.
        ///
        /// <para>1 in the middle of the plunging band, falling to 0 at the spilling boundary below and
        /// the collapsing boundary above. The edges are smoothed over a fraction of the band, which is
        /// the same reasoning that made the break gate a smoothstep: a hard classification would pop the
        /// lip and the barrel on and off along a contour as the seabed gradient crossed a threshold, and
        /// the seabed gradient is a sampled quantity with texture quantization in it.</para>
        ///
        /// <para><b>⚠️ This widens NOTHING.</b> It is a smoothing of the same Battjes thresholds
        /// <see cref="ClassFor"/> uses, and at the band's centre the two agree exactly. Barrels still
        /// appear only where the bathymetry earns them — which is the claim ADR 0040 is making, and the
        /// one this weight must not quietly relax. <c>BreakerPlungingTests</c> pins that the weight is 0
        /// wherever <see cref="ClassFor"/> says <see cref="BreakerClass.Spilling"/> at the shipped
        /// thresholds' midpoints.</para>
        /// </summary>
        public static float PlungingWeight01(float iribarren, in BreakerSettings settings)
        {
            float spilling = Mathf.Max(0f, settings.SpillingLimit);
            float plunging = Mathf.Max(spilling + 1e-3f, settings.PlungingLimit);

            // Feather each edge by a tenth of the band, STRADDLING the threshold rather than starting at
            // it — so the half-weight crossing lands exactly on Battjes' number.
            //
            // ⚠️ The first version feathered UPWARD from each limit, and its own test caught it: the
            // half-weight point came out at ξ 0.641 against a published threshold of 0.5. That is not a
            // softening, it is a 28 % shift of the spilling/plunging boundary — barrels suppressed on
            // slopes that had earned them, silently, by an implementation detail. The docstring above
            // already said "allowed to blur the boundary, NOT to move it"; only the measurement noticed
            // that the code did not.
            float half = (plunging - spilling) * 0.05f;
            float risingIn = WaveFetch.SmoothstepEdge(spilling - half, spilling + half, iribarren);
            float fallingOut = WaveFetch.SmoothstepEdge(plunging - half, plunging + half, iribarren);
            return Mathf.Clamp01(risingIn * (1f - fallingOut));
        }

        /// <summary>
        /// <b>How far forward the lip is thrown</b> (metres), for a wave of
        /// <paramref name="waveHeightMeters"/> breaking at this plunging weight.
        ///
        /// <para>A plunging breaker's crest outruns its own base and lands ahead of it; the throw scales
        /// with the height of the wave doing the throwing, which is the depth-limited <c>γ·d</c> here
        /// rather than the deep-water height — a wave that has broken is only as tall as the water it is
        /// running over. A spilling breaker throws nothing, so the weight multiplies the whole
        /// thing.</para>
        /// </summary>
        public static float LipThrowMeters(float waveHeightMeters, float plungingWeight01, float throwPerHeight)
            => Mathf.Max(0f, waveHeightMeters) * Mathf.Clamp01(plungingWeight01) * Mathf.Max(0f, throwPerHeight);

        /// <summary>
        /// <see cref="MetersSinceBreak"/> off the pre-solved contour, and along an <b>explicit</b>
        /// travel direction.
        ///
        /// <para><b>Why the direction is a parameter here.</b> PR 1 marches along the train's
        /// deep-water heading, which is right for a hull sampling the sea it is in. A drawn breaker line
        /// is not that: a shoaling wave <b>refracts</b> toward shore-normal, which is why surf runs in
        /// parallel to the depth contours however the swell was heading offshore. The renderer therefore
        /// marches back along the seabed gradient — the shoreward direction it already derives per pixel
        /// — and this overload is how both sides can be handed the same direction and stay exact twins.
        /// Refraction is not otherwise modelled; the direction is where it enters, and that is stated
        /// rather than quietly assumed.</para>
        ///
        /// <para>The contiguity product, the fixed bound and the no-clamp-before-the-decay discipline
        /// are all exactly <see cref="MetersSinceBreak"/>'s — this is the same march with a cheaper gate
        /// and a supplied heading.</para>
        /// </summary>
        public static float MetersSinceBreakAlong(Vector2 worldPos, Vector2 travelDirection,
                                                  float waterLevelMeters, ITidalTerrain terrain,
                                                  in BreakerContour contour, float fetchEnvelope01,
                                                  in BreakerSettings settings)
        {
            if (terrain == null || !contour.Breaks) return 0f;

            float sqrMagnitude = travelDirection.x * travelDirection.x + travelDirection.y * travelDirection.y;
            if (sqrMagnitude < 1e-12f) return 0f;            // no heading, no bore
            float inv = 1f / Mathf.Sqrt(sqrMagnitude);
            Vector2 back = new Vector2(-travelDirection.x * inv, -travelDirection.y * inv);

            float step = Mathf.Max(MinStepMeters, settings.WhitewaterStepMeters);

            float contiguous = 1f;
            float age = 0f;
            for (int i = 1; i <= MarchSteps; i++)            // FIXED bound — the HLSL [unroll] contract
            {
                Vector2 p = WaveFetch.Pixelize(new Vector2(worldPos.x + back.x * (step * i),
                                                           worldPos.y + back.y * (step * i)));
                float depth = waterLevelMeters - terrain.ElevationAt(p);
                contiguous *= Breaking01FromContour(depth, in contour, fetchEnvelope01);
                age += contiguous;
            }

            return step * age;
        }
    }
}
