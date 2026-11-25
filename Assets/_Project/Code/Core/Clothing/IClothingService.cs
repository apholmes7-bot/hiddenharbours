using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>Player keys the clothing service hands wardrobes out under.</summary>
    public static class ClothingPlayers
    {
        /// <summary>The local player (the one the save, the needs and the dirt belong to).</summary>
        public const string Local = "player.local";
    }

    /// <summary>
    /// <b>Clothing, as a seam</b> (store clothing, pass 1, rule 4). The shipped garments, and one
    /// <see cref="PlayerWardrobe"/> per player. The needs and dirt services read the local player's
    /// wardrobe through this; the try-on page and the rack use it; none of them names another module.
    /// FLAG lead-architect: new Core contract (clothing).
    /// </summary>
    public interface IClothingService
    {
        /// <summary>Every shipped garment, ordinal by id. Empty when none ship.</summary>
        IReadOnlyList<ClothingDef> All { get; }

        /// <summary>The garment with <paramref name="clothingId"/>, or null.</summary>
        ClothingDef Find(string clothingId);

        /// <summary>The local player's wardrobe. Never null.</summary>
        PlayerWardrobe Local { get; }

        /// <summary>The wardrobe of <paramref name="playerKey"/>, made empty on first ask. Each key has its
        /// own; one player's clothes never move another's.</summary>
        PlayerWardrobe For(string playerKey);
    }
}
