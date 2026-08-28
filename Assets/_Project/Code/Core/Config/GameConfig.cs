using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Central tunables for the simulation — "no magic numbers" (CLAUDE.md rule 6). The owner
    /// can tune feel here in the Inspector with no code. Create one via
    /// Assets &gt; Create &gt; Hidden Harbours &gt; Game Config and place it in Data/Config.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        /// <summary>The day length the clock ships with — the single fallback consumers use when no
        /// config asset is wired (EditMode, a bare test rig). One constant, never a scattered literal.
        ///
        /// <para>⚠ <b>1800 = a 30-minute day, ruled by the owner 2026-08-01</b> (was 1200 / 20 min). It is
        /// one of the two levers behind "the tide falls too fast" — this one stretches every in-game hour
        /// from 50 to 75 real seconds, so EVERY tide window gets 1.5× longer in real minutes without moving
        /// a single in-game-hour figure. The other lever is St Peters' tide amplitude
        /// (<c>StPetersBuilder.TideAmplitude</c> 3.5 → 2.2 m). Together the peak water-level rate falls
        /// ~2.4×, from ~3.5 cm/s to ~1.5 cm/s.</para>
        ///
        /// <para>Everything else paced in in-game time — freshness, NPC routines, market ticks, day/night,
        /// the moon — slows by the same 1.5× in REAL time. That is the sanctioned consequence of the day-
        /// length ruling, not a regression.</para></summary>
        public const float DefaultSecondsPerDay = 1800f;

        /// <summary>The ruled default for <see cref="InteriorRockScale"/> (ADR 0038 proposal 1) — read
        /// by <c>GameServices.InteriorRockScale</c> when no config is wired, so a test rig and a scene
        /// with no GameConfig draw the same cabin the shipped asset does.</summary>
        public const float DefaultInteriorRockScale = 0.45f;

        /// <summary>The ruled default for <see cref="StairClimbSeconds"/> (ADR 0036) — read by
        /// <c>BuildingInterior</c> when no config is wired, so an EditMode rig, a bare region scene and
        /// the shipped asset all climb a stair at the same pace.</summary>
        public const float DefaultStairClimbSeconds = 0.5f;

        /// <summary>
        /// The default for <see cref="MaxOneHandFishLengthMeters"/> — read by
        /// <c>GameServices.MaxOneHandFishLengthMeters</c> when no config is wired, so an EditMode rig and
        /// a bare art scene split one-hand from two-hand exactly as the shipped asset does.
        ///
        /// <para><b>0.62 m is the ART RIG's own cradle point, converted.</b> <c>fishIsoRig.js</c> holds a
        /// fish in one hand below 2.2 kg and cradles it across both at or above — and 2.2 kg is 0.62 m
        /// through the same cube law <see cref="CatchSize"/> inverts. So the hands agree with the picture
        /// by construction rather than by a number someone matched by eye, and the shipped roster splits
        /// where the rig says it does: a 2 kg cod (0.60 m) hangs from a fist, a 12 kg one (1.09 m) does
        /// not.</para>
        /// </summary>
        public const float DefaultMaxOneHandFishLengthMeters = 0.62f;

        /// <summary>The default for <see cref="FishLengthPerKgCubeRootMeters"/> — the least-squares fit
        /// across <c>fishIsoRig.js</c>'s four declared species (cod, haddock, pollock, mackerel), which
        /// it reproduces to within 4 %. See <see cref="CatchSize"/> for why the law is a cube root.</summary>
        public const float DefaultFishLengthPerKgCubeRootMeters = 0.477f;

        [Header("Clock")]
        [Tooltip("Real seconds per in-game day. 1800 = a 30-minute day (the owner's 2026-08-01 tide-pacing " +
                 "ruling; was 1200 = 20 min). Raising this slows EVERY real-time pace in the game — tide, " +
                 "rot, routines, the moon — because they are all clocked in in-game hours.")]
        public float SecondsPerDay = DefaultSecondsPerDay;
        [Min(1)] public int DaysPerWeek = 7;
        [Min(1)] public int DaysPerSeason = 28;
        [Tooltip("Which weekday is Market Day at Nine Mile Creek (0 = Monday).")]
        public int MarketDayIndex = 4; // Friday

        [Header("Tide")]
        [Tooltip("Principal lunar semidiurnal period in hours (~12.42 = two highs per tidal day).")]
        public float TidalPeriodHours = 12.4206f;

        /// <summary>The canon lunar month — the fallback for consumers with no config wired, and the
        /// sibling of <see cref="DefaultSecondsPerDay"/>. The DRAWN moon and the tide's spring/neap
        /// envelope must derive from the SAME period or full moon stops landing on a spring tide
        /// (vision-and-pillars §5.5), so neither may carry its own copy.</summary>
        public const float DefaultLunarMonthDays = 28f;

        /// <summary>The shipping neap fraction, as a const so code that must reason about neap
        /// water WITHOUT a config asset in hand can read it (the <see cref="DefaultLunarMonthDays"/>
        /// convention). St Peters' shore paint places its intertidal families against neap high
        /// water — amplitude × this — and a literal 0.45 there would be a second copy of a number
        /// the owner tunes here.</summary>
        public const float DefaultNeapAmplitudeFraction = 0.45f;

        [Tooltip("Moon cycle in in-game days; drives the spring/neap envelope. Canon: 28.")]
        public float LunarMonthDays = DefaultLunarMonthDays;
        [Tooltip("At neap, amplitude is this fraction of spring amplitude (0..1).")]
        [Range(0f, 1f)] public float NeapAmplitudeFraction = DefaultNeapAmplitudeFraction;

        [Tooltip("The tide TABLE the player reads to plan a crossing (VS-06): how many days the almanac " +
                 "page covers and how finely it hunts each high and low water. Defaults reproduce the " +
                 "step sizes the HUD gauge and the editor tool already used, so the shipped page reads " +
                 "exactly as before.")]
        public TideTableSettings TideTable = TideTableSettings.Default;

        [Tooltip("The masthead pennant (VS-19) — the boat's own wind instrument. Where the burgee gives " +
                 "up and hangs, the apparent wind at which it flies board-stiff, and how quickly it " +
                 "chases a veering gust. This is the read that replaces squinting at a HUD line.")]
        public TelltaleSettings Telltale = TelltaleSettings.Default;

        [Header("Weather")]
        [Tooltip("Baseline wind strength (m/s) before regional/temporal variation.")]
        public float BaseWindStrength = 4f;
        [Tooltip("How much wind strength swings over time (m/s).")]
        public float WindVariability = 5f;
        [Tooltip("How quickly weather evolves. Larger = slower, smoother changes (hours).")]
        public float WeatherChangeHours = 6f;
        [Tooltip("Base fog tendency for the world (0..1). Regions add their own bias later.")]
        [Range(0f, 1f)] public float BaseFogBias = 0.15f;

        [Header("On-foot / Wading")]
        [Tooltip("Deepest water still WALKABLE on foot (m). At/under this the player wades — walkable but " +
                 "slowed, more as it deepens. 0 would collapse the wade band and make any water a wall. " +
                 "Global for M1 (per-region override is a later item). Owner-tunable feel.")]
        [Min(0f)] public float WadeDepth = 0.5f;
        [Tooltip("Deepest water the player can still move through ON FOOT (m) — the escape-valve limit. " +
                 "Between WadeDepth and this the player SLOW-SWIMS: very slow + vulnerable, used to get OUT " +
                 "toward shallower ground so a rising tide never traps them, never to cross. Deeper than " +
                 "this is BOAT-ONLY — a soft wall stops the player stepping in. Must be > WadeDepth.")]
        [Min(0f)] public float SwimLimit = 2.0f;
        [Tooltip("Move-speed multiplier at the DEEP edge of the wade band (0..1): full speed on dry ground " +
                 "ramps down to this by WadeDepth. Lower = wading feels heavier. Cozy-but-teeth: a drag, " +
                 "not a wall.")]
        [Range(0f, 1f)] public float WadeSlowFactor = 0.6f;
        [Tooltip("Move-speed multiplier in the SLOW-SWIM band (0..1): the crawl the player swims OUT at. " +
                 "Deliberately low so swimming is an escape, never a travel shortcut. Never lethal — just " +
                 "slow + exposed.")]
        [Range(0f, 1f)] public float SwimSlowFactor = 0.25f;

        [Header("Seakeeping forces (ADR 0018 B3 — the sea pushes the boat)")]
        [Tooltip("World-wide seakeeping FORCE policy (the sea fighting back): the master switch + bite " +
                 "strength, how the bite grows with sea state, how exposure falls off with depth (open water " +
                 "bites, the lee of land is sheltered), and how much a head / beam / following sea matters. " +
                 "ON by default with a moderate 'first feel' bite — calm sheltered water is UNCHANGED by " +
                 "construction (force scales by SeaState01 × exposure). Dial Strength to taste; set Enabled " +
                 "off to restore today's flat-water handling. Per-hull response lives on each BoatHullDef.")]
        public SeakeepingSettings Seakeeping = SeakeepingSettings.Default;

        [Header("Storm seakeeping read (ADR 0018 B2.5 — the hull answers the storm)")]
        [Tooltip("World-wide STORM policy for the VISUAL rock (the owner's 'the boat seemed to not be " +
                 "responsive to the waves'): above a storm-start sea state the rock's gains, caps and a " +
                 "mesh hull's rock amplitudes GROW with the sea, a mesh hull gains real front-to-back " +
                 "pitch from the wave slope, output smoothing tightens, and the vertical ride carries " +
                 "WEIGHT (a gravity-capped spring chase — the hull unweights over a steep crest and " +
                 "lands, never bolted to the surface). Calm/moderate water is byte-identical by " +
                 "construction (the blend is exactly 0 below the storm start); Enabled off restores " +
                 "today's read at every sea state. Per-hull character rides each BoatHullDef's " +
                 "existing seakeeping data.")]
        public StormRockSettings StormRock = StormRockSettings.Default;

        [Header("Ground tackle (dropping the hook)")]
        [Tooltip("World-wide ANCHORING policy: the dinghy-class rode a hull carries when her own Def " +
                 "does not say (BoatHullDef.RodeMeters = 0), the swing-circle floor, the firm-limit trio " +
                 "the rode is checked with (the mooring rope's own numbers — one restraint mechanism, two " +
                 "consumers), and how a DRAGGING anchor lets her creep when a rising tide takes the bottom " +
                 "away from the hook. The gate itself is not tunable: she anchors where the rode reaches " +
                 "the seabed, and nowhere else.")]
        public AnchorSettings Anchor = AnchorSettings.Default;

        [Header("The shared wave field (ADR 0018 — ONE sea, every consumer)")]
        [Tooltip("The wind + sea-state → wave-train derivation: how many trains, their wavelengths and " +
                 "amplitudes, the crest sharpening, and the ADR 0027 JONSWAP spectrum (SpectrumBlend 0 = " +
                 "the hand-authored 4-train sea; dial it up for variance in sizes, a fan of directions, " +
                 "and waves that arrive in SETS).\n\n" +
                 "⚠️ THIS IS THE ONLY PLACE THESE LIVE. Eight components used to carry their own copy — " +
                 "the water shader's bridge, the hull rocking, the seakeeping forces, the wake, the " +
                 "buoys, the trap haul and the drift weed — and tuning one without the others meant the " +
                 "hull rode a sea the shader was not drawing, which is the one thing ADR 0018 exists to " +
                 "prevent.")]
        public WaveFieldSettings WaveField = WaveFieldSettings.Default;

        [Tooltip("The presentation smoother shared by every LOOK consumer (ADR 0018 addendum): how " +
                 "languidly the train parameters chase the drifting weather, and the glass snap floor " +
                 "(glass is sacred). The SIM path — seakeeping forces — deliberately does NOT ease; it " +
                 "reads the pure WaveMath at game time, so gameplay never depends on frame rate.")]
        public WaveFieldAnimatorSettings WaveFieldAnimator = WaveFieldAnimatorSettings.Default;

        [Tooltip("WIND FETCH (ADR 0027 #1): how far the wind has blown over open water before it " +
                 "reaches a spot, and what that does to the waves there — lee shores go calm, exposed " +
                 "shores build. SHIPS OFF (Strength 0 = the exact passthrough); turn Strength up to " +
                 "see it.\n\n" +
                 "⚠️ This is ONE field, not a look. The envelope scales the waves the HULL rides as " +
                 "well as the ones the shader draws — deliberately, so the player never sees glass " +
                 "behind a headland and feels open-water swell in it. Raising Strength changes boat " +
                 "feel and wants a feel verdict, like the spectrum did.")]
        public WaveFetchSettings WaveFetch = WaveFetchSettings.Default;

        [Tooltip("BREAKING WAVES (ADR 0040): where the sea gives out and how — the breaker index γ, " +
                 "the Iribarren thresholds that separate a spilling crumble from a plunging barrel, and " +
                 "the whitewater decay. Ships at the TEXTBOOK PHYSICS (γ = 0.78, Battjes' 0.5/3.3/5.0), " +
                 "because these are constants of the sea rather than art direction.\n\n" +
                 "⚠️ WHERE the surf appears is decided by the PAINTED SEABED and the TIDE, not here — " +
                 "depth is waterLevel − seabed, so a bar that boils at half-ebb sleeps at high water with " +
                 "nothing animating it. Widening the plunging band puts barrels on shoals that have not " +
                 "earned them, which is the one dial to be careful with.")]
        public BreakerSettings Breakers = BreakerSettings.Default;

        [Header("Market (VS-16)")]
        [Tooltip("Demand D at the home cove (Coddle Cove) in priceMult = 1/(1+e·S/D). 1 = neutral baseline.")]
        [Min(0.01f)] public float MarketDemandCove = 1f;
        [Tooltip("Demand D at Nine Mile Creek. Set higher than the cove so WHERE you sell matters: Nine Mile Creek " +
                 "pays a premium on a glut (the reason to make the hop), and its supply recovers separately. " +
                 "(economy-and-business §1.2/§1.4)")]
        // ⚠ RENAMED from MarketDemandGreywick (plan-to-m1 §7.10). The old name is the KEY this
        // value is serialised under in every existing GameConfig.asset — including the owner's
        // hand-tuned local copy — so without this attribute the rename would silently reset his
        // tuning to the 1.4 default and nothing would say so.
        [UnityEngine.Serialization.FormerlySerializedAs("MarketDemandGreywick")]
        [Min(0.01f)] public float MarketDemandNineMileCreek = 1.4f;
        [Tooltip("Demand D at the ISLAND GENERAL STORE on St Peters (plan-to-m1 §7.5) — the first market the " +
                 "player meets, and deliberately the WORST. Keep this BELOW MarketDemandNineMileCreek: the " +
                 "gap IS the economic reason to walk the tide-gated sandbar, and it is what teaches 'where " +
                 "you sell matters' in the first hour. A village shop buying clams over the counter is not " +
                 "competing with a wharf that ships to the mainland — the default is a little under the cove " +
                 "baseline, so the store pays worst, the cove middling, Nine Mile Creek best.")]
        [Min(0.01f)] public float MarketDemandStPetersStore = 0.7f;

        // ---- what each outlet PAYS, before any glut (the price LEVEL) ----------------------------
        // ⚠ WHY THIS EXISTS AND DEMAND ALONE WAS NOT ENOUGH. Demand D only ever appears as S/D inside
        // priceMult = 1/(1+e·S/D). At zero supply that term is 1 for EVERY value of D — so on a market
        // nobody has sold into yet, a low-demand outlet and a high-demand one quote the *same* price, and
        // the first clam of the game fetches the same coin at the village counter as on the Creek's wharf.
        // Demand is a GLUT-ABSORPTION lever, not a price-level one, and plan-to-m1 §7.5's "deliberately
        // worse prices" is a level difference. So this is the level term the canon formula already has:
        // effPrice = P0 · demandMood · seasonDemand · priceMult (economy-and-business §1.2). These are the
        // static seed of that `demandMood` — a per-market multiplier on base value, kept SEPARATE from the
        // supply curve so the two stay independently tunable. When the M2 sim gives demandMood its random
        // walk, it multiplies onto this rather than replacing it.
        [Tooltip("What the home cove pays per unit before any glut, as a multiplier on the species' base " +
                 "value. 1 = pays the book price (the neutral baseline).")]
        [Min(0.01f)] public float MarketPriceLevelCove = 1f;
        [Tooltip("What Nine Mile Creek pays per unit before any glut. 1 = the book price; it is the BEST " +
                 "outlet in M1 by virtue of also having the highest demand (it absorbs a glut better).")]
        [Min(0.01f)] public float MarketPriceLevelNineMileCreek = 1f;
        [Tooltip("What the ISLAND GENERAL STORE pays per unit before any glut — the ONE number that makes " +
                 "the crossing pay (plan-to-m1 §7.5). Keep it BELOW MarketPriceLevelNineMileCreek: this is " +
                 "the gap the player feels on their first bucket of clams, and the whole 'where you sell " +
                 "matters' lesson. 0.6 = the village counter pays 60% of dockside. ⚠ Every unit is floored " +
                 "at ₲1 (SellPricing.UnitPrice), so on a 2₲ clam this lever has only 2₲ of room to move — " +
                 "see the note on FishSpeciesDef base values in m1-progression-pacing §5.")]
        [Min(0.01f)] public float MarketPriceLevelStPetersStore = 0.6f;

        [Tooltip("Fraction of a category's accumulated supply (glut) cleared at each daily settle (0..1). " +
                 "Higher = faster price recovery over days (economy-and-business §1.3). Deterministic — fired " +
                 "on day rollover, not per frame.")]
        [Range(0f, 1f)] public float MarketDailyRecovery = 0.5f;

        [Header("The fronted licence fee (plan-to-m1 §7.5 — the Aunt Ginny beat)")]
        // ⚠ THIS NUMBER EXISTS TO PREVENT A DEADLOCK, not for flavour. CatchLicensePolicy.MayLand FAILS
        // CLOSED: a species some licence gates cannot be landed without that licence. The clam licence gates
        // clams, and clams are the player's only income on day one — so "buy the clam licence with clam
        // money" is a hard soft-lock the moment the dig starts consulting the gate. §7.5's ruled fix is a
        // character beat rather than a mechanic: Ginny FRONTS the fee, and the player still walks to the
        // store and buys the licence themselves (they meet the vendor, they learn licences gate species, and
        // it plants a small warm debt). This is the mechanism half of that beat; her words are world-content's.
        // GUARD-RAIL: keep this ≥ the clam licence's Price. StPetersContentValidationTests enforces it, so a
        // reprice of the licence cannot silently re-open the deadlock.
        [Min(0)]
        [Tooltip("₲ Aunt Ginny fronts the player once, so the clam licence can be bought before there is any " +
                 "clam money. Must stay ≥ the clam licence fee (content validation enforces it). Granted " +
                 "once per game, flag-guarded — see FrontedFeeGrant.")]
        public int FrontedLicenceFee = 20;

        [Header("Carrying (two hands — the size a fish must be under to ride one of them)")]
        [Tooltip("How long a fish may be, nose to tail in metres, and still be carried in ONE hand by " +
                 "the gill — leaving the other hand for the rod or the pail. Anything longer is cradled " +
                 "across both arms, so it can only be picked up with both hands free. Default 0.62 is " +
                 "the fish rig's own 2.2 kg cradle point expressed as a length, so the hands and the " +
                 "picture agree by construction. Raise it and the big ones stop being a decision; drop " +
                 "it and even a small cod occupies you completely.")]
        [Min(0f)] public float MaxOneHandFishLengthMeters = DefaultMaxOneHandFishLengthMeters;

        [Tooltip("Metres of fish per cube root of a kilogram — how a landed catch's WEIGHT (the number " +
                 "the item actually carries) becomes the LENGTH the rule above compares against. The " +
                 "fish rig builds mass from length × girth² and girth tracks length, so mass goes as the " +
                 "cube of length; this is that law inverted, fitted to the rig's own four species. " +
                 "⚠️ Not a feel dial — move it only if the fish rig's proportions move.")]
        [Min(0f)] public float FishLengthPerKgCubeRootMeters = DefaultFishLengthPerKgCubeRootMeters;

        [Header("Helm throttle (the notched single-lever throttle)")]
        [Tooltip("Feel of the diegetic notched throttle on engine hulls: how many detents from neutral to " +
                 "full AHEAD and to full ASTERN (reverse usually shorter), and an optional hold-to-repeat " +
                 "rate. Up/Down step a HELD detent (so you can sit at part-throttle — impossible with the " +
                 "old on/off key). The detent value IS the LeverRig 'drive' the console draws. Consumed by " +
                 "ThrottleDetentModel; owner-tunable, no code (rule 6).")]
        public HelmThrottleSettings HelmThrottle = HelmThrottleSettings.Default;

        [Header("Helm overlay (the diegetic instrument card — ADR 0025 S1)")]
        [Tooltip("The screen-space card that shows the active hull's control (tiller or binnacle lever) " +
                 "while piloting a motorised hull: placement, the click-to-focus enlargement, and the " +
                 "pointer-mapping radii. Presentation only — reposition/rescale freely, no code (rule 6).")]
        public HelmOverlaySettings HelmOverlay = HelmOverlaySettings.Default;

        [Header("Depth sounder (the purchasable brow instrument — ADR 0025 S2)")]
        [Tooltip("The diegetic depth sounder: how often it takes a sounding, its shallow-alarm defaults " +
                 "and travel, the alarm flash rate, and where its card sits on screen. Also carries the " +
                 "PLACEHOLDER water temperature the LCD shows until a real water-temperature model " +
                 "exists (there is none in the sim today — no temperature is invented here).")]
        public DepthSounderSettings DepthSounder = DepthSounderSettings.Default;

        [Header("Fish finder (the sonar upgrade — ADR 0025 S3)")]
        [Tooltip("The colour sonar that replaces the plain depth sounder in the same cutout: the vertical " +
                 "RANGE (metres) a freshly fitted unit starts at, how often the scan repaints, where its " +
                 "card sits on screen, how fish marks are sized and scattered, and the four PLACEHOLDER " +
                 "status-strip values. The shallow alarm is NOT here: the finder keeps the depth sounder's " +
                 "alarm and reads its settings, so there is only ever one alarm rule.")]
        public FishFinderSettings FishFinder = FishFinderSettings.Default;

        [Header("Chartplotter (the GPS chart — ADR 0025 S6)")]
        [Tooltip("How much navigation the player may keep (waypoints, route legs, track breadcrumbs), " +
                 "how far the boat must move before the track takes another crumb, and the chart's " +
                 "RANGE ladder in nautical miles. The caps are what keep nav data bounded in the save; " +
                 "the range ladder is what makes a chart of a few-hundred-metre harbour readable.")]
        public ChartplotterSettings Chartplotter = ChartplotterSettings.Default;

        [Header("Radar (the PPI scope — ADR 0025 S5)")]
        [Tooltip("What the radar can see and how it draws it: the RANGE ladder in nautical miles, how " +
                 "hard the set is turned up (gain), how much sea clutter a full gale throws, and how " +
                 "finely the coastline is scanned. The range ladder is what makes a scope of a " +
                 "few-hundred-metre harbour readable; the scan settings are the rule-7 budget for the " +
                 "land echo.")]
        public RadarSettings Radar = RadarSettings.Default;

        [Header("Helm wheel (the grabbable steering wheel — ADR 0025 S2a)")]
        [Tooltip("Mouse-spin feel of the console/sport steering wheel: lock-to-lock turns, coast " +
                 "friction, and the optional self-centre spring (0 = a real cable helm that HOLDS " +
                 "where released; > 0 = springy arcade feel). Rig defaults from wheelRig.js; " +
                 "owner-tunable, no code (rule 6).")]
        public HelmWheelSettings HelmWheel = HelmWheelSettings.Default;

        [Header("Boat-UI windows (draggable/resizable instrument cards — 2026-08-07 ruling)")]
        [Tooltip("The windowed boat UI's chrome sizes and resize/collapse bounds: the hover title " +
                 "strip every instrument card is dragged by, its two buttons, the corner grip, and " +
                 "the scale clamps. Presentation only — where a player parks a window is transient " +
                 "session state, never saved.")]
        public BoatUiWindowSettings BoatUiWindows = BoatUiWindowSettings.Default;

        [Header("The strike (owner drop §10.2 — \"pull back and press maybe?\": BOTH candidates, tunable)")]
        [Tooltip("Which gesture sets the hook on the true take, and how hard the pull-back must be. " +
                 "BOTH candidates ship ON so the owner picks in play — turn one off to feel the other " +
                 "alone. The strike is judged by the same bite sequence the tells render from; these " +
                 "dials only decide what counts as the player striking.")]
        public StrikeSettings Strike = StrikeSettings.Default;

        [Header("Bait economy (owner drop §10.2 — a TENTATIVE ruling behind a flag)")]
        [Tooltip("OFF (default): bait is spent at the BITE — something ate it, landed or not (the " +
                 "2026-07-25 ruling; today's live behaviour). ON: bait is spent only on a LANDED " +
                 "catch — the owner's tentative §10.2 reversal ('perhaps bait is only lost after " +
                 "catching a fish'): teases, missed strikes, lost fights and unlicensed releases " +
                 "cost time only. ⚠ Flagged to economy-sim — flipping this changes bait's real cost " +
                 "per fish, which is a pacing dial (§7.4/§7.5).")]
        public bool BaitSpentOnCatchOnly = false;

        [Header("Rod fight (Rod Fishing v2 — the deep→surface fight, cove defaults)")]
        [Tooltip("Fight-wide DEFAULT tuning for the v2 rod fight (pull-on-slack / maintain-on-run + " +
                 "counter-steer + the deep→surface arc). These are the forgiving cove baselines the owner " +
                 "dials; a species' RodFightDef overrides them per fish later. Two guard-rails keep the cove " +
                 "cozy and are test-enforced: TensionRisePerSec > LandingFillPerSec (a blind sustained pull " +
                 "SNAPS before it lands — skill is a pulse, not a pin) and RunTensionPressure < " +
                 "TensionFallPerSec (MAINTAIN always bleeds tension, even mid-run — a run is a 'back off' " +
                 "tell, never an unavoidable snap).")]
        public RodFightSettings RodFight = RodFightSettings.Default;

        [Header("Flick-cast (Rod Fishing v2 — the gesture cast)")]
        [Tooltip("The mouse-gesture cast that replaced the old press-to-cast: HOLD to start, drag the " +
                 "mouse BEHIND the character to wind the rod back, sweep it forward past them, and RELEASE " +
                 "to let the spool loose. Where you flicked = direction; how fast/far you swept = power " +
                 "(capped by the rod); WHEN you released = quality. A mistimed or weak cast is just a SHORT " +
                 "cast — reel in and go again, no penalty. Every feel dial lives here.")]
        public FlickCastSettings FlickCast = FlickCastSettings.Default;

        [Header("Mooring lines (M2-38 — throw a rope to a cleat, mind the tide)")]
        [Tooltip("The rope you throw to a cleat and make fast: how near you must stand to work a cleat, " +
                 "how close the toss must land to catch, and — the part that carries the seamanship — how " +
                 "much SCOPE (line length) you can pay out, in what steps, and how hard the loop will be " +
                 "worked before it slips. Scope is the player's choice and the tide is the test: too short " +
                 "a line on a falling tide hangs the boat and the loop surrenders (no damage — coil and " +
                 "try again). Every dial is here so the feel is tuned without code.")]
        public MooringLineSettings MooringLine = MooringLineSettings.Default;

        [Header("Ladder boarding (the tide-gap climb — when a step aboard becomes a climb down)")]
        [Tooltip("How the fisher gets aboard when the tide has dropped the boat below the wharf: the GAP " +
                 "at which a step becomes a climb, how near a ladder must be to serve a berth, and the " +
                 "measured rig geometry the climb is driven by. The threshold is the dial that matters — " +
                 "raise it and low water still lets you step across (the ladder becomes a rarity); lower " +
                 "it and the ladder is the ordinary way aboard for most of the tide.")]
        public LadderBoardingSettings LadderBoarding = LadderBoardingSettings.Default;

        [Header("Displaced water (ADR 0023 — the sea's readable drama)")]
        [Tooltip("Owner tuning for the displaced water surface (ADR 0023, phase 2): how much taller " +
                 "the sea DRAWS than it simulates, how wide the tear-safe calm band along every shore " +
                 "is, and how strongly the rare big wave is marked by foam and shade. All read LIVE " +
                 "each water tick (~8 Hz), so tuning this asset in Play moves the sea within a " +
                 "second. WaveExaggeration is THE one shared constant every water-riding visual " +
                 "reads (surface lift now; hull heave, buoys and wake in phase 3) — tune it here " +
                 "and boat and sea stay on the same water, never retuned apart.")]
        public DisplacedWaterSettings DisplacedWater = DisplacedWaterSettings.Default;

        /// <summary>The keyline gate's ship default — <b>OFF</b> (ADR 0031: the 1 px outline is retired
        /// from the world-art style, and the mesh fleet is the first family to go without it). A const so
        /// consumers with no config wired (EditMode, a bare test rig) resolve the SAME style the shipped
        /// game does — and because <c>GameConfig.asset</c> lags the code, the code default here is what
        /// actually ships.</summary>
        public const bool DefaultHullKeylineFlood = false;

        [Header("World-art style (ADR 0031 — the keyline retirement)")]
        [Tooltip("Draw the legacy 1 px keyline around every MESH hull (the fullscreen flood ported from " +
                 "the rigs — ADR 0022 phase 3, rule 2)? OFF is the shipped style since ADR 0031: the " +
                 "outline is retired, and a hull's edge is carried by its own shaded turning faces. ON " +
                 "restores yesterday's look byte-for-byte for an A/B. This gates ONLY the flood — the " +
                 "depth-edge darkening that keeps overlapping parts of one boat readable always runs. " +
                 "Baked SPRITE sheets (buildings, trees, props) still carry drawn keylines until their " +
                 "families are naturally redone; that mixed period is accepted (ADR 0031, Half B skipped).")]
        public bool HullKeylineFlood = DefaultHullKeylineFlood;

        // -----------------------------------------------------------------------------------------
        //  The pixel grid (owner playtest 2026-08-23 — "the running fisher and the Otter go soft")
        // -----------------------------------------------------------------------------------------
        // The locked Pixel Perfect Camera runs GridSnapping.PixelSnapping, and that mode snaps
        // SPRITE RENDERERS to a world grid and nothing else — it never moves the camera, and it never
        // reaches a MeshRenderer. So the camera sampled the snapped world from an arbitrary sub-pixel
        // offset (an asset texel got 2 screen pixels one frame and 3 the next: the "soft while
        // moving" read), and a mesh vehicle was not snapped by anything at all. This puts both
        // RENDERED positions back on the grid; see Core.PixelGrid for the full derivation.
        //
        // ⚠️ A FLAT FIELD WITH A CODE-SIDE DEFAULT, for the same reason the silhouette block below
        // is three flat scalars: a field missing from GameConfig.asset's YAML comes back as the C#
        // type default, and for a bool that is FALSE — the feature silently off, reported as on.

        /// <summary>Ship default — <b>ON</b>. Integer-pixel movement on the play grid is the bible's
        /// own pixel discipline (§3.4, §9.2 "no sub-pixel shimmer"), not an option; OFF exists so the
        /// owner can A/B the exact build that shipped before this flag against the one that snaps.</summary>
        public const bool DefaultPixelGridSnap = true;

        [Header("Pixel grid (the sub-pixel softness fix — bible §3.4 pixel discipline)")]
        [Tooltip("Round the RENDERED position of the camera and of every mesh vehicle onto the " +
                 "current framing's whole-pixel grid? ON is the shipped discipline: a running " +
                 "fisher and a driven Otter keep even, stable texels instead of going soft while " +
                 "they move. OFF restores the pre-fix look exactly — the camera lands wherever its " +
                 "smoothing puts it and a mesh vehicle draws at a raw float position — so the two " +
                 "can be A/B'd on one flag. This NEVER touches a simulated body: the physics " +
                 "position stays the honest float, only the drawn transform is rounded (rule 5), so " +
                 "determinism and every saved value are unaffected.")]
        public bool PixelGridSnap = DefaultPixelGridSnap;

        // -----------------------------------------------------------------------------------------
        //  The silhouette through foliage (owner ruling, 2026-08-16)
        // -----------------------------------------------------------------------------------------
        // "Slight occlusion — you always know where you are, you never lose your character." Dense
        // woods draw in FRONT of the fisher, opaque and correctly sorted; she reads through them as a
        // tinted silhouette. These three are the look, and they are the owner's to tune.
        //
        // ⚠️ THREE SCALARS, NOT A SETTINGS STRUCT, and that is deliberate. GameConfig.asset lags the
        // code: a field the shipped YAML has never heard of keeps its initializer here, but a field
        // MISSING FROM INSIDE a serialized struct comes back as the C# default — zero — because the
        // struct as a whole is overwritten by what the asset does carry. Packed into a struct, an
        // un-re-serialized asset would therefore ship strength 0 and a black tint: the feature silently
        // off, reported as on. Flat fields with code-side Default consts cannot fail that way, and
        // GameConfigSilhouetteDefaultTests pins it against the REAL shipped asset.

        /// <summary>Ship default — <b>ON</b>. The whole point of the arc is that the woods can be made
        /// deep without losing the fisher in them, so the silhouette is not an option the density pass
        /// depends on someone remembering to switch on.</summary>
        public const bool DefaultFoliageSilhouette = true;

        /// <summary>Ship default strength — how far a covered foliage pixel moves toward the tint.
        /// 0.55 reads as "clearly her, clearly behind something": low enough that the canopy still
        /// looks like canopy (the leaves keep better than half their own colour), high enough to find
        /// her at a glance against dark spruce, which is the darkest thing she can stand behind on
        /// this coast.</summary>
        public const float DefaultFoliageSilhouetteStrength = 0.55f;

        /// <summary>Ship default tint — a pale warm bone. NOT white: a pure white silhouette reads as a
        /// UI cutout pasted over the world, and this coast's palette has no pure white in it. Warm
        /// because every light in this game is warm (sun, lamp, window) and a cool silhouette would
        /// read as a ghost rather than as a person with the light behind her.</summary>
        public static readonly Color DefaultFoliageSilhouetteTint = new Color(0.94f, 0.90f, 0.82f, 1f);

        [Header("Foliage silhouette (the fisher read through dense woods)")]
        [Tooltip("Let the player read through foliage that draws in front of her? ON is the shipped " +
                 "look: trees and shrubs stay opaque and correctly sorted, and her shape shows through " +
                 "them in the tint below. OFF restores plain occlusion, where deep woods can hide her " +
                 "completely. This costs one extra draw call for the whole screen however dense the " +
                 "forest is, so it is a look switch and not a performance one.")]
        public bool FoliageSilhouette = DefaultFoliageSilhouette;

        [Tooltip("The colour a foliage pixel moves toward where the player is behind it. A pale warm " +
                 "bone by default. Pure white reads as a UI cutout pasted over the world.")]
        public Color FoliageSilhouetteTint = DefaultFoliageSilhouetteTint;

        [Range(0f, 1f)]
        [Tooltip("How far toward the tint a covered foliage pixel moves. 0 is off (and genuinely free " +
                 "— the foliage skips the lookup entirely), 1 is a flat opaque cutout of her shape " +
                 "with no leaf detail left in it. 0.55 is the shipped read.")]
        public float FoliageSilhouetteStrength = DefaultFoliageSilhouetteStrength;

        [Header("Depth drop (Rod Fishing v2 — the weighted rig's fall + the slack bottom tell)")]
        [Tooltip("The depth-fishing game's tunables (drop a weighted rig, count the fall, feel the floor): " +
                 "how fast a rig sinks per kilogram (heavier = faster — the whole 'count the fall' read), " +
                 "how much line the reel carries, the just-off-the-floor sweet window, the fishing depth " +
                 "zones in metres, and how strongly the held depth weights the catch toward the species " +
                 "that live there. Dial these to make depth feel readable; they never make a fish " +
                 "impossible — depth is a WEIGHT on the catch roll, not a wall.")]
        public DepthDropSettings DepthDrop = DepthDropSettings.Default;

        [Header("Bait & tackle (what's on the hook — a WEIGHT on the catch, never a wall)")]
        [Tooltip("How much the right bait and the right tackle matter. Bait is the PRECISE tool (a fish " +
                 "that wants food refuses the wrong food); tackle is the BROAD one (a curious fish will " +
                 "still hit the wrong lure). Turn the boosts to 1 and the damps to 1 to make what's on " +
                 "the hook irrelevant again.")]
        public BaitTackleSettings BaitTackle = BaitTackleSettings.Default;

        [Header("Jigging (working the lure — the five hand-feels)")]
        [Tooltip("What each kind of tackle wants you to DO with it, and how much it matters. Bait fishes " +
                 "itself; a lure only fishes if you work it, and each one wants its own tempo and stroke " +
                 "size. Raise Tolerance01 to make the actions easier to find by feel; raise " +
                 "DeadLureFraction01 to be kinder to a player who leaves the rod still.")]
        public JiggingSettings Jigging = JiggingSettings.Default;

        [Header("The sea in the fight (P1 — rough water fishes better and fights harder)")]
        [Tooltip("How much the WEATHER matters to fishing. Rough water is a TRADE: the fish are bolder so " +
                 "bites come quicker and run bigger, but the swell works your line so every fish is " +
                 "harder to hold — and in a real blow a good fish can genuinely beat you. Set every " +
                 "factor to 0 to go back to weather-blind fishing, where a gale plays exactly like a " +
                 "flat calm.")]
        public SeaFishingSettings SeaFishing = SeaFishingSettings.Default;

        [Header("Fish schools (ADR 0025 S3 — WHERE the fish are, the thing the finder draws)")]
        [Tooltip("The deterministic school sim: how often a patch of water holds fish, how long they " +
                 "hang about, how big the patch is, how deep they sit, how many show — and how much all " +
                 "of that speeds the bite and steers WHICH fish takes. One model: the marks on the fish " +
                 "finder's glass ARE these schools, and these schools are what changes the fishing. " +
                 "Recomputed from (worldSeed, gameTime, place, weather, season) like the tide — never " +
                 "saved, so tuning these moves the whole sea at once with no save surgery.")]
        public FishSchoolSettings FishSchools = FishSchoolSettings.Default;

        [Header("Freshness & rot (M1 §7.3 — the clock on every catch)")]
        [Tooltip("How fast each storage mode rots a landed catch, and how far gone a catch can be " +
                 "before no buyer will take it. The per-species base rate lives on each " +
                 "FishSpeciesDef.SpoilPerDay; these are the world-policy dials on top of it.")]
        public FreshnessSettings Freshness = FreshnessSettings.Default;

        [Header("Fuel (fuel-and-refuelling.md §9 — the shape of the burn, whole-fleet)")]
        [Tooltip("How thirst rises with throttle, with the catch aboard, and with the sea — the " +
                 "dimensionless curve every hull's burn is multiplied by. A particular boat's thirst is " +
                 "her own BoatHullDef.FullThrottleLitresPerHour; this is the shape they all share.\n\n" +
                 "⭐ If fuel feels too thirsty or too free ACROSS THE BOARD, move BurnScale — one " +
                 "number re-prices fuel for the whole game, instead of re-authoring 38 hull assets.")]
        public FuelSettings Fuel = FuelSettings.Default;

        [Header("Pots (trap-fishing — the starter kit)")]
        [Tooltip("Pots granted ONCE per game as the cozy starter kit (Economy's StartingPots, flag-" +
                 "guarded): a new game starts with these, and an existing save gets them on its first " +
                 "load after the pots-are-owned update — so nobody is ever stranded potless mid-loop. " +
                 "Each entry names an authored TrapDef by stable id with a count. Owner-tunable; every " +
                 "FURTHER pot is bought at the shipwright (the P2 money wheel).")]
        public PotStarterEntry[] StarterPotKit =
        {
            new PotStarterEntry("trap.lobster", 2),
            new PotStarterEntry("trap.crab", 1),
        };

        [Header("Player zoom (the wheel is the player's eye — owner ruling 2026-08-19)")]
        [Tooltip("How far the mouse wheel may take the WALKING view in and out, in metres of world " +
                 "height. Closest is the interior close-up (you are reading a room); farthest is the " +
                 "outdoor walk-and-look. Both quantise to the camera's integer pixel-perfect steps, so " +
                 "typing any number picks the nearest crisp stop rather than a blurry one — and the " +
                 "wheel only ever moves the ON-FOOT view: the helm keeps the hull's ruled framing, the " +
                 "deck keeps its own. Untick WheelEnabled to take the wheel out of the game entirely.")]
        public PlayerZoomSettings PlayerZoom = PlayerZoomSettings.Default;

        [Header("Boat interiors (ADR 0038 — the cabin that rides)")]
        [Tooltip("How much of her own rock a boat's INTERIOR draw takes — the comfort clamp ruled by " +
                 "ADR 0038 proposal 1. 1 = full fidelity (exactly the hull's own roll/pitch/heave, which " +
                 "is what the kit bakes); 0 = dead flat, the ACCESSIBILITY setting; 0.45 is the ruled " +
                 "default. A cabin fills the frame in a way a deck does not, so the same rock that reads " +
                 "as life outdoors reads as nausea indoors.\n\n" +
                 "⚠ It is a comfort filter on ONE DRAW. It must never feed back into the hull's pose, " +
                 "her handling, or anything saved (rule 5) — the boat moves exactly as she did at every " +
                 "value, and only the picture of her inside is calmed. That is safe only because the " +
                 "interior and the exterior are never co-visible: entering is a LAYER SWAP (ADR 0038 " +
                 "proposal 3), so the two poses are never on screen together to disagree.")]
        [Range(0f, 1f)] public float InteriorRockScale = DefaultInteriorRockScale;

        [Header("Interior stairs (ADR 0036 — the climb between storeys)")]
        [Tooltip("How long the player takes to walk up (or down) one storey, in seconds. The storey " +
                 "above is drawn at its true height, so the two floors are a real distance apart and " +
                 "the fisher walks that distance rather than blinking across it.\n\n" +
                 "Feel, not physics: the climb covers no GROUND, so this is not a speed and no honest " +
                 "stride can be derived from it. Short reads as a step up and long reads as a cutscene; " +
                 "0.5 s is the ruled default. Set it to 0 for an instant swap — the pre-2026-08-23 " +
                 "behaviour, and a legitimate accessibility answer for anyone who does not want the " +
                 "camera moved for them.")]
        [Min(0f)] public float StairClimbSeconds = DefaultStairClimbSeconds;

        // Convenience
        /// <summary>
        /// THE shared displacement exaggeration (ADR 0023 §(2)) — the accessor every water-riding
        /// consumer reads: the displaced surface's vertex lift today; phase 3's hull heave and every
        /// buoy/wake/oar anchor that turns wave metres into screen metres. Always read it from here
        /// (through <see cref="ShoreFadeMath.DisplacedHeight"/>), never cache a copy — the
        /// overlay-pose lesson made structural: a boat's heave must ride exactly the sea it is
        /// drawn on, including while the owner is tuning this value in Play.
        /// </summary>
        public float WaveExaggeration => DisplacedWater.WaveExaggeration;

        public float SecondsPerHour => SecondsPerDay / 24f;
        public float SecondsPerWeek => SecondsPerDay * DaysPerWeek;
        public float SecondsPerSeason => SecondsPerDay * DaysPerSeason;
        public float SecondsPerYear => SecondsPerSeason * 4f;
    }

    /// <summary>
    /// The fight-wide default tuning for the v2 rod fight (<see cref="GameConfig.RodFight"/>), named and
    /// owner-tunable (rule 6). Lives in Core beside the config it rides on — the same Core-policy /
    /// feature-consumer split as <see cref="SeakeepingSettings"/> (Core) vs its Boats-side math: Core cannot
    /// reference the Fishing module (rule 4), so the tunables live here and the pure math that consumes them
    /// (<c>RodFightMath</c>, Fishing-side) takes them as floats. Per-species overrides arrive later as a
    /// <c>RodFightDef</c> (lead-architect's contract) carrying these same six fields.
    ///
    /// All rates are in normalised gauge-units (0..1) per second; the caller integrates and clamps.
    /// <see cref="Default"/> is the forgiving-cove reference tuning and satisfies both guard-rails
    /// (<c>RodFightMath.PullAloneSnapsBeforeLanding</c> / <c>MaintainOutbleedsTheRun</c>), which the
    /// EditMode tests assert against the shipped asset.
    /// </summary>
    [System.Serializable]
    public struct RodFightSettings
    {
        [Tooltip("Tension gained per second while PULLING (reeling), 0..1-gauge/s. The snap pressure of a " +
                 "held reel. Guard-rail: must exceed LandingFillPerSec, so a blind sustained pull always " +
                 "snaps before it lands (skill is a pulse, not a pin).")]
        [Min(0f)] public float TensionRisePerSec;

        [Tooltip("Tension bled per second while MAINTAINING (holding steady, not reeling), 0..1-gauge/s. " +
                 "The recovery of backing off. Guard-rail: must exceed RunTensionPressure, so a MAINTAIN " +
                 "nets tension DOWN even through her hardest run.")]
        [Min(0f)] public float TensionFallPerSec;

        [Tooltip("Landing gained per second by a clean REEL of her slack, 0..1-gauge/s — and the ceiling on " +
                 "reeling against a run you're fully leaning into. The pace of a well-fought fight. Keep " +
                 "below TensionRisePerSec (the snap-before-land guard-rail).")]
        [Min(0f)] public float LandingFillPerSec;

        [Tooltip("EXTRA tension per second her run adds at full effort (fishEffort01 = 1), applied whether " +
                 "pulling or maintaining — she is fighting too. Keep below TensionFallPerSec so a run is a " +
                 "'back off' tell, never an unavoidable snap.")]
        [Min(0f)] public float RunTensionPressure;

        [Tooltip("Tension bled per second by leaning FULLY against a full run — live from the hookup, deep " +
                 "or surfaced; the same magnitude is the penalty for going WITH her. Bigger = the lean " +
                 "matters more, and the fight is more about direction than timing.")]
        [Min(0f)] public float CounterSteerRelief;

        [Tooltip("Landing fraction (0..1) at which she breaks the surface: the fight crosses Deep (she is " +
                 "unseen — you read her through the rod and the line's entry point) → Surface (she is " +
                 "visible at the end of your line). The fight itself is the same either side; this is how " +
                 "long she stays down. Lower = she shows herself sooner.")]
        [Range(0f, 1f)] public float SurfaceThreshold01;

        [Tooltip("DECK FISHING — the 'light real factor' (rod v2 §4.2): EXTRA tension per second " +
                 "(0..1-gauge/s) at the WORST deck stance, i.e. the line running fully ACROSS the hull " +
                 "(the fish off the far rail / astern of the wrong side while the unmanned boat " +
                 "weathervanes under you). It fades linearly to 0 as you walk the rail toward a clean " +
                 "line, and is exactly 0 anywhere off a boat — dock and shore fishing never feel it. " +
                 "0 = OFF: deck fights read exactly like the dock (set 0 to feel dock-parity first). " +
                 "Guard-rail: keep below TensionFallPerSec − RunTensionPressure so backing off still " +
                 "recovers even at the worst stance mid-run (cozy — a bad angle is a 'walk the rail' " +
                 "nudge, never an unavoidable snap; test-enforced on the default).")]
        [Min(0f)] public float DeckAngleFactor;

        /// <summary>
        /// The forgiving-cove reference tuning: a pull loads clearly faster than it lands (0.55 &gt; 0.35 —
        /// the blind hold snaps first), a maintain bleeds twice her run's pressure (0.70 &gt; 0.35 — backing
        /// off always recovers), a moderate counter-steer axis, the surface break at half-landed so both
        /// halves of the arc get play, and a gentle deck-angle factor (0.15) that keeps the on-deck
        /// guard-rail comfortably true (0.35 + 0.15 &lt; 0.70 — a maintain still bleeds at the worst
        /// stance mid-run; set it to 0 for exact dock-parity).
        /// </summary>
        public static RodFightSettings Default => new RodFightSettings
        {
            TensionRisePerSec = 0.55f,
            TensionFallPerSec = 0.70f,
            LandingFillPerSec = 0.35f,
            RunTensionPressure = 0.35f,
            CounterSteerRelief = 0.45f,
            SurfaceThreshold01 = 0.5f,
            DeckAngleFactor = 0.15f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>notched single-lever throttle</b> (<see cref="GameConfig.HelmThrottle"/>).
    /// The throttle is no longer on/off: Up/Down each step a <b>held</b> detent, so the player can hold
    /// part-throttle — the thing a keyboard couldn't do. These counts define how many detents lie between
    /// neutral and each end of travel; <c>ThrottleDetentModel</c> (Core) consumes them and produces the
    /// signed <c>drive</c> both the physics (<c>BoatController.SetControl</c>) and the diegetic lever
    /// (<c>LeverRig</c>) read. Reverse travel is usually shorter than ahead, like a real binnacle.
    /// </summary>
    [System.Serializable]
    public struct HelmThrottleSettings
    {
        [Tooltip("Detents between neutral and FULL AHEAD (≥ 1). More = finer throttle control; each Up " +
                 "press advances one. E.g. 4 gives quarter-throttle steps (0 / 0.25 / 0.5 / 0.75 / 1).")]
        [Min(1)] public int AheadNotches;

        [Tooltip("Detents between neutral and FULL ASTERN (≥ 1). Usually fewer than ahead (reverse has " +
                 "shorter travel). Full astern is still normalised to drive −1 (the astern-power factor on " +
                 "the hull does the 'weaker reverse'), so this is only how many steps to get there.")]
        [Min(1)] public int AsternNotches;

        [Tooltip("Hold-to-repeat rate, detents/second, while Up or Down is HELD (0 = edge-only: one detent " +
                 "per press, the deliberate binnacle feel). > 0 lets a held key walk the throttle open.")]
        [Min(0f)] public float HoldRepeatPerSec;

        [Tooltip("How long a throttle key must be HELD (real seconds) before hold-to-repeat starts " +
                 "walking detents (the owner's 'auto-repeat after a delay'). The press itself always " +
                 "steps immediately; this only delays the repeats.")]
        [Min(0f)] public float HoldRepeatDelaySec;

        [Tooltip("The neutral snap window, in drive units around 0: a mouse-dragged lever RELEASED " +
                 "inside this window clicks into the neutral detent (a real lever's centre gate); " +
                 "released anywhere else it holds exactly where it was left.")]
        [Range(0f, 0.5f)] public float NeutralSnapWindow01;

        /// <summary>Four ahead notches (quarter steps), two astern; press steps at once, repeats walk on
        /// after 0.35 s at 3/s (the owner's stepped-and-held directive, 2026-08-03); a ±0.08 centre gate.</summary>
        public static HelmThrottleSettings Default => new HelmThrottleSettings
        {
            AheadNotches = 4,
            AsternNotches = 2,
            HoldRepeatPerSec = 3f,
            HoldRepeatDelaySec = 0.35f,
            NeutralSnapWindow01 = 0.08f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>helm instrument overlay</b> (<see cref="GameConfig.HelmOverlay"/>) — the
    /// screen-space card that shows the active hull's diegetic control (the tiller or the binnacle
    /// lever, ADR 0025 S1) while piloting a motorised hull. Placement, the click-to-FOCUS enlargement
    /// (owner addition 2026-08-03: clicking an instrument brings it to a bigger state where its controls
    /// are properly clickable; Esc/click-away returns), and the pointer-mapping radii all live here so
    /// the whole feel is dialled in the Inspector with no code (rule 6). Presentation preferences only —
    /// never sim state, never saved (rule 5).
    /// </summary>
    [System.Serializable]
    public struct HelmOverlaySettings
    {
        [Tooltip("Scale of the small dash-card state (screen pixels per rig pixel). 1 = native rig size.")]
        [Min(0.1f)] public float SmallScale;

        [Tooltip("Scale of the FOCUSED state (click the instrument to enlarge; Esc/click-away returns). " +
                 "Bigger = controls easier to hit; the rig's hit geometry scales with it.")]
        [Min(0.1f)] public float FocusScale;

        [Tooltip("Where the SMALL card centres horizontally, as a 0..1 fraction of screen width. " +
                 "0.5 = bottom-centre (the owner's placement ruling, 2026-08-03).")]
        [Range(0f, 1f)] public float SmallCenterX01;

        [Tooltip("Where the FOCUSED card centres horizontally, as a 0..1 fraction of screen width.")]
        [Range(0f, 1f)] public float FocusCenterX01;

        [Tooltip("Margin (px) from the screen's bottom edge — BOTH states anchor to the bottom of the " +
                 "screen (the helm rises from the dash).")]
        [Min(0f)] public float MarginY;

        [Tooltip("How close (rig-space px) a click must land to the lever's grip to START a drag; " +
                 "clicks further out on the card jump-to-sig along the travel arc instead.")]
        [Min(1f)] public float GrabRadiusPx;

        [Tooltip("The tiller's throttle drag travel: how many rig-space px of vertical drag sweep the " +
                 "drive across its FULL range (up = ahead). Smaller = twitchier.")]
        [Min(1f)] public float TillerDragFullDrivePx;

        [Tooltip("Scale of the COMPOSED DASH's small state (S2a — the 600×510 console/sport card is " +
                 "far bigger than a lone instrument, so it gets its own dial). 0.5 = half rig size.")]
        [Min(0.1f)] public float DashSmallScale;

        [Tooltip("Scale of the composed dash's FOCUSED state. Clamped at runtime so the card always " +
                 "fits the screen.")]
        [Min(0.1f)] public float DashFocusScale;

        /// <summary>Native-size card CENTRED AT THE BOTTOM (the owner's 2026-08-03 placement ruling),
        /// 2× focus rising from the same bottom-centre anchor.</summary>
        public static HelmOverlaySettings Default => new HelmOverlaySettings
        {
            SmallScale = 1f,
            FocusScale = 2f,
            SmallCenterX01 = 0.5f,
            FocusCenterX01 = 0.5f,
            MarginY = 16f,
            GrabRadiusPx = 30f,
            TillerDragFullDrivePx = 140f,
            DashSmallScale = 0.5f,
            DashFocusScale = 1.5f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>grabbable steering wheel</b> (<see cref="GameConfig.HelmWheel"/> —
    /// ADR 0025 S2a). The spin model itself is the wheel rig's own
    /// (<c>docs/art/rigs/ui/console-wheel/Art/wheelRig.js</c> <c>step()</c>, ported to
    /// <c>WheelRigGeometry</c>); these are the three knobs the rig exposes, defaulted to its own
    /// values, plus the rim-grab pad. The wheel only ever DRIVES steer during a focused grab — it
    /// otherwise mirrors <c>BoatController.Steer</c> (one state, one owner).
    /// </summary>
    [System.Serializable]
    public struct HelmWheelSettings
    {
        [Tooltip("Lock-to-lock turns EACH WAY (wheelRig.js default 1.5 — cable steer on a 7 m " +
                 "skiff). Full lock = turns × 360° of wheel; steer = wheel angle / lock.")]
        [Min(0.25f)] public float Turns;

        [Tooltip("Coast friction (per-second exponential decay of spin velocity) after the rim is " +
                 "released. wheelRig.js default 2.4. Higher = the wheel dies faster.")]
        [Min(0f)] public float Friction;

        [Tooltip("Self-centre spring (0 = a working cable helm: released, the wheel coasts and " +
                 "HOLDS — the rig's stock feel). > 0 opts into a springy arcade return-to-centre.")]
        [Min(0f)] public float SelfCentre;

        [Tooltip("How far outside the wheel's outer rim (rig px) a grab still catches — the same " +
                 "kind of forgiveness as the lever's GrabRadiusPx.")]
        [Min(0f)] public float RimGrabPadPx;

        [Tooltip("KEY STEER EASE (S4.5, the owner's 'the wheel needs to follow the arrow keys — " +
                 "gradual and smooth'): how long (real seconds) a held steer key takes to wind the " +
                 "helm from CENTRE to FULL LOCK. A full reversal takes twice this — a wheel does not " +
                 "spin faster because you asked for more of it. Releasing winds back to centre at the " +
                 "same rate. ⚠ This eases the commanded STEER, not just the wheel picture, so it is a " +
                 "handling change as well as a look: full lock arrives this much later than it used " +
                 "to. 0 = OFF (the pre-S4.5 instant lock-to-lock step). The gamepad stick is already " +
                 "analog and is passed through undamped.")]
        [Min(0f)] public float SteerEaseSeconds;

        /// <summary>The wheel rig's own stock feel (wheelRig.js:183-191): 1.5 turns, friction 2.4,
        /// no self-centre (cable steer holds), an 8 px rim-grab pad — plus a quarter-second wind to
        /// full lock on the keys (S4.5; the owner tunes it, code default ships).</summary>
        public static HelmWheelSettings Default => new HelmWheelSettings
        {
            Turns = 1.5f,
            Friction = 2.4f,
            SelfCentre = 0f,
            RimGrabPadPx = 8f,
            SteerEaseSeconds = 0.25f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>boat-UI windows</b> (<see cref="GameConfig.BoatUiWindows"/> — the
    /// 2026-08-07 windowing ruling: every instrument card draggable, resizable, collapsible, plus the
    /// one hide-all input). The chrome's strip/button/grip sizes and the resize/collapse scale bounds
    /// live here so the whole feel is dialled in the Inspector with no code (rule 6). Presentation
    /// preferences only — where a player parks a window is transient session state (rule 5), never
    /// saved, and never stored here.
    ///
    /// <para><b>The size floor is about grabbability, not legibility.</b> Resizing a window only
    /// re-targets the destination rect of the instrument's ONE native raster (the letterbox
    /// contract) — no rig is ever re-rendered small, so no font law is in play at any size. MinScale
    /// simply keeps a window big enough to grab back.</para>
    /// </summary>
    [System.Serializable]
    public struct BoatUiWindowSettings
    {
        [Tooltip("Height (screen px) of the window title strip that appears on hover above each " +
                 "boat-UI card — the grab handle for dragging.")]
        [Min(0f)] public float TitleBarPx;

        [Tooltip("Width (screen px) of the two strip buttons (collapse tier, hide).")]
        [Min(0f)] public float ChromeButtonPx;

        [Tooltip("Size (screen px) of the corner resize grip inside the card's bottom-right.")]
        [Min(0f)] public float GripPx;

        [Tooltip("The COMPACT collapse tier's scale multiplier on the window's Full size — the " +
                 "glance-sized middle tier between Full and the bare title bar.")]
        [Range(0.1f, 1f)] public float CompactScale;

        [Tooltip("Floor on the per-window resize multiplier. Grabbability, not legibility — the " +
                 "raster is never re-rendered, only re-targeted.")]
        [Min(0.05f)] public float MinScale;

        [Tooltip("Ceiling on the per-window resize multiplier (the window still clamps to the " +
                 "screen and under the HUD band whatever this says).")]
        [Min(0.1f)] public float MaxScale;

        /// <summary>An 18 px strip with 22 px buttons and a 14 px grip; Compact at 55% of Full;
        /// resize clamped 0.35×–3× of the dialled base size.</summary>
        public static BoatUiWindowSettings Default => new BoatUiWindowSettings
        {
            TitleBarPx = 18f,
            ChromeButtonPx = 22f,
            GripPx = 14f,
            CompactScale = 0.55f,
            MinScale = 0.35f,
            MaxScale = 3f,
        };
    }

    /// <summary>
    /// Owner tuning for the <b>depth sounder</b> (<see cref="GameConfig.DepthSounder"/>) — the first
    /// purchasable brow instrument (ADR 0025 S2, <c>docs/art/rigs/ui/depth-finder/</c>). Everything the
    /// instrument's behaviour and placement depends on lives here so it is dialled in the Inspector with
    /// no code (rule 6).
    ///
    /// <para><b>The reading itself is not tunable</b> — it is <c>waterLevel − seabedElevation</c> over the
    /// one shared height map (<see cref="DepthSounder.DisplayDepth"/>, rule 5). These are the instrument's
    /// dials, not the sea's.</para>
    ///
    /// <para><b>Why the card placement lives here and not in <see cref="HelmOverlaySettings"/>:</b> the
    /// sounder is a SECOND card that can be on screen beside the piloting control, so it needs its own
    /// corner and its own scale. Keeping them separate also means the owner can move one without moving
    /// the other.</para>
    /// </summary>
    [System.Serializable]
    public struct DepthSounderSettings
    {
        [Tooltip("How often the transducer takes a sounding (real seconds). The tide moves in MINUTES, so " +
                 "reading per frame would be pure waste (rule 7). Smaller = more responsive when the boat " +
                 "is crossing a bar quickly.")]
        [Min(0.01f)] public float ReadIntervalSec;

        [Tooltip("Shallow set-point (m) a freshly fitted sounder starts at — the depth at or below which " +
                 "the glass flashes SHALLOW.")]
        [Min(0f)] public float DefaultAlarmMetres;

        [Tooltip("Is the shallow alarm armed out of the box? (A new sounder normally arrives armed.)")]
        public bool DefaultArmed;

        [Tooltip("Does a freshly fitted sounder read in FEET rather than metres? The player can toggle it " +
                 "on the glass either way; this is only where it starts.")]
        public bool DefaultFeet;

        [Tooltip("How far each press of the ALARM up/down pushers moves the set-point (m). The rig's own " +
                 "pushers are labelled ±0.5.")]
        [Min(0.01f)] public float AlarmStepMetres;

        [Tooltip("Shallowest set-point the pushers can reach (m).")]
        [Min(0f)] public float MinAlarmMetres;

        [Tooltip("Deepest set-point the pushers can reach (m).")]
        [Min(0f)] public float MaxAlarmMetres;

        [Tooltip("Flash rate of the SHALLOW alarm, full on/off cycles per second. The card repaints only " +
                 "on a flash FLIP and only while the alarm is sounding (rule 7).")]
        [Min(0f)] public float AlarmBlinkHz;

        [Tooltip("⚠ PLACEHOLDER. The water temperature (°C) the LCD prints. There is NO water-temperature " +
                 "model in the simulation yet and this slice does not build one (rule 8 — stay in phase); " +
                 "the rig has a temp line, so it shows this constant until a real one exists. Flagged in " +
                 "the PR that shipped it.")]
        public float PlaceholderWaterTempC;

        [Tooltip("⚠ FALLBACK ONLY since S4.5. Scale of the loose small card (screen px per rig px, " +
                 "1 = native). The sounder now draws FLUSH in the dash's own brow mount by default, " +
                 "sized by that authored cutout, so this is only reached when there is no dash on " +
                 "screen to mount into.")]
        [Min(0.1f)] public float CardScale;

        [Tooltip("Scale of the EXPANDED state (click the flush sounder to blow it up; Esc/click-away " +
                 "returns it to the dash). Bigger = the three pushers are easier to hit; the rig's hit " +
                 "geometry scales with it. This one is very much live — it is the state you read the " +
                 "instrument in.")]
        [Min(0.1f)] public float FocusScale;

        [Tooltip("⚠ FALLBACK ONLY since S4.5 (see CardScale). Margin (px) from the screen's RIGHT edge " +
                 "to the loose small card.")]
        [Min(0f)] public float MarginX;

        [Tooltip("⚠ FALLBACK ONLY since S4.5 (see CardScale). Margin (px) from the screen's TOP edge " +
                 "to the loose small card.")]
        [Min(0f)] public float MarginY;

        [Tooltip("Where the EXPANDED card centres on screen, as a 0..1 fraction of screen width.")]
        [Range(0f, 1f)] public float FocusCenterX01;

        [Tooltip("Where the EXPANDED card centres on screen, as a 0..1 fraction of screen height.")]
        [Range(0f, 1f)] public float FocusCenterY01;

        /// <summary>A 4 Hz sounding, armed at 3 m with ±0.5 m pushers (the rig's own defaults), a 2 Hz
        /// flash, metres, and a native-size card in the top-right with a 2× focused state.</summary>
        public static DepthSounderSettings Default => new DepthSounderSettings
        {
            ReadIntervalSec = 0.25f,
            DefaultAlarmMetres = 3f,
            DefaultArmed = true,
            DefaultFeet = false,
            AlarmStepMetres = 0.5f,
            MinAlarmMetres = 0.5f,
            MaxAlarmMetres = 20f,
            AlarmBlinkHz = 2f,
            PlaceholderWaterTempC = 12f,
            CardScale = 1f,
            FocusScale = 2f,
            MarginX = 24f,
            MarginY = 16f,
            FocusCenterX01 = 0.5f,
            FocusCenterY01 = 0.5f,
        };
    }

    /// <summary>
    /// The <b>fish finder's</b> owner tunables (<see cref="GameConfig.FishFinder"/> — ADR 0025 S3, the sonar
    /// that supersedes the plain depth sounder in the same cutout).
    ///
    /// <para><b>Why this block exists at Step 0 with one field in it.</b> The vertical RANGE is the finder's
    /// only genuinely NEW piece of persisted state, and it is the denominator of the whole picture — the
    /// bottom contour sits at <c>depth / range</c> (<c>fishRig.js:239</c>). That makes its default a
    /// safety-critical number rather than a taste one: at zero the contour is Inf/NaN, not small. Naming it
    /// here, once, is what lets the save migration heal an old row and the UI draw a fresh one from the SAME
    /// value (rule 6 — the number is data, never a literal in two places). The rest of the finder's tuning
    /// — card placement, scan cadence, mark sizing and the placeholder strip — joined it in the UI slice
    /// (S3b) and is documented field by field below.</para>
    ///
    /// <para><b><see cref="WaterfallHz"/> is the perf knob, not a taste one.</b> The rig's scan phase
    /// free-runs, and its <c>sonarView</c> is O(width) in <c>fillRect</c> spans plus a whole-texture
    /// upload; repainting it per frame is a rule-7 break on its own. The host quantizes the phase to this
    /// rate and folds the bucket into its change key, so the instrument's cost is data the owner can dial
    /// rather than a frame counter.</para>
    ///
    /// <para><b>Four fields are ⚠ PLACEHOLDERS</b> (<see cref="PlaceholderSens01"/>,
    /// <see cref="PlaceholderLink"/>, <see cref="PlaceholderBatt01"/>, <see cref="PlaceholderVolts"/>) —
    /// the rig's status strip shows a sensitivity, a transducer link, a battery and a supply voltage, and
    /// the simulation models none of those. Following the shipped precedent
    /// (<see cref="DepthSounderSettings.PlaceholderWaterTempC"/>) they are constants with a tooltip that
    /// says what they are and why, rather than an invented electrical model (rule 8).</para>
    ///
    /// <para>The alarm, its set-point, units and night backlight are deliberately NOT here: the finder
    /// keeps the depth sounder's shallow alarm unchanged, reading the same
    /// <see cref="DepthSounderSettings"/> and the same <see cref="SounderPrefs"/> (one alarm rule, not
    /// two).</para>
    /// </summary>
    [System.Serializable]
    public struct FishFinderSettings
    {
        [Tooltip("Vertical sonar scale (metres) a freshly fitted fish finder starts at — how deep the " +
                 "bottom of the glass reaches. The player steps it with the RANGE pushers; the rig's own " +
                 "scale choices are 10 / 20 / 40 / 60 m. ⚠ Must stay ABOVE ZERO: the bottom contour is " +
                 "drawn at depth ÷ range, so a zero range is a divide-by-zero (Inf/NaN — a garbage " +
                 "contour), not merely a small picture. The save migration heals any stored range that " +
                 "is not positive back to this value.")]
        [Min(1f)] public float DefaultRangeMetres;

        [Tooltip("How many times a second the sonar redraws its scan — the finder's PING RATE, and the " +
                 "single number that decides what it costs (rule 7). The rig's scan phase free-runs, so a " +
                 "naive port would raster thousands of spans AND upload a whole texture EVERY FRAME; the " +
                 "host quantizes the phase to this rate instead and change-detects on the bucket. " +
                 "MEASURED: one repaint is ~4.8 ms (480x660, raster + upload), so each Hz here costs " +
                 "~4.8 ms per second of a 16.7 ms frame budget. Shipped at 4 = ~19 ms/s; 8 scrolls more " +
                 "smoothly for ~38 ms/s. ⚠ Keep it a MULTIPLE of 2x DepthSounder.AlarmBlinkHz, so a " +
                 "sounding alarm's flashes land on scan steps and cost no extra repaints.")]
        [Min(0.5f)] public float WaterfallHz;

        [Tooltip("Does a freshly fitted finder tag each fish mark with its depth? The player toggles it by " +
                 "tapping the sonar glass; this is only where it starts. Not persisted — unlike the alarm " +
                 "and the range, it is a look-at-it-now toggle, not a setting a fisherman expects to find " +
                 "tomorrow.")]
        public bool DefaultFishId;

        [Tooltip("⚠ FALLBACK ONLY since S4.5. Scale of the loose small card, as a fraction of the rig's " +
                 "native 480x660 (the surface is always native and the card is a scaled blit of it). " +
                 "The finder now draws FLUSH in the dash's authored brow mount by default, sized by " +
                 "that cutout, so this is only reached when there is no dash on screen to mount into.")]
        [Range(0.2f, 2f)] public float CardScale;

        [Tooltip("Scale of the EXPANDED state (click the flush finder to blow it up; Esc/click-away " +
                 "returns it to the dash), as a fraction of the rig's native 480x660. Bigger = the " +
                 "pushers are easier to hit; the rig's hit geometry scales with it. This is the state " +
                 "the instrument is actually READ in — below ~83% of native its typography breaks " +
                 "down, so keep it at or above 1.")]
        [Range(0.2f, 2f)] public float FocusScale;

        [Tooltip("⚠ FALLBACK ONLY since S4.5 (see CardScale). Margin (px) from the screen's RIGHT edge " +
                 "to the loose small card.")]
        [Min(0f)] public float MarginX;

        [Tooltip("⚠ FALLBACK ONLY since S4.5 (see CardScale). Margin (px) from the screen's TOP edge " +
                 "to the loose small card.")]
        [Min(0f)] public float MarginY;

        [Tooltip("Where the EXPANDED card centres on screen, as a 0..1 fraction of screen width.")]
        [Range(0f, 1f)] public float FocusCenterX01;

        [Tooltip("Where the EXPANDED card centres on screen, as a 0..1 fraction of screen height.")]
        [Range(0f, 1f)] public float FocusCenterY01;

        [Tooltip("Fish-mark size (the rig's own 'size' multiplier, ~0.5-2.5) for a school you are only " +
                 "clipping the EDGE of — signal strength 0. Small marks read as a weak return.")]
        [Range(0.2f, 3f)] public float MarkSizeMin;

        [Tooltip("Fish-mark size for a school you are sitting on the CENTRE of — signal strength 1.")]
        [Range(0.2f, 3f)] public float MarkSizeMax;

        [Tooltip("How much each individual mark's size wanders from the school's, 0..1. Pure look: a " +
                 "shoal of identical fish reads as a graphic, not a return. Deterministic (the rig's own " +
                 "hash noise), never random — a repaint must not reshuffle the picture.")]
        [Range(0f, 1f)] public float MarkSizeJitter01;

        [Tooltip("How far in from the left/right edges of the sonar box marks are scattered, as a 0..1 " +
                 "fraction of its width. Keeps a mark from being half-clipped by the ruler or the bezel.")]
        [Range(0f, 0.45f)] public float MarkXMargin01;

        [Tooltip("How far above/below the school's stated depth individual marks may sit (metres). A " +
                 "school is an AREA at a DEPTH, not a line of fish at one exact metre. Purely cosmetic: " +
                 "the depth that fishes is the school's own.")]
        [Min(0f)] public float MarkDepthJitterMetres;

        [Tooltip("Hard cap on how many fish icons ONE school draws, however dense it is. The glass is " +
                 "small and the raster is per-mark (rule 7) — beyond a handful you are drawing soup.")]
        [Range(1, 24)] public int MaxMarksPerSchool;

        [Tooltip("⚠ PLACEHOLDER. Signal-strength bars (0..1) on the status strip. There is NO transducer " +
                 "or sensitivity model in the simulation and this slice does not build one (rule 8 — stay " +
                 "in phase); the rig has the bars, so they show this constant. Flagged in the PR that " +
                 "shipped it.")]
        [Range(0f, 1f)] public float PlaceholderSens01;

        [Tooltip("⚠ PLACEHOLDER. Transducer-link indicator on the status strip. No transducer model " +
                 "exists; a fitted finder is simply linked. Flagged in the PR that shipped it.")]
        public bool PlaceholderLink;

        [Tooltip("⚠ PLACEHOLDER. Battery fill (0..1) on the status strip. There is NO electrical model on " +
                 "any boat and this slice does not build one (rule 8). Below 0.2 the rig draws the cell " +
                 "red, so leave it above that unless you want a permanent low-battery warning. Flagged in " +
                 "the PR that shipped it.")]
        [Range(0f, 1f)] public float PlaceholderBatt01;

        [Tooltip("⚠ PLACEHOLDER. Supply volts printed on the status strip. No electrical model exists. " +
                 "Flagged in the PR that shipped it.")]
        [Min(0f)] public float PlaceholderVolts;

        /// <summary>The rig's own defaults where it has them — 20 m scale (the second of its four RANGE
        /// steps, <c>fishRig.js:131</c>/<c>:317</c>), fish-ID on (<c>:319</c>), and the four status-strip
        /// placeholders exactly as <c>fishRig.js:320-321</c> states them (sens 0.75, link on, batt 0.8,
        /// 4.0 V) — so the shipped glass looks like the art director's preview until real models exist.
        /// A 4 Hz scan (measured at ~4.8 ms a repaint, so ~19 ms of every second — and a multiple of
        /// 2× the 2 Hz alarm blink, so the flash costs nothing extra), a half-size card in the top-right,
        /// and a native-size focused state.</summary>
        public static FishFinderSettings Default => new FishFinderSettings
        {
            DefaultRangeMetres = 20f,
            WaterfallHz = 4f,
            DefaultFishId = true,
            CardScale = 0.5f,
            FocusScale = 1f,
            MarginX = 24f,
            MarginY = 16f,
            FocusCenterX01 = 0.5f,
            FocusCenterY01 = 0.5f,
            MarkSizeMin = 0.7f,
            MarkSizeMax = 1.6f,
            MarkSizeJitter01 = 0.25f,
            MarkXMargin01 = 0.12f,
            MarkDepthJitterMetres = 0.6f,
            MaxMarksPerSchool = 8,
            PlaceholderSens01 = 0.75f,
            PlaceholderLink = true,
            PlaceholderBatt01 = 0.8f,
            PlaceholderVolts = 4f,
        };
    }

    /// <summary>
    /// The chartplotter's tunables (<see cref="GameConfig.Chartplotter"/> — ADR 0025 S6): the caps that
    /// bound nav data in the save, and the RANGE ladder the chart zooms through.
    ///
    /// <para><b>⚠ The range ladder is NOT the rig's.</b> <c>navRig.js:540</c> ships
    /// <c>RANGE_STEPS=[1,2,4,6,10,16]</c> nautical miles, drawn against a fictional 22 × 17 NM chart.
    /// The real world is a few hundred metres across — St Peters is
    /// <c>WorldSizeMeters {760, 520}</c> = <b>0.41 × 0.28 NM</b> — so the rig's SMALLEST range already
    /// shows the entire region at under half the chart's width, and every step above it is mostly
    /// empty sea. The unit (NM) is kept because that is what the rig draws and what a plotter reads
    /// in; only the ladder is re-scaled, to even doublings from a harbour range. Nothing about the
    /// chart's depths or distances changes — this is the zoom control, not the survey.</para>
    /// </summary>
    [System.Serializable]
    public struct ChartplotterSettings
    {
        [Tooltip("Most waypoints the player may keep across the whole save (all regions together). The " +
                 "chart refuses to mark another once this is reached rather than silently dropping the " +
                 "oldest — a waypoint is a deliberate act and losing one quietly would be worse.")]
        [Min(1)] public int MaxWaypoints;

        [Tooltip("Most legs one planned route may have. A route is a working plan, not an archive.")]
        [Min(1)] public int MaxRouteLegs;

        [Tooltip("Most track breadcrumbs kept. This one IS a ring buffer — the track is a record of " +
                 "where you have been and the OLDEST crumb is the right thing to lose when it fills.")]
        [Min(2)] public int MaxTrackPoints;

        [Tooltip("How far (metres) the boat must move before the track takes another crumb. Together " +
                 "with MaxTrackPoints this decides how much water the breadcrumb remembers: 8 m × 512 " +
                 "≈ 4 km of sailing, several times across a region. Too small and the track is a dense " +
                 "smear that fills with one harbour manoeuvre.")]
        [Min(0.5f)] public float TrackMinSpacingMetres;

        [Tooltip("The CLOSEST chart range, in nautical miles — the first rung of the ladder. 0.05 NM " +
                 "≈ 93 m across the glass, about a wharf and its approach.")]
        [Min(0.001f)] public float MinRangeNM;

        [Tooltip("How many rungs the range ladder has. Each is DOUBLE the one below, so the default " +
                 "6 rungs from 0.05 NM reach 1.6 NM (≈ 93 m … 3 km) — from a berth to well outside " +
                 "any region the game has.")]
        [Min(1)] public int RangeStepCount;

        [Tooltip("Which rung a freshly fitted plotter starts on (0 = closest). 2 = 0.2 NM ≈ 370 m, " +
                 "which frames a whole small region.")]
        [Min(0)] public int DefaultRangeStep;

        /// <summary>
        /// The range in NM at a rung, healed against a config block that never got written.
        ///
        /// <para><b>Why this heals rather than trusting the field.</b> A struct member absent from the
        /// wired <c>GameConfig.asset</c> block deserializes to ZERO, not to <see cref="Default"/> — the
        /// trap that shipped three features inert on 2026-08-05. A zero <see cref="MinRangeNM"/> would
        /// make the chart's world-to-screen scale divide by zero and draw nothing at all, so it is
        /// caught here, at the one place the ladder is read.</para>
        /// </summary>
        public float RangeNMAt(int step)
        {
            float min = MinRangeNM > 0f ? MinRangeNM : Default.MinRangeNM;
            int count = RangeStepCount > 0 ? RangeStepCount : Default.RangeStepCount;
            if (step < 0) step = 0;
            else if (step > count - 1) step = count - 1;
            return min * (1 << step);
        }

        /// <summary>Number of rungs, healed the same way <see cref="RangeNMAt"/> heals.</summary>
        public int SafeRangeStepCount => RangeStepCount > 0 ? RangeStepCount : Default.RangeStepCount;

        /// <summary>The rung a fresh unit starts on, clamped into the healed ladder.</summary>
        public int SafeDefaultRangeStep
        {
            get
            {
                int c = SafeRangeStepCount;
                int s = DefaultRangeStep;
                if (s < 0) s = 0;
                else if (s > c - 1) s = c - 1;
                return s;
            }
        }

        /// <summary>Shipped defaults: bounded nav data, and a range ladder sized for a harbour rather
        /// than for the rig's fictional ocean chart.</summary>
        public static ChartplotterSettings Default => new ChartplotterSettings
        {
            MaxWaypoints = 64,
            MaxRouteLegs = 24,
            MaxTrackPoints = 512,
            TrackMinSpacingMetres = 8f,
            MinRangeNM = 0.05f,
            RangeStepCount = 6,
            DefaultRangeStep = 2,
        };
    }

    /// <summary>
    /// The radar's tunables (<see cref="GameConfig.Radar"/> — ADR 0025 S5): the RANGE ladder the scope
    /// zooms through, how hard the set is turned up, how much clutter the sea throws back, and the
    /// budget the coastline scan runs to.
    ///
    /// <para><b>⚠ The range ladder is NOT the rig's</b> — the same correction
    /// <see cref="ChartplotterSettings"/> makes, for the same reason. <c>radarRig.js:129</c> ships
    /// <c>RANGE_STEPS=[0.5,1,2,3,6,12,24]</c> nautical miles, drawn against a fictional ocean. St Peters
    /// is <c>WorldSizeMeters {760, 520}</c> = <b>0.41 × 0.28 NM</b>, so the rig's SMALLEST range already
    /// puts the whole region inside a quarter of the scope and every rung above it is empty water with
    /// the coast pinned at the centre. The unit (NM) is kept, because that is what the rig draws and
    /// what a radar reads in; only the ladder is re-scaled, to even doublings from a harbour range.
    /// Deliberately the SAME ladder shape as the plotter's, with its own fields: the two instruments
    /// sit side by side on one brow and a skipper comparing them should not have to translate.</para>
    ///
    /// <para><b>Gain is here and clutter is not.</b> The rig draws GAIN, SEA and RAIN dials but authors
    /// no pusher for any of them, so none can be a player preference (rule 6 — a knob that never moves).
    /// Gain is factory tuning and lives here. SEA clutter is read live from the sea state instead, which
    /// is the honest source and is what makes the instrument answer to the weather (P1) — this block
    /// only says how much clutter a FULL gale is worth. RAIN has no source at all in this slice and is
    /// drawn at zero: precipitation returns are the Smother's payoff (canon M4), and mapping the
    /// existing <c>Visibility</c> (fog) onto the rain dial would invert the instrument's whole meaning,
    /// since seeing THROUGH fog is exactly what a radar is for.</para>
    /// </summary>
    [System.Serializable]
    public struct RadarSettings
    {
        [Tooltip("The CLOSEST radar range, in nautical miles — the first rung of the ladder. 0.05 NM " +
                 "≈ 93 m to the scope's rim, about a wharf and its approach.")]
        [Min(0.001f)] public float MinRangeNM;

        [Tooltip("How many rungs the range ladder has. Each is DOUBLE the one below, so the default " +
                 "6 rungs from 0.05 NM reach 1.6 NM (≈ 93 m … 3 km) — from a berth to well outside " +
                 "any region the game has.")]
        [Min(1)] public int RangeStepCount;

        [Tooltip("Which rung a freshly fitted set starts on (0 = closest). 2 = 0.2 NM ≈ 370 m to the " +
                 "rim, which puts a whole small region on the scope.")]
        [Min(0)] public int DefaultRangeStep;

        [Tooltip("How many range RINGS the scope draws between the centre and the rim (radarRig.js " +
                 "`rings`). Four is the marine convention and makes each ring a quarter of the range.")]
        [Min(1)] public int Rings;

        [Tooltip("How hard the set is turned up, 0..1 (the rig's GAIN dial). Scales every echo's " +
                 "brightness. Factory tuning: no pusher exposes it, so it is not a player preference.")]
        [Range(0f, 1f)] public float Gain;

        [Tooltip("How much sea clutter a FULL gale throws back, 0..1 (the rig's SEA dial). Scaled by " +
                 "the live sea state, so flat calm is clean and a blow speckles the middle of the " +
                 "scope — the instrument answering to the weather rather than to a constant.")]
        [Range(0f, 1f)] public float SeaClutterAtFullSeaState;

        [Tooltip("How fast the aerial turns, in revolutions per minute. A real small-craft set runs 24 " +
                 "to 48; 24 is one sweep every two and a half seconds, which reads as deliberate " +
                 "rather than frantic on a scope this size.")]
        [Min(1f)] public float SweepRpm;

        [Tooltip("How many times a SECOND the scope may repaint while the set is transmitting. This is " +
                 "the rule-7 budget: a turning sweep changes the picture every frame, so the glass is " +
                 "capped here rather than repainting per frame. In STANDBY nothing turns and the scope " +
                 "falls back to pure change-detection, costing nothing at all. ONE repaint of this " +
                 "480x660 canvas measures 4.79 ms (ADR 0025), so 8 Hz is about 38 ms/s — roughly 3.8% " +
                 "of a 60 fps budget. Raising this is a real cost: 12 Hz is 5.7%.")]
        [Min(1f)] public float SweepRepaintHz;

        [Tooltip("How many AZIMUTHS the coastline scan takes per sweep. The land echo is a polar scan " +
                 "outward from the boat, so this times MaxLandEchoes bounds the whole cost. 72 = every " +
                 "5°, which is finer than the rig's own 5° bearing ticks.")]
        [Min(8)] public int LandScanAzimuths;

        [Tooltip("Most land echoes the scan may publish in one bake. A hard ceiling so a boat sitting " +
                 "in a cove full of rock cannot cost more than a boat in open water (rule 7).")]
        [Min(1)] public int MaxLandEchoes;

        [Tooltip("Metres of tide movement that force the coastline scan to re-run. A radar sees the " +
                 "waterline of the MOMENT, so the coast really does move with the tide — but it moves " +
                 "in minutes, and re-scanning for a centimetre would be a rule-7 disaster.")]
        [Min(0.001f)] public float LandRescanTideMetres;

        /// <summary>
        /// The range in NM at a rung, healed against a config block that never got written.
        ///
        /// <para><b>Why this heals rather than trusting the field.</b> A struct member absent from the
        /// wired <c>GameConfig.asset</c> block deserializes to ZERO, not to <see cref="Default"/> — the
        /// trap that shipped three features inert on 2026-08-05. A zero <see cref="MinRangeNM"/> would
        /// make the scope's world-to-glass scale divide by zero and draw every contact on top of the
        /// boat, so it is caught here, at the one place the ladder is read.</para>
        /// </summary>
        public float RangeNMAt(int step)
        {
            float min = MinRangeNM > 0f ? MinRangeNM : Default.MinRangeNM;
            int count = RangeStepCount > 0 ? RangeStepCount : Default.RangeStepCount;
            if (step < 0) step = 0;
            else if (step > count - 1) step = count - 1;
            return min * (1 << step);
        }

        /// <summary>Number of rungs, healed the same way <see cref="RangeNMAt"/> heals.</summary>
        public int SafeRangeStepCount => RangeStepCount > 0 ? RangeStepCount : Default.RangeStepCount;

        /// <summary>The rung a fresh unit starts on, clamped into the healed ladder.</summary>
        public int SafeDefaultRangeStep
        {
            get
            {
                int c = SafeRangeStepCount;
                int s = DefaultRangeStep;
                if (s < 0) s = 0;
                else if (s > c - 1) s = c - 1;
                return s;
            }
        }

        /// <summary>Range rings, healed — a zero would draw none and make the scope unreadable.</summary>
        public int SafeRings => Rings > 0 ? Rings : Default.Rings;

        /// <summary>Aerial speed, healed — a zero would leave the sweep parked and make a transmitting
        /// set look exactly like a broken one.</summary>
        public float SafeSweepRpm => SweepRpm > 0f ? SweepRpm : Default.SweepRpm;

        /// <summary>Repaint ceiling, healed — a zero would freeze the picture entirely.</summary>
        public float SafeSweepRepaintHz => SweepRepaintHz > 0f ? SweepRepaintHz : Default.SweepRepaintHz;

        /// <summary>Degrees the aerial turns in a second, from <see cref="SafeSweepRpm"/>.</summary>
        public float SweepDegreesPerSecond => SafeSweepRpm * 360f / 60f;

        /// <summary>Azimuths per land scan, healed — a zero would silently drop the coast entirely,
        /// which is the failure mode hardest to tell from "there is no land here".</summary>
        public int SafeLandScanAzimuths => LandScanAzimuths >= 8 ? LandScanAzimuths : Default.LandScanAzimuths;

        /// <summary>Land-echo ceiling, healed — a zero would draw no coast at all.</summary>
        public int SafeMaxLandEchoes => MaxLandEchoes > 0 ? MaxLandEchoes : Default.MaxLandEchoes;

        /// <summary>Tide step that re-runs the coastline scan, healed — a zero would re-scan every
        /// single frame the tide moved a float's worth, which is every frame.</summary>
        public float SafeLandRescanTideMetres
            => LandRescanTideMetres > 0f ? LandRescanTideMetres : Default.LandRescanTideMetres;

        /// <summary>Shipped defaults: a range ladder sized for a harbour rather than for the rig's
        /// fictional ocean, a set turned up about three-quarters, and a coastline scan whose worst case
        /// is a few hundred terrain samples on a slow tick.</summary>
        public static RadarSettings Default => new RadarSettings
        {
            MinRangeNM = 0.05f,
            RangeStepCount = 6,
            DefaultRangeStep = 2,
            Rings = 4,
            Gain = 0.72f,                       // radarRig.js:396 — the rig's own authored default
            SeaClutterAtFullSeaState = 0.45f,
            SweepRpm = 24f,
            SweepRepaintHz = 8f,
            LandScanAzimuths = 72,              // every 5°
            MaxLandEchoes = 96,
            LandRescanTideMetres = 0.05f,
        };
    }

    /// <summary>
    /// The owner-tunable feel of the <b>flick-cast</b> (<see cref="GameConfig.FlickCast"/> — Rod Fishing v2,
    /// design/rod-fishing-v2-brainstorm.md §2.2), named and serializable so the whole gesture is dialled in
    /// the Inspector with no code (rule 6). Lives in Core beside the config it rides on, the same
    /// Core-policy / feature-consumer split as <see cref="RodFightSettings"/>: the pure maths that consumes
    /// it (<c>FlickCastMath</c>, Fishing-side) is fed this struct plus the cast cap.
    ///
    /// <para><b>Two dials decide the distance</b> (owner's ruling 2026-07-23): how far you WIND BACK sets
    /// the range you're aiming at (<see cref="FullRangeWindBackMetres"/>), and how hard you SNAP the
    /// forward sweep decides how much of it you deliver (<see cref="FullSnapFlickSpeed"/> /
    /// <see cref="LimpFlickFraction01"/>). Where you release carries no penalty — the earlier model scored
    /// release position in world metres and, on a ~16 m-wide screen, quietly collapsed every cast onto the
    /// <see cref="MinCastMetres"/> floor.</para>
    ///
    /// <para><b>The per-gear seam.</b> <see cref="MaxCastDistanceMetres"/> is the CAP a full wind-back,
    /// fully snapped, reaches. It is a GameConfig field for now; later, better rods/tackle extend it
    /// (P4), so the maths takes the cap as its own explicit parameter — a GearDef's own cap slots in
    /// without touching this struct.</para>
    /// </summary>
    [System.Serializable]
    public struct FlickCastSettings
    {
        [Tooltip("How far BEHIND the character (m, along the flick) the mouse must have wound back for the " +
                 "gesture to count as a cast at all. Below this the rod was never loaded — nothing flies, " +
                 "you just stand back up (no penalty). Smaller = more forgiving wind-up.")]
        [Min(0f)] public float MinWindBackMetres;

        [Tooltip("Shortest forward sweep (m, wind-back point to release) that still casts. Anything shorter " +
                 "is a twitch, not a flick — nothing flies. Keep small; this only rejects accidents.")]
        [Min(0f)] public float MinFlickLengthMetres;

        [Tooltip("THE RANGE DIAL. How far behind you the mouse must be drawn (m) to aim the FULL cast " +
                 "distance. Draw back half of this and you're aiming half as far. This is exactly what the " +
                 "aim preview shows you while you wind back. Smaller = long casts come from small " +
                 "wind-ups; larger = you must really draw back to reach out.")]
        [Min(0f)] public float FullRangeWindBackMetres;

        [Tooltip("THE SNAP DIAL. Sweep SPEED (m/s at the fastest part of the forward flick) that delivers " +
                 "the whole range you aimed at. Smaller = even a gentle sweep gets there; larger = only a " +
                 "real snap of the wrist does.")]
        [Min(0f)] public float FullSnapFlickSpeed;

        [Tooltip("Fraction of the aimed range (0..1) that a completely LIMP sweep still delivers. This is " +
                 "the dribbled cast that lands well short of where you were aiming — keep it above 0 so a " +
                 "weak flick still puts the line in the water (cozy fail). Lower = the wrist-snap matters " +
                 "more.")]
        [Range(0f, 1f)] public float LimpFlickFraction01;

        [Tooltip("Shortest distance (m) any successful cast lands from the character. The floor under a " +
                 "weak flick, so the bobber is always at least in the water, not on your boots.")]
        [Min(0f)] public float MinCastMetres;

        [Tooltip("The CAP (m): the farthest a full wind-back, fully snapped, can reach with the starter " +
                 "rod. Better rods/tackle extend this later (per-gear data — the P4 upgrade you feel).")]
        [Min(0f)] public float MaxCastDistanceMetres;

        [Tooltip("How fast the cast line flies out (m/s) once released — pacing for the line-in-flight " +
                 "beat between the flick and the splash-down. Feel only; distance is decided at release.")]
        [Min(0.01f)] public float LineFlightMetresPerSec;

        /// <summary>The forgiving-cove reference tuning: a ~0.6 m wind-back to cast at all, the full 12 m
        /// range from a 4 m draw-back (about a quarter of the on-foot screen — deliberate but easy), the
        /// whole of it delivered by a brisk 12 m/s sweep, a limp sweep still throwing a quarter of what it
        /// aimed at, and nothing shorter than 1.5 m.</summary>
        public static FlickCastSettings Default => new FlickCastSettings
        {
            MinWindBackMetres = 0.6f,
            MinFlickLengthMetres = 0.4f,
            FullRangeWindBackMetres = 4f,
            FullSnapFlickSpeed = 12f,
            LimpFlickFraction01 = 0.25f,
            MinCastMetres = 1.5f,
            MaxCastDistanceMetres = 12f,
            LineFlightMetresPerSec = 18f,
        };
    }

    /// <summary>
    /// The owner-tunable feel of a <b>mooring line</b> (<see cref="GameConfig.MooringLine"/> — M2-38,
    /// design/deck-boarding-cleats-and-interact-capture.md §3). Lives in Core beside the config it rides
    /// on, the same Core-policy / feature-consumer split as <see cref="FlickCastSettings"/>: the pure
    /// maths that consumes it (<see cref="MooringLineMath"/>) is fed these numbers, and the Boats/Player
    /// consumers read them off the shared config each time they work a line.
    ///
    /// <para><b>The dial that matters is the scope range against a region's real DROP</b> — which is the
    /// tidal range PLUS how high the wharf stands, not the tidal range alone. St Peters is the worked
    /// example and it is a taller pier than it looks: its deck is measured at <b>+5.35 m</b> above datum
    /// and the tide swings <b>±2.2 m</b> (the 2026-08-01 pacing ruling), so the gap from a bollard down to
    /// a small hull's cleat runs from ~2.6 m at high water to <b>~7.0 m at low</b>. A line has to cover
    /// that vertically before it reaches across the water at all. Hence the defaults below:
    /// <list type="bullet">
    ///   <item><b>9 m</b> to start — she rides the whole ebb, swinging ~8.6 m at high water and ~5.7 m at
    ///   low. The boat is visibly drawn in as the water goes, which is the tell, but she is never hung.</item>
    ///   <item><b>Snug her to ~4 m</b> at high water and it looks perfect — and the ebb collects on it.
    ///   That is the lesson, and it is the player's own choice that sets it up.</item>
    ///   <item><b>16 m</b> at the top so a big hull on a spring tide still has an answer.</item>
    /// </list>
    /// Re-tune these when a region's wharf height or tide amplitude changes, or the gradient flattens and
    /// "mind the tide" becomes scenery. <c>MooringLineMathTests</c> pins that gradient against St Peters'
    /// actual numbers.</para>
    ///
    /// <para><b>Not here on purpose:</b> rope damage, breaking strain and multi-line rafting. V1's failure
    /// is the loop SLIPPING (<see cref="WorkingLoadFactor"/>) and the boat going quietly adrift — the
    /// cozy fail the backlog names. A parting rope is a different, harsher feature and a separate call.</para>
    /// </summary>
    [System.Serializable]
    public struct MooringLineSettings
    {
        [Tooltip("How close (m) you must stand to a cleat to work it — start a toss from it, or tighten, " +
                 "slacken and cast off a line already made fast to it. Roughly arm's reach: you are " +
                 "handling the fitting, not gesturing at it from across the deck.")]
        [Min(0f)] public float CleatReachMetres;

        [Tooltip("How near the far cleat the toss must LAND (m) for the loop to catch. This is the whole " +
                 "skill of the throw — the flick-cast decides where the line lands, and this decides " +
                 "whether that was good enough. Larger = kinder. Miss and the line simply falls in the " +
                 "water: coil it and try again, no penalty (cozy fail).")]
        [Min(0f)] public float TossCatchRadiusMetres;

        [Tooltip("SCOPE the line starts at (m) when it first catches — a sensible working length before " +
                 "the player has tightened or slackened anything.")]
        [Min(0f)] public float DefaultScopeMetres;

        [Tooltip("Shortest scope (m) you can haul a line in to. Above zero: a line hauled to nothing " +
                 "would pin the boat rigidly against the wharf, which is neither seamanlike nor a thing " +
                 "the constraint should have to express.")]
        [Min(0f)] public float MinScopeMetres;

        [Tooltip("Longest scope (m) you can pay out. THE tide dial — see the struct doc: this must be " +
                 "comfortably larger than the region's tidal range or a short line is never a mistake, " +
                 "and never so large that scope stops being a decision.")]
        [Min(0f)] public float MaxScopeMetres;

        [Tooltip("How much line (m) one press of tighten/slacken pays out or hauls in. Stepped rather " +
                 "than continuous so the player can COUNT the scope they are giving the tide, and so a " +
                 "keypress is a decision rather than a drag.")]
        [Min(0.01f)] public float ScopeStepMetres;

        [Tooltip("How far past bar-taut (×) the loop will be worked before it SLIPS off the cleat and the " +
                 "boat goes adrift. 1.0 = it surrenders the instant the line comes taut; 1.25 = it will " +
                 "take a quarter again its length of strain first. Keep above 1 — teeth should be earned " +
                 "by misjudging the tide, not by touching the water.")]
        [Min(1f)] public float WorkingLoadFactor;

        [Tooltip("How long (real seconds) the line must stay over its working load before the loop lets " +
                 "go. A grace period so a single wave that snatches the rope does not cast you off — it " +
                 "is a SUSTAINED overload (a tide that has run away from your scope) that loses the boat.")]
        [Min(0f)] public float SlipGraceSeconds;

        /// <summary>The St Peters reference tuning: arm's reach to a fitting, a forgiving 1.5 m catch on
        /// the throw, 9 m of scope to start, stepped by the metre between 2 m and 16 m, and a loop that
        /// takes a quarter again its length of strain for a couple of seconds before it surrenders. Sized
        /// against that pier's REAL drop (~2.6 m at high water, ~7.0 m at low — see the struct doc), so
        /// the starting line rides an ordinary ebb out and a deliberately snugged one does not.</summary>
        public static MooringLineSettings Default => new MooringLineSettings
        {
            CleatReachMetres = 1.5f,
            TossCatchRadiusMetres = 1.5f,
            DefaultScopeMetres = 9f,
            MinScopeMetres = 2f,
            MaxScopeMetres = 16f,
            ScopeStepMetres = 1f,
            WorkingLoadFactor = 1.25f,
            SlipGraceSeconds = 2f,
        };
    }

    /// <summary>
    /// The owner-tunable knobs of <b>ladder boarding</b> (<see cref="GameConfig.LadderBoarding"/>) — the
    /// tide-gap climb, and the geometry the rig's <c>ladderDown</c> clip was authored against.
    ///
    /// <para><b>Two of these are FEEL and the rest are MEASUREMENTS.</b>
    /// <see cref="BoardClampMetres"/> and <see cref="LadderReachMetres"/> are the owner's to tune. The
    /// four below them are the rig's and the wharf kit's own published numbers, exposed only so a future
    /// kit revision is a data edit rather than a code change — <b>changing them without a re-bake puts
    /// the fisher's feet between the rungs</b>, which is precisely what
    /// <c>LadderBoardingMathTests.TheStair_ReproducesTheBakedDescendTable</c> exists to catch.</para>
    /// </summary>
    [System.Serializable]
    public struct LadderBoardingSettings
    {
        [Tooltip("THE THRESHOLD. How far (m) the boat's deck may lie BELOW the wharf top and still be " +
                 "boarded with a step. Past this, boarding goes down a ladder instead.\n\n" +
                 "⚠ The art kit states two different numbers for this and they measure different things. " +
                 "1.2 m is what characterIsoRig6.js cites as where its 'board' clip soft-clamps, measured " +
                 "on the deck-to-WATER drop, and it is what ships here. wharfIsoRig.js:1103 implements a " +
                 "stricter 0.55 m on the deck-to-GUNWALE gap — which is the quantity this field actually " +
                 "compares, and which is also the rail height the one shipped 'board' sheet was baked at. " +
                 "So 1.2 is the generous reading: it keeps a step aboard available for the upper half of " +
                 "an ordinary tide (the sea has moods, and boarding should FEEL them), at the cost of " +
                 "letting the step clip stretch past the sheer it was drawn for. Dial it to 0.55 for the " +
                 "wharf kit's own stricter rule — one number, no code.")]
        [Min(0f)] public float BoardClampMetres;

        [Tooltip("How near (m) a ladder must be to where you are boarding for it to serve that berth. A " +
                 "wharf with no ladder within reach simply has no climb to offer: boarding falls back to " +
                 "the step, however deep the gap. Roughly the width of a berth, so the ladder mid-wall " +
                 "serves the boats lying either side of it and not the whole quay.")]
        [Min(0f)] public float LadderReachMetres;

        [Tooltip("MEASURED — the wharf kit's rung spacing (m), WharfIso.FIT.ladder.rung. The tread of " +
                 "the descent stair, and what the clip's foot placement was authored against.")]
        [Min(0.01f)] public float RungMetres;

        [Tooltip("MEASURED — the rig's standoff (m), ladderMount().standoff: how far the climber's pivot " +
                 "sits off the ladder plane. Seat the sprite on the ladder line instead and its hands go " +
                 "inside the wall. Not a nudge to taste.")]
        [Min(0f)] public float StandoffMetres;

        [Tooltip("MEASURED — one whole loop of the ladderDown clip (real seconds; 10 frames × 110 ms). " +
                 "The climb is NOT rate-scaled to a duration the way the boarding vault is: real rungs " +
                 "are a real distance apart, so a deeper gap takes longer rather than the same time " +
                 "faster. That is what makes the tide legible on the way down.")]
        [Min(0.01f)] public float ClimbLoopSeconds;

        [Tooltip("How long (real seconds) the unauthored TURN-AROUND at the top of the ladder is given — " +
                 "the moment the fisher swings off the wharf edge onto the top rung. The kit has no clip " +
                 "for it and says it is the gap players will notice, so it is covered with the authored " +
                 "'boardDown' step rather than a cut. Also covers the step OFF at the bottom onto the " +
                 "gunwale, which is the same authored motion.")]
        [Min(0f)] public float TransitionSeconds;

        /// <summary>The Nine Mile Creek reference tuning: a step aboard stays available until the boat's
        /// deck is 1.2 m below the planks, one ladder serves the berths within 4 m of it, and the climb
        /// runs the rig's own measured geometry at its own baked rate. Sized against that wharf's REAL
        /// numbers — a +3.0 m deck against a 2.2 m tide amplitude — so a dory is stepped onto around high
        /// water and climbed down to for the bottom half of the ebb.</summary>
        public static LadderBoardingSettings Default => new LadderBoardingSettings
        {
            BoardClampMetres = 1.2f,
            LadderReachMetres = 4f,
            RungMetres = LadderBoardingMath.RigRungMetres,
            StandoffMetres = LadderBoardingMath.RigStandoffMetres,
            ClimbLoopSeconds = LadderBoardingMath.RigLoopSeconds,
            TransitionSeconds = 0.45f,
        };
    }

    /// <summary>
    /// The owner-tunable knobs of the DISPLACED water surface (<see cref="GameConfig.DisplacedWater"/> —
    /// ADR 0023 phase 2 step 3), named and serializable so the sea's drama is dialled on the config asset
    /// with no code (rule 6). Lives in Core beside the config it rides on — the same Core-policy /
    /// feature-consumer split as <see cref="SeakeepingSettings"/> and <see cref="RodFightSettings"/>:
    /// Core cannot reference the Art module (rule 4), so the tunables live here and the Art-side
    /// consumers (<c>WaterSurface</c> / <c>DisplacedWaterSurface</c>) read them each throttled tick.
    ///
    /// <para><b>Lockstep (the twin discipline).</b> <see cref="Default"/> must equal the water shader's
    /// property defaults AND the Art-side twin constants (<c>WhitecapSalienceMath.Default*</c>) —
    /// config, shader and twin can never disagree silently. <c>DisplacedWaterConfigTests</c> pins all
    /// three sides; change any one only with the others, in the same commit.</para>
    ///
    /// <para><b>What is deliberately NOT here.</b> The four remaining salience properties
    /// (<c>_CapSolidMargin</c> / <c>_CapDitherBand</c> / <c>_EnvelopeBands</c> /
    /// <c>_EnvelopeBandDitherWin</c>) are STYLE constants of the band/dither language — they stay
    /// material-level on <c>Water.mat</c>. The per-coast shore gradient stays on each scene's
    /// <c>DisplacedWaterSurface</c> (it is terrain data, not world policy).</para>
    /// </summary>
    [System.Serializable]
    public struct DisplacedWaterSettings
    {
        [Tooltip("The SHARED displacement exaggeration (ADR 0023 §(2)): how much taller the sea DRAWS " +
                 "than it simulates. 1 = sim-true (already readable); the sweet spot is 1.5–2, and " +
                 "×1.5 (the default) is also provably shear-free at the coast; ×3 BREAKS the ¾-iso " +
                 "framing — crests visually detach from their troughs (spike-measured), so stay well " +
                 "under it. This ONE value drives the surface's lift AND (phase 3) hull heave, buoys " +
                 "and wake — everything rises on the same sea, never retuned apart.")]
        [Min(0f)] public float WaveExaggeration;

        [Tooltip("Safety coefficient of the DERIVED shore-fade band (band = coefficient × wave " +
                 "envelope × exaggeration × shore steepness). 2 (the default) is the proven tear-safe " +
                 "value. RAISING it widens the calm shallow band hugging every shore (safe, just " +
                 "calmer coasts); LOWERING it below ~1.5 risks the coast visibly TEARING — water " +
                 "drawn over dry sand at a crest. 1.5 is exactly marginal, so stay at 2 or above.")]
        [Min(0f)] public float ShoreBandCoefficient;

        [Tooltip("Master strength of the envelope whitecap salience (0..1): how strongly SOLID foam " +
                 "cores are reserved for the rare near-envelope wave. 1 (the default) = the full " +
                 "retune — everyday chop wears thin milky streaks and only the big one wears a solid " +
                 "core. 0 = the legacy look exactly: every crest capped with equal salience (the big " +
                 "wave hides in the speckle again).")]
        [Range(0f, 1f)] public float CapSalienceStrength;

        [Tooltip("Crest height — as a fraction of the sea's wave envelope (0..1) — where whitecap " +
                 "solid cores BEGIN. 0.62 (the default, spike-tuned) reserves cores for near-envelope " +
                 "waves: LOWER it and more everyday waves earn a solid core; RAISE it and cores get " +
                 "rarer still. Envelope-relative, so a bigger SEA does not fake a bigger WAVE.")]
        [Range(0f, 1f)] public float CapEnvelopeThreshold;

        [Tooltip("Strength of the envelope VALUE BANDS (0..1): the posterized light/dark stepping " +
                 "that marks tall water by SHADE before its foam (only a near-envelope crest can " +
                 "reach the top band). 0.35 is the default production blend; 0 = no envelope shading " +
                 "(the pre-retune look).")]
        [Range(0f, 1f)] public float EnvelopeBandStrength;

        [Tooltip("Is the DISPLACED sea the one the player sees? ON (the default since 2026-08-05) " +
                 "makes the vertex-displaced surface the game's water and hides the flat sprite face; " +
                 "OFF restores the flat face exactly, which is what shipped through ADR 0023 phase 3. " +
                 "The dev O key still flips it live either way — this only decides which side a scene " +
                 "STARTS on, so the owner can compare without a rebuild. Edit mode is unaffected and " +
                 "always shows the flat face: the displaced path is a Play instrument, and the coast " +
                 "is designed against the flat water (ADR 0014).")]
        public bool DefaultOn;

        /// <summary>
        /// The ADR-cited defaults: ×1.5 exaggeration (the readability sweet spot, shear-free at the
        /// coast), the proven tear-safe band coefficient (<see cref="ShoreFadeMath.RecommendedBandCoefficient"/>),
        /// full envelope salience with the spike-tuned 0.62 threshold, and the production 0.35 band
        /// blend. Pinned equal to the shader property defaults and the Art twin constants by
        /// <c>DisplacedWaterConfigTests</c>.
        ///
        /// <para><b><see cref="DefaultOn"/> is true</b> as of 2026-08-05 — the displaced sea becomes
        /// the game's water. ⚠️ That default reaches a SCENE only if the config asset actually
        /// serializes the key: a struct field absent from the YAML deserializes to <c>false</c>, not
        /// to this property, so <c>GameConfig.asset</c> carries <c>DefaultOn: 1</c> explicitly and
        /// <c>DisplacedDefaultTests</c> asserts that it does. This is the standing "the asset lags the
        /// code" trap in its most dangerous shape — a silent revert that looks like nothing changed.</para>
        /// </summary>
        public static DisplacedWaterSettings Default => new DisplacedWaterSettings
        {
            WaveExaggeration = 1.5f,
            ShoreBandCoefficient = ShoreFadeMath.RecommendedBandCoefficient,
            CapSalienceStrength = 1f,
            CapEnvelopeThreshold = 0.62f,
            EnvelopeBandStrength = 0.35f,
            DefaultOn = true,
        };
    }

    /// <summary>
    /// Tunables for the <b>depth drop</b> — Rod Fishing v2's weighted-rig fall, the slack "bottom" tell,
    /// and the depth-targeted catch weighting (<c>docs/design/rod-fishing-v2-brainstorm.md</c> §2.1/§2.3/§6;
    /// <see cref="GameConfig.DepthDrop"/>). Lives in Core beside the config it rides on, exactly like
    /// <see cref="RodFightSettings"/>: Core cannot reference the Fishing module (rule 4), so the tunables
    /// live here as plain numbers and the pure Fishing-side maths that consumes them
    /// (<c>DepthDropMath</c>) takes them as parameters.
    ///
    /// <para><b>The read is diegetic (owner's call, decision #4):</b> there is no depth gauge. The player
    /// COUNTS THE FALL — a heavier rig sinks faster — and FEELS the floor when the line goes slack. Every
    /// field here shapes that read or the catch weighting behind it; none of them draws a number on
    /// screen.</para>
    /// </summary>
    [System.Serializable]
    public struct DepthDropSettings
    {
        // ---- the fall (the count-the-fall depth read) --------------------------------------------

        [Tooltip("Extra sink speed per kilogram of rig weight (m/s per kg). THE tactical knob: a heavy jig " +
                 "reaches the deep band quickly, a light rig sinks slowly and fishes the mid-column longer. " +
                 "Bigger = weight matters more.")]
        [Min(0f)] public float SinkSpeedPerKgMps;

        [Tooltip("Slowest a rig ever sinks (m/s) — even a bare hook goes down eventually. Keeps a featherweight " +
                 "rig from hanging forever.")]
        [Min(0f)] public float MinSinkSpeedMps;

        [Tooltip("Fastest a rig ever sinks (m/s) — the heaviest lead still falls like a lure, not a brick. " +
                 "Caps how much the count-the-fall read can be shortcut.")]
        [Min(0f)] public float MaxSinkSpeedMps;

        // ---- the reachable band ------------------------------------------------------------------

        [Tooltip("How much line the reel carries (m) — the deepest the rig can EVER go, even over deeper " +
                 "water. The floor of the reachable band is the shallower of this and the seabed. Gear " +
                 "upgrades can extend it later.")]
        [Min(0f)] public float MaxLineMeters;

        [Tooltip("The bottom-fishing SWEET SPOT: how far above the floor (m) still counts as 'just off the " +
                 "bottom'. Bottom out, then reel up within this window to target bottom fish. Sitting ON the " +
                 "floor (line slack) is outside the window — the lift is the skill beat.")]
        [Min(0f)] public float BottomSweetWindowMeters;

        [Tooltip("How fast holding the action reels the rig UP (m/s) while waiting — the 'reel up slightly' " +
                 "move that lifts a bottomed rig into the sweet window.")]
        [Min(0f)] public float ReelUpMps;

        [Tooltip("A handline rigged with at least this much weight (kg) fishes the DEPTH branch (drop and " +
                 "read the column) instead of the cast/bobber branch. Jigging and longline gear always fish " +
                 "the depth branch; nets/traps never do.")]
        [Min(0f)] public float WeightedHandlineMinKg;

        // ---- the fishing depth zones (metres — where each kind of fish lives) ---------------------

        [Tooltip("Held depths down to this (m) read as TIDEPOOL water — the shore scraps.")]
        [Min(0f)] public float TidepoolMaxMeters;

        [Tooltip("Held depths down to this (m) read as the SHALLOWS.")]
        [Min(0f)] public float ShallowsMaxMeters;

        [Tooltip("Held depths down to this (m) read as INSHORE water.")]
        [Min(0f)] public float InshoreMaxMeters;

        [Tooltip("Held depths down to this (m) read as MIDWATER — stop the drop mid-column to fish it.")]
        [Min(0f)] public float MidwaterMaxMeters;

        [Tooltip("Held depths down to this (m) read as DEEP water; anything deeper is ABYSSAL.")]
        [Min(0f)] public float DeepMaxMeters;

        // ---- the catch weighting (depth as the species-targeting tactic) --------------------------

        [Tooltip("Catch-weight multiplier for a species whose preferred depth zones INCLUDE the zone you're " +
                 "holding in (≥ 1). Bigger = choosing the right depth pays off more.")]
        [Min(1f)] public float InBandAffinity;

        [Tooltip("Catch-weight multiplier for a species you're holding OUTSIDE its preferred zones (0..1). " +
                 "Kept above zero on purpose: depth is a weight, never a wall — the wrong depth makes a fish " +
                 "unlikely, not impossible.")]
        [Range(0.01f, 1f)] public float OffBandAffinity;

        [Tooltip("EXTRA catch-weight multiplier for a BOTTOM species while the rig is held just off the floor " +
                 "(inside the sweet window). The payoff for bottoming out and lifting slightly (≥ 1).")]
        [Min(1f)] public float BottomWindowAffinity;

        /// <summary>
        /// The forgiving-cove reference tuning: a 0.2 kg rig sinks ~0.9 m/s (a countable ~11 s to 10 m), a
        /// 1 kg jig ~2.5 m/s (the heavy shortcut), 60 m of line, a 1 m off-floor sweet window, and a
        /// clear-but-gentle ×2 zone / ×2.5 bottom-window weighting over a ×0.5 off-zone damp.
        /// </summary>
        public static DepthDropSettings Default => new DepthDropSettings
        {
            SinkSpeedPerKgMps = 2.0f,
            MinSinkSpeedMps = 0.5f,
            MaxSinkSpeedMps = 3.5f,
            MaxLineMeters = 60f,
            BottomSweetWindowMeters = 1.0f,
            ReelUpMps = 1.5f,
            WeightedHandlineMinKg = 0.2f,
            TidepoolMaxMeters = 0.6f,
            ShallowsMaxMeters = 3f,
            InshoreMaxMeters = 10f,
            MidwaterMaxMeters = 30f,
            DeepMaxMeters = 90f,
            InBandAffinity = 2.0f,
            OffBandAffinity = 0.5f,
            BottomWindowAffinity = 2.5f,
        };
    }

    /// <summary>
    /// THE STRIKE (<see cref="GameConfig.Strike"/> — owner drop §10.2). His words were "pull back and
    /// press maybe?", explicitly tentative — so BOTH candidates ship, each behind its own toggle, and
    /// he picks (or keeps both) by feel with no code change (rule 6). The press is the plain action
    /// edge; the pull-back is a committed haul of the pointer AWAY from the bobber — the mirror of
    /// the flick-cast's forward sweep. Consumed by the Fishing-side <c>StrikeYank</c>/<c>
    /// StrikeGestureMath</c> (the Core-policy / feature-consumer split, the RodFightSettings shape).
    /// </summary>
    [System.Serializable]
    public struct StrikeSettings
    {
        [Tooltip("The PRESS candidate: the action press strikes (today's input, and the gamepad-safe " +
                 "one). Turn off to feel the pull-back alone.")]
        public bool PressStrikes;

        [Tooltip("The PULL-BACK candidate: hauling the pointer away from the bobber strikes — cast " +
                 "forward, strike back. Turn off to feel the press alone.")]
        public bool PullBackStrikes;

        [Min(0f)]
        [Tooltip("How fast (m/s, world) the pointer must move AWAY from the bobber for the motion to " +
                 "count as hauling. Below this it's repositioning, not striking. The on-foot view is " +
                 "~16 m wide — 6 m/s is a deliberate wrist-snap, not a drift.")]
        public float YankMinSpeedMps;

        [Min(0f)]
        [Tooltip("Backward travel (m, world) the haul must bank at qualifying speed before it fires. " +
                 "Bigger = a fuller arm movement; smaller = a flick of the wrist.")]
        public float YankMinMetres;

        [Range(0f, 1f)]
        [Tooltip("Below this fraction of the qualifying speed the haul STALLS and its banked travel " +
                 "is forfeit — a yank is one committed motion, not a slow wander that eventually " +
                 "adds up.")]
        public float YankStallFraction01;

        /// <summary>Both candidates on (the owner picks in play); a brisk 6 m/s haul, 0.8 m of it,
        /// breaking below a quarter of qualifying speed.</summary>
        public static StrikeSettings Default => new StrikeSettings
        {
            PressStrikes = true,
            PullBackStrikes = true,
            YankMinSpeedMps = 6f,
            YankMinMetres = 0.8f,
            YankStallFraction01 = 0.25f,
        };
    }

    /// <summary>
    /// FRESHNESS &amp; ROT (<see cref="GameConfig.Freshness"/> — M1 §7.3), the owner-tunable dials of
    /// the freshness clock, named and serializable so rot is tuned on the config asset with no code
    /// (rule 6). The same Core-policy / feature-consumer split as <see cref="RodFightSettings"/>: the
    /// pure maths that consumes them is <see cref="Freshness"/> (Core), fed a <see cref="SpoilPolicy"/>
    /// built from these fields via <see cref="Policy"/>.
    ///
    /// <para><b><see cref="Default"/> must mirror <see cref="SpoilPolicy.Default"/></b> — the shipped
    /// GameConfig.asset predates this block, so the C# defaults here ARE the live game until the owner
    /// re-saves the asset; keeping them equal to the Core policy default means the maths and the config
    /// can never disagree silently (test-pinned).</para>
    /// </summary>
    [System.Serializable]
    public struct FreshnessSettings
    {
        [Tooltip("Rate scale while a catch sits IN THE OPEN (a bucket on the wharf, a bare hold). " +
                 "1 = the species' own SpoilPerDay; this is the reference rate the cold modes beat.")]
        [Min(0f)] public float AmbientRateMultiplier;

        [Tooltip("Rate scale while KEPT ALIVE (shellfish in a wet bucket). 0 = fully arrested — the " +
                 "M1 default; raise it later to give live holding a slow attrition without touching code.")]
        [Min(0f)] public float LiveRateMultiplier;

        [Tooltip("Rate scale while FROZEN (Ginny's freezer). 0 = fully arrested; the freezer never " +
                 "runs out — its limit is WHERE it is (home), not how long it holds.")]
        [Min(0f)] public float FrozenRateMultiplier;

        [Tooltip("Rate scale while ON ICE and the ice still holds. 0 = ice stops rot dead while it " +
                 "lasts; raise it slightly to make ice a strong slow rather than a stop. What makes ice " +
                 "interesting either way is that it RUNS OUT — the melt lives on the container Def.")]
        [Min(0f)] public float IcedRateMultiplier;

        [Tooltip("The spoil (0..1) at which NO buyer will take a catch at any price — it is rubbish, " +
                 "still occupying hold space until dumped. 0.9 = refused while visibly off but before " +
                 "liquid; 1 = sellable right up to the instant it is gone.")]
        [Range(0f, 1f)] public float UnsellableSpoil;

        /// <summary>These dials as the Core <see cref="SpoilPolicy"/> the freshness maths consumes.</summary>
        public SpoilPolicy Policy => new SpoilPolicy(AmbientRateMultiplier, LiveRateMultiplier,
                                                     FrozenRateMultiplier, IcedRateMultiplier,
                                                     UnsellableSpoil);

        /// <summary>Mirrors <see cref="SpoilPolicy.Default"/> exactly (see the struct remarks).</summary>
        public static FreshnessSettings Default => new FreshnessSettings
        {
            AmbientRateMultiplier = 1f,
            LiveRateMultiplier    = 0f,
            FrozenRateMultiplier  = 0f,
            IcedRateMultiplier    = 0f,
            UnsellableSpoil       = 0.9f,
        };
    }

    /// <summary>
    /// One entry of the pot starter kit (<see cref="GameConfig.StarterPotKit"/>): a trap kind by stable
    /// TrapDef id, and how many the kit grants. Plain serializable data so the owner tunes the kit on
    /// the GameConfig asset — no code, no scene rebuild (rule 6).
    /// </summary>
    [System.Serializable]
    public struct PotStarterEntry
    {
        [Tooltip("Stable TrapDef id to grant (e.g. \"trap.lobster\"). Must name an authored TrapDef " +
                 "(content validation checks this).")]
        public string TrapDefId;

        [Min(0)]
        [Tooltip("How many of this pot the starter kit grants. 0 disables the entry.")]
        public int Count;

        public PotStarterEntry(string trapDefId, int count)
        {
            TrapDefId = trapDefId;
            Count = count;
        }
    }

    /// <summary>
    /// THE WHEEL IS THE PLAYER'S EYE (<see cref="GameConfig.PlayerZoom"/> — owner ruling 2026-08-19):
    /// <i>"Mouse wheel modifies player zoom — closer to look at interiors, out when outside."</i>
    ///
    /// <para><b>A range, not a free zoom.</b> Pixel art shimmers at a fractional camera scale, so the
    /// camera only ever stops on the integer pixel-perfect steps the follow-cam's ladder defines. These
    /// two heights name the ENDS of the walking player's stretch of that ladder; the wheel walks the
    /// whole-number stops between them and nothing in between exists. Type any number you like — it
    /// quantises to the nearest crisp stop, so the worst a hand-typed value can do is pick a
    /// neighbouring tier, never a blurry framing.</para>
    ///
    /// <para><b>Metres, deliberately, and never step numbers.</b> Ladder steps count UPWARD as the view
    /// gets CLOSER, which reads backwards to anybody tuning a camera. Every other camera dial in the
    /// project is world height in metres and so is this one: smaller number = closer in. The reference
    /// stops at the locked PPU 32 on a 1080p screen are 33.75 / 16.88 / 11.25 / 8.44 / 6.75 / 5.63 /
    /// 4.82 / 4.22 m, and the walking defaults below pick the 11.25 → 5.63 band: one stop WIDER than
    /// today's on-foot framing at the far end, and at the near end the closest framing the game already
    /// ships (the step a live trap haul uses).</para>
    ///
    /// <para><b>These two clamps are the WALKER's range, and only hers.</b> Aboard and on deck the wheel
    /// works too (owner ruling 2026-08-22) but it does not use these heights — it steps within a band
    /// centred on the hull's own ruled framing, <see cref="AboardStopsCloser"/> stops in and
    /// <see cref="AboardStopsWider"/> stops out. That band is re-centred every time the ruled framing
    /// changes, so the hull's "whole vessel visible" derivation, the deck step and the haul tighten all
    /// remain the thing you are given each time you arrive at them; the wheel is a look around from
    /// there, never a new resting place. A live haul and a road vehicle stay ruled outright.</para>
    /// </summary>
    [System.Serializable]
    public struct PlayerZoomSettings
    {
        [Tooltip("Let the mouse wheel (and the gamepad's shoulder buttons) change the walking view at " +
                 "all. Off = the on-foot framing is fixed, exactly as it was before this existed.")]
        public bool WheelEnabled;

        [Tooltip("CLOSEST the wheel may take the walking view, in metres of world height — the interior " +
                 "close-up. Smaller = further in. 5.625 is the tightest framing the game already " +
                 "ships (the live-haul step).")]
        [Min(0.5f)] public float ClosestWorldHeightMeters;

        [Tooltip("FARTHEST the wheel may take the walking view, in metres of world height. Larger = " +
                 "further out. 11.25 is one crisp stop wider than the standing on-foot framing — enough " +
                 "to see where you are going outdoors without the fisher shrinking to a speck.")]
        [Min(0.5f)] public float FarthestWorldHeightMeters;

        [Tooltip("How much scroll earns ONE tier, in the Input System's OWN units — which reports ONE " +
                 "per wheel detent on this project's setup (owner-measured 2026-08-26). The raw Win32 " +
                 "120-per-detent scale never reaches this code; shipping 120 here made a tier cost 120 " +
                 "clicks, a wheel dead to the hand. A trackpad reports a stream of fractions and has " +
                 "to travel the same distance to earn the same tier.")]
        [Min(1f)] public float WheelUnitsPerNotch;

        [Tooltip("Seconds to ease one tier step. 0 = snap. Either way the view LANDS on a crisp " +
                 "pixel-perfect stop — the ease only bridges the frames between two stops.")]
        [Min(0f)] public float StepSeconds;

        // ---- the ABOARD band (owner ruling 2026-08-22) -------------------------------------------
        //
        // ⚠️ COUNTS OF STOPS, and this is the one place in the camera's tuning that is not metres.
        // Every other dial is a world height because a ladder STEP INDEX reads backwards (it counts
        // up as the view gets closer). These two are neither: they are "how many stops either way",
        // and they cannot be metres because the thing they are measured from — the hull's own ruled
        // framing — is different for every vessel the player will ever own. A dory and a tanker share
        // an allowance of "two stops"; they share no pair of metre clamps at all.

        [Tooltip("How many crisp stops the wheel may take the view CLOSER than the ruled framing " +
                 "while aboard or on deck. 0 = the wheel cannot zoom in there at all. The band is " +
                 "centred on the hull's own framing and is re-centred every time that framing " +
                 "changes, so this is a look, never a new resting place.")]
        [Min(0)] public int AboardStopsCloser;

        [Tooltip("How many crisp stops the wheel may take the view WIDER than the ruled framing " +
                 "while aboard or on deck. 0 = the wheel cannot zoom out there at all. Separate from " +
                 "the closer allowance because the two answer different questions: in to read the " +
                 "deck, out to see the water you are crossing.")]
        [Min(0)] public int AboardStopsWider;

        /// <summary>
        /// The shipping range: 11.25 m out (one stop wider than standing on foot) to 5.625 m in (the
        /// live-haul step, the closest framing already in the game), 120 units of scroll to the tier,
        /// a short ease so a step reads as a move rather than a cut, and an aboard band of two stops
        /// either way around whatever the helm or the deck ruled.
        ///
        /// <para>⚠️ The two heights are the ×3 and ×6 PPU-32 stops at 1080p written out as literals,
        /// because the ladder that derives them lives in the App camera and Core may not reach it.
        /// <c>PlayerZoomTierTests</c> is the tripwire that fires if the ladder and these defaults ever
        /// drift apart.</para>
        /// </summary>
        public static PlayerZoomSettings Default => new PlayerZoomSettings
        {
            WheelEnabled = true,
            ClosestWorldHeightMeters = 5.625f,   // 1080 / (6 x 32)
            FarthestWorldHeightMeters = 11.25f,  // 1080 / (3 x 32)
            WheelUnitsPerNotch = 1f,             // one detent, in Input System units — MEASURED, not Win32's 120
            StepSeconds = 0.18f,
            AboardStopsCloser = 2,               // two stops in from the hull's ruled framing…
            AboardStopsWider = 2,                // …and two out (owner ruling 2026-08-22)
        };

        /// <summary>
        /// True when NOTHING on this struct has been authored — every field sitting on its C# zero.
        ///
        /// <para><b>⚠️ This is a real shape, not a hypothetical.</b> A <c>GameConfig</c> asset
        /// serialized before <see cref="GameConfig.PlayerZoom"/> existed carries no YAML for it, and
        /// Unity deserializes the missing block as <c>default</c> — which reads as a wheel that is
        /// OFF with a range of 0 m to 0 m. Every one of those is a silent, plausible-looking answer:
        /// nothing throws, nothing logs, and the wheel simply does nothing forever. The
        /// <c>[Min]</c> attributes do not help, because they police the Inspector and not the
        /// deserializer.</para>
        ///
        /// <para><b>Why WheelEnabled alone is not the test.</b> An owner who deliberately turns the
        /// wheel off keeps the rest of their tuning; an unwritten struct has no tuning to keep. Asking
        /// whether the WHOLE struct is blank is what separates "off on purpose" from "never
        /// authored", so <see cref="Sanitized"/> can heal the second without ever overriding the
        /// first.</para>
        /// </summary>
        public bool IsUnauthored =>
            !WheelEnabled && ClosestWorldHeightMeters <= 0f && FarthestWorldHeightMeters <= 0f
            && WheelUnitsPerNotch <= 0f && StepSeconds <= 0f
            && AboardStopsCloser == 0 && AboardStopsWider == 0;

        /// <summary>
        /// These settings with any UNSET value replaced by the shipped default — the read every
        /// consumer should make, so a config asset older than a field can never quietly pin the wheel.
        ///
        /// <para>Wholly blank (<see cref="IsUnauthored"/>) means the block was never written, and the
        /// answer is the shipped defaults entire. Otherwise only the values that cannot mean anything
        /// are healed: a clamp of 0 m is not a framing, and a notch size of 0 is not a wheel. Every
        /// authored value — <see cref="WheelEnabled"/> set to false very much included — is left
        /// exactly as the owner typed it, and the two stop allowances are left alone at zero because
        /// zero is a legitimate answer there ("the wheel does not move the helm").</para>
        /// </summary>
        public PlayerZoomSettings Sanitized()
        {
            if (IsUnauthored) return Default;

            PlayerZoomSettings d = Default;
            PlayerZoomSettings s = this;
            if (s.ClosestWorldHeightMeters <= 0f) s.ClosestWorldHeightMeters = d.ClosestWorldHeightMeters;
            if (s.FarthestWorldHeightMeters <= 0f) s.FarthestWorldHeightMeters = d.FarthestWorldHeightMeters;
            if (s.WheelUnitsPerNotch <= 0f) s.WheelUnitsPerNotch = d.WheelUnitsPerNotch;
            if (s.StepSeconds < 0f) s.StepSeconds = d.StepSeconds;
            if (s.AboardStopsCloser < 0) s.AboardStopsCloser = 0;
            if (s.AboardStopsWider < 0) s.AboardStopsWider = 0;
            return s;
        }
    }

    /// <summary>
    /// WHAT'S ON THE HOOK (<see cref="GameConfig.BaitTackle"/>) — how much the right bait and the right
    /// tackle change what bites. Consumed by <c>CatchResolver.BaitAffinity</c> /
    /// <c>LureAffinity</c> as plain multipliers on the catch roll, the same Core-policy /
    /// feature-consumer split as <see cref="RodFightSettings"/>.
    ///
    /// <para><b>Weights, never walls</b> — the promise depth already makes. The wrong kit catches LESS;
    /// it never catches NOTHING. That is what lets a beginner with a bare spoon still fill a bucket
    /// while an angler who has learned the pairings fills it faster and with what they were after.</para>
    ///
    /// <para><b>Bait is precise, tackle is broad.</b> The wrong bait is damped harder than the wrong
    /// lure, because a fish that wants food will refuse the wrong food outright, while a curious fish
    /// will still occasionally hit a lure it doesn't love. That asymmetry is the whole reason to carry
    /// both: bait to TARGET, tackle to COVER.</para>
    /// </summary>
    [System.Serializable]
    public struct BaitTackleSettings
    {
        [Tooltip("How much likelier a species is when the bait on the hook is one it wants (≥ 1; " +
                 "1 = bait doesn't matter). This is the strongest targeting tool in the game — bigger " +
                 "than the depth weight on purpose, because choosing bait is a deliberate act.")]
        [Min(1f)] public float BaitFavourBoost;

        [Tooltip("What's left of a species' chance when the bait is one it does NOT want (0..1; " +
                 "1 = no penalty). Low: the wrong bait is a real mistake. Never 0 — a haddock will " +
                 "still occasionally take a squid strip meant for cod.")]
        [Range(0f, 1f)] public float WrongBaitDamp01;

        [Tooltip("How much likelier a species is when the tackle tied on is one it chases (≥ 1; " +
                 "1 = tackle doesn't matter). Smaller than the bait boost — tackle covers water, bait " +
                 "picks a fish.")]
        [Min(1f)] public float LureFavourBoost;

        [Tooltip("What's left of a species' chance on a lure it doesn't favour (0..1). Gentler than the " +
                 "wrong-bait damp: a fish will hit the wrong lure out of curiosity far more readily " +
                 "than it will eat the wrong food.")]
        [Range(0f, 1f)] public float WrongLureDamp01;

        /// <summary>The reference tuning: the right bait roughly triples a species' share and the wrong
        /// one cuts it to a third; the right tackle doubles and the wrong one costs a third. So a
        /// correctly-baited, correctly-tackled cast is worth about six times a badly-chosen one — a
        /// decision you can feel — while nothing is ever off the table.</summary>
        public static BaitTackleSettings Default => new BaitTackleSettings
        {
            BaitFavourBoost = 3f,
            WrongBaitDamp01 = 0.35f,
            LureFavourBoost = 2f,
            WrongLureDamp01 = 0.65f,
        };
    }

    /// <summary>
    /// WORKING THE LURE (<see cref="GameConfig.Jigging"/> — owner's ask 2026-07-25). Five hand-feels,
    /// tuned in ONE place rather than restated on every tackle asset: a piece of tackle just names its
    /// <c>JigStyle</c> and the numbers live here, so the owner can re-feel all five without touching
    /// content. The pure maths that consumes them is <c>JigMath</c> (Fishing-side).
    ///
    /// <para>Tempo is strokes per second (a stroke = one reversal of the hand: the top of a lift, the
    /// bottom of a drop). Stroke is how far the pointer travels between reversals, in world metres — so
    /// these read against the same on-screen scale the cast does (the on-foot view is about 16 m wide).</para>
    /// </summary>
    [System.Serializable]
    public struct JiggingSettings
    {
        [Header("The five actions (tempo = strokes/sec, stroke = world metres)")]
        [Tooltip("COD JIG — big slow heaves, then let it flutter back. She takes it on the drop.")]
        [Min(0f)] public float LiftAndDropTempo;
        [Min(0f)] public float LiftAndDropStroke;

        [Tooltip("MACKEREL FEATHERS — short fast twitches, like a shoal of fry breaking up.")]
        [Min(0f)] public float QuickJerksTempo;
        [Min(0f)] public float QuickJerksStroke;

        [Tooltip("SPOON — one long even sweep, wobbling steadily through the water.")]
        [Min(0f)] public float SteadySweepTempo;
        [Min(0f)] public float SteadySweepStroke;

        [Tooltip("SOFT BAIT — slow and small, with long pauses. Something dying, not something fleeing.")]
        [Min(0f)] public float SlowCrawlTempo;
        [Min(0f)] public float SlowCrawlStroke;

        [Tooltip("SPINNER — quick and continuous; the blade has to keep turning or it is just a weight.")]
        [Min(0f)] public float FastSteadyTempo;
        [Min(0f)] public float FastSteadyStroke;

        [Header("How forgiving, and how dead")]
        [Tooltip("How far off the target action you can be and still score well (0..1). Bigger = the " +
                 "actions are easy to find by feel. At 0.5 you do well anywhere from about two-thirds " +
                 "to one-and-a-half times the target.")]
        [Range(0.05f, 1f)] public float Tolerance01;

        [Tooltip("What fraction of its fishing a MOTIONLESS lure keeps (0..1). The owner's call was " +
                 "'nearly dead, but bait still fishes' — low enough that working it is obviously the " +
                 "point, above zero because nothing else in this fishing is a hard wall. Expressed as a " +
                 "much longer wait for a bite, so the player can SEE it rather than being quietly " +
                 "denied by a dice roll.")]
        [Range(0.01f, 1f)] public float DeadLureFraction01;

        /// <summary>The reference feel. The five actions are spread wide enough to be distinct in the
        /// hand — a lift-and-drop is a big movement twice a second, a spinner is small and five times a
        /// second — and a dead lure fishes at a tenth, so it waits roughly ten times as long.</summary>
        public static JiggingSettings Default => new JiggingSettings
        {
            LiftAndDropTempo = 0.8f,  LiftAndDropStroke = 1.6f,
            QuickJerksTempo  = 4.0f,  QuickJerksStroke  = 0.35f,
            SteadySweepTempo = 1.2f,  SteadySweepStroke = 1.1f,
            SlowCrawlTempo   = 0.5f,  SlowCrawlStroke   = 0.45f,
            FastSteadyTempo  = 5.0f,  FastSteadyStroke  = 0.6f,
            Tolerance01 = 0.5f,
            DeadLureFraction01 = 0.1f,
        };
    }

    /// <summary>
    /// THE SEA'S HAND IN THE CATCH (<see cref="GameConfig.SeaFishing"/> — owner's ruling 2026-07-25),
    /// the dials behind Pillar 1's arrival in the rod loop. The pure maths that consumes them is
    /// <c>SeaFightMath</c> (Fishing-side), the same Core-policy / feature-consumer split as
    /// <see cref="RodFightSettings"/>.
    ///
    /// <para><b>The trade.</b> Rough water makes fish bolder — so it fishes BETTER
    /// (<see cref="SeaBoldness01"/> quickens the bite, <see cref="SeaBigFishBias01"/> brings up the better
    /// fish) — while the swell works your line, so it fights HARDER
    /// (<see cref="SeaFightFactor"/>). Weather becomes a decision, not a tax.</para>
    ///
    /// <para><b>All three at 0 = weather-blind fishing</b>, bit-for-bit as the rod loop shipped before
    /// this change: a gale plays exactly like a flat calm. That is the off-switch and the A/B baseline.</para>
    /// </summary>
    [System.Serializable]
    public struct SeaFishingSettings
    {
        [Tooltip("THE COST. Extra tension per second (0..1-gauge/s) the SEA puts through your line at a " +
                 "full storm — the swell working the rod, a wave snatching the line. It grows with the " +
                 "SQUARE of the sea state, so a chop is barely felt and a real sea is dangerous. 0 = OFF " +
                 "(the fight ignores the weather). Guard-rail: keep it low enough that easing off still " +
                 "recovers in EVERYDAY weather (test-enforced up to SeaFightMath.CozySeaCeiling01) — " +
                 "above that line the sea is meant to be able to beat you.")]
        [Min(0f)] public float SeaFightFactor;

        [Tooltip("THE REWARD (bites). How much quicker fish bite at a full storm, 0..1 — broken water " +
                 "hides you and emboldens them. 0.5 = bites arrive in half the time in a storm. Grows " +
                 "LINEARLY (unlike the cost), so even a chop already fishes noticeably better. 0 = OFF.")]
        [Range(0f, 1f)] public float SeaBoldness01;

        [Tooltip("THE REWARD (size). How strongly a full storm favours the BIG ones, 0..1 — the better " +
                 "fish come up to feed in broken water. 1 = a storm catch sits at the very top of the " +
                 "species' weight range. This is what makes a hard sea worth fishing, and a storm fish a " +
                 "story. 0 = OFF (the plain uniform weight roll).")]
        [Range(0f, 1f)] public float SeaBigFishBias01;

        /// <summary>
        /// The reference tuning, solved against the shipped species rather than guessed. The cost squares
        /// away to almost nothing in everyday weather — at the cozy ceiling it adds only 0.087, which every
        /// authored personality absorbs with room to spare — yet reaches 0.35 in a full storm, enough to
        /// out-pressure the ease and genuinely take the three STRONG fish (cod, pollock, haddock). The
        /// mackerel is deliberately left un-toothed even in a gale: a small fish becoming unlandable in
        /// weather would be frustration, not danger. The rewards arrive earlier and more gently — a storm
        /// bites about a third quicker and leans the size roll halfway up the range.
        /// </summary>
        public static SeaFishingSettings Default => new SeaFishingSettings
        {
            SeaFightFactor = 0.35f,
            SeaBoldness01 = 0.35f,
            SeaBigFishBias01 = 0.5f,
        };
    }

    /// <summary>
    /// WHERE THE FISH ARE (<see cref="GameConfig.FishSchools"/> — ADR 0025 S3, the owner's ruling of
    /// 2026-08-03). The dials behind the deterministic school sim: <c>FishSchoolMath</c> /
    /// <c>FishSchoolModel</c> (Fishing-side) consume them, the same Core-policy / feature-consumer split
    /// as <see cref="SeaFishingSettings"/> and <see cref="RodFightSettings"/>.
    ///
    /// <para><b>One model, two readers — so these numbers move BOTH at once.</b> The fish finder draws
    /// the schools these dials produce, and the fishing path raises the bite rate and weights the species
    /// roll from the SAME schools. There is deliberately no second "bite rate" number here: the density
    /// (<see cref="MinMarks"/>/<see cref="MaxMarks"/>) is both how many fish the glass draws and the
    /// expected bite rate, mapped by <see cref="BiteRatePerMark"/>. Tune the picture and you have tuned
    /// the fishing; they cannot drift apart.</para>
    ///
    /// <para><b>The owner's shape.</b> Fish are found in AREAS (<see cref="MinRadiusMetres"/>..
    /// <see cref="MaxRadiusMetres"/>), for a WHILE (<see cref="MinWindowHours"/>..
    /// <see cref="MaxWindowHours"/>), at a DEPTH (<see cref="MinDepthFraction01"/>..
    /// <see cref="MaxDepthFraction01"/> of the water column there). Whether a patch holds fish at all is
    /// decided by LOCATION (<see cref="CellSizeMetres"/> + <see cref="MinWaterColumnMetres"/>), WEATHER
    /// (<see cref="SeaStateAppearanceBias"/>) and DATE (the four season multipliers) — and those same
    /// three pick WHICH species are down there, weather by pushing the school deeper in a blow
    /// (<see cref="SeaStateDepthBias01"/>, which changes the depth band and so the species that live in
    /// it), date through each species' own authored season window.</para>
    ///
    /// <para><b>Recomputed, never saved</b> (rule 5) — schools are a function of
    /// <c>(worldSeed, gameTime, place, weather, season)</c> exactly as the tide and the wind are. Change
    /// any dial here and every school in the world changes with it, in an existing save, with no
    /// migration: there is nothing about a school on <c>SaveData</c> to be stale.</para>
    ///
    /// <para><b>The off switch.</b> <see cref="BaseAppearanceChance01"/> = 0 is an empty sea: no schools,
    /// no marks, and the catch roll is bit-for-bit the one that shipped before this existed (every
    /// school term is neutral with no school present). That is the A/B baseline.</para>
    /// </summary>
    [System.Serializable]
    public struct FishSchoolSettings
    {
        // ---- the lattice (WHERE and WHEN a patch of water is even asked the question) ---------------

        [Tooltip("Grain of the school sim (m): the world is diced into cells this wide, and each cell " +
                 "independently holds a school or doesn't. Smaller = fish are found in more, smaller " +
                 "pockets and the sea reads busier; larger = long empty runs between good ground. " +
                 "⚠ Also the hard cap on MaxRadiusMetres (a school never spills more than one cell), so " +
                 "raising the radius means raising this first.")]
        [Min(1f)] public float CellSizeMetres;

        [Tooltip("How often (in-game HOURS) a cell re-rolls — the fish move on and a new lot may show " +
                 "up. Each school's window lives inside one of these slots, so this is also the longest " +
                 "a window can be. Shorter = a restless sea you must keep re-reading; longer = ground " +
                 "that stays good long enough to be worth remembering.")]
        [Min(0.05f)] public float SlotHours;

        // ---- the appearance gate (location · weather · date — the owner's three) --------------------

        [Tooltip("Base chance (0..1) a cell holds a school in a given slot, BEFORE weather and season " +
                 "adjust it. This is the master 'how much fish is in the sea' dial. 0 = an empty sea: " +
                 "no marks and a catch roll bit-for-bit the pre-school one (the A/B off switch).")]
        [Range(0f, 1f)] public float BaseAppearanceChance01;

        [Tooltip("LOCATION gate: a cell whose water column is shallower than this (m) never holds a " +
                 "school. Fish are not on the beach — this is what keeps marks off the flats and out " +
                 "of the drying sandbar as the tide falls.")]
        [Min(0f)] public float MinWaterColumnMetres;

        [Tooltip("WEATHER gate: how much a full storm adds to (or, negative, takes from) the appearance " +
                 "chance. Positive by default and for the same reason SeaBoldness01 is — broken water " +
                 "emboldens fish, so a blow shows MORE of them. 0 = weather-blind schools.")]
        [Range(-1f, 1f)] public float SeaStateAppearanceBias;

        [Tooltip("DATE gate — Early Spring: multiplier on the appearance chance in this season. 1 = " +
                 "neutral. Below 1 = a lean season you have to work; above 1 = the run is on.")]
        [Min(0f)] public float EarlySpringAppearance;

        [Tooltip("DATE gate — High Summer: multiplier on the appearance chance in this season.")]
        [Min(0f)] public float HighSummerAppearance;

        [Tooltip("DATE gate — The Turn: multiplier on the appearance chance in this season.")]
        [Min(0f)] public float TheTurnAppearance;

        [Tooltip("DATE gate — Hard Winter: multiplier on the appearance chance in this season. Kept " +
                 "well below 1 by default: the winter sea is meant to be hard fishing.")]
        [Min(0f)] public float HardWinterAppearance;

        // ---- the window (the owner: finding one opens a window of time) -----------------------------

        [Tooltip("Shortest a school hangs about (in-game hours) once it shows. Clamped to SlotHours.")]
        [Min(0.01f)] public float MinWindowHours;

        [Tooltip("Longest a school hangs about (in-game hours). Clamped to SlotHours — a window never " +
                 "outlives the slot it was rolled in. The average window ÷ SlotHours is roughly the " +
                 "fraction of 'present' cells that are actually SHOWING at any instant, so this dial and " +
                 "BaseAppearanceChance01 together set how much of the sea is fishable right now.")]
        [Min(0.01f)] public float MaxWindowHours;

        // ---- the area (the owner: a tool for locating AREAS where fish are) -------------------------

        [Tooltip("Smallest school area (radius, m). Small = a tight spot you must sit right on top of.")]
        [Min(0.1f)] public float MinRadiusMetres;

        [Tooltip("Largest school area (radius, m). ⚠ Clamped to CellSizeMetres — the sim only searches " +
                 "the neighbouring cells, so a school that could spill further than one cell would be " +
                 "invisible from its own outer ring.")]
        [Min(0.1f)] public float MaxRadiusMetres;

        // ---- the depth (the owner: bites happen at approximately the depth shown) -------------------

        [Tooltip("Shallowest a school sits, as a FRACTION of the water column there (0 = the surface, " +
                 "1 = on the bottom). A fraction rather than metres so the same dial works over a 2 m " +
                 "flat and a 60 m hole, and so the mark always sits above the finder's bottom contour.")]
        [Range(0f, 1f)] public float MinDepthFraction01;

        [Tooltip("Deepest a school sits, as a fraction of the water column there.")]
        [Range(0f, 1f)] public float MaxDepthFraction01;

        [Tooltip("WEATHER, the second way: how far down a full storm pushes the school (added to the " +
                 "depth fraction). Fish go deep when it blows — and because depth picks the band, this " +
                 "is also how the weather changes WHICH species you find. 0 = weather-blind depth.")]
        [Range(0f, 1f)] public float SeaStateDepthBias01;

        [Tooltip("The water column (m) assumed where no bathymetry is authored (no tidal terrain — a " +
                 "bare test rig, an unpainted region). The 'no height map means open water' posture the " +
                 "rest of the module already takes, given a depth so schools still sit somewhere sane.")]
        [Min(0.1f)] public float OpenWaterColumnMetres;

        // ---- the density (the owner: one fish = lower bite rate, several = higher) ------------------

        [Tooltip("Fewest fish a school shows. The owner's ruling in one number: this IS the mark count " +
                 "on the glass AND the expected bite rate. 1 = the lonely single-fish mark.")]
        [Min(1)] public int MinMarks;

        [Tooltip("Most fish a school shows — the fat, worth-stopping-for return.")]
        [Min(1)] public int MaxMarks;

        [Tooltip("How much ONE mark speeds the bite: the wait is divided by (1 + marks × this), so 0.35 " +
                 "means a 3-fish school bites about twice as fast. This is the whole density→bite-rate " +
                 "map — the one place the picture becomes the fishing. 0 = marks are decoration.")]
        [Min(0f)] public float BiteRatePerMark;

        [Tooltip("Ceiling on that speed-up, so a freak fat school can't make bites instant (which reads " +
                 "as a bug and leaves no room for the cast/settle beat). 1 = the density never speeds " +
                 "anything.")]
        [Min(1f)] public float MaxBiteRateMultiplier;

        // ---- holding at the depth shown -------------------------------------------------------------

        [Tooltip("What the school is worth when you hold the rig in the WRONG depth band (0..1 of its " +
                 "full effect). Never 0 — being over fish is worth something even fished badly, the same " +
                 "promise depth and bait already make. Lower = the depth read on the glass matters more.")]
        [Range(0f, 1f)] public float OffDepthMatch01;

        [Tooltip("What the school is worth on a cast that plays NO depth game at all (the bobber/legacy " +
                 "branch, which has no held depth to judge). 1 = a bobber gets the school's full lift, " +
                 "so this feature never makes the old way of fishing worse. Lower it to make depth " +
                 "fishing the only way to properly work a school.")]
        [Range(0f, 1f)] public float DepthlessMatch01;

        // ---- which fish are down there ---------------------------------------------------------------

        [Tooltip("Fewest species one school holds (picked from the region's authored pool that the " +
                 "season allows and that live at the school's depth band).")]
        [Min(1)] public int MinSpecies;

        [Tooltip("Most species one school holds. Higher = a mixed shoal and a less targeted catch.")]
        [Min(1)] public int MaxSpecies;

        [Tooltip("How strongly a species IN the school is favoured in the catch roll. Applied on top of " +
                 "bait/tackle/depth as one more soft WEIGHT — never a filter, so an odd fish can always " +
                 "still take. Eased by how well you are sitting on the school, so clipping the rim " +
                 "barely re-weights anything. 1 = the school says nothing about what bites.")]
        [Min(1f)] public float SchoolSpeciesBoost;

        [Tooltip("How much a species NOT in the school is damped (0..1) while you are on one. Keep " +
                 "clearly above 0: fishing a school of mackerel should not make a cod impossible, only " +
                 "unlikely. 1 = no damp.")]
        [Range(0f, 1f)] public float OffSchoolSpeciesDamp01;

        /// <summary>
        /// The reference tuning for the St Peters opening, sized against the region rather than guessed:
        /// 120 m cells over a 760×520 m region give ~6×4 patches of ground, a little over half of which
        /// hold fish in a given 2.5 h slot, each showing for roughly half of it — so at any moment
        /// something like a tenth of the water is fishable and a working morning means reading the glass
        /// and moving, not parking. Schools run 22–55 m across (a dory covers one in a few seconds of
        /// steaming), sit a quarter to four-fifths of the way down the column, and hold 1–5 fish: a
        /// single mark bites ~1.35× as fast, a full five ~2.75× (the ceiling is 3). Winter is deliberately
        /// lean and high summer generous. Nothing here can zero a fish out: the wrong depth is still worth
        /// a third of the school, and a species the school does not hold is damped to 0.4, never barred.
        /// </summary>
        public static FishSchoolSettings Default => new FishSchoolSettings
        {
            CellSizeMetres = 120f,
            SlotHours = 2.5f,

            BaseAppearanceChance01 = 0.55f,
            MinWaterColumnMetres = 1.5f,
            SeaStateAppearanceBias = 0.25f,
            EarlySpringAppearance = 1f,
            HighSummerAppearance = 1.2f,
            TheTurnAppearance = 1f,
            HardWinterAppearance = 0.55f,

            MinWindowHours = 0.75f,
            MaxWindowHours = 2f,

            MinRadiusMetres = 22f,
            MaxRadiusMetres = 55f,

            MinDepthFraction01 = 0.25f,
            MaxDepthFraction01 = 0.8f,
            SeaStateDepthBias01 = 0.25f,
            OpenWaterColumnMetres = 25f,

            MinMarks = 1,
            MaxMarks = 5,
            BiteRatePerMark = 0.35f,
            MaxBiteRateMultiplier = 3f,

            OffDepthMatch01 = 0.35f,
            DepthlessMatch01 = 1f,

            MinSpecies = 1,
            MaxSpecies = 3,
            SchoolSpeciesBoost = 3f,
            OffSchoolSpeciesDamp01 = 0.4f,
        };
    }
}
