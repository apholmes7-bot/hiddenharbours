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
