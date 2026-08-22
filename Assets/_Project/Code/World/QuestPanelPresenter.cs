using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The current quest, as a leaf off her notebook, pinned lower-right</b> — the owner's 2026-08-22
    /// diegetic ruling.
    ///
    /// <para>What it replaced: a bottom-centre banner of plain white outlined text
    /// (<see cref="OnboardingDirector"/>'s old hint). The words are unchanged and the source is
    /// unchanged — the director still decides which beat the player is on, and this only draws it. What
    /// changed is the voice: the same sentence in the notebook's stock, ink and face reads as something
    /// she wrote down, and a game whose main UI is a book (the 2026-08-20 ruling) should not also be
    /// shouting at the player in a system font.</para>
    ///
    /// <para><b>The corner is chosen, not merely free.</b> Bottom-centre belongs to the nav cluster and
    /// the helm dash; the note must never be the thing sitting on the compass. Lower-right is clear of
    /// both, clear of the top band, and clear of the interact popup's top-left.</para>
    ///
    /// <para><b>Rebuilds only on a CHANGE</b> (rule 7): the text it was given, or the screen it was laid
    /// out for. A hint that holds for three minutes costs two compares a frame and no allocation.</para>
    ///
    /// <para>Art is optional throughout, like the book's: an unbaked kit draws the right SHAPE in
    /// fallback tints and is obviously unbaked, rather than drawing nothing.</para>
    /// </summary>
    public sealed class QuestPanelPresenter : MonoBehaviour
    {
        /// <summary>
        /// Under the notebook (400) and the shell, alongside the HUD band — the sorting order the banner
        /// this replaced already had, so nothing else on screen changed order underneath it.
        /// </summary>
        public const int SortingOrder = 94;

        private Canvas _canvas;
        private RectTransform _noteRect;

        private Sprite _stock;
        private Sprite _currentMark;
        private Font _face;
        private bool _artResolved;

        // What is drawn right now, and what it was drawn FOR. Both are the change-detection.
        private string _text;
        private int _builtScreenW = -1, _builtScreenH = -1;
        private bool _overflowReported;

        /// <summary>The line currently on the note, or null when nothing is showing.</summary>
        public string Text => _text;

        /// <summary>True when there is a note on screen. The readable half of "did the banner go".</summary>
        public bool IsShowing => !string.IsNullOrEmpty(_text);

        /// <summary>The size the note was last laid out at — worth reading in a test or a proof.</summary>
        public QuestPanelFit Fit { get; private set; }

        /// <summary>
        /// Put a line on the note, or clear it with null/empty. Cheap to call every frame: the drawing
        /// only happens when the text actually changes.
        /// </summary>
        public void Show(string text)
        {
            string next = string.IsNullOrEmpty(text) ? null : text;
            if (next == _text) return;
            _text = next;
            Rebuild();
        }

        /// <summary>Take the note down.</summary>
        public void Hide() => Show(null);

        private void LateUpdate()
        {
            // A window resize re-fits the note. Read here rather than off a resize callback because
            // Screen is the only honest source and this is two int compares.
            if (!IsShowing) return;
            if (Screen.width == _builtScreenW && Screen.height == _builtScreenH) return;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        // ---- the drawing --------------------------------------------------------------------------

        private void Rebuild()
        {
            if (!IsShowing)
            {
                if (_canvas != null) _canvas.gameObject.SetActive(false);
                return;
            }

            EnsureArt();
            EnsureCanvas();
            _canvas.gameObject.SetActive(true);

            List<string> rows = QuestPanelLayout.Rows(_text);
            QuestPanelFit fit = QuestPanelLayout.Fit(rows.Count, Screen.width, Screen.height);
            Fit = fit;

            _builtScreenW = Screen.width;
            _builtScreenH = Screen.height;

            ReportOverflow(fit);
            ClearNote();

            _noteRect.sizeDelta = new Vector2(fit.WidthPx, fit.HeightPx);
            _noteRect.localScale = Vector3.one * fit.Scale;
            _noteRect.anchoredPosition = QuestPanelLayout.AnchoredPosition(in fit);

            // The leaf itself, then its printed furniture, then her hand on top of it.
            AddPiece(_noteRect, "Leaf", _stock, 0, 0, fit.WidthPx, fit.HeightPx, NotebookInk.Paper);

            int textX = NotebookKit.PadL + NotebookKit.CursorLane;
            int textW = NotebookKit.CellWidth * fit.Cols - 1;

            // The head letters which page this came off — the same stub the book's fore-edge shows, so
            // the note reads as torn from TASKS rather than as a second, separate thing.
            AddText(_noteRect, NotebookContent.LabelQuests, textX, 2, textW, NotebookInk.InkFaint);

            for (int r = 0; r < fit.Lines; r++)
            {
                int rowY = NotebookKit.HeadHeight + r * NotebookKit.Pitch;

                // Every ruled line is printed whether or not anything sits on it — it is stock, not
                // decoration that follows the text (the book's own rule).
                AddPiece(_noteRect, "Rule", null, NotebookKit.RuleInset, rowY + NotebookKit.RuleRow,
                         fit.WidthPx - 2 * NotebookKit.RuleInset, 1, NotebookInk.Rule);

                if (r >= rows.Count) continue;

                // The current-step arrow, on the first line only: one note, one step, and that arrow is
                // what says "this is the live one" everywhere else in the book.
                if (r == 0)
                {
                    AddPiece(_noteRect, "Current", _currentMark, NotebookKit.PadL + 2, rowY, 8, 8,
                             NotebookInk.Gold);
                }

                AddText(_noteRect, rows[r], textX, rowY, textW, NotebookInk.Ink);
            }
        }

        /// <summary>
        /// A nudge too long for the note loses its tail. Said once per offending line rather than per
        /// rebuild — a resize would otherwise repeat it forever — but never swallowed: a hint whose last
        /// words vanish is indistinguishable from a hint that was written badly, and only the writing
        /// lane can fix it.
        /// </summary>
        private void ReportOverflow(in QuestPanelFit fit)
        {
            if (!fit.Overflowed) { _overflowReported = false; return; }
            if (_overflowReported) return;

            _overflowReported = true;
            Debug.LogWarning("[QuestPanel] A quest line needs more than " + QuestPanelLayout.MaxLines +
                             " lines at " + QuestPanelLayout.Cols + " columns, so its tail is not " +
                             "drawn: " + _text);
        }

        private void ClearNote()
        {
            for (int i = _noteRect.childCount - 1; i >= 0; i--)
            {
                GameObject child = _noteRect.GetChild(i).gameObject;
                // Detached and deactivated before Destroy, which is deferred to the end of the frame —
                // otherwise the old note would still be drawing under the new one for a frame. The
                // notebook's own Rebuild does this, for exactly the same reason.
                child.transform.SetParent(null, false);
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        private void EnsureArt()
        {
            if (_artResolved) return;
            _artResolved = true;

            // The book's own loaders, so the note cannot end up on a different bake of the same kit.
            if (_stock == null) _stock = NotebookPresenter.LoadPiece(NotebookKit.ShippedStock);
            if (_currentMark == null) _currentMark = NotebookPresenter.LoadPiece(NotebookKit.ShippedCurrentMark);
            if (_face == null) _face = NotebookPresenter.LoadFace();
        }

        private void EnsureCanvas()
        {
            if (_noteRect != null) return;

            var canvasGo = new GameObject("QuestNoteCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            // ConstantPixelSize, like the book: the note is drawn in whole kit pixels at a whole scale,
            // and a scaler that resampled it would undo the very thing that keeps the face crisp.
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var note = new GameObject("QuestNote", typeof(RectTransform));
            _noteRect = note.GetComponent<RectTransform>();
            _noteRect.SetParent(canvasGo.GetComponent<RectTransform>(), false);

            // Anchored AND pivoted lower-right, so the note grows up and left out of the corner it is
            // pinned to and never has to be re-positioned when its line count changes.
            _noteRect.anchorMin = new Vector2(1f, 0f);
            _noteRect.anchorMax = new Vector2(1f, 0f);
            _noteRect.pivot = new Vector2(1f, 0f);
        }

        // ---- primitives, the book's (top-left origin, kit pixels) ----------------------------------

        private static RectTransform AddPiece(RectTransform parent, string name, Sprite sprite,
                                              int x, int y, int w, int h, Color fallbackTint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackTint;
            image.raycastTarget = false;   // a note is read, never pressed

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

            // Only meaningful on the FALLBACK — a bitmap font ignores fontSize entirely. The kit's face
            // sets at its baked pixel size and the whole note is scaled by Fit.Scale instead.
            text.fontSize = NotebookKit.GlyphHeight;
        }

        private static Font FallbackFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
