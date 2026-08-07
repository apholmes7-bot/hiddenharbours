using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The finished on-deck figure, as the hull's drawer needs them</b> — the one place the
    /// character's picture and the hull's geometry are put into the same sentence.
    ///
    /// <para>Pulled out of <see cref="DeckRiderVisual"/> as a pure static for one reason: it is a
    /// COMPOSITION of quantities that mean nothing to each other (a sprite, a world point, a deck
    /// position, a bake elevation, a lean), and the failure mode of such a thing is not a wrong
    /// formula but a swapped argument — a deck height passed where a roll belongs, a heading where an
    /// elevation belongs. Neither compiles differently, neither throws, and both draw a fisher who is
    /// occluded by the wrong half of the boat. That is a defect only a test can see, and a test can
    /// only see it if the composition is reachable without a live hull, a live camera and a GPU.</para>
    ///
    /// <para>It decides nothing about the LOOK: the cell, the flip and the tint arrive already chosen
    /// by the one authority (<c>IsoCharacterSprite</c> picks the cell, the deck rider leans it). All
    /// this adds is the pair of numbers the drawer cannot work out for itself — how far from the
    /// camera the boots are, and how much nearer the head is.</para>
    /// </summary>
    public static class DeckOccupantPoseMath
    {
        /// <summary>
        /// Compose the pose for a fisher standing at <paramref name="deckLocal"/> on a hull drawn at
        /// <paramref name="drawnHeadingDegrees"/> by artwork baked at
        /// <paramref name="bakeElevationDegrees"/>.
        ///
        /// <para>The two derived terms, and why each is what it is:</para>
        /// <list type="bullet">
        ///   <item><b>Depth</b> — <see cref="DeckAreaMath.DeckDepth"/> of the boots, the THIRD row of
        ///   the very projection the deck walk uses for the first two. Using the same matrix is what
        ///   makes "the fisher is clamped onto this deck" and "the fisher is behind this wheelhouse"
        ///   answers to one question rather than two opinions.</item>
        ///   <item><b>Lean into the scene</b> — <see cref="DeckAreaMath.DepthPerScreenRise"/>, so the
        ///   figure is a sloped plane and not a flat card. A body's head is nearer the camera than its
        ///   boots; without the slope, a wheelhouse that clears the boots clears the head too, which
        ///   is whole-object occlusion smuggled back in one level down.</item>
        /// </list>
        ///
        /// <para>Pure, static, allocation-free, deterministic — and it is called every frame someone is
        /// aboard, so it stays that way (rule 7).</para>
        /// </summary>
        public static HullDeckOccupantPose For(Sprite sprite, Vector2 worldFeet, Vector2 deckLocal,
                                               float deckHeightMeters, float drawnHeadingDegrees,
                                               float bakeElevationDegrees, float rollDegrees,
                                               bool flipX, Color tint)
            => new HullDeckOccupantPose(
                   sprite,
                   worldFeet,
                   DeckAreaMath.DeckDepth(deckLocal, deckHeightMeters, drawnHeadingDegrees,
                                          bakeElevationDegrees),
                   DeckAreaMath.DepthPerScreenRise(bakeElevationDegrees),
                   rollDegrees,
                   flipX,
                   tint);
    }
}
