using UnityEngine;

namespace HiddenHarbours.Tools.GoodsModel
{
    // Editor-only research content. No runtime module references this assembly or these assets.
    [CreateAssetMenu(menuName = "Hidden Harbours/Prototypes/Goods container")]
    public sealed class GoodsContainerDef : ScriptableObject
    {
        public GoodsContainerSpec Spec = new GoodsContainerSpec();
        public string DimensionSource;
        [TextArea] public string Provenance;
    }
}
