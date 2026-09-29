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
                //
                // ⭐ The lifecycle PASS is a prerequisite (2026-08-19). The rig carries a two-line
                // hook after build(b) that reads root.BuildingLifecycle and NO-OPS when it is absent
                // — so without the declaration a phase/decay bake silently renders the finished
                // house. Loading it changes nothing for a finished build: measured byte-identical.
                //
                // ⭐ THE VILLAGE RETURN (drop 14, switched with the re-bake in #898). The returned house
                // draws the coastal-heritage look and lights through CoastalPass.light, so the companion
                // is its second prerequisite; with it absent the rig draws the classic look (measured:
                // with {classic:true, coastalPass:false} it draws today's five houses pixel for pixel,
                // VillageReturnIntakeTests). Its door ANCHOR now follows the door it draws, which is why
                // a room registers against its own shell (InteriorRigBaker.ExteriorOptsFor). The
                // companion names interiorProp and never house: house names IT.
                ["house"] = new RigEntry($"{VillageReturnFolder}/houses-kit/Art/houseIsoRig.js", "HouseIso",
                                         AzimuthConvention.CounterClockwise,
                                         new[] { "buildingLifecycle", "coastalPass" }),

                // The net-shed / storage-barn / fish-plant family — the wharf's working buildings.
                // Same story as the house, one size worse: the 1200×1160 cell is sized to hold the
                // `cannery`, so a net shed occupies a fraction of it and eight uncropped facings would
                // be 9600 px wide (the kit's own reference sheet, which is why that PNG stayed in
                // docs/ and was never imported).
                ["wharfBuilding"] = new RigEntry($"{RigFolder}/wharfBuildingRig.js", "WharfBuilding",
                                                 AzimuthConvention.CounterClockwise,
                                                 new[] { "buildingLifecycle" }),
            };
    }
}
