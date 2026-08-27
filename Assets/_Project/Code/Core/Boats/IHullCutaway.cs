namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A hull renderer that can be CUT OPEN</b> — the owner's 2026-08-26 ruling, as a seam.
    ///
    /// <para><b>The ruled look, verbatim in intent:</b> in a boat interior the view shows the boat
    /// EXTERIOR with a wall/roof CUTAWAY revealing the interior. At the helm → exterior only. Player
    /// out on deck → exterior only. That is not the sprite-overdraw interim (a room drawn ON TOP of
    /// the hull); it is the hull's own house faces going away while the space inside her stays inside
    /// her silhouette.</para>
    ///
    /// <para><b>A SECOND interface, not a widening of <see cref="IHullMeshRenderer"/>.</b> Ten test
    /// doubles implement that one, and adding a member to it makes every one of them stop compiling —
    /// in files nobody touched, which this project has already paid for once (a Core interface gained
    /// a member, a double in an unedited file silently stopped implementing it, and the whole batch
    /// editor refused to start with "Scripts have compiler errors" and no test results at all). The
    /// deck-occupant slots took the same shape for the same reason: ask for the capability with
    /// <c>as IHullCutaway</c>, and a renderer that has not got it says so by being null.</para>
    ///
    /// <para><b>Who drives it.</b> The Boats lane, from <c>CabinSignals</c> plus helm occupancy —
    /// Boats owns the cabin, the hull identity and the <see cref="HullMeshDef"/> that carries the
    /// level table, and it may not name the Art type that implements this. See
    /// <c>BoatCutaway</c>.</para>
    /// </summary>
    public interface IHullCutaway
    {
        /// <summary>
        /// True when this renderer's mesh actually carries per-face level tags. False on every hull
        /// baked before the cutaway kit — and on a hull whose rig gained tags but whose mesh has not
        /// been re-baked, which is the state a stale asset leaves behind and the one worth being able
        /// to ask about rather than infer.
        /// </summary>
        bool CarriesLevelTags { get; }

        /// <summary>
        /// The level currently cut away — 0 for "none, draw her whole exterior", which is both the
        /// shipped picture and the state every hull starts in.
        /// </summary>
        int CutawayLevel { get; }

        /// <summary>
        /// Cut this hull open at <paramref name="levelTag"/> (a TexCoord1.x tag from her own
        /// <see cref="HullMeshDef.LevelTags"/>), or 0 to close her up again.
        ///
        /// <para>Idempotent, and cheap enough to call on a transition without checking first. Asking a
        /// hull with no tags for a cut is not an error — she simply cannot be cut, and stays whole.</para>
        /// </summary>
        void ShowCutawayLevel(int levelTag);
    }
}
