using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>Whether interacting talks to a person or reads a thing — picks the prompt verb.</summary>
    public enum InteractKind { Talk, Read }

    /// <summary>
    /// A world thing the player can walk up to and INTERACT with (an NPC, Ned's logbook). It is just
    /// authored data — speaker, portrait, which conversation to play, and an optional onboarding flag
    /// to set once read. The <see cref="WorldInteractor"/> does the proximity + input work and hands
    /// the conversation to the <see cref="DialoguePresenter"/>; this component holds no logic so it
    /// stays trivial to place and wire from the greybox builder.
    ///
    /// The dialogue COPY lives in <see cref="WorldStrings"/> (the loc seam), looked up by
    /// <see cref="ConversationId"/>; this component supplies only the per-speaker presentation
    /// (name + portrait) and the flag bookkeeping.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
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

        public InteractKind Kind => _kind;
        public string Speaker => _speaker;
        public Sprite Portrait => _portrait;
        public string ConversationId => _conversationId;
        public string CompletionFlag => _completionFlag;

        /// <summary>Wire an interactable in one call (tests / editor).</summary>
        public void Configure(InteractKind kind, string speaker, Sprite portrait,
                              string conversationId, string completionFlag)
        {
            _kind = kind;
            _speaker = speaker;
            _portrait = portrait;
            _conversationId = conversationId;
            _completionFlag = completionFlag;
        }
    }
}
