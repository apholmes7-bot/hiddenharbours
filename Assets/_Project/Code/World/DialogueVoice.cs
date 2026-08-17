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
            TimbreId = null,
        };

        /// <summary>
        /// This voice with any nonsense laundered out — the one place a zero rate or a zero stride is
        /// turned back into something that can actually finish a line. A def deserialized from an older
        /// asset (every field zero) therefore fills at the default cadence rather than hanging forever on
        /// the first character, which is the failure mode worth designing out.
        /// </summary>
        public DialogueVoice Sanitised() => new DialogueVoice
        {
            CharactersPerSecond = CharactersPerSecond > 0f ? CharactersPerSecond : DefaultCharactersPerSecond,
            CharactersPerTick = CharactersPerTick > 0 ? CharactersPerTick : DefaultCharactersPerTick,
            PunctuationPauseSeconds = Mathf.Max(0f, PunctuationPauseSeconds),
            TimbreId = TimbreId,
        };
    }

    /// <summary>
    /// <b>One character's speaking cadence, as DATA</b> (ADR 0003 / CLAUDE.md rule 2): one asset per file
    /// under <c>Data/NPCs/Voices</c>, keyed by a stable, append-only <see cref="Id"/>
    /// (<c>voice.snake_case</c>, e.g. <c>voice.aunt_ginny</c>), and pointed at from
    /// <see cref="NpcDef.Voice"/>.
    ///
    /// <para><b>Why an asset of its own rather than four fields on <see cref="NpcDef"/>.</b> NpcDef's own
    /// charter is to stay the minimal identity shape (name + dialogue + flag + body), and a cadence is
    /// SHARED: half the harbour can speak in the same unhurried island voice and be re-tuned in one edit.
    /// It is also the shape the owner's taste pass wants — sit with one asset, drag one slider, hear the
    /// difference — rather than hunting the same number across a dozen character files.</para>
    ///
    /// <para>Leave <see cref="NpcDef.Voice"/> empty and nothing breaks: that speaker fills at
    /// <see cref="DialogueVoice.Default"/>. An unauthored voice is a default cadence, never a silent or
    /// stalled bubble.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Dialogue Voice", fileName = "Voice")]
    public class DialogueVoiceDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (voice.snake_case, e.g. voice.aunt_ginny). Content-validated for " +
                 "uniqueness.")]
        public string Id = "voice.example";

        [Header("Cadence (rule 6 — the owner's dial, not a constant in code)")]
        [Tooltip("How this person's bubble fills and clicks. Defaults mirror DialogueVoice.Default.")]
        public DialogueVoice Voice = DialogueVoice.Default;

        /// <summary>This asset's cadence, laundered (see <see cref="DialogueVoice.Sanitised"/>). Null-safe
        /// at the call site via <see cref="VoiceOf"/>.</summary>
        public DialogueVoice Resolved => Voice.Sanitised();

        /// <summary>The cadence of a voice asset that may not exist — the default when it does not. The
        /// one lookup every caller should use, so "no voice authored" can never mean "no fill".</summary>
        public static DialogueVoice VoiceOf(DialogueVoiceDef def)
            => def != null ? def.Resolved : DialogueVoice.Default;
    }
}
