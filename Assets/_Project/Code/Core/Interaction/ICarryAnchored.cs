namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A carriable that names the SHAPE it is in the hand</b> — the one thing the carried object
    /// contributes to how it is held.
    ///
    /// <para><b>Why a second, optional interface rather than a member on <see cref="ICarriable"/>.</b>
    /// The same reason <see cref="ICarriable"/> and <see cref="IInteractable"/> are separate: a thing can
    /// be carried without having a hand-prop row (a jerry can, a shovel — the rig bakes no row for
    /// either), and adding a member every implementor must answer "none" to buys nothing. A carrier
    /// tests for this with <c>is</c>, and an implementor that does not have it keeps whatever pose the
    /// carrier used before this existed. That is also what makes this change safe to land: the fuel
    /// kit's four container classes and the shovel are untouched and behave identically.</para>
    ///
    /// <para><b>Why the carried thing supplies only a KEY and not the numbers.</b> Every number a carry
    /// pose needs — which wrist, how far off it, which turntable cell, over or under — is a fact about
    /// the CARRIER's arms, baked from the character rig into
    /// <see cref="CarryAnchorTableDef"/>. A clam does not know how long a fisher's arm is, and if it
    /// carried its own copy of that, then a second character with different proportions would hold it
    /// wrong. So the thing held says only "I am a handful of shellfish" and the holder answers the rest.
    /// It is the same division the deck-occupant seam settled: the hull owns the slots, the occupant owns
    /// which slot it wants.</para>
    /// </summary>
    public interface ICarryAnchored
    {
        /// <summary>
        /// The hand-prop row this thing is held by — <c>clam</c>, <c>fish</c>, <c>rodTrail</c>,
        /// <c>rope</c>: a key in <see cref="CarryAnchorTableDef.Props"/>, which is the rig's own
        /// <c>CharacterHands6.PROP_ORDER</c>.
        ///
        /// <para><b>Null or empty is legal and means "no row"</b> — the carrier falls back to its own
        /// single serialized offset, which is exactly what every carriable did before the table existed.
        /// It is also the honest answer for the carried SHOVEL: the rig bakes seven prop rows and none of
        /// them is a spade (<c>NoShovelRow_Exists_TheCarriedShovelIsAnOpenArtAsk</c> pins that), and
        /// borrowing the gaff's row would put a 1.04 m spade on a 1.55 m pole's grip — a tuned constant
        /// re-used on a new lever, which is the mistake this project has paid for more than once.</para>
        /// </summary>
        string HandPropKey { get; }
    }

    /// <summary>
    /// The seven prop keys the character rig bakes rows for, stated once so no call site spells one as a
    /// literal — and so the set a reader can choose from is visible in one place rather than scattered
    /// across implementors.
    ///
    /// <para><b>⚠️ There is no shovel.</b> The rig bakes rodTrail, rodSling, fish, clam, knife, gaff and
    /// rope, and that is the whole list — <c>NoShovelRow_Exists_TheCarriedShovelIsAnOpenArtAsk</c> pins
    /// it. A carried spade keeps the pre-table pose until the art lane bakes a row for it. Do NOT point
    /// it at <see cref="Gaff"/>: that row is a 1.55 m pole's grip and a spade is 1.04 m, and re-using a
    /// tuned constant on a new lever is a mistake this project has already paid for
    /// (<c>overlay-pose-rotates-about-the-origin</c>).</para>
    /// </summary>
    public static class CarryPropKeys
    {
        /// <summary>The rod carried in the hand — rides idle/walk/run with free arms.</summary>
        public const string RodTrail = "rodTrail";

        /// <summary>The rod slung on the BACK, so it rides the clips that need both hands. Imported as
        /// data; nothing poses it yet (see <see cref="CarryAnchorTableDef"/>).</summary>
        public const string RodSling = "rodSling";

        /// <summary>A finfish held by the gill.</summary>
        public const string Fish = "fish";

        /// <summary>A handful of shellfish.</summary>
        public const string Clam = "clam";

        /// <summary>Declared art debt — a spec row with no rig baked.</summary>
        public const string Knife = "knife";

        /// <inheritdoc cref="Knife"/>
        public const string Gaff = "gaff";

        /// <summary>A carried hank of rope — a half-scale deck coil.</summary>
        public const string Rope = "rope";

        /// <summary>
        /// Which shape a landed catch is in the hand. The rig bakes exactly two catch shapes, so this is
        /// a two-way split of a category the content already stamps on every
        /// <see cref="CatchItem"/> — a derivation, not new content, which is why it is a function here
        /// rather than another table the owner has to keep in step with the fish roster.
        ///
        /// <para><b>What it gets approximately right on purpose.</b> A lobster is
        /// <see cref="FishCategory.Shellfish"/> and will be held like a handful of clams, which is not
        /// what a lobster looks like. There is no lobster row to pick instead, and the honest choice
        /// between "the wrong shellfish" and "a fish" is the former. The day the rig bakes a lobster row,
        /// this is the one function that changes.</para>
        /// </summary>
        public static string ForCatch(FishCategory category)
            => category == FishCategory.Shellfish ? Clam : Fish;
    }
}
