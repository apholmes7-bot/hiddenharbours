using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The native size each brow instrument rasters at (ADR 0025 S4.5). Pure, so it is
    /// EditMode-testable without a canvas — the S1/S2/S3b discipline.
    ///
    /// <para><b>The flush mounts retired with the dash card (H2).</b> An instrument's place on the helm
    /// is now its named housing in the band, and the instrument itself is only ever drawn on its
    /// raised card, at the native size kept here wherever the screen has room
    /// (<see cref="HelmBandLayout.RaisedCardRect"/>). The raster is still the one native-size picture
    /// each host hands its <c>RawImage</c>; there is simply no second, flush destination for it.</para>
    /// </summary>
    public static class HelmInstrumentMountLayout
    {
        /// <summary>The rig resolution an instrument rasters at — the native size its raised card
        /// keeps. Zero for a slot with no renderer: a caller then raises nothing rather than guessing a
        /// shape for an instrument that does not exist.</summary>
        public static void InstrumentNativeSize(SounderKind sounder, out int w, out int h)
        {
            switch (sounder)
            {
                case SounderKind.Depth: w = DepthRigRender.W; h = DepthRigRender.H; break;
                case SounderKind.Fish: w = FishRigRender.W; h = FishRigRender.H; break;
                default: w = 0; h = 0; break;
            }
        }
    }
}
