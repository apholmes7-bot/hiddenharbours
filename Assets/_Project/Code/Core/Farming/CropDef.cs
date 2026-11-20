using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One crop</b> (farm slice 1): when it goes in, when it comes out, how it grows and what it yields.
    /// One asset per crop under <c>Data/Resources/Farming/Crops/</c>, id <c>crop.snake_case</c> (rule 2);
    /// every number is the owner's to tune (rule 6).
    ///
    /// <para><b>Calendar.</b> Windows are written as <see cref="SeasonDay"/>s on the game's four 28-day
    /// seasons (time-tides-weather §2) and never wrap the year end. A crop is sown on one day inside
    /// <see cref="PlantFrom"/>..<see cref="PlantTo"/>, matures <see cref="DaysToMature"/> days later, and is
    /// only ever <see cref="FarmStage.Ready"/> inside <see cref="HarvestFrom"/>..<see cref="HarvestTo"/>.
    /// The content validation test proves every sowing day in the window can still be harvested in time.</para>
    ///
    /// <para><b>Read through <see cref="IFarmService"/></b>, and the state of a plot is a pure function of
    /// this, its <see cref="FarmPlotDef"/>, the world seed and the day (<see cref="FarmMath"/>, rule 5).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Farming/Crop", fileName = "Crop")]
    public sealed class CropDef : ScriptableObject
    {
        [Tooltip("Stable id, `crop.snake_case` (crop.potato). Append-only: a plot's rotation names it.")]
        public string Id;

        [Tooltip("The name the player reads.")]
        public string DisplayName;

        [Header("Calendar (inclusive, never wrapping the year end)")]
        [Tooltip("First day the crop may be sown.")]
        public SeasonDay PlantFrom = new SeasonDay(Season.EarlySpring, 1);
        [Tooltip("Last day the crop may be sown.")]
        public SeasonDay PlantTo = new SeasonDay(Season.EarlySpring, 28);
        [Tooltip("First day a mature crop counts as ready. Mature before this, it waits as Growing.")]
        public SeasonDay HarvestFrom = new SeasonDay(Season.TheTurn, 1);
        [Tooltip("Last day of the harvest window. A tender always lifts it by then.")]
        public SeasonDay HarvestTo = new SeasonDay(Season.TheTurn, 28);

        [Header("Growth")]
        [Tooltip("Game days from sowing to mature.")]
        [Min(1)] public int DaysToMature = 30;
        [Tooltip("Game days the ground lies tilled before sowing.")]
        [Min(1)] public int TillLeadDays = 2;
        [Tooltip("A tender weeds or hills the rows every this-many days between sowing and readiness.")]
        [Min(1)] public int TendEveryDays = 4;
        [Tooltip("Up to this many extra days a ready crop may stand before the tender lifts it (seeded per " +
                 "plot and year). It always stands ready for at least one day.")]
        [Min(0)] public int HarvestSpreadDays = 3;
        [Tooltip("Growth (0..1 of the way to mature) at which the first green shows: Planted to Sprouting.")]
        [Range(0f, 1f)] public float SproutAt = 0.15f;
        [Tooltip("Growth at which the rows read as a growing crop: Sprouting to Growing. Above SproutAt.")]
        [Range(0f, 1f)] public float GrowingAt = 0.4f;
        [Tooltip("Visual only: how far (0..1 of growth) one cell may run ahead of or behind the plot, so the " +
                 "rows do not look stamped. The plot's stage never changes with it.")]
        [Range(0f, 0.3f)] public float CellVariation = 0.08f;

        [Header("Yield")]
        [Tooltip("Produce items one plot cell yields at harvest.")]
        [Min(0)] public int YieldPerCell = 1;
        [Tooltip("The produce item this crop yields: a ProduceDef id (`produce.snake_case`).")]
        public string ProduceId;
    }
}
