using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE COAST TALKS</b> — the pool of unprompted speech bubbles the owner ruled for on 2026-09-06:
    /// <i>"npcs to engage in conversation with each other and the conversations to be visible bubbles as
    /// they talk, the player can still walk and run as normal. These speech bubbles can also be used by
    /// the player to narrate their internal dialogue."</i>
    ///
    /// <para><b>The law, and it is a test.</b> Nothing here touches the player. It never raises
    /// <see cref="InteractionGate"/>, never claims the move axis, never reads a key, and has no press to
    /// wait for: each bubble fills at its speaker's cadence, stands for the read pause, and closes
    /// itself (<see cref="AmbientDwell"/>). <c>AmbientSpeechPlayTests</c> walks the player through a
    /// line and asserts she kept moving on every frame and the bubble went away unpressed — because a
    /// comment saying "non-blocking" is not a guarantee, and this is the one property the whole feature
    /// is for.</para>
    ///
    /// <para><b>Why a second presenter rather than a wider <see cref="DialoguePresenter"/>.</b> The modal
    /// one is a conversation: it has ONE slot, one runner, one option picker, a gate and a press, and
    /// every one of those is right for the thing it draws. Ambient speech is the opposite shape — several
    /// at once, no state to advance, no way to interrupt it. Widening the modal to hold a pool would have
    /// put "am I the interruptible one?" inside every method it has. So the two share what is genuinely
    /// shared and nothing else: the kit's numbers (<see cref="DialogueBubbleKit"/>), the screen-edge
    /// solve (<see cref="DialogueBubbleLayout"/>), the fill and its audio ticks
    /// (<see cref="DialogueTypewriter"/>) — and, at runtime, the modal presenter's own wired sprites, so
    /// there is exactly ONE place in the project where the bubble is dressed.</para>
    ///
    /// <para><b>The modal always wins.</b> If the player presses Talk on somebody who is mid-chat, the
    /// ambient bubble over that speaker is cancelled the same frame the conversation opens (owner Q2, and
    /// the natural reading of the design's own "walking away is how you end a conversation"). One person
    /// never has two bubbles.</para>
    ///
    /// <para><b>Self-installing (the <c>DayNightController</c> / <c>GullFlock</c> idiom).</b> One hidden
    /// persistent host per launch, so ambient speech works in every region — built, greybox, or a
    /// PlayMode fixture — with no scene wiring and no builder change. It draws at canvas sorting 109, one
    /// under the modal bubble's 110: an overheard line must never cover the conversation the player is
    /// actually in.</para>
    ///
    /// <para><b>Rule 4 clean.</b> It subscribes to <see cref="AmbientSpeechRequested"/> and publishes
    /// <see cref="AmbientSpeechEnded"/> and <see cref="DialogueTypewriterTick"/>. No feature module is
    /// named by it and it names none.</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class AmbientSpeechPresenter : MonoBehaviour
    {
        // ---- constants ------------------------------------------------------------------------

        const float Scale = DialogueBubbleKit.ArtScale;

        /// <summary>How many ambient bubbles may be up at once. The pool is this size and never grows:
        /// a bubble is built on wake and reused forever, so a chatty harbour allocates nothing per line
        /// (rule 7). The number, and the reasoning for it, live in
        /// <see cref="DialogueBubbleKit.AmbientBubbleCap"/>.</summary>
        public const int MaxConcurrent = DialogueBubbleKit.AmbientBubbleCap;

        /// <summary>One under the modal bubble (110). An overheard line never covers the conversation the
        /// player is in.</summary>
        public const int SortingOrder = 109;

        /// <summary>
        /// <b>What the player's own thought looks like</b> — owner ruling 2026-09-06, asked before it was
        /// invented because the bubble kit draws six SPEECH tails and no thought variant: an inner-voice
        /// bubble is <b>tailless</b> and a shade <b>cooler</b> than a spoken one.
        ///
        /// <para>A multiplier over whatever the speech bubble is already drawing, not a colour of its
        /// own, so it holds in both branches: over the kit's warm paper sprite, and over the greybox
        /// tinted rect when the art is not imported. Barely blue and barely darker — the tail's absence
        /// is the loud half of the distinction and this only has to agree with it.</para>
        ///
        /// <para>⚑ If the art lane later draws real thought lobes, they land as two more entries in the
        /// kit's tail set and this constant retires. Nothing else here moves.</para>
        /// </summary>
        public static readonly Color InnerVoiceTint = new Color(0.90f, 0.94f, 1f, 1f);

        /// <summary>The panel's greybox colour — the warm paper the modal bubble falls back to when the
        /// kit is not imported. Mirrored rather than shared because it is <see cref="DialoguePresenter"/>'s
        /// own private construction detail, and reaching into it to share one <c>Color</c> would couple
        /// the two presenters for nothing. If the greybox paper is ever re-chosen, both move.</summary>
        public static readonly Color PanelFallback = new Color(0.97f, 0.96f, 0.92f, 0.98f);

        /// <summary>The gold chip's greybox colour (see <see cref="PanelFallback"/>).</summary>
        public static readonly Color ChipFallback = new Color(0.88f, 0.69f, 0.23f, 0.98f);

        static readonly Color Ink = new Color(0.13f, 0.12f, 0.10f, 1f);

        static readonly float MaxBubbleWidth = DialogueBubbleKit.PanelWidthFor(DialogueBubbleKit.MaxCols) * Scale;
        static readonly float MinBubbleWidth = DialogueBubbleKit.PanelWidthFor(DialogueBubbleKit.MinCols) * Scale;
        static readonly float MinBubbleHeight = DialogueBubbleKit.PanelHeightFor(DialogueBubbleKit.MinLines) * Scale;
        static readonly float BubblePadX = DialogueBubbleKit.PanelInsetL * Scale;
        static readonly float BubblePadBottom = DialogueBubbleKit.PanelInsetB * Scale;
        static readonly float BubblePadTop = (DialogueBubbleKit.PanelInsetT + DialogueBubbleKit.ChipHeight) * Scale;
        static readonly float TailWidth = DialogueBubbleKit.TailWidth * Scale;
        static readonly float TailHeight = DialogueBubbleKit.TailHeights[0] * Scale;
        static readonly float TailInset = DialogueBubbleKit.TailInsetFromEdge * Scale;

        const float ScreenMargin = 16f;
        const int BodyFontSize = 24;
        const int NameFontSize = 17;

        /// <summary>How often the modal presenter is looked for again after it has gone (a region
        /// unload destroys it). A find every frame in a scene that has none would be a per-frame
        /// <c>FindAnyObjectByType</c> for nothing.</summary>
        const float ModalRescanSeconds = 0.5f;

        // ---- one bubble -----------------------------------------------------------------------

        /// <summary>
        /// One pooled bubble: its objects, and whatever line it is currently carrying. Built once in
        /// <see cref="AmbientSpeechPresenter.Awake"/> and reused — <see cref="Release"/> only hides it.
        /// </summary>
        private sealed class Slot
        {
            public GameObject Root;
            public RectTransform BubbleRect;
            public Image Panel;
            public Image Tail;
            public Image Chip;
            public Text NameText;
            public Text BodyText;

            public bool Active;
            public int Id;
            public Transform Anchor;

            /// <summary>Whether this line ARRIVED with an anchor. Without it a null
            /// <see cref="Anchor"/> is ambiguous: a line spoken by nobody in particular (legal — it
            /// parks at the screen's speaking position) reads identically to a speaker who has just
            /// been destroyed, and only one of those should end the bubble.</summary>
            public bool HadAnchor;

            public Vector3 AnchorOffset;
            public string SpeakerId;
            public AmbientSpeechKind Kind;
            public DialogueVoice Voice;
            public DialogueTypewriter Typewriter;

            /// <summary>Seconds since this line started, against <see cref="Dwell"/>.</summary>
            public float Elapsed;

            /// <summary>Its whole life, from <see cref="AmbientDwell.Seconds"/>. Computed once at the
            /// start, never re-derived from frames — that is what keeps a sequenced exchange the same
            /// length on every machine.</summary>
            public float Dwell;

            /// <summary>Order of arrival, so "displace the oldest" is a comparison rather than a
            /// scan of start times that a paused frame could make ambiguous.</summary>
            public long Sequence;

            /// <summary>The tail index chosen last frame, or -1. Read by tests.</summary>
            public int TailIndex = -1;
        }

        // ---- state ----------------------------------------------------------------------------

        private static bool _installed;

        /// <summary>
        /// The one presenter, or null before the first scene. Set in <c>Awake</c>, dropped in
        /// <c>OnDestroy</c>.
        ///
        /// <para><b>Why a singleton accessor and not a <c>Find</c>.</b> The host is a hidden
        /// <c>DontDestroyOnLoad</c> object, and anything that wants to reach it — a quit-to-title that
        /// clears the screen, a fixture that has to know it is talking to the SAME pool the game is
        /// using — should not be guessing at what a scene query returns for a hidden object. A second
        /// instance would draw every line twice and double every end signal, so being able to name the
        /// real one is the property that matters.</para>
        /// </summary>
        public static AmbientSpeechPresenter Instance { get; private set; }

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Slot[] _slots;
        private Camera _camera;

        private Sprite _panelSprite;
        private Sprite _goldSprite;
        private Sprite[] _tailSprites;
        private bool _dressed;

        private DialoguePresenter _modal;
        private float _modalRescanClock;

        private long _sequence;
        private bool _subscribed;

        // ---- read surface (tests, and anybody who wants to know) --------------------------------

        /// <summary>How many bubbles are up right now. Never above <see cref="MaxConcurrent"/> — that is
        /// a test.</summary>
        public int ActiveCount
        {
            get
            {
                if (_slots == null) return 0;
                int n = 0;
                for (int i = 0; i < _slots.Length; i++) if (_slots[i].Active) n++;
                return n;
            }
        }

        /// <summary>True while <paramref name="id"/> is on screen.</summary>
        public bool IsShowing(int id) => Find(id) != null;

        /// <summary>What <paramref name="id"/>'s bubble is currently showing, or null when it is not up.
        /// Mid-fill this is a prefix of the line, which is the point.</summary>
        public string VisibleTextOf(int id)
        {
            Slot s = Find(id);
            return s?.Typewriter?.VisibleText;
        }

        /// <summary>True when <paramref name="id"/>'s bubble is drawing a tail. False for an inner-voice
        /// bubble by the owner's 2026-09-06 ruling, and for any speaker who is off camera.</summary>
        public bool TailIsVisibleFor(int id)
        {
            Slot s = Find(id);
            return s != null && s.Tail != null && s.Tail.enabled;
        }

        /// <summary>The colour <paramref name="id"/>'s panel is drawn in — the inner-voice cool tint is
        /// visible here without a screenshot.</summary>
        public Color PanelColourOf(int id)
        {
            Slot s = Find(id);
            return s != null && s.Panel != null ? s.Panel.color : Color.clear;
        }

        /// <summary>True once the kit's sprites have been taken from the modal presenter. False means
        /// every bubble is drawing as a tinted rect, exactly as the modal does undressed.</summary>
        public bool HasBubbleArt => _dressed && _panelSprite != null;

        /// <summary>The camera speakers are projected through. Left empty it resolves to
        /// <see cref="Camera.main"/> each frame, which is what a region change needs.</summary>
        public void SetCamera(Camera camera) => _camera = camera;

        /// <summary>
        /// Take everything off the screen now, as <see cref="AmbientSpeechEndReason.Cancelled"/>.
        ///
        /// <para>For the moments where the world the bubbles belong to stops being the world on screen —
        /// quitting to the title, a hard region change — and for test teardown, so one fixture's chatter
        /// cannot still be up when the next one counts bubbles. Every cancelled line publishes, so a
        /// caller waiting on an end signal is never left hanging.</para>
        /// </summary>
        public void CancelAll()
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Active) Release(_slots[i], AmbientSpeechEndReason.Cancelled);
        }

        /// <summary>
        /// Dress the pool by hand — the fixture seam, and the escape hatch if a scene ever wants a
        /// different kit than the modal's. The game does not call it: the sprites are taken from the
        /// modal presenter the first time one is found, so the bubble is dressed in ONE place.
        /// </summary>
        public void WireKitArt(Sprite panel, Sprite gold, Sprite[] tails)
        {
            _panelSprite = panel;
            _goldSprite = gold;
            _tailSprites = tails != null && tails.Length >= DialogueBubbleKit.TailPieces.Length ? tails : null;
            _dressed = true;
        }

        // ---- lifecycle --------------------------------------------------------------------------

        /// <summary>
        /// Spawn the single self-installing host before the first scene, guarded against domain reloads
        /// and additive loads. Mirrors the project's other self-installing presenters.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("AmbientSpeechPresenter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<AmbientSpeechPresenter>();
        }

        private void Awake()
        {
            Instance = this;
            BuildCanvas();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        private void OnEnable()
        {
            if (_subscribed) return;
            EventBus.Subscribe<AmbientSpeechRequested>(OnRequested);
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed) EventBus.Unsubscribe<AmbientSpeechRequested>(OnRequested);
            _subscribed = false;

            // Everything up is cancelled, so a caller waiting on an end signal is never left hanging by
            // a presenter that went away underneath it.
            CancelAll();
        }

        // ---- the request ------------------------------------------------------------------------

        private void OnRequested(AmbientSpeechRequested request)
        {
            // An empty line is not a bubble. Refused outright and silently: nothing was accepted, so
            // nothing ends, and a caller that sequences on AmbientSpeechEnded is not owed one.
            if (_slots == null || string.IsNullOrWhiteSpace(request.Line)) return;

            Slot slot = FreeSlot() ?? Oldest();
            if (slot == null) return;                       // MaxConcurrent is 0 — not a shipped value

            if (slot.Active) Release(slot, AmbientSpeechEndReason.Displaced);

            slot.Active = true;
            slot.Id = request.Id;
            slot.Anchor = request.Anchor;
            slot.HadAnchor = request.Anchor != null;
            slot.AnchorOffset = request.AnchorOffset;
            slot.SpeakerId = request.SpeakerId;
            slot.Kind = request.Kind;
            slot.Voice = ResolveVoice(request.VoiceId);
            slot.Typewriter = new DialogueTypewriter(request.Line, slot.Voice);
            slot.Elapsed = 0f;
            slot.Dwell = AmbientDwell.Seconds(request.Line, slot.Voice);
            slot.Sequence = ++_sequence;

            // The player's own thought has no speaker but her — a name chip over her head would be a
            // label on the person you are playing.
            string name = request.Kind == AmbientSpeechKind.InnerVoice ? null : request.SpeakerName;
            if (slot.NameText != null) slot.NameText.text = name ?? string.Empty;

            ApplyLook(slot);
            SizeBubbleFor(slot, request.Line);
            slot.BodyText.text = string.Empty;
            slot.Root.SetActive(true);
            Track(slot);
        }

        // ---- the fill ---------------------------------------------------------------------------

        private void Update()
        {
            if (_slots == null) return;

            // ⚠ SCALED time, deliberately unlike the modal bubble's unscaled fill. A conversation the
            // player is IN should read at the same speed whether or not the world is paused under it;
            // the harbour talking to itself is part of that world, and chatter that ran out behind an
            // open menu would be a line the player never saw.
            float dt = Time.deltaTime;

            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (!s.Active) continue;

                // The speaker was destroyed (region unload, an NPC despawned mid-word). There is nothing
                // left to hang the bubble on, and a bubble parked at the screen's speaking position would
                // claim somebody said it.
                if (s.HadAnchor && s.Anchor == null)
                {
                    Release(s, AmbientSpeechEndReason.Cancelled);
                    continue;
                }

                if (s.Typewriter != null && !s.Typewriter.IsComplete)
                {
                    if (s.Typewriter.Advance(dt) > 0) s.BodyText.text = s.Typewriter.VisibleText;

                    while (s.Typewriter.TryTakeTick(out int index, out char c))
                        EventBus.Publish(new DialogueTypewriterTick(s.SpeakerId, s.Voice.TimbreId, index, c));
                }

                s.Elapsed += dt;
                if (s.Elapsed >= s.Dwell) Release(s, AmbientSpeechEndReason.Dwelled);
            }
        }

        private void LateUpdate()
        {
            if (_slots == null) return;

            ResolveModal();

            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (!s.Active) continue;

                // THE MODAL WINS. The player pressed Talk on somebody who was already mid-line; the
                // conversation they chose is the one that gets the bubble.
                if (_modal != null && _modal.IsShowing && s.Anchor != null &&
                    _modal.SpeakerAnchor == s.Anchor)
                {
                    Release(s, AmbientSpeechEndReason.Cancelled);
                    continue;
                }

                Track(s);
            }
        }

        // ---- placement --------------------------------------------------------------------------

        /// <summary>
        /// Put one bubble where its speaker is, this frame — the same solve the modal bubble uses, so an
        /// overheard line clamps at the screen edge with its tail still pointing exactly as a spoken one
        /// does.
        /// </summary>
        private void Track(Slot s)
        {
            if (s.Root == null || s.BubbleRect == null || _canvasRect == null) return;

            Vector2 canvasSize = _canvasRect.rect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f) return;

            Vector2 anchorPoint;
            bool visible = true;
            Camera cam = ResolveCamera();
            bool hasAnchor = s.Anchor != null && cam != null;

            if (hasAnchor)
            {
                Vector3 screen = cam.WorldToScreenPoint(s.Anchor.position + s.AnchorOffset);
                visible = screen.z > 0f;                       // behind the camera projects mirrored
                float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                anchorPoint = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y)) / scale;
                if (visible)
                    visible = DialogueBubbleLayout.AnchorIsOnScreen(anchorPoint, canvasSize, ScreenMargin);
            }
            else
            {
                anchorPoint = new Vector2(canvasSize.x * 0.5f, canvasSize.y * 0.22f);
            }

            if (s.Root.activeSelf != visible) s.Root.SetActive(visible);
            if (!visible) return;

            DialogueBubblePlacement place = DialogueBubbleLayout.Solve(
                anchorPoint, s.BubbleRect.sizeDelta, canvasSize, ScreenMargin, TailHeight, TailInset);

            s.BubbleRect.anchoredPosition = place.BubbleCentre;
            PlaceTail(s, place, anchorPoint, hasAnchor);
            PlaceChip(s, place);
        }

        /// <summary>
        /// Aim the tail's measured tip pixel at the speaker — <see cref="DialoguePresenter.PlaceTail"/>'s
        /// arithmetic, with one extra rule: <b>an inner-voice bubble has no tail at all</b> (owner ruling
        /// 2026-09-06). Nothing is pointing at the player because nobody is speaking.
        /// </summary>
        private void PlaceTail(Slot s, in DialogueBubblePlacement place, Vector2 anchorPoint, bool hasAnchor)
        {
            if (s.Tail == null) return;

            bool show = hasAnchor && s.Kind != AmbientSpeechKind.InnerVoice;
            if (s.Tail.enabled != show) s.Tail.enabled = show;
            if (!show) { s.TailIndex = -1; return; }

            var rt = s.Tail.rectTransform;

            if (_tailSprites == null)
            {
                // Greybox: one tinted rect, flipped in y when the bubble hangs under the speaker.
                s.TailIndex = -1;
                s.Tail.sprite = null;
                s.Tail.type = Image.Type.Simple;
                rt.sizeDelta = new Vector2(TailWidth, TailHeight);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = place.TailRoot;
                rt.localScale = new Vector3(1f, place.TailPointsDown ? 1f : -1f, 1f);
                return;
            }

            float bubbleLeft = place.BubbleCentre.x - s.BubbleRect.sizeDelta.x * 0.5f;
            int index = DialogueBubbleKit.TailIndexFor(place.TailPointsDown, place.TailRoot.x,
                                                       bubbleLeft, s.BubbleRect.sizeDelta.x, Scale);
            s.TailIndex = index;

            s.Tail.sprite = _tailSprites[index];
            s.Tail.color = TintFor(s, Color.white);
            s.Tail.type = Image.Type.Simple;
            rt.localScale = Vector3.one;                       // the up tails are drawn, never flipped
            rt.sizeDelta = new Vector2(DialogueBubbleKit.TailWidth * Scale,
                                       DialogueBubbleKit.TailHeights[index] * Scale);

            // The tip pixel IS the pivot, so setting the position sets where the tip lands. The kit
            // reports the tip from the TOP-left and a pivot is from the bottom-left, hence the y flip;
            // the +0.5 centres it in the pixel rather than on its corner (ADR 0026).
            float tipX = DialogueBubbleKit.TailTipX[index] + 0.5f;
            float tipY = DialogueBubbleKit.TailTipY[index] + 0.5f;
            rt.pivot = new Vector2(tipX / DialogueBubbleKit.TailWidth,
                                   1f - tipY / DialogueBubbleKit.TailHeights[index]);
            rt.anchoredPosition = new Vector2(place.TailRoot.x, anchorPoint.y);
        }

        /// <summary>The name chip on the edge away from the tail, gated on there being a name — the
        /// modal's rule, unchanged. An inner-voice line has no name, so this hides itself.</summary>
        private void PlaceChip(Slot s, in DialogueBubblePlacement place)
        {
            if (s.Chip == null) return;

            string name = s.NameText != null ? s.NameText.text : null;
            bool show = !string.IsNullOrEmpty(name);
            if (s.Chip.enabled != show) s.Chip.enabled = show;
            if (s.NameText != null && s.NameText.enabled != show) s.NameText.enabled = show;
            if (!show) return;

            var rt = s.Chip.rectTransform;
            rt.sizeDelta = new Vector2(DialogueBubbleKit.ChipWidthFor(name.Length) * Scale,
                                       DialogueBubbleKit.ChipHeight * Scale);

            float overlap = DialogueBubbleKit.ChipOverlap * Scale;
            float x = DialogueBubbleKit.ChipMountX * Scale;

            if (place.TailPointsDown)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(x, overlap);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(x, -overlap);
            }
        }

        private void SizeBubbleFor(Slot s, string full)
        {
            if (s.BubbleRect == null || s.BodyText == null) return;

            string previous = s.BodyText.text;
            s.BodyText.text = full;

            float wanted = s.BodyText.preferredWidth + BubblePadX * 2f;
            float width = Mathf.Clamp(wanted, MinBubbleWidth, MaxBubbleWidth);
            s.BubbleRect.sizeDelta = new Vector2(width, MinBubbleHeight);

            // preferredHeight wraps against the width just set, so it is read after it.
            float height = s.BodyText.preferredHeight + BubblePadTop + BubblePadBottom;
            s.BubbleRect.sizeDelta = new Vector2(width, Mathf.Max(MinBubbleHeight, height));

            s.BodyText.text = previous;
        }

        // ---- look -------------------------------------------------------------------------------

        /// <summary>The inner-voice cool tint over whatever this bubble is already drawing — see
        /// <see cref="InnerVoiceTint"/>. A speech bubble gets its colour back unchanged.</summary>
        private static Color TintFor(Slot s, Color baseColour)
            => s.Kind == AmbientSpeechKind.InnerVoice ? baseColour * InnerVoiceTint : baseColour;

        /// <summary>Push this slot's sprites and colours for the kind of line it is now carrying. Called
        /// on every request, because one pooled bubble carries an overheard line and then a thought.</summary>
        private void ApplyLook(Slot s)
        {
            if (s.Panel != null)
            {
                if (_panelSprite != null)
                {
                    s.Panel.sprite = _panelSprite;
                    s.Panel.type = _panelSprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
                    s.Panel.color = TintFor(s, Color.white);
                }
                else
                {
                    s.Panel.sprite = null;
                    s.Panel.type = Image.Type.Simple;
                    s.Panel.color = TintFor(s, PanelFallback);
                }
            }

            if (s.Chip != null)
            {
                if (_goldSprite != null)
                {
                    s.Chip.sprite = _goldSprite;
                    s.Chip.type = _goldSprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
                    s.Chip.color = Color.white;
                }
                else
                {
                    s.Chip.sprite = null;
                    s.Chip.type = Image.Type.Simple;
                    s.Chip.color = ChipFallback;
                }
            }

            if (s.Tail != null && _tailSprites == null) s.Tail.color = TintFor(s, PanelFallback);
        }

        // ---- pool bookkeeping --------------------------------------------------------------------

        private Slot Find(int id)
        {
            if (_slots == null) return null;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Active && _slots[i].Id == id) return _slots[i];
            return null;
        }

        private Slot FreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (!_slots[i].Active) return _slots[i];
            return null;
        }

        private Slot Oldest()
        {
            Slot best = null;
            for (int i = 0; i < _slots.Length; i++)
                if (best == null || _slots[i].Sequence < best.Sequence) best = _slots[i];
            return best;
        }

        /// <summary>Take a bubble off the screen and say so exactly once. The slot keeps its objects —
        /// the pool never destroys anything.</summary>
        private void Release(Slot s, AmbientSpeechEndReason reason)
        {
            if (!s.Active) return;

            int id = s.Id;
            s.Active = false;
            s.Typewriter = null;
            s.Anchor = null;
            s.HadAnchor = false;
            s.SpeakerId = null;
            s.TailIndex = -1;
            if (s.BodyText != null) s.BodyText.text = string.Empty;
            if (s.NameText != null) s.NameText.text = string.Empty;
            if (s.Tail != null) s.Tail.enabled = false;
            if (s.Chip != null) s.Chip.enabled = false;
            if (s.Root != null) s.Root.SetActive(false);

            EventBus.Publish(new AmbientSpeechEnded(id, reason));
        }

        // ---- resolution ---------------------------------------------------------------------------

        /// <summary>
        /// The cadence behind an id, or the default when nobody has registered one. See
        /// <see cref="DialogueVoiceCatalog"/> for why an ambient line names its voice rather than
        /// carrying it.
        /// </summary>
        private static DialogueVoice ResolveVoice(string voiceId) => DialogueVoiceCatalog.VoiceOf(voiceId);

        /// <summary>
        /// <b>⚠ Explicit <c>!= null</c>, never <c>??</c></b>: a destroyed camera is fake-null and the
        /// null-propagating operators sail past Unity's overloaded <c>==</c>, so a region reload would
        /// leave this pointed at a corpse.
        /// </summary>
        private Camera ResolveCamera()
        {
            if (_camera != null) return _camera;
            Camera main = Camera.main;
            return main != null ? main : null;
        }

        /// <summary>
        /// Find the modal presenter, and take its sprites the first time we do. It is dressed by the
        /// region builder, so this is how an ambient bubble ends up wearing exactly the kit the
        /// conversation bubble is wearing without a second wiring step to forget.
        ///
        /// <para>Throttled: a region with no presenter (a fixture, the shell) would otherwise pay a
        /// <c>FindAnyObjectByType</c> every frame for an answer that is not going to change.</para>
        /// </summary>
        private void ResolveModal()
        {
            if (_modal == null)
            {
                _modalRescanClock -= Time.unscaledDeltaTime;
                if (_modalRescanClock > 0f) return;
                _modalRescanClock = ModalRescanSeconds;
                _modal = FindAnyObjectByType<DialoguePresenter>(FindObjectsInactive.Exclude);
            }

            // ⚠ The dressing is retried SEPARATELY from the find, and that is not tidiness. A builder
            // dresses the presenter after its Awake, so the modal is routinely found undressed — and
            // folding this into the find's early-out froze the pool on the greybox rects for the rest
            // of the session. It costs a null check and a property read on the frames it is already
            // false.
            if (_dressed || _modal == null) return;
            if (_modal.TryReadKitArt(out Sprite panel, out Sprite gold, out Sprite[] tails))
                WireKitArt(panel, gold, tails);
        }

        // ---- canvas construction (code-driven, no prefab) --------------------------------------------

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("AmbientSpeech_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            // The 9-slice borders scale by referencePixelsPerUnit / sprite.pixelsPerUnit. Pinned to the
            // kit's own PPU x ArtScale the multiplier is EXACTLY ArtScale, so a kit pixel is a whole
            // number of canvas units and the panel's 1 px ink ring stays hard at every pixel-perfect zoom
            // tier (ruling #327). The modal canvas does the same; a different answer here would draw two
            // different bubbles.
            _canvas.referencePixelsPerUnit = DialogueBubbleKit.PixelsPerUnit * DialogueBubbleKit.ArtScale;
            _canvasRect = canvasGo.GetComponent<RectTransform>();

            var slots = new List<Slot>(MaxConcurrent);
            for (int i = 0; i < MaxConcurrent; i++) slots.Add(BuildSlot(canvasGo.transform, i));
            _slots = slots.ToArray();
        }

        private Slot BuildSlot(Transform parent, int index)
        {
            var s = new Slot();

            s.Root = new GameObject($"AmbientBubble{index}", typeof(RectTransform));
            s.Root.transform.SetParent(parent, false);
            Stretch((RectTransform)s.Root.transform);
            var rootRt = (RectTransform)s.Root.transform;

            // Tail UNDER the body, so its blunt end is hidden by the bubble's own edge.
            s.Tail = MakeImage(rootRt, "Tail", PanelFallback);
            Corner(s.Tail.rectTransform, new Vector2(0.5f, 1f));
            s.Tail.rectTransform.sizeDelta = new Vector2(TailWidth, TailHeight);
            s.Tail.enabled = false;

            s.Panel = MakeImage(rootRt, "Bubble", PanelFallback);
            s.BubbleRect = s.Panel.rectTransform;
            Corner(s.BubbleRect, new Vector2(0.5f, 0.5f));
            s.BubbleRect.sizeDelta = new Vector2(MinBubbleWidth, MinBubbleHeight);

            s.Chip = MakeImage(s.BubbleRect, "NameChip", ChipFallback);
            s.Chip.rectTransform.anchorMin = new Vector2(0f, 1f);
            s.Chip.rectTransform.anchorMax = new Vector2(0f, 1f);
            s.Chip.rectTransform.pivot = new Vector2(0f, 0.5f);
            s.Chip.enabled = false;

            s.NameText = MakeText(s.Chip.rectTransform, "Name", TextAnchor.MiddleLeft, NameFontSize);
            var nr = s.NameText.rectTransform;
            nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one;
            nr.pivot = new Vector2(0.5f, 0.5f);
            nr.offsetMin = new Vector2(DialogueBubbleKit.ChipPadX * Scale, 0f);
            nr.offsetMax = new Vector2(-DialogueBubbleKit.ChipPadX * Scale, 0f);
            s.NameText.enabled = false;

            s.BodyText = MakeText(s.BubbleRect, "Body", TextAnchor.UpperLeft, BodyFontSize);
            var br = s.BodyText.rectTransform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.pivot = new Vector2(0.5f, 0.5f);
            br.offsetMin = new Vector2(BubblePadX, BubblePadBottom);
            br.offsetMax = new Vector2(-BubblePadX, -BubblePadTop);
            s.BodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            s.BodyText.verticalOverflow = VerticalWrapMode.Overflow;

            s.Root.SetActive(false);
            return s;
        }

        private static Image MakeImage(RectTransform parent, string name, Color fallback)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = fallback;
            img.raycastTarget = false;      // an ambient bubble is scenery; it never eats a click
            return img;
        }

        private static Text MakeText(RectTransform parent, string name, TextAnchor align, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = DefaultFont();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Ink;               // dark ink on light paper needs no outline
            text.raycastTarget = false;
            return text;
        }

        private static void Corner(RectTransform rt, Vector2 pivot)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = pivot;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Font DefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
