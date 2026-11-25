using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where an island store's shelves can be asked for, by shop</b> (store sold-out slice). The Economy
    /// module registers each store's <see cref="IIslandShelves"/> here; a reader in another module (the
    /// sold-out cards on the shelves, a keeper's line) finds them by <c>shop.snake_case</c> id and never
    /// names the Economy class (rule 4). Cleared by <see cref="GameServices.Reset"/>.
    /// FLAG lead-architect: additive Core contract (island shelves lookup).
    /// </summary>
    public static class IslandShelvesDirectory
    {
        private static readonly Dictionary<string, IIslandShelves> _byShop =
            new Dictionary<string, IIslandShelves>(StringComparer.Ordinal);

        /// <summary>Registers (or replaces) the shelves of <see cref="IIslandShelvesView.ShopId"/>.</summary>
        public static void Register(IIslandShelves shelves)
        {
            if (shelves == null || string.IsNullOrEmpty(shelves.ShopId)) return;
            _byShop[shelves.ShopId] = shelves;
        }

        /// <summary>Removes these shelves, only if they are the ones registered for their shop.</summary>
        public static void Unregister(IIslandShelves shelves)
        {
            if (shelves == null || string.IsNullOrEmpty(shelves.ShopId)) return;
            if (_byShop.TryGetValue(shelves.ShopId, out IIslandShelves held) && ReferenceEquals(held, shelves))
                _byShop.Remove(shelves.ShopId);
        }

        /// <summary>The shelves of a shop, when the Economy module has registered them.</summary>
        public static bool TryGet(string shopId, out IIslandShelves shelves)
        {
            shelves = null;
            return !string.IsNullOrEmpty(shopId) && _byShop.TryGetValue(shopId, out shelves) && shelves != null;
        }

        /// <summary>How many stores are registered.</summary>
        public static int Count => _byShop.Count;

        public static void Clear() => _byShop.Clear();
    }

    /// <summary>
    /// A shelf line's item count changed: the run came, the islanders' buying thinned it, or the player
    /// bought off it. Published only when the whole-item count moves, so a reader can refresh on it alone.
    /// It neither charges money nor gives anything.
    /// </summary>
    public readonly struct IslandShelvesChanged
    {
        public readonly string ShopId;
        public readonly string LineId;
        public readonly int Count;

        public IslandShelvesChanged(string shopId, string lineId, int count)
        {
            ShopId = shopId; LineId = lineId; Count = count;
        }
    }
}
