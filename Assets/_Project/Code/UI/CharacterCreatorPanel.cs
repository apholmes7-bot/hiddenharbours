using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace HiddenHarbours.UI
{
    /// <summary>A modal view of the live player, with notebook controls along the bottom.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class CharacterCreatorPanel : MonoBehaviour
    {
        private const int Scale = DialogueBubbleKit.ArtScale;
        private const int PageCount = 5;
        private const float Width = 1920f, Height = 1080f;
        private const float Margin = NotebookKit.CellHeight * Scale;
        private const float StripHeight = NotebookKit.Pitch * Scale * 9;
        private const float RowHeight = NotebookKit.TabHeight * Scale;
        private static readonly int FigureTexture = Shader.PropertyToID("_HHFigureScreenTex");
        private static readonly int FigureMap = Shader.PropertyToID("_HHFigurePixelMap");
        private CharacterCreatorModel _model;
        private GameObject _root;
        private RectTransform _book, _keyboardRoot, _rotateLeft, _rotateRight, _animationButton, _randomize;
        private RectTransform[] _tabs, _rows, _keyRects;
        private Image[] _tabImages, _rowImages, _keyImages;
        private Text[] _rowLabels, _keyLabels;
        private Text _hint, _summary, _animationLabel;
        private Image _background;
        private RawImage _portrait;
        private Font _face;
        private Keyboard _keyboard;
        private Camera _worldCamera, _creatorCamera;
        private RenderTexture _creatorTarget;
        private Mesh _creatorQuadMesh;
        private Material _creatorOverlayMaterial;
        private MeshRenderer _creatorOverlay;
        private MaterialPropertyBlock _creatorProperties;
        private bool _fallbackExportReady;
        private Canvas _canvas;
        private int _figureLayer, _pixelMultiple, _creatorWarmFrames;
        private bool _creatorProbeDone, _fallback, _turnHeld, _refreshRenderers;
        private string _viewReason;
        private Rect _figureScreenRect;
        private IsoCharacterSprite _character;
        private ICharacterFigure _figure;
        private Transform _figureRoot;
        private Quaternion _poseRotation;
        private float _openingHeading, _openingSpeed;
        private bool _headingHeld, _speedHeld;
        private CharacterStance _openingStance;
        private readonly Dictionary<Renderer, bool> _rendering = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Canvas, bool> _canvases = new Dictionary<Canvas, bool>();
        private readonly Dictionary<GameObject, int> _layers = new Dictionary<GameObject, int>();
        private readonly List<Renderer> _figureRenderers = new List<Renderer>(8);
        private float _figureHeightPixels;
        private Renderer _tallestRenderer;
        private bool _layoutValid;
        private int _facingIndex, _animationIndex;

        public float FigureHeightPixels => _figureHeightPixels;
        public float TargetFraction => GameServices.CreatorFigureHeightFraction;
        public Color BackdropColor => GameServices.CreatorBackdropColor;
        public int FacingIndex => _facingIndex;
        public string AnimationName => CharacterCreatorViewStrings.Animations[_animationIndex];
        public bool HasPortrait => _layoutValid && _portrait != null && _portrait.enabled;
        public RectTransform ControlStrip => _book;
        public int PixelMultiple => _pixelMultiple;
        public Rect FigureScreenRect => _figureScreenRect;
        public string ViewMode => _fallback ? "Figure texture, point scaled" : "Creator camera, isolated UI layer";
        public string ViewReason => _viewReason;
        private int Page => PageFor(_model.FocusRow, _model.OutfitRow);
        internal static int PageFor(int row, int outfitRow) => row == 0 ? 0 : row < outfitRow ? 1
            : row == outfitRow ? 2 : row == outfitRow + 1 ? 3 : 4;
        private int FirstRow(int page) => page == 0 ? 0 : page == 1 ? 1 : page == 2 ? _model.OutfitRow
            : page == 3 ? _model.NameRow : _model.ResetRow;

        public bool Open(CharacterCreatorModel model)
        {
            if (model == null || !model.IsOpen || (_model != null && _model.IsOpen)
                || InteractionGate.IsBlocked || MoveActionClaim.IsClaimed || CancelKeyClaim.IsHeld) return false;
            _model = model;
            InteractionGate.IsBlocked = true;
            MoveActionClaim.IsClaimed = true;
            CancelKeyClaim.Hold();
            Build();
            CaptureWorld();
            BindKeyboard();
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            _model.Changed += Refresh;
            Refresh();
            return true;
        }

        private void CaptureWorld()
        {
            _creatorProperties ??= new MaterialPropertyBlock();
            _worldCamera = Camera.main;
            _figureLayer = LayerMask.NameToLayer("UI");
            if (_worldCamera != null && _figureLayer >= 0)
            {
                var cameraHost = new GameObject("CreatorCamera", typeof(Camera));
                cameraHost.transform.SetParent(transform, false);
                _creatorCamera = cameraHost.GetComponent<Camera>();
                _creatorCamera.enabled = false;
                _creatorCamera.orthographic = true;
                _creatorCamera.cullingMask = 1 << _figureLayer;
                _creatorCamera.clearFlags = CameraClearFlags.SolidColor;
                _creatorCamera.backgroundColor = BackdropColor;
                _creatorCamera.allowHDR = false; _creatorCamera.allowMSAA = false;
                _creatorCamera.nearClipPlane = _worldCamera.nearClipPlane;
                _creatorCamera.farClipPlane = _worldCamera.farClipPlane;
                _creatorCamera.depth = _worldCamera.depth + 1f;
                _viewReason = "Only the live mesh uses the existing UI layer; the existing facet export excludes water and deck.";
            }
            else UseFallback("The existing UI layer or world camera is unavailable.");
            Transform player = GameServices.PlayerTransform;
            if (player != null)
            {
                foreach (MonoBehaviour component in player.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component is ICharacterFigureStand stand && stand.FigureCharacter != null) _character = stand.FigureCharacter;
                    if (component is ICharacterFigure figure && figure.PresentationRoot != null) _figure = figure;
                }
                if (_character == null) _character = player.GetComponentInChildren<IsoCharacterSprite>();
            }
            if (_character != null)
            {
                _openingHeading = _character.HeadingDegrees; _openingSpeed = _character.Speed;
                _headingHeld = _character.IsHeadingHeld; _speedHeld = _character.IsSpeedHeld;
                _openingStance = _character.Stance;
            }
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (!canvas.transform.IsChildOf(_root.transform))
                { _canvases[canvas] = canvas.enabled; canvas.enabled = false; }
            _refreshRenderers = true;
        }

        private void RestoreWorld()
        {
            RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            if (_figureRoot != null) _figureRoot.localRotation = _poseRotation;
            foreach (var pair in _rendering) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            _rendering.Clear();
            foreach (var pair in _canvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
            _canvases.Clear();
            RestoreLayers();
            ReleaseCreatorCamera();
            if (_character != null)
            {
                _character.Stance = _openingStance;
                if (_headingHeld) _character.HoldHeading(_openingHeading); else _character.ReleaseHeading();
                if (_speedHeld) _character.HoldSpeed(_openingSpeed); else _character.ReleaseSpeed();
                _character = null;
            }
        }

        private void OnDisable() => _model?.Cancel();
        private void OnDestroy()
        {
            _model?.Cancel();
            UnbindKeyboard();
            if (_model != null) _model.Changed -= Refresh;
            RestoreWorld();
            if (_root != null) Destroy(_root);
        }
        private void BindKeyboard()
        {
            if (ReferenceEquals(_keyboard, Keyboard.current)) return;
            UnbindKeyboard(); _keyboard = Keyboard.current;
            if (_keyboard != null) _keyboard.onTextInput += Type;
        }
        private void UnbindKeyboard()
        { if (_keyboard != null) _keyboard.onTextInput -= Type; _keyboard = null; }
        private void Type(char character) => _model?.Type(character);

        private void Update()
        {
            if (_model == null || !_model.IsOpen) return;
            BindKeyboard();
            // The world's stand states its context at order 100; these presentation-only holds
            // follow it, before the skin and the existing mesh presenter consume them in LateUpdate.
            if (_character != null && _character.Visual != null)
            {
                _character.Stance = CharacterStance.Free;
                _character.HoldSpeed(_animationIndex == 0 ? 0f : _character.Visual.WalkSpeedThreshold);
            }
            Keyboard kb = _keyboard; Gamepad pad = Gamepad.current;
            if ((kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame))
            { ChoosePage((Page + 1) % PageCount); return; }
            if (pad != null && pad.leftShoulder.wasPressedThisFrame)
            { ChoosePage((Page + PageCount - 1) % PageCount); return; }
            if (_model.NameEditing)
            {
                if (kb != null && kb.backspaceKey.wasPressedThisFrame) _model.Backspace();
                if ((kb != null && kb.enterKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
                { _model.Back(); return; }
                Vector2 axis = pad != null ? pad.dpad.ReadValue() : Vector2.zero;
                if (pad != null && axis.sqrMagnitude < WardrobePickerModel.AxisThreshold * WardrobePickerModel.AxisThreshold) axis = pad.leftStick.ReadValue();
                if (kb != null) axis += new Vector2((kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0),
                    (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0));
                _model.Input(axis.x, axis.y, pad != null && pad.buttonSouth.wasPressedThisFrame,
                    (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame));
            }
            else
            {
                float turn = pad != null ? pad.leftStick.x.ReadValue() : 0f;
                if (kb != null) turn += (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
                bool held = Mathf.Abs(turn) >= WardrobePickerModel.AxisThreshold;
                if (held && !_turnHeld) Rotate(turn > 0f ? 1 : -1);
                _turnHeld = held;
                if ((kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) CycleAnimation();
                if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) _model.Randomize();
                Vector2 axis = pad != null ? pad.dpad.ReadValue() : Vector2.zero;
                if (kb != null) axis += new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                    (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0));
                _model.Input(axis.x, axis.y, (kb != null && kb.enterKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame),
                    (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame));
            }
            ReadMouse();
        }

        private void LateUpdate()
        {
            if (_model == null || !_model.IsOpen || _worldCamera == null) return;
            Transform player = GameServices.PlayerTransform;
            if ((_figure == null || _figure.PresentationRoot == null) && player != null)
                foreach (MonoBehaviour component in player.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is ICharacterFigure figure && figure.PresentationRoot != null) { _figure = figure; break; }
            Transform root = _figure?.PresentationRoot;
            if (root == null || _character == null || _character.Visual == null) { _layoutValid = false; return; }
            if (_figureRoot != root) { _figureRoot = root; _refreshRenderers = true; }
            // The real mesh is still posed by its owner, in its original stand. Turn only its
            // presentation transform after that pose; the gameplay transform is never moved.
            _poseRotation = root.localRotation;
            root.localRotation = _poseRotation * Quaternion.AngleAxis(_facingIndex * (360f / _character.Visual.FacingCount), Vector3.forward);
            if (_fallback && _refreshRenderers)
            {
                foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (!_rendering.ContainsKey(renderer)) _rendering.Add(renderer, renderer.forceRenderingOff);
                _refreshRenderers = false;
            }
            if (_fallback)
                foreach (var pair in _rendering)
                    if (pair.Key != null) pair.Key.forceRenderingOff = !pair.Key.transform.IsChildOf(root) || pair.Value;
            root.GetComponentsInChildren(true, _figureRenderers);
            _tallestRenderer = null;
            float height = 0f;
            Bounds bounds = default;
            foreach (Renderer renderer in _figureRenderers)
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.bounds.size.y > height)
                { _tallestRenderer = renderer; bounds = renderer.bounds; height = bounds.size.y; }
            if (height <= 0f) { _layoutValid = false; return; }
            if (!_fallback)
            {
                foreach (Renderer renderer in _figureRenderers)
                    if (renderer != null)
                    {
                        GameObject host = renderer.gameObject;
                        if (!_layers.ContainsKey(host)) _layers.Add(host, host.layer);
                        host.layer = _figureLayer;
                    }
                EnsureCreatorTarget();
                float artHeight = height * FigurePixelGrid.ArtPixelsPerMetre;
                _pixelMultiple = Mathf.Max(1, Mathf.FloorToInt(Screen.height * TargetFraction / artHeight));
                _creatorCamera.orthographicSize = Screen.height / (2f * FigurePixelGrid.ArtPixelsPerMetre * _pixelMultiple);
                Vector3 center = bounds.center;
                Quaternion rotation = _worldCamera.transform.rotation;
                Vector3 forward = rotation * Vector3.forward, up = rotation * Vector3.up;
                float distance = Mathf.Max(bounds.extents.magnitude + _creatorCamera.nearClipPlane,
                    Mathf.Abs(Vector3.Dot(bounds.center - _worldCamera.transform.position, forward)));
                center -= up * (_creatorCamera.orthographicSize * StripHeight / Height);
                _creatorCamera.transform.SetPositionAndRotation(center - forward * distance, rotation);
                _creatorCamera.aspect = (float)_creatorTarget.width / _creatorTarget.height;
                _figureScreenRect = ProjectBounds(_creatorCamera, bounds);
                _figureHeightPixels = _figureScreenRect.height;
                if (!EnsureCreatorOverlay()) { UseFallback("The figure overlay shader is unavailable."); return; }
                _tallestRenderer.GetPropertyBlock(_creatorProperties);
                _creatorOverlay.SetPropertyBlock(_creatorProperties);
                _creatorOverlay.transform.localPosition = Vector3.forward * (_creatorCamera.nearClipPlane + 1f);
                _creatorOverlay.transform.localScale = new Vector3(_creatorCamera.orthographicSize * _creatorCamera.aspect * 2f,
                    _creatorCamera.orthographicSize * 2f, 1f);
                _creatorCamera.enabled = true;
            }
            _layoutValid = true;
            Color backdrop = BackdropColor; backdrop.a = 1f; _background.color = backdrop;
        }

        private void OnCameraRendered(ScriptableRenderContext context, Camera camera)
        {
            if (_portrait == null || _model == null || !_model.IsOpen || !_layoutValid) return;
            if ((!_fallback && camera != _creatorCamera) || (_fallback && camera != _worldCamera)) return;
            if (!_fallback)
            {
                if (++_creatorWarmFrames < 3) return;
                if (!_creatorProbeDone)
                {
                    _creatorProbeDone = true;
                    if (!IsIsolatedFigure(_creatorTarget, true))
                    { UseFallback("The creator camera produced no isolated figure; the fallback must supply a written figure export."); return; }
                }
                // The view's overlay reads the GPU-bound export during this camera's ordinary
                // sprite pass. Shader.GetGlobalTexture can still return its CPU placeholder.
                _portrait.texture = _creatorTarget;
                _portrait.uvRect = new Rect(0f, 0f, 1f, 1f);
                _portrait.enabled = true;
                Stretch(_portrait.rectTransform);
            }
            else
            {
                Texture texture = Shader.GetGlobalTexture(FigureTexture);
                Vector4 map = Shader.GetGlobalVector(FigureMap);
                bool available = texture is RenderTexture export && export.IsCreated()
                    && texture.width >= 8 && texture.height >= 8 && map.w > 0f;
                if (!available)
                {
                    _portrait.enabled = false;
                    _viewReason = "FAILED: no written figure export is available; placeholder textures are never scaled.";
                    return;
                }
                if (!_fallbackExportReady) _fallbackExportReady = IsIsolatedFigure((RenderTexture)texture);
                _portrait.enabled = _fallbackExportReady;
                if (!_fallbackExportReady || _tallestRenderer == null)
                { _viewReason = "FAILED: the figure export has not produced an isolated figure."; return; }
                Vector3 min = camera.WorldToScreenPoint(_tallestRenderer.bounds.min), max = camera.WorldToScreenPoint(_tallestRenderer.bounds.max);
                Vector2 low = new Vector2((min.x - map.x) / map.w, (min.y - map.y) / map.w);
                Vector2 high = new Vector2((max.x - map.x) / map.w, (max.y - map.y) / map.w);
                _pixelMultiple = Mathf.Max(1, Mathf.FloorToInt(Screen.height * TargetFraction / (high.y - low.y)));
                float x = Mathf.Max(0f, Mathf.Floor(low.x) - FigurePixelGrid.GuardTexels), y = Mathf.Max(0f, Mathf.Floor(low.y) - FigurePixelGrid.GuardTexels);
                float w = Mathf.Min(texture.width - x, Mathf.Ceil(high.x) + FigurePixelGrid.GuardTexels - x);
                float h = Mathf.Min(texture.height - y, Mathf.Ceil(high.y) + FigurePixelGrid.GuardTexels - y);
                if (w <= 0f || h <= 0f || high.y <= low.y)
                {
                    _portrait.enabled = false;
                    _viewReason = "FAILED: the figure bounds do not intersect the written export.";
                    return;
                }
                RectTransform rect = _portrait.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, (1f + StripHeight / Height) * 0.5f); rect.pivot = Vector2.one * 0.5f;
                rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(w, h) * _pixelMultiple / _canvas.scaleFactor;
                _portrait.texture = texture; _portrait.uvRect = new Rect(x / texture.width, y / texture.height, w / texture.width, h / texture.height);
                Vector2 origin = new Vector2(Screen.width * 0.5f - w * _pixelMultiple * 0.5f,
                    Screen.height * (1f + StripHeight / Height) * 0.5f - h * _pixelMultiple * 0.5f);
                _figureScreenRect = Rect.MinMaxRect(origin.x + (low.x - x) * _pixelMultiple, origin.y + (low.y - y) * _pixelMultiple,
                    origin.x + (high.x - x) * _pixelMultiple, origin.y + (high.y - y) * _pixelMultiple);
                _figureHeightPixels = _figureScreenRect.height;
            }
        }

        private bool EnsureCreatorOverlay()
        {
            if (_creatorOverlay != null) return true;
            Shader shader = Shader.Find("HiddenHarbours/IsoFacetOverlay");
            if (shader == null || !shader.isSupported) return false;
            _creatorOverlayMaterial = new Material(shader) { name = "CreatorFigureOverlay", hideFlags = HideFlags.DontSave };
            _creatorQuadMesh = new Mesh { name = "CreatorFigureQuad", hideFlags = HideFlags.DontSave };
            _creatorQuadMesh.vertices = new[] { new Vector3(-.5f,-.5f,0f), new Vector3(.5f,-.5f,0f),
                new Vector3(.5f,.5f,0f), new Vector3(-.5f,.5f,0f) };
            _creatorQuadMesh.triangles = new[] { 0,1,2,0,2,3 };
            _creatorQuadMesh.RecalculateBounds();
            var host = new GameObject("CreatorFigureOverlay", typeof(MeshFilter), typeof(MeshRenderer)) { layer = _figureLayer };
            host.transform.SetParent(_creatorCamera.transform, false);
            host.GetComponent<MeshFilter>().sharedMesh = _creatorQuadMesh;
            _creatorOverlay = host.GetComponent<MeshRenderer>();
            _creatorOverlay.sharedMaterial = _creatorOverlayMaterial;
            _creatorOverlay.sortingOrder = short.MaxValue;
            return true;
        }

        private static Rect ProjectBounds(Camera camera, Bounds bounds)
        {
            Vector2 low = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 high = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                low = Vector2.Min(low, point); high = Vector2.Max(high, point);
            }
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }
        private void EnsureCreatorTarget()
        {
            if (_creatorTarget != null && _creatorTarget.width == Screen.width && _creatorTarget.height == Screen.height) return;
            if (_creatorTarget != null)
            {
                _creatorCamera.targetTexture = null;
                if (RenderTexture.active == _creatorTarget) RenderTexture.active = null;
                _creatorTarget.Release(); Destroy(_creatorTarget);
            }
            _creatorTarget = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
                { name = "CreatorFigureTarget", filterMode = FilterMode.Point, antiAliasing = 1 };
            _creatorTarget.Create(); _creatorCamera.targetTexture = _creatorTarget;
        }
        private static bool IsIsolatedFigure(RenderTexture texture, bool backdrop = false)
        {
            if (texture == null || !texture.IsCreated() || texture.width < 8 || texture.height < 8) return false;
            RenderTexture previous = RenderTexture.active;
            var probe = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                probe.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0); probe.Apply(false, false);
                Color32[] pixels = probe.GetPixels32();
                bool emptyCorners = pixels[0].a == 0 && pixels[texture.width - 1].a == 0
                    && pixels[pixels.Length - 1].a == 0 && pixels[pixels.Length - texture.width].a == 0;
                if (backdrop)
                    emptyCorners = SamePixel(pixels[0], pixels[texture.width - 1])
                        && SamePixel(pixels[0], pixels[pixels.Length - 1])
                        && SamePixel(pixels[0], pixels[pixels.Length - texture.width]);
                if (!emptyCorners) return false;
                foreach (Color32 pixel in pixels) if (backdrop ? !SamePixel(pixel, pixels[0]) : pixel.a > 0) return true;
                return false;
            }
            finally { RenderTexture.active = previous; Destroy(probe); }
        }
        private static bool SamePixel(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g)
            + Math.Abs(a.b - b.b) + Math.Abs(a.a - b.a) <= 4;

        private void RestoreLayers()
        { foreach (var pair in _layers) if (pair.Key != null) pair.Key.layer = pair.Value; _layers.Clear(); }
        private void ReleaseCreatorCamera()
        {
            if (_creatorOverlay != null) { Destroy(_creatorOverlay.gameObject); _creatorOverlay = null; }
            if (_creatorOverlayMaterial != null) { Destroy(_creatorOverlayMaterial); _creatorOverlayMaterial = null; }
            if (_creatorQuadMesh != null) { Destroy(_creatorQuadMesh); _creatorQuadMesh = null; }
            if (_portrait != null) { _portrait.enabled = false; _portrait.texture = null; }
            if (_creatorCamera != null) { _creatorCamera.enabled = false; _creatorCamera.targetTexture = null; Destroy(_creatorCamera.gameObject); _creatorCamera = null; }
            if (_creatorTarget != null)
            {
                if (RenderTexture.active == _creatorTarget) RenderTexture.active = null;
                _creatorTarget.Release(); Destroy(_creatorTarget); _creatorTarget = null;
            }
        }
        private void UseFallback(string reason)
        { _fallback = true; _fallbackExportReady = false; _viewReason = reason; RestoreLayers(); ReleaseCreatorCamera(); _refreshRenderers = true; }

        private void Rotate(int direction)
        {
            if (_character == null || _character.Visual == null) return;
            int count = _character.Visual.FacingCount;
            _facingIndex = (_facingIndex + direction + count) % count;
        }
        private void CycleAnimation()
        { _animationIndex = (_animationIndex + 1) % CharacterCreatorViewStrings.Animations.Length; _animationLabel.text = AnimationName; }
        private void ChoosePage(int page)
        { if (_model.NameEditing) _model.Back(); _model.Focus(FirstRow(page)); }
        private static bool Hit(RectTransform rect, Vector2 point) => rect != null && rect.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(rect, point);
        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 point = mouse.position.ReadValue();
            for (int i = 0; i < _tabs.Length; i++) if (Hit(_tabs[i], point)) { ChoosePage(i); return; }
            if (Hit(_rotateLeft, point)) { Rotate(-1); return; }
            if (Hit(_rotateRight, point)) { Rotate(1); return; }
            if (Hit(_animationButton, point)) { CycleAnimation(); return; }
            if (Hit(_randomize, point) && !_model.NameEditing) { _model.Randomize(); return; }
            if (_model.NameEditing)
            {
                for (int i = 0; i < _keyRects.Length; i++) if (Hit(_keyRects[i], point)) { _model.PressKey(i); return; }
                return;
            }
            for (int i = 0; i < _rows.Length; i++) if (Hit(_rows[i], point))
            {
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
                _model.Changed -= Refresh; UnbindKeyboard(); RestoreWorld(); _root.SetActive(false);
                InteractionGate.IsBlocked = false; MoveActionClaim.IsClaimed = false; CancelKeyClaim.Release(); return;
            }
            _refreshRenderers = true;
            for (int i = 0; i < _tabs.Length; i++) _tabImages[i].color = i == Page ? NotebookInk.Gold : NotebookInk.Paper;
            for (int i = 0, visible = 0; i < _rows.Length; i++)
            {
                bool show = PageFor(i, _model.OutfitRow) == Page && !_model.NameEditing;
                _rows[i].gameObject.SetActive(show);
                if (!show) continue;
                float w = (Width - Margin * 2f) / 4f;
                Place(_rows[i], Margin + (visible % 4) * w, RowHeight + Margin, w - NotebookKit.CellWidth * Scale, RowHeight);
                _rows[i].anchoredPosition += Vector2.down * (visible / 4) * (RowHeight + NotebookKit.Edge * Scale);
                Place(_rowLabels[i].rectTransform, NotebookKit.Edge * Scale, 0f,
                    w - NotebookKit.CellWidth * Scale - NotebookKit.Edge * Scale, RowHeight);
                _rowLabels[i].text = _model.RowLabel(i);
                _rowImages[i].color = i == _model.FocusRow ? NotebookInk.Gold : NotebookInk.Paper;
                visible++;
            }
            _summary.text = _model.NameEditing ? _model.RowLabel(_model.NameRow)
                : Page == 4 ? _model.RowLabel(0) + "    " + _model.RowLabel(_model.OutfitRow) + "    " + _model.RowLabel(_model.NameRow) : "";
            Place(_summary.rectTransform, Margin, _model.NameEditing ? RowHeight + NotebookKit.Edge * Scale : RowHeight * 4f + Margin,
                Width - Margin * 2f, RowHeight);
            _keyboardRoot.gameObject.SetActive(_model.NameEditing);
            for (int i = 0; i < _keyLabels.Length; i++)
            { _keyLabels[i].text = _model.KeyLabel(i); _keyImages[i].color = i == _model.KeyboardIndex ? NotebookInk.Gold : NotebookInk.Paper; }
            _hint.text = _model.Refusal ?? (_model.NameEditing ? CharacterCreatorViewStrings.NameHint : CharacterCreatorViewStrings.Hint);
            _hint.color = _model.Refusal != null ? NotebookInk.InkStrong : NotebookInk.Ink;
        }

        private void Build()
        {
            _face = Resources.Load<Font>(HarbourType.ResourceKey);
            if (_face == null) _face = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _root = new GameObject("CharacterCreator", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root.transform.SetParent(transform, false);
            Canvas canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas = canvas;
            canvas.sortingOrder = short.MaxValue; canvas.referencePixelsPerUnit = NotebookKit.PixelsPerUnit;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height); scaler.matchWidthOrHeight = 0.5f;
            RectTransform background = Image(_root.transform, "BlankGround", BackdropColor); Stretch(background);
            _background = background.GetComponent<Image>();
            var portrait = new GameObject("LiveFigure", typeof(RectTransform), typeof(RawImage));
            portrait.transform.SetParent(_root.transform, false); _portrait = portrait.GetComponent<RawImage>();
            _portrait.raycastTarget = false; _portrait.enabled = false; Stretch((RectTransform)portrait.transform);
            _book = Image(_root.transform, "Controls", NotebookInk.Paper);
            _book.anchorMin = new Vector2(0f,0f); _book.anchorMax = new Vector2(1f,0f); _book.pivot = Vector2.zero;
            _book.sizeDelta = new Vector2(0f,StripHeight);
            _tabs = new RectTransform[PageCount]; _tabImages = new Image[PageCount];
            for (int i=0;i<PageCount;i++)
            { _tabs[i]=Button(_book,"Step"+i,CharacterCreatorViewStrings.Tabs[i],Margin+i*210f,NotebookKit.Edge*Scale,200f); _tabImages[i]=_tabs[i].GetComponent<Image>(); }
            _rotateLeft=Button(_book,"TurnLeft","<",1100f,NotebookKit.Edge*Scale,65f);
            _rotateRight=Button(_book,"TurnRight",">",1175f,NotebookKit.Edge*Scale,65f);
            _animationButton=Button(_book,"Animation",AnimationName,1250f,NotebookKit.Edge*Scale,230f);
            _animationLabel=_animationButton.GetComponentInChildren<Text>();
            _randomize=Button(_book,"Randomize",CharacterCreatorViewStrings.Randomize,1490f,NotebookKit.Edge*Scale,Width-Margin-1490f);
            _summary=Label(_book,"Summary","",Margin,RowHeight+Margin,Width-Margin*2f,RowHeight);
            _hint=Label(_book,"Hint","",Margin,StripHeight-RowHeight,Width-Margin*2f,RowHeight);
            _rows=new RectTransform[_model.RowCount]; _rowImages=new Image[_rows.Length]; _rowLabels=new Text[_rows.Length];
            for (int i=0;i<_rows.Length;i++)
            { _rows[i]=Button(_book,"Choice"+i,"",0f,0f,1f); _rowImages[i]=_rows[i].GetComponent<Image>(); _rowLabels[i]=_rows[i].GetComponentInChildren<Text>(); }
            _keyboardRoot=Image(_book,"NameKeyboard",NotebookInk.Paper);
            Place(_keyboardRoot,Margin,RowHeight*2f+NotebookKit.Edge*Scale,Width-Margin*2f,RowHeight*4f);
            _keyRects=new RectTransform[CharacterCreatorModel.KeyboardKeyCount]; _keyImages=new Image[_keyRects.Length]; _keyLabels=new Text[_keyRects.Length];
            float keyWidth=(Width-Margin*2f)/CharacterCreatorModel.KeyboardColumns;
            for (int i=0;i<_keyRects.Length;i++)
            { _keyRects[i]=Button(_keyboardRoot,"Key"+i,"",(i%CharacterCreatorModel.KeyboardColumns)*keyWidth,(i/CharacterCreatorModel.KeyboardColumns)*RowHeight,keyWidth-NotebookKit.Edge*Scale);
                _keyImages[i]=_keyRects[i].GetComponent<Image>(); _keyLabels[i]=_keyRects[i].GetComponentInChildren<Text>(); }
        }
        private RectTransform Button(Transform parent,string name,string copy,float x,float y,float width)
        {
            RectTransform rect=Image(parent,name,NotebookInk.Paper); Place(rect,x,y,width,RowHeight);
            Label(rect,"Label",copy,NotebookKit.Edge*Scale,0f,width-NotebookKit.Edge*Scale,RowHeight); return rect;
        }
        private static RectTransform Image(Transform parent,string name,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            Image image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; return (RectTransform)go.transform;
        }
        private Text Label(Transform parent,string name,string copy,float x,float y,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            Text text=go.GetComponent<Text>(); text.font=_face; text.fontSize=NotebookKit.GlyphHeight*Scale; text.text=copy; text.color=NotebookInk.Ink;
            text.alignment=TextAnchor.MiddleLeft; text.raycastTarget=false; text.horizontalOverflow=HorizontalWrapMode.Overflow;
            text.verticalOverflow=VerticalWrapMode.Truncate; Place(text.rectTransform,x,y,width,height); return text;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        { rect.anchorMin=rect.anchorMax=new Vector2(0f,1f); rect.pivot=new Vector2(0f,1f); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(width,height); }
    }

    internal static class CharacterCreatorViewStrings
    {
        public static readonly string[] Tabs={"BODY","COLOURS","OUTFIT","NAME","READY"};
        public static readonly string[] Animations={"Idle","Walk"};
        public const string Hint="Arrows/stick: turn   A/D or d-pad: change   Tab/shoulders: step   Space/Y: pose   R/X: randomize   Enter/A: choose   Esc/B: leave";
        public const string NameHint="Type, or arrows/d-pad and A: keys   Enter/Start or Done: finish   Esc/B: back";
        public const string Randomize="Randomize";
    }
}
