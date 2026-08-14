namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Hands a landed catch can be put INTO</b> — the seam behind the owner's third ruling of
    /// 2026-08-13: "a clam can be in hand but needs to be placed in a container to stack — same with fish
    /// or other items."
    ///
    /// <para><b>What actually changes, stated plainly.</b> Before this, every catch source did
    /// <c>hold.TryAdd(item)</c> and the fish teleported from the water into an abstract container. Now the
    /// on-foot sources offer it to the HANDS first: you are holding a clam, and it is not "in your
    /// inventory" until you put it somewhere that stacks. The hold-direct path is untouched and remains
    /// the default for everything at sea — see the remarks on <see cref="TryPutInHand"/>.</para>
    ///
    /// <para><b>Why a second interface rather than a member on <see cref="ICarrier"/>.</b> They are read
    /// and write. <see cref="ICarrier"/> answers "what is in your hands" and is asked by GATES, dozens of
    /// times a second, by lanes that must never be able to change anything. This one PUTS something there
    /// and is called at the instant a catch lands. Keeping them apart means a gate cannot accidentally
    /// acquire the power to fill your hands, which is a property worth more than the one saved
    /// interface.</para>
    /// </summary>
    public interface ICatchHands
    {
        /// <summary>
        /// Put a landed catch in the fisher's hands. Returns false when it could not go there — hands
        /// already full of something that is not a tool — and the caller must then fall back to its own
        /// hold rather than dropping the catch on the floor.
        ///
        /// <para><b>The tool it displaces is not lost.</b> An implementor holding a TOOL when a catch
        /// lands slings it (you tuck the shovel under your arm to lift the clam out) and takes it back the
        /// moment the hands are free again. That is the owner's stated default — "the rod auto-stows to
        /// slung on a landed catch, fish in hand" — and it is what makes the loop work at all: digging
        /// requires the shovel IN HAND, so without a sling the very first clam would have nowhere to
        /// go.</para>
        /// </summary>
        bool TryPutInHand(in CatchItem item);
    }

    /// <summary>
    /// <b>A catch has just been landed and is in the fisher's hands</b> — the moment of the fish, as
    /// distinct from the moment it gets stowed.
    ///
    /// <para><b>Why this is not <see cref="FishCaught"/>, and why that one did not move.</b>
    /// <c>FishCaught</c> has always meant "a catch entered a hold" — it is published only on a successful
    /// <c>TryAdd</c>, and its consumers act on that: the hold-fill renderers re-read the container, the
    /// onboarding director ticks off "you have clams", the deck presenters re-stack their totes. Moving it
    /// to the hand would fire all of them over a container that had not changed. So <c>FishCaught</c>
    /// stays exactly where it is, on the stack, and this new event carries the hand moment for anything
    /// that wants it (a sound, a line of copy, a future close-up).</para>
    ///
    /// <para>Nothing is required to consume it. It exists so the two moments are separable rather than
    /// conflated, which is the thing that would have been expensive to unpick later.</para>
    /// </summary>
    public readonly struct CatchLanded
    {
        /// <summary>What is now in the fisher's hands.</summary>
        public readonly CatchItem Item;

        public CatchLanded(in CatchItem item) => Item = item;
    }
}
