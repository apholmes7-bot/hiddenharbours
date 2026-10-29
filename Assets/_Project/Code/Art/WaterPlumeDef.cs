using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// A creek or river mouth's own water (ADR 0027 #7, gains 2): a <see cref="WaterTypeDef"/> carried a reach
    /// out from a point and feathered into its region's water. One per file with a stable
    /// <c>water_plume.snake_case</c> id; its region's <see cref="WaterSurface"/> lists it (at most
    /// <see cref="WaterType.MaxPlumes"/>). Presentation only: it moves no current, depth or hull.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Water Plume", fileName = "WaterPlume")]
    public sealed class WaterPlumeDef : ScriptableObject
    {
        public const string IdPrefix = "water_plume.";
        public const float MinReachMeters = 1f;
        public const float MaxReachMeters = 400f;

        [Tooltip("Stable id, water_plume.snake_case. Ids are append-only.")]
        public string Id = "water_plume.example";
        [Tooltip("The region whose water this mouth feeds (region.snake_case).")]
        public string RegionId = "";
        [Tooltip("The mouth, in world XY metres.")]
        public Vector2 Centre;
        [Tooltip("How far the mouth's water reaches. Default 40 m; 1 to 400 m.")]
        [Range(MinReachMeters, MaxReachMeters)] public float ReachMeters = 40f;
        [Tooltip("The soft rim inside the reach where the mouth's water mixes into the region's. " +
                 "Default 16 m; 0 to the reach.")]
        [Min(0f)] public float FeatherMeters = 16f;
        [Tooltip("The water the mouth carries.")]
        public WaterTypeDef Type;

        /// <summary>The shader's plume slot: mouth x, mouth y, reach, feather.</summary>
        public Vector4 Shape => new Vector4(Centre.x, Centre.y, ReachMeters, FeatherMeters);

        /// <summary>A stable id, a region, a finite mouth, a reach and feather inside range, and a valid type.</summary>
        public bool IsValid() => !string.IsNullOrWhiteSpace(Id)
            && Id.StartsWith(IdPrefix, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(RegionId)
            && Finite(Centre.x) && Finite(Centre.y)
            && ReachMeters >= MinReachMeters && ReachMeters <= MaxReachMeters
            && FeatherMeters >= 0f && FeatherMeters <= ReachMeters
            && Type != null && Type.IsValid();

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
