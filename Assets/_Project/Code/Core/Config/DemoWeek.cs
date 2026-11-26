using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The demo ends at an islander's word.</b> The one listener to <see cref="StoryWordGiven"/> that
    /// ends anything, and only in the demo configuration (<see cref="DemoBuild.Enabled"/>): it records
    /// the ending in the save's existing flag list, which writes the demo's own save file, then raises
    /// <see cref="DemoEnded"/> for whoever shows the closing card. In the full game it does nothing at
    /// all: the word is just a word.
    ///
    /// <para><b>The ending is the household's.</b> The word is heard by whoever talked to her; the
    /// ending is raised for everyone. Only the host owns the save, so only the host writes
    /// (<see cref="IsHost"/>); a guest raises the same signal and writes nothing. Until there is more
    /// than one player the host is always here.</para>
    ///
    /// <para>It owns no screen and moves nobody. Nothing here runs per frame.</para>
    /// </summary>
    public static class DemoWeek
    {
        /// <summary>The saved flag that says this demo game has ended. A key in the existing flag list;
        /// no saved field and no format change. The full game never writes or reads it.</summary>
        public const string EndedKey = "demo.island_week_ended";

        /// <summary>Whether this machine owns the save. Null (the only state today) means it does. A
        /// session with guests assigns this; a guest then never writes the ending.</summary>
        public static Func<bool> IsHost { get; set; }

        private static bool HostHere => IsHost == null || IsHost();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            IsHost = null;
            Listen();
        }

        /// <summary>Listen for the word. Safe to call again: it never listens twice, and a call after
        /// the bus was cleared puts the listener back.</summary>
        public static void Listen()
        {
            EventBus.Unsubscribe<StoryWordGiven>(OnWordSignal);
            EventBus.Subscribe<StoryWordGiven>(OnWordSignal);
        }

        /// <summary>True when this is the demo and its loaded game has ended. Read from the save, so it
        /// is still true after the game is closed and opened again.</summary>
        public static bool Ended
        {
            get
            {
                if (!DemoBuild.Enabled) return false;
                ISaveService save = GameServices.Save;
                return save != null && save.GetFlag(EndedKey);
            }
        }

        private static void OnWordSignal(StoryWordGiven word) => OnWord(in word);

        /// <summary>
        /// Decide whether this word ends the demo, and end it. True when it did. Never twice for one
        /// game: once the ending is in the save, a second word (or the same one raised again) ends
        /// nothing.
        /// </summary>
        public static bool OnWord(in StoryWordGiven word)
        {
            if (!DemoBuild.Enabled || !word.EndsDemo) return false;

            if (HostHere)
            {
                ISaveService save = GameServices.Save;
                if (save == null || save.Current == null || save.GetFlag(EndedKey)) return false;
                save.SetFlag(EndedKey, true);   // the flag write is the save: the demo's own file
            }

            EventBus.Publish(new DemoEnded());
            return true;
        }
    }
}
