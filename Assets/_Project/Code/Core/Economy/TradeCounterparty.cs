namespace HiddenHarbours.Core
{
    /// <summary>Authored identity of the other side of a till. No display names or scene objects.</summary>
    public readonly struct TradeCounterparty
    {
        public const string UnknownId = "unknown";
        private readonly string _merchantId;
        private readonly string _vendorId;
        private readonly string _tradeId;

        // Properties also normalize default(struct), not just explicitly constructed values.
        public string MerchantId => KnownOrUnknown(_merchantId);
        public string VendorId => KnownOrUnknown(_vendorId);
        public string TradeId => KnownOrUnknown(_tradeId);

        public TradeCounterparty(string merchantId, string vendorId, string tradeId)
        {
            _merchantId = merchantId;
            _vendorId = vendorId;
            _tradeId = tradeId;
        }

        public static string KnownOrUnknown(string id)
            => string.IsNullOrWhiteSpace(id) ? UnknownId : id;
    }

    /// <summary>
    /// A completed transaction, counted once by CustomLedger. Summary replies such as
    /// CounterSellReported do not implement this: CatchSold is the canonical sale fact.
    /// Units are things, individual baits, or litres for a fuel trade; never money or total owned.
    /// </summary>
    public interface ICustomSignal
    {
        TradeCounterparty Counterparty { get; }
        decimal CustomUnits { get; }
    }
}
