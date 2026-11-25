using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A fixture worked from the helm</b>: something mounted at a boat's wheel that the skipper can
    /// press without leaving it (owner ask 2026-11-25: the cabin-light switch at the helm).
    ///
    /// <para>Optional, beside <see cref="IInteractable"/>. The helm itself is not a registrant (the
    /// control switcher owns taking and leaving it), so the interact press at the helm never reached the
    /// registry. A registrant that also implements this is gathered by the switcher at the helm and
    /// offered on the chooser beside "Leave the helm" (and beside "Take the helm" when he stands next to
    /// the wheel on deck). With none in reach the helm press is exactly what it always was.</para>
    ///
    /// <para>The marker is how the Player module finds these without naming a Boats class.</para>
    /// </summary>
    public interface IHelmFixture
    {
        /// <summary>The boat whose helm this fixture is at, or null when it is not at a helm right now
        /// (a switch mounted by the door, say). Compared against the boat the player is aboard.</summary>
        GameObject HelmBoat { get; }
    }
}
