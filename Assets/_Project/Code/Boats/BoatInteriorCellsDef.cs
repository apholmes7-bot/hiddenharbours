using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>One hull's interior PIXELS, kept off her visual def on purpose</b> — and loadable only when
    /// somebody actually opens the door.
    ///
    /// <para><b>Why this asset exists at all: rule 7.</b> These sheets are enormous. The cells bake
    /// UNCROPPED into the full hull cell so they composite 1:1 (the kit's own registration law), which
    /// costs <b>12.3 MB per lobster-class hull, 281 MB for the tanker, 340 MB for the packet</b>, and
    /// 1238 MB if the whole fleet were resident. Hard <c>Sprite[]</c> references on
    /// <see cref="BoatVisualDef"/> would drag all of a hull's pages in the moment SHE loads — so a creek
    /// with ten lobster boats would carry ~123 MB of cabin texture whether or not anyone ever goes
    /// below, and today nobody can (the entry gate is shut fleet-wide). Paying that for a feature that
    /// cannot be used is the definition of a budget regression.</para>
    ///
    /// <para><b>So the reference is broken deliberately.</b> The visual def keeps only the LINK to the
    /// <see cref="BoatInteriorDef"/> — a few hundred bytes of geometry and ids — and the pixels live
    /// here, under <c>Resources</c>, reachable by a key derived from that def's id and by nothing else.
    /// Unity cannot pull an asset nobody references, so <b>resident-while-shut is structurally zero</b>
    /// rather than merely small.</para>
    ///
    /// <para><b>When it loads:</b> at the door's CUE START (<see cref="BoatCabinDoor"/>), never at hull
    /// spawn. The leaf's own baked cue is ~560 ms of declared, diegetic animation, which is exactly the
    /// window a synchronous load wants to hide in — and it only ever runs after
    /// <c>BoatInteriorEntryPolicy.MayOffer</c> has already said yes, so an unenterable cabin never
    /// touches the disk.</para>
    ///
    /// <para><b>When it goes:</b> with the hull's lifetime, ref-counted through
    /// <see cref="BoatInteriorCells"/> because two boats of one class share these pixels. ⚠️ Released in
    /// <c>OnDestroy</c> and never in <c>OnDisable</c> — root-toggling is how a region hop works, and a
    /// release there would drop the sheets out from under a player standing in the cabin mid-crossing.</para>
    ///
    /// <para><b>Builder-written, never hand-authored</b> — same rule and reason as the interior defs:
    /// 24 hulls of up to 24 sprite references each is exactly the transcription a person gets wrong once
    /// and nobody notices, because a cabin one cell out of step still draws a cabin.</para>
    /// </summary>
    public sealed class BoatInteriorCellsDef : ScriptableObject
    {
        /// <summary>The <c>Resources</c> subfolder these live in.</summary>
        public const string ResourcesFolder = "BoatInteriorCells";

        [Header("Identity (imported — see the class doc before editing)")]
        [Tooltip("The BoatInteriorDef id these cells belong to — 'interior.tanker_iso'. Checked against " +
                 "the def at load, so a mis-keyed asset is caught rather than drawn.")]
        public string InteriorDefId = "";

        [Header("The pixels")]
        [Tooltip("The sliced cells, element [row * Facings + facing] — BoatInteriorKit.CellIndex's own " +
                 "LEVEL-MAJOR order, carried across unchanged.\n\n" +
                 "⚠ The cell for a given facing is resolved from the DRAWN HEADING, never by reusing " +
                 "the hull's own facing index: this compass and these sheets need not share a facing " +
                 "count OR a handedness, and on the lobster boat they share NEITHER.")]
        public Sprite[] Cells = Array.Empty<Sprite>();

        [Tooltip("Facings per row. 8 across the whole kit.")]
        [Min(1)] public int Facings = 8;

        [Tooltip("The SHEET's own level keys, in the order the cells are laid out — 'bridge', 'house', " +
                 "'below'. Provenance and diagnostics; the runtime maps through CellRowForLevel.")]
        public string[] CellLevels = Array.Empty<string>();

        [Tooltip("⚠ THE MAP THE WHOLE THING TURNS ON: for each of the DEF's levels, which ROW draws it " +
                 "— or -1 when nothing does.\n\n" +
                 "The two lists are NOT the same list. A BoatInteriorDef declares every level a route " +
                 "can reach, INCLUDING exterior working decks (main_deck, and the tanker's poop_deck) " +
                 "the interior sheets never bake; and the sheets run bridge/house/below where the defs " +
                 "run main_deck/house_sole/bridge_sole/below_sole. Different length AND different " +
                 "order. Indexing by the def's level index draws the BRIDGE while the player stands on " +
                 "the HOUSE sole — a perfectly plausible picture of the wrong room.")]
        public int[] CellRowForLevel = Array.Empty<int>();

        [Tooltip("Whether the runtime must UN-MIRROR these cells — IsoFacing.HeadingToFacingIndex " +
                 "does `idx = count - idx` when this is true. It is FALSE across this kit, and not " +
                 "by accident: every cell is rendered through RigBaker.DirForCell, which corrects " +
                 "the source rig's handedness AT BAKE TIME, so the cells come off the press " +
                 "canonically clockwise (cell i depicts +45°·i). ⚠ Do NOT wire this to the " +
                 "sheet contract's `convention` field: that records the RIG the room was cut " +
                 "from, a different fact, and one that IS CounterClockwise across the kit. " +
                 "Conflating the two mirrored every correct sheet on all 27 hulls, until it " +
                 "reached the owner's eye as a cabin drawn on the wrong part of the boat.")]
        public bool CellsAreCounterClockwise = false;

        [Header("Budget (imported — what loading this costs)")]
        [Tooltip("Megabytes of RGBA32 this hull's pages occupy once resident. Written by the builder " +
                 "from the sheet dimensions so the cost is legible in the inspector and assertable in " +
                 "a test, rather than something a profiler has to be opened to discover.")]
        public float ResidentMegabytes;

        /// <summary>
        /// The <c>Resources</c> path for an interior def id. <b>Dots become underscores</b>: Unity's
        /// <c>Resources.Load</c> takes a path with no extension and a dotted name invites it to strip
        /// the wrong tail, so <c>interior.tanker_iso</c> is filed as <c>interior_tanker_iso</c>. One
        /// documented transform, applied in one place, and round-tripped by a test.
        /// </summary>
        public static string PathFor(string interiorDefId)
            => string.IsNullOrEmpty(interiorDefId)
                ? null
                : $"{ResourcesFolder}/{interiorDefId.Replace('.', '_')}";

        /// <summary>The asset file name (no extension) for an interior def id.</summary>
        public static string FileNameFor(string interiorDefId)
            => string.IsNullOrEmpty(interiorDefId) ? null : interiorDefId.Replace('.', '_');

        /// <summary>
        /// True when these cells are whole enough to draw for <paramref name="def"/>: the right hull,
        /// a complete array for the rows declared, and a row map that covers the def with at least one
        /// level that actually draws something.
        ///
        /// <para>All-or-nothing on purpose: a PARTLY wired sheet is the failure that draws a plausible
        /// picture of the wrong thing, so a short array is refused whole rather than indexed into.</para>
        /// </summary>
        public bool IsUsableFor(BoatInteriorDef def)
        {
            if (def == null) return false;
            if (!string.Equals(InteriorDefId, def.Id, StringComparison.Ordinal)) return false;
            if (Facings < 1) return false;

            int rows = CellLevels != null ? CellLevels.Length : 0;
            int want = rows * Facings;
            if (want <= 0 || Cells == null || Cells.Length != want) return false;
            for (int i = 0; i < Cells.Length; i++) if (Cells[i] == null) return false;

            int levels = def.Levels != null ? def.Levels.Length : 0;
            if (CellRowForLevel == null || CellRowForLevel.Length != levels) return false;

            bool anyDrawn = false;
            for (int i = 0; i < CellRowForLevel.Length; i++)
            {
                int row = CellRowForLevel[i];
                if (row < -1 || row >= rows) return false;
                if (row >= 0) anyDrawn = true;
            }
            return anyDrawn;
        }
    }
}
