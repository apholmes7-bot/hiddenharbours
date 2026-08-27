using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>THE SELLER'S WARES BOOK</b> — the second book, drawn in the first one's hand.
    ///
    /// <para><b>It is opened by a person and closed back to a person.</b> No key summons it: a
    /// conversation publishes <c>CatalogViewRequested</c> and this opens; closing publishes
    /// <c>CatalogClosed</c> and the picker re-arms in the bubble above. A book with nobody holding it is
    /// a menu, which is the test this whole surface exists to pass.</para>
    ///
    /// <para><b>It opens LOW.</b> Over the lower half of the screen, with the seller and their bubble
    /// still visible above it — and it sorts BELOW the dialogue canvas so it can never occlude them. You
    /// are looking at a book someone handed you, in a place, with them still in it (design §3.1).</para>
    ///
    /// <para><b>Notebook language, exactly.</b> <see cref="NotebookInk"/> for every colour,
    /// <see cref="NotebookKit"/> for every dimension, the same baked kit sprites, the same face. Only
    /// <see cref="NotebookInk.LedgerCover"/> differs, because the stock in it is not hers.
    /// <c>QuestPanelPresenter</c> is the precedent: this is the THIRD surface in the book's hand, not a
    /// new visual language.</para>
    ///
    /// <para><b>It decides nothing.</b> <see cref="CatalogBook"/> is the state and is pure; this draws
    /// it. Rows rebuild on open, on tab change and after a purchase — never per frame (rule 7).</para>
    /// </summary>
    public sealed class CatalogBookPresenter : MonoBehaviour
    {
        /// <summary>Above the HUD (100), BELOW the dialogue bubble (110) — the speaker is never
        /// occluded by the book she is holding out.</summary>
        public const int SortingOrder = 105;

        /// <summary>How much of the screen height the book takes, opening from the bottom.</summary>
        public const float LowerScreenFraction = 0.52f;

        private static CatalogBookPresenter _instance;

        /// <summary>True while a book is open anywhere.</summary>
        public static bool IsOpen => _instance != null && _instance._book != null && _instance._showing;

        private CatalogBook _book;
        private bool _showing;
        private string _sellerId = "";
        private readonly List<BuyRow> _rows = new();
        private readonly List<BuyRow> _page = new();

        private Canvas _canvas;
        private GameObject _root;
        private RectTransform _spread;
        private Font _face;
        private Sprite _stock, _tab, _pill, _stamp, _next, _prev;

        // ---- state a test can read ---------------------------------------------------------------

        /// <summary>The book's state, or null while nothing is open. Exposed so the layout can be
        /// asserted without a canvas — the same bargain the dialogue presenter makes.</summary>
        public CatalogBook Book => _book;

        /// <summary>Whose book is open.</summary>
        public string SellerId => _sellerId;

        private void OnEnable()
        {
            _instance = this;
            EventBus.Subscribe<CatalogViewRequested>(OnRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CatalogViewRequested>(OnRequested);
            // A book torn down mid-read must still hand the conversation back, or the bubble above stays
            // dimmed for ever with no picker and no way out.
            if (_showing) Publish();
            if (_instance == this) _instance = null;
        }

        private void OnRequested(CatalogViewRequested request) => Open(request.SellerId, request.Section);

        // ---- opening and closing ------------------------------------------------------------------

        /// <summary>Open a seller's book, optionally on a named shelf.</summary>
        public void Open(string sellerId, string section)
        {
            if (string.IsNullOrEmpty(sellerId)) return;

            _sellerId = sellerId;
            _book = new CatalogBook(LinesPerLeaf());
            _showing = true;

            CatalogSection? on = CatalogSections.TryParse(section, out CatalogSection parsed)
                ? parsed : (CatalogSection?)null;
            Rebuild(on);
            Draw();
        }

        /// <summary>Put the book away and hand the player back to whoever lent it.</summary>
        public void Close()
        {
            if (!_showing) return;
            _showing = false;
            _book = null;
            _rows.Clear();
            if (_root != null) _root.SetActive(false);
            Publish();
        }

        private void Publish()
        {
            string seller = _sellerId;
            _sellerId = "";
            EventBus.Publish(new CatalogClosed(seller));
        }

        // ---- the rows -----------------------------------------------------------------------------

        /// <summary>Resolve this seller's counter and list what they stock. On open, on tab change, and
        /// after a purchase — never per frame.</summary>
        private void Rebuild(CatalogSection? openOn = null)
        {
            if (_book == null) return;

            SaveData save = GameServices.Save?.Current;
            int purse = GameServices.Wallet?.Money ?? 0;
            BuyCatalog.Build(_sellerId, purse, save, GameServices.Licenses, _rows);
            _book.SetRows(_rows, SellerNameFor(_sellerId), purse, openOn);
        }

        /// <summary>The name in the head. The seller id until content gives sellers names of their own —
        /// a stand-in that is honest about being one rather than a blank.</summary>
        private static string SellerNameFor(string sellerId) => sellerId ?? "";

        // ---- input ---------------------------------------------------------------------------------

        private void Update()
        {
            if (!_showing || _book == null) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;

            // Close on Esc / gamepad East — the project's shared Cancel convention. New Input System
            // only; legacy UnityEngine.Input compiles and then throws at runtime.
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (pad != null && pad.buttonEast.wasPressedThisFrame)) { Close(); return; }

            if (_book.Move(ReadMoveAxis(kb, pad))) Draw();

            int tab = ReadTabStep(kb, pad);
            if (tab != 0 && _book.StepSection(tab)) Draw();

            if ((kb != null && kb.eKey.wasPressedThisFrame) ||
                (pad != null && pad.buttonSouth.wasPressedThisFrame)) Confirm();
        }

        /// <summary>+1 up the list, -1 down. W/S, the arrows, or a stick — the latch in
        /// <see cref="CatalogBook"/> is what makes a held key step once.</summary>
        private static float ReadMoveAxis(Keyboard kb, Gamepad pad)
        {
            float axis = 0f;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) axis += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) axis -= 1f;
            }
            if (Mathf.Abs(axis) < AxisLatch.Threshold && pad != null) axis = pad.leftStick.ReadValue().y;
            return axis;
        }

        /// <summary>The fore-edge stubs, on the same left/right the page turn uses.</summary>
        private static int ReadTabStep(Keyboard kb, Gamepad pad)
        {
            if (kb != null)
            {
                if (kb.eKey.wasPressedThisFrame) return 0;                    // Interact, not a tab
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return 1;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) return -1;
            }
            if (pad != null)
            {
                if (pad.rightShoulder.wasPressedThisFrame) return 1;
                if (pad.leftShoulder.wasPressedThisFrame) return -1;
            }
            return 0;
        }

        /// <summary>
        /// Take the row the cursor is on, through <see cref="BuyCatalog.Confirm"/> — the one place a
        /// purchase is spent from a book, so "the panel is a skin" is a function and not a promise.
        /// </summary>
        public bool Confirm()
        {
            if (_book == null || !_book.HasRows) return false;

            BuyRow row = _book.Current;
            if (!BuyCatalog.Confirm(row)) return false;

            Rebuild();     // money moved and the row may have gone owned
            Draw();
            return true;
        }

        // ---- drawing --------------------------------------------------------------------------------

        private int LinesPerLeaf()
        {
            int usable = Mathf.RoundToInt(Screen.height * LowerScreenFraction) - NotebookKit.HeadHeight;
            int lines = usable / (NotebookKit.Pitch * Scale());
            return Mathf.Clamp(lines, NotebookKit.MinLines, NotebookKit.MaxLines);
        }

        /// <summary>⚠️ INTEGER ONLY. A fractional scale puts the hand on a half cell and the wobble stops
        /// reading as a hand and starts reading as a bug.</summary>
        private static int Scale()
            => Mathf.Clamp(Mathf.FloorToInt(Screen.height / 360f), 1, NotebookKit.MaxScale);

        private void Draw()
        {
            EnsureCanvas();
            EnsureArt();
            if (_spread == null || _book == null) return;

            for (int i = _spread.childCount - 1; i >= 0; i--) Destroy(_spread.GetChild(i).gameObject);
            _root.SetActive(true);

            int cols = NotebookKit.ClosestTierCols;
            int leafW = NotebookKit.PageWidthFor(cols);
            int leafH = NotebookKit.PageHeightFor(_book.LinesPerLeaf);
            int scale = Scale();

            _spread.localScale = new Vector3(scale, scale, 1f);
            _spread.sizeDelta = new Vector2(leafW * 2 + NotebookKit.Gutter, leafH + NotebookKit.HeadHeight);

            // the cover, then two leaves of stock on it
            Piece("Cover", null, -NotebookKit.CoverPad, -NotebookKit.CoverPad,
                  leafW * 2 + NotebookKit.Gutter + NotebookKit.CoverPad * 2,
                  leafH + NotebookKit.HeadHeight + NotebookKit.CoverPad * 2, NotebookInk.LedgerCover);
            Piece("LeftLeaf", _stock, 0, NotebookKit.HeadHeight, leafW, leafH, NotebookInk.Paper);
            Piece("RightLeaf", _stock, leafW + NotebookKit.Gutter, NotebookKit.HeadHeight,
                  leafW, leafH, NotebookInk.Paper);

            DrawHead(leafW);
            DrawStubs(leafW, leafH);
            DrawList(leafW);
            DrawEntry(leafW, leafH);
        }

        /// <summary>Seller on the left of the head, the balance on the right — on EVERY tab, in the same
        /// place ADR 0039 §2 put it in her notebook, so money never has two spellings.</summary>
        private void DrawHead(int leafW)
        {
            Label(_book.SellerName, NotebookKit.PadL, 3, leafW, NotebookInk.InkStrong);
            Label(CatalogBook.Price(_book.Purse), leafW + NotebookKit.Gutter, 3, leafW,
                  NotebookInk.Ink, TextAnchor.UpperRight);
        }

        /// <summary>The fore edge: one stub per section THIS seller stocks, never a fixed six.</summary>
        private void DrawStubs(int leafW, int leafH)
        {
            int x = leafW * 2 + NotebookKit.Gutter;
            for (int i = 0; i < _book.Stubs.Count && i < NotebookKit.MaxTabs; i++)
            {
                CatalogSection s = _book.Stubs[i];
                int y = NotebookKit.HeadHeight + NotebookKit.TabTop
                        + i * (NotebookKit.TabHeight + NotebookKit.TabGap);
                bool open = s == _book.Section;
                Piece($"Stub_{s}", _tab, x, y, NotebookKit.TabChipWidth, NotebookKit.TabHeight,
                      open ? NotebookInk.Paper : NotebookInk.Tab);
                Label(CatalogSections.ChipFor(s), x + NotebookKit.TabPad, y + 2, NotebookKit.TabChipWidth,
                      open ? NotebookInk.InkStrong : NotebookInk.Ink);
            }
        }

        /// <summary>The left leaf: one ruled line per listing, price right-aligned.</summary>
        private void DrawList(int leafW)
        {
            _book.VisibleRows(_page);
            int textX = NotebookKit.PadL + NotebookKit.CursorLane;
            int textW = leafW - textX - NotebookKit.PadR;

            for (int r = 0; r < _page.Count; r++)
            {
                BuyRow row = _page[r];
                int y = NotebookKit.HeadHeight + r * NotebookKit.Pitch;

                Piece("Rule", null, NotebookKit.RuleInset, y + NotebookKit.RuleRow,
                      leafW - 2 * NotebookKit.RuleInset, 1, NotebookInk.Rule);

                CatalogRowState state = CatalogBook.StateOf(row);
                Color ink = state == CatalogRowState.TooDear ? NotebookInk.InkFaint : NotebookInk.Ink;

                if (r == _book.IndexOnPage)
                {
                    Piece("Cursor", _pill, NotebookKit.PadL, y, leafW - NotebookKit.PadL - NotebookKit.PadR,
                          NotebookKit.CellHeight - 1, NotebookInk.Gold);
                    ink = NotebookInk.InkStrong;
                }

                Label(row.DisplayName, textX, y, textW, ink);
                Label(CatalogBook.Price(row.Quote.Price), textX, y, textW, ink, TextAnchor.UpperRight);

                // Ownership is a STAMP, a rule through the price and a sentence — three codings of one
                // fact, which is what ux-and-mobile-controls.md §4 asks for instead of colour alone.
                if (state != CatalogRowState.Owned) continue;
                Piece("Stamp", _stamp, leafW - NotebookKit.PadR - 8, y, 8, 8, NotebookInk.Gold);
                Piece("Struck", null, textX, y + NotebookKit.CellHeight / 2, textW, 1, NotebookInk.Ink);
            }

            if (_book.PageCount > 1)
                Piece("Next", _book.Page < _book.PageCount - 1 ? _next : _prev,
                      leafW - NotebookKit.PadR - 6, NotebookKit.HeadHeight + _book.LinesPerLeaf
                      * NotebookKit.Pitch, 6, 6, NotebookInk.Ink);
        }

        /// <summary>The right leaf: the row the cursor is on, written up.</summary>
        private void DrawEntry(int leafW, int leafH)
        {
            if (!_book.HasRows)
            {
                Label("Nothing on this shelf today.", leafW + NotebookKit.Gutter + NotebookKit.PadL,
                      NotebookKit.HeadHeight + NotebookKit.Pitch, leafW, NotebookInk.InkFaint);
                return;
            }

            BuyRow row = _book.Current;
            int x = leafW + NotebookKit.Gutter + NotebookKit.PadL;
            int w = leafW - NotebookKit.PadL - NotebookKit.PadR;
            int y = NotebookKit.HeadHeight + NotebookKit.Pitch;

            Label(row.DisplayName.ToUpperInvariant(), x, y, w, NotebookInk.InkStrong);
            y += NotebookKit.Pitch * 2;

            y = Wrapped(row.Flavor, x, y, w, NotebookInk.Ink);
            if (!string.IsNullOrEmpty(row.Note)) y = Wrapped(row.Note, x, y + NotebookKit.Pitch, w,
                                                            NotebookInk.Ink);

            // the buy line, at the foot of the leaf
            int foot = NotebookKit.HeadHeight + leafH - NotebookKit.PadB - NotebookKit.Pitch;
            Piece("EntryRule", null, x, foot - 4, w, 1, NotebookInk.Rule);
            Label(CatalogBook.Price(row.Quote.Price), x, foot, w, NotebookInk.InkStrong);
            Label(_book.StatusFor(row), x, foot, w,
                  CatalogBook.StateOf(row) == CatalogRowState.Affordable
                      ? NotebookInk.Gold : NotebookInk.Ink, TextAnchor.UpperRight);
        }

        /// <summary>Lay a paragraph out on the book's own wrap, and answer where the next line starts.</summary>
        private int Wrapped(string text, int x, int y, int w, Color ink)
        {
            if (string.IsNullOrEmpty(text)) return y;

            int cols = Mathf.Max(1, w / NotebookKit.CellWidth);
            List<string> lines = NotebookLayout.Wrap(text, cols);
            for (int i = 0; i < lines.Count; i++)
            {
                Label(lines[i], x, y, w, ink);
                y += NotebookKit.Pitch;
            }
            return y;
        }

        // ---- the canvas ------------------------------------------------------------------------------

        private void EnsureArt()
        {
            if (_face == null) _face = Resources.Load<Font>(HarbourType.ResourceKey);
            if (_stock == null) _stock = LoadPiece(NotebookKit.ShippedStock);
            if (_tab == null) _tab = LoadPiece(NotebookKit.ShippedTab);
            if (_pill == null) _pill = LoadPiece(NotebookKit.ShippedSelect);
            if (_stamp == null) _stamp = LoadPiece(NotebookKit.PieceStampDone);
            if (_next == null) _next = LoadPiece(NotebookKit.PieceMarkNext);
            if (_prev == null) _prev = LoadPiece(NotebookKit.PieceMarkPrev);
        }

        /// <summary>One baked piece by name, or null when the kit has not been baked — every draw is
        /// null-tolerant, so an unbaked kit is a flat-coloured book rather than an exception.</summary>
        private static Sprite LoadPiece(string piece)
            => string.IsNullOrEmpty(piece) ? null : Resources.Load<Sprite>(NotebookKit.ResourceKeyFor(piece));

        private void EnsureCanvas()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("Catalog_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            _root = new GameObject("CatalogRoot", typeof(RectTransform));
            _root.transform.SetParent(canvasGo.transform, false);

            var spreadGo = new GameObject("Spread", typeof(RectTransform));
            spreadGo.transform.SetParent(_root.transform, false);
            _spread = spreadGo.GetComponent<RectTransform>();

            // LOW: pinned to the bottom edge, so the speaker and their bubble stay in the clear above.
            _spread.anchorMin = new Vector2(0.5f, 0f);
            _spread.anchorMax = new Vector2(0.5f, 0f);
            _spread.pivot = new Vector2(0.5f, 0f);
            _spread.anchoredPosition = new Vector2(0f, NotebookKit.PadB);
        }

        private void Piece(string name, Sprite sprite, int x, int y, int w, int h, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_spread, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
        }

        private void Label(string content, int x, int y, int w, Color ink,
                           TextAnchor align = TextAnchor.UpperLeft)
        {
            if (string.IsNullOrEmpty(content)) return;

            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_spread, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, NotebookKit.CellHeight);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = _face != null ? _face : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = ink;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            // Only meaningful on the FALLBACK — the kit's bitmap face ignores fontSize and sets at its
            // baked pixel size; the whole spread is scaled by Scale() instead.
            text.fontSize = NotebookKit.GlyphHeight;
        }
    }
}
