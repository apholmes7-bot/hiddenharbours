using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where a mesh figure stands, as the one who stands it there already knows it.</b> The inputs a
    /// figure is posed from, published by whoever owns them: the player's <c>DeckRiderVisual</c>, a
    /// <c>MooredBoat</c>'s skipper. ADR 0044 d, moved into Core by the 2026-09-17 amendment so that the
    /// CAST can draw through the same seam as the player without Boats or World referencing Art (rule 4).
    ///
    /// <para><b>Everything here is read LIVE, every pose, and nothing is computed by the figure.</b> The
    /// stand point is the one the hull's deck-occupant slot is already fed from, and the bearing is the
    /// one the sprite's facing is already composed from. A figure that derived either for itself would be
    /// a second authority for where a person is standing, and then the mesh and its occluder would part
    /// company the first time one of them moved.</para>
    /// </summary>
    public interface ICharacterFigureStand
    {
        /// <summary>The sprite whose stance, gait and facing ARE the figure's inputs. Null = nothing to
        /// pose from.</summary>
        IsoCharacterSprite FigureCharacter { get; }

        /// <summary>The hull VISUAL transform the character is standing on — the object the hull's
        /// renderer is installed on — or null ashore. A mesh figure can only be drawn through a facet hull,
        /// so this is also where the figure is parented.</summary>
        Transform FigureHull { get; }

        /// <summary>Where the figure's feet are, in that hull's rig frame and metres
        /// <c>(deck x, deck y, deck height)</c> — exactly as published to the deck-occupant slot.</summary>
        Vector3 FigureStandRigMetres { get; }

        /// <summary>Where the figure is looking RELATIVE TO THE DECK (degrees; 0 = at the bow, +90 = to
        /// starboard), the <see cref="DeckRiderFacingMath"/> convention.</summary>
        float FigureDeckBearingDegrees { get; }
    }

    /// <summary>
    /// <b>The seam a mesh figure takes the draw through</b> (was the Player's <c>IDeckRiderFigure</c>). The
    /// owner of the sprite knows that something ELSE may be drawing the character this frame; it does not
    /// know what, and it must not — the sprite path is the one that has to stay byte-identical, so it
    /// learns exactly one fact (<see cref="DrawsInsteadOfSprite"/>) and the figure is handed exactly one
    /// call (<see cref="PoseFigure"/>) at the point where its inputs are already published.
    ///
    /// <para>With no figure installed every expression that mentions this interface collapses to the code
    /// that was there before it existed. That is the toggle-0 contract, and it is the reason the interface
    /// is this small.</para>
    /// </summary>
    public interface ICharacterFigure
    {
        /// <summary>True on the frames this figure is really on screen, so the sprite must not be. False
        /// the moment anything is missing — a skin, a hull, a clip — because two figures is a worse
        /// failure than the old one.</summary>
        bool DrawsInsteadOfSprite { get; }

        /// <summary>
        /// Pose from the stand's already-decided inputs, ONCE per frame, after they are published this
        /// frame. <paramref name="aboard"/> is the caller's word for whether the figure is standing on a
        /// hull — told rather than worked out, because a second opinion about it is a second bug.
        /// </summary>
        void PoseFigure(ICharacterFigureStand stand, bool aboard);
    }

    /// <summary>
    /// <b>Puts a mesh figure on a character that has a skin to draw</b> — the cast's way in. Implemented in
    /// Art (where the facet renderers live); called by Boats with nothing but Core types in hand.
    /// </summary>
    public interface ICharacterFigurePresentationService
    {
        /// <summary>
        /// Attach a figure to <paramref name="host"/> — the GameObject carrying the character's
        /// <see cref="SpriteRenderer"/> and <see cref="IsoCharacterSprite"/> — posed every frame from
        /// <paramref name="stand"/>. Returns null, and adds NOTHING, when there is no skin to draw: a
        /// character whose art def names no <see cref="CharacterSkinDef"/> keeps exactly the components it
        /// had. The live switch (<see cref="GameConfig"/>) is NOT consulted here — it is read every pose,
        /// so the owner can flip it with the figure already attached.
        /// </summary>
        ICharacterFigure Attach(GameObject host, ICharacterFigureStand stand);
    }

    /// <summary>
    /// The service locator for <see cref="ICharacterFigurePresentationService"/>. Deliberately NOT a
    /// <c>GameServices</c> member, for the reason <see cref="HullMeshPresentation"/> gives:
    /// <c>GameServices.Reset()</c> clears game-STATE services between tests and scenes, and this is
    /// stateless presentation wiring that must survive those resets. Art self-registers at runtime load;
    /// EditMode tests and editor tooling register explicitly. Consumers null-check — a null service means
    /// "no mesh figures here", and the sprite stands.
    /// </summary>
    public static class CharacterFigurePresentation
    {
        public static ICharacterFigurePresentationService Service { get; set; }
    }
}
