namespace HiddenHarbours.Core
{
    /// <summary>Raised once when a new in-game day begins.</summary>
    public readonly struct DayStarted
    {
        public readonly int DayOfSeason;   // 1-based
        public readonly Season Season;
        public readonly int Year;
        public DayStarted(int dayOfSeason, Season season, int year)
        {
            DayOfSeason = dayOfSeason; Season = season; Year = year;
        }
    }

    /// <summary>Raised when the season rolls over.</summary>
    public readonly struct SeasonChanged
    {
        public readonly Season Season;
        public readonly int Year;
        public SeasonChanged(Season season, int year) { Season = season; Year = year; }
    }

    /// <summary>Raised when the tide crosses between rising and falling (a "turn of the tide").</summary>
    public readonly struct TideTurned
    {
        public readonly bool NowRising;
        public readonly float TideHeight;
        public TideTurned(bool nowRising, float tideHeight)
        {
            NowRising = nowRising; TideHeight = tideHeight;
        }
    }

    /// <summary>Raised when a boat touches bottom (draught exceeded local water depth). P5.</summary>
    public readonly struct BoatGrounded
    {
        public readonly object Boat;        // boxed reference to the boat (kept engine-light in Core)
        public readonly float Severity;     // 0..1, how hard
        public BoatGrounded(object boat, float severity) { Boat = boat; Severity = severity; }
    }

    /// <summary>Raised when a fish is landed into the hold.</summary>
    public readonly struct FishCaught
    {
        public readonly CatchItem Item;
        public FishCaught(CatchItem item) { Item = item; }
    }

    /// <summary>Raised when a hold's catch is sold at market.</summary>
    public readonly struct CatchSold
    {
        public readonly int TotalPaid;   // ₲
        public readonly int Count;
        public CatchSold(int totalPaid, int count) { TotalPaid = totalPaid; Count = count; }
    }

    /// <summary>
    /// Raised when spoiled catch is DUMPED out of a hold (the M1 §7.3 dispose verb — emptied on the
    /// wharf, tipped over the side). The hold-reading presenters (deck tray, tote fill) refresh on it
    /// exactly as they do on <see cref="CatchSold"/>; it is its own signal because nothing was paid —
    /// a dump must never flash the payout HUD.
    /// </summary>
    public readonly struct CatchDumped
    {
        public readonly int Count;
        public CatchDumped(int count) { Count = count; }
    }

    /// <summary>Raised whenever the player's balance changes.</summary>
    public readonly struct MoneyChanged
    {
        public readonly int NewBalance;
        public readonly int Delta;
        public MoneyChanged(int newBalance, int delta) { NewBalance = newBalance; Delta = delta; }
    }

    /// <summary>
    /// Raised when the player buys a boat at the Shipwright. The economy side has already deducted the
    /// money; <c>gameplay-systems</c> listens for this to add the hull to the owned fleet and swap the
    /// active boat — so Economy never references the Boats module (cross-module talk via Core/EventBus).
    /// Keyed by stable boat id (e.g. "boat.punt"), matching <c>BoatHullDef.Id</c>.
    /// </summary>
    public readonly struct BoatPurchased
    {
        public readonly string BoatId;
        public readonly int PricePaid;   // ₲
        public BoatPurchased(string boatId, int pricePaid)
        {
            BoatId = boatId; PricePaid = pricePaid;
        }
    }

    /// <summary>
    /// Raised when the active boat changes (an upgrade swap), carrying the framing the camera should
    /// use for that hull. Lets the camera (App) zoom to the new boat WITHOUT referencing the Boats
    /// module — data-driven from <c>BoatHullDef.CameraWorldHeightMeters</c>. Bigger boat = more water
    /// on screen (the tangible "bigger boat" beat). Keyed by stable boat id.
    /// </summary>
    public readonly struct ActiveBoatChanged
    {
        public readonly string BoatId;
        public readonly float CameraWorldHeightMeters;

        /// <summary>The hull's length (m) — <c>BoatHullDef.LengthMeters</c>. The camera FLOORS the
        /// authored framing above by what it takes to actually show a vessel this long (the owner's
        /// 2026-07-29 "whole vessel visible" ruling, §9.8). Carried on the signal because the
        /// derivation belongs to the camera's own pure policy and App cannot reach a
        /// <c>BoatHullDef</c>; 0 (a publisher that does not know the length) simply leaves the
        /// authored framing standing, which is the pre-ruling behaviour.</summary>
        public readonly float HullLengthMeters;

        public ActiveBoatChanged(string boatId, float cameraWorldHeightMeters,
                                 float hullLengthMeters = 0f)
        {
            BoatId = boatId;
            CameraWorldHeightMeters = cameraWorldHeightMeters;
            HullLengthMeters = hullLengthMeters;
        }
    }

