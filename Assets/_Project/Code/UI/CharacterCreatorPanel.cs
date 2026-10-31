using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HiddenHarbours.UI
{
    /// <summary>Runtime-built HUD view. The model owns choices, focus, keyboard and input latches.
    /// Phase B opens this view aboard; nothing installs or opens it automatically.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCreatorPanel : MonoBehaviour
    {
        private const float PanelWidth = 470f;
        private const float PanelHeight = 670f;
        private const float RowHeight = 32f;
        private const float KeyWidth = 52f;
        private const float KeyHeight = 42f;
        private const float Padding = 18f;
        private const int CopySize = 17;
        private static readonly Color Stock = new Color(0.941f, 0.925f, 0.878f, 0.98f);
        private static readonly Color Ink = new Color(0.10f, 0.16f, 0.16f, 1f);
        private static readonly Color Selected = new Color(0.72f, 0.83f, 0.79f, 1f);
        private CharacterCreatorModel _model;
        private GameObject _root;
        private GameObject _keyboardRoot;
        private Text[] _rows;
        private Text[] _keys;
        private Text _status;
        private Text _hint;
        private Keyboard _keyboard;

        public bool Open(CharacterCreatorModel model)
        {
            if (model == null || !model.IsOpen || (_model != null && _model.IsOpen)
                || InteractionGate.IsBlocked || MoveActionClaim.IsClaimed || CancelKeyClaim.IsHeld) return false;
            _model = model;
            InteractionGate.IsBlocked = true;
            MoveActionClaim.IsClaimed = true;
            CancelKeyClaim.Hold();
            Build();
            BindKeyboard();
            _model.Changed += Refresh;
            Refresh();
            return true;
        }

        private void OnDisable() => _model?.Cancel();

        private void OnDestroy()
        {
            _model?.Cancel();
            UnbindKeyboard();
            if (_model != null) _model.Changed -= Refresh;
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
            if (_model == null || !_model.IsOpen) return;
            BindKeyboard();
            Keyboard kb = _keyboard;
            Gamepad pad = Gamepad.current;
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
                x = axis.x;
                y = axis.y;
                if (_model.NameEditing && pad.startButton.wasPressedThisFrame) { _model.Back(); return; }
            }
            bool confirm = (kb != null && kb.enterKey.wasPressedThisFrame)
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame)
                || (pad != null && pad.buttonEast.wasPressedThisFrame);
            _model.Input(x, y, confirm, cancel);
            ReadMouse();
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !_model.IsOpen) return;
            Vector2 point = mouse.position.ReadValue();
            if (_model.NameEditing)
            {
                if (!mouse.leftButton.wasPressedThisFrame) return;
                for (int i = 0; i < _keys.Length; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_keys[i].rectTransform, point))
                    { _model.PressKey(i); return; }
                return;
            }
            if (mouse.delta.ReadValue().sqrMagnitude == 0f && !mouse.leftButton.wasPressedThisFrame) return;
            for (int i = 0; i < _rows.Length; i++)
            {
                RectTransform rect = _rows[i].rectTransform;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, point)) continue;
                _model.Focus(i);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (i <= _model.OutfitRow)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, point, null, out Vector2 local);
                        _model.Adjust(local.x < 0f ? -1 : 1);
                    }
                    else _model.Activate();
                }
                return;
            }
        }

        private void Refresh()
        {
            if (!_model.IsOpen)
            {
                _model.Changed -= Refresh;
                UnbindKeyboard();
                _root.SetActive(false);
                InteractionGate.IsBlocked = false;
                MoveActionClaim.IsClaimed = false;
                CancelKeyClaim.Release();
                return;
            }
            if (_rows.Length != _model.RowCount) Build();
            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i].text = (i <= _model.OutfitRow ? "<  " : "") + _model.RowLabel(i)
                    + (i <= _model.OutfitRow ? "  >" : "");
                _rows[i].transform.parent.GetComponent<Image>().color = i == _model.FocusRow ? Selected : Stock;
            }
            _keyboardRoot.SetActive(_model.NameEditing);
            for (int i = 0; i < _keys.Length; i++)
            {
                _keys[i].text = _model.KeyLabel(i);
                _keys[i].transform.parent.GetComponent<Image>().color = i == _model.KeyboardIndex ? Selected : Stock;
            }
            _hint.text = _model.NameEditing ? CharacterCreatorStrings.NameHint : CharacterCreatorStrings.Hint;
            _status.text = _model.Refusal ?? string.Join("\n", _model.UnavailableBodies);
        }

        private void Build()
        {
            if (_root != null) Destroy(_root);
            _root = new GameObject("CharacterCreator", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root.transform.SetParent(transform, false);
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = WardrobePicker.SortingOrder;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panel = Box(_root.transform, "Choices", Stock);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-Padding, 0f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            Label(panel, "Title", CharacterCreatorStrings.Title, Padding, Padding, PanelWidth - Padding * 2f, RowHeight);
            _rows = new Text[_model.RowCount];
            for (int i = 0; i < _rows.Length; i++)
            {
                RectTransform row = Box(panel, "Choice" + i, Stock);
                Place(row, Padding, Padding + RowHeight * (i + 1), PanelWidth - Padding * 2f, RowHeight);
                _rows[i] = Label(row, "Label", "", 0f, 0f, row.sizeDelta.x, RowHeight);
            }
            _status = Label(panel, "Refusal", "", Padding, Padding + RowHeight * (_rows.Length + 1),
                PanelWidth - Padding * 2f, RowHeight * 4f);
            _status.fontSize = CopySize - 3;
            _status.alignment = TextAnchor.UpperLeft;
            _hint = Label(panel, "Controls", "", Padding, PanelHeight - RowHeight * 2f - Padding,
                PanelWidth - Padding * 2f, RowHeight * 2f);
            _hint.fontSize = CopySize - 3;

            RectTransform keyboard = Box(_root.transform, "NameKeyboard", Stock);
            keyboard.anchorMin = keyboard.anchorMax = new Vector2(0f, 0f);
            keyboard.pivot = Vector2.zero;
            keyboard.anchoredPosition = new Vector2(Padding, Padding);
            keyboard.sizeDelta = new Vector2(KeyWidth * CharacterCreatorModel.KeyboardColumns + Padding * 2f,
                KeyHeight * (CharacterCreatorModel.KeyboardKeyCount / CharacterCreatorModel.KeyboardColumns) + Padding * 2f);
            _keyboardRoot = keyboard.gameObject;
            _keys = new Text[CharacterCreatorModel.KeyboardKeyCount];
            for (int i = 0; i < _keys.Length; i++)
            {
                RectTransform key = Box(keyboard, "Key" + i, Stock);
                Place(key, Padding + i % CharacterCreatorModel.KeyboardColumns * KeyWidth,
                    Padding + i / CharacterCreatorModel.KeyboardColumns * KeyHeight, KeyWidth, KeyHeight);
                _keys[i] = Label(key, "Label", "", 0f, 0f, KeyWidth, KeyHeight);
                _keys[i].alignment = TextAnchor.MiddleCenter;
            }
        }

        private static RectTransform Box(Transform parent, string label, Color colour)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        private static Text Label(Transform parent, string label, string copy, float x, float y, float width, float height)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = copy;
            text.fontSize = CopySize;
            text.color = Ink;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Place(text.rectTransform, x, y, width, height);
            return text;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x + width * 0.5f, -y - height * 0.5f);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
