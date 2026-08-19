using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The wardrobe in the player's room</b> — placed, interactable, and honest about being early.
    /// Pressing it publishes <see cref="WardrobeRequested"/> and says there is nothing to change into
    /// yet; the character-customization lane subscribes to that signal when it lands and this component
    /// does not change.
    ///
    /// <para><b>Why ship a fixture with no feature behind it.</b> Because the two halves are separate
    /// jobs and the seam between them is cheaper to build now than to retrofit: the wardrobe's PLACE in
    /// the room (which the owner will judge by walking to it) and the wardrobe's CONTENTS (a whole
    /// customization UI) have nothing to do with each other. Shipping the prop means the room is
    /// finished and the next PR is purely its own feature — and a signal published into an empty room
    /// is a valid state, exactly as an empty <see cref="Interactables"/> registry is.</para>
    ///
    /// <para><b>The refusal is the feature, for now.</b> P5's cozy-with-teeth applies to being told no:
    /// a wardrobe that opened onto nothing would read as broken, and one that says you have only what
    /// you stand up in reads as the opening.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorWardrobe : MonoBehaviour, IInteractable
    {
        /// <summary>What the player is told while there is nothing to change into. Public so the test
        /// asserts the contract rather than a re-typed literal.</summary>
        public const string NothingYetText = "Only what you stand up in, for now.";

        [Tooltip("Stable id — unique among live interactables, and what a later customization consumer " +
                 "matches on to tell the player's own wardrobe from one in a shop.")]
        [SerializeField] private string _id = "fixture.interior.wardrobe";

        [Tooltip("How close (m) you must stand.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.2f;

        // ---- the interact seam ---------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <inheritdoc/>
        public int Priority => InteractPriority.Fixture;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Always available. "Nothing to change into" is a refusal with a line, not an absence
        /// — see <see cref="IInteractable.IsAvailable"/>.</summary>
        public bool IsAvailable => true;

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => Open();

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Wire this wardrobe from the builder.</summary>
        public void Configure(string id, float reachMeters)
        {
            _id = id;
            _reachMeters = Mathf.Max(0f, reachMeters);
        }

        /// <summary>Open it: raise the signal the customization lane will answer, and say the honest
        /// thing meanwhile. Public so tests drive it without input.</summary>
        public void Open()
        {
            EventBus.Publish(new WardrobeRequested(_id));
            EventBus.Publish(new DevNotice(NothingYetText));
        }
    }
}
