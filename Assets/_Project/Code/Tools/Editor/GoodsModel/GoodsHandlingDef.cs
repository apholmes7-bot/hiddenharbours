using UnityEngine;

namespace HiddenHarbours.Tools.GoodsModel
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Prototypes/Goods handling")]
    public sealed class GoodsHandlingDef : ScriptableObject
    {
        public string Id;
        public GoodsHandLimits Limits = new GoodsHandLimits();
    }
}
