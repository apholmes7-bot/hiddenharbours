using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A wash station standing in the world</b> (washrooms plan section 3: the first placed stations).
    /// A basin, a shower or a tub is one <see cref="WashStationDef"/> id plus this anchor; what washing DOES
    /// is the Core service's (<see cref="ICleanlinessService.BeginWash"/>), and this component only decides
    /// when it starts and stops (rule 4: it names nothing outside Core).
    ///
    /// <para><b>The three ways a wash ends.</b> Press again ("Stop washing"); walk off (further than
    /// <see cref="ReachMeters"/> plus <see cref="WalkAwaySlackMeters"/> from the station); or the station has
    /// done all it can (dirt down to the Def's own floor, <c>1 - Ceiling</c>: a basin stops at 80 % clean, a
    /// shower at fully clean). How long that takes is the Def's rate, nothing here. Without soap a shower or
    /// a bath stops at the unsoaped cap instead (<see cref="ICleanlinessService.CeilingFor"/>), and says so.</para>
    ///
    /// <para><b>Which basin is washing.</b> The service only knows a station KIND by id, so two basins share
    /// one id; <see cref="Active"/> remembers which placed one began it, and only that one watches for the
    /// walk-off. If the service stops washing by any other road (the sea, a clock seek, the guard window) the
    /// watcher lets go on its next check.</para>
    ///
    /// <para><b>No save.</b> Nothing here is stored; the wash state is the cleanliness save's own.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WashStationPoint : MonoBehaviour, IInteractable
    {
        [Tooltip("Stable id, unique among live interactables (fixture.<place>.<what>, e.g. " +
                 "fixture.st_peters.ginny_cottage.washstand).")]
        [SerializeField] private string _id = "fixture.washstation";

        [Tooltip("The WashStationDef id this station is (washstation.sink, washstation.shower, ...). Its rate " +
                 "and how clean it can get you live on that asset.")]
        [SerializeField] private string _stationId = "washstation.sink";

        [Tooltip("How close (m) you must stand to start washing.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.2f;

        [Tooltip("How much further than the reach (m) you can drift while washing before it counts as " +
                 "walking away. A little, so a shuffle at the basin does not end the wash.")]
        [SerializeField, Min(0f)] private float _walkAwaySlackMeters = 0.6f;

        [Tooltip("What the popup offers when not washing (present tense, a few words, never the key).")]
        [SerializeField] private string _washLabel = WorldStrings.WashAtBasin;

        [Tooltip("The short line shown when washing begins.")]
        [SerializeField] private string _beginLine = WorldStrings.WashBeginBasin;

        [Tooltip("The short line shown when the station has done all it can.")]
        [SerializeField] private string _doneLine = WorldStrings.WashDoneBasin;

        /// <summary>The placed station that began the wash going on now, or null.</summary>
        public static WashStationPoint Active { get; private set; }

        private WashStationDef _def;

        // ---- the interact seam ---------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <summary>The station kind's id.</summary>
        public string StationId => _stationId;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <summary>The drift allowed past the reach before a wash counts as walked away from.</summary>
        public float WalkAwaySlackMeters => _walkAwaySlackMeters;

        /// <inheritdoc/>
        public int Priority => InteractPriority.Fixture;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot | InteractContext.OnDeck;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Available when its Def is shipped and washes (a toilet is a seat, not this).</summary>
        public bool IsAvailable
        {
            get { WashStationDef def = Def; return def != null && def.Washes; }
        }

        /// <summary>"Wash up at the basin", or "Stop washing" while this one is going.</summary>
        public string VerbLabel => IsWashingHere ? WorldStrings.WashStop : _washLabel;

        /// <summary>True while the service is washing at this station's kind AND this placed one began it.</summary>
        public bool IsWashingHere => Active == this && Def != null && GameServices.Cleanliness.WashingAt == Def;

        /// <summary>The shipped Def, found once through the Core service.</summary>
        public WashStationDef Def => _def != null ? _def : (_def = GameServices.Cleanliness.FindStation(_stationId));

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => Toggle();

        /// <summary>Wire this station from a builder or a test.</summary>
        public void Configure(string id, string stationId, float reachMeters, float walkAwaySlackMeters,
                              string washLabel, string beginLine, string doneLine)
        {
            _id = id;
            _stationId = stationId;
            _reachMeters = Mathf.Max(0f, reachMeters);
            _walkAwaySlackMeters = Mathf.Max(0f, walkAwaySlackMeters);
            if (!string.IsNullOrEmpty(washLabel)) _washLabel = washLabel;
            if (!string.IsNullOrEmpty(beginLine)) _beginLine = beginLine;
            if (!string.IsNullOrEmpty(doneLine)) _doneLine = doneLine;
            _def = null;
        }

        /// <summary>The press: stop if washing here, else begin (or say you are already as clean as this gets
        /// you). Public so tests drive it without input.</summary>
        public void Toggle()
        {
            if (IsWashingHere) { Stop(WorldStrings.WashStopped); return; }
            WashStationDef def = Def;
            if (def == null || !def.Washes) return;
            ICleanlinessService c = GameServices.Cleanliness;
            if (WashStationRules.IsDone(c.Dirt, c.CeilingFor(def)))
            {
                // As clean as this station gets him. When that is only because he has no soap, he says so
                // (the same thought a soapless wash begins with) instead of the station's "done" line.
                if (c.SoapMatters(def) && !c.HasSoap) CleanlinessResponder.SayNoSoap();
                else EventBus.Publish(new DevNotice(_doneLine));
                return;
            }
            if (Active != null && Active != this) Active = null;
            c.BeginWash(def);
            if (c.WashingAt != def) return;   // no save: nothing began
            Active = this;
            EventBus.Publish(new DevNotice(_beginLine));
        }

        /// <summary>
        /// One check of a wash in progress (the frame loop calls it; tests call it with a position). Lets go
        /// if the service stopped by another road, ends the wash when the player has walked off or the
        /// station has done all it can. Returns true while still washing here.
        /// </summary>
        public bool Watch(Vector2? playerPosition)
        {
            if (Active != this) return false;
            WashStationDef def = Def;
            ICleanlinessService c = GameServices.Cleanliness;
            if (def == null || c.WashingAt != def) { Active = null; return false; }
            if (playerPosition.HasValue &&
                WashStationRules.WalkedAway(WorldPosition, playerPosition.Value, _reachMeters, _walkAwaySlackMeters))
            {
                Stop(WorldStrings.WashWalkedOff);
                return false;
            }
            if (WashStationRules.IsDone(c.Dirt, c.CeilingFor(def)))
            {
                Stop(c.SoapMatters(def) && !c.WashSoaped ? WorldStrings.WashDoneNoSoap : _doneLine);
                return false;
            }
            return true;
        }

        private void Stop(string line)
        {
            if (Active == this) Active = null;
            GameServices.Cleanliness.EndWash();
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
            // A station switched off under you (the storey changed, the region unloaded) is a station you
            // have left.
            if (Active == this) Stop(null);
        }
    }

    /// <summary>The two pure rules a placed wash station applies, kept apart so a test pins them.</summary>
    public static class WashStationRules
    {
        /// <summary>Within this much of the station's floor counts as "done": the service eases toward its
        /// floor, so an exact compare would wait on a rounding.</summary>
        public const float DoneTolerance = 0.005f;

        /// <summary>The dirt a station stops at: <c>1 - Ceiling</c> (a basin 0.2, a shower 0).</summary>
        public static float FloorOf(WashStationDef def) => def == null ? 1f : 1f - Mathf.Clamp01(def.Ceiling);

        /// <summary>True when the station can take no more dirt off.</summary>
        public static bool IsDone(float dirt, WashStationDef def) => def == null || dirt <= FloorOf(def) + DoneTolerance;

        /// <summary>True when a wash that can reach <paramref name="ceiling"/> (0..1, from
        /// <see cref="ICleanlinessService.CeilingFor"/>: the station's own, or the unsoaped cap without soap)
        /// can take no more dirt off.</summary>
        public static bool IsDone(float dirt, float ceiling) => dirt <= 1f - Mathf.Clamp01(ceiling) + DoneTolerance;

        /// <summary>True when the player is further than reach plus slack from the station.</summary>
        public static bool WalkedAway(Vector2 station, Vector2 player, float reachMeters, float slackMeters)
        {
            float limit = Mathf.Max(0f, reachMeters) + Mathf.Max(0f, slackMeters);
            return (player - station).sqrMagnitude > limit * limit;
        }
    }
}
