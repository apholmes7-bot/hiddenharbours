using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>Where the option rows are drawn. ⚑ OWNER TASTE (2026-08-17) — Animal Crossing parks the
    /// choices in a corner; Eastward rides them with the bubble. One inspector dial, no code.</summary>
    public enum OptionBubblePlacement
    {
        /// <summary>Directly under (or over) the speech bubble, on the same tail line. The default.</summary>
        RidesTheBubble = 0,
        /// <summary>Pinned to the bottom-right of the screen, the Animal Crossing shape.</summary>
        ScreenBottomRight = 1,
    }

    /// <summary>
    /// <b>THE SPEECH BUBBLE, ANCHORED AT WHOEVER IS TALKING</b> — the Animal Crossing / Eastward shape the
    /// owner ruled on 2026-07-30 (`design/dialogue-and-knowledge.md` §2, pulled into phase 2026-08-17).
    /// The bubble hangs over the speaker's head with its tail aimed at them, the world stays visible
    /// behind it, the text fills a character at a time — <i>and the fill IS the sound</i> — and the
    /// exchange can end in rows the player chooses between.
    ///
    /// <para><b>What replaced what.</b> This was the VS-21 screen-space panel with a portrait box and a
    /// nameplate. The panel is gone and so is the portrait (the character on screen IS the portrait —
    /// see <see cref="DialogueLine"/>). Everything else about the old component was right and is kept:
    /// it is a pure VIEW driven by <see cref="WorldInteractor"/>; the sequencing lives in the testable
    /// <see cref="DialogueRunner"/>; it builds its own canvas in <see cref="Awake"/> so it needs no
    /// prefab and works headless; it falls back to tinted rects when the art is not imported; and while
    /// a conversation is up it raises <see cref="InteractionGate"/> so the shared Interact key does not
    /// also board the dory underneath.</para>
    ///
    /// <para><b>World-TRACKED screen space, not world space.</b> The bubble is a screen-space canvas
    /// whose position is recomputed each <c>LateUpdate</c> from the speaker's world point through the
    /// camera. Text therefore renders at screen resolution and stays crisp at every one of the discrete
    /// pixel-perfect zoom steps (ruling #327) instead of being resampled at whatever the current step is,
    /// and screen-edge clamping — the Eastward read, where the bubble slides but the tail keeps
    /// pointing — is expressible at all. The clamp itself is
    /// <see cref="DialogueBubbleLayout"/>: pure, and tested at the corners.</para>
    ///
    /// <para><b>Art is a sibling lane and this does not wait for it.</b> Every sprite slot below is
    /// optional and resolves through one lookup that tolerates absence (the <c>TryLoadCamperSprite</c>
    /// pattern); with nothing wired the bubble draws as tinted rects and every behaviour here is already
    /// exercisable. <c>DialogueBubbleArtTests</c> is the tripwire that fires when the kit lands and names
    /// the flip.</para>
    ///
    /// <para><b>Cross-module clean (rule 4).</b> It publishes <see cref="DialogueTypewriterTick"/> and
    /// <see cref="DialogueOptionPicked"/> through Core and subscribes to nothing. The audio of the tick is
    /// the audio lane's own PR; no sound is synthesised here.</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class DialoguePresenter : MonoBehaviour
    {
        // ---- layout constants (canvas units at the 1280×720 reference; plumbing, not owner feel) -----
        //
        // These are the bubble's SHAPE, not its FEEL: the feel dials that belong to the owner are the
        // per-character cadence (DialogueVoice, an asset) and the option placement (below). A bubble's
        // corner radius and its maximum text width are the sort of number that only ever changes when the
        // art kit lands and declares its own — at which point they come from the kit's contract, not from
        // a play-test (rule 6's line between a tunable and a constant).
        const float MaxBubbleWidth = 460f;
        const float MinBubbleWidth = 180f;
        const float MinBubbleHeight = 64f;
        const float BubblePadX = 22f;
        const float BubblePadTop = 30f;      // room for the small name line
        const float BubblePadBottom = 20f;
        const float ScreenMargin = 16f;
        const float TailHeight = 18f;
        const float TailWidth = 22f;
        const float TailInset = 26f;         // the tail never rides off the bubble's rounded corner
        const int BodyFontSize = 24;
        const int NameFontSize = 17;
        const int OptionFontSize = 22;
        const float OptionRowHeight = 34f;
        const float OptionPadding = 12f;

        /// <summary>
        /// <b>Where the art lane's bubble kit lands</b>, declared here so the tripwire has something to
        /// watch and the import PR has one place to look. Nothing reads this at runtime — the sprites
        /// below are wired by the region builders — but declaring the path is what turns
        /// <c>DialogueBubbleArtTests</c> from a hopeful comment into a test that goes red the day the
        /// kit arrives (the #555 lesson: leave a tripwire that FAILS when the art lands, naming the flip).
        /// </summary>
        public const string ArtFolder = "Assets/_Project/Art/UI/DialogueBubble";

        /// <summary>True once the 9-sliced bubble body has been wired — i.e. the kit has landed and the
        /// greybox tinted-rect branch is no longer what ships. The readable half of "is this still
        /// greybox", for the tripwire and for tooling.</summary>
        public bool HasBubbleArt => _bubbleSprite != null;

        /// <summary>True once the tail sprite has been wired (see <see cref="HasBubbleArt"/>).</summary>
        public bool HasTailArt => _tailSprite != null;

        [Header("Bubble art (optional — falls back to tinted rects)")]
        [Tooltip("The 9-sliced bubble body. When the art lane's bubble kit lands this is its sprite and " +
                 "the import PR wires it; until then the bubble draws as a tinted rect and everything " +
                 "here behaves identically.")]
        [SerializeField] private Sprite _bubbleSprite;

        [Tooltip("The tail that points at the speaker. Authored pointing DOWN — it is flipped when a " +
                 "speaker near the top of the screen makes the bubble hang below them.")]
        [SerializeField] private Sprite _tailSprite;

        [Tooltip("The option bubble's background. Falls back to a tinted rect like the body.")]
        [SerializeField] private Sprite _optionSprite;

        [Header("Framing")]
        [Tooltip("The camera the speaker's world position is projected through. Left empty it resolves " +
                 "Camera.main every tick, which is what a region scene played directly needs.")]
        [SerializeField] private Camera _camera;

        [Tooltip("⚑ OWNER TASTE: where the option rows sit. RidesTheBubble keeps the choice in the " +
                 "speaker's own visual language; ScreenBottomRight is the Animal Crossing corner.")]
        [SerializeField] private OptionBubblePlacement _optionPlacement = OptionBubblePlacement.RidesTheBubble;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameObject _root;
        private RectTransform _bubbleRect;
        private Image _tail;
        private Text _nameText;
        private Text _bodyText;
        private Text _hintText;

        private GameObject _optionRoot;
        private RectTransform _optionRect;
        private readonly List<Text> _optionRows = new List<Text>();

        private DialogueRunner _runner;
        private DialogueTypewriter _typewriter;
        private DialogueOptionPicker _picker;
        private Action _onComplete;

        private Transform _anchor;
        private Vector3 _anchorOffset;
        private string _speakerName;
        private DialogueVoice _voice;
        private IReadOnlyList<DialogueOption> _options;
        private string _dialogueId;
        private string _speakerId;

        /// <summary>True while a conversation is on screen (lines or options).</summary>
        public bool IsShowing { get; private set; }

        /// <summary>True while the current line is still populating — an Interact press here fills it
        /// instantly rather than skipping it, which is the standard bargain and the one the option
        /// picker depends on (you cannot confirm a row you have not read).</summary>
        public bool IsFilling => _typewriter != null && !_typewriter.IsComplete;

        /// <summary>True while the option rows are up and the move axis is choosing between them.</summary>
        public bool IsChoosing => _picker != null;

        /// <summary>Which row the cursor is on, or -1 when nothing is being chosen. Exposed so the
        /// selection can be driven and asserted WITHOUT a key press — headless input cannot deliver one,
        /// and a self-skipping key-driven test is a hole (the #555 lesson).</summary>
        public int SelectedOption => _picker != null ? _picker.Index : -1;

        /// <summary>The rows on offer (the appended close row included), or null when not choosing.</summary>
        public IReadOnlyList<DialogueOption> Options => _picker != null ? _options : null;

        /// <summary>The text currently drawn in the bubble — the revealed prefix while filling.</summary>
        public string VisibleText => _typewriter != null ? _typewriter.VisibleText : string.Empty;

        /// <summary>Where the bubble body sits this frame, in canvas units from the bottom-left. Exposed
        /// for tests: the bubble following a walking speaker is a STATE that can be asserted, where a
        /// screenshot of it is not — and a headless CI cannot read pixels at all.</summary>
        public Vector2 BubblePosition => _bubbleRect != null ? _bubbleRect.anchoredPosition : Vector2.zero;

        /// <summary>Where the tail meets the bubble this frame, in the same frame as
        /// <see cref="BubblePosition"/> — the half that must keep pointing at the speaker.</summary>
        public Vector2 TailPosition =>
            _tail != null ? _tail.rectTransform.anchoredPosition : Vector2.zero;

        /// <summary>True while the tail is drawn — false for a conversation with nobody on screen to
        /// point at.</summary>
        public bool TailIsVisible => _tail != null && _tail.enabled;

        /// <summary>True while the bubble is actually on screen. False when the speaker has walked out of
        /// shot mid-conversation: a tail aimed off the edge at somebody you cannot see is worse than no
        /// bubble at all.</summary>
        public bool BubbleIsVisible => _root != null && _root.activeSelf;

        private void Awake()
        {
            BuildCanvas();
            HideRoot();
        }

        private void OnDisable()
        {
            // A bubble destroyed/disabled mid-line must never leave interaction wedged off.
            if (IsShowing) InteractionGate.Reset();
        }

        // ---- public API (driven by WorldInteractor) -----------------------------------------

        /// <summary>
        /// Begin showing a conversation. Empty/null lines complete immediately (no-op view).
        /// </summary>
        public void Play(in DialogueRequest request, Action onComplete = null)
        {
            _runner = new DialogueRunner(request.Lines);
            _runner.Open();
            _onComplete = onComplete;

            _anchor = request.Anchor;
            _anchorOffset = request.AnchorOffset;
            _voice = request.Voice.Sanitised();
            _speakerName = request.Lines != null && request.Lines.Count > 0 ? request.Lines[0].Speaker : null;
            _options = request.Options;
            _dialogueId = request.DialogueId;
            _speakerId = request.SpeakerId;
            _picker = null;

            if (!_runner.IsOpen)
            {
                // No lines. A conversation that is nothing BUT options is legal and useful (a sign, a
                // device); one that is nothing at all is the old no-op. Either way the bubble starts
                // EMPTY — inheriting the previous conversation's words would be a ghost.
                _typewriter = null;
                if (_bodyText != null) _bodyText.text = "";
                if (_nameText != null) _nameText.text = "";
                if (_bubbleRect != null) _bubbleRect.sizeDelta = new Vector2(MinBubbleWidth, MinBubbleHeight);
                if (!TryOpenOptions()) { Finish(); return; }
                IsShowing = true;
                InteractionGate.IsBlocked = true;
                ShowRoot();
                Track();
                return;
            }

            IsShowing = true;
            InteractionGate.IsBlocked = true;
            ShowRoot();
            Render(_runner.Current);
            Track();
        }

        /// <summary>The pre-bubble signature: some lines, spoken by nobody in particular.</summary>
        public void Play(IReadOnlyList<DialogueLine> lines, Action onComplete = null)
            => Play(DialogueRequest.Plain(lines), onComplete);

        /// <summary>
        /// Take an Interact press. In order: fill the current line if it is still populating, confirm the
        /// highlighted option if the rows are up, otherwise advance to the next line (and past the last
        /// line, into the options if there are any, else close). Returns true when the press was spent.
        /// </summary>
        public bool Advance()
        {
            if (!IsShowing) return false;

            if (IsFilling) { _typewriter.CompleteNow(); Draw(); ShowHint(true); return true; }
            if (_picker != null) { ConfirmOption(); return true; }

            if (_runner.Advance()) { Render(_runner.Current); return true; }

            // Past the last line: the choice, if there is one, else the end.
            if (TryOpenOptions()) return true;
            Finish();
            return true;
        }

        /// <summary>
        /// Feed this frame's move axis (+1 = up the rows, -1 = down). Returns true when the cursor moved.
        /// A no-op unless the option rows are up, so the same axis that walks the fisher is only ever
        /// borrowed while there is something to choose (the ledger is exhausted A–Z: nothing new binds).
        /// </summary>
        public bool MoveSelection(float axis)
        {
            if (!IsShowing || _picker == null) return false;
            if (!_picker.Step(axis)) return false;
            DrawOptions();
            return true;
        }

        /// <summary>Close the conversation now (cancel — the player walked away). Fires the completion
        /// callback like a normal close, so no caller has two endings to handle.</summary>
        public void Close()
        {
            if (!IsShowing) return;
            _runner.Close();
            Finish();
        }

        private void Finish()
        {
            IsShowing = false;
            _picker = null;
            _typewriter = null;
            _anchor = null;
            InteractionGate.IsBlocked = false;
            HideRoot();
            var cb = _onComplete;
            _onComplete = null;
            cb?.Invoke();
        }

        // ---- the fill -----------------------------------------------------------------------

        private void Update()
        {
            if (!IsShowing || _typewriter == null || _typewriter.IsComplete) return;

            // Unscaled: a conversation reads at the same speed whether or not the world is paused
            // underneath it, and nothing here feeds the simulation (rule 5 is untouched — no clock, no
            // seed, nothing saved).
            if (_typewriter.Advance(Time.unscaledDeltaTime) > 0) Draw();

            while (_typewriter.TryTakeTick(out int index, out char c))
                EventBus.Publish(new DialogueTypewriterTick(_speakerId, _voice.TimbreId, index, c));

            if (_typewriter.IsComplete) ShowHint(true);
        }

        private void LateUpdate()
        {
            if (IsShowing) Track();
        }

        // ---- rendering ----------------------------------------------------------------------

        private void Render(in DialogueLine line)
        {
            _typewriter = new DialogueTypewriter(line.Text ?? string.Empty, _voice);
            if (_nameText != null) _nameText.text = line.Speaker ?? "";

            // Size the bubble from the WHOLE line, then let it fill: a bubble that grew line-by-line as
            // the words arrived would jitter under the tail, which is the one thing the anchoring is for.
            SizeBubbleFor(line.Text ?? string.Empty);
            ShowHint(false);
            Draw();
        }

        private void Draw()
        {
            if (_bodyText != null) _bodyText.text = _typewriter != null ? _typewriter.VisibleText : "";
        }

        private void ShowHint(bool show)
        {
            if (_hintText != null && _hintText.enabled != show) _hintText.enabled = show;
        }

        private void SizeBubbleFor(string full)
        {
            if (_bubbleRect == null || _bodyText == null) return;

            string previous = _bodyText.text;
            _bodyText.text = full;

            float wanted = _bodyText.preferredWidth + BubblePadX * 2f;
            float width = Mathf.Clamp(wanted, MinBubbleWidth, MaxBubbleWidth);
            _bubbleRect.sizeDelta = new Vector2(width, MinBubbleHeight);

            // preferredHeight wraps against the rect width we just set, so it must be read after it.
            float height = _bodyText.preferredHeight + BubblePadTop + BubblePadBottom;
            _bubbleRect.sizeDelta = new Vector2(width, Mathf.Max(MinBubbleHeight, height));

            _bodyText.text = previous;
        }

        // ---- the anchor ---------------------------------------------------------------------

        /// <summary>
        /// Put the bubble where the speaker is, this frame. Runs in <c>LateUpdate</c> so the speaker has
        /// already moved (a villager writes her pose in <c>Update</c>) — otherwise the bubble trails one
        /// frame behind a walking speaker, which reads as lag on the one element that must feel welded.
        /// </summary>
        private void Track()
        {
            if (_root == null || _bubbleRect == null || _canvasRect == null) return;

            Vector2 canvasSize = _canvasRect.rect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f) return;

            Vector2 anchorPoint;
            bool visible = true;

            Camera cam = ResolveCamera();
            if (_anchor != null && cam != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(_anchor.position + _anchorOffset);
                // Behind the camera projects to a mirrored point in front of it — never draw that.
                visible = screen.z > 0f;
                float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                // ⭐ Rounded in SCREEN pixels before the canvas conversion, so the bubble lands on the
                // pixel grid instead of shimmering between two of them as the speaker walks.
                anchorPoint = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y)) / scale;
                if (visible) visible = DialogueBubbleLayout.AnchorIsOnScreen(anchorPoint, canvasSize,
                                                                            ScreenMargin);
            }
            else
            {
                // Nobody on screen to hang it off (a legacy string conversation, or no camera at all):
                // park it where the old panel sat, low and centred, and keep the tail hidden.
                anchorPoint = new Vector2(canvasSize.x * 0.5f, canvasSize.y * 0.22f);
            }

            bool hasAnchor = _anchor != null && cam != null;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;

            DialogueBubblePlacement place = DialogueBubbleLayout.Solve(
                anchorPoint, _bubbleRect.sizeDelta, canvasSize, ScreenMargin, TailHeight, TailInset);

            _bubbleRect.anchoredPosition = place.BubbleCentre;

            if (_tail != null)
            {
                bool showTail = hasAnchor;
                if (_tail.enabled != showTail) _tail.enabled = showTail;
                if (showTail)
                {
                    var tailRt = _tail.rectTransform;
                    tailRt.sizeDelta = new Vector2(TailWidth, TailHeight);
                    tailRt.anchoredPosition = place.TailRoot;
                    // Authored pointing down; flipped when the bubble had to hang under the speaker.
                    tailRt.localScale = new Vector3(1f, place.TailPointsDown ? 1f : -1f, 1f);
                }
            }

            PlaceOptions(place, canvasSize);
        }

        /// <summary>
        /// Which camera the speaker is projected through — the wired one where a scene names it, else
        /// whatever is currently tagged MainCamera.
        ///
        /// <para><b>⚠ Explicit <c>!= null</c>, never <c>??</c></b> (the <c>ResolvePlayer</c> lesson): a
        /// destroyed camera is fake-null and the null-propagating operators sail straight past Unity's
        /// overloaded <c>==</c>, so a region reload would leave this pointed at a corpse.</para>
        /// </summary>
        private Camera ResolveCamera()
        {
            if (_camera != null) return _camera;
            Camera main = Camera.main;
            return main != null ? main : null;
        }

        // ---- the options --------------------------------------------------------------------

        /// <summary>Open the option rows if this conversation has any. Returns false when it does not,
        /// which is the signal to just end.</summary>
        private bool TryOpenOptions()
        {
            if (_options == null || _options.Count == 0) return false;

            _picker = new DialogueOptionPicker(_options.Count);
            _typewriter = null;
            ShowHint(false);
            DrawOptions();
            if (_optionRoot != null) _optionRoot.SetActive(true);
            return true;
        }

        private void ConfirmOption()
        {
            if (!_picker.Confirm(out int index) || index < 0 || index >= _options.Count) { Finish(); return; }

            DialogueOption picked = _options[index];
            _picker = null;
            if (_optionRoot != null) _optionRoot.SetActive(false);

            EventBus.Publish(new DialogueOptionPicked(_dialogueId, picked.Id, _speakerId));

            // A row that answers plays its answer and then the conversation ends. One round, no nesting
            // (rule 8) — a reply never leads to more options.
            if (picked.ReplyLines != null && picked.ReplyLines.Length > 0)
            {
                var reply = new List<DialogueLine>(picked.ReplyLines.Length);
                for (int i = 0; i < picked.ReplyLines.Length; i++)
                    reply.Add(new DialogueLine(_speakerName, picked.ReplyLines[i]));

                _options = null;                 // the reply is terminal — no second picker
                _runner = new DialogueRunner(reply);
                _runner.Open();
                Render(_runner.Current);
                return;
            }

            Finish();
        }

        private void DrawOptions()
        {
            EnsureOptionRows(_options.Count);
            for (int i = 0; i < _optionRows.Count; i++)
            {
                bool used = i < _options.Count;
                _optionRows[i].gameObject.SetActive(used);
                if (!used) continue;

                bool selected = i == _picker.Index;
                _optionRows[i].text = (selected ? WorldStrings.OptionCursor : "   ") + _options[i].Label;
                _optionRows[i].color = selected
                    ? new Color(0.13f, 0.12f, 0.10f, 1f)
                    : new Color(0.13f, 0.12f, 0.10f, 0.55f);
            }

            if (_optionRect != null)
            {
                float width = MaxBubbleWidth;
                float height = _options.Count * OptionRowHeight + OptionPadding * 2f;
                _optionRect.sizeDelta = new Vector2(width, height);
            }
        }

        private void PlaceOptions(in DialogueBubblePlacement place, Vector2 canvasSize)
        {
            if (_optionRect == null || _picker == null) return;

            Vector2 size = _optionRect.sizeDelta;
            Vector2 pos;

            if (_optionPlacement == OptionBubblePlacement.ScreenBottomRight)
            {
                pos = new Vector2(canvasSize.x - ScreenMargin - size.x * 0.5f,
                                  ScreenMargin + size.y * 0.5f);
            }
            else
            {
                // Riding the bubble: stacked on the far side of it from the speaker, so the rows never
                // cover the person you are talking to.
                float bubbleHalf = _bubbleRect.sizeDelta.y * 0.5f;
                float gap = 8f + size.y * 0.5f + bubbleHalf;
                float y = place.TailPointsDown ? place.BubbleCentre.y + gap : place.BubbleCentre.y - gap;
                float x = Mathf.Clamp(place.BubbleCentre.x,
                                      ScreenMargin + size.x * 0.5f,
                                      Mathf.Max(ScreenMargin + size.x * 0.5f,
                                                canvasSize.x - ScreenMargin - size.x * 0.5f));
                y = Mathf.Clamp(y, ScreenMargin + size.y * 0.5f,
                                Mathf.Max(ScreenMargin + size.y * 0.5f,
                                          canvasSize.y - ScreenMargin - size.y * 0.5f));
                pos = new Vector2(x, y);
            }

            _optionRect.anchoredPosition = pos;
        }

        private void EnsureOptionRows(int count)
        {
            while (_optionRows.Count < count)
            {
                var row = MakeText(_optionRect, $"Option{_optionRows.Count}", TextAnchor.MiddleLeft,
                                   OptionFontSize);
                SetOutline(row, false);   // dark text on a light bubble needs no halo
                var rt = row.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(OptionPadding, 0f);
                rt.offsetMax = new Vector2(-OptionPadding, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, OptionRowHeight);
                rt.anchoredPosition = new Vector2(0f, -OptionPadding - OptionRowHeight * _optionRows.Count);
                _optionRows.Add(row);
            }
        }

        private void ShowRoot() { if (_root != null) _root.SetActive(true); }

        private void HideRoot()
        {
            if (_root != null) _root.SetActive(false);
            if (_optionRoot != null) _optionRoot.SetActive(false);
        }

        // ---- canvas construction (code-driven, no prefab) -----------------------------------

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Dialogue_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 110; // above the HUD (100)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = canvasGo.GetComponent<RectTransform>();

            // Root holder (toggled on/off). Everything under it is positioned from the bottom-left in
            // canvas units, which is the frame DialogueBubbleLayout answers in.
            _root = new GameObject("DialogueRoot", typeof(RectTransform));
            _root.transform.SetParent(canvasGo.transform, false);
            Stretch(_root.GetComponent<RectTransform>());

            var rootRt = (RectTransform)_root.transform;

            // The tail sits UNDER the body so its blunt end is hidden by the bubble's own edge.
            _tail = MakeImage(rootRt, "Tail", _tailSprite, new Color(0.97f, 0.96f, 0.92f, 0.98f));
            Corner(_tail.rectTransform, new Vector2(0.5f, 1f));
            _tail.rectTransform.sizeDelta = new Vector2(TailWidth, TailHeight);
            _tail.enabled = false;

            var bubble = MakeImage(rootRt, "Bubble", _bubbleSprite, new Color(0.97f, 0.96f, 0.92f, 0.98f));
            if (_bubbleSprite != null) bubble.type = Image.Type.Sliced;   // 9-slice when the kit lands
            _bubbleRect = bubble.rectTransform;
            Corner(_bubbleRect, new Vector2(0.5f, 0.5f));
            _bubbleRect.sizeDelta = new Vector2(MinBubbleWidth, MinBubbleHeight);

            // The speaker's name, small, on the bubble's shoulder. NOT a nameplate and NOT a portrait —
            // the person is right there under the tail; this is for a letter or a logbook, which have a
            // name and no body.
            _nameText = MakeText(_bubbleRect, "Name", TextAnchor.UpperLeft, NameFontSize);
            _nameText.color = new Color(0.24f, 0.22f, 0.18f, 0.95f);
            SetOutline(_nameText, false);
            var nr = _nameText.rectTransform;
            nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.offsetMin = new Vector2(BubblePadX, 0f);
            nr.offsetMax = new Vector2(-BubblePadX, 0f);
            nr.sizeDelta = new Vector2(nr.sizeDelta.x, 22f);
            nr.anchoredPosition = new Vector2(0f, -6f);

            _bodyText = MakeText(_bubbleRect, "Body", TextAnchor.UpperLeft, BodyFontSize);
            _bodyText.color = new Color(0.13f, 0.12f, 0.10f, 1f);
            SetOutline(_bodyText, false);
            var br = _bodyText.rectTransform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.pivot = new Vector2(0.5f, 0.5f);
            br.offsetMin = new Vector2(BubblePadX, BubblePadBottom);
            br.offsetMax = new Vector2(-BubblePadX, -BubblePadTop);
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Overflow;

            _hintText = MakeText(_bubbleRect, "ContinueHint", TextAnchor.LowerRight, 16);
            _hintText.text = WorldStrings.ContinueHint;
            _hintText.color = new Color(0.30f, 0.28f, 0.24f, 0.75f);
            SetOutline(_hintText, false);
            var hr = _hintText.rectTransform;
            hr.anchorMin = new Vector2(1f, 0f); hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(1f, 0f);
            hr.anchoredPosition = new Vector2(-8f, 4f);
            hr.sizeDelta = new Vector2(80f, 22f);
            _hintText.enabled = false;

            // The option bubble is its own root so it can be parked somewhere else entirely (the owner's
            // AC-corner dial) without the speech bubble knowing.
            _optionRoot = new GameObject("OptionRoot", typeof(RectTransform), typeof(Image));
            _optionRoot.transform.SetParent(canvasGo.transform, false);
            var optionImage = _optionRoot.GetComponent<Image>();
            if (_optionSprite != null) { optionImage.sprite = _optionSprite; optionImage.type = Image.Type.Sliced; optionImage.color = Color.white; }
            else optionImage.color = new Color(0.97f, 0.96f, 0.92f, 0.98f);
            optionImage.raycastTarget = false;
            _optionRect = _optionRoot.GetComponent<RectTransform>();
            Corner(_optionRect, new Vector2(0.5f, 0.5f));
            _optionRect.sizeDelta = new Vector2(MaxBubbleWidth, OptionRowHeight * 2f + OptionPadding * 2f);
            _optionRoot.SetActive(false);
        }

        /// <summary>Anchor a rect to the canvas's bottom-left corner, so its anchoredPosition IS its
        /// position in the canvas-unit frame <see cref="DialogueBubbleLayout"/> answers in.</summary>
        private static void Corner(RectTransform rt, Vector2 pivot)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = pivot;
        }

        private static Image MakeImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            if (sprite != null) { img.sprite = sprite; img.color = Color.white; }
            else img.color = color;     // fallback tint when the art isn't imported
            img.raycastTarget = false;
            return img;
        }

        private static Text MakeText(RectTransform parent, string name, TextAnchor align, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = DefaultFont();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        /// <summary>Dark text on a light bubble needs no outline; white text over the world does.</summary>
        private static void SetOutline(Text text, bool on)
        {
            var outline = text.GetComponent<Outline>();
            if (outline != null) outline.enabled = on;
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
