using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The authored index of every <see cref="ConversationDef"/> the self-installing
    /// <see cref="NpcConversationDirector"/> loads at boot — the <c>InnerVoiceLibrary</c> /
    /// <c>SeaweedLibrary</c> / <c>AmbientFleetLibrary</c> pattern: the exchanges live one per file under
    /// <c>Data/NPCs/Conversations</c> (ADR 0003), this one asset gathers the references, and it sits in
    /// <c>Resources</c> so a <see cref="RuntimeInitializeOnLoadMethod"/> host can find it with no scene
    /// and no builder wiring.
    ///
    /// <para>Append-only: a new exchange is a new def asset plus one entry here. Null entries are
    /// skipped, so a half-filled array costs the exchanges that are missing and nothing else — and the
    /// content validator fails on both a hole and an authored def nobody listed.</para>
    ///
    /// <para>Create via Assets ▸ Create ▸ Hidden Harbours ▸ Conversation Library, save at
    /// <c>Resources/ConversationLibrary</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Conversation Library", fileName = "ConversationLibrary")]
    public sealed class ConversationLibrary : ScriptableObject
    {
        /// <summary>Resources path (no extension) the director loads the library from at boot.</summary>
        public const string ResourcesPath = "ConversationLibrary";

        [Tooltip("Every authored two-hander. One Def per file in Data/NPCs/Conversations; null entries " +
                 "are skipped.")]
        public ConversationDef[] Conversations = Array.Empty<ConversationDef>();
    }
}
