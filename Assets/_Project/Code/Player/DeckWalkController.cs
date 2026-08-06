using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// Walking the boat's DECK (trap arc Build 5 — the on-deck control state). While the player is
    /// <c>ControlMode.OnDeck</c> this drives them around the walkable deck with the normal walk keys,
    /// riding the boat as it rocks and drifts; the <see cref="ControlSwitcher"/> enables it on boarding
    /// and disables it at the helm / ashore (it is dead otherwise — one controller owns movement per
    /// mode, the same discipline as <see cref="PlayerWalkController"/> vs the boat helm).
    ///
    /// <para><b>Riding the boat.</b> The switcher parents the player to the boat's PHYSICS ROOT (the
    /// Rigidbody2D body — never the counter-rotated visual child, which is stomped back to identity
    /// every LateUpdate and would swing anything parented to it out from under the player), so the
    /// boat's drift carries the player for free. The player's own Rigidbody2D is un-simulated while on
    /// deck (the hull collider must not fight the footprint collider), so this moves the TRANSFORM
    /// directly — greybox-simple, no physics duel.</para>
    ///
    /// <para><b>The deck is THIS hull's authored polygons (M2-37, data half).</b> Each measured hull
    /// carries its walkable areas as data — <see cref="BoatDeckDef"/>, imported straight from that rig's
    /// <c>docs/art/rigs/gameplay/&lt;rig&gt;.gameplay.json</c> sidecar and reached through
    /// <see cref="BoatDeckAreas"/> on the boat root. A dory's bilge floor is a 0.45 m centreline strip;
    /// a lobster boat's is a 3 m-wide cockpit plus a raised foredeck; a tanker's is nine areas including
    /// a catwalk. Before this they were all ONE 1.4 × 3.2 m rectangle tuned to the dory. A hull with no
    /// imported deck keeps that rectangle — absence is data, and an unmeasured hull is better served by
    /// a rough box than by no deck at all.</para>
    ///
    /// <para><b>Two frames, and the projection between them.</b> The polygons are HULL metres, so the
    /// clamp runs there: heading-independent, nothing to re-project per tick, and a step is a real
    /// distance on a real deck. The player's transform lives in SCREEN metres, so the clamped point is
    /// projected out through <see cref="DeckAreaMath.DeckToWorld"/> — the drawn heading's rotation
    /// (unchanged from the rectangle's), plus this artwork's own iso foreshortening and the lift from
    /// the deck's height. That squash is not optional: without it a 12 m hull pointing north lets the
    /// player walk 6.5 m up-screen past a bow the ¾ camera draws only 4.2 m away — the wake plume's old
    /// "way off to the stern" bug, in the deck clamp. The heading read is
    /// <see cref="IBoatHullPresenter.DrawnHeadingDegrees"/> (the SNAPPED facing — the picture the player
    /// sees; a smooth-rotating hull uses its true heading; the transient wave-roll tilt is deliberately
    /// ignored so the deck doesn't slosh the player about), and the elevation is that presenter's own
    /// <see cref="IBoatHullPresenter.BakeElevationDegrees"/> — per artwork, never a global. The player's
    /// world rotation is still stomped upright each LateUpdate (the DirectionalBoatSprite convention) so
    /// the fisher never spins with the hull.</para>
    ///
    /// <para>Input is dev-keyed via the New Input System (legacy Input throws at runtime), mirroring
    /// <see cref="PlayerWalkController.ReadInput"/>; a real InputService replaces it later (ui-ux).
    /// The clamp maths is pure + static so the bounds rule is EditMode-testable.</para>
    /// </summary>
    public sealed class DeckWalkController : MonoBehaviour
    {
        [Header("Deck walk (greybox tunables, rule 6)")]
        [Tooltip("Walk speed on the deck (m/s). A touch slower than ashore — you're stepping over gear. " +
                 "Metres of DECK per second: on a measured hull that is honest hull travel, so the ¾ " +
                 "camera shows less screen movement along the foreshortened axis, as it should.")]
        [SerializeField] private float _moveSpeed = 2.5f;
        [Tooltip("FALLBACK deck rectangle for a hull with NO imported BoatDeckDef: its centre, as a " +
                 "DECK-FRAME offset from the boat's position (x abeam, y along the keel toward the bow). " +
                 "Rotated with the drawn facing so it stays put on the pictured hull at every heading. " +
                 "Ignored the moment the hull carries authored polygons.")]
        [SerializeField] private Vector2 _deckCenter = Vector2.zero;
        [Tooltip("FALLBACK half-extents (m) of that rectangle in the DECK FRAME: x = half the beam, " +
                 "y = half the length along the keel. Greybox: sized to the dory/skiff footprint — which " +
                 "is precisely why a measured hull must not use it.")]
        [SerializeField] private Vector2 _deckHalfExtents = new Vector2(0.7f, 1.6f);

        private Transform _boatRoot;
        private IBoatHullPresenter _hull;   // the drawn-facing read (resolved at Bind; null = smooth hull)
        private BoatDeckDef _deck;          // the authored areas (resolved at Bind; null = the rectangle)

        // The player's position IN THE HULL FRAME — the authoritative state on the polygon path, because
        // the projection cannot be inverted from a screen offset alone (along-hull distance and height
        // land on the same screen axis). Re-seeded from the transform on Bind/enable/SnapTo, which are
        // the only places anything outside this component moves the player on deck.
        private Vector2 _deckLocal;
        private float _deckHeight;          // height above the keel of the area under _deckLocal (m)
        private int _deckArea = -1;         // which area holds it — the per-tick search hint (rule 7)

        /// <summary>How many passes <see cref="SeedDeckLocal"/> takes to invert the projection. Four:
        /// one is exact on a flat sole, and a sheer-following foredeck is within a millimetre by the
        /// fourth. Not a feel knob — an iteration count on a converging solve, and it runs on boarding
        /// only.</summary>
        private const int SeedPasses = 4;

        /// <summary>The boat physics root the deck belongs to (set by the switcher on boarding).</summary>
        public Transform BoatRoot => _boatRoot;

        /// <summary>Centre offset of the FALLBACK deck rectangle (deck frame: x abeam, y toward the bow).</summary>
        public Vector2 DeckCenter => _deckCenter;

        /// <summary>Half-extents of the FALLBACK deck rectangle (m; deck frame — beam × length).</summary>
        public Vector2 DeckHalfExtents => _deckHalfExtents;

        /// <summary>The authored deck this walk is clamping to, or null when it is on the rectangle.</summary>
        public BoatDeckDef Deck => _deck;

        /// <summary>The player's position in the HULL frame (x abeam, y toward the bow; metres) — the
        /// stance the fight's deck-angle term grades, published every tick through
        /// <see cref="DeckStance"/>.</summary>
        public Vector2 DeckLocalPosition => _deckLocal;

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
        ///
        /// <para>This is the PLAN-VIEW transform — the rectangle path's, and the shape the Fishing lane's
        /// parity test pins. A measured hull goes through <see cref="DeckAreaMath.WorldToDeck"/>, which is
        /// this rotation plus that artwork's foreshortening.</para>
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

        /// <summary>
        /// One deck-walk step in the HULL FRAME, for a hull with authored polygons. The screen-axis input
        /// becomes the deck direction that DRAWS along it (so "press up, go up-screen" survives the
        /// foreshortening at every heading), the step is <paramref name="speed"/> metres of DECK per
        /// second, and the result is clamped onto the walkable areas. Clamps even with no input, so a
        /// turning hull keeps the player aboard. Allocation-free; the def is the only thing it reads.
        /// </summary>
        public static Vector2 StepOnDeckPolygon(Vector2 deckLocal, Vector2 moveInput, float speed, float dt,
                                                float drawnHeadingDeg, float bakeElevationDegrees,
                                                BoatDeckDef deck, ref int areaHint, out float heightMeters)
        {
            Vector2 next = deckLocal;
            float mag = Mathf.Min(1f, moveInput.magnitude);
            if (mag > 1e-4f)
            {
                Vector2 dir = DeckAreaMath.WorldDirectionToDeck(moveInput, drawnHeadingDeg, bakeElevationDegrees);
                if (dir.sqrMagnitude > 1e-10f)
                    next += dir.normalized * (mag * Mathf.Max(0f, speed) * Mathf.Max(0f, dt));
            }

            if (deck == null) { heightMeters = 0f; return next; }
            return deck.ClampToWalkable(next, ref areaHint, out heightMeters);
        }

        // ---- lifecycle ----------------------------------------------------------------------

        /// <summary>Bind the deck to a boat's PHYSICS ROOT (the switcher calls this on boarding). Resolves
        /// the hull through the presenter seam (<see cref="BoatHullPresenterHost.Resolve"/> — ADR 0022
        /// phase 4) so the clamp follows the DRAWN facing whichever path draws it: quantised for a sprite
        /// compass, continuous for a mesh hull. A boat with neither clamps to its true heading (its
        /// picture rotates with the hull). Also resolves her authored deck, and seats the player's
        /// hull-frame position from wherever they are standing right now.</summary>
        public void Bind(Transform boatRoot)
        {
            _boatRoot = boatRoot;
            _hull = boatRoot != null ? BoatHullPresenterHost.Resolve(boatRoot.gameObject) : null;
            _deck = boatRoot != null ? BoatDeckAreas.Resolve(boatRoot.gameObject) : null;
            _deckArea = -1;
            SeedDeckLocalFromTransform();
        }

        /// <summary>The compass heading of the hull picture on screen — the frame the deck lives in.
        /// Snap-directional boats give the quantized facing; a mesh hull (or no skin at all) the true
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

        /// <summary>The deck areas to clamp against THIS frame — the same live-read discipline as
        /// <see cref="LiveHull"/>, for the same reason: the dev hull picker changes the boat under the
        /// player's feet, and a stale polygon set would strand them off the new hull.</summary>
        private BoatDeckDef LiveDeck()
        {
            if (_boatRoot == null) return _deck;
            BoatDeckDef live = BoatDeckAreas.Resolve(_boatRoot.gameObject);
            return live != null ? live : _deck;
        }

        /// <summary>The artwork's own bake elevation (40° for every iso rig, 90° = a plan view for art
        /// that was never baked by a camera). Per artwork, read off the presenter — never a constant.</summary>
        private float BakeElevationDegrees()
        {
            var hull = LiveHull();
            return hull != null ? hull.BakeElevationDegrees : DeckAreaMath.PlanViewElevationDegrees;
        }

        /// <summary>Snap the player onto the deck at a boat-relative WORLD-axis spot (clamped onto the
        /// drawn hull's walkable area) — used by the switcher when boarding lands you on deck / stepping
        /// back from the helm.</summary>
        public void SnapTo(Vector2 boatRelative)
        {
            if (_boatRoot == null) return;
            Vector2 boatPos = _boatRoot.position;
            float heading = DrawnHeadingDegrees();
            BoatDeckDef deck = LiveDeck();

            if (deck != null && deck.HasWalkableDeck())
            {
                float elevation = BakeElevationDegrees();
                SeedDeckLocal(boatRelative, heading, elevation, deck);
                transform.position = boatPos + DeckAreaMath.DeckToWorld(_deckLocal, _deckHeight,
                                                                        heading, elevation);
                return;
            }

            Vector2 clamped = ClampToDeckHeading(boatRelative, heading, _deckCenter, _deckHalfExtents);
            _deckLocal = WorldToDeckFrame(clamped, heading);
            _deckHeight = 0f;
            _deckArea = -1;
            transform.position = boatPos + clamped;
        }

        private void OnEnable() => SeedDeckLocalFromTransform();

        private void Update()
        {
            if (_boatRoot == null) return;
            Vector2 boatPos = _boatRoot.position;
            float drawnHeading = DrawnHeadingDegrees();
            BoatDeckDef deck = LiveDeck();
            Vector2 input = ReadInput();

            Vector2 relative, stanceCenter, stanceHalfExtents;

            if (deck != null && deck.HasWalkableDeck())
            {
                // THE MEASURED PATH: step and clamp in the hull's own metres, then project the one
                // resulting point onto the drawn hull. The polygon never moves, so a heading change
                // costs nothing (rule 7).
                float elevation = BakeElevationDegrees();
                _deckLocal = StepOnDeckPolygon(_deckLocal, input, _moveSpeed, Time.deltaTime,
                                               drawnHeading, elevation, deck, ref _deckArea, out _deckHeight);
                relative = DeckAreaMath.DeckToWorld(_deckLocal, _deckHeight, drawnHeading, elevation);
                stanceCenter = deck.WalkCenter;
                stanceHalfExtents = deck.WalkHalfExtents;
            }
            else
            {
                // THE GREYBOX FALLBACK, unchanged: an unmeasured hull keeps the world-axis step and the
                // un-foreshortened rectangle it has always had. No data, no better answer.
                relative = (Vector2)transform.position - boatPos;
                relative = StepOnDeck(relative, input, _moveSpeed, Time.deltaTime,
                                      drawnHeading, _deckCenter, _deckHalfExtents);
                _deckLocal = WorldToDeckFrame(relative, drawnHeading);
                _deckHeight = 0f;
                _deckArea = -1;
                stanceCenter = _deckCenter;
                stanceHalfExtents = _deckHalfExtents;
            }

            transform.position = boatPos + relative;

            // Publish the LIVE deck frame through Core (DeckStance — Rod Fishing v2 §4): hull position,
            // the drawn facing, the walkable bounds and where the angler actually stands in them,
            // re-published every tick so the drifting, weathervaning hull reaches consumers (the
            // deck-angle fight term) at the same frame the player is clamped to. The bounds are now THIS
            // hull's — a dragger grades her rails as a dragger, not as a dory — and the stance carries
            // the angler's hull-frame position outright, so no consumer has to re-invert the projection.
            // Publisher-owned: cleared the moment deck-walking ends (OnDisable).
            DeckStance.Publish(this, new DeckStanceState(boatPos, drawnHeading, stanceCenter,
                                                         stanceHalfExtents, _deckLocal));
        }

        /// <summary>Deck-walking ended (helm taken / stepped ashore / teardown) — the player no longer
        /// stands on a deck, so the published stance goes with them (a dock cast must read NO stance:
        /// the deck-angle term's off-contract).</summary>
        private void OnDisable() => DeckStance.Clear(this);

        private void LateUpdate()
        {
            // The player rides the ROTATING physics root but must stay screen-upright (the picture the
            // player sees is the counter-rotated snap-directional visual) — stomp world rotation, the
            // DirectionalBoatSprite convention.
            if (transform.rotation != Quaternion.identity) transform.rotation = Quaternion.identity;
        }

        /// <summary>Read the player's current world position back into the hull frame — done on
        /// Bind/enable, when whatever put them there was not this component.</summary>
        private void SeedDeckLocalFromTransform()
        {
            if (_boatRoot == null) return;
            Vector2 relative = (Vector2)transform.position - (Vector2)_boatRoot.position;
            float heading = DrawnHeadingDegrees();
            BoatDeckDef deck = LiveDeck();

            if (deck != null && deck.HasWalkableDeck())
            {
                SeedDeckLocal(relative, heading, BakeElevationDegrees(), deck);
                return;
            }
            _deckLocal = WorldToDeckFrame(relative, heading);
            _deckHeight = 0f;
            _deckArea = -1;
        }

        /// <summary>
        /// Recover a hull-frame position from a boat-relative WORLD offset, on a hull with real areas.
        ///
        /// <para>The projection folds along-hull distance and deck height onto the same screen axis, so
        /// there is no closed-form inverse: this is a fixed-point iteration. Guess height 0, clamp to
        /// find which area that answer lands on, re-read the offset at THAT area's height, repeat. A
        /// flat sole is exact on the first pass because its height does not depend on where you stand;
        /// a sheer-following foredeck needs the iteration, because there the height varies fastest along
        /// the very axis the projection folded it into — two passes leaves you about half a metre out
        /// near the stemhead, four converges to millimetres. It only ever runs on boarding / a snap,
        /// never per tick, so the extra passes cost nothing that matters.</para>
        /// </summary>
        private void SeedDeckLocal(Vector2 worldRelative, float heading, float elevation, BoatDeckDef deck)
        {
            float height = 0f;
            for (int pass = 0; pass < SeedPasses; pass++)
            {
                Vector2 local = DeckAreaMath.WorldToDeck(worldRelative, height, heading, elevation);
                _deckLocal = deck.ClampToWalkable(local, ref _deckArea, out height);
            }
            _deckHeight = height;
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
