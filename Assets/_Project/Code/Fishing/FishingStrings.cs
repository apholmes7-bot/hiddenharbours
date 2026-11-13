namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The home for the player-facing sentences the catch loop says when it has nowhere to put a catch.
    /// Same convention as <c>ControlStrings</c>, <c>HudStrings</c> and <c>WorldStrings</c>: there is no
    /// runtime localization system wired yet, so centralising the copy here is the seam. When loc tables
    /// land these become lookups and no call site changes.
    ///
    /// <para><b>A sentence may only name a buyer that buys.</b> The pail sentence used to send the player
    /// to a place with no working buyer. It now names the one counter that takes a pail today. The hold
    /// sentences name no buyer at all, because nothing buys a boat's hold yet: when a hold buyer exists,
    /// this is the one place to say where it is.</para>
    ///
    /// <para><b>None of these names a key</b>, and every member is an interned literal: no composed
    /// string, no garbage.</para>
    /// </summary>
    public static class FishingStrings
    {
        /// <summary>The dig is refused because the pail has no room. Points at the store counter that
        /// buys a pail. <c>BucketStrings.Full</c> in the Player module says the same sentence at the pail
        /// itself; a test holds the two equal, because the modules cannot see each other.</summary>
        public const string PailFull = "The bucket's full — Marguerite at LeBlanc's store will buy them.";

        /// <summary>The cast is refused because the hold has no room. Said BEFORE a fish is hooked, so
        /// nothing is landed and lost. Names no buyer (see the class note).</summary>
        public const string HoldFull = "The hold's full — there's no room to stow another fish.";

        /// <summary>The rare case the cast gate cannot see: the hold filled between the cast and the
        /// landing, so the fish had nowhere to go. Said so the loss is never silent.</summary>
        public const string LandedNoRoom = "The hold's full — nowhere to stow it, so it went back over the side.";

        /// <summary>Her one excited line at the hook-set when the fish is in the top band of her
        /// species (<c>GameConfig.FishFeel.BigOneThreshold01</c>) — said as an inner-voice thought.</summary>
        public const string BigOneHooked = "Woah — that's a big one!";
    }
}
