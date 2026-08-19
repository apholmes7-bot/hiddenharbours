using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE PLAYER'S NOTEBOOK — the book itself, on screen.</b>
    ///
    /// <para><b>A pure view.</b> It owns no game state: quests come from <see cref="QuestBoard"/>
    /// reading the world's flags, knowledge from its Defs, and her notes are handed in already loaded.
    /// Closing the book changes nothing, so nothing here can corrupt a save.</para>
    ///
    /// <para><b>Right art or fallback, never wrong art.</b> Every baked piece is optional. A null
    /// sprite leaves that piece on a tinted rect drawn at the kit's own measured size — obviously
    /// unfinished, rather than finished and wrong (the <c>TryLoadCamperSprite</c> rule, #555). The
    /// same goes for the face: with no baked font the book sets in the built-in legacy font at the
    /// same cell pitch, which is ugly and legible rather than absent.</para>
    ///
    /// <para><b>Self-built canvas, screen space.</b> Like <c>DialoguePresenter</c> and every other
    /// screen in this repo, the hierarchy is built in code — there is no prefab to keep in sync and
    /// no scene to merge. It is built at KIT PIXELS (1 unit = 1 kit pixel) and the whole book is then
    /// scaled by an INTEGER, which is the only way the pointing hand lands on a whole cell.</para>
    ///
    /// <para><b>Overflow is MORE PAGES.</b> There is no scrollbar anywhere in this file and there must
    /// never be one: <see cref="NotebookLayout.Paginate"/> turns anything that does not fit into the
    /// next spread, and the player turns the page.</para>
    ///
    /// <para><b>Headless-safe.</b> Everything up to and including pagination runs with no canvas, no
    /// camera and no art — which is how the layout is proven in batch mode. Only
    /// <see cref="Rebuild"/> touches Unity objects.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NotebookPresenter : MonoBehaviour
    {
        /// <summary>Where the baked pieces live — the kit's own answer, not a second copy of it.</summary>
        public const string ArtFolder = NotebookKit.ArtFolder;

        /// <summary>
        /// The room the book is allowed, as a fraction of the screen.
        ///
        /// <para>The kit publishes the consequence rather than leaving it to be discovered: at the
        /// closest tier the open book is 73.4% of the screen and the world shows through the
        /// remaining 26.6%. It DOMINATES by design — it is held up in front of her, not docked in a
        /// corner — and this constant is where that choice is made knowingly.</para>
        /// </summary>
        public const float RoomFraction = 0.859f;

        // ---- the baked pieces, all optional -------------------------------------------------------

        [SerializeField] private Sprite _stock;
        [SerializeField] private Sprite _box;
        [SerializeField] private Sprite _boxTicked;
        [SerializeField] private Sprite _currentMark;
        [SerializeField] private Sprite _tab;
        [SerializeField] private Sprite _ribbon;
        [SerializeField] private Sprite _select;
        [SerializeField] private Sprite _cursor;
        [SerializeField] private Sprite _markNext;
        [SerializeField] private Sprite _markPrev;
        [SerializeField] private Sprite _stampDone;

        [Tooltip("The kit's face, baked to a custom Font asset. Null falls back to the built-in " +
                 "legacy font — legible, and obviously not the book's hand.")]
        [SerializeField] private Font _face;

        // ---- state ---------------------------------------------------------------------------------

        /// <summary>Is the book up? The one thing outside this file needs to know.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Which stub is selected, an index into <see cref="Tabs"/>.</summary>
        public int TabIndex { get; private set; }

        /// <summary>Which spread of the current tab is showing. A page turn moves this.</summary>
        public int SpreadIndex { get; private set; }

        /// <summary>The tab set as last built — data-driven, so its COUNT is content and not a constant.</summary>
        public IReadOnlyList<NotebookTab> Tabs => _tabs;

        /// <summary>The spreads of the CURRENT tab, already flowed.</summary>
        public IReadOnlyList<NotebookSpread> Spreads => _spreads;

        /// <summary>The size the book was last laid out at. <see cref="NotebookFit.Clamped"/> is worth
        /// reading — it says the room could not hold even the smallest legal book.</summary>
        public NotebookFit Fit { get; private set; }

        /// <summary>True when the face is the kit's own baked font rather than the built-in fallback.
        /// The readable half of "is the book still set in Arial".</summary>
        public bool HasKitFace => _face != null;

        private readonly List<NotebookTab> _tabs = new List<NotebookTab>();
        private List<NotebookSpread> _spreads = new List<NotebookSpread>();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameObject _root;
        private RectTransform _bookRect;

        private Func<IReadOnlyList<QuestDef>> _quests;
        private Func<IReadOnlyList<KnowledgePageDef>> _knowledge;
        private Func<IReadOnlyList<NotebookNote>> _notes;
        private IFlagStore _flags;

        private bool _gateWasBlocked;
        private Vector2 _lastAxis;

        // ---- wiring ---------------------------------------------------------------------------------

        /// <summary>
        /// Hand the book its art. Every argument is optional; a null leaves that piece on its tinted-rect
        /// fallback. Called by the region builder at build time, so nothing here reads an asset path at
        /// runtime.
        /// </summary>
        public void WireKitArt(Sprite stock = null, Sprite box = null, Sprite boxTicked = null,
                               Sprite currentMark = null, Sprite tab = null, Sprite ribbon = null,
                               Sprite select = null, Sprite cursor = null, Sprite markNext = null,
                               Sprite markPrev = null, Sprite stampDone = null, Font face = null)
        {
            if (stock != null) _stock = stock;
            if (box != null) _box = box;
            if (boxTicked != null) _boxTicked = boxTicked;
            if (currentMark != null) _currentMark = currentMark;
            if (tab != null) _tab = tab;
            if (ribbon != null) _ribbon = ribbon;
            if (select != null) _select = select;
            if (cursor != null) _cursor = cursor;
            if (markNext != null) _markNext = markNext;
            if (markPrev != null) _markPrev = markPrev;
            if (stampDone != null) _stampDone = stampDone;
            if (face != null) _face = face;
        }

        /// <summary>
        /// Tell the book where its contents come from.
        ///
        /// <para>Suppliers rather than lists, deliberately: the book is rebuilt every time it opens, so
        /// a quest completed while it was shut is simply true the next time she looks — there is no
        /// cache to invalidate and no subscription to leak.</para>
        /// </summary>
        public void WireContent(Func<IReadOnlyList<QuestDef>> quests,
                                Func<IReadOnlyList<KnowledgePageDef>> knowledge,
                                Func<IReadOnlyList<NotebookNote>> notes,
                                IFlagStore flags)
        {
            _quests = quests;
            _knowledge = knowledge;
            _notes = notes;
            _flags = flags;
        }

        // ---- open / close ----------------------------------------------------------------------------

        private void OnEnable() => EventBus.Subscribe<NotebookRequested>(OnRequested);

        private void OnDisable()
        {
            EventBus.Unsubscribe<NotebookRequested>(OnRequested);
            if (IsOpen) Close();
        }

        private void OnRequested(NotebookRequested e) => Toggle();

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>
        /// Open the book.
        ///
        /// <para><b>It takes the interact verb with it.</b> <see cref="InteractionGate"/> is how a modal
        /// says "the key is mine" in this repo, and the book up is exactly that: the pump, the ladder
        /// and every other candidate stand down until it is shut. That is also what keeps Esc from
        /// reaching the pause menu, so the book's own close is reached first with no change to the
        /// shell.</para>
        /// </summary>
        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            _gateWasBlocked = InteractionGate.IsBlocked;
            InteractionGate.IsBlocked = true;

            SpreadIndex = 0;
            RebuildModel();
            Rebuild();
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            // Restore rather than clear: a book opened over something that already owned the key must
            // not hand the key back to the world on its way out.
            InteractionGate.IsBlocked = _gateWasBlocked;

            if (_root != null) _root.SetActive(false);
        }

        // ---- the model ---------------------------------------------------------------------------------

        /// <summary>
        /// Rebuild the tab set and re-flow the current tab. Headless-safe: no canvas is touched, which
        /// is how the whole of pagination is proven in batch mode.
        /// </summary>
        public void RebuildModel()
        {
            Fit = FitForScreen();

            _tabs.Clear();
            var built = NotebookContent.Build(
                _quests?.Invoke(), _knowledge?.Invoke(), _notes?.Invoke(), _flags, Fit.Cols);
            _tabs.AddRange(built);

            if (_tabs.Count == 0)
            {
                _spreads = new List<NotebookSpread>();
                return;
            }

            TabIndex = Mathf.Clamp(TabIndex, 0, _tabs.Count - 1);
            _spreads = NotebookLayout.Paginate(_tabs[TabIndex].Blocks, Fit.Cols, Fit.Lines);
            SpreadIndex = Mathf.Clamp(SpreadIndex, 0, Mathf.Max(0, _spreads.Count - 1));
        }

        /// <summary>The size the current screen affords. Split out so a test can drive it with an
        /// explicit room instead of a screen.</summary>
        public NotebookFit FitForScreen()
        {
            int w = Screen.width > 0 ? Screen.width : 1280;
            int h = Screen.height > 0 ? Screen.height : 720;
            return NotebookLayout.Fit(Mathf.RoundToInt(w * RoomFraction), Mathf.RoundToInt(h * RoomFraction));
        }

        /// <summary>Select a stub. Turning to a tab always opens it at its FIRST spread — a book
        /// remembers where its ribbon is, not where every tab was left.</summary>
        public void SelectTab(int index)
        {
            if (_tabs.Count == 0) return;

            int wrapped = ((index % _tabs.Count) + _tabs.Count) % _tabs.Count;
            if (wrapped == TabIndex) return;

            TabIndex = wrapped;
            SpreadIndex = 0;
            _spreads = NotebookLayout.Paginate(_tabs[TabIndex].Blocks, Fit.Cols, Fit.Lines);
            if (IsOpen) Rebuild();
        }

        /// <summary>Turn a page. Clamped rather than wrapped: running off the end of a book puts you at
        /// the last page, not back at the front.</summary>
        public void TurnPage(int delta)
        {
            if (_spreads.Count == 0) return;

            int next = Mathf.Clamp(SpreadIndex + delta, 0, _spreads.Count - 1);
            if (next == SpreadIndex) return;

            SpreadIndex = next;
            if (IsOpen) Rebuild();
        }

        /// <summary>True when there is a spread after this one — what the "next" mark is drawn for.</summary>
        public bool HasNextSpread => SpreadIndex < _spreads.Count - 1;

        /// <summary>True when there is a spread before this one.</summary>
        public bool HasPrevSpread => SpreadIndex > 0;

        // ---- input ------------------------------------------------------------------------------------

        /// <summary>
        /// Drive selection from the move axis, with the option picker's EDGE LATCH — a held stick moves
        /// one step, not one per frame. Separated from <c>Update</c> so a headless test can drive it
        /// directly; virtual key presses are undeliverable in PlayMode and are never used here.
        /// </summary>
        public void DriveAxis(Vector2 axis)
        {
            const float On = 0.5f, Off = 0.35f;

            bool wasX = Mathf.Abs(_lastAxis.x) > Off;
            bool wasY = Mathf.Abs(_lastAxis.y) > Off;

            if (!wasY && Mathf.Abs(axis.y) > On) SelectTab(TabIndex + (axis.y > 0f ? -1 : 1));
            if (!wasX && Mathf.Abs(axis.x) > On) TurnPage(axis.x > 0f ? 1 : -1);

            _lastAxis = axis;
        }

        // ---- the drawing -------------------------------------------------------------------------------

        /// <summary>
        /// Draw the current spread. The ONLY method here that needs Unity objects — everything the
        /// layout decided has already been decided by the time it runs.
        /// </summary>
        public void Rebuild()
        {
            EnsureCanvas();
            if (_root == null) return;

            _root.SetActive(true);

            // ⚠️ Detached AND deactivated before Destroy, which the teardown sites elsewhere do not
            // need to do. Destroy is deferred to the end of the frame, and this method REBUILDS in
            // place — so the old spread would still be parented and drawing while the new one was
            // added under it, and a page turn would show both leaves at once for a frame. Taking them
            // out of the hierarchy now makes the swap atomic; the deferred Destroy still collects them.
            for (int i = _bookRect.childCount - 1; i >= 0; i--)
            {
                GameObject child = _bookRect.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                child.SetActive(false);
                Destroy(child);
            }

            int cols = Fit.Cols, lines = Fit.Lines;
            _bookRect.sizeDelta = new Vector2(NotebookKit.SpreadWidthFor(cols), NotebookKit.SpreadHeightFor(lines));
            _bookRect.localScale = Vector3.one * Fit.Scale;

            // The cover, then the two leaves, then the fore-edge stubs.
            AddPiece(_bookRect, "Cover", null, 0, 0,
                     NotebookKit.SpreadWidthFor(cols), NotebookKit.SpreadHeightFor(lines), CoverInk);

            int inset = NotebookKit.CoverPad + NotebookKit.Edge;
            int pageW = NotebookKit.PageWidthFor(cols), pageH = NotebookKit.PageHeightFor(lines);

            NotebookSpread spread = _spreads.Count > 0 ? _spreads[SpreadIndex] : new NotebookSpread();

            DrawLeaf(spread.Left, inset, inset, pageW, pageH, cols, lines);
            DrawLeaf(spread.Right, inset + pageW + NotebookKit.Gutter, inset, pageW, pageH, cols, lines);

            DrawTabs(inset + 2 * pageW + NotebookKit.Gutter, inset);
            DrawPageMarks(inset, inset, pageW, pageH);
        }

        private void DrawLeaf(NotebookLeaf leaf, int x, int y, int pageW, int pageH, int cols, int lines)
        {
            RectTransform page = AddPiece(_bookRect, "Leaf", _stock, x, y, pageW, pageH, PaperInk);

            // The printed margin rule — the lane the pointing hand rests over.
            AddPiece(page, "MarginRule", null, NotebookKit.PadL + NotebookKit.CursorLane - 1,
                     NotebookKit.HeadHeight, 1, lines * NotebookKit.Pitch, RuleInk);

            int titleX = NotebookKit.PadL + NotebookKit.CursorLane;
            int textX = titleX + NotebookKit.BoxLane;
            int titleW = NotebookKit.CellWidth * NotebookKit.TitleColsFor(cols) - 1;
            int textW = NotebookKit.CellWidth * cols - 1;

            for (int r = 0; r < lines; r++)
            {
                int rowY = NotebookKit.HeadHeight + r * NotebookKit.Pitch;

                // Every ruled line is printed, whether or not anything sits on it — it is stock, not
                // decoration that follows the text.
                AddPiece(page, "Rule", null, NotebookKit.RuleInset, rowY + NotebookKit.RuleRow,
                         pageW - 2 * NotebookKit.RuleInset, 1, RuleInk);
            }

            if (leaf == null) return;

            foreach (NotebookPlacement placement in leaf.Placements)
            {
                for (int k = 0; k < placement.RowCount; k++)
                {
                    NotebookRow row = placement.Block.Rows[placement.FromRow + k];
                    int r = placement.Row + k;
                    if (r >= lines) break;

                    int rowY = NotebookKit.HeadHeight + r * NotebookKit.Pitch;

                    if (row.HasBox)
                    {
                        bool ticked = row.State == NotebookStepState.Done;
                        Sprite boxSprite = ticked && _boxTicked != null ? _boxTicked : _box;
                        AddPiece(page, "Box", boxSprite,
                                 titleX + (NotebookKit.BoxLane - NotebookKit.BoxSize) / 2,
                                 rowY + NotebookKit.BaselineRow - NotebookKit.BoxSize + 1,
                                 NotebookKit.BoxSize, NotebookKit.BoxSize,
                                 ticked ? InkStrong : Ink);

                        if (row.State == NotebookStepState.Current)
                        {
                            AddPiece(page, "Current", _currentMark, NotebookKit.PadL + 2,
                                     rowY, 8, 8, GoldInk);
                        }
                    }

                    bool wide = row.Wide;
                    AddText(page, row.Text,
                            wide ? titleX : textX,
                            rowY,
                            wide ? titleW : textW,
                            row.State == NotebookStepState.Done ? InkFaint : Ink);
                }

                if (placement.Block.Archived && _stampDone != null && placement.FromRow == 0)
                {
                    AddPiece(page, "Stamp", _stampDone, textX,
                             NotebookKit.HeadHeight + placement.Row * NotebookKit.Pitch, 25, 10, GoldInk);
                }
            }
        }

        private void DrawTabs(int x, int y)
        {
            for (int i = 0; i < _tabs.Count && i < NotebookKit.MaxTabs; i++)
            {
                int tabY = y + NotebookKit.TabTop + i * (NotebookKit.TabHeight + NotebookKit.TabGap);

                RectTransform chip = AddPiece(_bookRect, "Tab", _tab, x, tabY,
                                              NotebookKit.TabCol, NotebookKit.TabHeight,
                                              i == TabIndex ? GoldInk : TabInk);

                // The chip clips at the kit's cap, applied here rather than at the model, so the full
                // label stays readable to anything that wants it.
                string label = _tabs[i].Label ?? "";
                if (label.Length > NotebookKit.ChipChars) label = label.Substring(0, NotebookKit.ChipChars);

                AddText(chip, label, NotebookKit.TabPad, NotebookKit.TabPad - 1,
                        NotebookKit.TabCol - 2 * NotebookKit.TabPad,
                        i == TabIndex ? InkStrong : Ink);
            }

            if (_ribbon != null && _tabs.Count > 0)
            {
                AddPiece(_bookRect, "Ribbon", _ribbon, x - NotebookKit.RibbonWidth - 1, y, 5, 26, GoldInk);
            }
        }

        private void DrawPageMarks(int x, int y, int pageW, int pageH)
        {
            if (HasPrevSpread)
                AddPiece(_bookRect, "Prev", _markPrev, x + NotebookKit.PadL, y + pageH - 8, 5, 7, InkFaint);

            if (HasNextSpread)
                AddPiece(_bookRect, "Next", _markNext,
                         x + 2 * pageW + NotebookKit.Gutter - NotebookKit.PadR - 5, y + pageH - 8, 5, 7, InkFaint);
        }

        // ---- primitives ---------------------------------------------------------------------------------

        /// <summary>
        /// One piece. <paramref name="sprite"/> null draws the tinted rect instead — at the same rect,
        /// so an unbaked book has the right SHAPE and obviously the wrong surface.
        /// </summary>
        private RectTransform AddPiece(RectTransform parent, string name, Sprite sprite,
                                       int x, int y, int w, int h, Color fallbackTint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            // Top-left origin, in kit pixels — the same frame the contract's rects are quoted in, so a
            // number can be read off the kit and typed straight in.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackTint;
            image.raycastTarget = false;

            return rect;
        }

        private void AddText(RectTransform parent, string content, int x, int y, int w, Color ink)
        {
            if (string.IsNullOrEmpty(content)) return;

            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, NotebookKit.CellHeight);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = _face != null ? _face : FallbackFont();
            text.color = ink;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            // ⚠️ Only meaningful on the FALLBACK. A bitmap font ignores fontSize entirely — Unity's own
            // words are "font size and style overrides are only supported for dynamic fonts" — so the
            // kit's face sets at its baked pixel size and the whole book is scaled by Fit.Scale
            // instead. That is exactly what a pixel face wants; setting it here would be a no-op that
            // reads like a knob.
            text.fontSize = NotebookKit.GlyphHeight;
        }

        private static Font FallbackFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        private void EnsureCanvas()
        {
            if (_root != null) return;

            var canvasGo = new GameObject("NotebookCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD and above a bubble: the book is held up in front of everything.
            _canvas.sortingOrder = 400;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            _canvasRect = canvasGo.GetComponent<RectTransform>();

            _root = new GameObject("Notebook", typeof(RectTransform));
            var rootRect = _root.GetComponent<RectTransform>();
            rootRect.SetParent(_canvasRect, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var book = new GameObject("Book", typeof(RectTransform));
            _bookRect = book.GetComponent<RectTransform>();
            _bookRect.SetParent(rootRect, false);
            _bookRect.anchorMin = new Vector2(0.5f, 0.5f);
            _bookRect.anchorMax = new Vector2(0.5f, 0.5f);
            _bookRect.pivot = new Vector2(0.5f, 0.5f);
            _bookRect.anchoredPosition = Vector2.zero;
        }

        // ---- the palette, which is the bubble kit's ------------------------------------------------------

        private static readonly Color CoverInk = new Color(0.055f, 0.180f, 0.180f, 1f);
        private static readonly Color PaperInk = new Color(0.921f, 0.886f, 0.800f, 1f);
        private static readonly Color RuleInk = new Color(0.451f, 0.478f, 0.478f, 0.55f);
        private static readonly Color Ink = new Color(0.145f, 0.157f, 0.172f, 1f);
        private static readonly Color InkStrong = new Color(0.086f, 0.094f, 0.105f, 1f);
        private static readonly Color InkFaint = new Color(0.145f, 0.157f, 0.172f, 0.45f);
        private static readonly Color GoldInk = new Color(0.839f, 0.667f, 0.325f, 1f);
        private static readonly Color TabInk = new Color(0.788f, 0.741f, 0.639f, 1f);
    }
}
