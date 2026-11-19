using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The current quest, as a leaf off his notebook, pinned lower-right</b> — the owner's 2026-08-22
    /// diegetic ruling.
    ///
    /// <para>What it replaced: a bottom-centre banner of plain white outlined text
    /// (<see cref="OnboardingDirector"/>'s old hint). The words are unchanged and the source is
    /// unchanged — the director still decides which beat the player is on, and this only draws it. What
    /// changed is the voice: the same sentence in the notebook's stock, ink and face reads as something
    /// he wrote down, and a game whose main UI is a book (the 2026-08-20 ruling) should not also be
    /// shouting at the player in a system font.</para>
    ///
    /// <para><b>It folds, and it hides</b> (owner asks 2026-11-16: "a way to minimize hide the current
    /// tasks ui", then "hide it entirely"). Three modes (<see cref="TasksNoteMode"/>, Core's
    /// <see cref="TasksNoteFold"/>): shown; a chip in the same corner reading "TASKS 1"; or hidden, with
    /// nothing drawn at all. T or the pad's west button cycles them; a click on the head folds to the
    /// chip and a click on the chip opens it; the settings sheet sets any of the three. As a chip, a new or
    /// finished task flashes it gold for a moment (<see cref="QuestChipSignal"/>) and never opens the note
    /// by itself. Hidden, a task change is silent: no chip, no flash, no box stated, and the Notebook's
    /// TASKS page is where it is read. The mode is remembered as a setting, not in the save.</para>
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

        // The helm footprint the note is placed for (ADR 0050). A change re-places it; it never
        // rebuilds it — the note's size does not depend on where it stands.
        private HelmFootprintArea _placedFor;

        // The mode, as last DRAWN (the live one is TasksNoteFold.Mode; a difference is a rebuild).
        private TasksNoteMode _drawnMode;
        private bool _drawing;
        private readonly QuestChipSignal _signal = new QuestChipSignal();
        private Text _chipLabel;
        private Image _chipMark;
        private bool _chipLit;

        /// <summary>The line currently on the note, or null when nothing is showing.</summary>
        public string Text => _text;

        /// <summary>True when there is a current task (open note or folded chip).</summary>
        public bool IsShowing => !string.IsNullOrEmpty(_text);

        /// <summary>The current tasks the chip counts. The note carries one step at a time.</summary>
        public int TaskCount => IsShowing ? 1 : 0;

        /// <summary>True when the folded chip is on screen rather than the open note.</summary>
        public bool IsChipShowing => _drawing && _drawnMode == TasksNoteMode.Chip;

        /// <summary>True when the open note is on screen.</summary>
        public bool IsNoteShowing => _drawing && _drawnMode == TasksNoteMode.Shown;

        /// <summary>True when anything at all is drawn (note or chip). False while hidden.</summary>
        public bool IsDrawing => _drawing;

        /// <summary>The chip's flash, readable by a test or a proof.</summary>
        public QuestChipSignal Signal => _signal;

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
            if (QuestChipSignal.Calls(TasksNoteFold.Mode, _text, next)) _signal.Start(SignalSeconds);
            _text = next;
            Rebuild();
        }

        /// <summary>Take the note down.</summary>
        public void Hide() => Show(null);

        /// <summary>Re-read the mode and redraw if it changed — what LateUpdate does each frame, callable
        /// from a test or a proof without waiting for a frame.</summary>
        public void SyncFold()
        {
            TasksNoteMode mode = TasksNoteFold.Mode;
            if (mode == _drawnMode) return;
            // Opened, the note itself says what changed; hidden, nothing flashes. Only the chip flashes.
            if (mode != TasksNoteMode.Chip) _signal.Stop();
            Rebuild();
        }

        private static float SignalSeconds => GameServices.Config != null
            ? GameServices.Config.TasksChipSignalSeconds : GameConfig.DefaultTasksChipSignalSeconds;

        private static float BlinkSeconds => GameServices.Config != null
            ? GameServices.Config.TasksChipBlinkSeconds : GameConfig.DefaultTasksChipBlinkSeconds;

        private void LateUpdate()
        {
            // The mode first: one compare a frame, a rebuild only on the press.
            if (TasksNoteFold.Mode != _drawnMode) { SyncFold(); return; }

            // The chip's flash: a tint each frame it is lit or unlit, set only when that flips.
            if (_signal.Active)
            {
                _signal.Tick(Time.unscaledDeltaTime);
                if (!_signal.Active && !IsShowing) { Rebuild(); return; }   // the last task done: chip goes
                TintChip(false);
            }

            // A window resize re-fits the note. Read here rather than off a resize callback because
            // the screen (HudScreen: the player's own, unless a plate hands it another) is the only
            // honest source and this is two int compares.
            if (!_drawing) return;
            if (HudScreen.Width == _builtScreenW && HudScreen.Height == _builtScreenH)
            {
                // What the helm covers is read off the Core seam, never the helm's classes (rule 4).
                if (HelmFootprint.Current != _placedFor) Place();
                return;
            }
            Rebuild();
        }

        /// <summary>Stand the note in its corner, kept out of what the helm's UI covers. Nothing
        /// covered: the corner, exactly as it was built before the seam existed.</summary>
        private void Place()
        {
            HelmFootprintArea covered = HelmFootprint.Current;
            QuestPanelFit fit = Fit;
            _placedFor = covered;

            if (_drawnMode == TasksNoteMode.Chip)
            {
                // The chip: the note's own corner, the note's own scale. It states its box like the note
                // does, so the interaction prompt keeps off it too, and it is what a click opens.
                _noteRect.anchoredPosition = QuestPanelLayout.ChipAnchoredPosition(fit.Scale, in covered,
                                                                                   _builtScreenW, _builtScreenH);
                Rect chip = QuestPanelLayout.ChipScreenRect(fit.Scale, in covered, _builtScreenW, _builtScreenH);
                QuestNoteFootprint.Publish(chip);
                TasksNoteFold.SetClickTarget(chip);
                return;
            }

            _noteRect.anchoredPosition = QuestPanelLayout.AnchoredPosition(in fit, in covered,
                                                                           _builtScreenW, _builtScreenH);

            // Say where the note stands (Core seam), so the interaction prompt keeps off it without
            // UI and World knowing each other. Only reached on a change; a re-statement is dropped.
            QuestNoteFootprint.Publish(QuestPanelLayout.NoteScreenRect(in fit, in covered,
                                                                       _builtScreenW, _builtScreenH));
            // ...and where a click folds it: its head, never the step's words.
            TasksNoteFold.SetClickTarget(QuestPanelLayout.HeadScreenRect(in fit, in covered,
                                                                         _builtScreenW, _builtScreenH));
        }

        private void OnEnable()
        {
            if (_drawing && _noteRect != null) Place();
        }

        // The canvas hangs off this object, so a disabled or destroyed presenter draws no note.
        private void OnDisable()
        {
            QuestNoteFootprint.Clear();
            TasksNoteFold.SetClickTarget(default);
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        // ---- the drawing --------------------------------------------------------------------------

        private void Rebuild()
        {
            // Hidden draws nothing: no chip, no flash, no footprint, nothing to click.
            _drawnMode = TasksNoteFold.Mode;
            bool chip = _drawnMode == TasksNoteMode.Chip && (IsShowing || _signal.Active);
            bool note = _drawnMode == TasksNoteMode.Shown && IsShowing;
            _chipLabel = null;
            _chipMark = null;

            if (!chip && !note)
            {
                _drawing = false;
                if (_canvas != null) _canvas.gameObject.SetActive(false);
                QuestNoteFootprint.Clear();
                TasksNoteFold.SetClickTarget(default);
                return;
            }

            EnsureArt();
            EnsureCanvas();
            _canvas.gameObject.SetActive(true);
            _drawing = true;

            List<string> rows = QuestPanelLayout.Rows(_text);
            QuestPanelFit fit = QuestPanelLayout.Fit(rows.Count, HudScreen.Width, HudScreen.Height);
            Fit = fit;

            _builtScreenW = HudScreen.Width;
            _builtScreenH = HudScreen.Height;

            ClearNote();
            _noteRect.localScale = Vector3.one * fit.Scale;

            if (chip)
            {
                BuildChip();
                Place();
                return;
            }

            ReportOverflow(fit);

            _noteRect.sizeDelta = new Vector2(fit.WidthPx, fit.HeightPx);
            Place();

            // The leaf itself, then its printed furniture, then his hand on top of it.
            AddPiece(_noteRect, "Leaf", _stock, 0, 0, fit.WidthPx, fit.HeightPx, NotebookInk.Paper);

            int textX = NotebookKit.PadL + NotebookKit.CursorLane;
            int textW = NotebookKit.CellWidth * fit.Cols - 1;

            // The head letters which page this came off — the same stub the book's fore-edge shows, so
            // the note reads as torn from TASKS rather than as a second, separate thing.
            AddText(_noteRect, NotebookContent.LabelQuests, textX, 2, textW, NotebookInk.InkFaint);

            // The fold mark at the head's far end: the head is what a click folds, and this says so.
            AddText(_noteRect, WorldStrings.TasksFoldMark, textX + textW - NotebookKit.CellWidth, 2,
                    NotebookKit.CellWidth, NotebookInk.InkFaint);

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
        /// The note folded up to its head: the same leaf, the same TASKS lettering, with the count of
        /// current tasks after it, the current-step arrow in its lane, and the open mark at the far end.
        /// </summary>
        private void BuildChip()
        {
            int w = QuestPanelLayout.ChipWidthPx;
            int h = QuestPanelLayout.ChipHeightPx;
            _noteRect.sizeDelta = new Vector2(w, h);

            AddPiece(_noteRect, "Leaf", _stock, 0, 0, w, h, NotebookInk.Paper);

            int textX = NotebookKit.PadL + NotebookKit.CursorLane;
            int count = Mathf.Min(TaskCount, MaxChipCount);

            // Built on a rebuild only (a fold, a task change), never per frame.
            _chipMark = AddPiece(_noteRect, "Current", _currentMark, NotebookKit.PadL + 2, 2, 8, 8,
                                 NotebookInk.Gold).GetComponent<Image>();
            _chipLabel = AddText(_noteRect, NotebookContent.LabelQuests + " " + count, textX, 2,
                                 NotebookKit.CellWidth * (QuestPanelLayout.ChipCols - 2), NotebookInk.Ink);
            AddText(_noteRect, WorldStrings.TasksUnfoldMark,
                    textX + NotebookKit.CellWidth * (QuestPanelLayout.ChipCols - 1), 2, NotebookKit.CellWidth,
                    NotebookInk.InkFaint);

            TintChip(true);
        }

        /// <summary>The count is one digit on the chip (<see cref="QuestPanelLayout.ChipCols"/>).</summary>
        private const int MaxChipCount = 9;

        /// <summary>
        /// The flash: while the signal runs, the TASKS lettering goes gold and the arrow blinks with it,
        /// on and off; after it, plain ink. Touches the graphics only when lit/unlit flips, so a quiet
        /// frame costs a compare.
        /// </summary>
        private void TintChip(bool force)
        {
            if (_chipLabel == null) return;
            bool lit = _signal.Active && QuestPanelLayout.SignalLit(_signal.Elapsed, BlinkSeconds);
            bool markOn = !_signal.Active || lit;
            if (!force && lit == _chipLit && (_chipMark == null || _chipMark.enabled == markOn)) return;
            _chipLit = lit;
            _chipLabel.color = lit ? NotebookInk.Gold : NotebookInk.Ink;
            if (_chipMark != null) _chipMark.enabled = markOn;
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

        private Text AddText(RectTransform parent, string content, int x, int y, int w, Color ink)
        {
            if (string.IsNullOrEmpty(content)) return null;

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
            return text;
        }

        private static Font FallbackFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
