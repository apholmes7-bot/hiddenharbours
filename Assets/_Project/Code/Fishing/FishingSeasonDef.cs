using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// One open season for one species in one region (a port's waters): the fishery is open from the
    /// open day through the close day, both inclusive, and closed the rest of the year. Outside the window
    /// an animal of this species hauled in this region is still caught but must go back over the side
    /// (not kept, not sellable). One entity per file, stable id (rule 2); values are owner-tunable data
    /// (rule 6). The read is pure and recomputed, never saved (<see cref="FishingSeasonMath"/>).
    ///
    /// <para>Referenced from the <see cref="DeckWorkDef"/> that sorts the species on deck, beside its
    /// legal-size rule: the season is another legal rule of the same honest fishery. A region with no
    /// entry for the species is open all year; two entries for one species and region are a union.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing Season", fileName = "FishingSeason")]
    public class FishingSeasonDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only id (e.g. \"season.lobster_st_peters\"). Never reuse or rename.")]
        public string Id = "season.new";
        public string DisplayName = "Fishing season";

        [Header("Which fishery")]
        [Tooltip("The species this season governs (a FishSpeciesDef id, e.g. \"fish.lobster\").")]
        public string SpeciesId = "fish.lobster";
        [Tooltip("The region (port waters) this season applies in: where the gear was SET, read from the " +
                 "placed trap's region id (e.g. \"region.st_peters\").")]
        public string RegionId = "region.new";

        [Header("Open window (inclusive both ends; open after close wraps the year end)")]
        [Tooltip("Season of the first open day.")]
        public Season OpenSeason = Season.EarlySpring;
        [Min(1)]
        [Tooltip("Day of that season (1-based) the fishery opens. Past the season length clamps to its last day.")]
        public int OpenDayOfSeason = 1;
        [Tooltip("Season of the last open day.")]
        public Season CloseSeason = Season.HighSummer;
        [Min(1)]
        [Tooltip("Day of that season (1-based) that is the last open day. Past the season length clamps to its last day.")]
        public int CloseDayOfSeason = 1;

        /// <summary>Whether this entry governs the species in the region (ordinal-ignore-case ids).</summary>
        public bool Covers(string speciesId, string regionId)
            => string.Equals(SpeciesId, speciesId, System.StringComparison.OrdinalIgnoreCase)
               && string.Equals(RegionId, regionId, System.StringComparison.OrdinalIgnoreCase);
    }
}
