namespace HiddenHarbours.Core
{
    /// <summary>
    /// Raised when the player buys a consumable SUPPLY over a counter (the island general store's ice —
    /// plan-to-m1 §7.5). The economy side has already deducted the price and incremented the counted stock
    /// (<see cref="SaveData.SupplyStock"/> via <see cref="SupplyLocker"/>); ui-ux can subscribe for a toast
    /// and a till-beat without referencing the Economy module (cross-module talk via Core/EventBus).
    /// Keyed by the stable <b>SupplyDef</b> id (e.g. "supply.ice") — the id the stock is counted under.
    ///
    /// <para>Lives beside <c>PotSignals</c>/<c>LicenseSignals</c>: economy-owned signals are added
    /// additively in their own Core/Events file, same EventBus (coordination.md §1 — "keep it additive").</para>
    /// </summary>
    public readonly struct SupplyPurchased
    {
        /// <summary>Stable SupplyDef id of the supply bought (e.g. "supply.ice").</summary>
        public readonly string SupplyId;
        /// <summary>₲ paid.</summary>
        public readonly int PricePaid;
        /// <summary>Total of this supply OWNED after the purchase.</summary>
        public readonly int OwnedCount;

        public SupplyPurchased(string supplyId, int pricePaid, int ownedCount)
        {
            SupplyId = supplyId; PricePaid = pricePaid; OwnedCount = ownedCount;
        }
    }

    /// <summary>
    /// Raised when the player buys BAIT over a counter (plan-to-m1 §7.5 — the store restocks the bait the
    /// rod and the pots eat). The economy side has already deducted the price and incremented the counted
    /// stock (<see cref="SaveData.BaitStock"/> via <see cref="TackleBox"/>). Keyed by the stable
    /// <b>BaitDef</b> id (e.g. "bait.capelin").
    /// </summary>
    public readonly struct BaitPurchased
    {
        /// <summary>Stable BaitDef id of the bait bought (e.g. "bait.capelin").</summary>
        public readonly string BaitId;
        /// <summary>₲ paid for the whole lot bought in this transaction.</summary>
        public readonly int PricePaid;
        /// <summary>How many individual baits this purchase added (the offer's lot size).</summary>
        public readonly int CountBought;
        /// <summary>Total of this bait OWNED after the purchase.</summary>
        public readonly int OwnedCount;

        public BaitPurchased(string baitId, int pricePaid, int countBought, int ownedCount)
        {
            BaitId = baitId; PricePaid = pricePaid; CountBought = countBought; OwnedCount = ownedCount;
        }
    }

    /// <summary>
    /// Raised when the player buys a HELM INSTRUMENT and it is fitted to a specific hull (ADR 0025 S2 —
    /// the depth sounder is the first). The economy side has already deducted the price and recorded the
    /// fitment (<see cref="SaveData.HullInstruments"/> via <see cref="InstrumentLocker"/>). Carries BOTH
    /// ids because an instrument is bolted into one boat: "you bought a sounder" is only half the news.
    /// </summary>
    public readonly struct InstrumentPurchased
    {
        /// <summary>Stable instrument id bought (e.g. "instrument.depth_sounder").</summary>
        public readonly string InstrumentId;
        /// <summary>Stable hull id it was fitted to (e.g. "boat.skiff").</summary>
        public readonly string HullId;
        /// <summary>₲ paid.</summary>
        public readonly int PricePaid;

        public InstrumentPurchased(string instrumentId, string hullId, int pricePaid)
        {
            InstrumentId = instrumentId; HullId = hullId; PricePaid = pricePaid;
        }
    }

