using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Data definition for one region's <b>ambient fisher fleet</b> (canon M2-33, P3 "Living Working
    /// Coast"): a handful of NPC boats that sail the deep water, set their own buoys, and haul them —
    /// pure presentational flavour. Content is data, not code (ADR 0003): a region gets a fleet by
    /// authoring one of these assets (Data/Boats) and listing it in the Resources
    /// <see cref="AmbientFleetLibrary"/>; no code change, no scene/builder wiring.
    ///
    /// <para><b>Decor tier, deterministic, never saved.</b> Everything the fleet does is recomputed
    /// from <c>(worldSeed, gameTime)</c> (rule 5): spots, schedule, and buoy state are pure functions
    /// (<see cref="AmbientFleetPlan"/> / <see cref="AmbientFleetSchedule"/>). Nothing here enters the
    /// save, the market, or the player's catch — the fleet is the coast <em>looking</em> worked, not
    /// simulated competitors.</para>
    ///
    /// <para><b>Owner tunables.</b> Every number an owner might want to feel-tune lives here
    /// (rule 6): boat count, speed band, work rhythm, depth margin, avoidance berths, buoy palette.
    /// Timings are expressed as fractions of the game day / slot so they follow the clock's day
    /// length with no unit conversion.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Ambient Fleet", fileName = "AmbientFleet")]
    public class AmbientFleetDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): type.snake_case.")]
        public string Id = "fleet.st_peters_ambient";
        [Tooltip("Scene name of the region this fleet works (e.g. StPeters). The presenter activates the " +
                 "fleet only while this scene is active. Blank = any region that registers a tidal terrain.")]
        public string RegionSceneName = "StPeters";

        [Header("Fleet")]
        [Tooltip("How many boats work the grounds. The owner's ask is 3-5; each boat gets a seeded " +
                 "identity (speed, phase, buoy colour) so the fleet reads as individuals.")]
        [Range(1, 8)] public int BoatCount = 4;
        [Tooltip("Hull sprite (drawn bow-up, like the Dory/Punt — the root rotates to heading). Null = a " +
                 "small greybox wedge is generated in code so the fleet still reads before art is wired.")]
        public Sprite HullSprite;
        [Tooltip("SpriteRenderer sortingOrder for the hulls. 0 matches the player's hull (Sea plane is -5).")]
        public int HullSortingOrder = 0;
        [Tooltip("Slowest cruise speed in the fleet (world m per clock second). Each boat picks a seeded " +
                 "speed between Min and Max so the fleet doesn't move in lockstep.")]
        [Min(0.1f)] public float MinSpeedMetersPerSecond = 1.6f;
        [Tooltip("Fastest cruise speed in the fleet (world m per clock second).")]
        [Min(0.1f)] public float MaxSpeedMetersPerSecond = 2.4f;
        [Tooltip("How fast a boat can swing her bow (degrees per clock second). Small-boat handling: " +
                 "brisk but never twitchy.")]
        [Min(1f)] public float TurnRateDegreesPerSecond = 70f;

        [Header("Fishing grounds (the world-rect spots are drawn from; the depth gate carves the real shape)")]
        [Tooltip("Centre of the grounds rectangle (world units). St Peters default: the deep harbour " +
                 "south of the sandbar, clear of the island and the player's slip.")]
        public Vector2 GroundsCenter = new Vector2(5f, -32f);
        [Tooltip("Size of the grounds rectangle (world units).")]
        public Vector2 GroundsSize = new Vector2(85f, 22f);
        [Tooltip("Buoy spots each boat works per day. 2 keeps the loop readable (set one, go work the " +
                 "other, come back) and the buoy count modest (rule 7).")]
        [Range(1, 4)] public int SpotsPerBoat = 2;
        [Tooltip("Minimum water depth (m) a spot or travel leg must keep at the LOWEST water the tide can " +
                 "ever reach (spring low = mean − amplitude), so a falling tide can never strand a planned " +
                 "route. St Peters: floor −4 m, spring low −3.5 m → at most ~0.5 m clears; 0.4 floats a punt.")]
        [Min(0.05f)] public float MinDepthMeters = 0.4f;
        [Tooltip("Minimum spacing (m) between any two planned buoy spots, across the whole fleet.")]
        [Min(0f)] public float SpotSpacingMeters = 8f;
        [Tooltip("Sampling step (m) when validating the depth of a travel leg between two spots.")]
        [Min(0.5f)] public float LegSampleStepMeters = 4f;
        [Tooltip("Max seeded candidates tried per spot before the planner relaxes the depth margin.")]
        [Range(8, 256)] public int MaxCandidateTries = 64;

        [Header("Work rhythm (fractions of the game day / of a slot — follows the clock, no unit maths)")]
        [Tooltip("How many work slots a game day divides into. One slot = sail to the next spot, lay " +
                 "alongside, work it. 12 slots on a 20-minute day ≈ a beat every 100 real seconds per boat.")]
        [Range(2, 48)] public int SlotsPerDay = 12;
        [Tooltip("Fraction of a slot when the boat should be holding alongside her buoy spot (arrived, " +
                 "working). Before this she is in transit.")]
        [Range(0f, 1f)] public float WorkWindowStartFraction = 0.55f;
        [Tooltip("Fraction of a slot when the work is done and she bears away for the next spot. The " +
                 "place/haul FLIP lands at the midpoint of the window.")]
        [Range(0f, 1f)] public float WorkWindowEndFraction = 0.8f;
        [Tooltip("Clock seconds the buoy pop (on place) / dip-and-vanish (on haul) beat takes.")]
        [Min(0.1f)] public float BuoyBeatSeconds = 0.9f;
        [Tooltip("How far (world units) the buoy sinks during the haul dip / rises from on the place pop.")]
        [Min(0f)] public float BuoyDipMeters = 0.45f;

        [Header("Keeping clear (separation steering + the tide-aware look-ahead)")]
        [Tooltip("Radius (m) inside which NPC boats shoulder away from each other.")]
        [Min(0f)] public float BoatAvoidRadius = 6f;
        [Tooltip("Radius (m) inside which NPC boats give the PLAYER's boat a berth (bigger — they get " +
                 "out of your way, you never get out of theirs).")]
        [Min(0f)] public float PlayerAvoidRadius = 8f;
        [Tooltip("Radius (m) of the polite berth around the player's own placed buoys (read off the Core " +
                 "TrapPlaced signal — never their gear, never fouling your line).")]
        [Min(0f)] public float PlayerBuoyAvoidRadius = 4f;
        [Tooltip("How far ahead of the bow (m) the shoal probes look. The live twin of the plan-time " +
                 "depth gate: catches a detour pushed off the planned route.")]
        [Min(0.5f)] public float DepthLookAheadMeters = 6f;
        [Tooltip("Angle (degrees) the port/starboard shoal probes splay off the bow.")]
        [Range(5f, 80f)] public float DepthProbeSideDegrees = 40f;
        [Tooltip("Distance (m) from the target at which a boat starts easing off to arrive gently.")]
        [Min(0.1f)] public float ArriveSlowRadius = 5f;
        [Tooltip("Distance (m) inside which a boat holds station at her spot (stopped, working).")]
        [Min(0.1f)] public float HoldRadius = 1.2f;

        [Header("Buoys (colour = whose gear it is — ratified owner direction; the player's is yellow)")]
        [Tooltip("Float colours cycled per boat, so each fisher's gear reads as theirs and NEVER as the " +
                 "player's yellow. Defaults: red, white, green, orange.")]
        public Color[] BuoyPalette =
        {
            new Color(0.85f, 0.20f, 0.15f),   // red
            new Color(0.92f, 0.92f, 0.88f),   // white
            new Color(0.20f, 0.65f, 0.30f),   // green
            new Color(0.95f, 0.55f, 0.15f),   // orange
        };
        [Tooltip("SpriteRenderer sortingOrder for the buoys. 3 matches the player's trap buoys (above the " +
                 "Sea plane at -5).")]
        public int BuoySortingOrder = 3;

        // Appended after BuoySortingOrder (owner feedback on #189 — "spinning in circles"). Fields are
        // append-only: the shipped asset picks these defaults up without an edit.
        [Header("Seamanship (how she handles — arrive, settle, turn with way)")]
        [Tooltip("Fraction of the turn rate she keeps with no way on (bare steerage). She turns WITH " +
                 "way: full rate at cruise, this floor when nearly stopped — so she can't pirouette on " +
                 "the spot. Keep near the default: much lower (or a much faster fleet) and a boat can " +
                 "end up circling a spot she can never quite reach.")]
        [Range(0.05f, 1f)] public float SteerageTurnFraction = 0.45f;
        [Tooltip("Heading error (degrees) at which she is fully eased down for the turn — slow through " +
                 "the turn, drive out of it. Smaller = she slows for gentler course changes.")]
        [Range(10f, 180f)] public float SlowForTurnDegrees = 90f;
        [Tooltip("Fraction of speed she keeps through the hardest turn (a come-about). 1 = never slows " +
                 "for a turn (the old spin); lower reads as more deliberate seamanship.")]
        [Range(0.05f, 1f)] public float SlowForTurnSpeedFraction = 0.35f;
        [Tooltip("How fast way builds when she gets under way (fraction of cruise per second — 0.7 ≈ " +
                 "stopped to full in a second and a half). Way comes OFF instantly: drag stops a punt " +
                 "far faster than oars drive one.")]
        [Min(0.05f)] public float AccelFractionPerSecond = 0.7f;
        [Tooltip("Minimum speed (fraction of cruise) she keeps while genuinely manoeuvring clear of " +
                 "something — enough way to steer with, engaged only against a real push (see " +
                 "HoldEnterRepulsion), never by a faint far-off neighbour.")]
        [Range(0f, 1f)] public float AvoidNudgeSpeedFraction = 0.4f;

        [Header("Lying-to at the spot (the settle gate — with hysteresis so she isn't easily woken)")]
        [Tooltip("Social push (summed avoidance from boats/player/buoys; ~0.25 ≈ the player about 6 m " +
                 "off) above which she won't SETTLE at her spot — she stands off politely instead. " +
                 "Below it she comes alongside, takes the way off, and lies-to.")]
        [Min(0f)] public float HoldEnterRepulsion = 0.25f;
        [Tooltip("Social push it takes to WAKE a boat already lying at her spot (~0.6 ≈ the player " +
                 "within about 3 m). Keep it well above HoldEnterRepulsion — the gap is the hysteresis " +
                 "that stops a drifting-past player rousing her into circles.")]
        [Min(0f)] public float HoldWakeRepulsion = 0.6f;
        [Tooltip("A held boat displaced beyond HoldRadius × this gets under way again to re-take her " +
                 "spot. 2 = she tolerates small nudges without fuss.")]
        [Min(1f)] public float HoldReleaseRadiusFactor = 2f;

        [Header("Passing head-on (the starboard convention — gated to a true bow-to-bow meet)")]
        [Tooltip("How hard two boats meeting head-on both bear away to starboard so they agree how to " +
                 "pass (0 = deadlock risk; higher = a wider, earlier swing).")]
        [Range(0f, 1f)] public float HeadOnStarboardBias = 0.35f;
        [Tooltip("Angle (degrees) between her course and the push at which the starboard bias STARTS " +
                 "to engage (180 = dead opposed). Below this a push composes straight — glancing " +
                 "avoidance must never curl sideways into an orbit.")]
        [Range(90f, 180f)] public float HeadOnBiasBeginDegrees = 120f;
        [Tooltip("Angle (degrees) at which the starboard bias is FULLY engaged. Keep above " +
                 "HeadOnBiasBeginDegrees.")]
        [Range(90f, 180f)] public float HeadOnBiasFullDegrees = 155f;
    }
}
