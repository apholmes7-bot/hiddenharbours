namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Reading a day off the clock</b> — which of a table's blocks is running, how long it has been
    /// running, and how far a body moving at a speed has got through it. Pure, total, allocation-free.
    ///
    /// <para><b>Why it is here.</b> <c>World.RoutineSchedule</c> wrote this for villagers; the road
    /// fleet's scheduled trips need the same four functions and live in a module that may not name World
    /// (rule 4). The wrap-past-midnight rule in <see cref="BlockIndexAt"/> is subtle enough that a second
    /// transcription of it would be a defect waiting for a night shift, so it moved down to Core and
    /// <c>RoutineSchedule</c> delegates. The jitter stayed in World: a departure jitter is a villager's
    /// PERSONALITY, not a property of a day, and a truck's timetable does not have one.</para>
    ///
    /// <para><b>A block carries a DEPARTURE and no end.</b> One block's end IS the next one's start, so a
    /// gap or an overlap is inexpressible — the routine engine's law, kept, because a timetable that can
    /// express a hole is a timetable that will eventually have one.</para>
    /// </summary>
    public static class DaySchedule
    {
        /// <summary>Game hours in a day. Not a tunable: it is what "hour of day" means.</summary>
        public const float HoursPerDay = 24f;

        /// <summary>An hour value folded into [0, 24). Negative-safe.</summary>
        public static float Wrap24(float hour)
        {
            float h = hour % HoursPerDay;
            return h < 0f ? h + HoursPerDay : h;
        }

        /// <summary>
        /// Which block is running at <paramref name="hourOfDay"/>, given the DEPARTURE hours: the block
        /// whose departure is the most recent one at or before now — which for an hour before the first
        /// departure of the day is the LAST block, still running through from yesterday. That wrap is the
        /// whole reason this is a function and not an array index.
        ///
        /// <para>Total: any hour, any table with at least one entry, no allocation. Departure hours need
        /// not be sorted — the most recent one is found by measuring backwards from now.</para>
        /// </summary>
        public static int BlockIndexAt(float hourOfDay, float[] departureHours)
        {
            if (departureHours == null || departureHours.Length == 0) return -1;
            float h = Wrap24(hourOfDay);

            int best = 0;
            float bestElapsed = float.MaxValue;
            for (int i = 0; i < departureHours.Length; i++)
            {
                float elapsed = Wrap24(h - Wrap24(departureHours[i]));
                if (elapsed < bestElapsed) { bestElapsed = elapsed; best = i; }
            }
            return best;
        }

        /// <summary>How long the block departing at <paramref name="departureHour"/> has been running at
        /// <paramref name="hourOfDay"/>, in game hours — wrapped, so a block that spans midnight measures
        /// straight through it.</summary>
        public static float ElapsedHours(float hourOfDay, float departureHour) =>
            Wrap24(Wrap24(hourOfDay) - Wrap24(departureHour));

        /// <summary>
        /// How many GAME hours travelling <paramref name="routeLengthMetres"/> takes at
        /// <paramref name="speedMetresPerSecond"/> — the one conversion between the world's metres and
        /// the clock's hours. <paramref name="secondsPerGameHour"/> is passed in rather than read: it is
        /// the owner's day-length knob (<c>GameConfig.SecondsPerHour</c>) and this file may not reach for
        /// a service.
        ///
        /// <para>Zero for a zero-length route (two blocks in one place: nobody travels), and zero rather
        /// than infinity for a nonsensical speed or day length, so a half-authored timetable leaves a body
        /// standing rather than teleporting or NaN-ing.</para>
        /// </summary>
        public static float TravelHours(float routeLengthMetres, float speedMetresPerSecond,
                                        float secondsPerGameHour)
        {
            if (routeLengthMetres <= 0f || speedMetresPerSecond <= 0f || secondsPerGameHour <= 0f)
                return 0f;
            return routeLengthMetres / speedMetresPerSecond / secondsPerGameHour;
        }

        /// <summary>How far along the leg a body is, in metres, after <paramref name="elapsedHours"/> of a
        /// block — the inverse of <see cref="TravelHours"/>, and what a position is sampled at.</summary>
        public static float DistanceTravelled(float elapsedHours, float speedMetresPerSecond,
                                              float secondsPerGameHour)
        {
            if (elapsedHours <= 0f || speedMetresPerSecond <= 0f || secondsPerGameHour <= 0f) return 0f;
            return elapsedHours * secondsPerGameHour * speedMetresPerSecond;
        }
    }
}
