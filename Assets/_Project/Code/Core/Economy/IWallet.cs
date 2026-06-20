namespace HiddenHarbours.Core
{
    /// <summary>The player's money (₲). Implemented by PlayerWallet (in Player).</summary>
    public interface IWallet
    {
        int Money { get; }
        void Add(int amount);

        /// <summary>Spend if affordable; returns false (and changes nothing) if not.</summary>
        bool TrySpend(int amount);
    }
}
