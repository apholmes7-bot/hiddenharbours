namespace HiddenHarbours.World
{
    /// <summary>
    /// Something that can give a villager a day that differs from the one authored on their
    /// <see cref="RoutineDef"/> — the beach on a fine evening, say.
    ///
    /// <para><b>The villager ASKS; nothing pushes.</b> <see cref="VillagerRoutine"/> asks once when the
    /// game day changes (and again if <see cref="Version"/> moves), so the answer is in place on the
    /// very first frame of the day and nobody pops across the village a tick late.</para>
    ///
    /// <para><b>The answer must be a pure function</b> of the villager's authored stops, the world seed
    /// and the day (rule 5): asked twice, it answers the same. Nothing about it is saved.</para>
    /// </summary>
    public interface IRoutineDaySource
    {
        /// <summary>Moves whenever a day already handed out may be stale for a reason other than the
        /// date (the terrain was replaced, the content was reloaded).</summary>
        int Version { get; }

        /// <summary>
        /// Today's plan for <paramref name="villager"/>, or null to keep the authored day.
        /// <paramref name="blockInteriors"/> is the plan's per-block interior table, as
        /// <see cref="RoutinePlanner.BuildFromStops"/> returns it.
        /// </summary>
        RoutinePlan PlanDay(VillagerRoutine villager, int dayIndex, float secondsPerGameHour,
                            out BuildingInterior[] blockInteriors);
    }
}
