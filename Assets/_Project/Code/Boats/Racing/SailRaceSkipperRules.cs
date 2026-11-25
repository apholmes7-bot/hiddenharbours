using System;

namespace HiddenHarbours.Boats
{
    [Serializable]
    public struct SailRaceSkipperRules
    {
        public float HeaderDegrees, LaylineMarginMetres, MinimumTackSeconds;
        public SailRaceSkipperRules(float headerDegrees, float laylineMarginMetres, float minimumTackSeconds)
        {
            RaceGeometry.RequirePositive(headerDegrees, nameof(headerDegrees));
            RaceGeometry.RequireNonNegative(laylineMarginMetres, nameof(laylineMarginMetres));
            RaceGeometry.RequireNonNegative(minimumTackSeconds, nameof(minimumTackSeconds));
            if (headerDegrees >= 90f) throw new ArgumentOutOfRangeException(nameof(headerDegrees));
            HeaderDegrees = headerDegrees; LaylineMarginMetres = laylineMarginMetres; MinimumTackSeconds = minimumTackSeconds;
        }
        internal void Validate() => _ = new SailRaceSkipperRules(HeaderDegrees, LaylineMarginMetres, MinimumTackSeconds);
    }
}
