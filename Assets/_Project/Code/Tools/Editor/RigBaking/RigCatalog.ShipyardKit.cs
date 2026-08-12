using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The shipyard iso kit — the boatyard that services the fleet.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> ShipyardKitRigs() =>
            new RigRegistration
            {
                // The boatyard: 20 parts in metres and 5 named SITES that assemble them around a
                // hull, sized by the boat each yard serves. Tight cells like wharfIso — it reports
                // px,py per bake and declares no pivot global, so it loads with InstallModule.
                //
                // ⚠️ CounterClockwise is MEASURED, not inferred from its th = +dir*PI/4 sign (which
                // the rig-lane README rules inadmissible). ShipyardIso.project() returns figures
                // IDENTICAL to WharfIso.project() at all 8 dirs to 4 dp, and wharfIso is measured
                // CCW — so the two ride one turntable. A probe that read the positive sign as
                // clockwise would have registered this family mirrored and flipped all 25 keys at
                // once. See docs/art/rigs/shipyard-iso-kit/VERIFICATION.md §2.
                ["shipyardIso"] = new RigEntry($"{RigFolder}/shipyard-iso-kit/shipyardIsoRig.js",
                                               "ShipyardIso", AzimuthConvention.CounterClockwise),
            };
    }
}
