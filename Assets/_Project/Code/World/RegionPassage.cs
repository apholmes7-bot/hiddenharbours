using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A passage between regions (VS-22): a trigger the player/boat enters to travel to a target
    /// <see cref="RegionDef"/> via the <see cref="RegionSceneLoader"/>. The Greywick scene places one
    /// of these at the harbour mouth as the "back to Coddle Cove" hop.
    ///
    /// SCOPE: a stub for the Cove↔Greywick transition. The matching Cove→Greywick passage belongs in
    /// the Cove (greybox) scene, which this task must not touch — see the TODO; placing it there needs
    /// a GreyboxBuilder change. Triggering is forgiving (P5): any collider that enters fires it, since
    /// the only mover in the greybox is the player/boat. A real version gates by an actor tag + an
    /// on-screen "sail to Port Greywick" prompt (ui-ux). Needs a trigger Collider2D on the same
    /// GameObject (the builder adds a BoxCollider2D); <see cref="Reset"/> flags it as a trigger.
    ///
    /// <para><b>Re-fire guard (the helm-drop fix).</b> A passage must fire <i>once per genuine crossing</i>,
    /// never on the boat that just arrived. Two ways it could double-fire and drop the helm — and every fire
    /// re-runs travel, which teleports + <c>Stop()</c>s the boat and re-binds control (a beat of dead helm,
    /// then recovery, exactly the owner's "controls cut out crossing the boundary"): (1) the boat <b>lingers
    /// in or nudges back into</b> the wide, forgiving trigger band while crossing; (2) a re-activated region's
    /// passage <see cref="OnTriggerEnter2D"/> <b>re-fires on the already-overlapping boat</b> when its scene
    /// root is toggled back on — Unity re-raises trigger-enter for colliders already inside on
    /// <c>SetActive(true)</c> (the scene-toggle "bounce" flagged after #74). Three layers stop it, so the helm
    /// stays live crossing the shore↔open-water boundary for BOTH the rowed Dory and the engine Punt:
    /// a <b>leave-then-enter latch</b> (it won't re-arm until the body has left and come back), a
    /// <b>cooldown window</b> after a fire (debounce), and <b>priming OFF on enable</b> (a freshly
    /// activated/arrived region starts un-primed, so a boat sitting in the band can't fire it until it
    /// genuinely leaves and re-enters). The latch decision is a pure, EditMode-testable function
    /// (<see cref="ShouldFire"/>); deterministic, owner-tunable, nothing saved (CLAUDE.md rules 5/6).</para>
    /// </summary>
    public sealed class RegionPassage : MonoBehaviour
    {
        [Tooltip("Where this passage leads.")]
        [SerializeField] private RegionDef _target;
        [Tooltip("The loader that performs the additive scene load.")]
        [SerializeField] private RegionSceneLoader _loader;

        [Header("Re-fire guard (keeps the helm live across the boundary)")]
        [Tooltip("Seconds after a crossing during which this passage ignores further trigger entries — a " +
                 "debounce so the boat lingering in / nudging back into the wide band doesn't re-fire travel " +
                 "(which would teleport + Stop the boat and drop the helm for a beat). Owner-tunable.")]
        [SerializeField] private float _reentryCooldownSeconds = 1.5f;

        public RegionDef Target => _target;

        // The leave-then-enter latch: set true when the passage fires, so it WON'T fire again on the same
        // body lingering in / nudging back into the band — only a real OnTriggerExit2D re-arms it. Guards the
        // same-region "boat sat on the wide band" re-fire.
        private bool _consumed;
        // Real-time stamp of the last fire AND of (re-)activation; trigger-entries within
        // _reentryCooldownSeconds of it are ignored. Guards the scene-toggle bounce (the re-activated region
        // re-raises trigger-enter on the still-overlapping boat right after OnEnable stamps this).
        private float _lastActivateTime = float.NegativeInfinity;

        private void Reset()
        {
            // Authoring convenience: a passage is a trigger.
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnEnable()
        {
            // (Re-)activation stamps the cooldown so any IMMEDIATE post-activation trigger-enter is debounced.
            // This is the timing-independent half of the scene-toggle-bounce guard: when a region's scene root
            // is toggled back on with the boat already overlapping this band, Unity re-raises OnTriggerEnter2D
            // on the next physics step — landing inside this fresh cooldown window, so it's swallowed. We start
            // PRIMED (not consumed) so a GENUINE approach seconds later (past the cooldown) still fires; the
            // latch (set by Activate, cleared by a real OnTriggerExit2D) is the other half for the same-region
            // lingering case. Defensive against either the source OR a re-activated region re-firing.
            _consumed = false;
            _lastActivateTime = Time.unscaledTime;
        }

        /// <summary>
        /// Pure re-fire decision (EditMode-testable without colliders/scenes): a trigger-enter fires only
        /// when the passage is PRIMED (not waiting for a leave) AND the entry is outside the cooldown window
        /// after the last fire. This is the whole guard against the just-arrived boat re-firing the passage
        /// and dropping the helm.
        /// </summary>
        /// <param name="consumed">True while the passage is waiting for the body to leave before re-arming.</param>
        /// <param name="now">Current (unscaled) time.</param>
        /// <param name="lastActivateTime">When the passage last fired.</param>
        /// <param name="cooldownSeconds">The debounce window after a fire.</param>
        public static bool ShouldFire(bool consumed, float now, float lastActivateTime, float cooldownSeconds)
            => !consumed && (now - lastActivateTime) >= cooldownSeconds;

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Guarded: only a GENUINE re-entry (after leaving) outside the cooldown takes the passage.
            if (ShouldFire(_consumed, Time.unscaledTime, _lastActivateTime, _reentryCooldownSeconds))
                Activate();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            // The body left the band — re-arm so the NEXT genuine entry can fire. (We don't track per-collider
            // identity in the greybox: the only mover is the player/boat, so a single latch is enough.)
            _consumed = false;
        }

        /// <summary>
        /// Take the passage now (also callable from a future Interact prompt / dev input). Latches the
        /// re-fire guard (consumed + stamps the time) so the crossing fires exactly once until the boat
        /// leaves and re-enters / the cooldown elapses — the just-arrived boat never re-fires it.
        /// </summary>
        public void Activate()
        {
            if (_loader == null || _target == null)
            {
                Debug.LogWarning("[RegionPassage] Not wired (need a target region + a loader).", this);
                return;
            }

            // Latch BEFORE travelling: travel toggles scenes / re-activates this passage, which would
            // otherwise immediately re-enter on the still-overlapping boat. Consumed + the timestamp make
            // the guard hold across that bounce.
            _consumed = true;
            _lastActivateTime = Time.unscaledTime;
            _loader.Travel(_target);
        }

        // TODO (Cove side): place the matching Coddle Cove -> Port Greywick passage in the cove scene.
        // That lives in GreyboxBuilder (out of scope for VS-22), so it's left as a wiring note.
    }
}
