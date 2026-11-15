using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Why a row in the chooser cannot be picked. Append-only.</summary>
    public enum InteractChoiceBlock
    {
        /// <summary>Pickable.</summary>
        None = 0,

        /// <summary>Somebody is using it (<see cref="IExclusiveInteractable.IsOccupied"/>): a bunk with
        /// someone in it.</summary>
        Occupied = 1,

        /// <summary>The thing said this one action is not on offer right now
        /// (<see cref="IMultiActionInteractable.ActionAvailable"/>).</summary>
        Unavailable = 2,
    }

    /// <summary>One row of the chooser: which thing, which of its actions, what it says, and whether it
    /// can be picked.</summary>
    public readonly struct InteractChoice
    {
        /// <summary>The thing the row acts on.</summary>
        public readonly IInteractable Candidate;

        /// <summary>The action index on an <see cref="IMultiActionInteractable"/>, or -1 for the plain
        /// <see cref="IInteractable.Interact"/>.</summary>
        public readonly int Action;

        /// <summary>The words on the row (the candidate's own <see cref="IInteractable.VerbLabel"/> or
        /// action label).</summary>
        public readonly string Label;

        /// <summary>Why the row cannot be picked, or <see cref="InteractChoiceBlock.None"/>.</summary>
        public readonly InteractChoiceBlock Block;

        public InteractChoice(IInteractable candidate, int action, string label, InteractChoiceBlock block)
        {
            Candidate = candidate;
            Action = action;
            Label = label;
            Block = block;
        }

        /// <summary>True when the row can be picked.</summary>
        public bool Enabled => Block == InteractChoiceBlock.None;
    }

    /// <summary>
    /// <b>The options in reach, as a list, and which one is selected</b> — the whole chooser rule as a
    /// POCO (owner ask 2026-11-15: "when a player is standing in a place with multiple options a choice
    /// will appear … a bunk occupied is unable to be selected at that time, or a nearby bunk will
    /// automatically be selected if the player is in the selectable area").
    ///
    /// <para><b>What it is built from.</b> The same four filters <see cref="InteractResolver"/> applies
    /// (<see cref="InteractResolver.Qualifies"/>), so a row is never offered that the single press would
    /// not have considered. Rows are ordered by the resolver's own ladder — priority, then nearness, then
    /// ordinal id — which is a total order, so the list is the same whatever order things registered in
    /// (rule 5).</para>
    ///
    /// <para><b>Which things become rows.</b> Only things at or below
    /// <see cref="GameConfig.InteractChooserMaxPriority"/> (the fixture rung by default): a bed, a door,
    /// a lid, a counter. A thing in your hands, at your feet, or a tool target keeps the ladder's answer
    /// outright, because what you are holding is what you meant (<see cref="InteractPriority"/>); a list
    /// every time you stood near a fixture with a pail in hand would be a tax on carrying.</para>
    ///
    /// <para><b>Exclusive things</b> (<see cref="IExclusiveInteractable"/>): one row per group. The row is
    /// the best FREE member in reach; only when every member in reach is occupied is the best occupied
    /// one shown, greyed and unpickable. The same swap applies to <see cref="Default"/>, so a single press
    /// standing between a taken bunk and a free one turns you in at the free one.</para>
    ///
    /// <para><b>Rows that read the same collapse</b> to the better-ranked one (an ATV's two mount sides
    /// both say the one verb): a list of two identical lines is not a choice.</para>
    ///
    /// <para><b>Allocation-free</b> (rule 7): fixed buffers, an insertion sort over at most
    /// <see cref="ScratchCapacity"/> qualifiers, walked by index. It is rebuilt every frame for the
    /// highlight, so it must cost nothing.</para>
    /// </summary>
    public sealed class InteractChoiceList
    {
        /// <summary>The most rows there can ever be: one per number key, 1 to 9. Structural, not feel —
        /// the feel cap is <see cref="GameConfig.InteractChooserMaxRows"/>, clamped under this.</summary>
        public const int HardCap = 9;

        /// <summary>How many qualifiers are considered before rows are made. More than any place in the
        /// game has in reach; the worst-ranked are dropped first, so an overflow loses the least likely
        /// rows rather than the best.</summary>
        public const int ScratchCapacity = 16;

        private readonly IInteractable[] _scratch = new IInteractable[ScratchCapacity];
        private readonly float[] _scratchSqr = new float[ScratchCapacity];
        private int _scratchCount;

        private readonly InteractChoice[] _rows = new InteractChoice[HardCap];

        /// <summary>How many rows there are.</summary>
        public int Count { get; private set; }

        /// <summary>The selected row, or -1 when no row can be picked.</summary>
        public int Selected { get; private set; } = -1;

        /// <summary>
        /// What a single press acts on: the resolver's winner, swapped for a free member of its group when
        /// it is an occupied exclusive thing. Null when nothing qualified. With the chooser off, or with
        /// fewer than two pickable rows, this is the whole answer.
        /// </summary>
        public IInteractable Default { get; private set; }

        /// <summary>How many rows can be picked.</summary>
        public int EnabledCount { get; private set; }

        /// <summary>True when there is something to choose between: two or more pickable rows. A greyed
        /// row on its own beside one pickable row is not a choice, and the press acts as it always did.</summary>
        public bool IsChoice => EnabledCount >= 2;

        /// <summary>Row <paramref name="index"/>. Out of range is the empty row.</summary>
        public InteractChoice this[int index]
            => index >= 0 && index < Count ? _rows[index] : default;

        /// <summary>Forget everything.</summary>
        public void Clear()
        {
            for (int i = 0; i < Count; i++) _rows[i] = default;
            for (int i = 0; i < _scratchCount; i++) _scratch[i] = null;
            Count = 0;
            EnabledCount = 0;
            _scratchCount = 0;
            Selected = -1;
            Default = null;
        }

        /// <summary>Take a copy of <paramref name="other"/> (the chooser freezes the list it opened on).</summary>
        public void CopyFrom(InteractChoiceList other)
        {
            Clear();
            if (other == null) return;
            for (int i = 0; i < other.Count; i++) _rows[i] = other._rows[i];
            Count = other.Count;
            EnabledCount = other.EnabledCount;
            Selected = other.Selected;
            Default = other.Default;
        }

        /// <summary>
        /// Build the rows for an actor. See the class note for the rules.
        /// </summary>
        /// <param name="candidates">The registrants (null is empty).</param>
        /// <param name="actor">Who is reaching.</param>
        /// <param name="facingArcDegrees">The arc, as for <see cref="InteractResolver.TryResolve"/>.</param>
        /// <param name="maxChoicePriority">The highest rung that becomes a row.</param>
        /// <param name="maxRows">The feel cap on rows, clamped to 1..<see cref="HardCap"/>.</param>
        public void Build(IReadOnlyList<IInteractable> candidates, in InteractActor actor, float facingArcDegrees,
                          int maxChoicePriority, int maxRows)
        {
            Clear();
            if (candidates == null || candidates.Count == 0) return;
            if (!InteractResolver.TryResolve(candidates, actor, facingArcDegrees, out IInteractable best)) return;

            float cosHalfArc = InteractArc.CosHalfArc(facingArcDegrees);
            Vector2 facing = actor.Facing;
            int rowCap = Mathf.Clamp(maxRows, 1, HardCap);

            Default = FreeMateFor(best, candidates, actor, facing, cosHalfArc);

            if (Default.Priority > maxChoicePriority)
            {
                // A held thing, a thing at your feet, a tool target: the ladder's answer stands. Only its
                // own actions (if it has several) can make a list.
                AddRowsFor(Default, rowCap);
                FinishSelection();
                return;
            }

            // Every qualifier on or below the rung, sorted by the resolver's ladder.
            for (int i = 0; i < candidates.Count; i++)
            {
                IInteractable c = candidates[i];
                if (c == null || c.Priority > maxChoicePriority) continue;
                if (!InteractResolver.Qualifies(c, actor, facing, cosHalfArc, out float sqr)) continue;
                InsertSorted(c, sqr);
            }

            for (int i = 0; i < _scratchCount && Count < rowCap; i++)
            {
                IInteractable c = _scratch[i];
                string group = GroupOf(c);
                if (group != null)
                {
                    if (GroupAlreadyListed(group)) continue;
                    c = BestOfGroupFrom(i, group);
                }
                AddRowsFor(c, rowCap);
            }

            FinishSelection();
        }

        /// <summary>
        /// Only <see cref="Default"/>, with no rows built: what the per-frame highlight needs. The
        /// resolver's winner, swapped for a free member of its group when it is an occupied exclusive
        /// thing. Allocation-free.
        /// </summary>
        public static bool TryResolveDefault(IReadOnlyList<IInteractable> candidates, in InteractActor actor,
                                             float facingArcDegrees, out IInteractable target)
        {
            target = null;
            if (!InteractResolver.TryResolve(candidates, actor, facingArcDegrees, out IInteractable best)) return false;
            target = FreeMateFor(best, candidates, actor, actor.Facing, InteractArc.CosHalfArc(facingArcDegrees));
            return true;
        }

        /// <summary>
        /// Move the selection one pickable row down (<paramref name="direction"/> &gt; 0) or up (&lt; 0),
        /// wrapping at both ends and skipping greyed rows. True when it moved.
        /// </summary>
        public bool Step(int direction)
        {
            if (direction == 0 || EnabledCount == 0) return false;
            int dir = direction > 0 ? 1 : -1;
            int start = Selected;
            int i = start < 0 ? (dir > 0 ? Count - 1 : 0) : start;
            for (int n = 0; n < Count; n++)
            {
                i = (i + dir + Count) % Count;
                if (!_rows[i].Enabled) continue;
                Selected = i;
                return i != start;
            }
            return false;
        }

        /// <summary>Select row <paramref name="index"/> (a number key, a hover). Refused — and the
        /// selection kept — for a greyed row or one out of range.</summary>
        public bool TrySelect(int index)
        {
            if (index < 0 || index >= Count || !_rows[index].Enabled) return false;
            Selected = index;
            return true;
        }

        /// <summary>The first pickable row, or -1.</summary>
        public int FirstEnabled()
        {
            for (int i = 0; i < Count; i++) if (_rows[i].Enabled) return i;
            return -1;
        }

        // ---- building --------------------------------------------------------------------------

        private void FinishSelection()
        {
            EnabledCount = 0;
            for (int i = 0; i < Count; i++) if (_rows[i].Enabled) EnabledCount++;

            // Start on the row a single press would have acted on, so opening the list and confirming at
            // once does exactly what the press always did.
            Selected = -1;
            for (int i = 0; i < Count; i++)
            {
                if (!ReferenceEquals(_rows[i].Candidate, Default) || !_rows[i].Enabled) continue;
                Selected = i;
                break;
            }
            if (Selected < 0) Selected = FirstEnabled();
        }

        private void AddRowsFor(IInteractable c, int rowCap)
        {
            if (c == null) return;
            if (c is IMultiActionInteractable multi && multi.ActionCount >= 2)
            {
                for (int a = 0; a < multi.ActionCount && Count < rowCap; a++)
                {
                    string label = multi.ActionLabel(a);
                    if (string.IsNullOrEmpty(label)) continue;
                    InteractChoiceBlock block = BlockOf(c);
                    if (block == InteractChoiceBlock.None && !multi.ActionAvailable(a))
                        block = InteractChoiceBlock.Unavailable;
                    _rows[Count++] = new InteractChoice(c, a, label, block);
                }
                return;
            }

            // A candidate with no words still acts on a single press, but it cannot be shown as a row.
            string verb = c.VerbLabel;
            if (string.IsNullOrEmpty(verb) || Count >= rowCap) return;
            // Two rows that read the same are not a choice the player can make (an ATV's two mount sides,
            // two lockers side by side): the better-ranked one — already listed, the scratch is sorted —
            // stands for both, as the single press would.
            if (HasEnabledRowSaying(verb)) return;
            _rows[Count++] = new InteractChoice(c, -1, verb, BlockOf(c));
        }

        private bool HasEnabledRowSaying(string label)
        {
            for (int r = 0; r < Count; r++)
                if (_rows[r].Enabled && string.Equals(_rows[r].Label, label, System.StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static InteractChoiceBlock BlockOf(IInteractable c)
            => c is IExclusiveInteractable ex && ex.IsOccupied ? InteractChoiceBlock.Occupied
                                                               : InteractChoiceBlock.None;

        private static string GroupOf(IInteractable c)
        {
            if (!(c is IExclusiveInteractable ex)) return null;
            string g = ex.ExclusiveGroup;
            return string.IsNullOrEmpty(g) ? null : g;
        }

        private bool GroupAlreadyListed(string group)
        {
            for (int r = 0; r < Count; r++)
                if (string.Equals(GroupOf(_rows[r].Candidate), group, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>From scratch position <paramref name="from"/> on (the group's best-ranked member),
        /// the first free member; else the member at <paramref name="from"/>, occupied.</summary>
        private IInteractable BestOfGroupFrom(int from, string group)
        {
            for (int j = from; j < _scratchCount; j++)
            {
                IInteractable c = _scratch[j];
                if (!string.Equals(GroupOf(c), group, System.StringComparison.Ordinal)) continue;
                if (!((IExclusiveInteractable)c).IsOccupied) return c;
            }
            return _scratch[from];
        }

        /// <summary>The occupied-bunk swap for the single press: <paramref name="best"/> unless it is an
        /// occupied member of a group with a free member in reach, in which case the best free one.</summary>
        private static IInteractable FreeMateFor(IInteractable best, IReadOnlyList<IInteractable> candidates,
                                                 in InteractActor actor, Vector2 facing, float cosHalfArc)
        {
            if (!(best is IExclusiveInteractable ex) || !ex.IsOccupied) return best;
            string group = GroupOf(best);
            if (group == null) return best;

            IInteractable mate = null;
            int matePriority = 0;
            float mateSqr = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                IInteractable c = candidates[i];
                if (c == null || ReferenceEquals(c, best)) continue;
                if (!string.Equals(GroupOf(c), group, System.StringComparison.Ordinal)) continue;
                if (((IExclusiveInteractable)c).IsOccupied) continue;
                if (!InteractResolver.Qualifies(c, actor, facing, cosHalfArc, out float sqr)) continue;
                if (mate == null || InteractResolver.Beats(c, c.Priority, sqr, mate, matePriority, mateSqr))
                {
                    mate = c;
                    matePriority = c.Priority;
                    mateSqr = sqr;
                }
            }
            return mate ?? best;
        }

        private void InsertSorted(IInteractable c, float sqr)
        {
            int at = _scratchCount;
            while (at > 0 && InteractResolver.Beats(c, c.Priority, sqr, _scratch[at - 1], _scratch[at - 1].Priority,
                                                    _scratchSqr[at - 1]))
                at--;
            if (at >= ScratchCapacity) return;   // worse than everything kept, and the buffer is full

            int last = Mathf.Min(_scratchCount, ScratchCapacity - 1);
            for (int k = last; k > at; k--)
            {
                _scratch[k] = _scratch[k - 1];
                _scratchSqr[k] = _scratchSqr[k - 1];
            }
            _scratch[at] = c;
            _scratchSqr[at] = sqr;
            if (_scratchCount < ScratchCapacity) _scratchCount++;
        }
    }
}
