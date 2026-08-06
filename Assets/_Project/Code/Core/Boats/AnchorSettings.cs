using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Every constant of the <b>ground tackle</b> model — dropping the hook, holding on it, and losing it
    /// (P1 "the sea has moods" / P5 "cozy, but with teeth") — named and owner-tunable (rule 6): the
    /// fallback rode a hull carries when her Def does not say, the swing-circle floor, the firm-limit
    /// trio the rode is checked with, and how a DRAGGING anchor lets her go.
    ///
    /// <para><b>Why it lives in Core (the <see cref="SeakeepingSettings"/> split, verbatim).</b> This is
    /// world-wide anchoring <i>policy</i> carried on the Core <c>GameConfig</c> the owner tunes. Core
    /// cannot reference the Boats module (rule 4), so the shared tunable struct lives here; the maths
    /// that consumes it (<c>AnchorMath</c>) and the component that runs it (<c>BoatAnchor</c>) are
    /// Boats-side. The per-hull RODE LENGTH is <c>BoatHullDef.RodeMeters</c> data — bigger vessel,
    /// longer rode, deeper anchorages (P2).</para>
    ///
    /// <para><b>⚠ The GameConfig YAML trap (#420).</b> This is a STRUCT that <c>GameConfig.asset</c>
    /// serializes field by field, and a field the YAML omits deserializes to its <b>C# default</b> — 0
    /// for a float — <i>not</i> to <see cref="Default"/>. A zero <see cref="DefaultRodeMeters"/> would
    /// mean every un-authored hull carries no rode and can never anchor anywhere, which reads exactly
    /// like a feature that did not ship. The asset must carry every key below, and
    /// <c>AnchorConfigTests</c> is the guard.</para>
    /// </summary>
    [Serializable]
    public struct AnchorSettings
    {
        [Header("The rode (how much line she carries)")]
        [Tooltip("Rode length (m) for a hull whose BoatHullDef leaves RodeMeters at 0 — the DINGHY-CLASS " +
                 "SHORT rode. Because a hull asset written before that field existed deserializes it as " +
                 "0, this is what every untouched hull carries: short enough that deep water genuinely " +
                 "finds no bottom, long enough to anchor anywhere a small boat has business being.")]
        [Min(0.1f)] public float DefaultRodeMeters;

        [Header("The swing circle")]
        [Tooltip("Floor on the swing radius (m). The swing circle is sqrt(rode² − depth²), so a rode that " +
                 "only just reaches bottom gives a circle of ~zero and the boat would be welded in place. " +
                 "This lets her always fidget a little on her hook. Small by design.")]
        [Min(0f)] public float MinSwingMeters;

        [Header("The firm limit — a rode is a rope, not a rubber band")]
        [Tooltip("How much the rode may over-stretch past the swing circle (m) before the firm limit " +
                 "checks her. SMALL by design; 0 = perfectly inextensible. Handed straight to the mooring " +
                 "line's restraint maths (BoatMooring.TetherForce / ConstrainToRope) — one mechanism, two " +
                 "consumers — so an anchored boat is brought up exactly the way a tied one is.")]
        [Min(0f)] public float RodeGiveMeters;

        [Tooltip("How firmly the taut rode checks the boat at the end of her swing (design-unit force per " +
                 "metre past the allowed give). HIGH by design so the stop is near-rigid: she comes up on " +
                 "her hook, she is not pulled back softly.")]
        [Min(0f)] public float LimitStiffness;

        [Tooltip("Damping on the rode at the limit (design-unit force per m/s of OUTWARD speed), so a boat " +
                 "surging to the end of her scope is arrested cleanly instead of bouncing. Only ever brakes " +
                 "outward motion — a rode cannot shove the boat off her anchor.")]
        [Min(0f)] public float LimitDamping;

        [Header("Dragging (the rising-tide failure — P5 teeth)")]
        [Tooltip("⚠ _confirm (owner): the speed (m/s) a DRAGGING anchor lets her make. When the tide rises " +
                 "past the rode the hook loses the bottom and skips along it: she is no longer held, but " +
                 "the tackle still checks her, so she creeps off downwind/downtide at about this instead of " +
                 "sailing away. It is a settling speed, not a hard cap — a real blow will push her a little " +
                 "past it. Raise it for a nastier drag, lower it for a gentler one; 0 makes a dragging " +
                 "anchor behave as a full brake.")]
        [Min(0f)] public float DragCreepMetersPerSec;

        [Tooltip("How hard the dragging tackle checks speed ABOVE the creep (design-unit force per m/s of " +
                 "excess). Higher = she settles onto the creep speed faster; 0 = the dragging anchor does " +
                 "nothing at all and she drifts free.")]
        [Min(0f)] public float DragBrakeStrength;

        /// <summary>
        /// The reference tuning. A 6 m dinghy-class rode: enough for St Peters' channel and the bar at any
        /// state of tide, and deliberately NOT enough for the deep harbour at high water (that basin runs
        /// to ~6.2 m over its −4 m floor on a spring high), so "she'll find no bottom out there" and the
        /// rising-tide drag are both reachable in the opening region rather than theoretical. The
        /// firm-limit trio is the mooring rope's own shipped tuning (give 0.15 m, stiffness 1200, damping
        /// 120) — the same rope maths deserves the same rope feel. A dragging anchor lets her make about a
        /// third of a metre a second: clearly moving, clearly not free.
        /// </summary>
        public static AnchorSettings Default => new AnchorSettings
        {
            DefaultRodeMeters = 6f,
            MinSwingMeters = 0.75f,
            RodeGiveMeters = 0.15f,
            LimitStiffness = 1200f,
            LimitDamping = 120f,
            DragCreepMetersPerSec = 0.35f,
            DragBrakeStrength = 900f,
        };
    }
}
