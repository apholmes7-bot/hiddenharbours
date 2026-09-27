using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// Where Claude Design's village return (drop 14, 2026-09-26) is landed, as delivered:
        /// <c>houses-kit/</c>, <c>shop-building-kit/</c> and <c>yard-landscaping-kit/</c> side by side,
        /// with the drop's own notes and <c>INTAKE.md</c> at the top.
        /// </summary>
        const string VillageReturnFolder = RigFolder + "/village-return";

        /// <summary>
        /// The COASTAL HERITAGE pass — the manors (exterior shell and interior floors) and the
        /// aesthetic companion the houses, cottages and manors all read.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away. They were first written in #853 (the 09-15 drop, never merged) and
        /// carried here with the drop-14 sources: the two manor rigs are byte-identical to #853's once
        /// line endings are normalised; <c>coastalPass.js</c> is the new 3.2.0 and was re-measured.</para>
        ///
        /// <para>⚠️ <b>NOTHING THE GAME BAKES INSTALLS THESE YET.</b> The drop is landed beside today's
        /// rigs (<c>docs/art/rigs/village-return/</c>), and <c>house</c> and <c>interior</c> still name
        /// today's sources with today's prerequisites. They switch to the returned rigs, and gain the
        /// companion as a prerequisite, in the same commit as the re-bake — so the game draws today's
        /// sheets until the sheets that match the new sources exist. The manors are the v3 return's:
        /// they have no bake menu and no placement, only these keys.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> CoastalHeritageRigs() =>
            new RigRegistration
            {
                // ⭐ NOT A RIG — A PASS, like buildingLifecycle, and the second entry in the catalog
                // that draws no building of its own. It installs globalThis.CoastalPass and exposes no
                // W/H/pivot triple, so it loads through InstallModule; the convention is the
                // placeholder every non-directional entry carries.
                //
                // Its ONE hard dependency is PropIso: the pass's prop emitter (`coastalPass.js:52`)
                // throws "CoastalPass needs Art/interiorPropRig.js" by name rather than degrading. It
                // throws when it is first asked to dress a room, not when it loads (measured: the file
                // loads alone), so the prerequisite below is what makes a bake never reach that throw.
                // Its other reference, root.HouseIso.entrance(), is reached only
                // while applying the pass to a house — by which time the host rig that named the
                // companion as ITS prerequisite has already run. Declaring "house" here instead would
                // close a cycle (house -> coastalPass -> house) that InstallPrerequisites does not
                // guard against.
                //
                // ⚠️ THE COMPANION IS AESTHETIC AND THE STAIRS ARE NOT. `CoastalPass.enabled = false`
                // (or `coastalPass:false` in the options) reverts the LOOK. It does not revert the
                // stair geometry, which lives in the host rigs: measured on all four shipped interior
                // rooms, the floor-to-floor rise is identical with the companion on, off and not loaded
                // (sageCottage 2.65 · school 2.59 · redSaltbox 2.74 · whiteFarmhouse 2.92; re-measured
                // on 3.2.0). It is also the shared LIGHT engine now (CoastalPass.light): the houses,
                // shops, yard and outbuildings relight through it when it is loaded and draw the
                // classic look without it.
                ["coastalPass"] = new RigEntry($"{VillageReturnFolder}/houses-kit/Art/coastalPass.js",
                                               "CoastalPass",
                                               AzimuthConvention.Clockwise,
                                               new[] { "interiorProp" }),

                // The manor SHELL — Second Empire mansard, stone laird's house and their kin, six
                // presets. Same turntable, same elev 40°, same 32 px = 1 m as the houses and the
                // fleet. The cell is 1040×1300 with the pivot at 520/1000, which is larger in both
                // axes than the house cell (992×1060) — eight uncropped facings would be 8320 px,
                // past the 4096 cap, so it bakes through BuildingRigBaker's tight crop like every
                // other building and never the boat turntable.
                //
                // The convention is DECLARED by family, not measured: the rig ships the same
                // N NE E SE S SW W NW order array as HouseIso and WharfBuilding and comes from the
                // same hand, so counter-clockwise is the only reading consistent with its siblings.
                // BuildingRigAzimuthProbe measures it at bake time from the door anchor (ManorIso
                // reports one at anchors(dir,opts).door, in cell pixels) and the bake REFUSES on a
                // mismatch — so a wrong guess here costs a red bake, never a wrong sheet.
                ["manorIso"] = new RigEntry($"{VillageReturnFolder}/houses-kit/Art/manorIsoRig.js",
                                            "ManorIso",
                                            AzimuthConvention.CounterClockwise,
                                            new[] { "coastalPass" }),

                // The manor INTERIOR — one sheet per floor of the stack (reception / chamber / attic),
                // seven presets, 960×1180 with the pivot at 480/840.
                //
                // ⭐ MANORISO MUST BE DEFINED BEFORE MANORUNITISO, and naming it first below is the
                // mechanism: InstallPrerequisites is depth-first AND IN ORDER, so "manorIso" runs (and
                // pulls the companion and PropIso ahead of itself) before this rig's own source. The
                // unit rig reads root.ManorIso in five places — the tier table, the spine, the floor
                // ladder — and the rigs' shared failure mode is to resolve a missing dependency as
                // `opts[k] ?? fallback` and render something plausible rather than complain. Listing
                // interiorProp and coastalPass explicitly as well is belt and braces: both arrive
                // transitively through manorIso today, and this entry should not silently depend on
                // that staying true.
                //
                // ⚠️ IT DOES NOT BAKE THROUGH InteriorRigBaker AS IT STANDS. That baker reads
                // `anchors(dir,opts).door.x` / `.floor.x` / `.Wd` / `.Ln` / `.storeyZ`; ManorUnitIso's
                // anchors(dir,opts) returns a different shape — { anchors:[…], openings:[…], dims }.
                // The floor plan the colliders need is on plan(opts).levels[i]: floorZ (1.05 / 4.6 /
                // 8.15 on mansardSeat), stairs[] with foot/head, going, rise, both landings and the
                // voidA span, and voids[] — the openings this level's floor carries for the flight
                // arriving from below. Read the flight rise as floorRise/steps (3.55/18), never the
                // reported `rise` (0.197, which is rounded and comes 0.004 m short over 18 treads).
                ["manorUnitIso"] = new RigEntry($"{VillageReturnFolder}/houses-kit/Art/manorUnitIsoRig.js",
                                                "ManorUnitIso",
                                                AzimuthConvention.CounterClockwise,
                                                new[] { "manorIso", "interiorProp", "coastalPass" }),
            };
    }
}
