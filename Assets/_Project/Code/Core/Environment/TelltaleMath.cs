using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Apparent wind: the wind the BOAT feels, true wind minus her own velocity. Motoring hard into a
    /// flat calm still makes a wind across her deck; bearing away in a blow takes the sting out of it.
    /// <see cref="SailDrive"/> resolves her sails against this.
    ///
    /// <para>The name is historical: it once also drove a masthead pennant, which no boat flies any
    /// more (owner ruling 2026-11-20). Only the apparent-wind sum is left.</para>
    ///
    /// <para><b>Deterministic and engine-light.</b> A pure function of <c>(trueWind, boatVelocity)</c>
    /// with no clock read, no RNG and no state, so it is EditMode-testable. Vector2 only.</para>
    /// </summary>
    public static class TelltaleMath
    {
        /// <summary>Apparent wind across her deck: what the boat feels, in m/s.</summary>
        public static Vector2 ApparentWind(Vector2 trueWind, Vector2 boatVelocity)
            => trueWind - boatVelocity;
    }
}
