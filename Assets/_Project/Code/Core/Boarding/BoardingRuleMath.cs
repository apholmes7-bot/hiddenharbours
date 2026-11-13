using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What the boarding rule says about one spot on one hull.</summary>
    public enum BoardingAccess
    {
        /// <summary>A low, unrailed hull: climb aboard or go over the side anywhere along her.</summary>
        Anywhere = 0,
        /// <summary>A restricted hull, and this spot is within reach of one of her boarding points.</summary>
        AtAPoint = 1,
        /// <summary>A restricted hull that HAS boarding points, but none within reach of this spot.</summary>
        FindAPoint = 2,
        /// <summary>A restricted hull with no boarding point at all: a wharf or gangway only.</summary>
        NoPoint = 3,
    }

    /// <summary>
    /// ⭐ <b>May a person climb this hull out of the water, or leave her over the side, HERE?</b>
    /// The pure rule behind the owner's 2026-11-13 playtest note: <i>"boarding the tanker player can
    /// easily climb up massive walls which is wrong, should only be ladders, railings should stop
    /// players from jumping in, maybe different on the fishing vessels"</i>.
    ///
    /// <para><b>The rule.</b> A hull is RESTRICTED when her freeboard is over the climb-anywhere limit
    /// (<c>GameConfig.ClimbAnywhereMaxFreeboardMetres</c>) OR her rails block the side. A restricted hull
    /// is climbed from the water, and left over the side, only within reach of one of her
    /// <see cref="BoardingPoint"/>s; with no points she refuses both, and is boarded from a wharf or
    /// gangway. Every other hull — the dory, the small fishing boats — is climb-anywhere, as before.</para>
    ///
    /// <para><b>Every input is data.</b> Freeboard, the rail flag and the points come from the hull's
    /// deck Def; the limit and the default reach from <c>GameConfig</c>. Nothing here names a hull.
    /// Positions are in the hull's deck frame (see <see cref="BoardingPoint"/>), so the answer does not
    /// depend on her heading. Pure and allocation-free: safe to ask every frame for a prompt.</para>
    ///
    /// <para><b>This rule is about the WATER side only.</b> Stepping between a deck and a wharf, a beach
    /// or a gangway is a different verb and is not asked here.</para>
    /// </summary>
    public static class BoardingRuleMath
    {
        /// <summary>Is this hull one you cannot simply climb anywhere? Railed, or too high. A freeboard
        /// exactly AT the limit is still climb-anywhere ("at or under").</summary>
        public static bool IsRestricted(float freeboardMetres, bool railsBlockTheSide,
                                        float climbAnywhereMaxFreeboardMetres)
            => railsBlockTheSide || Safe(freeboardMetres) > Safe(climbAnywhereMaxFreeboardMetres);

        /// <summary>The reach of one point: its own when authored (&gt; 0), else the config default.</summary>
        public static float ReachOf(in BoardingPoint point, float defaultReachMetres)
            => point.ReachMetres > 0f ? point.ReachMetres : Mathf.Max(0f, Safe(defaultReachMetres));

        /// <summary>Index of the NEAREST boarding point whose reach covers <paramref name="deckLocal"/>,
        /// or -1. Nearest rather than first so the answer does not depend on authoring order.</summary>
        public static int PointWithinReach(BoardingPoint[] points, Vector2 deckLocal, float defaultReachMetres)
        {
            if (points == null) return -1;
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                float reach = ReachOf(points[i], defaultReachMetres);
                float sqr = (points[i].DeckLocalMeters - deckLocal).sqrMagnitude;
                if (sqr <= reach * reach && sqr < bestSqr) { best = i; bestSqr = sqr; }
            }
            return best;
        }

        /// <summary>
        /// The whole rule for one spot. <paramref name="deckLocal"/> is where the person is in the hull's
        /// deck frame — the swimmer beside her, or the rider at her rail.
        /// </summary>
        public static BoardingAccess Evaluate(float freeboardMetres, bool railsBlockTheSide,
                                              BoardingPoint[] points, Vector2 deckLocal,
                                              float climbAnywhereMaxFreeboardMetres, float defaultReachMetres)
        {
            if (!IsRestricted(freeboardMetres, railsBlockTheSide, climbAnywhereMaxFreeboardMetres))
                return BoardingAccess.Anywhere;
            if (points == null || points.Length == 0) return BoardingAccess.NoPoint;
            return PointWithinReach(points, deckLocal, defaultReachMetres) >= 0
                ? BoardingAccess.AtAPoint : BoardingAccess.FindAPoint;
        }

        /// <summary>Does this verdict let the climb, or the exit over the side, go ahead?</summary>
        public static bool Allows(BoardingAccess access)
            => access == BoardingAccess.Anywhere || access == BoardingAccess.AtAPoint;

        private static float Safe(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
    }
}
