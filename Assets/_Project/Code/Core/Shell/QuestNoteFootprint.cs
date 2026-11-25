using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Raised when the quest note's box on screen changes: shown, hidden, re-sized or
    /// moved. Never raised for a re-statement of the same box.</summary>
    public readonly struct QuestNoteFootprintChanged
    {
        /// <summary>The box now, in screen pixels. Zero-sized when the note is not drawn.</summary>
        public readonly Rect Current;

        /// <summary>The box before this change.</summary>
        public readonly Rect Previous;

        public QuestNoteFootprintChanged(Rect current, Rect previous)
        {
            Current = current;
            Previous = previous;
        }
    }

    /// <summary>
    /// <b>What the quest note (TASKS) covers on screen</b>, in screen pixels (bottom-left origin, y
    /// up) — the same shape of seam as <see cref="HelmFootprint"/> (ADR 0050), for the same reason.
    ///
    /// <para>The note is World's and the interaction prompt is UI's; both live in the lower right, and
    /// neither may reference the other (rule 4). So the note STATES its box here and the prompt reads
    /// it. The note's owner publishes on a change (a new line, a new screen size, a helm lift, a hide),
    /// never per frame; <see cref="Publish"/> drops a re-statement, so a quiet frame raises nothing
    /// and allocates nothing.</para>
    ///
    /// <para>A zero-sized rect is "no note". Nothing here is saved.</para>
    /// </summary>
    public static class QuestNoteFootprint
    {
        /// <summary>The note's box now. Zero-sized when no note is drawn.</summary>
        public static Rect Current { get; private set; }

        /// <summary>True when no note is drawn.</summary>
        public static bool IsNone => IsEmpty(Current);

        /// <summary>True for a rect that covers nothing — how "no note" is spelt.</summary>
        public static bool IsEmpty(Rect rectPx) => !(rectPx.width > 0f) || !(rectPx.height > 0f);

        /// <summary>State the note's box. A box that covers nothing is stored as the default rect, so
        /// every "no note" compares equal.</summary>
        public static void Publish(Rect rectPx)
        {
            if (IsEmpty(rectPx)) rectPx = default;
            if (rectPx == Current) return;
            Rect previous = Current;
            Current = rectPx;
            EventBus.Publish(new QuestNoteFootprintChanged(rectPx, previous));
        }

        /// <summary>The note is gone.</summary>
        public static void Clear() => Publish(default);

        /// <summary>Back to "no note" with no event — for domain reload and for tests.</summary>
        public static void Reset() => Current = default;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
