using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The commercial block: nine trades across three rigs whose install order is load-bearing and
        /// whose every load-order failure is silent.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> ShopKitRigs() =>
            new RigRegistration
            {
                // ---- THE COMMERCIAL BLOCK (the 2026-08-06 shop kit) ------------------------------
                // Nine trades — general store, fish market, chandlery, bakery, restaurant, tavern,
                // post office, takeout stand, gift shop — across three rigs that share ONE camera, one
                // palette and one footprint formula. Measured rather than read off the header: all
                // three project identically to 0.0000 px at every facing, and all three turn the same
                // way as houseIsoRig and the fleet.
                //
                // ⭐ ALL THREE PUT THE DOOR ON +Y — which is why this family does NOT inherit the
                // interiorIsoRig trap two entries up. THAT rig's door is on −Y, so a room stands a
                // half-turn from its shell and the offset must be measured and carried. The shop kit's
                // room is the shopfront seen from inside (its own README: "the +Y wall is the street
                // elevation from the inside"), so shell and room register at the SAME facing.
                // ShopRegistrationProbe measures that at bake time and writes the working into the
                // contract. It is not declared here, and it must not be assumed to be 4 by analogy.
                //
                // ⚠️ THE LOAD ORDER IS LOAD-BEARING AND EVERY ONE OF ITS FAILURES IS SILENT. Measured:
                //   · Shopfront ALONE resolves restaurant 8.90 × 11.25 m; with ShopInterior loaded,
                //     9.00 × 11.50. The 0.5 m cell SNAP lives in the interior rig and the other two
                //     read it back — so a shell baked without it is a shell its own interior cannot fit.
                //   · ShopInterior.dims({room:'kitchen'}) without ShopBuilding returns the WHOLE SHELL
                //     (9.00 × 11.50, planned=false) instead of the kitchen (6.09 × 5.09): the same call,
                //     3.5× the area, no error. The rig reports which happened as dims().planned, and
                //     ShopRigBaker asserts it rather than trusting the load set.
                //   · shopBuildingRig's README says it "throws without ShopInterior". It does NOT — it
                //     installs cleanly and fails later, at render. Do not lean on that guard.
                // Declaring the dependency here is what stops every caller having to remember it.
                ["shopInterior"] = new RigEntry($"{RigFolder}/shop-building-kit/shopInteriorRig.js",
                                                "ShopInterior", AzimuthConvention.CounterClockwise),

                ["shopfront"] = new RigEntry($"{RigFolder}/shop-building-kit/shopfrontRig.js",
                                             "Shopfront", AzimuthConvention.CounterClockwise,
                                             new[] { "shopInterior" }),

                // Depth-first in this order installs shopInterior, then shopfront, then this — the whole
                // kit in one host, the only configuration in which every number above is right. NOT a
                // cycle: shopInterior needs nothing loaded to install, and its plan-adoption use of
                // ShopBuilding resolves at CALL time (measured), so the graph stays acyclic.
                ["shopBuilding"] = new RigEntry($"{RigFolder}/shop-building-kit/shopBuildingRig.js",
                                                "ShopBuilding", AzimuthConvention.CounterClockwise,
                                                new[] { "shopInterior", "shopfront" }),
            };
    }
}
