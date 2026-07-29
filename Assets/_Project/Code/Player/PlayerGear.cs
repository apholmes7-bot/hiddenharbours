using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// Maps the player's <b>owned-gear ids</b> (recorded in the save by Economy's purchase/grant flows)
    /// to on-foot <b>capabilities</b> (St Peters opening): the shovel enables digging, the bucket gives an
    /// on-foot clam hold, the rod enables rod-fishing. The gear's economic side (price, the purchase
    /// record) is Economy's; its <em>capability</em> is gameplay-systems' — this is the single place that
    /// reads the owned-gear list and answers "can the player do X?", so the dig interaction, the rod
    /// enable, and any UI agree.
    ///
    /// <para><b>Seam discipline.</b> Reads only the Core save seam (<see cref="GameServices.Save"/> →
    /// <see cref="SaveData.OwnedGear"/>) — never Economy's concrete shop. The ids are the stable Def ids
    /// ("gear.shovel" / "gear.bucket" / "gear.rod"); they're constants here so a typo can't silently
    /// disable a capability, and they match the authored <c>GearOffer</c> ids (validated in content tests).
    /// A null save (EditMode, pre-boot) reads as "owns nothing" — capabilities are off until granted.</para>
    /// </summary>
    public static class PlayerGear
    {
        /// <summary>Stable gear ids (match the authored GearOffer assets in Data/Gear). Append-only.</summary>
        public const string ShovelId = "gear.shovel";
        public const string BucketId = "gear.bucket";
        public const string RodId    = "gear.rod";
        /// <summary>The watch: the diegetic instrument that grants the time/date readout (the keystone
        /// diegetic-UI proof — diegetic-ui-and-inventory.md §3.3). Unlike the boat-fitted instruments
        /// (sounder/radar/gps/compass), the watch is CARRIED on the player and reads on any boat / ashore.</summary>
        public const string WatchId  = "gear.watch";

        /// <summary>True iff the player owns the gear with this stable id (reads the live save's owned-gear
        /// list). Null save / null-or-empty id → false.</summary>
        public static bool Owns(string gearId) => Owns(GameServices.Save?.Current, gearId);

        /// <summary>Testable overload over an explicit save blob.</summary>
        public static bool Owns(SaveData save, string gearId)
            => save?.OwnedGear != null && !string.IsNullOrEmpty(gearId) && save.OwnedGear.Contains(gearId);

        /// <summary>Can the player dig clams? Requires the shovel.</summary>
        public static bool CanDig(SaveData save) => Owns(save, ShovelId);
        public static bool CanDig() => CanDig(GameServices.Save?.Current);

        /// <summary>Does the player have a clam bucket (the on-foot clam hold)?</summary>
        public static bool HasBucket(SaveData save) => Owns(save, BucketId);
        public static bool HasBucket() => HasBucket(GameServices.Save?.Current);

        /// <summary>Can the player rod-fish? Requires the rod (bought at Nine Mile Creek). NB: landing COD on the
        /// rod additionally needs the cod licence — that's the separate land-time gate in Fishing.</summary>
        public static bool CanRodFish(SaveData save) => Owns(save, RodId);
        public static bool CanRodFish() => CanRodFish(GameServices.Save?.Current);

        /// <summary>Does the player own the watch? (Gates the diegetic clock/date readout — the watch is the
        /// first "true UI" the player earns; before it there is no time on screen, diegetic-ui §3.3.)</summary>
        public static bool HasWatch(SaveData save) => Owns(save, WatchId);
        public static bool HasWatch() => HasWatch(GameServices.Save?.Current);
    }
}
