using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>One thing that stands outside a store: its sign, or a display put out while it is open.</summary>
    [Serializable]
    public sealed class StoreFrontPiece
    {
        /// <summary>Stable piece id, <c>front.snake_case</c>, unique in its store. Append-only.</summary>
        public string Id;

        /// <summary>The picture, pivoted at its bottom centre (where it stands on the ground).</summary>
        public Sprite Sprite;

        /// <summary>Where it stands, in world metres from the building's ground point (its transform).</summary>
        public Vector2 Offset;

        /// <summary>The draw order among the village props: the village's Y sort (1202 less 4 a metre of the
        /// world height it stands at), written here so nothing reads the Art module.</summary>
        public int SortingOrder;

        /// <summary>Out only while the store is open, and put away when it closes. Off for the sign.</summary>
        public bool OnlyWhileOpen;
    }

    /// <summary>
    /// <b>What stands outside a store</b> (owner 2026-11-25: "add a shop sign and maybe a couple displays when
    /// the store is open, when closed these items should not be visible outside"). One asset per store under
    /// <c>Resources/StoreFronts</c>, read by <see cref="StoreFrontsDirector"/>. Dressing only: nothing is sold
    /// from a display, and nothing here is state or saved. Whether the store is open is worked out from the
    /// clock each check (<see cref="IsOpenAt"/>), so a load or a scene change shows the right front at once.
    /// </summary>
    [CreateAssetMenu(menuName = "World/Store Front", fileName = "StoreFront")]
    public sealed class StoreFrontDef : ScriptableObject
    {
        /// <summary>Where these Defs are loaded from (under a Resources folder).</summary>
        public const string ResourcesFolder = "StoreFronts";

        /// <summary>Stable id, <c>storefront.snake_case</c>. Append-only.</summary>
        public string Id;

        /// <summary>The shop (<c>shop.snake_case</c>) this front belongs to. Its island shelves carry the same
        /// hours (a content test holds the two together).</summary>
        public string ShopId;

        /// <summary>The name of the building's GameObject in its region scene (the one carrying its
        /// <see cref="BuildingInterior"/>).</summary>
        public string BuildingName;

        [Tooltip("Hour of the day (0 to 24) the store opens and the displays go out. The same as the store's " +
                 "island shelves' OpensHour.")]
        [Range(0f, 24f)] public float OpensHour = 7f;

        [Tooltip("Hour of the day the store closes and the displays come in. The same as the store's island " +
                 "shelves' ClosesHour.")]
        [Range(0f, 24f)] public float ClosesHour = 19f;

        [Tooltip("How often, in real seconds, the front looks at the clock. A slow check: the hour moves far " +
                 "slower than this.")]
        [Min(0.1f)] public float CheckRealSeconds = 1f;

        [Tooltip("The material the pieces draw with (the village props' lit sprite material). Optional.")]
        public Material Material;

        /// <summary>The sign and the displays.</summary>
        public List<StoreFrontPiece> Pieces = new List<StoreFrontPiece>();

        /// <summary>True from the opening hour up to, not including, the closing hour.</summary>
        public static bool IsOpenAt(float hourOfDay, float opensHour, float closesHour)
            => hourOfDay >= opensHour && hourOfDay < closesHour;

        /// <summary>True when this store is open at <paramref name="hourOfDay"/>.</summary>
        public bool IsOpenAt(float hourOfDay) => IsOpenAt(hourOfDay, OpensHour, ClosesHour);
    }
}
