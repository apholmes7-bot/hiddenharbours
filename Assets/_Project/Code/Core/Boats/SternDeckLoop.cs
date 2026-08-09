using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which beat of the gear cycle the stern deck is working</b> — the canon M2-33 loop, named once
    /// so the presenter, the clip driver and the tests all say the same word for the same moment.
    ///
    /// <para>The owner's cycle reads: come up on your buoy → gaff the line → work the hauler → trap
    /// aboard on the stern deck → empty → chop/re-bait → stack → toss to re-set. The GAFF is not a stage
    /// of its own: the rig's own known-open list says the gaff is unauthored and beat 1 of the
    /// <c>hauler</c> clip reads as the reach for it, so gaffing IS the first beat of
    /// <see cref="Hauling"/> rather than a pose this enum could name and nothing could draw.</para>
    ///
    /// <para><b>These are presentation stages, not sim state.</b> Nothing here decides WHAT is caught,
    /// what it is worth, or whether a pot may be set — that is the trap controllers' business and stays
    /// exactly where it is (rule 5). A stage only says which picture the deck should be showing.</para>
    /// </summary>
    public enum SternDeckStage
    {
        /// <summary>Nothing doing — no pot worked, no line coming in. The deck draws its gear and its
        /// stacks and nothing else.</summary>
        None = 0,

        /// <summary>Working the hauler: the warp coming in over the rail, paced by the LINE hauled.</summary>
        Hauling = 1,

        /// <summary>The pot lands on the stern deck — the one-shot that puts her down.</summary>
        Aboard = 2,

        /// <summary>Emptying her at the bench: picking, sorting, banding. Paced by the QUANTITY worked
        /// (animals handled), never by a clock.</summary>
        Emptying = 3,

        /// <summary>Chopping bait and re-baiting her at the board. Paced by the QUANTITY worked.</summary>
        Baiting = 4,

        /// <summary>Squared away — she goes onto the stack, the one-shot that lifts her there.</summary>
        Stacking = 5,

        /// <summary>Over the rail she goes, re-set. The one-shot that ends the cycle.</summary>
        Setting = 6,
    }

    /// <summary>
    /// Which SURFACE of a hull a pot is being stacked on. Both are rig-measured capacities the caller
    /// supplies (<c>TrapIso.CAPS</c>); neither number appears here.
    /// </summary>
    public enum DeckStackSurface
    {
        /// <summary>Nowhere — every surface this hull offers is full. A real answer, not a failure:
        /// the caller refuses the stack and says so out loud.</summary>
        None = 0,
        /// <summary>The working deck — where pots go first, because that is where the hands are.</summary>
        Deck = 1,
        /// <summary>The washboard — the overflow a working boat actually uses once her deck is full.</summary>
        Washboard = 2,
    }

    /// <summary>
    /// <b>The stern-deck gear cycle, as pure arithmetic</b> — which clip a beat plays, how far along that
    /// clip a QUANTITY has carried it, and where the next pot on a stack goes.
    ///
    /// <para><b>⚠️ NO CONTENT LIVES HERE</b> (rule 2). Not one capacity, tier step, frame count or mount.
    /// Capacities come from <c>TrapIso.CAPS</c> (lobster boat and Cape Islander do not agree, and neither
    /// agrees with a wharf), tier steps and slews from <c>TrapIso.STACK</c> per BUILD, stations from
    /// <c>DeckGear.station()</c>, mounts from each hull's own rig. Every one of them arrives as an
    /// argument. A capacity written into this file would be the same defect as a hard-coded fish price,
    /// and it is the defect that made a single hard-coded deck rectangle wrong on every hull but one.</para>
    ///
    /// <para><b>⚠️ A QUANTITY IS NEVER A CLOCK</b> (the #469 rule). The hauler's hands move with the
    /// LINE and the bench's move with the ANIMALS WORKED, so every driven clip here is positioned from a
    /// count, not from elapsed seconds. <see cref="DrivenFramePosition"/> is the whole of that rule and it
    /// is a function so there is nowhere for a timer to hide. A clip paced by a clock would drift off the
    /// work inside one pot — the rope stops and the hands keep heaving.</para>
    ///
    /// <para>Static, allocation-free and Unity-lifetime-free, so it is EditMode-testable without a scene
    /// and cannot grow a module dependency. It sits in Core beside <see cref="DeckGearPlacement"/> for the
    /// same reason that does: Fishing owns the loop's state and Boats owns the hull, so the arithmetic
    /// they share may live in neither (rule 4).</para>
    /// </summary>
    public static class SternDeckLoop
    {
        /// <summary>
        /// The whole-body clip a beat is drawn with, or <see cref="CharacterClip.None"/> for a beat that
        /// has no authored clip of its own.
        ///
        /// <para><see cref="SternDeckStage.Emptying"/> maps to <c>bench</c> because the rig authors ONE
        /// two-handed bench task and says so — banding, gutting and emptying are the same picture. Do not
        /// reach for <see cref="CharacterClip.Toss"/> for anything but a pot: the rig's known-open list is
        /// explicit that a light object thrown flat is a different arc.</para>
        /// </summary>
        public static CharacterClip ClipFor(SternDeckStage stage) => stage switch
        {
            SternDeckStage.Hauling => CharacterClip.Hauler,
            SternDeckStage.Aboard => CharacterClip.Place,
            SternDeckStage.Emptying => CharacterClip.Bench,
            SternDeckStage.Baiting => CharacterClip.Chop,
            SternDeckStage.Stacking => CharacterClip.Lift,
            SternDeckStage.Setting => CharacterClip.Toss,
            _ => CharacterClip.None,
        };

        /// <summary>
        /// True for the beats whose clip is paced by a QUANTITY and must therefore be driven
        /// (<see cref="CharacterClipPlayer.PlayDriven"/> + <see cref="CharacterClipPlayer.SeekFrame"/>)
        /// rather than played off the clock. False for the one-shots, which have a duration and are
        /// honestly played by <see cref="CharacterClipPlayer.Play"/>.
        ///
        /// <para>The three driven beats are exactly the three LOOPING clips in the kit, which is not a
        /// coincidence: a loop is what a quantity of work looks like.</para>
        /// </summary>
        public static bool IsQuantityPaced(SternDeckStage stage)
            => stage == SternDeckStage.Hauling
            || stage == SternDeckStage.Emptying
            || stage == SternDeckStage.Baiting;

        /// <summary>
        /// <b>How far along a driven clip a quantity of work has carried it, in FRAMES.</b>
        ///
        /// <para><paramref name="quantity01"/> is the work done as a fraction of the whole beat — line
        /// hauled for the hauler, animals worked over animals aboard for the bench. <paramref
        /// name="cyclesPerUnit"/> is how many times round the clip a whole beat's worth of work turns the
        /// hands, which is the number an owner tunes; <paramref name="clipFrameCount"/> is the art's own
        /// frame count, so re-authoring an 8-frame heave as 10 does not quietly change how many heaves a
        /// pot takes.</para>
        ///
        /// <para>Runs PAST the last frame on purpose: a looping clip wraps, and
        /// <see cref="CharacterClipPlayer.SeekFrame"/> is documented to take as many cycles as the
        /// caller's quantity covers. Negative quantities clamp to zero rather than winding the hands
        /// backwards.</para>
        /// </summary>
        public static float DrivenFramePosition(float quantity01, float cyclesPerUnit, int clipFrameCount)
            => Mathf.Max(0f, quantity01) * Mathf.Max(0f, cyclesPerUnit) * Mathf.Max(0, clipFrameCount);

        /// <summary>
        /// The work-done fraction of a counted beat: <paramref name="worked"/> of
        /// <paramref name="total"/>.
        ///
        /// <para>A beat with NOTHING to work is COMPLETE, not zero — an empty pot has been emptied — so
        /// the hands finish their stroke and stand up rather than freezing on frame 0 forever. That is
        /// the one case worth stating: every other ratio is the obvious one.</para>
        /// </summary>
        public static float Worked01(int worked, int total)
        {
            if (total <= 0) return 1f;
            return Mathf.Clamp01(worked / (float)total);
        }

        /// <summary>
        /// <b>Where the next pot goes</b> — the deck while it has room, then the washboard, then nowhere.
        ///
        /// <para>Deck first because that is where the hands are; a working boat only starts piling gear on
        /// her washboards once her deck is full. Both capacities are the HULL's own rig data
        /// (<c>TrapIso.CAPS</c> — a lobster boat and a Cape Islander do not agree), and a capacity of 0 is
        /// a real answer meaning "you cannot stack there" rather than a missing value to fall back from:
        /// a wharf's washboard capacity is exactly that.</para>
        ///
        /// <para><see cref="DeckStackSurface.None"/> is the refusal, and it must be SURFACED by the
        /// caller, never eaten — a pot that silently fails to appear is the same defect as a deck slot
        /// that silently over-claims.</para>
        /// </summary>
        public static DeckStackSurface ChooseStackSurface(int deckCount, int deckCapacity,
                                                          int washboardCount, int washboardCapacity)
        {
            if (deckCount < deckCapacity) return DeckStackSurface.Deck;
            if (washboardCount < washboardCapacity) return DeckStackSurface.Washboard;
            return DeckStackSurface.None;
        }

        /// <summary>
        /// How many pots this hull can hold across every surface she offers. The number a caller checks
        /// before promising the player somewhere to put the next one.
        /// </summary>
        public static int TotalStackCapacity(int deckCapacity, int washboardCapacity)
            => Mathf.Max(0, deckCapacity) + Mathf.Max(0, washboardCapacity);

        /// <summary>
        /// Every occupant one worked deck can be holding at once — the number a caller compares against
        /// <see cref="IDeckOccupantSlots.Capacity"/> before it starts handing out claims it cannot keep.
        ///
        /// <para>The hands are counted too (<paramref name="hands"/>): a fisher standing on the deck holds
        /// a slot of her own, and so does a second hand once one is aboard. Every term is passed in
        /// because every term is somebody else's data.</para>
        /// </summary>
        public static int OccupantsNeeded(int gearPieces, int stackedPots, bool potInPlay, int hands)
            => Mathf.Max(0, gearPieces) + Mathf.Max(0, stackedPots) + (potInPlay ? 1 : 0) + Mathf.Max(0, hands);
    }
}