    /// <summary>
    /// Raised when a one-time fee is FRONTED to the player — the mechanism behind the Aunt Ginny beat that
    /// unblocks the clam licence (plan-to-m1 §7.5's chicken-and-egg). The wallet has already been credited
    /// and the one-time flag persisted. world-content subscribes (or calls the grant's own seam from her
    /// dialogue) to put words to it; the economy never writes dialogue.
    /// </summary>
    public readonly struct FeeFronted
    {
        /// <summary>Stable flag key the one-time grant was recorded under (e.g. "ginny_fronted_clam_fee").</summary>
        public readonly string GrantKey;
        /// <summary>₲ added to the player's wallet.</summary>
        public readonly int Amount;

        public FeeFronted(string grantKey, int amount)
        {
            GrantKey = grantKey; Amount = amount;
        }
    }

    /// <summary>
    /// Raised when the player buys FUEL at a pump (<c>fuel-and-refuelling.md</c>). The economy side has
    /// already deducted the price and poured the fuel into the vessel through <see cref="IFuelVessel"/>;
    /// ui-ux can subscribe for a toast and audio for the pump's clatter, neither of them referencing the
    /// Economy module (cross-module talk via Core/EventBus).
    ///
    /// <para><b>Priced by the LITRE, so the quantity is a float</b> — unlike every other signal in this
    /// file, which counts whole things. A fill is a continuous amount, and rounding it here would make the
    /// announced litres disagree with the litres actually in the can. Only the PRICE is whole, because
    /// money is.</para>
    ///
    /// <para>Keyed by the stable <b>FuelStationDef</b> id (e.g. "station.route_91") AND the grade — which
    /// site and which fuel are both load-bearing, since the island's whole fuel design is that the same
    /// litre costs differently in different places.</para>
    /// </summary>
    public readonly struct FuelPurchased
    {
        /// <summary>Stable FuelStationDef id of the site sold at (e.g. "station.route_91").</summary>
        public readonly string StationId;
        /// <summary>Grade bought — one of <see cref="FuelGrades"/>.</summary>
        public readonly string Grade;
        /// <summary>Litres actually delivered into the vessel.</summary>
        public readonly float Litres;
        /// <summary>₲ paid.</summary>
        public readonly int PricePaid;

        public FuelPurchased(string stationId, string grade, float litres, int pricePaid)
        {
            StationId = stationId; Grade = grade; Litres = litres; PricePaid = pricePaid;
        }
    }

    /// <summary>
    /// <b>A conversation asking for a seller's wares book.</b> Published by the dialogue presenter when
    /// the player picks a row that was authored with a catalog pointer; the economy side listens and
    /// opens the book. It is the first and only thing World says about shopping, and it says it without
    /// naming an economy type (rule 4) — World branches on nothing, and Economy never learns what a
    /// <c>DialogueOption</c> is.
    ///
    /// <para><b>The conversation does not end on this.</b> The bubble stays up and dimmed, the rows go
    /// down, and the picker comes back when <see cref="CatalogClosed"/> lands — so browse, then sell,
    /// then "See you later." is one conversation with one person (the 2026-08-27 ruling on R2). A book
    /// with nobody holding it is a menu.</para>
    /// </summary>
    public readonly struct CatalogViewRequested
    {
        /// <summary>Whose book to open — the seller id the row was authored with, matched against the
        /// <c>SellerId</c> on the vendor components that own the purchase seams.</summary>
        public readonly string SellerId;

        /// <summary>Which section to open on, or empty for "the first stub this seller has". A plain
        /// string on purpose: the section enum lives in Economy and World may not name it, and an
        /// unrecognised value opening on the first tab is a kinder failure than a compile-time coupling
        /// between the two modules.</summary>
        public readonly string Section;

        /// <summary>The speaker's <c>NpcDef.Id</c> — whose counter you are standing at, so the book can
        /// head itself with the person you are actually talking to.</summary>
        public readonly string SpeakerId;

        public CatalogViewRequested(string sellerId, string section, string speakerId)
        {
            SellerId = sellerId; Section = section ?? ""; SpeakerId = speakerId;
        }
    }

