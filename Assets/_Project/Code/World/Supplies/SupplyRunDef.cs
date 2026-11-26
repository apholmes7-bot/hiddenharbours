using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Gathering what a bought boat needs, from people.</b> The root of one supply run: which boat
    /// (<see cref="BoatId"/>), who tells the player what she wants (<see cref="TellerNpcId"/>) and the flag
    /// that records he has (<see cref="NamedFlag"/>), the supplies and their givers (<see cref="Gifts"/>),
    /// and the question to ask around (<see cref="Topic"/>).
    ///
    /// <para><b>Live only while she is his, on the slip and unmended.</b> Before he buys her and after she
    /// is mended, nothing here speaks and no row appears. Loaded from
    /// <c>Resources/<see cref="ResourcesFolder"/></c>, so no scene has to carry it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Supplies/Supply Run", fileName = "SupplyRun")]
    public sealed class SupplyRunDef : ScriptableObject
    {
        /// <summary>Where the runs are loaded from (a folder under a Resources folder).</summary>
        public const string ResourcesFolder = "NpcSupplies";

        [Tooltip("Stable, append-only id (supplyrun.snake_case).")]
        public string Id = "supplyrun.example";

        [Tooltip("The boat this run mends (boat.snake_case). Live while she is owned, on the slip and not repaired.")]
        public string BoatId = "boat.dory";

        [Tooltip("Who tells the player what she wants (npc.snake_case).")]
        public string TellerNpcId = "";

        [Tooltip("Stable, append-only save-flag key, set once the teller has named the supplies. The rows " +
                 "and the clue question appear only after it.")]
        public string NamedFlag = "";

        [Tooltip("Appended to the teller's lines the first time, while anything is missing. Names the supplies.")]
        [TextArea] public string[] NamingLines = new string[0];

        [Tooltip("Appended to the teller's lines after that, while anything is still missing.")]
        [TextArea] public string[] MissingLines = new string[0];

        [Tooltip("Appended to the teller's lines once every supply is in hand.")]
        [TextArea] public string[] AllInHandLines = new string[0];

        [Tooltip("The supplies and who gives them.")]
        public SupplyGiftDef[] Gifts = new SupplyGiftDef[0];

        [Tooltip("The question asked around to find the givers.")]
        public ClueTopicDef Topic;
    }
}
