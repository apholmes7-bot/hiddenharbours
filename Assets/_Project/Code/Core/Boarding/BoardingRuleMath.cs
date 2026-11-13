using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What the boarding rule says about one spot on one hull.</summary>
    public enum BoardingAccess
    {
        /// <summary>This verb is not restricted on this hull: anywhere along her.</summary>
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
    /// <para><b>Two questions, two rules.</b> <b>Climbing ABOARD from the water</b> is restricted when
    /// the top of her side is over the climb-anywhere limit
    /// (<c>GameConfig.ClimbAnywhereMaxFreeboardMetres</c>), OR her rails block the side, OR her data
    /// says outright that she is climbed only at a boarding point (the owner's ruling of the same day
    /// on the working boats: <i>"Side dragger and stern trawler, cape islander and lobster boats should
    /// have stern or side ladders. They would definitely be too tall to climb"</i>). <b>Going OVER THE
    /// SIDE</b> is restricted by her rails alone: height stops a swimmer getting up, not a deckhand
    /// jumping down. A restricted verb happens only within reach of one of her
    /// <see cref="BoardingPoint"/>s; with no points it is refused, and she is boarded from a wharf or
    /// gangway. Every other hull — the dory, the punt, the skiffs — is climb-anywhere, as before.</para>
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
        /// <summary>Is this hull one a swimmer cannot simply climb anywhere? Railed, flagged
        /// ladder-only in her data, or too high. A freeboard exactly AT the limit is still
        /// climb-anywhere ("at or under").</summary>
        public static bool IsClimbRestricted(float freeboardMetres, bool railsBlockTheSide,
                                             bool climbAboardOnlyAtBoardingPoints,
                                             float climbAnywhereMaxFreeboardMetres)
            => railsBlockTheSide || climbAboardOnlyAtBoardingPoints
               || Safe(freeboardMetres) > Safe(climbAnywhereMaxFreeboardMetres);

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
        /// The climb ABOARD from the water, for one spot. <paramref name="deckLocal"/> is where the
        /// swimmer is beside her, in the hull's deck frame.
        /// </summary>
        public static BoardingAccess EvaluateClimb(float freeboardMetres, bool railsBlockTheSide,
                                                   bool climbAboardOnlyAtBoardingPoints,
                                                   BoardingPoint[] points, Vector2 deckLocal,
                                                   float climbAnywhereMaxFreeboardMetres,
                                                   float defaultReachMetres)
        {
            if (!IsClimbRestricted(freeboardMetres, railsBlockTheSide, climbAboardOnlyAtBoardingPoints,
                                   climbAnywhereMaxFreeboardMetres))
                return BoardingAccess.Anywhere;
            return AtHerPoints(points, deckLocal, defaultReachMetres);
        }

        /// <summary>
        /// Going OVER THE SIDE, for one spot. Only rails restrict it — a high unrailed working deck is
        /// still jumped from. <paramref name="deckLocal"/> is where the rider stands at her rail.
        /// </summary>
        public static BoardingAccess EvaluateOverTheSide(bool railsBlockTheSide, BoardingPoint[] points,
                                                         Vector2 deckLocal, float defaultReachMetres)
            => railsBlockTheSide ? AtHerPoints(points, deckLocal, defaultReachMetres) : BoardingAccess.Anywhere;

        private static BoardingAccess AtHerPoints(BoardingPoint[] points, Vector2 deckLocal,
                                                  float defaultReachMetres)
        {
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
