using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The deck-loop kit: the stern-deck working furniture, and the shared turntable
        /// (<c>deckIsoSolid</c>) that this whole family — and two later kits — lathe against.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> DeckLoopKitRigs() =>
            new RigRegistration
            {
                // ---- the deck-loop kit (drop of 2026-08-09) — the stern-deck working furniture ----
                //
                // ⚠️ CLOCKWISE, AND THAT IS MEASURED, NOT A PRIOR. This kit rides its OWN turntable
                // (isoSolid.js) and turns the OPPOSITE WAY to every boat in this catalog. Measured
                // against utility-iso (registered CounterClockwise) in one harness: utility's +X
                // ground-plane bearing steps +45°/dir, this kit's steps −45°/dir. Its drop README
                // declared CW and — unusually — is correct. Applying the fleet's CCW correction here
                // mirrors all eight cells of every piece. See docs/art/rigs/deck-loop-kit/IMPORT.md,
                // and re-measure with `node docs/art/rigs/deck-loop-kit/_verify.js`.
                //
                // ⚠️ The −46.75°/step SCREEN figure does not distinguish the two families — this kit
                // shares it with the CCW pack. Only the un-squashed ground-plane bearing is a
                // handedness test.
                //
                // isoSolid is the shared turntable, not a piece of art: no W/H/pivot triple, so it
                // loads with InstallModule and is declared as everything else's prerequisite. A
                // missing turntable does not throw — every rig here lathes against it.
                ["deckIsoSolid"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/isoSolid.js", "IsoSolid",
                                                AzimuthConvention.Clockwise),

                // Five pieces of working furniture. station() is the crew contract — where to stand,
                // which dir to face, and the WORLD-METRE workZ a clip must be handed; the hauler is
                // the one station whose operator faces outboard (turn 4).
                ["deckGear"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/deckGearRig.js", "DeckGear",
                                            AzimuthConvention.Clockwise,
                                            prerequisites: new[] { "deckIsoSolid" }),

                // Four builds. CAPS carries the per-hull stack limits (lobsterBoat 5 deck / 2
                // washboard, capeIslander 3 / 2, wharf 6 / 0) — those are rig DATA, not game
                // constants, and the deck loop reads them from here.
                ["trap"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/trapIsoRig.js", "TrapIso",
                                        AzimuthConvention.Clockwise,
                                        prerequisites: new[] { "deckIsoSolid" }),

                // ⚠️ TWO GLOBALS IN ONE FILE, and the drop README names only the second. TrapFauna
                // is the bakeable half: render(kind, opts) takes NO dir, because what a pot comes up
                // holding is not directional. TrapCatch is a canvas FAÇADE — it calls
                // document.createElement and cannot run in the bare host at all; bake through
                // TrapFauna.
                //
                // ⚠️ catchKit + crustacean are PREREQUISITES, and they are the reason this entry is
                // three keys long instead of one. The file's two halves split the kit's kinds: this
                // rig owns six (urchin whelk starfish sculpin kelp baitbag) and DELEGATES the rest —
                // lobster, crab, jonah, short — to CatchKit / Crustacean. TrapCatch returns NULL for a
                // delegated kind when those are absent, so a host without them produces empty mixes in
                // silence; TrapFauna.render, since #480, THROWS for the same kinds rather than drawing
                // a byte-identical urchin in their place. Declaring the pair here means neither
                // failure can reach a bake, in the same way deckIsoSolid means the turntable cannot go
                // missing. Both load clean in a bare host — catchKit only reaches
                // document.createElement inside item(), which no bake path calls (the storage baker
                // installs its canvas shim for exactly that call).
                ["trapFauna"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/trapFaunaRig.js", "TrapFauna",
                                             AzimuthConvention.Clockwise,
                                             prerequisites: new[] { "deckIsoSolid", "catchKit", "crustacean" }),

                // The banding/grading tray. Distinct from the top-level fishTrayRig.js — this is the
                // kit's own FishTray2, with nest/stack steps in metres.
                ["fishTray2"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/trayIsoRig.js", "FishTray2",
                                             AzimuthConvention.Clockwise,
                                             prerequisites: new[] { "deckIsoSolid" }),

                // Eight fleet colour schemes over ONE spar-buoy geometry: every scheme measures the
                // same 10×32 cell at the same pivot, and each turns barely at all (98.8–99.0% pixel
                // agreement with dir 0). A packer may collapse those rows; a baker must not assume it.
                ["buoyIso"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/buoyIsoRig.js", "BuoyIso",
                                           AzimuthConvention.Clockwise,
                                           prerequisites: new[] { "deckIsoSolid" }),
            };
    }
}
