using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>How a school at a given depth is drawn on the water (<see cref="SwimmerVisibility"/>).</summary>
    public enum SwimmerDraw
    {
        /// <summary>Nothing on the water. The school is still there, still on the finder, still biting —
        /// it is simply too deep to see.</summary>
        None = 0,

        /// <summary>A shape under the surface and no more: the rig's <c>shadow</c> anim, no fins, no
        /// frames of swim. You can tell something is down there; you cannot tell what.</summary>
        Shadow = 1,

        /// <summary>The fish itself, dimmed — deep enough that the rig's own <c>waterZ</c> depth tint has
        /// already taken most of the colour out of it.</summary>
        Dim = 2,

        /// <summary>The fish itself, in full: every anim the rig bakes, with its dithered shadow ellipse
        /// on the ground beneath it.</summary>
        Full = 3
    }

    /// <summary>
    /// HOW DEEP IS TOO DEEP TO SEE — the owner's ruling of 2026-09-05 in one table:
    /// <i>"some fish will be not visible if swimming deep"</i>.
    ///
    /// <para><b>The finder and the bite are untouched.</b> This decides only what the WATER shows. A Deep
    /// school still draws its mark on the glass, still raises the bite rate and still weights the species
    /// roll exactly as before — which is the point: the instrument earns its keep precisely where your
    /// eyes cannot help you. Making the sea honest must not make the sonar redundant.</para>
    ///
    /// <para><b>One classifier, not a second one.</b> The band comes from
    /// <see cref="DepthDropMath.ZoneForDepth"/> — the very same call the depth drop, the catch resolver's
    /// band affinity and <see cref="FishSchoolModel"/>'s species pick already use, reading the owner's own
    /// <c>GameConfig.DepthDrop</c> thresholds. Inventing a "visible depth" number here would be a second
    /// set of bands to drift out of step with the first, and the owner would have to tune the sea twice
    /// to move one thing.</para>
    /// </summary>
    public static class SwimmerVisibility
    {
        /// <summary>
        /// What a school this deep is drawn as. The bands are the owner's
        /// (<c>GameConfig.DepthDrop</c>); the mapping below is the ruling.
        /// </summary>
        /// <param name="depthMetres">The school's <c>FishSchool.DepthMetres</c>.</param>
        /// <param name="d">The owner's depth-band thresholds (<c>GameConfig.DepthDrop</c>).</param>
        public static SwimmerDraw For(float depthMetres, in DepthDropSettings d)
            => For(DepthDropMath.ZoneForDepth(depthMetres, d.TidepoolMaxMeters, d.ShallowsMaxMeters,
                                              d.InshoreMaxMeters, d.MidwaterMaxMeters, d.DeepMaxMeters));

        /// <summary>The ruling itself, band by band — split out so a test can state the table without
        /// having to build a settings struct to reach it.</summary>
        public static SwimmerDraw For(FishDepthBand band)
        {
            switch (band)
            {
                // Right up in the light: the whole fish, and its shadow on the bottom under it.
                case FishDepthBand.Tidepool:
                case FishDepthBand.Shallows:
                    return SwimmerDraw.Full;

                // The working cove water — still a fish, but the rig's baked depth tint has it.
                case FishDepthBand.Inshore:
                    return SwimmerDraw.Dim;

                // The open column: a shape passing under the surface.
                case FishDepthBand.Midwater:
                    return SwimmerDraw.Shadow;

                // Over the drop-off and beyond — the glass, and only the glass.
                case FishDepthBand.Deep:
                case FishDepthBand.Abyssal:
                default:
                    return SwimmerDraw.None;
            }
        }

        /// <summary>Is anything drawn on the water at all for this band? The presenter's early-out, and
        /// the one line a budget argument rests on: a Deep school costs nothing to show.</summary>
        public static bool Draws(FishDepthBand band) => For(band) != SwimmerDraw.None;
    }
}
