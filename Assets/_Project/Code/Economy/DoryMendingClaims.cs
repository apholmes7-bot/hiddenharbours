using System.Collections.Generic;

namespace HiddenHarbours.Economy
{
    /// <summary>Loaded-world authority shared by all player sessions at its slips. Dispatch commands
    /// on the simulation thread. Keep this instance across session replacements; dispose departing
    /// sessions to release their claims. This is neither a singleton nor saved state.</summary>
    public sealed class DoryMendingClaims
    {
        private readonly Dictionary<string, DoryMendingSession> _owners =
            new Dictionary<string, DoryMendingSession>(System.StringComparer.Ordinal);

        internal bool HeldByOther(string boatId, DoryMendingSession actor) =>
            _owners.TryGetValue(boatId, out var owner) && !ReferenceEquals(owner, actor);

        internal bool TryClaim(string boatId, DoryMendingSession actor)
        {
            if (HeldByOther(boatId, actor)) return false;
            _owners[boatId] = actor;
            return true;
        }

        internal void Release(string boatId, DoryMendingSession actor)
        {
            if (_owners.TryGetValue(boatId, out var owner) && ReferenceEquals(owner, actor))
                _owners.Remove(boatId);
        }
    }
}
