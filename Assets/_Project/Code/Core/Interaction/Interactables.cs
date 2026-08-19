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

        /// <summary>
        /// <b>Where the registrant with this id lives in the scene</b> — the seam the affordance lane
        /// resolves an offer to a thing it can draw on.
        ///
        /// <para><b>Why this lookup belongs HERE and not in the art lane.</b> The highlight's contract
        /// (see <see cref="IInteractable"/>) is that "the interaction lane never references a renderer and
        /// the art lane never references a registrant". A presenter that walked <see cref="Active"/> and
        /// cast the winner to a <see cref="UnityEngine.Component"/> would be doing exactly the second of
        /// those. So the registry — which already holds registrants, and is Core — answers the question
        /// instead, and hands back a bare <see cref="UnityEngine.Transform"/>. The art lane learns WHERE,
        /// never WHAT: it cannot reach a registrant's members through this, and adding a fixture type
        /// needs no art-side change.</para>
        ///
        /// <para><b>Why a transform and not the position.</b> <see cref="IInteractable.WorldPosition"/>
        /// already says where a thing IS, and <see cref="InteractOfferChanged"/> carries it. What the
        /// affordance needs is the scene NODE — the renderers hang off it, and it keeps following the
        /// fixture without anything having to republish as it moves.</para>
        ///
        /// <para><b>A registrant that is not a <see cref="UnityEngine.Component"/> answers false</b> rather
        /// than throwing. Plain-object registrants are legal (the resolver's tests use them) and simply
        /// have nowhere to draw — which the caller must handle anyway, since an id may name something that
        /// is not in this registry at all (a transit key, an NPC).</para>
        ///
        /// <para><b>Rule 7.</b> A linear walk of a list that is small by construction, called only when the
        /// offer CHANGES, and allocating nothing.</para>
        /// </summary>
        public static bool TryGetTransform(string id, out UnityEngine.Transform transform)
        {
            transform = null;
            if (string.IsNullOrEmpty(id)) return false;

            for (int i = 0; i < Registered.Count; i++)
            {
                IInteractable candidate = Registered[i];
                if (candidate == null) continue;
                if (!string.Equals(candidate.Id, id, System.StringComparison.Ordinal)) continue;

                // ⚠️ `as` then `== null`, never `??`: a destroyed Component is "fake null" and the
                // null-coalescing operator tests the real C# reference, so it would hand back a dead
                // transform (memory: unity-null-coalescing-defeats-fake-null).
                var component = candidate as UnityEngine.Component;
                if (component == null) return false;

                transform = component.transform;
                return true;
            }

            return false;
        }
    }
}
