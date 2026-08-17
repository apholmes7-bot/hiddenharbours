using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One line in a conversation: who is speaking and what they say. A plain value so the presentation
    /// layer can render it and the pure <see cref="DialogueRunner"/> can sequence it without any
    /// MonoBehaviour. Copy is centralised in <see cref="WorldStrings"/> (the loc seam); the speaker comes
    /// from the <see cref="Interactable"/> being talked to.
    ///
    /// <para><b>⛔ There is no portrait here, and there will not be one</b> (owner ruling 2026-07-30,
    /// `design/dialogue-and-knowledge.md` §2): *"i will stay away from that design and follow an
    /// interaction behaviour more like animal crossing"*. The character on screen IS the portrait — the
    /// bubble is anchored at them and they turn to face you — so a second, smaller drawing of the same
    /// person in a box was cancelled along with the five #354 portrait asks. The slot came out with the
    /// panel it belonged to.</para>
    /// </summary>
    public readonly struct DialogueLine
    {
        public readonly string Speaker;
        public readonly string Text;

        public DialogueLine(string speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }
    }

    /// <summary>
    /// <b>Everything one conversation needs to be shown</b>, in one value — the lines, WHO is saying them
    /// and where they are standing, how fast their bubble fills, and what the player may choose at the
    /// end.
    ///
    /// <para>A struct rather than seven arguments because the presenter's <c>Play</c> is the seam
    /// <see cref="WorldInteractor"/> drives and the seam a test drives, and a seam with seven positional
    /// arguments is one that gets called wrongly. Everything on it is optional except the lines: no
    /// anchor puts the bubble at the screen's speaking position, no voice fills at
    /// <see cref="DialogueVoice.Default"/>, no options ends the conversation on the last line — which is
    /// exactly the behaviour every caller had before any of this existed.</para>
    /// </summary>
    public readonly struct DialogueRequest
    {
        /// <summary>What is said, in order.</summary>
        public readonly IReadOnlyList<DialogueLine> Lines;

        /// <summary>The speaker, in the world — the bubble hangs over them and the tail points at them.
        /// Null is legal (a conversation with no body on screen) and parks the bubble at a fixed
        /// screen position instead.</summary>
        public readonly Transform Anchor;

        /// <summary>How far above <see cref="Anchor"/> the tail points, in world metres — head height for
        /// a character whose transform is at their feet, zero when the caller wired an anchor exactly
        /// where it belongs. Kept beside the transform rather than folded into it so a speaker needs no
        /// child object to have a bubble at the right height.</summary>
        public readonly Vector3 AnchorOffset;

        /// <summary>How fast this speaker's bubble fills, and how it ticks.</summary>
        public readonly DialogueVoice Voice;

        /// <summary>The rows the player may choose once the lines have played, ALREADY final — the close
        /// row appended (see <see cref="DialogueOptionPicker.RowsFor"/>). Null = no picker.</summary>
        public readonly IReadOnlyList<DialogueOption> Options;

        /// <summary>The <c>DialogueDef.Id</c> these lines came from, for the pick signal. May be null.</summary>
        public readonly string DialogueId;

        /// <summary>The speaker's <c>NpcDef.Id</c>, for the tick and pick signals. May be null.</summary>
        public readonly string SpeakerId;

        public DialogueRequest(IReadOnlyList<DialogueLine> lines, Transform anchor, Vector3 anchorOffset,
                               in DialogueVoice voice, IReadOnlyList<DialogueOption> options = null,
                               string dialogueId = null, string speakerId = null)
        {
            Lines = lines;
            Anchor = anchor;
            AnchorOffset = anchorOffset;
            Voice = voice;
            Options = options;
            DialogueId = dialogueId;
            SpeakerId = speakerId;
        }

        /// <summary>Just some lines, spoken by nobody in particular — the legacy <c>Play(lines)</c> shape.</summary>
        public static DialogueRequest Plain(IReadOnlyList<DialogueLine> lines)
            => new DialogueRequest(lines, null, Vector3.zero, DialogueVoice.Default);
    }

    /// <summary>
    /// The pure advance/close state machine for a single conversation — no Unity lifecycle, so it is
    /// fully unit-testable (EditMode). The <see cref="DialoguePresenter"/> owns one of these and
    /// drives it from the Interact key; this class only tracks "which line are we on, and are we
    /// still open." Keeping the logic engine-light + testable follows CLAUDE.md §5.
    ///
    /// Semantics:
    ///   • <see cref="Open"/> starts at line 0 (a no-op if there are no lines — it stays closed).
    ///   • <see cref="Advance"/> steps to the next line; on the last line it closes and returns false.
    ///   • <see cref="Close"/> ends the conversation early.
    /// </summary>
    public sealed class DialogueRunner
    {
        private readonly IReadOnlyList<DialogueLine> _lines;

        public DialogueRunner(IReadOnlyList<DialogueLine> lines)
        {
            _lines = lines ?? System.Array.Empty<DialogueLine>();
        }

        /// <summary>True while a line is being shown (between Open and the close past the last line).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Index of the line currently shown (0-based); -1 when closed.</summary>
        public int Index { get; private set; } = -1;

        public int Count => _lines.Count;

        /// <summary>True if Advance would move to another line rather than close.</summary>
        public bool HasNext => IsOpen && Index < _lines.Count - 1;

        /// <summary>The line currently shown. Valid only while <see cref="IsOpen"/>.</summary>
        public DialogueLine Current => _lines[Index];

        /// <summary>Begin the conversation at the first line. No lines → stays closed (no-op).</summary>
        public void Open()
        {
            if (_lines.Count == 0) { IsOpen = false; Index = -1; return; }
            IsOpen = true;
            Index = 0;
        }

        /// <summary>
        /// Step to the next line. Returns true if a new line is now shown, or false if that was the
        /// last line (and the conversation has now closed). A no-op returning false if already closed.
        /// </summary>
        public bool Advance()
        {
            if (!IsOpen) return false;
            if (Index < _lines.Count - 1)
            {
                Index++;
                return true;
            }
            Close();
            return false;
        }

        /// <summary>End the conversation now (e.g. the player walked off / cancelled).</summary>
        public void Close()
        {
            IsOpen = false;
            Index = -1;
        }
    }
}
