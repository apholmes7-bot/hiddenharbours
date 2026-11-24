using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One thing the player took off a store shelf, as saved</b> (save v26): one row per purchase instant
    /// per shelf line. The shelves themselves are never saved; they are rebuilt from the calendar and the
    /// seed, and these rows are applied on top in one fixed order (day, seconds, shop, line), so the same save
    /// always gives the same shelves (rule 5). What a villager bought, what a delivery brought and what a
    /// shelf holds are recomputed and have no row here.
    ///
    /// <para><b>Data only at v26.</b> The row and its list exist; nothing reads or writes them yet. Flat and
    /// JsonUtility-friendly.</para>
    /// </summary>
    [Serializable]
    public sealed class StorePurchaseRowDto
    {
        /// <summary>The shop's stable id (<c>shop.*</c>).</summary>
        public string ShopId = "";

        /// <summary>The shelf line's stable id inside that shop (<c>line.*</c>).</summary>
        public string ShelfLineId = "";

        /// <summary>How much was taken, in the good's own units (kilograms or litres), as it was at the moment
        /// of purchase. A double because the shelf's own running stock is one: the row replays exactly, and a
        /// retuned item size cannot change what an old purchase took.</summary>
        public double Units;

        /// <summary>The game day of the purchase (<c>IGameClock.DayIndex</c>).</summary>
        public int Day;

        /// <summary>The game clock (<c>IGameClock.TotalSeconds</c>) at the purchase, at full precision.</summary>
        public double Seconds;
    }

    /// <summary>
    /// <b>One special order the player has paid for, as saved</b> (save v26): goods ordered at a counter, paid
    /// on order, that arrive on a later supply run and are then collected. One row per order.
    ///
    /// <para><b>Reserved, not yet used.</b> The row, its list and the order counter
    /// (<see cref="SaveData.NextSpecialOrderNumber"/>) exist so the feature needs no further schema change;
    /// nothing reads or writes them yet.</para>
    ///
    /// <para><b>A paid order is never thrown away by a load.</b> The load repair has no content to look a
    /// listing up in and no purse to refund to, so a row that carries money or goods is always kept, however
    /// damaged the rest of it is. Deciding what a damaged order becomes (a refund, a repair) is the feature's
    /// own start-up work.</para>
    /// </summary>
    [Serializable]
    public sealed class SpecialOrderDto
    {
        /// <summary><see cref="State"/>: paid for, not yet arrived.</summary>
        public const int StateWaiting = 0;

        /// <summary><see cref="State"/>: arrived, waiting to be collected.</summary>
        public const int StateArrived = 1;

        /// <summary><see cref="State"/>: collected. The feature drops such a row when it next writes.</summary>
        public const int StateCollected = 2;

        /// <summary>The order's stable id, <c>order.&lt;seller&gt;.&lt;n&gt;</c>, where n comes from
        /// <see cref="SaveData.NextSpecialOrderNumber"/> and is never used twice.</summary>
        public string OrderId = "";

        /// <summary>Whose counter took the order (a stable seller id).</summary>
        public string SellerId = "";

        /// <summary>Where the parcel is collected (a stable id). <b>Empty means the seller's own counter</b>,
        /// which is the only place an order comes to today.</summary>
        public string DeliverToId = "";

        /// <summary>What was ordered: one line per kind of good.</summary>
        public List<SpecialOrderLineDto> Lines = new List<SpecialOrderLineDto>();

        /// <summary>What left the player's purse for the whole order, kept so a refund is exact.</summary>
        public int PaidPrice;

        /// <summary>The game clock (<c>IGameClock.TotalSeconds</c>) when he paid.</summary>
        public double OrderedAtSeconds;

        /// <summary>The game day of the supply run it comes on, fixed when he paid.</summary>
        public int DueDay;

        /// <summary>Where the order stands: <see cref="StateWaiting"/>, <see cref="StateArrived"/> or
        /// <see cref="StateCollected"/>. An int, so a later step needs no new field.</summary>
        public int State;
    }

    /// <summary>
    /// <b>One kind of good on a special order</b> (save v26, reserved): a catalogue listing and how many.
    /// Ids only, so a retuned listing follows into every save.
    /// </summary>
    [Serializable]
    public sealed class SpecialOrderLineDto
    {
        /// <summary>The catalogue listing's stable id.</summary>
        public string ListingId = "";

        /// <summary>How many (at least 1 on a kept line).</summary>
        public int Count = 1;
    }

    /// <summary>
    /// <b>A shopping bag that holds goods, as saved</b> (save v26): in the player's hand or set down
    /// somewhere. One row per bag.
    ///
    /// <para><b>The goods in a bag stay counted in his supplies</b> (<see cref="SaveData.SupplyStock"/>:
    /// bought means owned at once). <see cref="Items"/> only says which of them are set aside in the bag and
    /// cannot be used until it is put away. So a damaged or dropped bag row can never lose goods: the worst
    /// case is that they are found put away.</para>
    ///
    /// <para><b>Data only at v26.</b> Nothing reads or writes the list yet. Flat and JsonUtility-friendly.</para>
    /// </summary>
    [Serializable]
    public sealed class ShoppingBagDto
    {
        /// <summary>The bag kind's stable Def id (<c>shoppingbag.*</c>).</summary>
        public string BagDefId = "";

        /// <summary>True: in his hand when the game was saved (the place fields below then mean nothing).
        /// False: set down.</summary>
        public bool Held;

        /// <summary>The region it stands in (the <see cref="PlacedTrapDto.Region"/> precedent), so a bag left
        /// in an unloaded region is still recorded and rebuilt when that region loads.</summary>
        public string Region = "";

        /// <summary>World X of where it stands, metres. Flat floats on the <see cref="PlacedTrapDto.PosX"/>
        /// precedent (the save stores no vectors).</summary>
        public float PosX;

        /// <summary>World Y of where it stands (see <see cref="PosX"/>).</summary>
        public float PosY;

        /// <summary>The storey it stands on: 0 the ground and everywhere outdoors, 1 the floor above, the
        /// same number as <see cref="SaveData.RestLevel"/>.</summary>
        public int Level;

        /// <summary>Empty ashore. <b>Reserved</b>: the hull a bag was set down aboard, its position then
        /// local to that hull.</summary>
        public string HullId = "";

        /// <summary>True when it was left by the bed as he lay down, rather than set down by his own press.</summary>
        public bool ByTheBed;

        /// <summary>The goods set aside in it, one row per kind.</summary>
        public List<BagItemDto> Items = new List<BagItemDto>();
    }

    /// <summary>
    /// <b>One kind of good in a shopping bag</b> (save v26): a supply id and how many of the player's own
    /// stock of it are in the bag. Never more than he owns.
    /// </summary>
    [Serializable]
    public sealed class BagItemDto
    {
        /// <summary>The good's stable supply id (<c>supply.*</c>), the <see cref="SupplyStock.SupplyId"/> key.</summary>
        public string SupplyId = "";

        /// <summary>How many are in the bag (at least 1 on a kept row).</summary>
        public int Count;
    }
}
