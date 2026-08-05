using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Every constant of the STORM SEAKEEPING READ (ADR 0018 B2.5 — "the hull answers the sea it is
    /// actually in"), named and owner-tunable (rule 6). The owner's 2026-08-05 ask, decoded: the
    /// visual rock's amplitudes were one fixed tune — the baked/def rock cycle draws the SAME
    /// attitude in a chop and a gale, and the transform path's gains pin against caps sized for a
    /// fresh breeze — so a storm read no bigger than a lop. These knobs make the visual response
    /// GROW with the sea, and give the vertical ride WEIGHT (a spring-damper chase with a free-fall
    /// cap: the hull can never be dragged down faster than gravity).
    ///
    /// <para><b>Calm is sacred (the negative control).</b> At or below
    /// <see cref="StormStartSeaState01"/> the storm blend is exactly 0: every multiplier is exactly
    /// 1, every extra exactly 0, and the weight filter passes the ride through untouched — the
    /// owner's tuned calm/moderate feel is byte-identical with this block on. <see cref="Enabled"/>
    /// off restores today's behaviour at EVERY sea state (the A/B).</para>
    ///
    /// <para><b>Why it lives in Core (beside <see cref="SeakeepingSettings"/>).</b> It is the
    /// world-wide storm-response policy carried on the Core <c>GameConfig</c> the owner tunes; the
    /// consumer math (<c>StormRockMath</c>, Boats-side) takes it as a struct. Core cannot reference
    /// Boats (rule 4) — the exact split <see cref="SeakeepingSettings"/> /
    /// <c>SeakeepingForcesMath</c> already use. Per-hull character keeps riding the existing
    /// <c>BoatHullDef</c> seakeeping data (mass factor / liveliness / damping), never a second
    /// per-hull knob here.</para>
    /// </summary>
    [Serializable]
    public struct StormRockSettings
    {
        [Tooltip("Master switch (ADR 0018 B2.5). ON by default — the owner asked for a storm the hull answers. " +
                 "Calm/moderate water is UNCHANGED by construction (the blend below is exactly 0 there); " +
                 "turning this off restores today's visual rock at every sea state.")]
        public bool Enabled;

        [Header("The storm response curve (TIME — grows off the same SeaState01 axis as everything else)")]
        [Tooltip("Sea state (0..1, the continuous axis) at or below which the storm response is exactly zero — " +
                 "the owner's tuned cozy band, untouched. 0.4 sits just above Chop/Moderate (3/7 ≈ 0.43 is the " +
                 "Chop band edge); the dory's safe limit (Popple, 4/7 ≈ 0.57) is well inside the growth range.")]
        public float StormStartSeaState01;

        [Tooltip("Shape of the storm blend from StormStart to sea state 1: blend = t^exponent. 1 = linear; " +
                 ">1 keeps moderate seas gentle and saves the drama for a real blow (the same convention as " +
                 "the wave field's own amplitude exponent).")]
        public float ResponseExponent;

        [Tooltip("At full storm (sea state 1) the transform rock's gains AND caps — and a mesh hull's " +
                 "def rock amplitudes — are multiplied by this. 1 = no growth (today's fixed tune); 2.2 " +
                 "makes a gale visibly outrank a chop while staying inside the iso projection's honest range.")]
        public float MaxResponseMultiplier;

        [Header("Mesh storm attitude (the REAL front-to-back read — continuous hulls only)")]
        [Tooltip("Degrees of REAL pitch per unit of bow-axis wave slope, added on a mesh hull at full storm " +
                 "blend — the bow riding up the face and dropping into the trough, retargeting live as the " +
                 "player turns (a head sea pitches, a beam sea does not). The def's canned rock cycle stays " +
                 "underneath as the calm baseline.")]
        public float MeshStormPitchDegreesPerSlope;

        [Tooltip("Hard cap on the mesh storm pitch (degrees). Keeps the storm attitude inside the facet " +
                 "projection's readable envelope.")]
        public float MeshStormMaxPitchDegrees;

        [Tooltip("Degrees of REAL roll per unit of beam-axis wave slope, added on a mesh hull at full storm " +
                 "blend — the beam-sea lean the fixed def amplitudes cannot grow into.")]
        public float MeshStormRollDegreesPerSlope;

        [Tooltip("Hard cap on the mesh storm roll (degrees).")]
        public float MeshStormMaxRollDegrees;

        [Header("Smoothing (velvet is for calm — a storm is violent)")]
        [Tooltip("At full storm blend the motion output-smoothing time constant is multiplied by this " +
                 "(0..1). 0.4 turns a 0.2 s calm velvet into 0.08 s — still fps-independent and continuous, " +
                 "but the storm's snap is no longer laundered away. 1 = storm smoothing identical to calm.")]
        public float StormSmoothingFactor;

        [Header("Weight — the ride obeys gravity (the spring-damper chase under the displaced ride)")]
        [Tooltip("How much of the weight filter engages at full storm blend (0..1). 1 = the full weighted " +
                 "chase; 0 = the ride stays surface-bolted as today (weight off). Fades in with the storm " +
                 "blend, so calm water keeps the exact surface read.")]
        public float HeaveWeight01;

        [Tooltip("Stiffness of the ride's chase toward the surface (rad/s — roughly how fast the hull " +
                 "re-finds the water). ~7 tracks the dominant swell tightly but lets a sharpened crest " +
                 "drop away underneath for a beat — the unweight-and-land that reads as mass.")]
        public float HeaveChaseStiffnessRadPerSec;

        [Tooltip("Damping ratio of the chase (1 = critically damped: settles with no bounce; <1 rings, " +
                 ">1 wallows). The hull's own SeakeepingDamping is a FORCE-model drag and deliberately " +
                 "does not alias onto this.")]
        public float HeaveDampingRatio;

        [Tooltip("How the hull's seakeeping RESPONSE (BoatHullDef liveliness / mass factor) bends the chase " +
                 "stiffness: stiffness × response^exponent. A lively dory (response 1) chases at the base " +
                 "rate; a heavy trader (response < 1) re-finds the water more slowly — felt weight per hull " +
                 "from data that already exists. 0 = every hull chases alike.")]
        public float HullStiffnessResponseExponent;

        [Tooltip("Upper clamp on the per-hull response factor wherever the storm read scales by it (the " +
                 "attitude extras and the chase stiffness), so an extreme liveliness value cannot fling the " +
                 "visual.")]
        public float MaxHullResponseScale;

        [Tooltip("The FREE-FALL CAP, in g's: the ride's downward acceleration never exceeds gravity × this. " +
                 "1 = physical (crossing a steep crest the hull unweights, falls at g, catches up, lands — " +
                 "P5's tooth); bigger permits harder slams; the wave field's own Gravity constant supplies g.")]
        public float MaxDownwardAccelInGs;

        [Tooltip("Hard band (metres) the weighted ride may stray from the surface — the honesty bound: the " +
                 "hull never hovers above the sea nor submarines below it by more than this, whatever the " +
                 "spring does.")]
        public float SurfaceBandMeters;

        [Tooltip("Settle snap (metres): when the weighted ride is within this of the surface and nearly at " +
                 "rest it snaps EXACTLY onto it — no eternal micro-motion on a flattening sea. (The 'nearly " +
                 "at rest' velocity threshold is this × the chase stiffness — the spring's own velocity " +
                 "scale, not a second knob.)")]
        public float SettleEpsilonMeters;

        /// <summary>
        /// The reference tuning (B2.5 first feel — the owner dials from here). Storm response starts
        /// just above Chop (0.4), grows with a 1.6 curve to ×2.2 gains/caps at a full storm; the mesh
        /// storm attitude adds up to 10° of real pitch / 8° of real roll from the decomposed slope;
        /// smoothing tightens ×0.4; the weight filter fully engages by full storm with a ~7 rad/s
        /// critically-damped chase, a 1 g free-fall cap, a 0.8 m honesty band and a 4 mm settle snap.
        /// </summary>
        public static StormRockSettings Default => new StormRockSettings
        {
            Enabled = true,
            StormStartSeaState01 = 0.4f,
            ResponseExponent = 1.6f,
            MaxResponseMultiplier = 2.2f,
            MeshStormPitchDegreesPerSlope = 14f,
            MeshStormMaxPitchDegrees = 10f,
            MeshStormRollDegreesPerSlope = 9f,
            MeshStormMaxRollDegrees = 8f,
            StormSmoothingFactor = 0.4f,
            HeaveWeight01 = 1f,
            HeaveChaseStiffnessRadPerSec = 7f,
            HeaveDampingRatio = 1f,
            HullStiffnessResponseExponent = 0.5f,
            MaxHullResponseScale = 2f,
            MaxDownwardAccelInGs = 1f,
            SurfaceBandMeters = 0.8f,
            SettleEpsilonMeters = 0.004f,
        };
    }
}
