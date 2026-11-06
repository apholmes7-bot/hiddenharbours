using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HiddenHarbours.UI
{
    /// <summary>A notebook view of the existing model. Focus determines the page; the live player is the picture.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCreatorPanel : MonoBehaviour
    {
        private const int Columns = 16;
        private const int Lines = 16;
        private const int Scale = DialogueBubbleKit.ArtScale;
        private const int PageCount = 5;
        private const int KeyWidth = 24;
        private const int KeyHeight = 13;
        private const int KeyGap = 1;
        private const float ScreenMargin = 24f;
        private CharacterCreatorModel _model;
        private GameObject _root, _thoughtAnchor;
        private RectTransform _book, _keyboardRoot, _previous, _next, _leave, _previousLeaf, _nextLeaf;
        private RectTransform[] _tabs, _rows, _keyRects;
        private Image[] _tabImages, _rowImages, _cursors, _keyImages;
        private Text[] _rowLabels, _keyLabels;
        private Text _guide, _heading, _hint, _pageNumber;
        private Font _face;
        private Sprite _stock, _selection, _cursor, _tab;
        private Keyboard _keyboard;
        private int _visibleLeaf, _leafCount;
        private readonly List<int> _leafStarts = new List<int>();
        private readonly List<Renderer> _figureRenderers = new List<Renderer>(16);
        private readonly List<Canvas> _hudCanvases = new List<Canvas>(4);
        private readonly List<bool> _hudVisibility = new List<bool>(4);
        private Canvas _canvas;
        private Camera _camera;
        private float _figureHeightPixels;
        private Renderer _tallestRenderer;
        private bool _layoutValid;

        public bool Open(CharacterCreatorModel model)
        {
            if (model == null || !model.IsOpen || (_model != null && _model.IsOpen)
                || InteractionGate.IsBlocked || MoveActionClaim.IsClaimed || CancelKeyClaim.IsHeld) return false;
            _model = model;
            InteractionGate.IsBlocked = true;
            MoveActionClaim.IsClaimed = true;
            CancelKeyClaim.Hold();
            Build();
            HideHud();
            BindKeyboard();
            _model.Changed += Refresh;
            Refresh();
            // Core owns the speaker location and the existing speech presenter owns its kit and dwell.
            // A private child anchor also lets closing this view cancel a still-visible thought.
            Transform player = GameServices.PlayerTransform;
            if (player != null)
            {
                _thoughtAnchor = new GameObject("CreatorThought");
                _thoughtAnchor.transform.SetParent(player, false);
                EventBus.Publish(new AmbientSpeechRequested(AmbientSpeechId.Next(), _thoughtAnchor.transform,
                    Vector3.zero, null, null, CharacterCreatorViewStrings.OpeningThought, null,
                    AmbientSpeechKind.InnerVoice));
            }
            return true;
        }

        private void OnDisable() => _model?.Cancel();
        private void OnDestroy()
        {
            _model?.Cancel();
            UnbindKeyboard();
            if (_model != null) _model.Changed -= Refresh;
            RestoreHud();
            if (_thoughtAnchor != null) Destroy(_thoughtAnchor);
            if (_root != null) Destroy(_root);
        }

        private void BindKeyboard()
        {
            if (ReferenceEquals(_keyboard, Keyboard.current)) return;
            UnbindKeyboard();
            _keyboard = Keyboard.current;
            if (_keyboard != null) _keyboard.onTextInput += Type;
        }
        private void UnbindKeyboard()
        {
            if (_keyboard != null) _keyboard.onTextInput -= Type;
            _keyboard = null;
        }
        private void Type(char character) => _model?.Type(character);

        private void Update()
        {
            if (_model == null || !_model.IsOpen || !_layoutValid) return;
            BindKeyboard();
            Keyboard kb = _keyboard;
            Gamepad pad = Gamepad.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
            { Turn(kb.shiftKey.isPressed ? -1 : 1); return; }
            if (pad != null && (pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame))
            { Turn(pad.leftShoulder.wasPressedThisFrame ? -1 : 1); return; }
            float x = 0f, y = 0f;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed) x--;
                if (kb.rightArrowKey.isPressed) x++;
                if (kb.upArrowKey.isPressed) y++;
                if (kb.downArrowKey.isPressed) y--;
                if (_model.NameEditing && kb.backspaceKey.wasPressedThisFrame) _model.Backspace();
                if (_model.NameEditing && kb.enterKey.wasPressedThisFrame) { _model.Back(); return; }
            }
            if (pad != null && x == 0f && y == 0f)
            {
                Vector2 axis = pad.dpad.ReadValue();
                if (axis.sqrMagnitude < WardrobePickerModel.AxisThreshold * WardrobePickerModel.AxisThreshold)
                    axis = pad.leftStick.ReadValue();
                x = axis.x; y = axis.y;
                if (_model.NameEditing && pad.startButton.wasPressedThisFrame) { _model.Back(); return; }
            }
            _model.Input(x, y,
                (kb != null && kb.enterKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame),
                (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame));
            ReadMouse();
        }

        private void HideHud()
        {
            HudController hud = FindAnyObjectByType<HudController>();
            if (hud == null) return;
            hud.GetComponentsInChildren(true, _hudCanvases);
            foreach (Canvas canvas in _hudCanvases)
            {
                _hudVisibility.Add(canvas.enabled);
                canvas.enabled = false;
            }
        }

        private void RestoreHud()
        {
            for (int i = 0; i < _hudCanvases.Count; i++)
                if (_hudCanvases[i] != null) _hudCanvases[i].enabled = _hudVisibility[i];
            _hudCanvases.Clear();
            _hudVisibility.Clear();
        }

        private void LateUpdate()
        {
            if (_model == null || !_model.IsOpen) return;
            if (_camera == null) _camera = Camera.main;
            Transform player = GameServices.PlayerTransform;
            if (_camera == null || player == null) return;

            // The preserved body silhouette remains measurable when the mesh replaces its drawing.
            // A cached result list avoids an array allocation on each pose; no feature type is read.
            player.GetComponentsInChildren(true, _figureRenderers);
            float height = 0f;
            _tallestRenderer = null;
            foreach (Renderer renderer in _figureRenderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
                // Temporary engine geometry is not an authored part of the person.
                if ((renderer.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;
                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min, max = bounds.max;
                float low = float.PositiveInfinity, high = float.NegativeInfinity;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = _camera.WorldToScreenPoint(new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z));
                    if (point.z <= 0f) continue;
                    low = Mathf.Min(low, point.y);
                    high = Mathf.Max(high, point.y);
                }
                float projectedHeight = high - low;
                // A person on this screen cannot be taller than its entire viewport.
                if (projectedHeight > height && projectedHeight <= _camera.pixelHeight)
                {
                    height = projectedHeight;
                    _tallestRenderer = renderer;
                }
            }
            if (height <= 0f)
            {
                _figureHeightPixels = 0f;
                _layoutValid = false;
                _book.gameObject.SetActive(false);
                return;
            }
            // Each pose replaces the last measurement; one tall pass must not shrink later pages.
            _figureHeightPixels = height;
            Vector3 screen = _camera.WorldToScreenPoint(player.position);
            if (_figureHeightPixels <= 0f || screen.z <= 0f) return;
            RectTransform frame = (RectTransform)_root.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screen, null, out Vector2 local);
            float factor = _canvas.scaleFactor;
            float margin = ScreenMargin / factor;
            float safeLeft = Mathf.Max(frame.rect.width * 0.5f + margin,
                local.x + frame.rect.width * 0.5f + _figureHeightPixels / factor);
            int pageWidth = NotebookKit.PageWidthFor(Columns);
            int bookWidth = NotebookKit.SpreadWidthFor(Columns);
            int overhang = NotebookKit.CoverPad + NotebookKit.Edge + pageWidth * 2
                + NotebookKit.Gutter + NotebookKit.TabChipWidth - bookWidth;
            int bookHeight = NotebookKit.SpreadHeightFor(Lines);
            const int head = 34;
            int bottom = bookHeight + 4 + (KeyHeight + KeyGap)
                * (CharacterCreatorModel.KeyboardKeyCount / CharacterCreatorModel.KeyboardColumns) + 4;
            int scale = Mathf.Min(Scale, Mathf.FloorToInt(Mathf.Min(
                (frame.rect.width - margin - safeLeft) / (bookWidth + overhang),
                (frame.rect.height - margin * 2f) / (bottom + head))));
            _layoutValid = scale >= 1;
            _book.gameObject.SetActive(_layoutValid);
            if (!_layoutValid) return;
            float top = (frame.rect.height - (bottom + head) * scale) * 0.5f + head * scale;
            _book.localScale = Vector3.one * scale;
            _book.anchoredPosition = new Vector2(-margin - overhang * scale, -top);
        }

        // Presentation navigation only: every destination is a row already owned by the model.
        private int Page => PageFor(_model.FocusRow, _model.OutfitRow);
        public static int PageFor(int row, int outfitRow) => row == 0 ? 0 : row < outfitRow ? 1
            : row == outfitRow ? 2 : row == outfitRow + 1 ? 3 : 4;
        private int FirstRow(int page) => page == 0 ? 0 : page == 1 ? 1 : page == 2 ? _model.OutfitRow
            : page == 3 ? _model.NameRow : _model.ResetRow;
        private void ChoosePage(int page)
        {
            if (page < 0 || page >= PageCount || (page == 1 && _model.OutfitRow == 1)) return;
            if (_model.NameEditing) _model.Back();
            _model.Focus(FirstRow(page));
        }
        private void Turn(int direction)
        {
            int page = Page + direction;
            if (page == 1 && _model.OutfitRow == 1) page += direction;
            ChoosePage(page);
        }
        private static bool Hit(RectTransform rect, Vector2 point) => rect != null && rect.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(rect, point);
        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !_model.IsOpen || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 point = mouse.position.ReadValue();
            for (int i = 0; i < _tabs.Length; i++)
                if (Hit(_tabs[i], point)) { ChoosePage(i); return; }
            if (Hit(_leave, point)) { _model.Cancel(); return; }
            if (Hit(_previous, point)) { Turn(-1); return; }
            if (Hit(_next, point)) { Turn(1); return; }
            if (Hit(_previousLeaf, point)) { _model.Focus(_leafStarts[_visibleLeaf - 1]); return; }
            if (Hit(_nextLeaf, point)) { _model.Focus(_leafStarts[_visibleLeaf + 1]); return; }
            if (_model.NameEditing)
            {
                for (int i = 0; i < _keyRects.Length; i++)
                    if (Hit(_keyRects[i], point)) { _model.PressKey(i); return; }
                return;
            }
            for (int i = 0; i < _rows.Length; i++)
                if (Hit(_rows[i], point))
                {
                    // Focus can redraw the page, so measure the click before notifying the model.
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_rows[i], point, null, out Vector2 local);
                    int direction = local.x < _rows[i].rect.width * 0.5f ? -1 : 1;
                    _model.Focus(i);
                    if (i <= _model.OutfitRow) _model.Adjust(direction); else _model.Activate();
                    return;
                }
        }

        private void Refresh()
        {
            if (!_model.IsOpen)
            {
                _model.Changed -= Refresh;
                UnbindKeyboard();
                RestoreHud();
                _root.SetActive(false);
                if (_thoughtAnchor != null) Destroy(_thoughtAnchor);
                InteractionGate.IsBlocked = false;
                MoveActionClaim.IsClaimed = false;
                CancelKeyClaim.Release();
                return;
            }
            EnsureRows();
            int page = Page;
            for (int i = 0; i < _tabs.Length; i++)
            {
                bool available = i != 1 || _model.OutfitRow > 1;
                _tabImages[i].color = !available ? NotebookInk.InkFaint : i == page ? NotebookInk.Gold : Color.white;
            }
            _heading.text = CharacterCreatorViewStrings.Pages[page];
            _guide.text = Guide(page);
            _guide.color = _model.Refusal == null ? NotebookInk.Ink : NotebookInk.InkStrong;
            LayoutChoices(page);
            _pageNumber.text = CharacterCreatorViewStrings.Step + (page + 1) + "/" + PageCount
                + (_leafCount > 1 ? "   " + (_visibleLeaf + 1) + "/" + _leafCount : "");
            _previous.gameObject.SetActive(page > 0 && !_model.NameEditing);
            _next.gameObject.SetActive(page < PageCount - 1 && !_model.NameEditing);
            _leave.gameObject.SetActive(!_model.NameEditing);
            _previousLeaf.gameObject.SetActive(_visibleLeaf > 0);
            _nextLeaf.gameObject.SetActive(_visibleLeaf + 1 < _leafCount);
            _keyboardRoot.gameObject.SetActive(_model.NameEditing);
            for (int i = 0; i < _keyLabels.Length; i++)
            {
                _keyLabels[i].text = _model.KeyLabel(i);
                _keyImages[i].color = i == _model.KeyboardIndex ? NotebookInk.Gold : Color.white;
            }
            _hint.text = Wrapped(_model.NameEditing ? CharacterCreatorViewStrings.NameHint : CharacterCreatorViewStrings.Hint,
                (NotebookKit.SpreadWidthFor(Columns) - NotebookKit.PadL * 2) / NotebookKit.CellWidth);
        }

        private string Guide(int page)
        {
            if (_model.Refusal != null) return Wrapped(CharacterCreatorViewStrings.Refused + _model.Refusal, Columns);
            if (page == 3)
            {
                int cap = GameServices.PlayerNameMaxLength;
                return Wrapped(CharacterCreatorViewStrings.Guides[page] + "\n\n" + CharacterCreatorViewStrings.NameLimit
                    + cap + "\n" + CharacterCreatorViewStrings.NameLength + _model.Name.Trim().Length, Columns);
            }
            if (page == 4) return Wrapped(_model.RowLabel(0) + "\n\n" + _model.RowLabel(_model.OutfitRow)
                + "\n\n" + _model.RowLabel(_model.NameRow) + "\n\n" + CharacterCreatorViewStrings.Guides[page], Columns);
            return Wrapped(CharacterCreatorViewStrings.Guides[page], Columns);
        }
        private static string Wrapped(string copy, int columns)
        {
            // Explicit paragraphs are separate blocks; wrapping never invents a smaller font.
            string[] paragraphs = copy.Split('\n');
            var lines = new List<string>();
            foreach (string paragraph in paragraphs) lines.AddRange(NotebookLayout.Wrap(paragraph, columns));
            return string.Join("\n", lines);
        }
        private string ChoiceLabel(int row) => row == _model.ResetRow ? CharacterCreatorViewStrings.Reset
            : row == _model.CancelRow ? CharacterCreatorViewStrings.Cancel
            : row == _model.ConfirmRow ? CharacterCreatorViewStrings.Confirm : _model.RowLabel(row);

        private void LayoutChoices(int page)
        {
            var blocks = new List<NotebookBlock>();
            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i].gameObject.SetActive(false);
                if (PageFor(i, _model.OutfitRow) != page) continue;
                var rows = new List<NotebookRow>();
                foreach (string line in NotebookLayout.Wrap(ChoiceLabel(i), Columns))
                    rows.Add(new NotebookRow(line, true, false, NotebookStepState.Todo, false));
                // The last ruled line holds the two visible change arrows, or breathing room for an action.
                rows.Add(new NotebookRow(i <= _model.OutfitRow ? CharacterCreatorViewStrings.ChangeArrows : "",
                    true, false, NotebookStepState.Todo, false));
                blocks.Add(new NotebookBlock(NotebookBlockKind.Note, rows, i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            List<NotebookSpread> spreads = NotebookLayout.Paginate(blocks, Columns, Lines);
            _leafCount = 0; _visibleLeaf = 0; _leafStarts.Clear();
            NotebookLeaf selected = null;
            foreach (NotebookSpread spread in spreads)
            {
                FindLeaf(spread.Left, ref selected);
                FindLeaf(spread.Right, ref selected);
            }
            if (selected == null && spreads.Count > 0) selected = spreads[0].Left;
            if (selected == null) return;
            int width = NotebookKit.PageWidthFor(Columns) - NotebookKit.PadL - NotebookKit.PadR;
            foreach (NotebookPlacement placement in selected.Placements)
            {
                int row = int.Parse(placement.Block.SourceId, System.Globalization.CultureInfo.InvariantCulture);
                RectTransform rect = _rows[row];
                int height = placement.RowCount * NotebookKit.Pitch;
                Place(rect, NotebookKit.PadL, NotebookKit.HeadHeight + placement.Row * NotebookKit.Pitch, width, height);
                rect.gameObject.SetActive(true);
                var copy = new System.Text.StringBuilder();
                for (int k = 0; k < placement.RowCount; k++)
                {
                    if (k > 0) copy.Append('\n');
                    copy.Append(placement.Block.Rows[placement.FromRow + k].Text);
                }
                _rowLabels[row].text = copy.ToString();
                Place(_rowLabels[row].rectTransform, NotebookKit.CursorLane, 0, width - NotebookKit.CursorLane, height);
                bool focused = row == _model.FocusRow;
                _rowImages[row].enabled = focused;
                _cursors[row].enabled = focused;
            }
        }
        private void FindLeaf(NotebookLeaf leaf, ref NotebookLeaf selected)
        {
            if (leaf.Used == 0) return;
            _leafStarts.Add(int.Parse(leaf.Placements[0].Block.SourceId, System.Globalization.CultureInfo.InvariantCulture));
            foreach (NotebookPlacement placement in leaf.Placements)
                if (int.Parse(placement.Block.SourceId, System.Globalization.CultureInfo.InvariantCulture) == _model.FocusRow)
                { selected = leaf; _visibleLeaf = _leafCount; break; }
            _leafCount++;
        }

        private void Build()
        {
            _face = Resources.Load<Font>(HarbourType.ResourceKey);
            if (_face == null) _face = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _stock = Piece(NotebookKit.ShippedStock); _selection = Piece(NotebookKit.ShippedSelect);
            _cursor = Piece(NotebookKit.PieceCursorHand); _tab = Piece(NotebookKit.ShippedTab);
            _root = new GameObject("CharacterCreator", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root.transform.SetParent(transform, false);
            Canvas canvas = _root.GetComponent<Canvas>();
            _canvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = WardrobePicker.SortingOrder;
            canvas.referencePixelsPerUnit = NotebookKit.PixelsPerUnit;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _book = Image(_root.transform, "Notebook", null, NotebookInk.Cover);
            _book.anchorMin = _book.anchorMax = Vector2.one;
            _book.pivot = Vector2.one;
            _book.sizeDelta = new Vector2(NotebookKit.SpreadWidthFor(Columns), NotebookKit.SpreadHeightFor(Lines));
            _book.localScale = Vector3.one * Scale;
            _book.gameObject.SetActive(false);
            int inset = NotebookKit.CoverPad + NotebookKit.Edge;
            int pageWidth = NotebookKit.PageWidthFor(Columns), pageHeight = NotebookKit.PageHeightFor(Lines);
            RectTransform left = Leaf("Guide", inset, inset, pageWidth, pageHeight);
            _heading = Label(left, "Step", "", NotebookKit.PadL, 2, pageWidth - NotebookKit.PadL * 2, NotebookKit.CellHeight);
            _guide = Label(left, "Explanation", "", NotebookKit.PadL + NotebookKit.CursorLane,
                NotebookKit.HeadHeight, Columns * NotebookKit.CellWidth, Lines * NotebookKit.Pitch);
            RectTransform right = Leaf("Choices", inset + pageWidth + NotebookKit.Gutter, inset, pageWidth, pageHeight);
            _pageNumber = Label(right, "Progress", "", NotebookKit.PadL, 2, pageWidth - NotebookKit.PadL * 2, NotebookKit.CellHeight);
            _previousLeaf = Image(right, "PreviousChoices", Piece(NotebookKit.PieceMarkPrev), NotebookInk.Ink);
            Place(_previousLeaf, NotebookKit.PadL, pageHeight - NotebookKit.PadB, 5, 7);
            _nextLeaf = Image(right, "MoreChoices", Piece(NotebookKit.PieceMarkNext), NotebookInk.Ink);
            Place(_nextLeaf, pageWidth - NotebookKit.PadR - 5, pageHeight - NotebookKit.PadB, 5, 7);
            _tabs = new RectTransform[PageCount]; _tabImages = new Image[PageCount];
            int tabX = inset + pageWidth * 2 + NotebookKit.Gutter;
            for (int i = 0; i < PageCount; i++)
            {
                _tabs[i] = Image(_book, "Step" + i, _tab, NotebookInk.Tab);
                _tabImages[i] = _tabs[i].GetComponent<Image>();
                Place(_tabs[i], tabX, inset + NotebookKit.TabTop + i * (NotebookKit.TabHeight + NotebookKit.TabGap),
                    NotebookKit.TabChipWidth, NotebookKit.TabHeight);
                Label(_tabs[i], "Label", CharacterCreatorViewStrings.Tabs[i], NotebookKit.TabPad, 2,
                    NotebookKit.TabChipWidth - NotebookKit.TabPad * 2, NotebookKit.CellHeight);
            }
            RectTransform ribbon = Image(_book, "Ribbon", Piece(NotebookKit.PieceRibbon), NotebookInk.Gold);
            Place(ribbon, tabX - NotebookKit.RibbonWidth - 1, inset, NotebookKit.RibbonWidth, 26);
            RectTransform controls = Image(_book, "ControlsStock", _stock, NotebookInk.Paper);
            Place(controls, 0, -34, NotebookKit.SpreadWidthFor(Columns), NotebookKit.CellHeight * 3);
            _hint = Label(controls, "Controls", "", NotebookKit.PadL, 0,
                NotebookKit.SpreadWidthFor(Columns) - NotebookKit.PadL * 2, NotebookKit.CellHeight * 3);
            int bottom = NotebookKit.SpreadHeightFor(Lines);
            _previous = Button(_book, "PreviousStep", CharacterCreatorViewStrings.Previous, 0, bottom + 4, 55);
            _next = Button(_book, "NextStep", CharacterCreatorViewStrings.Next, 100, bottom + 4, 55);
            _leave = Button(_book, "Leave", CharacterCreatorViewStrings.Leave, 0, bottom + 19, 155);
            _keyboardRoot = Image(_book, "NameKeyboard", null, NotebookInk.Cover);
            Place(_keyboardRoot, 0, bottom + 4, _book.sizeDelta.x, (KeyHeight + KeyGap) * 4 + 4);
            _keyRects = new RectTransform[CharacterCreatorModel.KeyboardKeyCount];
            _keyImages = new Image[_keyRects.Length]; _keyLabels = new Text[_keyRects.Length];
            for (int i = 0; i < _keyRects.Length; i++)
            {
                _keyRects[i] = Image(_keyboardRoot, "Key" + i, _selection, NotebookInk.Paper);
                _keyImages[i] = _keyRects[i].GetComponent<Image>();
                Place(_keyRects[i], 4 + i % CharacterCreatorModel.KeyboardColumns * (KeyWidth + KeyGap),
                    2 + i / CharacterCreatorModel.KeyboardColumns * (KeyHeight + KeyGap), KeyWidth, KeyHeight);
                _keyLabels[i] = Label(_keyRects[i], "Label", "", 0, 1, KeyWidth, KeyHeight);
                _keyLabels[i].alignment = TextAnchor.MiddleCenter;
            }
            _choiceParent = right;
        }
        private RectTransform _choiceParent;
        private void EnsureRows()
        {
            if (_rows != null && _rows.Length == _model.RowCount) return;
            if (_rows != null) foreach (RectTransform row in _rows)
            { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            int count = _model.RowCount;
            _rows = new RectTransform[count]; _rowImages = new Image[count];
            _rowLabels = new Text[count]; _cursors = new Image[count];
            for (int i = 0; i < count; i++)
            {
                _rows[i] = Image(_choiceParent, "Choice" + i, _selection, NotebookInk.Gold);
                _rowImages[i] = _rows[i].GetComponent<Image>();
                _rowLabels[i] = Label(_rows[i], "Label", "", 0, 0, 1, 1);
                RectTransform cursor = Image(_rows[i], "Cursor", _cursor, NotebookInk.InkStrong);
                Place(cursor, 1, 0, NotebookKit.CursorLane - 2, NotebookKit.CellHeight);
                _cursors[i] = cursor.GetComponent<Image>();
            }
        }
        private RectTransform Leaf(string name, int x, int y, int width, int height)
        {
            RectTransform leaf = Image(_book, name, _stock, NotebookInk.Paper);
            Place(leaf, x, y, width, height);
            for (int i = 0; i < Lines; i++)
            {
                RectTransform rule = Image(leaf, "Rule", null, NotebookInk.Rule);
                Place(rule, NotebookKit.RuleInset, NotebookKit.HeadHeight + i * NotebookKit.Pitch + NotebookKit.RuleRow,
                    width - NotebookKit.RuleInset * 2, 1);
            }
            RectTransform margin = Image(leaf, "Margin", null, NotebookInk.Rule);
            Place(margin, NotebookKit.PadL + NotebookKit.CursorLane - 1, NotebookKit.HeadHeight, 1, Lines * NotebookKit.Pitch);
            return leaf;
        }
        private RectTransform Button(Transform parent, string name, string copy, int x, int y, int width)
        {
            RectTransform rect = Image(parent, name, _tab, NotebookInk.Paper);
            Place(rect, x, y, width, NotebookKit.TabHeight);
            Label(rect, "Label", copy, 2, 1, width - 4, NotebookKit.CellHeight);
            return rect;
        }
        private static Sprite Piece(string name) => Resources.Load<Sprite>(NotebookKit.ResourceKeyFor(name));
        private static RectTransform Image(Transform parent, string name, Sprite sprite, Color fallback)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallback;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }
        private Text Label(Transform parent, string name, string copy, int x, int y, int width, int height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = _face; text.fontSize = NotebookKit.GlyphHeight;
            text.text = copy; text.color = NotebookInk.Ink;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Place(text.rectTransform, x, y, width, height);
            return text;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }

    // View-only copy stays together, following WardrobeStrings; choice labels still come from the model.
    internal static class CharacterCreatorViewStrings
    {
        public static readonly string[] Pages = { "Your body", "Your colours", "Your outfit", "Your name", "Ready to go?" };
        public static readonly string[] Tabs = { "BODY", "TINTS", "WEAR", "NAME", "READY" };
        public static readonly string[] Guides =
        {
            "Make yourself at home.\n\nChoose a body. Watch yourself aboard as each choice changes.",
            "A little colour.\n\nChange the marked part. Up and down reaches every colour this body offers.",
            "What to wear.\n\nChoose a starter outfit. Your own figure aboard is the preview.",
            "A name, if you like.\n\nSelect the name to type or use the keys. Blank is fine.",
            "Keep these choices to begin. Leaving keeps what you arrived with."
        };
        public const string OpeningThought = "New waters. A moment to settle in.";
        public const string Hint = "Up/down: choose   Left/right: change\nEnter/A: select   Esc/B: leave\nTab/shoulders: turn step";
        public const string NameHint = "Type, or arrows/stick and A: keys\nEnter/Start or Done: finish name\nEsc/B: finish name; again: leave";
        public const string ChangeArrows = "< Prev   Next >";
        public const string Step = "Step ";
        public const string NameLimit = "Name limit: ";
        public const string NameLength = "Name length: ";
        public const string Refused = "Could not keep that choice.\n\n";
        public const string Previous = "< Back";
        public const string Next = "Next >";
        public const string Leave = "Leave as I was";
        public const string Reset = "Reset choices";
        public const string Cancel = "Leave as I was";
        public const string Confirm = "Keep these choices";
    }
}
