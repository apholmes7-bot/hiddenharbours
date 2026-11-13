using UnityEngine;

namespace HiddenHarbours.Economy.SupplyChain
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish chain/Truck run")]
    public sealed class FishTruckRunDef : ScriptableObject
    {
        public FishTruckRunSpec Spec = new FishTruckRunSpec();
    }
}
