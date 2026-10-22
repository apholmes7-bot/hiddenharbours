namespace HiddenHarbours.Core
{
    /// <summary>
    /// The mesh state for an explicit whole-body clip. The clip player remains the authority
    /// for its frame and heading. Null leaves the move with the sprite; a mapped key still
    /// requires both a baked clip and CharacterSkinDef.MeshStates permission in the presenter.
    /// State names describe baked art, not gameplay tuning. All answers are string literals.
    /// </summary>
    public static class CharacterClipStateMap
    {
        public const string Board = "board";
        public const string BoardDown = "boardDown";
        public const string LadderDown = "ladderDown";
        public const string Haul = "haul";
        public const string Hauler = "hauler";
        public const string Bench = "bench";
        public const string Chop = "chop";
        public const string Lift = "lift";
        public const string Place = "place";
        public const string Toss = "toss";
        public const string BoardBuckets = "board_buckets";
        public const string BoardTray = "board_tray";
        public const string BoardPot = "board_pot";
        public const string BoardDownBuckets = "boardDown_buckets";
        public const string BoardDownTray = "boardDown_tray";
        public const string BoardDownPot = "boardDown_pot";

        /// <summary>
        /// Only the vault has carrying variants in this wave. Empty and other carry keys use
        /// the bare vault; the other mapped moves keep their own two-handed pose. Water,
        /// sleep, drive and reaches remain unmapped. No concatenation, cache or allocation.
        /// </summary>
        public static string StateKeyFor(CharacterClip clip, string carry = null) => clip switch
        {
            CharacterClip.Board => carry switch
            {
                "buckets" => BoardBuckets,
                "tray" => BoardTray,
                "pot" => BoardPot,
                _ => Board,
            },
            CharacterClip.BoardDown => carry switch
            {
                "buckets" => BoardDownBuckets,
                "tray" => BoardDownTray,
                "pot" => BoardDownPot,
                _ => BoardDown,
            },
            CharacterClip.LadderDown => LadderDown,
            CharacterClip.Haul => Haul,
            CharacterClip.Hauler => Hauler,
            CharacterClip.Bench => Bench,
            CharacterClip.Chop => Chop,
            CharacterClip.Lift => Lift,
            CharacterClip.Place => Place,
            CharacterClip.Toss => Toss,
            _ => null,
        };
    }
}
