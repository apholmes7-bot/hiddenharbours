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
        [Tooltip("Require water strictly below the sequence's work line to start or continue. " +
                 "Keep enabled for wet-slip jobs; disable for a job on a hull lying dry above high water.")]
        public bool NeedsLowWater = true;
    }
}
