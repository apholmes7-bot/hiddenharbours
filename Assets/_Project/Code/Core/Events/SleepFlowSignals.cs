namespace HiddenHarbours.Core
{
    /// <summary>A kept sleep crossed the configured morning. Published once after the save succeeds,
    /// by either sleep path; never by a load, an abandoned sleep or a save without sleep.</summary>
    public readonly struct SleepReachedMorning
    {
        /// <summary>The game-clock instant of the morning crossed, even if the sleeper woke later.</summary>
        public readonly double MorningSeconds;

        public SleepReachedMorning(double morningSeconds) => MorningSeconds = morningSeconds;
    }

    /// <summary>Where a sleep is (owner ruling 2026-11-15). See <see cref="SleepFlow"/>.</summary>
    public enum SleepPhase
    {
        /// <summary>No sleep in hand: the world runs as normal.</summary>
        Awake = 0,

        /// <summary>Lying down, choosing how long to sleep. The clock still runs at its normal pace.</summary>
        Choosing = 1,

        /// <summary>Asleep: time runs fast to the chosen wake instant, and a press wakes early.</summary>
        Asleep = 2,
    }

    /// <summary>
    /// Raised by <see cref="SleepFlow"/> each time the sleep changes: lying down, a press that changes the
    /// hours, falling asleep, waking. Everything a chooser or a bedside clock needs to draw itself, so a
    /// presenter in UI or Player reads this and never <see cref="SleepFlow"/>'s statics (rule 4).
    /// Published only on a change, never per frame.
    /// </summary>
    public readonly struct SleepFlowChanged
    {
        public readonly SleepPhase Phase;
        /// <summary>The chosen length in in-game hours (while choosing, the offer; asleep, the choice).</summary>
        public readonly float Hours;
        public readonly float MinHours;
        public readonly float MaxHours;
        /// <summary>The clock reading the sleep began at (asleep and at the wake), else 0.</summary>
        public readonly double StartSeconds;
        /// <summary>Where the chosen sleep wakes: while choosing, where it WOULD wake if begun now.</summary>
        public readonly double WakeSeconds;

        public SleepFlowChanged(SleepPhase phase, float hours, float minHours, float maxHours,
                                double startSeconds, double wakeSeconds)
        {
            Phase = phase;
            Hours = hours;
            MinHours = minHours;
            MaxHours = maxHours;
            StartSeconds = startSeconds;
            WakeSeconds = wakeSeconds;
        }
    }
}
