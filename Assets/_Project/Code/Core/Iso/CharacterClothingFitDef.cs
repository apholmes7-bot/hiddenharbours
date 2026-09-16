using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One explicit fit contract per asset. Matching IDs authorize looking up a fitted part;
    /// the baker must also check stable bone IDs, bind transforms, weights and source digests.
    /// Equal bone counts are not evidence that two characters can share clothing.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Characters/Clothing Fit", fileName = "CharacterClothingFit")]
    public sealed class CharacterClothingFitDef : ScriptableObject
    {
        public CharacterClothingFitSpec Spec = new CharacterClothingFitSpec();
    }
}
