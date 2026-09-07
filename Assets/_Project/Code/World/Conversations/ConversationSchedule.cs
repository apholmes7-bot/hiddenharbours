namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>WHICH MINUTE TODAY'S EXCHANGE HAPPENS AT</b> — the pure, seeded arithmetic behind
    /// <see cref="NpcConversationDirector"/>, so "same seed, same day ⇒ same chat at the same minute" is
    /// an EditMode assertion with plain numbers rather than something you have to stand in the village
    /// and wait for (CLAUDE.md rule 5).
    ///
    /// <para><b>It reuses the routine engine's own hashing</b> (<see cref="RoutineSchedule.Hash01"/>:
    /// FNV-1a plus the avalanche finalizer, process-stable, never <c>UnityEngine.Random</c> and never
    /// <c>string.GetHashCode</c>). A second hash living next door to that one would be two answers to
    /// "what does this seed mean", and they would drift.</para>
    ///
    /// <para><b>⭐ The day IS folded into the seed here, and that is the OPPOSITE of what
    /// <see cref="RoutineSchedule.RoutineSeed"/> does — deliberately.</b> A villager's departure jitter
    /// is who they are: the postmistress opens a few minutes past eight every morning and the player can
    /// learn it, so the day is excluded there on purpose. Chatter is the other thing entirely — two
    /// neighbours who said the same words at the same second of every single day would read as a
    /// mechanism rather than as a village. So today's exchange lands at its own minute, and tomorrow's
    /// lands at a different one, and both are perfectly reproducible from
    /// <c>(worldSeed, dayIndex, conversationId)</c>.</para>
    /// </summary>
    public static class ConversationSchedule
    {
        /// <summary>Stream id for the start-minute roll, so this can never collide with the routine
        /// engine's departure jitter (<see cref="RoutineSchedule.JitterStream"/>) even at the same
        /// seed.</summary>
        public const int StartStream = 41;

        /// <summary>
        /// This exchange's seed: <c>(worldSeed, dayIndex, conversationId)</c>. The day is in it — see the
        /// class remarks for why that is right here and wrong one file over.
        /// </summary>
        public static uint SeedFor(int worldSeed, int dayIndex, string conversationId)
        {
            uint h = RoutineSchedule.Fold(2166136261u, worldSeed);
            h = RoutineSchedule.Fold(h, dayIndex);
            h = RoutineSchedule.Fold(h, conversationId);
            return h;
        }

        /// <summary>
        /// The game hour today's run of <paramref name="conversationId"/> starts at: a seeded point
        /// inside <c>[earliestHour, latestHour]</c>.
        ///
        /// <para>An empty or inverted window collapses to <paramref name="earliestHour"/> rather than
        /// throwing or picking something outside it — a half-authored def should produce a conversation
        /// at a defensible time, not no conversation and no explanation. A window that crosses midnight
        /// is NOT supported and also collapses: the village's exchanges are daytime things, and a
        /// wrapping window would need the day index to change halfway through its own seed.</para>
        /// </summary>
        public static float StartHourFor(int worldSeed, int dayIndex, string conversationId,
                                         float earliestHour, float latestHour)
        {
            float span = latestHour - earliestHour;
            if (span <= 0f) return RoutineSchedule.Wrap24(earliestHour);

            float t = RoutineSchedule.Hash01(SeedFor(worldSeed, dayIndex, conversationId), StartStream, 0);
            return RoutineSchedule.Wrap24(earliestHour + t * span);
        }

        /// <summary>
        /// Is <paramref name="hourOfDay"/> at or past today's start, and still inside the window?
        ///
        /// <para>The upper bound matters as much as the lower one: it is what stops an exchange that
        /// could not start (one of them was still walking, or the player was mid-conversation with one of
        /// them) from firing hours later at a moment the author never pictured. Miss the window and the
        /// pair simply do not talk today.</para>
        /// </summary>
        public static bool IsDue(float hourOfDay, float startHour, float latestHour)
            => hourOfDay >= startHour && hourOfDay <= latestHour;
    }
}
