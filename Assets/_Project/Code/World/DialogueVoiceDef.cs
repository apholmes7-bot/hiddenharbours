using UnityEngine;

namespace HiddenHarbours.World
{
    // ⚠️ THIS TYPE MUST STAY IN A FILE OF ITS OWN NAME. Unity gives a .cs file exactly one
    // MonoScript, and it is the type whose name matches the FILE — so while DialogueVoiceDef lived in
    // DialogueVoice.cs beside the struct, an asset's `m_Script: {fileID: 11500000, guid: <that file>}`
    // resolved to the STRUCT, and the asset failed to load with
    //     [Assert] 'HiddenHarbours.World.DialogueVoice' is missing the class attribute
    //             'ExtensionOfNativeClass'!
    // The def shipped with no assets authored against it, so nothing had ever caught it. Found
    // 2026-09-07 by the first voice asset in the project (voice.inner). Do not fold it back in.

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
