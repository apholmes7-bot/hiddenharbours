using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What a food store does to what is kept in it. Append-only: the int is saved.</summary>
    public enum KitchenStorageKind
    {
        /// <summary>Dry goods on a shelf or in a locker. Changes nothing about how food keeps: what spoils
        /// in the open spoils here too, and dry goods (no spoil rate) never do.</summary>
        Pantry = 0,

        /// <summary>An insulated box that keeps food cold only while it holds ICE. The ice melts on the game
        /// clock at the def's rate; with none left it is just a box (owner 2026-11-15: "cooler which needs
        /// ice").</summary>
        Cooler = 1,

        /// <summary>Keeps food cold while the place it stands in has power (<see cref="IKitchenPower"/>).
        /// Without power it is just a box.</summary>
        Fridge = 2,
    }

    /// <summary>
    /// <b>One kind of food store, as data</b> (kitchens slice 1, owner ask 2026-11-15: "food storage, a
    /// fridge, cooler which needs ice, and pantry"). One asset per kind under
    /// <c>Data/Resources/Kitchen/Storage/</c>, found by <see cref="KitchenStorageCatalog"/>.
    ///
    /// <para><b>Location-agnostic.</b> A kitchen piece is a kind, a capacity and an anchor, wherever it
    /// stands: a boat's interior anchor names one of these by id today, and a home, a staffed shop or a
    /// player-built building names the same ids tomorrow. Nothing here knows what a boat is.</para>
    ///
    /// <para><b>Cold is the existing freshness maths.</b> While a store is cold its contents are held in
    /// <see cref="ColdMode"/> (<see cref="StorageMode.Iced"/> by default, so the owner's
    /// <c>GameConfig.Freshness.IcedRateMultiplier</c> decides how much a fridge or a cooler slows rot);
    /// while it is not, they are <see cref="StorageMode.Ambient"/>. A cooler's ice running out partway
    /// through a span uses <see cref="Freshness.SettleThroughProtection"/>, so a sleep straight through the
    /// melt lands on the number an unbroken session would (rule 5).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Kitchen Storage", fileName = "KitchenStorage")]
    public sealed class KitchenStorageDef : ScriptableObject
    {
        /// <summary>The ice a cooler takes when its def names none.</summary>
        public const string DefaultIceSupplyId = "supply.ice";

        [Header("Identity")]
        [Tooltip("Stable id, append-only: kitchen_storage.<snake_case>, e.g. kitchen_storage.cooler.")]
        public string Id = "";

        [Tooltip("What the player would call it ('Cooler', 'Pantry locker').")]
        public string DisplayName = "";

        [Header("What it does")]
        public KitchenStorageKind Kind = KitchenStorageKind.Pantry;

        [Tooltip("How many food items it holds in all (one item = one count of any food). Owner's to tune.")]
        [Min(1)] public int CapacityItems = 8;

        [Tooltip("How food is held while this store is COLD (a cooler with ice, a fridge with power). Iced " +
                 "by default: a fridge and an iced cooler keep food at the same chill, and " +
                 "GameConfig.Freshness.IcedRateMultiplier is the dial for how much that slows rot. Ignored " +
                 "by a pantry, which is never cold.")]
        public StorageMode ColdMode = StorageMode.Iced;

        [Header("Cooler only")]
        [Tooltip("The SupplyDef id a cooler is packed with, spent from the supply locker one bag at a time.")]
        public string IceSupplyId = DefaultIceSupplyId;

        [Tooltip("How many bags of ice it can hold at once.")]
        [Min(1)] public int IceCapacityUnits = 2;

        [Tooltip("How many IN-GAME hours one bag of ice keeps it cold. In-game hours, so the day length never " +
                 "changes it. An insulated cooler is a lidded tote: 12 h, twice an open tote's 6 h.")]
        [Min(0.1f)] public float IceHoursPerUnit = 12f;

        [Header("Who may use it")]
        [Tooltip("The home whose deed opens this store (home.ginny_lot_camper for the camper's freezer, fridge " +
                 "and pantry). Empty = open to the player with no deed: every galley store aboard, and every " +
                 "store he lodges beside. Read through StorageAccess; a closed store takes nothing in and gives " +
                 "nothing out.")]
        public string DeedHomeId = "";

        /// <summary>True for a store that can ever be cold.</summary>
        public bool CanBeCold => Kind == KitchenStorageKind.Cooler || Kind == KitchenStorageKind.Fridge;
    }
}
