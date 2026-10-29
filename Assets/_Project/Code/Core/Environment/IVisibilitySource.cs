namespace HiddenHarbours.Core
{
    /// <summary>
    /// Core seam for the air's visibility at any world time, past ones included (ADR 0027 #7, the water
    /// type's silt). It is the fog law <see cref="IEnvironmentService.Sample"/> reads now, asked at t, so
    /// the visibility at now is the sample's own. Implementations evaluate the law without storing
    /// history: a tap a window ago costs one evaluation, and a jump to t reads what playing to t would.
    /// The sibling of <see cref="IWaveWindSource"/>, which brings the sea state to the same taps.
    /// </summary>
    public interface IVisibilitySource
    {
        /// <summary>0..1 air clarity at <paramref name="gameTimeSeconds"/> (1 = clear, 0 = thick murk).</summary>
        float VisibilityAt(double gameTimeSeconds);
    }
}
