using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>A place a villager fishes from the shore</b>, as data (ADR 0003): which region's waters, with what
    /// gear, and which species may bite there, by stable id. Read by <see cref="ShoreCatchFacts"/>, which rolls
    /// a villager angler's catch through the same <see cref="CatchResolver"/> the player's catch uses, so a
    /// fish out of season for the player is out of season for him (ADR 0053 §11). Presentation only: no fish
    /// is taken from any stock and nothing is saved.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Shore Spot", fileName = "ShoreSpot")]
    public sealed class ShoreSpotDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (spot.region.place, e.g. spot.st_peters.wharf_head). An NPC angling " +
                 "activity names its spot by this id.")]
        public string Id = "spot.example";

        [Tooltip("The region whose waters these are (region.snake_case). A species restricted to other regions " +
                 "never bites here.")]
        public string RegionId = "";

        [Tooltip("What he fishes with. A species that cannot be caught on this gear never bites here.")]
        public Gear Gear = Gear.Handline;

        [Tooltip("The species that may bite here, by fish id (fish.snake_case). Each is still filtered by its own " +
                 "season, hours and tide, exactly as the player's catch is.")]
        public string[] SpeciesIds = Array.Empty<string>();

        [Tooltip("The chance a session gets a bite at all when something is in season (0..1). The rest of the " +
                 "time he casts, waits and reels in empty.")]
        [Range(0f, 1f)] public float BiteChance01 = 0.7f;

        [Header("Where he stands to fish it")]
        [Tooltip("On: an angler fishing this spot stands at Stand Position facing Stand Heading, not at his " +
                 "routine station, and everything he casts or draws in keeps to the water side of the edge line.")]
        public bool HasStand;

        [Tooltip("The plank he stands on (world metres, the region scene's frame). Must be walkable.")]
        public Vector2 StandPosition;

        [Tooltip("The compass heading he faces (0 = North, clockwise): straight out over open water, down a " +
                 "lane with no moored boat or piling in it.")]
        public float StandHeadingDegrees;

        [Tooltip("A point on the deck's edge line he fishes over (world metres).")]
        public Vector2 EdgePoint;

        [Tooltip("The compass bearing pointing from the edge out over the water (0 = North, clockwise).")]
        public float EdgeOutwardDegrees;

        [Tooltip("How far beyond the edge line a cast, a fish or a splash is kept (m). Wide enough that the " +
                 "fish's sprite never laps the planks.")]
        [Min(0f)] public float EdgeMarginM = 0.4f;

        /// <summary>The authored stand, or false when the spot has none.</summary>
        public bool TryGetStand(out HiddenHarbours.Core.ShoreStand stand)
        {
            if (!HasStand) { stand = default; return false; }
            stand = new HiddenHarbours.Core.ShoreStand(StandPosition, StandHeadingDegrees,
                HiddenHarbours.Core.ShoreEdge.FromCompass(EdgePoint, EdgeOutwardDegrees, EdgeMarginM));
            return true;
        }
    }
}
