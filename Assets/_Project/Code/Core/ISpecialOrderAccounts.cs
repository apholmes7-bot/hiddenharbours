using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Authoritative payer identities for one world's orders. Unknown ownership returns null.
    /// A successful payment assigns the order to that player before returning; failure changes nothing.
    /// Calls come from the world's single book on the simulation thread.
    /// </summary>
    public interface ISpecialOrderAccounts
    {
        string OwnerOf(string orderId);
        bool TryPay(string playerId, string orderId, int amount);
    }

    /// <summary>
    /// Session-only accounts. The host binds each stable player identity to his purse.
    /// Existing saved rows cannot recover their payer from version 26; no owner is guessed here.
    /// </summary>
    public sealed class SessionSpecialOrderAccounts : ISpecialOrderAccounts
    {
        private readonly Dictionary<string, IWallet> _wallets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _owners = new(StringComparer.Ordinal);
        private bool _paying;

        public SessionSpecialOrderAccounts(IReadOnlyDictionary<string, IWallet> wallets)
        {
            if (wallets == null) throw new ArgumentNullException(nameof(wallets));
            foreach (var pair in wallets)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                    throw new ArgumentException("Each account requires a player identity and a purse.");
                _wallets.Add(pair.Key, pair.Value);
            }
        }

        public string OwnerOf(string orderId) => orderId != null && _owners.TryGetValue(orderId, out var player)
            ? player : null;

        public bool TryPay(string playerId, string orderId, int amount)
        {
            if (_paying || string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(orderId)
                || amount <= 0 || _owners.ContainsKey(orderId) || !_wallets.TryGetValue(playerId, out var wallet))
                return false;
            _paying = true;
            try
            {
                if (!wallet.TrySpend(amount)) return false;
                _owners.Add(orderId, playerId);
                return true;
            }
            finally { _paying = false; }
        }

        /// <summary>Detached session facts for inspection; this is not a save row or an ownership import.</summary>
        public SpecialOrderOwnership[] CaptureOwnership()
        {
            var ids = new List<string>(_owners.Keys);
            ids.Sort(StringComparer.Ordinal);
            var result = new SpecialOrderOwnership[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                result[i] = new SpecialOrderOwnership { OrderId = ids[i], PlayerId = _owners[ids[i]] };
            return result;
        }
    }

    [Serializable]
    public sealed class SpecialOrderOwnership
    {
        public string OrderId;
        public string PlayerId;
    }
}