    /// <summary>
    /// <b>A conversation asking the counter to take what the player is carrying.</b> Published by the
    /// dialogue presenter when the player picks a row authored with a counter-sell pointer; the economy
    /// side listens, hands the wired hold to the seller's existing sell components, and answers with
    /// <see cref="CounterSellReported"/>.
    ///
    /// <para><b>It is the sell verb's whole crossing, and it names no economy type</b> (rule 4). The
    /// sale itself is not new: it is the same <c>FishBuyer</c> at the same <c>Market</c> quoting the same
    /// <c>SellPricing</c> the counter has always used (owner ruling R7, 2026-08-27) — the row is a door
    /// onto it, not a second implementation of it.</para>
    ///
    /// <para><b>Answered SYNCHRONOUSLY, on the same publish.</b> The presenter is holding the
    /// conversation open on this and speaks the outcome in the seller's own bubble, so the reply has to
    /// be in hand by the time the publish returns. A seller nobody answers for reads as a sale that sold
    /// nothing, which is the same words as an empty pail and is the kind failure here.</para>
    /// </summary>
    public readonly struct CounterSellRequested
    {
        /// <summary>Whose counter is being sold over — the same seller id the browse row opens a book
        /// with, matched against the <c>SellerId</c> on the vendor components that stand on it.</summary>
        public readonly string SellerId;

        /// <summary>The speaker's <c>NpcDef.Id</c> — who is doing the taking, so an economy-side log can
        /// name the person rather than a counter.</summary>
        public readonly string SpeakerId;

        public CounterSellRequested(string sellerId, string speakerId)
        {
            SellerId = sellerId ?? ""; SpeakerId = speakerId ?? "";
        }
    }

    /// <summary>
    /// <b>What the counter did with it — facts, never words.</b> The economy reports the payout and how
    /// much left the hold; world-content puts that into the seller's mouth from lines the owner authored
    /// on the option asset.
    ///
    /// <para><b>The split is deliberate and is <see cref="FeeFronted"/>'s rule kept</b>: "the economy
    /// never writes dialogue". A sentence composed here would be a seller's voice living in the buy
    /// stack, unreachable to the owner and identical at every counter on the coast.</para>
    /// </summary>
    public readonly struct CounterSellReported
    {
        /// <summary>Whose counter answered — the id <see cref="CounterSellRequested.SellerId"/> asked.</summary>
        public readonly string SellerId;

        /// <summary>₲ paid for the lot. Zero when nothing was sold.</summary>
        public readonly int Payout;

        /// <summary>How many units actually left the hold. <b>This, not the payout, is what says a sale
        /// happened</b> — a lot can price at ₲0 (a glutted market, a worthless species) and that is a
        /// sale that emptied your pail, not an empty pail.</summary>
        public readonly int UnitsSold;

        /// <summary>True when the counter actually took something.</summary>
        public bool SoldAnything => UnitsSold > 0;

        public CounterSellReported(string sellerId, int payout, int unitsSold)
        {
            SellerId = sellerId ?? ""; Payout = payout; UnitsSold = unitsSold;
        }
    }

    /// <summary>
    /// <b>The wares book has been shut.</b> Published by the economy side when the player closes the
    /// catalog; the dialogue presenter listens and re-arms the picker on the same rows, so the player is
    /// handed back to the person who lent them the book rather than to an empty street.
    ///
    /// <para><b>It carries the seller, not a result.</b> What was bought is already reported by the
    /// purchase signals the vendors publish (<see cref="SupplyPurchased"/> and its siblings), and a
    /// second telling of the same fact is a second place for it to be wrong.</para>
    /// </summary>
    public readonly struct CatalogClosed
    {
        /// <summary>Whose book was shut — the id <see cref="CatalogViewRequested.SellerId"/> opened.</summary>
        public readonly string SellerId;

        public CatalogClosed(string sellerId) { SellerId = sellerId; }
    }
}
