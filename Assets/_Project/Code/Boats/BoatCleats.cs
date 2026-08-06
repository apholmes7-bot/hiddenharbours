using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>This hull's tie-off points, published to the rope gameplay</b> (M2-38). Reads the rig's own
    /// <c>CLEATS</c> off <see cref="BoatDeckDef.Cleats"/> — imported, never typed — projects each onto the
    /// drawn hull, and registers it as a Core <see cref="IMooringCleat"/> so the Player-lane rope
    /// controller can find it without either module referencing the other (rule 4).
    ///
    /// <para><b>The twin of <see cref="BoatDeckAreas"/>, and deliberately shaped like it.</b> That component
    /// is how the deck-walk asks "where can I stand on this hull?"; this is how the rope asks "where can I
    /// tie to it?". Both answer from the SAME imported sidecar, so when the art director moves a rail the
    /// walkable area and the cleats move together — the one-source-of-truth promise
    /// design/deck-boarding-cleats-and-interact-capture.md §2 makes.</para>
    ///
    /// <para><b>Projection, not raw metres</b> (the lesson <see cref="DeckAreaMath"/> was written for). A
    /// cleat's authored position is hull-local metres in the rig's frame; the hull is DRAWN by a ¾ camera
    /// that squashes the along-view axis. Publishing raw metres would put the bow cleat well off the
    /// picture on a north-facing hull, exactly as the wake plume once sat astern of the boat. So each read
    /// goes through <see cref="DeckAreaMath.DeckToWorld"/> with the presenter's live drawn heading and its
    /// own artwork's bake elevation — never a constant, never baked here.</para>
    ///
    /// <para><b>Height is kept OUT of the projection, on purpose.</b> <see cref="IMooringCleat.WorldPosition"/>
    /// is where the fitting is DRAWN (the projection folds height into screen-Y, which is what makes the
    /// rope meet the bollard on screen). <see cref="IMooringCleat.ElevationMeters"/> is the fitting's real
    /// height above chart datum, which is what the tide law needs. Collapsing the two is how a mooring
    /// would silently stop caring about the tide.</para>
    ///
    /// <para><b>Absence is data.</b> A hull with no sidecar, or a sidecar with no <c>CLEATS</c> section,
    /// registers nothing and simply cannot be tied to — not an error, the same contract
    /// <see cref="BoatDeckDef.HasWashboards"/> already states for washboards.</para>
    ///
    /// <para><b>No per-frame allocation (rule 7).</b> The <see cref="IMooringCleat"/> handles are built
    /// once per deck (re-built only when the hull is actually re-skinned under the player, which the dev
    /// picker does) and each one computes its live position on read. Nothing here has an
    /// <c>Update</c>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatCleats : MonoBehaviour
    {
        /// <summary>
        /// One cleat of one hull, as the Core contract sees it. A thin live view: it holds its index and
        /// its owner, and recomputes position/elevation on every read, because a boat's fitting swings
        /// with her heading and rides the tide.
        /// </summary>
        private sealed class HullCleat : IMooringCleat
        {
            private readonly BoatCleats _owner;
            private readonly int _index;
            private readonly string _id;

            public HullCleat(BoatCleats owner, int index, string id)
            {
                _owner = owner;
                _index = index;
                _id = id;
            }

            public string Id => _id;
            public CleatSide Side => CleatSide.Boat;
            public Vector2 WorldPosition => _owner != null ? _owner.WorldPositionOf(_index) : Vector2.zero;
            public float ElevationMeters => _owner != null ? _owner.ElevationOf(_index) : 0f;
        }

        [Tooltip("The hull whose draught sets how far her cleats stand above the water she floats in. " +
                 "Resolved from the sibling BoatController at bind; a hull with no def falls back to zero " +
                 "draught, which reads the cleat height straight off the keel.")]
        [SerializeField] private BoatHullDef _hull;

        private BoatDeckDef _deck;
        private IMooringCleat[] _cleats = Array.Empty<IMooringCleat>();
        private bool _registered;

        /// <summary>The live handles this hull has published (empty when she carries no cleat data).</summary>
        public IMooringCleat[] Published => _cleats;

        /// <summary>True when this hull offers anything to tie to — a boat you cannot moor is data saying
        /// so, not a fault.</summary>
        public bool HasCleats() => _cleats.Length > 0;

        private void Awake() => Rebuild();

        private void OnEnable()
        {
            // Rebuild first: a hull re-skinned while disabled must publish the NEW cleats, not the old.
            Rebuild();
            RegisterAll();
        }

        private void OnDisable() => UnregisterAll();

        /// <summary>
        /// Point this component at a hull + deck in one call (the skinner, region builders, tests). Safe at
        /// any time: it re-publishes, so a hull swapped under the player's feet is tied to by her own new
        /// cleats on the very next read rather than through handles captured at boarding.
        /// </summary>
        public void Configure(BoatHullDef hull, BoatDeckDef deck)
        {
            _hull = hull;
            UnregisterAll();
            Build(deck);
            if (isActiveAndEnabled) RegisterAll();
        }

        /// <summary>
        /// Point a boat root at (or away from) a hull's cleats — the twin of
        /// <see cref="BoatDeckAreas.Write"/>, called from the same two places in
        /// <see cref="BoatHullSkinner"/> and for the same reason: a hull swapped under the player must
        /// publish HER cleats, and a swap DOWN to an unmeasured hull must clear the old ones rather than
        /// leave a rope tied to a boat that has been sold.
        ///
        /// <para>Adds the component only when there is something to say: a hull that never had cleat data
        /// does not grow an empty marker.</para>
        /// </summary>
        public static void Write(GameObject root, BoatHullDef hull, BoatDeckDef deck)
        {
            if (root == null) return;
            var host = root.GetComponent<BoatCleats>();
            if (host == null)
            {
                bool nothingToSay = deck == null || deck.Cleats == null || deck.Cleats.Length == 0;
                if (nothingToSay) return;
                host = root.AddComponent<BoatCleats>();
            }
            host.Configure(hull, deck);
        }

        /// <summary>Re-read the deck off the boat root (<see cref="BoatDeckAreas"/>) and the hull off the
        /// sibling controller, then rebuild the published handles.</summary>
        public void Rebuild()
        {
            if (_hull == null)
            {
                var boat = GetComponent<BoatController>();
                if (boat != null) _hull = boat.Hull;
            }

            BoatDeckDef deck = BoatDeckAreas.Resolve(gameObject);
            if (deck == _deck && _cleats.Length > 0) return;   // nothing changed — keep the handles
            bool was = _registered;
            UnregisterAll();
            Build(deck);
            if (was && isActiveAndEnabled) RegisterAll();
        }

        private void Build(BoatDeckDef deck)
        {
            _deck = deck;
            DeckCleat[] source = deck != null ? deck.Cleats : null;
            if (source == null || source.Length == 0)
            {
                _cleats = Array.Empty<IMooringCleat>();
                return;
            }

            _cleats = new IMooringCleat[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                DeckCleat c = source[i];
                // Id is the hull's deck id plus the rig's own cleat id — stable, and readable in a log.
                string deckId = string.IsNullOrEmpty(deck.Id) ? "deck.unknown" : deck.Id;
                string cleatId = c != null && !string.IsNullOrEmpty(c.Id) ? c.Id : i.ToString();
                _cleats[i] = new HullCleat(this, i, $"{deckId}.{cleatId}");
            }
        }

        private void RegisterAll()
        {
            if (_registered) return;
            for (int i = 0; i < _cleats.Length; i++) MooringCleats.Register(_cleats[i]);
            _registered = _cleats.Length > 0;
        }

        private void UnregisterAll()
        {
            if (!_registered) return;
            for (int i = 0; i < _cleats.Length; i++) MooringCleats.Unregister(_cleats[i]);
            _registered = false;
        }

        // ---- the live reads -----------------------------------------------------------------------

        /// <summary>
        /// Where cleat <paramref name="index"/> is DRAWN, in world metres: the boat's position plus its
        /// hull-local offset projected through the drawn heading and this artwork's own foreshortening.
        /// The same transform the deck-walk clamps the player with, so a player standing at a cleat and
        /// the cleat itself are measured in one frame.
        /// </summary>
        public Vector2 WorldPositionOf(int index)
        {
            DeckCleat c = CleatAt(index);
            if (c == null) return transform.position;

            var local = new Vector2(c.PositionMeters.x, c.PositionMeters.y);
            Vector2 offset = DeckAreaMath.DeckToWorld(local, c.PositionMeters.z,
                                                      DrawnHeadingDegrees(), BakeElevationDegrees());
            return (Vector2)transform.position + offset;
        }

        /// <summary>
        /// How high cleat <paramref name="index"/> stands above chart datum right now: the deterministic
        /// water level plus the fitting's height above this hull's floating waterline. <b>This is the
        /// tidal half of the mechanic</b> — she floats, so her fittings rise and fall with the sea, while
        /// the wharf's do not.
        ///
        /// <para>With no environment service wired (EditMode, pre-bootstrap) the water reads 0 — the
        /// region simply isn't tide-driven, and the cleat sits at its own freeboard above datum. That is
        /// the established gate-off shape, not a special case.</para>
        /// </summary>
        public float ElevationOf(int index)
        {
            DeckCleat c = CleatAt(index);
            if (c == null) return 0f;

            float draught = _hull != null ? _hull.DraughtMeters : 0f;
            float freeboard = MooringLineMath.CleatHeightAboveWaterline(c.PositionMeters.z, draught);

            IEnvironmentService env = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            float water = env != null ? env.WaterLevelAt(clock != null ? clock.TotalSeconds : 0.0) : 0f;

            return MooringLineMath.BoatCleatElevation(water, freeboard);
        }

        private DeckCleat CleatAt(int index)
        {
            DeckCleat[] source = _deck != null ? _deck.Cleats : null;
            if (source == null || index < 0 || index >= source.Length) return null;
            return source[index];
        }

        /// <summary>The compass heading of the hull PICTURE — the frame her cleats are drawn in. Read live
        /// off the presenter host for the same reason the deck-walk does: the dev picker swaps hulls under
        /// the player, and a stale presenter would hang the rope off the old boat's bow.</summary>
        private float DrawnHeadingDegrees()
        {
            IBoatHullPresenter hull = LiveHull();
            if (hull != null) return hull.DrawnHeadingDegrees();
            return DirectionalBoatSprite.HeadingDegreesFromBow(transform.up);
        }

        /// <summary>This artwork's own bake elevation (40° for the iso rigs; 90° = a plan view for art no
        /// camera ever baked). Per artwork, never a global.</summary>
        private float BakeElevationDegrees()
        {
            IBoatHullPresenter hull = LiveHull();
            return hull != null ? hull.BakeElevationDegrees : DeckAreaMath.PlanViewElevationDegrees;
        }

        /// <summary>The presenter to read THIS frame, through the shared resolver — the host's when the
        /// skinner has published one, else a wrap of a scene-serialised sprite rig, else null.</summary>
        private IBoatHullPresenter LiveHull() => BoatHullPresenterHost.Resolve(gameObject);
    }
}
