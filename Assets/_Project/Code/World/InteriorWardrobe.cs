using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The wardrobe in the player's room</b> — placed, interactable, and now answered. Pressing it
    /// publishes <see cref="WardrobeRequested"/>; the UI lane's picker is subscribed and opens beside it
    /// with what the player owns.
    ///
    /// <para><b>Why this component still knows nothing about clothes.</b> It names WHICH wardrobe and
    /// nothing else. What is in it, what the player is wearing, how a colour reaches a pixel and what
    /// happens to the save are four separate answers living in three other modules, and World may not
    /// reference any of them (rule 4). So the fixture's whole job is the signal — which is why the
    /// customization work landed with barely a line of this file changing, and why a wardrobe in a shop,
    /// an inn or a boat's cabin is this same component with a different id.</para>
    ///
    /// <para><b>The refusal moved, it did not go away.</b> This used to say "only what you stand up in"
    /// unconditionally, because there was genuinely nothing to change into. That line now belongs to the
    /// picker (<c>WardrobeStrings.NothingToWear</c>), which says it only when it is TRUE — a wardrobe
    /// asset that holds nothing. Keeping a copy here would mean a fixture asserting something about
    /// content it cannot see, and two places to fix when it stopped being so.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorWardrobe : MonoBehaviour, IInteractable
    {
        [Tooltip("Stable id — unique among live interactables, and what the picker matches on to tell " +
                 "the player's own wardrobe from one in a shop. It is also how the picker finds this " +
                 "fixture in the world to hang its panel beside, so it must name a REGISTERED " +
                 "interactable, not merely be unique.")]
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

        /// <summary>Always available. Whether there is anything inside is the picker's question, and it
        /// answers that out loud — see <see cref="IInteractable.IsAvailable"/>.</summary>
        public bool IsAvailable => true;

        /// <summary>What it offers, not what is in it — an empty wardrobe still opens, and says so out
        /// loud when it does (see <see cref="IsAvailable"/>).</summary>
        public string VerbLabel => "Open the wardrobe";

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

        /// <summary>Open it: raise the signal the picker answers. Public so tests drive it without
        /// input.</summary>
        public void Open() => EventBus.Publish(new WardrobeRequested(_id));
    }
}
