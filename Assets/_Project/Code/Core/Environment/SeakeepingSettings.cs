using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Every constant of the seakeeping FORCE model (ADR 0018 B3 — the sea pushes the boat), named and
    /// owner-tunable (rule 6): the master switch + strength, the two-axis modulation (sea-state already
    /// lives in the wave field; PLACE is the exposure falloff here), and how the wave shove splits
    /// across head / beam / following seas. Serializable so <c>GameConfig</c> surfaces it for the owner;
    /// <see cref="Default"/> is the reference "first feel" tuning the owner dials from.
    ///
    /// <para><b>Why it lives in Core (beside <see cref="WaveFieldSettings"/>).</b> It is the world-wide
    /// seakeeping <i>policy</i> — is it on, how hard does it bite, how does exposure fall off, how much
    /// does a head vs beam vs following sea matter — carried on the Core <c>GameConfig</c> the owner
    /// tunes. Core cannot reference the Boats module (rule 4), so the shared tunable struct lives here;
    /// the FORCE math that consumes it (<c>SeakeepingForcesMath</c>) is Boats-side. Exactly the split
    /// already used for <see cref="WaveFieldSettings"/> (Core) vs its <c>BoatWaveMotion</c> consumer
    /// (Boats). The per-hull RESPONSE is <c>BoatHullDef</c> data.</para>
    /// </summary>
    [Serializable]
    public struct SeakeepingSettings
    {
        [Tooltip("Master switch (ADR 0018 B3). ON by default — the owner wants to feel the sea fight back. " +
                 "Because every force scales by SeaState01 × exposure, calm sheltered handling is UNCHANGED " +
                 "by construction even with this on; turning it off restores today's flat-water feel exactly.")]
        public bool Enabled;

        [Tooltip("Overall bite of the sea (design-unit force multiplier). Moderate by default — a FIRST feel " +
                 "version for the owner to tune. Bigger = the sea shoves harder in rough/exposed water; 0 = no " +
                 "environmental force (same as Enabled = false). This is the main dial.")]
        public float Strength;

        [Tooltip("Response curve of the bite to SeaState01: biteScale = SeaState01^exponent. 1 = linear; >1 " +
                 "keeps gentle seas gentle and saves the teeth for a real blow (matches the field's own " +
                 "amplitude exponent so force and visible wave grow together). At SeaState01 = 0 this is 0 — " +
                 "glass never pushes.")]
        public float SeaStateExponent;

        [Header("Exposure — PLACE (open water bites, the lee of land is sheltered)")]
        [Tooltip("Water depth (m) at/above which the hull feels the FULL open sea (exposure = 1). Deeper/further " +
                 "offshore = more exposed. A SIMPLE M1 signal — the same water depth the boat-cross gate reads " +
                 "(seabed + tide). A richer exposure model (fetch, headland shadow) is a later refinement.")]
        public float FullExposureDepthMeters;

        [Tooltip("Water depth (m) at/below which the hull is fully SHELTERED (exposure = 0) — the shallow, " +
                 "near-shore lee where the sea can't build. Between this and full-exposure depth, exposure " +
                 "ramps smoothly. Must be < FullExposureDepthMeters. Open water (no seabed map wired) reads as " +
                 "fully exposed by construction.")]
        public float ShelterDepthMeters;

        [Header("Per-axis weights (how much each point of sail matters)")]
        [Tooltip("HEAD SEA weight: punching bow-first into a rising face costs headway + a pitching shove. " +
                 "Scales the along-bow (into-the-sea) component of the wave force.")]
        public float HeadSeaWeight;

        [Tooltip("BEAM SEA weight: a wave on the side shoves the hull sideways AND yaws it — the dangerous " +
                 "point of sail that demands active steering. Scales the along-beam force component.")]
        public float BeamSeaWeight;

        [Tooltip("FOLLOWING SEA weight: a sea from astern surges you along and can slew (broach) the stern. " +
                 "Scales the along-bow component when the sea is OVERTAKING (pushing the boat forward).")]
        public float FollowingSeaWeight;

        [Tooltip("Yaw torque per unit of beam/following slew (design-unit torque multiplier) — how hard a " +
                 "beam or following sea tries to turn the bow, so holding course demands the helm. The " +
                 "'demands active steering' teeth; 0 = pure translation, no wave-driven yaw.")]
        public float YawFromSlew;

        [Header("BROKEN WATER (ADR 0040) — the surf shoves, and the pocket slews")]
        [Tooltip("Master switch for the surf's own forces. ON by default — a bore running up a beach is " +
                 "the most physical thing the sea does. Off restores the swell-only push exactly.\n\n" +
                 "⚠️ This rides the SAME force channel as the swell above: one Resolve, one AddForce. " +
                 "There is deliberately no second force path.")]
        public bool SurfEnabled;

        [Tooltip("How hard the broken water shoves a hull SHOREWARD (design-unit force multiplier). The " +
                 "shove scales with the bore's standing height (gamma x depth) and its remaining energy, " +
                 "so it is strongest in the boil at the break line and dies as the bore runs in.\n\n" +
                 "⚠️ It is gated by Breaking01, NOT by the depth-ramp exposure the swell uses — surf " +
                 "exists only in shallow water, which that ramp calls 'sheltered'. See SurfState.")]
        public float SurfShoveStrength;

        [Tooltip("How hard the POCKET slews the bow (design-unit torque multiplier) — the broach. Keyed " +
                 "on the beam component of the shove and boosted where the break is plunging, because " +
                 "that is the water that throws a boat sideways. 0 = the surf pushes but never turns you.")]
        public float SurfBroachTorque;

        [Tooltip("How much of the shove PULSES with the bore (ADR 0040 revision 3), 0..1. " +
                 "0 = the steady lean the surf gave before the bore existed — a hull in the boil is " +
                 "pushed evenly, second after second. 1 = the whole shove rides the bore's own clock: a " +
                 "front arrives, peaks and passes, the water lets go between crests, and the drift up " +
                 "the beach comes in STEPS one wave period apart. " +
                 "⚠️ A pulse REDISTRIBUTES the shove, it does not add to it — the pulse averages well " +
                 "under 1 over a period, so at 1 the same SurfShoveStrength hits harder at each front " +
                 "and carries her less far overall. Raise SurfShoveStrength if you want the old reach " +
                 "back with the beat on top.")]
        [Range(0f, 1f)] public float SurfBorePulse01;

        [Tooltip("How far a hull is LIFTED as a bore front passes under her: metres of ride per metre of " +
                 "the bore's run-up level. 0 = no lift at all (the ride exactly as it was before the " +
                 "bore); 1 = she rises by the level the wash actually reached, which is already capped " +
                 "at the drawn-edge ceiling (Breakers ▸ RunUpCapMeters). " +
                 "This one is PRESENTATION — there is no vertical axis in a 2D sim to push along, so it " +
                 "rides the displaced sea's existing ride channel (the one her passengers stand on) and " +
                 "is 0 whenever the displaced sea is off, exactly like every other ride.")]
        [Range(0f, 2f)] public float SurfLiftScale;

        /// <summary>
        /// The reference "first feel" tuning (ADR 0018 B3 — moderate, gentle-to-medium, never capsizing in
        /// M1). Enabled; a moderate strength; a 1.35 sea-state exponent matching the field so the shove grows
        /// with the visible wave; exposure ramps from fully sheltered at 1 m depth to full open sea at 6 m
        /// (St Peters' channel is a few metres — the offshore reach is where it bites); a beam sea weighted
        /// hardest (the dangerous point of sail), a head sea next, a following sea gentler; a moderate
        /// wave-driven yaw so a beam sea makes you work the helm.
        /// </summary>
        public static SeakeepingSettings Default => new SeakeepingSettings
        {
            Enabled = true,
            Strength = 220f,
            SeaStateExponent = 1.35f,
            FullExposureDepthMeters = 6f,
            ShelterDepthMeters = 1f,
            HeadSeaWeight = 1f,
            BeamSeaWeight = 1.3f,
            FollowingSeaWeight = 0.7f,
            YawFromSlew = 0.9f,

            // The surf's own bite (ADR 0040 PR 3) — a FIRST feel version, like the swell's was. The
            // shove is a little stronger than the swell's master because broken water is more violent
            // than a slope, and the broach is moderate: enough that being caught beam-on in the boil
            // costs you the helm, not so much that it spins you.
            SurfEnabled = true,
            SurfShoveStrength = 260f,
            SurfBroachTorque = 1.2f,
            // …and the bore's own beat on top of it (revision 3). The pulse ships FULL: a bore is an
            // event, and a steady lean is the thing the owner said the surf was missing. The lift ships
            // at 1 = "she rises by the level the wash reached", which the run-up cap already bounds.
            SurfBorePulse01 = 1f,
            SurfLiftScale = 1f,
        };
    }
}
