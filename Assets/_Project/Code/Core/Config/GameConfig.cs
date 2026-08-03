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

        [Header("Displaced water (ADR 0023 — the sea's readable drama)")]
        [Tooltip("Owner tuning for the displaced water surface (ADR 0023, phase 2): how much taller " +
                 "the sea DRAWS than it simulates, how wide the tear-safe calm band along every shore " +
                 "is, and how strongly the rare big wave is marked by foam and shade. All read LIVE " +
                 "each water tick (~8 Hz), so tuning this asset in Play moves the sea within a " +
                 "second. WaveExaggeration is THE one shared constant every water-riding visual " +
                 "reads (surface lift now; hull heave, buoys and wake in phase 3) — tune it here " +
                 "and boat and sea stay on the same water, never retuned apart.")]
        public DisplacedWaterSettings DisplacedWater = DisplacedWaterSettings.Default;

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

        [Header("Freshness & rot (M1 §7.3 — the clock on every catch)")]
        [Tooltip("How fast each storage mode rots a landed catch, and how far gone a catch can be " +
                 "before no buyer will take it. The per-species base rate lives on each " +
                 "FishSpeciesDef.SpoilPerDay; these are the world-policy dials on top of it.")]
        public FreshnessSettings Freshness = FreshnessSettings.Default;

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

        [Tooltip("Margin (px) from the screen's right edge to the small card (placeholder bottom-right " +
                 "placement — reposition freely).")]
        [Min(0f)] public float MarginX;

        [Tooltip("Margin (px) from the screen's bottom edge to the small card.")]
        [Min(0f)] public float MarginY;

        [Tooltip("Where the FOCUSED card centres on screen, as a 0..1 fraction of screen width.")]
        [Range(0f, 1f)] public float FocusCenterX01;

        [Tooltip("Where the FOCUSED card centres on screen, as a 0..1 fraction of screen height.")]
        [Range(0f, 1f)] public float FocusCenterY01;

        [Tooltip("How close (rig-space px) a click must land to the lever's grip to START a drag; " +
                 "clicks further out on the card jump-to-sig along the travel arc instead.")]
        [Min(1f)] public float GrabRadiusPx;

        [Tooltip("The tiller's throttle drag travel: how many rig-space px of vertical drag sweep the " +
                 "drive across its FULL range (up = ahead). Smaller = twitchier.")]
        [Min(1f)] public float TillerDragFullDrivePx;

        /// <summary>Native-size card bottom-right, 2× focus centred just above screen centre.</summary>
        public static HelmOverlaySettings Default => new HelmOverlaySettings
        {
            SmallScale = 1f,
            FocusScale = 2f,
            MarginX = 24f,
            MarginY = 16f,
            FocusCenterX01 = 0.5f,
            FocusCenterY01 = 0.5f,
            GrabRadiusPx = 30f,
            TillerDragFullDrivePx = 140f,
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

        /// <summary>
        /// The ADR-cited defaults: ×1.5 exaggeration (the readability sweet spot, shear-free at the
        /// coast), the proven tear-safe band coefficient (<see cref="ShoreFadeMath.RecommendedBandCoefficient"/>),
        /// full envelope salience with the spike-tuned 0.62 threshold, and the production 0.35 band
        /// blend. Pinned equal to the shader property defaults and the Art twin constants by
        /// <c>DisplacedWaterConfigTests</c>.
        /// </summary>
        public static DisplacedWaterSettings Default => new DisplacedWaterSettings
        {
            WaveExaggeration = 1.5f,
            ShoreBandCoefficient = ShoreFadeMath.RecommendedBandCoefficient,
            CapSalienceStrength = 1f,
            CapEnvelopeThreshold = 0.62f,
            EnvelopeBandStrength = 0.35f,
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
}
