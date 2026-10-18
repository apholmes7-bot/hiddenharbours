using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// What kind of place a bird haunt is. Open water is not authored: the surface schools are its
    /// haunts, made up by the flock while a school shows.
    /// </summary>
    public enum BirdHauntKind : byte
    {
        /// <summary>A fish plant or a drying yard: busiest in its working hours.</summary>
        FishPlant = 0,

        /// <summary>A wharf and its landing: busiest while the boats come in, and on market day.</summary>
        Wharf = 1,

        /// <summary>A beach the tide uncovers.</summary>
        Beach = 2,

        /// <summary>Flats, bars and beds the tide uncovers.</summary>
        Flats = 3,

        /// <summary>A place the birds stand about on: a breakwater, a roof, a plot.</summary>
        Loafing = 4,

        /// <summary>Where the birds sleep, and where a colony sits through the breeding seasons.</summary>
        Roost = 5,
    }

    /// <summary>
    /// ONE BIRD AT ONE PLACE — HOW MANY of a species use a place. A place's WHERE and WHEN (its box, its
    /// roost, its hours, its tide) are the place's own; the counts are per bird, so a second species
    /// is a second entry, not a second place.
    /// </summary>
    [Serializable]
    public sealed class BirdHauntEntry
    {
        [Tooltip("The bird, as a bird. id (bird.herring_gull). One entry per bird per place.")]
        public string SpeciesId = "";

        [Tooltip("Haunts: the most of this bird the place ever holds.")]
        [Min(0)] public int Capacity;

        [Tooltip("Haunts: birds at the peak of a fine High Summer morning, before the day's jitter.")]
        [Min(0f)] public float Typical;

        [Tooltip("Haunts: the season's share of Typical, in the clock's order: Early Spring, High Summer, " +
                 "The Turn, Hard Winter.")]
        public float[] SeasonWeights = { 1f, 1f, 1f, 1f };

        [Tooltip("Roosts: the colony, as a share of its haunts' population, on the rock day and night, in " +
                 "the clock's season order.")]
        public float[] Residents = { 0f, 0f, 0f, 0f };

        [Tooltip("Roosts: the most of this bird the ledges hold; any more roost off the map.")]
        [Min(0)] public int Room;
    }

    /// <summary>
    /// ONE BIRD HAUNT — a place on the coast that birds use: a fish plant, a wharf, a beach, the flats,
    /// a loafing spot, or a roost. One asset per place (rule 2), under <c>Data/Birds/Haunts</c>, gathered
    /// by <see cref="BirdHauntLibrary"/>.
    ///
    /// <para><b>Where and when are the place's; how many is the bird's.</b> The box, the roost, the
    /// shelter, the working hours, the market day, the tide, the perches and the way out belong to the
    /// place. How many of a bird use it lives in that bird's <see cref="BirdHauntEntry"/> in
    /// <see cref="Birds"/>, keyed by its <c>bird.</c> id.</para>
    ///
    /// <para><b>Ids are permanent once merged</b>: <c>birdhaunt.&lt;region&gt;_&lt;place&gt;</c>, or
    /// <c>birdroost.&lt;region&gt;_&lt;place&gt;</c> for a roost. Every hash about a place's birds is keyed
    /// by its id, so renaming one moves every bird it has.</para>
    ///
    /// <para><b>Content, never state.</b> Nothing here enters the save; the birds are a function of
    /// these numbers, the seed and the game time (rule 5).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Bird Haunt", fileName = "BirdHaunt")]
    public sealed class BirdHauntDef : ScriptableObject
    {
        /// <summary>The id prefix of a haunt.</summary>
        public const string HauntPrefix = "birdhaunt.";

        /// <summary>The id prefix of a roost.</summary>
        public const string RoostPrefix = "birdroost.";

        /// <summary>The id prefix of a bird.</summary>
        public const string SpeciesPrefix = "bird.";

        [Tooltip("birdhaunt.<region>_<place>, or birdroost.<region>_<place> for a roost. Permanent once merged.")]
        public string Id = "";

        [Tooltip("The region it belongs to (region.st_peters). The flock uses only the current region's places.")]
        public string RegionId = "";

        public BirdHauntKind Kind = BirdHauntKind.Loafing;

        [Tooltip("The centre of the box on the ground the birds use, world metres (the key-scene frame).")]
        public Vector2 Centre;

        [Tooltip("The box's half-size, metres (x across, y up the map).")]
        public Vector2 HalfSize = new Vector2(8f, 8f);

        [Tooltip("Haunts: the roost its birds sleep at (birdroost.…). Empty on a roost.")]
        public string RoostId = "";

        [Tooltip("Haunts: keeps the tuning's ShelterKeep of its birds, all on their feet, in a gale.")]
        public bool Sheltered;

        [Header("Working hours")]
        [Tooltip("The hour its work starts (the plant's shift, the morning landing).")]
        [Range(0f, 24f)] public float BusyFromHour;

        [Tooltip("The hour its work stops. From = To means no working hours.")]
        [Range(0f, 24f)] public float BusyToHour;

        [Tooltip("x birds inside the working hours (1 = none). Keep it at or under the tuning's PopulationHeadroom.")]
        [Min(0f)] public float BusyBoost = 1f;

        [Tooltip("Works only on fine days: no boost in wind or fog (the flakes are not spread in the rain).")]
        public bool BusyOnFineDaysOnly;

        [Tooltip("x birds on market day (1 = none).")]
        [Min(0f)] public float MarketDayBoost = 1f;

        [Header("Tide")]
        [Tooltip("The tide uncovers it: it opens as the water falls.")]
        public bool HasTide;

        [Tooltip("Tide height (m) below which it starts to open.")]
        public float TideOpensBelowMetres;

        [Tooltip("How much lower (m) the tide falls before it is fully open.")]
        [Min(0f)] public float TideRampMetres = 0.8f;

        [Tooltip("The share of its birds it keeps at high water (0 = covered, none).")]
        [Range(0f, 1f)] public float HighWaterKeep;

        [Header("The day")]
        [Tooltip("The quiet middle of the day, as a share of the morning crowd.")]
        [Range(0f, 1f)] public float MiddayLevel = 0.8f;

        [Tooltip("The afternoon, as a share of the morning crowd.")]
        [Range(0f, 1f)] public float AfternoonLevel = 0.8f;

        [Header("Perches and the way out")]
        [Tooltip("Hints: the ids of perches and key-scene gull pieces inside the box (the content test " +
                 "checks that each one exists). The birds claim perches through the perch registry.")]
        public string[] PerchIds = Array.Empty<string>();

        [Tooltip("Roosts: the way its birds fly when they leave the map (to forage farther afield, or to " +
                 "sleep elsewhere when the rock is full). A haunt left at zero uses its roost's.")]
        public Vector2 AwayHeading;

        [Header("The birds")]
        [Tooltip("How many of each bird use it: one entry per bird, keyed by its bird. id.")]
        public List<BirdHauntEntry> Birds = new List<BirdHauntEntry>();

        /// <summary>Is it a roost?</summary>
        public bool IsRoost => Kind == BirdHauntKind.Roost;

        /// <summary>The entry for one bird, by its <c>bird.</c> id. False when the place has none.</summary>
        public bool TryGetEntry(string speciesId, out BirdHauntEntry entry)
        {
            if (Birds != null && !string.IsNullOrEmpty(speciesId))
                for (int i = 0; i < Birds.Count; i++)
                {
                    BirdHauntEntry candidate = Birds[i];
                    if (candidate != null && string.Equals(candidate.SpeciesId, speciesId, StringComparison.Ordinal))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            entry = null;
            return false;
        }

        /// <summary>
        /// The schedule's view of this place and ONE of its birds: plain numbers and a hashed id, nothing
        /// from the engine. <paramref name="roostIndex"/> is the roost's index in the caller's list
        /// (−1 for none, and always −1 for a roost). The away heading comes out normalised (zero stays
        /// zero; the caller falls back to the roost's).
        /// </summary>
        public GullHauntSpec ToSpec(BirdHauntEntry entry, int roostIndex)
        {
            var spec = new GullHauntSpec
            {
                IdHash = GullHash.Id(Id),
                Kind = Kind,
                CentreX = Centre.x,
                CentreY = Centre.y,
                HalfX = Mathf.Abs(HalfSize.x),
                HalfY = Mathf.Abs(HalfSize.y),
                RoostIndex = IsRoost ? -1 : roostIndex,
                Sheltered = Sheltered,
                BusyFromHour = BusyFromHour,
                BusyToHour = BusyToHour,
                BusyBoost = BusyBoost,
                BusyOnFineDaysOnly = BusyOnFineDaysOnly,
                MarketDayBoost = MarketDayBoost,
                HasTide = HasTide,
                TideOpensBelowMetres = TideOpensBelowMetres,
                TideRampMetres = TideRampMetres,
                HighWaterKeep = HighWaterKeep,
                MiddayLevel = MiddayLevel,
                AfternoonLevel = AfternoonLevel,
            };

            float length = AwayHeading.magnitude;
            if (length > 0f)
            {
                spec.AwayX = AwayHeading.x / length;
                spec.AwayY = AwayHeading.y / length;
            }

            if (entry != null)
            {
                spec.Capacity = Mathf.Clamp(entry.Capacity, 0, GullRoster.MaxSlotsPerPlace);
                spec.Typical = Mathf.Max(0f, entry.Typical);
                spec.SeasonWeights = Seasons(entry.SeasonWeights);
                spec.Residents = Seasons(entry.Residents);
                spec.Room = Mathf.Clamp(entry.Room, 0, GullRoster.MaxSlotsPerPlace);
            }
            return spec;
        }

        private static GullSeasons Seasons(float[] four)
            => four == null ? default : new GullSeasons(At(four, 0), At(four, 1), At(four, 2), At(four, 3));

        private static double At(float[] values, int i) => i < values.Length ? values[i] : 0.0;
    }
}
