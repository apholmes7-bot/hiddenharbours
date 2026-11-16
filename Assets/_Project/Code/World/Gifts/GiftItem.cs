using System;
using System.Text;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The thing being offered, as the gift rule sees it</b> (gifting slice 1): its stable id, the name a
    /// line may say, the tags a person's tastes can match, and whether it may be given at all. A value, so
    /// <see cref="GiftRule"/> stays pure and an EditMode test builds one by hand.
    ///
    /// <para><b>Where tags come from.</b> There is no item-tag system yet, so a tag is DERIVED from what the
    /// item already carries, never listed in code: every loose catch carries <see cref="CatchTag"/>, plus
    /// its <see cref="FishCategory"/> in snake case (<c>inshore_groundfish</c>, <c>shellfish</c>,
    /// <c>pelagic</c>, <c>estuary</c> ...). A later item family (food in the hand, produce) adds its own
    /// factory here and its own tags; the rule does not change.</para>
    ///
    /// <para><b>Whether it may be given</b> is the item Def's own flag, asked through Core
    /// (<see cref="IGiftableFacts"/>, published off <c>FishSpeciesDef.Giftable</c>). Unknown to the facts,
    /// or no facts published, a loose catch is giftable: the default the owner asked for (catch and food
    /// giftable; tools and quest items not — and a tool never reaches here, because the gift act only
    /// ever offers the loose catch in the hand).</para>
    /// </summary>
    public readonly struct GiftItem
    {
        /// <summary>The tag every loose catch carries, whatever its species.</summary>
        public const string CatchTag = "catch";

        public readonly string Id;
        public readonly string DisplayName;
        public readonly string[] Tags;
        public readonly bool Giftable;

        public GiftItem(string id, string displayName, string[] tags, bool giftable)
        {
            Id = id;
            DisplayName = displayName;
            Tags = tags ?? Array.Empty<string>();
            Giftable = giftable;
        }

        /// <summary>A loose catch as a gift: its species id and name, the <see cref="CatchTag"/> and its
        /// category tag, and the species' giftable flag (default true; see the type remarks).</summary>
        public static GiftItem FromCatch(in CatchItem item, IGiftableFacts facts)
            => new GiftItem(item.SpeciesId, item.DisplayName,
                            new[] { CatchTag, CategoryTag(item.Category) },
                            IsGiftable(in item, facts));

        /// <summary>May this catch be given? The species' authored flag when the facts know it, else true.
        /// Allocation-free: the offer asks it every frame a giftable target is faced (rule 7).</summary>
        public static bool IsGiftable(in CatchItem item, IGiftableFacts facts)
        {
            if (string.IsNullOrEmpty(item.SpeciesId)) return false;
            if (facts != null && facts.TryIsGiftable(item.SpeciesId, out bool giftable)) return giftable;
            return true;
        }

        /// <summary>A category's tag: its enum name in snake case (<c>InshoreGroundfish</c> →
        /// <c>inshore_groundfish</c>). Derived, so a new category gets a tag with no edit here.</summary>
        public static string CategoryTag(FishCategory category) => SnakeCase(category.ToString());

        /// <summary>PascalCase → snake_case, ASCII. Public for the tests.</summary>
        public static string SnakeCase(string pascal)
        {
            if (string.IsNullOrEmpty(pascal)) return string.Empty;
            var sb = new StringBuilder(pascal.Length + 4);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
