using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The persistent-core half of VS-22 travel: it carries the player/boat/hold/wallet across an additive
    /// region hop and re-binds them to whichever region just became active. Lives on the persistent core
    /// (DontDestroyOnLoad) and reacts to <see cref="SceneManager.activeSceneChanged"/> (raised by the
    /// <c>RegionSceneLoader</c> when it loads or re-activates a region): it deactivates the region we left,
    /// activates the one we arrived in, silences the arrived region's stray camera/listener (the core owns
    /// those), points the hold proxies at the live hold, and repositions the rig + re-points the control
    /// switcher's dock to the new region's <see cref="RegionAnchor"/>.
    ///
    /// PRAGMATIC SLICE — flagged for lead-architect. Region scenes are TOGGLED (their roots SetActive),
    /// not unloaded/reloaded, so nothing duplicates the persistent core and no scene re-runs its Awakes;
    /// the loader re-activates an already-loaded region instead of loading a second copy. The reposition +
    /// dock re-point is a pure static helper (<see cref="ApplyArrival"/>) so it's unit-testable without
    /// scene loading; the toggling/camera glue is play-mode only.
    /// </summary>
    public sealed class RegionTravelCoordinator : MonoBehaviour
    {
        [Tooltip("The persistent player (repositioned to a region's disembark point on arrival).")]
        [SerializeField] private Transform _player;
        [Tooltip("The persistent boat (repositioned to a region's arrival point on arrival).")]
        [SerializeField] private Transform _boat;
        [Tooltip("The persistent control switcher (its dock zone is re-pointed to the arrived region).")]
        [SerializeField] private ControlSwitcher _switcher;
        [Tooltip("The persistent hold (injected into each region's wharf hold proxy so selling hits the real hold).")]
        [SerializeField] private ShipHold _hold;

        private void OnEnable() => SceneManager.activeSceneChanged += OnActiveSceneChanged;
        private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        // ---- pure logic (unit-testable) -----------------------------------------------------

        /// <summary>
        /// Re-bind the persistent rig to a region: park the boat at the region's arrival point, place the
        /// (hidden, aboard) player at its disembark spot, re-point the control switcher's dock, and
        /// RE-ASSERT the control mode — WITHOUT resetting which mode you're in (you arrive still aboard /
        /// still on foot). Null-safe throughout.
        ///
        /// <para>The re-assert (<see cref="ControlSwitcher.ReassertControlMode"/>) is the fix for "boat
        /// controls stop after returning to a scene": the persistent switcher carries the mode across the
        /// hop, but the scene toggle never re-enabled the active boat's controller + input to match it, so
        /// a re-activated region (especially on a RETURN trip) could leave the helm dead. Re-asserting on
        /// every arrival makes boat OR foot control reliably live after EVERY return, for both the rowed
        /// Dory and the engine Punt; it also re-raises the camera signals so the view follows the right
        /// target. Idempotent.</para>
        /// </summary>
        public static void ApplyArrival(Transform player, Transform boat, ControlSwitcher switcher, RegionAnchor anchor)
            => ApplyArrival(player, boat, switcher, anchor, null);

        /// <summary>
        /// <inheritdoc cref="ApplyArrival(Transform, Transform, ControlSwitcher, RegionAnchor)"/>
        ///
        /// <para><paramref name="arrivalKey"/> names WHICH WAY IN the player took — the key the
        /// <c>RegionPassage</c> they crossed published. It selects among the region's
        /// <see cref="NamedArrival"/>s; null, empty or unmatched means the region's own single arrival,
        /// which is every region built before Nine Mile Creek's mainland.</para>
        ///
        /// <para><b>⚠ The DOCK keeps the region's own step-off spot, never the arrival's.</b>
        /// <c>SetDock</c> says where you land when you get OFF THE BOAT, and that is a property of the
        /// wharf — it is true for the whole visit, not just for the moment you turned up. Passing the
        /// per-passage point in here would mean walking in over the tidal bar re-pointed every later
        /// disembark at the bar landing, so a player who moored at the wharf would step off it onto a
        /// beach 400 m away. Where you ARRIVE and where you STEP ASHORE are two different questions and
        /// only the first one is the passage's to answer.</para>
        /// </summary>
        public static void ApplyArrival(Transform player, Transform boat, ControlSwitcher switcher,
                                        RegionAnchor anchor, string arrivalKey)
        {
            if (anchor == null) return;

            Transform arrivalPoint = anchor.ArrivalPointFor(arrivalKey);
            Transform landingPoint = anchor.DisembarkPointFor(arrivalKey);

            if (boat != null && arrivalPoint != null) boat.position = arrivalPoint.position;
            if (player != null && landingPoint != null) player.position = landingPoint.position;
            if (switcher != null)
            {
                switcher.SetDock(anchor.DockZone, anchor.DisembarkPoint);   // the WHARF's step-off — see above
                switcher.ReassertControlMode();   // re-enable the active boat/foot controller after the toggle
            }
        }

        // ---- play-mode glue (scene toggling — flagged for Unity verification) ---------------

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            // WHICH WAY IN, taken once. Consumed here whether or not the arrived region has named
            // arrivals, so a key can never survive its own crossing and steer the next one.
            string arrivalKey = GameServices.ConsumePendingArrivalKey();

            SetSceneRootsActive(previous, false);   // the region we left (persistents already DDOL'd out)
            SetSceneRootsActive(next, true);
            SilenceRegionCamera(next);              // the persistent core's camera/listener are the live ones
            BindHoldProxies(next);

            var anchor = RegionAnchor.ForScene(next);
            // A key the region does not answer to is a MIS-WIRE, not a style: the player lands at the
            // default and everything looks fine, which is exactly how a passage pointed at a renamed
            // arrival stays broken. Say so once, then fall back.
            if (anchor != null && !string.IsNullOrEmpty(arrivalKey) && !anchor.HasArrival(arrivalKey))
                Debug.LogWarning($"[RegionTravelCoordinator] Passage asked for arrival '{arrivalKey}' in " +
                                 $"'{anchor.RegionId}', which authors no such way in — landing at the " +
                                 "region's default instead.", anchor);
            ApplyArrival(_player, _boat, _switcher, anchor, arrivalKey);

            // The boat was just teleported to the arrival point — bring it to rest there so a residual
            // velocity from the previous region doesn't carry it off the mark once the controller re-enables.
            if (_boat != null)
            {
                var boatController = _boat.GetComponent<BoatController>();
                if (boatController != null) boatController.Stop();
            }
        }

        private static void SetSceneRootsActive(Scene scene, bool active)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var go in scene.GetRootGameObjects()) go.SetActive(active);
        }

        // A region scene authored for standalone review carries its own Camera + AudioListener; the
        // persistent core already has them, so quiet the region's to avoid a double render / listener warning.
        private static void SilenceRegionCamera(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var cam in go.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
                foreach (var listener in go.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
        }

        private void BindHoldProxies(Scene scene)
        {
            if (_hold == null || !scene.IsValid() || !scene.isLoaded) return;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var proxy in go.GetComponentsInChildren<PersistentHoldProxy>(true))
                    proxy.Bind(_hold);
        }

        /// <summary>Wire the coordinator in one call (tests / editor builder).</summary>
        public void Configure(Transform player, Transform boat, ControlSwitcher switcher, ShipHold hold)
        {
            _player = player; _boat = boat; _switcher = switcher; _hold = hold;
        }
    }
}
