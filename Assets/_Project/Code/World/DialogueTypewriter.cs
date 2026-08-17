namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One line filling into the bubble, a character at a time</b> — and the thing that decides when
    /// the audio ticks, because *the fill IS the sound* (`design/dialogue-and-knowledge.md` §2). Pure:
    /// no Unity lifecycle, no <c>Time</c>, no randomness, so it is EditMode-testable with plain numbers
    /// and a list of deltas — the <see cref="DialogueRunner"/> split, applied one level down.
    ///
    /// <para><b>What it owns.</b> How many characters are visible at time <c>t</c>, the breath after
    /// punctuation, and WHICH revealed characters are audible ticks. What it deliberately does not own:
    /// any sound (the presenter publishes <c>Core.DialogueTypewriterTick</c> and the audio lane decides
    /// what that is), and any layout.</para>
    ///
    /// <para><b>Ticks are drained, not fired.</b> <see cref="Advance"/> reveals; the caller then pulls
    /// ticks with <see cref="TryTakeTick"/> until it returns false. A queue-free drain (one forward
    /// cursor over the text) keeps this allocation-free per frame (rule 7) and keeps the class free of a
    /// callback the tests would have to fake.</para>
    ///
    /// <para><b>Completing a line does not machine-gun the audio.</b> <see cref="CompleteNow"/> — what an
    /// impatient Interact press does — reveals the rest and DISCARDS the ticks it skipped past, because
    /// eighty clicks in one frame is a buzz, not a voice. The audible ordinal keeps counting through the
    /// discard so the stride's rhythm is the same either way.</para>
    /// </summary>
    public sealed class DialogueTypewriter
    {
        private readonly string _text;
        private readonly DialogueVoice _voice;
        private readonly float _secondsPerCharacter;

        private float _accumulatedSeconds;
        private float _pendingPauseSeconds;   // the breath owed after the punctuation just revealed

        private int _tickCursor;              // first revealed index not yet drained as a tick
        private int _audibleSeen;             // how many audible characters have gone past the cursor

        private string _visibleCache;
        private int _visibleCacheAt = -1;

        public DialogueTypewriter(string text, in DialogueVoice voice)
        {
            _text = text ?? string.Empty;
            _voice = voice.Sanitised();
            _secondsPerCharacter = 1f / _voice.CharactersPerSecond;
        }

        /// <summary>The whole line, revealed or not.</summary>
        public string Text => _text;

        /// <summary>The cadence this line is filling at (already laundered).</summary>
        public DialogueVoice Voice => _voice;

        /// <summary>How many characters are on screen.</summary>
        public int Revealed { get; private set; }

        /// <summary>How many there are in total.</summary>
        public int Length => _text.Length;

        /// <summary>True once the whole line is on screen.</summary>
        public bool IsComplete => Revealed >= _text.Length;

        /// <summary>
        /// The text as it should be drawn right now. Rebuilt only when <see cref="Revealed"/> actually
        /// changes — so a finished line costs nothing per frame, and a filling one costs one small string
        /// per revealed character rather than one per frame (rule 7).
        /// </summary>
        public string VisibleText
        {
            get
            {
                if (_visibleCacheAt != Revealed)
                {
                    _visibleCache = Revealed >= _text.Length ? _text : _text.Substring(0, Revealed);
                    _visibleCacheAt = Revealed;
                }
                return _visibleCache;
            }
        }

        /// <summary>
        /// Let <paramref name="deltaSeconds"/> of fill happen. Returns how many characters were newly
        /// revealed (0 when the line is already full, or when the next character is not due yet).
        ///
        /// <para>A long frame reveals several characters rather than one, on purpose: the fill is a
        /// function of elapsed time, so a hitch does not slow the line down and the cadence stays the
        /// cadence the voice asked for.</para>
        /// </summary>
        public int Advance(float deltaSeconds)
        {
            if (IsComplete || deltaSeconds <= 0f) return 0;

            _accumulatedSeconds += deltaSeconds;

            int before = Revealed;
            while (Revealed < _text.Length)
            {
                float due = _secondsPerCharacter + _pendingPauseSeconds;
                if (_accumulatedSeconds < due) break;

                _accumulatedSeconds -= due;
                _pendingPauseSeconds = 0f;

                char c = _text[Revealed];
                Revealed++;
                if (IsBreath(c)) _pendingPauseSeconds = _voice.PunctuationPauseSeconds;
            }
            return Revealed - before;
        }

        /// <summary>
        /// Reveal the rest of the line at once — the impatient Interact press. Ticks that were skipped
        /// past are discarded (see the class remarks), and the audible ordinal is carried through them so
        /// the stride does not shift phase.
        /// </summary>
        public void CompleteNow()
        {
            while (_tickCursor < _text.Length)
            {
                if (IsAudible(_text[_tickCursor])) _audibleSeen++;
                _tickCursor++;
            }
            Revealed = _text.Length;
            _accumulatedSeconds = 0f;
            _pendingPauseSeconds = 0f;
        }

        /// <summary>
        /// Pull the next audio tick owed for characters revealed so far, or return false when there are
        /// none left this frame. Call it in a loop after <see cref="Advance"/>.
        ///
        /// <para>Whitespace never ticks and the stride is the voice's
        /// (<see cref="DialogueVoice.CharactersPerTick"/>), so a normal line clicks a few times a second
        /// rather than once per glyph.</para>
        /// </summary>
        public bool TryTakeTick(out int charIndex, out char character)
        {
            while (_tickCursor < Revealed)
            {
                char c = _text[_tickCursor];
                int index = _tickCursor;
                _tickCursor++;

                if (!IsAudible(c)) continue;

                _audibleSeen++;
                if (_audibleSeen % _voice.CharactersPerTick != 0) continue;

                charIndex = index;
                character = c;
                return true;
            }

            charIndex = -1;
            character = '\0';
            return false;
        }

        /// <summary>
        /// Does this character make a sound? <b>Whitespace does not</b> — a space is a gap between
        /// syllables, and ticking on it turns the gap into a beat. Everything else does, punctuation
        /// included (it is what a pause is heard against).
        /// </summary>
        public static bool IsAudible(char c) => !char.IsWhiteSpace(c);

        /// <summary>
        /// Does the line take a breath after this character? Sentence and clause marks only — the pause
        /// is what makes filled text read as spoken rather than typed.
        /// </summary>
        public static bool IsBreath(char c)
            => c == '.' || c == '!' || c == '?' || c == ',' || c == ';' || c == ':' || c == '—';
    }
}
