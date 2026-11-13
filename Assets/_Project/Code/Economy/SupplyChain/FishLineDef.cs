using UnityEngine;

namespace HiddenHarbours.Economy.SupplyChain
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish chain/Line")]
    public sealed class FishLineDef : ScriptableObject
    {
        public FishLineSpec Spec = new FishLineSpec();
    }
}
