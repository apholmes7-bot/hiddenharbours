using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The food lookup</b>, a pure POCO over a set of <see cref="FoodDef"/>s: is this item food, and what
    /// food is it? No state, no randomness, no clock (rule 5).
    ///
    /// <para><b>Load order never matters.</b> A blank-id or duplicate-id Def is skipped; Defs are sorted
    /// ordinal by id, and when two name the same <see cref="FoodDef.ItemId"/> the lowest id wins. The
    /// content validation test fails any such asset loudly, so this only keeps a broken asset from making
    /// the answer depend on import order.</para>
    /// </summary>
    public sealed class FoodBook
    {
        /// <summary>The folder under any <c>Resources</c> root the shipped foods live in.</summary>
        public const string ResourcesFolder = "Food";

        private readonly List<FoodDef> _all = new List<FoodDef>();
        private readonly Dictionary<string, FoodDef> _byId = new Dictionary<string, FoodDef>(StringComparer.Ordinal);
        private readonly Dictionary<string, FoodDef> _byItem = new Dictionary<string, FoodDef>(StringComparer.Ordinal);

        public FoodBook(IEnumerable<FoodDef> foods)
        {
            if (foods != null)
                foreach (FoodDef def in foods)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id) || _byId.ContainsKey(def.Id)) continue;
                    _byId.Add(def.Id, def);
                    _all.Add(def);
                }
            _all.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (FoodDef def in _all)
                if (!string.IsNullOrEmpty(def.ItemId) && !_byItem.ContainsKey(def.ItemId))
                    _byItem.Add(def.ItemId, def);
        }

        /// <summary>Every shipped food under <c>Resources/Food/</c>.</summary>
        public static FoodBook FromResources() => new FoodBook(Resources.LoadAll<FoodDef>(ResourcesFolder));

        /// <summary>Every kept food, sorted ordinal by id.</summary>
        public IReadOnlyList<FoodDef> All => _all;

        /// <summary>The food with this <see cref="FoodDef.Id"/>, or null.</summary>
        public FoodDef Find(string id)
            => !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out FoodDef def) ? def : null;

        /// <summary>The food describing this item (<c>supply.bread</c>, <c>fish.atlantic_cod</c>), or null when
        /// the item is not food.</summary>
        public FoodDef FindByItem(string itemId)
            => !string.IsNullOrEmpty(itemId) && _byItem.TryGetValue(itemId, out FoodDef def) ? def : null;
    }
}
