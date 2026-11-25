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
    /// <para><b>What ends it.</b> Nothing yet. Launching her, once she is mended, is the slice that will
    /// clear the flag and bring the sailing boat back at the slip's water.</para>
    /// </summary>
    public static class SlipDoryStory
    {
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
}
