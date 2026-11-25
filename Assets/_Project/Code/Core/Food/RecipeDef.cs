using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One recipe</b>: at a station, one input becomes one output after some work. One asset per recipe
    /// under <c>Data/Resources/Recipes/</c> (rule 2); every number is the owner's to tune (rule 6).
    ///
    /// <para><b>The ruling, in data</b> (owner, 2026-11-14): kitchenettes cook raw food, but food is first
    /// cut/prepared at <see cref="RecipeStations.Prep"/>, then cooked at <see cref="RecipeStations.Cooktop"/>.
    /// So a prep recipe takes a raw catch (<see cref="RecipeInputKind.Species"/> or
    /// <see cref="RecipeInputKind.Category"/>) and a cooktop recipe takes a prep output
    /// (<see cref="RecipeInputKind.Intermediate"/>); a microwave recipe takes a stock item
    /// (<see cref="RecipeInputKind.Item"/>); the content validation test refuses anything else.</para>
    ///
    /// <para><b>Reuse.</b> This is the single-input slice of the planned production recipe
    /// (<c>design/economy-and-business.md</c> §9.1: inputs → facility → time → outputs). <see cref="Station"/>
    /// is an open string id so the plant and salt-house recipes are new assets, not new code; quantities and
    /// several inputs are fields added later, append-only. Read through <see cref="RecipeBook"/>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Food/Recipe", fileName = "Recipe")]
    public sealed class RecipeDef : ScriptableObject
    {
        [Tooltip("Stable id, `recipe.snake_case` (recipe.cod_fillet). Append-only: never changes once shipped.")]
        public string Id;

        [Tooltip("The name the player reads.")]
        public string DisplayName;

        [Tooltip("The station that does this work, a stable id (`station.prep`, `station.cooktop`). Open set: " +
                 "a new station is a new id, no code.")]
        public string Station;

        [Tooltip("What kind of input this recipe takes.")]
        public RecipeInputKind InputKind;

        [Tooltip("Species: the `fish.*` id. Intermediate: the output id of the recipe that feeds this one. " +
                 "Item: a stock item id (a food item id or a supply id). Unused for Category.")]
        public string InputId;

        [Tooltip("Category: the fish category taken. Unused otherwise.")]
        public FishCategory InputCategory;

        [Tooltip("What comes out, a stable id (`food.snake_case`). A later recipe names it as its input.")]
        public string OutputId;

        [Tooltip("Seconds of work at the station (game-time seconds).")]
        [Min(0f)] public float WorkSeconds;

        [Tooltip("Share of one meal (0..1) eating the output relieves. Only cooked outputs feed you; 0 on " +
                 "every prep recipe.")]
        [Range(0f, 1f)] public float HungerRelief;
    }
}
