using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>How fast this person's bubble fills, and how it clicks</b> — the characterisation channel the
    /// owner named on 2026-07-30: *"the sounds can be the speak bubble itself populating"*, with
    /// per-character cadence as the taste surface (`design/dialogue-and-knowledge.md` §2 and §5 Q2).
    ///
    /// <para>A plain serializable value, not a MonoBehaviour and not a ScriptableObject, so the fill rule
    /// (<see cref="DialogueTypewriter"/>) is POCO-testable with numbers rather than assets — the same
    /// split <see cref="RoutinePlan"/> uses. The AUTHORED form is <see cref="DialogueVoiceDef"/>; this is
    /// what comes out of it.</para>
    ///
    /// <para><b>The numbers here are a SHIPPED DEFAULT, not a design ruling.</b> They exist so the
    /// greybox has a cadence before anybody has authored one, and every one of them is meant to be
    /// overridden per character in an asset (rule 6 — the tunables live in Def assets, editable by the
    /// owner). They are deliberately in one place, named, and mirrored by the def's field defaults, the
    /// way <c>PlayerWalkController</c>'s wade fallbacks mirror <c>GameConfig</c>.</para>
    /// </summary>
    [System.Serializable]
    public struct DialogueVoice
    {
        /// <summary>The default fill rate, in characters per second. Fast enough to read as speech rather
        /// than as a loading bar, slow enough that the click is a rhythm — the starting point for the
        /// owner's per-character pass, not a ruling.</summary>
        public const float DefaultCharactersPerSecond = 42f;

        /// <summary>The default stride: one audio tick every N audible characters. 2 keeps a chatty
        /// cadence from becoming a buzz at the default rate.</summary>
        public const int DefaultCharactersPerTick = 2;

        /// <summary>The default pause after a sentence-ending or clause-ending mark, in seconds — the
        /// breath that makes filled text read as spoken rather than typed.</summary>
        public const float DefaultPunctuationPauseSeconds = 0.16f;

        /// <summary>
        /// The default time a FILLED line stands before an unprompted bubble closes itself, in seconds.
        ///
        /// <para>It exists because an ambient bubble has no press to wait for: the owner's 2026-09-06
        /// law is that overheard speech and the inner voice take nothing from the player, so the line has
        /// to end on its own (<see cref="AmbientDwell"/>). 1.4 s is a starting point at the fill rate
        /// above — long enough that a short line does not blink out the instant its last character
        /// lands, short enough that a bubble does not follow a walking villager across the harbour. ⚑ The
        /// owner's dial, per voice, in the asset: a slow speaker can be given a longer beat without
        /// touching a line of code.</para>
        /// </summary>
        public const float DefaultReadPauseSeconds = 1.4f;

        [Tooltip("How fast the bubble fills, in characters per second. THE per-character taste dial: a " +
                 "brisk skipper fills faster than Aunt Ginny. Clamped above zero — a voice that fills at " +
                 "0 would never finish a line.")]
        [Min(1f)] public float CharactersPerSecond;

        [Tooltip("One audio tick every N AUDIBLE characters (whitespace never ticks). 1 clicks on every " +
                 "letter; 3 is a slower, softer patter. The audio lane chooses what the tick sounds " +
                 "like — this only chooses when one happens.")]
        [Min(1)] public int CharactersPerTick;

        [Tooltip("Extra pause after . ! ? , ; : — and — in seconds, so a line breathes at its punctuation " +
                 "instead of running flat to the end.")]
        [Min(0f)] public float PunctuationPauseSeconds;

        [Tooltip("How long a FILLED line stands before an UNPROMPTED bubble closes itself, in seconds. " +
                 "Only ambient speech reads it — an NPC talking to another NPC, or the player's own " +
                 "inner voice — because those have no Interact press to end them. A modal conversation " +
                 "is unaffected: it still waits for the player.\n\n" +
                 "Zero means 'unauthored' and reads as the shipped default; a bubble that vanished the " +
                 "instant its last character landed was never read.")]
        [Min(0f)] public float ReadPauseSeconds;

        [Tooltip("What this voice SOUNDS like, as a stable id the audio lane resolves (e.g. " +
                 "\"timbre.warm_low\"). Nothing in the World module reads it — it rides the Core tick " +
                 "signal so audio can pick a sample without either module naming the other (rule 4).")]
        public string TimbreId;

        /// <summary>The cadence a speaker with no authored voice fills at. Every field is one of the
        /// <c>Default*</c> constants above, so there is exactly one place to change the shipped feel.</summary>
        public static DialogueVoice Default => new DialogueVoice
        {
            CharactersPerSecond = DefaultCharactersPerSecond,
            CharactersPerTick = DefaultCharactersPerTick,
            PunctuationPauseSeconds = DefaultPunctuationPauseSeconds,
            ReadPauseSeconds = DefaultReadPauseSeconds,
            TimbreId = null,
        };

        /// <summary>
        /// This voice with any nonsense laundered out — the one place a zero rate or a zero stride is
        /// turned back into something that can actually finish a line. A def deserialized from an older
        /// asset (every field zero) therefore fills at the default cadence rather than hanging forever on
        /// the first character, which is the failure mode worth designing out.
        ///
        /// <para><b>The read pause is laundered the same way, and deliberately not like the punctuation
        /// pause.</b> Zero punctuation pause is a legitimate authoring choice (a flat, hurried voice);
        /// zero read pause is not — it is what every voice asset written before the field existed
        /// deserializes to, and it would close an ambient bubble in the same frame its last character
        /// landed. So zero here means "unauthored", exactly as it does for the fill rate.</para>
        /// </summary>
        public DialogueVoice Sanitised() => new DialogueVoice
        {
            CharactersPerSecond = CharactersPerSecond > 0f ? CharactersPerSecond : DefaultCharactersPerSecond,
            CharactersPerTick = CharactersPerTick > 0 ? CharactersPerTick : DefaultCharactersPerTick,
            PunctuationPauseSeconds = Mathf.Max(0f, PunctuationPauseSeconds),
            ReadPauseSeconds = ReadPauseSeconds > 0f ? ReadPauseSeconds : DefaultReadPauseSeconds,
            TimbreId = TimbreId,
        };
    }
}
