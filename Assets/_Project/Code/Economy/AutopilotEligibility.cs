using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>Pure snapshot of hull id / AutopilotOffered pairs supplied from the hull registry.
    /// No drive inference, console requirement or content list. Recreate when replacing the registry.</summary>
    public sealed class AutopilotEligibility : IAutopilotEligibility
    {
        readonly Dictionary<string, bool> _offered = new Dictionary<string, bool>(StringComparer.Ordinal);

        public AutopilotEligibility(IEnumerable<KeyValuePair<string, bool>> authoredFlags)
        {
            if (authoredFlags == null) return;
            foreach (var flag in authoredFlags)
            {
                if (string.IsNullOrWhiteSpace(flag.Key)) continue;
                // Any duplicate is ambiguous, even when both flags happen to agree.
                if (_offered.ContainsKey(flag.Key)) _offered[flag.Key] = false;
                else _offered.Add(flag.Key, flag.Value);
            }
        }

        public bool CanTakeAutopilot(string hullId)
            => hullId != null && _offered.TryGetValue(hullId, out bool offered) && offered;
    }
}
