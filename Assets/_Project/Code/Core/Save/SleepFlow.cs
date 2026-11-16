namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A sleep, from lying down to waking</b> (owner ruling 2026-11-15: "Player chooses sleep
    /// duration. Time moves fast with player/s able to cancel with a button. Maximum 8 hours sleep before
    /// auto waking." and "A sleep is a save.").
    ///
    /// <para><b>The steps.</b> A bed or bunk asks to rest (<see cref="RestSaveRequested"/>); with a chooser
    /// present, <see cref="RestSaveResponder"/> hands it here. <see cref="Begin"/> lies the player down and
    /// offers a length (until the morning, never past eight hours: <see cref="SleepPlan"/>);
    /// <see cref="Adjust"/> moves it a config step at a time; <see cref="Confirm"/> falls asleep, or
    /// <see cref="Decline"/> gets up without sleeping. Asleep, <see cref="Tick"/> runs the clock forward
    /// fast (the clock's own <see cref="IGameClock.FastForwardTo"/>, so every midnight crossed fires its
    /// day rollover exactly as a normal night would) until the chosen instant, and wakes on its own there.
    /// <see cref="Cancel"/> wakes early. Every wake relieves tiredness for the hours actually slept
    /// (<see cref="INeedsService.SleepSince"/>), stamps the place and writes the save, and says so with a
    /// <see cref="GameSaved"/>.</para>
    ///
    /// <para><b>Quit mid-sleep: the last completed sleep stands.</b> While asleep, the save service
    /// refuses every write (<see cref="InProgress"/>): an autosave on quit or on a flag would otherwise
    /// keep a night half slept, and the reload must restart the day where the player last WOKE. A quit to
    /// the title gets the player up without a write (<see cref="Abandon"/>).</para>
    ///
    /// <para><b>Static, like <see cref="RestSaveResponder"/>.</b> One sleep at a time, no frame of its
    /// own: the presenter that owns input calls <see cref="Tick"/> with the real seconds of its frame. An
    /// EditMode test drives the whole flow as calls. No allocation per frame: <see cref="Tick"/> publishes
    /// nothing until the wake.</para>
    ///
    /// <para><b>Deterministic.</b> Where a sleep wakes is a function of when it began and how long it was;
    /// the world at that instant is a function of the instant (rule 5). Fast time and normal time reaching
    /// the same clock reading leave the same world.</para>
    /// </summary>
    public static class SleepFlow
    {
        /// <summary>What the player is told when they slept the whole chosen length.</summary>
        public const string RestedText = "You wake rested. The day is kept.";

        /// <summary>What the player is told when they woke before the chosen length was up.</summary>
        public const string WokeEarlyText = "You wake early. The day is kept.";

        /// <summary>What the player is told when the bed is someone else's (one bed, one sleeper).</summary>
        public const string OccupiedText = "Someone is already sleeping there.";

        public static SleepPhase Phase { get; private set; }

        /// <summary>Lying down or asleep: the player's controls are parked (<see cref="ControlIntentGates"/>).</summary>
        public static bool HoldsTheWorld => Phase != SleepPhase.Awake;

        /// <summary>Asleep: time is running fast and no save may be written until the wake.</summary>
        public static bool InProgress => Phase == SleepPhase.Asleep;

        /// <summary>
        /// Is something present that lets the player choose (the chooser in Player)? Without one, a rest is
        /// the instant one-press night (<see cref="RestSaveResponder.Fulfil"/>), which is what every test rig
        /// and a scene with no chooser keep. <see cref="Reset"/> clears it.
        /// </summary>
        public static bool ChooserPresent { get; set; }

        /// <summary>The length on offer (choosing) or chosen (asleep), in in-game hours.</summary>
        public static float Hours { get; private set; }

        /// <summary>The clock reading the sleep began at (asleep).</summary>
        public static double StartSeconds { get; private set; }

        /// <summary>Where the sleep wakes on its own (asleep).</summary>
        public static double WakeSeconds { get; private set; }

        /// <summary>The bed or bunk that asked, kept for the wake's write.</summary>
        public static RestSaveRequested Request => _request;

        private static RestSaveRequested _request;

        /// <summary>
        /// Lie down in the bed <paramref name="request"/> names, and offer a length. True if a sleep is now
        /// in hand. A second request while one is in hand is ignored (false). A bed someone else is in is
        /// refused with a <see cref="GameSaved"/> saying so. With no living clock, or an hour that is held,
        /// there is no night to choose: the rest is the save-only beat it always was.
        /// </summary>
        public static bool Begin(in RestSaveRequested request)
        {
            if (Phase != SleepPhase.Awake) return false;

            IGameClock clock = LiveClock();
            if (clock == null || clock.IsHeld)
            {
                RestSaveResponder.Fulfil(request);
                return false;
            }

            if (!RestQuorum.TryClaim(request.BedId, RestQuorum.LocalSleeper))
            {
                EventBus.Publish(new GameSaved(false, OccupiedText));
                return false;
            }

            _request = request;
            Hours = SleepPlan.DefaultHours(clock.TotalSeconds, GameServices.Config);
            StartSeconds = 0d;
            WakeSeconds = 0d;
            Phase = SleepPhase.Choosing;
            Announce(clock);
            return true;
        }

        /// <summary>One press of the chooser: longer (positive) or shorter (negative) by a config step.</summary>
        public static void Adjust(int direction)
        {
            if (Phase != SleepPhase.Choosing || direction == 0) return;
            GameConfig config = GameServices.Config;
            float next = SleepPlan.Step(Hours, direction, SleepPlan.StepHours(config),
                                        SleepPlan.MinHours(config), SleepPlan.MaxHours(config));
            if (next == Hours) return;
            Hours = next;
            Announce(LiveClock());
        }

        /// <summary>
        /// Fall asleep for the hours on offer. The night moves only when the quorum is met
        /// (<see cref="RestQuorum"/>: one of one today, so always).
        /// </summary>
        public static void Confirm()
        {
            if (Phase != SleepPhase.Choosing) return;
            IGameClock clock = LiveClock();
            if (clock == null || clock.IsHeld)
            {
                // The clock went away or was held while the player chose: no night, the save-only beat.
                Release();
                RestSaveResponder.Fulfil(_request);
                return;
            }
            if (!RestQuorum.IsMet(1)) return;   // the others are not asleep yet (no multiplayer today)

            StartSeconds = clock.TotalSeconds;
            WakeSeconds = SleepPlan.WakeSeconds(StartSeconds, Hours, GameServices.Config);
            Phase = SleepPhase.Asleep;
            Announce(clock);
        }

        /// <summary>Get up without sleeping: no clock moves, nothing is saved.</summary>
        public static void Decline()
        {
            if (Phase != SleepPhase.Choosing) return;
            Release();
            Announce(LiveClock());
        }

        /// <summary>The button: while choosing, get up; while asleep, wake now.</summary>
        public static void Cancel()
        {
            if (Phase == SleepPhase.Choosing) Decline();
            else if (Phase == SleepPhase.Asleep) Wake();
        }

        /// <summary>
        /// Run the night forward by <paramref name="wallSeconds"/> of real time at the config's sleep pace,
        /// and wake on arrival. A paused clock (the pause menu) pauses the night with it. A clock that
        /// refuses to move (it was held, or went away) wakes the player where it stands.
        /// </summary>
        public static void Tick(float wallSeconds)
        {
            if (Phase != SleepPhase.Asleep) return;
            IGameClock clock = LiveClock();
            if (clock == null)
            {
                Wake();
                return;
            }
            if (clock.IsPaused) return;

            double now = clock.TotalSeconds;
            if (now >= WakeSeconds)
            {
                Wake();
                return;
            }

            GameConfig config = GameServices.Config;
            double next = SleepPlan.NextSeconds(now, WakeSeconds, wallSeconds,
                                                SleepPlan.GameHoursPerRealSecond(config),
                                                config != null ? config.SecondsPerDay : GameServices.SecondsPerDay);
            if (next > now && !clock.FastForwardTo(next))
            {
                Wake();
                return;
            }
            if (next >= WakeSeconds) Wake();
        }

        /// <summary>
        /// Wake now: relieve tiredness for the hours actually slept, stamp where the player slept, write
        /// the save and announce it. The phase goes back to awake FIRST, because the save service refuses
        /// writes while a sleep is in progress. Not asleep answers an <c>Ok:false</c> outcome and does
        /// nothing.
        /// </summary>
        public static GameSaved Wake()
        {
            if (Phase != SleepPhase.Asleep) return new GameSaved(false, string.Empty);
            Phase = SleepPhase.Awake;

            IGameClock clock = LiveClock();
            double now = RestSaveResponder.ClockSeconds(clock);
            bool slept = now > StartSeconds;
            if (slept) GameServices.Needs.SleepSince(StartSeconds);

            string text = now >= WakeSeconds ? RestedText : WokeEarlyText;
            GameSaved outcome = RestSaveResponder.Keep(_request, slept, text);

            RestQuorum.Release(_request.BedId, RestQuorum.LocalSleeper);
            Announce(clock);
            EventBus.Publish(outcome);
            return outcome;
        }

        /// <summary>Get up with no write and no relief (a quit to the title mid-sleep: the last completed
        /// sleep stands). Safe when awake.</summary>
        public static void Abandon()
        {
            if (Phase == SleepPhase.Awake) return;
            Release();
            Announce(LiveClock());
        }

        /// <summary>Back to no sleep and no chooser, publishing nothing (teardown, tests).</summary>
        public static void Reset()
        {
            Phase = SleepPhase.Awake;
            ChooserPresent = false;
            Hours = 0f;
            StartSeconds = 0d;
            WakeSeconds = 0d;
            _request = default;
            RestQuorum.Reset();
        }

        private static void Release()
        {
            RestQuorum.Release(_request.BedId, RestQuorum.LocalSleeper);
            Phase = SleepPhase.Awake;
        }

        private static void Announce(IGameClock clock)
        {
            GameConfig config = GameServices.Config;
            double wake = WakeSeconds;
            if (Phase == SleepPhase.Choosing && clock != null)
                wake = SleepPlan.WakeSeconds(clock.TotalSeconds, Hours, config);
            EventBus.Publish(new SleepFlowChanged(Phase, Hours, SleepPlan.MinHours(config),
                                                  SleepPlan.MaxHours(config), StartSeconds, wake));
        }

        /// <summary>The clock, or null for none or a destroyed one behind the interface.</summary>
        private static IGameClock LiveClock()
        {
            IGameClock clock = GameServices.Clock;
            if (clock == null) return null;
            if (clock is UnityEngine.Object unityObject && unityObject == null) return null;
            return clock;
        }
    }
}
