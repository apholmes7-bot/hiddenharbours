using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>The wardrobe's other half</b> — the small panel that opens beside a wardrobe when the player
    /// works it, lists what is in it, recolours the fisher as the highlight moves, and either commits that
    /// choice to the save or puts back exactly what was there.
    ///
    /// <para><b>The player IS the preview.</b> There is no paper-doll screen, no mannequin render and no
    /// second copy of the character to keep in step: moving the highlight publishes
    /// <see cref="OutfitPreviewRequested"/>, whoever is drawing the fisher answers it, and what you are
    /// looking at is the actual character standing in the actual room. That is the diegetic-UI canon read
    /// literally, and it is also the cheapest possible implementation — the preview costs two
    /// <c>SetVectorArray</c> calls, because it is the same palette swap the worn outfit already uses.</para>
    ///
    /// <para><b>It reaches the fisher and the save WITHOUT naming either module.</b> UI may not reference
    /// Art or World (rule 4), so the two ends are Core seams: the preview goes out as a signal that
    /// <c>PlayerOutfitVisual</c> subscribes to, and the commit goes through
    /// <see cref="OutfitLocker.Wear(string)"/>, which writes the save field and announces the change. This
    /// component never touches a material, a renderer or a <c>SaveData</c> field.</para>
    ///
    /// <para><b>A preview can never leak into the save.</b> There is exactly one way to persist — the
    /// confirm path — and every other way this picker can end (cancel, the fixture going away, the player
    /// leaving reach, the region unloading, this object being destroyed) runs
    /// <see cref="CancelAndClose"/>, which re-publishes the preview with
    /// <see cref="WardrobePickerModel.RevertOutfitId"/>. That id is captured when the doors open and is
    /// readonly, so no sequence of steps can lose it.</para>
    ///
    /// <para><b>While it is open it owns both the interact key and the move axis.</b>
    /// <see cref="InteractionGate"/> is the modal hold this codebase already has, and it is what stops the
    /// confirm press ALSO boarding a boat and stops Esc opening the pause menu underneath
    /// (<c>ShellPresenter.CanTogglePause</c> already declines while it is up). <see cref="MoveActionClaim"/>
    /// is the narrower, newer one: the dev-key ledger is exhausted A–Z, so the highlight steers on the same
    /// keys that walk the fisher, and without the claim arrowing down a list of eight would walk you out of
    /// your own wardrobe. Both are released by the single close path.</para>
    ///
    /// <para><b>Self-installing</b>, in <c>ShellPresenter</c>'s shape and for its reason: no scene wiring,
    /// so a wardrobe works in whatever region it is placed in without a scene builder being re-run over the
    /// owner's hand-painted work. Nothing is built until the first time a wardrobe is actually opened
    /// (rule 7) — a save that never opens one pays for a subscription and an empty <c>Update</c>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WardrobePicker : MonoBehaviour
    {
        /// <summary>Above the dialogue bubble (110), below the market screens (200) — a fixture's panel is
        /// the frontmost thing in the world layer, and the shell still covers it.</summary>
        public const int SortingOrder = 115;

        /// <summary>How much further than the fixture's own reach the player may drift before the picker
        /// gives up on them, matching <c>WorldInteractor.DisengageSlack</c>. With the move axis claimed
        /// this cannot fire from walking; it is the guarantee that a player moved by anything ELSE (a
        /// region travel, a rest, a future cutscene) cannot be left standing in a panel forever.</summary>
        public const float DisengageSlack = 1.35f;

        // The panel's stock and ink. Warm paper with dark ink, as the dialogue bubble's fallback is — the
        // wardrobe's list is an object in the room, not a screen over it (the shell's chalk-on-dusk is the
        // other sensibility, and it belongs to the menus outside the world).
        private static readonly Color PanelStock = new Color(0.941f, 0.925f, 0.878f, 0.98f);
        private static readonly Color PanelEdge = new Color(0.180f, 0.176f, 0.157f, 1f);
        private static readonly Color Ink = new Color(0.145f, 0.137f, 0.125f, 1f);
        private static readonly Color InkFaint = new Color(0.145f, 0.137f, 0.125f, 0.62f);
        private static readonly Color RowSelected = new Color(0.180f, 0.176f, 0.157f, 0.14f);
        private static readonly Color RowIdle = new Color(0f, 0f, 0f, 0f);
        private static readonly Color ChipEdge = new Color(0.180f, 0.176f, 0.157f, 0.85f);
        private static readonly Color ChipUnknown = new Color(0.55f, 0.55f, 0.55f, 1f);

        private const int TitleFontSize = 18;
        private const int RowFontSize = 16;
        private const int HintFontSize = 12;
        private const float EdgeThickness = 1f;

        private static WardrobePicker _instance;

        [Tooltip("The wardrobe to list. Left empty (the runtime case) it is loaded from Resources, the " +
                 "same way the icon / fish / seaweed libraries reach the game.")]
        [SerializeField] private CharacterWardrobeDef _wardrobe;

        [Tooltip("Which camera the fixture is projected through. Empty uses whatever is tagged " +
                 "MainCamera, which is the runtime case.")]
        [SerializeField] private Camera _camera;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameObject _root;
        private RectTransform _panelRect;
        private Text _titleText;
        private Text _hintText;
        private readonly List<Row> _rows = new List<Row>();

        private WardrobePickerModel _model;
        private IInteractable _fixture;
        private Vector2 _anchorWorld;
        private int _openedFrame = -1;
        private Vector2 _panelCentre;
        private Vector2 _panelSize;

        /// <summary>One row's parts, kept together so redrawing the highlight is three writes and no
        /// searching.</summary>
        private sealed class Row
        {
            public RectTransform Rect;
            public Image Strip;
            public Image Chip;
            public Text Cursor;
            public Text Label;
        }

        // ---- installation -------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[WardrobePicker]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<WardrobePicker>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnEnable() => EventBus.Subscribe<WardrobeRequested>(OnWardrobeRequested);

        private void OnDisable()
        {
            EventBus.Unsubscribe<WardrobeRequested>(OnWardrobeRequested);
            // A picker torn down mid-choice must not leave a preview on the fisher, nor the player unable
            // to walk. One close path, taken by every ending.
            CancelAndClose();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ---- what a test and the world can see ----------------------------------------------

        /// <summary>
        /// The live picker, or null before it installs. For tests and tooling — the installed one is the
        /// only one there is (<see cref="Awake"/> destroys a second), so a test drives THIS rather than
        /// standing up a rival that would be torn down under it.
        ///
        /// <para><b>⚠ Explicit <c>!= null</c></b>: a destroyed picker is fake-null, and a caller doing
        /// <c>Instance?.Foo()</c> on the raw field would sail straight past Unity's overloaded
        /// <c>==</c>.</para>
        /// </summary>
        public static WardrobePicker Instance => _instance != null ? _instance : null;

        /// <summary>True while the panel is up.</summary>
        public bool IsOpen => _model != null;

        /// <summary>Which wardrobe is open, or empty — the fixture id the signal carried.</summary>
        public string OpenFixtureId { get; private set; } = "";

        /// <summary>How many outfits the open panel is listing; 0 when closed.</summary>
        public int OutfitCount => _model?.Count ?? 0;

        /// <summary>The highlighted row, or -1 when closed.</summary>
        public int SelectedIndex => _model?.Index ?? -1;

        /// <summary>The outfit id currently being PREVIEWED, or empty when closed.</summary>
        public string SelectedOutfitId => _model != null ? _model.SelectedOutfitId : "";

        /// <summary>What a cancel would put back — the id that was in the save when the panel opened.
        /// Empty when closed.</summary>
        public string RevertOutfitId => _model != null ? _model.RevertOutfitId : "";

        /// <summary>The wardrobe being listed, resolved from Resources when nothing was wired. For tests
        /// and tooling.</summary>
        public CharacterWardrobeDef Wardrobe => ResolveWardrobe();

        /// <summary>Point this picker at a wardrobe and a camera (tests / a scene that wants its own).</summary>
        public void Configure(CharacterWardrobeDef wardrobe, Camera camera)
        {
            _wardrobe = wardrobe;
            _camera = camera;
        }

        // ---- opening and closing -------------------------------------------------------------

        private void OnWardrobeRequested(WardrobeRequested e) => Open(e.FixtureId);

        /// <summary>
        /// Open the panel for a fixture. Public so a PlayMode test drives the real path without a keyboard
        /// (a virtual key press is undeliverable to the new Input System's device state).
        ///
        /// <para>Returns false when it declined: a wardrobe with nothing in it (the honest refusal the
        /// fixture used to give unconditionally, now given only when it is true), no wardrobe asset at all,
        /// or another modal already holding interaction. Declining is a state, not a fault.</para>
        /// </summary>
        public bool Open(string fixtureId)
        {
            if (IsOpen) return false;

            // Someone else's modal is up (a conversation mid-sentence). Its hold is on the same key that
            // would confirm here, so opening over it would give one press two meanings.
            if (InteractionGate.IsBlocked) return false;

            var wardrobe = ResolveWardrobe();
            var ids = OutfitIdsOf(wardrobe);
            if (ids.Count == 0)
            {
                EventBus.Publish(new DevNotice(WardrobeStrings.NothingToWear));
                return false;
            }

            OpenFixtureId = string.IsNullOrEmpty(fixtureId) ? "" : fixtureId;
            _fixture = FindFixture(OpenFixtureId);
            _anchorWorld = _fixture != null ? _fixture.WorldPosition : AnchorFallback();

            _model = new WardrobePickerModel(ids, OutfitLocker.WornOutfitId());
            _openedFrame = Time.frameCount;

            // ⭐ TAKE BOTH HOLDS BEFORE ANYTHING ELSE. The press that opened this panel is still down this
            // frame — see the _openedFrame guard in Update — and the gate is what stops the world's other
            // readers spending it a second time meanwhile.
            InteractionGate.IsBlocked = true;
            MoveActionClaim.IsClaimed = true;

            EnsureBuilt(ids.Count);
            DrawRows();
            Track();
            if (_root != null) _root.SetActive(true);

            // The fisher is already wearing the selected outfit on the ordinary open (the cursor starts on
            // what is worn), so this is a no-op there — Apply is idempotent. It matters for the one case
            // where it is not: a saved outfit the wardrobe no longer holds, where the cursor starts at the
            // top and the preview must move to match what the list is pointing at.
            PublishPreview();
            return true;
        }

        /// <summary>
        /// Wear what the cursor is on: persist it, announce it, and close. Returns false when there was
        /// nothing to confirm.
        ///
        /// <para><b>The save write is <see cref="OutfitLocker.Wear(string)"/>'s, not this component's</b> —
        /// it normalises the id, declines a no-op, writes the file and publishes
        /// <see cref="OutfitChanged"/>, which re-skins the fisher through the same path the preview used
        /// (and is therefore free: <c>Apply</c> is idempotent, and the preview already put the colours
        /// there). If it declines because no save service is attached — an EditMode rig, a pre-boot
        /// scene — the session keeps the look and the save is untouched, which is the same posture
        /// <c>PlayerOutfitVisual</c> takes when content fails to resolve.</para>
        /// </summary>
        public bool Confirm()
        {
            if (_model == null) return false;
            if (!_model.TryConfirm(out string outfitId)) return false;

            OutfitLocker.Wear(outfitId);
            Close();
            return true;
        }

        /// <summary>
        /// Leave it: put back exactly what was in the save when the panel opened, and close. Safe to call
        /// when nothing is open.
        /// </summary>
        public void CancelAndClose()
        {
            if (_model == null) return;

            EventBus.Publish(new OutfitPreviewRequested(_model.RevertOutfitId));
            Close();
        }

        /// <summary>Step the highlight and preview what it lands on. Returns true when it moved. The axis
        /// is latched, so one row per push — feed 0 between pushes, exactly as a real key does.</summary>
        public bool MoveSelection(float axis)
        {
            if (_model == null) return false;
            if (!_model.Step(axis)) return false;

            DrawRows();
            PublishPreview();
            return true;
        }

        /// <summary>Put the highlight on a row directly (the mouse's path) and preview it. Returns true
        /// when it moved.</summary>
        public bool SelectRow(int index)
        {
            if (_model == null || index < 0 || index >= _model.Count) return false;
            if (index == _model.Index) return false;

            _model.SelectIndex(index);
            DrawRows();
            PublishPreview();
            return true;
        }

        private void Close()
        {
            _model = null;
            _fixture = null;
            OpenFixtureId = "";
            _openedFrame = -1;
            InteractionGate.IsBlocked = false;
            MoveActionClaim.IsClaimed = false;
            if (_root != null) _root.SetActive(false);
        }

        private void PublishPreview() =>
            EventBus.Publish(new OutfitPreviewRequested(_model.SelectedOutfitId));

        // ---- the frame ------------------------------------------------------------------------

        private void Update()
        {
            if (_model == null) return;

            // ⭐ THE PRESS THAT OPENED THIS PANEL IS STILL DOWN. Interact is read in another component's
            // Update, in an order nobody defines, so without this a wardrobe opened with E would be
            // confirmed by the same E on the same frame — and the player would never see the list.
            if (Time.frameCount == _openedFrame) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;

            if (CancelPressed(kb, pad)) { CancelAndClose(); return; }
            if (ConfirmPressed(kb, pad)) { Confirm(); return; }

            MoveSelection(ReadAxis(kb, pad));
            ReadMouse();
        }

        private void LateUpdate()
        {
            if (_model == null) return;

            // The fixture went away underneath us — a region unloaded, the interior was torn down. Put the
            // preview back rather than leaving a panel pinned to a wardrobe that no longer exists.
            if (_fixture != null && !IsRegistered(_fixture)) { CancelAndClose(); return; }
            if (_fixture != null) _anchorWorld = _fixture.WorldPosition;

            if (PlayerHasLeft()) { CancelAndClose(); return; }

            Track();
        }

        /// <summary>
        /// Has the player left the wardrobe? With the move axis claimed they cannot walk out, so in
        /// ordinary play this never fires — it is the guarantee that anything ELSE which moves them (a
        /// region travel, a rest, a future cutscene) cannot strand them inside a panel with their own
        /// movement held. Never true when there is nobody to measure, which is the EditMode/rig case.
        /// </summary>
        private bool PlayerHasLeft()
        {
            if (_fixture == null) return false;

            Transform player = GameServices.PlayerTransform;   // laundered to a REAL null by the accessor
            if (player == null) return false;

            float reach = Mathf.Max(0.01f, _fixture.ReachMeters) * DisengageSlack;
            return ((Vector2)player.position - _fixture.WorldPosition).sqrMagnitude > reach * reach;
        }

        // ---- input ----------------------------------------------------------------------------

        /// <summary>
        /// This frame's selection axis — +1 up the list, -1 down. The SAME keys that walk the fisher
        /// (<c>PlayerWalkController.ReadInput</c>), which is why the picker claims the move action while it
        /// is open: the dev-key ledger is exhausted A–Z and there is no fresh key to bind.
        ///
        /// <para>The pad is read alongside on the stick and the d-pad. On-foot walking is keyboard-only
        /// today, so a pad cannot fight the claim — it simply steers the list.</para>
        /// </summary>
        private static float ReadAxis(Keyboard kb, Gamepad pad)
        {
            float axis = 0f;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) axis += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) axis -= 1f;
            }
            if (Mathf.Abs(axis) >= WardrobePickerModel.AxisThreshold) return axis;

            if (pad != null)
            {
                if (pad.dpad.up.isPressed) axis += 1f;
                if (pad.dpad.down.isPressed) axis -= 1f;
                if (Mathf.Abs(axis) < WardrobePickerModel.AxisThreshold)
                    axis += pad.leftStick.ReadValue().y;
            }
            return axis;
        }

        /// <summary>Interact confirms — the key that opened the panel is the key that closes it on a
        /// choice, which is the convention the dialogue's option rows already set. South on a pad.</summary>
        private static bool ConfirmPressed(Keyboard kb, Gamepad pad) =>
            (kb != null && kb.eKey.wasPressedThisFrame) ||
            (pad != null && pad.buttonSouth.wasPressedThisFrame);

        /// <summary>Esc leaves it — the close key every overlay in this project already uses (the
        /// chartplotter, the sounder, the radar, the buy screen). East on a pad. Safe from the pause
        /// menu because <see cref="InteractionGate"/> is up, which <c>ShellPresenter</c> already
        /// honours.</summary>
        private static bool CancelPressed(Keyboard kb, Gamepad pad) =>
            (kb != null && kb.escapeKey.wasPressedThisFrame) ||
            (pad != null && pad.buttonEast.wasPressedThisFrame);

        /// <summary>
        /// The mouse's half: MOVING over a row highlights it (and so previews it), and a left click wears
        /// it. Hover only takes the selection when the mouse actually moved this frame, so a stationary
        /// cursor resting over the list cannot fight the keys.
        /// </summary>
        private void ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || _canvas == null) return;

            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            Vector2 point = mouse.position.ReadValue() / scale;
            int row = WardrobePickerLayout.RowIndexAt(point, _panelCentre, _panelSize, _model.Count);

            if (mouse.delta.ReadValue().sqrMagnitude > 0f && row >= 0) SelectRow(row);
            if (mouse.leftButton.wasPressedThisFrame && row >= 0)
            {
                SelectRow(row);
                Confirm();
            }
        }

        // ---- placement -------------------------------------------------------------------------

        /// <summary>Put the panel beside the wardrobe, this frame.</summary>
        private void Track()
        {
            if (_root == null || _panelRect == null || _canvasRect == null) return;

            Vector2 canvasSize = _canvasRect.rect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f) return;

            Camera cam = ResolveCamera();
            Vector2 anchorPoint;
            if (cam != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(new Vector3(_anchorWorld.x, _anchorWorld.y, 0f));
                float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                // Rounded in SCREEN pixels before the canvas conversion, so the panel lands on the pixel
                // grid instead of shimmering between two of them — the dialogue bubble's lesson.
                anchorPoint = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y)) / scale;
                if (screen.z <= 0f ||
                    !WardrobePickerLayout.AnchorIsOnScreen(anchorPoint, canvasSize,
                                                           WardrobePickerLayout.ScreenMargin))
                {
                    // The wardrobe is behind the camera or off the view. Nothing to hang a panel off.
                    anchorPoint = canvasSize * 0.5f;
                }
            }
            else
            {
                // No camera at all (a headless rig): park it centred so the panel is still assertable.
                anchorPoint = canvasSize * 0.5f;
            }

            var place = WardrobePickerLayout.Solve(
                anchorPoint, _panelSize, canvasSize,
                WardrobePickerLayout.ScreenMargin, WardrobePickerLayout.Gap);

            _panelCentre = place.PanelCentre;
            _panelRect.anchoredPosition = _panelCentre;
        }

        /// <summary>
        /// Which camera the fixture is projected through — the wired one where there is one, else whatever
        /// is tagged MainCamera.
        ///
        /// <para><b>⚠ Explicit <c>!= null</c>, never <c>??</c></b>: a destroyed camera is fake-null, and
        /// the null-propagating operators sail straight past Unity's overloaded <c>==</c>, so a region
        /// reload would leave this pointed at a corpse.</para>
        /// </summary>
        private Camera ResolveCamera()
        {
            if (_camera != null) return _camera;
            Camera main = Camera.main;
            return main != null ? main : null;
        }

        /// <summary>The wardrobe this picker lists — the wired one, else the shipped asset. Same explicit
        /// null discipline as the camera, for the same reason.</summary>
        private CharacterWardrobeDef ResolveWardrobe()
        {
            if (_wardrobe != null) return _wardrobe;
            _wardrobe = Resources.Load<CharacterWardrobeDef>(CharacterWardrobeDef.ResourcesPath);
            return _wardrobe != null ? _wardrobe : null;
        }

        /// <summary>The fixture that raised the signal, by id, or null when it is not registered (a test
        /// driving <see cref="Open"/> directly, a fixture already torn down).</summary>
        private static IInteractable FindFixture(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId)) return null;
            var live = Interactables.Active;
            for (int i = 0; i < live.Count; i++)
                if (live[i] != null && string.Equals(live[i].Id, fixtureId, System.StringComparison.Ordinal))
                    return live[i];
            return null;
        }

        /// <summary>Is this fixture still live? A hand-rolled scan rather than LINQ's <c>Contains</c>:
        /// <see cref="Interactables.Active"/> is an <c>IReadOnlyList</c> (no <c>Contains</c> of its own),
        /// and this runs every frame the panel is up, where an enumerator allocation would be a per-frame
        /// allocation in a UI (rule 7).</summary>
        private static bool IsRegistered(IInteractable fixture)
        {
            var live = Interactables.Active;
            for (int i = 0; i < live.Count; i++)
                if (ReferenceEquals(live[i], fixture)) return true;
            return false;
        }

        /// <summary>Where to hang the panel when the fixture is not registered — the player, if Core knows
        /// of one, and otherwise the origin (which <see cref="Track"/> then centres).</summary>
        private static Vector2 AnchorFallback()
        {
            Transform player = GameServices.PlayerTransform;
            return player != null ? (Vector2)player.position : Vector2.zero;
        }

        /// <summary>Every outfit id the wardrobe holds, in the order it holds them (rule 2: the ORDER is
        /// authoring, not a sort). Skips a null slot or an id-less Def rather than listing a row that
        /// cannot be worn.</summary>
        private static List<string> OutfitIdsOf(CharacterWardrobeDef wardrobe)
        {
            var ids = new List<string>(wardrobe != null ? wardrobe.Count : 0);
            if (wardrobe?.Outfits == null) return ids;

            foreach (var o in wardrobe.Outfits)
                if (o != null && !string.IsNullOrEmpty(o.Id)) ids.Add(o.Id);
            return ids;
        }

        // ---- the panel (code-driven, no prefab) ------------------------------------------------

        private void EnsureBuilt(int rowCount)
        {
            if (_root != null && _rows.Count == rowCount) return;
            if (_root != null) Destroy(_root);
            _rows.Clear();

            if (_canvas == null) BuildCanvas();

            _panelSize = WardrobePickerLayout.PanelSize(rowCount);

            _root = new GameObject("WardrobeRoot", typeof(RectTransform));
            _root.transform.SetParent(_canvas.transform, false);
            var rootRt = (RectTransform)_root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // The edge is a slightly larger rect UNDER the stock, which is a 1 px ink ring without needing
            // a 9-sliced sprite the art lane has not been asked for yet.
            var edge = MakeImage(rootRt, "Edge", PanelEdge);
            edge.rectTransform.anchorMin = Vector2.zero;
            edge.rectTransform.anchorMax = Vector2.zero;
            edge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            edge.rectTransform.sizeDelta = _panelSize + new Vector2(EdgeThickness * 2f, EdgeThickness * 2f);
            _panelRect = edge.rectTransform;

            var stock = MakeImage(_panelRect, "Stock", PanelStock);
            stock.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            stock.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            stock.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            stock.rectTransform.anchoredPosition = Vector2.zero;
            stock.rectTransform.sizeDelta = _panelSize;

            _titleText = MakeText(_panelRect, "Title", TextAnchor.MiddleLeft, TitleFontSize, Ink);
            PlaceInPanel(_titleText.rectTransform,
                         _panelSize.y * 0.5f - WardrobePickerLayout.PadY
                             - WardrobePickerLayout.TitleHeight * 0.5f,
                         WardrobePickerLayout.TitleHeight);
            _titleText.text = WardrobeStrings.Title;

            for (int i = 0; i < rowCount; i++) _rows.Add(BuildRow(i));

            _hintText = MakeText(_panelRect, "Hint", TextAnchor.MiddleCenter, HintFontSize, InkFaint);
            PlaceInPanel(_hintText.rectTransform,
                         -_panelSize.y * 0.5f + WardrobePickerLayout.PadY
                             + WardrobePickerLayout.HintHeight * 0.5f,
                         WardrobePickerLayout.HintHeight);
            _hintText.text = WardrobeStrings.Hint;

            _root.SetActive(false);
        }

        private Row BuildRow(int index)
        {
            var row = new Row();

            var strip = MakeImage(_panelRect, $"Row{index}", RowIdle);
            row.Strip = strip;
            row.Rect = strip.rectTransform;
            PlaceInPanel(row.Rect, WardrobePickerLayout.RowOffsetY(_panelSize, index),
                         WardrobePickerLayout.RowHeight);

            row.Cursor = MakeText(row.Rect, "Cursor", TextAnchor.MiddleLeft, RowFontSize, Ink);
            LeftInRow(row.Cursor.rectTransform, 0f, 12f);

            var chip = MakeImage(row.Rect, "ChipEdge", ChipEdge);
            LeftInRow(chip.rectTransform, 14f, WardrobePickerLayout.ChipSize);
            chip.rectTransform.sizeDelta = new Vector2(WardrobePickerLayout.ChipSize,
                                                       WardrobePickerLayout.ChipSize);

            var chipFill = MakeImage(chip.rectTransform, "Chip", ChipUnknown);
            chipFill.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            chipFill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            chipFill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            chipFill.rectTransform.anchoredPosition = Vector2.zero;
            chipFill.rectTransform.sizeDelta =
                new Vector2(WardrobePickerLayout.ChipSize - 2f, WardrobePickerLayout.ChipSize - 2f);
            row.Chip = chipFill;

            row.Label = MakeText(row.Rect, "Label", TextAnchor.MiddleLeft, RowFontSize, Ink);
            LeftInRow(row.Label.rectTransform, 34f,
                      WardrobePickerLayout.PanelWidth - WardrobePickerLayout.PadX * 2f - 34f);

            return row;
        }

        /// <summary>Redraw the list: labels, chips, the worn mark and the cursor. Runs on a CHANGE, never
        /// per frame (rule 7) — the panel is text on a rect and nothing about it animates.</summary>
        private void DrawRows()
        {
            if (_model == null) return;
            var wardrobe = ResolveWardrobe();

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                string id = _model.OutfitIdAt(i);
                var def = wardrobe != null ? wardrobe.Outfit(id) : null;

                bool selected = i == _model.Index;
                bool worn = string.Equals(id, _model.RevertOutfitId, System.StringComparison.Ordinal);

                row.Cursor.text = selected ? WardrobeStrings.Cursor : "";
                row.Strip.color = selected ? RowSelected : RowIdle;
                row.Label.text = worn
                    ? $"{LabelOf(def, id)} {WardrobeStrings.WornMark}"
                    : LabelOf(def, id);
                row.Chip.color = WardrobeChip.ColourFor(wardrobe, def, ChipUnknown);
            }
        }

        /// <summary>What the row reads. The Def's label where content gave one, and the id itself where it
        /// did not — a row that showed nothing would be a coat you could wear but not name.</summary>
        private static string LabelOf(CharacterOutfitDef def, string id) =>
            def != null && !string.IsNullOrEmpty(def.Label) ? def.Label : id;

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Wardrobe_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRect = canvasGo.GetComponent<RectTransform>();
        }

        /// <summary>Centre a full-width strip in the panel at an offset from the panel's centre.</summary>
        private void PlaceInPanel(RectTransform rt, float offsetY, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, offsetY);
            rt.sizeDelta = new Vector2(_panelSize.x - WardrobePickerLayout.PadX * 2f, height);
        }

        /// <summary>Place a part inside a row, measured from the row's left edge.</summary>
        private static void LeftInRow(RectTransform rt, float x, float width)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(width, WardrobePickerLayout.RowHeight);
        }

        private static Image MakeImage(RectTransform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;   // the picker hit-tests itself; no EventSystem is required
            return img;
        }

        private static Text MakeText(RectTransform parent, string name, TextAnchor align, int fontSize,
                                     Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = DefaultFont();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = colour;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Font DefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
