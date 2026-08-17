namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The sound IS the bubble populating</b> — one of these per audible character as the speech
    /// bubble fills (`design/dialogue-and-knowledge.md` §2, the owner's 2026-07-30 direction: "the sounds
    /// can be the speak bubble itself populating"). No voice, no animalese synth: the fill event is the
    /// audio event.
    ///
    /// <para><b>Why the presenter publishes rather than plays.</b> The World module may not reach into
    /// Audio and Audio may not reach into World (CLAUDE.md rule 4), so the tick crosses through Core the
    /// same way <see cref="InteractPerformed"/> does. The presenter owns WHEN a character lands; the audio
    /// lane owns what that sounds like, in its own later PR. Until it subscribes, a conversation is
    /// silent and nothing else changes.</para>
    ///
    /// <para><b>Not every character ticks.</b> The presenter emits on the voice's stride and skips
    /// whitespace (a space is not a syllable), so this is a handful of events per second and not one per
    /// revealed glyph — see <c>DialogueTypewriter.IsAudible</c>, which is where that rule lives and is
    /// tested.</para>
    /// </summary>
    public readonly struct DialogueTypewriterTick
    {
        /// <summary>Whose cadence is filling — the speaker's <c>NpcDef.Id</c>, or null on the legacy
        /// string-driven path. What an audio lane keys a per-character timbre off.</summary>
        public readonly string SpeakerId;

        /// <summary>The voice asset's id (<c>voice.snake_case</c>), or null when the speaker has no
        /// authored voice and is filling at the default cadence.</summary>
        public readonly string VoiceId;

        /// <summary>Index of the character just revealed, within the line being filled.</summary>
        public readonly int CharIndex;

        /// <summary>The character just revealed. Punctuation reads differently from a letter, and the
        /// audio lane may want to know that without re-deriving it.</summary>
        public readonly char Character;

        public DialogueTypewriterTick(string speakerId, string voiceId, int charIndex, char character)
        {
            SpeakerId = speakerId;
            VoiceId = voiceId;
            CharIndex = charIndex;
            Character = character;
        }
    }

    /// <summary>
    /// <b>The player chose something in a conversation.</b> Published AFTER the option bubble has closed on
    /// that row, so a listener sees the choice as made rather than as pending.
    ///
    /// <para>This is the seam that lets world-content author a choice that DOES something —
    /// "can you front me the fee?" — without any module referencing any other (rule 4): the option is a
    /// row of data on a <c>DialogueDef</c>, the pick is this signal, and whichever system cares
    /// (economy, quests) subscribes and matches on <see cref="OptionId"/>. Nothing in the World module
    /// branches on an option id.</para>
    ///
    /// <para>The always-last close row ("See you later.") publishes too, under the reserved id
    /// <c>DialogueOption.CloseId</c> — uniform is easier to reason about than special, and content
    /// validation keeps authored ids off that one.</para>
    /// </summary>
    public readonly struct DialogueOptionPicked
    {
        /// <summary>The <c>DialogueDef.Id</c> the option was authored on.</summary>
        public readonly string DialogueId;

        /// <summary>The chosen option's stable id (<c>option.snake_case</c>).</summary>
        public readonly string OptionId;

        /// <summary>The speaker's <c>NpcDef.Id</c>, so one shared option id can mean "ask THIS person".</summary>
        public readonly string SpeakerId;

        public DialogueOptionPicked(string dialogueId, string optionId, string speakerId)
        {
            DialogueId = dialogueId;
            OptionId = optionId;
            SpeakerId = speakerId;
        }
    }
}
