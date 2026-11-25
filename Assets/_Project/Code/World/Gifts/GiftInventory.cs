using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>What the player could give, wherever he keeps it</b> (owner rule 2026-11-17): the giftable things
    /// he has in his hands, in what he carries, and in his storage containers, counted by item TYPE, and the
    /// act of taking exactly ONE of a type, from the nearest place first.
    ///
    /// <para><b>The sources, in the order they are walked</b> (the take order the owner gave: hands, then
    /// backpack, then storage):</para>
    /// <list type="number">
    ///   <item>His hands: the loose catch in a hand (<see cref="ICatchHands"/>), then a container held in a
    ///   hand, a pail he picked up (<see cref="CarriedItem.Held"/>, <see cref="HandLoad.Container"/>).</item>
    ///   <item>What he carries on his person: the belt pail (<see cref="GameServices.PlayerHold"/>). There is
    ///   no backpack in the game yet; when there is, it answers here.</item>
    ///   <item>His storage containers, in their registration order (<see cref="PlayerStorage"/>: the boat
    ///   hold, Ginny's freezer).</item>
    /// </list>
    /// <para>All through Core contracts; no other module's class is named. A container reached twice (the
    /// same pail in hand and on the list) is walked once.</para>
    ///
    /// <para><b>One type, one row.</b> A type is the item's stable id (the species id for a catch). Types are
    /// listed in the order first met on that walk, so the thing in his hand is the first Give row. At most
    /// <see cref="MaxKinds"/> types are kept (the chooser's own structure: nine rows, one of them Talk).</para>
    ///
    /// <para><b>Taking one from a container</b> uses the Core rebuild pattern (<see cref="IHold"/> has no
    /// partial remove: <c>SellService</c>, <see cref="CatchDisposal"/>). A container whose rebuild could
    /// not hold the rest (its capacity under what would remain) is skipped rather than risk losing catch.</para>
    ///
    /// <para><b>Rule 7.</b> One instance per interactor, reused; gathering walks lists by index into fixed
    /// buffers, so a press allocates nothing here. It is asked on a press, never per frame.</para>
    /// </summary>
    public sealed class GiftInventory
    {
        /// <summary>The most types listed: the chooser's nine rows, less Talk.</summary>
        public const int MaxKinds = InteractChoiceList.HardCap - 1;

        private const int NoSource = -1;
        private const int HandSource = -2;

        private ICatchHands _hands;
        private IGiftableFacts _facts;
        private readonly List<IHold> _holds = new List<IHold>(8);
        private readonly List<CatchItem> _keep = new List<CatchItem>(32);

        private readonly string[] _ids = new string[MaxKinds];
        private readonly string[] _names = new string[MaxKinds];
        private readonly int[] _counts = new int[MaxKinds];

        /// <summary>How many giftable types he has (0 = nothing to give, so no Give row).</summary>
        public int KindCount { get; private set; }

        /// <summary>Type <paramref name="kind"/>'s stable id, or null out of range.</summary>
        public string KindId(int kind) => kind >= 0 && kind < KindCount ? _ids[kind] : null;

        /// <summary>Type <paramref name="kind"/>'s display name, or null out of range.</summary>
        public string KindName(int kind) => kind >= 0 && kind < KindCount ? _names[kind] : null;

        /// <summary>How many of type <paramref name="kind"/> he has across every source (0 out of range).</summary>
        public int CountOf(int kind) => kind >= 0 && kind < KindCount ? _counts[kind] : 0;

        /// <summary>How many containers were gathered (for the tests).</summary>
        public int HoldCount => _holds.Count;

        /// <summary>
        /// Read what he has now. <paramref name="catchHands"/> is the loose catch in hand,
        /// <paramref name="carrier"/> the hands' read side (for a container held in a hand),
        /// <paramref name="carried"/> what he carries on his person; then every <see cref="PlayerStorage"/>
        /// container. Any of them may be null. Call it again before acting, so the act reads the present.
        /// </summary>
        public void Gather(ICatchHands catchHands, ICarrier carrier, IHold carried, IGiftableFacts facts)
        {
            _hands = Launder(catchHands);
            _facts = facts;
            _holds.Clear();
            AddHold(CarriedItem.Held(carrier, HandLoad.Container) as IHold);
            AddHold(carried);
            for (int i = 0; i < PlayerStorage.Count; i++) AddHold(PlayerStorage.Get(i));
            Tally();
        }

        /// <summary>Forget the gathered sources and types.</summary>
        public void Clear()
        {
            _hands = null;
            _facts = null;
            _holds.Clear();
            _keep.Clear();
            for (int i = 0; i < KindCount; i++) { _ids[i] = null; _names[i] = null; _counts[i] = 0; }
            KindCount = 0;
        }

        /// <summary>The one of type <paramref name="id"/> a take would give, without moving it: from the
        /// first source in the walk that has one. False when he has none left.</summary>
        public bool TryPeekOne(string id, out CatchItem item)
            => Find(id, out item, out _, out _);

        /// <summary>
        /// Take exactly ONE of type <paramref name="id"/>: from his hands first, then what he carries, then
        /// storage. False, with nothing moved, when he has none left anywhere it can be taken from.
        /// </summary>
        public bool TryTakeOne(string id, out CatchItem item)
        {
            if (!Find(id, out item, out int source, out int index)) return false;

            if (source == HandSource)
            {
                if (_hands == null || !_hands.TryTakeFromHand(out CatchItem taken)) return false;
                item = taken;
                Tally();
                return true;
            }

            IHold hold = _holds[source];
            // The surgical remove first: the one item goes and nothing else is touched (a persisted hold's
            // save row is not rewritten, no item is re-moded). Only a hold without it is rebuilt.
            if (hold is IHoldRemove surgical && surgical.TryRemoveAt(index))
            {
                Tally();
                return true;
            }
            IReadOnlyList<CatchItem> items = hold.Items;
            _keep.Clear();
            for (int i = 0; i < items.Count; i++)
                if (i != index) _keep.Add(items[i]);
            int kept = 0;
            hold.Clear();
            for (int i = 0; i < _keep.Count; i++)
                if (hold.TryAdd(_keep[i])) kept++;
            if (kept < _keep.Count)
                UnityEngine.Debug.LogWarning("[GiftInventory] A container took back " + kept + " of " + _keep.Count +
                                             " after a gift was taken from it.");
            _keep.Clear();
            Tally();
            return true;
        }

        private bool Find(string id, out CatchItem item, out int source, out int index)
        {
            item = default;
            source = NoSource;
            index = -1;
            if (string.IsNullOrEmpty(id)) return false;

            if (_hands != null && _hands.TryPeekHand(out CatchItem held) && Matches(in held, id))
            {
                item = held;
                source = HandSource;
                return true;
            }

            for (int h = 0; h < _holds.Count; h++)
            {
                IHold hold = Launder(_holds[h]);
                if (hold == null || !MayTakeFrom(hold)) continue;
                IReadOnlyList<CatchItem> items = hold.Items;
                if (items == null) continue;
                for (int i = 0; i < items.Count; i++)
                {
                    CatchItem c = items[i];
                    if (!Matches(in c, id)) continue;
                    item = c;
                    source = h;
                    index = i;
                    return true;
                }
            }
            return false;
        }

        // The rebuild re-adds everything but the one taken; a container that could not hold that many (a
        // belt pail whose gear was lost) would drop catch, so it is not taken from. A hold with the surgical
        // remove is never rebuilt, so it can always be taken from.
        private static bool MayTakeFrom(IHold hold)
            => hold is IHoldRemove || hold.CapacityUnits >= hold.UsedUnits - 1;

        private bool Matches(in CatchItem item, string id)
            => string.Equals(item.SpeciesId, id, StringComparison.Ordinal) && GiftItem.IsGiftable(in item, _facts);

        private void Tally()
        {
            for (int i = 0; i < KindCount; i++) { _ids[i] = null; _names[i] = null; _counts[i] = 0; }
            KindCount = 0;

            if (_hands != null && _hands.TryPeekHand(out CatchItem held)) Count(in held);

            for (int h = 0; h < _holds.Count; h++)
            {
                IHold hold = Launder(_holds[h]);
                if (hold == null || !MayTakeFrom(hold)) continue;
                IReadOnlyList<CatchItem> items = hold.Items;
                if (items == null) continue;
                for (int i = 0; i < items.Count; i++)
                {
                    CatchItem c = items[i];
                    Count(in c);
                }
            }
        }

        private void Count(in CatchItem item)
        {
            if (!GiftItem.IsGiftable(in item, _facts)) return;
            for (int k = 0; k < KindCount; k++)
            {
                if (!string.Equals(_ids[k], item.SpeciesId, StringComparison.Ordinal)) continue;
                _counts[k]++;
                return;
            }
            if (KindCount >= MaxKinds) return;   // more types than rows: the later ones wait
            _ids[KindCount] = item.SpeciesId;
            _names[KindCount] = item.DisplayName;
            _counts[KindCount] = 1;
            KindCount++;
        }

        private void AddHold(IHold hold)
        {
            hold = Launder(hold);
            if (hold == null) return;
            for (int i = 0; i < _holds.Count; i++)
                if (ReferenceEquals(_holds[i], hold)) return;
            _holds.Add(hold);
        }

        // Interface-typed references skip Unity's == overload; a destroyed component must read as gone.
        private static T Launder<T>(T thing) where T : class
            => thing is UnityEngine.Object o && o == null ? null : thing;
    }
}
