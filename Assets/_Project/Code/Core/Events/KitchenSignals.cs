namespace HiddenHarbours.Core
{
    // Kitchens slice 1 (owner ask 2026-11-15): food stores, seats and dining. Location-agnostic: a store is
    // named by its storage KEY (KitchenStorageStateDto.Key) and a seat by its id, whether it stands in a
    // galley aboard, a home, a staffed shop or a player-built building. Audio and animation listen here;
    // nothing in this slice draws a bar or a number (owner ruling 2026-11-14).

    /// <summary>Food went into a store (<see cref="KitchenStores.Store"/>).</summary>
    public readonly struct KitchenItemStored
    {
        public readonly string StorageKey;
        public readonly string StorageDefId;
        public readonly string ItemId;
        public readonly int Count;

        public KitchenItemStored(string storageKey, string storageDefId, string itemId, int count)
        {
            StorageKey = storageKey; StorageDefId = storageDefId; ItemId = itemId; Count = count;
        }
    }

    /// <summary>Food came out of a store (<see cref="KitchenStores.Take"/>).</summary>
    public readonly struct KitchenItemTaken
    {
        public readonly string StorageKey;
        public readonly string StorageDefId;
        public readonly string ItemId;
        public readonly int Count;

        public KitchenItemTaken(string storageKey, string storageDefId, string itemId, int count)
        {
            StorageKey = storageKey; StorageDefId = storageDefId; ItemId = itemId; Count = count;
        }
    }

    /// <summary>A sold boat's stores were emptied (<see cref="KitchenStores.ClearOutBoat"/>): the food
    /// lots, each with its own freshness clock, handed to the player, and the unmelted ice bags returned to
    /// the supply locker.</summary>
    public readonly struct KitchenBoatClearedOut
    {
        public readonly string OwnedBoatId;
        public readonly System.Collections.Generic.IReadOnlyList<KitchenStackDto> Lots;
        public readonly int IceUnitsReturned;

        public KitchenBoatClearedOut(string ownedBoatId, System.Collections.Generic.IReadOnlyList<KitchenStackDto> lots,
                                     int iceUnitsReturned)
        {
            OwnedBoatId = ownedBoatId; Lots = lots; IceUnitsReturned = iceUnitsReturned;
        }
    }

    /// <summary>A cooler was packed with ice (<see cref="KitchenStores.PackIceFromLocker"/>).</summary>
    public readonly struct KitchenIcePacked
    {
        public readonly string StorageKey;
        public readonly int Units;
        /// <summary>The game-clock instant the ice now runs out at.</summary>
        public readonly double IceRunsOutAtGameSeconds;

        public KitchenIcePacked(string storageKey, int units, double iceRunsOutAtGameSeconds)
        {
            StorageKey = storageKey; Units = units; IceRunsOutAtGameSeconds = iceRunsOutAtGameSeconds;
        }
    }

    /// <summary>A cooler's last ice melted: it is just a box now. Raised once per packing, by the slow
    /// tick (<see cref="KitchenStores.Tick"/>), at the first tick at or after the melt instant.</summary>
    public readonly struct KitchenIceRanOut
    {
        public readonly string StorageKey;
        /// <summary>When the ice actually ran out (the melt instant, not the tick that noticed).</summary>
        public readonly double RanOutAtGameSeconds;

        public KitchenIceRanOut(string storageKey, double ranOutAtGameSeconds)
        {
            StorageKey = storageKey; RanOutAtGameSeconds = ranOutAtGameSeconds;
        }
    }

    /// <summary>The player sat down (<see cref="SeatSession.Sit"/>).</summary>
    public readonly struct PlayerSat
    {
        public readonly string SeatId;
        /// <summary>True when the seat is by a table or counter (<see cref="KitchenLayout.IsDiningSeat"/>).</summary>
        public readonly bool AtDiningSeat;

        public PlayerSat(string seatId, bool atDiningSeat) { SeatId = seatId; AtDiningSeat = atDiningSeat; }
    }

    /// <summary>The player got up (<see cref="SeatSession.Stand"/>), after the seated rest was banked.</summary>
    public readonly struct PlayerStood
    {
        public readonly string SeatId;
        /// <summary>In-game seconds spent seated.</summary>
        public readonly double SeatedGameSeconds;

        public PlayerStood(string seatId, double seatedGameSeconds) { SeatId = seatId; SeatedGameSeconds = seatedGameSeconds; }
    }

    /// <summary>The player ate sitting down (<see cref="INeedsService.Eat(float, EatingPosture)"/>).</summary>
    public readonly struct PlayerDined
    {
        public readonly EatingPosture Posture;
        /// <summary>The food's own relief (0..1 of a full meal) before the seat's bonus.</summary>
        public readonly float BaseRelief;
        /// <summary>The relief actually taken off hunger.</summary>
        public readonly float Relief;

        public PlayerDined(EatingPosture posture, float baseRelief, float relief)
        {
            Posture = posture; BaseRelief = baseRelief; Relief = relief;
        }
    }
}
