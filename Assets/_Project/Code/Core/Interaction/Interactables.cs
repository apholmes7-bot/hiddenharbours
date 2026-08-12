using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The <b>registry of things the interact verb can act on</b> in the active region — every fixture,
    /// every loose object, every future pick-up — and the list <see cref="InteractResolver"/> walks.
    ///
    /// <para><b>A registry, not a <see cref="GameServices"/> slot</b>, for exactly the reason
    /// <see cref="MooringCleats"/> and <see cref="StandableSurfaces"/> are ones: there are many at a time,
    /// they appear and disappear with their components, and no installer should have to know about them.
    /// Same shape, same scene-scoped register-on-enable / relinquish-on-disable contract, and the same
    /// "empty registry is a valid answer" property — which here carries real weight, because an empty
    /// registry is precisely what makes the verb's arrival a no-op for every INTERACT behaviour that
    /// predates it.</para>
    ///
    /// <para><b>No allocation per query (rule 7).</b> A plain list walked by index — no enumerator, no
    /// hashing, no LINQ. The list is small by construction (one region's fixtures plus whatever is loose
    /// on the ground near you), and it is walked every frame to publish the highlight candidate, so
    /// "small and boring" is the requirement, not an optimisation.</para>
    /// </summary>
    public static class Interactables
    {
        // One region's fixtures plus a handful of loose objects is the realistic size.
        private static readonly List<IInteractable> Registered = new List<IInteractable>(16);

        /// <summary>How many candidates are registered right now (0 = nothing here to act on).</summary>
        public static int Count => Registered.Count;

        /// <summary>The live registry, for a consumer driving a pure overload with it (and for
        /// diagnostics). Read-only: add and remove through <see cref="Register"/> /
        /// <see cref="Unregister"/> so the two can never diverge.</summary>
        public static IReadOnlyList<IInteractable> Active => Registered;

        /// <summary>Add a candidate (a registrant's <c>OnEnable</c>). Null and double registration are
        /// no-ops — a component enabled twice without a disable between must not appear twice, or the
        /// resolver would weigh the same thing from two entries.</summary>
        public static void Register(IInteractable interactable)
        {
            if (interactable == null || Registered.Contains(interactable)) return;
            Registered.Add(interactable);
        }

        /// <summary>Remove a candidate (a registrant's <c>OnDisable</c>). Unregistered is a no-op — a
        /// teardown must never have to check first.</summary>
        public static void Unregister(IInteractable interactable)
        {
            if (interactable == null) return;
            Registered.Remove(interactable);
        }

        /// <summary>Empty the registry (scene teardown / test isolation). A leaked registration would let
        /// the verb act on a thing that is no longer loaded, so tests clear this in their fixture.</summary>
        public static void Clear() => Registered.Clear();
    }
}
