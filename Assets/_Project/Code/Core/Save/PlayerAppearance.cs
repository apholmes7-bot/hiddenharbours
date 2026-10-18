using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The player's look over the save</b> — <see cref="IPlayerAppearance"/>'s implementation
    /// (character PR 2b, ADR 0051), the <see cref="OutfitLocker"/> shape with a builder in front of
    /// the write: a recipe the builder refuses is never shown and never saved.
    ///
    /// <para><b>The preview lives here, not in the save.</b> It is a field of this object, published
    /// and forgotten; <see cref="SaveData.PlayerRecipe"/> changes on <see cref="Commit"/> alone, and
    /// only when the recipe differs from what it holds, so re-keeping the look already worn writes
    /// no file and publishes no change.</para>
    ///
    /// <para>Both seams are injectable, so the tests drive it on a temporary save path with a builder
    /// of their own: <paramref name="save"/> defaults to <see cref="GameServices.Save"/> (read at each
    /// call, since it self-installs) and the refusal to the shipped builder under Resources.</para>
    /// </summary>
    public sealed class PlayerAppearance : IPlayerAppearance
    {
        private readonly Func<ISaveService> _save;
        private readonly Func<CharacterRecipe, string> _refuse;
        private CharacterRecipe _preview;

        public PlayerAppearance(Func<ISaveService> save = null, Func<CharacterRecipe, string> refuse = null)
        {
            _save = save ?? (() => GameServices.Save);
            _refuse = refuse ?? RefuseWithShippedBuilder;
        }

        public CharacterRecipe Committed => CommittedOf(_save()?.Current);

        public CharacterRecipe Showing => _preview != null ? _preview.Clone() : Committed;

        public bool IsPreviewing => _preview != null;

        public bool Preview(CharacterRecipe recipe, out string refusal)
        {
            refusal = Check(recipe);
            if (refusal != null) return false;
            _preview = recipe.Clone();
            EventBus.Publish(new PlayerAppearancePreviewRequested(_preview));
            return true;
        }

        public void CancelPreview()
        {
            if (_preview == null) return;
            _preview = null;
            EventBus.Publish(new PlayerAppearancePreviewRequested(Committed));
        }

        public bool Commit(CharacterRecipe recipe, out string refusal)
        {
            ISaveService save = _save();
            if (save?.Current == null)
            {
                refusal = "No save is loaded, so there is nowhere to keep the look.";
                return false;
            }
            refusal = Check(recipe);
            if (refusal != null) return false;

            bool previewing = _preview != null;
            _preview = null;
            CharacterRecipe held = CommittedOf(save.Current);
            if (held.SameAs(recipe))
            {
                // Nothing to write. A preview that showed something else is put back.
                if (previewing) EventBus.Publish(new PlayerAppearancePreviewRequested(held));
                return true;
            }

            CharacterRecipe kept = recipe.Clone();
            kept.SchemaVersion = CharacterRecipe.Version;
            kept.Heal();
            save.Current.PlayerRecipe = kept;
            save.Save();
            EventBus.Publish(new PlayerAppearanceChanged(kept));
            return true;
        }

        /// <summary>A copy of <paramref name="data"/>'s recipe; empty for no save or no recipe.</summary>
        public static CharacterRecipe CommittedOf(SaveData data) =>
            data?.PlayerRecipe != null ? data.PlayerRecipe.Clone() : new CharacterRecipe();

        /// <summary>Null if the shipped builder builds <paramref name="recipe"/> for the player.</summary>
        public static string RefuseWithShippedBuilder(CharacterRecipe recipe) =>
            CharacterBuilder.Refuse(CharacterBuilderDef.LoadShipped(), recipe, CharacterAudience.Player);

        private string Check(CharacterRecipe recipe)
        {
            if (recipe == null) return "No recipe.";
            // The empty recipe is the body as baked: always buildable, whatever the tables say. A fit
            // named on its own is still checked, so a save never keeps a fit the builder would refuse.
            return recipe.IsEmpty && string.IsNullOrEmpty(recipe.FitId) ? null : _refuse(recipe);
        }
    }
}
