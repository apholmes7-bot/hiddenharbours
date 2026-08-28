using System.Collections.Generic;
using System.Globalization;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>What a row is, told three ways on the page (see <see cref="CatalogBook.StatusFor"/>).</summary>
    public enum CatalogRowState
    {
        /// <summary>Full ink, gold pill on the cursor, the seller's verb on the buy line.</summary>
        Affordable = 0,
        /// <summary>Price ruled through, the done stamp, "Already yours."</summary>
        Owned = 1,
        /// <summary>Faint ink, and the SHORTFALL — a number to plan against, not a refusal.</summary>
        TooDear = 2,
    }

    /// <summary>
    /// <b>The seller's wares book, as state.</b> Which stubs it has, which shelf is open, where the
    /// cursor is, what page that puts you on, and what each row says about itself.
    ///
    /// <para><b>Pure, and that is the point.</b> No canvas, no sprites, no Unity lifecycle — so the
    /// things worth getting right (the cursor wraps, the page follows it, a listing you cannot afford
    /// says how short you are, an empty shelf shows no stub) are EditMode assertions rather than
    /// something to squint at in play. <see cref="CatalogBookPresenter"/> draws this and decides
    /// nothing; it is the same split as <c>NotebookLayout</c> and <c>NotebookPresenter</c>.</para>
    ///
    /// <para><b>Rebuilt on open, on tab change, and after a purchase — never per frame</b> (rule 7).</para>
    /// </summary>
    public sealed class CatalogBook
    {
        /// <summary>The currency mark, as the note strings already spell it.</summary>
        public const string Money = "₲";

        private readonly AxisLatch _latch = new AxisLatch();
        private readonly List<BuyRow> _all = new();
        private readonly List<BuyRow> _shelf = new();
        private readonly List<CatalogSection> _stubs = new();
        private bool _everFilled;

        public CatalogBook(int linesPerLeaf)
        {
            LinesPerLeaf = linesPerLeaf < 1 ? 1 : linesPerLeaf;
        }

        /// <summary>How many ruled lines one leaf holds — the page break. From the notebook's own fit.</summary>
        public int LinesPerLeaf { get; }

        /// <summary>Whose book this is, for the head.</summary>
        public string SellerName { get; private set; } = "";

        /// <summary>The balance, drawn in the book's head on every tab (ADR 0039 §2's rule, same place,
        /// so it never has two spellings).</summary>
        public int Purse { get; private set; }

        /// <summary>The shelves this seller actually stocks, in shelf order. A seller with one section
        /// shows one stub; an empty section shows none (owner ruling R5).</summary>
        public IReadOnlyList<CatalogSection> Stubs => _stubs;

        /// <summary>Which shelf is open.</summary>
        public CatalogSection Section { get; private set; } = CatalogSection.Gear;

        /// <summary>Every row on the open shelf, in order. The cursor indexes into THIS.</summary>
        public IReadOnlyList<BuyRow> Shelf => _shelf;

        /// <summary>Which row the cursor is on, or -1 when the shelf is empty.</summary>
        public int Index { get; private set; } = -1;

        /// <summary>True when there is a row to read.</summary>
        public bool HasRows => _shelf.Count > 0;

        /// <summary>The row the right leaf is written up for.</summary>
        public BuyRow Current => HasRows ? _shelf[Index] : default;

        /// <summary>Which leaf-page the cursor puts you on, and how many there are. The page FOLLOWS the
        /// cursor rather than being turned separately — one gesture, not two.</summary>
        public int Page => HasRows ? Index / LinesPerLeaf : 0;

        /// <inheritdoc cref="Page"/>
        public int PageCount => _shelf.Count == 0 ? 1 : (_shelf.Count + LinesPerLeaf - 1) / LinesPerLeaf;

        /// <summary>The rows drawn on the left leaf right now — the cursor's page.</summary>
        public void VisibleRows(List<BuyRow> into)
        {
            into.Clear();
            int from = Page * LinesPerLeaf;
            for (int i = from; i < _shelf.Count && i < from + LinesPerLeaf; i++) into.Add(_shelf[i]);
        }

        /// <summary>Where the cursor sits within the drawn page.</summary>
        public int IndexOnPage => HasRows ? Index - Page * LinesPerLeaf : -1;

        // ---- building ---------------------------------------------------------------------------

        /// <summary>
        /// Take a freshly built row set. Keeps the open shelf if the seller still stocks it — so a
        /// rebuild after a purchase does not throw you back to the first tab — and otherwise opens the
        /// first stub there is.
        /// </summary>
        public void SetRows(IReadOnlyList<BuyRow> rows, string sellerName, int purse,
                            CatalogSection? openOn = null)
        {
            SellerName = sellerName ?? "";
            Purse = purse;

            _all.Clear();
            if (rows != null) for (int i = 0; i < rows.Count; i++) _all.Add(rows[i]);

            _stubs.Clear();
            foreach (CatalogSection s in CatalogSections.InOrder)
                if (StocksAnythingIn(s)) _stubs.Add(s);

            // A FRESH book opens on this seller's first stub. Keeping the field's initial value here
            // would open whatever CatalogSection happens to be declared first-ish — invisible whenever
            // that section is also the first stub, and wrong the moment it is not.
            CatalogSection want;
            if (openOn.HasValue) want = openOn.Value;
            else if (_everFilled) want = Section;                       // a rebuild keeps the open shelf
            else want = _stubs.Count > 0 ? _stubs[0] : CatalogSection.Gear;
            _everFilled = true;

            Section = _stubs.Contains(want) ? want : (_stubs.Count > 0 ? _stubs[0] : CatalogSection.Gear);

            string wasOn = HasRows ? Current.Id : null;
            FillShelf();
            RestoreCursor(wasOn);
        }

        private bool StocksAnythingIn(CatalogSection s)
        {
            for (int i = 0; i < _all.Count; i++) if (_all[i].Section == s) return true;
            return false;
        }

        private void FillShelf()
        {
            _shelf.Clear();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].Section == Section) _shelf.Add(_all[i]);
            Index = _shelf.Count > 0 ? 0 : -1;
        }

        /// <summary>After a rebuild, put the cursor back on the row it was on — a purchase must not move
        /// the page under the hand that made it.</summary>
        private void RestoreCursor(string id)
        {
            if (string.IsNullOrEmpty(id) || _shelf.Count == 0) return;
            for (int i = 0; i < _shelf.Count; i++)
            {
                if (!string.Equals(_shelf[i].Id, id, System.StringComparison.Ordinal)) continue;
                Index = i;
                return;
            }
        }

        // ---- moving -----------------------------------------------------------------------------

        /// <summary>
        /// Feed this frame's move axis (+1 = up the list, -1 = down). Returns true when the cursor moved.
        ///
        /// <para>Wraps at both ends and steps once per push — the same latched rule the option picker
        /// uses in the bubble (<see cref="AxisLatch"/>), so choosing a row of stock and choosing a line
        /// of dialogue feel like one gesture rather than two dialects.</para>
        /// </summary>
        public bool Move(float axis)
        {
            if (_shelf.Count <= 1) { _latch.Absorb(axis); return false; }

            int dir = _latch.Step(axis);
            if (dir == 0) return false;

            // Up the list means TOWARD row 0 — the first row is drawn at the top of the leaf.
            Index = dir > 0 ? (Index - 1 + _shelf.Count) % _shelf.Count : (Index + 1) % _shelf.Count;
            return true;
        }

        /// <summary>Open a shelf by name. False when this seller does not stock it, so a dialogue row
        /// pointing at a section she has nothing on opens her first stub instead of an empty leaf.</summary>
        public bool OpenSection(CatalogSection section)
        {
            if (!_stubs.Contains(section) || section == Section) return false;
            Section = section;
            FillShelf();
            _latch.Reset();
            return true;
        }

        /// <summary>Step to the next/previous stub down the fore edge, wrapping.</summary>
        public bool StepSection(int dir)
        {
            if (_stubs.Count <= 1 || dir == 0) return false;
            int at = _stubs.IndexOf(Section);
            if (at < 0) at = 0;
            int next = (at + (dir > 0 ? 1 : -1) + _stubs.Count) % _stubs.Count;
            return OpenSection(_stubs[next]);
        }

        // ---- what a row says about itself --------------------------------------------------------

        /// <summary>Owned, too dear, or yours to take.</summary>
        public static CatalogRowState StateOf(in BuyRow row)
        {
            if (row.Quote.Owned) return CatalogRowState.Owned;
            return row.Quote.Affordable ? CatalogRowState.Affordable : CatalogRowState.TooDear;
        }

        /// <summary>
        /// The action, in the seller's words (owner ruling R6). One switch, and most of the voice: you
        /// buy a boat, you take a thing off a shelf, you put a tired hull right, and you sign for a
        /// licence. (Loc-seam literals, the HudStrings convention.)
        /// </summary>
        public static string VerbFor(in BuyRow row)
        {
            switch (row.Quote.Kind)
            {
                case BuyRowKind.Boat:       return "Buy her";
                case BuyRowKind.BoatRepair: return "Put her right";
                case BuyRowKind.License:    return "Sign for it";
                case BuyRowKind.Instrument: return "Fit it";
                default:                    return "Take it";
            }
        }

        /// <summary>
        /// The status sentence — the third coding of a row's state, beside the ink and the mark, because
        /// <c>ux-and-mobile-controls.md</c> §4 forbids colour alone.
        ///
        /// <para><b>The too-dear line names the SHORTFALL, not a refusal.</b> "You're ₲540 short" is a
        /// number the player can plan against; "you can't afford this" is a locked door, and P5 asks for
        /// the first one.</para>
        /// </summary>
        public string StatusFor(in BuyRow row)
        {
            switch (StateOf(row))
            {
                case CatalogRowState.Owned:
                    return "Already yours.";
                case CatalogRowState.TooDear:
                    return "You're " + Price(row.Quote.Price - Purse) + " short.";
                default:
                    return VerbFor(row);
            }
        }

        /// <summary>A price as the book writes it — the mark, a thin space, and grouped digits.</summary>
        public static string Price(int amount)
            => Money + " " + amount.ToString("N0", CultureInfo.InvariantCulture);
    }
}
