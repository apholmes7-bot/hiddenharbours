using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// Every authored <see cref="ShoreSpotDef"/>, loaded from Resources at boot by
    /// <see cref="ShoreCatchRegistrar"/> (the <see cref="FishSpeciesLibrary"/> pattern). Append-only; a null
    /// entry is skipped and the first spot with an id wins.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Shore Spot Library", fileName = "ShoreSpotLibrary")]
    public sealed class ShoreSpotLibrary : ScriptableObject
    {
        [Tooltip("Every shore spot a villager angler may name. Append-only.")]
        public ShoreSpotDef[] Spots = Array.Empty<ShoreSpotDef>();

        /// <summary>The runtime facts over these spots, resolving species through the registry.</summary>
        public ShoreCatchFacts BuildFacts() => new ShoreCatchFacts(Spots, FishSpeciesRegistry.Get);
    }
}
