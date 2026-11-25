using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>One island's back room</b>, as data: which imported goods the island's store holds, which supply role
    /// brings them over the bar, and how far back the ledger replays to recompute a day. One asset per island
    /// (<c>islandledger.st_peters</c>). Read by <see cref="IslandStoreLedger"/>; nothing here is state.
    ///
    /// <para>The buffer target is the route's <see cref="IslandSupplyConfig.BufferDays"/>, the run cadence is the
    /// runner's <see cref="SupplyRoleDef.CadenceDays"/> and the run's capacity is the runner's carriers, so
    /// each number has one home. See <c>docs/design/st-peters-living-economy.md</c> §4.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Economy/Island Ledger", fileName = "IslandLedger")]
    public sealed class IslandLedgerDef : ScriptableObject
    {
        /// <summary>Stable id, <c>islandledger.snake_case</c>. Append-only.</summary>
        public string Id;

        /// <summary>The island's region (must equal the route's <see cref="IslandSupplyConfig.IslandRegionId"/>).</summary>
        public string IslandRegionId;

        /// <summary>The supply role whose runs fill the back room (the quad runner on St Peters).</summary>
        public SupplyRoleDef Runner;

        /// <summary>The goods the back room holds. Only <see cref="GoodSource.MainlandOverBar"/> goods are
        /// stocked; island-own and by-sea goods are ignored by the ledger.</summary>
        public List<IslandGoodDef> Goods = new List<IslandGoodDef>();

        /// <summary>How many days before the asked day the replay starts from a full back room. Long enough
        /// that a run tops the room right up inside it, after which the start no longer matters; a test
        /// proves doubling it changes nothing on the shipped data.</summary>
        [Min(1)] public int LookbackDays = 28;
    }
}
