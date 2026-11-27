namespace HiddenHarbours.Core
{
    /// <summary>A priced vehicle deed offered by a seller. Content supplies every value.</summary>
    public interface IVehicleSale
    {
        string Id { get; }
        string VehicleId { get; }
        string SellerId { get; }
        int Price { get; }
        bool StartsDamaged { get; }
        int RepairCost { get; }
    }

    /// <summary>
    /// The actual counter taking payment. The caller supplies its authored identity and account;
    /// this seam neither creates money nor invents a persistent merchant economy.
    /// Wallet.Add must accept a nonnegative amount within the integer balance limit.
    /// </summary>
    public interface IVehicleSeller
    {
        TradeCounterparty Counterparty { get; }
        IWallet Wallet { get; }
    }

    /// <summary>Bind an authored seller identity to the account provided by the caller.</summary>
    public sealed class VehicleSellerAccount : IVehicleSeller
    {
        public TradeCounterparty Counterparty { get; }
        public IWallet Wallet { get; }

        public VehicleSellerAccount(TradeCounterparty counterparty, IWallet wallet)
        {
            Counterparty = counterparty;
            Wallet = wallet;
        }
    }
}
