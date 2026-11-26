using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>Identity of an existing catalog seller, separate from its wares and future stock rules.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Seller", fileName = "Seller")]
    public sealed class SellerDef : ScriptableObject
    {
        public string Id = "";
        [Tooltip("RegionDef.Id of the seller's counter or planned premises; classification only.")]
        public string RegionId = "";
        [Tooltip("NpcDef.Id of the keeper. Empty until authored; never infer a person from a shop name.")]
        public string KeeperNpcId = "";
        [Tooltip("Stable trade/kind used to compare custom between merchants of the same trade.")]
        public string TradeId = "";

        public TradeCounterparty Counterparty => new TradeCounterparty(KeeperNpcId, Id, TradeId);
    }
}
