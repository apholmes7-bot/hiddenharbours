using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Art's side of the cast figure seam</b> (ADR 0044, amendment 2026-09-17) — the
    /// <see cref="ICharacterFigurePresentationService"/> that puts a <see cref="CharacterFigurePresenter"/>
    /// on a character whose art def names a skin. Boats calls it through
    /// <see cref="CharacterFigurePresentation.Service"/> with nothing but Core types in hand (rule 4).
    ///
    /// <para><b>Self-registering at runtime</b>, before the first scene — the
    /// <see cref="IsoFacetHullPresentationService"/> pattern — so a player build and PlayMode get the cast
    /// figures with no wiring and no scene edit. EditMode tests and editor tooling call
    /// <see cref="EnsureRegistered"/> explicitly; edit-time scene builders deliberately do not, so no
    /// built scene ever serialises a figure presenter: it is attached live, per run, when the character is
    /// stood.</para>
    /// </summary>
    public sealed class CharacterFigurePresentationService : ICharacterFigurePresentationService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAtLoad() => EnsureRegistered();

        /// <summary>Idempotent registration. Never replaces a live service (a test double stays).</summary>
        public static void EnsureRegistered()
        {
            CharacterFigurePresentation.Service ??= new CharacterFigurePresentationService();
        }

        /// <inheritdoc/>
        public ICharacterFigure Attach(GameObject host, ICharacterFigureStand stand)
        {
            if (host == null || stand == null) return null;
            if (stand is Object standObject && standObject == null) return null;

            // No skin, no component: a character with nothing to draw as a mesh keeps exactly the
            // components it had, which is what makes "the sprite, exactly as today" checkable.
            IsoCharacterSprite character = stand.FigureCharacter;
            CharacterVisualDef visual = character != null ? character.Visual : null;
            if (visual == null || visual.Skin == null) return null;

            var presenter = host.GetComponent<CharacterFigurePresenter>();
            if (presenter == null) presenter = host.AddComponent<CharacterFigurePresenter>();
            presenter.Configure(stand);
            return presenter;
        }
    }
}
