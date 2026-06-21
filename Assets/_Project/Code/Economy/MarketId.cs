namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Which market a <see cref="Market"/> is — so it can read its own demand level D from
    /// <c>GameConfig</c> (the cove vs Port Greywick). Kept tiny and stable; new outlets append here.
    /// Default is <see cref="Cove"/>, so the home-wharf market needs no extra wiring.
    /// </summary>
    public enum MarketId
    {
        Cove = 0,     // the home wharf at Coddle Cove (neutral demand baseline)
        Greywick = 1, // Port Greywick — different demand, a reason to choose where to sell (VS-16)
    }
}
