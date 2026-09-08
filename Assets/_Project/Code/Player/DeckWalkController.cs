using UnityEngine;
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
    /// <para>⚠️ <b>That stomp covers the DECK only</b>, because the switcher disables this controller at the
    /// helm — which is how the drawn pilot came to inherit the hull's rotation and lie over further with
    /// every degree she turned (owner playtest 2026-08-07). The invariant now belongs to
    /// <c>ControlSwitcher.LateUpdate</c>, which holds it in every aboard mode; this one stays as the deck's
    /// own guarantee, and the two agree because both write the same identity.</para>
    ///
    /// <para>Input arrives as <see cref="DeckIntents"/> (ADR 0043), read once per frame from
    /// <see cref="DeckInputSource"/> — the bindings asset by default (<see cref="DeviceDeckIntentSource"/>,
    /// the same four letters and four arrows this controller used to poll inline), or a
    /// <see cref="HeldDeckIntents"/> handed in through <see cref="ConfigureDeckInput"/>. The clamp maths
    /// is pure + static so the bounds rule is EditMode-testable.</para>
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

        // Her cabin doorway, through the Core seam (rule 4 — Player never names the door's own type), and
        // the hull it was looked for under. Null for most of the fleet, which has no measured interior.
        private ICabinThreshold _doorway;
        private Transform _doorwaySearchedUnder;

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

        /// <summary>
        /// How high above the KEEL the deck under the player is (metres) — the third component of
        /// where they stand, and the one the plan-view <see cref="DeckLocalPosition"/> cannot carry.
        /// 0 on the greybox rectangle, which has no sheer to follow.
        ///
        /// <para>Load-bearing as soon as anything asks a 3D question about the fisher: the hull's
        /// per-pixel occlusion compares her geometry against the DEPTH of their feet, and in a ¾ view
        /// height and along-hull distance land on the same screen axis — a cockpit sole and a raised
        /// foredeck at the same <c>y</c> sit at very different depths.</para>
        /// </summary>
        public float DeckHeightMeters => _deckHeight;

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
            // Look for her doorway again on the next tick. A bind is exactly the moment a cached "she has
            // no cabin" could be stale — the installer builds in its own Start, and a test stands a whole
            // boat up inside one frame.
            _doorway = null;
            _doorwaySearchedUnder = null;
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
        ///
        /// <para><b>⭐ …and then the same last resort the STATIC twins take</b> (2026-09-08). This used
        /// to stop at the bind-time cache, while <see cref="DrawnHeadingDegreesOf"/> and
        /// <see cref="BakeElevationDegreesOf"/> — the reads the switcher's helm and boarding stations go
        /// through — fall through to <see cref="BoatHullPresenterHost.Resolve"/>. Two readers of ONE
        /// fact with different last resorts: on any rig where the host has not published a presenter and
        /// the bind happened before the skinner did, the stations project through the artwork's own
        /// elevation while the walk they hand the answer to un-projects through the PLAN VIEW, and a
        /// seat comes out 0.36 m from where it was aimed. Whatever a hull's picture is drawn at, every
        /// read of it must be the same read — that is the whole of #789, one method further in.</para>
        /// </summary>
        private IBoatHullPresenter LiveHull()
        {
            if (_boatRoot == null) return _hull;
            var host = _boatRoot.GetComponent<BoatHullPresenterHost>();
            if (host != null && host.Presenter != null) return host.Presenter;
            return _hull != null ? _hull : BoatHullPresenterHost.Resolve(_boatRoot.gameObject);
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

        /// <summary>
        /// ⭐ Put her at a <b>DECK-FRAME</b> point, <b>unclamped</b> — the washboard verb's placement.
        ///
        /// <para><b>⚠ Why it must not clamp.</b> <see cref="SnapTo"/> pulls its argument onto the
        /// walkable areas, and a washboard is deliberately NOT one of them (<c>Accepts</c> takes
        /// <c>Deck</c> only unless a caller asks for side decks). Put the rail through that clamp and she
        /// is dragged straight back inboard — the verb would look like it fired and do nothing. The
        /// caller has already chosen a point on the rail via <see cref="TryWashboardStand"/>; this
        /// places her there and believes it.</para>
        /// </summary>
        /// <summary>
        /// ⭐ Is the walker out on the RAIL? Owned by <c>ControlSwitcher</c>'s washboard verb — this
        /// component does not decide it, it obeys it: while set, the walk clamps to the hull's box
        /// instead of her deck polygons and moves at <see cref="WashboardSlowFactor"/>.
        /// </summary>
        public bool OnWashboard
        {
            get => _onWashboard;
            set => _onWashboard = value;
        }
        private bool _onWashboard;

        /// <summary>Move-speed multiplier out on the rail (<c>GameConfig.WashboardSlowFactor</c>, set by
        /// the switcher). You are on the gunwale over open water; it is not somewhere you stroll.</summary>
        public float WashboardSlowFactor
        {
            get => _washboardSlowFactor;
            set => _washboardSlowFactor = Mathf.Clamp(value, 0.05f, 1f);
        }
        private float _washboardSlowFactor = 0.5f;

        public void SnapToDeckLocal(Vector2 deckLocal)
        {
            if (_boatRoot == null) return;
            float heading = DrawnHeadingDegrees();
            float elevation = BakeElevationDegrees();
            _deckLocal = deckLocal;
            _deckArea = -1;                                    // off the walkable areas: no hint to keep
            transform.position = _boatRoot.position
                               + (Vector3)DeckAreaMath.DeckToWorld(deckLocal, _deckHeight,
                                                                   heading, elevation);
        }

        /// <summary>
        /// <b>Where a boat-relative WORLD-axis spot actually LANDS on this hull</b> — the world position
        /// <see cref="SnapTo"/> would put the player at, worked out without moving anybody and without
        /// touching a field. The boarding move's whole geometry comes from here: the RAIL is this query
        /// asked from where the fisher is standing (outside the deck, so the clamp returns the nearest
        /// point on the outline), and the SEAT is the same query asked at the switcher's board offset
        /// (inside the deck, so the clamp returns it unchanged).
        ///
        /// <para><b>Why it is asked every frame rather than captured once.</b> The answer is a boat-frame
        /// fact projected through the hull's LIVE drawn heading and position, so a hull that rocks, turns
        /// and drifts under an in-flight arc keeps moving the arc's endpoint with her. Capturing a world
        /// point at the key-press would land the fisher where the boat USED to be.</para>
        ///
        /// <para><paramref name="includeWashboards"/> opens the side decks to the clamp. The walk itself
        /// never does (a washboard is somewhere you climb onto, not somewhere you stroll); the boarding
        /// move does, on the hulls whose data carries them, because the strip you actually step over on
        /// the way aboard IS the washboard. An open boat has none and the deck outline answers instead —
        /// absence is data.</para>
        /// </summary>
        /// <returns>False on an unbound walk (no boat to be relative to), with <paramref name="world"/>
        /// left at <see cref="Vector3.zero"/> — the caller falls back to its own placement.</returns>
        public bool TryDeckPointWorld(Vector2 boatRelative, bool includeWashboards, out Vector3 world)
            => TryDeckPointWorldOn(_boatRoot, boatRelative, includeWashboards, out world);

        /// <summary>
        /// ⭐ <b>The same query about a hull this walk is NOT bound to</b> — added 2026-09-03 for the
        /// boarding REACH gate, which has to ask "how far is she from that hull's rail?" every frame,
        /// from on foot, about a boat nobody is standing on.
        ///
        /// <para><b>⚠ Why it could not just call <see cref="Bind"/> first.</b> <c>Bind</c> is not a
        /// lookup, it is a state change: it re-points <c>_boatRoot</c>, re-resolves the hull and the
        /// deck, throws away the cached area hint and <b>re-seeds the walker's deck-local position from
        /// the transform</b>. Doing that from a per-frame predicate — and the interact popup asks the
        /// reach question every frame — would have this component quietly moving the player's deck
        /// position as a side effect of being asked a question. A predicate must not write.</para>
        ///
        /// <para>So this takes the hull as an argument and reads everything live off it, exactly as
        /// <c>Bind</c> would have, but stores none of it. The bound overload above is now this one asked
        /// about <c>_boatRoot</c>, so there is one implementation of the rail rather than two that can
        /// drift.</para>
        /// </summary>
        public bool TryDeckPointWorldOn(Transform boatRoot, Vector2 boatRelative, bool includeWashboards,
                                        out Vector3 world)
        {
            world = Vector3.zero;
            if (boatRoot == null) return false;

            Vector3 boatPos = boatRoot.position;
            float heading = DrawnHeadingDegreesOf(boatRoot);
            BoatDeckDef deck = DeckOf(boatRoot);

            if (deck != null && deck.HasWalkableDeck())
            {
                float elevation = BakeElevationDegreesOf(boatRoot);
                int hint = -1;
                Vector2 local = SeedDeckLocalPure(boatRelative, heading, elevation, deck,
                                                  includeWashboards, ref hint, out float height);
                world = boatPos + (Vector3)DeckAreaMath.DeckToWorld(local, height, heading, elevation);
                return true;
            }

            world = boatPos + (Vector3)ClampToDeckHeading(boatRelative, heading, _deckCenter, _deckHalfExtents);
            return true;
        }

        /// <summary>
        /// ⭐ A DECK-FRAME point projected to world for a hull this walk may not be bound to —
        /// <b>without clamping it onto the deck</b>. The clamped twin above answers "where on her may I
        /// stand?"; this answers "where in the world is that spot beside her?", which is what a probe
        /// looking for planks ALONGSIDE her needs: every point it cares about is outside her by
        /// construction, and a clamp would drag each one back onto the hull.
        /// </summary>
        public bool TryDeckFramePointWorld(Transform boatRoot, Vector2 deckPoint, out Vector3 world)
        {
            world = Vector3.zero;
            if (boatRoot == null) return false;
            float heading = DrawnHeadingDegreesOf(boatRoot);
            float elevation = BakeElevationDegreesOf(boatRoot);
            world = boatRoot.position + (Vector3)DeckAreaMath.DeckToWorld(deckPoint, 0f, heading, elevation);
            return true;
        }

        /// <summary>
        /// The hull's walkable BOX in her own deck frame (centre + half-extents, metres) — her authored
        /// one where she has deck data, else this walk's fallback rectangle. The coarse shape a probe
        /// walks the sides of; the polygons are for standing on, not for asking what is beside her.
        /// </summary>
        public bool TryDeckBox(Transform boatRoot, out Vector2 center, out Vector2 halfExtents)
        {
            center = _deckCenter;
            halfExtents = _deckHalfExtents;
            if (boatRoot == null) return false;

            BoatDeckDef deck = DeckOf(boatRoot);
            if (deck != null && deck.HasWalkableDeck() && deck.WalkHalfExtents.sqrMagnitude > 1e-6f)
            {
                center = deck.WalkCenter;
                halfExtents = deck.WalkHalfExtents;
            }
            return true;
        }

        /// <summary>
        /// ⭐ <b>Where the washboard is, and which way is off her</b> — the geometry behind the owner's
        /// two-press exit (2026-09-02).
        ///
        /// <para><b>⚠ Only two hull families actually HAVE washboards.</b> The cape islander and the
        /// lobster boats author <see cref="DeckAreaKind.Washboard"/> areas; the starter dory, the punt
        /// and the skiffs author none, and that is data rather than an omission — an open boat has no
        /// side deck to climb onto, you step over her gunwale from where you sit. So this answers in
        /// two ways and says which: <b>authored</b> washboard areas where they exist, and otherwise a
        /// <b>derived gunwale band</b> <paramref name="derivedBandWidth"/> wide just inside her walkable
        /// edge. Both are somewhere to stand at the rail; only the first is a place the rig drew.</para>
        ///
        /// <para><b>Outboard is measured off her walkable BOX, not off the washboard's own polygon.</b>
        /// A washboard strip is narrow and has two long edges — the rail and the inboard lip — so the
        /// nearest EDGE to somebody standing on it is a coin toss between "the sea" and "the cockpit",
        /// and half the time the predicate would send her the wrong way. Away from the hull's centreline
        /// is unambiguous, and it is also what a person means by outboard.</para>
        /// </summary>
        /// <param name="fromDeckPoint">Where she stands now, deck frame.</param>
        /// <param name="derivedBandWidth">Gunwale-band width for a hull with no authored washboards
        /// (<c>GameConfig.WashboardWidthMetres</c>). Clamped to half the walkable half-width, or on a
        /// narrow hull the "band" would be her whole deck.</param>
        /// <param name="standDeck">Deck-frame point out on the rail to stand at.</param>
        /// <param name="outwardNormal">Deck-frame unit vector pointing off her, there.</param>
        /// <param name="authored">True when this hull's rig drew real washboard areas.</param>
        public bool TryWashboardStand(Transform boatRoot, Vector2 fromDeckPoint, float derivedBandWidth,
                                      out Vector2 standDeck, out Vector2 outwardNormal, out bool authored)
        {
            standDeck = fromDeckPoint;
            outwardNormal = Vector2.zero;
            authored = false;
            if (boatRoot == null) return false;
            if (!TryDeckBox(boatRoot, out Vector2 centre, out Vector2 half)) return false;
            if (half.sqrMagnitude <= 1e-6f) return false;          // no walkable area ⇒ no rail

            BoatDeckDef deck = DeckOf(boatRoot);
            if (deck != null && deck.HasWashboards())
            {
                authored = true;
                if (!TryNearestWashboardPoint(deck, fromDeckPoint, out standDeck)) return false;
            }
            else
            {
                // The derived band: out to her edge, then back in by half the band's width so she is
                // standing ON the gunwale rather than balanced on its outer lip.
                float band = Mathf.Clamp(derivedBandWidth, 0.01f,
                                         Mathf.Max(0.02f, Mathf.Min(half.x, half.y) * 0.5f));
                Vector2 d = fromDeckPoint - centre;
                Vector2 onEdge = centre + new Vector2(
                    Mathf.Clamp(d.x, -half.x, half.x), Mathf.Clamp(d.y, -half.y, half.y));
                Vector2 n = OverTheSideMath.OutwardNormalOnBox(centre, half, onEdge);

                // ⭐ A FISHER STANDING DEAD AMIDSHIPS HAS NO NEAREST RAIL (2026-09-08). Port and
                // starboard are exactly equidistant, both land inside OutwardNormalOnBox's tie band, and
                // the two normals SUM TO ZERO — so this returned false and the whole rail verb did
                // nothing. It was unreachable only because the boarding seat was a world-axis offset
                // that clamped her against a rail on any hull not lying north; with her seated where the
                // seat is actually authored, amidships is the ORDINARY case, and the verb was resting on
                // a bug.
                //
                // The tie goes to STARBOARD: arbitrary, but stable and cheap to be wrong about.
                // <see cref="BerthPilot.Berth.FromShorePoint"/> answers a degenerate shore point the
                // same way and for the same reason. Press one only puts her ON the rail — the press
                // that decides in-or-out is the FACING (owner, 2026-09-02), and stepping back inboard
                // from the wrong gunwale costs one press on a boat 0.45 m wide.
                if (n.sqrMagnitude <= 1e-6f) n = new Vector2(1f, 0f);

                // Push out to the boundary along the normal first (a point amidships clamps to itself),
                // then back in by half a band.
                float outToEdge = Mathf.Abs(n.x) > Mathf.Abs(n.y)
                    ? half.x - Mathf.Abs(d.x) : half.y - Mathf.Abs(d.y);
                standDeck = onEdge + n * Mathf.Max(0f, outToEdge) - n * (band * 0.5f);
            }

            outwardNormal = OverTheSideMath.OutwardNormalOnBox(centre, half, standDeck);
            return outwardNormal.sqrMagnitude > 1e-6f;
        }

        /// <summary>The nearest point inside any authored <see cref="DeckAreaKind.Washboard"/> area.</summary>
        private static bool TryNearestWashboardPoint(BoatDeckDef deck, Vector2 deckPoint, out Vector2 onIt)
        {
            onIt = deckPoint;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < deck.Areas.Length; i++)
            {
                DeckArea a = deck.Areas[i];
                if (a == null || a.Kind != DeckAreaKind.Washboard || !a.IsUsable()) continue;
                if (DeckAreaMath.Contains(a.Outline, deckPoint)) { onIt = deckPoint; return true; }
                Vector2 p = DeckAreaMath.ClosestPointOnOutline(a.Outline, deckPoint, out float sqr);
                if (sqr < best) { best = sqr; onIt = p; found = true; }
            }
            return found;
        }

        /// <summary>The drawn heading of a hull this walk may not be bound to — the same read
        /// <see cref="DrawnHeadingDegrees"/> makes, without the cached presenter.
        ///
        /// <para><b>Public since 2026-09-07</b> so the switcher's HELM STATION can be projected through
        /// the very same compass the deck it sits on is projected through. It was already the only read
        /// of a hull's drawn facing anyone outside this file wanted, and a fourth hand-written copy of it
        /// (<c>ArrivalOpening</c>, <c>BoatCleats</c> and <c>DeckRiderVisual</c> each carry one) is how a
        /// helm and the deck under it end up disagreeing about which way she is pointing.</para></summary>
        public static float DrawnHeadingDegreesOf(Transform boatRoot)
        {
            if (boatRoot == null) return 0f;
            var host = boatRoot.GetComponent<BoatHullPresenterHost>();
            IBoatHullPresenter hull = host != null ? host.Presenter : null;
            if (hull == null) hull = BoatHullPresenterHost.Resolve(boatRoot.gameObject);
            return hull != null ? hull.DrawnHeadingDegrees()
                                : DirectionalBoatSprite.HeadingDegreesFromBow(boatRoot.up);
        }

        /// <summary>The bake elevation of a hull this walk may not be bound to. Public for the reason
        /// <see cref="DrawnHeadingDegreesOf"/> is: heading and elevation are the two halves of ONE
        /// projection (<see cref="DeckAreaMath.DeckToWorld"/>), and a caller handed one without the other
        /// would foreshorten a hull by the wrong artwork's camera.</summary>
        public static float BakeElevationDegreesOf(Transform boatRoot)
        {
            if (boatRoot == null) return DeckAreaMath.PlanViewElevationDegrees;
            var host = boatRoot.GetComponent<BoatHullPresenterHost>();
            IBoatHullPresenter hull = host != null ? host.Presenter : null;
            if (hull == null) hull = BoatHullPresenterHost.Resolve(boatRoot.gameObject);
            return hull != null ? hull.BakeElevationDegrees : DeckAreaMath.PlanViewElevationDegrees;
        }

        /// <summary>The authored deck of a hull this walk may not be bound to. Falls back to the bound
        /// hull's deck ONLY when asked about the bound hull, so a question about another boat can never
        /// be answered with this one's areas.</summary>
        private BoatDeckDef DeckOf(Transform boatRoot)
        {
            if (boatRoot == null) return null;
            BoatDeckDef live = BoatDeckAreas.Resolve(boatRoot.gameObject);
            if (live != null) return live;
            return ReferenceEquals(boatRoot, _boatRoot) ? _deck : null;
        }

        /// <summary>True when the hull under this walk offers washboards to step over on the way aboard.
        /// Live-read (the dev hull picker swaps hulls under the player), null-safe, and false on every
        /// open boat — which is the DATA saying so, not a missing import.</summary>
        public bool HullHasWashboards()
        {
            BoatDeckDef deck = LiveDeck();
            return deck != null && deck.HasWashboards();
        }

        private void OnEnable() => SeedDeckLocalFromTransform();

        /// <summary>Where the deck-walk intents come from (ADR 0043) — the bindings asset until something
        /// else is configured; made lazily. Never serialized: a source is code, not scene data.</summary>
        private IControlIntentSource<DeckIntents> _input;

        public IControlIntentSource<DeckIntents> DeckInputSource
        {
            get
            {
                if (_input == null) _input = new DeviceDeckIntentSource();
                return _input;
            }
        }

        /// <summary>Hand the deck walk a different source — a scripted journey, a future device. Null
        /// restores the bindings asset. Takes effect on the next frame's read.</summary>
        public void ConfigureDeckInput(IControlIntentSource<DeckIntents> source) => _input = source;

        private void Update()
        {
            if (_boatRoot == null) return;
            Vector2 boatPos = _boatRoot.position;
            float drawnHeading = DrawnHeadingDegrees();
            BoatDeckDef deck = LiveDeck();
            // THE ONE READ PER FRAME (ADR 0043 §2); the gates are applied inside the source.
            Vector2 input = DeckInputSource.Read().Move;

            Vector2 relative, stanceCenter, stanceHalfExtents;

            // ⭐ OUT ON THE RAIL, the walkable shape is her BOX, not her polygons (2026-09-02).
            //
            // ⚠ Without this the washboard verb looks like it fires and does nothing. The polygon clamp
            // takes Deck areas only — a washboard is deliberately not one — so the switcher would place
            // her on the gunwale and the very next tick would drag her straight back inboard. Her
            // walkable box is the honest envelope out there: it reaches the rail (which the polygons by
            // definition stop short of) and it still cannot put her in the sea.
            if (_onWashboard && TryDeckBox(_boatRoot, out Vector2 railCentre, out Vector2 railHalf))
            {
                relative = (Vector2)transform.position - boatPos;
                relative = StepOnDeck(relative, input, _moveSpeed * WashboardSlowFactor, Time.deltaTime,
                                      drawnHeading, railCentre, railHalf);
                _deckLocal = WorldToDeckFrame(relative, drawnHeading);
                _deckArea = -1;
                stanceCenter = railCentre;
                stanceHalfExtents = railHalf;
            }
            else if (deck != null && deck.HasWalkableDeck())
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

            // ⭐ AND HER CABIN DOOR, if this hull has one standing open. The owner's 2026-08-28 ruling is
            // that "a player can walk through freely when opened", so the crossing is a step and not a
            // press: the door owns the threshold band and the one-crossing-per-approach latch (both sides
            // of one doorway must share one), and all this does is tell it where she is standing.
            //
            // ⚠ Not on the rail. Out on the washboard _deckLocal is read back off her world offset with
            // no foreshortening inverted, so it is not the hull-frame point the threshold is measured in
            // — and a doorway is not something you cross from the gunwale anyway.
            if (!_onWashboard) WalkThroughAnOpenCabinDoor();

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

        /// <summary>
        /// ⭐ <b>Has she just stepped through this hull's cabin door?</b> One call a tick, handing the
        /// doorway where she is standing in the HULL's own metres — the frame <c>_deckLocal</c> is
        /// already in, and the frame a threshold is measured in, so nothing is projected, inverted or
        /// re-derived here.
        ///
        /// <para><b>⛔ THROUGH THE CORE SEAM, and this component names no Boats type to do it.</b> The
        /// question is asked of <see cref="ICabinThreshold"/> — Player says where she is standing, Boats
        /// decides whether that was a crossing and makes it (rule 4). Every part of the decision is on
        /// the far side: whether the leaf is open, whether she is inside the measured opening, whether
        /// this approach has already been spent, which way it goes, and whether the room will have her.
        /// This component owns where the player stands and nothing else — the same division of labour it
        /// keeps with the hull presenter and the deck areas.</para>
        /// </summary>
        private void WalkThroughAnOpenCabinDoor()
        {
            ICabinThreshold doorway = LiveCabinThreshold();
            if (doorway != null) doorway.TryWalkThrough(_deckLocal);
        }

        /// <summary>
        /// This hull's doorway, or null — the same live-read discipline as <see cref="LiveHull"/> and
        /// <see cref="LiveDeck"/>, for the same reason: the dev hull picker changes the boat under the
        /// player's feet, and a doorway captured at boarding would be the previous hull's.
        ///
        /// <para><b>⚠ The liveness test goes through <c>UnityEngine.Object</c>'s own <c>==</c>.</b> An
        /// interface reference compared with <c>== null</c> / <c>?.</c> sees the raw managed reference
        /// and is satisfied by a destroyed component's fake-null, so a torn-down door would go on
        /// answering with whatever it last held. <c>BoatCutaway.Renderer</c> keeps this exact pattern for
        /// this exact reason.</para>
        ///
        /// <para><b>The searched-root latch is rule 7, not tidiness.</b> Most of the fleet has no
        /// measured interior, and without it every hull with no cabin would pay a whole-hierarchy walk
        /// on every tick of every deck walk. One search per hull answers "she has none" for good, and a
        /// re-skin under her feet (a new root, or a door torn off) starts a fresh one.</para>
        /// </summary>
        private ICabinThreshold LiveCabinThreshold()
        {
            if (_doorway is UnityEngine.Object live && live != null) return _doorway;
            if (_doorway != null) { _doorway = null; _doorwaySearchedUnder = null; }

            if (ReferenceEquals(_doorwaySearchedUnder, _boatRoot)) return null;

            _doorwaySearchedUnder = _boatRoot;
            _doorway = _boatRoot != null
                ? _boatRoot.GetComponentInChildren<ICabinThreshold>(includeInactive: true)
                : null;
            return _doorway is UnityEngine.Object found && found != null ? _doorway : null;
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
            // false = no washboards: the WALK never stands on a side deck (that is somewhere you climb
            // onto). Only the boarding move opens them, and it asks through TryDeckPointWorld.
            => _deckLocal = SeedDeckLocalPure(worldRelative, heading, elevation, deck, false,
                                              ref _deckArea, out _deckHeight);

        /// <summary>The seeding iteration itself, with nothing of this component's state in it — so the
        /// walk's own seating (<see cref="SeedDeckLocal"/>) and the boarding move's read-only question
        /// (<see cref="TryDeckPointWorld"/>) are the SAME maths and cannot drift into disagreeing about
        /// where the deck is. Pure + static + allocation-free.
        ///
        /// <para><b>⭐ PUBLIC because there is a THIRD caller now</b> (2026-09-04): the arrival's passenger
        /// walk (<c>ArrivalDeckWalk</c>), which rides somebody else's hull and so cannot own one of these
        /// components at all — the <c>ControlSwitcher</c> owns those and enables them by MODE, and an
        /// arrival deliberately never sets a mode (she is not aboard HER boat). It joins the deck the same
        /// way the aft door joins the sole: by reading a world point back into the hull frame. Handing it
        /// this iteration rather than letting it write a second one is the whole of why the two cannot
        /// drift into disagreeing about where the planking is.</para></summary>
        public static Vector2 SeedDeckLocalPure(Vector2 worldRelative, float heading, float elevation,
                                                BoatDeckDef deck, bool includeWashboards,
                                                ref int areaHint, out float heightMeters)
        {
            float height = 0f;
            Vector2 seated = Vector2.zero;
            for (int pass = 0; pass < SeedPasses; pass++)
            {
                Vector2 local = DeckAreaMath.WorldToDeck(worldRelative, height, heading, elevation);
                seated = deck.ClampToWalkable(local, ref areaHint, out height, includeWashboards);
            }
            heightMeters = height;
            return seated;
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
