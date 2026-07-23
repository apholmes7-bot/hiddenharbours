namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// Fishing gear. A flags enum so a species can be catchable by several methods, and a boat can
    /// carry several. The player fishes with one selected gear at a time. (design/fish-and-content.md)
    /// </summary>
    [System.Flags]
    public enum Gear
    {
        None     = 0,
        Handline = 1 << 0,
        Longline = 1 << 1,
        Net      = 1 << 2,
        Trap     = 1 << 3,
        Dredge   = 1 << 4,
        Jig      = 1 << 5,

        /// <summary>
        /// Hand-dig with a clam shovel/fork on the bared flats (St Peters opening). The dedicated tag
        /// economy flagged for review (fish-and-content §3.5a): the soft-shell clam was authored on
        /// <see cref="Handline"/> as a STOPGAP so it had *some* gear, but a clam isn't hooked on a line —
        /// it's dug by hand. This is the additive, append-only reconciliation (a new flag bit; no existing
        /// value renumbered, so no other asset shifts). The clam is re-tagged to this; it keeps the clam
        /// OUT of the rod's resolver pool (the rod fishes Handline/Jig), which is correct — you can't reel
        /// up a clam. The on-foot dig (<c>ClamDig</c>) is what yields it. FLAG: append-only enum change,
        /// re-tagged Data/Fish/SoftShellClam.asset off the Handline stopgap.
        /// </summary>
        ClamFork = 1 << 6
    }

    /// <summary>Which seasons a species bites in. Flags so a fish can span several.</summary>
    [System.Flags]
    public enum SeasonMask
    {
        None        = 0,
        EarlySpring = 1 << 0,
        HighSummer  = 1 << 1,
        TheTurn     = 1 << 2,
        HardWinter  = 1 << 3,
        AllYear     = EarlySpring | HighSummer | TheTurn | HardWinter
    }

    /// <summary>
    /// A lure / artificial-attractant tag — Rod Fishing v2's gear-side "what's tied on", DISTINCT from
    /// bait (design/rod-fishing-v2-brainstorm.md §6.2). Bait is a consumable the fish eats; a lure is the
    /// presentation the rod <i>works</i>. Adding a new gear/bait/<b>lure</b> tag is the one catch input that
    /// touches code and is <b>review-gated</b> (fish-and-content.md §6.1) — so it is authored here in the
    /// Fishing enum set (lead-architect-reviewed), never invented by a content asset.
    ///
    /// <para><b>Flags</b>, mirroring <see cref="Gear"/>: a species can be drawn by several lure types (a
    /// favored-lure mask) while the live catch context carries the ONE lure currently tied on and
    /// AND-tests it against that mask — exactly how <see cref="Gear"/> is a mask on the species yet a single
    /// selected value in <see cref="CatchContext"/>.</para>
    ///
    /// <para><b>Append-only.</b> Add members only at the END; never renumber a bit (assets/masks serialize
    /// the integer).</para>
    ///
    /// <para><b>WIRING SEAM — deliberately NOT wired in this contract PR (non-breaking, deferred).</b> This
    /// change defines the vocabulary only; it does not touch <see cref="CatchResolver"/> / the
    /// <see cref="CatchContext"/> struct and does not re-balance any roll (a resolver change owned by
    /// gameplay-systems / economy-sim). When wired, the intended seam is: an optional <c>LureTag Lure</c> on
    /// the catch context (default <see cref="None"/>, added via an additive constructor overload so no caller
    /// breaks) + an optional <c>FavoredLures</c> mask on <see cref="FishSpeciesDef"/>, applied as a soft
    /// WEIGHT (like bait's "Preferred" mode), never a hard filter. Until then a species' catchability is
    /// unchanged.</para>
    /// </summary>
    /// <summary>
    /// The fishing depth zones a species lives in — the canon <c>depthBand</c> flags of
    /// fish-and-content.md §3.1 (<c>Tidepool, Shallows, Inshore, Midwater, Deep, Abyssal</c>), wired as
    /// Rod Fishing v2's <b>species-targeting depth weight</b> (brainstorm §2.3/§6.1): the player HOLDS the
    /// weighted rig at a column depth, that depth maps to one of these zones
    /// (<c>DepthDropMath.ZoneForDepth</c>, thresholds in <c>GameConfig.DepthDrop</c>), and a species whose
    /// mask includes the zone is weighted UP in the catch roll (outside it, damped — never zeroed: a soft
    /// WEIGHT alongside bait/season/weather/time, not a filter).
    ///
    /// <para><b>Named <c>FishDepthBand</c>, not the canon's bare <c>DepthBand</c>,</b> because Core already
    /// owns that name for the on-foot wading read (<see cref="HiddenHarbours.Core.DepthBand"/> —
    /// Dry/Wade/Swim/Deep), and files in this module use both namespaces. Same concept family, different
    /// axis: that one is where the PLAYER stands, this one is where the FISH lives.</para>
    ///
    /// <para><b>Flags, append-only</b> (the <see cref="Gear"/>/<see cref="LureTag"/> discipline): a species
    /// may span several zones; never renumber a bit. <see cref="None"/> = not yet authored → the species is
    /// depth-NEUTRAL (weight ×1 at every depth), so every existing asset behaves exactly as before this
    /// field existed.</para>
    /// </summary>
    [System.Flags]
    public enum FishDepthBand
    {
        None     = 0,
        Tidepool = 1 << 0, // the shore scraps — barely-covered rock and pool
        Shallows = 1 << 1, // just off the beach
        Inshore  = 1 << 2, // the working cove water
        Midwater = 1 << 3, // the open column — stop the drop mid-fall to fish it
        Deep     = 1 << 4, // over the drop-off
        Abyssal  = 1 << 5  // the far, black water
    }

    /// <summary>
    /// Species behaviour flags — the canon <c>behaviorFlags</c> vocabulary of fish-and-content.md §3.1.
    /// Only <see cref="Bottom"/> is consumed today (Rod Fishing v2's bottom-fishing weight: a Bottom
    /// species is boosted while the rig is held just off the floor — brainstorm §2.3); the rest of the
    /// canon list (<c>Legendary, FightsHard, FogOnly, …</c>) is APPENDED here as later systems wire it,
    /// never invented by a content asset (the review-gated tag rule, fish-and-content.md §6.1).
    ///
    /// <para><b>Flags, append-only:</b> never renumber a bit (assets serialize the integer).</para>
    /// </summary>
    [System.Flags]
    public enum FishFlags
    {
        None   = 0,
        /// <summary>A floor-dweller (cod, halibut, monkfish): weighted UP when the rig is held inside the
        /// off-floor sweet window (<c>GameConfig.DepthDrop.BottomSweetWindowMeters</c>). Sitting ON the
        /// floor doesn't count — the bottom-out-then-lift is the skill beat.</summary>
        Bottom = 1 << 0
    }

    [System.Flags]
    public enum LureTag
    {
        None     = 0,
        Spoon    = 1 << 0, // wobbling metal spoon — flash & vibration on the retrieve
        Plug     = 1 << 1, // swimming plug / crankbait — a diving hard body
        SoftBait = 1 << 2, // soft-plastic body — slow, lifelike, for finicky fish
        Feather  = 1 << 3, // feathered / fly dressing — light surface & pelagic work
        Spinner  = 1 << 4  // spinning blade — flash that draws a reaction strike
    }
}
