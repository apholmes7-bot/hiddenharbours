namespace HiddenHarbours.UI
{
    /// <summary>
    /// Every user-facing string the wardrobe picker shows, in one place — the same seam
    /// <see cref="HudStrings"/> and <c>ShellStrings</c> hold open. There is still no runtime localization
    /// system wired (the M1 DoD's known gap, a lead-architect call); when one lands these accessors route
    /// to the tables and no call site changes.
    ///
    /// <para>Kept allocation-free: interned literals, never built strings.</para>
    /// </summary>
    public static class WardrobeStrings
    {
        /// <summary>The panel's heading. Lower-case and plain, in the game's own voice — this is a
        /// cupboard in a cottage, not a character-creation screen.</summary>
        public const string Title = "What to wear";

        /// <summary>
        /// The controls line under the list. Both ways out are named, because the picker owns the move
        /// axis while it is open (<c>MoveActionClaim</c>) — so a player who expected to walk away has to
        /// be told what to press instead, and cannot discover it by trying.
        /// </summary>
        public const string Hint = "W/S choose   ·   E wear   ·   Esc leave it";

        /// <summary>Marks the row the player is already wearing, so the list says which one is yours
        /// without relying on the cursor being parked there (the cursor moves; this does not). A word-free
        /// glyph beside the label rather than a colour — the accessibility rule the HUD follows, applied
        /// to a list whose whole subject is colour.</summary>
        public const string WornMark = "•";

        /// <summary>What the cursor draws beside the row it is on.</summary>
        public const string Cursor = "▸";

        /// <summary>
        /// Shown instead of the picker when the wardrobe holds nothing — the honest refusal
        /// <c>InteriorWardrobe</c> used to give unconditionally, kept for the one case where it is still
        /// true. A shipped build cannot reach this (the fisher's wardrobe holds eight), so it is really a
        /// message about content: a wardrobe asset that failed to load, or one authored empty.
        /// </summary>
        public const string NothingToWear = "Only what you stand up in, for now.";
    }
}