    /// <summary>
    /// Raised when the player takes the wheel of a road vehicle (ADR 0035), carrying the framing the
    /// camera should use for her — the land twin of <see cref="ActiveBoatChanged"/>, and published on
    /// entering <see cref="ControlMode.Driving"/> exactly as that one is published on taking the helm.
    ///
    /// <para><b>Why a second signal rather than reusing the boat's.</b> <see cref="ActiveBoatChanged"/>
    /// means "the hull you are piloting is now this one", and the camera is not its only reader — the
    /// owned-fleet reframe and the propulsion audio hang off the same idea. A truck published on it would
    /// be a boat as far as every one of them is concerned. The two carry the same SHAPE because the
    /// question is the same (which thing, framed how); they are separate because the answers are consumed
    /// by different code.</para>
    ///
    /// <para>No length term, deliberately. The hull's is there to FLOOR the framing so a 110 m tanker
    /// fits on screen (the §9.8 ruling); a road vehicle's authored framing already shows her many times
    /// over — the Dually is 6.64 m against an 18 m view — so there is nothing for a floor to do, and a
    /// term nobody needs is a term that goes stale.</para>
    /// </summary>
    public readonly struct ActiveVehicleChanged
    {
        /// <summary>Stable <c>vehicle.*</c> id of the machine now being driven.
        ///
        /// <para>Nothing is published when the wheel is GIVEN UP, which mirrors the helm exactly: the
        /// camera keeps the last framing it was handed and <see cref="ControlModeChanged"/> is what moves
        /// it back to the on-foot step. A "no vehicle" signal would be a second way of saying the same
        /// thing, and two ways of saying it is how they come to disagree.</para></summary>
        public readonly string VehicleId;

        /// <summary>Her authored framing — <c>VehicleDef.CameraWorldHeightMeters</c>.</summary>
        public readonly float CameraWorldHeightMeters;

        public ActiveVehicleChanged(string vehicleId, float cameraWorldHeightMeters)
        {
            VehicleId = vehicleId;
            CameraWorldHeightMeters = cameraWorldHeightMeters;
        }
    }

    /// <summary>
    /// <b>Somebody worked a driver's door</b> (ADR 0035) — raised by the door's own
    /// <see cref="IInteractable"/> registration when the interact verb reaches it, and answered by whoever
    /// owns the control modes.
    ///
    /// <para><b>Why a signal and not a call.</b> The door is a Vehicles-lane component and the state
    /// machine is a Player-lane one; neither may name the other (rule 4). This is the same one-way Core
    /// handoff <see cref="TrapPlaced"/> makes between Fishing and Boats — the publisher states what
    /// happened, and does not care whether anything is listening. With nothing subscribed (an art scene, an
    /// EditMode fixture) working the door is simply a no-op rather than a null reference.</para>
    ///
    /// <para><b>It is a REQUEST.</b> The subscriber re-reads its own gates before honouring it — a blocked
    /// interaction gate, a machine that has stopped being drivable, a player who is not on their feet — so
    /// publishing this never itself puts anyone behind a wheel.</para>
    /// </summary>
    public readonly struct DriveSeatRequested
    {
        /// <summary>The seat being asked for. Never null from the door, but a subscriber must still test
        /// <see cref="IDriveSeat.IsAlive"/> before using it — see that interface's fake-null warning.</summary>
        public readonly IDriveSeat Seat;

        public DriveSeatRequested(IDriveSeat seat) { Seat = seat; }
    }

