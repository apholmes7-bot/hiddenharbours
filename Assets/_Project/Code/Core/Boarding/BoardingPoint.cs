using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What kind of way-aboard a <see cref="BoardingPoint"/> is. Append-only: the value is
    /// serialized into hull deck assets, so a kind is never renumbered or removed.</summary>
    public enum BoardingPointKind
    {
        /// <summary>A ladder that reaches the water (a stern or pilot ladder).</summary>
        Ladder = 0,
        /// <summary>A swim or boarding platform at the waterline.</summary>
        Platform = 1,
        /// <summary>An opening in the rail or bulwark you can step through.</summary>
        Gate = 2,
    }

    /// <summary>
    /// <b>One place on a hull where a person may climb out of the water, or leave over the side</b>
    /// (owner, 2026-11-13: <i>"should only be ladders, railings should stop players from jumping in"</i>).
    /// Authored per hull as data; <see cref="BoardingRuleMath"/> is the only reader of the rule.
    /// Not the wharf's fixed ladder: that is <see cref="IBoardingLadder"/> (world-placed, tide-aware).
    ///
    /// <para>The position is in the HULL's own deck frame, in metres — x abeam to starboard, y along the
    /// keel toward the bow — the same frame the walkable deck areas and <see cref="OverTheSideMath"/>
    /// use, so it does not change with her heading.</para>
    /// </summary>
    [Serializable]
    public struct BoardingPoint
    {
        [Tooltip("Stable name of this point on this hull (e.g. 'stern_ladder'). Append-only.")]
        public string Id;

        [Tooltip("Ladder, platform or gate. All three allow the climb and the exit; the kind is for art, " +
                 "prompts and later animation.")]
        public BoardingPointKind Kind;

        [Tooltip("Where it is on the hull, deck frame, metres: x abeam to starboard, y toward the bow.")]
        public Vector2 DeckLocalMeters;

        [Tooltip("How close (m) you must be to use it. 0 = use GameConfig.BoardingPointReachMetres.")]
        [Min(0f)] public float ReachMetres;
    }
}
