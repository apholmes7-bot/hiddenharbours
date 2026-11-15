using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One farm plot</b> (farm slice 1): a dedicated piece of ground in a region, the crop it carries each
    /// year, and who works it. One asset per plot under <c>Data/Resources/Farming/Plots/</c>, id
    /// <c>plot.snake_case</c> (rule 2).
    ///
    /// <para><b>Placement is the next slice.</b> <see cref="Area"/> stays empty and
    /// <see cref="AreaIsPlaceholder"/> stays set until the owner picks the spot; <see cref="PlacementNote"/>
    /// says where it is meant to go. Nothing draws a plot in slice 1, and no scene is touched.</para>
    ///
    /// <para><b>HOOK, player farming (a later slice).</b> <see cref="Access"/>,
    /// <see cref="ApproverNpcId"/> and <see cref="PurchasePrice"/> say how the player may come to work it.
    /// A player-worked plot keeps the days its work was actually done in the save and builds a
    /// <see cref="FarmPlotSchedule"/> from them; <see cref="FarmMath.StateOn"/> reads that schedule exactly
    /// as it reads a tender's seeded one.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Farming/Farm Plot", fileName = "FarmPlot")]
    public sealed class FarmPlotDef : ScriptableObject
    {
        [Tooltip("Stable id, `plot.snake_case` (plot.ginny_kitchen). Append-only: it also seeds the plot's " +
                 "sowing and harvest days, so renaming it would move them.")]
        public string Id;

        [Tooltip("The name the player reads.")]
        public string DisplayName;

        [Header("Where")]
        [Tooltip("The region the plot lies in (`region.snake_case`).")]
        public string RegionId;
        [Tooltip("The plot's rectangle in region space (metres). Empty until the placement slice.")]
        public Rect Area;
        [Tooltip("Set while Area is not yet chosen. The placement slice clears it.")]
        public bool AreaIsPlaceholder = true;
        [Tooltip("Where the plot is meant to go, in words, until it is placed.")]
        [TextArea] public string PlacementNote;
        [Tooltip("The plot's size in cells (columns, rows). A cell is one unit of yield and, later, one bed.")]
        public Vector2Int Cells = new Vector2Int(4, 3);

        [Header("Rotation")]
        [Tooltip("Crop ids by year, repeating: year 1 sows the first, year 2 the second, and so on. An empty " +
                 "entry is a rest year (the ground stays fallow).")]
        public string[] CropRotation = new string[0];

        [Header("Who works it")]
        [Tooltip("The NPCs who tend the plot (`npc.snake_case`).")]
        public string[] TenderNpcIds = new string[0];
        [Tooltip("Set while the tenders are not yet real NpcDefs (a farmer still to be cast). The content " +
                 "validation test then lists them instead of failing.")]
        public bool TendersArePlaceholder;
        [Tooltip("How the player may come to work the plot. Nothing reads this yet.")]
        public FarmPlotAccess Access = FarmPlotAccess.TendersOnly;
        [Tooltip("SideJobByApproval: the NPC who agrees to the side job.")]
        public string ApproverNpcId;
        [Tooltip("ForPurchase: the price, in the game's currency.")]
        [Min(0)] public int PurchasePrice;

        /// <summary>The number of cells, never negative.</summary>
        public int CellCount => Mathf.Max(0, Cells.x) * Mathf.Max(0, Cells.y);
    }
}