    /// <summary>
    /// Raised once, after a save has been loaded and its persistent player state has been re-applied to
    /// the live services (the clock seeked, the wallet brought to the saved balance, the owned fleet/
    /// licences/gear restored) — the "resume exactly where it was saved" signal (VS-08 load-restore).
    /// <para>Published by the composition root through <see cref="SaveRestore"/> after restore completes,
    /// so a lane that holds <em>derived</em> live state (e.g. the owned fleet re-granting its hulls) can
    /// re-sync from <see cref="ISaveService.Current"/> on a single, well-defined edge instead of polling.
    /// A new game raises it too (the loaded blob is just a fresh one), so subscribers have one code path.
    /// It carries no payload: consumers read what they need from <see cref="GameServices.Save"/>. The
    /// tide/wind/weather are NOT in scope — they are recomputed from <c>(worldSeed, gameTime)</c>, never
    /// restored (CLAUDE.md rule 5); only the clock + persistent player state are restored before this
    /// fires.</para>
    /// </summary>
    public readonly struct GameLoaded
    {
    }

    /// <summary>
    /// Where the player's control lives (trap arc Build 5 split the old binary on-foot/aboard model).
    /// <list type="bullet">
    ///   <item><see cref="OnFoot"/> — walking the coast; the walk controller drives.</item>
    ///   <item><see cref="Aboard"/> — <b>at the helm</b>, piloting: steering input is live, the walk is
    ///   frozen. The name is kept from the old binary model (least-breaking: every existing consumer
    ///   that read <c>Aboard</c> as "actively piloting" — camera boat-framing, propulsion audio, the
    ///   fleet's reframe-on-grant — stays correct unchanged).</item>
    ///   <item><see cref="OnDeck"/> — standing on the boat's deck as a walkable character (boarded, but
    ///   NOT at the helm). This is where boat/gear work happens (set a pot, haul a trap); steering is
    ///   dead. Boarding lands here; take the helm to get <see cref="Aboard"/>; disembark from here.</item>
    ///   <item><see cref="Driving"/> — <b>behind the wheel of a road vehicle</b> (ADR 0035): the fisher is
    ///   IN the cab, the truck's controller has the input, and the camera is on her. The land twin of
    ///   <see cref="Aboard"/>, and deliberately a state of its own rather than a reuse of it — every
    ///   consumer that reads <c>Aboard</c> means "piloting a BOAT" (the water framing, the propulsion
    ///   audio, the fleet's reframe-on-grant), and a truck answering yes to those would be wrong in each
    ///   one. You enter it from <see cref="OnFoot"/> at the door and leave it back to
    ///   <see cref="OnFoot"/> beside her; there is no deck-equivalent middle state, because a cab is a
    ///   seat rather than a place you walk about in.</item>
    /// </list>
    /// Values are append-only (the enum is a Core contract): OnDeck was appended so the numeric values
    /// of the two original states never shifted, and Driving is appended after it for the same reason.
    /// </summary>
    public enum ControlMode { OnFoot, Aboard, OnDeck, Driving }

    /// <summary>
    /// Raised when control switches between on-foot, on-deck and the helm (board / take the helm /
    /// step back / disembark). Lets the camera (App) retarget between the player and the boat WITHOUT
    /// the switcher referencing the camera — a Player/Boats-lane switcher can't reference App (that
    /// would be circular), so the handoff goes through Core. The boat's framing still arrives via
    /// <see cref="ActiveBoatChanged"/> on taking the helm.
    /// </summary>
    public readonly struct ControlModeChanged
    {
        public readonly ControlMode Mode;
        public ControlModeChanged(ControlMode mode) { Mode = mode; }
    }

    /// <summary>
    /// A short, transient, player-facing GREYBOX notice ("Pot set", "Too shallow here", "+5 herring
    /// bait") — the owner's on-screen feedback for the trap loop so he never needs the Unity Console.
    /// Published by gameplay systems (Fishing etc.) and rendered by whatever toast/dev overlay is
    /// listening (<c>DevToast</c> in the greybox) — the usual one-way Core handoff, so Fishing never
    /// references a UI class. Deliberately dumb (a string): this whole channel is scaffolding that the
    /// diegetic UI direction replaces later; nothing downstream may make gameplay decisions off it.
    /// </summary>
    public readonly struct DevNotice
    {
        public readonly string Text;
        public DevNotice(string text) { Text = text; }
    }

