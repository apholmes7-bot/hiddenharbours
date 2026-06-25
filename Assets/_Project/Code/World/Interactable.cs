using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Whether interacting talks to a person or reads a thing — picks the prompt verb.</summary>
    public enum InteractKind { Talk, Read }

    /// <summary>
    /// A world thing the player can walk up to and INTERACT with (an NPC, a letter). It is just
    /// authored data — speaker, portrait, which conversation to play, and an optional onboarding flag
    /// to set once read. The <see cref="WorldInteractor"/> does the proximity + input work and hands
    /// the conversation to the <see cref="DialoguePresenter"/>; this component holds no logic so it
    /// stays trivial to place and wire from the region builder.
    ///
    /// <para>Content is DATA (CLAUDE.md rule 2): the preferred wiring is an <see cref="NpcDef"/>
    /// (which carries the speaker name, a <see cref="DialogueDef"/>, the verb, and the flag) dropped
    /// in via <see cref="Npc"/> — so the lines live in an asset the owner can edit, not in code. The
    /// legacy per-field string path (<see cref="ConversationId"/> → <see cref="WorldStrings"/>) remains
    /// as a fallback for the older Coddle Cove wiring; when an <see cref="NpcDef"/> is set it wins.</para>
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [Header("Data-driven (preferred) — the NPC/dialogue assets")]
        [Tooltip("The NpcDef this interactable represents. When set, its name/dialogue/verb/flag drive " +
                 "the conversation (content as data) and the per-field strings below are ignored.")]
        [SerializeField] private NpcDef _npc;

        [Header("Legacy fields (used only when no NpcDef is assigned)")]
        [Tooltip("Talk to a person, or Read a thing — selects the floating prompt's verb.")]
        [SerializeField] private InteractKind _kind = InteractKind.Talk;

        [Tooltip("Speaker name shown on the nameplate (e.g. \"Aunt Ginny\").")]
        [SerializeField] private string _speaker = "";

        [Tooltip("Portrait shown in the dialogue panel. Optional — null shows name + text only.")]
        [SerializeField] private Sprite _portrait;

        [Tooltip("Stable conversation id looked up in WorldStrings (e.g. \"ginny\", \"logbook\").")]
        [SerializeField] private string _conversationId = "";

        [Tooltip("Onboarding flag set true when this conversation completes (e.g. \"met_ginny\"). " +
                 "Empty = no flag. Drives the warmer 'met before' variant and the onboarding nudges.")]
        [SerializeField] private string _completionFlag = "";

        /// <summary>The NpcDef driving this interactable, if any (the data-driven path).</summary>
        public NpcDef Npc => _npc;

        /// <summary>True when this interactable's content comes from an <see cref="NpcDef"/> with dialogue.</summary>
        public bool HasNpcData => _npc != null && _npc.HasDialogue;

        // Presentation/lookup accessors prefer the NpcDef when one is assigned, else the legacy fields.
        public InteractKind Kind => _npc != null ? _npc.Kind : _kind;
        public string Speaker => _npc != null ? _npc.DisplayName : _speaker;
        public Sprite Portrait => _portrait;
        public string ConversationId => _conversationId;
        public string CompletionFlag => _npc != null ? _npc.CompletionFlag : _completionFlag;

        /// <summary>The authored dialogue lines for this interactable, or null if it uses the legacy path.</summary>
        public string[] DialogueLines(bool metBefore) => HasNpcData ? _npc.Dialogue.Lines(metBefore) : null;

        /// <summary>Wire the legacy (WorldStrings) path in one call (tests / older cove builder).</summary>
        public void Configure(InteractKind kind, string speaker, Sprite portrait,
                              string conversationId, string completionFlag)
        {
            _npc = null;
            _kind = kind;
            _speaker = speaker;
            _portrait = portrait;
            _conversationId = conversationId;
            _completionFlag = completionFlag;
        }

        /// <summary>Wire the data-driven path in one call: an NpcDef (name/dialogue/verb/flag) + an
        /// optional portrait (presentation, owned by art-pipeline). Tests / the region builder.</summary>
        public void Configure(NpcDef npc, Sprite portrait = null)
        {
            _npc = npc;
            _portrait = portrait;
        }
    }
}
