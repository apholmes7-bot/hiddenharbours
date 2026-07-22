using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The Core seam between posing a mesh hull and drawing one (ADR 0022 phase 4).</b> Boats
    /// decides WHERE the hull points and how it rocks; Art owns the facet URP pass that draws it —
    /// and rule 4 forbids either referencing the other's concrete classes. This interface is the
    /// hull-drawer as Boats is allowed to see it: four pose channels in RIG units, a configured
    /// check, and sorting.
    ///
    /// <para>Implemented by Art's <c>IsoFacetHullRenderer</c>; installed through
    /// <see cref="HullMeshPresentation.Service"/>. All pose setters are cheap and idempotent (the
    /// renderer dirty-checks), safe to write every LateUpdate with no allocation (rule 7).</para>
    /// </summary>
    public interface IHullMeshRenderer
    {
        /// <summary>Heading in RIG dir units (1 = 45°, fractional allowed — continuous is the point).
        /// Map a compass heading through <see cref="HullMeshMath.HeadingToDirUnits"/>.</summary>
        float HeadingDirUnits { get; set; }

        /// <summary>Roll in degrees, the rig's own convention (+ = the rig's rockMotion roll).</summary>
        float RollDegrees { get; set; }

        /// <summary>Pitch in degrees, the rig's own convention.</summary>
        float PitchDegrees { get; set; }

        /// <summary>Heave in rig PIXELS (world metres = px / <see cref="HullMeshDef.PxPerMetre"/>).</summary>
        float HeavePixels { get; set; }

        /// <summary>True when a hull setup is loaded and the renderer can draw.</summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Where this hull sorts against the scene's sprites — whole-object, exactly as a baked
        /// sprite would (ADR 0022 "Unchanged"). Sets the SortingGroup the overlay quad sorts under.
        /// </summary>
        void SetSorting(int sortingLayerId, int sortingOrder);
    }

    /// <summary>Installs / removes the Art-side mesh-hull renderer on a host GameObject.</summary>
    public interface IHullMeshPresentationService
    {
        /// <summary>
        /// Install (or re-configure in place) a mesh-hull renderer on <paramref name="host"/> from a
        /// baked def. Returns null — with a logged reason — when the def is unusable, so the caller
        /// can fall back to the sprite path rather than field an invisible boat.
        /// </summary>
        IHullMeshRenderer Install(GameObject host, HullMeshDef def);

        /// <summary>Remove a previously installed renderer (and everything it owns) from
        /// <paramref name="host"/>. Safe when none is present.</summary>
        void Remove(GameObject host);
    }

    /// <summary>
    /// The service locator for <see cref="IHullMeshPresentationService"/>. Deliberately NOT a
    /// <c>GameServices</c> member: <c>GameServices.Reset()</c> clears game-STATE services between
    /// tests/scenes, and this is stateless presentation wiring that must survive those resets. Art
    /// self-registers at runtime load; EditMode tests and editor tooling register explicitly.
    /// Consumers null-check — a null service means "no mesh path here", and the skinner's sprite
    /// fallback stands.
    /// </summary>
    public static class HullMeshPresentation
    {
        public static IHullMeshPresentationService Service { get; set; }
    }
}
