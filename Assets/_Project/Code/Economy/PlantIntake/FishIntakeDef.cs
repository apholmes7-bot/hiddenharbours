using System;
using UnityEngine;

namespace HiddenHarbours.Economy.PlantIntake
{
    /// <summary>Uninstalled intake proposal. No inventory search, wallet credit or world registration.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish plant/Intake")]
    public sealed class FishIntakeDef : ScriptableObject
    {
        public string Id;
        public int MaximumDeliveryGrams;
        public string[] AcceptedSpeciesIds;
        public FishGradeDef[] Grades;

        public FishIntakeSpec BuildSpec()
        {
            FishPlantUnits.Require(Grades != null, "grade references");
            var grades = new FishGradeSpec[Grades.Length];
            for (int i = 0; i < grades.Length; i++)
            {
                if (Grades[i] == null) throw new ArgumentException("Unresolved fish grade reference.");
                grades[i] = Grades[i].BuildSpec();
            }
            return new FishIntakeSpec(Id, MaximumDeliveryGrams, AcceptedSpeciesIds, grades);
        }
    }
}
