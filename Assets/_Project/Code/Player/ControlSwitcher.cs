using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The control-mode state machine (OnFoot ⇄ Aboard) — step 2 of the on-foot player, completing the
    /// walk → board → sail → fish/sell/buy → dock → disembark → walk loop. Owns the active mode and
    /// gates which controller receives input: on foot the <see cref="PlayerWalkController"/> drives;
    /// aboard the <see cref="BoatController"/> + its dev input drive. INTERACT (dev key E) boards when
    /// on foot within the dock zone, and disembarks when aboard with the boat back in the dock zone.
    ///
    /// Cross-module via Core only: it toggles the Player + Boats controllers it holds and hands the
    /// camera off through the Core <see cref="ControlModeChanged"/> / <see cref="ActiveBoatChanged"/>
    /// signals — it never references the App camera (that would be a circular dependency). Greybox dev
    /// input + a tiny prompt; a real InputService / interaction UI replace them later (ui-ux).
    /// </summary>
    public class ControlSwitcher : MonoBehaviour
    {
        [Header("Player (on foot)")]
        [SerializeField] private PlayerWalkController _playerWalk;

        [Header("Boat (aboard)")]
        [SerializeField] private BoatController _boatController;
        [Tooltip("The boat's input behaviour (DevBoatInput) — enabled only while aboard.")]
        [SerializeField] private Behaviour _boatInput;

        [Header("Dock zone")]
        [Tooltip("The dock/mooring point. Board when the player is within range; disembark when the boat is.")]
        [SerializeField] private Transform _dockZone;
        [SerializeField] private float _zoneRadius = 3f;
        [Tooltip("Where the on-foot player is placed after disembarking (on the dock).")]
        [SerializeField] private Transform _disembarkPoint;

        public ControlMode Mode { get; private set; } = ControlMode.OnFoot;

        private Text _hint;

        private Transform Player => _playerWalk != null ? _playerWalk.transform : null;
        private Transform Boat   => _boatController != null ? _boatController.transform : null;

        // ---- zone tests (testable via positioned transforms) --------------------------------

        public bool InBoardZone()
            => Player != null && _dockZone != null
               && Vector2.Distance(Player.position, _dockZone.position) <= _zoneRadius;

        public bool InDockZone()
            => Boat != null && _dockZone != null
               && Vector2.Distance(Boat.position, _dockZone.position) <= _zoneRadius;

        /// <summary>True if INTERACT would transition right now (in the right zone for the current mode).</summary>
        public bool CanInteract()
            => Mode == ControlMode.OnFoot ? InBoardZone() : InDockZone();

        /// <summary>Attempt the board/disembark transition. Returns true if it happened.</summary>
        public bool TryInteract()
        {
            if (!CanInteract()) return false;
            if (Mode == ControlMode.OnFoot) Board();
            else Disembark();
            return true;
        }

        // ---- transitions --------------------------------------------------------------------

        private void Board()
        {
            SetPlayerActive(false);                                  // hide & freeze the on-foot player
            if (_boatController != null) _boatController.enabled = true;
            if (_boatInput != null) _boatInput.enabled = true;
            Mode = ControlMode.Aboard;

            // Camera: zoom to the boat's framing (#17 ActiveBoatChanged) + retarget to it (ControlModeChanged).
            BoatHullDef hull = _boatController != null ? _boatController.Hull : null;
            float height = hull != null ? hull.CameraWorldHeightMeters : 14f;
            string id = hull != null ? hull.Id : null;
            EventBus.Publish(new ActiveBoatChanged(id, height));
            EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));
        }

        private void Disembark()
        {
            if (_boatController != null) _boatController.enabled = false;
            if (_boatInput != null) _boatInput.enabled = false;

            if (Player != null && _disembarkPoint != null)
                Player.position = _disembarkPoint.position;          // step back onto the dock
            SetPlayerActive(true);
            Mode = ControlMode.OnFoot;

            // Camera: retarget to the player + reframe to the on-foot view (CameraFollow owns the value).
            EventBus.Publish(new ControlModeChanged(ControlMode.OnFoot));
        }

        private void SetPlayerActive(bool active)
        {
            if (_playerWalk == null) return;
            _playerWalk.enabled = active;
            var sr = _playerWalk.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = active;
            var rb = _playerWalk.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;        // no residual drift while hidden
        }

        /// <summary>Wire the switcher in one call (tests / editor) and start on foot.</summary>
        public void Configure(PlayerWalkController playerWalk, BoatController boatController, Behaviour boatInput,
                              Transform dockZone, float zoneRadius, Transform disembarkPoint)
        {
            _playerWalk = playerWalk;
            _boatController = boatController;
            _boatInput = boatInput;
            _dockZone = dockZone;
            _zoneRadius = zoneRadius;
            _disembarkPoint = disembarkPoint;
            Mode = ControlMode.OnFoot;
        }

        // ---- lifecycle (greybox dev input + prompt) -----------------------------------------

        private void Awake() => BuildHint();

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) TryInteract();
            UpdateHint();
        }

        private void UpdateHint()
        {
            if (_hint == null) return;
            bool show = CanInteract();
            if (_hint.enabled != show) _hint.enabled = show;
            if (show)
            {
                string text = Mode == ControlMode.OnFoot ? "E: Board" : "E: Dock";
                if (_hint.text != text) _hint.text = text;
            }
        }

        // A tiny screen-space prompt shown only when a board/dock is available (nice-to-have; ui-ux polishes).
        private void BuildHint()
        {
            var canvasGo = new GameObject("ControlHint_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 160f);
            rt.sizeDelta = new Vector2(400f, 60f);

            _hint = go.GetComponent<Text>();
            _hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hint.fontSize = 34;
            _hint.alignment = TextAnchor.LowerCenter;
            _hint.color = Color.white;
            _hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hint.verticalOverflow = VerticalWrapMode.Overflow;
            _hint.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            _hint.enabled = false;
        }
    }
}
