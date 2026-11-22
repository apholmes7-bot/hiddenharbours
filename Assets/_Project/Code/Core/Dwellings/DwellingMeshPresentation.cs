using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Installs and removes the Art-side facet renderer for a PARKED DWELLING (ADR 0060).</b> The
    /// sibling of <see cref="IHullMeshPresentationService"/> and <see cref="IVehicleMeshPresentationService"/>,
    /// and a separate interface for the reason ADR 0035 gave vehicles theirs: installing a hull bolts on a
    /// foam injector, a reflection and a waterline clamp, none of which a camper on gravel may have. A
    /// separate entry point cannot make that mistake.
    ///
    /// <para><b>What an implementation does on install:</b> the same facet renderer the hulls and vehicles
    /// use, in a still pose (no rock, no heave, waterline clamp off), sun and lamp shadow casters fitted
    /// once, and no foam and no reflection. It refuses (returns null, with a logged reason) a def whose
    /// <see cref="DwellingMeshDef.IsUsable"/> is false.</para>
    ///
    /// <para><b>What the caller does with the returned renderer.</b> It sets
    /// <see cref="IHullMeshRenderer.HeadingDirUnits"/> once from the site's ground bearing (through
    /// <see cref="HullMeshMath.HeadingToDirUnits"/> and the def's measured azimuth); the sort order is
    /// <see cref="HullSorting.OrderAt"/> at the dwelling's ground pivot, written once at enable because a
    /// dwelling never moves (ADR 0059). The cut is asked for with <c>as IHullCutaway</c> and driven from
    /// <c>BuildingInterior</c>'s inside state, never from a second source of truth. The occupant's
    /// per-pixel hiding rides the renderer's existing <see cref="IHullMeshRenderer.DeckOccupants"/> seam.</para>
    /// </summary>
    public interface IDwellingMeshPresentationService
    {
        /// <summary>
        /// Install (or re-configure in place) a dwelling renderer on <paramref name="host"/>. Returns
        /// null, with a logged reason, when the def is unusable, so the caller can refuse to place an
        /// invisible home.
        /// </summary>
        IHullMeshRenderer Install(GameObject host, DwellingMeshDef def);

        /// <summary>
        /// Attach one moving fixture part (a freezer lid, the door leaf) to a dwelling already installed on
        /// <paramref name="host"/>, as a facet prop. <paramref name="slot"/> names the instance, so a
        /// re-install reconfigures in place rather than accumulating parts. Returns null, with a logged
        /// reason, when the def is unusable or nothing is installed.
        /// </summary>
        IHullPropRenderer AttachPart(GameObject host, HullPropMeshDef def, string slot);

        /// <summary>Remove just the part in <paramref name="slot"/>. Safe when none is there.</summary>
        void DetachPart(GameObject host, string slot);

        /// <summary>Remove a previously installed dwelling renderer and its parts from
        /// <paramref name="host"/>. Safe when none is present.</summary>
        void Remove(GameObject host);
    }

    /// <summary>
    /// The service locator for <see cref="IDwellingMeshPresentationService"/>. Not a <c>GameServices</c>
    /// member, for the same reason as <see cref="HullMeshPresentation"/>: <c>GameServices.Reset()</c>
    /// clears game state between tests and scenes, and this is stateless presentation wiring. Art
    /// registers at runtime load; edit-time scene builders do not, so a built scene serialises no
    /// renderer and the dwelling skins itself on enable. A null service means "no mesh path here".
    /// </summary>
    public static class DwellingMeshPresentation
    {
        public static IDwellingMeshPresentationService Service { get; set; }
    }
}