    /// <summary>
    /// Raised when a trap is dropped into the world (trap-fishing arc Build 3). The gameplay side
    /// (<c>Fishing</c>) owns the logical placed trap and its deterministic soak/catch; it publishes this so
    /// the <b>visual</b> side (<c>Boats</c>' buoy) can drop a bobbing buoy at the set position WITHOUT the
    /// two modules referencing each other — the same one-way Core handoff <see cref="BoatPurchased"/> uses
    /// (Fishing never references Boats, Boats never references Fishing). Keyed by the trap's stable
    /// <see cref="PlacedTrapDto.InstanceId"/> so the buoy can be matched and removed on
    /// <see cref="TrapRemoved"/>. Carries a plain position (Core stays engine-light — no GameObject handle).
    /// </summary>
    public readonly struct TrapPlaced
    {
        /// <summary>Stable per-instance id of the placed trap (matches its <see cref="PlacedTrapDto.InstanceId"/>).</summary>
        public readonly string InstanceId;
        /// <summary>World X of the set trap (where the buoy floats).</summary>
        public readonly float PosX;
        /// <summary>World Y of the set trap.</summary>
        public readonly float PosY;
        public TrapPlaced(string instanceId, float posX, float posY)
        {
            InstanceId = instanceId; PosX = posX; PosY = posY;
        }
    }

    /// <summary>
    /// Raised when a placed trap leaves the world — hauled up or cleared (trap-fishing arc Build 3). The
    /// buoy side listens to remove the matching buoy by <see cref="InstanceId"/>. The twin of
    /// <see cref="TrapPlaced"/>; same Core-mediated, one-way handoff.
    /// </summary>
    public readonly struct TrapRemoved
    {
        public readonly string InstanceId;
        public TrapRemoved(string instanceId) { InstanceId = instanceId; }
    }

    /// <summary>
    /// The stage of the manual trap-HAUL minigame (trap-fishing arc Build 4, redesigned Build 6) — haul WITH
    /// the swell (hold as she lifts, ease as she falls). Deliberately tiny and diegetic (the owner's low-HUD
    /// direction): the READ is the rope in the world (its taut/slack shape + a strain shudder) and a
    /// creak/strain audio cue, NOT a HUD bar. This enum + <see cref="TrapHaulState"/> let AUDIO (a creak groan
    /// that tightens with strain, a "good take" clunk) and any future diegetic listener react WITHOUT
    /// referencing the Fishing module — the same Core-mediated handoff the fishing fight uses
    /// (<see cref="FishingStateChanged"/>).
    /// </summary>
    public enum TrapHaulPhase
    {
        /// <summary>Not hauling — nothing to react to.</summary>
        Idle = 0,
        /// <summary>Laid alongside a pot, hauling with the swell — holding on the lift gains line toward the
        /// surface; holding through the drop strains and slips it back.</summary>
        Hauling = 1,
        /// <summary>The pot broke the surface — the catch is landing (a ready trap). A brief success beat.</summary>
        Surfaced = 2,
        /// <summary>Hauled a pot that hadn't soaked — it came up empty ("not ready yet"). Cozy no-op beat.</summary>
        Empty = 3,
    }

