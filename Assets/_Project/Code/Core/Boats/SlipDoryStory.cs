namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The new-game dory lies on the slip, not at the pier.</b> A game begun from the title page starts
    /// with no boat he can use: the only dory in St Peters is a broken one beached on the slip, and she is
    /// somebody else's until he buys her. That fact is one saved flag, <see cref="OnTheSlipFlag"/>, set by
    /// <see cref="BeginForNewGame"/> when the shell starts a new game.
    ///
    /// <para><b>Why a flag and not a save field.</b> The save already has a flag store
    /// (<see cref="ISaveService.GetFlag"/> / <see cref="ISaveService.SetFlag"/>), and a flag that is absent
    /// reads false. So an old game, saved before this existed, carries no flag and keeps playing exactly
    /// as it did: its own boat at the pier, no broken dory, nothing for sale. No new field, no format
    /// change.</para>
    ///
    /// <para><b>Who reads it.</b> The travel coordinator keeps the player's own sailing boat out of the
    /// world while the flag is on (<see cref="WithholdsPlayersBoat"/>), and the slip dory in World shows
    /// herself only while it is on. Both go through this Core type, so neither knows the other.</para>
    ///
    /// <para><b>What ends it.</b> Launching her (<see cref="TryLaunch"/>): once she is his and mended, the
    /// launch clears the flag, once, and says so on the bus (<see cref="SlipDoryLaunched"/>). From then on
    /// the save reads exactly like an old one that owns a mended dory: the slip prop is gone and his own
    /// boat is back in the world. Nothing new is saved: the flag, the owned list and the repaired list
    /// were already there.</para>
    ///
    /// <para><b>Only the host writes.</b> The save is the host's (<see cref="IsHost"/>); a guest's launch
    /// changes nothing and reports <see cref="SlipDoryLaunchResult.NotHost"/>.</para>
    /// </summary>
    public static class SlipDoryStory
    {
        /// <summary>Whether this machine owns the save. Null (the only state today, one player) means it
        /// does. A session with guests assigns this, the same shape as the demo week's host seam.</summary>
        public static System.Func<bool> IsHost { get; set; }

        /// <summary>True when this machine may write the save.</summary>
        public static bool HostHere => IsHost == null || IsHost();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHost() => IsHost = null;

        /// <summary>What a launch would do now, without doing it.</summary>
        public static SlipDoryLaunchResult PeekLaunch(ISaveService save, string boatId, bool host)
        {
            SaveData data = save?.Current;
            if (data == null || string.IsNullOrEmpty(boatId)) return SlipDoryLaunchResult.NoGame;
            if (!IsOnTheSlip(save)) return SlipDoryLaunchResult.NotOnTheSlip;
            if (data.OwnedBoats == null || !data.OwnedBoats.Contains(boatId)) return SlipDoryLaunchResult.NotHis;
            if (data.RepairedBoats == null || !data.RepairedBoats.Contains(boatId)) return SlipDoryLaunchResult.NotMended;
            if (!host) return SlipDoryLaunchResult.NotHost;
            return SlipDoryLaunchResult.Launched;
        }

        /// <summary>
        /// <b>Launch her.</b> Only when the dory is on the slip, his, and mended, and only on the host.
        /// Clears <see cref="OnTheSlipFlag"/>, names her his active hull when he has none, writes once and
        /// publishes <see cref="SlipDoryLaunched"/>. Every other result changes nothing. A second launch
        /// finds the flag gone and returns <see cref="SlipDoryLaunchResult.NotOnTheSlip"/>, so the flag is
        /// cleared exactly once and a loaded game never replays it.
        /// </summary>
        public static SlipDoryLaunchResult TryLaunch(ISaveService save, string boatId, bool host)
        {
            SlipDoryLaunchResult would = PeekLaunch(save, boatId, host);
            if (would != SlipDoryLaunchResult.Launched) return would;
            using (save.DeferWrites())
            {
                save.SetFlag(OnTheSlipFlag, false);
                SaveData data = save.Current;
                if (string.IsNullOrEmpty(data.ActiveHullId)) data.ActiveHullId = boatId;
                save.Save();
            }
            EventBus.Publish(new SlipDoryLaunched(boatId));
            return SlipDoryLaunchResult.Launched;
        }

        /// <summary>The saved flag: true while the new game's dory lies broken on the St Peters slip.
        /// Stable, append-only: a save names it.</summary>
        public const string OnTheSlipFlag = "st_peters.dory_on_slip";

        /// <summary>True while the dory lies on the slip in this save. False with no save service, which
        /// is every context that predates the story (tests, greybox boots, old saves).</summary>
        public static bool IsOnTheSlip(ISaveService save) => save != null && save.GetFlag(OnTheSlipFlag);

        /// <summary>A new game starts with her on the slip. The shell calls this right after the fresh
        /// save is minted, and only for a new game; a Continue never comes here.</summary>
        public static void BeginForNewGame(ISaveService save)
        {
            if (save == null) return;
            save.SetFlag(OnTheSlipFlag, true);
        }

        /// <summary>True while the player's own sailing boat must stay out of the world: there is no usable
        /// boat in this game yet, only the broken one on the slip.</summary>
        public static bool WithholdsPlayersBoat(ISaveService save) => IsOnTheSlip(save);
    }

    /// <summary>How a launch of the slip dory came out. Append-only.</summary>
    public enum SlipDoryLaunchResult
    {
        /// <summary>No game loaded, or no boat named: nothing happened.</summary>
        NoGame = 0,
        /// <summary>She is not on the slip in this game (an old save, or already launched).</summary>
        NotOnTheSlip = 1,
        /// <summary>She is not his yet.</summary>
        NotHis = 2,
        /// <summary>She is his but not mended.</summary>
        NotMended = 3,
        /// <summary>This machine does not own the save; the host launches.</summary>
        NotHost = 4,
        /// <summary>The flag is cleared and she is his boat on the water.</summary>
        Launched = 5,
    }

    /// <summary>Raised once, when the mended slip dory is launched. The travel rig shows his boat again
    /// and the slip prop hides itself; nothing re-raises it on a load (the flag is simply gone).</summary>
    public readonly struct SlipDoryLaunched
    {
        /// <summary>The stable hull id of the boat launched (<c>boat.dory</c>).</summary>
        public readonly string BoatId;
        public SlipDoryLaunched(string boatId) { BoatId = boatId; }
    }
}
