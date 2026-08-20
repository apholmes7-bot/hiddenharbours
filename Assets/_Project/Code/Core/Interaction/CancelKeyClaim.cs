using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A tiny global claim on the <b>CANCEL key</b> (Esc, and East on a pad), raised by a modal that
    /// closes itself with it — so the press that puts a page away cannot ALSO open the pause menu
    /// underneath it.
    ///
    /// <para><b>Why it exists, in one sentence.</b> The modal and <c>ShellPresenter</c> read the same key
    /// in their own <c>Update</c>s, in an order Unity does not define, so on the frame Esc closes a page
    /// the shell may read a gate the page has ALREADY lowered — a coin toss that opens the pause menu on
    /// the way out. <c>WardrobePicker.OwnsCancelKey</c> settled that for the wardrobe by remembering the
    /// frame; this is the same answer for a modal the shell cannot see.</para>
    ///
    /// <para><b>Why it is in Core rather than beside the modal that raises it.</b> The wardrobe picker
    /// lives in UI, so the shell can simply ask it. The player's notebook is drawn by
    /// <c>NotebookPresenter</c> in <b>World</b>, and UI may not reference World (rule 4) — <c>ShellPresenter</c>
    /// naming it would not merely be impolite, it would not compile. So Core states WHAT ("the cancel key
    /// is spoken for this frame") and the two modules never meet.</para>
    ///
    /// <para><b>Why it is not <see cref="InteractionGate"/>.</b> That gate is a modal's hold on the
    /// INTERACT key, and the shell already declines while it is up — but only for a modal that raises it
    /// and lowers it on the SAME frame as the press, which is exactly the race above. This is the narrower
    /// question, and the only one with a frame in it: not "is a modal up" but "was this key already spent,
    /// this frame".</para>
    ///
    /// <para><b>Held for the duration, released with a latch.</b> <see cref="Hold"/> while the page is up;
    /// <see cref="Release"/> on the way out, which remembers the frame so
    /// <see cref="OwnedThisFrame"/> still answers "mine" for the rest of it. By the next frame the key is
    /// the shell's again — the latch is one frame wide and cannot wedge Esc off.</para>
    ///
    /// <para><b>One claimant today</b> (the notebook), stated the way <see cref="MoveActionClaim"/> states
    /// its own: a second modal wanting this should raise it too rather than growing a claimant list here,
    /// because the question the shell asks is about the KEY and not about who has it.</para>
    /// </summary>
    public static class CancelKeyClaim
    {
        private static bool _held;

        /// <summary>The frame the key was last let go on. <c>-1</c> is "never", and
        /// <see cref="Time.frameCount"/> is never negative, so no real frame can collide with it.</summary>
        private static int _releasedFrame = -1;

        /// <summary>True while a modal is up and holding the key. The duration half of the answer.</summary>
        public static bool IsHeld => _held;

        /// <summary>
        /// <b>The question the shell asks.</b> True while a modal holds the cancel key, AND for the rest
        /// of the frame one let it go — see the type remarks for why the closing frame counts.
        /// </summary>
        public static bool OwnedThisFrame => _held || Time.frameCount == _releasedFrame;

        /// <summary>Take the key (a modal opening).</summary>
        public static void Hold() => _held = true;

        /// <summary>Give it back, remembering this frame so the closing press is not spent twice.</summary>
        public static void Release()
        {
            _held = false;
            _releasedFrame = Time.frameCount;
        }

        /// <summary>Clear both halves (scene teardown / tests) — a torn-down modal must never leave Esc
        /// wedged off, and a stale latch must never survive into another test's frame.</summary>
        public static void Reset()
        {
            _held = false;
            _releasedFrame = -1;
        }
    }
}
