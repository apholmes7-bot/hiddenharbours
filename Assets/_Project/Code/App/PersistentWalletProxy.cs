using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// A scene-local stand-in for the persistent player's wallet. A region scene (e.g. Port Greywick)
    /// loads additively and can't serialize-reference the persistent wallet — it lives in the persistent
    /// core, a different scene — so that region's Shipwright / WharfSellPoint resolve their
    /// <see cref="IWallet"/> from THIS proxy (they do <c>GetComponent&lt;IWallet&gt;()</c> on a provider
    /// GameObject), and the proxy forwards to the live <see cref="GameServices.Wallet"/> at runtime. So
    /// "the coin crosses the travel": you spend/earn the same persistent wallet on either shore.
    ///
    /// A wiring shim, not a new gameplay system — flagged for lead-architect with the travel approach.
    /// Null-safe before the services are wired (reads as ₲0 / spends fail), so a standalone-opened region
    /// scene doesn't throw.
    /// </summary>
    public sealed class PersistentWalletProxy : MonoBehaviour, IWallet
    {
        public int Money => GameServices.Wallet != null ? GameServices.Wallet.Money : 0;

        public void Add(int amount) => GameServices.Wallet?.Add(amount);

        public bool TrySpend(int amount) => GameServices.Wallet != null && GameServices.Wallet.TrySpend(amount);
    }
}
