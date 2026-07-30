namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Which market a <see cref="Market"/> is — so it can read its own demand level D from
    /// <c>GameConfig</c> (the cove vs Nine Mile Creek). Kept tiny and stable; new outlets append here.
    /// Default is <see cref="Cove"/>, so the home-wharf market needs no extra wiring.
    /// </summary>
    public enum MarketId
    {
        Cove = 0,     // the home wharf at Coddle Cove (neutral demand baseline)
        NineMileCreek = 1, // Nine Mile Creek — different demand, a reason to choose where to sell (VS-16)

        // The island general store at St Peters (plan-to-m1 §7.5). The FIRST market the player meets and
        // deliberately the WORST: it buys shellfish over the counter at a lower demand than either wharf,
        // so "where you sell matters" is taught in the first hour and the tide-gated crossing to Nine Mile
        // Creek has an economic reason on top of a story one. Its supply glut recovers separately, like
        // every other channel's.
        StPetersStore = 2,
    }
}
