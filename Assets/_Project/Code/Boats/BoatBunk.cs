using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>A bunk aboard your own boat that you can turn in at</b> (owner, 2026-11-14: "I also want bunks
    /// on boats sleepable to replenish fatigue"). Diegetic: you walk below, stand at the berth, press,
    /// and the night passes exactly as it does in your bed ashore. No bar, no HUD.
    ///
    /// <para><b>One per <c>sleep</c> anchor, and absence is data.</b>
    /// <see cref="BoatInteriorInstaller"/> builds one of these for every
    /// <see cref="BoatInteriorAnchor"/> whose <see cref="BoatInteriorAnchor.Action"/> is
    /// <see cref="SleepAction"/>, at that anchor's own reach point in the hull frame (a
    /// <see cref="HullLocalAnchor"/> on the same child, exactly as her doors hang). A hull whose rig
    /// names no bunk gets none; nothing per-hull lives in C# (rule 2).</para>
    ///
    /// <para><b>Offered only where you could actually lie down on it:</b> inside THIS hull's cabin, on
    /// the level the berth is on (<see cref="IsAvailable"/>). Everything else is a short in-world line
    /// rather than a missing prompt — the cozy refusal <c>InteriorBed</c> already makes (P5):</para>
    /// <list type="bullet">
    ///   <item>not the boat the player is aboard (<see cref="NotYourBoatText"/>);</item>
    ///   <item>deck work mid-cycle — the #193 guard, re-asserted here on the OnDeck path, because the
    ///   bed's half of it (OnFoot only) cannot apply to a bunk that is only ever reached from a deck
    ///   (<see cref="DeckWorkText"/>);</item>
    ///   <item>a co-op session (<see cref="CoopText"/>) — host-only sleep is a later stage, and main has
    ///   no way to detect a session yet, so this is a marked SEAM that always answers "solo";</item>
    ///   <item>adrift or under way while <see cref="GameConfig.BunkRequiresMooredOrAnchored"/> is on
    ///   (<see cref="NotSecuredText"/>).</item>
    /// </list>
    ///
    /// <para><b>What the press does.</b> It publishes <see cref="RestSaveRequested"/> with
    /// <see cref="RestSaveRequested.WakesAtSpawn"/> set, and stops. <c>RestSaveResponder</c> brings the
    /// morning (and with it <see cref="DayStarted"/>), relieves the sleep need through
    /// <see cref="INeedsService.Sleep"/> (the very line a bed's rest runs, gated on the morning coming),
    /// forgets the rest anchor, and writes the save — the bed's beat in every respect but the anchor. The save holds no boat position, so a wake spot on a
    /// cabin sole would reload the player in open water (ADR 0037, dated note 2026-11-14).</para>
    ///
    /// <para><b>No sleeping pose yet.</b> <see cref="SleepBeatRequested"/> is NOT published: the rig
    /// gives a bunk only the spot you stand on, not a mattress centre or a pillow end, and the presenter
    /// lays the fisher down at the position it is handed. The screen cover still hides the night. When
    /// the rig carries those two points this is one publish more.</para>
    ///
    /// <para>Cross-module rule: this reads Core (the helm slot, the bus, the config) and its own module
    /// (the cabin, the mooring, the anchor). It never references World — <c>InteriorBed</c> and this are
    /// two speakers of the same Core signal.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatBunk : MonoBehaviour, IInteractable
    {
        /// <summary>The rig's verb for a berth (<see cref="BoatInteriorAnchor.Action"/>). The one string
        /// the installer and this agree on.</summary>
        public const string SleepAction = "sleep";

        /// <summary>The prompt — the same word as the bed ashore, deliberately.</summary>
        public const string Verb = "Sleep";

        /// <summary>Pressed on a boat the player is not aboard as their own.</summary>
        public const string NotYourBoatText = "Not your boat to sleep aboard.";

        /// <summary>Pressed from outside the cabin, or from another level of it. Unreachable through the
        /// resolver (<see cref="IsAvailable"/> is false there); kept for a direct caller.</summary>
        public const string NotBelowText = "You'd need to be down at the bunk.";

        /// <summary>Pressed with deck work mid-cycle (#193).</summary>
        public const string DeckWorkText = "Not with gear still on the go.";

        /// <summary>Pressed in a co-op session. See <see cref="InCoopSession"/>.</summary>
        public const string CoopText = "Not while the crew's still up.";

        /// <summary>Pressed adrift or under way with the safety rule on.</summary>
        public const string NotSecuredText = "It's not safe to turn in out here.";

        /// <summary>Where the player is turning in, for the notice.</summary>
        public const string PlaceName = "your bunk aboard";

        /// <summary>Why a press was refused, or <see cref="None"/> when it turned the player in.
        /// Public so the tests assert the rule rather than re-typed prose.</summary>
        public enum Refusal
        {
            None = 0,
            NotOnDeck = 1,
            NotBelow = 2,
            NotYourBoat = 3,
            Coop = 4,
            DeckWork = 5,
            NotSecured = 6,
        }

        private string _id = "fixture.boat.bunk";
        private BoatInterior _cabin;
        private BoatController _boat;
        private Vector3 _reachPoint;

        // The #193 guard's live half: what the deck loop and the trap haul last said. Held from the bus
        // rather than polled, because Boats may not reference the Fishing controllers that own them.
        private bool _deckLoopBusy;
        private bool _trapHaulBusy;

        /// <summary>The anchor's reach point, hull-local metres (z = height on the sole).</summary>
        public Vector3 ReachPoint => _reachPoint;

        /// <summary>The cabin this bunk is in.</summary>
        public BoatInterior Cabin => _cabin;

        /// <summary>True while deck work is mid-cycle, as last heard on the bus.</summary>
        public bool DeckWorkUnderway => _deckLoopBusy || _trapHaulBusy;

        // ---- the interact seam ---------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters
        {
            get
            {
                GameConfig config = GameServices.Config;
                return config != null ? config.BunkReachMetres : GameConfig.DefaultBunkReachMetres;
            }
        }

        /// <summary>The berth rung: below her doors, so a bunk never takes the door's press.</summary>
        public int Priority => InteractPriority.Berth;

        /// <summary>From a deck only — walking a cabin sole is <see cref="InteractContext.OnDeck"/>.</summary>
        public InteractContext Contexts => InteractContext.OnDeck;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Inside this hull's cabin, on the bunk's own level. Everything else the press answers
        /// with a line (<see cref="Judge"/>).</summary>
        public bool IsAvailable => IsBelowAtTheBunk;

        /// <inheritdoc/>
        public string VerbLabel => Verb;

        /// <summary>Wire this bunk. Public so the installer never reaches a field, and so a test stands
        /// one up with no scene.</summary>
        public void Configure(BoatInterior cabin, BoatController boat, string id, Vector3 reachPoint)
        {
            _cabin = cabin;
            _boat = boat;
            _id = string.IsNullOrEmpty(id) ? _id : id;
            _reachPoint = reachPoint;
        }

        /// <summary>
        /// Turn in, or be told why not. The actor's own context is checked first: the resolver only
        /// routes an OnDeck press here, and this is where that stops being an assumption.
        /// </summary>
        public void Interact(in InteractActor actor) => TryTurnIn(actor.Context, actor.Position);

        /// <summary>
        /// The whole press as one call: judge it, then either say the refusal or publish the rest.
        /// Returns what was decided. Public so tests drive it without input.
        /// </summary>
        public Refusal TryTurnIn(InteractContext context, Vector2 wakePosition)
        {
            Refusal refusal = Judge(context);
            if (refusal != Refusal.None)
            {
                EventBus.Publish(new DevNotice(TextFor(refusal)));
                return refusal;
            }

            // The SAVE half only, and with no wake spot: see the class remarks. The position and level
            // are still filled honestly (the player's feet, the cabin's level) for any listener that
            // wants them; the responder records neither because WakesAtSpawn says so.
            int level = _cabin != null ? _cabin.Level : 0;
            EventBus.Publish(new RestSaveRequested(PlaceName, wakePosition, level, wakesAtSpawn: true));
            return Refusal.None;
        }

        /// <summary>The rule, in its fixed order. Public and side-effect free.</summary>
        public Refusal Judge(InteractContext context)
        {
            // #193, first half: only ever from a deck.
            if (context != InteractContext.OnDeck) return Refusal.NotOnDeck;
            if (!IsBelowAtTheBunk) return Refusal.NotBelow;
            if (!IsPlayersBoat) return Refusal.NotYourBoat;
            if (InCoopSession()) return Refusal.Coop;
            // #193, second half: never mid-cycle.
            if (DeckWorkUnderway) return Refusal.DeckWork;
            if (RequiresSecured() && !IsSecured()) return Refusal.NotSecured;
            return Refusal.None;
        }

        /// <summary>The player-facing line for a refusal.</summary>
        public static string TextFor(Refusal refusal)
        {
            switch (refusal)
            {
                case Refusal.NotYourBoat: return NotYourBoatText;
                case Refusal.Coop:        return CoopText;
                case Refusal.DeckWork:    return DeckWorkText;
                case Refusal.NotSecured:  return NotSecuredText;
                case Refusal.NotBelow:    return NotBelowText;
                case Refusal.NotOnDeck:   return "Not from here.";
                default:                  return "";
            }
        }

        /// <summary>
        /// Inside this hull's cabin, on the level the berth is on.
        ///
        /// <para>The berth's level is the cabin's OWN answer for the anchor's height
        /// (<see cref="BoatInterior.LevelIndexAtHeight"/>) — the same derivation her doors use for their
        /// sill, so a bunk and a door can never disagree about which floor is which. A cabin that cannot
        /// place the height (−1) accepts any level she is inside on, rather than offering a bunk nobody can
        /// ever reach.</para>
        /// </summary>
        public bool IsBelowAtTheBunk
        {
            get
            {
                if (_cabin == null || !_cabin.IsInside) return false;
                int bunkLevel = _cabin.LevelIndexAtHeight(_reachPoint.z);
                return bunkLevel < 0 || bunkLevel == _cabin.Level;
            }
        }

        /// <summary>The boat the player is aboard (helm or deck) is this one — the Core helm slot's
        /// <see cref="HelmSlot.IsPlayersBoat(object)"/>, keyed on the same controller token the
        /// ControlSwitcher declares.</summary>
        public bool IsPlayersBoat => _boat != null && GameServices.Helm.IsPlayersBoat((object)_boat);

        /// <summary>
        /// Made fast to a cleat, berthed, or holding at anchor. A painter in hand or rooted ashore cannot
        /// count: boarding stows it (<see cref="BoatMooring.Stow"/>), and only a line made fast survives.
        /// </summary>
        public bool IsSecured()
        {
            if (_boat == null) return false;
            var mooring = _boat.GetComponent<BoatMooring>();
            if (mooring != null && mooring.IsMadeFast) return true;
            var anchor = _boat.GetComponent<BoatAnchor>();
            if (anchor != null && anchor.IsHolding) return true;
            var berth = _boat.GetComponent<MooredBoat>();
            return berth != null && berth.Way == VesselWay.Moored;
        }

        private static bool RequiresSecured()
        {
            GameConfig config = GameServices.Config;
            return config != null ? config.BunkRequiresMooredOrAnchored
                                  : GameConfig.DefaultBunkRequiresMooredOrAnchored;
        }

        /// <summary>
        /// ⚠ SEAM(co-op): is a co-op session running? Host-only sleep is a later stage, and main has no
        /// session concept to ask yet (the co-op spike has not landed), so this answers false and the bunk
        /// behaves as single-player. When a Core session seam exists, ask it HERE and nowhere else; the
        /// refusal line and its place in <see cref="Judge"/> are already wired.
        /// </summary>
        private static bool InCoopSession() => false;

        // ---- the #193 guard, heard on the bus ------------------------------------------------

        /// <summary>The stern deck's cycle: anything but an idle deck with nothing in play is work
        /// under way. Public so a test drives it without a deck.</summary>
        public void OnDeckLoopChanged(DeckLoopStateChanged e)
            => _deckLoopBusy = e.State.Stage != SternDeckStage.None || e.State.PotInPlay;

        /// <summary>The trap haul: hauling is work under way; the surfaced/empty beats and idle are
        /// not. Public so a test drives it without a haul.</summary>
        public void OnTrapHaulChanged(TrapHaulStateChanged e)
            => _trapHaulBusy = e.State.Phase == TrapHaulPhase.Hauling;

        private void OnEnable()
        {
            EventBus.Subscribe<DeckLoopStateChanged>(OnDeckLoopChanged);
            EventBus.Subscribe<TrapHaulStateChanged>(OnTrapHaulChanged);
            Interactables.Register(this);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DeckLoopStateChanged>(OnDeckLoopChanged);
            EventBus.Unsubscribe<TrapHaulStateChanged>(OnTrapHaulChanged);
            Interactables.Unregister(this);
        }
    }
}
