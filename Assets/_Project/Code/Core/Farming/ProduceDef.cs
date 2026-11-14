using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One farm produce item</b> (farm slice 1; owner ruling 2026-11-14, relayed: "each crop is a real
    /// item, this food can be stored, prepped and eaten, some items can be eaten raw with less of a
    /// hunger/energy advantage, prepping them increases value and energy"). One asset per item under
    /// <c>Data/Resources/Farming/Produce/</c>, id <c>produce.snake_case</c> (rule 2). A
    /// <see cref="CropDef"/> names the item it yields by <see cref="Id"/>.
    ///
    /// <para><b>Hunger is on the recipe scale.</b> Relief is a share of one meal (0..1), the same scale as
    /// the draft recipe contract's <c>HungerRelief</c> (PR 1209, not depended on here). Raw is always less
    /// than prepped: the content validation test refuses a raw figure at or above the prepped one.</para>
    ///
    /// <para><b>HOOK, storage and eating (a later slice).</b> Nothing stores, carries, sells or eats this yet,
    /// and nothing is saved. A later slice joins here: a stack of <see cref="Id"/> in a container or the
    /// hands (the save names it by id), the eat action reading <see cref="RawHungerRelief"/> when
    /// <see cref="EatenRaw"/> is set, and a recipe taking <see cref="Id"/> as its input (a new append-only
    /// input kind beside the fish ones) whose cooked output carries the larger relief and value.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Farming/Produce", fileName = "Produce")]
    public sealed class ProduceDef : ScriptableObject
    {
        [Tooltip("Stable id, `produce.snake_case` (produce.potato). Append-only: a crop, a later save and a " +
                 "later recipe name this item by it.")]
        public string Id;

        [Tooltip("The name the player reads.")]
        public string DisplayName;

        [Tooltip("What one item fetches raw at a counter, in the game's currency. The economy's to tune; a " +
                 "prepped dish is worth more and carries its own value on its recipe.")]
        [Min(0)] public int BaseValue = 1;

        [Tooltip("Can this be eaten straight from the ground? (A carrot, yes; a potato, no.)")]
        public bool EatenRaw;

        [Tooltip("Share of one meal (0..1) eating ONE raw item relieves. 0 when it is not eaten raw. Always " +
                 "less than PreppedHungerRelief.")]
        [Range(0f, 1f)] public float RawHungerRelief;

        [Tooltip("Share of one meal (0..1) the prepped or cooked item is meant to relieve: the bar the raw " +
                 "figure must stay under, and the target a later recipe for this item starts from.")]
        [Range(0f, 1f)] public float PreppedHungerRelief;
    }
}
