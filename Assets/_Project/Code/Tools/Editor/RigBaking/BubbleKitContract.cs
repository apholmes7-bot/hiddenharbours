using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.World;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>THE ORACLE</b> for the dialogue bubble kit — <c>dialogueBubble.contract.json</c>, read as
    /// data.
    ///
    /// <para>The kit GENERATES that file out of <c>dialogueBubbleRig.js</c> at the rig's own defaults
    /// and says so on the tin: "regenerate, never hand-edit". So it is the one place any of these
    /// numbers is true, and everything downstream is checked against it rather than trusted:
    /// <see cref="BubbleKitBaker"/> refuses to write a piece whose rendered size disagrees, the
    /// importer takes its 9-slice borders from it, and <c>BubbleKitContractTests</c> diffs every
    /// constant in <see cref="DialogueBubbleKit"/> against it so the runtime copy cannot drift.</para>
    ///
    /// <para>Same rule as <see cref="NavBuoyContract"/> — "the contract is the oracle" — and the same
    /// reason: this repo has shipped art whose numbers were transcribed by hand and quietly wrong.</para>
    ///
    /// <para>⚠️ <b><c>JsonUtility</c> cannot read a nested array of arrays</b>, and the contract has
    /// one: <c>panel.slices.cols</c> is <c>[[0,6],[6,6],[12,6]]</c>. It is not parsed here and does
    /// not need to be — <c>slices.corner</c> carries the only number the border needs, and the
    /// column spans are derivable from it and the tile size. Adding a field for them would silently
    /// deserialize to nulls.</para>
    /// </summary>
    public sealed class BubbleKitContract
    {
        /// <summary>Where the art lane's kit lives, relative to the repo root.</summary>
        public const string KitFolder = "docs/art/rigs/dialogue-bubble-kit";

        public const string ContractFileName = "dialogueBubble.contract.json";

        // ---- the committed shape, as JsonUtility sees it -------------------------------------
        // Only the fields read below are declared; JsonUtility ignores the rest of the file, so the
        // contract may carry as much prose as the art lane likes without touching this.

        [Serializable] public class GridBlock { public int ppu; }

        [Serializable]
        public class CellBlock
        {
            public int w, h, advance, capHeight, xHeight, ascender, descender, baselineRow, leading,
                       lineHeight;
            public int[] glyph;
        }

        [Serializable]
        public class TypeBlock
        {
            public CellBlock cell;
            public LadderRow[] ladder;
            public OptionRow[] options;
        }

        [Serializable]
        public class LadderRow
        {
            public int cols, lines, charsPerLine, cells;
            public int[] panel;      // [w, h]
            public int[] textBox;    // [w, h]
        }

        [Serializable]
        public class OptionRow
        {
            public int rows, rowH, textX, textY;
            public int[] px;         // [w, h]
        }

        [Serializable] public class InsetBlock { public int l, t, r, b; }

        [Serializable] public class SlicesBlock { public int corner; }

        [Serializable]
        public class PanelBlock
        {
            public int border, chamfer, corner, insetL, insetT, insetR, insetB, slice;
            public InsetBlock textInset;
            public SlicesBlock slices;
            public CellRange minCell, maxCell;
        }

        [Serializable] public class CellRange { public int cols, lines; }

        [Serializable]
        public class TailEntry
        {
            public string key, label, dir;
            public int w, h;
            public int[] tip;        // [x, y]
        }

        [Serializable]
        public class TailBlock
        {
            public int w, gap;
            public TailEntry[] set;
        }

        [Serializable]
        public class ChipBlock { public int h, padX, mountX, overlap, capH; }

        [Serializable]
        public class MarkerBlock { public int w, h, insetR, dropB, ms; public int[] bob; }

        [Serializable]
        public class CaretBlock { public int w, h, ms; public int[] cell; }

        [Serializable]
        public class HighlightEntry { public string key, label; public bool ship; }

        [Serializable]
        public class OptionsBlock
        {
            public int rowH, gutter, insetT, insetB, insetR, minRows, maxRows, pillChamfer,
                       speakerClear;
            public HighlightEntry[] highlight;
        }

        [Serializable]
        public class CursorEntry
        {
            public string key, label;
            public bool ship;
            public int w, h;
            public int[] point;      // [x, y]
        }

        [Serializable]
        public class StockEntry
        {
            public string key, label, paper, hi, lo, ink, edge;
            public bool ship;
        }

        [Serializable]
        public class OccupancyBlock { public int gap; public bool holds; }

        [Serializable]
        public class Root
        {
            public string kit;
            public int version;
            public GridBlock grid;
            public TypeBlock type;
            public PanelBlock panel;
            public TailBlock tail;
            public ChipBlock chip;
            public MarkerBlock marker;
            public CaretBlock caret;
            public OptionsBlock options;
            public CursorEntry[] cursor;
            public StockEntry[] stocks;
            public OccupancyBlock occupancy;
        }

        public Root Data { get; }

        /// <summary>Absolute path the contract was read from, for error messages worth acting on.</summary>
        public string SourcePath { get; }

        BubbleKitContract(Root data, string sourcePath)
        {
            Data = data;
            SourcePath = sourcePath;
        }

        /// <summary>Read and parse the kit's contract. Throws with the path if it is not there —
        /// a missing contract means the kit has not landed, which is a different problem from a
        /// contract that disagrees, and the two must not read alike.</summary>
        public static BubbleKitContract Load(string repoRoot = null)
        {
            string root = repoRoot ?? RigCatalog.RepoRoot;
            string full = Path.Combine(root, KitFolder, ContractFileName);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"The bubble kit's contract is missing at {full}. The kit is committed under " +
                    $"{KitFolder}/ — if this fired, the branch predates that import.", full);

            var data = JsonUtility.FromJson<Root>(File.ReadAllText(full));
            if (data?.grid == null || data.panel == null || data.type?.cell == null)
                throw new InvalidOperationException(
                    $"{full} parsed but came back without its grid/panel/type blocks. The contract " +
                    "changed shape — fix this reader rather than the file, which is generated.");

            return new BubbleKitContract(data, full);
        }

        // ---- lookups -------------------------------------------------------------------------

        public TailEntry Tail(string key)
        {
            foreach (var t in Data.tail.set)
                if (string.Equals(t.key, key, StringComparison.Ordinal)) return t;
            throw new ArgumentException($"No tail '{key}' in the contract. " +
                                        $"Known: {string.Join(", ", KeysOf(Data.tail.set))}.");
        }

        public CursorEntry Cursor(string key)
        {
            foreach (var c in Data.cursor)
                if (string.Equals(c.key, key, StringComparison.Ordinal)) return c;
            throw new ArgumentException($"No cursor '{key}' in the contract.");
        }

        /// <summary>The ladder row for a given cell count, or null. The ladder is the kit's own
        /// generated table, so a test that walks it is testing the kit's arithmetic and not a
        /// transcription of it.</summary>
        public LadderRow Ladder(int cols, int lines)
        {
            foreach (var r in Data.type.ladder)
                if (r.cols == cols && r.lines == lines) return r;
            return null;
        }

        static IEnumerable<string> KeysOf(TailEntry[] set)
        {
            foreach (var t in set) yield return t.key;
        }

        // ---- the oracle check ----------------------------------------------------------------

        /// <summary>
        /// Diff every number <see cref="DialogueBubbleKit"/> carries against the contract, returning
        /// one line per disagreement and an empty list when they agree.
        ///
        /// <para>Returns rather than throws so the caller chooses the consequence: the bake REFUSES
        /// on any mismatch, and the test reports all of them at once instead of stopping at the
        /// first — a wholesale regrid would otherwise take one green-red cycle per number.</para>
        /// </summary>
        public IReadOnlyList<string> Disagreements()
        {
            var bad = new List<string>();

            void Check(string what, int contractValue, int csValue)
            {
                if (contractValue != csValue)
                    bad.Add($"{what}: contract says {contractValue}, DialogueBubbleKit says {csValue}");
            }

            Check("grid.ppu", Data.grid.ppu, DialogueBubbleKit.PixelsPerUnit);

            var cell = Data.type.cell;
            Check("type.cell.w", cell.w, DialogueBubbleKit.CellWidth);
            Check("type.cell.h", cell.h, DialogueBubbleKit.CellHeight);
            Check("type.cell.advance", cell.advance, DialogueBubbleKit.CellWidth);
            Check("type.cell.lineHeight", cell.lineHeight, DialogueBubbleKit.CellHeight);
            Check("type.cell.capHeight", cell.capHeight, DialogueBubbleKit.CapHeight);
            Check("type.cell.xHeight", cell.xHeight, DialogueBubbleKit.XHeight);
            Check("type.cell.ascender", cell.ascender, DialogueBubbleKit.Ascender);
            Check("type.cell.descender", cell.descender, DialogueBubbleKit.Descender);
            Check("type.cell.baselineRow", cell.baselineRow, DialogueBubbleKit.BaselineRow);
            Check("type.cell.leading", cell.leading, DialogueBubbleKit.Leading);
            if (cell.glyph != null && cell.glyph.Length == 2)
            {
                Check("type.cell.glyph[0]", cell.glyph[0], DialogueBubbleKit.GlyphWidth);
                Check("type.cell.glyph[1]", cell.glyph[1], DialogueBubbleKit.GlyphHeight);
            }
            else bad.Add("type.cell.glyph is not a 2-element array");

            var p = Data.panel;
            Check("panel.slice", p.slice, DialogueBubbleKit.PanelSlice);
            Check("panel.slices.corner", p.slices.corner, DialogueBubbleKit.PanelCorner);
            Check("panel.corner", p.corner, DialogueBubbleKit.PanelCorner);
            Check("panel.border", p.border, DialogueBubbleKit.PanelBorder);
            Check("panel.chamfer", p.chamfer, DialogueBubbleKit.PanelChamfer);
            Check("panel.insetL", p.insetL, DialogueBubbleKit.PanelInsetL);
            Check("panel.insetT", p.insetT, DialogueBubbleKit.PanelInsetT);
            Check("panel.insetR", p.insetR, DialogueBubbleKit.PanelInsetR);
            Check("panel.insetB", p.insetB, DialogueBubbleKit.PanelInsetB);
            Check("panel.textInset.l", p.textInset.l, DialogueBubbleKit.PanelInsetL);
            Check("panel.textInset.t", p.textInset.t, DialogueBubbleKit.PanelInsetT);
            Check("panel.textInset.r", p.textInset.r, DialogueBubbleKit.PanelInsetR);
            Check("panel.textInset.b", p.textInset.b, DialogueBubbleKit.PanelInsetB);
            Check("panel.minCell.cols", p.minCell.cols, DialogueBubbleKit.MinCols);
            Check("panel.minCell.lines", p.minCell.lines, DialogueBubbleKit.MinLines);
            Check("panel.maxCell.cols", p.maxCell.cols, DialogueBubbleKit.MaxCols);
            Check("panel.maxCell.lines", p.maxCell.lines, DialogueBubbleKit.MaxLines);

            Check("tail.w", Data.tail.w, DialogueBubbleKit.TailWidth);
            Check("tail.gap", Data.tail.gap, DialogueBubbleKit.TailGap);

            Check("chip.h", Data.chip.h, DialogueBubbleKit.ChipHeight);
            Check("chip.padX", Data.chip.padX, DialogueBubbleKit.ChipPadX);
            Check("chip.mountX", Data.chip.mountX, DialogueBubbleKit.ChipMountX);
            Check("chip.overlap", Data.chip.overlap, DialogueBubbleKit.ChipOverlap);

            Check("marker.w", Data.marker.w, DialogueBubbleKit.MarkerWidth);
            Check("marker.h", Data.marker.h, DialogueBubbleKit.MarkerHeight);
            Check("marker.insetR", Data.marker.insetR, DialogueBubbleKit.MarkerInsetR);
            Check("marker.dropB", Data.marker.dropB, DialogueBubbleKit.MarkerDropB);
            Check("marker.ms", Data.marker.ms, DialogueBubbleKit.MarkerFrameMs);

            Check("caret.w", Data.caret.w, DialogueBubbleKit.CaretWidth);
            Check("caret.h", Data.caret.h, DialogueBubbleKit.CaretHeight);
            Check("caret.ms", Data.caret.ms, DialogueBubbleKit.CaretBlinkMs);

            var o = Data.options;
            Check("options.rowH", o.rowH, DialogueBubbleKit.OptionRowHeight);
            Check("options.gutter", o.gutter, DialogueBubbleKit.OptionGutter);
            Check("options.insetT", o.insetT, DialogueBubbleKit.OptionInsetT);
            Check("options.insetB", o.insetB, DialogueBubbleKit.OptionInsetB);
            Check("options.insetR", o.insetR, DialogueBubbleKit.OptionInsetR);
            Check("options.minRows", o.minRows, DialogueBubbleKit.OptionMinRows);
            Check("options.maxRows", o.maxRows, DialogueBubbleKit.OptionMaxRows);
            Check("options.pillChamfer", o.pillChamfer, DialogueBubbleKit.OptionPillChamfer);
            Check("options.speakerClear", o.speakerClear, DialogueBubbleKit.SpeakerClear);

            return bad;
        }
    }
}
