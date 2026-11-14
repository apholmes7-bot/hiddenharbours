using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One character trait</b> (traits slice 1, owner ruling 2026-11-14): a stable id, the words the
    /// player reads, and a list of small bonuses on existing seams. One asset per trait under
    /// <c>Data/Resources/Traits/</c> (rule 2); every number is the owner's to tune.
    ///
    /// <para><b>The ruling, in data.</b> A player picks <see cref="GameServices.TraitPicksPerCharacter"/>
    /// named traits (two). Bonuses are small and there are <b>no drawbacks</b>: the content validation
    /// test refuses a modifier that would make its stat worse. A trait only moves what its own player does
    /// (co-op: each player has their own list), and it is a fixed number read from the save, never a roll
    /// (rule 5).</para>
    ///
    /// <para>Read through <see cref="ITraitService"/>, never directly by a feature module: the module asks
    /// for its stat's combined multiplier and stays ignorant of which traits exist.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character/Trait", fileName = "Trait")]
    public sealed class TraitDef : ScriptableObject
    {
        [Tooltip("Stable id, `trait.snake_case` (trait.speedwalker). Append-only: a save names traits by " +
                 "this id, so it never changes once shipped.")]
        public string Id;

        [Tooltip("The name the player reads on the trait card.")]
        public string DisplayName;

        [Tooltip("One line the player reads under the name.")]
        [TextArea] public string Description;

        [Tooltip("The bonuses this trait gives. Each names one stat; several modifiers may share a stat.")]
        public TraitModifier[] Modifiers = new TraitModifier[0];
    }
}
