using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The storage containers the player keeps his catch in</b>: his boat's hold, the freezer at home.
    /// Not what he carries (that is <see cref="GameServices.PlayerHold"/> and his hands), and never somebody
    /// else's hold. A lane whose container is the player's registers it here, so a reader in another lane
    /// (the gift rows, which list what he has anywhere) can find it through Core without naming the class.
    ///
    /// <para><b>Order is registration order</b>, and it is the order a reader walks. Registering the same
    /// hold twice is a no-op, so a component that registers on every enable stays listed once.</para>
    ///
    /// <para><b>Lifetime: register on enable, unregister on destroy, never on disable</b> (the
    /// <see cref="GameServices.PlayerHold"/> rule): root toggling is how a region hop works, and a container
    /// that left the list on disable would vanish mid-crossing. A destroyed container left behind reads as
    /// null (<see cref="Get"/> launders Unity's fake-null), so a reader skips it either way.</para>
    ///
    /// <para>Allocation-free to read: a count and an index, no enumerator (rule 7). Cleared by
    /// <see cref="GameServices.Reset"/>.</para>
    /// FLAG lead-architect: new Core contract (the player's storage, read by the gift rows).
    /// </summary>
    public static class PlayerStorage
    {
        private static readonly List<IHold> _stores = new List<IHold>(4);

        /// <summary>How many containers are registered (a destroyed one still counts until it is
        /// unregistered; <see cref="Get"/> returns null for it).</summary>
        public static int Count => _stores.Count;

        /// <summary>Container <paramref name="index"/>, or null when out of range or destroyed.</summary>
        public static IHold Get(int index)
        {
            if (index < 0 || index >= _stores.Count) return null;
            IHold hold = _stores[index];
            return hold is UnityEngine.Object o && o == null ? null : hold;
        }

        /// <summary>List <paramref name="hold"/> as one of the player's storage containers. Null and a hold
        /// already listed are ignored.</summary>
        public static void Register(IHold hold)
        {
            if (hold == null || IndexOf(hold) >= 0) return;
            _stores.Add(hold);
        }

        /// <summary>Take <paramref name="hold"/> off the list. A hold not listed is ignored.</summary>
        public static void Unregister(IHold hold)
        {
            int i = IndexOf(hold);
            if (i >= 0) _stores.RemoveAt(i);
        }

        /// <summary>Forget every container (test isolation, <see cref="GameServices.Reset"/>).</summary>
        public static void Clear() => _stores.Clear();

        private static int IndexOf(IHold hold)
        {
            if (hold == null) return -1;
            for (int i = 0; i < _stores.Count; i++)
                if (ReferenceEquals(_stores[i], hold)) return i;
            return -1;
        }
    }
}
