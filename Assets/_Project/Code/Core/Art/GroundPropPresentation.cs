using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>The light a thing on the ground gives, if it gives any: a fire on the sand.</summary>
    public struct GroundGlow
    {
        /// <summary>False = no light at all, and nothing is created for one.</summary>
        public bool Lit;

        public Color Color;

        /// <summary>How far the glow lights the ground round it, in metres.</summary>
        public float RangeMetres;

        public float Intensity;

        /// <summary>0 steady .. 1 a strong wobble.</summary>
        public float Flicker01;

        /// <summary>How high above the ground the light sits, in metres.</summary>
        public float HeightMetres;
    }

    /// <summary>
    /// <b>Draw this thing that somebody put down on the ground</b> — asked by a module that knows WHERE
    /// and WHEN (the World puts a fire on the beach while people are round it) of the module that knows
    /// HOW a sprite sorts among the others and how a light is drawn.
    ///
    /// <para><b>Why this is a seam in Core (rule 4).</b> The depth sort and the additive light are Art
    /// components; World does not reference Art and must not start. The same shape as
    /// <see cref="ICharacterFigurePresentationService"/>, for the same reason.</para>
    /// </summary>
    public interface IGroundPropPresentationService
    {
        /// <summary>
        /// Dress <paramref name="host"/>, which is already where the thing stands: draw
        /// <paramref name="sprite"/> on it (null = nothing to draw), sorted with the world round it, and
        /// give it <paramref name="glow"/> when that is lit. Visual only: it drives no simulation and
        /// saves nothing. Destroying the host removes all of it.
        /// </summary>
        void Dress(GameObject host, Sprite sprite, in GroundGlow glow);
    }

    /// <summary>
    /// The service locator for <see cref="IGroundPropPresentationService"/>. Null where nothing draws
    /// (a headless test, a build without Art): the caller keeps a bare object and loses only the look.
    /// </summary>
    public static class GroundPropPresentation
    {
        public static IGroundPropPresentationService Service { get; set; }
    }
}
