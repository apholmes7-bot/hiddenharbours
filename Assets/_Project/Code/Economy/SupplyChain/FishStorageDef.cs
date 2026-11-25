using UnityEngine;

namespace HiddenHarbours.Economy.SupplyChain
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish chain/Storage")]
    public sealed class FishStorageDef : ScriptableObject
    {
        public FishStorageSpec Spec = new FishStorageSpec();
    }
}
