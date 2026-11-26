using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One recipe field and the value a garment sets it to (<c>outfit</c> = <c>oil</c>).</summary>
    [Serializable]
    public struct ClothingLookKey
    {
        [Tooltip("A CharacterRecipe field name (outfit, shirt, hat, garment...).")]
        public string Field;
        [Tooltip("The rig's own key for that field (oil, navy, souwester...).")]
        public string Value;

        public ClothingLookKey(string field, string value)
        {
            Field = field;
            Value = value;
        }
    }

    /// <summary>
    /// <b>One garment the player can buy, own and wear</b> (store clothing, pass 1). Content as data: one
    /// asset per file under <c>Data/Resources/Clothing</c>, keyed by a stable, append-only
    /// <see cref="Id"/> (<c>clothing.snake_case</c>).
    ///
    /// <para><b>Two looks, kept apart.</b> <see cref="StandIn"/> is what the try-on page shows TODAY: colour
    /// keys only, which the builder paints on the baked body at runtime (ADR 0051). <see cref="RigLook"/> is
    /// what the garment IS on the rig (a structural key such as <c>hat = souwester</c>): structure is baked,
    /// so that look waits for the art pass and nothing reads it yet. A garment with no stand-in shows him
    /// unchanged and says its picture is to come.</para>
    ///
    /// <para><b>Owned and worn are per player</b> (<see cref="PlayerWardrobe"/>); this Def holds nothing
    /// about who has it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character/Clothing", fileName = "Clothing")]
    public sealed class ClothingDef : ScriptableObject
    {
        /// <summary>The id prefix every garment carries.</summary>
        public const string IdPrefix = "clothing.";

        [Tooltip("Stable id, `clothing.snake_case`. Append-only: once shipped it never changes.")]
        public string Id = "clothing.example";

        [Tooltip("The name the player reads on the rack.")]
        public string DisplayName = "";

        [Tooltip("Where it is worn. One garment per slot.")]
        public ClothingSlot Slot = ClothingSlot.Top;

        [Tooltip("One plain line the player reads: what it does. Say 'just for looks' when it does nothing.")]
        [TextArea] public string Does = "";

        [Tooltip("What one costs at the rack (whole coins). PROPOSAL numbers until the owner tunes them.")]
        [Min(0)] public int Price;

        [Tooltip("What it does, each naming one system already on main. Empty = just for looks.")]
        public ClothingEffect[] Effects = Array.Empty<ClothingEffect>();

        [Tooltip("Colour keys the try-on page paints on him today, layered head to hands (outerwear last).")]
        public ClothingLookKey[] StandIn = Array.Empty<ClothingLookKey>();

        [Tooltip("What the garment is on the character rig (structural keys). Read by the art pass, not the game.")]
        public ClothingLookKey[] RigLook = Array.Empty<ClothingLookKey>();

        /// <summary>True when the garment changes nothing but how he looks.</summary>
        public bool LookOnly => Effects == null || Effects.Length == 0;
    }
}
