using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// What one missing fact of a story gate reads as: the line on the Tasks note, and what the
    /// gate's islander says when asked before it holds. Keyed by the progress rules' own text key.
    /// </summary>
    [Serializable]
    public struct StoryGateNote
    {
        [Tooltip("The text key the progress rules give this fact (e.g. tasks.st_peters.mend_dory).")]
        public string TextKey;
        [Tooltip("The Tasks note while this fact is the first one missing.")]
        [TextArea] public string TaskNote;
        [Tooltip("What the islander adds when he talks to them while this fact is the first one missing. " +
                 "Empty = they say nothing about it.")]
        [TextArea] public string SpokenHint;
    }

    /// <summary>
    /// A story gate, as data (one asset per gate, under <c>Resources/StoryGates</c>): when the facts
    /// of a stretch of the story hold and the player talks to the named islander, that islander says
    /// the word and one saved flag is set. Whatever waits on that flag (a campaign barrier's
    /// <see cref="CampaignBarrierDef.LiftedByFlag"/>) answers the same step.
    ///
    /// <para>The gate decides nothing itself: the facts are read through Core's island-week seam, and
    /// the rule is <see cref="StoryGate"/>. Every word the player meets is on this asset or on the
    /// conversation it names.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "World/Story Gate", fileName = "StoryGate")]
    public sealed class StoryGateDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (story_gate.snake_case).")]
        public string Id = "story_gate.";
        [TextArea] public string Description = "";

        [Header("Who says the word")]
        [Tooltip("The islander whose ordinary Talk carries the gate (npc.snake_case).")]
        public string NpcId = "npc.";
        [Tooltip("The word itself: this conversation's first lines are played in the islander's bubble " +
                 "when every fact holds.")]
        public DialogueDef Word;

        [Header("What must hold")]
        [Tooltip("The progress rules this gate reads (island_week.snake_case). It must be the id of the " +
                 "shipped rules asset.")]
        public string ProgressId = "island_week.";

        [Header("When the gate is live")]
        [Tooltip("A saved flag the gate needs before it does anything. Empty = always live. While this flag " +
                 "is unset the islander talks as ever and no note is shown: a save that never had the flag " +
                 "written never meets the gate.")]
        public string LiveWhileFlag = "";

        [Header("What the word sets")]
        [Tooltip("The saved flag set when the word has been heard to its end. Once set the gate is done for " +
                 "good and the islander talks as ever.")]
        public string SetsFlag = "";

        [Header("Before the facts hold")]
        [Tooltip("One row per fact of the progress rules, by its text key.")]
        public StoryGateNote[] Notes = new StoryGateNote[0];

        /// <summary>The row for <paramref name="textKey"/>, if one is authored.</summary>
        public bool TryNote(string textKey, out StoryGateNote note)
        {
            if (Notes != null && !string.IsNullOrEmpty(textKey))
                for (int i = 0; i < Notes.Length; i++)
                    if (Notes[i].TextKey == textKey) { note = Notes[i]; return true; }
            note = default;
            return false;
        }

        /// <summary>Null when the asset can do its job, else what is wrong with it.</summary>
        public string ValidationError()
        {
            if (!HasPrefix(Id, "story_gate.")) return "Expected a story_gate.* id.";
            if (!HasPrefix(NpcId, "npc.")) return "Expected an npc.* id.";
            if (!HasPrefix(ProgressId, "island_week.")) return "Expected an island_week.* progress id.";
            if (string.IsNullOrWhiteSpace(SetsFlag)) return "A gate must set a flag.";
            if (SetsFlag == LiveWhileFlag) return "The flag the gate sets cannot be the flag it waits on.";
            if (Word == null || Word.FirstLines == null || Word.FirstLines.Length == 0)
                return "A gate needs a word to say.";
            for (int i = 0; i < Word.FirstLines.Length; i++)
                if (string.IsNullOrWhiteSpace(Word.FirstLines[i])) return "A line of the word is empty.";
            if (Notes != null)
                for (int i = 0; i < Notes.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(Notes[i].TextKey)) return "A note has no text key.";
                    if (string.IsNullOrWhiteSpace(Notes[i].TaskNote)) return "A note has no Tasks text.";
                    for (int j = 0; j < i; j++)
                        if (Notes[j].TextKey == Notes[i].TextKey) return "Two notes share a text key.";
                }
            return null;
        }

        private static bool HasPrefix(string id, string prefix)
            => !string.IsNullOrWhiteSpace(id) && id.StartsWith(prefix, StringComparison.Ordinal)
               && id.Length > prefix.Length;
    }
}
