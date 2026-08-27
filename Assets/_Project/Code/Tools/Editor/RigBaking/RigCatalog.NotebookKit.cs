using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The player's notebook — the main UI surface, drawn as a physical book rather than a menu.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entry below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> NotebookKitRigs() =>
            new RigRegistration
            {
                // NOT DIRECTIONAL for the open spread — no camera, no turntable. Every piece of
                // furniture is a flat screen-space stamp on the 32 px/m assets grid, so the
                // convention below is the placeholder every non-directional entry carries and
                // nothing probes it. (The CLOSED object, NotebookIsoKit, is 8-facing and rides the
                // same file — it is not baked yet; see the note on isoSolid below.)
                //
                // ⚠️ IT DRAWS, so it needs a canvas. NotebookKitBaker.CanvasShimJs installs a
                // rasteriser BEFORE this loads (ADR 0021 §5: what the rig needs and does not
                // provide is the HOST's job, never a patch to the art director's file). That shim is
                // a SUPERSET of the bubble kit's — this rig also uses save/restore and
                // imageSmoothingEnabled, which the bubble shim does not provide.
                //
                // ⚠️ THE FACE IS A HARD PREREQUISITE, NOT A COURTESY. This kit ships no type of its
                // own: "CELL and FONT are imported from the dialogue bubble kit by reference", and
                // the rig's probeType() reports sameFontObject: true — literal object identity, not
                // matching numbers. Without dialogueBubble loaded first there is nothing to set the
                // page in. The kit's Art/ folder carries its own harness copy of dialogueBubbleRig.js
                // which is BYTE-IDENTICAL to the shipping one (verified; NotebookKitRigSourcesTests
                // keeps it that way), and we point at the SHIPPING file for the same reason #561 did:
                // pointing at a duplicate forks the rig the day either copy moves.
                //
                // ⚠️ isoSolid IS DECLARED EVEN THOUGH THE FURNITURE DOES NOT NEED IT — and that is
                // the trap. Load the rig without isoSolid and it still resolves, NotebookKit still
                // registers, and every piece of furniture still draws; only NotebookIsoKit silently
                // fails to appear. A missing global that costs nothing today is exactly the kind of
                // absence nobody notices until the closed book is wanted, so the prerequisite is
                // declared now, matching the kit's own published loadOrder. deckIsoSolid is the
                // shared turntable already in this catalog; the kit's bundled copy is byte-identical
                // to it, so there is no third fork.
                ["notebook"] = new RigEntry(
                    $"{RigFolder}/notebook-kit/Art/notebookRig3.js", "NotebookKit",
                    AzimuthConvention.Clockwise,
                    prerequisites: new[] { "dialogueBubble", "deckIsoSolid" }),
            };
    }
}
