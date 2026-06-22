using System;
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Scene-local stand-in for the persistent boat's hold (<see cref="IHold"/>) — the hold counterpart to
    /// <see cref="PersistentWalletProxy"/>. A region scene's <c>WharfSellPoint</c> resolves its IHold from
    /// this proxy (<c>GetComponent&lt;IHold&gt;()</c> on a provider GameObject); the proxy forwards to the
    /// live persistent hold, so "selling your catch" empties the SAME hold you filled out at the cove —
    /// the catch crosses the travel.
    ///
    /// The hold is injected by the <see cref="RegionTravelCoordinator"/> on arrival (<see cref="Bind"/>),
    /// with a lazy <see cref="UnityEngine.Object.FindAnyObjectByType{T}()"/> fallback on the one persistent
    /// <see cref="ShipHold"/> so a standalone-opened region scene still resolves it. A wiring shim, not a
    /// new system — flagged with the travel approach; null-safe before a hold exists.
    /// </summary>
    public sealed class PersistentHoldProxy : MonoBehaviour, IHold
    {
        private IHold _hold;

        /// <summary>Point the proxy at the live persistent hold (called by the travel coordinator).</summary>
        public void Bind(IHold hold) => _hold = hold;

        // Lazily resolve the one persistent ShipHold if nobody bound us (e.g. standalone scene review).
        private IHold Resolve() => _hold ??= FindAnyObjectByType<ShipHold>();

        public int CapacityUnits => Resolve()?.CapacityUnits ?? 0;
        public int UsedUnits => Resolve()?.UsedUnits ?? 0;
        public IReadOnlyList<CatchItem> Items => Resolve()?.Items ?? Array.Empty<CatchItem>();

        public bool TryAdd(CatchItem item)
        {
            var h = Resolve();
            return h != null && h.TryAdd(item);
        }

        public void Clear() => Resolve()?.Clear();
    }
}
