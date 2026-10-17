namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The player's look, as a seam</b> (character PR 2b, ADR 0051, rule 4): the one place a
    /// picker asks to SHOW a <see cref="CharacterRecipe"/> or to KEEP one. Core states the change on
    /// the <see cref="EventBus"/> (<see cref="PlayerAppearancePreviewRequested"/>,
    /// <see cref="PlayerAppearanceChanged"/>) and whoever draws the player answers it: the picker never
    /// touches a material or a shader array, and the drawing side never reads the save.
    ///
    /// <para><b>Preview and commit are separate.</b> A preview is shown and never written; only
    /// <see cref="Commit"/> reaches the save, and it ends any preview. Cancelling re-publishes the
    /// committed recipe as a preview, so a preview ends the way it began (the
    /// <see cref="OutfitPreviewRequested"/> shape).</para>
    ///
    /// <para><b>The player only.</b> Nothing here carries a figure key (ADR 0044 §9.3): the cast keeps
    /// its baked defs, and its presentation key is never saved.</para>
    /// </summary>
    public interface IPlayerAppearance
    {
        /// <summary>A copy of the recipe on the save. Empty when the player is as baked, and when no
        /// save is loaded.</summary>
        CharacterRecipe Committed { get; }

        /// <summary>A copy of what is on screen: the open preview, else <see cref="Committed"/>.</summary>
        CharacterRecipe Showing { get; }

        bool IsPreviewing { get; }

        /// <summary>Show <paramref name="recipe"/> without keeping it. False, with the builder's
        /// refusal, for a recipe the builder does not build for the player; nothing is shown then.</summary>
        bool Preview(CharacterRecipe recipe, out string refusal);

        /// <summary>End an open preview: the committed recipe is shown again. Nothing is written.</summary>
        void CancelPreview();

        /// <summary>Keep <paramref name="recipe"/>: write it to the save, persist it and announce it,
        /// only when it changes what the save holds. Ends any preview. False, with the refusal, for a
        /// recipe the builder refuses or when no save is loaded; nothing is written then.</summary>
        bool Commit(CharacterRecipe recipe, out string refusal);
    }
}
