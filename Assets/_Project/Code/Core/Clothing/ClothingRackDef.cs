using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One garment on a rack and how many of it the rack holds.</summary>
    [Serializable]
    public struct ClothingRackLine
    {
        [Tooltip("The garment.")]
        public ClothingDef Clothing;

        [Tooltip("How many the rack holds. Each garment runs out on its own. PROPOSAL until the owner tunes it.")]
        [Min(0)] public int Stock;

        public ClothingRackLine(ClothingDef clothing, int stock)
        {
            Clothing = clothing;
            Stock = stock;
        }
    }

    /// <summary>
    /// <b>A clothes rack in a store</b> (store clothing, pass 1): which garments it offers and how many of
    /// each. Data only; the rack's place on the floor is the store scene's, and its live count is
    /// <see cref="ClothingRackStock"/>'s. Stable id <c>clothing_rack.snake_case</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Shops/Clothing Rack", fileName = "ClothingRack")]
    public sealed class ClothingRackDef : ScriptableObject
    {
        /// <summary>The id prefix every rack carries.</summary>
        public const string IdPrefix = "clothing_rack.";

        [Tooltip("Stable id, `clothing_rack.snake_case`. Append-only.")]
        public string Id = "clothing_rack.example";

        [Tooltip("The shop this rack stands in (`shop.snake_case`).")]
        public string ShopId = "";

        [Tooltip("The rack's name on the try-on page.")]
        public string DisplayName = "";

        [Tooltip("The garments, in the order the rack holds them. One line per garment.")]
        public ClothingRackLine[] Lines = Array.Empty<ClothingRackLine>();
    }
}
