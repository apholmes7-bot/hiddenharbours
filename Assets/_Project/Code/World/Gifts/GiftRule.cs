using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The gift rule</b> (gifting slice 1, owner ask 2026-11-15): (a person's tastes, the item, how many
    /// gifts they have had today) → how they take it. Pure and deterministic — no clock, no random, no Unity
    /// object — so the whole decision is pinned by EditMode tests and the act that calls it
    /// (<see cref="WorldInteractor"/>) only moves things.
    ///
    /// <para><b>The order, and why.</b>
    /// <list type="number">
    ///   <item>Not giftable → <see cref="GiftReaction.NotGiftable"/>. A thing that cannot be given is
    ///   refused before anything else is asked; the item stays in the hand.</item>
    ///   <item>Today's gift already had → <see cref="GiftReaction.AlreadyGiftedToday"/>. The daily limit is
    ///   data (<see cref="GiftConfig.GiftsPerNpcPerDay"/>; the owner's lean is one); the item stays in the
    ///   hand and they decline kindly.</item>
    ///   <item>Tastes (<see cref="Preference"/>): an ID beats a TAG, then loved, liked, disliked in that
    ///   order (generous on a tie: cozy, P5); nothing matched → <see cref="GiftReaction.Neutral"/>.</item>
    /// </list></para>
    /// </summary>
    public static class GiftRule
    {
        /// <summary>The whole decision. <paramref name="giftsPerDay"/> of zero or less means no daily limit.</summary>
        public static GiftReaction Evaluate(NpcGiftPreferences prefs, in GiftItem item, int giftsToday, int giftsPerDay)
        {
            if (!item.Giftable || string.IsNullOrEmpty(item.Id)) return GiftReaction.NotGiftable;
            if (giftsPerDay > 0 && giftsToday >= giftsPerDay) return GiftReaction.AlreadyGiftedToday;
            return Preference(prefs, in item);
        }

        /// <summary>Tastes only: ids first (loved, liked, disliked), then tags in the same order; else neutral.
        /// Null preferences are neutral.</summary>
        public static GiftReaction Preference(NpcGiftPreferences prefs, in GiftItem item)
        {
            if (prefs == null) return GiftReaction.Neutral;

            if (Contains(prefs.LovedItemIds, item.Id)) return GiftReaction.Loved;
            if (Contains(prefs.LikedItemIds, item.Id)) return GiftReaction.Liked;
            if (Contains(prefs.DislikedItemIds, item.Id)) return GiftReaction.Disliked;

            if (AnyTag(prefs.LovedTags, item.Tags)) return GiftReaction.Loved;
            if (AnyTag(prefs.LikedTags, item.Tags)) return GiftReaction.Liked;
            if (AnyTag(prefs.DislikedTags, item.Tags)) return GiftReaction.Disliked;

            return GiftReaction.Neutral;
        }

        /// <summary>True for the reactions where the person TAKES the item (it leaves the hand, today's gift
        /// is counted, the moment is published). A refusal and a declined-today take nothing.</summary>
        public static bool IsAccepted(GiftReaction reaction) =>
            reaction == GiftReaction.Loved || reaction == GiftReaction.Liked
            || reaction == GiftReaction.Neutral || reaction == GiftReaction.Disliked;

        /// <summary>The relationship event an accepted gift records (player → person). False for a reaction
        /// that took nothing. The points themselves are the owner's, on the relationship config (rule 6).</summary>
        public static bool TryRelationshipKind(GiftReaction reaction, out RelationshipEventKind kind)
        {
            switch (reaction)
            {
                case GiftReaction.Loved: kind = RelationshipEventKind.GiftLoved; return true;
                case GiftReaction.Liked: kind = RelationshipEventKind.GiftLiked; return true;
                case GiftReaction.Neutral: kind = RelationshipEventKind.GiftNeutral; return true;
                case GiftReaction.Disliked: kind = RelationshipEventKind.GiftDisliked; return true;
                default: kind = default; return false;
            }
        }

        /// <summary>
        /// The line the person says: their own list for this reaction when they have one, else the shared
        /// list; one picked by the day (<see cref="PickLine"/>), with <c>{item}</c> filled in. Null when
        /// neither list has a line (the act then says nothing rather than an empty bubble).
        /// </summary>
        public static string Line(GiftReaction reaction, GiftLineSet own, GiftLineSet shared, int dayIndex, string itemName)
        {
            string[] lines = own != null ? own.For(reaction) : null;
            if (lines == null || lines.Length == 0) lines = shared != null ? shared.For(reaction) : null;
            string line = PickLine(lines, dayIndex);
            if (string.IsNullOrEmpty(line)) return null;
            return line.Replace(ItemToken, string.IsNullOrEmpty(itemName) ? FallbackItemName : itemName);
        }

        /// <summary>The token a line may carry for the item's name.</summary>
        public const string ItemToken = "{item}";

        /// <summary>What <see cref="ItemToken"/> becomes for an item with no name.</summary>
        public const string FallbackItemName = "that";

        /// <summary>One line of a list, by the game day: the same day says the same line, the next day the
        /// next one (deterministic, rule 5; never a random). Skips empty entries. Null for no line.</summary>
        public static string PickLine(string[] lines, int dayIndex)
        {
            if (lines == null || lines.Length == 0) return null;
            int n = lines.Length;
            int start = ((dayIndex % n) + n) % n;
            for (int k = 0; k < n; k++)
            {
                string s = lines[(start + k) % n];
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
        }

        private static bool Contains(string[] list, string value)
        {
            if (list == null || string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < list.Length; i++)
                if (string.Equals(list[i], value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool AnyTag(string[] wanted, string[] tags)
        {
            if (wanted == null || tags == null) return false;
            for (int i = 0; i < tags.Length; i++)
                if (Contains(wanted, tags[i])) return true;
            return false;
        }
    }
}
