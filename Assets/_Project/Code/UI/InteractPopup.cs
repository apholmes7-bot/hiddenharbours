using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>The one small popup, lower right</b> — the single element the owner's 2026-08-19 ruling put in
    /// place of every floating world prompt in the game.
    ///
    /// <para><b>The ruling, restated because this component is its whole implementation.</b> The UI is four
    /// surfaces — boat instruments, watch face, notebook, dialogue — plus this: a small panel, fixed in the
    /// lower right, showing at most ONE line naming the interaction available on the trigger the player is
    /// FACING, and showing nothing when there is no facing candidate. It never stacks and it never queues.
    /// The "E: Talk to Aunt Ginny" that used to hang over the world, and the "E: Board" that used to sit
    /// above it, are both gone; this says "Talk — Aunt Ginny" and "Climb aboard" instead, and it never
    /// names the key.</para>
    ///
    /// <para><b>It computes nothing.</b> Every word it draws arrives on
    /// <see cref="InteractOfferChanged"/>, published by <see cref="InteractOffer"/> from the answers the
    /// three readers of the interact key have already worked out — the switcher's transitions,
    /// <c>WorldInteractor</c>'s conversations, and the <see cref="InteractVerb"/> registry. That is the
    /// point of the seam: a popup that re-derived candidacy would be a fourth, lagging opinion about what
    /// the press does, and would eventually disagree with the press itself. This class subscribes, trims,
    /// and toggles.</para>
    ///
    /// <para><b>A one-line speech bubble</b> (owner ruling: the action UI wears "the same design
    /// language as our notebook and chat bubbles"): the bubble kit's cream panel with its ink ring and
    /// chamfered corners (<see cref="BubbleKitLink"/>, the bubble's own sprite), the game's one face
    /// (<see cref="HarbourType"/>) in notebook ink, every measure a whole number of kit pixels — the
    /// same dress as <see cref="InteractChooserView"/>. The PLACED rect (<see cref="Panel"/>) is the
    /// frame the helm and the quest note step around; the drawn bubble hangs off its right edge and
    /// widens leftward for a long label.</para>
    ///
    /// <para><b>Two things suppress it beyond "no offer".</b> A modal that is up
    /// (<see cref="InteractionGate"/>, and the shell holding the world) and the two views where the boat's
    /// own instruments rule — <see cref="ControlMode.Aboard"/> and <see cref="ControlMode.Driving"/>. The
    /// feeders already go quiet under the gate, so this is belt-and-braces rather than the only latch, and
    /// deliberately so: this codebase's own rule is that a gate holding on one of two paths is not a
    /// gate.</para>
    ///
    /// <para><b>Rule 7.</b> The panel is built ONCE in <see cref="Awake"/> and reused; the label is written
    /// only when the offer changes (event-driven, never polled); <see cref="Update"/> reads two static
    /// bools and touches nothing unless the answer changed. Nothing here allocates per frame —
    /// <see cref="InteractPopupLayout.Fit"/> returns its input unchanged on the common path.</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class InteractPopup : MonoBehaviour
    {
        private static InteractPopup _instance;

        /// <summary>
        /// Self-install once when the game starts — no builder run and no scene wiring, following
        /// <see cref="RegionFadeOverlay"/>'s precedent exactly.
        ///
        /// <para><b>Why not added by the scene builders, the way the HUD is.</b> Because the thing it
        /// replaces was not in a scene either: every prompt it retires was built at runtime by the
        /// component that owned it (<c>WorldInteractor</c>, <c>ControlSwitcher</c>), so a popup that needed
        /// a builder re-run would leave every already-saved scene — including a region scene opened
        /// directly for review — with no interaction affordance at all until somebody remembered. Guarded
        /// on <see cref="_instance"/>, so it is one popup however many times this runs.</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("InteractPopup");
            _instance = go.AddComponent<InteractPopup>();
        }

        /// <summary>The installed popup, or null. For a test that wants the live one rather than its
        /// own.</summary>
        public static InteractPopup Instance => _instance;

        [Tooltip("Persist across scene loads like the HUD and the services. The popup is always-on.")]
        [SerializeField] private bool _persistAcrossScenes = true;

        private GameObject _canvasGo;
        private RectTransform _panel;
        private RectTransform _bubble;
        private Image _bubbleImage;
        private Text _label;

        // What is currently offered, and what is currently DRAWN. Two facts, because the drawn state is a
        // function of the offer AND the suppressions, and only the combination decides whether anything is
        // on screen.
        private InteractOfferChanged _offer;
        private bool _shown;
        private string _painted;

        // What the panel is placed FOR: the helm's footprint and the screen (ADR 0050). Re-placed only
        // on a change, so a quiet frame costs a struct compare and two int compares.
        private HelmFootprintArea _placedFor;
        private int _placedW = -1, _placedH = -1;

        // ...and the quest note's box (TASKS, QuestNoteFootprint): the prompt never sits on the note.
        private Rect _placedTasks;

        private void Awake()
        {
            // A popup stood up by hand (a test, a review scene) claims the slot if nothing has yet, so the
            // self-install below it is a no-op rather than a second panel.
            if (_instance == null) _instance = this;
            Build();
            if (_persistAcrossScenes && Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InteractOfferChanged>(OnOffer);
            EventBus.Subscribe<QuestNoteFootprintChanged>(OnQuestNoteMoved);

            // Catch up with whatever is already standing. The offer channel is edge-triggered, so a popup
            // enabled after the player has walked up to a fixture would otherwise sit blank until they
            // stepped away and back.
            _offer = InteractOffer.Current;
            Apply();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InteractOfferChanged>(OnOffer);
            EventBus.Unsubscribe<QuestNoteFootprintChanged>(OnQuestNoteMoved);
        }

        // Re-placed the moment the note says it moved, not a frame later: the note places itself in
        // LateUpdate, and a poll here would leave the prompt on it for one drawn frame.
        private void OnQuestNoteMoved(QuestNoteFootprintChanged e) => Place();

        private void OnOffer(InteractOfferChanged e) { _offer = e; Apply(); }

        /// <summary>
        /// Re-read the things that are FLAGS rather than signals, once a frame.
        ///
        /// <para><b>And reconcile with <see cref="InteractOffer.Current"/></b>, which is the subscription's
        /// safety net rather than a second source: this popup is <c>DontDestroyOnLoad</c> and lives for the
        /// whole session, while <see cref="EventBus.Clear{T}"/> is what every test suite in this project
        /// uses to isolate itself — one <c>Clear&lt;InteractOfferChanged&gt;</c> anywhere would otherwise
        /// leave the shipped popup permanently deaf, in the build as well as the suite. The compare is a
        /// field-by-field struct test on a settled value (<see cref="InteractOfferChanged.SameAs"/>), so a
        /// quiet frame costs five compares and touches nothing.</para>
        /// </summary>
        private void Update()
        {
            InteractOfferChanged live = InteractOffer.Current;
            if (!live.SameAs(_offer)) _offer = live;
            Apply();
            Place();
        }

        /// <summary>
        /// Keep the panel out of what the helm's UI covers (ADR 0050) — read from the Core footprint
        /// seam, never from the helm's own classes. Belt and braces: the helm and the cab already
        /// suppress the popup (<see cref="Suppressed"/>), so this only matters where an offer stands
        /// beside a helm card. Nothing covered puts it exactly where it was built.
        /// </summary>
        private void Place()
        {
            if (_panel == null) return;
            HelmFootprintArea covered = HelmFootprint.Current;
            int w = HudScreen.Width, h = HudScreen.Height;
            Rect tasks = QuestNoteFootprint.Current;
            if (covered == _placedFor && w == _placedW && h == _placedH && tasks == _placedTasks) return;
            _placedTasks = tasks;
            _placedFor = covered;
            _placedW = w;
            _placedH = h;
            _panel.anchoredPosition = InteractPopupLayout.PanelAnchoredPosition(in covered, tasks, w, h);
            FitBubble();
        }

        /// <summary>
        /// Show what is offered, or nothing — the single place the drawn state is decided.
        ///
        /// <para>Idempotent and change-detected at both levels: the panel's active flag is written only
        /// when visibility flips, and the label's text only when the words differ. That is what lets this
        /// be called from three event handlers and every frame without costing anything.</para>
        /// </summary>
        private void Apply()
        {
            if (_panel == null) return;

            bool show = _offer.Has && !Suppressed();
            if (show)
            {
                string text = InteractPopupLayout.Fit(_offer.Label);
                if (!string.Equals(text, _painted, System.StringComparison.Ordinal))
                {
                    _painted = text;
                    _label.text = text;
                    FitBubble();
                }
            }

            if (_shown == show) return;
            _shown = show;
            _panel.gameObject.SetActive(show);
        }

        /// <summary>
        /// Is anything entitled to stop the popup being drawn, regardless of what is offered?
        ///
        /// <para><b>The gate and the shell</b> — a dialogue bubble or a picker owns the interact key, or the
        /// title page / pause menu owns the world. In both cases the press the popup describes cannot
        /// happen, and a popup that outlives its own actionability teaches the player a lie (the same rule
        /// the interact highlight is held to).</para>
        ///
        /// <para><b>The helm and the cab.</b> Not because the press is dead there — it steps you back — but
        /// because the owner's ruling gives those two views to the boat's own instruments, and a paper note
        /// over a dash is the plain-text band returning by the side door.
        /// <see cref="InteractContext.OnDeck"/> is deliberately NOT suppressed: the deck is a place you
        /// work, there is no dash up, and its offers are ordinary offers.</para>
        ///
        /// <para><b>Where the mode comes from, and why it is not a subscription.</b>
        /// <see cref="InteractActorProbe"/> carries the actor's context, written by the same component and
        /// on the same frame as the transit offers themselves — so the popup cannot be showing a boarding
        /// line while believing it is at a helm. A <see cref="ControlModeChanged"/> subscription would be
        /// a second copy of that fact that only updates on transitions, and one
        /// <see cref="EventBus.Clear{T}"/> in any test suite would leave this session-long component deaf
        /// to it for good.</para>
        ///
        /// <para><b>No probe means no suppression</b>, matching the probe's own fail-open contract: a
        /// region scene with no persistent rig has nobody at a helm, so guessing "suppressed" there would
        /// hide the popup for the whole scene.</para>
        /// </summary>
        /// <remarks>
        /// <b>The rule itself now lives in Core</b> (<see cref="InteractOfferVisibility"/>), unchanged and
        /// for one reason: the affordance lane lights the fixture this popup names, and the two are one
        /// statement to the player. Two copies of "may this be shown?" would drift, and the drift is
        /// visible — a lit bed under an empty corner. One rule, both surfaces.
        /// </remarks>
        private bool Suppressed() => InteractOfferVisibility.Suppressed;

        // ---- the one panel, built once ---------------------------------------------------------

        private void Build()
        {
            RectTransform host = PaperUi.MakeScreen(transform, "InteractPopup_Canvas",
                                                    InteractPopupLayout.SortingOrder);
            _canvasGo = host.parent.gameObject;
            // A kit pixel is Kit reference units, so a 9-slice border of N kit pixels must draw N x Kit:
            // the reference PPU is the sprites' PPU times the art scale (the bubble's canvas rule). The
            // scaler owns that number and writes it to the canvas, so it is set there.
            float refPpu = DialogueBubbleKit.PixelsPerUnit * DialogueBubbleKit.ArtScale;
            CanvasScaler scaler = _canvasGo.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.referencePixelsPerUnit = refPpu;
            Canvas canvas = _canvasGo.GetComponent<Canvas>();
            if (canvas != null) canvas.referencePixelsPerUnit = refPpu;

            // The PLACED rect: exactly where the layout says, at the placed size — the frame the helm and
            // the quest note are stepped around. It draws nothing itself.
            // No GraphicRaycaster work to do: the popup is a note, not a control; every graphic below sets
            // raycastTarget = false so it can never eat a click meant for the world.
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(host, false);
            _panel = go.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = InteractPopupLayout.PanelAnchoredPosition();
            _panel.sizeDelta = InteractPopupLayout.PanelSize();

            // The DRAWN bubble: a one-line speech bubble hung off the frame's right edge, as wide as its
            // words, in the bubble's own panel sprite (BubbleKitLink — a reference, not a copy).
            BubbleKitLink kit = BubbleKitLink.Shipped;
            Sprite panelArt = kit != null ? kit.Panel : null;
            var bubbleGo = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
            bubbleGo.transform.SetParent(_panel, false);
            _bubble = bubbleGo.GetComponent<RectTransform>();
            _bubble.anchorMin = new Vector2(1f, 1f);
            _bubble.anchorMax = new Vector2(1f, 1f);
            _bubble.pivot = new Vector2(1f, 1f);
            _bubble.sizeDelta = InteractPopupLayout.PanelSize();
            _bubbleImage = bubbleGo.GetComponent<Image>();
            _bubbleImage.sprite = panelArt;
            _bubbleImage.type = panelArt != null && panelArt.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            _bubbleImage.color = InteractChooserStyle.PanelTint(panelArt != null);
            _bubbleImage.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(_bubble, false);
            RectTransform lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f);
            lrt.anchoredPosition = InteractPopupLayout.TextAnchoredPosition();
            lrt.sizeDelta = InteractPopupLayout.TextSize();
            _label = labelGo.GetComponent<Text>();
            Font face = Resources.Load<Font>(HarbourType.ResourceKey);
            _label.font = face != null ? face : PaperUi.DefaultFont();
            _label.fontSize = InteractPopupLayout.FontSize;
            // UPPER-left, as the speech bubble's body: the line's top sits a whole number of kit pixels
            // into the bubble. Middle alignment would centre a 10-px line in an 8-px box — half a kit
            // pixel off the grid, which is where broken glyphs come from.
            _label.alignment = TextAnchor.UpperLeft;
            _label.supportRichText = false;
            _label.color = InteractChooserStyle.Words;
            _label.raycastTarget = false;
            // One line, cut rather than wrapped — the layout rule has already trimmed the string, and this
            // is what stops a face-metric surprise turning it into two lines anyway.
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;

            _panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Size the drawn bubble to its words and put its top-left corner — where the words start — on a
        /// whole screen pixel. Called when the words change and when the panel is re-placed; never per
        /// quiet frame.
        /// </summary>
        private void FitBubble()
        {
            if (_bubble == null) return;
            string text = _label.text;
            float width = InteractPopupLayout.BubbleWidth(
                Mathf.Max(_label.preferredWidth, InteractPopupLayout.EstimateTextUnits(text)));
            _label.rectTransform.sizeDelta = new Vector2(width - 2f * InteractPopupLayout.TextPadX,
                                                         InteractPopupLayout.TextSize().y);

            // Right edge first (the chooser snaps the same edge the same way), then the left from it.
            float s = HudBandLayout.ScaleFactor(HudScreen.Width, HudScreen.Height);
            Vector2 frame = _panel.anchoredPosition;
            float right = frame.x + InteractPopupLayout.PanelWidth;
            Vector2 span = InteractPopupLayout.SnapSpan(right - width, width, s);
            float top = frame.y;
            _bubble.sizeDelta = new Vector2(span.y, InteractPopupLayout.PanelHeight);
            _bubble.anchoredPosition = new Vector2(span.x + span.y - right,
                                                   InteractPopupLayout.SnapToScreen(top, s) - top);
        }

        // ---- test / tooling seam ----------------------------------------------------------------

        /// <summary>
        /// The line currently on screen, or "" when the popup is hiding.
        ///
        /// <para><b>Public on purpose</b>, the #580 pattern: this is the readable half of "what is the game
        /// offering", and reading the <see cref="Text"/> component would be reading pixels' next-of-kin
        /// rather than the decision. A PlayMode test asserts on this.</para>
        /// </summary>
        public string ShownLabel => _shown ? _painted : "";

        /// <summary>True while the panel is actually drawn. Distinct from "something is offered" — the
        /// suppressions live between the two, and a test of the modal rule needs to see the
        /// difference.</summary>
        public bool IsShowing => _shown;

        /// <summary>The offer the popup is currently holding, drawn or not. For a test that wants to prove
        /// the suppression rather than the plumbing.</summary>
        public InteractOfferChanged Offer => _offer;

        /// <summary>The panel's rect, for a layout test that wants to check where it actually landed
        /// rather than only what <see cref="InteractPopupLayout"/> says it should.</summary>
        public RectTransform Panel => _panel;

        /// <summary>The drawn bubble inside <see cref="Panel"/> (its size follows the words).</summary>
        public RectTransform Bubble => _bubble;

        /// <summary>The sprite the bubble wears (the bubble kit's panel, or null when unlinked).</summary>
        public Sprite BubbleArt => _bubbleImage != null ? _bubbleImage.sprite : null;

        /// <summary>The face the line is set in.</summary>
        public Font ShownFace => _label != null ? _label.font : null;

        /// <summary>The canvas this popup built, so a test can tear the whole thing down in one
        /// call.</summary>
        public GameObject CanvasObject => _canvasGo;
    }
}
