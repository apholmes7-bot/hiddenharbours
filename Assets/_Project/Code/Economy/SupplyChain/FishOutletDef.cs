using UnityEngine;

namespace HiddenHarbours.Economy.SupplyChain
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish chain/Outlet")]
    public sealed class FishOutletDef : ScriptableObject
    {
        public FishOutletSpec Spec = new FishOutletSpec();
    }
}
