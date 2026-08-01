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
    ///
    /// <para><b>A third, CONDITIONAL pool</b> (<see cref="ConditionalLines"/>, gated by
    /// <see cref="ConditionalFlag"/>) lets a speaker acknowledge something that has happened to this
    /// player — Aunt Ginny mentioning the licence fee she fronted — without a C# branch per beat. The
    /// gate is a stable save-flag KEY authored in the asset, read through the world's own
    /// <see cref="IFlagStore"/>, so a flag another module persists (the economy's fronted-fee grant
    /// records <c>ginny_fronted_clam_fee</c> through the Core save service) is reachable as DATA rather
    /// than through a cross-module reference (CLAUDE.md rule 4). One condition per asset is deliberate:
    /// a second beat is a second block appended here later, not a rules engine now (rule 8).</para>
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

        [Header("Conditional lines (an extra beat, gated on a world flag)")]
        [Tooltip("Stable, append-only save-flag key that unlocks ConditionalLines (e.g. " +
                 "\"ginny_fronted_clam_fee\", the key the economy's fronted-fee grant persists). " +
                 "Empty = no condition, and the lines below never play.")]
        public string ConditionalFlag = "";

        [Tooltip("Lines APPENDED to whichever pool plays (first or repeat), but only while " +
                 "ConditionalFlag is set — so the speaker acknowledges what has actually happened to " +
                 "this player, and says nothing about it before it has.")]
        [TextArea] public string[] ConditionalLines = new string[0];

        /// <summary>
        /// The lines to play given whether the speaker has been met before. The conditional pool is NOT
        /// consulted (no flag store to ask) — every pre-existing caller keeps its exact behaviour.
        /// </summary>
        public string[] Lines(bool metBefore) => Lines(metBefore, null);

        /// <summary>
        /// The lines to play: the first/repeat pool, plus <see cref="ConditionalLines"/> appended when
        /// <paramref name="flags"/> says <see cref="ConditionalFlag"/> is set. Appended rather than
        /// substituted, because the conditional beat is something the speaker says AS WELL — and it
        /// keeps being said on the re-greet, which is the point of a debt somebody is remembering.
        /// Allocates only when the condition actually fires, and only at the start of a conversation.
        /// </summary>
        public string[] Lines(bool metBefore, IFlagStore flags)
        {
            string[] pool = (metBefore && RepeatLines != null && RepeatLines.Length > 0)
                ? RepeatLines
                : FirstLines;

            if (!ConditionMet(flags) || ConditionalLines == null || ConditionalLines.Length == 0)
                return pool;

            int poolCount = pool != null ? pool.Length : 0;
            var combined = new string[poolCount + ConditionalLines.Length];
            if (poolCount > 0) System.Array.Copy(pool, combined, poolCount);
            System.Array.Copy(ConditionalLines, 0, combined, poolCount, ConditionalLines.Length);
            return combined;
        }

        /// <summary>
        /// True when this asset carries a condition AND the store says it is met. An unset key, a null
        /// store (EditMode / pre-boot) and an unset flag all read the same: the extra lines stay silent.
        /// Failing CLOSED is deliberate — a line about a debt the player was never given would be worse
        /// than a line that is missing.
        /// </summary>
        public bool ConditionMet(IFlagStore flags)
            => !string.IsNullOrEmpty(ConditionalFlag) && flags != null && flags.Get(ConditionalFlag);
    }
}
