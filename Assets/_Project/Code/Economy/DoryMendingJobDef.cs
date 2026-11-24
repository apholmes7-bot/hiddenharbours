using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Economy
{
    [CreateAssetMenu(menuName = "Mending/Job", fileName = "MendingJob")]
    public sealed class DoryMendingJobDef : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public int Order;
        public string CompletedFlag;
        public MendingMaterial[] Materials;
        [Min(0.01f)] public float WorkGameMinutes;
        public bool NeedsLowWater = true;
    }
}
