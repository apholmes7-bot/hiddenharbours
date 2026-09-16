using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The COASTAL HERITAGE pass — the manors (exterior shell and interior floors) and the
        /// aesthetic companion the houses, cottages and manors all read.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        ///
        /// <para>⚠️ <b>The manors are NEW to this repo.</b> No <c>manorIsoRig.js</c>,
        /// <c>manorUnitIsoRig.js</c> or <c>coastalPass.js</c> has ever been committed here — checked
        /// across the whole history, not just the tip. Nothing below replaces anything; the four host
        /// rigs that DID move (house, interior, interiorProp, wharfBuilding) keep their existing
        /// entries in their own kit files and only gain the companion as a prerequisite.</para>
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
                // Its ONE hard dependency is PropIso: `coastalPass.js:41` throws
                // "CoastalPass needs Art/interiorPropRig.js" by name rather than degrading, so the
                // prerequisite below is what turns a missing companion into a load error instead of a
                // half-dressed room. Its other reference, root.HouseIso.entrance(), is reached only
                // while applying the pass to a house — by which time the host rig that named the
                // companion as ITS prerequisite has already run. Declaring "house" here instead would
                // close a cycle (house -> coastalPass -> house) that InstallPrerequisites does not
                // guard against.
                //
                // ⚠️ THE COMPANION IS AESTHETIC AND THE STAIRS ARE NOT. `CoastalPass.enabled = false`
                // (or `coastalPass:false` in the options) reverts the LOOK. It does not revert the
                // stair geometry, which lives in the host rigs: measured on all four shipped interior
                // rooms, the floor-to-floor rise is identical with the companion on and off
                // (sageCottage 2.65 · school 2.59 · redSaltbox 2.74 · whiteFarmhouse 2.92). What the
                // companion does move is the GROUND room's ceiling: with it on, the plate drops to
                // sit under the upper floor's 0.24 m slab.
                ["coastalPass"] = new RigEntry($"{RigFolder}/coastalPass.js", "CoastalPass",
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
                ["manorIso"] = new RigEntry($"{RigFolder}/manorIsoRig.js", "ManorIso",
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
                ["manorUnitIso"] = new RigEntry($"{RigFolder}/manorUnitIsoRig.js", "ManorUnitIso",
                                                AzimuthConvention.CounterClockwise,
                                                new[] { "manorIso", "interiorProp", "coastalPass" }),
            };
    }
}
