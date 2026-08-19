using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>WHERE THE OPEN BOOK'S CONTENTS COME FROM in a running game</b> — the four sources
    /// <see cref="NotebookPresenter.WireContent"/> is otherwise handed, resolved for a book that installs
    /// itself and therefore has nobody to hand them to it.
    ///
    /// <para><b>Nothing here is a store.</b> Quests and knowledge are shipped Defs; whether a step is done
    /// is read from the flags the world already keeps (<see cref="QuestBoard"/>), so no quest state is
    /// saved anywhere and a quest authored later is retroactively correct on an old save. Her own notes
    /// are the single exception, and only because nothing else could reconstruct them.</para>
    ///
    /// <para><b>Resolved on every open, never cached.</b> The presenter holds these as SUPPLIERS for that
    /// reason: a step completed while the book was shut is simply true the next time she looks, with no
    /// cache to invalidate and no subscription to leak. The cost is a <c>Resources.LoadAll</c> per open of
    /// a handful of small text assets, which is a page the player is about to read rather than anything
    /// on the frame budget (rule 7).</para>
    ///
    /// <para><b>Sorted by id, always.</b> <c>Resources.LoadAll</c> makes no ordering promise, and a book
    /// whose pages shuffled between two opens of the same save would be a bug nobody could reproduce.
    /// Ordinal by <c>Id</c> is arbitrary but it is FIXED, which is the property that matters.</para>
    /// </summary>
    public static class NotebookContentSource
    {
        /// <summary>
        /// The Resources folders the shipped Defs are loaded from — the paths under
        /// <c>Assets/_Project/Data/Resources/</c>.
        ///
        /// <para>They are under <c>Data/</c> still, so <c>QuestContentValidationTests</c>'s sweep of the
        /// data root reaches them exactly as before; they are under a <c>Resources</c> root as well
        /// because the book installs itself and no builder exists to bake references into a scene. Both
        /// facts have to be true at once, and this folder is where they are both true.</para>
        /// </summary>
        public const string QuestsFolder = "Quests";

        /// <inheritdoc cref="QuestsFolder"/>
        public const string KnowledgeFolder = "Knowledge";

        /// <summary>Every shipped quest, in a fixed order. Empty is a real answer — a project with no
        /// quests authored yet opens the book on two empty tabs, not on an exception.</summary>
        public static IReadOnlyList<QuestDef> Quests() => Sorted(Resources.LoadAll<QuestDef>(QuestsFolder),
                                                                 q => q != null ? q.Id : "");

        /// <summary>Every shipped knowledge page, in a fixed order.</summary>
        public static IReadOnlyList<KnowledgePageDef> Knowledge() =>
            Sorted(Resources.LoadAll<KnowledgePageDef>(KnowledgeFolder), k => k != null ? k.Id : "");

        /// <summary>
        /// Her own lines, from the save, in HER order.
        ///
        /// <para><b>Order is the save's, and is not sorted</b> — unlike the Defs above. Position in
        /// <c>SaveData.PlayerNotes</c> IS the position on her page (the DTO carries no id precisely
        /// because of that), so re-ordering them here would rearrange the player's own list.</para>
        ///
        /// <para><b>Nothing is normalised on the way through</b>: no trim, no truncation, no collapsing
        /// of whitespace. <c>PlayerNoteDto</c> is explicit that the only correct transformation of her
        /// prose is none, and this is a read path — the one place a "tidy" would be easiest to slip in
        /// and hardest to notice.</para>
        ///
        /// <para>No save service (an EditMode rig, a pre-boot scene) is an empty list, which is what a
        /// notebook nobody has written in holds.</para>
        /// </summary>
        public static IReadOnlyList<NotebookNote> Notes()
        {
            var notes = new List<NotebookNote>();

            ISaveService save = GameServices.Save;
            List<PlayerNoteDto> written = save?.Current?.PlayerNotes;
            if (written == null) return notes;

            for (int i = 0; i < written.Count; i++)
            {
                PlayerNoteDto dto = written[i];
                notes.Add(new NotebookNote(dto.Text, dto.IsTask, dto.Done));
            }
            return notes;
        }

        /// <summary>The flags a quest step is read against — the save-backed store, the same one
        /// <c>WorldInteractor</c> and <c>OnboardingDirector</c> use, so the book and the world cannot
        /// disagree about what has been done.</summary>
        public static IFlagStore Flags() => new SaveFlagStore();

        /// <summary>
        /// One sorted, null-free list. <c>Resources.LoadAll</c> can hand back a null slot for an asset
        /// whose script failed to compile, and one of those reaching the layout would be a
        /// <c>NullReferenceException</c> in the middle of drawing a page.
        /// </summary>
        private static List<T> Sorted<T>(T[] loaded, Func<T, string> key) where T : UnityEngine.Object
        {
            var list = new List<T>();
            if (loaded == null) return list;

            for (int i = 0; i < loaded.Length; i++)
                if (loaded[i] != null) list.Add(loaded[i]);

            list.Sort((a, b) => string.CompareOrdinal(key(a), key(b)));
            return list;
        }
    }
}
