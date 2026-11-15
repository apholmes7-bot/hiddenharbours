using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One thing the player can eat, and what it becomes in the galley</b> (first food slice; owner
    /// rulings 2026-11-14: hunger slows, never stops, and is read diegetically, never off a bar; food is
    /// prepped at a prep station, then cooked on a cooktop). One asset per food under
    /// <c>Data/Resources/Food/</c>, id <c>food.snake_case</c> (rule 2); every number is the owner's (rule 6).
    ///
    /// <para><b>A food describes an item, it is not the item.</b> <see cref="ItemId"/> names the thing
    /// held: a <c>supply.*</c> bought over a counter (counted in <see cref="SupplyLocker"/>), a
    /// <c>produce.*</c> from a farm plot, a <c>fish.*</c> catch in the hand, or a <c>food.*</c> recipe output.
    /// So the economy keeps its own Defs and prices, and the eat action asks this one question only:
    /// can it be eaten as it is, and how much of a meal is it.</para>
    ///
    /// <para><b>The relief scale</b> is the one <see cref="INeedsService.Eat"/>, <see cref="RecipeDef"/> and
    /// <see cref="ProduceDef"/> already use: a share of one full meal, 0..1. For a farm produce item the
    /// content validation test pins <see cref="HungerRelief"/> and <see cref="EdibleRaw"/> to the produce
    /// asset's own raw figures, so the two cannot drift apart.</para>
    ///
    /// <para><b>The galley chain is ids only.</b> <see cref="PreppedId"/> and <see cref="CookedId"/> name
    /// recipe outputs (<see cref="RecipeDef.OutputId"/>); the stations that run them are a later slice.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Food/Food", fileName = "Food")]
    public sealed class FoodDef : ScriptableObject
    {
        [Tooltip("Stable id, `food.snake_case` (food.bread). Append-only: never changes once shipped.")]
        public string Id;

        [Tooltip("The item this describes, by its own stable id: `supply.*` (shop food in the supply locker), " +
                 "`produce.*` (a farm item), `fish.*` (a catch in the hand) or `food.*` (a recipe output). One " +
                 "food per item.")]
        public string ItemId;

        [Tooltip("Can it be eaten as it is held, with no galley work? (Bread and a carrot, yes; a raw cod, no.)")]
        public bool EdibleRaw;

        [Tooltip("Share of one meal (0..1) eating ONE unit as it is relieves. Above 0 when edible raw; 0 when not.")]
        [Range(0f, 1f)] public float HungerRelief;

        [Tooltip("What the prep station makes of it: a prep recipe's output id (`food.cod_fillet`). Empty when " +
                 "it needs no prep.")]
        public string PreppedId;

        [Tooltip("What the cooktop makes of it: a cooktop recipe's output id (`food.pan_fried_cod`). Required " +
                 "when it is not edible raw; empty when it is never cooked.")]
        public string CookedId;
    }
}
