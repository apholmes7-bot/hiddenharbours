using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The St Peters supply route and store policy</b> (island supply slice S1): the bar the runs cross,
    /// the legs either side of it, the working day a run may use, and the store's buffer. One asset,
    /// <c>Data/Resources/IslandSupply/IslandSupplyConfig.asset</c>.
    ///
    /// <para>The bar geometry mirrors the terrain the builder lays (<c>StPetersBuilder.SandbarCrestElevation</c>,
    /// <c>ChannelBedElevation</c>, <c>ChannelAlong</c>, <c>ChannelHalfWidth</c>); an EditMode test fails if the
    /// two drift. The tide itself is NOT copied here: the windows come from the region's own tide profile
    /// (<see cref="IslandRegionId"/>) through the real tide model.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Island Supply/Config", fileName = "IslandSupplyConfig")]
    public sealed class IslandSupplyConfig : ScriptableObject
    {
        /// <summary>The Resources path this config loads from.</summary>
        public const string ResourcesPath = "IslandSupply/IslandSupplyConfig";

        [Header("Route")]
        [Tooltip("The region whose tide opens and closes the bar.")]
        public string IslandRegionId = "region.st_peters";

        [Tooltip("Bar head to the NMC landing, metres (scene-sizing doc §5.2: the honest walk, no door skip).")]
        [Min(1f)] public float BarLengthMetres = 611f;

        [Tooltip("Sandbar crest elevation, metres above datum. Dry while the water is below it.")]
        public float BarCrestElevationMetres = 0.88f;

        [Tooltip("The gut's bed elevation, metres above datum (negative: below datum).")]
        public float GutBedElevationMetres = -0.6f;

        [Tooltip("Distance from the island's bar head to the middle of the gut, metres.")]
        [Min(0f)] public float GutCentreFromIslandMetres = 189f;

        [Tooltip("Width of the gut along the walk line, metres.")]
        [Min(0f)] public float GutWidthMetres = 30f;

        [Tooltip("Village green to the bar head, metres (island side, never flooded).")]
        [Min(0f)] public float IslandPathMetres = 52f;

        [Tooltip("NMC landing to the errand stop (wharf market and pumps), metres (mainland side, never flooded).")]
        [Min(0f)] public float MainlandRoadMetres = 372f;

        [Header("Working day")]
        [Tooltip("Earliest hour a supply run may leave.")]
        [Range(0f, 24f)] public float RunDayStartHour = 6f;

        [Tooltip("Latest hour a supply run may be back on the island.")]
        [Range(0f, 24f)] public float RunDayEndHour = 20f;

        [Header("Store")]
        [Tooltip("Days of bar demand the island store keeps in its back room against a closed bar.")]
        [Min(0f)] public float BufferDays = 4f;

        [Header("Search")]
        [Tooltip("Resolution of the window and departure search, in game minutes.")]
        [Min(0.01f)] public float SearchStepGameMinutes = 1f;
    }
}
