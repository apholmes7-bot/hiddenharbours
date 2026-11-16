namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Can this item be given away?</b> — asked by item id, so the world layer (which gives) never names
    /// the module whose Def owns the answer (rule 4). Published by the Fishing module's species registry
    /// beside <see cref="IFishSizeFacts"/>, off <c>FishSpeciesDef.Giftable</c>; a later item family (food in
    /// the hand, produce) answers through the same seam.
    ///
    /// <para><b>Null is legal</b> (<see cref="GameServices.GiftableFacts"/> unset: a bare scene, an EditMode
    /// rig): the giver then falls back to its own default for the kind of thing held — a loose catch is
    /// giftable.</para>
    /// FLAG lead-architect: new Core contract (2026-11-15, NPC gifting).
    /// </summary>
    public interface IGiftableFacts
    {
        /// <summary>True when the id is known; <paramref name="giftable"/> is then its authored flag.
        /// False (and <paramref name="giftable"/> false) for an unknown id: the caller decides.</summary>
        bool TryIsGiftable(string itemId, out bool giftable);
    }
}
