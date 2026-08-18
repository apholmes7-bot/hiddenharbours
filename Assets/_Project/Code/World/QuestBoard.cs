using System.Collections.Generic;

namespace HiddenHarbours.World
{
    /// <summary>
    /// What a quest looks like RIGHT NOW, derived from the flags — never stored.
    ///
    /// <para>A value type on purpose: it is a reading taken at a moment, not a record with a
    /// lifetime. Nothing may hold one and treat it as current later.</para>
    /// </summary>
    public readonly struct QuestReading
    {
        /// <summary>Every step's flag is set — the quest belongs on the Done tab.</summary>
        public readonly bool Archived;

        /// <summary>Index of the first step whose flag is still unset — the one the eye must find
        /// first. <c>-1</c> when there is no such step (an archived quest, or the degenerate
        /// no-steps case).</summary>
        public readonly int CurrentStepIndex;

        /// <summary>How many steps are done, and how many there are. The pencil "n more" and any
        /// progress read come from these rather than from a second walk of the list.</summary>
        public readonly int DoneCount, StepCount;

        public QuestReading(bool archived, int currentStepIndex, int doneCount, int stepCount)
        {
            Archived = archived;
            CurrentStepIndex = currentStepIndex;
            DoneCount = doneCount;
            StepCount = stepCount;
        }

        /// <summary>The complement of <see cref="Archived"/>, named so call sites read as the tabs do.</summary>
        public bool Active => !Archived;
    }

    /// <summary>
    /// <b>THE QUEST VIEW — flags in, active/current/archived out.</b> Pure, engine-light and
    /// allocation-free per quest, so it can be asked every time the book is drawn without a cache to
    /// go stale.
    ///
    /// <para><b>There is no state here and there must never be.</b> See <see cref="QuestDef"/> for
    /// the full argument; the short version is that a saved "step 2 done" that disagreed with the
    /// flag would be a bug with no correct resolution, so the disagreement is made unrepresentable.
    /// If a future lane is tempted to add a cache or a saved snapshot, that is the thing to push back
    /// on.</para>
    ///
    /// <para><b>Null-tolerant throughout</b>, the established posture for the optional Core seams: a
    /// null flag store reads as "nothing is done yet" rather than throwing, so an EditMode rig or a
    /// scene opened without the bootstrap still draws a sensible book.</para>
    /// </summary>
    public static class QuestBoard
    {
        /// <summary>
        /// Is this step done? An empty flag id is NOT done, and deliberately so: it is how a
        /// half-authored step behaves, and it keeps the quest visibly ACTIVE rather than quietly
        /// completing it. <c>QuestContentValidationTests</c> is what stops one shipping.
        /// </summary>
        public static bool IsDone(QuestStep step, IFlagStore flags)
        {
            if (step == null || string.IsNullOrEmpty(step.Flag) || flags == null) return false;
            return flags.Get(step.Flag);
        }

        /// <summary>
        /// Read a quest against the flags.
        ///
        /// <para><b>⚠ A quest with NO STEPS is ACTIVE, not archived</b> — even though "every step's
        /// flag is set" is vacuously true of an empty list. Archived is defined as
        /// <c>StepCount &gt; 0 &amp;&amp; all done</c> for one reason: a stepless quest is a content
        /// bug, and the two ways to be wrong are not equally wrong. Shown on the Done tab it reads as
        /// a thing the player finished and never did — an invisible lie. Shown on Active with nothing
        /// to do it reads as broken, which is what it is. Validation forbids it either way; this
        /// decides only how a broken asset fails.</para>
        /// </summary>
        public static QuestReading Read(QuestDef def, IFlagStore flags)
        {
            if (def?.Steps == null) return new QuestReading(false, -1, 0, 0);

            int steps = def.Steps.Count;
            int done = 0, current = -1;

            for (int i = 0; i < steps; i++)
            {
                if (IsDone(def.Steps[i], flags)) { done++; continue; }
                if (current < 0) current = i;   // the FIRST unset step, in authored order
            }

            bool archived = steps > 0 && done == steps;
            return new QuestReading(archived, current, done, steps);
        }

        /// <summary>True iff this quest belongs on the Active tab.</summary>
        public static bool IsActive(QuestDef def, IFlagStore flags) => !Read(def, flags).Archived;

        /// <summary>True iff this quest belongs on the Done tab.</summary>
        public static bool IsArchived(QuestDef def, IFlagStore flags) => Read(def, flags).Archived;

        /// <summary>
        /// Split a set of quests into the two tabs, preserving the order they were given in. Nulls in
        /// the input are skipped rather than throwing — a missing asset reference should cost one
        /// quest, not the whole book.
        /// </summary>
        public static void Sort(IEnumerable<QuestDef> quests, IFlagStore flags,
                                List<QuestDef> active, List<QuestDef> archived)
        {
            active?.Clear();
            archived?.Clear();
            if (quests == null) return;

            foreach (var q in quests)
            {
                if (q == null) continue;
                if (Read(q, flags).Archived) archived?.Add(q);
                else active?.Add(q);
            }
        }
    }
}
