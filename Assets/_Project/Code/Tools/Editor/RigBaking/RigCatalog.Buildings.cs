using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The building exteriors — the clapboard houses of Nine Mile Creek and the wharf's working
        /// buildings. Both bake through BuildingRigBaker, never the boat turntable.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> BuildingRigs() =>
            new RigRegistration
            {
                // ---- the buildings (Nine Mile Creek + the St Peters village) --------------------

                // The clapboard houses. Same turntable, same elev 40°, same 32 px = 1 m as the fleet,
                // so a cottage and a Cape Islander stand in one space. In the README's INFERRED
                // counter-clockwise group and never measured — BuildingRigAzimuthProbe measures it at
                // bake time from the door anchor (a building has no bow taper for RigAzimuthProbe to
                // read) and the bake REFUSES on a mismatch, same as every sibling.
                //
                // ⚠️ Baked by BuildingRigBaker, never the boat turntable: the cell is 992×1060, so
                // eight facings in a row would be 7936 px — past the 4096 cap. The building baker
                // tight-crops to the drawn pixels first, which is what makes the bake possible at all.
                ["house"] = new RigEntry($"{RigFolder}/houseIsoRig.js", "HouseIso",
                                         AzimuthConvention.CounterClockwise),

                // The net-shed / storage-barn / fish-plant family — the wharf's working buildings.
                // Same story as the house, one size worse: the 1200×1160 cell is sized to hold the
                // `cannery`, so a net shed occupies a fraction of it and eight uncropped facings would
                // be 9600 px wide (the kit's own reference sheet, which is why that PNG stayed in
                // docs/ and was never imported).
                ["wharfBuilding"] = new RigEntry($"{RigFolder}/wharfBuildingRig.js", "WharfBuilding",
                                                 AzimuthConvention.CounterClockwise),
            };
    }
}
