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
    /// </summary>
    public sealed class RegionPassage : MonoBehaviour
    {
        [Tooltip("Where this passage leads.")]
        [SerializeField] private RegionDef _target;
        [Tooltip("The loader that performs the additive scene load.")]
        [SerializeField] private RegionSceneLoader _loader;

        public RegionDef Target => _target;

        private void Reset()
        {
            // Authoring convenience: a passage is a trigger.
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => Activate();

        /// <summary>Take the passage now (also callable from a future Interact prompt / dev input).</summary>
        public void Activate()
        {
            if (_loader == null || _target == null)
            {
                Debug.LogWarning("[RegionPassage] Not wired (need a target region + a loader).", this);
                return;
            }
            _loader.Travel(_target);
        }

        // TODO (Cove side): place the matching Coddle Cove -> Port Greywick passage in the cove scene.
        // That lives in GreyboxBuilder (out of scope for VS-22), so it's left as a wiring note.
    }
}
