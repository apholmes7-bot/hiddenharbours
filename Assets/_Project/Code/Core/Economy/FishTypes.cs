namespace HiddenHarbours.Core
{
    /// <summary>The eight catch categories (design/fish-and-content.md). Used for market pricing.</summary>
    public enum FishCategory
    {
        InshoreGroundfish = 0,
        Shellfish,
        Pelagic,
        Tidepool,
        Deepwater,
        Storm,
        Estuary,
        Legendary
    }

    /// <summary>Rarity tiers — drive spawn weighting and value bands (design/fish-and-content.md).</summary>
    public enum Rarity
    {
        Common = 0,
        Uncommon,
        Rare,
        Prize,
        Legendary
    }
}
