namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>HOW LONG AN UNPROMPTED BUBBLE STAYS UP</b> — the whole of the arithmetic that replaces the
    /// Interact press for a line nobody asked for.
    ///
    /// <para>A modal conversation ends when the player presses on. An ambient one cannot: the owner's
    /// 2026-09-06 law is that overheard speech and the inner voice take NOTHING from the player, so
    /// there is no press to wait for and the bubble has to know its own life. That life is two terms and
    /// nothing else — <see cref="FillSeconds"/>, which is exactly as long as
    /// <see cref="DialogueTypewriter"/> takes to put the line on screen at this voice's cadence, plus
    /// <see cref="DialogueVoice.ReadPauseSeconds"/>, the breath the words stand in once they are all
    /// there.</para>
    ///
    /// <para><b>Why a closed form rather than "watch the typewriter and then start a timer".</b> A
    /// caller sequencing a two-hander needs to know when line 2 is due BEFORE line 1 has finished — that
    /// is what makes a conversation deterministic from (worldSeed, gameTime) rather than from however
    /// many frames the machine happened to render. So the duration is computed, not observed. The two
    /// must agree exactly, and <c>AmbientDwellTests</c> is where they are held against each other: the
    /// closed form here and the iterative reveal there are independent transcriptions of one rule, so a
    /// disagreement is a real defect in one of them rather than a mirror agreeing with itself.</para>
    ///
    /// <para><b>Pure.</b> No Unity, no <c>Time</c>, no assets — plain strings and a struct, so every
    /// number below is EditMode-assertable (the <see cref="DialogueTypewriter"/> split, one level
    /// out).</para>
    /// </summary>
    public static class AmbientDwell
    {
        /// <summary>
        /// How long <paramref name="line"/> takes to FILL at <paramref name="voice"/>'s cadence, in
        /// seconds — the same elapsed time <see cref="DialogueTypewriter.Advance"/> needs before
        /// <see cref="DialogueTypewriter.IsComplete"/> goes true.
        ///
        /// <para><b>The one subtlety, and it is worth the sentence.</b> The typewriter charges a
        /// punctuation pause to the character AFTER the mark (it is the breath before the next word), so
        /// a mark in the LAST position is never paid for — there is no next character to delay. Counting
        /// every mark in the line would therefore over-report every sentence that ends in a full stop,
        /// which is most of them.</para>
        /// </summary>
        public static float FillSeconds(string line, in DialogueVoice voice)
        {
            if (string.IsNullOrEmpty(line)) return 0f;

            DialogueVoice v = voice.Sanitised();
            int length = line.Length;

            // Marks at [0, length - 2] only: see the remarks. A one-character line can never pay a pause.
            int paidBreaths = 0;
            for (int i = 0; i < length - 1; i++)
                if (DialogueTypewriter.IsBreath(line[i])) paidBreaths++;

            return length / v.CharactersPerSecond + paidBreaths * v.PunctuationPauseSeconds;
        }

        /// <summary>
        /// The whole life of an ambient bubble carrying <paramref name="line"/> at
        /// <paramref name="voice"/>'s cadence: the fill, then the read pause.
        ///
        /// <para>An empty line dwells for nothing at all — zero, not a read pause over an empty bubble —
        /// which is what makes "refuse the request outright" the presenter's correct answer to one.</para>
        /// </summary>
        public static float Seconds(string line, in DialogueVoice voice)
        {
            if (string.IsNullOrEmpty(line)) return 0f;
            return FillSeconds(line, voice) + voice.Sanitised().ReadPauseSeconds;
        }
    }
}
