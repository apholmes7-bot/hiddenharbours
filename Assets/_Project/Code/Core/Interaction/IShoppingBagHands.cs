namespace HiddenHarbours.Core
{
    /// <summary>Why a good cannot go into the shopping bag. Append-only.</summary>
    public enum ShoppingBagRefusal
    {
        /// <summary>It can go in.</summary>
        None = 0,

        /// <summary>No bag in his hand, and no free hand to hold a new one.</summary>
        HandsFull = 1,

        /// <summary>The bag in his hand holds as many goods as it can.</summary>
        BagFull = 2,

        /// <summary>No game is loaded to keep the goods in.</summary>
        NoSave = 3,
    }

    /// <summary>
    /// <b>The paper shopping bag side of the player's hands</b> (buying at the shelf, slice 2). Published by
    /// the Player lane's <c>CarryHands</c> as <see cref="GameServices.BagHands"/>, so a store's shelf (World)
    /// can bag a good without referencing Player (rule 4). The bag is a real carried thing in one of his two
    /// hands; what is in it is <see cref="ShoppingBagContents"/>.
    /// </summary>
    public interface IShoppingBagHands
    {
        /// <summary>A shopping bag is in one of his hands.</summary>
        bool HoldsBag { get; }

        /// <summary>Whether one more good can go into a <paramref name="bag"/>, asked BEFORE money moves:
        /// the bag in his hand has room, or a hand is free for a new bag.</summary>
        ShoppingBagRefusal CanBag(ShoppingBagDef bag);

        /// <summary>Puts one <paramref name="supplyId"/> (already bought, so already in his supplies) into
        /// the bag in his hand, putting a new <paramref name="bag"/> in a free hand first if he holds none.
        /// Nothing changes on a refusal.</summary>
        ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId);

        /// <summary>Puts the shopping away into his storage <paramref name="storageId"/>: the goods can be
        /// used from now on and the bag leaves his hand. False, and nothing changes, with no bag.</summary>
        bool TryPutAway(string storageId);
    }

    /// <summary>Raised when he puts the shopping in his bag away into his own storage.</summary>
    public readonly struct ShoppingPutAway
    {
        /// <summary>The storage it went into (<see cref="StorageIds"/>).</summary>
        public readonly string StorageId;

        /// <summary>How many goods were in the bag.</summary>
        public readonly int Count;

        public ShoppingPutAway(string storageId, int count)
        {
            StorageId = storageId;
            Count = count;
        }
    }
}
