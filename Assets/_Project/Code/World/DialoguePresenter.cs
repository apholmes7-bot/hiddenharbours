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
        // ---- layout constants (canvas units at the 1280×720 reference) -------------------------
        //
        // ⭐ THESE NOW COME FROM THE KIT. They used to be canvas units chosen for the greybox, and
        // the tripwire's flip checklist said exactly that: "the layout numbers here are canvas units
        // chosen for the greybox, not measurements of the kit". Every one below is now the kit's own
        // declared pixel count times DialogueBubbleKit.ArtScale — so the bubble's shape is the art
        // director's arithmetic rather than a plausible-looking number, and it moves when he
        // regenerates his contract instead of when somebody eyeballs a play-test.
        //
        // The grid question the tripwire raised (24 vs 32) is answered in the kit: 32 px = 1 m, the
        // assets grid, because the bubble is an object IN the scene. See DialogueBubbleKit.
        //
        // Still not owner feel: the feel dials are the per-character cadence (DialogueVoice, an
        // asset) and the option placement (below). ArtScale is the one dial here, and it is a
        // legibility call, not a taste one.

        const float Scale = DialogueBubbleKit.ArtScale;

        /// <summary>The widest the panel may grow — the kit's 34-column maximum. "Past that the line
        /// wants a second bubble, not a taller one."</summary>
        static readonly float MaxBubbleWidth = DialogueBubbleKit.PanelWidthFor(DialogueBubbleKit.MaxCols) * Scale;

        static readonly float MinBubbleWidth = DialogueBubbleKit.PanelWidthFor(DialogueBubbleKit.MinCols) * Scale;
        static readonly float MinBubbleHeight = DialogueBubbleKit.PanelHeightFor(DialogueBubbleKit.MinLines) * Scale;

        /// <summary>The text inset — the rect text may fill is the panel inset by this on each side.
        /// Measured by the art lane off its own drawing, not derived.</summary>
        static readonly float BubblePadX = DialogueBubbleKit.PanelInsetL * Scale;
        static readonly float BubblePadBottom = DialogueBubbleKit.PanelInsetB * Scale;

        /// <summary>Top inset plus the name line that rides inside the panel's shoulder.</summary>
        static readonly float BubblePadTop = (DialogueBubbleKit.PanelInsetT + DialogueBubbleKit.ChipHeight) * Scale;

        /// <summary>Tail geometry, in canvas units. The HEIGHT is per-tail (the centre pair are a row
        /// shorter), so this is only the fallback rect's size.</summary>
        static readonly float TailWidth = DialogueBubbleKit.TailWidth * Scale;
        static readonly float TailHeight = DialogueBubbleKit.TailHeights[0] * Scale;

        /// <summary>The tail never rides off the panel's chamfered corner — the kit's corner + 2.</summary>
        static readonly float TailInset = DialogueBubbleKit.TailInsetFromEdge * Scale;

        static readonly float OptionRowHeight = DialogueBubbleKit.OptionRowHeight * Scale;
        static readonly float OptionPadding = DialogueBubbleKit.OptionInsetT * Scale;

        /// <summary>Where the row label starts inside the option bubble — the kit's own text origin,
        /// which is also what leaves room for the cursor to the left of it.</summary>
        static readonly float OptionTextX = DialogueBubbleKit.OptionTextX * Scale;

        const float ScreenMargin = 16f;
        const int BodyFontSize = 24;
        const int NameFontSize = 17;
        const int OptionFontSize = 22;

        /// <summary>
        /// <b>Where the art lane's bubble kit lands</b>, declared here so the tripwire has something to
        /// watch and the import PR has one place to look. Nothing reads this at runtime — the sprites
        /// below are wired by the region builders — but declaring the path is what turns
        /// <c>DialogueBubbleArtTests</c> from a hopeful comment into a test that goes red the day the
        /// kit arrives (the #555 lesson: leave a tripwire that FAILS when the art lands, naming the flip).
        /// </summary>
        public const string ArtFolder = "Assets/_Project/Art/UI/DialogueBubble";

        /// <summary>How far the bubble fades back while the speaker's book is open over the lower
        /// screen. Presentation plumbing, not owner feel: far enough that the book is plainly what you
        /// are reading, near enough that the speaker is plainly still standing there mid-sentence -
        /// which is the whole of the design's "the book does not replace them".</summary>
        public const float DimmedAlpha = 0.45f;

        /// <summary>True once the 9-sliced bubble body has been wired — i.e. the kit has landed and the
        /// greybox tinted-rect branch is no longer what ships. The readable half of "is this still
        /// greybox", for the tripwire and for tooling.</summary>
        public bool HasBubbleArt => _bubbleSprite != null;

        /// <summary>True once the tail art has been wired — either the six-tail set or the single
        /// fallback sprite (see <see cref="HasBubbleArt"/>).</summary>
        public bool HasTailArt => _tailSprite != null || HasTailSet;

        /// <summary>True once all six drawn tails are wired, which is what lets the tail be CHOSEN
        /// per placement rather than flipped.</summary>
        public bool HasTailSet
        {
            get
            {
                if (_tailSprites == null ||
                    _tailSprites.Length != DialogueBubbleKit.TailPieces.Length) return false;
                for (int i = 0; i < _tailSprites.Length; i++)
                    if (_tailSprites[i] == null) return false;
                return true;
            }
        }

        /// <summary>Which of the kit's six tails is drawn this frame, or −1 when the tail is hidden
        /// or the set is not wired. Exposed so "the tail follows the placement" is a STATE a
        /// headless test can assert — there is no screenshot to take in CI.</summary>
        public int TailIndex { get; private set; } = -1;

        /// <summary>Which bob frame the continue marker is on, or −1 when it is not shown.</summary>
        public int MarkerFrame { get; private set; } = -1;

        /// <summary>True while the caret is drawn — i.e. while the line is still populating.</summary>
        public bool CaretIsVisible => _caret != null && _caret.enabled;

        /// <summary>
        /// Wire the baked kit onto this presenter. Called by the region builders (the editor side
        /// resolves the sprites; see <c>DialogueBubbleArt.Dress</c>), and by tests that want a
        /// dressed presenter without an asset round-trip.
        ///
        /// <para>Every argument is optional. A null leaves that piece on its tinted-rect fallback,
        /// so a half-baked folder degrades piece by piece rather than all at once — right art or
        /// fallback, never wrong art.</para>
        /// </summary>
        public void WireKitArt(Sprite panel = null, Sprite gold = null, Sprite[] tails = null,
                               Sprite[] markerFrames = null, Sprite caret = null,
                               Sprite cursor = null)
        {
            if (panel != null)
            {
                _bubbleSprite = panel;
                // The option bubble is the SAME panel: the kit lays its rows inside the same
                // panel() the speech body uses, so one tile dresses both.
                _optionSprite = panel;
            }
            if (gold != null) _goldSprite = gold;
            if (tails != null) _tailSprites = tails;
            if (markerFrames != null) _markerSprites = markerFrames;
            if (caret != null) _caretSprite = caret;
            if (cursor != null) _cursorSprite = cursor;

            ApplyKitArt();
        }

        [Header("Bubble art (optional — falls back to tinted rects)")]
        [Tooltip("The 9-sliced bubble body. When the art lane's bubble kit lands this is its sprite and " +
                 "the import PR wires it; until then the bubble draws as a tinted rect and everything " +
                 "here behaves identically.")]
        [SerializeField] private Sprite _bubbleSprite;

        [Tooltip("The tail that points at the speaker — the fallback single sprite, used when the " +
                 "six-tail set below is not wired.")]
        [SerializeField] private Sprite _tailSprite;

        [Tooltip("The option bubble's background. Falls back to a tinted rect like the body.")]
        [SerializeField] private Sprite _optionSprite;

        [Tooltip("The gold 9-slice: the name chip AND the selected-row pill. One tile serves both — " +
                 "the kit draws the chip as its panel with the gold ramp, and the pill is the same " +
                 "call at row height.")]
        [SerializeField] private Sprite _goldSprite;

        [Tooltip("The six tails in kit order: left, centre, right, leftUp, centreUp, rightUp.\n\n" +
                 "⚠ The UP SET IS DRAWN, NOT FLIPPED. A tail is two colours with no shading so a " +
                 "y-flip would fight nothing — but the kit ships the up tails anyway, so the tip " +
                 "pixel is MEASURED in both directions instead of derived. Do not save three " +
                 "sprites by flipping the down set: the tip is the anchor, and a derived tip is a " +
                 "guess about the one pixel that has to be right.")]
        [SerializeField] private Sprite[] _tailSprites;

        [Tooltip("The continue marker's two bob frames. Frame 1 is the same shape one row lower, " +
                 "and both are baked in a common cell so the bob is a straight sprite swap.")]
        [SerializeField] private Sprite[] _markerSprites;

        [Tooltip("The caret — a 1×6 ink bar in the cell the next character lands in. Shown while " +
                 "filling only; it hands off to the continue marker when the line completes.")]
        [SerializeField] private Sprite _caretSprite;

        [Tooltip("The option-row cursor. ⚑ OWNER TASTE: the pointing hand ships; the wool glove, " +
                 "brass tack and fish hook all bake and are one constant away " +
                 "(DialogueBubbleKit.ShippedCursor).")]
        [SerializeField] private Sprite _cursorSprite;

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

        private Image _bubbleImage;
        private Image _chipImage;
        private Image _marker;
        private Image _caret;

        private GameObject _optionRoot;
        private RectTransform _optionRect;
        private Image _optionImage;
        private readonly List<Text> _optionRows = new List<Text>();
        private Image _pill;
        private Image _cursor;

        /// <summary>Unscaled seconds since the current line finished filling — the continue marker's
        /// bob clock. Reset per line so the marker always appears on frame 0.</summary>
        private float _markerClock;

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
        private CanvasGroup _rootGroup;
        private bool _awaitingCatalog;

        /// <summary>True while a conversation is on screen (lines or options).</summary>
        public bool IsShowing { get; private set; }

        /// <summary>True while the current line is still populating — an Interact press here fills it
        /// instantly rather than skipping it, which is the standard bargain and the one the option
        /// picker depends on (you cannot confirm a row you have not read).</summary>
        public bool IsFilling => _typewriter != null && !_typewriter.IsComplete;

        /// <summary>True while the option rows are up and the move axis is choosing between them.</summary>
        public bool IsChoosing => _picker != null;

        /// <summary>
        /// True while a catalog row has handed off and the conversation is HOLDING for the book to shut.
        ///
        /// <para>The bubble is still up (dimmed), <see cref="IsShowing"/> is still true and the
        /// interaction gate is still blocked - the player is mid-conversation, reading something the
        /// speaker handed them. Nothing advances the conversation in this state; only
        /// <c>Core.CatalogClosed</c> gets out of it.</para>
        /// </summary>
        public bool IsAwaitingCatalog => _awaitingCatalog;

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

        private void OnEnable() => EventBus.Subscribe<CatalogClosed>(OnCatalogClosed);

        private void OnDisable()
        {
            EventBus.Unsubscribe<CatalogClosed>(OnCatalogClosed);

            // A bubble destroyed/disabled mid-line must never leave interaction wedged off.
            if (IsShowing) InteractionGate.Reset();
        }

        /// <summary>
        /// The book has been shut: put the same rows back and hand the player to the person who lent it.
        ///
        /// <para>Guarded on the hold flag so a stray close (a second book, a signal arriving after the
        /// conversation was walked away from) cannot re-open a picker on a conversation that has already
        /// ended. If the rows cannot be rebuilt the conversation ends cleanly, rather than hanging with
        /// a dimmed bubble and no way out.</para>
        /// </summary>
        private void OnCatalogClosed(CatalogClosed closed)
        {
            if (!_awaitingCatalog) return;
            _awaitingCatalog = false;
            SetDimmed(false);

            if (!IsShowing) return;
            if (!TryOpenOptions()) Finish();
        }

        /// <summary>Fade the bubble back (or bring it forward) without hiding it - the speaker stays on
        /// screen, animating, with the tail still pointing at them.</summary>
        private void SetDimmed(bool dim)
        {
            if (_rootGroup != null) _rootGroup.alpha = dim ? DimmedAlpha : 1f;
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
            _awaitingCatalog = false;   // a fresh conversation never inherits a previous book's hold
            SetDimmed(false);

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

            // The book is open over the lower screen and this conversation is holding for it. An
            // Interact press belongs to the book, not to the bubble - without this the press would
            // fall through to the runner and could END the conversation under the open panel.
            if (_awaitingCatalog) return false;

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
            _awaitingCatalog = false;
            SetDimmed(false);
            _typewriter = null;
            _anchor = null;
            TailIndex = -1;
            MarkerFrame = -1;
            if (_marker != null) _marker.enabled = false;
            if (_caret != null) _caret.enabled = false;
            if (_pill != null) _pill.enabled = false;
            if (_cursor != null) _cursor.enabled = false;
            InteractionGate.IsBlocked = false;
            HideRoot();
            var cb = _onComplete;
            _onComplete = null;
            cb?.Invoke();
        }

        // ---- the fill -----------------------------------------------------------------------

        private void Update()
        {
            if (!IsShowing) return;

            // Unscaled: a conversation reads at the same speed whether or not the world is paused
            // underneath it, and nothing here feeds the simulation (rule 5 is untouched — no clock, no
            // seed, nothing saved).
            float dt = Time.unscaledDeltaTime;

            // The marker bobs whenever it is up, which includes while the option rows are open —
            // it is the "there is more" beat, not a property of the typewriter.
            TickMarker(dt);

            if (_typewriter != null && !_typewriter.IsComplete)
            {
                if (_typewriter.Advance(dt) > 0) Draw();

                while (_typewriter.TryTakeTick(out int index, out char c))
                    EventBus.Publish(new DialogueTypewriterTick(_speakerId, _voice.TimbreId, index, c));

                if (_typewriter.IsComplete) ShowHint(true);
            }

            // Unconditional: this is what HIDES the caret when the fill ends, so it cannot be
            // reached only on the frames the caret should be visible.
            PlaceCaret();
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

        /// <summary>
        /// "The line is finished — press on." Shown the instant the fill completes.
        ///
        /// <para>The kit's continue marker REPLACES the text hint when it is wired; with no art the
        /// hint carries on as before. Both are never up at once — two ways of saying the same thing
        /// in one corner reads as a bug.</para>
        /// </summary>
        private void ShowHint(bool show)
        {
            bool hasMarker = _marker != null && _markerSprites != null &&
                             _markerSprites.Length > 0 && _markerSprites[0] != null;

            bool showText = show && !hasMarker;
            if (_hintText != null && _hintText.enabled != showText) _hintText.enabled = showText;

            bool showMarker = show && hasMarker;
            if (_marker != null && _marker.enabled != showMarker) _marker.enabled = showMarker;

            if (showMarker)
            {
                // Restart the bob with the marker, so it always appears on frame 0 rather than
                // wherever a free-running clock happened to be.
                _markerClock = 0f;
                SetMarkerFrame(0);
            }
            else
            {
                MarkerFrame = -1;
            }
        }

        private void SetMarkerFrame(int frame)
        {
            if (_marker == null || _markerSprites == null || _markerSprites.Length == 0) return;

            int i = frame % _markerSprites.Length;
            if (i < 0) i += _markerSprites.Length;
            if (_markerSprites[i] == null) return;

            MarkerFrame = i;
            _marker.sprite = _markerSprites[i];
        }

        /// <summary>
        /// Step the continue marker's 2-frame idle bob — +1 px every 420 ms.
        ///
        /// <para>Unscaled, like the fill: a conversation reads at the same speed whether or not the
        /// world is paused underneath it.</para>
        /// </summary>
        private void TickMarker(float unscaledDelta)
        {
            if (_marker == null || !_marker.enabled) return;

            _markerClock += unscaledDelta;
            float frameSeconds = DialogueBubbleKit.MarkerFrameMs / 1000f;
            if (frameSeconds <= 0f) return;

            SetMarkerFrame(Mathf.FloorToInt(_markerClock / frameSeconds));
        }

        /// <summary>
        /// Put the caret where the next character will land, and show it WHILE FILLING ONLY.
        ///
        /// <para>⚠️ <b>The honest limit.</b> The kit's caret owns a CELL — its text is a monospace
        /// 5×10 grid, which is what lets a caret occupy one character's worth of space exactly. This
        /// presenter still draws its words in the built-in legacy font, because importing the kit's
        /// newly-drawn face as a Unity font asset is a separate job from dressing the bubble (the kit
        /// ships glyph ROWS — <c>type.caps</c>, <c>type.lower</c>, <c>type.rest</c>, <c>type.figs</c>
        /// — not a font). So the caret is placed at the end of the drawn text rather than in a cell,
        /// via the text generator's own cursor position: right behaviour, right pixel size, and the
        /// grid arrives with the face. Flagged in the PR, not papered over.</para>
        /// </summary>
        private void PlaceCaret()
        {
            if (_caret == null) return;

            bool show = _caretSprite != null && IsFilling && _bodyText != null;
            if (_caret.enabled != show) _caret.enabled = show;
            if (!show) return;

            var gen = _bodyText.cachedTextGenerator;
            if (gen == null || gen.characterCount == 0) return;

            // characterCount includes a trailing sentinel whose cursorPos is where the NEXT glyph
            // would start — which is exactly where the caret belongs.
            UICharInfo last = gen.characters[gen.characterCount - 1];
            float scale = _bodyText.pixelsPerUnit > 0f ? 1f / _bodyText.pixelsPerUnit : 1f;

            // Generator coordinates are in the TEXT rect's local space, and cursorPos.y is the TOP
            // of the line (hence the caret's top-left pivot). The body text is anchored to the whole
            // bubble rect, so its anchoredPosition is already the offset from the bubble's centre —
            // which makes the conversion one addition, with the caret anchored to that same centre.
            var caretRt = _caret.rectTransform;
            caretRt.anchorMin = caretRt.anchorMax = new Vector2(0.5f, 0.5f);
            caretRt.pivot = new Vector2(0f, 1f);
            caretRt.anchoredPosition = _bodyText.rectTransform.anchoredPosition +
                                       new Vector2(last.cursorPos.x, last.cursorPos.y) * scale;
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

            PlaceTail(place, anchorPoint, hasAnchor);
            PlaceChip(place);
            PlaceOptions(place, canvasSize);
        }

        /// <summary>
        /// <b>Aim the tail's measured tip pixel at the speaker.</b>
        ///
        /// <para>This is the one place the kit's most expensive measurement is spent. The tail's tip
        /// pixel IS the anchor — the art lane measured it in BOTH directions rather than deriving the
        /// up set by flipping the down set, precisely so this function has a number instead of an
        /// assumption. So the tail is not centred, or aligned by a corner, or scaled to fit: it is
        /// positioned so that one specific pixel lands on one specific point.</para>
        ///
        /// <para>Which of the six is chosen follows the placement, not the speaker's facing: the
        /// bubble may have slid away from a screen edge, and the tail leaves whichever third of the
        /// panel is nearest the person talking. <see cref="DialogueBubbleKit.TailIndexFor"/> is the
        /// pure half of that, so the choice is assertable without a camera.</para>
        ///
        /// <para>Without the six-tail set wired this falls back to the greybox behaviour exactly —
        /// one sprite (or a tinted rect), flipped in y when the bubble hangs under the speaker.</para>
        /// </summary>
        private void PlaceTail(in DialogueBubblePlacement place, Vector2 anchorPoint, bool hasAnchor)
        {
            if (_tail == null) return;

            if (_tail.enabled != hasAnchor) _tail.enabled = hasAnchor;
            if (!hasAnchor) { TailIndex = -1; return; }

            var tailRt = _tail.rectTransform;

            if (!HasTailSet)
            {
                // Greybox / single-sprite path, unchanged.
                TailIndex = -1;
                _tail.sprite = _tailSprite;
                if (_tailSprite != null) _tail.color = Color.white;
                _tail.type = Image.Type.Simple;
                tailRt.sizeDelta = new Vector2(TailWidth, TailHeight);
                tailRt.pivot = new Vector2(0.5f, 1f);
                tailRt.anchoredPosition = place.TailRoot;
                tailRt.localScale = new Vector3(1f, place.TailPointsDown ? 1f : -1f, 1f);
                return;
            }

            float bubbleLeft = place.BubbleCentre.x - _bubbleRect.sizeDelta.x * 0.5f;
            int index = DialogueBubbleKit.TailIndexFor(place.TailPointsDown, place.TailRoot.x,
                                                       bubbleLeft, _bubbleRect.sizeDelta.x, Scale);
            TailIndex = index;

            _tail.sprite = _tailSprites[index];
            _tail.color = Color.white;
            _tail.type = Image.Type.Simple;
            // Never flipped — the up tails are drawn. A flip here would silently move the tip.
            tailRt.localScale = Vector3.one;

            float w = DialogueBubbleKit.TailWidth * Scale;
            float h = DialogueBubbleKit.TailHeights[index] * Scale;
            tailRt.sizeDelta = new Vector2(w, h);

            // Pivot at the tip, expressed in the sprite's own normalised space, so setting the
            // rect's position IS setting where the tip lands. The kit reports the tip from the
            // TOP-left; a RectTransform pivot is from the BOTTOM-left, hence the y flip. The +0.5
            // puts the pivot at the CENTRE of that pixel rather than on its corner — the tip is a
            // pixel, not a lattice point (the same ADR 0026 reasoning the bakers use for rig pivots).
            float tipX = DialogueBubbleKit.TailTipX[index] + 0.5f;
            float tipY = DialogueBubbleKit.TailTipY[index] + 0.5f;
            tailRt.pivot = new Vector2(tipX / DialogueBubbleKit.TailWidth,
                                       1f - tipY / DialogueBubbleKit.TailHeights[index]);

            // x from the placement (it keeps pointing at the speaker even once the bubble has
            // stopped sliding); y is the speaker's own anchor, which is what the tip reaches for.
            tailRt.anchoredPosition = new Vector2(place.TailRoot.x, anchorPoint.y);
        }

        /// <summary>
        /// Put the name chip on the panel edge AWAY from the tail — top-left for a down tail,
        /// bottom-left for an up one — "so a name and a tail never fight for a corner".
        /// </summary>
        private void PlaceChip(in DialogueBubblePlacement place)
        {
            if (_chipImage == null) return;

            // ⚠ Gated on the NAME, not on the art. The greybox showed the speaker's name on the
            // bubble's shoulder with no background at all, and hiding it whenever the gold tile is
            // missing would make dressing the bubble REMOVE information — the one thing an art pass
            // must not do. With no sprite the chip draws as a tinted rect like every other piece.
            // The CURRENT line's speaker, not the conversation's — a two-hander swaps names line by
            // line, and a chip sized for the wrong one is visibly too long or too short.
            string name = _nameText != null ? _nameText.text : _speakerName;

            bool show = !string.IsNullOrEmpty(name);
            if (_chipImage.enabled != show) _chipImage.enabled = show;
            if (_nameText != null && _nameText.enabled != show) _nameText.enabled = show;
            if (!show) return;

            var rt = _chipImage.rectTransform;
            rt.sizeDelta = new Vector2(DialogueBubbleKit.ChipWidthFor(name.Length) * Scale,
                                       DialogueBubbleKit.ChipHeight * Scale);

            // The chip overlaps the panel edge by `overlap`, so it reads as pinned to the paper
            // rather than floating beside it.
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

            // A CATALOG row hands off and comes back. The conversation does NOT end: the bubble stays
            // up and fades back, the rows go down, IsShowing stays true and the interaction gate stays
            // blocked, until CatalogClosed re-arms the picker on these same rows. _options is
            // deliberately NOT cleared here - it is exactly what comes back (owner ruling on R2,
            // 2026-08-27). Finish() must not be reached: it would clear InteractionGate.IsBlocked, drop
            // the anchor and hide the bubble, which is the player walking away mid-book.
            if (picked.OpensCatalog)
            {
                _awaitingCatalog = true;
                SetDimmed(true);
                EventBus.Publish(new CatalogViewRequested(
                    picked.CatalogSellerId, picked.CatalogSection, _speakerId));
                return;
            }

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

            // ⚑ The gold pill and the cursor sprite carry selection when the kit is wired; the
            // text cursor and the dimmed-label treatment carry it when it is not. Never both — a
            // "▸" sitting next to a drawn pointing hand is the wrong art, not a richer one.
            bool hasCursorArt = _cursor != null && _cursorSprite != null;
            bool hasPillArt = _pill != null && _goldSprite != null;

            for (int i = 0; i < _optionRows.Count; i++)
            {
                bool used = i < _options.Count;
                _optionRows[i].gameObject.SetActive(used);
                if (!used) continue;

                bool selected = i == _picker.Index;
                _optionRows[i].text = hasCursorArt
                    ? _options[i].Label
                    : (selected ? WorldStrings.OptionCursor : "   ") + _options[i].Label;

                // On gold, the selected label is dark ink at full strength; the unselected rows keep
                // the greybox's soft treatment either way.
                _optionRows[i].color = selected
                    ? new Color(0.13f, 0.12f, 0.10f, 1f)
                    : new Color(0.13f, 0.12f, 0.10f, hasPillArt ? 0.75f : 0.55f);
            }

            if (_optionRect != null)
            {
                float width = MaxBubbleWidth;
                float height = _options.Count * OptionRowHeight + OptionPadding * 2f;
                _optionRect.sizeDelta = new Vector2(width, height);
            }

            PlaceSelectionMarks(hasPillArt, hasCursorArt);
        }

        /// <summary>
        /// Put the pill behind the selected row and the cursor beside it.
        ///
        /// <para>Both hang off the SAME row rect the label uses, so selection cannot drift from the
        /// thing it is selecting — the failure that makes a menu unusable and is invisible in a
        /// screenshot of any single frame.</para>
        /// </summary>
        private void PlaceSelectionMarks(bool hasPillArt, bool hasCursorArt)
        {
            int index = _picker != null ? _picker.Index : -1;
            bool valid = index >= 0 && index < _optionRows.Count;

            if (_pill != null)
            {
                bool show = hasPillArt && valid;
                if (_pill.enabled != show) _pill.enabled = show;
                if (show)
                {
                    var rowRt = _optionRows[index].rectTransform;
                    var pillRt = _pill.rectTransform;
                    // The kit insets the pill by panel.insetL − 1 on each side of the bubble.
                    float inset = (DialogueBubbleKit.PanelInsetL - 1) * Scale;
                    pillRt.offsetMin = new Vector2(inset, pillRt.offsetMin.y);
                    pillRt.offsetMax = new Vector2(-inset, pillRt.offsetMax.y);
                    pillRt.sizeDelta = new Vector2(pillRt.sizeDelta.x, OptionRowHeight);
                    pillRt.anchoredPosition = new Vector2(0f, rowRt.anchoredPosition.y);
                }
            }

            if (_cursor != null)
            {
                bool show = hasCursorArt && valid;
                if (_cursor.enabled != show) _cursor.enabled = show;
                if (show)
                {
                    var rowRt = _optionRows[index].rectTransform;
                    // Left of the label, inside the kit's own text origin — which is what that
                    // 16 px inset is FOR.
                    _cursor.rectTransform.anchoredPosition =
                        new Vector2(OptionTextX * 0.5f,
                                    rowRt.anchoredPosition.y - OptionRowHeight * 0.5f);
                }
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
                // Left inset is the kit's own text origin, which is what leaves the cursor its
                // column; without art that same inset just reads as comfortable padding.
                rt.offsetMin = new Vector2(OptionTextX, 0f);
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

            // ⭐ THE 9-SLICE BORDERS ARE SCALED BY referencePixelsPerUnit / sprite.pixelsPerUnit, and
            // that ratio is NOT something to leave at its default. At Unity's default 100 against the
            // kit's PPU 32 the multiplier is 3.125 — a fractional scale, which resamples the panel's
            // 1 px ink ring into mush at exactly the pixel-perfect zoom tiers ruling #327 exists to
            // keep crisp. Pinning the reference to PPU × ArtScale makes it EXACTLY ArtScale, so a kit
            // pixel is a whole number of canvas units and the corners stay hard.
            _canvas.referencePixelsPerUnit =
                DialogueBubbleKit.PixelsPerUnit * DialogueBubbleKit.ArtScale;

            _canvasRect = canvasGo.GetComponent<RectTransform>();

            // Root holder (toggled on/off). Everything under it is positioned from the bottom-left in
            // canvas units, which is the frame DialogueBubbleLayout answers in.
            _root = new GameObject("DialogueRoot", typeof(RectTransform));
            _root.transform.SetParent(canvasGo.transform, false);
            Stretch(_root.GetComponent<RectTransform>());
            _rootGroup = _root.AddComponent<CanvasGroup>();

            var rootRt = (RectTransform)_root.transform;

            // The tail sits UNDER the body so its blunt end is hidden by the bubble's own edge.
            _tail = MakeImage(rootRt, "Tail", _tailSprite, new Color(0.97f, 0.96f, 0.92f, 0.98f));
            Corner(_tail.rectTransform, new Vector2(0.5f, 1f));
            _tail.rectTransform.sizeDelta = new Vector2(TailWidth, TailHeight);
            _tail.enabled = false;

            _bubbleImage = MakeImage(rootRt, "Bubble", _bubbleSprite,
                                     new Color(0.97f, 0.96f, 0.92f, 0.98f));
            _bubbleRect = _bubbleImage.rectTransform;
            Corner(_bubbleRect, new Vector2(0.5f, 0.5f));
            _bubbleRect.sizeDelta = new Vector2(MinBubbleWidth, MinBubbleHeight);

            // The speaker's name, small, on the bubble's shoulder. NOT a nameplate and NOT a portrait —
            // the person is right there under the tail; this is for a letter or a logbook, which have a
            // name and no body.
            //
            // The chip is a gold 9-slice under the text, mounted x + 4 and overlapping the panel edge
            // by 4 — and it rides the edge AWAY FROM THE TAIL (top-left for a down tail, bottom-left
            // for an up one) so a name and a tail never fight for the same corner. Placed each frame
            // in PlaceChip, because which edge is "away" depends on the placement.
            _chipImage = MakeImage(_bubbleRect, "NameChip", _goldSprite,
                                   new Color(0.88f, 0.69f, 0.23f, 0.98f));
            _chipImage.rectTransform.anchorMin = new Vector2(0f, 1f);
            _chipImage.rectTransform.anchorMax = new Vector2(0f, 1f);
            _chipImage.rectTransform.pivot = new Vector2(0f, 0.5f);
            _chipImage.enabled = false;

            _nameText = MakeText(_chipImage.rectTransform, "Name", TextAnchor.MiddleLeft, NameFontSize);
            _nameText.color = new Color(0.13f, 0.12f, 0.10f, 1f);
            SetOutline(_nameText, false);
            var nr = _nameText.rectTransform;
            nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one;
            nr.pivot = new Vector2(0.5f, 0.5f);
            nr.offsetMin = new Vector2(DialogueBubbleKit.ChipPadX * Scale, 0f);
            nr.offsetMax = new Vector2(-DialogueBubbleKit.ChipPadX * Scale, 0f);

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

            // The continue marker: 7×4 with a +1 px bob, mounted x + w − 11 and riding the bottom
            // edge 2 px up — "right of centre, clear of every tail". It appears only when the fill
            // completes, which is exactly when the text hint above used to. When the marker sprite
            // is wired it replaces that hint; without it the hint carries on.
            _marker = MakeImage(_bubbleRect, "ContinueMarker", null, Color.white);
            _marker.rectTransform.anchorMin = new Vector2(1f, 0f);
            _marker.rectTransform.anchorMax = new Vector2(1f, 0f);
            _marker.rectTransform.pivot = new Vector2(0f, 1f);
            _marker.rectTransform.sizeDelta =
                new Vector2(DialogueBubbleKit.MarkerWidth * Scale,
                            DialogueBubbleKit.MarkerCellHeight * Scale);
            _marker.rectTransform.anchoredPosition =
                new Vector2(-DialogueBubbleKit.MarkerInsetR * Scale,
                            DialogueBubbleKit.MarkerDropB * Scale);
            _marker.enabled = false;

            // The caret: a 1×6 ink bar in the cell the next character lands in, shown WHILE FILLING
            // only. See PlaceCaret for the honest limit on "cell" here.
            _caret = MakeImage(_bubbleRect, "Caret", null, Color.white);
            _caret.rectTransform.anchorMin = new Vector2(0f, 1f);
            _caret.rectTransform.anchorMax = new Vector2(0f, 1f);
            _caret.rectTransform.pivot = new Vector2(0f, 1f);
            _caret.rectTransform.sizeDelta = new Vector2(DialogueBubbleKit.CaretWidth * Scale,
                                                         DialogueBubbleKit.CaretHeight * Scale);
            _caret.enabled = false;

            // The option bubble is its own root so it can be parked somewhere else entirely (the owner's
            // AC-corner dial) without the speech bubble knowing.
            _optionRoot = new GameObject("OptionRoot", typeof(RectTransform), typeof(Image));
            _optionRoot.transform.SetParent(canvasGo.transform, false);
            _optionImage = _optionRoot.GetComponent<Image>();
            _optionImage.raycastTarget = false;
            _optionRect = _optionRoot.GetComponent<RectTransform>();
            Corner(_optionRect, new Vector2(0.5f, 0.5f));
            _optionRect.sizeDelta = new Vector2(MaxBubbleWidth, OptionRowHeight * 2f + OptionPadding * 2f);

            // ⚑ THE GOLD PILL — the shipped row highlight. It is created BEFORE the rows so it sits
            // behind them in sibling order: the pill is the ground the label stands on, and the kit
            // draws dark ink over gold, not gold over ink.
            //
            // "The one place a saturated colour is allowed, because selection is the one thing that
            // must never be missed."
            _pill = MakeImage(_optionRect, "Pill", _goldSprite,
                              new Color(0.88f, 0.69f, 0.23f, 0.98f));
            _pill.rectTransform.anchorMin = new Vector2(0f, 1f);
            _pill.rectTransform.anchorMax = new Vector2(1f, 1f);
            _pill.rectTransform.pivot = new Vector2(0.5f, 1f);
            _pill.enabled = false;

            // The cursor sprite that replaces WorldStrings.OptionCursor's "▸".
            _cursor = MakeImage(_optionRect, "Cursor", null, Color.white);
            _cursor.rectTransform.anchorMin = new Vector2(0f, 1f);
            _cursor.rectTransform.anchorMax = new Vector2(0f, 1f);
            _cursor.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _cursor.enabled = false;

            _optionRoot.SetActive(false);

            ApplyKitArt();
        }

        /// <summary>
        /// Push whatever sprites are currently wired onto the canvas objects. Split out of
        /// <see cref="BuildCanvas"/> because <see cref="WireKitArt"/> can arrive AFTER Awake — a
        /// builder dresses the presenter on an already-constructed component — and because it keeps
        /// the "sprite or fallback tint" decision in exactly one place per piece.
        ///
        /// <para><b>Every branch here leaves something drawable.</b> A null sprite means the tinted
        /// rect stays, which is the greybox the whole mechanism was built and tested against.</para>
        /// </summary>
        private void ApplyKitArt()
        {
            SetSprite(_bubbleImage, _bubbleSprite, Image.Type.Sliced,
                      new Color(0.97f, 0.96f, 0.92f, 0.98f));
            SetSprite(_optionImage, _optionSprite, Image.Type.Sliced,
                      new Color(0.97f, 0.96f, 0.92f, 0.98f));
            SetSprite(_pill, _goldSprite, Image.Type.Sliced,
                      new Color(0.88f, 0.69f, 0.23f, 0.98f));
            SetSprite(_chipImage, _goldSprite, Image.Type.Sliced,
                      new Color(0.88f, 0.69f, 0.23f, 0.98f));

            if (_cursor != null && _cursorSprite != null)
            {
                _cursor.sprite = _cursorSprite;
                _cursor.color = Color.white;
                _cursor.rectTransform.sizeDelta =
                    new Vector2(_cursorSprite.rect.width * Scale, _cursorSprite.rect.height * Scale);
            }

            if (_caret != null && _caretSprite != null)
            {
                _caret.sprite = _caretSprite;
                _caret.color = Color.white;
            }

            if (_marker != null && _markerSprites != null && _markerSprites.Length > 0 &&
                _markerSprites[0] != null)
            {
                _marker.sprite = _markerSprites[0];
                _marker.color = Color.white;
            }

            // The chip only draws when there is a name to put in it AND art to draw it with; a gold
            // slab behind nothing is worse than the shoulder text the greybox had.
            if (_chipImage != null) _chipImage.enabled = false;
        }

        /// <summary>Sprite when there is one, tinted rect when there is not. The single place that
        /// decision is made, so "right art or fallback, never wrong art" is one function.</summary>
        private static void SetSprite(Image image, Sprite sprite, Image.Type type, Color fallback)
        {
            if (image == null) return;

            if (sprite != null)
            {
                image.sprite = sprite;
                // ⚠ Sliced needs a border or Unity logs a warning per frame and draws it Simple
                // anyway. The fixed-size stamps have no border and must stay Simple.
                image.type = sprite.border == Vector4.zero ? Image.Type.Simple : type;
                image.color = Color.white;
                return;
            }

            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = fallback;
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
