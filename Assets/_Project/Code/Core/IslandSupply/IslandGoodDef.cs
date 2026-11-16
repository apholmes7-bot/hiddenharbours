using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One good St Peters uses and does not make</b> (island supply slice S1; owner 2026-11-15: "Note which
    /// items must be sourced off St. Peter's island from NMC to keep the island running"). One asset per good
    /// under <c>Data/Resources/IslandSupply/Goods/</c>, id <c>islandgood.snake_case</c> (rule 2).
    ///
    /// <para>The daily figures are for the WHOLE island (18 residents, the store, school, post office and
    /// cannery; <c>docs/design/island-supply.md</c>), as carried: mass and volume include the bag, can or
    /// cylinder the good travels in. They are the owner's to tune. <see cref="SupplyPlanMath"/> adds them
    /// up; nothing consumes, stocks or sells a good yet (S7).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Island Supply/Good", fileName = "IslandGood")]
    public sealed class IslandGoodDef : ScriptableObject
    {
        [Tooltip("Stable id, `islandgood.snake_case`. Append-only.")]
        public string Id;

        [Tooltip("The name the player reads.")]
        public string DisplayName;

        [Tooltip("Where it comes from. Only MainlandOverBar goods load the bar runs.")]
        public GoodSource Source = GoodSource.MainlandOverBar;

        [Tooltip("The unit DailyUnits is counted in (kg, L, cylinder...). A label for people; the maths reads " +
                 "DailyMassKg and DailyVolumeLitres.")]
        public string Unit = "kg";

        [Tooltip("Average island use per game day, in Unit.")]
        [Min(0f)] public float DailyUnits;

        [Tooltip("Mass carried per game day for the island's own use, kg, packaging included.")]
        [Min(0f)] public float DailyMassKg;

        [Tooltip("Volume carried per game day for the island's own use, litres, packaging included.")]
        [Min(0f)] public float DailyVolumeLitres;

        [Tooltip("Extra on top of island use for the player to buy off the store shelf, as a share of the " +
                 "island use (0.25 = +25%). 0 when the player does not buy it here.")]
        [Range(0f, 1f)] public float PlayerAllowanceFraction;

        [Tooltip("Factor on the daily figures in winter (stove oil doubles). 1 = no change.")]
        [Min(0f)] public float WinterMultiplier = 1f;

        [Tooltip("What it travels in, in the real world (bag, jerry can, 20 lb cylinder...).")]
        public string Container;

        [Tooltip("Where the numbers came from, so the owner can judge them.")]
        [TextArea] public string Basis;
    }
}
