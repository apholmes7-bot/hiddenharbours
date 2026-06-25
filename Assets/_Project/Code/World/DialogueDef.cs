using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One authored conversation, as DATA (ADR 0003 / CLAUDE.md rule 2): one asset per file under
    /// <c>Data/NPCs/Dialogue</c>, keyed by a stable, append-only <see cref="Id"/>
    /// (<c>dialogue.snake_case</c>, e.g. <c>dialogue.ginny_first</c>). The lines an NPC speaks are no
    /// longer hard-coded in <see cref="WorldStrings"/> — they live here so the owner can edit the
    /// opening's words without touching code, and so new NPCs are a new asset, not a new C# branch.
    ///
    /// <para>Two pools: <see cref="FirstLines"/> plays the first time (the full beat), and
    /// <see cref="RepeatLines"/> is a shorter, warmer re-greet once met — exactly the
    /// <c>metBefore</c> split the legacy <see cref="WorldStrings.Conversation"/> had, now per-asset.
    /// Empty <see cref="RepeatLines"/> falls back to <see cref="FirstLines"/>.</para>
    ///
    /// <para>Localization seam: each line is plain English copy for now (the same stand-in the rest of
    /// the world layer uses — there is no runtime loc table wired yet, a lead-architect call). When loc
    /// tables land, a line becomes a key lookup and no call site changes (see <see cref="WorldStrings"/>).
    /// Keeping the FORMAT data-driven is the commitment now (design/npcs-and-routines.md §6).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Dialogue", fileName = "Dialogue")]
    public class DialogueDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (dialogue.snake_case, e.g. dialogue.ginny_first). Content-validated for uniqueness.")]
        public string Id = "dialogue.example";

        [Header("Lines (the localization seam — plain copy for now)")]
        [Tooltip("The conversation the FIRST time it plays (the full opening beat).")]
        [TextArea] public string[] FirstLines = new string[0];

        [Tooltip("A shorter, warmer re-greet once the player has met this speaker. Empty = reuse FirstLines.")]
        [TextArea] public string[] RepeatLines = new string[0];

        /// <summary>The lines to play given whether the speaker has been met before.</summary>
        public string[] Lines(bool metBefore)
            => (metBefore && RepeatLines != null && RepeatLines.Length > 0) ? RepeatLines : FirstLines;
    }
}
