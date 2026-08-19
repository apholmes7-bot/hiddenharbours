using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The stairwell</b> — the one way between a building's storeys, as a candidate for the single
    /// interact verb. Press it at the foot of the stairs and the ground floor gives way to the storey
    /// above; press it at the head of them and you come back down.
    ///
    /// <para><b>Two fixtures, not one.</b> A stair going up and a stair coming down are separate
    /// components on the same footprint coordinate, each declaring the level it works FROM
    /// (<see cref="FromLevel"/>). Only one of the pair is ever <see cref="IsAvailable"/>, so the
    /// resolver never has to choose between them and the prompt can never read "go up" while you are
    /// already up. A single toggling fixture would have been fewer objects and one more thing that can
    /// be in the wrong state.</para>
    ///
    /// <para><b>Nothing here moves the player.</b> Both stairs stand at the same point of the shared
    /// footprint, so the occupant is already where the other storey's stair is — the swap alone puts
    /// them at the top. See <see cref="BuildingInterior.TryGoToLevel"/> for why that is the whole trick.</para>
    ///
    /// <para><b>Interact, not walk-on.</b> A trigger volume on the stairwell would fire on any pass
    /// across it — including the one where you were walking to the window — and a storey that changes
    /// under you because you clipped a corner is the least cozy thing a house can do (P5). A press is
    /// unambiguous, it costs the player nothing, and it needs no new key: the verb is already bound and
    /// the dev-key ledger is spent A–Z.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorStair : MonoBehaviour, IInteractable
    {
        [Tooltip("The building whose storeys this stair moves between. Required — a stair with no " +
                 "interior is inert rather than broken.")]
        [SerializeField] private BuildingInterior _interior;

        [Tooltip("Stable id. Must be unique among live interactables, so the up and down halves of one " +
                 "stairwell need different ids (…stair_up / …stair_down).")]
        [SerializeField] private string _id = "fixture.interior.stair";

        [Tooltip("Which storey this half works FROM. The up-stair is 0, the down-stair is 1.")]
        [SerializeField, Min(0)] private int _fromLevel;

        [Tooltip("Which storey it lands you on.")]
        [SerializeField, Min(0)] private int _toLevel = 1;

        [Tooltip("How close (m) you must stand. Tighter than a shore fixture's reach on purpose: a " +
                 "stairwell is a specific place in a small room, and a generous radius would have it " +
                 "outranking the bed beside it.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.2f;

        [Tooltip("What the notice says on arriving. Empty for silence.")]
        [SerializeField] private string _arrivalText = "";

        /// <summary>Which storey this half works from — see <see cref="_fromLevel"/>.</summary>
        public int FromLevel => _fromLevel;

        /// <summary>Which storey it lands you on.</summary>
        public int ToLevel => _toLevel;

        // ---- the interact seam ---------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <inheritdoc/>
        public int Priority => InteractPriority.Fixture;

        /// <summary>On foot only. You cannot take a stair from a boat's deck, and the bit for it does
        /// not exist to be spent on rooms.</summary>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <summary>Facing is not required: you climb a stair by standing at it. Matches every other
        /// fixture you operate by walking up to (see <c>WetBucketPoint</c>).</summary>
        public bool RequiresFacing => false;

        /// <summary>
        /// Available only when the occupant is INSIDE this building and standing on the storey this
        /// half serves.
        ///
        /// <para>This is the gate that keeps the pair honest, and it is a genuine "not a thing to act on
        /// right now" rather than a refusal with a message — the other half of the stairwell is a metre
        /// away and is the answer. Note it also goes false the moment you step out of the house, so a
        /// stair inside a building you are not in never wins the press through a wall.</para>
        /// </summary>
        public bool IsAvailable =>
            _interior != null && _interior.IsInside && _interior.HasUpperLevel &&
            _interior.Level == _fromLevel;

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => Climb();

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Wire this stair from the builder. Public so the placement pass never has to reach a
        /// serialized field, and so a test can stand one up without a scene.</summary>
        public void Configure(BuildingInterior interior, string id, int fromLevel, int toLevel,
                              float reachMeters, string arrivalText)
        {
            _interior = interior;
            _id = id;
            _fromLevel = Mathf.Max(0, fromLevel);
            _toLevel = Mathf.Max(0, toLevel);
            _reachMeters = Mathf.Max(0f, reachMeters);
            _arrivalText = arrivalText ?? "";
        }

        /// <summary>Take the stair. Returns whether the storey actually changed. Public so tests drive
        /// it without input.</summary>
        public bool Climb()
        {
            if (_interior == null) return false;
            if (!_interior.TryGoToLevel(_toLevel)) return false;

            if (!string.IsNullOrEmpty(_arrivalText))
                EventBus.Publish(new DevNotice(_arrivalText));
            return true;
        }
    }
}
