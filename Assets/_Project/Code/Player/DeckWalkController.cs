using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// Walking the boat's DECK (trap arc Build 5 — the on-deck control state). While the player is
    /// <c>ControlMode.OnDeck</c> this drives them around a small bounded deck area with the normal walk
    /// keys, riding the boat as it rocks and drifts; the <see cref="ControlSwitcher"/> enables it on
    /// boarding and disables it at the helm / ashore (it is dead otherwise — one controller owns
    /// movement per mode, the same discipline as <see cref="PlayerWalkController"/> vs the boat helm).
    ///
    /// <para><b>Riding the boat.</b> The switcher parents the player to the boat's PHYSICS ROOT (the
    /// Rigidbody2D body — never the counter-rotated visual child, which is stomped back to identity
    /// every LateUpdate and would swing anything parented to it out from under the player), so the
    /// boat's drift carries the player for free. The player's own Rigidbody2D is un-simulated while on
    /// deck (the hull collider must not fight the footprint collider), so this moves the TRANSFORM
    /// directly — greybox-simple, no physics duel.</para>
    ///
    /// <para><b>The deck itself is a screen-aligned rectangle around the hull's centre.</b> The visible
    /// boat is the snap-directional picture (screen-aligned; the physics body's true rotation is hidden),
    /// so the walkable deck is clamped in WORLD axes around the boat's position — matching what the
    /// player sees, not the invisible physics heading. The player's world rotation is likewise stomped
    /// upright each LateUpdate (the DirectionalBoatSprite convention) so the fisher never spins with the
    /// hull. Bounds/speed are serialized tunables (rule 6); it does not need to look like anything yet
    /// (owner: greybox).</para>
    ///
    /// <para>Input is dev-keyed via the New Input System (legacy Input throws at runtime), mirroring
    /// <see cref="PlayerWalkController.ReadInput"/>; a real InputService replaces it later (ui-ux).
    /// The clamp maths is pure + static so the bounds rule is EditMode-testable.</para>
    /// </summary>
    public sealed class DeckWalkController : MonoBehaviour
    {
        [Header("Deck walk (greybox tunables, rule 6)")]
        [Tooltip("Walk speed on the deck (m/s). A touch slower than ashore — you're stepping over gear.")]
        [SerializeField] private float _moveSpeed = 2.5f;
        [Tooltip("Centre of the walkable deck rectangle, as a world-axis offset from the boat's position " +
                 "(the deck matches the screen-aligned boat picture, not the hidden physics heading).")]
        [SerializeField] private Vector2 _deckCenter = Vector2.zero;
        [Tooltip("Half-extents (m) of the walkable deck rectangle. Greybox: a simple bounded area sized " +
                 "to the dory/skiff footprint.")]
        [SerializeField] private Vector2 _deckHalfExtents = new Vector2(0.7f, 1.6f);

        private Transform _boatRoot;

        /// <summary>The boat physics root the deck belongs to (set by the switcher on boarding).</summary>
        public Transform BoatRoot => _boatRoot;

        /// <summary>Centre offset of the deck rectangle (world axes, from the boat position).</summary>
        public Vector2 DeckCenter => _deckCenter;

        /// <summary>Half-extents of the walkable deck rectangle (m).</summary>
        public Vector2 DeckHalfExtents => _deckHalfExtents;

        // ---- pure logic (unit-testable) -----------------------------------------------------

        /// <summary>Clamp a boat-relative (world-axis) position onto the walkable deck rectangle.</summary>
        public static Vector2 ClampToDeck(Vector2 boatRelative, Vector2 deckCenter, Vector2 deckHalfExtents)
            => new Vector2(
                Mathf.Clamp(boatRelative.x, deckCenter.x - deckHalfExtents.x, deckCenter.x + deckHalfExtents.x),
                Mathf.Clamp(boatRelative.y, deckCenter.y - deckHalfExtents.y, deckCenter.y + deckHalfExtents.y));

        /// <summary>
        /// One deck-walk step: move the player's boat-relative position by the input and keep it on the
        /// deck. Diagonals are magnitude-clamped so they aren't faster (the on-foot rule).
        /// </summary>
        public static Vector2 Step(Vector2 boatRelative, Vector2 moveInput, float speed, float dt,
                                   Vector2 deckCenter, Vector2 deckHalfExtents)
        {
            Vector2 next = boatRelative + Vector2.ClampMagnitude(moveInput, 1f) * (Mathf.Max(0f, speed) * dt);
            return ClampToDeck(next, deckCenter, deckHalfExtents);
        }

        // ---- lifecycle ----------------------------------------------------------------------

        /// <summary>Bind the deck to a boat's PHYSICS ROOT (the switcher calls this on boarding).</summary>
        public void Bind(Transform boatRoot) => _boatRoot = boatRoot;

        /// <summary>Snap the player onto the deck at a boat-relative spot (clamped to the bounds) —
        /// used by the switcher when boarding lands you on deck / stepping back from the helm.</summary>
        public void SnapTo(Vector2 boatRelative)
        {
            if (_boatRoot == null) return;
            Vector2 clamped = ClampToDeck(boatRelative, _deckCenter, _deckHalfExtents);
            transform.position = (Vector2)_boatRoot.position + clamped;
        }

        private void Update()
        {
            if (_boatRoot == null) return;
            Vector2 boatPos = _boatRoot.position;
            Vector2 relative = (Vector2)transform.position - boatPos;
            relative = Step(relative, ReadInput(), _moveSpeed, Time.deltaTime, _deckCenter, _deckHalfExtents);
            transform.position = boatPos + relative;
        }

        private void LateUpdate()
        {
            // The player rides the ROTATING physics root but must stay screen-upright (the picture the
            // player sees is the counter-rotated snap-directional visual) — stomp world rotation, the
            // DirectionalBoatSprite convention.
            if (transform.rotation != Quaternion.identity) transform.rotation = Quaternion.identity;
        }

        private static Vector2 ReadInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            Vector2 m = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) m.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) m.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) m.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) m.x -= 1f;
            return m;
        }

        /// <summary>Wire the deck in one call (tests / editor builder).</summary>
        public void Configure(float moveSpeed, Vector2 deckCenter, Vector2 deckHalfExtents)
        {
            _moveSpeed = moveSpeed;
            _deckCenter = deckCenter;
            _deckHalfExtents = deckHalfExtents;
        }
    }
}
