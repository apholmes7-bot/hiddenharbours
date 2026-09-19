using System.Collections.Generic;

namespace HiddenHarbours.Tests.Support
{
    /// <summary>
    /// ⭐ <b>Phase B, 2026-09-19, C6 — the cabin guards' fleet facts, named ONCE.</b> The EditMode walk over
    /// her measured floors and the PlayMode walk on the live hull read the same list, so a block on art
    /// cannot be retired in one runner and left standing in the other.
    /// </summary>
    public static class CabinFleet
    {
        /// <summary>
        /// 🔴 Hulls whose door the art has not yet made walkable, each with its reason. A debt with an
        /// owner, not an exemption: every guard runs them, Ignores them in these words while the block
        /// holds, and FAILS the day she walks to her door, so the entry is retired rather than left to hide
        /// the next regression.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> BlockedOnArt = new Dictionary<string, string>
        {
            ["Tanker"] =
                "blocked on ART (fleet report 2026-09-18, Cause 3): the Tanker's door is on an island. Its band " +
                "is 14.04 m from any deck she can walk; the only route is a ladder the importer turns " +
                "position-less, and the Tanker has no helm station. A3: the art side owes a walkable way to " +
                "the crew door and a helm station — an alley that meets the poop, or a door on a deck she " +
                "can reach.",
        };
    }
}
