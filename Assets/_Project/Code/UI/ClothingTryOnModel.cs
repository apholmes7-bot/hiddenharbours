using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>The try-on page at a clothes rack, as a rule</b> (store clothing, pass 1): which garment the
    /// cursor is on, what he is trying on, what a buy would do and what leaving puts back. Pure: no canvas,
    /// no input device, no Unity lifecycle, so "trying on never changes what he owns or wears" and "leaving
    /// restores exactly" are EditMode assertions.
    ///
    /// <para><b>It reuses the creator's preview, not a copy of it.</b> The figure on the page is the live
    /// player, shown through the same <see cref="IPlayerAppearance"/> seam the character creator previews
    /// through: a try is <see cref="IPlayerAppearance.Preview"/>, leaving is
    /// <see cref="IPlayerAppearance.CancelPreview"/>, and nothing here ever commits a recipe. Until garment
    /// art lands the preview paints each garment's <see cref="ClothingDef.StandIn"/> colour keys on the
    /// baked body; a garment with none (or one the builder refuses) shows him unchanged and says its
    /// picture is to come.</para>
    ///
    /// <para><b>Tries live here; owning and wearing live in the wardrobe.</b> A try is one garment per slot
    /// in this model's own array. Only <see cref="Activate"/> on a garment row reaches the wardrobe, through
    /// <see cref="ClothingShop.TryBuy"/> (or a plain wear for a garment he already owns).</para>
    ///
    /// <para><b>Rows.</b> One per garment the rack offers, in slot order (head, top, legs, feet, outerwear,
    /// hands) and then rack order, followed by one Leave row for the mouse. Keyboard, gamepad: up and down
    /// move and try on, Interact buys or puts on, Cancel leaves. Mouse: <see cref="Select"/> a row, then
    /// <see cref="Activate"/>.</para>
    /// </summary>
    public sealed class ClothingTryOnModel
    {
        private readonly ClothingRackStock _stock;
        private readonly PlayerWardrobe _wardrobe;
        private readonly IWallet _wallet;
        private readonly IPlayerAppearance _appearance;
        private readonly CharacterRecipe _openingRecipe;
        private readonly List<ClothingDef> _rows = new List<ClothingDef>();
        private readonly ClothingDef[] _tried = new ClothingDef[ClothingSlots.Count];
        private readonly AxisLatch _latch = new AxisLatch();

        public ClothingTryOnModel(ClothingRackStock stock, PlayerWardrobe wardrobe, IWallet wallet,
                                  IPlayerAppearance appearance)
        {
            _stock = stock;
            _wardrobe = wardrobe;
            _wallet = wallet;
            _appearance = appearance;
            _openingRecipe = appearance != null ? appearance.Committed : new CharacterRecipe();
            for (int o = 0; o < ClothingSlots.Order.Length; o++)
            {
                ClothingSlot slot = ClothingSlots.Order[o];
                for (int i = 0; stock != null && i < stock.LineCount; i++)
                {
                    ClothingDef def = stock.GarmentAt(i);
                    if (def != null && def.Slot == slot && !_rows.Contains(def)) _rows.Add(def);
                }
            }
            IsOpen = true;
            _latch.Absorb(1f);   // the push that opened the page is not the first step
            ShowLook();          // what he already wears, as the page will show it
        }

        public bool IsOpen { get; private set; }

        /// <summary>The garment rows, then <see cref="LeaveRow"/>.</summary>
        public int RowCount => _rows.Count + 1;
        public int LeaveRow => _rows.Count;
        public int FocusRow { get; private set; }

        /// <summary>The garment on <paramref name="row"/>, or null for the Leave row.</summary>
        public ClothingDef GarmentAt(int row) => row >= 0 && row < _rows.Count ? _rows[row] : null;

        /// <summary>The garment the cursor is on, or null on the Leave row.</summary>
        public ClothingDef Focused => GarmentAt(FocusRow);

        /// <summary>What he is trying on in <paramref name="slot"/> (not bought), or null.</summary>
        public ClothingDef TriedIn(ClothingSlot slot) => ClothingSlots.IsValid(slot) ? _tried[(int)slot] : null;

        /// <summary>What the page shows in <paramref name="slot"/>: the try, else what he wears.</summary>
        public ClothingDef ShowingIn(ClothingSlot slot) => TriedIn(slot) ?? _wardrobe?.WornIn(slot);

        /// <summary>True while the figure shows a stand-in look; false when the tried garments have no
        /// stand-in (or the builder refused it) and he is shown as he is.</summary>
        public bool StandInShown { get; private set; }

        /// <summary>The last buy's outcome, or null before any.</summary>
        public ClothingBuyResult? LastResult { get; private set; }

        /// <summary>How many of <paramref name="def"/> the rack has left.</summary>
        public int Left(ClothingDef def) => _stock != null ? _stock.Left(def) : 0;

        public bool Owns(ClothingDef def) => _wardrobe != null && _wardrobe.Owns(def);
        public bool Wears(ClothingDef def) => _wardrobe != null && _wardrobe.IsWearing(def);

        /// <summary>Moves the cursor to <paramref name="row"/> and, on a garment row, tries it on.</summary>
        public bool Select(int row)
        {
            if (!IsOpen || row < 0 || row >= RowCount) return false;
            FocusRow = row;
            ClothingDef def = GarmentAt(row);
            if (def != null && ClothingSlots.IsValid(def.Slot))
            {
                _tried[(int)def.Slot] = Wears(def) ? null : def;
                ShowLook();
            }
            return true;
        }

        /// <summary>One row up (-1) or down (+1), wrapping, trying on what it lands on.</summary>
        public bool Step(int direction)
        {
            if (!IsOpen || direction == 0 || RowCount == 0) return false;
            int next = (FocusRow + (direction > 0 ? 1 : -1) + RowCount) % RowCount;
            return Select(next);
        }

        /// <summary>
        /// Interact on the focused row. A garment he does not own: buy it (paid once, one off the rack, his,
        /// on him). One he owns but is not wearing: put it on, free. One he wears: nothing. The Leave row:
        /// leave. Returns true when something changed.
        /// </summary>
        public bool Activate()
        {
            if (!IsOpen) return false;
            if (FocusRow == LeaveRow) { Leave(); return true; }
            ClothingDef def = Focused;
            if (def == null || _wardrobe == null) return false;
            if (Owns(def))
            {
                if (Wears(def)) return false;
                bool worn = _wardrobe.Wear(def);
                if (worn) { _tried[(int)def.Slot] = null; ShowLook(); }
                return worn;
            }
            ClothingBuyResult result = ClothingShop.TryBuy(_stock, _wardrobe, _wallet, def);
            LastResult = result;
            if (result != ClothingBuyResult.Bought) return false;
            _tried[(int)def.Slot] = null;   // it is his now: worn, not tried
            ShowLook();
            return true;
        }

        /// <summary>Closes the page: every try is dropped and the figure shows exactly what was committed
        /// when the page opened (purchases stay his and stay on).</summary>
        public void Leave()
        {
            if (!IsOpen) return;
            for (int i = 0; i < _tried.Length; i++) _tried[i] = null;
            _appearance?.CancelPreview();
            StandInShown = false;
            IsOpen = false;
        }

        /// <summary>The device-free input step: the vertical axis moves (one row per push), confirm is
        /// Interact, cancel is Leave.</summary>
        public void Input(float vertical, bool confirm, bool cancel)
        {
            if (!IsOpen) return;
            if (cancel) { Leave(); return; }
            if (confirm) { Activate(); return; }
            int step = _latch.Step(vertical);
            if (step != 0) Step(-step);   // up on the stick is a row up the list
        }

        /// <summary>
        /// Paints the shown garments' stand-in colours on the opening recipe, head to hands (outerwear last),
        /// and previews it. Nothing to paint, or a refusal, shows him as committed.
        /// </summary>
        private void ShowLook()
        {
            if (_appearance == null) return;
            CharacterRecipe look = _openingRecipe.Clone();
            bool painted = false;
            for (int o = 0; o < ClothingSlots.Order.Length; o++)
            {
                ClothingDef def = ShowingIn(ClothingSlots.Order[o]);
                ClothingLookKey[] keys = def != null ? def.StandIn : null;
                if (keys == null) continue;
                for (int k = 0; k < keys.Length; k++)
                    if (!string.IsNullOrEmpty(keys[k].Field) && look.Set(keys[k].Field, keys[k].Value)) painted = true;
            }
            if (painted && _appearance.Preview(look, out _)) { StandInShown = true; return; }
            _appearance.CancelPreview();
            StandInShown = false;
        }
    }
}
