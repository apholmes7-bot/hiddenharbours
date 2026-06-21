using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One line in a conversation: who is speaking, their portrait (may be null — the panel then
    /// shows just a name + text), and the line itself. A plain serializable value so the
    /// presentation layer can render it and the pure <see cref="DialogueRunner"/> can sequence it
    /// without any MonoBehaviour. Copy is centralised in <see cref="WorldStrings"/> (the loc seam);
    /// portraits/speaker come from the <see cref="Interactable"/> being talked to.
    /// </summary>
    public readonly struct DialogueLine
    {
        public readonly string Speaker;
        public readonly Sprite Portrait;
        public readonly string Text;

        public DialogueLine(string speaker, Sprite portrait, string text)
        {
            Speaker = speaker;
            Portrait = portrait;
            Text = text;
        }
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
