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
        /// renderer is installed on — or null ashore. Aboard, the presenter behind this seam draws a figure
        /// only through a facet hull, so this is also where it parents her; ashore, only a stand that is an
        /// <see cref="ICharacterFigureAshoreStand"/> is drawn, through a facet id of her own (ADR 0044 §7.3,
        /// amendment 2026-09-27). This stand promises no hull.</summary>
        Transform FigureHull { get; }

        /// <summary>Where the figure's feet are, in that hull's rig frame and metres
        /// <c>(deck x, deck y, deck height)</c> — exactly as published to the deck-occupant slot.</summary>
        Vector3 FigureStandRigMetres { get; }

        /// <summary>Where the figure is looking RELATIVE TO THE DECK (degrees; 0 = at the bow, +90 = to
        /// starboard), the <see cref="DeckRiderFacingMath"/> convention.</summary>
        float FigureDeckBearingDegrees { get; }
    }

    /// <summary>
    /// <b>Who a figure is: its ONE key</b> (ADR 0044, amendments 2026-09-27 and 2026-09-28). A stand that
    /// can say who its character is answers this, and nothing invents a second key for the same figure.
    ///
    /// <para>The presenter reads the key once, when the figure is attached, and hashes it once. That one
    /// hash moves a villager's idle phase off her neighbours' ashore, so a village does not breathe in
    /// step, and seeds every figure's blink, so a crowd does not blink together. The key itself names the
    /// figure to the look seam (<see cref="CharacterLookTargets"/>).</para>
    /// </summary>
    public interface ICharacterFigureIdentity
    {
        /// <summary>
        /// Who she is, as a stable string: a villager's <c>NpcDef</c> id (<c>npc.aunt_ginny</c>), a moored
        /// skipper's <c>BoatOwnerDef</c> id. Both are append-only, so the same person gives the same key on
        /// every run and machine. It is PRESENTATION: it is never saved, and no simulation reads it (rule 5).
        /// Null or empty = nothing of her own: the world's idle phase, and a blink seeded by her skin alone.
        /// </summary>
        string FigureKey { get; }
    }

    /// <summary>
    /// <b>A stand on the GROUND: a villager on her own feet</b> (ADR 0044, amendment 2026-09-27). A second
    /// interface rather than a member on <see cref="ICharacterFigureStand"/>, so every stand and test double
    /// written before it compiles unchanged. A stand that is one of these and publishes no hull is drawn
    /// ashore, under a facet id of her own; any other stand with no hull keeps its sprite, as before.
    ///
    /// <para>Ashore the hull members are never read: <see cref="ICharacterFigureStand.FigureHull"/> is
    /// null, and the stand point and deck bearing answer zero. Her facing is her sprite's own
    /// <see cref="IsoCharacterSprite.HeadingDegrees"/>. Who she is, her <c>NpcDef</c> id, is her
    /// <see cref="ICharacterFigureIdentity.FigureKey"/>.</para>
    /// </summary>
    public interface ICharacterFigureAshoreStand : ICharacterFigureStand, ICharacterFigureIdentity
    {
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
    /// Art (where the facet renderers live); called by Boats (a moored skipper) and World (a villager) with
    /// nothing but Core types in hand.
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