    /// <summary>
    /// A read-only snapshot of the live trap-haul minigame (Build 4, redesigned Build 6), published on
    /// <see cref="TrapHaulStateChanged"/> so audio/diegetic listeners can voice the STRAIN and the take
    /// without knowing the take maths or referencing Fishing. Carries the diegetic reads: how taut/strained
    /// the rope is, how far the pot has been hauled, and whether the player is currently taking line well on
    /// the lift (a good take) — enough for a creak that tightens with strain and a satisfying clunk on a good
    /// take. Value struct (no GC).
    /// </summary>
    public readonly struct TrapHaulState
    {
        public readonly TrapHaulPhase Phase;
        /// <summary>0..1 rope strain — the diegetic "how hard is she pulling" read (calm ≈ 0, gale ≈ 1, and it
        /// spikes when you hold THROUGH a drop and fight the loading rope).</summary>
        public readonly float Strain01;
        /// <summary>0..1 haul progress — 0 on the bottom, 1 as the pot breaks the surface.</summary>
        public readonly float Line01;
        /// <summary>True while the player is taking line WELL ON THE LIFT (a clean, gaining take) — the "good
        /// take" clunk cue. False on idle ticks, on a slack pawl, and while fighting/slipping on a drop. (Kept
        /// as <c>PullOnBeat</c> for the append-only Core contract; the meaning is now "a good take on the
        /// lift".)</summary>
        public readonly bool PullOnBeat;
        /// <summary>World X of the hauled pot's BUOY — appended (the Core contract grows append-only) so a
        /// diegetic listener can point AT the worked buoy without referencing Fishing: the deck hauler's
        /// sprite faces it (flipX), audio could pan a creak toward it. (0,0) when idle / no live haul —
        /// only meaningful while <see cref="Phase"/> is <see cref="TrapHaulPhase.Hauling"/>.</summary>
        public readonly float BuoyX;
        /// <summary>World Y of the hauled pot's buoy (see <see cref="BuoyX"/>).</summary>
        public readonly float BuoyY;

        public TrapHaulState(TrapHaulPhase phase, float strain01, float line01, bool pullOnBeat)
            : this(phase, strain01, line01, pullOnBeat, 0f, 0f)
        {
        }

        public TrapHaulState(TrapHaulPhase phase, float strain01, float line01, bool pullOnBeat,
                             float buoyX, float buoyY)
        {
            Phase = phase;
            Strain01 = strain01;
            Line01 = line01;
            PullOnBeat = pullOnBeat;
            BuoyX = buoyX;
            BuoyY = buoyY;
        }

        /// <summary>The idle snapshot (not hauling).</summary>
        public static TrapHaulState Idle => new TrapHaulState(TrapHaulPhase.Idle, 0f, 0f, false);
    }

    /// <summary>Raised on each meaningful beat of the trap-haul minigame (a pull, the surface, going idle) so
    /// audio can voice the strain/clunk. Payload is a value struct (no GC). The twin of
    /// <see cref="FishingStateChanged"/> for the trap haul.</summary>
    public readonly struct TrapHaulStateChanged
    {
        public readonly TrapHaulState State;
        public TrapHaulStateChanged(TrapHaulState state) { State = state; }
    }

    // =====================================================================================
    //  RESTING — the player's own bed, and the manual save it offers
    // =====================================================================================

    /// <summary>
    /// <b>The player has asked to turn in at their own bed</b> — the manual save entry point, and the
    /// first one the game has had (every write until now was an autosave on suspend/quit, or the
    /// shell's own).
    ///
    /// <para><b>Why a signal and not a call on <see cref="ISaveService"/>.</b> The World lane could
    /// legally reach <c>GameServices.Save.Save()</c> — it is a Core interface, so rule 4 is satisfied
    /// either way — but a bed is not a save button. What the owner ruled is a diegetic beat: you walk
    /// to the bed, you lie down, the day is kept. That beat wants a fade, later an animation
    /// (<c>Fisher_sleep</c>), and eventually a "slept until morning" clock move — none of which the
    /// bed should own and none of which belong in the save service either. A signal is the seam those
    /// arrive on: the bed states the INTENT, and whoever is presenting the game answers it. It also
    /// means the bed is EditMode-testable with no save service in the scene at all.</para>
    ///
    /// <para>Answered by <see cref="RestSaveResponder"/>, which is what actually writes.</para>
    /// </summary>
    public readonly struct RestSaveRequested
    {
        /// <summary>Where the player is turning in, for the notice ("your bed at Ginny's"). Never null;
        /// may be empty, which reads as an unnamed bed.</summary>
        public readonly string Place;
        public RestSaveRequested(string place) { Place = place ?? ""; }
    }

    /// <summary>
    /// <b>The outcome of a save the player ASKED for</b>, so a surface can say so. Raised only for a
    /// requested save (<see cref="RestSaveRequested"/>) — the silent autosaves on suspend and quit
    /// stay silent, because a toast nobody asked for during a background is noise.
    ///
    /// <para><see cref="Ok"/> false is a real and expected outcome, not only a fault: a save requested
    /// while the shell is at the title is refused by the service's own write gate. The reason is
    /// player-facing prose.</para>
    /// </summary>
    public readonly struct GameSaved
    {
        /// <summary>True if a blob actually reached the disk.</summary>
        public readonly bool Ok;
        /// <summary>Player-facing prose for what happened. Never null.</summary>
        public readonly string Reason;
        public GameSaved(bool ok, string reason) { Ok = ok; Reason = reason ?? ""; }
    }

