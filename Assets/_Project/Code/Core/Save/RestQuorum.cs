using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Who has to be asleep before the night moves, and one sleeper per bed</b> (owner ruling
    /// 2026-11-15, "player/s able to cancel"). Multiplayer is NOT built; this is the seam in front of the
    /// rest flow so that, when it is, the question "may time run fast now?" already has one place to be
    /// asked. Today there is one player, so the quorum is one of one and every rule here reduces to the
    /// single-player behaviour exactly.
    ///
    /// <para><b>One bed, one occupant.</b> A bed or bunk is claimed by its id when a sleeper lies down
    /// and released on waking or getting up. A second sleeper asking for a claimed bed is refused. A rest
    /// that names no bed (an old request, a test rig) claims nothing and is never refused.</para>
    /// </summary>
    public static class RestQuorum
    {
        /// <summary>The local player's sleeper number. The only sleeper there is today.</summary>
        public const int LocalSleeper = 0;

        /// <summary>How many players must be asleep for the night to move. One today: there is no
        /// multiplayer. A session layer would raise this; nothing else needs to change.</summary>
        public static int Players => 1;

        /// <summary>Is the quorum met with <paramref name="sleepers"/> of <paramref name="players"/>
        /// asleep? Everyone present must be asleep; a world with no players has no night to move.</summary>
        public static bool IsMet(int sleepers, int players) => players > 0 && sleepers >= players;

        /// <summary><see cref="IsMet(int, int)"/> against today's <see cref="Players"/>.</summary>
        public static bool IsMet(int sleepers) => IsMet(sleepers, Players);

        private static readonly Dictionary<string, int> Occupants = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Lie <paramref name="sleeper"/> down in <paramref name="bedId"/>. True if the bed was free or is
        /// already theirs; false if someone else is in it. An empty id claims nothing and answers true.
        /// </summary>
        public static bool TryClaim(string bedId, int sleeper)
        {
            if (string.IsNullOrEmpty(bedId)) return true;
            if (Occupants.TryGetValue(bedId, out int occupant)) return occupant == sleeper;
            Occupants[bedId] = sleeper;
            return true;
        }

        /// <summary>Get <paramref name="sleeper"/> up out of <paramref name="bedId"/>. Releasing a bed that
        /// is someone else's, or nobody's, changes nothing.</summary>
        public static void Release(string bedId, int sleeper)
        {
            if (string.IsNullOrEmpty(bedId)) return;
            if (Occupants.TryGetValue(bedId, out int occupant) && occupant == sleeper) Occupants.Remove(bedId);
        }

        /// <summary>Is <paramref name="bedId"/> taken?</summary>
        public static bool IsOccupied(string bedId) => !string.IsNullOrEmpty(bedId) && Occupants.ContainsKey(bedId);

        /// <summary>Everyone up (a teardown, a quit to the title, a test).</summary>
        public static void Reset() => Occupants.Clear();
    }
}
