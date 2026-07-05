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

        // Convenience
        public float SecondsPerHour => SecondsPerDay / 24f;
        public float SecondsPerWeek => SecondsPerDay * DaysPerWeek;
        public float SecondsPerSeason => SecondsPerDay * DaysPerSeason;
        public float SecondsPerYear => SecondsPerSeason * 4f;
    }
}
