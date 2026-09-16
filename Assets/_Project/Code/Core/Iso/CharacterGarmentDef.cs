using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One authored garment per asset. Spec is the exact JSON record the offline workbench reads
    /// and writes. A valid spec still needs a validated Unity mesh binding before it can enter stock.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Characters/Garment", fileName = "CharacterGarment")]
    public sealed class CharacterGarmentDef : ScriptableObject
    {
        public CharacterGarmentSpec Spec = new CharacterGarmentSpec();
        public Sprite Thumbnail;
    }
}
