using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>One thing outside a store: its name board or a lamp on its face, or a display put out while it is open.</summary>
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

        /// <summary>Fixed to the building's face (the name board, its lamps): hidden while the player is
        /// inside, when the wall itself is taken away to show the room.</summary>
        public bool HideWhileInside;

        /// <summary>A lamp: it gives light at <see cref="GlowOffset"/> once it is dark, through the village's
        /// one lamp look (the dark gate every village lamp has; nothing here reads the clock for it).</summary>
        public bool Glows;

        /// <summary>Where the light comes from, metres from the piece's foot (the bulb under the shade).</summary>
        public Vector2 GlowOffset;

        /// <summary>The lamp's colour.</summary>
        public Color GlowColor = new Color(1f, 0.84f, 0.58f, 1f);

        /// <summary>How far round the bulb it lights, metres.</summary>
        [Min(0f)] public float GlowRangeMetres = 1.25f;

        /// <summary>How bright, on the village lamp look's own scale (1 = as bright as it).</summary>
        [Min(0f)] public float GlowIntensity = 1f;

        /// <summary>Optional: the piece as its lamps light it after sunset (the name board under its lamps),
        /// drawn over it above the night's darkness the way a lit window pane is, from sunset to sunrise (the
        /// shared sky's hours, <c>WatchFaceState.NightAt</c>). Same size and pivot as <see cref="Sprite"/>.</summary>
        public Sprite NightSprite;
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

        [Tooltip("The draw order of a piece's lit night picture: over the day/night darkness (32760), as a lit " +
                 "window pane is, and just under the lamps' own glow (32767).")]
        public int NightSortingOrder = 32765;

        [Tooltip("The material the pieces draw with (the village props' lit sprite material). Optional.")]
        public Material Material;

        /// <summary>The name board, its lamps and the displays.</summary>
        public List<StoreFrontPiece> Pieces = new List<StoreFrontPiece>();

        /// <summary>True from the opening hour up to, not including, the closing hour.</summary>
        public static bool IsOpenAt(float hourOfDay, float opensHour, float closesHour)
            => hourOfDay >= opensHour && hourOfDay < closesHour;

        /// <summary>True when this store is open at <paramref name="hourOfDay"/>.</summary>
        public bool IsOpenAt(float hourOfDay) => IsOpenAt(hourOfDay, OpensHour, ClosesHour);
    }
}
