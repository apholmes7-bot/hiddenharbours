namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>What a villager is DOING in a schedule block</b> — the lightweight activity vocabulary
    /// <c>design/npcs-and-routines.md</c> §2.2 asks for ("activities are lightweight tags that drive
    /// animation, interactability and dialogue context — not a needs simulation").
    ///
    /// <para><b>It is a tag, not a behaviour.</b> Nothing in the routine engine branches on this value:
    /// where the villager goes and when is decided by the station and the clock alone. The activity is
    /// carried so that presentation (a stance, an idle cycle), dialogue context and the owner's own
    /// reading of a routine table have a word for it. That separation is the point — an activity that
    /// changed the pathing would make "what is she doing?" and "where is she?" two answers that can
    /// disagree.</para>
    ///
    /// <para><b>Append-only.</b> A routine asset serializes these by index, so a new activity is a new
    /// member at the END and nothing already authored is renumbered. The four here are the four the
    /// handoff names; the doc's richer list (<c>sell</c>, <c>drink</c>, <c>mend_nets</c>,
    /// <c>tend_lamp</c>…) lands when something actually reads them.</para>
    /// </summary>
    public enum RoutineActivity
    {
        /// <summary>At home — asleep, or on their own step. The block every day both starts and ends in.</summary>
        Home = 0,

        /// <summary>At their post, doing the thing they are known for: the counter, the school, the flats,
        /// the wharf.</summary>
        Work = 1,

        /// <summary>Out on an errand — the store for their dinner, the post office with the day's mail. The
        /// blocks that make the village's paths get walked.</summary>
        Errand = 2,

        /// <summary>Off duty and somewhere public — the green in the evening, the pier head at sunset. What
        /// turns a village into a place people are IN rather than a place people work.</summary>
        Recreation = 3,

        /// <summary>At a wheel, on the road — the block a fish buyer spends bringing his truck down to the
        /// wharf and back (the road fleet's scheduled trips, 2026-09-04).
        ///
        /// <para>⚠️ A TAG like the four above, and NOTHING branches on it — least of all the driving. A
        /// scheduled trip is run by <c>Vehicles.ScheduledTrip</c> off its own timetable; this word exists
        /// so a routine table the owner reads, a dialogue context and an idle stance have a name for the
        /// hours somebody is away with the truck. Making the routine engine drive would give "where is
        /// she?" two answers that can disagree.</para></summary>
        Drive = 4,
    }
}
