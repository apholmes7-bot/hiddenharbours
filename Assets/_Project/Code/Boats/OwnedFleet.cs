using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Closes the buy-the-Punt loop (VS-16 boat grant). The Economy Shipwright takes the coin and
    /// publishes the Core signal <see cref="BoatPurchased"/> by stable boat id; this listens and grants
    /// the boat — looking the hull up in a data-driven registry and swapping the active
    /// <see cref="BoatController"/> + <see cref="ShipHold"/> (and the boat's sprite) to it. That's the
    /// "I'm a real fisher now" beat (P2 Dory→Dynasty; P4 earn it, then automate it). Cross-module talk
    /// is one-way through Core: Economy never references the Boats module, only the id.
    ///
    /// SCOPE: this is an in-session swap only. Persisting the owned boat across save/load is VS-08
    /// (see the TODO below) — not pulled forward here.
    /// </summary>
    public class OwnedFleet : MonoBehaviour
    {
        [Tooltip("Every hull the player can own, as data (ADR 0003). Looked up by stable Id — never by " +
                 "name. Add a boat by adding its BoatHullDef here, not by editing this class.")]
        [SerializeField] private BoatHullDef[] _registry;

        [Header("Active boat (what gets swapped on a grant)")]
        [SerializeField] private BoatController _boat;
        [SerializeField] private ShipHold _hold;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private void Awake() => EventBus.Subscribe<BoatPurchased>(OnBoatPurchased);

        private void OnDisable() => EventBus.Unsubscribe<BoatPurchased>(OnBoatPurchased);

        /// <summary>
        /// Grant a purchased boat by swapping the active hull. Data-driven lookup by stable Id; an
        /// unknown id (or a registry miss) is a graceful no-op so we never null-swap the player into a
        /// dead boat or throw. Public so EditMode tests can drive it through the bus without the
        /// play-mode lifecycle.
        /// </summary>
        public void OnBoatPurchased(BoatPurchased e)
        {
            var hull = FindHull(e.BoatId);
            if (hull == null) return;   // unknown id → no-op: no exception, no null-swap

            if (_boat != null) _boat.SetHull(hull);                                  // feel + mass
            if (_hold != null) _hold.SetHull(hull);                                  // capacity 6→14
            if (_spriteRenderer != null && hull.Sprite != null)
                _spriteRenderer.sprite = hull.Sprite;                               // visible swap

            // Re-point the camera to this hull's framing — the App's CameraFollow listens via Core, so
            // Boats never references it. Buying a bigger boat zooms the view out a touch (data-driven
            // from the hull). M2 hulls just set a larger CameraWorldHeightMeters — no new code here.
            EventBus.Publish(new ActiveBoatChanged(hull.Id, hull.CameraWorldHeightMeters));
            // TODO(VS-08): persist the owned boat across save/load. For now the grant is in-session only.
        }

        /// <summary>Find a hull in the registry by its stable Id. No DisplayName/name special-casing.</summary>
        private BoatHullDef FindHull(string boatId)
        {
            if (_registry == null || string.IsNullOrEmpty(boatId)) return null;
            for (int i = 0; i < _registry.Length; i++)
            {
                var h = _registry[i];
                if (h != null && h.Id == boatId) return h;
            }
            return null;
        }

        /// <summary>
        /// Wire the fleet in one call. Used by EditMode tests; the greybox builder wires the same
        /// serialized fields via SerializedObject so the refs persist into the saved scene.
        /// </summary>
        public void Configure(BoatHullDef[] registry, BoatController boat, ShipHold hold, SpriteRenderer spriteRenderer)
        {
            _registry = registry;
            _boat = boat;
            _hold = hold;
            _spriteRenderer = spriteRenderer;
        }
    }
}
