using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>How asking for a given supply came out. Append-only.</summary>
    public enum SupplyGiftOutcome
    {
        /// <summary>Every item went into the paper bag in his hand.</summary>
        Given = 0,
        /// <summary>He already has it (or has used it). Nothing changes.</summary>
        AlreadyReceived = 1,
        /// <summary>No bag in his hand and no free hand for one. Nothing changes.</summary>
        HandsFull = 2,
        /// <summary>The bag in his hand has no room. Nothing changes.</summary>
        BagFull = 3,
        /// <summary>His bag is set down somewhere else. Nothing changes.</summary>
        BagElsewhere = 4,
        /// <summary>No hands to carry it, or no bag kind to carry it in. Nothing changes.</summary>
        NoBag = 5,
        /// <summary>No game is loaded. Nothing changes.</summary>
        NoGame = 6,
        /// <summary>This machine does not own the save. Nothing changes.</summary>
        NotHost = 7,
    }

    /// <summary>
    /// <b>Hand a given supply over, into the paper bag</b>: the same carry the shelf uses, so a gift and a
    /// purchase arrive the same way and the bag goes home to be put away like any shopping.
    ///
    /// <para><b>All or none.</b> The bag is asked first (<see cref="IShoppingBagHands.CanBag"/>); then each
    /// item is added to his supplies and bagged. If any item is refused part way (a two-item gift into a
    /// bag with one space left), the ones already bagged come out and every added item is spent again, so
    /// a refusal leaves the save exactly as it was. Only the host writes, once.</para>
    /// </summary>
    public static class SupplyGiftResponder
    {
        public static SupplyGiftOutcome TryGive(SupplyGiftDef gift, ISaveService save, IShoppingBagHands hands,
                                                ShoppingBagDef bag, IFlagStore flags, bool host)
        {
            SaveData data = save?.Current;
            if (gift == null || data == null) return SupplyGiftOutcome.NoGame;
            if (!host) return SupplyGiftOutcome.NotHost;
            if (gift.Received(data, flags)) return SupplyGiftOutcome.AlreadyReceived;
            if (hands == null || bag == null || gift.ItemIds == null || gift.ItemIds.Length == 0)
                return SupplyGiftOutcome.NoBag;

            SupplyGiftOutcome room = Map(hands.CanBag(bag));
            if (room != SupplyGiftOutcome.Given) return room;

            string[] ids = gift.ItemIds;
            using (save.DeferWrites())
            {
                for (int i = 0; i < ids.Length; i++) SupplyLocker.Add(data, ids[i], 1);
                for (int i = 0; i < ids.Length; i++)
                {
                    SupplyGiftOutcome put = Map(hands.TryBag(bag, ids[i]));
                    if (put == SupplyGiftOutcome.Given) continue;
                    Undo(data, ids, bagged: i);
                    return put;
                }
                save.Save();
            }
            return SupplyGiftOutcome.Given;
        }

        // Out of the bag what went in, then spend every added item: each one is put away by then, so the
        // spend takes exactly what was added.
        private static void Undo(SaveData data, string[] ids, int bagged)
        {
            for (int i = 0; i < bagged; i++) ShoppingBagContents.TryTakeOut(data, ids[i]);
            for (int i = 0; i < ids.Length; i++) SupplyLocker.TrySpend(data, ids[i]);
        }

        private static SupplyGiftOutcome Map(ShoppingBagRefusal refusal)
        {
            switch (refusal)
            {
                case ShoppingBagRefusal.None: return SupplyGiftOutcome.Given;
                case ShoppingBagRefusal.HandsFull: return SupplyGiftOutcome.HandsFull;
                case ShoppingBagRefusal.BagFull: return SupplyGiftOutcome.BagFull;
                case ShoppingBagRefusal.BagElsewhere: return SupplyGiftOutcome.BagElsewhere;
                case ShoppingBagRefusal.NoSave: return SupplyGiftOutcome.NoGame;
                default: return SupplyGiftOutcome.NoBag;
            }
        }
    }
}
