using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE WORDS THE WORLD'S BOARDS SHOW, BY ID.</b> A sign, a name board or a notice bakes blank and
    /// carries a string id (<c>words.snake_case</c>, e.g. <c>words.cannery_sign</c>); the text is here, in a
    /// table the owner can edit, so renaming a place changes a string, never a pixel. One asset per table
    /// (ADR 0003); ids are stable and append-only.
    ///
    /// <para>Plain data. Nothing letters a board in the world yet: this table is where the text waits for
    /// that path, and the key scenes' tests hold every board's id to an entry here.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Words Table", fileName = "WordsTable")]
    public class WordsTableDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (wordstable.snake_case).")]
        public string Id = "wordstable.example";

        [Header("The words")]
        [Tooltip("One entry per string id. Edit the text freely; never rename an id.")]
        public WordsEntry[] Entries = Array.Empty<WordsEntry>();

        /// <summary>The entry with this id, or false.</summary>
        public bool TryGet(string id, out WordsEntry entry)
        {
            if (Entries != null && !string.IsNullOrEmpty(id))
                foreach (WordsEntry e in Entries)
                    if (e != null && e.Id == id)
                    {
                        entry = e;
                        return true;
                    }
            entry = null;
            return false;
        }
    }

    /// <summary>One string: its id, the text a board shows, and the id of a longer body it opens onto.</summary>
    [Serializable]
    public sealed class WordsEntry
    {
        [Tooltip("Stable id, append-only (words.snake_case).")]
        public string Id = "";

        [Tooltip("What the board shows. The owner's to edit.")]
        public string Text = "";

        [Tooltip("The id of the longer text the board opens onto (a notice's body), or empty.")]
        public string BodyId = "";
    }
}
