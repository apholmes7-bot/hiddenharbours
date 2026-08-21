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
    ///
    /// <para>…and, since the mesh hulls landed, it also PRESENTS the worn hull once at load
    /// (<see cref="PresentWornHull"/>) — the per-run choice ADR 0022 always assumed someone was making.
    /// See that method for why the built scene cannot make it for us.</para>
    /// </summary>
    public class OwnedFleet : MonoBehaviour
    {
        [Tooltip("Every hull the player can own, as data (ADR 0003). Looked up by stable Id — never by " +
                 "name. Add a boat by adding its BoatHullDef here, not by editing this class.")]
        [SerializeField] private BoatHullDef[] _registry;

        [Header("Active boat (what gets swapped on a grant)")]
        [SerializeField] private BoatController _boat;
        [SerializeField] private ShipHold _hold;

        // Her tank (§9.3). Not serialized: BoatController spawns it at runtime, so there is
        // nothing for a scene or a builder to point at — resolved on first hull change instead.
        private BoatFuelTank _tank;
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

        // Start, not Awake: every other Awake (BoatController's included) has run, so the hull this boat
        // wears is settled before we ask what it looks like. A save-restore that arrives later re-skins
        // through ApplyHull anyway, and one that has ALREADY arrived is handled too — see below.
        private void Start() => PresentWornHull();

        /// <summary>
        /// <b>Present the hull this boat is already wearing, from her data</b> — the load-time pass that
        /// makes a running session agree with the assets (ADR 0022: the whole fleet is mesh).
        ///
        /// <para><b>Why it has to exist.</b> A built scene carries a SERIALISED rig, and it is serialised by
        /// an edit-time builder (<c>PersistentCoreBuilder</c>, via the region builders) that deliberately
        /// registers no mesh presentation service — <c>IsoFacetHullPresentationService</c> self-registers at
        /// RUNTIME load only, because a builder must not bake a renderer whose setup is runtime-owned. So at
        /// build time <see cref="BoatHullSkinner.ShouldPresentMesh"/> answers "no mesh path here" and the
        /// builder banks the SPRITE compass, on the stated understanding that the mesh path would be chosen
        /// live, per run, by the skinner. <b>Nothing was making that per-run choice for the player's boat.</b>
        /// This class only re-skinned on a purchase or a save-restore, and <c>DevBoatPicker.Awake</c> only
        /// computes its roster index — so every session opened on the banked sprite rig and stayed there
        /// until the first runtime re-skin, which the owner was reaching by pressing V (the dev A/B toggle)
        /// or F. That is the whole of the "boats are sprites until I press V" defect.</para>
        ///
        /// <para><b>It presents the WORN hull, not the scene default</b>, which makes it order-independent
        /// against the save-restore: if <see cref="OnGameLoaded"/> has already run and re-pointed the
        /// controller, this presents that hull; if it has not, this presents the one the scene serialised and
        /// the restore re-skins over it later. Either way <see cref="BoatHullSkinner.ApplyHull"/> reads the
        /// variant off the VISUAL ASSET — nothing here names a variant, so a visual that is authored sprite
        /// (<c>visual.fishing_boat</c> carries no <c>Variant</c> key at all) still loads as a sprite.</para>
        ///
        /// <para>Public so PlayMode tests can drive the pass explicitly, in the same spirit as
        /// <see cref="OnBoatPurchased"/> and <see cref="RestoreFromSave"/> — though the load-time CLAIM can
        /// only be proven by letting the lifecycle fire it, which is a PlayMode-only affair.</para>
        /// </summary>
        public void PresentWornHull()
        {
            if (_boat == null) return;              // no controller wired → nothing wears a hull
            var hull = _boat.Hull;
            if (hull == null) return;               // a boat with no hull keeps whatever the scene drew

            // The SAME entry point a purchase and a save-restore use, so what you load into is exactly what
            // you would get by buying this boat or hopping to her on the picker — including ApplyHull's
            // propulsion gate, which the builder's bare Apply() could not set.
            //
            // PICTURE ONLY: no SetHull (nothing changed hands — she is already wearing this hull) and no
            // ActiveBoatChanged. Framing at load belongs to the ControlSwitcher, which publishes it when the
            // player actually boards; a grant's reframe is keyed on _aboard for exactly that reason.
            BoatHullSkinner.ApplyHull(gameObject, _spriteRenderer, hull, _boat);
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

            // THE TANK follows the hull too (fuel-and-refuelling.md §9.3). SwitchToHull banks what the
            // OLD hull had under HER id, then reads the new hull's saved row — so the fuel is per BOAT,
            // not per player: trade up with half a tank in the dory and the dory still has half a tank
            // when you come back to her. A hull with no saved row reads brim-full, which is also how
            // GameConfig.Fuel.NewBoatArrivesFull is honoured, so a PURCHASE needs no separate hook here.
            //
            // Found at Awake rather than serialized: the tank is runtime-spawned by BoatController (the
            // BoatAnchor pattern), so no already-built scene has one to wire, and requiring a builder
            // re-run to make fuel persist would be the exact prefab churn that spawn pattern avoids.
            if (_tank == null && _boat != null) _tank = _boat.GetComponent<BoatFuelTank>();
            if (_tank != null) _tank.SwitchToHull(hull);

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
                EventBus.Publish(new ActiveBoatChanged(hull.Id, hull.CameraWorldHeightMeters, hull.LengthMeters));
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
