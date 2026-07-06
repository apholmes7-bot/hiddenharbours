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
        public ActiveBoatChanged(string boatId, float cameraWorldHeightMeters)
        {
            BoatId = boatId; CameraWorldHeightMeters = cameraWorldHeightMeters;
        }
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

    /// <summary>Whether the player is walking the coast or sailing the boat.</summary>
    public enum ControlMode { OnFoot, Aboard }

    /// <summary>
    /// Raised when control switches between on-foot and aboard (board / disembark). Lets the camera
    /// (App) retarget between the player and the boat WITHOUT the switcher referencing the camera — a
    /// Player/Boats-lane switcher can't reference App (that would be circular), so the handoff goes
    /// through Core. The boat's framing still arrives via <see cref="ActiveBoatChanged"/> on boarding.
    /// </summary>
    public readonly struct ControlModeChanged
    {
        public readonly ControlMode Mode;
        public ControlModeChanged(ControlMode mode) { Mode = mode; }
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
    /// The stage of the manual trap-HAUL minigame (trap-fishing arc Build 4) — pull the rope in rhythm with
    /// the passing swell. Deliberately tiny and diegetic (the owner's low-HUD direction): the READ is the
    /// rope in the world (its taut shape + a strain shade) and a creak/strain audio cue, NOT a HUD bar. This
    /// enum + <see cref="TrapHaulState"/> let AUDIO (a creak groan, a "good pull" clunk) and any future
    /// diegetic listener react WITHOUT referencing the Fishing module — the same Core-mediated handoff the
    /// fishing fight uses (<see cref="FishingStateChanged"/>).
    /// </summary>
    public enum TrapHaulPhase
    {
        /// <summary>Not hauling — nothing to react to.</summary>
        Idle = 0,
        /// <summary>Laid alongside a ready pot, hauling in rhythm — pulls gain line toward the surface.</summary>
        Hauling = 1,
        /// <summary>The pot broke the surface — the catch is landing (a ready trap). A brief success beat.</summary>
        Surfaced = 2,
        /// <summary>Hauled a pot that hadn't soaked — it came up empty ("not ready yet"). Cozy no-op beat.</summary>
        Empty = 3,
    }

    /// <summary>
    /// A read-only snapshot of the live trap-haul minigame (Build 4), published on
    /// <see cref="TrapHaulStateChanged"/> so audio/diegetic listeners can voice the STRAIN and the beat
    /// without knowing the rhythm maths or referencing Fishing. Carries the diegetic reads: how taut/strained
    /// the rope is, how far the pot has been hauled, and whether the last pull landed on the swell's beat
    /// (a clean pull that gained line) — enough for a creak that tightens with strain and a satisfying clunk
    /// on a good pull. Value struct (no GC).
    /// </summary>
    public readonly struct TrapHaulState
    {
        public readonly TrapHaulPhase Phase;
        /// <summary>0..1 rope strain — the diegetic "how hard is she pulling" read (calm ≈ 0, gale ≈ 1).</summary>
        public readonly float Strain01;
        /// <summary>0..1 haul progress — 0 on the bottom, 1 as the pot breaks the surface.</summary>
        public readonly float Line01;
        /// <summary>True on the tick a pull LANDED ON THE BEAT (a clean pull that gained line) — the "good
        /// pull" clunk cue. False on idle ticks and on a mistimed pull (which gains nothing, no penalty).</summary>
        public readonly bool PullOnBeat;

        public TrapHaulState(TrapHaulPhase phase, float strain01, float line01, bool pullOnBeat)
        {
            Phase = phase;
            Strain01 = strain01;
            Line01 = line01;
            PullOnBeat = pullOnBeat;
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
}
