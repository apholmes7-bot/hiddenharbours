using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The default <see cref="IClothingService"/>: the garments under <c>Resources/Clothing/</c>, loaded once
    /// on first need, and a wardrobe per player key, made on first ask. Session-only (nothing is saved).
    /// </summary>
    public sealed class ClothingService : IClothingService
    {
        /// <summary>The folder under any <c>Resources</c> root the shipped garments live in.</summary>
        public const string ResourcesFolder = "Clothing";

        private readonly Func<ClothingDef[]> _catalogSource;
        private readonly Dictionary<string, PlayerWardrobe> _wardrobes =
            new Dictionary<string, PlayerWardrobe>(StringComparer.Ordinal);
        private List<ClothingDef> _all;

        public ClothingService(Func<ClothingDef[]> catalog = null)
        {
            _catalogSource = catalog ?? (() => Resources.LoadAll<ClothingDef>(ResourcesFolder));
        }

        public IReadOnlyList<ClothingDef> All => Catalog();

        public ClothingDef Find(string clothingId)
        {
            if (string.IsNullOrEmpty(clothingId)) return null;
            List<ClothingDef> all = Catalog();
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(all[i].Id, clothingId, StringComparison.Ordinal)) return all[i];
            return null;
        }

        public PlayerWardrobe Local => For(ClothingPlayers.Local);

        public PlayerWardrobe For(string playerKey)
        {
            playerKey ??= "";
            if (!_wardrobes.TryGetValue(playerKey, out PlayerWardrobe wardrobe))
            {
                wardrobe = new PlayerWardrobe(playerKey);
                _wardrobes.Add(playerKey, wardrobe);
            }
            return wardrobe;
        }

        private List<ClothingDef> Catalog()
        {
            if (_all != null) return _all;
            _all = new List<ClothingDef>();
            ClothingDef[] found = _catalogSource() ?? Array.Empty<ClothingDef>();
            foreach (ClothingDef def in found)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                bool repeat = false;
                for (int i = 0; i < _all.Count; i++)
                    if (string.Equals(_all[i].Id, def.Id, StringComparison.Ordinal)) { repeat = true; break; }
                if (!repeat) _all.Add(def);
            }
            _all.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return _all;
        }
    }
}
