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
    /// <b>A story gate: an islander's word, given when the facts hold.</b> One asset names who says it,
    /// which progress rules must hold, the hours it can be said in, the word itself and the saved flag
    /// the word sets. Nothing about a gate is in code: a new one is a new asset.
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
                 "when every fact holds and the hour is inside the window.")]
        public DialogueDef Word;
        [Tooltip("The word in a demo. Empty = the same word as the full game.")]
        public DialogueDef DemoWord;

        [Header("What must hold")]
        [Tooltip("The progress rules this gate reads (island_week.snake_case). It must be the id of the " +
                 "shipped rules asset.")]
        public string ProgressId = "island_week.";

        [Header("The hours the word can be said in")]
        [Tooltip("First hour of the window, 0..24 on the game clock.")]
        [Range(0f, 24f)] public float FromHour = 0f;
        [Tooltip("The hour the window closes, 0..24. Smaller than the first hour = the window runs past " +
                 "midnight. Equal to it = any hour.")]
        [Range(0f, 24f)] public float ToHour = 0f;
        [Tooltip("What the islander adds when every fact holds but the hour is outside the window. " +
                 "Empty = they say nothing about it.")]
        [TextArea] public string OutsideHoursLine = "";
        [Tooltip("The Tasks note once every fact holds and the word is still to be heard. Empty = no note.")]
        [TextArea] public string AllHoldNote = "";

        [Header("When the gate is live")]
        [Tooltip("A saved flag the gate needs before it does anything. Empty = always live. While this flag " +
                 "is unset the islander talks as ever and no note is shown: a save that never had the flag " +
                 "written never meets the gate.")]
        public string LiveWhileFlag = "";

        [Header("What the word sets")]
        [Tooltip("The saved flag set when the word has been heard to its end. Once set the word is never " +
                 "said again.")]
        public string SetsFlag = "";
        [Tooltip("A demo ends when this word is given. The full game ignores it.")]
        public bool EndsDemo;
        [Tooltip("What the islander adds to their ordinary talk in a demo once the word has been given. " +
                 "Empty = they say nothing more.")]
        [TextArea] public string DemoAfterLine = "";

        [Header("The morning after the word")]
        [Tooltip("The flag set by the next kept sleep that reaches morning after the word, in the full " +
                 "game only. Empty = no morning action.")]
        public string MorningSetsFlag = "";

        [Header("After the word, while passage is pending")]
        [Tooltip("The saved flag that ends the full game's reminder. Empty with no pending dialogue = " +
                 "ordinary talk after the word.")]
        public string PendingUntilFlag = "";
        [Tooltip("First lines added to the ordinary bubble after the word, until PendingUntilFlag is " +
                 "set. The demo always uses DemoAfterLine instead.")]
        public DialogueDef PendingDialogue;

        [Header("Before the facts hold")]
        [Tooltip("One row per fact of the progress rules, by its text key.")]
        public StoryGateNote[] Notes = new StoryGateNote[0];

        /// <summary>The row for <paramref name="textKey"/>, if the gate has one.</summary>
        public bool TryNote(string textKey, out StoryGateNote note)
        {
            if (Notes != null && !string.IsNullOrEmpty(textKey))
                for (int i = 0; i < Notes.Length; i++)
                    if (Notes[i].TextKey == textKey) { note = Notes[i]; return true; }
            note = default;
            return false;
        }

        /// <summary>True when <paramref name="hourOfDay"/> (0..24) is inside the window. The window is
        /// half-open, so an hour belongs to exactly one side of it.</summary>
        public bool InHours(float hourOfDay)
        {
            if (Mathf.Approximately(FromHour, ToHour)) return true;
            return FromHour < ToHour
                ? hourOfDay >= FromHour && hourOfDay < ToHour
                : hourOfDay >= FromHour || hourOfDay < ToHour;
        }

        /// <summary>The word for this configuration: the demo's own when it has one. Null = no word.</summary>
        public string[] WordLines(bool demo)
        {
            DialogueDef word = demo && HasLines(DemoWord) ? DemoWord : Word;
            return HasLines(word) ? word.FirstLines : null;
        }

        /// <summary>Null when the gate is whole; otherwise the first thing wrong with it.</summary>
        public string ValidationError()
        {
            if (!HasPrefix(Id, "story_gate.")) return "Expected a story_gate.* id.";
            if (!HasPrefix(NpcId, "npc.")) return "Expected an npc.* id.";
            if (!HasPrefix(ProgressId, "island_week.")) return "Expected an island_week.* progress id.";
            if (string.IsNullOrWhiteSpace(SetsFlag)) return "A gate must set a flag.";
            if (SetsFlag == LiveWhileFlag) return "The flag the gate sets cannot be the flag it waits on.";
            if (!string.IsNullOrWhiteSpace(MorningSetsFlag) &&
                (MorningSetsFlag == SetsFlag || MorningSetsFlag == LiveWhileFlag))
                return "The morning must set a different flag from the word or its activation.";
            if (FromHour < 0f || FromHour > 24f || ToHour < 0f || ToHour > 24f) return "An hour is 0..24.";
            if (!HasLines(Word)) return "A gate needs a word to say.";
            if (DemoWord != null && !HasLines(DemoWord)) return "The demo word has no lines.";
            bool hasPendingFlag = !string.IsNullOrWhiteSpace(PendingUntilFlag);
            if (hasPendingFlag != (PendingDialogue != null))
                return "A pending reminder needs both a dialogue and the flag that ends it.";
            if (hasPendingFlag && (PendingUntilFlag == SetsFlag || PendingUntilFlag == LiveWhileFlag))
                return "The pending reminder must end on a different flag from the word or its activation.";
            if (PendingDialogue != null && !HasLines(PendingDialogue))
                return "The pending reminder has no lines.";
            string bad = EmptyLine(Word) ?? (DemoWord != null ? EmptyLine(DemoWord) : null);
            bad ??= PendingDialogue != null ? EmptyLine(PendingDialogue) : null;
            if (bad != null) return bad;
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

        private static bool HasLines(DialogueDef word)
            => word != null && word.FirstLines != null && word.FirstLines.Length > 0;

        private static string EmptyLine(DialogueDef word)
        {
            for (int i = 0; i < word.FirstLines.Length; i++)
                if (string.IsNullOrWhiteSpace(word.FirstLines[i])) return "A line of the word is empty.";
            return null;
        }

        private static bool HasPrefix(string id, string prefix)
            => !string.IsNullOrWhiteSpace(id) && id.StartsWith(prefix, StringComparison.Ordinal)
               && id.Length > prefix.Length;
    }
}
