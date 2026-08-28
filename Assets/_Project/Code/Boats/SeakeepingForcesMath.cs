using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The per-hull seakeeping response, resolved from <see cref="BoatHullDef"/> — how much THIS hull
    /// is moved and slewed by the sea, and how it damps its own rocking. Kept as a small value so the
    /// force helper takes a struct, not the whole Def (rule 4 stays clean, EditMode-testable with plain
    /// numbers). A dory corks about (low mass factor, little damping); a laden trader shrugs.
    /// </summary>
    public readonly struct SeakeepingResponse
    {
        /// <summary>How readily the sea moves this hull (dimensionless, ≥ 0). Higher = corks about more.
        /// A light dory is high; a heavy hull is low. Multiplies the whole wave force + yaw.</summary>
        public readonly float Response;

        /// <summary>Self-damping the hull applies to the wave-driven motion (design-unit drag against the
        /// boat's velocity/spin, ≥ 0). A steadier hull settles faster between crests. Applied by the
        /// caller as a light drag; folded here so one struct carries the hull's whole sea character.</summary>
        public readonly float Damping;

        public SeakeepingResponse(float response, float damping)
        {
            Response = Mathf.Max(0f, response);
            Damping = Mathf.Max(0f, damping);
        }

        /// <summary>The inert hull — unmoved by the sea (used when disabled / no hull). Equivalent to
        /// <c>default</c>.</summary>
        public static readonly SeakeepingResponse Inert = default;
    }

    /// <summary>
    /// The environmental force + yaw the sea applies to a hull this tick — ADR 0018 Arc B3, the payoff
    /// of the shared wave field: <b>the sea actually pushes the boat around</b>. The
    /// <see cref="BoatController"/> adds these ON TOP of its existing engine/rudder/drag/wind model
    /// (they never replace it).
    /// </summary>
    public readonly struct SeakeepingForce
    {
        /// <summary>The wave force in WORLD space (design-unit, before the controller's physics-feel
        /// scale). Zero on glass, in shelter, or when disabled.</summary>
        public readonly Vector2 Force;

        /// <summary>The wave YAW torque (design-unit, before the feel scale; sign matches the controller's
        /// rudder/oar convention — negative = bow-right). The beam/following slew that makes holding
        /// course a real task. Zero on glass, in shelter, or when disabled.</summary>
        public readonly float Torque;

        public SeakeepingForce(Vector2 force, float torque)
        {
            Force = force;
            Torque = torque;
        }

        /// <summary>No force, no yaw — a dead-calm, sheltered, or switched-off sea. Equivalent to
        /// <c>default</c>.</summary>
        public static readonly SeakeepingForce None = default;
    }

    /// <summary>
    /// <b>The sea pushes the boat</b> (ADR 0018 Arc B3) — the pure, headless-testable core that turns a
    /// shared-wave-field <see cref="WaveSample"/> under the hull into a force + yaw torque on the
    /// planar rigid body, decomposed by the boat's point of sail (P1 "The Sea Has Moods", P5 "Cozy but
    /// with Teeth"). It is the FORCE twin of the visual-only <see cref="BoatWaveMotionMath"/>: that one
    /// tilts the sprite, this one shoves the hull.
    ///
    /// <para><b>The physical read.</b> A boat sitting on a tilted patch of water is pushed <i>down the
    /// slope</i> toward the trough — the base wave force points along <c>−slope</c>. Decomposed against
    /// the hull's heading that becomes, exactly:
    /// <list type="bullet">
    /// <item><b>HEAD SEA</b> (waves onto the bow — the surface rising ahead): a retarding, pitching
    /// shove along <c>−bow</c> that costs headway — punching into a steep sea is slow and wet.</item>
    /// <item><b>BEAM SEA</b> (waves on the side): a lateral shove along the beam PLUS a yaw torque that
    /// tries to slew the bow — the dangerous point of sail, holding course now demands the helm.</item>
    /// <item><b>FOLLOWING SEA</b> (waves from astern — the surface rising behind, so the boat is on the
    /// forward face): a surge along <c>+bow</c> that pushes you along and a yaw that can broach.</item>
    /// </list>
    /// Head vs following is one axis (the sign of the along-bow slope), weighted separately so the owner
    /// can make punching in cost more than surging along; the beam axis is always the beam shove + yaw.</para>
    ///
    /// <para><b>Two-axis modulation (the owner's ratified bite).</b> TIME is already baked into the
    /// <paramref name="wave"/> — the field's amplitudes (and thus its slope) scale with SeaState01, and
    /// are exactly 0 on glass — plus a <see cref="SeakeepingSettings.SeaStateExponent"/> here so the
    /// force curve matches the visible wave. PLACE is <see cref="Exposure01"/>: open water takes the
    /// full sea, the shallow lee of land is sheltered. Force = base × strength × seaBite × exposure ×
    /// hull response, so <b>calm sheltered handling is UNCHANGED by construction</b> even with the
    /// feature on.</para>
    ///
    /// <para><b>Determinism &amp; scope (rules 5, 7).</b> Pure, static, allocation-free — the
    /// <paramref name="wave"/> derives from the deterministic wave field
    /// (<c>WaveMath.TrainsFrom</c> + <c>WaveMath.Sample(pos, gameTime)</c>, the SIM reference path, NOT
    /// the presentation animator — ADR 0018 addendum), so same inputs → same force, forever, no RNG,
    /// nothing saved. <b>Gentle-to-medium, M1:</b> this makes rough/exposed water a real challenge
    /// (slows you, shoves you, demands steering) but does NOT capsize/swamp/kill — capsize/broach-to-
    /// swamping is an M2 escalation (logged, not built here).</para>
    /// </summary>
    public static class SeakeepingForcesMath
    {
        /// <summary>Below this squared magnitude a heading vector is undefined and falls back to +Y —
        /// the same defined-fallback convention as <see cref="BoatWaveMotionMath"/> / <c>WaveTrain</c>.</summary>
        public const float MinHeadingSqrMagnitude = 1e-12f;

        /// <summary>
        /// Exposure factor in [0, 1] from the water depth under the hull — PLACE, the owner's ratified
        /// second axis. Deep/offshore (depth ≥ full) → 1 (full open sea); shallow/near-shore
        /// (depth ≤ shelter) → 0 (the lee, sheltered); a smooth ramp between. Open water — no seabed map
        /// wired, depth <see cref="float.PositiveInfinity"/> from <c>BoatCrossing.DepthAt</c> — reads as
        /// fully exposed, so a normal open region feels the whole sea. A SIMPLE M1 model (a tunable
        /// falloff on one deterministic signal); a richer one (fetch, headland shadow) is a later item.
        /// Pure + static so it is EditMode-testable without a scene.
        /// </summary>
        public static float Exposure01(float waterDepthMeters, float shelterDepthMeters, float fullExposureDepthMeters)
        {
            // Guard a degenerate/inverted band: if the two depths cross, treat anything at/above the
            // shelter depth as fully exposed (never divide by zero, never invert the ramp).
            float lo = Mathf.Min(shelterDepthMeters, fullExposureDepthMeters);
            float hi = Mathf.Max(shelterDepthMeters, fullExposureDepthMeters);
            if (float.IsNaN(waterDepthMeters)) return 0f;
            if (hi - lo <= 1e-4f) return waterDepthMeters >= hi ? 1f : 0f;
            return Mathf.Clamp01((waterDepthMeters - lo) / (hi - lo));
        }

        /// <summary>
        /// SEE==FEEL (ADR 0023 phase 3 — owner ruling 2026-07-23, verbatim "Yes seas push should
        /// match"): the factor that makes the seakeeping FORCE read the sea the player SEES while
        /// the displaced surface is active — the surface's own published exaggeration × its shore
        /// fade at the depth under the hull (<see cref="ShoreFadeMath.Fade01"/>, the very factor
        /// the surface's vertex stage and the visual ride (<c>BoatWaveMotion</c>) multiply by).
        ///
        /// <para><b>Why scaling the resolved force IS reading the displaced height.</b>
        /// <see cref="Resolve"/> is LINEAR in the wave sample's amplitude: force and torque are
        /// both proportional to the sampled <see cref="WaveSample.Slope"/>, and the field's slope
        /// scales 1:1 with its height (∇(s·h) = s·∇h for the pointwise displaced factor s =
        /// exaggeration × fade). Multiplying the resolved force by this factor is therefore exactly
        /// resolving against the displaced field — the same wave sample × exaggeration × Fade01 —
        /// with no second field sample. (The fade's own spatial gradient is deliberately ignored,
        /// exactly as every visual consumer ignores it: the seam treats the fade as a pointwise
        /// factor, never a wave of its own.) Pinned by <c>SeeEqualsFeelForcesTests</c>.</para>
        ///
        /// <para><b>Contracts.</b> Displaced OFF (<paramref name="displacedActive"/> false) returns
        /// exactly 1 — the raw-sim force path is byte-identical to before (the A/B contract extends
        /// to physics). Open water (depth +∞, no seabed map) reads the full exaggeration; at/beyond
        /// the waterline (depth ≤ 0) and on a NaN depth it reads 0 (the fail-safe convention of
        /// <see cref="Exposure01"/>); a negative published exaggeration clamps to 0. Pure, static,
        /// allocation-free — the state is the ACTIVE surface's published truth (the Core
        /// <c>DisplacedSea</c> seam), never a per-consumer config read (the overlay-pose lesson).</para>
        /// </summary>
        /// <param name="displacedActive"><c>DisplacedSea.TryGet</c>'s result — false is the OFF contract.</param>
        /// <param name="waterDepthMeters">Local still-water depth under the hull (the same
        /// <c>BoatCrossing.DepthAt</c> read the exposure uses).</param>
        /// <param name="displaced">The active surface's published state (exaggeration + band).</param>
        public static float DisplacedForceScale(bool displacedActive, float waterDepthMeters,
                                                in DisplacedSeaState displaced)
        {
            if (!displacedActive) return 1f;
            if (float.IsNaN(waterDepthMeters)) return 0f;   // fail safe to no-force, like Exposure01
            return Mathf.Max(0f, displaced.Exaggeration)
                 * ShoreFadeMath.Fade01(waterDepthMeters, displaced.ShoreFadeBandMeters);
        }

        /// <summary>
        /// The per-hull response resolved from a hull's seakeeping data. Response falls with the hull's
        /// seakeeping mass factor (heavier = shrugs) and rises with its liveliness; damping is taken
        /// straight from the hull. Pure so a test can build a "dory" and a "trader" from plain numbers.
        /// </summary>
        /// <param name="seakeepingMassFactor">Hull mass factor (≥ ~0): higher = more inertia = the sea moves it less.</param>
        /// <param name="liveliness">How readily the hull corks about (≥ 0): a dory is high, a big hull low.</param>
        /// <param name="damping">Self-damping the hull applies to its wave-driven motion (≥ 0).</param>
        public static SeakeepingResponse ResponseFrom(float seakeepingMassFactor, float liveliness, float damping)
        {
            float mass = Mathf.Max(1e-3f, seakeepingMassFactor);
            float response = Mathf.Max(0f, liveliness) / mass;
            return new SeakeepingResponse(response, damping);
        }

        /// <summary>
        /// <b>THE SURF'S OWN BITE</b> (ADR 0040 PR 3) — the shove a bore gives a hull floating in it, and
        /// the slew the pocket gives her. Returned in the SAME <see cref="SeakeepingForce"/> the swell
        /// uses, so the controller adds one force and one torque: there is deliberately no second force
        /// path (the charter's own words).
        ///
        /// <para><b>The physical read.</b> Broken water is not a slope, it is a mass of aerated water
        /// moving shoreward. So the shove points along <see cref="SurfState.ShorewardDirection"/> — not
        /// down a gradient — and its size scales with what the bore is actually carrying:
        /// <see cref="SurfState.StandingHeightMeters"/> (γ·d, the depth-limited height, because a bore is
        /// only as tall as the water it runs over) times <see cref="SurfState.Whitewater01"/> (what is
        /// left of its energy). A hull sitting in dead foam at the top of the beach is barely pushed; one
        /// in the boil at the break line takes the lot.</para>
        ///
        /// <para><b>The broach.</b> A boat caught beam-on in breaking water gets thrown sideways and
        /// slewed — the classic broach. The torque keys on the BEAM component of the shove (a shove
        /// straight up the bow or dead astern does not turn you) and is boosted where the break is
        /// plunging, which is the water that actually does it. Sign follows the controller's existing
        /// rudder convention, exactly as <see cref="Resolve"/>'s does.</para>
        ///
        /// <para><b>⚠️⚠️ NOT scaled by <see cref="Exposure01"/>, and that is deliberate.</b> That
        /// exposure is a depth ramp that reads shallow water as sheltered — correct for swell, backwards
        /// for surf, and at the shipped tuning it is exactly 0 at the break depth. Routing the shove
        /// through it would multiply the whole feature away precisely where it acts. The gate is
        /// <see cref="SurfState.Breaking01"/> instead, which is a stricter guarantee of the same thing:
        /// zero in all calm water, all water too deep to break, and every sheltered corner where the
        /// waves never reach the criterion. Glass comes free — a glass sea has no contour.
        /// See <see cref="SurfState"/> for the full note.</para>
        ///
        /// <para><b>M1 law: gentle-to-medium never capsizes.</b> This adds planar force and yaw only —
        /// no roll, no swamping, no sinking. And it pushes rather than teleports: a hull's ~20 s time
        /// constant means she leans into the shove over seconds, which is what being carried by surf
        /// actually feels like.</para>
        ///
        /// <para>Pure, static, allocation-free, deterministic (rule 5). Returns
        /// <see cref="SeakeepingForce.None"/> when disabled, out of the surf, on glass, or for an inert
        /// hull — so every path that is not in broken water is byte-identical to before.</para>
        /// </summary>
        public static SeakeepingForce SurfShove(in SurfState surf, Vector2 heading,
                                                in SeakeepingResponse response,
                                                in SeakeepingSettings settings)
        {
            if (!settings.Enabled || !settings.SurfEnabled) return SeakeepingForce.None;
            if (!surf.IsWorking || response.Response <= 0f) return SeakeepingForce.None;

            float strength = Mathf.Max(0f, settings.SurfShoveStrength);
            if (strength <= 0f) return SeakeepingForce.None;

            // What the bore is carrying, times how much of it is left, times how readily this hull moves.
            float magnitude = strength
                            * surf.Breaking01
                            * surf.Whitewater01
                            * Mathf.Max(0f, surf.StandingHeightMeters)
                            * response.Response;
            if (magnitude <= 0f) return SeakeepingForce.None;

            Vector2 shove = surf.ShorewardDirection * magnitude;

            // The broach: only the BEAM component slews her, and the pocket is where it bites.
            float sqrMagnitude = heading.x * heading.x + heading.y * heading.y;
            Vector2 bow = sqrMagnitude < MinHeadingSqrMagnitude
                ? Vector2.up
                : heading * (1f / Mathf.Sqrt(sqrMagnitude));
            Vector2 starboard = new Vector2(bow.y, -bow.x);

            float shoveAlongBeam = shove.x * starboard.x + shove.y * starboard.y;
            float pocket = surf.Breaking01 * surf.Whitewater01;
            float torque = -shoveAlongBeam
                         * Mathf.Max(0f, settings.SurfBroachTorque)
                         * pocket
                         * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(surf.PlungingWeight01));

            return new SeakeepingForce(shove, torque);
        }

        /// <summary>
        /// Assemble the sea's force + yaw on the hull this tick — the heart of B3. <paramref name="wave"/>
        /// is <c>WaveMath.Sample</c> under the hull (its <c>Slope</c> and <c>Height</c> are read);
        /// <paramref name="heading"/> is the bow axis (<c>transform.up</c>, normalized here, +Y fallback);
        /// <paramref name="exposure01"/> is <see cref="Exposure01"/> (PLACE); <paramref name="response"/>
        /// is the per-hull character; <paramref name="settings"/> carries the master strength, the
        /// sea-state exponent (TIME), and the per-axis weights. Returns <see cref="SeakeepingForce.None"/>
        /// when disabled, on glass, in full shelter, or for an inert hull — so the OFF and calm/sheltered
        /// paths are exactly today's handling, by construction. Pure, static, allocation-free.
        /// </summary>
        /// <param name="seaState01">The continuous sea-state axis (TIME), clamped [0,1]. 0 = glass = no force.</param>
        public static SeakeepingForce Resolve(in WaveSample wave, Vector2 heading, float exposure01,
                                              float seaState01, in SeakeepingResponse response,
                                              in SeakeepingSettings settings)
        {
            if (!settings.Enabled) return SeakeepingForce.None;

            float strength = Mathf.Max(0f, settings.Strength);
            float exposure = Mathf.Clamp01(exposure01);
            float sea = Mathf.Clamp01(seaState01);
            if (strength <= 0f || exposure <= 0f || sea <= 0f || response.Response <= 0f)
                return SeakeepingForce.None;   // OFF / glass / full shelter / inert hull = today's handling

            // TIME bite: the force curve matches the field's own amplitude response so the shove grows
            // with the wave you can see. seaState01 = 0 → 0 exactly (glass never pushes — sacred).
            float seaBite = Mathf.Pow(sea, Mathf.Max(0.01f, settings.SeaStateExponent));

            float overall = strength * seaBite * exposure * response.Response;
            if (overall <= 0f) return SeakeepingForce.None;

            // Bow (heading) + starboard axes — the +Y / (y,−x) convention shared with BoatWaveMotionMath.
            float sqrMagnitude = heading.x * heading.x + heading.y * heading.y;
            Vector2 bow = sqrMagnitude < MinHeadingSqrMagnitude
                ? Vector2.up
                : heading * (1f / Mathf.Sqrt(sqrMagnitude));
            Vector2 starboard = new Vector2(bow.y, -bow.x);

            // A hull on a tilted patch of water is pushed DOWN the slope (toward the trough). Decompose
            // that base push onto the hull's own axes.
            float slopeAlongBow = wave.Slope.x * bow.x + wave.Slope.y * bow.y;          // + = surface rises AHEAD
            float slopeAlongBeam = wave.Slope.x * starboard.x + wave.Slope.y * starboard.y; // + = rises to STARBOARD

            // Along-bow: split head (surface rising ahead → boat pushed astern, retarding) vs following
            // (surface rising astern, slopeAlongBow < 0 → boat on the forward face, surged ahead). The
            // down-slope push is −slopeAlongBow either way; the WEIGHT differs by point of sail.
            float alongBowWeight = slopeAlongBow >= 0f
                ? Mathf.Max(0f, settings.HeadSeaWeight)       // head sea: costs headway
                : Mathf.Max(0f, settings.FollowingSeaWeight); // following sea: surges you along
            float bowForce = -slopeAlongBow * alongBowWeight;

            // Along-beam: the lateral shove — the dangerous point of sail.
            float beamForce = -slopeAlongBeam * Mathf.Max(0f, settings.BeamSeaWeight);

            Vector2 force = (bow * bowForce + starboard * beamForce) * overall;

            // Yaw: a beam/following slew tries to turn the bow (holding course now takes the helm). Sign
            // follows the controller's rudder/oar convention (positive-to-starboard slope → bow-right →
            // negative torque). The beam slope drives it; a following sea (slopeAlongBow < 0) adds a share
            // of the along-bow slope's asymmetry, the broach tendency.
            float slew = slopeAlongBeam;
            if (slopeAlongBow < 0f) slew += (-slopeAlongBow) * 0.5f; // following-sea broach share
            float torque = -slew * Mathf.Max(0f, settings.YawFromSlew) * overall;

            return new SeakeepingForce(force, torque);
        }
    }
}
