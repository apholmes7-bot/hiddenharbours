using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The occupant has gone below on this hull</b> — ADR 0038's third consequence, said out loud so
    /// the things that were drawing her <i>relative to the boat</i> can stop.
    ///
    /// <para><b>Why a signal and not a call.</b> The one listener that matters today owns two per-renderer
    /// shader properties on the PLAYER's own rider (<c>DeckRiderVisual</c>), and Boats may not name
    /// Player (rule 4). The cabin knows it swapped; it does not get to know who was watching. Anything
    /// else that needs the same edge later — audio muffling the sea, a light that only burns below —
    /// listens beside it rather than being wired in.</para>
    ///
    /// <para><b>⚠️ RAISED ON THE TRANSITION, NEVER ON A RE-ASSERT.</b> The cabin re-applies its layer swap
    /// every tick and again on every <c>OnEnable</c>, because root-toggling IS how a region hop works and
    /// the swap is an invariant that is maintained rather than merely established (ADR 0038 proposal 4).
    /// A publish from there would tell every listener that the player walked in again at every region
    /// boundary she crossed while below. The cabin's own state survives the toggle by design; a listener's
    /// answer to this must survive it the same way, and for the same reason.</para>
    ///
    /// <para><b><see cref="HullId"/> is an INSTANCE, not a class.</b> Eighteen lobster boats can be afloat
    /// in one creek and several may be the same def; the question a listener asks is "did the geometry in
    /// front of <i>me</i> change", which is about the hull she is standing on and not about her kind. It
    /// is the boat ROOT's <c>GetEntityId()</c> — the same object the rider is bound to — so the compare is
    /// a value and this event holds no reference to a boat that may be torn down under it. (⚠ The id is
    /// <c>GetEntityId</c> and not the deprecated <c>GetInstanceID</c>: 6000.5 marks that
    /// obsolete-as-ERROR, which compiles in a permissive harness and fails in the editor.)</para>
    ///
    /// <para>Presentation only. Nothing that writes a save, moves the clock or decides where the player
    /// may stand reads this: going below is a layer swap, not a place you travel to.</para>
    /// </summary>
    public readonly struct CabinEntered
    {
        /// <summary>The boat root's entity id — whose cabin was entered.</summary>
        public readonly EntityId HullId;

        /// <summary>Which of the def's levels she walked in onto. Re-published on a level change (a
        /// companionway taken while below), so a listener holding the level never reads a stale one;
        /// every listener that only cares about "inside or out" is unaffected, because entering a cabin
        /// you are already in is the same answer twice.</summary>
        public readonly int Level;

        public CabinEntered(EntityId hullId, int level)
        {
            HullId = hullId;
            Level = level;
        }
    }

    /// <summary>
    /// <b>The occupant has come back out on deck.</b> The bookend to <see cref="CabinEntered"/>, carrying
    /// the same hull so a listener can ignore a cabin it was never following.
    ///
    /// <para>Same law as its twin: published on the transition only. A cabin torn down with somebody
    /// inside publishes nothing at all — a listener that keyed on a hull which no longer exists must
    /// notice that for itself rather than be told, because the object it would be told about is the one
    /// that is gone.</para>
    /// </summary>
    public readonly struct CabinLeft
    {
        /// <summary>The boat root's entity id — whose cabin was left.</summary>
        public readonly EntityId HullId;

        public CabinLeft(EntityId hullId) { HullId = hullId; }
    }
}
