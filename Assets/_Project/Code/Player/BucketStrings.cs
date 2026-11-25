namespace HiddenHarbours.Player
{
    /// <summary>
    /// The pail's own player-facing sentences. Same convention as <see cref="ControlStrings"/>: no runtime
    /// localization system is wired yet, so centralising the copy is the seam.
    ///
    /// <para><b>A sentence may only name a buyer that buys.</b> Both pails used to send the player to a
    /// place with no working buyer. The sentence now names the one counter that takes a pail today.</para>
    /// </summary>
    public static class BucketStrings
    {
        /// <summary>The catch in hand would not go in: the pail has no room. The clam dig says the same
        /// sentence when it refuses a dig for a full pail (<c>FishingStrings.PailFull</c> in the Fishing
        /// module); the two modules cannot see each other, so a test holds the two sentences equal.</summary>
        public const string Full = "The bucket's full — Marguerite at LeBlanc's store will buy them.";
    }
}
