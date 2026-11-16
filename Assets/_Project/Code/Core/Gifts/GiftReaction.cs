namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How a person takes a gift</b> (gifting slice 1, owner ask 2026-11-15: "some items can be gifted to
    /// npcs and npcs have preferences"). The answer of the world layer's pure gift rule, carried on
    /// <see cref="GiftGiven"/> so a later system (relationships, a festival, a quest) can read it without
    /// naming the world module (rule 4).
    ///
    /// <para><b>Read diegetically, never shown as a number</b>: the person SAYS how they took it, in a
    /// spoken line; nothing draws hearts or a bar (the relationships ruling of 2026-11-14).</para>
    ///
    /// <para><b>Append-only, explicit values</b>: a save or a data asset may one day store the number.</para>
    /// </summary>
    public enum GiftReaction
    {
        /// <summary>Taken politely: nothing they love, like or dislike. The default for anyone with no
        /// preferences authored.</summary>
        Neutral = 0,

        /// <summary>One of their favourite things.</summary>
        Loved = 1,

        /// <summary>Something they are glad of.</summary>
        Liked = 2,

        /// <summary>Taken, out of manners, but not wanted.</summary>
        Disliked = 3,

        /// <summary>Not a thing that can be given at all (a tool, a quest item). Refused kindly; the item
        /// stays in the hand.</summary>
        NotGiftable = 4,

        /// <summary>They have already had their gift today. Declined kindly; the item stays in the hand.</summary>
        AlreadyGiftedToday = 5,
    }

    /// <summary>
    /// <b>A gift changed hands</b>: the player gave <see cref="ItemId"/> to <see cref="NpcId"/>, who took it
    /// as <see cref="Reaction"/>, on game day <see cref="DayIndex"/>. Published once per ACCEPTED gift (a
    /// refusal or a declined-today publishes nothing: no item moved). Nothing is required to consume it;
    /// the relationship deltas are applied by the giver through <see cref="IRelationshipService"/>, so this
    /// is for anything else that wants the moment (a sound, a quest step, a later reputation layer).
    /// </summary>
    public readonly struct GiftGiven
    {
        public readonly string NpcId;
        public readonly string ItemId;
        public readonly GiftReaction Reaction;
        public readonly int DayIndex;

        public GiftGiven(string npcId, string itemId, GiftReaction reaction, int dayIndex)
        {
            NpcId = npcId;
            ItemId = itemId;
            Reaction = reaction;
            DayIndex = dayIndex;
        }
    }
}
