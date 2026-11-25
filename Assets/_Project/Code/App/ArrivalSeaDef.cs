using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>The opening's two chart frames and named ways in, authored from the region plans.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Arrival at sea", fileName = "ArrivalSea")]
    public sealed class ArrivalSeaDef : ScriptableObject
    {
        public string Id;
        public RegionDef SeaRegion;
        public RegionDef HomeRegion;
        public string OpeningArrivalKey;
        public string HomeArrivalKey;
        public Vector2 LiePosition;
        public Vector2 ExitTarget;
    }
}
