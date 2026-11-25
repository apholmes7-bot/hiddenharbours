using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The kinds of shopping bag that ship</b>, by id: how a saved bag (<see cref="ShoppingBagDto.BagDefId"/>)
    /// finds its <see cref="ShoppingBagDef"/> again after the game is restarted, when no shelf is near to name
    /// it. One asset per kind under <c>Resources/ShoppingBags/</c>.
    /// </summary>
    public sealed class ShoppingBagBook
    {
        /// <summary>Where the bag kinds are loaded from (under a Resources folder).</summary>
        public const string ResourcesFolder = "ShoppingBags";

        private readonly Dictionary<string, ShoppingBagDef> _byId =
            new Dictionary<string, ShoppingBagDef>(StringComparer.Ordinal);

        public ShoppingBagBook(IEnumerable<ShoppingBagDef> bags)
        {
            if (bags == null) return;
            foreach (ShoppingBagDef def in bags)
            {
                if (def == null || string.IsNullOrEmpty(def.Id) || _byId.ContainsKey(def.Id)) continue;
                _byId.Add(def.Id, def);
            }
        }

        /// <summary>Every shipped kind under <c>Resources/ShoppingBags/</c>.</summary>
        public static ShoppingBagBook FromResources()
            => new ShoppingBagBook(Resources.LoadAll<ShoppingBagDef>(ResourcesFolder));

        /// <summary>How many kinds are known.</summary>
        public int Count => _byId.Count;

        /// <summary>The kind with this <see cref="ShoppingBagDef.Id"/>, or null.</summary>
        public ShoppingBagDef Find(string id)
            => !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out ShoppingBagDef def) ? def : null;
    }
}