    /// <summary>
    /// <b>The player has opened a wardrobe.</b> Published by the wardrobe fixture and answered by the
    /// character-customization lane when it lands; until then nothing subscribes and the fixture's own
    /// <see cref="DevNotice"/> is the whole of the feedback.
    ///
    /// <para><b>Why the signal exists before its consumer.</b> The wardrobe is placed and interactable
    /// in this PR and wired to customization in the next one. Publishing into an empty room is the
    /// cheapest possible seam between the two — the customization PR subscribes and changes nothing
    /// here — and it is exactly what <see cref="Interactables"/> does for a region with no fixtures:
    /// an unanswered signal is a valid state, not a fault.</para>
    /// </summary>
    public readonly struct WardrobeRequested
    {
        /// <summary>Which wardrobe — the fixture's <see cref="IInteractable.Id"/>, so a later
        /// consumer can tell the player's own from one in a shop.</summary>
        public readonly string FixtureId;
        public WardrobeRequested(string fixtureId) { FixtureId = fixtureId ?? ""; }
    }

    // =====================================================================================
    //  THE WARDROBE — what the player is wearing
    // =====================================================================================

    /// <summary>
    /// <b>The player has changed clothes.</b> Published by <see cref="OutfitLocker.Wear(string)"/>
    /// after the choice is persisted, and answered by whoever is drawing the fisher.
    ///
    /// <para><b>Why a signal rather than the picker setting a material.</b> The recolour is a palette
    /// swap on the player's own renderer (ADR 0029), which lives in the Art lane; the picker lives in
    /// UI and the wardrobe fixture in World. None of the three may reference the others (rule 4), and
    /// none of them should have to know how many renderers a fisher has — today the on-foot sprite,
    /// tomorrow a deck rider and a mirror's preview. Core states WHAT is worn; presentation answers
    /// with HOW. It is the shape <c>ActiveBoatChanged</c> already uses to move a hull's paint.</para>
    ///
    /// <para>Raised only on a real change (re-picking what is already on publishes nothing), so a
    /// subscriber may treat every one of these as work worth doing.</para>
    /// </summary>
    public readonly struct OutfitChanged
    {
        /// <summary>The <c>CharacterOutfitDef</c> id now worn. Never null; EMPTY means the outfit the
        /// sheets were baked in, which is a real answer and not an absence.</summary>
        public readonly string OutfitId;

        public OutfitChanged(string outfitId) { OutfitId = outfitId ?? ""; }
    }

    // =====================================================================================
    //  THE NOTEBOOK — the book she carries
    // =====================================================================================

    /// <summary>
    /// <b>Take the notebook out, or put it away.</b> A toggle, not an open: the same request from the
    /// same control both opens and shuts the book, because that is what reaching for a book you are
    /// already holding means.
    ///
    /// <para><b>Why a signal and not a call.</b> The book is drawn by <c>NotebookPresenter</c> in
    /// World; the control that asks for it is a row on the pause menu, in UI. Neither module may
    /// reference the other (rule 4), so Core states WHAT was asked and World answers with HOW — the
    /// shape <see cref="WardrobeRequested"/> already uses for the same reason.</para>
    ///
    /// <para><b>⚠️ It carries no key, because there is no key to carry.</b> The dev-key ledger is
    /// spent A-Z, so the book deliberately binds NOTHING new: it rides controls that already exist.
    /// Anything that can reach the player - a menu row, a desk, a later world fixture - publishes
    /// this, and the book neither knows nor cares which did.</para>
    /// </summary>
    public readonly struct NotebookRequested
    {
        /// <summary>Where the request came from, for flavour and for tests — a menu row, a fixture id.
        /// Never null; EMPTY means "no particular place", which is a real answer.</summary>
        public readonly string SourceId;

        public NotebookRequested(string sourceId) { SourceId = sourceId ?? ""; }
    }
}
