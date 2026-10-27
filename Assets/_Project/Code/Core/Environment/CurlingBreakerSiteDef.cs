using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>A permitted place for presentation; the surf still decides whether it emits.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Curling Breaker Site", fileName = "BreakerSite")]
    public sealed class CurlingBreakerSiteDef : ScriptableObject
    {
        public string Id = "";
        public string RegionId = "";
        public Vector2 Centre;
        [Range(0f, 6f)] public float LengthMeters;
        public bool Enabled;

        public bool IsValid(in CurlingBreakerSettings settings) => !string.IsNullOrWhiteSpace(Id)
            && Id.StartsWith("breaker_site.", System.StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(RegionId) && !float.IsNaN(Centre.x) && !float.IsNaN(Centre.y)
            && !float.IsInfinity(Centre.x) && !float.IsInfinity(Centre.y)
            && LengthMeters >= 2f * settings.EndTaperMeters && LengthMeters <= 6f;
    }
}
