using System;

namespace HiddenHarbours.Fishing
{
    /// <summary>Unwired, data-only session tuning. No defaults or authored content identities.</summary>
    [Serializable]
    public struct TrawlSessionDef
    {
        public string Id;
        public double DraughtMetres, MinKeelClearanceMetres, WarpDepthRatio, DoorSpreadMetres;
        public double SnagHazardPerHardMetre, ShallowChafePerMetre, CarryGameSecondsPerIndividual;
        public double InitialIntegrity01;
        public double[] GradeWeightBreaksKg;

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Id) || !TrawlNumbers.NonNegative(DraughtMetres)
                    || !TrawlNumbers.Positive(MinKeelClearanceMetres) || !TrawlNumbers.Positive(WarpDepthRatio)
                    || !TrawlNumbers.Positive(DoorSpreadMetres) || !TrawlNumbers.NonNegative(SnagHazardPerHardMetre)
                    || !TrawlNumbers.NonNegative(ShallowChafePerMetre)
                    || !TrawlNumbers.Positive(CarryGameSecondsPerIndividual)
                    || !TrawlNumbers.Positive(InitialIntegrity01) || InitialIntegrity01 > 1d
                    || GradeWeightBreaksKg == null) return false;
                for (int i = 0; i < GradeWeightBreaksKg.Length; i++)
                    if (!TrawlNumbers.Positive(GradeWeightBreaksKg[i])
                        || (i > 0 && GradeWeightBreaksKg[i] <= GradeWeightBreaksKg[i - 1])) return false;
                return true;
            }
        }
    }
}
