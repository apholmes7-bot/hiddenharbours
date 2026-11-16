using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>What one person thinks of a gift</b> (gifting slice 1, owner ask 2026-11-15), authored INLINE on
    /// their <see cref="NpcDef"/> (<see cref="NpcDef.Gifts"/>) so a villager's tastes sit beside their name
    /// and are reviewed with them. All of it is data (rule 2): item ids (<c>fish.atlantic_cod</c>) and item
    /// tags (<c>shellfish</c>, <c>catch</c>; see <see cref="GiftItem"/> for where a tag comes from).
    ///
    /// <para><b>Leave it all empty and the person takes any gift politely</b> (<see cref="Core.GiftReaction.Neutral"/>)
    /// and answers with the shared lines on <see cref="GiftConfig"/>. That is every villager authored before
    /// this existed, and it is a good answer, not a gap.</para>
    ///
    /// <para><b>An id beats a tag</b> (<see cref="GiftRule.Preference"/>): a person who likes shellfish but
    /// cannot abide green crab says so by listing the tag as liked and the id as disliked. The content
    /// validation test refuses an id or a tag listed twice for one person.</para>
    /// </summary>
    [Serializable]
    public sealed class NpcGiftPreferences
    {
        [Tooltip("Item ids this person LOVES (fish.atlantic_cod). Checked before any tag.")]
        public string[] LovedItemIds = new string[0];

        [Tooltip("Item ids this person likes.")]
        public string[] LikedItemIds = new string[0];

        [Tooltip("Item ids this person dislikes: they take it out of manners.")]
        public string[] DislikedItemIds = new string[0];

        [Tooltip("Item tags this person loves (shellfish, pelagic, catch ...). Only when no id matched.")]
        public string[] LovedTags = new string[0];

        [Tooltip("Item tags this person likes.")]
        public string[] LikedTags = new string[0];

        [Tooltip("Item tags this person dislikes.")]
        public string[] DislikedTags = new string[0];

        [Tooltip("This person's OWN words for each reaction. Any empty list falls back to the shared " +
                 "lines on GiftConfig, so override only the reactions that are theirs to say.")]
        public GiftLineSet Lines = new GiftLineSet();

        /// <summary>True when nothing is authored at all (no tastes and no lines of their own).</summary>
        public bool IsEmpty =>
            Empty(LovedItemIds) && Empty(LikedItemIds) && Empty(DislikedItemIds)
            && Empty(LovedTags) && Empty(LikedTags) && Empty(DislikedTags)
            && (Lines == null || Lines.IsEmpty);

        private static bool Empty(string[] a) => a == null || a.Length == 0;
    }

    /// <summary>
    /// <b>The spoken answer to a gift, by reaction</b> — one list of lines per <see cref="Core.GiftReaction"/>.
    /// Used twice: the shared, generic set on <see cref="GiftConfig"/>, and a person's own overrides on
    /// <see cref="NpcGiftPreferences.Lines"/>. One line is said per gift, picked by the game day
    /// (<see cref="GiftRule.PickLine"/>), so the same person does not answer the same way every day.
    ///
    /// <para>A line may carry <c>{item}</c>, replaced with the item's display name ("Atlantic Cod").</para>
    /// </summary>
    [Serializable]
    public sealed class GiftLineSet
    {
        [Tooltip("Said when the gift is one they love.")]
        public string[] Loved = new string[0];

        [Tooltip("Said when the gift is one they like.")]
        public string[] Liked = new string[0];

        [Tooltip("Said when the gift is taken politely.")]
        public string[] Neutral = new string[0];

        [Tooltip("Said when the gift is one they dislike (they still take it).")]
        public string[] Disliked = new string[0];

        [Tooltip("Said when the thing cannot be given at all. The item stays in the hand.")]
        public string[] NotGiftable = new string[0];

        [Tooltip("Said when they have already had their gift today. The item stays in the hand.")]
        public string[] AlreadyGiftedToday = new string[0];

        /// <summary>The list for one reaction (possibly null or empty).</summary>
        public string[] For(Core.GiftReaction reaction) => reaction switch
        {
            Core.GiftReaction.Loved => Loved,
            Core.GiftReaction.Liked => Liked,
            Core.GiftReaction.Disliked => Disliked,
            Core.GiftReaction.NotGiftable => NotGiftable,
            Core.GiftReaction.AlreadyGiftedToday => AlreadyGiftedToday,
            _ => Neutral,
        };

        /// <summary>True when no reaction has a line.</summary>
        public bool IsEmpty =>
            Empty(Loved) && Empty(Liked) && Empty(Neutral) && Empty(Disliked)
            && Empty(NotGiftable) && Empty(AlreadyGiftedToday);

        private static bool Empty(string[] a) => a == null || a.Length == 0;
    }
}
