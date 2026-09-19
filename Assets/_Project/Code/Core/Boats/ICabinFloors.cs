namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A CABIN'S FLOORS, AS THE WALKER ON THEM ASKS</b> — the seam that lets the player's own deck
    /// walk take a companionway, a ladder or a stair (a <see cref="BoatInteriorRoute"/>) without the
    /// Player lane naming a single Boats type.
    ///
    /// <para><b>Why the walker has to ask at all (Phase A, 2026-09-18).</b> Until now the only way
    /// between floors was her door, and the door is a threshold, not a stair: it swaps her between the
    /// deck and ONE level. The sport fishers' flybridges, skylounges and cabins below are joined by
    /// routes the def has always carried and nothing ever walked, so a skipper who left the Convertible's
    /// flybridge helm stood on a deck with no way down, and the Skybridge's skylounge had no stair. The
    /// walker is the only party that knows she has stepped onto a route's end; the cabin is the only
    /// party that may change what she is inside of. So the walker asks, and the cabin answers — the
    /// same division of labour <see cref="ICabinThreshold"/> draws for a doorway.</para>
    ///
    /// <para><b>Transitions, not state.</b> Everything here that changes anything is one of the cabin's
    /// three existing transitions — <see cref="TryEnter"/>, <see cref="TryExit"/>,
    /// <see cref="TryGoToLevel"/> — each of which refuses (and changes nothing) when it does not apply,
    /// and publishes <see cref="CabinEntered"/>/<see cref="CabinLeft"/> exactly as a door crossing does.
    /// The walker never holds a second copy of "is she inside"; it reads <see cref="IsInside"/> and
    /// <see cref="Level"/> back every tick.</para>
    ///
    /// <para><b>A SECOND interface rather than a member on an existing one</b> —
    /// <see cref="ICabinThreshold"/>'s reasoning, verbatim: a member added to a widely-implemented Core
    /// interface stops every test double compiling in files nobody touched. Ask for the capability with
    /// <c>GetComponentInChildren&lt;ICabinFloors&gt;()</c>; a hull without one says so by being null,
    /// which is most of the fleet and is DATA rather than a fault.</para>
    ///
    /// <para><b>⚠ Resolve it, do not cache it blind.</b> An interface reference does not go through
    /// <c>UnityEngine.Object</c>'s <c>==</c>, so a destroyed cabin still answers through this type.
    /// Every consumer tests liveness with <c>is UnityEngine.Object o &amp;&amp; o != null</c> — and a
    /// hull swap tears the cabin down and builds a new one, so a consumer re-resolves on a new skin.</para>
    /// </summary>
    public interface ICabinFloors
    {
        /// <summary>The imported room — its levels, its routes and the two tolerances the importer placed
        /// those routes by (<see cref="BoatInteriorDef.FloorTolerance"/>,
        /// <see cref="BoatInteriorDef.RouteEndReach"/>). Null for a cabin that was never wired.</summary>
        BoatInteriorDef Def { get; }

        /// <summary>Whether she is inside right now.</summary>
        bool IsInside { get; }

        /// <summary>Which of <see cref="Def"/>'s levels she is on; 0 outside.</summary>
        int Level { get; }

        /// <summary>
        /// Is <paramref name="level"/> a ROOM this cabin can show her standing in — usable, and drawn by
        /// whichever picture this hull has? False for an open working deck the def declares only so its
        /// height can be measured (the ships' <c>main_deck</c>), for a level no picture ever baked (the
        /// Convertible's <c>helm_deck</c>), and for an index that is not a level. A route end on such a
        /// level is a place on the DECK, never a reason to go inside.
        /// </summary>
        bool IsDrawnLevel(int level);

        /// <summary>Bring the room's picture in before she arrives in it — what the door does at its cue
        /// start. Idempotent, and a no-op on a hull whose room is the hull mesh itself.</summary>
        void EnsureCells();

        /// <summary>Go inside onto <paramref name="level"/>. False, changing nothing, when she is already
        /// in or the level is not usable.</summary>
        bool TryEnter(int level);

        /// <summary>Come out. False when she was not inside.</summary>
        bool TryExit();

        /// <summary>Change level while inside. False, changing nothing, outside, onto the level she is
        /// already on, or onto an unusable one.</summary>
        bool TryGoToLevel(int level);
    }
}
