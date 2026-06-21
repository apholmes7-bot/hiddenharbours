using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Light onboarding (VS-21): a single gentle hint line that points the player through one full
    /// loop — meet Aunt Ginny → take the dory out → cast and land a fish → sell at the wharf — then
    /// bows out. It reads the opening flags (met Ginny) and listens on Core signals
    /// (<see cref="ControlModeChanged"/>, <see cref="FishCaught"/>, <see cref="CatchSold"/>) to know
    /// which step you're on; it never references another module's types.
    ///
    /// Persistence: the loop's completion is the first sale, which sets <c>onboarded</c> via
    /// <see cref="OnboardingFlags"/> (PlayerPrefs-backed). On a later run the flag is already set, so
    /// the hint never shows again — the opening doesn't nag returning players. Deliberately minimal:
    /// no quests, no routines (that's M2), just one self-dismissing nudge.
    /// </summary>
    public sealed class OnboardingDirector : MonoBehaviour
    {
        [Tooltip("How long the closing 'that's the loop' line lingers after the first sale (real seconds).")]
        [SerializeField] private float _doneSeconds = 4f;

        private OnboardingFlags _flags;
        private Text _hint;

        private bool _boarded;
        private bool _caught;
        private float _doneTimer;
        private bool _subscribed;

        private void Awake()
        {
            _flags = new OnboardingFlags(new PlayerPrefsFlagStore());
            BuildHint();
        }

        private void OnEnable()  => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed) return;
            EventBus.Subscribe<ControlModeChanged>(OnMode);
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<CatchSold>(OnSold);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<ControlModeChanged>(OnMode);
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Unsubscribe<CatchSold>(OnSold);
            _subscribed = false;
        }

        private void Update()
        {
            if (_hint == null) return;

            // Once the loop's done, show the closing line briefly, then go quiet for good.
            if (_flags.Onboarded)
            {
                if (_doneTimer > 0f)
                {
                    _doneTimer -= Time.unscaledDeltaTime;
                    SetHint(WorldStrings.OnboardDone);
                }
                else SetHint(null);
                return;
            }

            SetHint(NextStep());
        }

        private string NextStep()
        {
            if (!_flags.MetGinny) return WorldStrings.OnboardTalkGinny;
            if (!_boarded)        return WorldStrings.OnboardGoFish;
            if (!_caught)         return WorldStrings.OnboardCast;
            return WorldStrings.OnboardSell;
        }

        // ---- signals ------------------------------------------------------------------------

        private void OnMode(ControlModeChanged e)
        {
            if (e.Mode == ControlMode.Aboard) _boarded = true;
        }

        private void OnCaught(FishCaught e) => _caught = true;

        private void OnSold(CatchSold e)
        {
            if (_flags.Onboarded) return;
            _flags.Onboarded = true;     // the loop is closed — persist so the opening never re-triggers
            _doneTimer = _doneSeconds;
        }

        // ---- hint label ---------------------------------------------------------------------

        private void SetHint(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (_hint.enabled != show) _hint.enabled = show;
            if (show && _hint.text != text) _hint.text = text;
        }

        private void BuildHint()
        {
            var canvasGo = new GameObject("Onboarding_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 94;
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
            rt.anchoredPosition = new Vector2(0f, 70f); // a low banner, clear of the board/dock hint
            rt.sizeDelta = new Vector2(900f, 44f);

            _hint = go.GetComponent<Text>();
            _hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hint.fontSize = 26;
            _hint.alignment = TextAnchor.LowerCenter;
            _hint.color = new Color(1f, 0.96f, 0.85f); // warm parchment
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
