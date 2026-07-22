using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Boats;

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
    /// <para><b>The deck is a rectangle in the DRAWN hull's frame.</b> The visible boat is the
    /// snap-directional picture: the sprite itself is screen-aligned, but the hull it DEPICTS points along
    /// the snapped facing (North art, East art, …) — so the walkable deck must be the hull rectangle
    /// (beam × length) ROTATED to that same drawn heading, not a fixed world-axis rect (which only matched
    /// the drawn deck when she happened to point North/South; at other headings the sprite could stand
    /// visibly off the hull — the owner's confinement bug). Each step is clamped in the deck frame of
    /// <see cref="DirectionalBoatSprite.DrawnHeadingDegrees"/> (the SNAPPED facing — the picture the player
    /// sees; a smooth-rotating hull uses its true heading; the transient wave-roll tilt is deliberately
    /// ignored so the deck doesn't slosh the player about). The player's world rotation is still stomped
    /// upright each LateUpdate (the DirectionalBoatSprite convention) so the fisher never spins with the
    /// hull. Bounds/speed are serialized tunables (rule 6); greybox.</para>
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
        [Tooltip("Centre of the walkable deck rectangle, as a DECK-FRAME offset from the boat's position " +
                 "(x abeam, y along the keel toward the bow). Rotated with the drawn facing so it stays put " +
                 "on the pictured hull at every heading.")]
        [SerializeField] private Vector2 _deckCenter = Vector2.zero;
        [Tooltip("Half-extents (m) of the walkable deck rectangle in the DECK FRAME: x = half the beam, " +
                 "y = half the length along the keel. Greybox: sized to the dory/skiff footprint.")]
        [SerializeField] private Vector2 _deckHalfExtents = new Vector2(0.7f, 1.6f);

        private Transform _boatRoot;
        private IBoatHullPresenter _hull;   // the drawn-facing read (resolved at Bind; null = smooth hull)

        /// <summary>The boat physics root the deck belongs to (set by the switcher on boarding).</summary>
        public Transform BoatRoot => _boatRoot;

        /// <summary>Centre offset of the deck rectangle (deck frame: x abeam, y toward the bow).</summary>
        public Vector2 DeckCenter => _deckCenter;

        /// <summary>Half-extents of the walkable deck rectangle (m; deck frame — beam × length).</summary>
        public Vector2 DeckHalfExtents => _deckHalfExtents;

        // ---- pure logic (unit-testable) -----------------------------------------------------

        /// <summary>Clamp a DECK-FRAME position (x abeam, y along the keel) onto the deck rectangle.</summary>
        public static Vector2 ClampToDeck(Vector2 boatRelative, Vector2 deckCenter, Vector2 deckHalfExtents)
            => new Vector2(
                Mathf.Clamp(boatRelative.x, deckCenter.x - deckHalfExtents.x, deckCenter.x + deckHalfExtents.x),
                Mathf.Clamp(boatRelative.y, deckCenter.y - deckHalfExtents.y, deckCenter.y + deckHalfExtents.y));

        /// <summary>
        /// One deck-frame step: move the deck-frame position by the input and keep it on the deck rectangle.
        /// Diagonals are magnitude-clamped so they aren't faster (the on-foot rule).
        /// </summary>
        public static Vector2 Step(Vector2 boatRelative, Vector2 moveInput, float speed, float dt,
                                   Vector2 deckCenter, Vector2 deckHalfExtents)
        {
            Vector2 next = boatRelative + Vector2.ClampMagnitude(moveInput, 1f) * (Mathf.Max(0f, speed) * dt);
            return ClampToDeck(next, deckCenter, deckHalfExtents);
        }

        /// <summary>
        /// A boat-relative WORLD offset expressed in the drawn hull's DECK FRAME (x abeam, y along the keel
        /// toward the bow), for a hull drawn at compass heading <paramref name="drawnHeadingDeg"/> (0 = North,
        /// 90 = East, clockwise — the project's bearing convention). The exact inverse of
        /// <see cref="DeckFrameToWorld"/>. Pure + static + deterministic.
        /// </summary>
        public static Vector2 WorldToDeckFrame(Vector2 worldOffset, float drawnHeadingDeg)
        {
            float rad = drawnHeadingDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(worldOffset.x * cos - worldOffset.y * sin,
                               worldOffset.x * sin + worldOffset.y * cos);
        }

        /// <summary>A deck-frame offset back in boat-relative WORLD axes (the inverse of
        /// <see cref="WorldToDeckFrame"/>): the deck frame's +Y maps to the drawn bow direction.</summary>
        public static Vector2 DeckFrameToWorld(Vector2 deckOffset, float drawnHeadingDeg)
        {
            float rad = drawnHeadingDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(deckOffset.x * cos + deckOffset.y * sin,
                               -deckOffset.x * sin + deckOffset.y * cos);
        }

        /// <summary>Clamp a boat-relative WORLD offset onto the deck rectangle of a hull DRAWN at
        /// <paramref name="drawnHeadingDeg"/> — into the deck frame, clamp, back out. This is the fix for
        /// the sprite standing off the pictured hull: the rectangle turns with the drawn facing.</summary>
        public static Vector2 ClampToDeckHeading(Vector2 worldRelative, float drawnHeadingDeg,
                                                 Vector2 deckCenter, Vector2 deckHalfExtents)
            => DeckFrameToWorld(
                   ClampToDeck(WorldToDeckFrame(worldRelative, drawnHeadingDeg), deckCenter, deckHalfExtents),
                   drawnHeadingDeg);

        /// <summary>
        /// One deck-walk step in WORLD axes (input is screen/world-axis, matching the on-foot walk), kept on
        /// the deck rectangle of the hull drawn at <paramref name="drawnHeadingDeg"/>. Clamps every step even
        /// with zero input, so the player is held to the drawn deck while the boat turns and rocks under them.
        /// </summary>
        public static Vector2 StepOnDeck(Vector2 worldRelative, Vector2 moveInput, float speed, float dt,
                                         float drawnHeadingDeg, Vector2 deckCenter, Vector2 deckHalfExtents)
        {
            Vector2 next = worldRelative + Vector2.ClampMagnitude(moveInput, 1f) * (Mathf.Max(0f, speed) * dt);
            return ClampToDeckHeading(next, drawnHeadingDeg, deckCenter, deckHalfExtents);
        }

        // ---- lifecycle ----------------------------------------------------------------------

        /// <summary>Bind the deck to a boat's PHYSICS ROOT (the switcher calls this on boarding). Resolves
        /// the hull through the presenter seam (<see cref="BoatHullPresenterHost.Resolve"/> — ADR 0022
        /// phase 4) so the clamp follows the DRAWN facing whichever path draws it: quantised for a sprite
        /// compass, continuous for a mesh hull. A boat with neither clamps to its true heading (its
        /// picture rotates with the hull).</summary>
        public void Bind(Transform boatRoot)
        {
            _boatRoot = boatRoot;
            _hull = boatRoot != null ? BoatHullPresenterHost.Resolve(boatRoot.gameObject) : null;
        }

        /// <summary>The compass heading of the hull picture on screen — the frame the deck rectangle lives
        /// in. Snap-directional boats give the quantized facing; a mesh hull (or no skin at all) the true
        /// physics heading.</summary>
        private float DrawnHeadingDegrees()
        {
            var hull = LiveHull();
            if (hull != null) return hull.DrawnHeadingDegrees();
            return _boatRoot != null
                ? DirectionalBoatSprite.HeadingDegreesFromBow(_boatRoot.up)
                : 0f;
        }

        /// <summary>
        /// The presenter to read THIS frame: the host's current one when the skinner has published one
        /// (so a hull swapped under the player's feet — the dev picker does exactly that — is never read
        /// through a stale presenter), else the one resolved at Bind. No allocation on the hot path.
        /// </summary>
        private IBoatHullPresenter LiveHull()
        {
            if (_boatRoot == null) return _hull;
            var host = _boatRoot.GetComponent<BoatHullPresenterHost>();
            return (host != null && host.Presenter != null) ? host.Presenter : _hull;
        }

        /// <summary>Snap the player onto the deck at a boat-relative WORLD-axis spot (clamped onto the
        /// drawn hull's deck rectangle) — used by the switcher when boarding lands you on deck / stepping
        /// back from the helm.</summary>
        public void SnapTo(Vector2 boatRelative)
        {
            if (_boatRoot == null) return;
            Vector2 clamped = ClampToDeckHeading(boatRelative, DrawnHeadingDegrees(), _deckCenter, _deckHalfExtents);
            transform.position = (Vector2)_boatRoot.position + clamped;
        }

        private void Update()
        {
            if (_boatRoot == null) return;
            Vector2 boatPos = _boatRoot.position;
            Vector2 relative = (Vector2)transform.position - boatPos;
            relative = StepOnDeck(relative, ReadInput(), _moveSpeed, Time.deltaTime,
                                  DrawnHeadingDegrees(), _deckCenter, _deckHalfExtents);
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
