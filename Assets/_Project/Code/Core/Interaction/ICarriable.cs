using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A thing you can pick up and put down</b> — the Core contract behind the carry verb.
    ///
    /// <para><b>Why this is in Core and not in Player, which is where the only implementor lives today.</b>
    /// Not a taste call — the assembly graph forces it. <c>HiddenHarbours.Fishing</c> references
    /// <c>Core</c> and <c>Economy</c>; it does <b>not</b> reference <c>Player</c>, and it must not start
    /// (rule 4). Yet the very next question the game needs to ask is "is the shovel in the fisher's
    /// hands?", and the asker is <c>ClamDig</c>, in Fishing. A concrete <c>CarryHands.Carried</c> typed to
    /// a Player class is a question Fishing physically cannot ask. So the contract moves down to the seam
    /// both lanes already share, and the hands publish themselves on <see cref="GameServices.Hands"/> the
    /// way every other cross-lane producer does.</para>
    ///
    /// <para><b>Why the drawing members are on the contract.</b> <see cref="IInteractable"/> deliberately
    /// says nothing about presentation, and that is right for it: a candidate is acted ON, never drawn.
    /// Carrying is the other case. Being held IS a visual state — the thing hangs at the carrier's hip,
    /// shows the facing the carrier is drawn at, and rides the carrier's sorting band — and the carrier is
    /// the only object that knows those three numbers. Leaving them off the contract would mean the hands
    /// hold something they cannot pose, which is not a carry at all. So <see cref="ShowFacing"/> and
    /// <see cref="RideSortingBand"/> are here, and they are the whole of it: the carrier states a facing
    /// index and a sorting slot, and the implementor decides what those mean for its own art.</para>
    ///
    /// <para><b>What is NOT here.</b> No pick-up rule (that is <c>CarryMath</c>, pure and testable, in the
    /// lane that owns the hands). No priority and no reach — a carriable that also wants to answer the
    /// interact press implements <see cref="IInteractable"/> as well, which is exactly what
    /// <c>CarriableFuelContainer</c> does. The two interfaces are deliberately separate: a thing can be
    /// carried without being a press candidate (a landed fish placed into your hand by the catch path),
    /// and a thing can be a press candidate without being carriable (a freezer).</para>
    /// </summary>
    public interface ICarriable
    {
        /// <summary>
        /// The stable <b>Def id</b> of WHAT this is — <c>tool.shovel</c>, <c>fuelstore.gas_jerry_s20</c>,
        /// <c>fish.soft_shell_clam</c>. A TYPE id, not an instance id.
        ///
        /// <para><b>⚠️ Do not confuse this with <see cref="IInteractable.Id"/>, which is the opposite
        /// thing.</b> That one must be UNIQUE among live registrants (it is the resolver's last tie-break,
        /// and duplicates make its order non-total). This one must be SHARED by every instance of the same
        /// kind, because it is what a gate matches on: "is a shovel in your hands" is a question about the
        /// kind of thing held, not about which shovel. An object that is both implements both, and the two
        /// ids differ.</para>
        /// </summary>
        string DefId { get; }

        /// <summary>True when this may be lifted at all. False is a permanent property of the thing (a
        /// 10,000 L bulk tank), not a live refusal — the live refusals are <c>CarryMath</c>'s.</summary>
        bool IsCarriable { get; }

        /// <summary>The transform the hands re-parent and pose. Never null for a live implementor; a
        /// consumer that holds a reference across frames must launder Unity's fake-null through THIS
        /// (a <see cref="UnityEngine.Object"/>, so <c>!= null</c> is the destroyed-object check) rather
        /// than through the interface reference, which does not carry that overload.</summary>
        Transform Transform { get; }

        /// <summary>How many facings this thing's art was baked at — the modulus a carrier resolves the
        /// carrier's drawn heading into. 0 means "no directional art", which every caller must answer
        /// null-safely rather than by dividing by it.</summary>
        int BakedFacings { get; }

        /// <summary>Draw the given baked facing cell. Out-of-range indices are the implementor's to clamp
        /// or ignore; a carrier states an index and does not check.</summary>
        void ShowFacing(int facingIndex);

        /// <summary>Draw at a sorting slot the CARRIER computed, inside the band the carrier already
        /// occupies (ADR 0032). No implementor may author a sorting order of its own while held — the
        /// number arrives derived from the carrier's live Y-sort output every frame.</summary>
        void RideSortingBand(int sortingLayerId, int sortingOrder);

        /// <summary>Called as this is lifted, with the hands that now hold it — handed in rather than
        /// searched for, so an implementor's "am I carried" can never bind to a different pair.</summary>
        void OnLifted(ICarrier carrier);

        /// <summary>Called once this is back down at its final position.</summary>
        void OnPlaced();
    }

    /// <summary>
    /// <b>A pair of hands</b> — whatever is holding something right now. The read side of
    /// <see cref="ICarriable"/>, published on <see cref="GameServices.Hands"/> so any lane can ask what is
    /// held without referencing the lane that owns the holding.
    ///
    /// <para>Deliberately read-only. Lifting and placing stay on the implementor, because they need a
    /// walkability read and a re-parent that are the carrier's own business; what crosses the module
    /// boundary is only the QUESTION "what is in your hands", which is what every gate downstream of this
    /// actually wants.</para>
    /// </summary>
    public interface ICarrier
    {
        /// <summary>What is held right now, or null. <b>Implementors must launder Unity's fake-null
        /// here</b> — see <see cref="GameServices.Hands"/> — so a consumer's plain <c>!= null</c> is
        /// right.</summary>
        ICarriable Carried { get; }

        /// <summary>True when something is held. Sugar over <see cref="Carried"/>, for the call sites that
        /// only care whether the hands are free.</summary>
        bool IsCarrying { get; }
    }

    /// <summary>
    /// "Is a <i>thing of this kind</i> in the player's hands?" — the one question the gates ask, asked
    /// once, in the one place that knows how to ask it safely.
    ///
    /// <para><b>Why this exists rather than the obvious one-liner at each call site.</b> The obvious
    /// one-liner is <c>GameServices.Hands?.Carried?.DefId == id</c>, and it is wrong twice over: Unity's
    /// <c>?.</c> sails straight past a destroyed component's fake-null (compile-clean, runtime-red), and
    /// an interface-typed reference does not carry <see cref="UnityEngine.Object"/>'s <c>==</c> overload
    /// that would have caught it. Stating the safe form once means the next gate to want it cannot get it
    /// wrong.</para>
    /// </summary>
    public static class CarriedItem
    {
        /// <summary>
        /// True iff the player's hands hold something whose <see cref="ICarriable.DefId"/> equals
        /// <paramref name="defId"/> (ordinal). Empty/null id, no published hands, or empty hands all
        /// answer false — never a throw, because "nobody is carrying anything" is the ordinary state of
        /// the world in EditMode, in a bare art scene, and for the first ten seconds of every game.
        /// </summary>
        public static bool InHand(string defId) => InHand(GameServices.Hands, defId);

        /// <summary>Testable overload over explicit hands — the form an EditMode test drives, since a
        /// plain MonoBehaviour's <c>OnEnable</c> never fires there and nothing publishes itself.</summary>
        public static bool InHand(ICarrier hands, string defId)
        {
            if (string.IsNullOrEmpty(defId)) return false;
            Both(hands, out ICarriable a, out ICarriable b);
            return Matches(a, defId) || Matches(b, defId);
        }

        private static bool Matches(ICarriable held, string defId)
            => held != null && string.Equals(held.DefId, defId, System.StringComparison.Ordinal);

        /// <summary>
        /// <b>Everything in the carrier's hands, right hand first</b> — the one place the two-hand
        /// downcast is written, so no lane has to know whether the carrier it was handed has one slot or
        /// two. The order is fixed (right, then left) rather than "most recent", so a caller that
        /// memoizes on the pair sees a stable answer while nothing has changed.
        ///
        /// <para><paramref name="second"/> is null for a one-slot carrier (a test double), for a single
        /// thing held, and for a two-handed CRADLE — a cradled fish is ONE object across both hands and
        /// must be reported once, or every caller here would count it twice. Both outputs are laundered,
        /// so a plain <c>!= null</c> is right.</para>
        ///
        /// <para>No allocation and no enumerator: two out parameters, because every caller is on a
        /// resolve or gate path that runs while the player merely stands near something (rule 7).</para>
        /// </summary>
        public static void Both(ICarrier hands, out ICarriable first, out ICarriable second)
        {
            first = second = null;
            if (hands == null) return;

            if (hands is IHandsCarrier two)
            {
                HandSlots slots = two.Slots;
                if (slots == null) { first = hands.Carried; return; }

                first = slots.Right ?? slots.Left;
                if (!slots.SpansBoth && !ReferenceEquals(slots.Left, first)) second = slots.Left;
                return;
            }

            first = hands.Carried;
        }

        /// <summary>True when <paramref name="thing"/> is in EITHER hand — what a carriable asks to know
        /// whether it is the one being held. Replaces <c>ReferenceEquals(hands.Carried, this)</c>, which
        /// was right while there was one slot and silently answers "no" for the off hand now there are
        /// two.</summary>
        public static bool IsHeld(ICarrier hands, ICarriable thing)
        {
            if (thing == null) return false;
            Both(hands, out ICarriable a, out ICarriable b);
            return ReferenceEquals(a, thing) || ReferenceEquals(b, thing);
        }

        /// <summary>The thing in her hands of this kind, or null — "is there a catch in her hands", asked
        /// without caring which hand it is in. A one-slot carrier answers from its single slot.</summary>
        public static ICarriable Held(ICarrier hands, HandLoad load)
        {
            if (hands is IHandsCarrier two && two.Slots != null) return two.Slots.FirstOf(load);
            ICarriable one = hands?.Carried;
            return one != null && HandSlots.LoadOf(one) == load ? one : null;
        }
    }
}
