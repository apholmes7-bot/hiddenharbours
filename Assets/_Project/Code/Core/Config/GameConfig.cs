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
        [Header("Clock")]
        [Tooltip("Real seconds per in-game day. 1200 = a 20-minute day.")]
        public float SecondsPerDay = 1200f;
        [Min(1)] public int DaysPerWeek = 7;
        [Min(1)] public int DaysPerSeason = 28;
        [Tooltip("Which weekday is Market Day at Greywick (0 = Monday).")]
        public int MarketDayIndex = 4; // Friday

        [Header("Tide")]
        [Tooltip("Principal lunar semidiurnal period in hours (~12.42 = two highs per tidal day).")]
        public float TidalPeriodHours = 12.4206f;
        [Tooltip("Moon cycle in in-game days; drives the spring/neap envelope. Canon: 28.")]
        public float LunarMonthDays = 28f;
        [Tooltip("At neap, amplitude is this fraction of spring amplitude (0..1).")]
        [Range(0f, 1f)] public float NeapAmplitudeFraction = 0.45f;

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

        [Header("Market (VS-16)")]
        [Tooltip("Demand D at the home cove (Coddle Cove) in priceMult = 1/(1+e·S/D). 1 = neutral baseline.")]
        [Min(0.01f)] public float MarketDemandCove = 1f;
        [Tooltip("Demand D at Port Greywick. Set higher than the cove so WHERE you sell matters: Greywick " +
                 "pays a premium on a glut (the reason to make the hop), and its supply recovers separately. " +
                 "(economy-and-business §1.2/§1.4)")]
        [Min(0.01f)] public float MarketDemandGreywick = 1.4f;
        [Tooltip("Fraction of a category's accumulated supply (glut) cleared at each daily settle (0..1). " +
                 "Higher = faster price recovery over days (economy-and-business §1.3). Deterministic — fired " +
                 "on day rollover, not per frame.")]
        [Range(0f, 1f)] public float MarketDailyRecovery = 0.5f;

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

        [Tooltip("Landing gained per second by a clean PULL in a full slack window, 0..1-gauge/s. The pace " +
                 "of a well-fought fight; the same rate scales the Surface counter-steer's tiring gain. " +
                 "Keep below TensionRisePerSec (the snap-before-land guard-rail).")]
        [Min(0f)] public float LandingFillPerSec;

        [Tooltip("EXTRA tension per second her run adds at full effort (fishEffort01 = 1), applied whether " +
                 "pulling or maintaining — she is fighting too. Keep below TensionFallPerSec so a run is a " +
                 "'back off' tell, never an unavoidable snap.")]
        [Min(0f)] public float RunTensionPressure;

        [Tooltip("Tension bled per second by a FULL counter-steer against a full dart (Surface phase only); " +
                 "the same magnitude is the penalty for steering INTO her. Bigger = the steer axis matters " +
                 "more once she surfaces.")]
        [Min(0f)] public float CounterSteerRelief;

        [Tooltip("Landing fraction (0..1) at which she breaks the surface: the fight crosses Deep (timing " +
                 "only, fish unseen, steer ignored) → Surface (timing + steer). Lower = the steer game " +
                 "starts sooner.")]
        [Range(0f, 1f)] public float SurfaceThreshold01;

        /// <summary>
        /// The forgiving-cove reference tuning: a pull loads clearly faster than it lands (0.55 &gt; 0.35 —
        /// the blind hold snaps first), a maintain bleeds twice her run's pressure (0.70 &gt; 0.35 — backing
        /// off always recovers), a moderate counter-steer axis, and the surface break at half-landed so both
        /// halves of the arc get play.
        /// </summary>
        public static RodFightSettings Default => new RodFightSettings
        {
            TensionRisePerSec = 0.55f,
            TensionFallPerSec = 0.70f,
            LandingFillPerSec = 0.35f,
            RunTensionPressure = 0.35f,
            CounterSteerRelief = 0.45f,
            SurfaceThreshold01 = 0.5f,
        };
    }

    /// <summary>
    /// The owner-tunable feel of the <b>flick-cast</b> (<see cref="GameConfig.FlickCast"/> — Rod Fishing v2,
    /// design/rod-fishing-v2-brainstorm.md §2.2), named and serializable so the whole gesture is dialled in
    /// the Inspector with no code (rule 6). Lives in Core beside the config it rides on, the same
    /// Core-policy / feature-consumer split as <see cref="RodFightSettings"/>: the pure maths that consumes
    /// it (<c>FlickCastMath</c>, Fishing-side) is fed this struct plus the cast cap.
    ///
    /// <para><b>The per-gear seam.</b> <see cref="MaxCastDistanceMetres"/> is the CAP a full-power,
    /// well-timed flick reaches. It is a GameConfig field for now; later, better rods/tackle extend it
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

        [Tooltip("Sweep LENGTH (m) that counts as full power. A longer drag past this adds nothing — " +
                 "smaller = big casts come easier from small gestures.")]
        [Min(0f)] public float FullPowerFlickMetres;

        [Tooltip("Sweep SPEED (m/s at the fastest part of the forward sweep) that counts as full power. " +
                 "Smaller = a lazy flick still throws far; larger = only a real snap of the wrist maxes out.")]
        [Min(0f)] public float FullPowerFlickSpeed;

        [Tooltip("How power blends sweep length vs sweep speed (0 = all length, 1 = all speed, 0.5 = even). " +
                 "Speed-heavy rewards the wrist-snap; length-heavy rewards the big wind-up.")]
        [Range(0f, 1f)] public float SpeedWeight01;

        [Tooltip("The SWEET RELEASE point: how far PAST the character (m, toward the water) the mouse " +
                 "should be when you release for a clean cast. Release around here = full quality.")]
        [Min(0f)] public float SweetReleaseMetres;

        [Tooltip("Half-width (m) of the full-quality band around the sweet release point. Wider = the " +
                 "timing beat is more forgiving.")]
        [Min(0f)] public float SweetWindowMetres;

        [Tooltip("How far (m) beyond the sweet band the release quality fades to zero. Releasing way too " +
                 "early (still behind you) or way too late piles the line at your feet — a short cast, " +
                 "never a fail.")]
        [Min(0f)] public float QualityFalloffMetres;

        [Tooltip("Fraction of the powered distance a completely MIS-timed release still flies (0..1). " +
                 "This is the 'piled-up line' short cast — keep it above 0 so a botched cast still plops " +
                 "in the water near you (cozy fail).")]
        [Range(0f, 1f)] public float PiledCastFraction01;

        [Tooltip("Shortest distance (m) any successful cast lands from the character. The floor under a " +
                 "weak/botched flick, so the bobber is always at least in the water, not on your boots.")]
        [Min(0f)] public float MinCastMetres;

        [Tooltip("The CAP (m): the farthest a full-power, perfectly-timed flick can reach with the starter " +
                 "rod. Better rods/tackle extend this later (per-gear data — the P4 upgrade you feel).")]
        [Min(0f)] public float MaxCastDistanceMetres;

        [Tooltip("How fast the cast line flies out (m/s) once released — pacing for the line-in-flight " +
                 "beat between the flick and the splash-down. Feel only; distance is decided at release.")]
        [Min(0.01f)] public float LineFlightMetresPerSec;

        /// <summary>The forgiving-cove reference tuning: a comfortable ~1.5 m wind-back, full power from a
        /// ~4 m or brisk sweep, a generous ±0.8 m sweet band ~1 m past the character, a mistimed cast still
        /// flying a quarter of its power, and the starter rod capped at 12 m.</summary>
        public static FlickCastSettings Default => new FlickCastSettings
        {
            MinWindBackMetres = 0.6f,
            MinFlickLengthMetres = 0.4f,
            FullPowerFlickMetres = 4f,
            FullPowerFlickSpeed = 12f,
            SpeedWeight01 = 0.5f,
            SweetReleaseMetres = 1.0f,
            SweetWindowMetres = 0.8f,
            QualityFalloffMetres = 2.0f,
            PiledCastFraction01 = 0.25f,
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
}
