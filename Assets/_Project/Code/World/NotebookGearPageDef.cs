using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The Gear page's authored shape</b>: which headings it has, which gear ids sit under each, in
    /// what order, and the name each is lettered with. One asset (ADR 0003), loaded from
    /// <c>Resources/NotebookPages</c> by <see cref="NotebookContentSource.GearPage"/>.
    ///
    /// <para><b>Names are copied from the owning Defs</b> (the shop offers, the tackle, trap and
    /// longline Defs, the watch) because the book may not name those modules' types (rule 4).
    /// <c>NotebookGearPageTests</c> pins every name to the Def that declares its id, so a rename there
    /// reds here rather than drifting.</para>
    ///
    /// <para><b>What the page lists is a flag, not code.</b> By default only what the player OWNS is
    /// shown; <see cref="ListUnowned"/> adds every entry here he does not own yet, lettered faint, without
    /// touching the ownership source.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Notebook Gear Page", fileName = "GearPage")]
    public sealed class NotebookGearPageDef : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("The gear's stable content id, exactly as the ownership records hold it (gear.rod, trap.lobster).")]
            public string Id;

            [Tooltip("The name lettered on the page: the owning Def's display name.")]
            public string Name;
        }

        [Serializable]
        public struct Group
        {
            [Tooltip("The heading lettered above this group (drawn in capitals).")]
            public string Heading;

            [Tooltip("The gear under this heading, in page order.")]
            public Entry[] Entries;
        }

        [Header("Identity")]
        [Tooltip("Stable id, append-only (notebook_page.snake_case).")]
        public string Id = "notebook_page.";

        [Header("Page")]
        [Tooltip("The headings, in page order.")]
        public Group[] Groups = Array.Empty<Group>();

        [Tooltip("The heading for owned gear no group lists (its name is made from its id).")]
        public string OtherHeading = "Other gear";

        [Tooltip("Off: only owned gear is listed. On: every entry above is listed, the unowned ones faint.")]
        public bool ListUnowned;
    }
}
