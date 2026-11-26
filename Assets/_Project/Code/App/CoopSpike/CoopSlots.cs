using System.Collections.Generic;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>Which starting place a participant takes. Keyed by connection, never by "host or guest".</summary>
    public static class CoopSlots
    {
        public const int None = -1;

        /// <summary>The lowest slot in [0, capacity) that no connected participant holds, or <see cref="None"/>.</summary>
        public static int LowestFree(IEnumerable<int> taken, int capacity)
        {
            if (capacity <= 0) return None;
            var used = new bool[capacity];
            if (taken != null)
                foreach (int slot in taken)
                    if (slot >= 0 && slot < capacity) used[slot] = true;
            for (int i = 0; i < capacity; i++)
                if (!used[i]) return i;
            return None;
        }

        /// <summary>Whether one more participant fits a session that already holds <paramref name="connected"/>.</summary>
        public static bool HasRoom(int connected, int capacity) => connected >= 0 && connected < capacity;
    }
}