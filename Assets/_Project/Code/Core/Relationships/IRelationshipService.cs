namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The web of ties between people, as a seam</b> (relationships slice 1, owner ruling 2026-11-14,
    /// rule 4). A feature module that knows something happened between two people (the crew system, the
    /// cannery, the shops; none raise events yet) records it here; a module that wants to react (talk,
    /// routines, career beats) asks for a pair's stage or listens for <see cref="RelationshipStageChanged"/>
    /// on the <see cref="EventBus"/>. Neither references the other.
    ///
    /// <para><b>No visible score, ever</b> (the ruling): this seam answers stages for code to act on; nothing
    /// may put a strength, a bar or hearts on screen. No romance stage exists.</para>
    ///
    /// <para><b>Deterministic, in memory (slice 1).</b> Seeded from the shipped
    /// <see cref="RelationshipTieDef"/>s; moved only by recorded events at <see cref="ApplyDay"/>. Not saved
    /// yet: the persistence seam is <see cref="RelationshipGraph.Snapshot"/> /
    /// <see cref="RelationshipGraph.Restore"/>, for slice 2.</para>
    /// </summary>
    public interface IRelationshipService
    {
        /// <summary>How <paramref name="a"/> and <paramref name="b"/> stand (either order).
        /// <see cref="RelationshipStage.Stranger"/> for a pair with no history or an unknown id.</summary>
        RelationshipStage Stage(string a, string b);

        /// <summary>Queue an event for the next <see cref="ApplyDay"/>. False, and ignored, for a bad id
        /// pair or a non-positive unit count.</summary>
        bool Record(in RelationshipEvent e);

        /// <summary>Apply the queued events (the slow tick: once, at the start of the next day; nothing calls
        /// it in slice 1). Publishes one <see cref="RelationshipStageChanged"/> per pair that crossed a stage
        /// and returns how many did.</summary>
        int ApplyDay();
    }
}
