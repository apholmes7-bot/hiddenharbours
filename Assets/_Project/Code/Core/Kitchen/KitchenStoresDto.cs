using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One lot of food in a store: an item id, how many, and the freshness clock they share (the
    /// <see cref="HeldCatchDto"/> shape, keyed by item rather than species). Flat and JsonUtility-friendly.
    /// </summary>
    [Serializable]
    public struct KitchenStackDto
    {
        /// <summary>Stable item id (a food Def id, or a catch's species id).</summary>
        public string ItemId;

        /// <summary>How many share this exact freshness state (≥ 1).</summary>
        public int Count;

        /// <summary>Spoil banked as of <see cref="LastSettleGameSeconds"/> (FreshnessState.SpoilAccrued).</summary>
        public float SpoilAccrued;

        /// <summary>The clock instant the banked spoil is correct at.</summary>
        public double LastSettleGameSeconds;

        /// <summary>The <see cref="StorageMode"/> held since, as its stable int value.</summary>
        public int Mode;

        public KitchenStackDto(string itemId, int count, in FreshnessState freshness)
        {
            ItemId = itemId;
            Count = count;
            SpoilAccrued = freshness.SpoilAccrued;
            LastSettleGameSeconds = freshness.LastSettleGameSeconds;
            Mode = (int)freshness.Mode;
        }

        /// <summary>This lot's clock as the runtime <see cref="FreshnessState"/>.</summary>
        public FreshnessState Freshness => new FreshnessState(SpoilAccrued, LastSettleGameSeconds, (StorageMode)Mode);

        /// <summary>The same lot with another clock.</summary>
        public KitchenStackDto With(in FreshnessState freshness) => new KitchenStackDto(ItemId, Count, freshness);

        /// <summary>The same lot with another count.</summary>
        public KitchenStackDto WithCount(int count) => new KitchenStackDto(ItemId, count, Freshness);
    }

    /// <summary>
    /// <b>What is in one food store, as saved</b> (kitchens slice 1, save v21). Keyed by a stable storage
    /// KEY that says where it stands (for a boat: <c>fixture.boat.&lt;interior id&gt;.&lt;anchor id&gt;</c>;
    /// a building names its own), with the store's def id beside it. The contents are SAVED; the cold is
    /// not ticked but stamped: a cooler stores the instant its ice runs out, so the ice left at any moment
    /// is a pure function of that instant and the clock, and a sleep straight past it lands on the same
    /// answer as stepping (rule 5).
    /// </summary>
    [Serializable]
    public sealed class KitchenStorageStateDto
    {
        /// <summary>The marker for "no ice packed".</summary>
        public const double NoIce = -1d;

        /// <summary>Where this store stands, stable across saves.</summary>
        public string Key = "";

        /// <summary>The <see cref="KitchenStorageDef.Id"/> it is.</summary>
        public string StorageDefId = "";

        /// <summary>A cooler: the game-clock instant its ice runs out, or <see cref="NoIce"/>.</summary>
        public double IceRunsOutAtGameSeconds = NoIce;

        /// <summary>True once <see cref="KitchenIceRanOut"/> was raised for the current packing.</summary>
        public bool IceOutAnnounced;

        /// <summary>A fridge: whether it had power when last settled (<see cref="IKitchenPower"/>).</summary>
        public bool Powered = true;

        /// <summary>The food in it, oldest first.</summary>
        public List<KitchenStackDto> Stacks = new List<KitchenStackDto>();
    }
}
