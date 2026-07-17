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
    /// SCOPE: in-session a purchase swaps the active hull; across save/load the owned fleet is RESTORED
    /// (VS-08 load-restore). On the Core <see cref="GameLoaded"/> edge this re-grants the saved boats from
    /// <see cref="ISaveService.Current"/> through the same hull-swap path a live purchase uses — applying
    /// the saved active hull last so you resume aboard the boat you saved in. It reads only the Core save
    /// seam, never Economy/Save concretes.
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

        // The camera framing belongs to PILOTING, not OWNERSHIP: a purchase grants the hull, but the
        // view only reframes when you're actually aboard that boat. We track the control mode off the
        // Core ControlModeChanged seam (the ControlSwitcher publishes it on board/disembark) so a buy at
        // the wharf — which is gated to the on-foot player — never zooms the on-foot camera. An upgrade
        // taken WHILE aboard still reframes to the new hull. Cross-module talk stays one-way via Core.
        private bool _aboard;

        private void Awake()
        {
            EventBus.Subscribe<BoatPurchased>(OnBoatPurchased);
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BoatPurchased>(OnBoatPurchased);
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
        }

        /// <summary>Track whether the player is currently piloting, so a grant only reframes when aboard.</summary>
        public void OnControlModeChanged(ControlModeChanged e) => _aboard = e.Mode == ControlMode.Aboard;

        /// <summary>
        /// Grant a purchased boat by swapping the active hull. Data-driven lookup by stable Id; an
        /// unknown id (or a registry miss) is a graceful no-op so we never null-swap the player into a
        /// dead boat or throw. Public so EditMode tests can drive it through the bus without the
        /// play-mode lifecycle.
        /// </summary>
        public void OnBoatPurchased(BoatPurchased e) => ApplyHull(e.BoatId);

        /// <summary>
        /// Restore the owned fleet from the loaded save (VS-08 load-restore), fired off the Core
        /// <see cref="GameLoaded"/> edge. Re-grants the saved active hull through the same swap path a live
        /// purchase uses, so reloading resumes you aboard the boat you saved in. Public so EditMode tests
        /// can drive it without the play-mode lifecycle.
        ///
        /// <para>Reading the fleet is data-only: the active hull (<see cref="SaveData.ActiveHullId"/>) is
        /// the one that drives feel/hold/sprite, so that's the hull we apply. The full owned list
        /// (<see cref="SaveData.OwnedBoats"/>) is the player's roster for a future fleet screen; with the
        /// single active boat the slice swaps, applying the active hull is the visible restore. An empty/
        /// unknown active id is a graceful no-op — the scene-default hull stands.</para>
        /// </summary>
        public void OnGameLoaded(GameLoaded _) => RestoreFromSave(GameServices.Save?.Current);

        /// <summary>Apply the saved active hull from an explicit blob (testable overload). No-op on a null
        /// save or empty active id.</summary>
        public void RestoreFromSave(SaveData data)
        {
            if (data == null) return;
            ApplyHull(data.ActiveHullId);
        }

        /// <summary>
        /// Swap the active boat to the hull with this stable id: feel + hold + sprite, and (only when
        /// piloting) the camera framing. The one place the swap happens, shared by a live purchase and a
        /// save-restore. An unknown id (or a registry miss) is a graceful no-op so we never null-swap the
        /// player into a dead boat or throw.
        /// </summary>
        private void ApplyHull(string boatId)
        {
            var hull = FindHull(boatId);
            if (hull == null) return;   // unknown id → no-op: no exception, no null-swap

            if (_boat != null) _boat.SetHull(hull);                                  // feel + mass
            if (_hold != null) _hold.SetHull(hull);                                  // capacity 6→14

            // THE VISIBLE SWAP — through the data-driven skin seam, never by poking a renderer. This used
            // to read `_spriteRenderer.sprite = hull.Sprite`, which was a REAL BUG for as long as the
            // player's boat has worn a directional skin: the skin DISABLES that base renderer and draws
            // the hull on a compass child instead, so writing its sprite changed nothing you could see.
            // Buying the Punt swapped your feel, your hold and your camera while the picture stayed the
            // iso dory. BoatHullSkinner handles BOTH directions — a hull that binds a Visual installs or
            // refreshes the compass; a plain hull tears the compass down and brings the base renderer back
            // with the new hull's Sprite — so every rung of the ladder shows the boat you actually bought.
            BoatHullSkinner.ApplyHull(gameObject, _spriteRenderer, hull, _boat);

            // Re-point the camera ONLY when actively piloting this boat — framing keys off PILOTING, not
            // ownership. A buy at the wharf (on foot) grants the hull but must NOT zoom the on-foot view;
            // the boat's framing arrives via ControlSwitcher.Board() when you next step aboard. An upgrade
            // taken while already aboard reframes here to the new hull. On a save-restore that completes on
            // foot, this likewise stays quiet — boarding will frame the restored hull. The App's
            // CameraFollow listens via Core, so Boats never references it; bigger boat → more water.
            if (_aboard)
                EventBus.Publish(new ActiveBoatChanged(hull.Id, hull.CameraWorldHeightMeters));
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
