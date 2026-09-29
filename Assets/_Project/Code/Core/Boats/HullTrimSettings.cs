using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>SHE TRIMS TO HER SPEED</b> — the world-wide policy of the hull trim. Owner, 2026-09-21,
    /// verbatim: <i>"Also i want trim added to the boats depending on speed and deacceleration"</i>.
    /// The bow lifts as she climbs her own bow wave, a planing hull settles once she is over it, and
    /// the nose dips briefly when she slows and her stern wave catches her up.
    ///
    /// <para><b>Why it lives in Core (beside <see cref="HullWeightSettings"/>).</b> Same split, same
    /// reason: this is policy carried on the <c>GameConfig</c> the owner tunes, and Core cannot
    /// reference Boats (rule 4). The consuming math is Boats-side (<c>HullTrimMath</c>), and the
    /// PER-HULL character — how far THIS bow rises, how hard it dips — lives on her own
    /// <c>BoatHullDef</c> (the <c>Trim*</c> fields). This block holds only what is true of every
    /// hull: where the hump sits on the Froude scale, and the limits and response a hull that
    /// authors none inherits.</para>
    ///
    /// <para><b><see cref="Enabled"/> off is the A/B.</b> Off, every hull's trim target is exactly 0
    /// and the drawn pitch is bit-identical to the pose before trim existed. A hull whose
    /// <c>Trim*</c> fields are all 0 is the same identity for that hull alone. A <c>GameConfig</c>
    /// serialized before this block existed deserializes it all-zero — i.e. OFF — so nothing trims
    /// until the block is authored.</para>
    ///
    /// <para><b>Nothing of trim is saved.</b> The target is recomputed every physics step from the
    /// hull's own speed through the water and her drive, and the drawn angle is a view-side lag of
    /// it; a load, a hull swap or a teleport starts her level.</para>
    /// </summary>
    [Serializable]
    public struct HullTrimSettings
    {
        [Tooltip("Master switch. ON = a hull whose BoatHullDef authors any Trim* value trims to her " +
                 "speed and her acceleration. OFF = no hull trims, and the drawn pitch is " +
                 "bit-identical to the pose before trim existed — one number reverses the whole " +
                 "change. A GameConfig serialized before this block existed reads OFF.")]
        public bool Enabled;

        [Header("Where the hump sits (Froude number Fn = speed / √(g · length))")]
        [Tooltip("The Froude number at which a hull reaches her full bow-up (BoatHullDef." +
                 "TrimHumpDegrees): the hump, where she is climbing her own bow wave. 0.40 is the " +
                 "textbook hull speed. The fleet's full-throttle speeds run from Fn 0.08 (the tanker) " +
                 "to 0.70 (the RIBs), so the working boats reach their hump at cruise and the ships " +
                 "never approach it. Also where the dip on a cut reaches full strength: a hull " +
                 "carrying less way than this dips proportionally less.")]
        [Min(0.001f)] public float HumpFroude;

        [Tooltip("The Froude number at which a planing hull has settled over her hump (BoatHullDef." +
                 "TrimPlaningDropDegrees has been taken off in full). Must sit above HumpFroude; " +
                 "it is clamped just above it if it does not. Only a hull that authors a planing drop " +
                 "reads it, so a displacement hull never planes.")]
        [Min(0.002f)] public float PlaningFroude;

        [Header("What a hull that authors none inherits (her 0 = these)")]
        [Tooltip("The most a bow may rise (degrees), for a hull whose TrimMaxBowUpDegrees is 0.")]
        [Min(0f)] public float DefaultMaxBowUpDegrees;

        [Tooltip("The most a bow may dip (degrees), for a hull whose TrimMaxBowDownDegrees is 0.")]
        [Min(0f)] public float DefaultMaxBowDownDegrees;

        [Tooltip("How long the drawn trim takes to follow its target (seconds, the time constant of " +
                 "one exponential lag), for a hull whose TrimResponseSeconds is 0. This lag is the " +
                 "whole filter: the target changes in steps when the throttle does, and she eases " +
                 "into each one instead of snapping. 0 = no lag at all.")]
        [Min(0f)] public float DefaultResponseSeconds;

        /// <summary>
        /// The shipped tuning: the hump at Fn 0.40 (the textbook hull speed) and the planing settle
        /// complete by Fn 0.55; a hull that authors no limits may rise 8° and dip 4°, and one that
        /// authors no response follows her target with a 0.6 s lag.
        /// </summary>
        public static HullTrimSettings Default => new HullTrimSettings
        {
            Enabled = true,
            HumpFroude = 0.40f,
            PlaningFroude = 0.55f,
            DefaultMaxBowUpDegrees = 8f,
            DefaultMaxBowDownDegrees = 4f,
            DefaultResponseSeconds = 0.6f,
        };
    }
}
