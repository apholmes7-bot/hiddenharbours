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

                // ⭐ PASS 2 of the same family (drop 13, in wharf-building-kit-v2/). A NEW
                // key beside pass 1, never a swap: the four placed sheets in Buildings.json name
                // `WharfBuilding.PRESETS` in their optionsJs, so pass 1 keeps baking them until the owner
                // rules a re-bake. Loading this chain moves none of their pixels — measured, and
                // WharfBuildingPass2IntakeTests re-measures it on the committed sheets every run.
                //
                // The prerequisites install depth-first in the README's load order: interiorProp (via
                // coastalPass), coastalPass, buildingLifecycle, the geometry, then the rig. weatherSky,
                // the README's other optional library, is left out: measured on Node, loading it changes
                // no pixel of any of the seven presets or the four placed builds. The kit's lib/ copies
                // are the viewer's; the catalog loads main's, which are the same blobs (the prop rig's copy
                // stays at its one home, docs/art/rigs/interiorPropRig.js: MultiunitKitIntakeTests).
                //
                // ⚠️ coastalPass IS NOT OPTIONAL HERE. Without CoastalPass.light the rig's render()
                // hands the call to pass 1 when WharfBuilding is loaded and throws when it is not —
                // either way, a pass-2 bake without the declaration never draws pass 2.
                //
                // The lifecycle hook sits inside the rig's model build: measured, the seven presets with
                // no lifecycle keys render byte-identical with the pass loaded and absent, and the decayed
                // placed builds differ. Counter-clockwise by BuildingRigAzimuthProbe's reading on all seven
                // presets; the door anchor at dir 2 sits where pass 1's does (netShed 518.4, cannery 353.6).
                // Same 1200×1160 cell and (600, 780) pivot as pass 1. Tight crops run from 280×311
                // (redShed) to 714×629 (cannery): the cannery needs the 4096 cap (5×2), and the fish
                // plant (676×591) packs 3×3 under 2048.
                ["wharfBuilding2"] = new RigEntry($"{RigFolder}/wharf-building-kit-v2/wharfBuildingRig2.js",
                                                  "WharfBuilding2", AzimuthConvention.CounterClockwise,
                                                  new[] { "coastalPass", "buildingLifecycle", "wharfBuilding2Geometry" }),
                // The geometry builder has no rendered heading. Clockwise is an unused placeholder,
                // following the existing non-directional catalog entries. It reads no other global.
                ["wharfBuilding2Geometry"] = new RigEntry($"{RigFolder}/wharf-building-kit-v2/wharfBuildingRig2.geo.js",
                                                          "WharfBuildingGeo2", AzimuthConvention.Clockwise),
            };
    }
}
