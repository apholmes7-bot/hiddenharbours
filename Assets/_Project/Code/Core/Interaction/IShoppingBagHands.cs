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

        /// <summary>His bag is set down somewhere with goods in it, and he has one bag at a time: he
        /// fetches it before he bags anything more.</summary>
        BagElsewhere = 4,
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

        /// <summary>His shopping bag lies set down in the world with goods in it.</summary>
        bool BagIsSetDown { get; }

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

    /// <summary>Raised when the shopping bag is set down with its goods inside: by his own press, or by
    /// the bed as he lies down to sleep.</summary>
    public readonly struct ShoppingBagSetDown
    {
        /// <summary>The region it was set down in ("" where none is known).</summary>
        public readonly string RegionId;

        /// <summary>Where it stands, world metres.</summary>
        public readonly UnityEngine.Vector2 Position;

        /// <summary>How many goods are in it.</summary>
        public readonly int Count;

        /// <summary>True when he lay down to sleep holding it and it was left by the bed.</summary>
        public readonly bool ByTheBed;

        public ShoppingBagSetDown(string regionId, UnityEngine.Vector2 position, int count, bool byTheBed)
        {
            RegionId = regionId ?? "";
            Position = position;
            Count = count;
            ByTheBed = byTheBed;
        }
    }

    /// <summary>Raised when he picks the set-down shopping bag up again.</summary>
    public readonly struct ShoppingBagPickedUp
    {
        /// <summary>How many goods are in it.</summary>
        public readonly int Count;

        public ShoppingBagPickedUp(int count) { Count = count; }
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
