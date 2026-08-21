using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The forecourt: 21 pieces over two rigs that share one cell, on the fuel kit's grade table.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> GasStationKitRigs() =>
            new RigRegistration
            {
                // ---- the gas station kit (owner drop of 2026-08-20) ------------------------------
                //
                // 21 pieces: dispenser ×7, island ×4, canopy ×3, sign ×3, store ×2, fillport ×2.
                // Seven DISPENSERS that are different machines rather than sizes of one, and the
                // amount a machine sells is `hoses` (one per grade position), not a size.
                //
                // ⚠️ CLOCKWISE, MEASURED — and it shares the deck-loop turntable rather than merely
                // resembling it. The drop ships its own isoSolid.js which is BYTE-IDENTICAL
                // (sha256 f7fc9db5…) to the registered deckIsoSolid, and its own fuelRig.js which is
                // BYTE-IDENTICAL (sha256 811361ff…) to the registered `fuel` entry's source. Both
                // copies are gitignored — a second copy of a registered global's source is the drift
                // the no-edit rule exists to prevent — and this entry declares the registered ones as
                // prerequisites. Byte-identity was checked against the GIT BLOB, not the working
                // tree: core.autocrlf is on here, so the checked-out fuelRig.js is 697 bytes longer
                // than the drop's for line endings alone and a naive size or hash comparison reports
                // a drift that does not exist.
                //
                // Handedness measured the only admissible way — the un-squashed ground-plane bearing
                // of +X, against utilityIso (registered CounterClockwise) in ONE host: this kit steps
                // −45.000°/dir, the reference +45.000°/dir. Two independent readings agree: the rig's
                // own project(), and the island's slot0→slot3 mounts, which are a real abeam pair in
                // the geometry rather than an abstract axis. ⚠️ Their SCREEN means are ∓46.7525° —
                // the same magnitude — so that figure does NOT distinguish the two families and must
                // not be used. StationRegistrationProbe re-measures both at every bake and refuses on
                // a disagreement with this declaration.
                //
                // ⚠️ NO W/H/pivot triple — the cell is per (type, size, opts) and comes from
                // cell(type, size, opts), so these load with InstallModule; Install would throw on the
                // missing pivot. Both DO expose DIRS as a number (8) and defaultElev (40).
                //
                // ⚠️ THREE SILENT-DIVERGENCE TRAPS, all in the kit's OWN DOCUMENTATION rather than in
                // its code, all measured and recorded rather than patched (his files run unmodified,
                // ADR 0021 §5) and all defended in GasStationSheetBaker:
                //   · The rig's header comment names the seventh dispenser `sCardlock`. The table key
                //     is `sLock`, and resolveSize falls through, so 'sCardlock' renders a KERB POST —
                //     byte-identical to sKerb, at 25×52 instead of 32×63. An unknown TYPE throws; an
                //     unknown SIZE does not.
                //   · The export README documents render(type, size, dir, opts). The rig is
                //     render(type, dir, opts) with size in opts. Called the README's way it does not
                //     throw — the size string lands where dir belongs and it renders sKerb.
                //   · The interior README documents gameplaySections({ size }). The rig's own call
                //     site passes a BARE STRING, and the object form silently returns the PAY BOOTH
                //     for every size asked of it. That one is load-bearing: it is what writes the
                //     storefront's SOLE and INTERACT into the sidecar.
                //
                // ⭐ Unlike the fuel kit there is NO phantom-state cache defect here: resolve()
                // normalises every option before both wearPass and the cache key, so an omitted
                // option and an explicitly defaulted one are one picture under one key. Measured over
                // all 21 pieces in BOTH call orders. The baker still passes every option explicitly.
                ["gasStation"] = new RigEntry($"{RigFolder}/gas-station-rig/rig/gasStationRig.js",
                                              "StationIso", AzimuthConvention.Clockwise,
                                              prerequisites: new[] { "deckIsoSolid", "fuel" }),

                // The sales floor. NOT a cycle and NOT a peer: the interior asks the shell for
                // shell(size) and paints into the shell's own cell(), so the shell must be installed
                // first — and it throws by name if it is not, which is the one dependency in this kit
                // that fails loudly. Installing THIS key depth-first brings the whole set up in the
                // only order that works: deckIsoSolid → fuel → gasStation → stationInterior.
                //
                // ⭐ SEAMLESS IS MEASURED, not asserted: StationInterior.cell(size) returns the same
                // quad AND the same pivot as StationIso.cell('store', size) at both sizes
                // (252×216 kiosk, 560×435 store, pivots identical). That is what lets the two sheets
                // crop to their OWN ink — the interior's is 34–47% smaller — and still register to
                // the pixel, because they are aligned by a shared world pivot and not by a cell corner.
                //
                // Interiors ashore do not move: there is no rock() parameter here and that is a law of
                // the kit, not an omission. Do not add one by analogy with the boat interiors.
                ["stationInterior"] = new RigEntry($"{RigFolder}/gas-station-rig/rig/stationInteriorRig.js",
                                                   "StationInterior", AzimuthConvention.Clockwise,
                                                   prerequisites: new[] { "gasStation" }),
            };
    }
}
