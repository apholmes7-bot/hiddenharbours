using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The aids to navigation: 14 IALA Region B marks. The SECOND buoy family in this catalog —
        /// see the entry's own note for why it is not the deck loop's spar float.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> NavBuoyKitRigs() =>
            new RigRegistration
            {
                // ---- the NAVIGATION BUOY kit (drop of 2026-08-11) — the aids to navigation --------
                //
                // ⚠️ THE SECOND BUOY FAMILY. `buoyIso` above is the LOBSTER SPAR FLOAT — 1.2 m of foam
                // in a fisher's colours on a 10×32 cell, baked to DeckLoopSheets/Buoys for the trap
                // loop. THIS is the channel furniture: 14 IALA Region B marks (4 cardinals, 4 laterals,
                // isolated danger, safe water, special, regulatory, mooring, spar) × 5 hull diameters
                // × 3 wear states, up to 6.6 m tall and made of steel. Different global, different
                // sheet folder (Art/Sprites/NavBuoys/Iso), different consumer. Nothing about this entry
                // touches the trap-loop buoys.
                //
                // ⚠️ IT RIDES deckIsoSolid, AND THAT IS NOT A SUBSTITUTION OF CONVENIENCE. The drop
                // shipped its own isoSolid.js; it is CHARACTER-IDENTICAL to the deck-loop kit's
                // already-registered copy (same sha256 once CRLF is normalised — the repo's copy is
                // CRLF, the drop's LF, which is the whole 216-byte difference). Committing a second
                // copy of a registered global's source is the drift docs/art/rigs/README.md's no-edit
                // rule exists to prevent — whichever loaded last would silently win for BOTH kits — so
                // the kit's copy is gitignored and this entry declares the existing turntable instead.
                //
                // The substitution is proven at PIXEL level, not by file hash: loaded against
                // deckIsoSolid, this rig reproduces all four of the drop's own reference sheets
                // BYTE-FOR-BYTE (PortCan|s18 working · CardinalW|s20 fresh+lit · StbdLit|s24
                // working+lit · Spar|s12 working). See docs/art/rigs/nav-buoy-kit/IMPORT.md.
                //
                // ⚠️ CLOCKWISE, MEASURED: the +X ground-plane bearing steps −45°/dir with the depth
                // un-squashed by /sin 40° — same turntable as the deck-loop kit, OPPOSITE to every
                // boat here. The SCREEN mean is −46.7525°, numerically identical to the figure the
                // iso-rig-pack records for its COUNTER-clockwise rigs, and is therefore not a
                // handedness test. NavBuoyRegistrationProbe re-measures from the live rig at bake time
                // and the bake refuses on a mismatch, same as every sibling.
                //
                // ⚠️ Loads with InstallModule, never Install: no W/H/pivot/DIRS/defaultElev globals at
                // all (measured — all five are `undefined`). Cell geometry is per type+size, from
                // cell(type,size) → {W,H,cx,cy}, and FACINGS come from the contract, never a DIRS field.
                //
                // ⚠️ A missing turntable here THROWS ("Cannot read properties of undefined (reading
                // 'tube')") rather than rendering wrong art — measured, and a happier failure mode than
                // the pass-6 character body's silent fallback. The prerequisite is still declared: a
                // loud failure at bake time is not a reason to leave the dependency to each caller.
                ["navBuoy"] = new RigEntry($"{RigFolder}/nav-buoy-kit/navBuoyRig.js", "NavBuoy",
                                           AzimuthConvention.Clockwise,
                                           prerequisites: new[] { "deckIsoSolid" }),
            };
    }
}
