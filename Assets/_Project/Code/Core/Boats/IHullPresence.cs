namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A HULL THAT IS ACTUALLY ON THE WATER RIGHT NOW</b>, and where her outline lies — the seam a
    /// swimmer's world asks "is there a boat here?" through.
    ///
    /// <para><b>Why a presence and not a boat.</b> Nothing on this seam is about what the vessel IS —
    /// no def, no owner, no propulsion, no way of steering her. It publishes one thing, her
    /// <see cref="HullFootprint"/> in world metres, because the only question asked of it is a question
    /// about WATER: does a hull reach into the place this person is swimming. Keeping it to the outline
    /// is what lets <c>Player</c> read it without knowing that <c>Boats</c> exists (rule 4), and what
    /// lets a test register a bare footprint with no GameObject behind it at all.</para>
    ///
    /// <para><b>Live, not cached.</b> The footprint is read fresh on every query because a hull under
    /// way moves and a moored one still swings; a registrant that cached her outline at spawn would
    /// answer for a boat that has left. It is a property rather than a method for exactly that reason —
    /// there is nothing to invalidate.</para>
    ///
    /// <para><b>Registering is a claim about the SEA, not about ownership.</b> Somebody else's moored
    /// boat is as swimmable-to as your own; the registry does not know whose she is and must not start
    /// to care. What it does mean is "a swimmer could plausibly be alongside this" — see
    /// <see cref="HullPresences"/> for what the water then allows.</para>
    /// </summary>
    public interface IHullPresence
    {
        /// <summary>Her outline in WORLD metres, as she lies this instant.</summary>
        HullFootprint Footprint { get; }
    }
}
