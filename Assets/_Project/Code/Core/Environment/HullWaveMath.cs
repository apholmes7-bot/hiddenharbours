using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The pure math of the waves a moving hull makes. Engine-light and allocation-free, so EditMode
    /// tests walk it headless; the water shader carries a line-for-line twin of
    /// <see cref="SourceHeight"/> (<c>HullWaveSourceHeight</c>) and the tests read the shader to pin it.
    ///
    /// <para><b>The model in one breath.</b> As she makes way, a hull drops wave SOURCES along her track
    /// (one chain at the stern, one at the bow). Each source sends out one ring: a band of crests
    /// <see cref="PacketWidth"/> wide whose centre spreads at <see cref="RingSpeed"/>, while the crests
    /// themselves run outward through the band at her speed (the phase speed of the wave that keeps
    /// pace with her, <see cref="StationaryWavelength"/>). A line of rings dropped by a source moving at
    /// V, each spreading at V·sin(α), is bounded by a V of half-angle α: with α = 19.47° that is the
    /// Kelvin wedge. Behind her on the track the crests of successive rings line up across the track
    /// (the transverse waves), because along the track the crests travel at exactly her speed and so
    /// keep station with her. Once laid, a ring belongs to the water: it keeps spreading, loses height
    /// (<see cref="TimeFade"/>, <see cref="Spread"/>) and dies where it is, whatever she does next, so
    /// a turn bends the wake and a stop leaves it running out across the water.</para>
    ///
    /// <para><b>Deterministic</b> (rule 5): no randomness and no frame counts; everything is a function of
    /// the source's birth time, place and the hull's speed and length then.</para>
    /// </summary>
    public static class HullWaveMath
    {
        /// <summary>Gravity, m/s². The same value the water's dispersion uses.</summary>
        public const float Gravity = 9.81f;

        /// <summary>The ring's band is cut beyond this many band half-widths (exp(-9) ≈ 1e-4 of the
        /// crest: invisible). Mirrored as <c>HH_HULL_WAVE_PACKET_REACH</c> in the water shader.</summary>
        public const float PacketReach = 3f;

        /// <summary>The budget spacing is widened by this factor (one part in ten thousand: invisible) so
        /// that rounding can never fit one more live source into a life than the budget.</summary>
        private const float BudgetMargin = 1.0001f;

        /// <summary>
        /// How fast a ring's band spreads for a hull at <paramref name="speed"/>: <c>V·sin α</c>. The
        /// envelope of rings dropped along her track is then a V of half-angle exactly α.
        /// </summary>
        public static float RingSpeed(float speed, float halfAngleDegrees)
        {
            if (speed <= 0f) return 0f;
            return speed * Mathf.Sin(Mathf.Clamp(halfAngleDegrees, 0f, 89f) * Mathf.Deg2Rad);
        }

        /// <summary>
        /// The half-angle of the V a line of rings makes, degrees: <c>asin(u / V)</c>. The inverse of
        /// <see cref="RingSpeed"/>; tests use it to prove the wedge.
        /// </summary>
        public static float WedgeHalfAngleDegrees(float ringSpeed, float speed)
        {
            if (speed <= 0f) return 0f;
            return Mathf.Asin(Mathf.Clamp01(ringSpeed / speed)) * Mathf.Rad2Deg;
        }

        /// <summary>The wavelength that keeps station behind a hull at <paramref name="speed"/>:
        /// <c>λ = 2πV²/g</c>. Its deep-water phase speed is exactly V.</summary>
        public static float StationaryWavelength(float speed)
        {
            if (speed <= 0f) return 0f;
            return 2f * Mathf.PI * speed * speed / Gravity;
        }

        /// <summary>The wavelength she draws: <see cref="StationaryWavelength"/> held inside the
        /// owner's range.</summary>
        public static float Wavelength(float speed, in HullWaveSettings s)
        {
            float lo = Mathf.Max(s.MinWavelengthMetres, 0.05f);
            float hi = Mathf.Max(s.MaxWavelengthMetres, lo);
            return Mathf.Clamp(StationaryWavelength(speed), lo, hi);
        }

        /// <summary>The wavelength a hull of <paramref name="lengthMetres"/> draws: as
        /// <see cref="Wavelength(float, in HullWaveSettings)"/>, with the longest also held to
        /// <c>MaxWavelengthPerMetreLength · L</c> (when that dial is above 0), so a small hull draws small
        /// waves and a long one long waves.</summary>
        public static float Wavelength(float speed, float lengthMetres, in HullWaveSettings s)
        {
            float lo = Mathf.Max(s.MinWavelengthMetres, 0.05f);
            float hi = Mathf.Max(s.MaxWavelengthMetres, lo);
            if (s.MaxWavelengthPerMetreLength > 0f && lengthMetres > 0f)
                hi = Mathf.Max(Mathf.Min(hi, s.MaxWavelengthPerMetreLength * lengthMetres), lo);
            return Mathf.Clamp(StationaryWavelength(speed), lo, hi);
        }

        /// <summary>Deep-water phase speed of a wave of <paramref name="wavelength"/>: <c>√(gλ/2π)</c>.</summary>
        public static float PhaseSpeed(float wavelength)
        {
            if (wavelength <= 0f) return 0f;
            return Mathf.Sqrt(Gravity * wavelength / (2f * Mathf.PI));
        }

        /// <summary>
        /// The speed a ring's crests run at: <paramref name="phaseSpeed"/> held to <c>MaxCrestHertz · λ</c>
        /// (when that dial is above 0), so no more than that many crests a second pass one spot of water.
        /// A short wave run at a fast hull's speed flashes light and dark; held, it crawls.
        /// </summary>
        public static float CrestSpeed(float phaseSpeed, float wavelength, in HullWaveSettings s)
        {
            if (s.MaxCrestHertz <= 0f || wavelength <= 0f) return phaseSpeed;
            return Mathf.Min(phaseSpeed, s.MaxCrestHertz * wavelength);
        }

        /// <summary>Froude number on length, <c>V / √(gL)</c>. 0 for no length.</summary>
        public static float Froude(float speed, float lengthMetres)
        {
            if (lengthMetres <= 0f || speed <= 0f) return 0f;
            return speed / Mathf.Sqrt(Gravity * lengthMetres);
        }

        /// <summary>
        /// Her crest height, metres: <c>HeightPerMetreLength · L · min(Fr / knee, 1)²</c>, eased in over
        /// one <c>MinSpeed</c> above <c>MinSpeed</c> and capped at <c>MaxHeightMetres</c>. A hull no longer than
        /// <c>SmallHullLengthMetres</c> is raised to at least <c>SmallHullCrestMetres · response</c> (0 = off),
        /// so a rowed dory's V reads at game zoom while longer hulls keep their height. So a rowed
        /// dory raises centimetres and a cargo vessel at speed raises tens of centimetres, from her
        /// length and her speed alone. Exactly 0 at or below <c>MinSpeed</c>.
        /// </summary>
        public static float CrestHeight(float speed, float lengthMetres, in HullWaveSettings s)
        {
            if (speed <= s.MinSpeed || lengthMetres <= 0f || s.HeightPerMetreLength <= 0f) return 0f;
            float onset = Mathf.Clamp01((speed - s.MinSpeed) / Mathf.Max(s.MinSpeed, 0.05f));
            float response = Mathf.Min(Froude(speed, lengthMetres) / Mathf.Max(s.FroudeKnee, 1e-3f), 1f);
            float h = s.HeightPerMetreLength * lengthMetres * response * response * onset;
            if (s.SmallHullCrestMetres > 0f && lengthMetres <= s.SmallHullLengthMetres)
                h = Mathf.Max(h, s.SmallHullCrestMetres * response * onset);
            return Mathf.Min(h, Mathf.Max(s.MaxHeightMetres, 0f));
        }

        /// <summary>
        /// The dials a hull of <paramref name="lengthMetres"/> lays her waves with: <paramref name="s"/>, with
        /// <c>SpacingWavelengths</c> replaced by <c>SmallHullSpacingWavelengths</c> for a hull no longer than
        /// <c>SmallHullLengthMetres</c> (when that dial is above 0). Otherwise <paramref name="s"/> unchanged.
        /// </summary>
        public static HullWaveSettings ForHull(float lengthMetres, in HullWaveSettings s)
        {
            HullWaveSettings e = s;
            if (s.SmallHullSpacingWavelengths > 0f && lengthMetres > 0f && lengthMetres <= s.SmallHullLengthMetres)
                e.SpacingWavelengths = s.SmallHullSpacingWavelengths;
            return e;
        }

        /// <summary>
        /// The dials a hull under motor or sail lays her V with: <paramref name="s"/> with no hull counted
        /// short (<c>SmallHullLengthMetres</c> = 0), so a motored punt lays the clean, evenly spaced rings a
        /// long hull does (no scatter, no broken arcs, no small-hull crest floor): the V window draws her
        /// (owner 11-21: "make motored boats a v wake"). Her trail foam is not read from these.
        /// </summary>
        public static HullWaveSettings ForMotor(in HullWaveSettings s)
        {
            HullWaveSettings e = s;
            e.SmallHullLengthMetres = 0f;
            return e;
        }

        /// <summary>The packed direction's grid: each axis in [-1, 1] is rounded to one of this many steps.
        /// Mirrored as <c>HH_HULL_WAVE_TRAVEL_STEPS</c> in the water shader.</summary>
        public const float TravelSteps = 2048f;

        /// <summary>Half the packed axis's range in steps ((steps - 1) / 2): an axis of -1 packs as 0 and +1
        /// as steps - 1. Mirrored as <c>HH_HULL_WAVE_TRAVEL_HALF</c> in the water shader.</summary>
        public const float TravelHalfRange = 1023.5f;

        /// <summary>
        /// A direction packed into one whole number for the shader: <c>qx·steps + qy + 1</c>, each q the
        /// axis rounded onto <see cref="TravelSteps"/>; at most 2²², exact in a float. 0 for no direction
        /// (a whole ring). Unpacked, each axis is within 1/2047 of the unit vector's.
        /// </summary>
        public static float PackTravel(Vector2 travel)
        {
            float m = travel.magnitude;
            if (m < 1e-6f) return 0f;
            Vector2 u = travel / m;
            float qx = Mathf.Round(Mathf.Clamp(u.x + 1f, 0f, 2f) * TravelHalfRange);
            float qy = Mathf.Round(Mathf.Clamp(u.y + 1f, 0f, 2f) * TravelHalfRange);
            return qx * TravelSteps + qy + 1f;
        }

        /// <summary>The inverse of <see cref="PackTravel"/> (the shader's decode); zero for 0.</summary>
        public static Vector2 UnpackTravel(float packed)
        {
            if (packed < 0.5f) return Vector2.zero;
            float p = packed - 1f;
            float qx = Mathf.Floor(p / TravelSteps);
            return new Vector2(qx, p - qx * TravelSteps) / TravelHalfRange - Vector2.one;
        }

        // ---- THE ARMS OF A MOTORED HULL'S V (owner 11-21: "make motored boats a v wake"; second look: "smooth
        // the v wake arms ... a half circle, then space then a half circle repeating") ----------------------
        // A ring laid by a hull going u at V spreads at V·sin α, so it touches the arm of her V on each side at
        // the point R·n (R its radius, n = u·sin α ± p·cos α, p her port): every ring's touching point lies on
        // the SAME straight line through the point that laid the chain. So each V ring is drawn as a straight
        // piece of that line (its crests across n, phase locked to its front), a compact window along the line
        // (t = −u·cos α ± p·sin α), and the pieces of neighbouring rings overlap and sum into one continuous arm
        // whatever the rings' spacing: no stacked arcs (the beads) and no half circles (each young ring's arc).

        /// <summary>The integral of the arm window <c>(1 − x²)²</c> over x in [−1, 1]: each ring's piece stands for
        /// <c>16/15 · L</c> metres of arm, and its height is scaled by its <see cref="HullWaveSource.ArmSpan"/>
        /// over that, so the summed arm is her crest height whatever the spacing.</summary>
        public const float VArmWindowArea = 16f / 15f;

        /// <summary>What the shader's <c>_HHHullWaveV</c> carries and the samples read: (sin α, cos α,
        /// tan MotorVArmHalfWidthDegrees, MotorVArmBlendWavelengths).</summary>
        public static Vector4 VArmFrame(in HullWaveSettings s)
        {
            float a = Mathf.Clamp(s.KelvinHalfAngleDegrees, 0.5f, 89f) * Mathf.Deg2Rad;
            float half = Mathf.Clamp(s.MotorVArmHalfWidthDegrees, 0f, 80f) * Mathf.Deg2Rad;
            return new Vector4(Mathf.Sin(a), Mathf.Cos(a), Mathf.Tan(half), Mathf.Max(s.MotorVArmBlendWavelengths, 0.05f));
        }

        /// <summary>What the shader's <c>_HHHullWaveV2</c> carries: (MotorVShare, 1 / (2π · PacketWavelengths),
        /// MotorVArmOverlapSpans, 0). A V ring's band width is worked out from its wavenumber with the second, so its 1/width slot
        /// carries its <see cref="HullWaveSource.ArmSpan"/>.</summary>
        public static Vector4 VArmMix(in HullWaveSettings s)
        {
            return new Vector4(Mathf.Clamp01(s.MotorVShare), 1f / (2f * Mathf.PI * Mathf.Max(s.PacketWavelengths, 0.05f)),
                               Mathf.Max(s.MotorVArmOverlapSpans, 0f), 0f);
        }

        /// <summary>1 / a V ring's band width, from its wavenumber and <see cref="VArmMix"/>: 1 /
        /// <see cref="PacketWidth"/> for the dials it was laid with.</summary>
        public static float VArmInvWidth(float wavenumber, Vector4 mix) => wavenumber * mix.y;

        /// <summary>How far a V ring's piece reaches either way along the arm, metres:
        /// <c>R · tan(half width) + blend · λ</c>, and never under <c>overlap · span</c>, so pieces laid far apart
        /// (a bow chain on few sources: the half circles' beat) still overlap into one line.</summary>
        public static float VArmHalfLength(float front, float wavenumber, float span, Vector4 frame, Vector4 mix)
        {
            float wavelength = wavenumber > 0f ? 2f * Mathf.PI / wavenumber : 0f;
            float grown = Mathf.Max(front, 0f) * frame.z + frame.w * wavelength;
            return Mathf.Max(Mathf.Max(grown, mix.z * Mathf.Max(span, 0f)), 1e-3f);
        }

        /// <summary>
        /// The metres of arm one V ring stands for: its spacing along the track, projected on the arm (cos α),
        /// over the share of her crest it was laid with (<see cref="SourceAmplitude"/>), so the overlapping
        /// pieces sum to her crest height. 0 for no spacing or no height.
        /// </summary>
        public static float VArmSpan(float spacingMetres, float wavelength, float gain, in HullWaveSettings s)
        {
            if (spacingMetres <= 0f || wavelength <= 0f) return 0f;
            float share = Mathf.Min(1f, Mathf.Max(gain, 0f) * spacingMetres / wavelength);
            if (share <= 0f) return 0f;
            float cos = Mathf.Cos(Mathf.Clamp(s.KelvinHalfAngleDegrees, 0.5f, 89f) * Mathf.Deg2Rad);
            return spacingMetres * cos / share;
        }

        /// <summary>
        /// The window along the arm, (1 − x²)², x = along / L, with its derivative in <paramref name="along"/>.
        /// Forward (along &lt; 0, toward the point that laid the chain) it reaches no further than that point
        /// (R · cot α), so nothing draws ahead of her bow and the arm opens out of her.
        /// </summary>
        public static float VArmWindow(float along, float front, float halfLength, Vector4 frame, out float dWindow)
        {
            float lead = Mathf.Min(halfLength, Mathf.Max(front, 0f) * frame.y / Mathf.Max(frame.x, 1e-4f));
            float len = Mathf.Max(along < 0f ? lead : halfLength, 1e-4f);
            float x = along / len;
            float q = 1f - x * x;
            if (q <= 0f) { dWindow = 0f; return 0f; }
            dWindow = -4f * q * x / len;
            return q * q;
        }

        /// <summary>
        /// One V ring's height at <paramref name="d"/> from where it was laid, and its slope: the straight piece
        /// of the arm on <paramref name="d"/>'s side of her <paramref name="travel"/>. The water shader's
        /// <c>HullWaveArmSource</c> is its twin (HullWaveMotorVTests reads it).
        /// </summary>
        public static float ArmHeight(Vector2 d, Vector2 travel, float amplitude, float front, float wavenumber,
                                      float phase, float span, float invSpread, Vector4 frame, Vector4 mix,
                                      out Vector2 slope)
        {
            slope = Vector2.zero;
            if (amplitude == 0f || span <= 0f || wavenumber <= 0f) return 0f;
            Vector2 p = new Vector2(-travel.y, travel.x);
            float side = Vector2.Dot(d, p) >= 0f ? 1f : -1f;
            Vector2 n = travel * frame.x + p * (side * frame.y);
            Vector2 t = -travel * frame.y + p * (side * frame.x);
            float across = Vector2.Dot(d, n);
            float along = Vector2.Dot(d, t);
            float halfLength = VArmHalfLength(front, wavenumber, span, frame, mix);
            float w = VArmWindow(along, front, halfLength, frame, out float dw);
            if (w <= 0f) return 0f;
            float norm = span / (VArmWindowArea * halfLength);
            float invWidth = VArmInvWidth(wavenumber, mix);
            float r = Mathf.Max(across, 0f);
            float h = SourceHeight(r, amplitude, front, wavenumber, phase, invWidth, invSpread);
            float dh = across > 0f ? SourceSlope(r, amplitude, front, wavenumber, phase, invWidth, invSpread) : 0f;
            slope = (n * (dh * w) + t * (h * dw)) * norm;
            return h * w * norm;
        }

        /// <summary>The radius past which a V ring draws nothing: its band's reach across the arm and its piece's
        /// reach along it, <c>√((front + PacketReach · width)² + L²)</c>. The pack culls with it; the shader's
        /// test is the same.</summary>
        public static float VArmReach(float front, float wavenumber, float span, Vector4 frame, Vector4 mix)
        {
            float invWidth = VArmInvWidth(wavenumber, mix);
            float across = front + (invWidth > 0f ? PacketReach / invWidth : 0f);
            float along = VArmHalfLength(front, wavenumber, span, frame, mix);
            return Mathf.Sqrt(across * across + along * along);
        }

        /// <summary>A V ring's crest phase: locked to its front (<c>k · front</c>, wrapped), so the crest of every
        /// ring sits on the arm line and the pieces add up in step into one line that keeps station with her.</summary>
        public static float VArmPhase(float wavenumber, float front)
        {
            double p = (double)wavenumber * System.Math.Max(front, 0f);
            p %= 2.0 * System.Math.PI;
            return (float)p;
        }

        /// <summary>True when a hull going <paramref name="travel"/> lays the rings of a V (MotorVShare above 0 and
        /// some way on); a rowed hull is handed zero travel.</summary>
        public static bool LaysV(Vector2 travel, in HullWaveSettings s)
        {
            return s.MotorVShare > 0f && travel.sqrMagnitude > 1e-12f;
        }

        /// <summary>True for a hull no longer than <c>SmallHullLengthMetres</c> (and longer than nothing).</summary>
        public static bool IsSmallHull(float lengthMetres, in HullWaveSettings s)
        {
            return lengthMetres > 0f && lengthMetres <= s.SmallHullLengthMetres;
        }

        /// <summary>
        /// A fixed number in [0, 1) for one ring, from the time it was laid (game seconds, to the tenth of a
        /// millisecond), a salt that tells her chains apart and a channel that tells the dials apart. A pure
        /// integer hash: the same voyage gives the same numbers on every run and at every frame rate (rule 5).
        /// </summary>
        public static float Scatter01(double birthSeconds, float salt, int channel)
        {
            unchecked
            {
                long t = (long)System.Math.Floor(birthSeconds * 10000.0);
                uint h = (uint)t ^ (uint)(t >> 32) * 0x9E3779B1u;
                h ^= (uint)Mathf.RoundToInt(salt * 1000f) * 0x85EBCA77u;
                h ^= (uint)channel * 0xC2B2AE3Du;
                h ^= h >> 16; h *= 0x7FEB352Du;
                h ^= h >> 15; h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h >> 8) * (1f / 16777216f);
            }
        }

        /// <summary>
        /// A short hull's ring made a little different from the one before it (the owner's look of 11-19: the
        /// rowed dory's stacked loops read as "calculated"): its height cut by up to
        /// <c>SmallHullHeightScatter</c>, its wavelength (and band) moved by up to
        /// <c>SmallHullWavelengthScatter</c> either way, and its crests set off by up to
        /// <c>SmallHullPhaseScatter</c> of half a wavelength either way, each from <see cref="Scatter01"/> of
        /// its birth time, and its centre laid up to <c>SmallHullCentreScatterMetres</c> off her track either way.
        /// It is marked to be drawn as broken arcs while <c>SmallHullArcShare</c> is above 0.
        /// A longer hull's ring, or every dial at 0, is returned unchanged.
        /// </summary>
        public static HullWaveSource ScatterRing(in HullWaveSource source, float lengthMetres, float salt,
                                                 in HullWaveSettings s)
        {
            HullWaveSource r = source;
            if (!IsSmallHull(lengthMetres, in s)) return r;
            double at = source.BirthSeconds;
            float height = Mathf.Clamp01(s.SmallHullHeightScatter);
            if (height > 0f) r.Amplitude *= 1f - height * Scatter01(at, salt, 0);
            float stretch = Mathf.Clamp(s.SmallHullWavelengthScatter, 0f, 0.9f);
            if (stretch > 0f && r.Wavenumber > 0f)
            {
                float scale = 1f + stretch * (2f * Scatter01(at, salt, 1) - 1f);
                r.Wavenumber /= scale;
                r.PacketWidth *= scale;
            }
            float phase = Mathf.Clamp01(s.SmallHullPhaseScatter);
            if (phase > 0f) r.PhaseOffset = phase * Mathf.PI * (2f * Scatter01(at, salt, 2) - 1f);
            float centre = Mathf.Max(s.SmallHullCentreScatterMetres, 0f);
            if (centre > 0f)
                r.Position += centre * new Vector2(2f * Scatter01(at, salt, 4) - 1f, 2f * Scatter01(at, salt, 5) - 1f);
            r.Arcs = s.SmallHullArcShare > 0f;
            return r;
        }

        /// <summary>
        /// The factor a short hull's next interval is laid at: from 1 up to <c>1 + 2·SmallHullSpacingScatter</c>
        /// (never closer, so her budget stays a hard bound), from <see cref="Scatter01"/> of the ring just laid.
        /// 1 for a longer hull or the dial at 0.
        /// </summary>
        public static float SpacingScatter(double birthSeconds, float lengthMetres, float salt, in HullWaveSettings s)
        {
            float spread = Mathf.Clamp(s.SmallHullSpacingScatter, 0f, 0.9f);
            if (spread <= 0f || !IsSmallHull(lengthMetres, in s)) return 1f;
            return 1f + 2f * spread * Scatter01(birthSeconds, salt, 3);
        }

        /// <summary>How long her waves live, seconds: per metre of her length, held in range.</summary>
        public static float Lifetime(float lengthMetres, in HullWaveSettings s)
        {
            float lo = Mathf.Max(s.MinLifetimeSeconds, 0.1f);
            float hi = Mathf.Max(s.MaxLifetimeSeconds, lo);
            return Mathf.Clamp(s.LifetimeSecondsPerMetreLength * Mathf.Max(lengthMetres, 0f), lo, hi);
        }

        /// <summary>
        /// Her waves' life once her budget is counted. With <c>KeepSpacing</c> on, a life her budget
        /// cannot cover at <c>SpacingWavelengths</c> is shortened to what it can (never below
        /// <c>MinLifetimeSeconds</c>, nor above <paramref name="lifetime"/>), so a fast hull keeps her
        /// rings close enough to read as one wake instead of single rings. Off, or at rest: unchanged.
        /// </summary>
        public static float KeptLifetime(float lifetime, float speed, float wavelength, int budget,
                                         in HullWaveSettings s)
        {
            if (!s.KeepSpacing || speed <= 0f || wavelength <= 0f || budget <= 0 || lifetime <= 0f)
                return lifetime;
            float fit = budget * Mathf.Max(s.SpacingWavelengths, 1e-3f) * wavelength / speed / BudgetMargin;
            float floor = Mathf.Min(Mathf.Max(s.MinLifetimeSeconds, 0.1f), lifetime);
            return Mathf.Clamp(fit, floor, lifetime);
        }

        /// <summary>
        /// The trail foam's fan dial while hull waves are on: 0 when <c>RetireFoamFan</c> is set (the
        /// waves draw the V now, and the fan's widening foam hid it), else <paramref name="fanFraction"/>.
        /// </summary>
        public static float FoamFanFraction(float fanFraction, in HullWaveSettings s)
        {
            return s.Enabled && s.RetireFoamFan ? 0f : fanFraction;
        }

        /// <summary>
        /// The trail foam's strength while hull waves are on and she makes way:
        /// <paramref name="strength"/> times <c>TrailFoamShare</c>. Moored, or hull waves off: unchanged.
        /// </summary>
        public static float TrailFoamStrength(float strength, bool makingWay, in HullWaveSettings s)
        {
            if (!s.Enabled || !makingWay) return strength;
            return strength * Mathf.Clamp01(s.TrailFoamShare);
        }

        /// <summary>
        /// <see cref="TrailFoamStrength(float, bool, in HullWaveSettings)"/> for a hull of
        /// <paramref name="lengthMetres"/>: a short hull lays <c>SmallHullTrailFoamShare</c> of it instead
        /// (when that dial is 0 or above), so a rowed hull's trail is a thin lace rather than a mat.
        /// </summary>
        public static float TrailFoamStrength(float strength, bool makingWay, float lengthMetres, in HullWaveSettings s)
        {
            if (!s.Enabled || !makingWay) return strength;
            if (s.SmallHullTrailFoamShare >= 0f && IsSmallHull(lengthMetres, in s))
                return strength * Mathf.Clamp01(s.SmallHullTrailFoamShare);
            return strength * Mathf.Clamp01(s.TrailFoamShare);
        }

        /// <summary>
        /// <see cref="TrailFoamStrength(float, bool, float, in HullWaveSettings)"/> for a hull that is
        /// <paramref name="motored"/> (under motor or sail, not oars): while she lays a V (MotorVShare above 0)
        /// her trail is that times <c>MotorVTrailFoamScale</c>, the same foam, less of it, so her bow's V reads
        /// over her stern (owner ruling 11-21: halve it). A rowed hull's trail is exactly the older law.
        /// </summary>
        public static float TrailFoamStrength(float strength, bool makingWay, float lengthMetres, bool motored,
                                              in HullWaveSettings s)
        {
            float laid = TrailFoamStrength(strength, makingWay, lengthMetres, in s);
            if (!s.Enabled || !makingWay || !motored || s.MotorVShare <= 0f) return laid;
            return laid * Mathf.Clamp01(s.MotorVTrailFoamScale);
        }

        /// <summary>
        /// Her churned trail band's half-width, metres, while hull waves are on and she makes way:
        /// <paramref name="radius"/> held to <c>TrailFoamMaxHalfWidthMetres</c> (0 = no limit). Moored,
        /// or hull waves off: unchanged.
        /// </summary>
        public static float TrailFoamRadius(float radius, bool makingWay, in HullWaveSettings s)
        {
            if (!s.Enabled || !makingWay || s.TrailFoamMaxHalfWidthMetres <= 0f) return radius;
            return Mathf.Min(radius, s.TrailFoamMaxHalfWidthMetres);
        }

        /// <summary>
        /// How the water draws a wave's light: the raw lift or darkening <paramref name="raw"/> (a share of
        /// the water's colour) brought softly under <paramref name="ceiling"/>:
        /// <c>raw · ceiling / (ceiling + |raw|)</c>. Slope 1 at zero, never reaching the ceiling, and smooth
        /// all the way, so a wave never shows a clipped, hard-edged band. Twin: the water shader's
        /// hull-wave shading (<c>hwShade</c>). 0 for no ceiling.
        /// </summary>
        /// <summary>While hull waves are on, the wake's own drawn foam puffs are not laid: her churn is the water's
        /// foam buffer, drawn by the one foam path every foam on the sea uses.</summary>
        public static bool DrawnFoamRetired(in HullWaveSettings s) => s.Enabled && s.RetireDrawnFoam;

        /// <summary>
        /// The low-light lift on her waves' shade: <c>1 + LowLightGain · dim</c>. The water draws the
        /// shade once before its palette grade as at noon, and the extra <c>LowLightGain · dim</c> share of
        /// it again after the grade, where the dusk value floor cannot flatten it. <c>dim</c> runs
        /// 0..1 as the sun's luminance <paramref name="sunLuminance"/> falls from
        /// <c>LowLightStartLuminance</c> to <c>LowLightFullLuminance</c>. Exactly 1 at or above the start
        /// (noon is untouched) and with no day/night cycle running (<paramref name="cycleOn"/> false).
        /// Twin of the shader's <c>hwDim</c>.
        /// </summary>
        public static float LowLightGain(float sunLuminance, bool cycleOn, in HullWaveSettings s)
        {
            if (!cycleOn) return 1f;
            float span = Mathf.Max(s.LowLightStartLuminance - s.LowLightFullLuminance, 1e-3f);
            float dim = Mathf.Clamp01((s.LowLightStartLuminance - sunLuminance) / span);
            return 1f + Mathf.Max(s.LowLightGain, 0f) * dim;
        }

        /// <summary>
        /// The low-light lift where <paramref name="vShare"/> (0..1) of the wave is a motored hull's V:
        /// <c>1 + lerp(LowLightGain, MotorVLowLightGain, vShare) · dim</c> (owner 11-22: the V's waves read
        /// as hard stripes at sunset). <paramref name="vShare"/> 0, a rowed hull's rings, is exactly
        /// <see cref="LowLightGain(float, bool, in HullWaveSettings)"/>. Twin of the shader's <c>hwLowGain</c>.
        /// </summary>
        public static float LowLightGain(float sunLuminance, bool cycleOn, float vShare, in HullWaveSettings s)
        {
            if (!cycleOn) return 1f;
            float span = Mathf.Max(s.LowLightStartLuminance - s.LowLightFullLuminance, 1e-3f);
            float dim = Mathf.Clamp01((s.LowLightStartLuminance - sunLuminance) / span);
            float ring = Mathf.Max(s.LowLightGain, 0f);
            float gain = ring + (Mathf.Max(s.MotorVLowLightGain, 0f) - ring) * Mathf.Clamp01(vShare);
            return 1f + gain * dim;
        }

        /// <summary>The shade the water draws: <see cref="SoftShade"/>, with a trough held to
        /// TroughCeilingShare of it. Twin of the shader's hull-wave shading.</summary>
        public static float WaveShade(float raw, in HullWaveSettings s)
        {
            float shade = SoftShade(raw, s.ShadeCeiling);
            return shade < 0f ? shade * s.TroughCeilingShare : shade;
        }

        /// <summary>How much a crest breaks (0..1, before CrestFoam): tall OR steep. Twin of the shader's
        /// hullCrestFoam.</summary>
        public static float CrestFoam01(float height, float steepness, in HullWaveSettings s)
        {
            float tall = Mathf.Clamp01((height - s.CrestFoamOnsetMetres) / Mathf.Max(s.CrestFoamRampMetres, 1e-3f));
            float steep = Mathf.Clamp01((steepness - s.CrestFoamSteepness) / Mathf.Max(s.CrestFoamSteepnessRamp, 1e-3f));
            return Mathf.Max(tall, steep);
        }

        public static float SoftShade(float raw, float ceiling)
        {
            if (ceiling <= 0f) return 0f;
            return raw * ceiling / (ceiling + Mathf.Abs(raw) + 1e-5f);
        }

        /// <summary>
        /// Seconds between two of her sources on one chain: the spacing the owner asked for in
        /// wavelengths, or wider when that would keep more than <paramref name="budget"/> alive over a
        /// life of <paramref name="lifetime"/>. The second term is what makes the budget a hard bound:
        /// a chain can never hold more than <c>budget</c> live sources. Infinity for a hull at rest.
        /// </summary>
        public static float DepositInterval(float speed, float wavelength, float lifetime, int budget,
                                            float spacingWavelengths)
        {
            if (speed <= 0f || wavelength <= 0f || budget <= 0) return float.PositiveInfinity;
            float bySpacing = Mathf.Max(spacingWavelengths, 1e-3f) * wavelength / speed;
            // A hair wider than life/budget, so float rounding can never let budget + 1 live at once.
            float byBudget = Mathf.Max(lifetime, 0f) / budget * BudgetMargin;
            return Mathf.Max(bySpacing, byBudget);
        }

        /// <summary>
        /// The height one source carries for a spacing of <paramref name="spacingMetres"/> along her
        /// track: her crest height times <c>min(1, gain · spacing / λ)</c>. Rings overlap about
        /// λ / spacing deep, so the sum stays near her crest height whatever the spacing.
        /// </summary>
        public static float SourceAmplitude(float crestHeight, float spacingMetres, float wavelength, float gain)
        {
            if (crestHeight <= 0f || wavelength <= 0f || spacingMetres <= 0f) return 0f;
            return crestHeight * Mathf.Min(1f, Mathf.Max(gain, 0f) * spacingMetres / wavelength);
        }

        /// <summary>A ring's band width, metres: <c>PacketWavelengths · λ</c>.</summary>
        public static float PacketWidth(float wavelength, in HullWaveSettings s)
        {
            return Mathf.Max(s.PacketWavelengths, 0.05f) * Mathf.Max(wavelength, 0f);
        }

        /// <summary>
        /// How much of its height a ring keeps at <paramref name="age"/>: <c>exp(-age/decay) · (1 - age/life)²</c>.
        /// 1 at birth, falling all its life, and EXACTLY 0 at the end of life and after, so retiring a
        /// source never pops. 0 for no life.
        /// </summary>
        public static float TimeFade(float age, float lifetime, float decaySeconds)
        {
            return TimeFade(age, lifetime, decaySeconds, 0f, 0f);
        }

        /// <summary>
        /// <see cref="TimeFade(float, float, float)"/> with its two ends shaped: the ring RISES from 0 to
        /// full over its first <paramref name="riseSeconds"/> (a smoothstep; 0 = born at full height), so a
        /// newborn ring paints no disk round the hull before it has spread, and its end fade
        /// <c>(remaining/endSpan)²</c> runs over only its last <paramref name="endSeconds"/> (0, or a full
        /// life, = the whole life, which is the three-argument law exactly). Still EXACTLY 0 at the end.
        /// </summary>
        public static float TimeFade(float age, float lifetime, float decaySeconds, float riseSeconds,
                                     float endSeconds)
        {
            if (lifetime <= 0f || age >= lifetime) return 0f;
            float a = Mathf.Max(age, 0f);
            float endSpan = endSeconds > 0f ? Mathf.Min(endSeconds, lifetime) : lifetime;
            float end = Mathf.Clamp01((lifetime - a) / endSpan);
            float decay = decaySeconds > 0f ? Mathf.Exp(-a / decaySeconds) : 1f;
            float rise = 1f;
            if (riseSeconds > 0f)
            {
                float t = Mathf.Clamp01(a / riseSeconds);
                rise = t * t * (3f - 2f * t);
            }
            return decay * end * end * rise;
        }

        /// <summary>Height lost to spreading round the ring: <c>1/√(1 + r/R)</c>.</summary>
        public static float Spread(float radius, float spreadMetres)
        {
            if (spreadMetres <= 0f) return 1f;
            return 1f / Mathf.Sqrt(1f + Mathf.Max(radius, 0f) / spreadMetres);
        }

        /// <summary>The band's profile across the ring: a Gaussian, cut at <see cref="PacketReach"/>.</summary>
        public static float Packet(float x)
        {
            if (x <= -PacketReach || x >= PacketReach) return 0f;
            return Mathf.Exp(-x * x);
        }

        /// <summary>
        /// The phase of a source's crests at <paramref name="age"/>: <c>k·c·age</c>, wrapped to [0, 2π) in
        /// double so the shader never sees a large angle.
        /// </summary>
        public static float Phase(float wavenumber, float phaseSpeed, double age)
        {
            return Phase(wavenumber, phaseSpeed, age, 0f);
        }

        /// <summary><see cref="Phase(float, float, double)"/> with the source's own fixed
        /// <paramref name="offset"/> (radians) added, wrapped into [0, 2π).</summary>
        public static float Phase(float wavenumber, float phaseSpeed, double age, float offset)
        {
            double p = (double)wavenumber * phaseSpeed * System.Math.Max(age, 0.0) + offset;
            p %= 2.0 * System.Math.PI;
            if (p < 0.0) p += 2.0 * System.Math.PI;
            return (float)p;
        }

        /// <summary>
        /// The height one source draws at distance <paramref name="radius"/> from where it was laid —
        /// the whole spatial law, and the C# twin of the water shader's <c>HullWaveSourceHeight</c>:
        /// <c>amp · Packet((r - front)/W) · Spread(r) · cos(k·r - phase)</c>.
        /// </summary>
        /// <param name="amplitude">Its height now (birth height × <see cref="TimeFade"/>).</param>
        /// <param name="front">Its band's radius now (ring speed × age).</param>
        /// <param name="wavenumber">2π/λ.</param>
        /// <param name="phase">From <see cref="Phase"/>.</param>
        /// <param name="invWidth">1 / band width.</param>
        /// <param name="invSpread">1 / <c>SpreadMetres</c> (0 = no spreading loss).</param>
        public static float SourceHeight(float radius, float amplitude, float front, float wavenumber,
                                         float phase, float invWidth, float invSpread)
        {
            if (amplitude == 0f) return 0f;
            float x = (radius - front) * invWidth;
            if (x <= -PacketReach || x >= PacketReach) return 0f;
            float env = Mathf.Exp(-x * x);
            float spread = 1f / Mathf.Sqrt(1f + radius * invSpread);
            return amplitude * env * spread * Mathf.Cos(wavenumber * radius - phase);
        }

        /// <summary>
        /// d(height)/d(radius) of <see cref="SourceHeight"/>, analytic — what lights the wave faces.
        /// Twin: the slope half of the shader's <c>HullWaveSourceHeight</c>.
        /// </summary>
        public static float SourceSlope(float radius, float amplitude, float front, float wavenumber,
                                        float phase, float invWidth, float invSpread)
        {
            if (amplitude == 0f) return 0f;
            float x = (radius - front) * invWidth;
            if (x <= -PacketReach || x >= PacketReach) return 0f;
            float env = Mathf.Exp(-x * x);
            float spread = 1f / Mathf.Sqrt(1f + radius * invSpread);
            float ph = wavenumber * radius - phase;
            float c = Mathf.Cos(ph);
            float s = Mathf.Sin(ph);
            float dEnv = -2f * x * invWidth * env;
            float dSpread = -0.5f * invSpread * spread * spread * spread;
            return amplitude * (dEnv * spread * c + env * dSpread * c - env * spread * wavenumber * s);
        }
    }
}
