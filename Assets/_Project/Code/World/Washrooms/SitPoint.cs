using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A seat that only seats</b> (kitchens and bathrooms placement plan, R1): the toilet in a bathroom and
    /// the seat in an outhouse. Press to sit; press again, or walk off, to get up. It washes nothing and feeds
    /// nothing. What sitting is worth is Core's: the press hands the player to a <see cref="SeatSession"/>, which
    /// banks the seated stretch through <see cref="INeedsService.RestSeated"/> and says
    /// <see cref="PlayerSat"/> and <see cref="PlayerStood"/> on the bus (rule 4: this names nothing outside
    /// Core).
    ///
    /// <para><b>Which seats.</b> A <see cref="WashStationDef"/> marked <see cref="WashStationDef.SitOnly"/>
    /// (today <c>washstation.toilet</c>). <see cref="WashStationPoint"/> refuses those by design; this is the
    /// other half.</para>
    ///
    /// <para><b>No pose yet.</b> Nothing on main holds the player still on a seat (the SeatSession's own
    /// SEAM). Until the Player module's sit pose lands, sitting is a state and a line: walking away is how
    /// the player gets up without pressing, the same reach-plus-slack rule as a wash.</para>
    ///
    /// <para><b>No save.</b> Sitting is not stored; a reload stands the player up.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SitPoint : MonoBehaviour, IInteractable
    {
        [Tooltip("Stable id, unique among live interactables (fixture.<place>.<building>.<slot>, e.g. " +
                 "fixture.st_peters.ginny_cottage.toilet).")]
        [SerializeField] private string _id = "fixture.seat";

        [Tooltip("The WashStationDef id this seat is. It must be a SitOnly station (washstation.toilet).")]
        [SerializeField] private string _stationId = "washstation.toilet";

        [Tooltip("How close (m) you must stand to sit.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.1f;

        [Tooltip("How much further than the reach (m) you can drift while seated before it counts as getting up.")]
        [SerializeField, Min(0f)] private float _walkAwaySlackMeters = 0.5f;

        [Tooltip("What the popup offers when not seated (present tense, a few words, never the key).")]
        [SerializeField] private string _sitLabel = WorldStrings.SitDown;

        [Tooltip("The short line shown when he sits.")]
        [SerializeField] private string _sitLine = WorldStrings.SitBegin;

        /// <summary>The placed seat the player is on now, or null.</summary>
        public static SitPoint Active { get; private set; }

        private static SeatSession _session;

        /// <summary>The one player's seat session. Tests hand in their own with <see cref="UseSession"/>.</summary>
        public static SeatSession Session => _session ?? (_session = new SeatSession());

        /// <summary>Swap the shared session (tests), standing up from any seat first. Null goes back to the default.</summary>
        public static void UseSession(SeatSession session)
        {
            if (Active != null) Active.GetUp(null);
            Active = null;
            _session = session;
        }

        private WashStationDef _def;

        // ---- the interact seam ---------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <summary>The seat kind's id.</summary>
        public string StationId => _stationId;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <summary>The drift allowed past the reach before sitting counts as walked away from.</summary>
        public float WalkAwaySlackMeters => _walkAwaySlackMeters;

        /// <inheritdoc/>
        public int Priority => InteractPriority.Fixture;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Available when its Def is shipped and is a seat (a basin is a wash station, not this).</summary>
        public bool IsAvailable
        {
            get { WashStationDef def = Def; return def != null && def.SitOnly; }
        }

        /// <summary>True while the player is on this seat.</summary>
        public bool IsSeatedHere => Active == this && Session.IsSeated && Session.SeatId == _id;

        /// <summary>"Sit down", or "Get up" while seated here.</summary>
        public string VerbLabel => IsSeatedHere ? WorldStrings.SitGetUp : _sitLabel;

        /// <summary>The shipped Def, found once through the Core service.</summary>
        public WashStationDef Def => _def != null ? _def : (_def = GameServices.Cleanliness.FindStation(_stationId));

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => Toggle();

        /// <summary>Wire this seat from a builder or a test.</summary>
        public void Configure(string id, string stationId, float reachMeters, float walkAwaySlackMeters,
                              string sitLabel, string sitLine)
        {
            _id = id;
            _stationId = stationId;
            _reachMeters = Mathf.Max(0f, reachMeters);
            _walkAwaySlackMeters = Mathf.Max(0f, walkAwaySlackMeters);
            if (!string.IsNullOrEmpty(sitLabel)) _sitLabel = sitLabel;
            if (!string.IsNullOrEmpty(sitLine)) _sitLine = sitLine;
            _def = null;
        }

        /// <summary>The press: get up if seated here, else sit. Public so tests drive it without input.</summary>
        public void Toggle()
        {
            if (IsSeatedHere) { GetUp(WorldStrings.SitStood); return; }
            if (!IsAvailable) return;
            if (Active != null && Active != this) Active.GetUp(null);
            Session.Sit(_id, atDiningSeat: false);
            if (!Session.IsSeated) return;
            Active = this;
            if (!string.IsNullOrEmpty(_sitLine)) EventBus.Publish(new DevNotice(_sitLine));
        }

        /// <summary>
        /// One check while seated (the frame loop calls it; tests call it with a position). Lets go if the
        /// session moved to another seat, gets up when the player has walked off. Returns true while still
        /// seated here.
        /// </summary>
        public bool Watch(Vector2? playerPosition)
        {
            if (Active != this) return false;
            if (!Session.IsSeated || Session.SeatId != _id) { Active = null; return false; }
            if (playerPosition.HasValue &&
                WashStationRules.WalkedAway(WorldPosition, playerPosition.Value, _reachMeters, _walkAwaySlackMeters))
            {
                GetUp(WorldStrings.SitStood);
                return false;
            }
            return true;
        }

        private void GetUp(string line)
        {
            if (Active == this) Active = null;
            if (Session.IsSeated && Session.SeatId == _id) Session.Stand();
            if (!string.IsNullOrEmpty(line)) EventBus.Publish(new DevNotice(line));
        }

        private void Update()
        {
            if (Active != this) return;
            Transform player = GameServices.PlayerTransform;
            Watch(player != null ? (Vector2)player.position : (Vector2?)null);
        }

        private void OnEnable() => Interactables.Register(this);

        private void OnDisable()
        {
            Interactables.Unregister(this);
            // A seat switched off under him (the storey changed, the region unloaded) is a seat he has left.
            if (Active == this) GetUp(null);
        }
    }
}
