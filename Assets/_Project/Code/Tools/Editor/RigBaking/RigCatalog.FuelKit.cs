using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// Fuel storage and dispensing: eight vessels over 21 sizes and 4 grades, plus the fill solver.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> FuelKitRigs() =>
            new RigRegistration
            {
                // ---- the fuel storage & dispensing kit (owner drop of 2026-08-11) ----------------
                //
                // Eight vessels over 21 sizes and 4 grades, plus a continuous fill solver:
                // jug · jerry · nozzle carried, drum · tote · skid · bulk · pump standing.
                //
                // ⚠️ CLOCKWISE, MEASURED — and it shares the deck-loop kit's turntable rather than
                // merely resembling it. The drop ships its own isoSolid.js which is BYTE-IDENTICAL
                // (LF-normalised sha256 f7fc9db5…) to the registered deckIsoSolid, so that copy is
                // gitignored — a second copy of a registered global's source is the drift the no-edit
                // rule exists to prevent — and this entry declares the registered one as prerequisite.
                // Identity is proved in PIXELS, not by hash: rendered against the REGISTERED
                // turntable the rig reproduces all TWELVE of the drop's reference sheets
                // byte-for-byte. See docs/art/rigs/fuel-storage-kit/IMPORT.md.
                //
                // Handedness measured the only admissible way — the un-squashed ground-plane bearing
                // of +X, against utilityIso (registered CounterClockwise) in ONE host: this kit steps
                // −45.000°/dir, the reference +45.000°/dir. ⚠️ Their SCREEN means are ∓46.7525°, the
                // same magnitude, so that figure does NOT distinguish the two families and must not
                // be used. FuelRegistrationProbe re-measures both at every bake and refuses on a
                // disagreement with this declaration.
                //
                // ⚠️ NO W/H/pivot triple — the cell is per (type, size, mode) and comes from
                // cell(type, size, opts), so this loads with InstallModule; Install would throw on
                // the missing pivot. It DOES expose DIRS as a number (8) and defaultElev (40).
                //
                // ⚠️ Two silent-divergence traps live in this rig, both recorded rather than patched
                // (his file runs unmodified, ADR 0021 §5) and both defended in FuelSheetBaker:
                // an OMITTED wear renders a phantom fourth state that collides in the geometry cache
                // with 'working' (see FuelStorageKit.BakedWear), and resolveSize falls through to a
                // default for any size it does not know. Unlike most of this lane, a missing
                // turntable THROWS here rather than rendering wrong.
                ["fuel"] = new RigEntry($"{RigFolder}/fuel-storage-kit/fuelRig.js", "FuelIso",
                                        AzimuthConvention.Clockwise,
                                        prerequisites: new[] { "deckIsoSolid" }),
            };
    }
}
