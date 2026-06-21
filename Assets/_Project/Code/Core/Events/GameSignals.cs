namespace HiddenHarbours.Core
{
    /// <summary>Raised once when a new in-game day begins.</summary>
    public readonly struct DayStarted
    {
        public readonly int DayOfSeason;   // 1-based
        public readonly Season Season;
        public readonly int Year;
        public DayStarted(int dayOfSeason, Season season, int year)
        {
            DayOfSeason = dayOfSeason; Season = season; Year = year;
        }
    }

    /// <summary>Raised when the season rolls over.</summary>
    public readonly struct SeasonChanged
    {
        public readonly Season Season;
        public readonly int Year;
        public SeasonChanged(Season season, int year) { Season = season; Year = year; }
    }

    /// <summary>Raised when the tide crosses between rising and falling (a "turn of the tide").</summary>
    public readonly struct TideTurned
    {
        public readonly bool NowRising;
        public readonly float TideHeight;
        public TideTurned(bool nowRising, float tideHeight)
        {
            NowRising = nowRising; TideHeight = tideHeight;
        }
    }

    /// <summary>Raised when a boat touches bottom (draught exceeded local water depth). P5.</summary>
    public readonly struct BoatGrounded
    {
        public readonly object Boat;        // boxed reference to the boat (kept engine-light in Core)
        public readonly float Severity;     // 0..1, how hard
        public BoatGrounded(object boat, float severity) { Boat = boat; Severity = severity; }
    }

    /// <summary>Raised when a fish is landed into the hold.</summary>
    public readonly struct FishCaught
    {
        public readonly CatchItem Item;
        public FishCaught(CatchItem item) { Item = item; }
    }

    /// <summary>Raised when a hold's catch is sold at market.</summary>
    public readonly struct CatchSold
    {
        public readonly int TotalPaid;   // ₲
        public readonly int Count;
        public CatchSold(int totalPaid, int count) { TotalPaid = totalPaid; Count = count; }
    }

    /// <summary>Raised whenever the player's balance changes.</summary>
    public readonly struct MoneyChanged
    {
        public readonly int NewBalance;
        public readonly int Delta;
        public MoneyChanged(int newBalance, int delta) { NewBalance = newBalance; Delta = delta; }
    }

    /// <summary>
    /// Raised when the player buys a boat at the Shipwright. The economy side has already deducted the
    /// money; <c>gameplay-systems</c> listens for this to add the hull to the owned fleet and swap the
    /// active boat — so Economy never references the Boats module (cross-module talk via Core/EventBus).
    /// Keyed by stable boat id (e.g. "boat.punt"), matching <c>BoatHullDef.Id</c>.
    /// </summary>
    public readonly struct BoatPurchased
    {
        public readonly string BoatId;
        public readonly int PricePaid;   // ₲
        public BoatPurchased(string boatId, int pricePaid)
        {
            BoatId = boatId; PricePaid = pricePaid;
        }
    }

    /// <summary>
    /// Raised when the active boat changes (an upgrade swap), carrying the framing the camera should
    /// use for that hull. Lets the camera (App) zoom to the new boat WITHOUT referencing the Boats
    /// module — data-driven from <c>BoatHullDef.CameraWorldHeightMeters</c>. Bigger boat = more water
    /// on screen (the tangible "bigger boat" beat). Keyed by stable boat id.
    /// </summary>
    public readonly struct ActiveBoatChanged
    {
        public readonly string BoatId;
        public readonly float CameraWorldHeightMeters;
        public ActiveBoatChanged(string boatId, float cameraWorldHeightMeters)
        {
            BoatId = boatId; CameraWorldHeightMeters = cameraWorldHeightMeters;
        }
    }
}
