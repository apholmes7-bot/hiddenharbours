using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>Session-local state only. Inject one store per fleet/session; no global registration.</summary>
    public sealed class InMemoryAutopilotFitments : IAutopilotFitments
    {
        readonly HashSet<string> _purchased = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _fitted = new HashSet<string>(StringComparer.Ordinal);

        public bool IsPurchased(string ownedBoatId) => ownedBoatId != null && _purchased.Contains(ownedBoatId);
        public bool IsFitted(string ownedBoatId) => ownedBoatId != null && _fitted.Contains(ownedBoatId);

        public void Fit(string ownedBoatId)
        {
            if (string.IsNullOrWhiteSpace(ownedBoatId) || !OwnedBoatId.IsValid(ownedBoatId))
                throw new ArgumentException("A fitment needs an owned boat id.", nameof(ownedBoatId));
            _purchased.Add(ownedBoatId);
            _fitted.Add(ownedBoatId);
        }

        public bool Remove(string ownedBoatId) => ownedBoatId != null && _fitted.Remove(ownedBoatId);
    }
}
