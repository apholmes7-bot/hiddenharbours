using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>One shelf line of an island store: which back-room good it shows, and in what items.</summary>
    [Serializable]
    public sealed class IslandShelfLine
    {
        /// <summary>Stable line id, <c>line.snake_case</c>, unique in its store. Append-only.</summary>
        public string Id;

        /// <summary>The back-room good the shelf shows. Must be one the store's ledger stocks.</summary>
        public IslandGoodDef Good;

        /// <summary>What one shelf item is, for people (a 20 L jerry can, a bag of ice).</summary>
        public string ItemName;

        /// <summary>How many of the good's units one shelf item holds.</summary>
        [Min(0.001f)] public float UnitsPerItem = 1f;

        /// <summary>The most items the shelf holds; more in the back room stays out of sight.</summary>
        [Min(1)] public int MaxOnShelf = 1;
    }

    /// <summary>
    /// <b>An island store's shelves</b>, as data (living island economy slice 2,
    /// <c>docs/design/st-peters-living-economy.md</c> §5): the shop they belong to, the back-room ledger they
    /// follow, the hours over which the islanders' buying draws them down, and the lines. One asset per
    /// store (<c>islandshelves.st_peters_general</c>). Read by <see cref="IslandStoreShelves"/>; nothing here
    /// is state.
    /// </summary>
    [CreateAssetMenu(menuName = "Economy/Island Store Shelves", fileName = "IslandStoreShelves")]
    public sealed class IslandStoreShelvesDef : ScriptableObject
    {
        /// <summary>Stable id, <c>islandshelves.snake_case</c>. Append-only.</summary>
        public string Id;

        /// <summary>The shop these shelves are in (<c>shop.snake_case</c>, a <see cref="ShopDef"/>'s id).</summary>
        public string ShopId;

        /// <summary>The back room the shelves follow.</summary>
        public IslandLedgerDef Ledger;

        /// <summary>Hour of day the store opens; the islanders' buying starts here.</summary>
        [Range(0f, 24f)] public float OpensHour = 7f;

        /// <summary>Hour of day the store closes; by now the islanders have bought the day's use.</summary>
        [Range(0f, 24f)] public float ClosesHour = 19f;

        /// <summary>The shelf lines, one per good.</summary>
        public List<IslandShelfLine> Lines = new List<IslandShelfLine>();
    }
}
