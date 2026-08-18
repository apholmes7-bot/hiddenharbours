using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>ONE HOW-IT-IS-DONE PAGE</b> — the printed voice of the almanac, as DATA. One asset per file
    /// under <c>Data/Knowledge</c>, keyed by a stable, append-only <see cref="Id"/>
    /// (<c>knowledge.snake_case</c>).
    ///
    /// <para>Knowledge lives in things and people, so the notebook is a thing she writes in and these
    /// are the pages that were already printed in it. The kit's word for the voice: sentence case,
    /// because "knowledge pages are the longest running text in the game and mixed case is what makes
    /// them readable" — CAPS is for tab chips and running heads, which are labels, not voice.</para>
    ///
    /// <para><b>Every authored page ships visible.</b> Whether knowledge should instead be EARNED — a
    /// page appearing when the player learns the thing — is a live question for the owner and
    /// explicitly not decided here. It is additive when it arrives: a gating flag on this asset, read
    /// the same way <see cref="QuestBoard"/> reads a step. Building the gate before the call is made
    /// would be the scope creep rule 8 names.</para>
    ///
    /// <para><b>The illustration slot reserves ROOM, not a picture.</b> The kit ships the frame, the
    /// corner ticks and the caption; what draws inside is a later content drop (knots, a fish plate,
    /// a rigged line). <see cref="SlotKey"/> names which one this page expects, so the art can land
    /// without touching the page; <see cref="SlotLines"/> is how many ruled lines it reserves, and
    /// the frame FLOWS with the text, so knowledge copy can never run through it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Knowledge Page", fileName = "KnowledgePage")]
    public class KnowledgePageDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (knowledge.snake_case, e.g. knowledge.reading_the_tide). " +
                 "Content-validated for uniqueness and shape.")]
        public string Id = "knowledge.example";

        [Tooltip("Which knowledge SUB-TAB this page files under (tab.snake_case, e.g. tab.water). " +
                 "N sub-tabs is DATA — the kit ships the chip and the label rect, never a label. " +
                 "The chip clips at 5 characters and 8 stubs fit the fore-edge.")]
        public string TabId = "tab.general";

        [Tooltip("The tab chip's LABEL. Clipped at 5 characters at scale 1, not shrunk — so write it " +
                 "short and in CAPS: it is a label, not voice. Pages sharing a TabId must agree on it.")]
        public string TabLabel = "GEN";

        [Header("The page")]
        [Tooltip("The section head, in ink with a ruled underline. Sentence case.")]
        public string Head = "";

        [Tooltip("The body, ONE ENTRY PER PARAGRAPH — not one per line. Wrapping is the presenter's " +
                 "job and depends on the zoom tier, so a hand-wrapped line would be wrong at every " +
                 "tier but one.")]
        public List<string> Body = new List<string>();

        [Header("Illustration slot (optional)")]
        [Tooltip("Which later illustration this page reserves room for (slot.snake_case, e.g. " +
                 "slot.tide_curve). Empty = no slot, and the page is pure text.")]
        public string SlotKey = "";

        [Tooltip("How many ruled lines the slot reserves. Ignored when SlotKey is empty. The kit's " +
                 "default is 5; below that the frame and its caption have nowhere to sit.")]
        public int SlotLines = 5;
    }
}
