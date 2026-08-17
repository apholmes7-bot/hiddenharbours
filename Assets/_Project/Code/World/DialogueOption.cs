using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One row the player can choose in a conversation</b> — the owner's 2026-07-30 direction:
    /// *"There will be options for the player to select for gameplay here or to ask a character
    /// questions"* (`design/dialogue-and-knowledge.md` §2). Authored as DATA on a
    /// <see cref="DialogueDef"/> (ADR 0003 / rule 2), so world-content adds a choice by typing a row in
    /// an inspector and nothing in C# grows a branch.
    ///
    /// <para><b>Flat, one round, on purpose (rule 8).</b> A row optionally says what the speaker answers
    /// (<see cref="ReplyLines"/>) and then the conversation ends. There is no nesting, no follow-up
    /// options and no condition — a dialogue TREE is the M2/M3 knowledge-graph work
    /// (`dialogue-and-knowledge.md` §4), and this is the shape it grows out of rather than something it
    /// has to replace.</para>
    ///
    /// <para><b>What a row DOES lives outside this module.</b> Picking one publishes
    /// <c>Core.DialogueOptionPicked</c> with <see cref="Id"/>; whichever system cares subscribes and acts
    /// (rule 4). The World module never branches on an option id — which is exactly why "can you front me
    /// the fee?" can be authored here without World ever naming the economy.</para>
    /// </summary>
    [System.Serializable]
    public struct DialogueOption
    {
        /// <summary>The reserved id of the always-appended close row. Authored options may not use it —
        /// <c>DialogueDefOptionsTests</c> holds that line — because the picker guarantees exactly one
        /// safe way out and two rows claiming it would make which one you got a coin toss.</summary>
        public const string CloseId = "option.close";

        [Tooltip("Stable, append-only id (option.snake_case, e.g. option.ask_about_grounds). What the " +
                 "Core DialogueOptionPicked signal carries — so whichever system acts on this choice " +
                 "matches on the id, never on the label. Never \"option.close\": that is reserved for " +
                 "the close row the picker appends itself.")]
        public string Id;

        [Tooltip("What the row SAYS, in the player's voice (e.g. \"Where's the cod running?\"). Plain " +
                 "copy for now — the same localization stand-in the rest of the world layer uses.")]
        public string Label;

        [Tooltip("What the speaker answers, if anything. Optional: an empty reply just closes the " +
                 "conversation, which is what a row that only fires a gameplay signal wants. One round " +
                 "only — a reply never leads to more options (that is the M2 knowledge-graph work).")]
        [TextArea] public string[] ReplyLines;

        /// <summary>True when this row is worth showing: it needs an id to report and a label to read.</summary>
        public bool IsAuthored => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(Label);

        /// <summary>The close row, built from the (localizable) copy in <see cref="WorldStrings"/>.</summary>
        public static DialogueOption Close => new DialogueOption
        {
            Id = CloseId,
            Label = WorldStrings.CloseConversationOption,
            ReplyLines = System.Array.Empty<string>(),
        };
    }

    /// <summary>
    /// <b>Which row the cursor is on, and how it got there.</b> The whole option-picker rule, pure: no
    /// input device, no canvas, no Unity lifecycle — so "the picker must never confirm an option the
    /// selection isn't on" is an EditMode assertion rather than a thing you hope about.
    ///
    /// <para><b>Driven by the MOVE AXIS, because the dev-key ledger is exhausted A–Z</b> (the 2026-08-17
    /// sweep). Selection is move-up/move-down and Interact confirms; nothing new is bound, and this class
    /// takes a plain float so it does not care whether that came from W/S, the arrows, or a stick.</para>
    ///
    /// <para><b>The axis is LATCHED, not sampled.</b> One row per push: the axis must fall back inside
    /// the dead zone before it can step again. Without that, a held key would rip through four rows in
    /// four frames. It also keeps the cost of choosing honest while the player is still standing in the
    /// world — a tap of W moves the cursor and nudges the fisher a centimetre, where a hold walks them
    /// out of conversation range, which is a legible way to leave.</para>
    /// </summary>
    public sealed class DialogueOptionPicker
    {
        /// <summary>How far the move axis must travel before it counts as a push, and how far back it must
        /// fall before the next one counts. Input plumbing, not owner feel — the same class of number as
        /// <c>VillagerRoutine.ShelterCheckSeconds</c>: big enough that stick drift cannot step a row,
        /// small enough that a deliberate tap always does.</summary>
        public const float AxisThreshold = 0.5f;

        private readonly int _count;
        private bool _latched;

        public DialogueOptionPicker(int optionCount)
        {
            _count = Mathf.Max(0, optionCount);
            Index = 0;
        }

        /// <summary>How many rows are on offer (the close row included — the picker is handed the final
        /// list, not the authored one).</summary>
        public int Count => _count;

        /// <summary>Which row the cursor is on. Always in range while <see cref="Count"/> &gt; 0.</summary>
        public int Index { get; private set; }

        /// <summary>True while the axis is still pushed from the last step — the next step waits for it to
        /// come back to neutral. Exposed because it is the half of the latch a test can see.</summary>
        public bool IsLatched => _latched;

        /// <summary>
        /// Feed this frame's move axis (+1 = up the list, -1 = down). Returns true when the cursor
        /// actually moved.
        ///
        /// <para>Wraps at both ends: with two to four rows, running off the bottom onto the top is the
        /// Animal Crossing feel and costs nobody a wrong pick, because confirming is a separate press.</para>
        /// </summary>
        public bool Step(float axis)
        {
            if (_count <= 1) { _latched = Mathf.Abs(axis) >= AxisThreshold; return false; }

            if (Mathf.Abs(axis) < AxisThreshold) { _latched = false; return false; }
            if (_latched) return false;

            _latched = true;
            // Up the list means TOWARD row 0 — the first row is drawn at the top of the bubble.
            Index = axis > 0f ? (Index - 1 + _count) % _count : (Index + 1) % _count;
            return true;
        }

        /// <summary>
        /// Confirm the row the cursor is on. Returns false (and <paramref name="index"/> = -1) when there
        /// is nothing to confirm, so a caller can never be handed a choice out of an empty picker.
        ///
        /// <para><b>It returns <see cref="Index"/> and nothing else</b> — it takes no argument and reads
        /// no input, so there is no path by which a confirm can land on a row the cursor was not on.
        /// That is the property the sabotage arm names, made structural rather than asserted.</para>
        /// </summary>
        public bool Confirm(out int index)
        {
            if (_count <= 0) { index = -1; return false; }
            index = Index;
            return true;
        }

        /// <summary>
        /// The rows to actually show for an authored list: every authored row, then the close row, always
        /// last. <b>The close row is appended here rather than authored</b> so no content can ship an
        /// option bubble with no safe way out — the Animal Crossing convention, made a guarantee.
        /// Returns null when there is nothing to choose between, which is the signal to skip the picker
        /// entirely and just end the conversation.
        /// </summary>
        public static List<DialogueOption> RowsFor(IReadOnlyList<DialogueOption> authored)
        {
            if (authored == null) return null;

            List<DialogueOption> rows = null;
            for (int i = 0; i < authored.Count; i++)
            {
                if (!authored[i].IsAuthored) continue;                 // half-typed row: skipped, not shown
                if (authored[i].Id == DialogueOption.CloseId) continue; // reserved — the appended one wins
                (rows ??= new List<DialogueOption>(authored.Count + 1)).Add(authored[i]);
            }

            if (rows == null) return null;      // nothing authored → no bubble, not a bubble with one row
            rows.Add(DialogueOption.Close);
            return rows;
        }
    }
}
