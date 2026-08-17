using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The camper — the repo's first PARKED DWELLING, two lengths off one loft.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entry below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> CamperKitRigs() =>
            new RigRegistration
            {
                // ---- the camper iso kit (owner drop of 2026-08-16, imported in #552) --------------
                //
                // A riveted-aluminium monocoque travel trailer in two lengths — bantam (16 ft) and
                // clipper (26 ft) — which are "the same loft re-run, not two models". Self-contained:
                // no isoSolid.js, no prerequisites. 384×320 cell, pivot (192,214), DIRS 8, elev 40,
                // 32 px = 1 m, so it stands in the same space as the houses and the fleet.
                //
                // ⚠️ COUNTER-CLOCKWISE, MEASURED TWO WAYS, and both agree with two rigs the catalog
                // already declares CCW when all three are loaded into ONE host:
                //   · the ground-plane bearing of the rig's own +Y steps +45.000°/dir — the SAME
                //     figure, to three decimals, as utilityIsoRig and houseIsoRig. Depth is
                //     un-squashed by sin(40°) = 0.6428 before the atan2; a raw screen angle is not a
                //     world angle at this elevation and would answer a different question.
                //   · the hitch — the camper's heading end — projects 107 px (bantam) / 158 px
                //     (clipper) WEST of the pivot at cell 2, which the rig's own `order` labels 'E'.
                //     A cell labelled east depicting west is the counter-clockwise signature.
                // CamperRigAzimuthProbe re-measures at every bake and the bake REFUSES on a
                // disagreement with this declaration.
                //
                // ⚠️ THE BOAT PROBE CANNOT READ THIS RIG and neither can the building one, verbatim.
                // RigAzimuthProbe breaks its 180° ambiguity with a bow taper; a box trailer with a
                // rounded dome at each end has none. BuildingRigAzimuthProbe reads the DOOR, which
                // works for a house because a house's door is on its +Y face — but a camper's door is
                // on the CURB side (+X), so it projects 5.8 px (bantam) / 2.6 px (clipper) off the
                // pivot at cell 2, under that probe's own MinDoorOffsetPx = 16 guard. It would refuse,
                // correctly. The camper probe reads the hitch instead.
                //
                // ⚠️ render(dir, opts) takes an INTEGER INDEX 0..7, NOT the compass string the README
                // talks in: camBasis does dir*PI/4, so render('N') makes the angle NaN and returns a
                // fully TRANSPARENT cell with no error thrown. Measured: render(0) draws 7833 opaque
                // px, render('N') draws 0. CamperSheetBaker asserts an opaque-pixel floor on every
                // single cell rather than trusting the call site.
                ["camper"] = new RigEntry($"{RigFolder}/camper-iso-kit/camperIsoRig.js", "CamperIso",
                                          AzimuthConvention.CounterClockwise),
            };
    }
}
