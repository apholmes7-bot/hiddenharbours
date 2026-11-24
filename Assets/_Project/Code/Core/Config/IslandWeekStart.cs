using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The island week begins when a new game is started, and at no other time.</b> One act, called
    /// by the shell from its New Game path: it writes the week's key into the flag list the save
    /// already has, once. Nothing else begins the week, so a save that was under way before the week
    /// existed never gets it, however empty that save looks.
    ///
    /// <para>It asks nothing of the rest of the save. Whatever else a new game sets up, before or
    /// after this call, does not change what it does.</para>
    ///
    /// <para>The week is the household's. The host's save holds it and only the host writes it; a
    /// player joining a game under way neither begins a week nor begins one again.</para>
    /// </summary>
    public static class IslandWeekStart
    {
        /// <summary>The saved flag the island week stands on: the weather on the island's sea edges
        /// and the word at the end of the week both wait on it.</summary>
        public const string FirstWeekKey = "st_peters.first_week";

        /// <summary>
        /// Whether this machine holds the household's save. Unset, it does: there is one player and one
        /// save. A second player's machine answers false here and writes nothing.
        /// </summary>
        public static Func<bool> IsHost { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsHost = null;

        /// <summary>True when the save is in, or has been through, its island week.</summary>
        public static bool Begun(ISaveService save) => save != null && save.GetFlag(FirstWeekKey);

        /// <summary>
        /// Begin the island week for the game that was just started. True when the key was written by
        /// this call; false when there is no save, this machine is not the host, or the week had
        /// already begun.
        /// </summary>
        public static bool BeginForNewGame(ISaveService save)
        {
            if (save == null || save.Current == null) return false;
            if (IsHost != null && !IsHost()) return false;
            if (save.GetFlag(FirstWeekKey)) return false;
            save.SetFlag(FirstWeekKey, true);
            return true;
        }
    }
}
