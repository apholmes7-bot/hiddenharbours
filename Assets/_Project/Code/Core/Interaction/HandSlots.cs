using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What KIND of thing is in a hand — the only property the sharing rule reads. Deliberately four
    /// values and not a type test: the rule is about what you are doing with your hands, and a jerry can,
    /// a shovel and a rod are the same answer to that question.
    /// </summary>
    public enum HandLoad
    {
        /// <summary>Nothing.</summary>
        Empty = 0,

        /// <summary>A one-handed working thing — the rod, the shovel, a fuel can. One at a time: you do
        /// not walk the flats with a rod in one hand and a spade in the other.</summary>
        Tool = 1,

        /// <summary>Something that STACKS what you catch — the pail. One at a time.</summary>
        Container = 2,

        /// <summary>A landed catch: a fish by the gill, a handful of clams. One at a time — the
        /// over-encumbrance rule in its cheapest honest form (diegetic-ui-and-inventory.md §4.2).</summary>
        Catch = 3,
    }

    /// <summary>
    /// Why a thing did not go into a hand. Every value is a COZY refusal (P5) with a sentence in
    /// <see cref="HandSlots.Message"/>, not an error — nothing here throws and nothing here logs.
    /// </summary>
    public enum HandSlotRefusal
    {
        /// <summary>It went in.</summary>
        None = 0,

        /// <summary>Nothing was offered (a null, or a destroyed object).</summary>
        NothingToHold,

        /// <summary>Both hands are already committed.</summary>
        HandsFull,

        /// <summary>A hand already holds one of these. One rod, one pail, one fish.</summary>
        OneOfThoseAtATime,

        /// <summary>It takes BOTH hands and at least one of them is not free — the big-fish cradle.</summary>
        NeedsBothHands,
    }

    /// <summary>
    /// A carriable that says what KIND of load it is, when <see cref="HandSlots.LoadOf"/> could not tell
    /// from the contracts it can already see.
    ///
    /// <para>Optional, like <see cref="ICarryAnchored"/> and for the same reason. Core classifies a
    /// container by the <see cref="IHold"/> it already implements and calls everything else a
    /// <see cref="HandLoad.Tool"/>; the one thing that needs saying out loud is a landed CATCH, whose
    /// class lives in the Player lane where Core cannot see it. Two lines on that class beat a type name
    /// spelled as a string here, or a <c>HandPropKey</c> comparison that would silently reclassify every
    /// catch the day the rig renamed a prop row.</para>
    /// </summary>
    public interface IHandLoad
    {
        /// <summary>Which kind of load this is.</summary>
        HandLoad HandLoad { get; }
    }

    /// <summary>
    /// <b>A carrier with TWO hands</b> — the optional half of <see cref="ICarrier"/>, published so a lane
    /// that only has the read contract can still ask about the second hand.
    ///
    /// <para>Optional rather than folded into <see cref="ICarrier"/> for the reason every other optional
    /// seam here exists: <see cref="ICarrier"/> is implemented by test doubles and read by four lanes, and
    /// widening it would break each of them for a question most of them do not ask. A consumer that DOES
    /// care goes through <see cref="CarriedItem"/>, which handles both shapes and is the only place the
    /// downcast is written.</para>
    /// </summary>
    public interface IHandsCarrier : ICarrier
    {
        /// <summary>The two hands. Never null for an implementor.</summary>
        HandSlots Slots { get; }
    }

    /// <summary>
    /// <b>The fisher's two hands</b> — which hand holds what, what may share the pair, and how big a fish
    /// one hand takes. A plain object: no MonoBehaviour, no scene, no input, so the whole rule is provable
    /// headless — the reason <c>CarryMath</c> and <see cref="InteractResolver"/> live apart from the
    /// components that act on them.
    ///
    /// <para><b>What changed, and why it needed a second slot.</b> One slot meant a landed fish DISPLACED
    /// the rod: the rod slung under her arm and the fish took the hand. The owner's ruling is that she has
    /// two hands and should use them — a fish in one and the rod in the other, a pail in one and the rod
    /// in the other. So the pair is the resource, the pair owns the slots, and a thing asks it for a
    /// hand.</para>
    ///
    /// <para><b>⭐ THE CONTINUITY LAW, which is the whole reason this is a two-slot table and not a
    /// bigger <c>Carried</c> field.</b> Taking something up may never move, re-pose or displace what is
    /// ALREADY held. <see cref="TryTake"/> only ever fills a FREE slot: it never swaps hands to make room,
    /// never slings, never re-assigns. So the rod she is holding stays in the hand it is in, at the wrist
    /// it is pinned to, for the whole of the fish's arrival — and a refusal changes nothing at all, which
    /// is the same law stated for the failing case. It is also why a slot is a PHYSICAL hand rather than
    /// an abstract index: the moment two things are held, each one's pose must come from its own hand's
    /// measured anchors, and a slot that could be re-labelled left/right per facing would slide a held
    /// thing across the body as she turned.</para>
    ///
    /// <para><b>Which hand, and what happens when both want the same one.</b> A caller passes the hand
    /// the art PREFERS at the heading she is drawn at (the rig's own per-facing answer — small props move
    /// to the near hand on the away headings, and which headings those are is measured art, not a rule).
    /// If that hand is free it is used; if it is not, the other hand is, and the thing stays there until
    /// it leaves. Nothing here re-derives a preference and nothing mirrors one.</para>
    /// </summary>
    public sealed class HandSlots
    {
        private ICarriable _left, _right;
        private HandLoad _leftLoad, _rightLoad;
        private bool _spansBoth;      // one thing across the pair — the cradled fish

        // ---- the rule, as pure statics (a truth table can drive these with no object at all) --------

        /// <summary>
        /// <b>May these two share the pair of hands?</b> The whole sharing rule, in one line: two
        /// DIFFERENT kinds may, the same kind twice may not.
        ///
        /// <para>That yields exactly the owner's two named pairings — a fish in one hand and the rod in
        /// the other, a pail in one hand and the rod in the other — plus the one the loop needs and
        /// nobody had to name: the pail in one hand and the fish in the other, which is how a caught
        /// fish gets into a carried pail at all. And it keeps every "one at a time" rule the game
        /// already had: one working tool, one pail, one catch.</para>
        ///
        /// <para><see cref="HandLoad.Empty"/> shares with nothing — an empty hand is not an occupant, and
        /// a caller asking whether nothing may share something has asked the wrong question.</para>
        /// </summary>
        public static bool MayShare(HandLoad a, HandLoad b)
            => a != HandLoad.Empty && b != HandLoad.Empty && a != b;

        /// <summary>
        /// <b>The size rule.</b> A fish longer than <paramref name="maxOneHandLengthMeters"/> is cradled
        /// across both arms; anything at or under it hangs from one hand by the gill.
        ///
        /// <para>It is a LENGTH and not a weight because that is what the arm has to clear: the rig's
        /// gill grip holds the fish head-up beside the thigh, and what decides whether that reads is how
        /// far the tail hangs. The threshold is the owner's dial
        /// (<see cref="GameConfig.MaxOneHandFishLengthMeters"/>), defaulted to the art rig's own cradle
        /// point expressed as a length — see that field.</para>
        ///
        /// <para>A non-positive threshold means "no fish ever needs both hands" rather than "every fish
        /// does": an unwired dial must not jam the hands (the fail-open rule the tide gate and the
        /// walkability read both keep).</para>
        /// </summary>
        public static bool NeedsBothHands(float fishLengthMeters, float maxOneHandLengthMeters)
            => maxOneHandLengthMeters > 0f && fishLengthMeters > maxOneHandLengthMeters;

        /// <summary>
        /// Which kind of load a carriable is. A thing that says so itself (<see cref="IHandLoad"/>) is
        /// taken at its word; an <see cref="IHold"/> is a container; everything else is a one-handed
        /// working thing, which is the honest default for a shovel, a rod and a jerry can alike.
        /// </summary>
        public static HandLoad LoadOf(ICarriable carriable)
        {
            ICarriable c = Launder(carriable);
            if (c == null) return HandLoad.Empty;
            if (c is IHandLoad declared) return declared.HandLoad;
            if (c is IHold) return HandLoad.Container;
            return HandLoad.Tool;
        }

        /// <summary>What the game SAYS when a hand is refused — one sentence, the player's language, no
        /// HUD. Beside the enum so a test can assert every refusal has something to say.</summary>
        public static string Message(HandSlotRefusal refusal) => refusal switch
        {
            HandSlotRefusal.NothingToHold => "There's nothing there to pick up.",
            HandSlotRefusal.HandsFull => "Your hands are full.",
            HandSlotRefusal.OneOfThoseAtATime => "You've got one of those already.",
            HandSlotRefusal.NeedsBothHands => "That one needs both hands.",
            _ => null,
        };

        // ---- the state ------------------------------------------------------------------------------

        /// <summary>What is in her LEFT hand, or null. Unity's fake-null is laundered here, so a plain
        /// <c>!= null</c> at the call site is right (the <c>CarryHands.Carried</c> contract).</summary>
        public ICarriable Left => Launder(_left);

        /// <summary>What is in her RIGHT hand, or null. Same laundering as <see cref="Left"/>.</summary>
        public ICarriable Right => Launder(_right);

        /// <summary>True while one thing occupies BOTH hands — the cradled fish. <see cref="Left"/> and
        /// <see cref="Right"/> are then the same object.</summary>
        public bool SpansBoth => _spansBoth && Left != null;

        /// <summary>True when neither hand holds anything.</summary>
        public bool IsEmpty => Left == null && Right == null;

        /// <summary>How many hands are committed: 0, 1 or 2 (a cradle is 2).</summary>
        public int UsedHands => SpansBoth ? 2 : (Left != null ? 1 : 0) + (Right != null ? 1 : 0);

        /// <summary>What is in the named hand. <see cref="CarryHandSide.Both"/> answers the cradled
        /// thing, or null when the two hands hold different things.</summary>
        public ICarriable InHand(CarryHandSide side) => side switch
        {
            CarryHandSide.Left => Left,
            CarryHandSide.Right => Right,
            CarryHandSide.Both => SpansBoth ? Left : null,
            _ => null,
        };

        /// <summary>The load in the named hand, or <see cref="HandLoad.Empty"/>.</summary>
        public HandLoad LoadIn(CarryHandSide side) => side switch
        {
            CarryHandSide.Left => Left != null ? _leftLoad : HandLoad.Empty,
            CarryHandSide.Right => Right != null ? _rightLoad : HandLoad.Empty,
            _ => HandLoad.Empty,
        };

        /// <summary>Which hand holds this thing — <see cref="CarryHandSide.Both"/> for a cradle,
        /// <see cref="CarryHandSide.None"/> when it is not held at all.</summary>
        public CarryHandSide SideOf(ICarriable thing)
        {
            if (Launder(thing) == null) return CarryHandSide.None;
            bool left = ReferenceEquals(Left, thing), right = ReferenceEquals(Right, thing);
            if (left && right) return CarryHandSide.Both;
            if (left) return CarryHandSide.Left;
            if (right) return CarryHandSide.Right;
            return CarryHandSide.None;
        }

        /// <summary>The first thing held whose load is <paramref name="load"/>, or null. How a caller
        /// asks "is there a catch in her hands" without caring which hand it is in.</summary>
        public ICarriable FirstOf(HandLoad load)
        {
            if (Left != null && _leftLoad == load) return Left;
            if (Right != null && _rightLoad == load) return Right;
            return null;
        }

        /// <summary>True when either hand holds a thing of this kind.</summary>
        public bool Holds(HandLoad load) => FirstOf(load) != null;

        // ---- taking and giving up ---------------------------------------------------------------------

        /// <summary>
        /// Put something in a hand. <see cref="HandSlotRefusal.None"/> means it went in and
        /// <paramref name="side"/> names where; <b>any other value means NOTHING changed</b> — the
        /// continuity law's failing half.
        /// </summary>
        /// <param name="thing">What to hold.</param>
        /// <param name="needsBothHands">True for a load that must be cradled (see
        /// <see cref="NeedsBothHands"/>). It then takes the pair or nothing.</param>
        /// <param name="preferred">The hand the ART prefers at the heading she is drawn at. Honoured
        /// when free; the other hand otherwise. <see cref="CarryHandSide.None"/> and
        /// <see cref="CarryHandSide.Both"/> both read as "no preference" and fall to the right hand,
        /// which is the rig's own default for every one-handed prop but the rope.</param>
        public HandSlotRefusal TryTake(ICarriable thing, bool needsBothHands, CarryHandSide preferred,
                                       out CarryHandSide side)
        {
            side = CarryHandSide.None;

            ICarriable c = Launder(thing);
            if (c == null) return HandSlotRefusal.NothingToHold;

            HandLoad load = LoadOf(c);
            HandSlotRefusal refusal = CanTake(load, needsBothHands);
            if (refusal != HandSlotRefusal.None) return refusal;

            if (needsBothHands)
            {
                _left = _right = c;
                _leftLoad = _rightLoad = load;
                _spansBoth = true;
                side = CarryHandSide.Both;
                return HandSlotRefusal.None;
            }

            bool leftFree = Left == null;
            bool takeLeft = preferred == CarryHandSide.Left ? leftFree : Right != null;
            side = takeLeft ? CarryHandSide.Left : CarryHandSide.Right;
            if (takeLeft) { _left = c; _leftLoad = load; }
            else { _right = c; _rightLoad = load; }
            return HandSlotRefusal.None;
        }

        /// <summary>
        /// <b>Would a load of this kind go in?</b> The whole rule, with nothing changed — so a caller that
        /// must BUILD the thing before it can offer it (a landed catch, which is spawned) can ask first
        /// and not create an object it would have to destroy again.
        ///
        /// <para><see cref="TryTake"/> is defined in terms of this, so the two can never drift: there is
        /// one rule and one place it is written.</para>
        /// </summary>
        public HandSlotRefusal CanTake(HandLoad load, bool needsBothHands)
        {
            if (load == HandLoad.Empty) return HandSlotRefusal.NothingToHold;

            if (needsBothHands)
                // A cradle needs the PAIR. Deliberately not "free a hand for it": that would move
                // something already held, which is the one thing the continuity law forbids.
                return IsEmpty ? HandSlotRefusal.None : HandSlotRefusal.NeedsBothHands;

            // A cradled thing commits both hands, so nothing may join it — and the refusal that reads
            // right is "full", not "one of those at a time", even when the kinds happen to match.
            if (SpansBoth) return HandSlotRefusal.HandsFull;

            // One of each kind. Checked BEFORE the free-slot search so a second rod is refused for the
            // reason it is actually refused, rather than sitting in the other hand.
            if (Holds(load)) return HandSlotRefusal.OneOfThoseAtATime;

            bool leftFree = Left == null, rightFree = Right == null;
            if (!leftFree && !rightFree) return HandSlotRefusal.HandsFull;

            // MayShare is the sharing rule and the check above is its "same kind" arm — so by here the
            // pair is legal. Stated as a guard rather than assumed, because the two would have to be
            // edited together and this is the cheap way to notice if they are not.
            HandLoad other = leftFree ? _rightLoad : _leftLoad;
            if (!IsEmpty && !MayShare(load, other)) return HandSlotRefusal.OneOfThoseAtATime;

            return HandSlotRefusal.None;
        }

        /// <summary>
        /// Give up whatever hand(s) this thing occupies. True when it was actually held — false is the
        /// ordinary "it was not in her hands" answer, never a fault.
        /// </summary>
        public bool Release(ICarriable thing)
        {
            CarryHandSide side = SideOf(thing);
            if (side == CarryHandSide.None) return false;

            if (side != CarryHandSide.Right) { _left = null; _leftLoad = HandLoad.Empty; }
            if (side != CarryHandSide.Left) { _right = null; _rightLoad = HandLoad.Empty; }
            if (side == CarryHandSide.Both) _spansBoth = false;
            return true;
        }

        /// <summary>Empty both hands. For teardown and region-hop resets, never for making room.</summary>
        public void Clear()
        {
            _left = _right = null;
            _leftLoad = _rightLoad = HandLoad.Empty;
            _spansBoth = false;
        }

        /// <summary>
        /// Drop any slot whose occupant has been DESTROYED out from under the hands (a region unloaded, a
        /// test tearing down), and report whether anything went. Called by the carrier before it poses,
        /// so a corpse never gets a wrist.
        /// </summary>
        public bool ForgetDestroyed()
        {
            bool changed = false;
            if (_left != null && Launder(_left) == null) { _left = null; _leftLoad = HandLoad.Empty; changed = true; }
            if (_right != null && Launder(_right) == null) { _right = null; _rightLoad = HandLoad.Empty; changed = true; }
            if (changed) _spansBoth = _left != null && ReferenceEquals(_left, _right);
            return changed;
        }

        /// <summary>
        /// ⚠️ Unity's fake-null, laundered. An INTERFACE reference does not carry
        /// <see cref="Object"/>'s <c>==</c> overload, so a destroyed component reads here as a live
        /// <see cref="ICarriable"/> and would hand every consumer a corpse whose <c>!= null</c> passes.
        /// Casting back to <c>Object</c> re-enters the Unity-aware comparison — the same laundering, for
        /// the same reason, as <c>CarryHands.Carried</c> and <see cref="GameServices.Hands"/>.
        /// </summary>
        private static ICarriable Launder(ICarriable c) => c is Object o && o == null ? null : c;
    }
}
