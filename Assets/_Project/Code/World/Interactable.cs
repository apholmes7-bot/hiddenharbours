using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Whether interacting talks to a person or reads a thing — picks the prompt verb.</summary>
    public enum InteractKind { Talk, Read }

    /// <summary>
    /// A world thing the player can walk up to and INTERACT with (an NPC, a letter). It is just
    /// authored data — speaker, which conversation to play, where their bubble hangs, and an optional
    /// onboarding flag to set once read. The <see cref="WorldInteractor"/> does the proximity + input work and hands
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

        [Header("Presentation")]
        [Tooltip("Where this speaker's speech bubble hangs from — normally a child transform at the top " +
                 "of their head. OPTIONAL: left empty the bubble anchors DefaultBubbleHeightMetres above " +
                 "this object's own origin, which is where a placed islander's feet are, so the greybox " +
                 "reads correctly with nothing wired. A letter on a table wants its own anchor.")]
        [SerializeField] private Transform _bubbleAnchor;

        [Header("Legacy fields (used only when no NpcDef is assigned)")]
        [Tooltip("Talk to a person, or Read a thing — selects the floating prompt's verb.")]
        [SerializeField] private InteractKind _kind = InteractKind.Talk;

        [Tooltip("Speaker name shown on the bubble (e.g. \"Aunt Ginny\").")]
        [SerializeField] private string _speaker = "";

        [Tooltip("Stable conversation id looked up in WorldStrings (e.g. \"ginny\", \"logbook\").")]
        [SerializeField] private string _conversationId = "";

        [Tooltip("Onboarding flag set true when this conversation completes (e.g. \"met_ginny\"). " +
                 "Empty = no flag. Drives the warmer 'met before' variant and the onboarding nudges.")]
        [SerializeField] private string _completionFlag = "";

        /// <summary>
        /// How far above a placed character's origin their speech bubble hangs, in metres, when no
        /// <see cref="_bubbleAnchor"/> is wired. A character is about 1.8 m of drawn body from a
        /// bottom-centre pivot (the sheets' honest metric scale, ADR 0026), so this clears the top of the
        /// head with a little air — the Animal Crossing gap between a person and what they are saying.
        /// </summary>
        public const float DefaultBubbleHeightMetres = 2.1f;

        /// <summary>The NpcDef driving this interactable, if any (the data-driven path).</summary>
        public NpcDef Npc => _npc;

        /// <summary>
        /// The transform the bubble FOLLOWS — the wired anchor if there is one, else this object itself.
        /// Paired with <see cref="BubbleAnchorOffset"/>, which is what makes a bare islander's bubble
        /// hang over their head rather than at their feet.
        ///
        /// <para><b>⚠ Explicit <c>!= null</c>, never <c>??</c>.</b> An anchor on a destroyed child is
        /// fake-null, and the null-propagating operators sail straight past Unity's overloaded
        /// <c>==</c> — the same trap <c>WorldInteractor.ResolvePlayer</c> documents.</para>
        /// </summary>
        public Transform BubbleAnchorTransform => _bubbleAnchor != null ? _bubbleAnchor : transform;

        /// <summary>How far above <see cref="BubbleAnchorTransform"/> the bubble hangs, in world metres.
        /// Zero when an anchor was wired (the author put it exactly where they meant it), head height
        /// when one was not.</summary>
        public Vector3 BubbleAnchorOffset => _bubbleAnchor != null
            ? Vector3.zero
            : new Vector3(0f, DefaultBubbleHeightMetres, 0f);

        /// <summary>Where the tail points, right now — the composed answer, for tests and tooling.</summary>
        public Vector3 BubbleAnchorWorld => BubbleAnchorTransform.position + BubbleAnchorOffset;

        /// <summary>True when this interactable's content comes from an <see cref="NpcDef"/> with dialogue.</summary>
        public bool HasNpcData => _npc != null && _npc.HasDialogue;

        // Presentation/lookup accessors prefer the NpcDef when one is assigned, else the legacy fields.
        public InteractKind Kind => _npc != null ? _npc.Kind : _kind;
        public string Speaker => _npc != null ? _npc.DisplayName : _speaker;
        public string ConversationId => _conversationId;
        public string CompletionFlag => _npc != null ? _npc.CompletionFlag : _completionFlag;

        /// <summary>The authored dialogue lines for this interactable, or null if it uses the legacy path.</summary>
        public string[] DialogueLines(bool metBefore) => HasNpcData ? _npc.Dialogue.Lines(metBefore) : null;

        /// <summary>
        /// The authored dialogue lines, with the asset's flag-gated extra beat included when
        /// <paramref name="flags"/> says its condition is met (see <see cref="DialogueDef.Lines(bool,
        /// IFlagStore)"/>). Null on the legacy path, as above.
        /// </summary>
        public string[] DialogueLines(bool metBefore, IFlagStore flags)
            => HasNpcData ? _npc.Dialogue.Lines(metBefore, flags) : null;

        /// <summary>Wire the legacy (WorldStrings) path in one call (tests / older cove builder).</summary>
        public void Configure(InteractKind kind, string speaker, string conversationId,
                              string completionFlag)
        {
            _npc = null;
            _kind = kind;
            _speaker = speaker;
            _conversationId = conversationId;
            _completionFlag = completionFlag;
        }

        /// <summary>Wire the data-driven path in one call: an NpcDef (name/dialogue/verb/flag/voice).
        /// Tests / the region builder.</summary>
        public void Configure(NpcDef npc)
        {
            _npc = npc;
        }

        /// <summary>Point the speech bubble somewhere other than this object's own origin (tests / the
        /// region builder). Null returns it to the default head height above the transform.</summary>
        public void ConfigureBubbleAnchor(Transform anchor)
        {
            _bubbleAnchor = anchor;
        }
    }
}
