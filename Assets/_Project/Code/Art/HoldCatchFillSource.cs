using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Feeds a <see cref="CatchFillRenderer"/> from a cargo hold — THROUGH CORE ONLY (rule 4):
    /// the hold is resolved as the <see cref="IHold"/> Core interface on this object or a parent
    /// (the same <c>GetComponent&lt;IHold&gt;()</c> resolution <c>WharfSellPoint</c> uses against
    /// the provider/proxy pattern), and refreshes ride the Core bus edges the deck tray already
    /// established — <see cref="FishCaught"/>, <see cref="CatchSold"/>, <see cref="GameLoaded"/>,
    /// <see cref="BoatPurchased"/>. No reference to Boats, Fishing or Economy concrete classes.
    ///
    /// <para><b>Seam note for lead-architect (flagged in the storage PR):</b> Core today has no
    /// service-locator read for "the player's hold" (<c>GameServices</c> carries Wallet/Licenses/
    /// ActiveBoat but not IHold), so this bridge requires an IHold provider in its own hierarchy —
    /// fine for deck containers and the wharf's <c>PersistentHoldProxy</c> object, but a
    /// free-standing dock tote would need either that proxy alongside it or a minimal
    /// <c>GameServices.Hold</c>-style accessor. If scene layouts start wanting the latter, that
    /// accessor is the right Core addition; this component would then prefer it over the
    /// hierarchy walk.</para>
    ///
    /// <para>Event-driven only: no per-frame polling; the species→kind mapping reuses the
    /// renderer's <see cref="CatchItemLibrary"/> (one data asset, one truth). The kind buffer is
    /// reused, so a refresh allocates nothing once warm.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CatchFillRenderer))]
    public sealed class HoldCatchFillSource : MonoBehaviour
    {
        private CatchFillRenderer _renderer;
        private IHold _hold;
        private readonly List<string> _kinds = new List<string>();

        private void Awake()
        {
            _renderer = GetComponent<CatchFillRenderer>();
        }

        private void OnEnable()
        {
            // The four Core edges a hold's contents can change on — the same set the Boats-lane
            // deck tray subscribes to (landed catch, sale, load-restore, bought hull).
            EventBus.Subscribe<FishCaught>(OnFishCaught);
            EventBus.Subscribe<CatchSold>(OnCatchSold);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            EventBus.Subscribe<BoatPurchased>(OnBoatPurchased);
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Unsubscribe<CatchSold>(OnCatchSold);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            EventBus.Unsubscribe<BoatPurchased>(OnBoatPurchased);
        }

        private void OnFishCaught(FishCaught _) => Refresh();
        private void OnCatchSold(CatchSold _) => Refresh();
        private void OnGameLoaded(GameLoaded _) => Refresh();
        private void OnBoatPurchased(BoatPurchased _) => Refresh();

        /// <summary>Re-read the hold and hand the renderer plain data. Public so tools/tests can
        /// force a re-read; event-time only otherwise.</summary>
        public void Refresh()
        {
            // Re-resolve when missing: the hold provider can spawn after us (scene load order).
            _hold ??= GetComponentInParent<IHold>();
            if (_hold == null || _renderer == null || _renderer.Library == null)
            {
                _renderer?.SetContents(null, 0f);
                return;
            }

            IReadOnlyList<CatchItem> items = _hold.Items;
            _kinds.Clear();
            for (int i = 0; i < items.Count; i++)
                _kinds.Add(_renderer.Library.KindFor(items[i].SpeciesId));

            float fill01 = _hold.CapacityUnits > 0
                ? (float)_hold.UsedUnits / _hold.CapacityUnits
                : 0f;
            _renderer.SetContents(_kinds, fill01);
        }
    }
}
