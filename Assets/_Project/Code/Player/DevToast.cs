using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// DELIBERATELY-UGLY greybox on-screen feedback ("toasts") for the trap loop — so the owner never
    /// needs the Unity Console to see placement refusals ("Too shallow here"), bait grants ("+5 herring
    /// bait"), sets ("Pot set") and haul results ("Caught: rock crab!"). It listens for the Core
    /// <see cref="DevNotice"/> signal (and <see cref="FishCaught"/>, to name the landed catch), so the
    /// systems that RAISE the messages (Fishing etc.) never reference this — or any — UI class (rule 4:
    /// cross-module talk through Core). Scaffolding only: the diegetic-UI direction replaces it wholesale.
    ///
    /// <para><b>Rule 7 (no per-frame allocations).</b> The small pool of Text entries is pre-built once
    /// in Awake and reused round-robin — a message never Instantiates/Destroys anything, and the
    /// per-frame fade only rewrites each active entry's colour alpha (no strings, no boxing). The only
    /// allocation is the message string itself, made by the publisher at event time (a keypress), never
    /// per frame.</para>
    ///
    /// <para>Mirrors the <see cref="ControlSwitcher"/> hint's self-built screen-space canvas (the greybox
    /// convention); it stacks just above that hint so the two never overlap.</para>
    /// </summary>
    public sealed class DevToast : MonoBehaviour
    {
        [Header("Toast feel (greybox tunables, rule 6)")]
        [Tooltip("How long (s) a toast stays fully readable before it starts to fade.")]
        [SerializeField] private float _visibleSeconds = 2.2f;
        [Tooltip("How long (s) the fade-out takes after the visible time.")]
        [SerializeField] private float _fadeSeconds = 0.8f;
        [Tooltip("How many toasts can be on screen at once (the pre-allocated pool). The oldest is " +
                 "reused when a new message arrives with the pool full.")]
        [Min(1)][SerializeField] private int _maxMessages = 4;
        [Tooltip("Toast font size (greybox).")]
        [SerializeField] private int _fontSize = 30;
        [Tooltip("Vertical gap (px at the 1280×720 reference) between stacked toasts.")]
        [SerializeField] private float _lineSpacing = 38f;
        [Tooltip("Anchored Y (px at the 1280×720 reference) of the lowest toast line — above the E-hint.")]
        [SerializeField] private float _baseY = 230f;

        private sealed class Entry
        {
            public Text Text;
            public float Age;       // seconds since shown; > visible+fade = free
            public bool Active;
        }

        private Entry[] _pool;
        private int[] _order;       // pre-allocated restack scratch (rule 7 — no per-message GC)
        private int _next;          // round-robin cursor into the pool

        // How far (reference units) the whole stack stands above its authored place to keep out of
        // what the helm's UI covers (ADR 0050), and the footprint + screen that was worked out for.
        // 0 with nothing covered: the authored place, bit-exact.
        private float _lift;
        private HelmFootprintArea _liftFor;
        private int _liftW = -1, _liftH = -1;

        /// <summary>The stack's current lift, reference units (test seam).</summary>
        public float Lift => _lift;

        // The canvas this draws on, and each toast line's box — reference units.
        private const float RefW = 1280f, RefH = 720f, MatchWidthOrHeight = 0.5f;
        private const float EntryWidth = 640f, EntryHeight = 44f;

        private void Awake() => BuildPool();

        private void OnEnable()
        {
            EventBus.Subscribe<DevNotice>(OnNotice);
            EventBus.Subscribe<FishCaught>(OnFishCaught);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
        }

        /// <summary>Show a message now (also the <see cref="DevNotice"/> handler). Public so a
        /// test/tool can drive it without the bus.</summary>
        public void Show(string message)
        {
            if (_pool == null || string.IsNullOrEmpty(message)) return;
            Entry e = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            e.Age = 0f;
            e.Active = true;
            if (e.Text != null)
            {
                e.Text.text = message;
                var c = e.Text.color; c.a = 1f; e.Text.color = c;
                e.Text.enabled = true;
                e.Text.transform.SetAsLastSibling();   // newest at the visual top of the stack
            }
            Restack();
        }

        private void OnNotice(DevNotice e) => Show(e.Text);

        // Name the landed catch (the haul/rod land path both raise FishCaught through Core) — event-time
        // string build only, never per frame.
        private void OnFishCaught(FishCaught e) => Show("Caught: " + e.Item.DisplayName + "!");

        private void Update()
        {
            if (_pool == null) return;
            RefreshLift();
            float dt = Time.unscaledDeltaTime;
            float lifetime = _visibleSeconds + _fadeSeconds;
            for (int i = 0; i < _pool.Length; i++)
            {
                Entry e = _pool[i];
                if (!e.Active) continue;
                e.Age += dt;
                if (e.Age >= lifetime)
                {
                    e.Active = false;
                    if (e.Text != null) e.Text.enabled = false;
                    continue;
                }
                if (e.Text != null && e.Age > _visibleSeconds && _fadeSeconds > 0f)
                {
                    var c = e.Text.color;
                    c.a = 1f - (e.Age - _visibleSeconds) / _fadeSeconds;
                    e.Text.color = c;
                }
            }
        }

        // Keep active toasts stacked oldest-at-bottom, newest-on-top. Called only when a message arrives
        // (never per frame); the scratch index array is pre-allocated with the pool — no GC (rule 7).
        private void Restack()
        {
            int count = 0;
            for (int i = 0; i < _pool.Length; i++)
                if (_pool[i].Active) _order[count++] = i;

            // Insertion-sort by age DESCENDING (oldest first → bottom slot). Tiny N (≤ _maxMessages).
            for (int i = 1; i < count; i++)
            {
                int key = _order[i];
                float age = _pool[key].Age;
                int j = i - 1;
                while (j >= 0 && _pool[_order[j]].Age < age) { _order[j + 1] = _order[j]; j--; }
                _order[j + 1] = key;
            }

            for (int slot = 0; slot < count; slot++)
                PlaceAt(_pool[_order[slot]], slot);
        }

        private void PlaceAt(Entry e, int slotFromBottom)
        {
            if (e.Text == null) return;
            var rt = (RectTransform)e.Text.transform;
            rt.anchoredPosition = new Vector2(0f, SlotY(_baseY, _lift, slotFromBottom, _lineSpacing));
        }

        // ---- keeping out of what the helm covers (ADR 0050) -----------------------------------------

        // Re-read the Core footprint seam (never the helm's own classes — rule 4) and restack on a
        // change only: a quiet frame is a struct compare and two int compares.
        private void RefreshLift()
        {
            HelmFootprintArea covered = HelmFootprint.Current;
            int w = Screen.width, h = Screen.height;
            if (covered == _liftFor && w == _liftW && h == _liftH) return;
            _liftFor = covered;
            _liftW = w;
            _liftH = h;
            float lift = LiftRef(in covered, w, h, _baseY, _lineSpacing, _pool.Length);
            if (lift == _lift) return;
            _lift = lift;
            Restack();
        }

        /// <summary>A toast line's anchored y (reference units): its slot above the stack's base, the
        /// whole stack raised by <paramref name="lift"/>. With no lift, the authored place exactly.</summary>
        public static float SlotY(float baseY, float lift, int slotFromBottom, float lineSpacing)
            => lift > 0f ? baseY + lift + slotFromBottom * lineSpacing
                         : baseY + slotFromBottom * lineSpacing;

        /// <summary>
        /// How far (reference units) the stack rises to keep out of what the helm's UI covers: above a
        /// full-width band (the band becomes the edge the stack stood on), above a card that reaches any
        /// of its <paramref name="slots"/> lines. Exactly 0 with nothing covered.
        /// </summary>
        public static float LiftRef(in HelmFootprintArea covered, float screenW, float screenH,
                                    float baseY, float lineSpacing, int slots)
        {
            if (covered.IsNone) return 0f;
            float px = covered.LiftToClear(StackScreenRect(screenW, screenH, baseY, lineSpacing, slots), 0f);
            return px > 0f ? px / CanvasScale(screenW, screenH) : 0f;
        }

        /// <summary>The column every toast line can occupy, in SCREEN pixels (bottom-left origin):
        /// centred, from the lowest line's foot to the top of the highest of
        /// <paramref name="slots"/>.</summary>
        public static Rect StackScreenRect(float screenW, float screenH, float baseY, float lineSpacing,
                                           int slots)
        {
            float s = CanvasScale(screenW, screenH);
            float bottom = baseY * s;
            float top = (baseY + Mathf.Max(0, slots - 1) * lineSpacing + EntryHeight) * s;
            float w = EntryWidth * s;
            return new Rect(screenW * 0.5f - w * 0.5f, bottom, w, top - bottom);
        }

        // The canvas scaler's own factor for this canvas (ScaleWithScreenSize, 1280×720, match 0.5).
        private static float CanvasScale(float screenW, float screenH)
        {
            if (!(screenW > 0f) || !(screenH > 0f)) return 1f;
            return Mathf.Pow(screenW / RefW, 1f - MatchWidthOrHeight)
                 * Mathf.Pow(screenH / RefH, MatchWidthOrHeight);
        }

        // ---- the pre-allocated pool (built once; nothing is created per message) ----------------

        private void BuildPool()
        {
            var canvasGo = new GameObject("DevToast_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 96;   // just above the ControlSwitcher hint (95)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = MatchWidthOrHeight;

            _pool = new Entry[Mathf.Max(1, _maxMessages)];
            _order = new int[_pool.Length];
            for (int i = 0; i < _pool.Length; i++)
            {
                var go = new GameObject("Toast_" + i, typeof(RectTransform), typeof(Text), typeof(Outline));
                go.transform.SetParent(canvasGo.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, _baseY + i * _lineSpacing);
                rt.sizeDelta = new Vector2(EntryWidth, EntryHeight);

                var text = go.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = _fontSize;
                text.alignment = TextAnchor.LowerCenter;
                text.color = new Color(1f, 0.95f, 0.75f, 1f);   // parchment-ish so it reads as dev, not HUD
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.enabled = false;

                var outline = go.GetComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(2f, -2f);

                _pool[i] = new Entry { Text = text, Age = 0f, Active = false };
            }
        }
    }
}
